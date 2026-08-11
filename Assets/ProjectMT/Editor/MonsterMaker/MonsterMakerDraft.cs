using System;
using System.Collections.Generic;
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
    public sealed class MonsterMakerMarkerDraft // 제작자가 직접 찍는 타격 시점
    {
        [SerializeField, Range(0f, 1f)] private float normalizedTime = 0.5f;
        [SerializeField, Min(0f)] private float powerRatio = 1f;
        [SerializeField] private string socketOverride;
        [SerializeField] private MonsterMakerFeedbackDraft feedback = new MonsterMakerFeedbackDraft();

        public float NormalizedTime => normalizedTime;
        public float PowerRatio => powerRatio;
        public string SocketOverride => socketOverride ?? string.Empty;
        public MonsterMakerFeedbackDraft Feedback => feedback;
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
        [SerializeField] private MonsterMakerFeedbackDraft attackStartFeedback = new MonsterMakerFeedbackDraft();
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
        public MonsterMakerFeedbackDraft AttackStartFeedback => attackStartFeedback;
        public IReadOnlyList<MonsterMakerMarkerDraft> Markers => markers ??
            (IReadOnlyList<MonsterMakerMarkerDraft>)Array.Empty<MonsterMakerMarkerDraft>();
    }

    [Serializable]
    public sealed class MonsterMakerAbilityDraft // 돌파 2·4 Ability 안정 ID 입력
    {
        [SerializeField] private string abilityId;
        [SerializeField] private string displayName;
        [SerializeField] private MonsterAbilityMode mode = MonsterAbilityMode.Passive;
        [SerializeField] private string triggerPolicyId;

        public string AbilityId => abilityId ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public MonsterAbilityMode Mode => mode;
        public string TriggerPolicyId => triggerPolicyId ?? string.Empty;
    }

    [CreateAssetMenu(menuName = "ProjectMT/Monster Maker/Draft", fileName = "Draft_monster")]
    public sealed class MonsterMakerDraft : ScriptableObject // 생성 전 사람의 결정을 보존하는 Editor 전용 원본
    {
        [SerializeField] private string monsterId;
        [SerializeField] private string displayName;
        [SerializeField] private MonsterRarity rarity = MonsterRarity.Common;
        [SerializeField] private Sprite portrait;
        [SerializeField] private MonsterPassiveSkill rarityPassiveSkill;
        [SerializeField] private MonsterActiveSkill rarityActiveSkill;
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
        [SerializeField] private MonsterMeleeAttackMode meleeMode = MonsterMeleeAttackMode.Single;
        [SerializeField] private MonsterMeleeAreaCenter meleeAreaCenter = MonsterMeleeAreaCenter.PrimaryTarget;
        [SerializeField, Min(0.01f)] private float meleeAreaRadius = 1.5f;
        [SerializeField, Min(1)] private int meleeMaxTargets = 4;
        [SerializeField] private MonsterRangedDeliveryMode rangedDeliveryMode = MonsterRangedDeliveryMode.Projectile;
        [SerializeField] private MonsterProjectileAttackMode projectileMode = MonsterProjectileAttackMode.Single;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private AudioClip projectileLaunchSound;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 9f;
        [SerializeField, Min(0.01f)] private float projectileLifetime = 3f;
        [SerializeField, Min(0.01f)] private float projectileHitRadius = 0.25f;
        [SerializeField, Min(1)] private int projectileMaxPiercingTargets = 2;
        [SerializeField, Min(0.01f)] private float projectileImpactRadius = 1.5f;
        [SerializeField, Min(1)] private int projectileMaxImpactTargets = 4;
        [SerializeField] private string specialEffectId;
        [SerializeField] private MonsterBuffTargetTeam specialTargetTeam = MonsterBuffTargetTeam.Allies;
        [SerializeField, Min(0.01f)] private float specialRadius = 2f;
        [SerializeField, Min(1)] private int specialMaxTargets = 5;
        [SerializeField, Min(0.01f)] private float specialDuration = 3f;
        [SerializeField] private MonsterBuffStackPolicy specialStackPolicy = MonsterBuffStackPolicy.RefreshDuration;
        [SerializeField] private MonsterStatModifier specialModifier;

        [SerializeField] private bool ascensionConfigured;
        [SerializeField] private MonsterStatModifier ascension1;
        [SerializeField] private MonsterMakerAbilityDraft ascension2 = new MonsterMakerAbilityDraft();
        [SerializeField] private MonsterStatModifier ascension3;
        [SerializeField] private MonsterMakerAbilityDraft ascension4 = new MonsterMakerAbilityDraft();
        [SerializeField] private MonsterStatModifier ascension5;

        [SerializeField] private MonsterMakerFeedbackDraft spawnFeedback = new MonsterMakerFeedbackDraft();
        [SerializeField] private MonsterMakerFeedbackDraft attackStartFeedback = new MonsterMakerFeedbackDraft();
        [SerializeField] private MonsterMakerFeedbackDraft attackMarkerFeedback = new MonsterMakerFeedbackDraft();
        [SerializeField] private MonsterMakerFeedbackDraft hitFeedback = new MonsterMakerFeedbackDraft();
        [SerializeField] private MonsterMakerFeedbackDraft deathFeedback = new MonsterMakerFeedbackDraft();
        [SerializeField] private MonsterMakerFeedbackDraft specialFeedback = new MonsterMakerFeedbackDraft();

        public string MonsterId => monsterId ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public MonsterRarity Rarity => rarity;
        public Sprite Portrait => portrait;
        public MonsterPassiveSkill RarityPassiveSkill => rarityPassiveSkill;
        public MonsterActiveSkill RarityActiveSkill => rarityActiveSkill;
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
        public AnimationClip IdleClip => idleClip;
        public float IdleSpeed => idleSpeed;
        public AnimationClip MoveClip => moveClip;
        public float MovePlaybackSpeed => movePlaybackSpeed;
        public IReadOnlyList<MonsterMakerAttackDraft> Attacks => attacks ??
            (IReadOnlyList<MonsterMakerAttackDraft>)Array.Empty<MonsterMakerAttackDraft>();
        public AnimationClip DeathClip => deathClip;
        public float DeathSpeed => deathSpeed;
        public MonsterCombatType CombatType => combatType;
        public MonsterMeleeAttackMode MeleeMode => meleeMode;
        public MonsterMeleeAreaCenter MeleeAreaCenter => meleeAreaCenter;
        public float MeleeAreaRadius => meleeAreaRadius;
        public int MeleeMaxTargets => meleeMaxTargets;
        public MonsterRangedDeliveryMode RangedDeliveryMode => rangedDeliveryMode;
        public MonsterProjectileAttackMode ProjectileMode => projectileMode;
        public GameObject ProjectilePrefab => projectilePrefab;
        public AudioClip ProjectileLaunchSound => projectileLaunchSound;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileLifetime => projectileLifetime;
        public float ProjectileHitRadius => projectileHitRadius;
        public int ProjectileMaxPiercingTargets => projectileMaxPiercingTargets;
        public float ProjectileImpactRadius => projectileImpactRadius;
        public int ProjectileMaxImpactTargets => projectileMaxImpactTargets;
        public string SpecialEffectId => specialEffectId ?? string.Empty;
        public MonsterBuffTargetTeam SpecialTargetTeam => specialTargetTeam;
        public float SpecialRadius => specialRadius;
        public int SpecialMaxTargets => specialMaxTargets;
        public float SpecialDuration => specialDuration;
        public MonsterBuffStackPolicy SpecialStackPolicy => specialStackPolicy;
        public MonsterStatModifier SpecialModifier => specialModifier;
        public bool AscensionConfigured => ascensionConfigured;
        public MonsterStatModifier Ascension1 => ascension1;
        public MonsterMakerAbilityDraft Ascension2 => ascension2;
        public MonsterStatModifier Ascension3 => ascension3;
        public MonsterMakerAbilityDraft Ascension4 => ascension4;
        public MonsterStatModifier Ascension5 => ascension5;
        public MonsterMakerFeedbackDraft SpawnFeedback => spawnFeedback;
        public MonsterMakerFeedbackDraft AttackStartFeedback => attackStartFeedback;
        public MonsterMakerFeedbackDraft AttackMarkerFeedback => attackMarkerFeedback;
        public MonsterMakerFeedbackDraft HitFeedback => hitFeedback;
        public MonsterMakerFeedbackDraft DeathFeedback => deathFeedback;
        public MonsterMakerFeedbackDraft SpecialFeedback => specialFeedback;
    }
}
