using System;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.CastleRaid
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(HealthComponent))]
    public sealed class CastleAssaultUnit : MonoBehaviour // NavMesh 기반 성 공격 유닛
    {
        private const float NavigationRecoveryInterval = 0.25f;
        private const float NavigationRefreshSpread = 0.08f;
        private const float TargetAwarenessInterval = 0.45f;
        private const float DefenseThreatAggroSeconds = 3.5f;
        private const float BreachFlowRadius = 1.35f;
        private const int PathCornerBufferSize = 64;

        [SerializeField] private NavMeshAgent agent; // 길찾기·이동 담당
        [SerializeField] private HealthComponent health; // 공용 체력 부품
        [SerializeField] private UnitVisualFeedback visualFeedback; // 피격·사망 연출
        [SerializeField] private MonsterAnimationDriver animationDriver; // 정식 몬스터 동작 재생

        private CastleRaidController controller; // 목표·공격 조율자
        private UnitStatsSnapshot stats; // 이번 실행 능력치
        private CastleTarget target; // 현재 공격 대상
        private int leasedSlotIndex = -1; // 대상 주변 공격 자리 번호
        private float attackCooldown; // 다음 공격 대기
        private MonsterRuntimeAssetSet runtimeAssetSet; // 편성 몬스터 실행 자산
        private bool attackActionRunning; // Marker 기반 공격 재생 중
        private int nextActionSequenceId; // 공격 재생 구분값
        private float deathPresentationDuration = UnitVisualFeedback.DeathPulseDurationSeconds;
        private NavMeshPath navigationPath; // 경로 검사 재사용 버퍼
        private readonly Vector3[] pathCornerBuffer = new Vector3[PathCornerBufferSize];
        private Vector3 requestedNavigationDestination; // 공격 자리 변경 감지값
        private bool hasNavigationDestination; // 같은 경로를 매 프레임 다시 넣지 않음
        private string unitId = string.Empty; // 포탑 표적 등급 판정용 편성 ID
        private Predicate<Vector3> slotPathPredicate; // 슬롯 경로 검사 재사용
        private bool navigationRefreshRequested; // 구조 변경 뒤 분산 갱신 예약
        private bool forceTargetRefresh; // 돌파 단계 변경 때 목표 우선순위 재평가
        private float nextNavigationRefreshAt;
        private float nextRecoveryCheckAt;
        private float nextTargetAwarenessAt;
        private CastleRaidAIProfile aiProfile;
        private CastleRaidSupportDecision supportDecision;
        private float nextSupportDecisionAt;
        private float supportCooldownRemaining;
        private float attackBuffRemaining;
        private float attackDamageMultiplier = 1f;
        private float defenseBuffRemaining;
        private float recentDamagePerSecond;
        private CastleTarget recentThreatAggressor;
        private float recentThreatAggroRemaining;
        private bool hasDefaultNavigationSettings;
        private bool defaultAutoTraverseOffMeshLink;
        private ObstacleAvoidanceType defaultObstacleAvoidanceType;
        private int defaultAvoidancePriority;
        private bool breachTraversalActive;
        private Vector3 breachTraversalDestination;
        private bool breachFlowModeActive;

        public bool IsAlive => health != null && health.IsAlive;
        public CastleTarget Target => target;
        public float DeathPresentationDuration => deathPresentationDuration;
        public string UnitId => unitId;
        public CastleRaidAIProfile AiProfile => aiProfile;
        public float MaxHealth => health == null ? 0f : health.MaxHealth;
        public float CurrentHealth => health == null ? 0f : health.CurrentHealth;
        public float HealthRatio => MaxHealth <= 0f ? 0f : Mathf.Clamp01(CurrentHealth / MaxHealth);
        public float MoveSpeed => Mathf.Max(0.1f, stats.moveSpeed);
        public float EstimatedDamagePerSecond => Mathf.Max(0.1f, stats.damage * attackDamageMultiplier) /
                                                  Mathf.Max(0.1f, stats.attackInterval);
        public float RecentDamagePerSecond => recentDamagePerSecond;
        public float EstimatedTimeToLive => recentDamagePerSecond <= 0.01f
            ? float.PositiveInfinity
            : CurrentHealth / recentDamagePerSecond;
        public bool HasCombatTarget => target != null && target.IsAlive;
        public bool HasAttackBuff => attackBuffRemaining > 0f;
        public bool HasDefenseBuff => defenseBuffRemaining > 0f;
        public CastleTarget RecentThreatAggressor => recentThreatAggroRemaining > 0f &&
                                                      recentThreatAggressor != null &&
                                                      recentThreatAggressor.IsAlive
            ? recentThreatAggressor
            : null;
        public float TurretCollisionRadius => agent == null ? 0.35f : Mathf.Max(0.15f, agent.radius);
        public Vector3 TurretHitPoint => transform.position + Vector3.up * (agent == null ? 0.55f : Mathf.Max(0.4f, agent.height * 0.45f));
        public bool NeedsStrategicDecision => !attackActionRunning &&
                                              (target == null || !target.IsAlive ||
                                               navigationRefreshRequested && Time.time >= nextNavigationRefreshAt ||
                                               Time.time >= nextTargetAwarenessAt ||
                                               Time.time >= nextRecoveryCheckAt);

        public event Action<CastleAssaultUnit> Died;
        public event Action<CastleAssaultUnit, DamageReport> Damaged;

        private void Awake()
        {
            ResolveReferences();
        }

        public void Initialize(BattleUnitSnapshot unit, CastleRaidController raidController)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            Shutdown(); // 풀 재사용 전 이전 연결 정리
            ResolveReferences();
            controller = raidController ?? throw new ArgumentNullException(nameof(raidController));
            unitId = unit.UnitId ?? string.Empty;
            aiProfile = controller.ResolveAIProfile(unitId);
            stats = unit.Stats;
            visualFeedback?.SetTint(unit.VisualTint); // 실제 편성 몬스터 색상 적용
            runtimeAssetSet = unit.RuntimeAssetSet;
            if (runtimeAssetSet != null && (animationDriver == null || !animationDriver.Initialize(runtimeAssetSet)))
            {
                Debug.LogError($"Castle Raid Monster has no valid animation driver. Unit={unit.UnitId}", this);
                runtimeAssetSet = null; // 외형은 유지하고 기존 즉시 공격으로 안전하게 복귀
            }

            attackCooldown = UnityEngine.Random.Range(0f, Mathf.Max(0.05f, stats.attackInterval * 0.4f)); // 동시 공격 분산
            attackActionRunning = false;
            nextActionSequenceId = 0;
            deathPresentationDuration = UnitVisualFeedback.DeathPulseDurationSeconds;
            navigationPath = new NavMeshPath(); // 풀 인스턴스 초기화 뒤 네이티브 경로 버퍼 준비
            hasNavigationDestination = false;
            slotPathPredicate ??= HasCompletePath;
            navigationRefreshRequested = false;
            forceTargetRefresh = false;
            nextNavigationRefreshAt = 0f;
            nextRecoveryCheckAt = Time.time + ResolveNavigationSpread();
            nextTargetAwarenessAt = Time.time + TargetAwarenessInterval + ResolveNavigationSpread();
            supportDecision = default;
            nextSupportDecisionAt = Time.time + ResolveNavigationSpread();
            supportCooldownRemaining = 0f;
            attackBuffRemaining = 0f;
            attackDamageMultiplier = 1f;
            defenseBuffRemaining = 0f;
            recentDamagePerSecond = 0f;
            recentThreatAggressor = null;
            recentThreatAggroRemaining = 0f;
            health.Initialize(stats.maxHealth);
            HideHealthBar(); // 풀 재사용 시 이전 피격 HP바를 숨긴다
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;

            agent.speed = Mathf.Max(0.1f, stats.moveSpeed);
            agent.acceleration = 16f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = Mathf.Max(0.35f, stats.attackRange * 0.75f);
            CaptureDefaultNavigationSettings();
            agent.autoTraverseOffMeshLink = false; // 돌파 링크도 일반 이동 속도로 직접 통과한다
            if (runtimeAssetSet?.BodyProfile != null)
            {
                agent.radius = Mathf.Max(0.1f, runtimeAssetSet.BodyProfile.BodyRadius);
                agent.height = Mathf.Max(agent.radius * 2f, runtimeAssetSet.BodyProfile.BodyHeight);
            }

            agent.isStopped = false;
            animationDriver?.PlayIdle(true);
        }

        public void Tick(float deltaTime)
        {
            Tick(deltaTime, true);
        }

        public void Tick(float deltaTime, bool allowStrategicDecision)
        {
            if (!IsAlive || controller == null)
            {
                return;
            }

            attackCooldown = Mathf.Max(0f, attackCooldown - deltaTime);
            TickRuntimeEffects(deltaTime);
            if (TickBreachTraversal(deltaTime))
            {
                return;
            }

            UpdateBreachFlowMode();
            if (attackActionRunning)
            {
                TickAttackAction(deltaTime);
                return;
            }

            if (aiProfile != null && aiProfile.Pattern == CastleRaidAiPattern.TacticalSupport &&
                TickTacticalSupport(deltaTime))
            {
                return;
            }

            if (target == null || !target.IsAlive)
            {
                if (allowStrategicDecision)
                {
                    RefreshNavigationPath(true); // 무거운 목표 판단은 프레임 예산을 받은 유닛만 수행
                }
            }
            else if (allowStrategicDecision && navigationRefreshRequested && Time.time >= nextNavigationRefreshAt)
            {
                RefreshNavigationPath(forceTargetRefresh);
            }
            else if (allowStrategicDecision && Time.time >= nextTargetAwarenessAt)
            {
                nextTargetAwarenessAt = Time.time + TargetAwarenessInterval + ResolveNavigationSpread();
                RefreshTargetAwareness();
            }
            else if (allowStrategicDecision && Time.time >= nextRecoveryCheckAt)
            {
                nextRecoveryCheckAt = Time.time + NavigationRecoveryInterval + ResolveNavigationSpread();
                if (ShouldRecoverNavigation())
                {
                    RefreshNavigationPath(false);
                }
            }

            if (target == null || !target.IsAlive)
            {
                StopMoving();
                return;
            }

            var destination = ResolveNavigationDestination(); // 대여 자리를 우선
            var distance = PlanarDistance(transform.position, destination);
            var attackDistance = Mathf.Max(0.5f, stats.attackRange);
            if (distance > attackDistance)
            {
                MoveTo(destination);
                return;
            }

            StopMoving();
            FaceTowards(target.transform.position, deltaTime);
            if (attackCooldown <= 0f)
            {
                StartAttack();
            }
        }

        public void ApplyDefenseDamage(float damage, Vector3 hitPoint, CastleTarget aggressor = null)
        {
            if (aggressor != null && aggressor.IsAlive &&
                (aggressor.TargetKind == CastleTargetKind.Defender ||
                 aggressor.TargetKind == CastleTargetKind.Building &&
                 aggressor.TryGetComponent<CastleTurretRuntime>(out _)))
            {
                recentThreatAggressor = aggressor;
                recentThreatAggroRemaining = DefenseThreatAggroSeconds;
                RequestNavigationRefresh(true, 0f); // 실제로 때린 수비대·포탑은 왕궁 경로가 막힌 동안 먼저 반격한다
            }

            health?.ApplyDamage(new DamageRequest(null, damage, hitPoint));
        }

        public bool CanReachTarget(CastleTarget candidate) // 목표 후보를 점유 변경 없이 실제 경로로 확인
        {
            if (candidate == null || agent == null || !agent.isOnNavMesh)
            {
                return false;
            }

            var alreadyInRange = PlanarDistance(transform.position, candidate.transform.position) <=
                                 Mathf.Max(0.5f, stats.attackRange);
            var destination = alreadyInRange ? transform.position : candidate.transform.position;
            if (!alreadyInRange && candidate.AttackSlots != null &&
                candidate.AttackSlots.TryResolveAvailablePosition(
                    this,
                    transform.position,
                    slotPathPredicate,
                    out _,
                    out var slotPosition))
            {
                destination = slotPosition;
            }

            if (candidate.TargetKind != CastleTargetKind.Wall && controller != null &&
                controller.IsTurretLineBlocked(destination, candidate.transform.position, 0.04f))
            {
                return false; // 경로 끝과 대상 사이에 살아 있는 성벽이 있으면 도달한 것으로 보지 않는다
            }

            return alreadyInRange || HasCompletePath(destination);
        }

        public bool TryMeasurePathToTarget(CastleTarget candidate, out float pathDistance)
        {
            pathDistance = float.PositiveInfinity;
            if (candidate == null || agent == null || !agent.isOnNavMesh)
            {
                return false;
            }

            var alreadyInRange = PlanarDistance(transform.position, candidate.transform.position) <=
                                 Mathf.Max(0.5f, stats.attackRange);
            var destination = alreadyInRange ? transform.position : candidate.transform.position;
            if (!alreadyInRange && candidate.AttackSlots != null &&
                candidate.AttackSlots.TryResolveAvailablePosition(
                    this,
                    transform.position,
                    slotPathPredicate,
                    out _,
                    out var slotPosition))
            {
                destination = slotPosition;
            }

            if (candidate.TargetKind != CastleTargetKind.Wall && controller != null &&
                controller.IsTurretLineBlocked(destination, candidate.transform.position, 0.04f))
            {
                return false; // 공격 자리와 대상 사이에 살아 있는 성벽이 있으면 주변 목표로 보지 않는다
            }

            if (alreadyInRange)
            {
                pathDistance = 0f;
                return true;
            }

            return TryMeasurePathToPosition(destination, out pathDistance);
        }

        public bool TryMeasurePathToPosition(Vector3 destination, out float pathDistance)
        {
            pathDistance = float.PositiveInfinity;
            if (agent == null || !agent.isOnNavMesh)
            {
                return false;
            }

            if (navigationPath == null)
            {
                navigationPath = new NavMeshPath();
            }

            if (!agent.CalculatePath(destination, navigationPath) ||
                navigationPath.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            var cornerCount = navigationPath.GetCornersNonAlloc(pathCornerBuffer);
            pathDistance = 0f;
            var previous = agent.nextPosition;
            for (var index = 0; index < cornerCount; index++)
            {
                pathDistance += PlanarDistance(previous, pathCornerBuffer[index]);
                previous = pathCornerBuffer[index];
            }

            return true;
        }

        public bool RefreshNavigationPath() // 길막 오브젝트 파괴 뒤 즉시 새 경로 요청
        {
            return RefreshNavigationPath(false);
        }

        public void RequestNavigationRefresh(bool retarget, float delay)
        {
            var requestedAt = Time.time + Mathf.Max(0f, delay);
            if (!navigationRefreshRequested || requestedAt < nextNavigationRefreshAt)
            {
                nextNavigationRefreshAt = requestedAt;
            }

            navigationRefreshRequested = true;
            forceTargetRefresh |= retarget;
        }

        private bool RefreshNavigationPath(bool retarget)
        {
            if (!IsAlive || controller == null || agent == null || !agent.isOnNavMesh)
            {
                return false;
            }

            navigationRefreshRequested = false;
            forceTargetRefresh = false;
            nextRecoveryCheckAt = Time.time + NavigationRecoveryInterval + ResolveNavigationSpread();
            var desiredTarget = controller.FindPriorityTarget(this, !retarget);
            if (desiredTarget != target)
            {
                SetTarget(desiredTarget);
            }
            else
            {
                TryLeaseTargetSlot(); // Carving 안정화 뒤 같은 목표의 공격 자리를 다시 잡는다
            }

            if (target == null || !target.IsAlive)
            {
                StopMoving();
                return false;
            }

            var destination = ResolveNavigationDestination();
            agent.ResetPath(); // 제거 전 장애물을 사용한 기존 경로 폐기
            hasNavigationDestination = false;
            return MoveTo(destination);
        }

        private void RefreshTargetAwareness()
        {
            if (!IsAlive || controller == null || agent == null || !agent.isOnNavMesh)
            {
                return;
            }

            var desiredTarget = controller.FindPriorityTarget(this, false);
            if (desiredTarget != target)
            {
                SetTarget(desiredTarget); // 왕궁 경로 개통·피격 위협·다음 방어층 변화를 즉시 반영한다
            }
        }

        public void Shutdown()
        {
            ReleaseTarget();
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Died -= HandleDied;
            }
            HideHealthBar();

            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }

            RestoreDefaultNavigationSettings();

            controller = null;
            unitId = string.Empty;
            aiProfile = null;
            supportDecision = default;
            nextSupportDecisionAt = 0f;
            supportCooldownRemaining = 0f;
            attackBuffRemaining = 0f;
            attackDamageMultiplier = 1f;
            defenseBuffRemaining = 0f;
            recentDamagePerSecond = 0f;
            recentThreatAggressor = null;
            recentThreatAggroRemaining = 0f;
            health?.SetIncomingDamageMultiplier(1f);
            runtimeAssetSet = null;
            attackActionRunning = false;
            nextActionSequenceId = 0;
            deathPresentationDuration = UnitVisualFeedback.DeathPulseDurationSeconds;
            hasNavigationDestination = false;
            leasedSlotIndex = -1;
            navigationRefreshRequested = false;
            forceTargetRefresh = false;
            nextNavigationRefreshAt = 0f;
            nextRecoveryCheckAt = 0f;
            nextTargetAwarenessAt = 0f;
            breachTraversalActive = false;
            breachTraversalDestination = default;
            breachFlowModeActive = false;
            animationDriver?.Shutdown();
            Died = null;
            Damaged = null;
        }

        private void SetTarget(CastleTarget nextTarget)
        {
            ReleaseTarget();
            target = nextTarget;
            hasNavigationDestination = false;
            leasedSlotIndex = -1;
            TryLeaseTargetSlot();
        }

        private void TryLeaseTargetSlot()
        {
            if (target == null || leasedSlotIndex >= 0)
            {
                return;
            }

            target.AttackSlots?.TryLeasePosition(
                this,
                transform.position,
                slotPathPredicate,
                out leasedSlotIndex,
                out _); // 실제로 닿는 빈 공격 자리를 확보
        }

        private void ReleaseTarget()
        {
            if (target != null)
            {
                target.AttackSlots?.Release(this);
            }

            target = null;
            leasedSlotIndex = -1;
            hasNavigationDestination = false;
        }

        private Vector3 ResolveNavigationDestination()
        {
            if (target != null && leasedSlotIndex >= 0 && target.AttackSlots != null &&
                target.AttackSlots.TryGetSlotPosition(leasedSlotIndex, out var slotPosition))
            {
                return slotPosition;
            }

            if (controller != null && controller.TryResolveRouteApproach(this, target, out var routeApproach))
            {
                return routeApproach; // 논리 경로가 지정한 첫 장애물의 열린 앞 셀
            }

            return target == null ? transform.position : target.transform.position;
        }

        private bool MoveTo(Vector3 destination)
        {
            if (!agent.isOnNavMesh)
            {
                return false;
            }

            if (hasNavigationDestination &&
                PlanarDistance(requestedNavigationDestination, destination) <= 0.05f &&
                (agent.pathPending || agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete))
            {
                agent.isStopped = false;
                animationDriver?.PlayMove();
                return true;
            }

            hasNavigationDestination = false;
            var resolved = destination;
            if (!CanUseDirectNavigationDestination(resolved) &&
                !(controller != null && controller.TryResolveInnerEntry(this, out resolved) && HasCompletePath(resolved)))
            {
                StopMoving();
                return false;
            }

            agent.isStopped = false;
            if (!agent.SetDestination(resolved))
            {
                StopMoving();
                return false;
            }

            requestedNavigationDestination = destination;
            hasNavigationDestination = true;
            animationDriver?.PlayMove();
            return true;
        }

        private bool TickBreachTraversal(float deltaTime)
        {
            if (agent == null || !agent.isOnNavMesh)
            {
                breachTraversalActive = false;
                SetBreachFlowMode(false);
                return false;
            }

            if (!breachTraversalActive)
            {
                if (!agent.isOnOffMeshLink)
                {
                    return false;
                }

                var linkData = agent.currentOffMeshLinkData;
                if (!linkData.valid)
                {
                    return false;
                }

                breachTraversalDestination = linkData.endPos + Vector3.up * agent.baseOffset;
                breachTraversalActive = true;
                agent.isStopped = false;
                SetBreachFlowMode(true);
            }

            transform.position = CastleBreachLinkMath.MoveAtConstantSpeed(
                transform.position,
                breachTraversalDestination,
                MoveSpeed,
                deltaTime);
            animationDriver?.PlayMove();
            if ((transform.position - breachTraversalDestination).sqrMagnitude > 0.0001f)
            {
                return true;
            }

            agent.CompleteOffMeshLink();
            breachTraversalActive = false;
            nextRecoveryCheckAt = Time.time + NavigationRecoveryInterval + ResolveNavigationSpread();
            return true;
        }

        private void UpdateBreachFlowMode()
        {
            var shouldFlow = breachTraversalActive || agent != null && agent.isOnNavMesh &&
                (agent.isOnOffMeshLink || agent.hasPath && controller != null &&
                 controller.IsNearActiveBreach(transform.position, BreachFlowRadius));
            SetBreachFlowMode(shouldFlow);
        }

        private void SetBreachFlowMode(bool enabled)
        {
            if (agent == null || breachFlowModeActive == enabled)
            {
                return;
            }

            breachFlowModeActive = enabled;
            agent.obstacleAvoidanceType = enabled
                ? ObstacleAvoidanceType.NoObstacleAvoidance
                : defaultObstacleAvoidanceType;
            agent.avoidancePriority = enabled ? 0 : defaultAvoidancePriority;
        }

        private void CaptureDefaultNavigationSettings()
        {
            if (agent == null || hasDefaultNavigationSettings)
            {
                return;
            }

            defaultAutoTraverseOffMeshLink = agent.autoTraverseOffMeshLink;
            defaultObstacleAvoidanceType = agent.obstacleAvoidanceType;
            defaultAvoidancePriority = agent.avoidancePriority;
            hasDefaultNavigationSettings = true;
        }

        private void RestoreDefaultNavigationSettings()
        {
            if (agent == null || !hasDefaultNavigationSettings)
            {
                return;
            }

            agent.autoTraverseOffMeshLink = defaultAutoTraverseOffMeshLink;
            agent.obstacleAvoidanceType = defaultObstacleAvoidanceType;
            agent.avoidancePriority = defaultAvoidancePriority;
        }

        private bool CanUseDirectNavigationDestination(Vector3 destination)
        {
            if (!HasCompletePath(destination))
            {
                return false;
            }

            if (target == null || target.TargetKind == CastleTargetKind.Wall)
            {
                return true;
            }

            if (target.AttackSlots != null && leasedSlotIndex < 0)
            {
                return false; // 공격 슬롯이 없는 왕궁 앵커를 NavMesh의 외부 근접점으로 오인하지 않는다
            }

            return controller == null ||
                   !controller.IsTurretLineBlocked(destination, target.transform.position, 0.04f);
        }

        private bool HasCompletePath(Vector3 destination)
        {
            if (navigationPath == null)
            {
                navigationPath = new NavMeshPath();
            }

            return agent.CalculatePath(destination, navigationPath) &&
                   navigationPath.status == NavMeshPathStatus.PathComplete;
        }

        private void StopMoving()
        {
            if (!agent.isOnNavMesh)
            {
                return;
            }

            if (!agent.isStopped || agent.hasPath || agent.pathPending)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            hasNavigationDestination = false;
            if (!attackActionRunning)
            {
                animationDriver?.PlayIdle();
            }
        }

        private void FaceTowards(Vector3 destination, float deltaTime)
        {
            var direction = destination - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 12f * deltaTime);
        }

        private void HandleDamaged(DamageReport report)
        {
            recentDamagePerSecond += report.AppliedDamage / 2.5f;
            visualFeedback?.PlayHit();
            CastleRaidOverheadHealthBar.ShowDamage(transform, health, true); // 아군은 초록색
            Damaged?.Invoke(this, report);
        }

        private void HandleDied(DamageReport report)
        {
            HideHealthBar();
            visualFeedback?.PlayDeath();
            attackActionRunning = false;
            deathPresentationDuration = Mathf.Max(
                UnitVisualFeedback.DeathPulseDurationSeconds,
                animationDriver == null ? 0f : animationDriver.PlayDeath());
            ReleaseTarget();
            Died?.Invoke(this); // Controller가 연출 뒤 풀 반환
        }

        private void HideHealthBar()
        {
            if (TryGetComponent<CastleRaidOverheadHealthBar>(out var healthBar))
            {
                healthBar.HideImmediately();
            }
        }

        private void ResolveReferences()
        {
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }

            if (visualFeedback == null)
            {
                visualFeedback = GetComponent<UnitVisualFeedback>();
            }

            if (animationDriver == null)
            {
                animationDriver = GetComponent<MonsterAnimationDriver>();
            }

            slotPathPredicate ??= HasCompletePath;
        }

        private bool ShouldRecoverNavigation()
        {
            if (target == null || !target.IsAlive || agent == null || !agent.isOnNavMesh || agent.pathPending)
            {
                return target == null || !target.IsAlive;
            }

            return hasNavigationDestination && (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete);
        }

        private float ResolveNavigationSpread()
        {
            return Mathf.Abs(GetInstanceID() % 9) / 8f * NavigationRefreshSpread;
        }

        private void StartAttack()
        {
            attackCooldown = Mathf.Max(0.1f, stats.attackInterval);
            if (runtimeAssetSet != null && animationDriver != null && animationDriver.IsReady)
            {
                attackActionRunning = true; // normalizedTime 0 Marker도 현재 대상을 사용
                if (animationDriver.TryBeginAttack(
                        stats.attackInterval,
                        ++nextActionSequenceId,
                        HandleAttackMarker))
                {
                    return;
                }

                attackActionRunning = false;
            }

            controller?.Attack(this, target, stats.damage * attackDamageMultiplier);
        }

        private void TickAttackAction(float deltaTime)
        {
            StopMoving();
            if (target != null && target.IsAlive)
            {
                FaceTowards(target.transform.position, deltaTime);
            }

            if (animationDriver == null || animationDriver.TickAttack(deltaTime, HandleAttackMarker))
            {
                attackActionRunning = false;
                animationDriver?.PlayIdle(true);
            }
        }

        private void HandleAttackMarker(int markerIndex, MonsterAttackMarker marker)
        {
            if (attackActionRunning && target != null && target.IsAlive)
            {
                var markerRatio = marker == null ? 1f : Mathf.Max(0f, marker.PowerRatio);
                controller?.Attack(
                    this,
                    target,
                    stats.damage * attackDamageMultiplier * markerRatio); // Marker 분배 피해와 지원 버프 반영
            }
        }

        private void TickRuntimeEffects(float deltaTime)
        {
            supportCooldownRemaining = Mathf.Max(0f, supportCooldownRemaining - deltaTime);
            recentDamagePerSecond *= Mathf.Exp(-deltaTime / 2.5f);
            recentThreatAggroRemaining = Mathf.Max(0f, recentThreatAggroRemaining - deltaTime);
            if (recentThreatAggroRemaining <= 0f || recentThreatAggressor == null ||
                !recentThreatAggressor.IsAlive)
            {
                recentThreatAggressor = null;
            }

            if (attackBuffRemaining > 0f)
            {
                attackBuffRemaining = Mathf.Max(0f, attackBuffRemaining - deltaTime);
                if (attackBuffRemaining <= 0f)
                {
                    attackDamageMultiplier = 1f;
                }
            }

            if (defenseBuffRemaining > 0f)
            {
                defenseBuffRemaining = Mathf.Max(0f, defenseBuffRemaining - deltaTime);
                if (defenseBuffRemaining <= 0f)
                {
                    health?.SetIncomingDamageMultiplier(1f);
                }
            }
        }

        private bool TickTacticalSupport(float deltaTime)
        {
            if (supportCooldownRemaining > 0f)
            {
                supportDecision = default;
                return false; // 재사용 대기 중에는 기본 진격형으로 전투
            }

            if (!supportDecision.IsValid || Time.time >= nextSupportDecisionAt)
            {
                nextSupportDecisionAt = Time.time + 0.25f + ResolveNavigationSpread();
                if (!controller.TrySelectSupportDecision(this, out supportDecision))
                {
                    supportDecision = default;
                    return false;
                }

                ReleaseTarget(); // 지원 이동 중 공격 자리 임대를 점유하지 않는다
            }

            var supportTarget = supportDecision.Target;
            var distance = PlanarDistance(transform.position, supportTarget.transform.position);
            if (distance > supportDecision.Profile.SupportRange)
            {
                return MoveTo(supportTarget.transform.position);
            }

            StopMoving();
            FaceTowards(supportTarget.transform.position, deltaTime);
            controller.CommitSupportDecision(this, supportDecision);
            supportTarget.ApplySupportAction(supportDecision);
            supportCooldownRemaining = supportDecision.Profile.SupportCooldown;
            supportDecision = default;
            animationDriver?.PlayIdle(true);
            return true;
        }

        private void ApplySupportAction(CastleRaidSupportDecision decision)
        {
            if (!decision.IsValid || decision.Target != this || health == null || !health.IsAlive)
            {
                return;
            }

            switch (decision.Action)
            {
                case CastleRaidSupportAction.Heal:
                    var before = health.CurrentHealth;
                    health.Heal(health.MaxHealth * decision.Profile.HealRatio);
                    controller?.PlaySupportFeedback(this, decision.Action, health.CurrentHealth - before);
                    break;
                case CastleRaidSupportAction.AttackBuff:
                    attackDamageMultiplier = Mathf.Max(
                        attackDamageMultiplier,
                        1f + decision.Profile.AttackBuffRate);
                    attackBuffRemaining = Mathf.Max(attackBuffRemaining, decision.Profile.SupportDuration);
                    break;
                case CastleRaidSupportAction.DefenseBuff:
                    health.SetIncomingDamageMultiplier(decision.Profile.DefenseDamageMultiplier);
                    defenseBuffRemaining = Mathf.Max(defenseBuffRemaining, decision.Profile.SupportDuration);
                    break;
            }
        }

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }
    }
}
