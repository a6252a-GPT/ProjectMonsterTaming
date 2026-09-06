using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Features.CommanderSkill;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.CommanderSkillWorkshop
{
    internal enum CommanderSkillWorkshopEffectKind { UnitEffect, CommanderMark, RecordedHitDamage, GlobalModifier, AreaDamage, Pull }

    [Serializable]
    internal sealed class CommanderMarkFeedbackDraft
    {
        [SerializeField] private GameObject vfxPrefab;
        [SerializeField, Min(0.05f)] private float lifetime = 1f;
        [SerializeField] private Vector3 localOffset;
        [SerializeField] private Vector3 localEuler;
        [SerializeField, Min(0.01f)] private float scale = 1f;
        [SerializeField] private AudioClip sound;
        [SerializeField, HideInInspector] private SfxCue sfxSource;
        [SerializeField] private CommanderMarkFeedbackAnchor anchor = CommanderMarkFeedbackAnchor.TargetCenter;
        public GameObject VfxPrefab => vfxPrefab;
        public float Lifetime => lifetime;
        public Vector3 LocalOffset => localOffset;
        public Vector3 LocalEuler => localEuler;
        public float Scale => scale;
        public AudioClip Sound => sound;
        public SfxCue SfxSource => sfxSource;
        public CommanderMarkFeedbackAnchor Anchor => anchor;

        public static CommanderMarkFeedbackDraft FromDefinition(CommanderMarkFeedbackSlot source)
        {
            return new CommanderMarkFeedbackDraft
            {
                vfxPrefab = source?.VfxPrefab,
                lifetime = source?.Lifetime ?? 1f,
                localOffset = source?.LocalOffset ?? Vector3.zero,
                localEuler = source?.LocalEuler ?? Vector3.zero,
                scale = source?.Scale ?? 1f,
                sound = source?.Sfx?.PrimaryClip,
                sfxSource = source?.Sfx,
                anchor = source?.Anchor ?? CommanderMarkFeedbackAnchor.TargetCenter
            };
        }
    }

    [Serializable]
    internal sealed class CommanderSkillWorkshopEffectDraft // 효과형 스킬을 조립하는 한 효과 카드
    {
        [SerializeField] private string effectId = "effect_01";
        [SerializeField] private CommanderSkillWorkshopEffectKind kind;
        [SerializeField] private CommanderSkillDamageKind damageKind = CommanderSkillDamageKind.Physical;
        [SerializeField, Min(0f)] private float baseDamage = 10f;
        [SerializeField, Min(0f)] private float perHitMultiplier = 1f;
        [SerializeField] private MonsterBasicAttackShape damageShape = MonsterBasicAttackShape.Circle;
        [SerializeField] private MonsterBasicAttackCenter damageCenter = MonsterBasicAttackCenter.PrimaryTarget;
        [SerializeField, Min(0f)] private float forwardOffset;
        [SerializeField, Range(5f, 180f)] private float angle = 90f;
        [SerializeField, Min(0.05f)] private float lineWidth = 1f;
        [SerializeField] private CommanderSkillUnitEffectType effectType = CommanderSkillUnitEffectType.Heal;
        [SerializeField] private CommanderSkillEffectValueSource valueSource =
            CommanderSkillEffectValueSource.TargetMissingHealthRatio;
        [SerializeField, Min(0f)] private float magnitude = 0.25f;
        [SerializeField, Min(0f)] private float duration;
        [SerializeField] private CommanderSkillEffectScope scope = CommanderSkillEffectScope.Area;
        [SerializeField, Min(0.1f)] private float radius = 5f;
        [SerializeField, Min(1)] private int maxTargets = 8;
        [SerializeField] private MonsterBuffStackPolicy stackPolicy = MonsterBuffStackPolicy.RefreshDuration;
        [SerializeField] private string markId = "mark_01";
        [SerializeField] private CommanderMarkTriggerType markTrigger = CommanderMarkTriggerType.HitCount;
        [SerializeField, Min(1)] private int requiredHits = 1;
        [SerializeField, Min(1)] private int requiredStacks = 1;
        [SerializeField, Min(1)] private int markMaxStacks = 1;
        [SerializeField] private bool consumeOnTrigger = true;
        [SerializeField] private bool refreshDurationOnApply = true;
        [SerializeField, Min(0f)] private float triggerCooldown;
        [SerializeField, Min(0f)] private float triggerDamage;
        [SerializeField, Min(0f)] private float triggerPerHitMultiplier = 1f;
        [SerializeField] private bool recordHitCount;
        [SerializeField] private bool countBasicAttack = true;
        [SerializeField] private bool countMonsterSkill = true;
        [SerializeField] private bool countCommanderSkill = true;
        [SerializeField] private bool countCommanderMarkTrigger;
        [SerializeField] private List<CommanderSkillWorkshopEffectDraft> triggerEffects =
            new List<CommanderSkillWorkshopEffectDraft>();
        [SerializeField, Min(0f)] private float recordedBaseMultiplier = 0.4f;
        [SerializeField, Min(0f)] private float recordedMultiplierPerHit = 0.12f;
        [SerializeField, Min(0)] private int maximumRecordedHits = 20;
        [SerializeField, Min(0.01f)] private float markRequiredHitsMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float markTriggerDamageMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float cooldownRecoveryMultiplier = 1f;
        [SerializeField] private CommanderMarkEffectDefinition sharedMarkDefinition;
        [SerializeField] private CommanderMarkFeedbackDraft onApply = new CommanderMarkFeedbackDraft();
        [SerializeField] private CommanderMarkFeedbackDraft loop = new CommanderMarkFeedbackDraft();
        [SerializeField] private CommanderMarkFeedbackDraft onStack = new CommanderMarkFeedbackDraft();
        [SerializeField] private CommanderMarkFeedbackDraft onTrigger = new CommanderMarkFeedbackDraft();
        [SerializeField] private CommanderMarkFeedbackDraft onRemove = new CommanderMarkFeedbackDraft();

        public string EffectId => effectId?.Trim() ?? string.Empty;
        public CommanderSkillWorkshopEffectKind Kind => kind;
        [SerializeField] private CommanderSkillPullCenter pullCenter;
        [SerializeField] private float pullDistance = 0.6f;
        [SerializeField] private float pullDuration = 0.2f;
        [SerializeField] private float pullStopDistance = 2f;
        [SerializeField] private int pullMaxTargets = 4;
        public CommanderSkillPullCenter PullCenter => pullCenter;
        public float PullDistance => pullDistance;
        public float PullDuration => pullDuration;
        public float PullStopDistance => pullStopDistance;
        public int PullMaxTargets => pullMaxTargets;
        public CommanderSkillDamageKind DamageKind => damageKind;
        public float BaseDamage => baseDamage;
        public float PerHitMultiplier => perHitMultiplier;
        public MonsterBasicAttackShape DamageShape => damageShape;
        public MonsterBasicAttackCenter DamageCenter => damageCenter;
        public float ForwardOffset => forwardOffset;
        public float Angle => angle;
        public float LineWidth => lineWidth;
        public CommanderSkillUnitEffectType EffectType => effectType;
        public CommanderSkillEffectValueSource ValueSource => valueSource;
        public float Magnitude => magnitude;
        public float Duration => duration;
        public CommanderSkillEffectScope Scope => scope;
        public float Radius => radius;
        public int MaxTargets => maxTargets;
        public MonsterBuffStackPolicy StackPolicy => stackPolicy;
        public string MarkId => markId?.Trim() ?? string.Empty;
        public CommanderMarkTriggerType MarkTrigger => markTrigger;
        public int RequiredHits => requiredHits;
        public int RequiredStacks => requiredStacks;
        public int MarkMaxStacks => markMaxStacks;
        public bool ConsumeOnTrigger => consumeOnTrigger;
        public bool RefreshDurationOnApply => refreshDurationOnApply;
        public float TriggerCooldown => triggerCooldown;
        public float TriggerDamage => triggerDamage;
        public float TriggerPerHitMultiplier => triggerPerHitMultiplier;
        public bool RecordHitCount => recordHitCount;
        public bool CountBasicAttack => countBasicAttack;
        public bool CountMonsterSkill => countMonsterSkill;
        public bool CountCommanderSkill => countCommanderSkill;
        public bool CountCommanderMarkTrigger => countCommanderMarkTrigger;
        public IReadOnlyList<CommanderSkillWorkshopEffectDraft> TriggerEffects => triggerEffects;
        public float RecordedBaseMultiplier => recordedBaseMultiplier;
        public float RecordedMultiplierPerHit => recordedMultiplierPerHit;
        public int MaximumRecordedHits => maximumRecordedHits;
        public float MarkRequiredHitsMultiplier => markRequiredHitsMultiplier;
        public float MarkTriggerDamageMultiplier => markTriggerDamageMultiplier;
        public float CooldownRecoveryMultiplier => cooldownRecoveryMultiplier;
        public CommanderMarkEffectDefinition SharedMarkDefinition => sharedMarkDefinition;
        public CommanderMarkFeedbackDraft OnApply => onApply;
        public CommanderMarkFeedbackDraft Loop => loop;
        public CommanderMarkFeedbackDraft OnStack => onStack;
        public CommanderMarkFeedbackDraft OnTrigger => onTrigger;
        public CommanderMarkFeedbackDraft OnRemove => onRemove;

        public void UseSharedMark(CommanderMarkEffectDefinition definition)
        {
            sharedMarkDefinition = definition;
        }

        public static CommanderSkillWorkshopEffectDraft CreateDefault(CommanderSkillCategory category, int index)
        {
            var draft = new CommanderSkillWorkshopEffectDraft
            {
                effectId = $"effect_{Mathf.Max(1, index):00}"
            };
            if (category == CommanderSkillCategory.Debuff)
            {
                draft.effectType = CommanderSkillUnitEffectType.Slow;
                draft.valueSource = CommanderSkillEffectValueSource.Flat;
                draft.magnitude = 0.2f;
                draft.duration = 4f;
                draft.scope = CommanderSkillEffectScope.Area;
            }
            return draft;
        }

        public static CommanderSkillWorkshopEffectDraft FromDefinition(CommanderUnitEffectDefinition source)
        {
            return FromUnitDefinition(source);
        }

        public static CommanderSkillWorkshopEffectDraft FromDefinition(CommanderPullEffectDefinition source) =>
            new CommanderSkillWorkshopEffectDraft { kind = CommanderSkillWorkshopEffectKind.Pull, effectId = source.EffectId,
                pullCenter = source.Center, pullDistance = source.Distance, pullDuration = source.Duration,
                pullStopDistance = source.StopDistance, pullMaxTargets = source.MaxTargets };

        private static CommanderSkillWorkshopEffectDraft FromUnitDefinition(CommanderUnitEffectDefinition source)
        {
            return new CommanderSkillWorkshopEffectDraft
            {
                effectId = source.EffectId,
                effectType = source.EffectType,
                valueSource = source.ValueSource,
                magnitude = source.Magnitude,
                duration = source.Duration,
                scope = source.Scope,
                radius = source.Radius,
                maxTargets = source.MaxTargets,
                stackPolicy = source.StackPolicy
            };
        }

        public static CommanderSkillWorkshopEffectDraft FromDefinition(CommanderAreaDamageEffectDefinition source)
        {
            return new CommanderSkillWorkshopEffectDraft
            {
                kind = CommanderSkillWorkshopEffectKind.AreaDamage,
                effectId = source.EffectId,
                damageKind = source.DamageKind,
                baseDamage = source.BaseDamage,
                perHitMultiplier = source.PerHitMultiplier,
                damageShape = source.Shape,
                damageCenter = source.Center,
                radius = source.Radius,
                forwardOffset = source.ForwardOffset,
                angle = source.Angle,
                lineWidth = source.LineWidth,
                maxTargets = source.MaxTargets
            };
        }

        public static CommanderSkillWorkshopEffectDraft FromDefinition(CommanderMarkEffectDefinition source)
        {
            var result = new CommanderSkillWorkshopEffectDraft
            {
                kind = CommanderSkillWorkshopEffectKind.CommanderMark,
                effectId = source.EffectId,
                markId = source.MarkId,
                duration = source.Duration,
                scope = source.Scope,
                radius = source.Radius,
                maxTargets = source.MaxTargets,
                markTrigger = source.TriggerType,
                requiredHits = source.RequiredHits,
                requiredStacks = source.RequiredStacks,
                markMaxStacks = source.MaxStacks,
                consumeOnTrigger = source.ConsumeOnTrigger,
                refreshDurationOnApply = source.RefreshDurationOnApply,
                triggerCooldown = source.TriggerCooldown,
                recordHitCount = source.RecordHitCount,
                countBasicAttack = source.CountBasicAttack,
                countMonsterSkill = source.CountMonsterSkill,
                countCommanderSkill = source.CountCommanderSkill,
                countCommanderMarkTrigger = source.CountCommanderMarkTrigger,
                onApply = CommanderMarkFeedbackDraft.FromDefinition(source.OnApply),
                loop = CommanderMarkFeedbackDraft.FromDefinition(source.Loop),
                onStack = CommanderMarkFeedbackDraft.FromDefinition(source.OnStack),
                onTrigger = CommanderMarkFeedbackDraft.FromDefinition(source.OnTrigger),
                onRemove = CommanderMarkFeedbackDraft.FromDefinition(source.OnRemove)
            };
            for (var index = 0; index < source.EffectsOnTrigger.Count; index++)
            {
                var trigger = source.EffectsOnTrigger[index];
                if (trigger is CommanderAreaDamageEffectDefinition damage)
                    result.triggerEffects.Add(FromDefinition(damage));
                else if (trigger is CommanderUnitEffectDefinition unit)
                    result.triggerEffects.Add(FromDefinition(unit));
                else if (trigger is CommanderRecordedHitDamageEffectDefinition recorded)
                    result.triggerEffects.Add(FromDefinition(recorded));
            }
            return result;
        }

        public static CommanderSkillWorkshopEffectDraft FromDefinition(CommanderRecordedHitDamageEffectDefinition source)
        {
            return new CommanderSkillWorkshopEffectDraft
            {
                kind = CommanderSkillWorkshopEffectKind.RecordedHitDamage,
                effectId = source.EffectId,
                recordedBaseMultiplier = source.BaseMultiplier,
                recordedMultiplierPerHit = source.MultiplierPerRecordedHit,
                maximumRecordedHits = source.MaximumRecordedHits
            };
        }

        public static CommanderSkillWorkshopEffectDraft FromDefinition(CommanderGlobalModifierEffectDefinition source)
        {
            return new CommanderSkillWorkshopEffectDraft
            {
                kind = CommanderSkillWorkshopEffectKind.GlobalModifier,
                effectId = source.EffectId,
                duration = source.Duration,
                markRequiredHitsMultiplier = source.MarkRequiredHitsMultiplier,
                markTriggerDamageMultiplier = source.MarkTriggerDamageMultiplier,
                cooldownRecoveryMultiplier = source.CooldownRecoveryMultiplier
            };
        }
    }

    internal sealed class CommanderSkillWorkshopDraft : ScriptableObject // 실제 자산 저장 전 격리된 편집 모델
    {
        [Header("Identity")]
        [SerializeField] private string skillId;
        [SerializeField] private string displayName = "새 군단장 공격 스킬";
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private CommanderSkillCategory category = CommanderSkillCategory.Attack;
        [SerializeField] private CommanderSkillRarity rarity;

        [Header("Cast Flow")]
        [SerializeField, Min(0f)] private float castTime = 0.5f;
        [SerializeField] private CommanderSkillAutoUseCondition autoUseCondition;
        [SerializeField] private float autoHealthThreshold = 0.85f;
        [SerializeField] private CommanderSkillAwakeningStage[] awakeningStages = Array.Empty<CommanderSkillAwakeningStage>();
        [SerializeField, Min(0.1f)] private float cooldown = 8f;

        [Header("Target")]
        [SerializeField] private CommanderSkillTargetTeam targetTeam = CommanderSkillTargetTeam.Enemy;
        [SerializeField] private CommanderSkillTargetSelection targetSelection =
            CommanderSkillTargetSelection.Nearest;
        [SerializeField, Min(1f)] private float targetRange = 20f;

        [Header("Attack Modules")]
        [SerializeField] private MonsterBasicAttackDeliveryModule deliveryModule =
            MonsterBasicAttackDeliveryModule.Direct;
        [SerializeField] private CommanderSkillDamageKind damageKind = CommanderSkillDamageKind.Physical;
        [SerializeField, Min(0f)] private float baseDamage = 20f;
        [SerializeField, Min(0f)] private float perHitMultiplier = 1f;
        [SerializeField] private MonsterBasicAttackShape shape = MonsterBasicAttackShape.Circle;
        [SerializeField] private MonsterBasicAttackCenter center = MonsterBasicAttackCenter.PrimaryTarget;
        [SerializeField, Min(0.1f)] private float radius = 2f;
        [SerializeField, Min(0f)] private float forwardOffset = 2f;
        [SerializeField, Range(5f, 180f)] private float angle = 90f;
        [SerializeField, Min(0.05f)] private float lineWidth = 2f;
        [SerializeField, Min(1)] private int maxTargets = 8;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField, Min(1f)] private float projectileSpeed = 16f;
        [SerializeField] private CommanderSkillTrajectory trajectory;
        [SerializeField, Min(0f)] private float arcHeight = 3f;

        [Header("Pattern")]
        [SerializeField] private CommanderSkillPatternType patternType;
        [SerializeField, Min(1)] private int repeatCount = 1;
        [SerializeField, Min(0f)] private float repeatInterval;
        [SerializeField, Min(0.01f)] private float patternDuration = 1f;
        [SerializeField, Min(0.01f)] private float tickInterval = 1f;
        [SerializeField, Min(0f)] private float randomRadius;
        [SerializeField] private bool firstBarrageHitAtTarget;
        [SerializeField, Min(1)] private int chainCount = 1;
        [SerializeField, Min(0.1f)] private float chainRadius = 4f;

        [Header("Effect Modules")]
        [SerializeField] private List<CommanderSkillWorkshopEffectDraft> effects =
            new List<CommanderSkillWorkshopEffectDraft>();

        [Header("Feedback")]
        [SerializeField] private GameObject castingVfxPrefab;
        [SerializeField, Min(0.05f)] private float castingVfxLifetime = 1f;
        [SerializeField] private Vector3 castingVfxLocalOffset;
        [SerializeField] private Vector3 castingVfxLocalEuler;
        [SerializeField, Min(0.01f)] private float castingVfxScale = 1f;
        [SerializeField] private AudioClip castingSound;
        [SerializeField, HideInInspector] private SfxCue castingSfxSource;
        [SerializeField] private GameObject castVfxPrefab;
        [SerializeField, Min(0.05f)] private float castVfxLifetime = 1f;
        [SerializeField] private Vector3 castVfxLocalOffset;
        [SerializeField] private Vector3 castVfxLocalEuler;
        [SerializeField, Min(0.01f)] private float castVfxScale = 1f;
        [SerializeField] private AudioClip castSound;
        [SerializeField, HideInInspector] private SfxCue castSfxSource;
        [SerializeField] private GameObject impactVfxPrefab;
        [SerializeField, Min(0.05f)] private float impactVfxLifetime = 1.5f;
        [SerializeField] private Vector3 impactVfxLocalOffset;
        [SerializeField] private Vector3 impactVfxLocalEuler;
        [SerializeField, Min(0.01f)] private float impactVfxScale = 1f;
        [SerializeField] private AudioClip impactSound;
        [SerializeField, HideInInspector] private SfxCue impactSfxSource;
        [SerializeField] private GameObject persistentVfxPrefab;
        [SerializeField] private Vector3 persistentVfxLocalOffset;
        [SerializeField] private Vector3 persistentVfxLocalEuler;
        [SerializeField, Min(0.01f)] private float persistentVfxScale = 1f;
        [SerializeField] private CommanderMarkFeedbackAnchor persistentVfxAnchor = CommanderMarkFeedbackAnchor.WorldPosition;

        [Header("Catalog")]
        [SerializeField] private bool registerInCatalog = true;
        [SerializeField, Min(1)] private int maxLevel = 200;
        [SerializeField, Min(1)] private int requiredDuplicateCount = 1;
        [SerializeField, Min(1)] private long baseGoldCost = 100L;
        [SerializeField, Min(1f)] private float goldCostGrowthMultiplier = 1.03f;
        [SerializeField, Min(0.01f)] private float maxLevelEffectMultiplier = 4.98f;
        [SerializeField, HideInInspector] private string loadedGrowthJson;

        [Header("Summon")]
        [SerializeField] private bool includeInSummonPool = true;
        [SerializeField, Min(1)] private int minimumSummonLevel = 1;
        [SerializeField, Min(1)] private int summonWeight = 100;

        public string SkillId => skillId?.Trim() ?? string.Empty;
        public string DisplayName => displayName?.Trim() ?? string.Empty;
        public string Description => description?.Trim() ?? string.Empty;
        public Sprite Icon => icon;
        public CommanderSkillCategory Category => category;
        public CommanderSkillRarity Rarity => rarity;
        public float CastTime => castTime;
        public CommanderSkillAutoUseCondition AutoUseCondition => autoUseCondition;
        public float AutoHealthThreshold => autoHealthThreshold;
        public CommanderSkillAwakeningStage[] AwakeningStages => awakeningStages;
        public float Cooldown => cooldown;
        public CommanderSkillTargetTeam TargetTeam => targetTeam;
        public CommanderSkillTargetSelection TargetSelection => targetSelection;
        public float TargetRange => targetRange;
        public MonsterBasicAttackDeliveryModule DeliveryModule => deliveryModule;
        public CommanderSkillDamageKind DamageKind => damageKind;
        public float BaseDamage => baseDamage;
        public float PerHitMultiplier => perHitMultiplier;
        public MonsterBasicAttackShape Shape => shape;
        public MonsterBasicAttackCenter Center => center;
        public float Radius => radius;
        public float ForwardOffset => forwardOffset;
        public float Angle => angle;
        public float LineWidth => lineWidth;
        public int MaxTargets => maxTargets;
        public GameObject ProjectilePrefab => projectilePrefab;
        public float ProjectileSpeed => projectileSpeed;
        public CommanderSkillTrajectory Trajectory => trajectory;
        public float ArcHeight => arcHeight;
        public CommanderSkillPatternType PatternType => patternType;
        public int RepeatCount => repeatCount;
        public float RepeatInterval => repeatInterval;
        public float PatternDuration => patternDuration;
        public float TickInterval => tickInterval;
        public float RandomRadius => randomRadius;
        public bool FirstBarrageHitAtTarget => firstBarrageHitAtTarget;
        public int ChainCount => chainCount;
        public float ChainRadius => chainRadius;
        public IReadOnlyList<CommanderSkillWorkshopEffectDraft> Effects => effects;
        public GameObject CastingVfxPrefab => castingVfxPrefab;
        public float CastingVfxLifetime => castingVfxLifetime;
        public Vector3 CastingVfxLocalOffset => castingVfxLocalOffset;
        public Vector3 CastingVfxLocalEuler => castingVfxLocalEuler;
        public float CastingVfxScale => castingVfxScale;
        public AudioClip CastingSound => castingSound;
        public SfxCue CastingSfxSource => castingSfxSource;
        public GameObject CastVfxPrefab => castVfxPrefab;
        public float CastVfxLifetime => castVfxLifetime;
        public Vector3 CastVfxLocalOffset => castVfxLocalOffset;
        public Vector3 CastVfxLocalEuler => castVfxLocalEuler;
        public float CastVfxScale => castVfxScale;
        public AudioClip CastSound => castSound;
        public SfxCue CastSfxSource => castSfxSource;
        public GameObject ImpactVfxPrefab => impactVfxPrefab;
        public float ImpactVfxLifetime => impactVfxLifetime;
        public Vector3 ImpactVfxLocalOffset => impactVfxLocalOffset;
        public Vector3 ImpactVfxLocalEuler => impactVfxLocalEuler;
        public float ImpactVfxScale => impactVfxScale;
        public AudioClip ImpactSound => impactSound;
        public SfxCue ImpactSfxSource => impactSfxSource;
        public GameObject PersistentVfxPrefab => persistentVfxPrefab;
        public Vector3 PersistentVfxLocalOffset => persistentVfxLocalOffset;
        public Vector3 PersistentVfxLocalEuler => persistentVfxLocalEuler;
        public float PersistentVfxScale => persistentVfxScale;
        public CommanderMarkFeedbackAnchor PersistentVfxAnchor => persistentVfxAnchor;
        public bool RegisterInCatalog => registerInCatalog;
        public int MaxLevel => maxLevel;
        public int RequiredDuplicateCount => requiredDuplicateCount;
        public long BaseGoldCost => baseGoldCost;
        public float GoldCostGrowthMultiplier => goldCostGrowthMultiplier;
        public float MaxLevelEffectMultiplier => maxLevelEffectMultiplier;
        public bool IncludeInSummonPool => includeInSummonPool;
        public int MinimumSummonLevel => minimumSummonLevel;
        public int SummonWeight => summonWeight;

        public void ResetDraft(CommanderSkillCategory nextCategory)
        {
            loadedGrowthJson = string.Empty;
            skillId = string.Empty;
            displayName = nextCategory == CommanderSkillCategory.Attack
                ? "새 군단장 공격 스킬"
                : nextCategory == CommanderSkillCategory.Buff
                    ? "새 군단장 버프 스킬"
                    : "새 군단장 디버프 스킬";
            description = string.Empty;
            icon = null;
            category = nextCategory;
            rarity = CommanderSkillRarity.Common;
            castTime = 0.5f;
            autoUseCondition = CommanderSkillAutoUseCondition.Always;
            autoHealthThreshold = 0.85f;
            awakeningStages = Array.Empty<CommanderSkillAwakeningStage>();
            cooldown = 8f;
            targetTeam = nextCategory == CommanderSkillCategory.Buff
                ? CommanderSkillTargetTeam.Ally
                : CommanderSkillTargetTeam.Enemy;
            targetSelection = CommanderSkillTargetSelection.Nearest;
            targetRange = 20f;
            deliveryModule = MonsterBasicAttackDeliveryModule.Direct;
            damageKind = CommanderSkillDamageKind.Physical;
            baseDamage = 20f;
            perHitMultiplier = 1f;
            shape = MonsterBasicAttackShape.Circle;
            center = MonsterBasicAttackCenter.PrimaryTarget;
            radius = 2f;
            forwardOffset = 2f;
            angle = 90f;
            lineWidth = 2f;
            maxTargets = 8;
            projectilePrefab = null;
            projectileSpeed = 16f;
            trajectory = CommanderSkillTrajectory.Straight;
            arcHeight = 3f;
            patternType = CommanderSkillPatternType.Single;
            repeatCount = 1;
            repeatInterval = 0f;
            patternDuration = 1f;
            tickInterval = 1f;
            randomRadius = 0f;
            firstBarrageHitAtTarget = false;
            chainCount = 1;
            chainRadius = 4f;
            effects.Clear();
            if (nextCategory != CommanderSkillCategory.Attack)
            {
                effects.Add(CommanderSkillWorkshopEffectDraft.CreateDefault(nextCategory, 1));
            }
            castingVfxPrefab = null;
            castingVfxLifetime = 1f;
            castingVfxLocalOffset = Vector3.zero;
            castingVfxLocalEuler = Vector3.zero;
            castingVfxScale = 1f;
            castingSound = null;
            castingSfxSource = null;
            castVfxPrefab = null;
            castVfxLifetime = 1f;
            castVfxLocalOffset = Vector3.zero;
            castVfxLocalEuler = Vector3.zero;
            castVfxScale = 1f;
            castSound = null;
            castSfxSource = null;
            impactVfxPrefab = null;
            impactVfxLifetime = 1.5f;
            impactVfxLocalOffset = Vector3.zero;
            impactVfxLocalEuler = Vector3.zero;
            impactVfxScale = 1f;
            impactSound = null;
            impactSfxSource = null;
            persistentVfxPrefab = null;
            persistentVfxLocalOffset = Vector3.zero;
            persistentVfxLocalEuler = Vector3.zero;
            persistentVfxScale = 1f;
            persistentVfxAnchor = CommanderMarkFeedbackAnchor.WorldPosition;
            registerInCatalog = true;
            maxLevel = 200;
            requiredDuplicateCount = 1;
            baseGoldCost = 100L;
            goldCostGrowthMultiplier = 1.03f;
            maxLevelEffectMultiplier = 4.98f;
            includeInSummonPool = true;
            minimumSummonLevel = 1;
            summonWeight = 100;
        }

        public void SetCategory(CommanderSkillCategory nextCategory)
        {
            category = nextCategory;
            targetTeam = nextCategory == CommanderSkillCategory.Buff
                ? CommanderSkillTargetTeam.Ally
                : CommanderSkillTargetTeam.Enemy;
            if (nextCategory != CommanderSkillCategory.Attack && effects.Count == 0)
            {
                effects.Add(CommanderSkillWorkshopEffectDraft.CreateDefault(nextCategory, 1));
            }
            if (nextCategory != CommanderSkillCategory.Attack)
            {
                for (var index = 0; index < effects.Count; index++)
                {
                    if (!CommanderUnitEffectDefinition.IsCompatible(nextCategory, effects[index].EffectType))
                    {
                        effects[index] = CommanderSkillWorkshopEffectDraft.CreateDefault(nextCategory, index + 1);
                    }
                }
            }
        }

        public void Load(CommanderSkillDefinition source)
        {
            if (source == null)
            {
                ResetDraft(CommanderSkillCategory.Attack);
                return;
            }

            skillId = source.SkillId;
            displayName = source.DisplayName;
            description = source.Description;
            icon = source.Icon;
            category = source.Category;
            rarity = source.Rarity;
            castTime = source.CastTime;
            autoUseCondition = source.AutoUseCondition;
            autoHealthThreshold = source.AutoHealthThreshold;
            awakeningStages = source.AwakeningStages.Select(stage =>
                stage == null ? null : new CommanderSkillAwakeningStage(stage.CopyModifiers())).ToArray();
            cooldown = source.Cooldown;
            targetTeam = source.Targeting.TargetTeam;
            targetSelection = source.Targeting.Selection;
            targetRange = source.Targeting.Range;
            patternType = source.Pattern.Type;
            repeatCount = source.Pattern.RepeatCount;
            repeatInterval = source.Pattern.RepeatInterval;
            patternDuration = source.Pattern.Duration;
            tickInterval = source.Pattern.TickInterval;
            randomRadius = source.Pattern.RandomRadius;
            firstBarrageHitAtTarget = source.Pattern.FirstBarrageHitAtTarget;
            chainCount = source.Pattern.ChainCount;
            chainRadius = source.Pattern.ChainRadius;
            castingVfxPrefab = source.CastingVfxPrefab;
            castingVfxLifetime = source.CastingVfxLifetime;
            castingVfxLocalOffset = source.CastingVfxLocalOffset;
            castingVfxLocalEuler = source.CastingVfxLocalEuler;
            castingVfxScale = source.CastingVfxScale;
            castingSfxSource = source.CastingSfx;
            castingSound = source.CastingSfx == null ? null : source.CastingSfx.PrimaryClip;
            castVfxPrefab = source.CastVfxPrefab;
            castVfxLifetime = source.CastVfxLifetime;
            castVfxLocalOffset = source.CastVfxLocalOffset;
            castVfxLocalEuler = source.CastVfxLocalEuler;
            castVfxScale = source.CastVfxScale;
            castSfxSource = source.CastSfx;
            castSound = source.CastSfx == null ? null : source.CastSfx.PrimaryClip;
            impactVfxPrefab = source.ImpactVfxPrefab;
            impactVfxLifetime = source.ImpactVfxLifetime;
            impactVfxLocalOffset = source.ImpactVfxLocalOffset;
            impactVfxLocalEuler = source.ImpactVfxLocalEuler;
            impactVfxScale = source.ImpactVfxScale;
            impactSfxSource = source.ImpactSfx;
            impactSound = source.ImpactSfx == null ? null : source.ImpactSfx.PrimaryClip;
            persistentVfxPrefab = source.PersistentVfxPrefab;
            persistentVfxLocalOffset = source.PersistentVfxLocalOffset;
            persistentVfxLocalEuler = source.PersistentVfxLocalEuler;
            persistentVfxScale = source.PersistentVfxScale;
            persistentVfxAnchor = source.PersistentVfxAnchor;
            effects.Clear();

            if (source is CommanderAttackSkillDefinition attack)
            {
                deliveryModule = attack.DeliveryModule;
                projectilePrefab = attack.ProjectilePrefab;
                projectileSpeed = attack.ProjectileSpeed;
                trajectory = attack.Trajectory;
                arcHeight = attack.ArcHeight;
                var damage = attack.AreaDamageEffect;
                if (damage != null)
                {
                    damageKind = damage.DamageKind;
                    baseDamage = damage.BaseDamage;
                    perHitMultiplier = damage.PerHitMultiplier;
                    shape = damage.Shape;
                    center = damage.Center;
                    radius = damage.Radius;
                    forwardOffset = damage.ForwardOffset;
                    angle = damage.Angle;
                    lineWidth = damage.LineWidth;
                    maxTargets = damage.MaxTargets;
                }
                for (var index = 0; index < source.Effects.Count; index++)
                {
                    if (source.Effects[index] is CommanderPullEffectDefinition pullEffect)
                        effects.Add(CommanderSkillWorkshopEffectDraft.FromDefinition(pullEffect));
                    else if (source.Effects[index] is CommanderUnitEffectDefinition unitEffect)
                        effects.Add(CommanderSkillWorkshopEffectDraft.FromDefinition(unitEffect));
                    else if (source.Effects[index] is CommanderMarkEffectDefinition markEffect)
                    {
                        var draftEffect = CommanderSkillWorkshopEffectDraft.FromDefinition(markEffect);
                        if (!string.Equals(AssetDatabase.GetAssetPath(markEffect), AssetDatabase.GetAssetPath(source),
                                StringComparison.OrdinalIgnoreCase))
                            draftEffect.UseSharedMark(markEffect);
                        effects.Add(draftEffect);
                    }
                    else if (source.Effects[index] is CommanderRecordedHitDamageEffectDefinition recorded)
                        effects.Add(CommanderSkillWorkshopEffectDraft.FromDefinition(recorded));
                    else if (source.Effects[index] is CommanderGlobalModifierEffectDefinition modifier)
                        effects.Add(CommanderSkillWorkshopEffectDraft.FromDefinition(modifier));
                }
            }
            else
            {
                for (var index = 0; index < source.Effects.Count; index++)
                {
                    if (source.Effects[index] is CommanderUnitEffectDefinition effect)
                    {
                        effects.Add(CommanderSkillWorkshopEffectDraft.FromDefinition(effect));
                    }
                    else if (source.Effects[index] is CommanderMarkEffectDefinition mark)
                    {
                        var draftEffect = CommanderSkillWorkshopEffectDraft.FromDefinition(mark);
                        if (!string.Equals(AssetDatabase.GetAssetPath(mark), AssetDatabase.GetAssetPath(source),
                                StringComparison.OrdinalIgnoreCase))
                            draftEffect.UseSharedMark(mark);
                        effects.Add(draftEffect);
                    }
                    else if (source.Effects[index] is CommanderRecordedHitDamageEffectDefinition recorded)
                    {
                        effects.Add(CommanderSkillWorkshopEffectDraft.FromDefinition(recorded));
                    }
                    else if (source.Effects[index] is CommanderGlobalModifierEffectDefinition modifier)
                    {
                        effects.Add(CommanderSkillWorkshopEffectDraft.FromDefinition(modifier));
                    }
                }
            }
        }

        public void LoadGrowth(CommanderSkillGrowthRule rule, bool registered)
        {
            loadedGrowthJson = rule == null ? string.Empty : JsonUtility.ToJson(rule);
            registerInCatalog = registered;
            if (rule == null)
            {
                return;
            }
            maxLevel = rule.MaxLevel;
            requiredDuplicateCount = rule.RequiredDuplicateCount;
            baseGoldCost = rule.BaseGoldCost;
            goldCostGrowthMultiplier = rule.GoldCostGrowthMultiplier;
            maxLevelEffectMultiplier = rule.GetDamageMultiplier(rule.MaxLevel);
        }

        public CommanderSkillGrowthRule BuildGrowthRule()
        {
            var previous = string.IsNullOrEmpty(loadedGrowthJson) ? null :
                JsonUtility.FromJson<CommanderSkillGrowthRule>(loadedGrowthJson);
            var curve = previous != null && previous.MaxLevel == MaxLevel &&
                Mathf.Approximately(previous.GetDamageMultiplier(MaxLevel), MaxLevelEffectMultiplier)
                ? previous.CopyDamageCurve()
                : AnimationCurve.Linear(1f, 1f, MaxLevel, MaxLevelEffectMultiplier);
            var rule = new CommanderSkillGrowthRule();
            rule.EditorConfigure(SkillId, MaxLevel, RequiredDuplicateCount, curve, BaseGoldCost, GoldCostGrowthMultiplier);
            rule.SetSupportCurves(previous?.CopyRatioCurve(), previous?.CopyControlCurve());
            return rule;
        }

        public void LoadSummon(CommanderSkillSummonConfig config, bool registered)
        {
            includeInSummonPool = false;
            minimumSummonLevel = 1;
            summonWeight = 100;
            if (!registered || config == null)
            {
                return;
            }

            for (var levelIndex = 0; levelIndex < config.Levels.Count; levelIndex++)
            {
                var pool = config.Levels[levelIndex]?.Pool;
                if (pool == null)
                {
                    continue;
                }

                for (var entryIndex = 0; entryIndex < pool.Count; entryIndex++)
                {
                    var entry = pool[entryIndex];
                    if (entry == null || !string.Equals(entry.SkillId, SkillId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    includeInSummonPool = true;
                    minimumSummonLevel = levelIndex + 1;
                    summonWeight = entry.Weight;
                    return;
                }
            }
        }

        public void PrepareFork(string nextSkillId, string nextDisplayName)
        {
            skillId = nextSkillId?.Trim() ?? string.Empty;
            displayName = nextDisplayName?.Trim() ?? string.Empty;
            castingSfxSource = null;
            castSfxSource = null;
            impactSfxSource = null;
        }

        public void NormalizeCatalogOptions()
        {
            if (!registerInCatalog)
            {
                includeInSummonPool = false;
            }
        }
    }
}
