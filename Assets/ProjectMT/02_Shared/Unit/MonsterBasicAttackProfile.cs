using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum MonsterBasicAttackDelivery // 기존 자산·표시 호환용 조합 결과
    {
        Contact,
        Projectile,
        Instant,
        Dash,
        MultiHit,
        ReturningProjectile,
        Breath,
        Beam,
        TravelingWave
    }

    public enum MonsterBasicAttackDeliveryModule // 피해가 목표까지 전달되는 핵심 방식
    {
        Direct,
        Projectile,
        TravelingArea
    }

    public enum MonsterBasicAttackCollisionModule // 이동 판정의 충돌 처리
    {
        DirectResolve,
        StopOnFirstTarget,
        Pierce,
        AreaImpact,
        PassThrough
    }

    public enum MonsterBasicAttackSequenceModule // 한 Marker 뒤 피해 단계 구성
    {
        Single,
        Burst,
        ReturnPasses
    }

    public enum MonsterBasicAttackMovementModule // 공격자 논리 루트 이동
    {
        None,
        Dash
    }

    public enum MonsterBasicAttackPresentationKind // 기본 연출 슬롯을 고르는 비전투 태그
    {
        Contact,
        Shot,
        Sweep,
        Thrust,
        Slam,
        Explosion,
        Instant,
        Dash,
        Combo,
        Scatter,
        Returning,
        Breath,
        Beam,
        Wave
    }

    public enum MonsterBasicAttackShape // XZ 판정 모양
    {
        Single,
        Fan,
        Line,
        Circle
    }

    public enum MonsterBasicAttackCenter
    {
        Source,
        PrimaryTarget,
        Forward
    }

    public enum MonsterBasicAttackProjectileTravel
    {
        None,
        Homing,
        Straight,
        Returning
    }

    public enum MonsterBasicAttackSweepDirection // 부채꼴 판정의 읽기 방향
    {
        Simultaneous,
        LeftToRight,
        RightToLeft
    }

    public enum MonsterBasicAttackProgression // 한 공격 블록 안에서 판정이 퍼지는 순서
    {
        Simultaneous,
        Forward,
        LeftToRight,
        RightToLeft,
        Outward
    }

    public enum MonsterBasicAttackMagicDirection
    {
        Forward,
        GroundUp,
        SkyDown
    }

    [CreateAssetMenu(menuName = "ProjectMT/Unit/Basic Attack Recipe", fileName = "BA_Custom")]
    public sealed class MonsterBasicAttackProfile : ScriptableObject // 공용 모듈을 조합한 기본공격 Recipe
    {
        public const int CurrentRecipeVersion = 1;
        public const int MaximumTargets = 32;
        public const int MaximumHitCount = 3;
        public const int MaximumProjectileCount = 12;

        [SerializeField] private string attackId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string designMemo;
        [SerializeField] private MonsterCombatType combatType;

        [Header("이전 VFX / SFX 호환")]
        [SerializeField, HideInInspector] private MonsterFeedbackCue launchFeedback = new MonsterFeedbackCue();
        [SerializeField, HideInInspector] private MonsterFeedbackCue projectileFeedback = new MonsterFeedbackCue();
        [SerializeField, HideInInspector] private MonsterFeedbackCue impactFeedback = new MonsterFeedbackCue();

        [Header("FEEL 전용 프리셋 슬롯")]
        [SerializeField] private BasicAttackFeelCue launchFeel = new BasicAttackFeelCue();
        [SerializeField] private BasicAttackFeelCue projectileFeel = new BasicAttackFeelCue();
        [SerializeField] private BasicAttackFeelCue impactFeel = new BasicAttackFeelCue();

        [Header("몬스터 고유 VFX 공간 계약")]
        [SerializeField] private List<MonsterBasicAttackVfxSlot> vfxSlots = new List<MonsterBasicAttackVfxSlot>();
        [SerializeField, HideInInspector] private List<MonsterBasicAttackVfxSlot> inactiveVfxSlots =
            new List<MonsterBasicAttackVfxSlot>();

        [Header("조립 모듈")]
        [SerializeField, HideInInspector] private int recipeVersion;
        [SerializeField] private MonsterBasicAttackDeliveryModule deliveryModule;
        [SerializeField] private MonsterBasicAttackCollisionModule collisionModule;
        [SerializeField] private MonsterBasicAttackSequenceModule sequenceModule;
        [SerializeField] private MonsterBasicAttackMovementModule movementModule;
        [SerializeField] private MonsterBasicAttackPresentationKind presentationKind;

        [Header("판정")]
        [SerializeField] private MonsterBasicAttackShape shape;
        [SerializeField] private MonsterBasicAttackCenter center = MonsterBasicAttackCenter.PrimaryTarget;
        [SerializeField] private MonsterBasicAttackProjectileTravel projectileTravel;
        [SerializeField] private MonsterBasicAttackSweepDirection sweepDirection;
        [SerializeField, Min(0.2f)] private float rangeMultiplier = 1f;
        [SerializeField, Min(0.05f)] private float radius = 0.35f;
        [SerializeField, Range(5f, 180f)] private float angle = 60f;
        [SerializeField, Min(0.05f)] private float lineWidth = 0.5f;
        [SerializeField, Min(0f)] private float forwardOffset = 1.5f;
        [SerializeField, Range(1, MaximumTargets)] private int maxTargets = 1;
        [SerializeField] private MonsterBasicAttackProgression progression;
        [SerializeField, Min(0f)] private float progressionDuration = 0.25f;
        [SerializeField, Min(0f)] private float telegraphDelay;
        [SerializeField, Min(0.05f)] private float visualDuration = 0.8f;
        [SerializeField] private MonsterBasicAttackMagicDirection magicDirection;

        [Header("투사체")]
        [SerializeField, HideInInspector] private GameObject projectileCarrierPrefab;
        [SerializeField, Range(1, MaximumProjectileCount)] private int projectileCount = 1;
        [SerializeField, Range(0f, 90f)] private float projectileSpreadAngle;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 9f;
        [SerializeField, Min(0.01f)] private float projectileLifetime = 3f;
        [SerializeField, Min(0.01f)] private float projectileCollisionRadius = 0.25f;

        [Header("피해 단계")]
        [SerializeField] private float[] damageRatios = { 1f };
        [SerializeField, Range(0.1f, 1f)] private float secondaryDamageRatio = 1f;
        [SerializeField, Range(0.01f, 0.3f)] private float repeatHitInterval = 0.08f;
        [SerializeField, Min(0.01f)] private float breathDuration = 0.8f;
        [SerializeField] private bool repeatImpactFeedback = true;

        [Header("공격자 이동·표시")]
        [SerializeField, Min(0f)] private float dashDistance;
        [SerializeField, Range(0.05f, 0.3f)] private float dashDuration = 0.1f;
        [SerializeField, Range(0.1f, 1f)] private float hitAreaVisibleDuration = 0.42f;

        [Header("이전 버전 호환")]
        [SerializeField, HideInInspector] private MonsterBasicAttackDelivery delivery;
        [SerializeField, HideInInspector] private bool stopOnFirstTarget;

        public string AttackId => attackId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? AttackId : displayName;
        public string DesignMemo => designMemo ?? string.Empty;
        public MonsterFeedbackCue LaunchFeedback => launchFeedback;
        public MonsterFeedbackCue ProjectileFeedback => projectileFeedback;
        public MonsterFeedbackCue ImpactFeedback => impactFeedback;
        public BasicAttackFeelCue LaunchFeel => launchFeel;
        public BasicAttackFeelCue ProjectileFeel => projectileFeel;
        public BasicAttackFeelCue ImpactFeel => impactFeel;
        public IReadOnlyList<MonsterBasicAttackVfxSlot> VfxSlots => vfxSlots ??
            (IReadOnlyList<MonsterBasicAttackVfxSlot>)Array.Empty<MonsterBasicAttackVfxSlot>();
        public IReadOnlyList<MonsterBasicAttackVfxSlot> InactiveVfxSlots => inactiveVfxSlots ??
            (IReadOnlyList<MonsterBasicAttackVfxSlot>)Array.Empty<MonsterBasicAttackVfxSlot>();
        public MonsterCombatType CombatType => combatType;
        public int RecipeVersion => recipeVersion;
        public bool IsModularRecipe => recipeVersion >= CurrentRecipeVersion;
        public MonsterBasicAttackDelivery Delivery => delivery;
        public MonsterBasicAttackDeliveryModule DeliveryModule => IsModularRecipe
            ? deliveryModule
            : ResolveLegacyDeliveryModule();
        public MonsterBasicAttackCollisionModule CollisionModule => IsModularRecipe
            ? collisionModule
            : ResolveLegacyCollisionModule();
        public MonsterBasicAttackSequenceModule SequenceModule => IsModularRecipe
            ? sequenceModule
            : ResolveLegacySequenceModule();
        public MonsterBasicAttackMovementModule MovementModule => IsModularRecipe
            ? movementModule
            : delivery == MonsterBasicAttackDelivery.Dash
                ? MonsterBasicAttackMovementModule.Dash
                : MonsterBasicAttackMovementModule.None;
        public MonsterBasicAttackPresentationKind PresentationKind => IsModularRecipe
            ? presentationKind
            : ResolveLegacyPresentationKind();
        public MonsterBasicAttackShape Shape => shape;
        public MonsterBasicAttackCenter Center => center;
        public MonsterBasicAttackProjectileTravel ProjectileTravel => projectileTravel;
        public MonsterBasicAttackSweepDirection SweepDirection => sweepDirection;
        public float RangeMultiplier => Mathf.Max(0.2f, rangeMultiplier);
        public float Radius => Mathf.Max(0.05f, radius);
        public float Angle => Mathf.Clamp(angle, 5f, 180f);
        public float LineWidth => Mathf.Max(0.05f, lineWidth);
        public float ForwardOffset => Mathf.Max(0f, forwardOffset);
        public int MaxTargets => Mathf.Clamp(maxTargets, 1, MaximumTargets);
        public MonsterBasicAttackProgression Progression => progression;
        public float ProgressionDuration => Mathf.Max(0f, progressionDuration);
        public float TelegraphDelay => Mathf.Max(0f, telegraphDelay);
        public float VisualDuration => Mathf.Max(0.05f, visualDuration);
        public MonsterBasicAttackMagicDirection MagicDirection => magicDirection;
        public GameObject ProjectileCarrierPrefab => projectileCarrierPrefab;
        public int ProjectileCount => Mathf.Clamp(projectileCount, 1, MaximumProjectileCount);
        public float ProjectileSpreadAngle => Mathf.Clamp(projectileSpreadAngle, 0f, 90f);
        public float ProjectileSpeed => Mathf.Max(0.01f, projectileSpeed);
        public float ProjectileLifetime => Mathf.Max(0.01f, projectileLifetime);
        public float ProjectileCollisionRadius => Mathf.Max(0.01f, projectileCollisionRadius);
        public IReadOnlyList<float> DamageRatios => damageRatios ??
            (IReadOnlyList<float>)Array.Empty<float>();
        public int HitCount => Mathf.Clamp(damageRatios?.Length ?? 0, 1, MaximumHitCount);
        public float SecondaryDamageRatio => Mathf.Clamp(secondaryDamageRatio, 0.1f, 1f);
        public float RepeatHitInterval => Mathf.Clamp(repeatHitInterval, 0.01f, 0.3f);
        public bool UsesBreathDurationContract => PresentationKind == MonsterBasicAttackPresentationKind.Breath;
        public float BreathDuration => Mathf.Max(0.01f, breathDuration);
        public bool RepeatImpactFeedback => repeatImpactFeedback;
        public float DashDistance => Mathf.Max(0f, dashDistance);
        public float DashDuration => Mathf.Clamp(dashDuration, 0.05f, 0.3f);
        public float HitAreaVisibleDuration => Mathf.Clamp(hitAreaVisibleDuration, 0.1f, 1f);
        public bool StopOnFirstTarget => CollisionModule == MonsterBasicAttackCollisionModule.StopOnFirstTarget;
        public bool UsesProjectileVisual => DeliveryModule != MonsterBasicAttackDeliveryModule.Direct;
        public bool UsesPatternSequence => SequenceModule != MonsterBasicAttackSequenceModule.Single;

        public static Vector3 ResolveDashDestination(
            Vector3 sourcePosition,
            Vector3 targetPosition,
            float maximumDistance,
            float stopDistance)
        {
            var direction = targetPosition - sourcePosition;
            direction.y = 0f;
            var distance = direction.magnitude;
            if (distance <= 0.001f)
            {
                return sourcePosition;
            }

            var advance = Mathf.Min(
                Mathf.Max(0f, maximumDistance),
                Mathf.Max(0f, distance - Mathf.Max(0.05f, stopDistance)));
            var destination = sourcePosition + direction / distance * advance;
            destination.y = sourcePosition.y;
            return destination;
        }

        public float ResolveRepeatHitInterval(float motionBreathDuration = 0f)
        {
            if (!UsesBreathDurationContract)
            {
                return RepeatHitInterval;
            }

            var duration = motionBreathDuration > 0f ? motionBreathDuration : BreathDuration;
            return Mathf.Max(0.01f, duration / Mathf.Max(1, HitCount));
        }

        public Vector3 ResolveProjectileDirection(Vector3 forward, int projectileIndex)
        {
            var count = ProjectileCount;
            var index = Mathf.Clamp(projectileIndex, 0, count - 1);
            var spreadRatio = count <= 1
                ? 0f
                : index / (float)(count - 1) - 0.5f;
            return Quaternion.AngleAxis(
                spreadRatio * ProjectileSpreadAngle,
                Vector3.up) * forward;
        }

        public float ResolveActivityDuration(
            float playbackSpeed = 1f,
            float motionBreathDuration = 0f)
        {
            var duration = Mathf.Max(VisualDuration, ProgressionDuration);
            if (HitCount > 1)
            {
                duration = Mathf.Max(
                    duration,
                    ResolveRepeatHitInterval(motionBreathDuration) * (HitCount - 1));
            }
            return duration / Mathf.Max(0.05f, playbackSpeed);
        }

        public MonsterProjectileAttackMode LegacyProjectileMode => CollisionModule switch
        {
            MonsterBasicAttackCollisionModule.AreaImpact => MonsterProjectileAttackMode.Area,
            MonsterBasicAttackCollisionModule.Pierce or MonsterBasicAttackCollisionModule.PassThrough =>
                MonsterProjectileAttackMode.Piercing,
            _ => MonsterProjectileAttackMode.Single
        };

        public float ResolveRange(float attackRange)
        {
            return Mathf.Max(0.2f, attackRange) * RangeMultiplier;
        }

        public float ResolveDamageRatio(int hitIndex)
        {
            if (damageRatios == null || damageRatios.Length == 0)
            {
                return 1f;
            }

            return Mathf.Max(0f, damageRatios[Mathf.Clamp(hitIndex, 0, damageRatios.Length - 1)]);
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(attackId) || string.IsNullOrWhiteSpace(displayName))
            {
                error = $"Basic Attack ID or name is blank. Profile={name}";
                return false;
            }

            if (!Enum.IsDefined(typeof(MonsterCombatType), combatType) || combatType == MonsterCombatType.Special ||
                !Enum.IsDefined(typeof(MonsterBasicAttackShape), shape) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackCenter), center) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackProjectileTravel), projectileTravel) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackSweepDirection), sweepDirection) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackProgression), progression) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackMagicDirection), magicDirection) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackDeliveryModule), DeliveryModule) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackCollisionModule), CollisionModule) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackSequenceModule), SequenceModule) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackMovementModule), MovementModule) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackPresentationKind), PresentationKind))
            {
                error = $"Basic Attack enum setting is invalid. Profile={name}";
                return false;
            }

            if (rangeMultiplier < 0.2f || radius <= 0f || angle < 5f || angle > 180f ||
                lineWidth <= 0f || forwardOffset < 0f || maxTargets < 1 || maxTargets > MaximumTargets ||
                projectileCount < 1 || projectileCount > MaximumProjectileCount ||
                damageRatios == null || damageRatios.Length < 1 || damageRatios.Length > MaximumHitCount ||
                projectileSpeed <= 0f || projectileLifetime <= 0f || projectileCollisionRadius <= 0f ||
                progressionDuration < 0f || telegraphDelay < 0f || visualDuration <= 0f ||
                UsesBreathDurationContract && breathDuration <= 0f)
            {
                error = $"Basic Attack geometry or hit limit is invalid. Profile={name}";
                return false;
            }

            var totalDamageRatio = 0f;
            for (var index = 0; index < damageRatios.Length; index++)
            {
                if (damageRatios[index] <= 0f)
                {
                    error = $"Basic Attack damage ratio must be positive. Profile={name}, Hit={index}";
                    return false;
                }

                totalDamageRatio += damageRatios[index];
            }

            if (Mathf.Abs(totalDamageRatio - 1f) > 0.001f || secondaryDamageRatio <= 0f || secondaryDamageRatio > 1f)
            {
                error = $"Basic Attack damage budget must total 1.0. Profile={name}, Total={totalDamageRatio:0.###}";
                return false;
            }

            if (launchFeel != null && !launchFeel.TryValidate(out error) ||
                projectileFeel != null && !projectileFeel.TryValidate(out error) ||
                impactFeel != null && !impactFeel.TryValidate(out error))
            {
                error = $"Basic Attack FEEL preset is invalid. Profile={name}, Detail={error}";
                return false;
            }

            if (launchFeedback != null && !launchFeedback.TryValidate(out error) ||
                projectileFeedback != null && !projectileFeedback.TryValidate(out error) ||
                impactFeedback != null && !impactFeedback.TryValidate(out error))
            {
                error = $"Basic Attack legacy presentation is invalid. Profile={name}, Detail={error}";
                return false;
            }

            var slotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deliveryVisualCount = 0;
            for (var index = 0; vfxSlots != null && index < vfxSlots.Count; index++)
            {
                var slot = vfxSlots[index];
                if (slot == null)
                {
                    error = $"Basic Attack VFX slot is null. Profile={name}";
                    return false;
                }
                if (!slot.TryValidate(out var slotError))
                {
                    error = $"Basic Attack VFX contract is invalid. Profile={name}, Detail={slotError}";
                    return false;
                }
                if (!MonsterBasicAttackVfxCompatibility.TryValidateSlot(this, slot, out var compatibilityError))
                {
                    error = $"Basic Attack VFX contract does not match its attack modules. Profile={name}, Detail={compatibilityError}";
                    return false;
                }
                if (!slotIds.Add(slot.SlotId))
                {
                    error = $"Basic Attack VFX slot ID is duplicated. Profile={name}, Slot={slot.SlotId}";
                    return false;
                }
                if (slot.IsDeliveryVisual)
                {
                    deliveryVisualCount++;
                }
            }
            if (deliveryVisualCount > 1 || deliveryVisualCount > 0 && !UsesProjectileVisual)
            {
                error = $"Basic Attack delivery visual slot count is invalid. Profile={name}";
                return false;
            }

            return TryValidateModuleCombination(out error);
        }

        private bool TryValidateModuleCombination(out string error)
        {
            if (DeliveryModule != MonsterBasicAttackDeliveryModule.Direct && combatType != MonsterCombatType.Ranged)
            {
                error = $"Moving Basic Attack delivery requires Ranged combat type. Profile={name}";
                return false;
            }

            if (DeliveryModule == MonsterBasicAttackDeliveryModule.Direct)
            {
                if (projectileTravel != MonsterBasicAttackProjectileTravel.None ||
                    CollisionModule != MonsterBasicAttackCollisionModule.DirectResolve || projectileCount != 1)
                {
                    error = $"Direct Basic Attack cannot use projectile travel, collision, or volley. Profile={name}";
                    return false;
                }
            }
            else if (DeliveryModule == MonsterBasicAttackDeliveryModule.Projectile)
            {
                if (projectileTravel == MonsterBasicAttackProjectileTravel.None ||
                    CollisionModule == MonsterBasicAttackCollisionModule.DirectResolve)
                {
                    error = $"Projectile Basic Attack requires travel and collision modules. Profile={name}";
                    return false;
                }

                if (CollisionModule == MonsterBasicAttackCollisionModule.AreaImpact && shape != MonsterBasicAttackShape.Circle)
                {
                    error = $"Area-impact Projectile requires a Circle shape. Profile={name}";
                    return false;
                }

                if (CollisionModule == MonsterBasicAttackCollisionModule.Pierce &&
                    projectileTravel != MonsterBasicAttackProjectileTravel.Straight)
                {
                    error = $"Piercing Projectile requires Straight travel. Profile={name}";
                    return false;
                }
            }
            else if (projectileTravel != MonsterBasicAttackProjectileTravel.Straight ||
                     CollisionModule != MonsterBasicAttackCollisionModule.PassThrough || projectileCount != 1)
            {
                error = $"Traveling Area requires Straight travel, PassThrough collision, and one actor. Profile={name}";
                return false;
            }

            switch (SequenceModule)
            {
                case MonsterBasicAttackSequenceModule.Single when HitCount != 1:
                    error = $"Single sequence requires one damage ratio. Profile={name}";
                    return false;
                case MonsterBasicAttackSequenceModule.Burst when
                    HitCount < 2 || DeliveryModule != MonsterBasicAttackDeliveryModule.Direct:
                    error = $"Burst sequence requires two or more Direct damage stages. Profile={name}";
                    return false;
                case MonsterBasicAttackSequenceModule.ReturnPasses when
                    HitCount != 2 || DeliveryModule != MonsterBasicAttackDeliveryModule.Projectile ||
                    projectileTravel != MonsterBasicAttackProjectileTravel.Returning ||
                    CollisionModule != MonsterBasicAttackCollisionModule.PassThrough || projectileCount != 1:
                    error = $"Return-pass sequence requires one Returning Projectile and two damage ratios. Profile={name}";
                    return false;
            }

            if (projectileTravel == MonsterBasicAttackProjectileTravel.Returning &&
                SequenceModule != MonsterBasicAttackSequenceModule.ReturnPasses)
            {
                error = $"Returning travel requires ReturnPasses sequence. Profile={name}";
                return false;
            }

            if (projectileCount > 1 &&
                (DeliveryModule != MonsterBasicAttackDeliveryModule.Projectile ||
                 projectileTravel != MonsterBasicAttackProjectileTravel.Straight ||
                 CollisionModule == MonsterBasicAttackCollisionModule.DirectResolve))
            {
                error = $"Volley requires a Straight Projectile collision recipe. Profile={name}";
                return false;
            }

            if (MovementModule == MonsterBasicAttackMovementModule.Dash &&
                dashDistance <= 0f)
            {
                error = $"Dash movement requires a positive distance. Profile={name}";
                return false;
            }

            if (sweepDirection != MonsterBasicAttackSweepDirection.Simultaneous &&
                (DeliveryModule != MonsterBasicAttackDeliveryModule.Direct ||
                 shape != MonsterBasicAttackShape.Fan || SequenceModule != MonsterBasicAttackSequenceModule.Single))
            {
                error = $"Directional sweep requires one Direct Fan hit. Profile={name}";
                return false;
            }

            error = null;
            return true;
        }

        private MonsterBasicAttackDeliveryModule ResolveLegacyDeliveryModule()
        {
            return delivery switch
            {
                MonsterBasicAttackDelivery.Projectile or MonsterBasicAttackDelivery.ReturningProjectile =>
                    MonsterBasicAttackDeliveryModule.Projectile,
                MonsterBasicAttackDelivery.TravelingWave => MonsterBasicAttackDeliveryModule.TravelingArea,
                _ => MonsterBasicAttackDeliveryModule.Direct
            };
        }

        private MonsterBasicAttackCollisionModule ResolveLegacyCollisionModule()
        {
            if (delivery == MonsterBasicAttackDelivery.TravelingWave ||
                delivery == MonsterBasicAttackDelivery.ReturningProjectile)
            {
                return MonsterBasicAttackCollisionModule.PassThrough;
            }

            if (delivery != MonsterBasicAttackDelivery.Projectile)
            {
                return MonsterBasicAttackCollisionModule.DirectResolve;
            }

            if (shape == MonsterBasicAttackShape.Circle)
            {
                return MonsterBasicAttackCollisionModule.AreaImpact;
            }

            if (stopOnFirstTarget || projectileCount > 1)
            {
                return MonsterBasicAttackCollisionModule.StopOnFirstTarget;
            }

            return projectileTravel == MonsterBasicAttackProjectileTravel.Straight
                ? MonsterBasicAttackCollisionModule.Pierce
                : MonsterBasicAttackCollisionModule.StopOnFirstTarget;
        }

        private MonsterBasicAttackSequenceModule ResolveLegacySequenceModule()
        {
            return delivery switch
            {
                MonsterBasicAttackDelivery.MultiHit or MonsterBasicAttackDelivery.Breath =>
                    MonsterBasicAttackSequenceModule.Burst,
                MonsterBasicAttackDelivery.ReturningProjectile => MonsterBasicAttackSequenceModule.ReturnPasses,
                _ => MonsterBasicAttackSequenceModule.Single
            };
        }

        private MonsterBasicAttackPresentationKind ResolveLegacyPresentationKind()
        {
            return delivery switch
            {
                MonsterBasicAttackDelivery.Projectile when projectileCount > 1 => MonsterBasicAttackPresentationKind.Scatter,
                MonsterBasicAttackDelivery.Projectile when shape == MonsterBasicAttackShape.Circle =>
                    MonsterBasicAttackPresentationKind.Explosion,
                MonsterBasicAttackDelivery.Projectile => MonsterBasicAttackPresentationKind.Shot,
                MonsterBasicAttackDelivery.Instant => MonsterBasicAttackPresentationKind.Instant,
                MonsterBasicAttackDelivery.Dash => MonsterBasicAttackPresentationKind.Dash,
                MonsterBasicAttackDelivery.MultiHit => MonsterBasicAttackPresentationKind.Combo,
                MonsterBasicAttackDelivery.ReturningProjectile => MonsterBasicAttackPresentationKind.Returning,
                MonsterBasicAttackDelivery.Breath => MonsterBasicAttackPresentationKind.Breath,
                MonsterBasicAttackDelivery.Beam => MonsterBasicAttackPresentationKind.Beam,
                MonsterBasicAttackDelivery.TravelingWave => MonsterBasicAttackPresentationKind.Wave,
                _ when shape == MonsterBasicAttackShape.Fan => MonsterBasicAttackPresentationKind.Sweep,
                _ when shape == MonsterBasicAttackShape.Line => MonsterBasicAttackPresentationKind.Thrust,
                _ when shape == MonsterBasicAttackShape.Circle => MonsterBasicAttackPresentationKind.Slam,
                _ => MonsterBasicAttackPresentationKind.Contact
            };
        }

        private void OnValidate()
        {
            if (IsModularRecipe)
            {
                SynchronizeLegacyFields();
            }
        }

#if UNITY_EDITOR
        public void EditorEnsureModularRecipe()
        {
            if (!IsModularRecipe)
            {
                ConfigureModulesFromLegacy();
            }

            EditorNormalizeModuleCombination();
        }

        public void EditorNormalizeModuleCombination()
        {
            recipeVersion = CurrentRecipeVersion;
            switch (deliveryModule)
            {
                case MonsterBasicAttackDeliveryModule.Direct:
                    projectileTravel = MonsterBasicAttackProjectileTravel.None;
                    collisionModule = MonsterBasicAttackCollisionModule.DirectResolve;
                    projectileCount = 1;
                    projectileSpreadAngle = 0f;
                    break;
                case MonsterBasicAttackDeliveryModule.Projectile:
                    combatType = MonsterCombatType.Ranged;
                    if (projectileTravel == MonsterBasicAttackProjectileTravel.None)
                    {
                        projectileTravel = MonsterBasicAttackProjectileTravel.Homing;
                    }
                    if (collisionModule == MonsterBasicAttackCollisionModule.DirectResolve)
                    {
                        collisionModule = MonsterBasicAttackCollisionModule.StopOnFirstTarget;
                    }
                    break;
                case MonsterBasicAttackDeliveryModule.TravelingArea:
                    combatType = MonsterCombatType.Ranged;
                    projectileTravel = MonsterBasicAttackProjectileTravel.Straight;
                    collisionModule = MonsterBasicAttackCollisionModule.PassThrough;
                    sequenceModule = MonsterBasicAttackSequenceModule.Single;
                    projectileCount = 1;
                    projectileSpreadAngle = 0f;
                    EnsureDamageRatioCount(1);
                    break;
            }

            switch (sequenceModule)
            {
                case MonsterBasicAttackSequenceModule.Single:
                    EnsureDamageRatioCount(1);
                    break;
                case MonsterBasicAttackSequenceModule.Burst:
                    deliveryModule = MonsterBasicAttackDeliveryModule.Direct;
                    projectileTravel = MonsterBasicAttackProjectileTravel.None;
                    collisionModule = MonsterBasicAttackCollisionModule.DirectResolve;
                    projectileCount = 1;
                    projectileSpreadAngle = 0f;
                    EnsureDamageRatioCount(Mathf.Clamp(HitCount, 2, MaximumHitCount));
                    break;
                case MonsterBasicAttackSequenceModule.ReturnPasses:
                    combatType = MonsterCombatType.Ranged;
                    deliveryModule = MonsterBasicAttackDeliveryModule.Projectile;
                    projectileTravel = MonsterBasicAttackProjectileTravel.Returning;
                    collisionModule = MonsterBasicAttackCollisionModule.PassThrough;
                    projectileCount = 1;
                    projectileSpreadAngle = 0f;
                    if (damageRatios == null || damageRatios.Length != 2)
                    {
                        damageRatios = new[] { 0.6f, 0.4f };
                    }
                    break;
            }

            if (projectileCount > 1)
            {
                combatType = MonsterCombatType.Ranged;
                deliveryModule = MonsterBasicAttackDeliveryModule.Projectile;
                projectileTravel = MonsterBasicAttackProjectileTravel.Straight;
                if (collisionModule == MonsterBasicAttackCollisionModule.DirectResolve)
                {
                    collisionModule = MonsterBasicAttackCollisionModule.StopOnFirstTarget;
                }
                sequenceModule = MonsterBasicAttackSequenceModule.Single;
                EnsureDamageRatioCount(1);
            }

            if (collisionModule == MonsterBasicAttackCollisionModule.AreaImpact)
            {
                shape = MonsterBasicAttackShape.Circle;
            }

            if (movementModule == MonsterBasicAttackMovementModule.Dash)
            {
                dashDistance = Mathf.Max(0.1f, dashDistance);
            }

            if (deliveryModule != MonsterBasicAttackDeliveryModule.Direct ||
                shape != MonsterBasicAttackShape.Fan || sequenceModule != MonsterBasicAttackSequenceModule.Single)
            {
                sweepDirection = MonsterBasicAttackSweepDirection.Simultaneous;
            }

            repeatImpactFeedback = presentationKind != MonsterBasicAttackPresentationKind.Breath;
            SynchronizeLegacyFields();
        }

        public void EditorConfigure(
            string id,
            string localizedName,
            MonsterCombatType type,
            MonsterBasicAttackDelivery deliveryMode,
            MonsterBasicAttackShape hitShape,
            MonsterBasicAttackCenter hitCenter,
            MonsterBasicAttackProjectileTravel travel,
            float rangeRatio,
            float hitRadius,
            float fanAngle,
            float width,
            int targetLimit,
            int volleyCount,
            float spreadAngle,
            float[] perHitDamageRatios,
            float secondaryRatio,
            float hitInterval = 0.08f,
            float advanceDistance = 0f,
            float advanceDuration = 0.1f,
            bool stopAfterFirstTarget = false,
            float areaVisibleDuration = 0.42f,
            float projectileMoveSpeed = 9f,
            float projectileLife = 3f,
            float projectileContactRadius = 0.25f)
        {
            attackId = id?.Trim();
            displayName = localizedName?.Trim();
            combatType = type;
            delivery = deliveryMode;
            shape = hitShape;
            center = hitCenter;
            projectileTravel = travel;
            rangeMultiplier = Mathf.Max(0.2f, rangeRatio);
            radius = Mathf.Max(0.05f, hitRadius);
            angle = Mathf.Clamp(fanAngle, 5f, 180f);
            lineWidth = Mathf.Max(0.05f, width);
            forwardOffset = 0f;
            maxTargets = Mathf.Clamp(targetLimit, 1, MaximumTargets);
            projectileCount = Mathf.Clamp(volleyCount, 1, MaximumProjectileCount);
            projectileSpreadAngle = Mathf.Clamp(spreadAngle, 0f, 90f);
            projectileSpeed = Mathf.Max(0.01f, projectileMoveSpeed);
            projectileLifetime = Mathf.Max(0.01f, projectileLife);
            projectileCollisionRadius = Mathf.Max(0.01f, projectileContactRadius);
            damageRatios = perHitDamageRatios == null || perHitDamageRatios.Length == 0
                ? new[] { 1f }
                : (float[])perHitDamageRatios.Clone();
            secondaryDamageRatio = Mathf.Clamp(secondaryRatio, 0.1f, 1f);
            repeatHitInterval = Mathf.Clamp(hitInterval, 0.01f, 0.3f);
            dashDistance = Mathf.Max(0f, advanceDistance);
            dashDuration = Mathf.Clamp(advanceDuration, 0.05f, 0.3f);
            stopOnFirstTarget = stopAfterFirstTarget;
            hitAreaVisibleDuration = Mathf.Clamp(areaVisibleDuration, 0.1f, 1f);
            progression = MonsterBasicAttackProgression.Simultaneous;
            progressionDuration = 0.25f;
            telegraphDelay = 0f;
            visualDuration = Mathf.Max(0.05f, areaVisibleDuration);
            magicDirection = MonsterBasicAttackMagicDirection.Forward;
            ConfigureModulesFromLegacy();
            EditorNormalizeModuleCombination();
        }

        public void EditorConfigureStepExtensions(
            MonsterBasicAttackCenter hitCenter,
            float centerForwardOffset,
            MonsterBasicAttackProgression hitProgression,
            float hitProgressionDuration,
            float warningDelay,
            float effectDuration,
            MonsterBasicAttackMagicDirection direction = MonsterBasicAttackMagicDirection.Forward)
        {
            center = hitCenter;
            forwardOffset = Mathf.Max(0f, centerForwardOffset);
            progression = hitProgression;
            progressionDuration = Mathf.Max(0f, hitProgressionDuration);
            telegraphDelay = Mathf.Max(0f, warningDelay);
            visualDuration = Mathf.Max(0.05f, effectDuration);
            magicDirection = direction;
        }

        public void EditorConfigureModules(
            MonsterBasicAttackDeliveryModule deliveryMode,
            MonsterBasicAttackCollisionModule collisionMode,
            MonsterBasicAttackSequenceModule sequenceMode,
            MonsterBasicAttackMovementModule movementMode,
            MonsterBasicAttackPresentationKind presentation)
        {
            deliveryModule = deliveryMode;
            collisionModule = collisionMode;
            sequenceModule = sequenceMode;
            movementModule = movementMode;
            presentationKind = presentation;
            EditorNormalizeModuleCombination();
        }

        public void EditorSetIdentity(string id, string localizedName)
        {
            attackId = id?.Trim();
            displayName = localizedName?.Trim();
        }

        public void EditorSetSweepDirection(MonsterBasicAttackSweepDirection direction)
        {
            sweepDirection = direction;
        }

        public void EditorSetBreathDuration(float duration)
        {
            breathDuration = Mathf.Max(0.01f, duration);
        }

        public void EditorSetProjectileCarrierPrefab(GameObject prefab)
        {
            projectileCarrierPrefab = prefab;
        }

        public void EditorSetDesignMemo(string memo)
        {
            designMemo = memo?.Trim();
        }

        public void EditorSetPresentationFeedback(
            MonsterFeedbackCue launch,
            MonsterFeedbackCue projectile,
            MonsterFeedbackCue impact)
        {
            launchFeedback = launch ?? new MonsterFeedbackCue();
            projectileFeedback = projectile ?? new MonsterFeedbackCue();
            impactFeedback = impact ?? new MonsterFeedbackCue();
        }

        public void EditorSetFeelFeedback(
            BasicAttackFeelCue launch,
            BasicAttackFeelCue projectile,
            BasicAttackFeelCue impact)
        {
            launchFeel = launch ?? new BasicAttackFeelCue();
            projectileFeel = projectile ?? new BasicAttackFeelCue();
            impactFeel = impact ?? new BasicAttackFeelCue();
        }

        public void EditorSetVfxSlots(IEnumerable<MonsterBasicAttackVfxSlot> slots)
        {
            var next = slots == null
                ? new List<MonsterBasicAttackVfxSlot>()
                : slots.Where(slot => slot != null)
                    .Select(slot => slot.EditorClone())
                    .ToList();
            inactiveVfxSlots ??= new List<MonsterBasicAttackVfxSlot>();
            foreach (var current in VfxSlots)
            {
                if (current == null || next.Any(candidate =>
                        string.Equals(candidate.SlotId, current.SlotId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                inactiveVfxSlots.RemoveAll(candidate => candidate != null &&
                    string.Equals(candidate.SlotId, current.SlotId, StringComparison.OrdinalIgnoreCase));
                inactiveVfxSlots.Add(current.EditorClone());
            }
            foreach (var active in next)
            {
                inactiveVfxSlots.RemoveAll(candidate => candidate != null &&
                    string.Equals(candidate.SlotId, active.SlotId, StringComparison.OrdinalIgnoreCase));
            }
            vfxSlots = next;
        }
#endif

        private void EnsureDamageRatioCount(int count)
        {
            count = Mathf.Clamp(count, 1, MaximumHitCount);
            if (damageRatios != null && damageRatios.Length == count)
            {
                return;
            }

            damageRatios = new float[count];
            var ratio = 1f / count;
            for (var index = 0; index < count; index++)
            {
                damageRatios[index] = ratio;
            }
        }

        private void ConfigureModulesFromLegacy()
        {
            deliveryModule = ResolveLegacyDeliveryModule();
            collisionModule = ResolveLegacyCollisionModule();
            sequenceModule = ResolveLegacySequenceModule();
            movementModule = delivery == MonsterBasicAttackDelivery.Dash
                ? MonsterBasicAttackMovementModule.Dash
                : MonsterBasicAttackMovementModule.None;
            presentationKind = ResolveLegacyPresentationKind();
            repeatImpactFeedback = presentationKind != MonsterBasicAttackPresentationKind.Breath;
            recipeVersion = CurrentRecipeVersion;
        }

        private void SynchronizeLegacyFields()
        {
            stopOnFirstTarget = collisionModule == MonsterBasicAttackCollisionModule.StopOnFirstTarget;
            if (movementModule == MonsterBasicAttackMovementModule.Dash)
            {
                delivery = MonsterBasicAttackDelivery.Dash;
            }
            else if (deliveryModule == MonsterBasicAttackDeliveryModule.TravelingArea)
            {
                delivery = MonsterBasicAttackDelivery.TravelingWave;
            }
            else if (deliveryModule == MonsterBasicAttackDeliveryModule.Projectile)
            {
                delivery = projectileTravel == MonsterBasicAttackProjectileTravel.Returning
                    ? MonsterBasicAttackDelivery.ReturningProjectile
                    : MonsterBasicAttackDelivery.Projectile;
            }
            else if (presentationKind == MonsterBasicAttackPresentationKind.Breath)
            {
                delivery = MonsterBasicAttackDelivery.Breath;
            }
            else if (presentationKind == MonsterBasicAttackPresentationKind.Beam)
            {
                delivery = MonsterBasicAttackDelivery.Beam;
            }
            else if (sequenceModule == MonsterBasicAttackSequenceModule.Burst)
            {
                delivery = MonsterBasicAttackDelivery.MultiHit;
            }
            else
            {
                delivery = combatType == MonsterCombatType.Ranged
                    ? MonsterBasicAttackDelivery.Instant
                    : MonsterBasicAttackDelivery.Contact;
            }
        }
    }
}
