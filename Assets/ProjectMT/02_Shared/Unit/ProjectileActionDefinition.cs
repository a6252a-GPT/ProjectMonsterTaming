using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum MonsterProjectileAttackMode
    {
        Single,
        Piercing,
        Area
    }

    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Action/Projectile", fileName = "ProjectileAction")]
    public sealed class ProjectileActionDefinition : MonsterActionDefinition // 원거리 세 방식 실행 데이터
    {
        [SerializeField] private MonsterProjectileAttackMode mode;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField, Min(0.01f)] private float speed = 9f;
        [SerializeField, Min(0.01f)] private float lifetime = 3f;
        [SerializeField, Min(0.01f)] private float hitRadius = 0.25f;
        [SerializeField, Min(1)] private int maxPiercingTargets = 2;
        [SerializeField, Min(0.01f)] private float impactRadius = 1.5f;
        [SerializeField, Min(1)] private int maxImpactTargets = 4;

        public override MonsterCombatType CombatType => MonsterCombatType.Ranged;
        public MonsterProjectileAttackMode Mode => mode;
        public GameObject ProjectilePrefab => projectilePrefab;
        public float Speed => Mathf.Max(0.01f, speed);
        public float Lifetime => Mathf.Max(0.01f, lifetime);
        public float HitRadius => Mathf.Max(0.01f, hitRadius);
        public int MaxPiercingTargets => Mathf.Max(1, maxPiercingTargets);
        public float ImpactRadius => Mathf.Max(0.01f, impactRadius);
        public int MaxImpactTargets => Mathf.Max(1, maxImpactTargets);

        public override bool TryValidate(out string error)
        {
            if (projectilePrefab == null || speed <= 0f || lifetime <= 0f)
            {
                error = $"Projectile reference, speed and lifetime are required. Action={name}";
                return false;
            }

            if (projectilePrefab.GetComponent<ProjectMT.Shared.Combat.MonsterProjectileActor>() == null)
            {
                error = $"Formal Projectile prefab requires MonsterProjectileActor. Action={name}";
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
            MonsterProjectileAttackMode attackMode,
            GameObject prefab,
            float projectileSpeed,
            float projectileLifetime,
            float collisionRadius,
            int piercingTargets,
            float areaRadius,
            int areaTargets)
        {
            mode = attackMode;
            projectilePrefab = prefab;
            speed = projectileSpeed;
            lifetime = projectileLifetime;
            hitRadius = collisionRadius;
            maxPiercingTargets = piercingTargets;
            impactRadius = areaRadius;
            maxImpactTargets = areaTargets;
        }
#endif
    }
}
