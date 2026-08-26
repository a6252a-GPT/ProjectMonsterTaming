using ProjectMT.Shared.Audio;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum MonsterRangedDeliveryMode // 원거리 피해가 도착하는 방식
    {
        Projectile,
        Instant
    }

    public enum MonsterProjectileAttackMode
    {
        Single,
        Piercing,
        Area
    }

    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Action/Projectile", fileName = "ProjectileAction")]
    public sealed class ProjectileActionDefinition : MonsterActionDefinition // 기존 자산 호환 원거리 전달·타격 데이터
    {
        [SerializeField] private MonsterRangedDeliveryMode deliveryMode;
        [SerializeField] private MonsterProjectileAttackMode mode;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private SfxCue launchSfx;
        [SerializeField] private bool overrideBasicAttackProfileTuning;
        [SerializeField, Min(0.01f)] private float speed = 9f;
        [SerializeField, Min(0.01f)] private float lifetime = 3f;
        [SerializeField, Min(0.01f)] private float hitRadius = 0.25f;
        [SerializeField, Min(1)] private int maxPiercingTargets = 2;
        [SerializeField, Min(0.01f)] private float impactRadius = 1.5f;
        [SerializeField, Min(1)] private int maxImpactTargets = 4;
        [SerializeField, Min(0f)] private float launchRecoilDistance;
        [SerializeField, Min(0.01f)] private float launchRecoilDuration = 0.12f;

        public override MonsterCombatType CombatType => MonsterCombatType.Ranged;
        public MonsterRangedDeliveryMode DeliveryMode => deliveryMode;
        public MonsterProjectileAttackMode Mode => mode;
        public GameObject ProjectilePrefab => projectilePrefab;
        public SfxCue LaunchSfx => launchSfx;
        public bool OverrideBasicAttackProfileTuning => overrideBasicAttackProfileTuning;
        public float Speed => Mathf.Max(0.01f, speed);
        public float Lifetime => Mathf.Max(0.01f, lifetime);
        public float HitRadius => Mathf.Max(0.01f, hitRadius);
        public float ResolvedSpeed => BasicAttackProfile != null && !overrideBasicAttackProfileTuning
            ? BasicAttackProfile.ProjectileSpeed
            : Speed;
        public float ResolvedLifetime => BasicAttackProfile != null && !overrideBasicAttackProfileTuning
            ? BasicAttackProfile.ProjectileLifetime
            : Lifetime;
        public float ResolvedHitRadius => BasicAttackProfile != null && !overrideBasicAttackProfileTuning
            ? BasicAttackProfile.ProjectileCollisionRadius
            : HitRadius;
        public int MaxPiercingTargets => Mathf.Max(1, maxPiercingTargets);
        public float ImpactRadius => Mathf.Max(0.01f, impactRadius);
        public int MaxImpactTargets => Mathf.Max(1, maxImpactTargets);
        public float LaunchRecoilDistance => Mathf.Max(0f, launchRecoilDistance);
        public float LaunchRecoilDuration => Mathf.Max(0.01f, launchRecoilDuration);

        public override bool TryValidate(out string error)
        {
            if (BasicAttackProfile != null)
            {
                if (BasicAttackProfile.CombatType != MonsterCombatType.Ranged)
                {
                    error = $"Projectile action requires a Ranged Basic Attack profile. Action={name}";
                    return false;
                }

                if (!BasicAttackProfile.TryValidate(out error))
                {
                    return false;
                }

                if (BasicAttackProfile.UsesProjectileVisual &&
                    (projectilePrefab == null || speed <= 0f || lifetime <= 0f))
                {
                    error = $"Projectile Basic Attack requires visual, speed and lifetime. Action={name}";
                    return false;
                }

                error = null;
                return true;
            }

            if (launchRecoilDistance < 0f || launchRecoilDuration <= 0f)
            {
                error = $"Projectile launch recoil settings are invalid. Action={name}";
                return false;
            }

            if (deliveryMode == MonsterRangedDeliveryMode.Instant)
            {
                if (mode == MonsterProjectileAttackMode.Piercing)
                {
                    error = $"Instant ranged attacks do not support Piercing yet. Action={name}";
                    return false;
                }

                if (mode == MonsterProjectileAttackMode.Area &&
                    (impactRadius <= 0f || maxImpactTargets < 1))
                {
                    error = $"Instant Area settings are invalid. Action={name}";
                    return false;
                }

                error = null;
                return true;
            }

            if (projectilePrefab == null || speed <= 0f || lifetime <= 0f)
            {
                error = $"Projectile visual, speed and lifetime are required. Action={name}";
                return false;
            }

            if (launchSfx != null && !launchSfx.HasPlayableClip)
            {
                error = $"Projectile launch SFX has no playable AudioClip. Action={name}";
                return false;
            }

            if (mode == MonsterProjectileAttackMode.Piercing &&
                (maxPiercingTargets < 1 || hitRadius <= 0f))
            {
                error = $"Piercing Projectile settings are invalid. Action={name}";
                return false;
            }

            if (mode == MonsterProjectileAttackMode.Area &&
                (impactRadius <= 0f || maxImpactTargets < 1))
            {
                error = $"Area Projectile settings are invalid. Action={name}";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            MonsterRangedDeliveryMode delivery,
            MonsterProjectileAttackMode attackMode,
            GameObject prefab,
            SfxCue projectileLaunchSfx,
            float projectileSpeed,
            float projectileLifetime,
            float collisionRadius,
            int piercingTargets,
            float areaRadius,
            int areaTargets,
            float recoilDistance = 0f,
            float recoilDuration = 0.12f,
            bool overrideProfileTuning = false)
        {
            deliveryMode = delivery;
            mode = attackMode;
            projectilePrefab = delivery == MonsterRangedDeliveryMode.Projectile ? prefab : null;
            launchSfx = delivery == MonsterRangedDeliveryMode.Projectile ? projectileLaunchSfx : null;
            overrideBasicAttackProfileTuning = overrideProfileTuning;
            speed = projectileSpeed;
            lifetime = projectileLifetime;
            hitRadius = collisionRadius;
            maxPiercingTargets = piercingTargets;
            impactRadius = areaRadius;
            maxImpactTargets = areaTargets;
            launchRecoilDistance = Mathf.Max(0f, recoilDistance);
            launchRecoilDuration = Mathf.Max(0.01f, recoilDuration);
        }

        public void EditorConfigure(
            MonsterProjectileAttackMode attackMode,
            GameObject prefab,
            float projectileSpeed,
            float projectileLifetime,
            float collisionRadius,
            int piercingTargets,
            float areaRadius,
            int areaTargets)
        {
            EditorConfigure(
                MonsterRangedDeliveryMode.Projectile,
                attackMode,
                prefab,
                null,
                projectileSpeed,
                projectileLifetime,
                collisionRadius,
                piercingTargets,
                areaRadius,
                areaTargets);
        }
#endif
    }
}
