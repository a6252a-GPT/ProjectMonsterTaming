using System.Collections.Generic;
using ProjectMT.Shared.Equipment;

namespace ProjectMT.Features.Equipment
{
    // 부위별 강화 보너스 배분 비율. 세 값의 합은 항상 1(=100%)이다.
    public readonly struct EquipmentSlotUpgradeRatio
    {
        public EquipmentSlotUpgradeRatio(float attackPower, float maxHealth, float defense)
        {
            AttackPower = attackPower;
            MaxHealth = maxHealth;
            Defense = defense;
        }

        public float AttackPower { get; }
        public float MaxHealth { get; }
        public float Defense { get; }
    }

    // 슬롯 강화로 얻는 능력치 보너스(%, 군단장 기본 스탯 대비 상대값).
    public readonly struct EquipmentSlotUpgradeBonus
    {
        public static readonly EquipmentSlotUpgradeBonus Zero = new EquipmentSlotUpgradeBonus(0f, 0f, 0f);

        public EquipmentSlotUpgradeBonus(float attackPowerPercent, float maxHealthPercent, float defensePercent)
        {
            AttackPowerPercent = attackPowerPercent;
            MaxHealthPercent = maxHealthPercent;
            DefensePercent = defensePercent;
        }

        public float AttackPowerPercent { get; }
        public float MaxHealthPercent { get; }
        public float DefensePercent { get; }

        public bool IsZero => AttackPowerPercent == 0f && MaxHealthPercent == 0f && DefensePercent == 0f;
    }

    // 슬롯 레벨 → 능력치 보너스·강화 비용 순수 계산기(MonoBehaviour 아님).
    // 장비 보유와 무관하게 여섯 부위 모두 군단 공용 성장으로 적용한다.
    public static class EquipmentSlotUpgradeCalculator
    {
        public const int MinLevel = 0;

        // 슬롯 레벨 1당 늘어나는 보너스 예산(%).
        private const float BonusBudgetPercentPerLevel = 1f;

        // 부위별 배분 비율. 각 부위의 합은 100%다.
        private static readonly Dictionary<EquipmentPart, EquipmentSlotUpgradeRatio> DistributionByPart =
            new Dictionary<EquipmentPart, EquipmentSlotUpgradeRatio>
            {
                { EquipmentPart.Weapon, new EquipmentSlotUpgradeRatio(1.00f, 0f, 0f) },
                { EquipmentPart.Helmet, new EquipmentSlotUpgradeRatio(0f, 0.70f, 0.30f) },
                { EquipmentPart.Armor, new EquipmentSlotUpgradeRatio(0f, 0.30f, 0.70f) },
                { EquipmentPart.Boots, new EquipmentSlotUpgradeRatio(0f, 0.50f, 0.50f) },
                { EquipmentPart.Glove, new EquipmentSlotUpgradeRatio(0.70f, 0.30f, 0f) },
                { EquipmentPart.Ring, new EquipmentSlotUpgradeRatio(0.34f, 0.33f, 0.33f) }
            };

        // 슬롯 강화 지원 부위인지.
        public static bool IsSlotUpgradeSupported(EquipmentPart part) => DistributionByPart.ContainsKey(part);

        public static float GetBonusBudgetPercent(int level)
        {
            return level > 0 ? BonusBudgetPercentPerLevel * level : 0f;
        }

        // 특정 부위·레벨의 공격력/체력/방어력 보너스(%)를 계산한다. 미지원 부위나 레벨 0이면 Zero.
        public static EquipmentSlotUpgradeBonus GetBonus(EquipmentPart part, int level)
        {
            if (level <= 0 || !DistributionByPart.TryGetValue(part, out var ratio))
            {
                return EquipmentSlotUpgradeBonus.Zero;
            }

            var budget = GetBonusBudgetPercent(level);
            return new EquipmentSlotUpgradeBonus(
                budget * ratio.AttackPower,
                budget * ratio.MaxHealth,
                budget * ratio.Defense);
        }

        // 다른 스탯 계산기(CommanderEquipmentStatsCalculator 등)가 그대로 누적할 수 있는 형태.
        public static List<EquipmentStatContribution> GetBonusContributions(EquipmentPart part, int level)
        {
            var result = new List<EquipmentStatContribution>();
            var bonus = GetBonus(part, level);
            if (bonus.IsZero)
            {
                return result;
            }

            if (bonus.AttackPowerPercent != 0f)
            {
                result.Add(new EquipmentStatContribution(EquipmentStatType.AttackPower, bonus.AttackPowerPercent, true));
            }

            if (bonus.MaxHealthPercent != 0f)
            {
                result.Add(new EquipmentStatContribution(EquipmentStatType.MaxHealth, bonus.MaxHealthPercent, true));
            }

            if (bonus.DefensePercent != 0f)
            {
                result.Add(new EquipmentStatContribution(EquipmentStatType.Defense, bonus.DefensePercent, true));
            }

            return result;
        }

        // 강화 비용 계산은 Shared의 EquipmentSlotUpgradeCostRules에 위임한다.
        public static long GetNextGoldCost(int level) => EquipmentSlotUpgradeCostRules.GetNextGoldCost(level);

        public static int GetNextStoneCost(int level) => EquipmentSlotUpgradeCostRules.GetNextStoneCost(level);
    }
}
