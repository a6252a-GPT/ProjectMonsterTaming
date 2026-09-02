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
            UnitEntryReason entryReason = UnitEntryReason.InitialDeployment,
            string displayName = null,
            MonsterBattlePresentationSnapshot presentation = default)
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
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? UnitId : displayName.Trim();
            Presentation = presentation;
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
        public string DisplayName { get; }
        public MonsterBattlePresentationSnapshot Presentation { get; }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(HealthComponent))]
    public sealed partial class UnitActor : MonoBehaviour // 공용 이동·공격 유닛
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
        private IUnitCombatAnimation combatAnimation;
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
        private float activeStunRemaining;
        private float activeSlowRemaining;
        private float activeSlowRate;
        private float activeBleedRemaining;
        private float activeBleedTickRemaining;
        private float activeBleedInterval;
        private float activeBleedDamage;
        private UnitActor activeBleedSource;
        private float activeBurnRemaining;
        private float activeBurnTickRemaining;
        private float activeBurnInterval;
        private float activeBurnDamage;
        private UnitActor activeBurnSource;
        private float activeAirborneElapsed;
        private float activeAirborneDuration;
        private float activeAirborneHeight;
        private float activeAirborneBaseY;
        private Animator[] fallbackHitStopAnimators = Array.Empty<Animator>();
        private float[] fallbackAnimatorSpeeds = Array.Empty<float>();
        private bool fallbackAnimatorsPaused;
        private bool fallbackAnimatorsResolved;

        public string UnitId { get; private set; }
        public string DisplayName { get; private set; }
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
        public bool IsActiveStunned => activeStunRemaining > 0f;
        public bool IsActiveAirborne => activeAirborneDuration > 0f;
        public bool IsActiveSlowed => activeSlowRemaining > 0f;
        public bool IsActiveBurning => activeBurnRemaining > 0f;
        public bool IsRanged => stats.ranged;
        public bool IsBoss { get; private set; }
        public bool IsCombatReady => combatReady;
        public UnitCombatBehavior CombatBehavior => combatBehavior;
        public UnitStatsSnapshot EffectiveStats => GetEffectiveStats(); // 피격 계산용 현재 Snapshot
        public float BodyRadius => Mathf.Max(0.1f, runtimeAssetSet?.BodyProfile?.BodyRadius ?? 0.45f);
        public float SupportOutputMultiplier => supportOutputMultiplier;
        public MonsterSkillRuntime SkillRuntime => monsterSkillRuntime;
        public MonsterBattlePresentationSnapshot Presentation { get; private set; }
        public int ActiveFocusPartySlotIndex => Presentation.PartySlotIndex;
        public bool CanQueueMonsterActiveFocus =>
            IsAlive && combatReady && canAttack && !isManuallyHeld;
        public bool CanArmMonsterActiveFocus =>
            CanQueueMonsterActiveFocus &&
            !attackActionRunning &&
            !IsHitStopped &&
            !IsInHitReaction &&
            !IsActiveStunned &&
            !IsActiveAirborne;

        public void SetActiveFocusTimeScale(float scale)
        {
            animationDriver?.SetFocusTimeScale(scale);
        }

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

            combatAnimation = GetComponent<IUnitCombatAnimation>();
        }

        public void Initialize(UnitSpawnRequest request, CombatWorld combatWorld, ICombatFeedbackPlayer feedbackPlayer)
        {
            Shutdown(); // 풀 재사용 전 이전 연결 정리
            UnitId = request.UnitId;
            DisplayName = request.DisplayName;
            Presentation = request.Presentation;
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
            world?.TrackMonsterActiveSkill(this);
            if (runtimeAssetSet != null)
            {
                world?.PlayMonsterFeedback(
                    runtimeAssetSet.FeedbackProfile?.Spawn,
                    animationDriver,
                    null,
                    runtimeAssetSet.BodyProfile?.VfxScale ?? 1f);
            }
        }

        public void Tick(float deltaTime)
        {
            if (TickLocalHitStop())
            {
                return;
            }

            if (TickActiveStatusEffects(deltaTime))
            {
                animationDriver?.PlayIdle();
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
            if (canAttack && attackCooldown <= 0f && !ShouldDeferBasicAttackForActive())
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
            world?.Unregister(this);
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

            world = null;
            feedback = null;
            Target = null;
            DisplayName = string.Empty;
            Presentation = default;
            followAnchor = null;
            hasLastAnchorPosition = false; // 08.07 안건준 추가 - 풀 재사용 전 이동 감지 상태 초기화
            isManuallyHeld = false;
            forcedTarget = null; // 08.07 안건준 추가 - 풀 재사용 전 강제 지정 상태 초기화
            forcedTargetTimer = 0f;
            localHitStopRemaining = 0f;
            ResetActiveStatusEffects();
            CancelCombatHitReaction();
            SetLocalAnimationPaused(false);
            fallbackHitStopAnimators = Array.Empty<Animator>();
            fallbackAnimatorSpeeds = Array.Empty<float>();
            fallbackAnimatorsResolved = false;
            Died = null; // 풀 재사용 전 외부 구독 제거
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
