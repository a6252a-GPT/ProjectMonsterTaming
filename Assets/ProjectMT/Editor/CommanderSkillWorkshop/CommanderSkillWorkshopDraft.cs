using System;
using System.Collections.Generic;
using ProjectMT.Features.CommanderSkill;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.EditorTools.CommanderSkillWorkshop
{
    [Serializable]
    internal sealed class CommanderSkillWorkshopEffectDraft // 효과형 스킬을 조립하는 한 효과 카드
    {
        [SerializeField] private string effectId = "effect_01";
        [SerializeField] private CommanderSkillUnitEffectType effectType = CommanderSkillUnitEffectType.Heal;
        [SerializeField] private CommanderSkillEffectValueSource valueSource =
            CommanderSkillEffectValueSource.TargetMissingHealthRatio;
        [SerializeField, Min(0f)] private float magnitude = 0.25f;
        [SerializeField, Min(0f)] private float duration;
        [SerializeField] private CommanderSkillEffectScope scope = CommanderSkillEffectScope.Area;
        [SerializeField, Min(0.1f)] private float radius = 5f;
        [SerializeField, Min(1)] private int maxTargets = 8;
        [SerializeField] private MonsterBuffStackPolicy stackPolicy = MonsterBuffStackPolicy.RefreshDuration;

        public string EffectId => effectId?.Trim() ?? string.Empty;
        public CommanderSkillUnitEffectType EffectType => effectType;
        public CommanderSkillEffectValueSource ValueSource => valueSource;
        public float Magnitude => magnitude;
        public float Duration => duration;
        public CommanderSkillEffectScope Scope => scope;
        public float Radius => radius;
        public int MaxTargets => maxTargets;
        public MonsterBuffStackPolicy StackPolicy => stackPolicy;

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
    }

    internal sealed class CommanderSkillWorkshopDraft : ScriptableObject // 실제 자산 저장 전 격리된 편집 모델
    {
        [Header("Identity")]
        [SerializeField] private string skillId;
        [SerializeField] private string displayName = "새 군단장 공격 스킬";
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private CommanderSkillCategory category = CommanderSkillCategory.Attack;

        [Header("Cast Flow")]
        [SerializeField, Min(0f)] private float castTime = 0.5f;
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

        [Header("Catalog")]
        [SerializeField] private bool registerInCatalog = true;
        [SerializeField, Min(1)] private int maxLevel = 200;
        [SerializeField, Min(1)] private int requiredDuplicateCount = 1;
        [SerializeField, Min(0.01f)] private float maxLevelEffectMultiplier = 4.98f;

        [Header("Summon")]
        [SerializeField] private bool includeInSummonPool = true;
        [SerializeField, Min(1)] private int minimumSummonLevel = 1;
        [SerializeField, Min(1)] private int summonWeight = 100;

        public string SkillId => skillId?.Trim() ?? string.Empty;
        public string DisplayName => displayName?.Trim() ?? string.Empty;
        public string Description => description?.Trim() ?? string.Empty;
        public Sprite Icon => icon;
        public CommanderSkillCategory Category => category;
        public float CastTime => castTime;
        public float Cooldown => cooldown;
        public CommanderSkillTargetTeam TargetTeam => targetTeam;
        public CommanderSkillTargetSelection TargetSelection => targetSelection;
        public float TargetRange => targetRange;
        public MonsterBasicAttackDeliveryModule DeliveryModule => deliveryModule;
        public CommanderSkillDamageKind DamageKind => damageKind;
        public float BaseDamage => baseDamage;
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
        public bool RegisterInCatalog => registerInCatalog;
        public int MaxLevel => maxLevel;
        public int RequiredDuplicateCount => requiredDuplicateCount;
        public float MaxLevelEffectMultiplier => maxLevelEffectMultiplier;
        public bool IncludeInSummonPool => includeInSummonPool;
        public int MinimumSummonLevel => minimumSummonLevel;
        public int SummonWeight => summonWeight;

        public void ResetDraft(CommanderSkillCategory nextCategory)
        {
            skillId = string.Empty;
            displayName = nextCategory == CommanderSkillCategory.Attack
                ? "새 군단장 공격 스킬"
                : nextCategory == CommanderSkillCategory.Buff
                    ? "새 군단장 버프 스킬"
                    : "새 군단장 디버프 스킬";
            description = string.Empty;
            icon = null;
            category = nextCategory;
            castTime = 0.5f;
            cooldown = 8f;
            targetTeam = nextCategory == CommanderSkillCategory.Buff
                ? CommanderSkillTargetTeam.Ally
                : CommanderSkillTargetTeam.Enemy;
            targetSelection = CommanderSkillTargetSelection.Nearest;
            targetRange = 20f;
            deliveryModule = MonsterBasicAttackDeliveryModule.Direct;
            damageKind = CommanderSkillDamageKind.Physical;
            baseDamage = 20f;
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
            registerInCatalog = true;
            maxLevel = 200;
            requiredDuplicateCount = 1;
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
            castTime = source.CastTime;
            cooldown = source.Cooldown;
            targetTeam = source.Targeting.TargetTeam;
            targetSelection = source.Targeting.Selection;
            targetRange = source.Targeting.Range;
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
                    shape = damage.Shape;
                    center = damage.Center;
                    radius = damage.Radius;
                    forwardOffset = damage.ForwardOffset;
                    angle = damage.Angle;
                    lineWidth = damage.LineWidth;
                    maxTargets = damage.MaxTargets;
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
                }
            }
        }

        public void LoadGrowth(CommanderSkillGrowthRule rule, bool registered)
        {
            registerInCatalog = registered;
            if (rule == null)
            {
                return;
            }
            maxLevel = rule.MaxLevel;
            requiredDuplicateCount = rule.RequiredDuplicateCount;
            maxLevelEffectMultiplier = rule.GetDamageMultiplier(rule.MaxLevel);
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
