using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    public enum CommanderSkillTrajectory
    {
        Straight,
        Arc
    }

    [CreateAssetMenu(menuName = "ProjectMT/Commander Skill/Attack Definition", fileName = "CS_Attack")]
    public sealed class CommanderAttackSkillDefinition : CommanderSkillDefinition // 공격 전달 방식 SO
    {
        [Header("Basic Attack Modules")]
        [SerializeField] private MonsterBasicAttackDeliveryModule deliveryModule =
            MonsterBasicAttackDeliveryModule.Projectile;

        [Header("Projectile Delivery")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField, Min(1f)] private float projectileSpeed = 16f;
        [SerializeField] private CommanderSkillTrajectory trajectory;
        [SerializeField, Min(0f)] private float arcHeight;

        public override CommanderSkillCategory Category => CommanderSkillCategory.Attack;
        public MonsterBasicAttackDeliveryModule DeliveryModule => deliveryModule;
        public GameObject ProjectilePrefab => projectilePrefab;
        public float ProjectileSpeed => Mathf.Max(1f, projectileSpeed);
        public CommanderSkillTrajectory Trajectory => trajectory;
        public float ArcHeight => Mathf.Max(0f, arcHeight);
        public CommanderAreaDamageEffectDefinition AreaDamageEffect =>
            TryGetEffect<CommanderAreaDamageEffectDefinition>(out var effect) ? effect : null;

        // 기존 조회 API는 새 효과 SO를 통하도록 유지한다.
        public float Damage => AreaDamageEffect == null ? 0f : AreaDamageEffect.BaseDamage;
        public float ImpactRadius => AreaDamageEffect == null ? 0f : AreaDamageEffect.Radius;
        public int MaxTargets => AreaDamageEffect == null ? 0 : AreaDamageEffect.MaxTargets;

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            if (deliveryModule is not MonsterBasicAttackDeliveryModule.Direct and
                not MonsterBasicAttackDeliveryModule.Projectile)
            {
                error = $"{SkillId}: commander attack supports direct or projectile delivery only.";
                return false;
            }

            if (deliveryModule == MonsterBasicAttackDeliveryModule.Projectile &&
                (projectilePrefab == null || projectileSpeed < 1f ||
                 float.IsNaN(projectileSpeed) || float.IsInfinity(projectileSpeed) ||
                 arcHeight < 0f || float.IsNaN(arcHeight) || float.IsInfinity(arcHeight)))
            {
                error = $"{SkillId}: projectile delivery is invalid.";
                return false;
            }

            if (AreaDamageEffect == null)
            {
                error = $"{SkillId}: an area damage effect is required for the current attack executor.";
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
            Sprite skillIcon,
            float cooldownSeconds,
            CommanderSkillTargetingDefinition targetingRule,
            CommanderAreaDamageEffectDefinition damageEffect,
            GameObject projectile,
            float speed,
            CommanderSkillTrajectory path,
            float pathArcHeight,
            GameObject castVfx,
            float castLifetime,
            SfxCue castCue,
            GameObject impactVfx,
            float impactLifetime,
            SfxCue impactCue)
        {
            EditorConfigure(
                id,
                title,
                body,
                skillIcon,
                0f,
                cooldownSeconds,
                targetingRule,
                damageEffect,
                MonsterBasicAttackDeliveryModule.Projectile,
                projectile,
                speed,
                path,
                pathArcHeight,
                castVfx,
                castLifetime,
                castCue,
                impactVfx,
                impactLifetime,
                impactCue);
        }

        public void EditorConfigure(
            string id,
            string title,
            string body,
            Sprite skillIcon,
            float castTimeSeconds,
            float cooldownSeconds,
            CommanderSkillTargetingDefinition targetingRule,
            CommanderAreaDamageEffectDefinition damageEffect,
            MonsterBasicAttackDeliveryModule delivery,
            GameObject projectile,
            float speed,
            CommanderSkillTrajectory path,
            float pathArcHeight,
            GameObject castVfx,
            float castLifetime,
            SfxCue castCue,
            GameObject impactVfx,
            float impactLifetime,
            SfxCue impactCue)
        {
            EditorConfigureCommon(
                id,
                title,
                body,
                skillIcon,
                castTimeSeconds,
                cooldownSeconds,
                targetingRule,
                new CommanderSkillEffectDefinition[] { damageEffect },
                castVfx,
                castLifetime,
                castCue,
                impactVfx,
                impactLifetime,
                impactCue);
            deliveryModule = delivery;
            projectilePrefab = projectile;
            projectileSpeed = Mathf.Max(1f, speed);
            trajectory = path;
            arcHeight = Mathf.Max(0f, pathArcHeight);
        }
#endif
    }
}
