using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    public enum CommanderSkillDamageKind
    {
        Fire,
        Ice,
        Arcane,
        Physical
    }

    [CreateAssetMenu(menuName = "ProjectMT/Commander Skill/Effects/Area Damage", fileName = "CSEffect_AreaDamage")]
    public sealed class CommanderAreaDamageEffectDefinition : CommanderSkillEffectDefinition // 범위 피해 수치 SO
    {
        [SerializeField] private CommanderSkillDamageKind damageKind;
        [SerializeField, Min(0f)] private float baseDamage = 10f;
        [SerializeField, Min(0.1f)] private float radius = 1.5f;
        [SerializeField, Min(1)] private int maxTargets = 12;

        public CommanderSkillDamageKind DamageKind => damageKind;
        public float BaseDamage => Mathf.Max(0f, baseDamage);
        public float Radius => Mathf.Max(0.1f, radius);
        public int MaxTargets => Mathf.Max(1, maxTargets);

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            if (!System.Enum.IsDefined(typeof(CommanderSkillDamageKind), damageKind) ||
                baseDamage < 0f || float.IsNaN(baseDamage) || float.IsInfinity(baseDamage) ||
                radius < 0.1f || float.IsNaN(radius) || float.IsInfinity(radius) ||
                maxTargets < 1)
            {
                error = $"{EffectId}: area damage values are invalid.";
                return false;
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            CommanderSkillDamageKind kind,
            float damage,
            float impactRadius,
            int targetCount)
        {
            EditorConfigureId(id);
            damageKind = kind;
            baseDamage = Mathf.Max(0f, damage);
            radius = Mathf.Max(0.1f, impactRadius);
            maxTargets = Mathf.Max(1, targetCount);
        }
#endif
    }
}
