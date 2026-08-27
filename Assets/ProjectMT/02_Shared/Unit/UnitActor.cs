using System;
using ProjectMT.Shared.Combat;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum UnitTeam // 전투 진영
    {
        Player,
        Enemy
    }

    public readonly struct UnitSpawnRequest // 유닛 한 기 생성 명세
    {
        public UnitSpawnRequest(
            string unitId,
            UnitStatsSnapshot stats,
            UnitTeam team,
            bool canMove = true,
            bool canAttack = true,
            float fixedDamagePerHit = 0f,
            Color visualTint = default,
            MonsterRuntimeAssetSet runtimeAssetSet = null,
            int appearanceSeed = 0,
            float visualScaleMultiplier = 1f,
            bool isBoss = false,
            float supportOutputMultiplier = 1f,
            MonsterPassiveSkill passiveSkill = null,
            MonsterActiveSkill activeSkill = null,
            int monsterLevel = 1,
            UnitEntryReason entryReason = UnitEntryReason.InitialDeployment)
        {
            UnitId = unitId ?? string.Empty;
            Stats = stats;
            Team = team;
            CanMove = canMove;
            CanAttack = canAttack;
            FixedDamagePerHit = fixedDamagePerHit;
            VisualTint = visualTint.a <= 0f ? Color.white : visualTint;
            RuntimeAssetSet = runtimeAssetSet;
            AppearanceSeed = appearanceSeed;
            VisualScaleMultiplier = Mathf.Max(0.01f, visualScaleMultiplier);
            IsBoss = isBoss;
            SupportOutputMultiplier = Mathf.Max(0f, supportOutputMultiplier);
            PassiveSkill = passiveSkill;
            ActiveSkill = activeSkill;
            MonsterLevel = Mathf.Max(1, monsterLevel);
            EntryReason = entryReason;
        }

        public string UnitId { get; }
        public UnitStatsSnapshot Stats { get; }
        public UnitTeam Team { get; }
        public bool CanMove { get; }
        public bool CanAttack { get; }
        public float FixedDamagePerHit { get; } // 콘텐츠 고정 피해값
        public Color VisualTint { get; }
        public MonsterRuntimeAssetSet RuntimeAssetSet { get; }
        public int AppearanceSeed { get; }
        public float VisualScaleMultiplier { get; }
        public bool IsBoss { get; }
        public float SupportOutputMultiplier { get; }
        public MonsterPassiveSkill PassiveSkill { get; }
        public MonsterActiveSkill ActiveSkill { get; }
        public int MonsterLevel { get; }
        public UnitEntryReason EntryReason { get; }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(HealthComponent))]
    public sealed class UnitActor : MonoBehaviour // 공용 이동·공격 유닛
    {
        [SerializeField] private HealthComponent health; // 체력 부품
        [SerializeField] private UnitVisualFeedback visualFeedback; // 피격 시각 연출
        [SerializeField] private MonsterAnimationDriver animationDriver; // 정식 Monster 동작 재생기

        private CombatWorld world; // 현재 전투 영역
        private ICombatFeedbackPlayer feedback; // 공용 연출 계약
        private UnitStatsSnapshot stats; // 이번 실행 능력치
        private bool canMove; // 이동 허용 여부
        private bool canAttack; // 공격 허용 여부
        private float attackCooldown; // 다음 공격 대기
        private float retargetCooldown; // 타깃 재탐색 대기
        private Transform followAnchor; // 추종 기준 대상
        private Vector3 followOffset; // 대형 내 위치
        private float followDetectionRange; // 추종 중 탐지 거리
        private float followLeashRange; // 기준점 복귀 거리
        // 08.07 안건준 추가 - 군단장(추종 기준점)이 실제로 움직이고 있는지 프레임 간 이동 거리로 직접 판단한다.
        // 이동 중에는 적 탐색·공격을 완전히 멈추고 대형 위치로만 이동해서, "따라가다가 적 보고 홱 돌아서
        // 공격하려다가 다시 따라가느라 홱 돌아서는" 부들부들 떨림을 없앤다. 군단장이 멈추면 다시 주변 적을
        // 찾아 공격한다. 추종 대상이 없으면(followAnchor == null) 기존 동작에 전혀 영향이 없다.
        private Vector3 lastAnchorPosition;
        private bool hasLastAnchorPosition;
        private const float AnchorMovingSpeedThreshold = 0.05f; // 이 값보다 느리면 "멈춰있다"고 판단(초당 이동 거리)
        private bool isManuallyHeld; // 플레이어가 직접 옮기는 동안 자기 행동 정지
        private IDamageable forcedTarget; // 08.07 안건준 추가 - 외부에서 강제로 지정한 우선 공격 대상(건물 등)
        private float forcedTargetTimer; // 08.07 안건준 추가 - 강제 지정 유지 시간(초)
        private float moveSpeedMultiplier = 1f; // 08.07 안건준 추가 - 콘텐츠 버프(예: 수호자의 탑 이동 속도 버프)로 인한 배율
        private float damageMultiplier = 1f; // 08.07 안건준 추가 - 콘텐츠 버프(예: 수호자의 탑 4번 건물 파괴 시 아군 공격력 2배)로 인한 배율
        private readonly System.Collections.Generic.List<ActiveMonsterBuff> monsterBuffs =
            new System.Collections.Generic.List<ActiveMonsterBuff>();
        private MonsterStatModifier activeMonsterBuffModifier;
        private float supportOutputMultiplier = 1f; // 회복·버프 출력 배율
        private readonly MonsterSkillRuntime monsterSkillRuntime = new MonsterSkillRuntime();
        private MonsterRuntimeAssetSet runtimeAssetSet;
        private UnitCombatBehavior combatBehavior;
        private bool combatReady;
        private IDamageable actionTarget;
        private bool attackActionRunning;
        private int nextActionSequenceId;
        private float localHitStopRemaining;
        private Vector3 combatKnockbackDirection;
        private float combatKnockbackDistance;
        private float combatKnockbackDuration;
        private float combatKnockbackElapsed;
        private float combatKnockbackAppliedDistance;
        private float combatPostKnockbackStaggerDuration;
        private float combatStaggerRemaining;
        private Animator[] fallbackHitStopAnimators = Array.Empty<Animator>();
        private float[] fallbackAnimatorSpeeds = Array.Empty<float>();
        private bool fallbackAnimatorsPaused;
        private bool fallbackAnimatorsResolved;

        public string UnitId { get; private set; }
        public UnitTeam Team { get; private set; }
        public HealthComponent Health => health;
        public UnitVisualFeedback VisualFeedback => visualFeedback;
        public UnitActor Target { get; private set; }
        public bool IsAlive => health != null && health.IsAlive;
        public bool IsManuallyHeld => isManuallyHeld;
        public MonsterRuntimeAssetSet RuntimeAssetSet => runtimeAssetSet;
        public MonsterAnimationDriver AnimationDriver => animationDriver;
        public bool IsHitStopped => localHitStopRemaining > 0f;
        public bool IsKnockedBack => combatKnockbackDistance > 0f;
        public bool IsHitStaggered => combatStaggerRemaining > 0f;
        public bool IsInHitReaction => IsKnockedBack || IsHitStaggered;
        public bool IsRanged => stats.ranged;
        public bool IsBoss { get; private set; }
        public bool IsCombatReady => combatReady;
        public UnitCombatBehavior CombatBehavior => combatBehavior;
        public UnitStatsSnapshot EffectiveStats => GetEffectiveStats(); // 피격 계산용 현재 Snapshot
        public float BodyRadius => Mathf.Max(0.1f, runtimeAssetSet?.BodyProfile?.BodyRadius ?? 0.45f);
        public float SupportOutputMultiplier => supportOutputMultiplier;
        public MonsterSkillRuntime SkillRuntime => monsterSkillRuntime;

        public event Action<UnitActor> Died;

        private void Awake()
        {
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

        public void Initialize(UnitSpawnRequest request, CombatWorld combatWorld, ICombatFeedbackPlayer feedbackPlayer)
        {
            Shutdown(); // 풀 재사용 전 이전 연결 정리
            UnitId = request.UnitId;
            Team = request.Team;
            stats = request.Stats;
            canMove = request.CanMove;
            canAttack = request.CanAttack;
            visualFeedback?.SetTint(request.VisualTint); // 풀 재사용마다 현재 몬스터 색상 적용
            world = combatWorld;
            feedback = feedbackPlayer;
            runtimeAssetSet = request.RuntimeAssetSet;
            supportOutputMultiplier = request.SupportOutputMultiplier;
            combatBehavior = UnitCombatBehavior.Default;
            combatReady = true;
            IsBoss = request.IsBoss;
            if (runtimeAssetSet != null && (animationDriver == null || !animationDriver.Initialize(runtimeAssetSet)))
            {
                Debug.LogError($"Formal Monster has no valid MonsterAnimationDriver. Unit={request.UnitId}", this);
                runtimeAssetSet = null; // 잘못된 Adapter에서도 기존 즉시 공격으로 안전하게 복귀
            }
            RefreshFallbackHitStopAnimators();
            attackCooldown = UnityEngine.Random.Range(0f, Mathf.Max(0.05f, stats.attackInterval * 0.35f)); // 동시 공격 분산
            retargetCooldown = UnityEngine.Random.Range(0f, 0.2f);
            moveSpeedMultiplier = 1f; // 08.07 안건준 추가 - 풀 재사용 전 이전 버프 배율 초기화
            damageMultiplier = 1f; // 08.07 안건준 추가 - 풀 재사용 전 이전 공격력 버프 배율 초기화
            health.Initialize(stats.maxHealth, request.FixedDamagePerHit);
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
            world?.Register(this);
            monsterSkillRuntime.Initialize(
                this,
                world,
                request.PassiveSkill,
                request.ActiveSkill,
                request.MonsterLevel,
                request.EntryReason);
            if (runtimeAssetSet != null)
            {
                world?.PlayMonsterFeedback(
                    runtimeAssetSet.FeedbackProfile?.Spawn,
                    animationDriver,
                    null,
                    runtimeAssetSet.BodyProfile?.VfxScale ?? 1f);
            }
        }

        public void SetFollowAnchor(Transform anchor, Vector3 offset, float detectionRange, float leashRange)
        {
            if (!ReferenceEquals(followAnchor, anchor))
            {
                hasLastAnchorPosition = false; // 08.07 안건준 추가 - 추종 대상이 바뀌면 이동 여부를 새로 측정
            }

            followAnchor = anchor;
            followOffset = offset;
            followDetectionRange = Mathf.Max(0.5f, detectionRange);
            followLeashRange = Mathf.Max(followDetectionRange, leashRange);
        }

        public void ClearFollowAnchor()
        {
            followAnchor = null;
            followOffset = Vector3.zero;
            hasLastAnchorPosition = false; // 08.07 안건준 추가 - 다음 추종 대상 기준으로 새로 측정하도록 초기화
        }

        public void SetCombatBehavior(UnitCombatBehavior behavior)
        {
            combatBehavior = behavior;
            SetCombatTarget(null);
            retargetCooldown = 0f; // 새 역할은 다음 Tick부터 즉시 반영
        }

        public void SetCombatReady(bool ready)
        {
            combatReady = ready;
            SetCombatTarget(null);
            retargetCooldown = 0f;
            if (ready)
            {
                return;
            }

            attackActionRunning = false;
            actionTarget = null;
            CancelCombatHitReaction();
            animationDriver?.PlayIdle(true); // 입장 이동은 콘텐츠 Controller가 별도로 재생
        }

        // 08.07 안건준 추가 - 콘텐츠 전용 스크립트가 일정 시간 동안 이 유닛의 공격을 특정 대상에 강제한다.
        // 아무도 호출하지 않으면 forcedTarget이 항상 null이라 기존 자동 전투(FindNearestOpponent) 동작에는
        // 전혀 영향이 없다. 유지 시간이 끝나거나 대상이 사라지면 자동으로 원래 탐색 방식으로 복귀한다.
        // (예: 수호자의 탑에서 군단장이 방어 건물 근처로 오면 아군이 적보다 건물을 먼저 공격하게 함)
        public void ForceTarget(IDamageable target, float holdSeconds)
        {
            if (target == null || !target.IsAlive)
            {
                return;
            }

            forcedTarget = target;
            forcedTargetTimer = Mathf.Max(0f, holdSeconds);
        }

        // 08.07 안건준 추가 - 지금 이 유닛이 target을 강제 공격 대상으로 삼고 있는지 확인.
        // 콘텐츠 쪽(예: 수호자의 탑 겹침 방지)에서 "공격 중인 대상에는 밀어내기를 적용하지 않는다"처럼
        // 판단할 때 쓴다. 아무도 호출하지 않으면 기존 동작에 영향이 없다.
        public bool IsForcedTargeting(IDamageable target)
        {
            return target != null && ReferenceEquals(forcedTarget, target);
        }

        // 08.07 안건준 추가 - 콘텐츠 전용 버프(예: 수호자의 탑 4번 건물의 적 이동 속도 버프)가 이동 속도를
        // 일시적으로 배율 조정할 때 쓴다. 아무도 호출하지 않으면 항상 1배라 기존 동작에 영향이 없다.
        public void SetMoveSpeedMultiplier(float multiplier)
        {
            moveSpeedMultiplier = Mathf.Max(0.01f, multiplier);
        }

        // 08.07 안건준 추가 - 콘텐츠 전용 버프(예: 수호자의 탑 4번 건물 파괴 시 아군 공격력 2배)가 공격력을
        // 일시적으로 배율 조정할 때 쓴다. 아무도 호출하지 않으면 항상 1배라 기존 동작에 영향이 없다.
        public void SetDamageMultiplier(float multiplier)
        {
            damageMultiplier = Mathf.Max(0.01f, multiplier);
        }

        public bool BeginManualReposition()
        {
            if (!IsAlive || isManuallyHeld)
            {
                return false;
            }

            isManuallyHeld = true;
            CancelCombatHitReaction();
            SetCombatTarget(null); // 잡힌 동안 자기 이동·공격·재탐색만 정지
            return true;
        }

        public void EndManualReposition()
        {
            isManuallyHeld = false;
            SetCombatTarget(null);
            retargetCooldown = 0f; // 착지 직후 새 위치에서 다시 탐색
        }

        public void Tick(float deltaTime)
        {
            if (TickLocalHitStop())
            {
                return;
            }

            if (TickCombatKnockback(deltaTime))
            {
                animationDriver?.PlayIdle();
                return; // 실제 밀림 중에는 자기 이동·공격을 겹치지 않음
            }

            if (TickCombatStagger(deltaTime))
            {
                animationDriver?.PlayIdle();
                return; // 밀림이 끝난 뒤 짧은 경직 동안 즉시 재접근하지 않음
            }

            if (!IsAlive || world == null)
            {
                return;
            }

            TickMonsterBuffs(deltaTime);

            if (!combatReady)
            {
                SetCombatTarget(null);
                return; // 입장 중에는 외부 행군만 허용
            }

            if (isManuallyHeld)
            {
                animationDriver?.PlayIdle();
                return; // 체력·피격·적 타깃 등록은 유지하고 자기 행동만 멈춤
            }

            monsterSkillRuntime.Tick(deltaTime, canAttack && !attackActionRunning);
            if (monsterSkillRuntime.IsExecuting)
            {
                animationDriver?.PlayIdle();
                return;
            }

            attackCooldown = Mathf.Max(0f, attackCooldown - deltaTime);
            retargetCooldown -= deltaTime;

            if (attackActionRunning)
            {
                TickAttackAction(deltaTime);
                return;
            }

            // 08.07 안건준 추가 - 강제 지정된 대상이 있으면 일반 추종/탐색 로직보다 우선한다.
            if (forcedTarget != null)
            {
                SetCombatTarget(null);
                forcedTargetTimer -= deltaTime;
                if (forcedTargetTimer > 0f && forcedTarget.IsAlive)
                {
                    TickForcedTarget(deltaTime);
                    return;
                }

                forcedTarget = null; // 유지 시간 종료 또는 대상 소멸 → 원래 자동 전투로 복귀
            }

            if (followAnchor != null)
            {
                var anchorPosition = followAnchor.position + followOffset;
                var anchorIsMoving = IsAnchorMoving(deltaTime); // 08.07 안건준 추가
                if (anchorIsMoving || PlanarDistance(transform.position, anchorPosition) > followLeashRange)
                {
                    SetCombatTarget(null); // 군단장 이동 중에는 대형 복귀 우선
                    MoveTowards(anchorPosition, deltaTime);
                    return;
                }
            }

            var targetInvalid = Target == null || !Target.IsAlive;
            var allowLiveRetarget = combatBehavior.TargetLoadPenalty <= 0f && retargetCooldown <= 0f;
            if (targetInvalid || allowLiveRetarget)
            {
                var range = followAnchor == null ? float.PositiveInfinity : followDetectionRange;
                SetCombatTarget(world.FindOpponent(
                    this,
                    range,
                    combatBehavior.TargetPriority,
                    combatBehavior.TargetLoadPenalty)); // 역할과 쏠림을 함께 반영
                retargetCooldown = combatBehavior.RetargetInterval;
            }

            if (Target == null)
            {
                if (followAnchor != null)
                {
                    MoveTowards(followAnchor.position + followOffset, deltaTime);
                }
                else
                {
                    animationDriver?.PlayIdle();
                }

                return;
            }

            var distance = PlanarDistance(transform.position, Target.transform.position);
            var attackRange = Mathf.Max(0.2f, GetEffectiveStats().attackRange);
            var retreatRange = attackRange * combatBehavior.RetreatRangeRatio;
            if (combatBehavior.UsesRetreat && distance < retreatRange)
            {
                MoveAwayFrom(Target.transform.position, deltaTime);
                return;
            }

            var preferredRange = ResolvePreferredRange(Target, attackRange);
            if (distance > Mathf.Max(0.2f, preferredRange))
            {
                MoveTowards(Target.transform.position, deltaTime);
                return;
            }

            FaceTowards(Target.transform.position, deltaTime);
            if (canAttack && attackCooldown <= 0f)
            {
                StartAttack(Target.Health); // 정식은 Animation Marker, 레거시는 기존 즉시 공격
            }
            else
            {
                animationDriver?.PlayIdle();
            }
        }

        public void Shutdown()
        {
            monsterSkillRuntime.Shutdown();
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Died -= HandleDied;
            }

            // 08.07 안건준 추가 - 던전을 클리어/실패로 나가면 CombatWorld.Clear()가 즉시 이 유닛의 Shutdown()을 부르므로,
            // 여기서도 배율을 1로 되돌려야 "이번 판에서 받은 공격력 버프"가 다음 판까지 새어나가지 않는다.
            // (Initialize()에서도 1로 리셋하지만, 나가는 시점에 바로 해제되는 걸 보장하려고 이중으로 초기화한다.)
            moveSpeedMultiplier = 1f;
            damageMultiplier = 1f;
            monsterBuffs.Clear();
            activeMonsterBuffModifier = default;
            supportOutputMultiplier = 1f;
            animationDriver?.Shutdown();
            runtimeAssetSet = null;
            combatBehavior = UnitCombatBehavior.Default;
            combatReady = false;
            IsBoss = false;
            actionTarget = null;
            attackActionRunning = false;
            nextActionSequenceId = 0;

            world?.Unregister(this);
            world = null;
            feedback = null;
            Target = null;
            followAnchor = null;
            hasLastAnchorPosition = false; // 08.07 안건준 추가 - 풀 재사용 전 이동 감지 상태 초기화
            isManuallyHeld = false;
            forcedTarget = null; // 08.07 안건준 추가 - 풀 재사용 전 강제 지정 상태 초기화
            forcedTargetTimer = 0f;
            localHitStopRemaining = 0f;
            CancelCombatHitReaction();
            SetLocalAnimationPaused(false);
            fallbackHitStopAnimators = Array.Empty<Animator>();
            fallbackAnimatorSpeeds = Array.Empty<float>();
            fallbackAnimatorsResolved = false;
            Died = null; // 풀 재사용 전 외부 구독 제거
        }

        public void ApplyLocalHitStop(float duration)
        {
            duration = Mathf.Clamp(duration, 0f, 0.06f);
            if (duration <= 0f || !gameObject.activeInHierarchy)
            {
                return;
            }

            localHitStopRemaining = Mathf.Max(localHitStopRemaining, duration); // 연속 타격은 합산하지 않음
            SetLocalAnimationPaused(true);
        }

        public float AdvanceForBasicAttack(
            Vector3 destination,
            float maxDistance,
            float stopDistance,
            float visualDuration)
        {
            if (!IsAlive || !combatReady || isManuallyHeld || maxDistance <= 0f)
            {
                return 0f;
            }

            var direction = destination - transform.position;
            direction.y = 0f;
            var distance = direction.magnitude;
            if (distance <= 0.001f)
            {
                return 0f;
            }

            direction /= distance;
            var advance = Mathf.Min(Mathf.Max(0f, maxDistance), Mathf.Max(0f, distance - Mathf.Max(0.05f, stopDistance)));
            if (advance <= 0f)
            {
                return 0f;
            }

            transform.position += direction * advance; // 전투 중 슬롯 고정 없이 실제 XZ 전진
            visualFeedback?.PlayAttackLunge(direction, Mathf.Min(0.3f, advance * 0.35f), visualDuration);
            return advance;
        }

        public bool TryApplyCombatKnockback(
            Vector3 worldDirection,
            float distance,
            float duration,
            float postKnockbackStagger = 0f)
        {
            if (Team == UnitTeam.Player || !IsAlive || IsBoss || !combatReady || isManuallyHeld || distance <= 0f)
            {
                return false; // 아군은 판정 루트를 밀지 않고 Visual 반동만 사용
            }

            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            distance = Mathf.Clamp(distance, 0f, 0.6f);
            duration = Mathf.Clamp(duration, 0.05f, 0.24f);
            postKnockbackStagger = Mathf.Clamp(postKnockbackStagger, 0f, 0.3f);
            if (IsKnockedBack && combatKnockbackElapsed < combatKnockbackDuration * 0.75f)
            {
                combatPostKnockbackStaggerDuration = Mathf.Max(
                    combatPostKnockbackStaggerDuration,
                    postKnockbackStagger);
                if (distance <= combatKnockbackDistance)
                {
                    return true; // 다단히트가 같은 밀림을 누적·재시작하지 않음
                }

                distance = Mathf.Max(0f, distance - combatKnockbackAppliedDistance); // 강한 요청만 남은 거리로 승격
            }

            combatKnockbackDirection = worldDirection.normalized;
            combatKnockbackDistance = distance;
            combatKnockbackDuration = duration;
            combatKnockbackElapsed = 0f;
            combatKnockbackAppliedDistance = 0f;
            combatPostKnockbackStaggerDuration = postKnockbackStagger;
            combatStaggerRemaining = 0f; // 새 타격은 남아 있던 이전 경직을 대체
            return distance > 0f;
        }

        private bool TickLocalHitStop()
        {
            if (localHitStopRemaining <= 0f)
            {
                return false;
            }

            localHitStopRemaining = Mathf.Max(0f, localHitStopRemaining - Time.unscaledDeltaTime);
            if (localHitStopRemaining <= 0f)
            {
                SetLocalAnimationPaused(false);
                return false;
            }

            return true;
        }

        private bool TickCombatKnockback(float deltaTime)
        {
            if (!IsKnockedBack)
            {
                return false;
            }

            combatKnockbackElapsed = Mathf.Min(
                combatKnockbackDuration,
                combatKnockbackElapsed + Mathf.Max(0f, deltaTime));
            var ratio = combatKnockbackDuration <= 0f ? 1f : combatKnockbackElapsed / combatKnockbackDuration;
            var pushRatio = Mathf.Clamp01(ratio / 0.65f);
            var easedPush = 1f - Mathf.Pow(1f - pushRatio, 3f); // 앞 65%에 퍽 밀고 뒤 35%는 정지
            var desiredDistance = combatKnockbackDistance * easedPush;
            var stepDistance = Mathf.Max(0f, desiredDistance - combatKnockbackAppliedDistance);
            if (stepDistance > 0f)
            {
                var nextPosition = transform.position + combatKnockbackDirection * stepDistance;
                nextPosition.y = transform.position.y; // 실제 Y는 지형 기준을 유지
                transform.position = nextPosition;
                combatKnockbackAppliedDistance = desiredDistance;
            }

            if (ratio >= 1f)
            {
                combatStaggerRemaining = combatPostKnockbackStaggerDuration;
                CompleteCombatKnockback();
            }

            return true;
        }

        private bool TickCombatStagger(float deltaTime)
        {
            if (!IsHitStaggered)
            {
                return false;
            }

            combatStaggerRemaining = Mathf.Max(0f, combatStaggerRemaining - Mathf.Max(0f, deltaTime));
            return true;
        }

        private void CompleteCombatKnockback()
        {
            combatKnockbackDirection = Vector3.zero;
            combatKnockbackDistance = 0f;
            combatKnockbackDuration = 0f;
            combatKnockbackElapsed = 0f;
            combatKnockbackAppliedDistance = 0f;
            combatPostKnockbackStaggerDuration = 0f;
        }

        private void CancelCombatHitReaction()
        {
            CompleteCombatKnockback();
            combatStaggerRemaining = 0f;
        }

        private void SetLocalAnimationPaused(bool paused)
        {
            if (animationDriver != null)
            {
                animationDriver.SetLocallyPaused(paused);
                return;
            }

            if (paused)
            {
                if (fallbackAnimatorsPaused)
                {
                    return;
                }

                RefreshFallbackHitStopAnimators();
                for (var index = 0; index < fallbackHitStopAnimators.Length; index++)
                {
                    var animator = fallbackHitStopAnimators[index];
                    if (animator == null)
                    {
                        continue;
                    }

                    fallbackAnimatorSpeeds[index] = animator.speed;
                    animator.speed = 0f;
                }

                fallbackAnimatorsPaused = true;
                return;
            }

            if (!fallbackAnimatorsPaused)
            {
                return;
            }

            for (var index = 0; index < fallbackHitStopAnimators.Length; index++)
            {
                if (fallbackHitStopAnimators[index] != null)
                {
                    fallbackHitStopAnimators[index].speed = fallbackAnimatorSpeeds[index];
                }
            }

            fallbackAnimatorsPaused = false;
        }

        private void RefreshFallbackHitStopAnimators()
        {
            if (animationDriver != null || fallbackAnimatorsPaused || fallbackAnimatorsResolved)
            {
                return;
            }

            fallbackHitStopAnimators = GetComponentsInChildren<Animator>(true);
            fallbackAnimatorSpeeds = new float[fallbackHitStopAnimators.Length];
            fallbackAnimatorsResolved = true;
        }

        // 08.07 안건준 추가 - damageMultiplier가 적용된 능력치 사본을 반환한다(원본 stats는 그대로 유지).
        // 배율이 항상 1이면 stats와 동일해서 기존 동작에 영향이 없다.
        private UnitStatsSnapshot GetEffectiveStats()
        {
            var effective = stats;
            effective.maxHealth *= Mathf.Max(0.01f, 1f + activeMonsterBuffModifier.HealthRate);
            effective.damage *= damageMultiplier *
                                Mathf.Max(0.01f, 1f + activeMonsterBuffModifier.AttackRate);
            effective.defense *= Mathf.Max(0.01f, 1f + activeMonsterBuffModifier.DefenseRate);
            effective.moveSpeed *= moveSpeedMultiplier *
                                   Mathf.Max(0.01f, 1f + activeMonsterBuffModifier.MoveSpeedRate);
            effective.attackRange *= Mathf.Max(0.01f, 1f + activeMonsterBuffModifier.AttackRangeRate);
            effective.attackInterval /= Mathf.Max(0.01f, 1f + activeMonsterBuffModifier.AttackSpeedRate);
            return effective;
        }

        public void ApplyMonsterBuff(
            string effectId,
            MonsterStatModifier modifier,
            float duration,
            MonsterBuffStackPolicy stackPolicy)
        {
            if (string.IsNullOrWhiteSpace(effectId) || modifier.IsEmpty || duration <= 0f || !IsAlive)
            {
                return;
            }

            ActiveMonsterBuff existing = null;
            for (var index = 0; index < monsterBuffs.Count; index++)
            {
                if (string.Equals(monsterBuffs[index].EffectId, effectId, StringComparison.OrdinalIgnoreCase))
                {
                    existing = monsterBuffs[index];
                    break;
                }
            }

            if (existing == null)
            {
                monsterBuffs.Add(new ActiveMonsterBuff(effectId, modifier, duration));
            }
            else if (stackPolicy == MonsterBuffStackPolicy.RefreshDuration)
            {
                existing.Modifier = modifier;
                existing.RemainingTime = duration;
            }
            else if (GetModifierStrength(modifier) > GetModifierStrength(existing.Modifier))
            {
                existing.Modifier = modifier;
                existing.RemainingTime = duration;
            }
            else
            {
                existing.RemainingTime = Mathf.Max(existing.RemainingTime, duration);
            }

            RebuildMonsterBuffModifier();
        }

        public float ScaleSupportOutput(float amount)
        {
            return Mathf.Max(0f, amount) * supportOutputMultiplier;
        }

        private void StartAttack(IDamageable target)
        {
            if (target == null || !target.IsAlive || world == null)
            {
                return;
            }

            var effectiveStats = GetEffectiveStats();
            attackCooldown = Mathf.Max(0.05f, effectiveStats.attackInterval);
            if (runtimeAssetSet != null && animationDriver != null && animationDriver.IsReady)
            {
                actionTarget = target; // normalizedTime 0 Marker도 같은 고정 타깃 사용
                attackActionRunning = true;
                if (animationDriver.TryBeginAttack(
                        effectiveStats.attackInterval,
                        ++nextActionSequenceId,
                        HandleAttackMarker))
                {
                    var startFeedback = animationDriver.CurrentAttackStartFeedback ??
                                        runtimeAssetSet.FeedbackProfile?.AttackStart;
                    world.PlayMonsterFeedback(
                        startFeedback,
                        animationDriver,
                        null,
                        runtimeAssetSet.BodyProfile?.VfxScale ?? 1f);
                    return;
                }

                actionTarget = null;
                attackActionRunning = false;
            }

            var component = target as Component;
            var targetActor = component != null ? component.GetComponent<UnitActor>() : null;
            if (targetActor != null)
            {
                world.Attack(this, targetActor, effectiveStats); // Runtime Asset 없는 레거시 호환 경로
            }
            else
            {
                world.AttackDamageable(this, target, effectiveStats);
            }
        }

        private void TickAttackAction(float deltaTime)
        {
            if (actionTarget != null && actionTarget.IsAlive)
            {
                FaceTowards(actionTarget.Position, deltaTime);
            }

            if (animationDriver == null || animationDriver.TickAttack(deltaTime, HandleAttackMarker))
            {
                attackActionRunning = false;
                actionTarget = null;
                animationDriver?.PlayIdle(true);
            }
        }

        private void HandleAttackMarker(int markerIndex, MonsterAttackMarker marker)
        {
            if (!attackActionRunning)
            {
                return;
            }

            if (actionTarget == null || !actionTarget.IsAlive || runtimeAssetSet == null)
            {
                return;
            }

            if (runtimeAssetSet.CombatProfile?.Action is ProjectileActionDefinition projectileAction)
            {
                var launchDirection = actionTarget.Position - transform.position;
                VisualFeedback?.PlayAttackRecoil(
                    launchDirection,
                    projectileAction.LaunchRecoilDistance,
                    projectileAction.LaunchRecoilDuration);
            }

            world?.ExecuteMonsterAction(
                this,
                actionTarget,
                GetEffectiveStats(),
                runtimeAssetSet,
                marker,
                animationDriver);
        }

        private void TickMonsterBuffs(float deltaTime)
        {
            var changed = false;
            for (var index = monsterBuffs.Count - 1; index >= 0; index--)
            {
                var buff = monsterBuffs[index];
                buff.RemainingTime -= Mathf.Max(0f, deltaTime);
                if (buff.RemainingTime > 0f)
                {
                    continue;
                }

                monsterBuffs.RemoveAt(index);
                changed = true;
            }

            if (changed)
            {
                RebuildMonsterBuffModifier();
            }
        }

        private void RebuildMonsterBuffModifier()
        {
            activeMonsterBuffModifier = default;
            for (var index = 0; index < monsterBuffs.Count; index++)
            {
                activeMonsterBuffModifier += monsterBuffs[index].Modifier;
            }

            if (health != null && health.IsAlive)
            {
                var maxHealth = stats.maxHealth *
                                Mathf.Max(0.01f, 1f + activeMonsterBuffModifier.HealthRate);
                health.SetMaxHealth(maxHealth, true);
            }
        }

        private static float GetModifierStrength(MonsterStatModifier modifier)
        {
            return Mathf.Abs(modifier.HealthRate) +
                   Mathf.Abs(modifier.AttackRate) +
                   Mathf.Abs(modifier.DefenseRate) +
                   Mathf.Abs(modifier.AttackSpeedRate) +
                   Mathf.Abs(modifier.MoveSpeedRate) +
                   Mathf.Abs(modifier.AttackRangeRate);
        }

        // 08.07 안건준 추가 - 강제 지정된 대상(IDamageable)을 향해 이동·공격한다.
        // 일반 Target 탐색·추종 로직과는 별개로 동작하며, 유지 시간이 끝나면 자동으로 원래 로직에 넘어간다.
        private void TickForcedTarget(float deltaTime)
        {
            Target = null; // 강제 지정 중에는 일반 Target 탐색 결과를 사용하지 않음
            var distance = PlanarDistance(transform.position, forcedTarget.Position);
            if (distance > Mathf.Max(0.2f, GetEffectiveStats().attackRange))
            {
                MoveTowards(forcedTarget.Position, deltaTime);
                return;
            }

            FaceTowards(forcedTarget.Position, deltaTime);
            if (canAttack && attackCooldown <= 0f)
            {
                StartAttack(forcedTarget); // 정식은 같은 Marker 경로, 레거시는 기존 구조물 공격
            }
            else
            {
                animationDriver?.PlayIdle();
            }
        }

        // 08.07 안건준 추가 - 추종 기준점(군단장)의 프레임 간 이동 거리를 재서 "지금 걷고 있는지" 판단한다.
        // 별도의 이동 컨트롤러 참조 없이, followAnchor의 위치 변화만으로 계산해서 어떤 콘텐츠에서도 그대로 쓸 수 있다.
        private bool IsAnchorMoving(float deltaTime)
        {
            var currentAnchorPosition = followAnchor.position;
            if (!hasLastAnchorPosition || deltaTime <= 0f)
            {
                lastAnchorPosition = currentAnchorPosition;
                hasLastAnchorPosition = true;
                return false;
            }

            var speed = PlanarDistance(currentAnchorPosition, lastAnchorPosition) / deltaTime;
            lastAnchorPosition = currentAnchorPosition;
            return speed > AnchorMovingSpeedThreshold;
        }

        private void MoveTowards(Vector3 destination, float deltaTime)
        {
            var effectiveStats = GetEffectiveStats();
            if (!canMove || effectiveStats.moveSpeed <= 0f)
            {
                animationDriver?.PlayIdle();
                return;
            }

            destination.y = transform.position.y;
            transform.position = Vector3.MoveTowards(transform.position, destination, effectiveStats.moveSpeed * deltaTime);
            FaceTowards(destination, deltaTime);
            animationDriver?.PlayMove();
        }

        private float ResolvePreferredRange(UnitActor target, float attackRange)
        {
            var configuredRange = attackRange * combatBehavior.PreferredRangeRatio;
            if (IsRanged || target == null)
            {
                return configuredRange;
            }

            var bodyRange = (BodyRadius + target.BodyRadius) * 0.9f;
            return Mathf.Min(attackRange * 0.94f, Mathf.Max(configuredRange, bodyRange));
        }

        private void SetCombatTarget(UnitActor target)
        {
            if (Target == target)
            {
                return;
            }

            Target = target;
        }

        private void MoveAwayFrom(Vector3 dangerPosition, float deltaTime)
        {
            var direction = transform.position - dangerPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = -transform.forward;
                direction.y = 0f;
            }

            MoveTowards(transform.position + direction.normalized, deltaTime);
        }

        private void FaceTowards(Vector3 destination, float deltaTime)
        {
            var direction = destination - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 12f * deltaTime);
        }

        private void HandleDamaged(DamageReport report)
        {
            monsterSkillRuntime.NotifyDamaged(report);
            feedback?.PlayHit(this, report);
            if (runtimeAssetSet != null)
            {
                world?.PlayMonsterFeedback(
                    runtimeAssetSet.FeedbackProfile?.HitReceived,
                    animationDriver,
                    runtimeAssetSet.BodyProfile?.HitCenterPath,
                    runtimeAssetSet.BodyProfile?.VfxScale ?? 1f);
            }
        }

        private void HandleDied(DamageReport report)
        {
            monsterSkillRuntime.Shutdown();
            feedback?.PlayDeath(this, report);
            attackActionRunning = false;
            actionTarget = null;
            CancelCombatHitReaction();
            var returnDelay = (animationDriver?.PlayDeath() ?? 0.38f) + localHitStopRemaining;
            if (runtimeAssetSet != null)
            {
                world?.PlayMonsterFeedback(
                    runtimeAssetSet.FeedbackProfile?.Death,
                    animationDriver,
                    runtimeAssetSet.BodyProfile?.HitCenterPath,
                    runtimeAssetSet.BodyProfile?.VfxScale ?? 1f);
            }

            Died?.Invoke(this);
            world?.NotifyDeath(this, returnDelay); // Death Clip 종료 뒤 풀 반환
        }

#if UNITY_EDITOR
        public void EditorConfigureReferences(
            HealthComponent healthComponent,
            UnitVisualFeedback feedbackComponent,
            MonsterAnimationDriver driver = null)
        {
            health = healthComponent;
            visualFeedback = feedbackComponent;
            animationDriver = driver;
        }
#endif

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }

        private sealed class ActiveMonsterBuff
        {
            public ActiveMonsterBuff(string effectId, MonsterStatModifier modifier, float remainingTime)
            {
                EffectId = effectId;
                Modifier = modifier;
                RemainingTime = remainingTime;
            }

            public string EffectId { get; }
            public MonsterStatModifier Modifier { get; set; }
            public float RemainingTime { get; set; }
        }
    }
}
