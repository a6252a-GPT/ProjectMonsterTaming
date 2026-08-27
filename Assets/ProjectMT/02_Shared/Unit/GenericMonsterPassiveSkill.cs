using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum GenericMonsterPassiveRuntimeKind
    {
        None = 0,
        RhythmPower = 1,
        SameTargetHaste = 2,
        RallySplash = 3,
        LowHealthHunter = 4,
        LongRangeAim = 5,
        CrisisDefense = 6,
        FrontlineBond = 7,
        FractureMark = 8,
        ThreatMark = 9,
        KillHeal = 10,
        CourageAura = 11,
        HealingShot = 12,
        EmergencyEntry = 13,
        FirstWave = 14
    }

    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Skill/Generic Passive", fileName = "MP_Generic")]
    public sealed class GenericMonsterPassiveSkill : MonsterPassiveSkill
    {
        [Header("Runtime Profile")]
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

        public GenericMonsterPassiveRuntimeKind RuntimeKind => runtimeKind;
        public bool NeedsRuntimeInitialization => runtimeKind == GenericMonsterPassiveRuntimeKind.None;
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

        public static int ResolveGrowthStage(int monsterLevel)
        {
            return Mathf.Clamp(Mathf.Max(1, monsterLevel) / 20, 0, 10);
        }

        public float ResolvePrimary(int monsterLevel)
        {
            return Mathf.Max(0f, primaryBase + primaryPerLevelStep * ResolveGrowthStage(monsterLevel));
        }

        public float ResolveSecondary(int monsterLevel)
        {
            return Mathf.Max(0f, secondaryBase + secondaryPerLevelStep * ResolveGrowthStage(monsterLevel));
        }

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            if (PresentationTier > MonsterSkillPresentationTier.Standard || Recipe.Effects.Count > 2)
            {
                error = $"Generic passive presentation/effect count exceeds the shared readability cap. Skill={SkillId}";
                return false;
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string title,
            string body,
            MonsterSkillPresentationTier tier,
            MonsterSkillRecipe skillRecipe,
            Sprite skillIcon = null)
        {
            EditorConfigureCommon(id, title, body, tier, skillRecipe, skillIcon);
        }

        public void EditorConfigureRuntime(
            GenericMonsterPassiveRuntimeKind kind,
            float basePrimary,
            float perStepPrimary,
            float baseSecondary = 0f,
            float perStepSecondary = 0f,
            int requiredHits = 1,
            int stackLimit = 1,
            float effectDuration = 0f,
            float internalCooldown = 0f,
            float conditionThreshold = 0f,
            float effectRadius = 0f,
            int targetLimit = 1)
        {
            runtimeKind = kind;
            primaryBase = Mathf.Max(0f, basePrimary);
            primaryPerLevelStep = Mathf.Max(0f, perStepPrimary);
            secondaryBase = Mathf.Max(0f, baseSecondary);
            secondaryPerLevelStep = Mathf.Max(0f, perStepSecondary);
            triggerCount = Mathf.Max(1, requiredHits);
            maxStacks = Mathf.Max(1, stackLimit);
            duration = Mathf.Max(0f, effectDuration);
            cooldown = Mathf.Max(0f, internalCooldown);
            threshold = Mathf.Max(0f, conditionThreshold);
            radius = Mathf.Max(0f, effectRadius);
            maxTargets = Mathf.Max(1, targetLimit);
        }
#endif
    }
}
