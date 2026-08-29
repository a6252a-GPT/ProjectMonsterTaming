using System;
using System.Collections.Generic;
using ProjectMT.Contents.CastleRaidHex;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    [Serializable]
    public sealed class MonsterMakerFeedbackDraft // 한 애니메이션 시점에 붙이는 선택 사운드·VFX 입력
    {
        [SerializeField] private AudioClip sound;
        [SerializeField, HideInInspector] private SfxCue sfx; // 기존 Draft 수동 Cue 호환
        [SerializeField] private GameObject vfxPrefab;
        [SerializeField, Min(0.01f)] private float vfxLifetime = 1f;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField, Min(0.01f)] private float scale = 1f;

        public AudioClip Sound => sound;
        public SfxCue Sfx => sfx;
        public GameObject VfxPrefab => vfxPrefab;
        public float VfxLifetime => Mathf.Max(0.01f, vfxLifetime);
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalEulerAngles => localEulerAngles;
        public float Scale => Mathf.Max(0.01f, scale);
        public bool HasSound => sound != null || sfx != null;
        public bool HasAny => HasSound || vfxPrefab != null;
    }

    [Serializable]
    public sealed class MonsterMakerActivePresentationSlotDraft // 공간 ID를 유지하는 몬스터별 VFX/SFX 입력
    {
        [SerializeField] private string slotId;
        [SerializeField] private MonsterMakerFeedbackDraft feedback = new MonsterMakerFeedbackDraft();

        public string SlotId => slotId ?? string.Empty;
        public MonsterMakerFeedbackDraft Feedback => feedback ??= new MonsterMakerFeedbackDraft();

#if UNITY_EDITOR
        public void EditorConfigure(string id, MonsterMakerFeedbackDraft source = null)
        {
            slotId = id?.Trim();
            feedback = source ?? new MonsterMakerFeedbackDraft();
        }
#endif
    }

    [Serializable]
    public sealed class MonsterMakerActiveStepPresentationDraft // Step/공간 ID를 유지하는 몬스터별 연출 연결
    {
        [SerializeField] private string stepId;
        [SerializeField] private bool motionConfigured;
        [SerializeField] private AnimationClip motionClip;
        [SerializeField, Min(0.01f)] private float motionPlaybackSpeed = 1f;
        [SerializeField, Min(0f)] private float motionCrossFadeDuration = 0.08f;
        [SerializeField, Range(0f, 1f)] private float motionCommitNormalizedTime = 0.25f;
        [SerializeField] private MonsterMakerFeedbackDraft telegraph = new MonsterMakerFeedbackDraft();
        [SerializeField] private MonsterMakerFeedbackDraft launch = new MonsterMakerFeedbackDraft();
        [SerializeField] private MonsterMakerFeedbackDraft travel = new MonsterMakerFeedbackDraft();
        [SerializeField] private MonsterMakerFeedbackDraft impact = new MonsterMakerFeedbackDraft();
        [SerializeField] private MonsterMakerFeedbackDraft teleportExit = new MonsterMakerFeedbackDraft();
        [SerializeField] private MonsterMakerFeedbackDraft teleportEnter = new MonsterMakerFeedbackDraft();
        [SerializeField] private List<MonsterMakerActivePresentationSlotDraft> slots =
            new List<MonsterMakerActivePresentationSlotDraft>();

        public string StepId => stepId ?? string.Empty;
        public AnimationClip MotionClip => motionClip;
        public float MotionPlaybackSpeed => Mathf.Max(0.01f, motionPlaybackSpeed);
        public float MotionCrossFadeDuration => Mathf.Max(0f, motionCrossFadeDuration);
        public float MotionCommitNormalizedTime => Mathf.Clamp01(motionCommitNormalizedTime);
        public MonsterMakerFeedbackDraft Telegraph => telegraph;
        public MonsterMakerFeedbackDraft Launch => launch;
        public MonsterMakerFeedbackDraft Travel => travel;
        public MonsterMakerFeedbackDraft Impact => impact;
        public MonsterMakerFeedbackDraft TeleportExit => teleportExit;
        public MonsterMakerFeedbackDraft TeleportEnter => teleportEnter;
        public IReadOnlyList<MonsterMakerActivePresentationSlotDraft> Slots => slots ??
            (IReadOnlyList<MonsterMakerActivePresentationSlotDraft>)
            Array.Empty<MonsterMakerActivePresentationSlotDraft>();

        public MonsterMakerActivePresentationSlotDraft ResolveSlot(string id)
        {
            for (var index = 0; index < Slots.Count; index++)
            {
                var candidate = Slots[index];
                if (candidate != null && string.Equals(candidate.SlotId, id, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            return null;
        }

#if UNITY_EDITOR
        public void EditorSetStepId(string id) { stepId = id?.Trim(); }

        public void EditorEnsureMotion(
            AnimationClip fallbackClip,
            float fallbackSpeed,
            float fallbackFadeDuration,
            float fallbackCommitTime)
        {
            if (motionConfigured) return;
            motionConfigured = true;
            motionClip = fallbackClip;
            motionPlaybackSpeed = Mathf.Max(0.01f, fallbackSpeed);
            motionCrossFadeDuration = Mathf.Max(0f, fallbackFadeDuration);
            motionCommitNormalizedTime = Mathf.Clamp01(fallbackCommitTime);
        }

        public void EditorSyncSlots(MonsterActiveAttackStep step)
        {
            slots ??= new List<MonsterMakerActivePresentationSlotDraft>();
            if (step == null)
            {
                slots.Clear();
                return;
            }

            var synced = new List<MonsterMakerActivePresentationSlotDraft>(step.PresentationSlots.Count);
            var migratedTimings = new HashSet<MonsterActivePresentationEvent>();
            for (var slotIndex = 0; slotIndex < step.PresentationSlots.Count; slotIndex++)
            {
                var contract = step.PresentationSlots[slotIndex];
                if (contract == null) continue;
                MonsterMakerActivePresentationSlotDraft existing = null;
                for (var index = 0; index < slots.Count; index++)
                {
                    if (slots[index] != null && string.Equals(
                            slots[index].SlotId,
                            contract.SlotId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        existing = slots[index];
                        break;
                    }
                }
                if (existing == null)
                {
                    existing = new MonsterMakerActivePresentationSlotDraft();
                    var legacy = migratedTimings.Add(contract.Timing)
                        ? ResolveLegacyFeedback(contract.Timing)
                        : null;
                    existing.EditorConfigure(contract.SlotId, legacy);
                }
                synced.Add(existing);
            }
            slots = synced;
        }

        private MonsterMakerFeedbackDraft ResolveLegacyFeedback(MonsterActivePresentationEvent timing)
        {
            return timing switch
            {
                MonsterActivePresentationEvent.Telegraph => telegraph,
                MonsterActivePresentationEvent.Launch => launch,
                MonsterActivePresentationEvent.Travel => travel,
                MonsterActivePresentationEvent.Impact => impact,
                MonsterActivePresentationEvent.TeleportExit => teleportExit,
                MonsterActivePresentationEvent.TeleportEnter => teleportEnter,
                _ => null
            };
        }
#endif
    }

    [Serializable]
    public sealed class MonsterMakerMarkerDraft // 제작자가 직접 찍는 타격 시점
    {
        [SerializeField, Range(0f, 1f)] private float normalizedTime = 0.5f;
        [SerializeField, Min(0f)] private float powerRatio = 1f;
        [SerializeField] private string socketOverride;

        public float NormalizedTime => normalizedTime;
        public float PowerRatio => powerRatio;
        public string SocketOverride => socketOverride ?? string.Empty;
    }

    [Serializable]
    public sealed class MonsterMakerAttackDraft // 한 공격 Clip과 Marker 묶음
    {
        [SerializeField] private string motionId = "attack01";
        [SerializeField] private AnimationClip clip;
        [SerializeField, Min(0.01f)] private float playbackSpeed = 1f;
        [SerializeField, Min(0f)] private float crossFadeDuration = 0.06f;
        [SerializeField, Min(0f)] private float weight = 1f;
        [SerializeField] private bool preventImmediateRepeat;
        [SerializeField] private bool overrideBreathDuration;
        [SerializeField, Min(0.01f)] private float breathDuration = 0.8f;
        [SerializeField] private List<MonsterMakerMarkerDraft> markers = new List<MonsterMakerMarkerDraft>
        {
            new MonsterMakerMarkerDraft()
        };

        public string MotionId => motionId ?? string.Empty;
        public AnimationClip Clip => clip;
        public float PlaybackSpeed => Mathf.Max(0.01f, playbackSpeed);
        public float CrossFadeDuration => Mathf.Max(0f, crossFadeDuration);
        public float Weight => Mathf.Max(0f, weight);
        public bool PreventImmediateRepeat => preventImmediateRepeat;
        public bool OverrideBreathDuration => overrideBreathDuration;
        public float BreathDuration => Mathf.Max(0.01f, breathDuration);
        public IReadOnlyList<MonsterMakerMarkerDraft> Markers => markers ??
            (IReadOnlyList<MonsterMakerMarkerDraft>)Array.Empty<MonsterMakerMarkerDraft>();

        public float ResolveBreathDuration(float profileDefault)
        {
            return overrideBreathDuration ? BreathDuration : Mathf.Max(0.01f, profileDefault);
        }
    }

    [Serializable]
    public sealed class MonsterMakerAbilityDraft // 돌파 2·4는 새 버튼이 아니라 기존 스킬 강화
    {
        [SerializeField] private string abilityId;
        [SerializeField] private string displayName;
        [SerializeField, HideInInspector] private MonsterAbilityMode mode = MonsterAbilityMode.Passive;
        [SerializeField, HideInInspector] private string triggerPolicyId;
        [SerializeField] private MonsterSkillAugmentOperation augmentOperation =
            MonsterSkillAugmentOperation.MagnitudeMultiplier;
        [SerializeField, Min(0f)] private float augmentScalarValue = 0.15f;
        [SerializeField, Min(1)] private int augmentIntegerValue = 1;

        public string AbilityId => abilityId ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public MonsterAbilityMode Mode => mode;
        public string TriggerPolicyId => triggerPolicyId ?? string.Empty;
        public MonsterSkillAugmentOperation AugmentOperation => augmentOperation;
        public float AugmentScalarValue => Mathf.Max(0f, augmentScalarValue);
        public int AugmentIntegerValue => Mathf.Max(1, augmentIntegerValue);
    }

    [Serializable]
    public sealed class MonsterMakerPassiveTuningDraft // 공용 동작을 쓰되 몬스터마다 독립적으로 저장하는 수치
    {
        [SerializeField] private bool initialized;
        [SerializeField] private GenericMonsterPassiveRuntimeKind runtimeKind;
        [SerializeField, Min(0f)] private float primaryBase;
        [SerializeField, Min(0f)] private float primaryPerLevelStep;
        [SerializeField, Min(0f)] private float secondaryBase;
        [SerializeField, Min(0f)] private float secondaryPerLevelStep;
        [SerializeField, Min(1)] private int triggerCount = 1;
        [SerializeField, Min(1)] private int maxStacks = 1;
        [SerializeField, Min(0f)] private float duration;
        [SerializeField, Min(0f)] private float cooldown;
        [SerializeField, Min(0f)] private float threshold;
        [SerializeField, Min(0f)] private float radius;
        [SerializeField, Min(1)] private int maxTargets = 1;

        public bool Initialized => initialized;
        public GenericMonsterPassiveRuntimeKind RuntimeKind => runtimeKind;
        public float PrimaryBase => Mathf.Max(0f, primaryBase);
        public float PrimaryPerLevelStep => Mathf.Max(0f, primaryPerLevelStep);
        public float SecondaryBase => Mathf.Max(0f, secondaryBase);
        public float SecondaryPerLevelStep => Mathf.Max(0f, secondaryPerLevelStep);
        public int TriggerCount => Mathf.Max(1, triggerCount);
        public int MaxStacks => Mathf.Max(1, maxStacks);
        public float Duration => Mathf.Max(0f, duration);
        public float Cooldown => Mathf.Max(0f, cooldown);
        public float Threshold => Mathf.Max(0f, threshold);
        public float Radius => Mathf.Max(0f, radius);
        public int MaxTargets => Mathf.Max(1, maxTargets);

        public bool Matches(GenericMonsterPassiveSkill template)
        {
            return initialized && template != null && runtimeKind == template.RuntimeKind;
        }

        public bool TryValidate(GenericMonsterPassiveSkill template, out string error)
        {
            if (!Matches(template))
            {
                error = "선택한 패시브 종류의 몬스터 전용 수치가 아직 준비되지 않았습니다.";
                return false;
            }
            if (float.IsNaN(primaryBase) || float.IsInfinity(primaryBase) || primaryBase < 0f ||
                float.IsNaN(primaryPerLevelStep) || float.IsInfinity(primaryPerLevelStep) || primaryPerLevelStep < 0f ||
                float.IsNaN(secondaryBase) || float.IsInfinity(secondaryBase) || secondaryBase < 0f ||
                float.IsNaN(secondaryPerLevelStep) || float.IsInfinity(secondaryPerLevelStep) || secondaryPerLevelStep < 0f)
            {
                error = "효과 수치와 레벨 증가량은 0 이상의 유효한 값이어야 합니다.";
                return false;
            }
            if (RequiresDuration(runtimeKind) && duration <= 0f)
            {
                error = "이 패시브의 지속시간은 0보다 커야 합니다.";
                return false;
            }
            if (runtimeKind == GenericMonsterPassiveRuntimeKind.LongRangeAim && threshold <= 0f)
            {
                error = "장거리 조준의 최소 거리는 0보다 커야 합니다.";
                return false;
            }
            if (runtimeKind == GenericMonsterPassiveRuntimeKind.FrontlineBond && radius <= 0f)
            {
                error = "진형 결속의 아군 탐색 반경은 0보다 커야 합니다.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void CopyFrom(GenericMonsterPassiveSkill source)
        {
            initialized = source != null;
            runtimeKind = source == null ? GenericMonsterPassiveRuntimeKind.None : source.RuntimeKind;
            primaryBase = source?.PrimaryBase ?? 0f;
            primaryPerLevelStep = source?.PrimaryPerLevelStep ?? 0f;
            secondaryBase = source?.SecondaryBase ?? 0f;
            secondaryPerLevelStep = source?.SecondaryPerLevelStep ?? 0f;
            triggerCount = source?.TriggerCount ?? 1;
            maxStacks = source?.MaxStacks ?? 1;
            duration = source?.Duration ?? 0f;
            cooldown = source?.Cooldown ?? 0f;
            threshold = source?.Threshold ?? 0f;
            radius = source?.Radius ?? 0f;
            maxTargets = source?.MaxTargets ?? 1;
        }

        private static bool RequiresDuration(GenericMonsterPassiveRuntimeKind kind)
        {
            return kind == GenericMonsterPassiveRuntimeKind.SameTargetHaste ||
                   kind == GenericMonsterPassiveRuntimeKind.ImpactStrike ||
                   kind == GenericMonsterPassiveRuntimeKind.CrisisDefense ||
                   kind == GenericMonsterPassiveRuntimeKind.FractureMark ||
                   kind == GenericMonsterPassiveRuntimeKind.ThreatMark ||
                   kind == GenericMonsterPassiveRuntimeKind.EmergencyEntry ||
                   kind == GenericMonsterPassiveRuntimeKind.FirstWave;
        }
    }

    [CreateAssetMenu(menuName = "ProjectMT/Monster Maker/Draft", fileName = "Draft_monster")]
    public sealed class MonsterMakerDraft : ScriptableObject // 생성 전 사람의 결정을 보존하는 Editor 전용 원본
    {
        [SerializeField] private string monsterId;
        [SerializeField] private string displayName;
        [SerializeField] private MonsterRarity rarity = MonsterRarity.Common;
        [SerializeField] private Sprite portrait;
        [SerializeField] private bool skillLoadoutConfigured;
        [SerializeField] private MonsterPassiveSkill rarityPassiveSkill;
        [SerializeField] private MonsterMakerPassiveTuningDraft passiveTuning = new MonsterMakerPassiveTuningDraft();
        [SerializeField] private MonsterActiveSkill rarityActiveSkill;
        [SerializeField] private MonsterActiveAttackProfile activeAttackProfile;
        [SerializeField] private string activeSkillName;
        [SerializeField, Min(1)] private int activeEnergyMaximum = 1000;
        [SerializeField] private AnimationClip activeSkillClip;
        [SerializeField, Min(0.01f)] private float activeSkillPlaybackSpeed = 1f;
        [SerializeField, Min(0f)] private float activeSkillCrossFadeDuration = 0.08f;
        [SerializeField, Range(0f, 1f)] private float activeSkillCommitNormalizedTime = 0.25f;
        [SerializeField, HideInInspector] private bool activeStepMotionModeConfigured;
        [SerializeField] private bool useCustomActiveStepMotions;
        [SerializeField] private List<MonsterActiveAttackStepTuning> activeAttackStepTunings =
            new List<MonsterActiveAttackStepTuning>();
        [SerializeField] private List<MonsterMakerActiveStepPresentationDraft> activeAttackPresentations =
            new List<MonsterMakerActiveStepPresentationDraft>();
        [SerializeField, TextArea(2, 5)] private string productionMemo;

        [SerializeField] private GameObject vendorPrefab;
        [SerializeField] private Animator animatorSource;
        [SerializeField] private Vector3 visualScale = Vector3.one;
        [SerializeField] private Vector3 visualLocalPosition;
        [SerializeField] private float groundOffset;
        [SerializeField] private float facingYawOffset;
        [SerializeField, Min(0.01f)] private float bodyRadius = 0.5f;
        [SerializeField, Min(0.01f)] private float bodyHeight = 1f;
        [SerializeField, Min(0.01f)] private float selectionRadius = 0.65f;
        [SerializeField, Min(0f)] private float hpBarHeight = 1.2f;
        [SerializeField] private string attackOriginPath = "AttackOrigin";
        [SerializeField] private string hitCenterPath = "HitCenter";
        [SerializeField] private Vector3 attackOriginLocalPosition = new Vector3(0f, 0.5f, 0.6f);
        [SerializeField] private Vector3 hitCenterLocalPosition = new Vector3(0f, 0.5f, 0f);
        [SerializeField] private MonsterRigMode rigMode = MonsterRigMode.Generic;
        [SerializeField, Min(0.01f)] private float previewScale = 1f;
        [SerializeField, Min(0.01f)] private float vfxScale = 1f;

        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0f)] private float attackPower = 10f;
        [SerializeField, Min(0f)] private float defense;
        [SerializeField, Min(0.01f)] private float attackSpeed = 1f;
        [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(0.01f)] private float attackRange = 1f;

        [SerializeField] private MonsterImpactStrength impactStrength = MonsterImpactStrength.Standard;
        [SerializeField] private MonsterReactionWeight reactionWeight = MonsterReactionWeight.Standard;
        [SerializeField] private MainBattleMonsterRole mainBattleRole = MainBattleMonsterRole.Vanguard;
        [SerializeField] private UnitTargetPriority mainBattleTargetPriority = UnitTargetPriority.Nearest;
        [SerializeField, Range(0.2f, 1f)] private float mainBattlePreferredRangeRatio = 0.72f;
        [SerializeField, Range(0f, 0.95f)] private float mainBattleRetreatRangeRatio;
        [SerializeField, Range(0.08f, 1f)] private float mainBattleRetargetInterval = 0.2f;

        [SerializeField] private AnimationClip idleClip;
        [SerializeField, Min(0.01f)] private float idleSpeed = 1f;
        [SerializeField] private AnimationClip moveClip;
        [SerializeField, Min(0.01f)] private float movePlaybackSpeed = 1f;
        [SerializeField] private List<MonsterMakerAttackDraft> attacks = new List<MonsterMakerAttackDraft>
        {
            new MonsterMakerAttackDraft()
        };
        [SerializeField] private AnimationClip deathClip;
        [SerializeField, Min(0.01f)] private float deathSpeed = 1f;

        [SerializeField] private MonsterCombatType combatType = MonsterCombatType.Melee;
        [SerializeField] private MonsterBasicAttackProfile basicAttackProfile;
        [SerializeField] private List<MonsterBasicAttackVfxBinding> basicAttackVfxBindings =
            new List<MonsterBasicAttackVfxBinding>();
        [SerializeField] private MonsterMeleeAttackMode meleeMode = MonsterMeleeAttackMode.Single;
        [SerializeField] private MonsterMeleeAreaCenter meleeAreaCenter = MonsterMeleeAreaCenter.PrimaryTarget;
        [SerializeField, Min(0.01f)] private float meleeAreaRadius = 1.5f;
        [SerializeField, Min(1)] private int meleeMaxTargets = 4;
        [SerializeField] private MonsterRangedDeliveryMode rangedDeliveryMode = MonsterRangedDeliveryMode.Projectile;
        [SerializeField] private MonsterProjectileAttackMode projectileMode = MonsterProjectileAttackMode.Single;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private bool overrideProjectileTuning;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 9f;
        [SerializeField, Min(0.01f)] private float projectileLifetime = 3f;
        [SerializeField, Min(0.01f)] private float projectileHitRadius = 0.25f;
        [SerializeField, Min(1)] private int projectileMaxPiercingTargets = 2;
        [SerializeField, Min(0.01f)] private float projectileImpactRadius = 1.5f;
        [SerializeField, Min(1)] private int projectileMaxImpactTargets = 4;
        [SerializeField, Min(0f)] private float projectileLaunchRecoilDistance;
        [SerializeField, Min(0.01f)] private float projectileLaunchRecoilDuration = 0.12f;
        [SerializeField] private string specialEffectId;
        [SerializeField] private MonsterBuffTargetTeam specialTargetTeam = MonsterBuffTargetTeam.Allies;
        [SerializeField, Min(0.01f)] private float specialRadius = 2f;
        [SerializeField, Min(1)] private int specialMaxTargets = 5;
        [SerializeField, Min(0.01f)] private float specialDuration = 3f;
        [SerializeField] private MonsterBuffStackPolicy specialStackPolicy = MonsterBuffStackPolicy.RefreshDuration;
        [SerializeField] private MonsterStatModifier specialModifier;

        [SerializeField] private HexCastleAssaultPattern castleRaidAiPattern = HexCastleAssaultPattern.GeneralAdvance;
        [SerializeField] private HexCastleAssaultSupportFocus castleRaidSupportFocus = HexCastleAssaultSupportFocus.Adaptive;
        [SerializeField, Min(1f)] private float castleRaidSupportRange = 5f;
        [SerializeField, Min(0.1f)] private float castleRaidSupportCooldown = 4f;
        [SerializeField, Min(0.1f)] private float castleRaidSupportDuration = 5f;
        [SerializeField, Range(0f, 1f)] private float castleRaidHealRatio = 0.24f;
        [SerializeField, Range(0f, 1f)] private float castleRaidAttackBuffRate = 0.2f;
        [SerializeField, Range(0.05f, 1f)] private float castleRaidDefenseDamageMultiplier = 0.75f;

        [SerializeField] private bool ascensionConfigured;
        [SerializeField] private MonsterStatModifier ascension1;
        [SerializeField] private MonsterMakerAbilityDraft ascension2 = new MonsterMakerAbilityDraft();
        [SerializeField] private MonsterStatModifier ascension3;
        [SerializeField] private MonsterMakerAbilityDraft ascension4 = new MonsterMakerAbilityDraft();
        [SerializeField] private MonsterStatModifier ascension5;

        [SerializeField] private MonsterMakerFeedbackDraft spawnFeedback = new MonsterMakerFeedbackDraft();
        [SerializeField] private MonsterMakerFeedbackDraft hitFeedback = new MonsterMakerFeedbackDraft();
        [SerializeField] private MonsterMakerFeedbackDraft deathFeedback = new MonsterMakerFeedbackDraft();
        [SerializeField] private MonsterMakerFeedbackDraft specialFeedback = new MonsterMakerFeedbackDraft();

        public string MonsterId => monsterId ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public MonsterRarity Rarity => rarity;
        public Sprite Portrait => portrait;
        public bool SkillLoadoutConfigured => skillLoadoutConfigured;
        public MonsterPassiveSkill RarityPassiveSkill => rarityPassiveSkill;
        public MonsterMakerPassiveTuningDraft PassiveTuning => passiveTuning;
        public MonsterActiveSkill RarityActiveSkill => rarityActiveSkill;
        public MonsterActiveAttackProfile ActiveAttackProfile => activeAttackProfile;
        public string ActiveSkillName => string.IsNullOrWhiteSpace(activeSkillName)
            ? activeAttackProfile?.DisplayName ?? string.Empty
            : activeSkillName.Trim();
        public int ActiveEnergyMaximum => activeEnergyMaximum;
        public AnimationClip ActiveSkillClip => activeSkillClip;
        public float ActiveSkillPlaybackSpeed => activeSkillPlaybackSpeed;
        public float ActiveSkillCrossFadeDuration => activeSkillCrossFadeDuration;
        public float ActiveSkillCommitNormalizedTime => activeSkillCommitNormalizedTime;
        public bool UseCustomActiveStepMotions => useCustomActiveStepMotions;
        public IReadOnlyList<MonsterActiveAttackStepTuning> ActiveAttackStepTunings =>
            activeAttackStepTunings ??
            (IReadOnlyList<MonsterActiveAttackStepTuning>)Array.Empty<MonsterActiveAttackStepTuning>();
        public IReadOnlyList<MonsterMakerActiveStepPresentationDraft> ActiveAttackPresentations =>
            activeAttackPresentations ??
            (IReadOnlyList<MonsterMakerActiveStepPresentationDraft>)
            Array.Empty<MonsterMakerActiveStepPresentationDraft>();
        public string ProductionMemo => productionMemo ?? string.Empty;
        public GameObject VendorPrefab => vendorPrefab;
        public Animator AnimatorSource => animatorSource;
        public Vector3 VisualScale => visualScale;
        public Vector3 VisualLocalPosition => visualLocalPosition;
        public float GroundOffset => groundOffset;
        public float FacingYawOffset => facingYawOffset;
        public float BodyRadius => bodyRadius;
        public float BodyHeight => bodyHeight;
        public float SelectionRadius => selectionRadius;
        public float HpBarHeight => hpBarHeight;
        public string AttackOriginPath => attackOriginPath ?? string.Empty;
        public string HitCenterPath => hitCenterPath ?? string.Empty;
        public Vector3 AttackOriginLocalPosition => attackOriginLocalPosition;
        public Vector3 HitCenterLocalPosition => hitCenterLocalPosition;
        public MonsterRigMode RigMode => rigMode;
        public float PreviewScale => previewScale;
        public float VfxScale => vfxScale;
        public float MaxHealth => maxHealth;
        public float AttackPower => attackPower;
        public float Defense => defense;
        public float AttackSpeed => attackSpeed;
        public float MoveSpeed => moveSpeed;
        public float AttackRange => attackRange;
        public MonsterImpactStrength ImpactStrength => impactStrength;
        public MonsterReactionWeight ReactionWeight => reactionWeight;
        public MainBattleMonsterRole MainBattleRole => mainBattleRole;
        public UnitTargetPriority MainBattleTargetPriority => mainBattleTargetPriority;
        public float MainBattlePreferredRangeRatio => Mathf.Clamp(mainBattlePreferredRangeRatio, 0.2f, 1f);
        public float MainBattleRetreatRangeRatio => Mathf.Clamp(
            mainBattleRetreatRangeRatio,
            0f,
            MainBattlePreferredRangeRatio - 0.05f);
        public float MainBattleRetargetInterval => Mathf.Clamp(mainBattleRetargetInterval, 0.08f, 1f);
        public AnimationClip IdleClip => idleClip;
        public float IdleSpeed => idleSpeed;
        public AnimationClip MoveClip => moveClip;
        public float MovePlaybackSpeed => movePlaybackSpeed;
        public IReadOnlyList<MonsterMakerAttackDraft> Attacks => attacks ??
            (IReadOnlyList<MonsterMakerAttackDraft>)Array.Empty<MonsterMakerAttackDraft>();
        public AnimationClip DeathClip => deathClip;
        public float DeathSpeed => deathSpeed;
        public MonsterCombatType CombatType => combatType;
        public MonsterBasicAttackProfile BasicAttackProfile => basicAttackProfile;
        public IReadOnlyList<MonsterBasicAttackVfxBinding> BasicAttackVfxBindings =>
            basicAttackVfxBindings ??
            (IReadOnlyList<MonsterBasicAttackVfxBinding>)Array.Empty<MonsterBasicAttackVfxBinding>();
        public MonsterMeleeAttackMode MeleeMode => meleeMode;
        public MonsterMeleeAreaCenter MeleeAreaCenter => meleeAreaCenter;
        public float MeleeAreaRadius => meleeAreaRadius;
        public int MeleeMaxTargets => meleeMaxTargets;
        public MonsterRangedDeliveryMode RangedDeliveryMode => rangedDeliveryMode;
        public MonsterProjectileAttackMode ProjectileMode => projectileMode;
        public GameObject ProjectilePrefab => projectilePrefab;
        public bool OverrideProjectileTuning => overrideProjectileTuning;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileLifetime => projectileLifetime;
        public float ProjectileHitRadius => projectileHitRadius;
        public float ResolvedProjectileSpeed => overrideProjectileTuning || basicAttackProfile == null
            ? ProjectileSpeed
            : basicAttackProfile.ProjectileSpeed;
        public float ResolvedProjectileLifetime => overrideProjectileTuning || basicAttackProfile == null
            ? ProjectileLifetime
            : basicAttackProfile.ProjectileLifetime;
        public float ResolvedProjectileHitRadius => overrideProjectileTuning || basicAttackProfile == null
            ? ProjectileHitRadius
            : basicAttackProfile.ProjectileCollisionRadius;
        public int ProjectileMaxPiercingTargets => projectileMaxPiercingTargets;
        public float ProjectileImpactRadius => projectileImpactRadius;
        public int ProjectileMaxImpactTargets => projectileMaxImpactTargets;
        public float ProjectileLaunchRecoilDistance => projectileLaunchRecoilDistance;
        public float ProjectileLaunchRecoilDuration => projectileLaunchRecoilDuration;
        public string SpecialEffectId => specialEffectId ?? string.Empty;
        public MonsterBuffTargetTeam SpecialTargetTeam => specialTargetTeam;
        public float SpecialRadius => specialRadius;
        public int SpecialMaxTargets => specialMaxTargets;
        public float SpecialDuration => specialDuration;
        public MonsterBuffStackPolicy SpecialStackPolicy => specialStackPolicy;
        public MonsterStatModifier SpecialModifier => specialModifier;
        public HexCastleAssaultPattern CastleRaidAiPattern => castleRaidAiPattern;
        public HexCastleAssaultSupportFocus CastleRaidSupportFocus => castleRaidSupportFocus;
        public float CastleRaidSupportRange => castleRaidSupportRange;
        public float CastleRaidSupportCooldown => castleRaidSupportCooldown;
        public float CastleRaidSupportDuration => castleRaidSupportDuration;
        public float CastleRaidHealRatio => castleRaidHealRatio;
        public float CastleRaidAttackBuffRate => castleRaidAttackBuffRate;
        public float CastleRaidDefenseDamageMultiplier => castleRaidDefenseDamageMultiplier;
        public bool AscensionConfigured => ascensionConfigured;
        public MonsterStatModifier Ascension1 => ascension1;
        public MonsterMakerAbilityDraft Ascension2 => ascension2;
        public MonsterStatModifier Ascension3 => ascension3;
        public MonsterMakerAbilityDraft Ascension4 => ascension4;
        public MonsterStatModifier Ascension5 => ascension5;
        public MonsterMakerFeedbackDraft SpawnFeedback => spawnFeedback;
        public MonsterMakerFeedbackDraft HitFeedback => hitFeedback;
        public MonsterMakerFeedbackDraft DeathFeedback => deathFeedback;
        public MonsterMakerFeedbackDraft SpecialFeedback => specialFeedback;

#if UNITY_EDITOR
        public void EditorSetPassiveTemplate(GenericMonsterPassiveSkill template, bool resetTuning = false)
        {
            skillLoadoutConfigured = template != null;
            rarityPassiveSkill = template;
            passiveTuning ??= new MonsterMakerPassiveTuningDraft();
            if (resetTuning || !passiveTuning.Matches(template))
            {
                passiveTuning.CopyFrom(template);
            }
        }

        public void EditorSetActiveAttackProfile(MonsterActiveAttackProfile profile)
        {
            activeAttackProfile = profile;
            if (profile != null && string.IsNullOrWhiteSpace(activeSkillName))
            {
                activeSkillName = profile.DisplayName;
            }
            EditorSyncActiveAttackAuthoring();
        }

        public void EditorSetResolvedActiveSkill(MonsterActiveSkill skill)
        {
            rarityActiveSkill = skill;
        }

        public void EditorSyncActiveAttackAuthoring()
        {
            activeAttackStepTunings ??= new List<MonsterActiveAttackStepTuning>();
            activeAttackPresentations ??= new List<MonsterMakerActiveStepPresentationDraft>();
            if (!activeStepMotionModeConfigured)
            {
                activeStepMotionModeConfigured = true;
                useCustomActiveStepMotions = activeSkillClip != null;
            }
            if (activeAttackProfile == null)
            {
                activeAttackStepTunings.Clear();
                activeAttackPresentations.Clear();
                return;
            }

            var syncedTunings = new List<MonsterActiveAttackStepTuning>(activeAttackProfile.Steps.Count);
            var syncedPresentations = new List<MonsterMakerActiveStepPresentationDraft>(activeAttackProfile.Steps.Count);
            for (var stepIndex = 0; stepIndex < activeAttackProfile.Steps.Count; stepIndex++)
            {
                var stepId = activeAttackProfile.Steps[stepIndex].StepId;
                MonsterActiveAttackStepTuning tuning = null;
                for (var index = 0; index < activeAttackStepTunings.Count; index++)
                {
                    if (activeAttackStepTunings[index] != null && string.Equals(
                            activeAttackStepTunings[index].StepId,
                            stepId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        tuning = activeAttackStepTunings[index];
                        break;
                    }
                }
                if (tuning == null)
                {
                    tuning = new MonsterActiveAttackStepTuning();
                    tuning.EditorConfigure(stepId);
                }
                syncedTunings.Add(tuning);

                MonsterMakerActiveStepPresentationDraft presentation = null;
                for (var index = 0; index < activeAttackPresentations.Count; index++)
                {
                    if (activeAttackPresentations[index] != null && string.Equals(
                            activeAttackPresentations[index].StepId,
                            stepId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        presentation = activeAttackPresentations[index];
                        break;
                    }
                }
                if (presentation == null)
                {
                    presentation = new MonsterMakerActiveStepPresentationDraft();
                    presentation.EditorSetStepId(stepId);
                }
                presentation.EditorEnsureMotion(
                    activeSkillClip,
                    activeSkillPlaybackSpeed,
                    activeSkillCrossFadeDuration,
                    activeSkillCommitNormalizedTime);
                presentation.EditorSyncSlots(activeAttackProfile.Steps[stepIndex]);
                syncedPresentations.Add(presentation);
            }

            activeAttackStepTunings = syncedTunings;
            activeAttackPresentations = syncedPresentations;
        }

        public void ResolveActiveStepMotion(
            MonsterMakerActiveStepPresentationDraft presentation,
            out AnimationClip clip,
            out float playbackSpeed,
            out float crossFadeDuration,
            out float commitNormalizedTime)
        {
            if (useCustomActiveStepMotions)
            {
                clip = presentation?.MotionClip;
                playbackSpeed = presentation?.MotionPlaybackSpeed ?? 1f;
                crossFadeDuration = presentation?.MotionCrossFadeDuration ?? 0.08f;
                commitNormalizedTime = presentation?.MotionCommitNormalizedTime ?? 0.25f;
                return;
            }

            var basicAttack = Attacks.Count > 0 ? Attacks[0] : null;
            clip = basicAttack?.Clip;
            playbackSpeed = basicAttack?.PlaybackSpeed ?? 1f;
            crossFadeDuration = basicAttack?.CrossFadeDuration ?? 0.06f;
            commitNormalizedTime = basicAttack != null && basicAttack.Markers.Count > 0
                ? basicAttack.Markers[0].NormalizedTime
                : 0.25f;
        }

        public void EditorSetBasicAttackProfile(MonsterBasicAttackProfile profile)
        {
            basicAttackProfile = profile;
            if (profile != null)
            {
                combatType = profile.CombatType;
            }
        }

        public void EditorPreserveLegacyProjectileTuning()
        {
            if (basicAttackProfile == null || !basicAttackProfile.UsesProjectileVisual)
            {
                overrideProjectileTuning = false;
                return;
            }

            overrideProjectileTuning =
                Mathf.Abs(projectileSpeed - basicAttackProfile.ProjectileSpeed) > 0.001f ||
                Mathf.Abs(projectileLifetime - basicAttackProfile.ProjectileLifetime) > 0.001f ||
                Mathf.Abs(projectileHitRadius - basicAttackProfile.ProjectileCollisionRadius) > 0.001f;
        }

        public void EditorAdoptBasicAttackProfileTuning()
        {
            overrideProjectileTuning = false;
            if (basicAttackProfile == null || !basicAttackProfile.UsesProjectileVisual)
            {
                return;
            }

            projectileSpeed = basicAttackProfile.ProjectileSpeed;
            projectileLifetime = basicAttackProfile.ProjectileLifetime;
            projectileHitRadius = basicAttackProfile.ProjectileCollisionRadius;
        }
#endif
    }
}
