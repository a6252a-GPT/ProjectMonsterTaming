using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
        InstantMagic
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
    public enum MonsterActiveMagicDirection { GroundUp, SkyDown }
    public enum MonsterActiveHitEffectType { Knockback, Airborne, Stun, Bleed, Slow }

    [Serializable]
    public sealed class MonsterActiveHitEffect // 한 Step 적중 뒤 조립하는 상태 효과
    {
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
                MonsterActiveHitEffectType.Bleed => magnitude > 0f && duration > 0f && tickInterval <= duration,
                MonsterActiveHitEffectType.Slow => magnitude > 0f && magnitude < 1f && duration > 0f,
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
    public sealed class MonsterActiveAttackStepTuning // 같은 프로필을 몬스터별 수치로 조정
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
        [SerializeField] private string displayName = "공격 1";
        [SerializeField, Min(0f)] private float delayAfterPrevious;
        [SerializeField] private MonsterActiveTargetPolicy targetPolicy;
        [SerializeField] private bool teleportBeforeAttack;
        [SerializeField, Min(0f)] private float teleportFrontDistance = 1f;
        [SerializeField] private MonsterActiveAttackPattern pattern;
        [SerializeField] private MonsterActiveAttackProgression progression;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [SerializeField, Range(1, 32)] private int maxTargets = 8;
        [SerializeField, Min(0.05f)] private float range = 4f;
        [SerializeField, Min(0.05f)] private float width = 1.2f;
        [SerializeField, Min(0.05f)] private float radius = 1.8f;
        [SerializeField, Min(0f)] private float forwardOffset = 1.5f;
        [SerializeField, Range(5f, 180f)] private float angle = 70f;
        [SerializeField, Min(0f)] private float progressionDuration = 0.25f;
        [SerializeField, Min(0f)] private float telegraphDelay = 0.12f;
        [SerializeField, Min(0.05f)] private float visualDuration = 0.8f;
        [SerializeField] private MonsterActiveProjectileFormation projectileFormation;
        [SerializeField, Range(1, 12)] private int projectileCount = 1;
        [SerializeField, Range(1f, 160f)] private float projectileFanAngle = 50f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 10f;
        [SerializeField, Min(0.01f)] private float projectileCollisionRadius = 0.25f;
        [SerializeField, Min(0.05f)] private float explosionRadius = 1.8f;
        [SerializeField] private MonsterActiveInstantMagicTarget instantMagicTarget;
        [SerializeField] private MonsterActiveMagicDirection magicDirection;
        [SerializeField] private List<MonsterActiveHitEffect> hitEffects = new List<MonsterActiveHitEffect>();
        [SerializeField] private List<MonsterActivePresentationSlot> presentationSlots =
            new List<MonsterActivePresentationSlot>();

        public string StepId => stepId?.Trim() ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? StepId : displayName.Trim();
        public float DelayAfterPrevious => Mathf.Max(0f, delayAfterPrevious);
        public MonsterActiveTargetPolicy TargetPolicy => targetPolicy;
        public bool TeleportBeforeAttack => teleportBeforeAttack;
        public float TeleportFrontDistance => Mathf.Max(0f, teleportFrontDistance);
        public MonsterActiveAttackPattern Pattern => pattern;
        public MonsterActiveAttackProgression Progression => progression;
        public float DamageMultiplier => Mathf.Max(0f, damageMultiplier);
        public int MaxTargets => Mathf.Clamp(maxTargets, 1, 32);
        public float Range => Mathf.Max(0.05f, range);
        public float Width => Mathf.Max(0.05f, width);
        public float Radius => Mathf.Max(0.05f, radius);
        public float ForwardOffset => Mathf.Max(0f, forwardOffset);
        public float Angle => Mathf.Clamp(angle, 5f, 180f);
        public float ProgressionDuration => Mathf.Max(0f, progressionDuration);
        public float TelegraphDelay => Mathf.Max(0f, telegraphDelay);
        public float VisualDuration => Mathf.Max(0.05f, visualDuration);
        public MonsterActiveProjectileFormation ProjectileFormation => projectileFormation;
        public int ProjectileCount => projectileFormation == MonsterActiveProjectileFormation.Single
            ? 1
            : Mathf.Clamp(projectileCount, 2, 12);
        public float ProjectileFanAngle => Mathf.Clamp(projectileFanAngle, 1f, 160f);
        public float ProjectileSpeed => Mathf.Max(0.1f, projectileSpeed);
        public float ProjectileCollisionRadius => Mathf.Max(0.01f, projectileCollisionRadius);
        public float ExplosionRadius => Mathf.Max(0.05f, explosionRadius);
        public MonsterActiveInstantMagicTarget InstantMagicTarget => instantMagicTarget;
        public MonsterActiveMagicDirection MagicDirection => magicDirection;
        public IReadOnlyList<MonsterActiveHitEffect> HitEffects => hitEffects ??
            (IReadOnlyList<MonsterActiveHitEffect>)Array.Empty<MonsterActiveHitEffect>();
        public IReadOnlyList<MonsterActivePresentationSlot> PresentationSlots => presentationSlots ??
            (IReadOnlyList<MonsterActivePresentationSlot>)Array.Empty<MonsterActivePresentationSlot>();
        public bool IsProjectile => pattern == MonsterActiveAttackPattern.PiercingProjectile ||
                                    pattern == MonsterActiveAttackPattern.ExplosiveProjectile;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(StepId) || string.IsNullOrWhiteSpace(DisplayName) ||
                !ActiveAttackValue.UsesSafeId(StepId) ||
                !Enum.IsDefined(typeof(MonsterActiveTargetPolicy), targetPolicy) ||
                !Enum.IsDefined(typeof(MonsterActiveAttackPattern), pattern) ||
                !Enum.IsDefined(typeof(MonsterActiveAttackProgression), progression) ||
                !ActiveAttackValue.IsFiniteNonNegative(delayAfterPrevious) ||
                !ActiveAttackValue.IsFiniteNonNegative(damageMultiplier) ||
                !ActiveAttackValue.IsFinitePositive(range) || !ActiveAttackValue.IsFinitePositive(width) ||
                !ActiveAttackValue.IsFinitePositive(radius) || !ActiveAttackValue.IsFiniteNonNegative(forwardOffset) ||
                !ActiveAttackValue.IsFiniteNonNegative(progressionDuration) ||
                !ActiveAttackValue.IsFiniteNonNegative(telegraphDelay) ||
                !ActiveAttackValue.IsFinitePositive(visualDuration) || maxTargets < 1 || maxTargets > 32 ||
                angle < 5f || angle > 180f)
            {
                error = $"공격 Step 기본값이 유효하지 않습니다. Step={StepId}";
                return false;
            }
            if (!SupportsProgression(pattern, progression))
            {
                error = $"공격 형태가 지원하지 않는 진행 방식입니다. Step={StepId}, Pattern={pattern}, Progression={progression}";
                return false;
            }
            if (IsProjectile && (!Enum.IsDefined(typeof(MonsterActiveProjectileFormation), projectileFormation) ||
                projectileCount < 1 || projectileCount > 12 || !ActiveAttackValue.IsFinitePositive(projectileSpeed) ||
                !ActiveAttackValue.IsFinitePositive(projectileCollisionRadius) || projectileFanAngle < 1f ||
                projectileFanAngle > 160f))
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
            var slotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < PresentationSlots.Count; index++)
            {
                var slot = PresentationSlots[index];
                var slotError = "연출 공간이 비어 있습니다.";
                if (slot == null || !slot.TryValidate(out slotError) || !slotIds.Add(slot.SlotId))
                {
                    error = $"VFX/SFX 공간 {index + 1}이 유효하지 않습니다. Step={StepId}, Detail={slotError}";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        public string BuildSummary()
        {
            var targetLabel = targetPolicy == MonsterActiveTargetPolicy.SameTarget ? "같은 대상" : "다른 대상";
            var teleportLabel = teleportBeforeAttack ? $" · 순간이동 {TeleportFrontDistance:0.#}m" : string.Empty;
            var effectLabel = HitEffects.Count > 0 ? $" · 타격효과 {HitEffects.Count}" : string.Empty;
            return $"{Pattern} / {Progression} · 피해 {DamageMultiplier:0.##}배 · {targetLabel}{teleportLabel}{effectLabel}";
        }

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
                MonsterActiveAttackPattern.SelfCircle or MonsterActiveAttackPattern.FrontCircle =>
                    attackProgression == MonsterActiveAttackProgression.Instant ||
                    attackProgression == MonsterActiveAttackProgression.Outward,
                _ => attackProgression == MonsterActiveAttackProgression.Instant
            };
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string title,
            MonsterActiveAttackPattern attackPattern,
            float power = 1f,
            float startDelay = 0f,
            MonsterActiveTargetPolicy targetSelection = MonsterActiveTargetPolicy.SameTarget,
            MonsterActiveAttackProgression attackProgression = MonsterActiveAttackProgression.Instant,
            MonsterActiveHitEffect[] effects = null)
        {
            stepId = id?.Trim();
            displayName = title?.Trim();
            pattern = attackPattern;
            damageMultiplier = Mathf.Max(0f, power);
            delayAfterPrevious = Mathf.Max(0f, startDelay);
            targetPolicy = targetSelection;
            progression = SupportsProgression(attackPattern, attackProgression)
                ? attackProgression
                : MonsterActiveAttackProgression.Instant;
            hitEffects = effects == null
                ? new List<MonsterActiveHitEffect>()
                : new List<MonsterActiveHitEffect>(effects);
            presentationSlots = CreateDefaultPresentationSlots(attackPattern);
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
            float effectVisualDuration = 0.8f)
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
        }

        public void EditorConfigureProjectile(
            MonsterActiveProjectileFormation formation,
            int count,
            float fanAngle,
            float speed,
            float collisionRadius,
            float blastRadius = 1.8f)
        {
            projectileFormation = formation;
            projectileCount = formation == MonsterActiveProjectileFormation.Single ? 1 : Mathf.Clamp(count, 2, 12);
            projectileFanAngle = Mathf.Clamp(fanAngle, 1f, 160f);
            projectileSpeed = Mathf.Max(0.1f, speed);
            projectileCollisionRadius = Mathf.Max(0.01f, collisionRadius);
            explosionRadius = Mathf.Max(0.05f, blastRadius);
        }

        public void EditorConfigureInstantMagic(
            MonsterActiveInstantMagicTarget targetMode,
            MonsterActiveMagicDirection direction)
        {
            instantMagicTarget = targetMode;
            magicDirection = direction;
        }

        public void EditorConfigureTeleport(bool enabled, float frontDistance)
        {
            teleportBeforeAttack = enabled;
            teleportFrontDistance = Mathf.Max(0f, frontDistance);
        }

        public void EditorSetPresentationSlots(IEnumerable<MonsterActivePresentationSlot> slots)
        {
            presentationSlots = slots == null
                ? new List<MonsterActivePresentationSlot>()
                : new List<MonsterActivePresentationSlot>(slots.Where(slot => slot != null));
        }

        private static List<MonsterActivePresentationSlot> CreateDefaultPresentationSlots(
            MonsterActiveAttackPattern attackPattern)
        {
            var result = new List<MonsterActivePresentationSlot>();
            result.Add(CreateSlot("telegraph", "판정 예고", MonsterActivePresentationEvent.Telegraph,
                MonsterActivePresentationAnchor.TargetPoint));
            result.Add(CreateSlot("launch", "공격 발동", MonsterActivePresentationEvent.Launch,
                MonsterActivePresentationAnchor.AttackOrigin));
            if (attackPattern == MonsterActiveAttackPattern.PiercingProjectile ||
                attackPattern == MonsterActiveAttackPattern.ExplosiveProjectile ||
                attackPattern == MonsterActiveAttackPattern.PiercingBeam)
            {
                result.Add(CreateSlot("travel", attackPattern == MonsterActiveAttackPattern.PiercingBeam
                        ? "빔 본체"
                        : "이동체",
                    MonsterActivePresentationEvent.Travel,
                    MonsterActivePresentationAnchor.AttackOrigin));
            }
            result.Add(CreateSlot("impact", "실제 타격", MonsterActivePresentationEvent.Impact,
                MonsterActivePresentationAnchor.TargetPoint));
            return result;
        }

        private static MonsterActivePresentationSlot CreateSlot(
            string id,
            string title,
            MonsterActivePresentationEvent timing,
            MonsterActivePresentationAnchor anchor)
        {
            var slot = new MonsterActivePresentationSlot();
            slot.EditorConfigure(id, title, timing, anchor);
            return slot;
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
            var duration = 0f;
            for (var index = 0; index < Steps.Count; index++)
            {
                var step = Steps[index];
                if (step != null)
                {
                    duration += step.DelayAfterPrevious + step.TelegraphDelay +
                                Mathf.Max(step.ProgressionDuration, step.VisualDuration);
                }
            }
            return duration;
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
