using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectMT.Shared.Unit
{
    public enum MonsterActiveAttackPattern
    {
        Line,
        Cone,
        SelfCircle,
        FrontCircle,
        PiercingProjectile,
        ExplosiveProjectile,
        PiercingBeam,
        InstantMagic,
        SingleTarget,
        StandardProjectile,
        ReturningProjectile,
        Breath,
        TravelingWave,
        TargetCircle
    }

    public enum MonsterActiveAttackProgression
    {
        Instant,
        Forward,
        LeftToRight,
        RightToLeft,
        Outward
    }

    public enum MonsterActiveTargetPolicy { SameTarget, DifferentTarget }
    public enum MonsterActiveProjectileFormation { Single, Fan }
    public enum MonsterActiveInstantMagicTarget { SingleTarget, TargetArea }
    public enum MonsterActiveMagicDirection { GroundUp, SkyDown, Forward }
    public enum MonsterActiveDamageMultiplierMode { Fixed, RandomRange }
    public enum MonsterActiveStepStartMode { AfterPreviousComplete, AfterPreviousLaunch }
    public enum MonsterActiveHitEffectType { Knockback, Airborne, Stun, Bleed, Slow, Pull, Burn }

    [Serializable]
    public sealed class MonsterActiveHitEffect // 한 Step 적중 뒤 조립하는 상태 효과
    {
        public const float MaximumPullDistance = 2f;
        public const float MaximumPullDuration = 1.5f;

        [SerializeField] private MonsterActiveHitEffectType type;
        [SerializeField, Min(0f)] private float magnitude = 0.25f;
        [SerializeField, Min(0f)] private float duration = 0.35f;
        [SerializeField, Min(0f)] private float secondaryMagnitude;
        [SerializeField, Min(0.01f)] private float tickInterval = 0.5f;

        public MonsterActiveHitEffectType Type => type;
        public float Magnitude => Mathf.Max(0f, magnitude);
        public float Duration => Mathf.Max(0f, duration);
        public float SecondaryMagnitude => Mathf.Max(0f, secondaryMagnitude);
        public float TickInterval => Mathf.Max(0.01f, tickInterval);

        public bool TryValidate(out string error)
        {
            var finite = ActiveAttackValue.IsFiniteNonNegative(magnitude) &&
                         ActiveAttackValue.IsFiniteNonNegative(duration) &&
                         ActiveAttackValue.IsFiniteNonNegative(secondaryMagnitude) &&
                         ActiveAttackValue.IsFinitePositive(tickInterval);
            var contextual = type switch
            {
                MonsterActiveHitEffectType.Knockback => magnitude > 0f && duration > 0f,
                MonsterActiveHitEffectType.Airborne => magnitude > 0f && duration > 0f,
                MonsterActiveHitEffectType.Stun => duration > 0f,
                MonsterActiveHitEffectType.Bleed or MonsterActiveHitEffectType.Burn =>
                    magnitude > 0f && duration > 0f && tickInterval <= duration,
                MonsterActiveHitEffectType.Slow => magnitude > 0f && magnitude < 1f && duration > 0f,
                MonsterActiveHitEffectType.Pull => magnitude > 0f && magnitude <= MaximumPullDistance &&
                                                   duration > 0f && duration <= MaximumPullDuration,
                _ => false
            };
            if (!Enum.IsDefined(typeof(MonsterActiveHitEffectType), type) || !finite || !contextual)
            {
                error = $"타격 효과 값이 유효하지 않습니다. Effect={type}";
                return false;
            }
            error = string.Empty;
            return true;
        }

        public MonsterActiveHitEffect Clone()
        {
            return new MonsterActiveHitEffect
            {
                type = type,
                magnitude = magnitude,
                duration = duration,
                secondaryMagnitude = secondaryMagnitude,
                tickInterval = tickInterval
            };
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            MonsterActiveHitEffectType effectType,
            float primaryValue,
            float effectDuration,
            float secondaryValue = 0f,
            float interval = 0.5f)
        {
            type = effectType;
            magnitude = Mathf.Max(0f, primaryValue);
            duration = Mathf.Max(0f, effectDuration);
            secondaryMagnitude = Mathf.Max(0f, secondaryValue);
            tickInterval = Mathf.Max(0.01f, interval);
        }
#endif
    }

    [Serializable]
    public sealed class MonsterActiveAttackStepTuning // 이전 Draft 복구용 보관 데이터. 현재 Runtime에는 투영하지 않는다.
    {
        [SerializeField] private string stepId;
        [SerializeField, Min(0.05f)] private float damageScale = 1f;
        [SerializeField, Min(0.05f)] private float sizeScale = 1f;
        [SerializeField, Min(0.05f)] private float timingScale = 1f;
        [SerializeField, Range(0, 12)] private int projectileCountOverride;

        public string StepId => stepId?.Trim() ?? string.Empty;
        public float DamageScale => Mathf.Max(0.05f, damageScale);
        public float SizeScale => Mathf.Max(0.05f, sizeScale);
        public float TimingScale => Mathf.Max(0.05f, timingScale);
        public int ProjectileCountOverride => Mathf.Clamp(projectileCountOverride, 0, 12);

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(StepId) || !ActiveAttackValue.IsFinitePositive(damageScale) ||
                !ActiveAttackValue.IsFinitePositive(sizeScale) || !ActiveAttackValue.IsFinitePositive(timingScale) ||
                projectileCountOverride < 0 || projectileCountOverride > 12)
            {
                error = $"몬스터 전용 Step 튜닝이 유효하지 않습니다. Step={StepId}";
                return false;
            }
            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            float damageMultiplier = 1f,
            float areaMultiplier = 1f,
            float timeMultiplier = 1f,
            int projectileCount = 0)
        {
            stepId = id?.Trim();
            damageScale = Mathf.Max(0.05f, damageMultiplier);
            sizeScale = Mathf.Max(0.05f, areaMultiplier);
            timingScale = Mathf.Max(0.05f, timeMultiplier);
            projectileCountOverride = Mathf.Clamp(projectileCount, 0, 12);
        }
#endif
    }

    [Serializable]
    public sealed class MonsterActiveAttackStep // 한 항목이 실제 공격 한 번이며 반복 수를 갖지 않는다
    {
        [SerializeField] private string stepId = "step_01";
        [SerializeField] private string displayName = "일자 피해";
        [SerializeField] private MonsterActiveStepStartMode startMode;
        [SerializeField, Min(0f)] private float delayAfterPrevious;
        [SerializeField, Min(0.05f)] private float playbackSpeed = 1f;
        [SerializeField] private MonsterActiveTargetPolicy targetPolicy;
        [FormerlySerializedAs("teleportBeforeAttack")]
        [SerializeField] private bool dashBeforeAttack;
        [FormerlySerializedAs("teleportFrontDistance")]
        [SerializeField, Min(0f)] private float dashFrontDistance = 1f;
        [SerializeField, Range(0.05f, 0.3f)] private float dashDuration = 0.1f;
        [SerializeField] private MonsterActiveAttackPattern pattern;
        [SerializeField] private MonsterActiveAttackProgression progression;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [SerializeField] private MonsterActiveDamageMultiplierMode damageMultiplierMode;
        [SerializeField, Min(0f)] private float maximumDamageMultiplier = 1f;
        [SerializeField, Range(1, 32)] private int maxTargets = 8;
        [SerializeField, Min(0.05f)] private float range = 4f;
        [SerializeField, Min(0.05f)] private float width = 1.2f;
        [SerializeField, Min(0.05f)] private float radius = 1.8f;
        [SerializeField, Min(0f)] private float forwardOffset = 1.5f;
        [SerializeField, Range(5f, 180f)] private float angle = 70f;
        [SerializeField, Min(0f)] private float progressionDuration = 0.25f;
        [SerializeField, Min(0f)] private float telegraphDelay = 0.12f;
        [SerializeField, Min(0.05f)] private float visualDuration = 0.8f;
        [SerializeField, Range(0.1f, 1f)] private float hitAreaVisibleDuration = 0.42f;
        [SerializeField] private MonsterActiveProjectileFormation projectileFormation;
        [SerializeField, Range(1, 12)] private int projectileCount = 1;
        [SerializeField, Range(0f, 160f)] private float projectileFanAngle = 50f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 10f;
        [SerializeField, Min(0.01f)] private float projectileCollisionRadius = 0.25f;
        [SerializeField, Min(0.05f)] private float explosionRadius = 1.8f;
        [SerializeField] private MonsterBasicAttackProjectileTravel projectileTravel =
            MonsterBasicAttackProjectileTravel.Homing;
        [SerializeField, HideInInspector] private bool projectileTravelConfigured;
        [SerializeField] private MonsterActiveInstantMagicTarget instantMagicTarget;
        [SerializeField] private MonsterActiveMagicDirection magicDirection;
        [SerializeField] private float[] damageRatios = { 1f };
        [SerializeField, Range(0.1f, 1f)] private float secondaryDamageRatio = 1f;
        [SerializeField, Range(0.01f, 0.3f)] private float repeatHitInterval = 0.08f;
        [SerializeField, Min(0.05f)] private float projectileLifetime = 3f;
        [SerializeField] private bool repeatImpactFeedback = true;
        [SerializeField] private List<MonsterActiveHitEffect> hitEffects = new List<MonsterActiveHitEffect>();
        [SerializeField] private List<MonsterBasicAttackVfxSlot> attackBlockVfxSlots =
            new List<MonsterBasicAttackVfxSlot>();
        [SerializeField, HideInInspector] private List<MonsterBasicAttackVfxSlot> inactiveAttackBlockVfxSlots =
            new List<MonsterBasicAttackVfxSlot>();
        [SerializeField, HideInInspector] private List<MonsterActivePresentationSlot> presentationSlots =
            new List<MonsterActivePresentationSlot>();

        public string StepId => stepId?.Trim() ?? string.Empty;
        public string DisplayName => GetPatternDisplayName(pattern);
        public MonsterActiveStepStartMode StartMode => startMode;
        public float DelayAfterPrevious => Mathf.Max(0f, delayAfterPrevious);
        public float PlaybackSpeed => Mathf.Max(0.05f, playbackSpeed);
        public MonsterActiveTargetPolicy TargetPolicy => targetPolicy;
        public bool DashBeforeAttack => dashBeforeAttack;
        public float DashDistance => Mathf.Max(0f, dashFrontDistance);
        public float DashFrontDistance => Mathf.Max(0f, dashFrontDistance);
        public float DashDuration => Mathf.Clamp(dashDuration, 0.05f, 0.3f);
        public MonsterActiveAttackPattern Pattern => pattern;
        public MonsterActiveAttackProgression Progression => progression;
        public float DamageMultiplier => Mathf.Max(0f, damageMultiplier);
        public MonsterActiveDamageMultiplierMode DamageMultiplierMode => damageMultiplierMode;
        public float MaximumDamageMultiplier => damageMultiplierMode == MonsterActiveDamageMultiplierMode.RandomRange
            ? Mathf.Max(DamageMultiplier, maximumDamageMultiplier)
            : DamageMultiplier;
        public int MaxTargets => Mathf.Clamp(maxTargets, 1, 32);
        public float Range => Mathf.Max(0.05f, range);
        public float Width => Mathf.Max(0.05f, width);
        public float Radius => Mathf.Max(0.05f, radius);
        public float ForwardOffset => Mathf.Max(0f, forwardOffset);
        public float Angle => Mathf.Clamp(angle, 5f, 180f);
        public float ProgressionDuration => Mathf.Max(0f, progressionDuration);
        public float TelegraphDelay => Mathf.Max(0f, telegraphDelay);
        public float VisualDuration => Mathf.Max(0.05f, visualDuration);
        public float HitAreaVisibleDuration => Mathf.Clamp(hitAreaVisibleDuration, 0.1f, 1f);
        public MonsterActiveProjectileFormation ProjectileFormation => projectileFormation;
        public int ProjectileCount => projectileFormation == MonsterActiveProjectileFormation.Single
            ? 1
            : Mathf.Clamp(projectileCount, 2, 12);
        public float ProjectileFanAngle => Mathf.Clamp(projectileFanAngle, 0f, 160f);
        public float ProjectileSpeed => Mathf.Max(0.1f, projectileSpeed);
        public float ProjectileCollisionRadius => Mathf.Max(0.01f, projectileCollisionRadius);
        public float ExplosionRadius => Mathf.Max(0.05f, explosionRadius);
        public MonsterBasicAttackProjectileTravel ProjectileTravel => projectileTravelConfigured
            ? projectileTravel
            : pattern switch
            {
                MonsterActiveAttackPattern.StandardProjectile =>
                    MonsterBasicAttackProjectileTravel.Homing,
                MonsterActiveAttackPattern.ReturningProjectile =>
                    MonsterBasicAttackProjectileTravel.Returning,
                _ => MonsterBasicAttackProjectileTravel.Straight
            };
        public MonsterActiveInstantMagicTarget InstantMagicTarget => instantMagicTarget;
        public MonsterActiveMagicDirection MagicDirection => magicDirection;
        public IReadOnlyList<float> DamageRatios => damageRatios ??
            (IReadOnlyList<float>)Array.Empty<float>();
        public int HitCount => Mathf.Clamp(damageRatios?.Length ?? 0, 1, MonsterBasicAttackProfile.MaximumHitCount);
        public float SecondaryDamageRatio => Mathf.Clamp(secondaryDamageRatio, 0.1f, 1f);
        public float RepeatHitInterval => Mathf.Clamp(repeatHitInterval, 0.01f, 0.3f);
        public float ProjectileLifetime => Mathf.Max(0.05f, projectileLifetime);
        public bool RepeatImpactFeedback => repeatImpactFeedback;
        public IReadOnlyList<MonsterActiveHitEffect> HitEffects => hitEffects ??
            (IReadOnlyList<MonsterActiveHitEffect>)Array.Empty<MonsterActiveHitEffect>();
        public IReadOnlyList<MonsterBasicAttackVfxSlot> AttackBlockVfxSlots => attackBlockVfxSlots ??
            (IReadOnlyList<MonsterBasicAttackVfxSlot>)Array.Empty<MonsterBasicAttackVfxSlot>();
        public IReadOnlyList<MonsterBasicAttackVfxSlot> InactiveAttackBlockVfxSlots =>
            inactiveAttackBlockVfxSlots ??
            (IReadOnlyList<MonsterBasicAttackVfxSlot>)Array.Empty<MonsterBasicAttackVfxSlot>();
        public IReadOnlyList<MonsterActivePresentationSlot> PresentationSlots => presentationSlots ??
            (IReadOnlyList<MonsterActivePresentationSlot>)Array.Empty<MonsterActivePresentationSlot>();
        public bool IsProjectile => pattern is MonsterActiveAttackPattern.PiercingProjectile or
            MonsterActiveAttackPattern.ExplosiveProjectile or
            MonsterActiveAttackPattern.StandardProjectile or
            MonsterActiveAttackPattern.ReturningProjectile or
            MonsterActiveAttackPattern.TravelingWave;

        public static bool SupportsEditableMultiHit(MonsterActiveAttackPattern attackPattern) =>
            attackPattern is not MonsterActiveAttackPattern.PiercingProjectile and
                not MonsterActiveAttackPattern.ExplosiveProjectile and
                not MonsterActiveAttackPattern.StandardProjectile and
                not MonsterActiveAttackPattern.ReturningProjectile and
                not MonsterActiveAttackPattern.PiercingBeam and
                not MonsterActiveAttackPattern.TravelingWave;

        public float ResolveDamageMultiplier(float random01) =>
            damageMultiplierMode == MonsterActiveDamageMultiplierMode.RandomRange
                ? Mathf.Lerp(DamageMultiplier, MaximumDamageMultiplier, Mathf.Clamp01(random01))
                : DamageMultiplier;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(StepId) || string.IsNullOrWhiteSpace(DisplayName) ||
                !ActiveAttackValue.UsesSafeId(StepId) ||
                !Enum.IsDefined(typeof(MonsterActiveTargetPolicy), targetPolicy) ||
                !Enum.IsDefined(typeof(MonsterActiveAttackPattern), pattern) ||
                !Enum.IsDefined(typeof(MonsterActiveAttackProgression), progression) ||
                !Enum.IsDefined(typeof(MonsterActiveDamageMultiplierMode), damageMultiplierMode) ||
                !Enum.IsDefined(typeof(MonsterActiveStepStartMode), startMode) ||
                !ActiveAttackValue.IsFiniteNonNegative(delayAfterPrevious) ||
                !ActiveAttackValue.IsFinitePositive(playbackSpeed) ||
                !ActiveAttackValue.IsFinitePositive(dashDuration) || dashDuration < 0.05f || dashDuration > 0.3f ||
                !ActiveAttackValue.IsFiniteNonNegative(damageMultiplier) ||
                !ActiveAttackValue.IsFiniteNonNegative(maximumDamageMultiplier) ||
                damageMultiplierMode == MonsterActiveDamageMultiplierMode.RandomRange &&
                maximumDamageMultiplier < damageMultiplier ||
                !ActiveAttackValue.IsFinitePositive(range) || !ActiveAttackValue.IsFinitePositive(width) ||
                !ActiveAttackValue.IsFinitePositive(radius) || !ActiveAttackValue.IsFiniteNonNegative(forwardOffset) ||
                !ActiveAttackValue.IsFiniteNonNegative(progressionDuration) ||
                !ActiveAttackValue.IsFiniteNonNegative(telegraphDelay) ||
                !ActiveAttackValue.IsFinitePositive(visualDuration) ||
                !ActiveAttackValue.IsFinitePositive(hitAreaVisibleDuration) ||
                hitAreaVisibleDuration < 0.1f || hitAreaVisibleDuration > 1f ||
                maxTargets < 1 || maxTargets > 32 ||
                angle < 5f || angle > 180f || damageRatios == null || damageRatios.Length < 1 ||
                damageRatios.Length > MonsterBasicAttackProfile.MaximumHitCount ||
                !ActiveAttackValue.IsFinitePositive(secondaryDamageRatio) ||
                secondaryDamageRatio > 1f || !ActiveAttackValue.IsFinitePositive(repeatHitInterval) ||
                !ActiveAttackValue.IsFinitePositive(projectileLifetime))
            {
                error = $"공격 Step 기본값이 유효하지 않습니다. Step={StepId}";
                return false;
            }
            var damageBudget = 0f;
            for (var index = 0; index < damageRatios.Length; index++)
            {
                if (!ActiveAttackValue.IsFinitePositive(damageRatios[index]))
                {
                    error = $"공격 Step 연타 피해 비율이 유효하지 않습니다. Step={StepId}, Hit={index + 1}";
                    return false;
                }
                damageBudget += damageRatios[index];
            }
            if (Mathf.Abs(damageBudget - 1f) > 0.001f)
            {
                error = $"공격 Step 연타 피해 합계는 1이어야 합니다. Step={StepId}, Total={damageBudget:0.###}";
                return false;
            }
            if (pattern == MonsterActiveAttackPattern.ReturningProjectile && HitCount != 2)
            {
                error = $"왕복 투사체는 기본공격 공용 계약상 왕복 2타여야 합니다. Step={StepId}";
                return false;
            }
            if (pattern == MonsterActiveAttackPattern.Breath && HitCount < 2)
            {
                error = $"브레스는 기본공격 공용 계약상 2타 이상의 연속 판정이 필요합니다. Step={StepId}";
                return false;
            }
            if (!SupportsEditableMultiHit(pattern) &&
                pattern != MonsterActiveAttackPattern.ReturningProjectile && HitCount != 1)
            {
                error = $"이 공격 형태는 기본공격 공용 계약상 단일 판정만 지원합니다. Step={StepId}, Pattern={pattern}";
                return false;
            }
            if (PresentationSlots.Count > 0)
            {
                error = $"구형 액티브 전용 계약이 남아 있습니다. Step={StepId}";
                return false;
            }
            var blockSlotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < AttackBlockVfxSlots.Count; index++)
            {
                var slot = AttackBlockVfxSlots[index];
                var slotError = "공용 공격 블록 공간이 비어 있습니다.";
                if (slot == null || !slot.TryValidate(out slotError) || !blockSlotIds.Add(slot.SlotId))
                {
                    error = $"공용 공격 블록 VFX/SFX 공간 {index + 1}이 유효하지 않습니다. Step={StepId}, Detail={slotError}";
                    return false;
                }
            }
            if (!SupportsProgression(pattern, progression))
            {
                error = $"공격 형태가 지원하지 않는 진행 방식입니다. Step={StepId}, Pattern={pattern}, Progression={progression}";
                return false;
            }
            var requiresFanAngle = projectileFormation == MonsterActiveProjectileFormation.Fan &&
                                   projectileCount > 1;
            if (IsProjectile && (!Enum.IsDefined(typeof(MonsterActiveProjectileFormation), projectileFormation) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackProjectileTravel), projectileTravel) ||
                projectileCount < 1 || projectileCount > 12 || !ActiveAttackValue.IsFinitePositive(projectileSpeed) ||
                !ActiveAttackValue.IsFinitePositive(projectileCollisionRadius) || projectileFanAngle < 0f ||
                projectileFanAngle > 160f || (requiresFanAngle && projectileFanAngle < 1f)))
            {
                error = $"투사체 설정이 유효하지 않습니다. Step={StepId}";
                return false;
            }
            if (pattern == MonsterActiveAttackPattern.ExplosiveProjectile &&
                !ActiveAttackValue.IsFinitePositive(explosionRadius))
            {
                error = $"폭발 투사체의 폭발 반경이 필요합니다. Step={StepId}";
                return false;
            }
            if (pattern == MonsterActiveAttackPattern.InstantMagic &&
                (!Enum.IsDefined(typeof(MonsterActiveInstantMagicTarget), instantMagicTarget) ||
                 !Enum.IsDefined(typeof(MonsterActiveMagicDirection), magicDirection)))
            {
                error = $"즉발 마법의 대상/등장 방향이 유효하지 않습니다. Step={StepId}";
                return false;
            }
            for (var index = 0; index < HitEffects.Count; index++)
            {
                var effect = HitEffects[index];
                var effectError = "타격 효과가 비어 있습니다.";
                if (effect == null || !effect.TryValidate(out effectError))
                {
                    error = $"타격 효과 {index + 1}이 유효하지 않습니다. Step={StepId}, Detail={effectError}";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        public string BuildSummary()
        {
            var targetLabel = targetPolicy == MonsterActiveTargetPolicy.SameTarget ? "같은 대상" : "다른 대상";
            var dashLabel = dashBeforeAttack ? $" · 돌진 거리 {DashDistance:0.#}m" : string.Empty;
            var effectLabel = HitEffects.Count > 0 ? $" · 타격효과 {HitEffects.Count}" : string.Empty;
            var damageLabel = damageMultiplierMode == MonsterActiveDamageMultiplierMode.RandomRange
                ? $"{DamageMultiplier:0.##}~{MaximumDamageMultiplier:0.##}배"
                : $"{DamageMultiplier:0.##}배";
            var startLabel = startMode == MonsterActiveStepStartMode.AfterPreviousLaunch
                ? $" · 발사 후 {DelayAfterPrevious:0.###}초 체인"
                : string.Empty;
            return $"{DisplayName} / {Progression} · 피해 {damageLabel} · 속도 {PlaybackSpeed:0.##}배 · {targetLabel}{startLabel}{dashLabel}{effectLabel}";
        }

        [Obsolete("몬스터별 Step 수치 배율은 더 이상 Runtime에 투영되지 않습니다. 프리셋을 복사해 수정하세요.")]
        public MonsterActiveAttackStep CloneWithTuning(MonsterActiveAttackStepTuning tuning)
        {
            var clone = Clone();
            if (tuning == null || !string.Equals(tuning.StepId, StepId, StringComparison.OrdinalIgnoreCase))
            {
                return clone;
            }
            clone.damageMultiplier *= tuning.DamageScale;
            clone.range *= tuning.SizeScale;
            clone.width *= tuning.SizeScale;
            clone.radius *= tuning.SizeScale;
            clone.forwardOffset *= tuning.SizeScale;
            clone.projectileCollisionRadius *= tuning.SizeScale;
            clone.explosionRadius *= tuning.SizeScale;
            clone.delayAfterPrevious *= tuning.TimingScale;
            clone.progressionDuration *= tuning.TimingScale;
            clone.telegraphDelay *= tuning.TimingScale;
            clone.visualDuration *= tuning.TimingScale;
            clone.projectileSpeed /= tuning.TimingScale;
            if (tuning.ProjectileCountOverride > 0)
            {
                clone.projectileCount = tuning.ProjectileCountOverride;
                clone.projectileFormation = tuning.ProjectileCountOverride <= 1
                    ? MonsterActiveProjectileFormation.Single
                    : MonsterActiveProjectileFormation.Fan;
            }
            return clone;
        }

        public MonsterActiveAttackStep Clone()
        {
            var clone = (MonsterActiveAttackStep)MemberwiseClone();
            clone.hitEffects = new List<MonsterActiveHitEffect>();
            for (var index = 0; index < HitEffects.Count; index++)
            {
                if (HitEffects[index] != null) clone.hitEffects.Add(HitEffects[index].Clone());
            }
            clone.presentationSlots = new List<MonsterActivePresentationSlot>();
            for (var index = 0; index < PresentationSlots.Count; index++)
            {
                if (PresentationSlots[index] != null) clone.presentationSlots.Add(PresentationSlots[index].Clone());
            }
            clone.attackBlockVfxSlots = AttackBlockVfxSlots
                .Where(slot => slot != null)
                .Select(slot => slot.Clone())
                .ToList();
            clone.inactiveAttackBlockVfxSlots = InactiveAttackBlockVfxSlots
                .Where(slot => slot != null)
                .Select(slot => slot.Clone())
                .ToList();
            clone.damageRatios = damageRatios == null ? new[] { 1f } : (float[])damageRatios.Clone();
            return clone;
        }

        public static bool SupportsProgression(
            MonsterActiveAttackPattern attackPattern,
            MonsterActiveAttackProgression attackProgression)
        {
            return attackPattern switch
            {
                MonsterActiveAttackPattern.Line =>
                    attackProgression == MonsterActiveAttackProgression.Instant ||
                    attackProgression == MonsterActiveAttackProgression.Forward,
                MonsterActiveAttackPattern.Cone =>
                    attackProgression == MonsterActiveAttackProgression.Instant ||
                    attackProgression == MonsterActiveAttackProgression.Forward ||
                    attackProgression == MonsterActiveAttackProgression.LeftToRight ||
                    attackProgression == MonsterActiveAttackProgression.RightToLeft,
                MonsterActiveAttackPattern.SelfCircle or MonsterActiveAttackPattern.FrontCircle or
                    MonsterActiveAttackPattern.TargetCircle =>
                    attackProgression == MonsterActiveAttackProgression.Instant ||
                    attackProgression == MonsterActiveAttackProgression.Outward,
                MonsterActiveAttackPattern.Breath =>
                    attackProgression == MonsterActiveAttackProgression.Instant ||
                    attackProgression == MonsterActiveAttackProgression.Forward,
                _ => attackProgression == MonsterActiveAttackProgression.Instant
            };
        }

        public static string GetPatternDisplayName(MonsterActiveAttackPattern attackPattern) =>
            attackPattern switch
            {
                MonsterActiveAttackPattern.Line => "일자 피해",
                MonsterActiveAttackPattern.Cone => "부채꼴 피해",
                MonsterActiveAttackPattern.SelfCircle => "내 주변 원형",
                MonsterActiveAttackPattern.FrontCircle => "내 앞 원형",
                MonsterActiveAttackPattern.PiercingProjectile => "관통 투사체",
                MonsterActiveAttackPattern.ExplosiveProjectile => "폭발 투사체",
                MonsterActiveAttackPattern.PiercingBeam => "관통 빔",
                MonsterActiveAttackPattern.InstantMagic => "즉발 마법",
                MonsterActiveAttackPattern.SingleTarget => "단일 타격",
                MonsterActiveAttackPattern.StandardProjectile => "일반 투사체",
                MonsterActiveAttackPattern.ReturningProjectile => "왕복 투사체",
                MonsterActiveAttackPattern.Breath => "원뿔 브레스",
                MonsterActiveAttackPattern.TravelingWave => "이동 파동",
                MonsterActiveAttackPattern.TargetCircle => "대상 중심 원형",
                _ => "공격"
            };

        public static string GetCanonicalStepId(int index) =>
            $"step_{Mathf.Max(0, index) + 1:00}";

        public bool HasCanonicalIdentity(int index) =>
            string.Equals(StepId, GetCanonicalStepId(index), StringComparison.Ordinal) &&
            string.Equals(DisplayName, GetPatternDisplayName(pattern), StringComparison.Ordinal);

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string title,
            MonsterActiveAttackPattern attackPattern,
            float power = 1f,
            float startDelay = 0f,
            MonsterActiveTargetPolicy targetSelection = MonsterActiveTargetPolicy.SameTarget,
            MonsterActiveAttackProgression attackProgression = MonsterActiveAttackProgression.Instant,
            MonsterActiveHitEffect[] effects = null,
            float stepPlaybackSpeed = 1f,
            MonsterActiveStepStartMode nextStepStartMode = MonsterActiveStepStartMode.AfterPreviousComplete)
        {
            stepId = id?.Trim();
            displayName = title?.Trim();
            pattern = attackPattern;
            damageMultiplier = Mathf.Max(0f, power);
            damageMultiplierMode = MonsterActiveDamageMultiplierMode.Fixed;
            maximumDamageMultiplier = damageMultiplier;
            startMode = nextStepStartMode;
            delayAfterPrevious = Mathf.Max(0f, startDelay);
            playbackSpeed = Mathf.Max(0.05f, stepPlaybackSpeed);
            targetPolicy = targetSelection;
            progression = SupportsProgression(attackPattern, attackProgression)
                ? attackProgression
                : MonsterActiveAttackProgression.Instant;
            hitEffects = effects == null
                ? new List<MonsterActiveHitEffect>()
                : new List<MonsterActiveHitEffect>(effects);
            // 공격 형태별 계약의 단일 원본은 Editor의 VFX 계약 템플릿이다.
            // 여기서 구형 공통 슬롯을 만들면 Workshop/API 생성 경로가 서로 달라진다.
            presentationSlots = new List<MonsterActivePresentationSlot>();
            displayName = GetPatternDisplayName(pattern);
            EditorNormalizeHitSequenceForPattern();
        }

        public void EditorConfigureGeometry(
            float attackRange,
            float attackWidth,
            float attackRadius,
            float centerOffset,
            float attackAngle,
            int targetLimit,
            float sweepDuration = 0.25f,
            float warningDelay = 0.12f,
            float effectVisualDuration = 0.8f,
            float areaVisibleDuration = 0.42f)
        {
            range = Mathf.Max(0.05f, attackRange);
            width = Mathf.Max(0.05f, attackWidth);
            radius = Mathf.Max(0.05f, attackRadius);
            forwardOffset = Mathf.Max(0f, centerOffset);
            angle = Mathf.Clamp(attackAngle, 5f, 180f);
            maxTargets = Mathf.Clamp(targetLimit, 1, 32);
            progressionDuration = Mathf.Max(0f, sweepDuration);
            telegraphDelay = Mathf.Max(0f, warningDelay);
            visualDuration = Mathf.Max(0.05f, effectVisualDuration);
            hitAreaVisibleDuration = Mathf.Clamp(areaVisibleDuration, 0.1f, 1f);
        }

        public void EditorConfigureProjectile(
            MonsterActiveProjectileFormation formation,
            int count,
            float fanAngle,
            float speed,
            float collisionRadius,
            float blastRadius = 1.8f,
            MonsterBasicAttackProjectileTravel travel = MonsterBasicAttackProjectileTravel.Straight,
            float lifetime = 3f)
        {
            projectileFormation = formation;
            projectileCount = formation == MonsterActiveProjectileFormation.Single ? 1 : Mathf.Clamp(count, 2, 12);
            projectileFanAngle = Mathf.Clamp(fanAngle, 0f, 160f);
            projectileSpeed = Mathf.Max(0.1f, speed);
            projectileCollisionRadius = Mathf.Max(0.01f, collisionRadius);
            explosionRadius = Mathf.Max(0.05f, blastRadius);
            projectileTravel = travel;
            projectileLifetime = Mathf.Max(0.05f, lifetime);
            projectileTravelConfigured = true;
        }

        public void EditorConfigureInstantMagic(
            MonsterActiveInstantMagicTarget targetMode,
            MonsterActiveMagicDirection direction)
        {
            instantMagicTarget = targetMode;
            magicDirection = direction;
        }

        public void EditorConfigureDash(
            bool enabled,
            float frontDistance,
            float arrivalFeedbackDuration = 0.1f)
        {
            dashBeforeAttack = enabled;
            dashFrontDistance = Mathf.Max(0f, frontDistance);
            dashDuration = Mathf.Clamp(arrivalFeedbackDuration, 0.05f, 0.3f);
        }

        [Obsolete("순간이동과 돌진은 하나의 돌진 계약으로 통합되었습니다.")]
        public void EditorConfigureTeleport(bool enabled, float frontDistance)
        {
            EditorConfigureDash(enabled, frontDistance);
        }

        public void EditorSetPlaybackSpeed(float speed)
        {
            playbackSpeed = Mathf.Max(0.05f, speed);
        }

        public void EditorConfigureStart(
            MonsterActiveStepStartMode mode,
            float delay)
        {
            startMode = mode;
            delayAfterPrevious = Mathf.Max(0f, delay);
        }

        public void EditorConfigureDamageRange(bool useRange, float maximumPower)
        {
            damageMultiplierMode = useRange
                ? MonsterActiveDamageMultiplierMode.RandomRange
                : MonsterActiveDamageMultiplierMode.Fixed;
            maximumDamageMultiplier = useRange
                ? Mathf.Max(damageMultiplier, maximumPower)
                : damageMultiplier;
        }

        public void EditorNormalizeIdentity(int index)
        {
            stepId = GetCanonicalStepId(index);
            displayName = GetPatternDisplayName(pattern);
            if (!ActiveAttackValue.IsFinitePositive(dashDuration)) dashDuration = 0.1f;
            dashDuration = Mathf.Clamp(dashDuration, 0.05f, 0.3f);
            if (!ActiveAttackValue.IsFinitePositive(hitAreaVisibleDuration))
                hitAreaVisibleDuration = 0.42f;
            hitAreaVisibleDuration = Mathf.Clamp(hitAreaVisibleDuration, 0.1f, 1f);
        }

        public void EditorSetPresentationSlots(IEnumerable<MonsterActivePresentationSlot> slots)
        {
            presentationSlots = slots == null
                ? new List<MonsterActivePresentationSlot>()
                : new List<MonsterActivePresentationSlot>(slots.Where(slot => slot != null));
        }

        public void EditorSetAttackBlockVfxSlots(IEnumerable<MonsterBasicAttackVfxSlot> slots)
        {
            var next = slots == null
                ? new List<MonsterBasicAttackVfxSlot>()
                : slots.Where(slot => slot != null)
                    .Select(slot => slot.EditorClone())
                    .ToList();
            inactiveAttackBlockVfxSlots ??= new List<MonsterBasicAttackVfxSlot>();
            foreach (var current in AttackBlockVfxSlots)
            {
                if (current == null || next.Any(candidate =>
                        string.Equals(candidate.SlotId, current.SlotId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                inactiveAttackBlockVfxSlots.RemoveAll(candidate => candidate != null &&
                    string.Equals(candidate.SlotId, current.SlotId, StringComparison.OrdinalIgnoreCase));
                inactiveAttackBlockVfxSlots.Add(current.EditorClone());
            }
            foreach (var active in next)
            {
                inactiveAttackBlockVfxSlots.RemoveAll(candidate => candidate != null &&
                    string.Equals(candidate.SlotId, active.SlotId, StringComparison.OrdinalIgnoreCase));
            }
            attackBlockVfxSlots = next;
        }

        public void EditorConfigureHitSequence(
            float[] perHitRatios,
            float secondaryRatio = 1f,
            float interval = 0.08f,
            bool replayImpact = true)
        {
            damageRatios = perHitRatios == null || perHitRatios.Length == 0
                ? new[] { 1f }
                : (float[])perHitRatios.Clone();
            secondaryDamageRatio = Mathf.Clamp(secondaryRatio, 0.1f, 1f);
            repeatHitInterval = Mathf.Clamp(interval, 0.01f, 0.3f);
            repeatImpactFeedback = replayImpact;
        }

        public void EditorNormalizeHitSequenceForPattern()
        {
            if (pattern == MonsterActiveAttackPattern.ReturningProjectile)
            {
                if (HitCount != 2) damageRatios = new[] { 0.6f, 0.4f };
                return;
            }
            if (pattern == MonsterActiveAttackPattern.Breath)
            {
                if (HitCount < 2) damageRatios = new[] { 1f / 3f, 1f / 3f, 1f / 3f };
                return;
            }
            if (!SupportsEditableMultiHit(pattern) && HitCount != 1)
            {
                damageRatios = new[] { 1f };
            }
        }

        public bool EditorCopyAttackBlockFrom(
            MonsterBasicAttackProfile source,
            out string error)
        {
            if (source == null)
            {
                error = "복사할 기본공격 프리셋이 없습니다.";
                return false;
            }
            if (!source.TryValidate(out error)) return false;
            pattern = source.PresentationKind switch
            {
                MonsterBasicAttackPresentationKind.Sweep => MonsterActiveAttackPattern.Cone,
                MonsterBasicAttackPresentationKind.Thrust => MonsterActiveAttackPattern.Line,
                MonsterBasicAttackPresentationKind.Slam when source.Center == MonsterBasicAttackCenter.Source =>
                    MonsterActiveAttackPattern.SelfCircle,
                MonsterBasicAttackPresentationKind.Slam when source.Center == MonsterBasicAttackCenter.Forward =>
                    MonsterActiveAttackPattern.FrontCircle,
                MonsterBasicAttackPresentationKind.Slam => MonsterActiveAttackPattern.TargetCircle,
                MonsterBasicAttackPresentationKind.Explosion =>
                    MonsterActiveAttackPattern.ExplosiveProjectile,
                MonsterBasicAttackPresentationKind.Instant => MonsterActiveAttackPattern.InstantMagic,
                MonsterBasicAttackPresentationKind.Returning =>
                    MonsterActiveAttackPattern.ReturningProjectile,
                MonsterBasicAttackPresentationKind.Breath => MonsterActiveAttackPattern.Breath,
                MonsterBasicAttackPresentationKind.Beam => MonsterActiveAttackPattern.PiercingBeam,
                MonsterBasicAttackPresentationKind.Wave => MonsterActiveAttackPattern.TravelingWave,
                MonsterBasicAttackPresentationKind.Shot when
                    source.CollisionModule == MonsterBasicAttackCollisionModule.Pierce =>
                    MonsterActiveAttackPattern.PiercingProjectile,
                MonsterBasicAttackPresentationKind.Shot or MonsterBasicAttackPresentationKind.Scatter =>
                    MonsterActiveAttackPattern.StandardProjectile,
                _ => MonsterActiveAttackPattern.SingleTarget
            };
            displayName = GetPatternDisplayName(pattern);
            range = source.RangeMultiplier;
            radius = source.Radius;
            explosionRadius = source.Radius;
            angle = source.Angle;
            width = source.LineWidth;
            forwardOffset = source.ForwardOffset;
            maxTargets = source.MaxTargets;
            progression = source.Progression switch
            {
                MonsterBasicAttackProgression.Forward => MonsterActiveAttackProgression.Forward,
                MonsterBasicAttackProgression.LeftToRight => MonsterActiveAttackProgression.LeftToRight,
                MonsterBasicAttackProgression.RightToLeft => MonsterActiveAttackProgression.RightToLeft,
                MonsterBasicAttackProgression.Outward => MonsterActiveAttackProgression.Outward,
                _ => MonsterActiveAttackProgression.Instant
            };
            progressionDuration = source.ProgressionDuration;
            telegraphDelay = source.TelegraphDelay;
            visualDuration = pattern == MonsterActiveAttackPattern.Breath
                ? source.BreathDuration
                : source.VisualDuration;
            hitAreaVisibleDuration = source.HitAreaVisibleDuration;
            projectileFormation = source.ProjectileCount > 1
                ? MonsterActiveProjectileFormation.Fan
                : MonsterActiveProjectileFormation.Single;
            projectileCount = source.ProjectileCount;
            projectileFanAngle = source.ProjectileSpreadAngle;
            projectileSpeed = source.ProjectileSpeed;
            projectileLifetime = source.ProjectileLifetime;
            projectileCollisionRadius = source.ProjectileCollisionRadius;
            projectileTravel = source.ProjectileTravel;
            projectileTravelConfigured = true;
            dashBeforeAttack = source.MovementModule == MonsterBasicAttackMovementModule.Dash;
            dashFrontDistance = source.DashDistance;
            dashDuration = source.DashDuration;
            damageRatios = source.DamageRatios.ToArray();
            secondaryDamageRatio = source.SecondaryDamageRatio;
            repeatHitInterval = source.RepeatHitInterval;
            repeatImpactFeedback = source.RepeatImpactFeedback;
            magicDirection = source.MagicDirection switch
            {
                MonsterBasicAttackMagicDirection.GroundUp => MonsterActiveMagicDirection.GroundUp,
                MonsterBasicAttackMagicDirection.SkyDown => MonsterActiveMagicDirection.SkyDown,
                _ => MonsterActiveMagicDirection.Forward
            };
            EditorSetAttackBlockVfxSlots(source.VfxSlots);
            EditorNormalizeHitSequenceForPattern();
            error = string.Empty;
            return true;
        }

        public void EditorCompileAttackBlock(MonsterBasicAttackProfile destination)
        {
            if (destination == null) return;
            var usesProjectileActor = pattern is MonsterActiveAttackPattern.PiercingProjectile or
                MonsterActiveAttackPattern.ExplosiveProjectile or
                MonsterActiveAttackPattern.StandardProjectile or
                MonsterActiveAttackPattern.ReturningProjectile;
            var usesDeliveryVisual = usesProjectileActor ||
                                     pattern == MonsterActiveAttackPattern.TravelingWave;
            var delivery = pattern switch
            {
                MonsterActiveAttackPattern.ReturningProjectile =>
                    MonsterBasicAttackDelivery.ReturningProjectile,
                MonsterActiveAttackPattern.Breath => MonsterBasicAttackDelivery.Breath,
                MonsterActiveAttackPattern.PiercingBeam => MonsterBasicAttackDelivery.Beam,
                MonsterActiveAttackPattern.TravelingWave => MonsterBasicAttackDelivery.TravelingWave,
                _ when usesProjectileActor => MonsterBasicAttackDelivery.Projectile,
                _ when HitCount > 1 => MonsterBasicAttackDelivery.MultiHit,
                MonsterActiveAttackPattern.InstantMagic => MonsterBasicAttackDelivery.Instant,
                _ => MonsterBasicAttackDelivery.Contact
            };
            var shape = pattern switch
            {
                MonsterActiveAttackPattern.Cone or MonsterActiveAttackPattern.Breath =>
                    MonsterBasicAttackShape.Fan,
                MonsterActiveAttackPattern.Line or MonsterActiveAttackPattern.PiercingBeam or
                    MonsterActiveAttackPattern.PiercingProjectile or
                    MonsterActiveAttackPattern.ReturningProjectile or
                    MonsterActiveAttackPattern.TravelingWave => MonsterBasicAttackShape.Line,
                MonsterActiveAttackPattern.SelfCircle or MonsterActiveAttackPattern.FrontCircle or
                    MonsterActiveAttackPattern.TargetCircle or
                    MonsterActiveAttackPattern.ExplosiveProjectile => MonsterBasicAttackShape.Circle,
                MonsterActiveAttackPattern.StandardProjectile when ProjectileCount > 1 =>
                    MonsterBasicAttackShape.Fan,
                MonsterActiveAttackPattern.InstantMagic when
                    instantMagicTarget == MonsterActiveInstantMagicTarget.TargetArea =>
                    MonsterBasicAttackShape.Circle,
                _ => MonsterBasicAttackShape.Single
            };
            var center = pattern switch
            {
                MonsterActiveAttackPattern.SelfCircle => MonsterBasicAttackCenter.Source,
                MonsterActiveAttackPattern.FrontCircle => MonsterBasicAttackCenter.Forward,
                MonsterActiveAttackPattern.TargetCircle => MonsterBasicAttackCenter.PrimaryTarget,
                MonsterActiveAttackPattern.Line or MonsterActiveAttackPattern.Cone or
                MonsterActiveAttackPattern.PiercingProjectile or
                    MonsterActiveAttackPattern.ReturningProjectile or
                    MonsterActiveAttackPattern.Breath or MonsterActiveAttackPattern.PiercingBeam or
                    MonsterActiveAttackPattern.TravelingWave => MonsterBasicAttackCenter.Source,
                MonsterActiveAttackPattern.StandardProjectile when ProjectileCount > 1 =>
                    MonsterBasicAttackCenter.Source,
                _ => MonsterBasicAttackCenter.PrimaryTarget
            };
            var presentation = pattern switch
            {
                MonsterActiveAttackPattern.ReturningProjectile =>
                    MonsterBasicAttackPresentationKind.Returning,
                MonsterActiveAttackPattern.Breath => MonsterBasicAttackPresentationKind.Breath,
                _ when HitCount > 1 => MonsterBasicAttackPresentationKind.Combo,
                MonsterActiveAttackPattern.Cone => MonsterBasicAttackPresentationKind.Sweep,
                MonsterActiveAttackPattern.Line => MonsterBasicAttackPresentationKind.Thrust,
                MonsterActiveAttackPattern.SelfCircle or MonsterActiveAttackPattern.FrontCircle or
                    MonsterActiveAttackPattern.TargetCircle =>
                    MonsterBasicAttackPresentationKind.Slam,
                MonsterActiveAttackPattern.PiercingProjectile when ProjectileCount > 1 =>
                    MonsterBasicAttackPresentationKind.Scatter,
                MonsterActiveAttackPattern.PiercingProjectile => MonsterBasicAttackPresentationKind.Shot,
                MonsterActiveAttackPattern.ExplosiveProjectile => MonsterBasicAttackPresentationKind.Explosion,
                MonsterActiveAttackPattern.StandardProjectile when ProjectileCount > 1 =>
                    MonsterBasicAttackPresentationKind.Scatter,
                MonsterActiveAttackPattern.StandardProjectile => MonsterBasicAttackPresentationKind.Shot,
                MonsterActiveAttackPattern.PiercingBeam => MonsterBasicAttackPresentationKind.Beam,
                MonsterActiveAttackPattern.TravelingWave => MonsterBasicAttackPresentationKind.Wave,
                MonsterActiveAttackPattern.InstantMagic => MonsterBasicAttackPresentationKind.Instant,
                _ when DashBeforeAttack => MonsterBasicAttackPresentationKind.Dash,
                _ => MonsterBasicAttackPresentationKind.Contact
            };
            var collision = pattern switch
            {
                MonsterActiveAttackPattern.PiercingProjectile => MonsterBasicAttackCollisionModule.Pierce,
                MonsterActiveAttackPattern.ExplosiveProjectile => MonsterBasicAttackCollisionModule.AreaImpact,
                MonsterActiveAttackPattern.StandardProjectile =>
                    MonsterBasicAttackCollisionModule.StopOnFirstTarget,
                MonsterActiveAttackPattern.ReturningProjectile or
                    MonsterActiveAttackPattern.TravelingWave => MonsterBasicAttackCollisionModule.PassThrough,
                _ => MonsterBasicAttackCollisionModule.DirectResolve
            };
            var sequence = pattern == MonsterActiveAttackPattern.ReturningProjectile
                ? MonsterBasicAttackSequenceModule.ReturnPasses
                : HitCount > 1 || pattern == MonsterActiveAttackPattern.Breath
                    ? MonsterBasicAttackSequenceModule.Burst
                    : MonsterBasicAttackSequenceModule.Single;
            var combatType = pattern is MonsterActiveAttackPattern.SingleTarget or
                MonsterActiveAttackPattern.Line or MonsterActiveAttackPattern.Cone or
                MonsterActiveAttackPattern.SelfCircle or MonsterActiveAttackPattern.FrontCircle or
                MonsterActiveAttackPattern.TargetCircle
                ? MonsterCombatType.Melee
                : MonsterCombatType.Ranged;
            var travel = pattern switch
            {
                MonsterActiveAttackPattern.StandardProjectile when ProjectileCount > 1 =>
                    MonsterBasicAttackProjectileTravel.Straight,
                MonsterActiveAttackPattern.StandardProjectile => ProjectileTravel,
                MonsterActiveAttackPattern.ReturningProjectile =>
                    MonsterBasicAttackProjectileTravel.Returning,
                MonsterActiveAttackPattern.PiercingProjectile or
                    MonsterActiveAttackPattern.ExplosiveProjectile => ProjectileTravel,
                MonsterActiveAttackPattern.TravelingWave =>
                    MonsterBasicAttackProjectileTravel.Straight,
                _ => MonsterBasicAttackProjectileTravel.None
            };
            destination.EditorConfigure(
                $"active_{StepId}",
                DisplayName,
                combatType,
                delivery,
                shape,
                center,
                travel,
                Range,
                pattern == MonsterActiveAttackPattern.ExplosiveProjectile ? ExplosionRadius : Radius,
                Angle,
                Width,
                MaxTargets,
                ProjectileCount,
                ProjectileFanAngle,
                damageRatios,
                SecondaryDamageRatio,
                RepeatHitInterval,
                DashBeforeAttack ? DashDistance : 0f,
                DashDuration,
                pattern == MonsterActiveAttackPattern.StandardProjectile,
                HitAreaVisibleDuration,
                ProjectileSpeed,
                ProjectileLifetime,
                ProjectileCollisionRadius);
            destination.EditorConfigureModules(
                pattern == MonsterActiveAttackPattern.TravelingWave
                    ? MonsterBasicAttackDeliveryModule.TravelingArea
                    : usesProjectileActor
                        ? MonsterBasicAttackDeliveryModule.Projectile
                        : MonsterBasicAttackDeliveryModule.Direct,
                collision,
                sequence,
                DashBeforeAttack ? MonsterBasicAttackMovementModule.Dash : MonsterBasicAttackMovementModule.None,
                presentation);
            destination.EditorSetBreathDuration(VisualDuration);
            destination.EditorConfigureStepExtensions(
                center,
                ForwardOffset,
                progression switch
                {
                    MonsterActiveAttackProgression.Forward => MonsterBasicAttackProgression.Forward,
                    MonsterActiveAttackProgression.LeftToRight => MonsterBasicAttackProgression.LeftToRight,
                    MonsterActiveAttackProgression.RightToLeft => MonsterBasicAttackProgression.RightToLeft,
                    MonsterActiveAttackProgression.Outward => MonsterBasicAttackProgression.Outward,
                    _ => MonsterBasicAttackProgression.Simultaneous
                },
                ProgressionDuration,
                TelegraphDelay,
                VisualDuration,
                magicDirection switch
                {
                    MonsterActiveMagicDirection.GroundUp => MonsterBasicAttackMagicDirection.GroundUp,
                    MonsterActiveMagicDirection.SkyDown => MonsterBasicAttackMagicDirection.SkyDown,
                    _ => MonsterBasicAttackMagicDirection.Forward
                });
            destination.EditorSetSweepDirection(progression switch
            {
                MonsterActiveAttackProgression.LeftToRight =>
                    MonsterBasicAttackSweepDirection.LeftToRight,
                MonsterActiveAttackProgression.RightToLeft =>
                    MonsterBasicAttackSweepDirection.RightToLeft,
                _ => MonsterBasicAttackSweepDirection.Simultaneous
            });
            destination.EditorSetVfxSlots(AttackBlockVfxSlots);
        }

#endif
    }

    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Skill/Active Attack Profile", fileName = "AAP_Attack")]
    public sealed class MonsterActiveAttackProfile : ScriptableObject // 재사용 가능한 공격 Step 조립 원본
    {
        public const int MaximumStepCount = 32;
        [SerializeField] private string profileId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private List<MonsterActiveAttackStep> steps = new List<MonsterActiveAttackStep>();
        [SerializeField] private BasicAttackFeelCue impactFeel = new BasicAttackFeelCue();

        public string ProfileId => profileId?.Trim() ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ProfileId : displayName.Trim();
        public string Description => description?.Trim() ?? string.Empty;
        public IReadOnlyList<MonsterActiveAttackStep> Steps => steps ??
            (IReadOnlyList<MonsterActiveAttackStep>)Array.Empty<MonsterActiveAttackStep>();
        public BasicAttackFeelCue ImpactFeel => impactFeel ??= new BasicAttackFeelCue();

        public bool TryValidate(out string error)
        {
            if (!ActiveAttackValue.UsesSafeId(ProfileId) || string.IsNullOrWhiteSpace(DisplayName))
            {
                error = $"공격 액티브 프로필 ID 또는 작업명이 비어 있습니다. Profile={name}";
                return false;
            }
            if (Steps.Count == 0 || Steps.Count > MaximumStepCount)
            {
                error = $"공격 액티브 Step은 1~{MaximumStepCount}개여야 합니다. Profile={ProfileId}";
                return false;
            }
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < Steps.Count; index++)
            {
                var step = Steps[index];
                var stepError = "Step이 비어 있습니다.";
                if (step == null || !step.TryValidate(out stepError))
                {
                    error = $"공격 Step {index + 1}이 유효하지 않습니다. {stepError}";
                    return false;
                }
                if (!step.HasCanonicalIdentity(index))
                {
                    error =
                        $"공격 Step ID/표시 이름은 순서와 공격 형태에서 자동 생성되어야 합니다. " +
                        $"Expected={MonsterActiveAttackStep.GetCanonicalStepId(index)} / " +
                        $"{MonsterActiveAttackStep.GetPatternDisplayName(step.Pattern)}, Actual={step.StepId}";
                    return false;
                }
                if (!ids.Add(step.StepId))
                {
                    error = $"공격 Step ID가 중복됩니다. Step={step.StepId}";
                    return false;
                }
            }
            if (!ImpactFeel.TryValidate(out var feelError))
            {
                error = $"공통 명중 FEEL 프리셋이 유효하지 않습니다. Detail={feelError}";
                return false;
            }
            error = string.Empty;
            return true;
        }

        public float EstimateDuration()
        {
            var previousLaunch = 0f;
            var previousEnd = 0f;
            for (var index = 0; index < Steps.Count; index++)
            {
                var step = Steps[index];
                if (step == null) continue;
                var speed = step.PlaybackSpeed;
                float launch;
                if (index > 0 && step.StartMode == MonsterActiveStepStartMode.AfterPreviousLaunch)
                {
                    launch = previousLaunch +
                             Mathf.Max(step.DelayAfterPrevious, step.TelegraphDelay) / speed;
                }
                else
                {
                    launch = previousEnd +
                             (step.DelayAfterPrevious + step.TelegraphDelay) / speed;
                }
                var end = launch + Mathf.Max(step.ProgressionDuration, step.VisualDuration) / speed;
                previousLaunch = launch;
                previousEnd = Mathf.Max(previousEnd, end);
            }
            return previousEnd;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string title,
            string body,
            IEnumerable<MonsterActiveAttackStep> attackSteps)
        {
            profileId = id?.Trim();
            displayName = title?.Trim();
            description = body?.Trim();
            steps = attackSteps == null
                ? new List<MonsterActiveAttackStep>()
                : new List<MonsterActiveAttackStep>(attackSteps);
            for (var index = 0; index < steps.Count; index++)
            {
                steps[index]?.EditorNormalizeIdentity(index);
            }
            impactFeel ??= new BasicAttackFeelCue();
        }

        public void EditorSetImpactFeel(BasicAttackFeelCue feel)
        {
            impactFeel = feel ?? new BasicAttackFeelCue();
        }
#endif
    }

    internal static class ActiveAttackValue
    {
        public static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        public static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        public static bool UsesSafeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            for (var index = 0; index < value.Length; index++)
            {
                var c = value[index];
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_' && c != '-') return false;
            }
            return true;
        }
    }
}
