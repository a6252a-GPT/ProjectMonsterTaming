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
        [SerializeField] private NavMeshAgent agent; // 길찾기·이동 담당
        [SerializeField] private HealthComponent health; // 공용 체력 부품
        [SerializeField] private UnitVisualFeedback visualFeedback; // 피격·사망 연출
        [SerializeField] private MonsterAnimationDriver animationDriver; // 정식 몬스터 동작 재생

        private CastleRaidController controller; // 목표·공격 조율자
        private UnitStatsSnapshot stats; // 이번 실행 능력치
        private CastleTarget target; // 현재 공격 대상
        private Transform leasedSlot; // 대상 주변 공격 자리
        private float attackCooldown; // 다음 공격 대기
        private MonsterRuntimeAssetSet runtimeAssetSet; // 편성 몬스터 실행 자산
        private bool attackActionRunning; // Marker 기반 공격 재생 중
        private int nextActionSequenceId; // 공격 재생 구분값
        private float deathPresentationDuration = UnitVisualFeedback.DeathPulseDurationSeconds;
        private NavMeshPath navigationPath; // 경로 검사 재사용 버퍼
        private Vector3 requestedNavigationDestination; // 공격 자리 변경 감지값
        private bool hasNavigationDestination; // 같은 경로를 매 프레임 다시 넣지 않음

        public bool IsAlive => health != null && health.IsAlive;
        public CastleTarget Target => target;
        public float DeathPresentationDuration => deathPresentationDuration;

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
            health.Initialize(stats.maxHealth);
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;

            agent.speed = Mathf.Max(0.1f, stats.moveSpeed);
            agent.acceleration = 16f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = Mathf.Max(0.35f, stats.attackRange * 0.75f);
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
            if (!IsAlive || controller == null)
            {
                return;
            }

            attackCooldown = Mathf.Max(0f, attackCooldown - deltaTime);
            if (attackActionRunning)
            {
                TickAttackAction(deltaTime);
                return;
            }

            var desiredTarget = controller.FindPriorityTarget(this); // 열린 경로 기준 목표 재평가
            if (desiredTarget != target)
            {
                SetTarget(desiredTarget);
            }

            if (target == null || !target.IsAlive)
            {
                StopMoving();
                return;
            }

            var destination = leasedSlot == null ? target.transform.position : leasedSlot.position; // 대여 자리를 우선
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

        public void ApplyDefenderDamage(float damage, Vector3 hitPoint)
        {
            health?.ApplyDamage(new DamageRequest(null, damage, hitPoint));
        }

        public bool CanReachTarget(CastleTarget candidate) // 목표 후보를 점유 변경 없이 실제 경로로 확인
        {
            if (candidate == null || agent == null || !agent.isOnNavMesh)
            {
                return false;
            }

            if (PlanarDistance(transform.position, candidate.transform.position) <= Mathf.Max(0.5f, stats.attackRange))
            {
                return true;
            }

            var destination = candidate.transform.position;
            if (candidate.AttackSlots != null &&
                candidate.AttackSlots.TryResolveAvailableSlot(this, transform.position, out var availableSlot))
            {
                destination = availableSlot.position;
            }

            return HasCompletePath(destination);
        }

        public bool RefreshNavigationPath() // 길막 오브젝트 파괴 뒤 즉시 새 경로 요청
        {
            if (!IsAlive || controller == null || agent == null || !agent.isOnNavMesh)
            {
                return false;
            }

            var desiredTarget = controller.FindPriorityTarget(this);
            if (desiredTarget != target)
            {
                SetTarget(desiredTarget);
            }

            if (target == null || !target.IsAlive)
            {
                StopMoving();
                return false;
            }

            var destination = leasedSlot == null ? target.transform.position : leasedSlot.position;
            agent.ResetPath(); // 제거 전 장애물을 사용한 기존 경로 폐기
            hasNavigationDestination = false;
            return MoveTo(destination);
        }

        public void Shutdown()
        {
            ReleaseTarget();
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Died -= HandleDied;
            }

            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }

            controller = null;
            runtimeAssetSet = null;
            attackActionRunning = false;
            nextActionSequenceId = 0;
            deathPresentationDuration = UnitVisualFeedback.DeathPulseDurationSeconds;
            hasNavigationDestination = false;
            animationDriver?.Shutdown();
            Died = null;
            Damaged = null;
        }

        private void SetTarget(CastleTarget nextTarget)
        {
            ReleaseTarget();
            target = nextTarget;
            hasNavigationDestination = false;
            if (target != null)
            {
                target.AttackSlots?.TryLease(this, transform.position, out leasedSlot); // 대상 주변 빈 자리 확보
            }
        }

        private void ReleaseTarget()
        {
            if (target != null)
            {
                target.AttackSlots?.Release(this);
            }

            target = null;
            leasedSlot = null;
            hasNavigationDestination = false;
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
            if (!HasCompletePath(resolved) &&
                !(controller != null && controller.TryResolveInnerEntry(transform.position, out resolved) && HasCompletePath(resolved)))
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

            agent.isStopped = true;
            agent.ResetPath();
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
            visualFeedback?.PlayHit();
            Damaged?.Invoke(this, report);
        }

        private void HandleDied(DamageReport report)
        {
            visualFeedback?.PlayDeath();
            attackActionRunning = false;
            deathPresentationDuration = Mathf.Max(
                UnitVisualFeedback.DeathPulseDurationSeconds,
                animationDriver == null ? 0f : animationDriver.PlayDeath());
            ReleaseTarget();
            Died?.Invoke(this); // Controller가 연출 뒤 풀 반환
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

            controller?.Attack(this, target, stats.damage);
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
                controller?.Attack(this, target, stats.damage); // 정식 동작의 실제 타격 시점에 성 피해 적용
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
