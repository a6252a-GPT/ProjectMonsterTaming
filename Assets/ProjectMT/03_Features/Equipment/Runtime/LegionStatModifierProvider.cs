using System;
using System.Collections.Generic;
using ProjectMT.Features.Commander;
using ProjectMT.Shared.Commander;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Stats;

namespace ProjectMT.Features.Equipment
{
    public static class LegionStatModifierProvider // 저장된 군단 성장 출처를 전투 입력 하나로 조립
    {
        public static List<StatModifier> Build(
            GameProgressView progress,
            CommanderGrowthConfig growthConfig,
            EquipmentBalanceConfig equipmentBalance)
        {
            if (growthConfig == null)
            {
                throw new ArgumentNullException(nameof(growthConfig));
            }

            if (equipmentBalance == null)
            {
                throw new ArgumentNullException(nameof(equipmentBalance));
            }

            var result = new List<StatModifier>();
            AppendCommanderGrowth(progress.CommanderLegionGrowth, growthConfig, result);
            EquipmentLegionModifierProvider.Append(progress.Equipment, equipmentBalance, result);
            AppendSlotUpgrades(progress.EquipmentSlotUpgrade, result);
            AppendPotential(progress.CommanderPotential, result);
            return result;
        }

        private static void AppendCommanderGrowth(
            CommanderLegionGrowthView growth,
            CommanderGrowthConfig config,
            List<StatModifier> destination)
        {
            foreach (CommanderLegionStat stat in Enum.GetValues(typeof(CommanderLegionStat)))
            {
                var value = config.GetLegionGrowthRate(stat, growth.GetLevel(stat));
                if (value <= 0f || !TryMapCommanderStat(stat, out var statId))
                {
                    continue;
                }

                destination.Add(new StatModifier(
                    statId,
                    StatOperation.AdditiveRate,
                    value,
                    $"commander-growth:{stat}"));
            }
        }

        private static void AppendSlotUpgrades(
            EquipmentSlotUpgradeView upgrades,
            List<StatModifier> destination)
        {
            foreach (EquipmentPart part in Enum.GetValues(typeof(EquipmentPart)))
            {
                EquipmentLegionModifierProvider.AppendContributions(
                    EquipmentSlotUpgradeCalculator.GetBonusContributions(part, upgrades.GetLevel(part)),
                    $"equipment-slot:{part}",
                    destination);
            }
        }

        private static void AppendPotential(
            CommanderPotentialView potential,
            List<StatModifier> destination)
        {
            for (var index = 0; index < CommanderPotentialData.SlotCount; index++)
            {
                var slot = potential.GetSlot(index);
                if (!slot.HasValue)
                {
                    continue;
                }

                EquipmentLegionModifierProvider.AppendContributions(
                    EquipmentOptionInfo.ResolveContributions(slot.OptionType, slot.Value),
                    $"commander-potential:{index}",
                    destination);
            }
        }

        private static bool TryMapCommanderStat(CommanderLegionStat source, out StatId destination)
        {
            switch (source)
            {
                case CommanderLegionStat.MaxHealth: destination = StatId.MaxHealth; return true;
                case CommanderLegionStat.AttackPower: destination = StatId.AttackPower; return true;
                case CommanderLegionStat.Defense: destination = StatId.Defense; return true;
                case CommanderLegionStat.AttackSpeed: destination = StatId.AttackSpeed; return true;
                case CommanderLegionStat.MoveSpeed: destination = StatId.MoveSpeed; return true;
                case CommanderLegionStat.AttackRange: destination = StatId.AttackRange; return true;
                default:
                    destination = default;
                    return false;
            }
        }
    }
}
