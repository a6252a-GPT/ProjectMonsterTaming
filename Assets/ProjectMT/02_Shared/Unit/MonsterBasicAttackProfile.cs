using System;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum MonsterBasicAttackDelivery // Marker 뒤 실제 피해가 전달되는 방식
    {
        Contact,
        Projectile,
        Instant,
        Dash,
        MultiHit,
        ReturningProjectile,
        Breath,
        Beam,
        TravelingWave
    }

    public enum MonsterBasicAttackShape // XZ 판정 모양
    {
        Single,
        Fan,
        Line,
        Circle
    }

    public enum MonsterBasicAttackCenter
    {
        Source,
        PrimaryTarget
    }

    public enum MonsterBasicAttackProjectileTravel
    {
        None,
        Homing,
        Straight,
        Returning
    }

    [CreateAssetMenu(menuName = "ProjectMT/Unit/Basic Attack Profile", fileName = "BA_BasicAttack")]
    public sealed class MonsterBasicAttackProfile : ScriptableObject // 여러 몬스터가 공유하는 기본공격 판정 Recipe
    {
        public const int MaximumTargets = 4;
        public const int MaximumHitCount = 3;
        public const int MaximumProjectileCount = 3;

        [SerializeField] private string attackId;
        [SerializeField] private string displayName;
        [SerializeField] private MonsterCombatType combatType;
        [SerializeField] private MonsterBasicAttackDelivery delivery;
        [SerializeField] private MonsterBasicAttackShape shape;
        [SerializeField] private MonsterBasicAttackCenter center = MonsterBasicAttackCenter.PrimaryTarget;
        [SerializeField] private MonsterBasicAttackProjectileTravel projectileTravel;
        [SerializeField, Min(0.2f)] private float rangeMultiplier = 1f;
        [SerializeField, Min(0.05f)] private float radius = 0.35f;
        [SerializeField, Range(5f, 180f)] private float angle = 60f;
        [SerializeField, Min(0.05f)] private float lineWidth = 0.5f;
        [SerializeField, Range(1, MaximumTargets)] private int maxTargets = 1;
        [SerializeField, Range(1, MaximumProjectileCount)] private int projectileCount = 1;
        [SerializeField, Range(0f, 90f)] private float projectileSpreadAngle;
        [SerializeField] private float[] damageRatios = { 1f };
        [SerializeField, Range(0.1f, 1f)] private float secondaryDamageRatio = 1f;
        [SerializeField, Range(0.01f, 0.3f)] private float repeatHitInterval = 0.08f;
        [SerializeField, Min(0f)] private float dashDistance;
        [SerializeField, Range(0.05f, 0.3f)] private float dashDuration = 0.1f;
        [SerializeField, Range(0.1f, 1f)] private float hitAreaVisibleDuration = 0.42f;
        [SerializeField] private bool stopOnFirstTarget;

        public string AttackId => attackId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? AttackId : displayName;
        public MonsterCombatType CombatType => combatType;
        public MonsterBasicAttackDelivery Delivery => delivery;
        public MonsterBasicAttackShape Shape => shape;
        public MonsterBasicAttackCenter Center => center;
        public MonsterBasicAttackProjectileTravel ProjectileTravel => projectileTravel;
        public float RangeMultiplier => Mathf.Max(0.2f, rangeMultiplier);
        public float Radius => Mathf.Max(0.05f, radius);
        public float Angle => Mathf.Clamp(angle, 5f, 180f);
        public float LineWidth => Mathf.Max(0.05f, lineWidth);
        public int MaxTargets => Mathf.Clamp(maxTargets, 1, MaximumTargets);
        public int ProjectileCount => Mathf.Clamp(projectileCount, 1, MaximumProjectileCount);
        public float ProjectileSpreadAngle => Mathf.Clamp(projectileSpreadAngle, 0f, 90f);
        public int HitCount => Mathf.Clamp(damageRatios?.Length ?? 0, 1, MaximumHitCount);
        public float SecondaryDamageRatio => Mathf.Clamp(secondaryDamageRatio, 0.1f, 1f);
        public float RepeatHitInterval => Mathf.Clamp(repeatHitInterval, 0.01f, 0.3f);
        public float DashDistance => Mathf.Max(0f, dashDistance);
        public float DashDuration => Mathf.Clamp(dashDuration, 0.05f, 0.3f);
        public float HitAreaVisibleDuration => Mathf.Clamp(hitAreaVisibleDuration, 0.1f, 1f);
        public bool StopOnFirstTarget => stopOnFirstTarget;
        public bool UsesProjectileVisual => delivery == MonsterBasicAttackDelivery.Projectile ||
                                            delivery == MonsterBasicAttackDelivery.ReturningProjectile ||
                                            delivery == MonsterBasicAttackDelivery.TravelingWave;

        public float ResolveRange(float attackRange)
        {
            return Mathf.Max(0.2f, attackRange) * RangeMultiplier;
        }

        public float ResolveDamageRatio(int hitIndex)
        {
            if (damageRatios == null || damageRatios.Length == 0)
            {
                return 1f;
            }

            return Mathf.Max(0f, damageRatios[Mathf.Clamp(hitIndex, 0, damageRatios.Length - 1)]);
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(attackId) || string.IsNullOrWhiteSpace(displayName))
            {
                error = $"Basic Attack ID or name is blank. Profile={name}";
                return false;
            }

            if (!Enum.IsDefined(typeof(MonsterCombatType), combatType) || combatType == MonsterCombatType.Special ||
                !Enum.IsDefined(typeof(MonsterBasicAttackDelivery), delivery) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackShape), shape) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackCenter), center) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackProjectileTravel), projectileTravel))
            {
                error = $"Basic Attack enum setting is invalid. Profile={name}";
                return false;
            }

            if (rangeMultiplier < 0.2f || radius <= 0f || angle < 5f || angle > 180f ||
                lineWidth <= 0f || maxTargets < 1 || maxTargets > MaximumTargets ||
                projectileCount < 1 || projectileCount > MaximumProjectileCount ||
                damageRatios == null || damageRatios.Length < 1 || damageRatios.Length > MaximumHitCount)
            {
                error = $"Basic Attack geometry or hit limit is invalid. Profile={name}";
                return false;
            }

            var totalDamageRatio = 0f;
            for (var index = 0; index < damageRatios.Length; index++)
            {
                if (damageRatios[index] <= 0f)
                {
                    error = $"Basic Attack damage ratio must be positive. Profile={name}, Hit={index}";
                    return false;
                }

                totalDamageRatio += damageRatios[index];
            }

            if (totalDamageRatio > 1.5f || secondaryDamageRatio <= 0f || secondaryDamageRatio > 1f)
            {
                error = $"Basic Attack damage budget is invalid. Profile={name}";
                return false;
            }

            if (UsesProjectileVisual && projectileTravel == MonsterBasicAttackProjectileTravel.None)
            {
                error = $"Projectile Basic Attack requires a travel mode. Profile={name}";
                return false;
            }

            if (!UsesProjectileVisual && projectileTravel != MonsterBasicAttackProjectileTravel.None)
            {
                error = $"Non-projectile Basic Attack cannot use projectile travel. Profile={name}";
                return false;
            }

            if (delivery == MonsterBasicAttackDelivery.MultiHit && damageRatios.Length < 2)
            {
                error = $"Multi-hit Basic Attack requires at least two hit ratios. Profile={name}";
                return false;
            }

            if (delivery == MonsterBasicAttackDelivery.ReturningProjectile &&
                (projectileTravel != MonsterBasicAttackProjectileTravel.Returning || damageRatios.Length != 2))
            {
                error = $"Returning Basic Attack requires two passes. Profile={name}";
                return false;
            }

            if (delivery == MonsterBasicAttackDelivery.Dash && dashDistance <= 0f)
            {
                error = $"Dash Basic Attack requires a positive distance. Profile={name}";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string localizedName,
            MonsterCombatType type,
            MonsterBasicAttackDelivery deliveryMode,
            MonsterBasicAttackShape hitShape,
            MonsterBasicAttackCenter hitCenter,
            MonsterBasicAttackProjectileTravel travel,
            float rangeRatio,
            float hitRadius,
            float fanAngle,
            float width,
            int targetLimit,
            int volleyCount,
            float spreadAngle,
            float[] perHitDamageRatios,
            float secondaryRatio,
            float hitInterval = 0.08f,
            float advanceDistance = 0f,
            float advanceDuration = 0.1f,
            bool stopAfterFirstTarget = false,
            float areaVisibleDuration = 0.42f)
        {
            attackId = id?.Trim();
            displayName = localizedName?.Trim();
            combatType = type;
            delivery = deliveryMode;
            shape = hitShape;
            center = hitCenter;
            projectileTravel = travel;
            rangeMultiplier = Mathf.Max(0.2f, rangeRatio);
            radius = Mathf.Max(0.05f, hitRadius);
            angle = Mathf.Clamp(fanAngle, 5f, 180f);
            lineWidth = Mathf.Max(0.05f, width);
            maxTargets = Mathf.Clamp(targetLimit, 1, MaximumTargets);
            projectileCount = Mathf.Clamp(volleyCount, 1, MaximumProjectileCount);
            projectileSpreadAngle = Mathf.Clamp(spreadAngle, 0f, 90f);
            damageRatios = perHitDamageRatios == null || perHitDamageRatios.Length == 0
                ? new[] { 1f }
                : (float[])perHitDamageRatios.Clone();
            secondaryDamageRatio = Mathf.Clamp(secondaryRatio, 0.1f, 1f);
            repeatHitInterval = Mathf.Clamp(hitInterval, 0.01f, 0.3f);
            dashDistance = Mathf.Max(0f, advanceDistance);
            dashDuration = Mathf.Clamp(advanceDuration, 0.05f, 0.3f);
            stopOnFirstTarget = stopAfterFirstTarget;
            hitAreaVisibleDuration = Mathf.Clamp(areaVisibleDuration, 0.1f, 1f);
        }
#endif
    }
}
