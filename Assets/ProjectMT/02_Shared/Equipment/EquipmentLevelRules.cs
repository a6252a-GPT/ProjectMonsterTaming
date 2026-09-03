using System;
using UnityEngine;
using Random = System.Random;

namespace ProjectMT.Shared.Equipment
{
    public static class EquipmentLevelRules
    {
        public static int RollLevel(int basisStage, EquipmentBalanceConfig balance, Random random)
        {
            Validate(basisStage, balance);
            if (random == null) throw new ArgumentNullException(nameof(random));
            var total = 0d;
            for (var index = 0; index < balance.LevelOutcomeCount; index++) total += balance.GetLevelWeight(index);
            var unitRoll = random.NextDouble();
            if (double.IsNaN(unitRoll) || unitRoll < 0d || unitRoll >= 1d)
                throw new ArgumentOutOfRangeException(nameof(random));
            var roll = unitRoll * total;
            var accumulated = 0d;
            var last = 1;
            for (var index = 0; index < balance.LevelOutcomeCount; index++)
            {
                var weight = balance.GetLevelWeight(index);
                if (weight <= 0f) continue;
                last = ResolveLevel(basisStage, balance.GetLevelOffset(index), balance.MaximumItemLevel);
                accumulated += weight;
                if (roll < accumulated) return last;
            }
            return last;
        }

        public static Vector2Int GetLevelRange(int basisStage, EquipmentBalanceConfig balance)
        {
            Validate(basisStage, balance);
            var minimum = int.MaxValue;
            var maximum = 1;
            for (var index = 0; index < balance.LevelOutcomeCount; index++)
            {
                if (balance.GetLevelWeight(index) <= 0f) continue;
                var level = ResolveLevel(basisStage, balance.GetLevelOffset(index), balance.MaximumItemLevel);
                minimum = Math.Min(minimum, level);
                maximum = Math.Max(maximum, level);
            }
            return new Vector2Int(minimum, maximum);
        }

        public static float GetMultiplier(int itemLevel, float growthPerLevel)
        {
            if (itemLevel < 1) throw new ArgumentOutOfRangeException(nameof(itemLevel));
            if (float.IsNaN(growthPerLevel) || float.IsInfinity(growthPerLevel) || growthPerLevel < 0f)
                throw new ArgumentOutOfRangeException(nameof(growthPerLevel));
            var value = 1d + (double)growthPerLevel * (itemLevel - 1L);
            if (value > float.MaxValue) throw new OverflowException("Equipment level multiplier overflow.");
            return (float)value;
        }

        private static int ResolveLevel(int stage, int offset, int maximum) =>
            (int)Math.Max(1L, Math.Min(maximum, (long)stage + offset));

        private static void Validate(int basisStage, EquipmentBalanceConfig balance)
        {
            if (basisStage < 1) throw new ArgumentOutOfRangeException(nameof(basisStage));
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            if (!balance.TryValidate(out var error)) throw new ArgumentException(error, nameof(balance));
        }
    }
}
