using System;
using System.Collections.Generic;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Stats;

namespace ProjectMT.Features.Commander
{
    public static class CommanderLegionModifierProvider // 군단장 레벨을 군단 공용 성장으로 변환
    {
        public static void Append(
            CommanderProgressView progress,
            CommanderGrowthConfig config,
            List<StatModifier> destination)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            var rate = config.GetAccumulatedCoreStatRate(progress.Level);
            if (rate <= 0f)
            {
                return;
            }

            destination.Add(new StatModifier(
                StatId.MaxHealth,
                StatOperation.AdditiveRate,
                rate,
                "commander_level"));
            destination.Add(new StatModifier(
                StatId.AttackPower,
                StatOperation.AdditiveRate,
                rate,
                "commander_level"));
            destination.Add(new StatModifier(
                StatId.Defense,
                StatOperation.AdditiveRate,
                rate,
                "commander_level"));
        }
    }
}
