using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum MonsterSkillTriggerType
    {
        CombatStart,
        CombatJoin,
        BasicAttackHit,
        BasicAttackNthHit,
        TargetChanged,
        Damaged,
        DamagedNthTime,
        HealthThresholdEntered,
        ShieldBroken,
        Kill,
        AllyHealthThresholdEntered,
        EnergyMax,
        ActiveCommitted,
        Interval,
        WaveStart,
        NoDamageForDuration,
        AllyActiveUsed,
        Death
    }

    public enum MonsterSkillConditionType
    {
        None,
        SelfHealthBelow,
        SelfHealthAbove,
        TargetHealthBelow,
        TargetHealthAbove,
        TargetIsRanged,
        TargetIsBoss,
        TargetHasMark,
        SelfHasShield,
        NearbyEnemyCountAtLeast,
        NearbyAllyCountAtLeast,
        DistanceAtLeast,
        DistanceAtMost,
        SameTargetContinuous,
        Stationary,
        OncePerBattle,
        OncePerWave,
        InternalCooldownReady
    }

    public enum MonsterSkillTargetType
    {
        Self,
        CurrentTarget,
        Attacker,
        NearestEnemy,
        FarthestEnemy,
        LowestHealthEnemy,
        HighestHealthEnemy,
        HighestAttackEnemy,
        RangedEnemyFirst,
        LowestHealthAlly,
        HighestAttackAlly,
        NearbyAllies,
        AllAllies,
        TargetAreaEnemies,
        DensestEnemyPosition
    }

    public enum MonsterSkillDeliveryType
    {
        Instant,
        Dash,
        Leap,
        Projectile,
        PiercingProjectile,
        TravelingWave,
        Aura,
        Zone,
        Mark,
        GroundDrop,
        ReturningProjectile,
        Radial
    }

    public enum MonsterSkillShapeType
    {
        Single,
        SelfCircle,
        TargetCircle,
        ForwardCone,
        Line,
        Capsule,
        Chain,
        Zone,
        Ring
    }

    public enum MonsterSkillEffectType
    {
        Damage,
        Heal,
        Shield,
        AttackBuff,
        DefenseBuff,
        AttackSpeedBuff,
        MoveSpeedBuff,
        AttackDebuff,
        DefenseDebuff,
        AttackSpeedDebuff,
        MoveSpeedDebuff,
        Mark,
        Slow,
        Stagger,
        Knockback,
        MiniAirborne,
        Taunt,
        EnergyGain,
        EnergyDrain,
        Dash,
        Retreat,
        KnockbackResistance,
        DamageReduction,
        DamageReflect,
        HealingReduction,
        Cleanse,
        StatusResistance,
        Root,
        Stun,
        Pull,
        ProjectileBlock,
        DamageShare,
        Dispel,
        Summon,
        Revive,
        OutgoingDamageRandomization
    }

    public enum MonsterSkillMagnitudeMode
    {
        Fixed,
        RandomRange
    }

    public enum MonsterSkillValueSource
    {
        Flat,
        AttackPowerRatio,
        MaxHealthRatio,
        TargetMaxHealthRatio,
        TargetMissingHealthRatio,
        TargetEnergyCapacityRatio,
        ReceivedDamageRatio
    }

    public enum MonsterSkillStackPolicy
    {
        Replace,
        RefreshDuration,
        Stack,
        StrongestWins
    }

    public enum MonsterSkillPresentationTier
    {
        Subtle,
        Standard,
        Heroic,
        Legendary,
        Mythic
    }

    public enum MonsterSkillCategory
    {
        Offense,
        Defense,
        Support,
        Control,
        Mobility,
        Summon
    }

    public enum MonsterActiveExecutionKind
    {
        Generic,
        DedicatedMythic
    }

    [Serializable]
    public sealed class MonsterSkillCondition
    {
        [SerializeField] private MonsterSkillConditionType type;
        [SerializeField] private float value;
        [SerializeField, Min(1)] private int count = 1;
        [SerializeField] private string referenceId;

        public MonsterSkillConditionType Type => type;
        public float Value => value;
        public int Count => Mathf.Max(1, count);
        public string ReferenceId => referenceId?.Trim() ?? string.Empty;

        public bool TryValidate(out string error)
        {
            if (!Enum.IsDefined(typeof(MonsterSkillConditionType), type) ||
                float.IsNaN(value) || float.IsInfinity(value) || count < 1)
            {
                error = $"Monster skill condition is invalid. Type={type}";
                return false;
            }

            if (type == MonsterSkillConditionType.TargetHasMark && string.IsNullOrWhiteSpace(ReferenceId))
            {
                error = "TargetHasMark condition requires a reference ID.";
                return false;
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            MonsterSkillConditionType conditionType,
            float threshold = 0f,
            int requiredCount = 1,
            string id = null)
        {
            type = conditionType;
            value = threshold;
            count = Mathf.Max(1, requiredCount);
            referenceId = id?.Trim();
        }
#endif
    }

    [Serializable]
    public sealed class MonsterSkillEffect
    {
        [SerializeField] private string effectId;
        [SerializeField] private MonsterSkillEffectType type;
        [SerializeField] private MonsterSkillValueSource valueSource = MonsterSkillValueSource.AttackPowerRatio;
        [SerializeField, Min(0f)] private float magnitude = 1f;
        [SerializeField] private MonsterSkillMagnitudeMode magnitudeMode;
        [SerializeField, Min(0f)] private float maximumMagnitude = 1f;
        [SerializeField, Min(0f)] private float duration;
        [SerializeField, Min(0f)] private float delay;
        [SerializeField, Min(0f)] private float repeatInterval;
        [SerializeField, Min(0f)] private float radius;
        [SerializeField, Min(1)] private int maxTargets = 1;
        [SerializeField, Min(1)] private int repeatCount = 1;
        [SerializeField] private MonsterSkillStackPolicy stackPolicy = MonsterSkillStackPolicy.RefreshDuration;

        public string EffectId => effectId?.Trim() ?? string.Empty;
        public MonsterSkillEffectType Type => type;
        public MonsterSkillValueSource ValueSource => valueSource;
        public float Magnitude => Mathf.Max(0f, magnitude);
        public MonsterSkillMagnitudeMode MagnitudeMode => magnitudeMode;
        public float MaximumMagnitude => magnitudeMode == MonsterSkillMagnitudeMode.RandomRange
            ? Mathf.Max(Magnitude, maximumMagnitude)
            : Magnitude;
        public float Duration => Mathf.Max(0f, duration);
        public float Delay => Mathf.Max(0f, delay);
        public float RepeatInterval => Mathf.Max(0f, repeatInterval);
        public float Radius => Mathf.Max(0f, radius);
        public int MaxTargets => Mathf.Max(1, maxTargets);
        public int RepeatCount => Mathf.Max(1, repeatCount);
        public MonsterSkillStackPolicy StackPolicy => stackPolicy;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(EffectId) ||
                !Enum.IsDefined(typeof(MonsterSkillEffectType), type) ||
                !Enum.IsDefined(typeof(MonsterSkillValueSource), valueSource) ||
                !Enum.IsDefined(typeof(MonsterSkillMagnitudeMode), magnitudeMode) ||
                !Enum.IsDefined(typeof(MonsterSkillStackPolicy), stackPolicy) ||
                float.IsNaN(magnitude) || float.IsInfinity(magnitude) || magnitude < 0f ||
                float.IsNaN(maximumMagnitude) || float.IsInfinity(maximumMagnitude) || maximumMagnitude < 0f ||
                float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f ||
                float.IsNaN(delay) || float.IsInfinity(delay) || delay < 0f ||
                float.IsNaN(repeatInterval) || float.IsInfinity(repeatInterval) || repeatInterval < 0f ||
                float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f ||
                maxTargets < 1 || repeatCount < 1)
            {
                error = $"Monster skill effect is invalid. Effect={EffectId}, Type={type}";
                return false;
            }

            if (magnitudeMode == MonsterSkillMagnitudeMode.RandomRange && maximumMagnitude < magnitude)
            {
                error = $"Monster skill random magnitude maximum is below its minimum. Effect={EffectId}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public float ResolveMagnitude(float random01)
        {
            return magnitudeMode == MonsterSkillMagnitudeMode.RandomRange
                ? Mathf.Lerp(Magnitude, MaximumMagnitude, Mathf.Clamp01(random01))
                : Magnitude;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            MonsterSkillEffectType effectType,
            MonsterSkillValueSource source,
            float amount,
            float effectDuration = 0f,
            float effectRadius = 0f,
            int targetLimit = 1,
            MonsterSkillStackPolicy policy = MonsterSkillStackPolicy.RefreshDuration,
            int repeats = 1,
            float startDelay = 0f,
            MonsterSkillMagnitudeMode amountMode = MonsterSkillMagnitudeMode.Fixed,
            float maximumAmount = -1f,
            float hitInterval = 0f)
        {
            effectId = id?.Trim();
            type = effectType;
            valueSource = source;
            magnitude = Mathf.Max(0f, amount);
            magnitudeMode = amountMode;
            maximumMagnitude = maximumAmount < 0f ? magnitude : Mathf.Max(0f, maximumAmount);
            duration = Mathf.Max(0f, effectDuration);
            delay = Mathf.Max(0f, startDelay);
            repeatInterval = Mathf.Max(0f, hitInterval);
            radius = Mathf.Max(0f, effectRadius);
            maxTargets = Mathf.Max(1, targetLimit);
            repeatCount = Mathf.Max(1, repeats);
            stackPolicy = policy;
        }
#endif
    }

    [Serializable]
    public sealed class MonsterSkillRecipe
    {
        [SerializeField] private MonsterSkillTriggerType trigger = MonsterSkillTriggerType.BasicAttackHit;
        [SerializeField, Min(1)] private int triggerCount = 1;
        [SerializeField, Min(0f)] private float internalCooldown;
        [SerializeField] private MonsterSkillCondition[] conditions = Array.Empty<MonsterSkillCondition>();
        [SerializeField] private MonsterSkillTargetType target = MonsterSkillTargetType.CurrentTarget;
        [SerializeField] private MonsterSkillDeliveryType delivery = MonsterSkillDeliveryType.Instant;
        [SerializeField] private MonsterSkillShapeType shape = MonsterSkillShapeType.Single;
        [SerializeField] private MonsterSkillEffect[] effects = Array.Empty<MonsterSkillEffect>();

        public MonsterSkillTriggerType Trigger => trigger;
        public int TriggerCount => Mathf.Max(1, triggerCount);
        public float InternalCooldown => Mathf.Max(0f, internalCooldown);
        public IReadOnlyList<MonsterSkillCondition> Conditions => conditions ?? Array.Empty<MonsterSkillCondition>();
        public MonsterSkillTargetType Target => target;
        public MonsterSkillDeliveryType Delivery => delivery;
        public MonsterSkillShapeType Shape => shape;
        public IReadOnlyList<MonsterSkillEffect> Effects => effects ?? Array.Empty<MonsterSkillEffect>();

        public bool TryValidate(out string error)
        {
            if (!Enum.IsDefined(typeof(MonsterSkillTriggerType), trigger) || triggerCount < 1 ||
                float.IsNaN(internalCooldown) || float.IsInfinity(internalCooldown) || internalCooldown < 0f ||
                !Enum.IsDefined(typeof(MonsterSkillTargetType), target) ||
                !Enum.IsDefined(typeof(MonsterSkillDeliveryType), delivery) ||
                !Enum.IsDefined(typeof(MonsterSkillShapeType), shape))
            {
                error = "Monster skill recipe trigger, target, delivery, or shape is invalid.";
                return false;
            }

            var sourceConditions = conditions ?? Array.Empty<MonsterSkillCondition>();
            for (var index = 0; index < sourceConditions.Length; index++)
            {
                if (sourceConditions[index] == null)
                {
                    error = $"Monster skill condition {index} is missing.";
                    return false;
                }

                if (!sourceConditions[index].TryValidate(out var conditionError))
                {
                    error = $"Monster skill condition {index} is invalid. {conditionError}";
                    return false;
                }
            }

            var sourceEffects = effects ?? Array.Empty<MonsterSkillEffect>();
            if (sourceEffects.Length == 0)
            {
                error = "Monster skill recipe requires at least one effect.";
                return false;
            }

            for (var index = 0; index < sourceEffects.Length; index++)
            {
                if (sourceEffects[index] == null)
                {
                    error = $"Monster skill effect {index} is missing.";
                    return false;
                }

                if (!sourceEffects[index].TryValidate(out var effectError))
                {
                    error = $"Monster skill effect {index} is invalid. {effectError}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public string BuildSummary()
        {
            var triggerLabel = triggerCount > 1 ? $"{trigger} {triggerCount}회" : trigger.ToString();
            var effectLabels = new List<string>();
            var sourceEffects = effects ?? Array.Empty<MonsterSkillEffect>();
            for (var index = 0; index < sourceEffects.Length; index++)
            {
                if (sourceEffects[index] != null)
                {
                    var effect = sourceEffects[index];
                    var label = effect.RepeatCount > 1
                        ? $"{effect.Type} x{effect.RepeatCount}"
                        : effect.Type.ToString();
                    effectLabels.Add(effect.Delay > 0f ? $"{label} (+{effect.Delay:0.##}s)" : label);
                }
            }

            return $"{triggerLabel} → {target} → {delivery}/{shape} → {string.Join(" + ", effectLabels)}";
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            MonsterSkillTriggerType triggerType,
            int requiredCount,
            float cooldown,
            MonsterSkillTargetType targetType,
            MonsterSkillDeliveryType deliveryType,
            MonsterSkillShapeType shapeType,
            MonsterSkillCondition[] skillConditions,
            MonsterSkillEffect[] skillEffects)
        {
            trigger = triggerType;
            triggerCount = Mathf.Max(1, requiredCount);
            internalCooldown = Mathf.Max(0f, cooldown);
            target = targetType;
            delivery = deliveryType;
            shape = shapeType;
            conditions = skillConditions ?? Array.Empty<MonsterSkillCondition>();
            effects = skillEffects ?? Array.Empty<MonsterSkillEffect>();
        }
#endif
    }

    public abstract class MonsterSkillDefinitionBase : ScriptableObject
    {
        [SerializeField] private string skillId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private MonsterSkillPresentationTier presentationTier = MonsterSkillPresentationTier.Standard;
        [SerializeField] private MonsterSkillRecipe recipe = new MonsterSkillRecipe();
        [SerializeField] private bool authoringEnabled = true;

        public string SkillId => skillId?.Trim() ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? SkillId : displayName;
        public string Description => description?.Trim() ?? string.Empty;
        public Sprite Icon => icon;
        public MonsterSkillPresentationTier PresentationTier => presentationTier;
        public MonsterSkillRecipe Recipe => recipe;
        public string RecipeSummary => recipe == null ? string.Empty : recipe.BuildSummary();
        public MonsterSkillCategory Category => ResolveCategory();
        public bool AuthoringEnabled => authoringEnabled;

        public virtual bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(SkillId) || string.IsNullOrWhiteSpace(DisplayName))
            {
                error = $"Monster skill ID or display name is blank. Skill={name}";
                return false;
            }

            if (!UsesSafeId(SkillId))
            {
                error = $"Monster skill ID uses unsupported characters. Skill={SkillId}";
                return false;
            }

            if (!Enum.IsDefined(typeof(MonsterSkillPresentationTier), presentationTier))
            {
                error = $"Monster skill presentation tier is invalid. Skill={SkillId}";
                return false;
            }

            if (recipe == null)
            {
                error = $"Monster skill recipe is missing. Skill={SkillId}";
                return false;
            }

            if (!recipe.TryValidate(out var recipeError))
            {
                error = $"Monster skill recipe is invalid. Skill={SkillId}, Detail={recipeError}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool UsesSafeId(string value)
        {
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var isLowerAscii = character >= 'a' && character <= 'z';
                var isDigit = character >= '0' && character <= '9';
                if (!isLowerAscii && !isDigit && character != '_' && character != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private MonsterSkillCategory ResolveCategory()
        {
            if (recipe == null)
            {
                return MonsterSkillCategory.Offense;
            }

            if (recipe.Delivery == MonsterSkillDeliveryType.Dash ||
                recipe.Delivery == MonsterSkillDeliveryType.Leap ||
                recipe.Delivery == MonsterSkillDeliveryType.ReturningProjectile)
            {
                return MonsterSkillCategory.Mobility;
            }

            var effects = recipe.Effects;
            for (var index = 0; index < effects.Count; index++)
            {
                var effect = effects[index];
                if (effect == null)
                {
                    continue;
                }

                switch (effect.Type)
                {
                    case MonsterSkillEffectType.Summon:
                    case MonsterSkillEffectType.Revive:
                        return MonsterSkillCategory.Summon;
                    case MonsterSkillEffectType.Heal:
                    case MonsterSkillEffectType.AttackBuff:
                    case MonsterSkillEffectType.AttackSpeedBuff:
                    case MonsterSkillEffectType.MoveSpeedBuff:
                    case MonsterSkillEffectType.EnergyGain:
                    case MonsterSkillEffectType.Cleanse:
                    case MonsterSkillEffectType.Dispel:
                        return MonsterSkillCategory.Support;
                    case MonsterSkillEffectType.Shield:
                    case MonsterSkillEffectType.DefenseBuff:
                    case MonsterSkillEffectType.KnockbackResistance:
                    case MonsterSkillEffectType.DamageReduction:
                    case MonsterSkillEffectType.DamageReflect:
                    case MonsterSkillEffectType.StatusResistance:
                    case MonsterSkillEffectType.ProjectileBlock:
                    case MonsterSkillEffectType.DamageShare:
                        return MonsterSkillCategory.Defense;
                    case MonsterSkillEffectType.Mark:
                    case MonsterSkillEffectType.Slow:
                    case MonsterSkillEffectType.Stagger:
                    case MonsterSkillEffectType.Knockback:
                    case MonsterSkillEffectType.MiniAirborne:
                    case MonsterSkillEffectType.Taunt:
                    case MonsterSkillEffectType.EnergyDrain:
                    case MonsterSkillEffectType.HealingReduction:
                    case MonsterSkillEffectType.Root:
                    case MonsterSkillEffectType.Stun:
                    case MonsterSkillEffectType.Pull:
                        return MonsterSkillCategory.Control;
                }
            }

            return MonsterSkillCategory.Offense;
        }

#if UNITY_EDITOR
        protected void EditorConfigureCommon(
            string id,
            string title,
            string body,
            MonsterSkillPresentationTier tier,
            MonsterSkillRecipe skillRecipe,
            Sprite skillIcon = null)
        {
            skillId = id?.Trim();
            displayName = title?.Trim();
            description = body?.Trim();
            presentationTier = tier;
            recipe = skillRecipe ?? new MonsterSkillRecipe();
            icon = skillIcon;
        }

        public void EditorSetAuthoringEnabled(bool enabled)
        {
            authoringEnabled = enabled;
        }
#endif
    }

    public abstract class MonsterPassiveSkill : MonsterSkillDefinitionBase
    {
    }

    public abstract class MonsterActiveSkill : MonsterSkillDefinitionBase
    {
        [SerializeField, Min(1)] private int energyCost = 1000;
        [SerializeField, HideInInspector, Min(0f)] private float energyPerSecond = 40f; // 구 자산 역직렬화 전용
        [SerializeField, HideInInspector, Min(0f)] private float energyPerBasicAttackHit = 120f; // 구 자산 역직렬화 전용
        [SerializeField, HideInInspector, Min(0f)] private float energyPerDamageReceived = 80f; // 구 자산 역직렬화 전용

        public int EnergyCost => Mathf.Max(1, energyCost);
        public float EnergyPerSecond => Mathf.Max(0f, energyPerSecond);
        public float EnergyPerBasicAttackHit => Mathf.Max(0f, energyPerBasicAttackHit);
        public float EnergyPerDamageReceived => Mathf.Max(0f, energyPerDamageReceived);
        public abstract MonsterActiveExecutionKind ExecutionKind { get; }

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            if (energyCost < 1 || Recipe.Trigger != MonsterSkillTriggerType.EnergyMax)
            {
                error = $"Monster active skill requires EnergyCost and EnergyMax trigger. Skill={SkillId}";
                return false;
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        protected void EditorSetEnergyCost(int value)
        {
            energyCost = Mathf.Max(1, value);
        }

        protected void EditorSetEnergyGeneration(float perSecond, float perBasicAttackHit, float perDamageReceived)
        {
            energyPerSecond = Mathf.Max(0f, perSecond);
            energyPerBasicAttackHit = Mathf.Max(0f, perBasicAttackHit);
            energyPerDamageReceived = Mathf.Max(0f, perDamageReceived);
        }
#endif
    }

}
