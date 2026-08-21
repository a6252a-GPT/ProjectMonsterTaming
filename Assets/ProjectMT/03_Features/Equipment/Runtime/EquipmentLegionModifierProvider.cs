using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.Stats;

namespace ProjectMT.Features.Equipment
{
    public static class EquipmentLegionModifierProvider // 장착 장비를 편성 전체 보너스로 변환
    {
        public static void Append(
            EquipmentSaveDataView equipment,
            EquipmentBalanceConfig balance,
            List<StatModifier> destination)
        {
            if (balance == null)
            {
                throw new ArgumentNullException(nameof(balance));
            }

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            foreach (EquipmentPart part in Enum.GetValues(typeof(EquipmentPart)))
            {
                if (!equipment.TryGetEquipped(part, out var instance) || instance == null)
                {
                    continue;
                }

                var coreStats = EquipmentGradeStatTable.GetCoreStatContributions(part, instance.Grade, balance);
                AppendContributions(coreStats, $"equipment:{instance.InstanceId}:core", destination);

                var options = instance.RandomOptions;
                for (var index = 0; index < options.Count; index++)
                {
                    var option = options[index];
                    if (option == null)
                    {
                        continue;
                    }

                    AppendContributions(
                        EquipmentOptionInfo.ResolveContributions(option.Type, option.Value),
                        $"equipment:{instance.InstanceId}:option:{index}",
                        destination);
                }
            }
        }

        internal static void AppendContributions(
            IReadOnlyList<EquipmentStatContribution> contributions,
            string sourceId,
            List<StatModifier> destination)
        {
            for (var index = 0; index < contributions.Count; index++)
            {
                var contribution = contributions[index];
                if (contribution.Value <= 0f || !TryMapStat(contribution.StatType, out var statId))
                {
                    continue;
                }

                // 장비 데이터는 표시용 백분율이고 공용 계약은 0.1 = 10% 규칙을 쓴다.
                destination.Add(new StatModifier(
                    statId,
                    contribution.IsRelativeToBase ? StatOperation.AdditiveRate : StatOperation.Flat,
                    contribution.Value / 100f,
                    sourceId));
            }
        }

        private static bool TryMapStat(EquipmentStatType source, out StatId destination)
        {
            switch (source)
            {
                case EquipmentStatType.AttackPower: destination = StatId.AttackPower; return true;
                case EquipmentStatType.MaxHealth: destination = StatId.MaxHealth; return true;
                case EquipmentStatType.Defense: destination = StatId.Defense; return true;
                case EquipmentStatType.AttackSpeed: destination = StatId.AttackSpeed; return true;
                case EquipmentStatType.MoveSpeed: destination = StatId.MoveSpeed; return true;
                case EquipmentStatType.CriticalRate: destination = StatId.CriticalRate; return true;
                case EquipmentStatType.CriticalDamage: destination = StatId.CriticalDamage; return true;
                case EquipmentStatType.SkillDamage: destination = StatId.SkillDamage; return true;
                case EquipmentStatType.BossDamage: destination = StatId.BossDamage; return true;
                case EquipmentStatType.NormalMonsterDamage: destination = StatId.NormalMonsterDamage; return true;
                case EquipmentStatType.SkillCooldownReduction: destination = StatId.SkillCooldownReduction; return true;
                case EquipmentStatType.DefensePenetration: destination = StatId.DefensePenetration; return true;
                case EquipmentStatType.DamageReduction: destination = StatId.DamageReduction; return true;
                default:
                    destination = default;
                    return false;
            }
        }
    }
}
