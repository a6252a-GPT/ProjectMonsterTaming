using System;
using ProjectMT.Features.Commander;
using ProjectMT.Shared.Equipment;

namespace ProjectMT.Features.Equipment
{
    public struct EquipmentLegionBonus // 장착 장비가 편성 전체에 주는 백분율 합계
    {
        public float AttackPower;
        public float MaxHealth;
        public float Defense;
        public float AttackSpeed;
        public float MoveSpeed;
        public float CriticalRate;
        public float CriticalDamage;
        public float SkillDamage;
        public float BossDamage;
        public float NormalMonsterDamage;
        public float SkillCooldownReduction;
        public float DefensePenetration;
        public float DamageReduction;

        public float GetValue(EquipmentStatType statType)
        {
            switch (statType)
            {
                case EquipmentStatType.AttackPower: return AttackPower;
                case EquipmentStatType.MaxHealth: return MaxHealth;
                case EquipmentStatType.Defense: return Defense;
                case EquipmentStatType.AttackSpeed: return AttackSpeed;
                case EquipmentStatType.MoveSpeed: return MoveSpeed;
                case EquipmentStatType.CriticalRate: return CriticalRate;
                case EquipmentStatType.CriticalDamage: return CriticalDamage;
                case EquipmentStatType.SkillDamage: return SkillDamage;
                case EquipmentStatType.BossDamage: return BossDamage;
                case EquipmentStatType.NormalMonsterDamage: return NormalMonsterDamage;
                case EquipmentStatType.SkillCooldownReduction: return SkillCooldownReduction;
                case EquipmentStatType.DefensePenetration: return DefensePenetration;
                case EquipmentStatType.DamageReduction: return DamageReduction;
                default: return 0f;
            }
        }
    }

    public static class EquipmentLegionBonusCalculator
    {
        public static EquipmentLegionBonus CalculateTotal()
        {
            var values = new float[Enum.GetValues(typeof(EquipmentStatType)).Length];

            foreach (EquipmentPart part in Enum.GetValues(typeof(EquipmentPart)))
            {
                // 부위 슬롯 영구 강화 보너스. 장비 장착 여부와 무관하게 적용한다.
                var slotLevel = EquipmentSlotUpgradeRuntime.GetLevel(part);
                if (slotLevel > 0)
                {
                    Accumulate(EquipmentSlotUpgradeCalculator.GetBonusContributions(part, slotLevel), values);
                }

                if (!EquipmentInventoryRuntime.TryGetEquipped(part, out var item) || item.Definition == null)
                {
                    continue;
                }

                Accumulate(item.Definition.CoreStatContributions, values);

                var options = item.Instance?.RandomOptions;
                if (options != null)
                {
                    for (var i = 0; i < options.Count; i++)
                    {
                        var contributions = EquipmentOptionInfo.ResolveContributions(options[i].Type, options[i].Value);
                        Accumulate(contributions, values);
                    }
                }
            }

            // 군단장 잠재능력 슬롯 기여분도 장비와 같은 방식으로 합산한다.
            Accumulate(CommanderPotentialCalculator.GetContributions(CommanderPotentialRuntime.GetView()), values);

            return new EquipmentLegionBonus
            {
                AttackPower = Get(values, EquipmentStatType.AttackPower),
                MaxHealth = Get(values, EquipmentStatType.MaxHealth),
                Defense = Get(values, EquipmentStatType.Defense),
                AttackSpeed = Get(values, EquipmentStatType.AttackSpeed),
                MoveSpeed = Get(values, EquipmentStatType.MoveSpeed),
                CriticalRate = Get(values, EquipmentStatType.CriticalRate),
                CriticalDamage = Get(values, EquipmentStatType.CriticalDamage),
                SkillDamage = Get(values, EquipmentStatType.SkillDamage),
                BossDamage = Get(values, EquipmentStatType.BossDamage),
                NormalMonsterDamage = Get(values, EquipmentStatType.NormalMonsterDamage),
                SkillCooldownReduction = Get(values, EquipmentStatType.SkillCooldownReduction),
                DefensePenetration = Get(values, EquipmentStatType.DefensePenetration),
                DamageReduction = Get(values, EquipmentStatType.DamageReduction)
            };
        }

        private static void Accumulate(
            System.Collections.Generic.IReadOnlyList<EquipmentStatContribution> contributions,
            float[] values)
        {
            if (contributions == null)
            {
                return;
            }

            for (var i = 0; i < contributions.Count; i++)
            {
                var contribution = contributions[i];
                values[(int)contribution.StatType] += contribution.Value;
            }
        }

        private static float Get(float[] values, EquipmentStatType statType) => values[(int)statType];
    }
}
