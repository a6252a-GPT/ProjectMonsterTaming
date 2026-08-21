using UnityEngine;

namespace ProjectMT.Shared.Stats
{
    [CreateAssetMenu(menuName = "ProjectMT/Stats/Combat Stat Config", fileName = "CombatStatConfig")]
    public sealed class CombatStatConfig : ScriptableObject // 전 콘텐츠가 함께 쓰는 능력치 기본값·상한
    {
        private static CombatStatConfig runtimeDefault;

        [Header("공통 기본값")]
        [SerializeField, Range(0f, 1f)] private float baseCriticalRate;
        [SerializeField, Min(1f)] private float baseCriticalDamageMultiplier = 1.5f;
        [SerializeField, Min(0.01f)] private float minimumDamage = 1f;
        [SerializeField, Min(0.01f)] private float defenseK = 100f;
        [SerializeField, Min(0.01f)] private float combatPowerDisplayScale = 4f;

        [Header("최종 상한")]
        [SerializeField, Range(0f, 1f)] private float criticalRateCap = 0.75f;
        [SerializeField, Min(1f)] private float criticalDamageMultiplierCap = 3f;
        [SerializeField, Range(0f, 3f)] private float attackSpeedBonusRateCap = 0.5f;
        [SerializeField, Range(0f, 3f)] private float moveSpeedBonusRateCap = 0.3f;
        [SerializeField, Range(0f, 1f)] private float skillCooldownReductionCap = 0.4f;
        [SerializeField, Range(0f, 1f)] private float defensePenetrationCap = 0.8f;
        [SerializeField, Range(0f, 1f)] private float damageReductionCap = 0.7f;
        [SerializeField, Range(0f, 3f)] private float attackRangeBonusRateCap = 0.2f;

        public float BaseCriticalRate => Mathf.Clamp(baseCriticalRate, 0f, CriticalRateCap);
        public float BaseCriticalDamageMultiplier => Mathf.Clamp(
            baseCriticalDamageMultiplier,
            1f,
            CriticalDamageMultiplierCap);
        public float MinimumDamage => Mathf.Max(0.01f, minimumDamage);
        public float DefenseK => Mathf.Max(0.01f, defenseK);
        public float CombatPowerDisplayScale => Mathf.Max(0.01f, combatPowerDisplayScale);
        public float CriticalRateCap => Mathf.Clamp01(criticalRateCap);
        public float CriticalDamageMultiplierCap => Mathf.Max(1f, criticalDamageMultiplierCap);
        public float AttackSpeedBonusRateCap => Mathf.Max(0f, attackSpeedBonusRateCap);
        public float MoveSpeedBonusRateCap => Mathf.Max(0f, moveSpeedBonusRateCap);
        public float SkillCooldownReductionCap => Mathf.Clamp01(skillCooldownReductionCap);
        public float DefensePenetrationCap => Mathf.Clamp01(defensePenetrationCap);
        public float DamageReductionCap => Mathf.Clamp01(damageReductionCap);
        public float AttackRangeBonusRateCap => Mathf.Max(0f, attackRangeBonusRateCap);

        public static CombatStatConfig RuntimeDefault
        {
            get
            {
                if (runtimeDefault == null)
                {
                    runtimeDefault = CreateInstance<CombatStatConfig>();
                    runtimeDefault.hideFlags = HideFlags.HideAndDontSave;
                }

                return runtimeDefault;
            }
        }

        public bool TryValidate(out string error)
        {
            if (defenseK <= 0f || minimumDamage <= 0f || combatPowerDisplayScale <= 0f ||
                baseCriticalDamageMultiplier < 1f || criticalDamageMultiplierCap < 1f ||
                baseCriticalDamageMultiplier > criticalDamageMultiplierCap ||
                baseCriticalRate < 0f || baseCriticalRate > criticalRateCap)
            {
                error = "Combat stat defaults or caps are invalid.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
