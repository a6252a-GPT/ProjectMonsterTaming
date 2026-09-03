using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;

namespace ProjectMT.Features.Equipment
{
    // 장비 획득 순간 부위·등급·레벨·랜덤 옵션의 최종값을 모두 확정한다.
    public static class EquipmentDropRoller
    {
        public const int DropCount = 6;

        public static List<EquipmentInstanceData> RollDrop(
            int basisStage,
            Random random = null)
        {
            return RollDrop(EquipmentBalanceConfig.RuntimeDefault, basisStage, random);
        }

        public static List<EquipmentInstanceData> RollDrop(
            EquipmentBalanceConfig balance,
            int basisStage,
            Random random = null)
        {
            Validate(balance, basisStage);
            var rng = random ?? new Random();
            var results = new List<EquipmentInstanceData>(DropCount);
            for (var index = 0; index < DropCount; index++)
            {
                results.Add(RollSingle(balance, basisStage, rng));
            }

            return results;
        }

        public static EquipmentInstanceData RollSingle(
            EquipmentBalanceConfig balance,
            int basisStage,
            Random random = null)
        {
            Validate(balance, basisStage);
            var rng = random ?? new Random();
            var itemLevel = EquipmentLevelRules.RollLevel(basisStage, balance, rng);
            var part = EquipmentPartInfo.RollUniform((float)rng.NextDouble());
            var grade = EquipmentGradeInfo.RollWeighted((float)(rng.NextDouble() * 100.0), balance);
            var randomOptions = EquipmentRandomOptionRoller.Roll(grade, itemLevel, balance, rng);
            return new EquipmentInstanceData(
                Guid.NewGuid().ToString("N"),
                part,
                grade,
                itemLevel,
                randomOptions);
        }

        private static void Validate(EquipmentBalanceConfig balance, int basisStage)
        {
            if (balance == null)
            {
                throw new ArgumentNullException(nameof(balance));
            }

            if (basisStage < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(basisStage));
            }

            if (!balance.TryValidate(out var error))
            {
                throw new ArgumentException(error, nameof(balance));
            }
        }
    }
}
