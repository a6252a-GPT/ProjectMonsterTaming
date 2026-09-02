using ProjectMT.Shared.Unit;
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
        [SerializeField] private MonsterBasicAttackShape shape = MonsterBasicAttackShape.Circle;
        [SerializeField] private MonsterBasicAttackCenter center = MonsterBasicAttackCenter.PrimaryTarget;
        [SerializeField, Min(0.1f)] private float radius = 1.5f;
        [SerializeField, Min(0f)] private float forwardOffset = 2f;
        [SerializeField, Range(5f, 180f)] private float angle = 90f;
        [SerializeField, Min(0.05f)] private float lineWidth = 2f;
        [SerializeField, Min(1)] private int maxTargets = 12;

        public CommanderSkillDamageKind DamageKind => damageKind;
        public float BaseDamage => Mathf.Max(0f, baseDamage);
        public MonsterBasicAttackShape Shape => shape;
        public MonsterBasicAttackCenter Center => center;
        public float Radius => Mathf.Max(0.1f, radius);
        public float ForwardOffset => Mathf.Max(0f, forwardOffset);
        public float Angle => Mathf.Clamp(angle, 5f, 180f);
        public float LineWidth => Mathf.Max(0.05f, lineWidth);
        public int MaxTargets => Mathf.Max(1, maxTargets);

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            if (!System.Enum.IsDefined(typeof(CommanderSkillDamageKind), damageKind) ||
                !System.Enum.IsDefined(typeof(MonsterBasicAttackShape), shape) ||
                !System.Enum.IsDefined(typeof(MonsterBasicAttackCenter), center) ||
                baseDamage < 0f || float.IsNaN(baseDamage) || float.IsInfinity(baseDamage) ||
                radius < 0.1f || float.IsNaN(radius) || float.IsInfinity(radius) ||
                forwardOffset < 0f || float.IsNaN(forwardOffset) || float.IsInfinity(forwardOffset) ||
                angle < 5f || angle > 180f || float.IsNaN(angle) || float.IsInfinity(angle) ||
                lineWidth < 0.05f || float.IsNaN(lineWidth) || float.IsInfinity(lineWidth) ||
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
            EditorConfigure(
                id,
                kind,
                damage,
                MonsterBasicAttackShape.Circle,
                MonsterBasicAttackCenter.PrimaryTarget,
                impactRadius,
                2f,
                90f,
                2f,
                targetCount);
        }

        public void EditorConfigure(
            string id,
            CommanderSkillDamageKind kind,
            float damage,
            MonsterBasicAttackShape hitShape,
            MonsterBasicAttackCenter hitCenter,
            float impactRadius,
            float centerForwardOffset,
            float fanAngle,
            float width,
            int targetCount)
        {
            EditorConfigureId(id);
            damageKind = kind;
            baseDamage = Mathf.Max(0f, damage);
            shape = hitShape;
            center = hitCenter;
            radius = Mathf.Max(0.1f, impactRadius);
            forwardOffset = Mathf.Max(0f, centerForwardOffset);
            angle = Mathf.Clamp(fanAngle, 5f, 180f);
            lineWidth = Mathf.Max(0.05f, width);
            maxTargets = Mathf.Max(1, targetCount);
        }
#endif
    }
}
