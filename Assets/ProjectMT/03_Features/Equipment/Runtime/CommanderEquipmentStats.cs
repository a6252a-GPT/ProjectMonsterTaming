using System;
using System.Collections.Generic;
using ProjectMT.Features.Commander;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;

namespace ProjectMT.Features.Equipment
{
    public struct EquipmentLegionBonus
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
            if (EquipmentInventoryRuntime.TryGetProgressView(out var progress, out var balance))
            {
                return CalculateTotal(progress, balance);
            }

            var values = CreateValues();
            foreach (EquipmentPart part in Enum.GetValues(typeof(EquipmentPart)))
            {
                Accumulate(
                    EquipmentSlotUpgradeCalculator.GetBonusContributions(
                        part,
                        EquipmentSlotUpgradeRuntime.GetLevel(part)),
                    values);

                if (EquipmentInventoryRuntime.TryGetEquipped(part, out var item))
                {
                    Accumulate(item.TotalContributions, values);
                }
            }

            Accumulate(CommanderPotentialCalculator.GetContributions(CommanderPotentialRuntime.GetView()), values);
            return Build(values);
        }

        // 저장 View를 입력받는 순수 경로. 자동분해·재시도 계획에서도 런타임 전역 상태 없이 같은 값을 쓴다.
        public static EquipmentLegionBonus CalculateTotal(
            GameProgressView progress,
            EquipmentBalanceConfig balance)
        {
            if (balance == null)
            {
                throw new ArgumentNullException(nameof(balance));
            }

            var values = CreateValues();
            foreach (EquipmentPart part in Enum.GetValues(typeof(EquipmentPart)))
            {
                Accumulate(
                    EquipmentSlotUpgradeCalculator.GetBonusContributions(
                        part,
                        progress.EquipmentSlotUpgrade.GetLevel(part)),
                    values);
                if (progress.Equipment.TryGetEquipped(part, out var instance) && instance != null)
                {
                    Accumulate(EquipmentStatCalculator.GetTotalContributions(instance, balance), values);
                }
            }

            Accumulate(CommanderPotentialCalculator.GetContributions(progress.CommanderPotential), values);
            return Build(values);
        }

        public static EquipmentLegionBonus CalculateEquipmentTotal(
            EquipmentSaveDataView equipment,
            EquipmentBalanceConfig balance)
        {
            if (balance == null)
            {
                throw new ArgumentNullException(nameof(balance));
            }

            var values = CreateValues();
            foreach (EquipmentPart part in Enum.GetValues(typeof(EquipmentPart)))
            {
                if (equipment.TryGetEquipped(part, out var instance) && instance != null)
                {
                    Accumulate(EquipmentStatCalculator.GetTotalContributions(instance, balance), values);
                }
            }

            return Build(values);
        }

        internal static void Accumulate(
            IReadOnlyList<EquipmentStatContribution> contributions,
            float[] values)
        {
            if (contributions == null)
            {
                return;
            }

            for (var index = 0; index < contributions.Count; index++)
            {
                var contribution = contributions[index];
                values[(int)contribution.StatType] += contribution.Value;
            }
        }

        private static float[] CreateValues() =>
            new float[Enum.GetValues(typeof(EquipmentStatType)).Length];

        private static EquipmentLegionBonus Build(float[] values)
        {
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

        private static float Get(float[] values, EquipmentStatType statType) => values[(int)statType];
    }
}
