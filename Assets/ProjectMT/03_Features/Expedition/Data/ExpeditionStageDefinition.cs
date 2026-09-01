using System;
using System.Collections.Generic;
using ProjectMT.Features.WorldDrops;
using ProjectMT.Shared.Items;
using UnityEngine;

namespace ProjectMT.Features.Expedition
{
    [Serializable]
    public sealed class ExpeditionDropDefinition // 적 처치 드랍 한 항목
    {
        [SerializeField] private string itemId = ItemIds.Gold;
        [SerializeField, Min(1)] private int quantity = 1;
        [SerializeField, Range(0f, 1f)] private float chance = 1f;

        public string ItemId => itemId?.Trim() ?? string.Empty;
        public int Quantity => Mathf.Max(1, quantity);
        public float Chance => Mathf.Clamp01(chance);

        public bool TryCreate(float roll, Vector3 position, out WorldItemDropRequest request)
        {
            request = new WorldItemDropRequest(ItemId, Quantity, position);
            return request.IsValid && (Chance >= 1f || Mathf.Clamp01(roll) < Chance);
        }

#if UNITY_EDITOR
        public static ExpeditionDropDefinition EditorCreate(string id, int amount, float dropChance)
        {
            return new ExpeditionDropDefinition
            {
                itemId = id?.Trim(),
                quantity = Mathf.Max(1, amount),
                chance = Mathf.Clamp01(dropChance)
            };
        }
#endif
    }

    [Serializable]
    public sealed class ExpeditionWaveDefinition // 한 웨이브의 적 수·간격·드랍표
    {
        [SerializeField, Min(1)] private int baseEnemyCount = 8;
        [SerializeField, Min(0)] private int extraEnemyEveryStages;
        [SerializeField, Min(0f)] private float spawnDelaySeconds;
        [SerializeField] private float formationForwardOffset;
        [SerializeField] private ExpeditionDropDefinition[] drops = Array.Empty<ExpeditionDropDefinition>();

        public float SpawnDelaySeconds => Mathf.Max(0f, spawnDelaySeconds);
        public float FormationForwardOffset => formationForwardOffset;
        public IReadOnlyList<ExpeditionDropDefinition> Drops => drops ?? Array.Empty<ExpeditionDropDefinition>();

        public int ResolveEnemyCount(int stage, int bandMinimumStage)
        {
            var count = Mathf.Max(1, baseEnemyCount);
            return extraEnemyEveryStages <= 0
                ? count
                : count + Mathf.Max(0, stage - Mathf.Max(1, bandMinimumStage)) / extraEnemyEveryStages;
        }

#if UNITY_EDITOR
        public static ExpeditionWaveDefinition EditorCreate(
            int enemyCount,
            int growthInterval,
            float delaySeconds,
            float forwardOffset,
            params ExpeditionDropDefinition[] dropTable)
        {
            return new ExpeditionWaveDefinition
            {
                baseEnemyCount = Mathf.Max(1, enemyCount),
                extraEnemyEveryStages = Mathf.Max(0, growthInterval),
                spawnDelaySeconds = Mathf.Max(0f, delaySeconds),
                formationForwardOffset = forwardOffset,
                drops = dropTable ?? Array.Empty<ExpeditionDropDefinition>()
            };
        }
#endif
    }

    [Serializable]
    public sealed class ExpeditionStageDefinition // 연속 단계 구간의 웨이브·외형 원본
    {
        [SerializeField] private string definitionId = "stage_01_10";
        [SerializeField, Min(1)] private int minimumStage = 1;
        [SerializeField, Min(0)] private int maximumStage = 10; // 0이면 상한 없음
        [SerializeField] private EnemyAppearanceGroup meleeAppearance = EnemyAppearanceGroup.Peasant;
        [SerializeField] private EnemyAppearanceGroup rangedAppearance = EnemyAppearanceGroup.Peasant;
        [SerializeField, Min(0)] private int rangedEveryUnits;
        [SerializeField] private ExpeditionSpawnPoolEntry[] spawnPool = Array.Empty<ExpeditionSpawnPoolEntry>();
        [SerializeField, Range(0, 4)] private int ninjaCount;
        [SerializeField] private ExpeditionWaveDefinition[] waves = Array.Empty<ExpeditionWaveDefinition>();

        public string DefinitionId => definitionId?.Trim() ?? string.Empty;
        public int MinimumStage => Mathf.Max(1, minimumStage);
        public int MaximumStage => Mathf.Max(0, maximumStage);
        public int WaveCount => waves?.Length ?? 0;
        public int NinjaCount => Mathf.Clamp(ninjaCount, 0, 4);
        public IReadOnlyList<ExpeditionSpawnPoolEntry> SpawnPool =>
            spawnPool ?? Array.Empty<ExpeditionSpawnPoolEntry>();

        public bool Contains(int stage)
        {
            stage = Mathf.Max(1, stage);
            return stage >= MinimumStage && (MaximumStage == 0 || stage <= MaximumStage);
        }

        public bool TryGetWave(int wave, out ExpeditionWaveDefinition definition)
        {
            var index = wave - 1;
            if (waves != null && index >= 0 && index < waves.Length && waves[index] != null)
            {
                definition = waves[index];
                return true;
            }

            definition = null;
            return false;
        }

        public bool IsRangedSlot(int unitIndex)
        {
            return rangedEveryUnits > 0 && (Mathf.Max(0, unitIndex) + 1) % rangedEveryUnits == 0;
        }

        public EnemyAppearanceGroup ResolveAppearance(bool ranged)
        {
            return ranged ? rangedAppearance : meleeAppearance;
        }

        public ExpeditionEnemySpawn ResolveSpawn(
            int stage,
            int wave,
            int unitIndex,
            int enemyCount,
            bool bossStage,
            int randomSeed)
        {
            var boss = bossStage && wave == WaveCount && unitIndex == Mathf.Max(0, enemyCount - 1);
            var ninjaInWave = GetNinjaCountForWave(wave);
            var eligibleSlots = Mathf.Max(0, enemyCount - (boss ? 1 : 0));
            ninjaInWave = Mathf.Min(ninjaInWave, eligibleSlots);
            if (!boss && unitIndex < ninjaInWave)
            {
                return new ExpeditionEnemySpawn(
                    EnemyAppearanceGroup.Ninja,
                    ExpeditionEnemyRole.Flanker,
                    false,
                    GetNinjaCountBeforeWave(wave) + unitIndex);
            }

            var candidates = spawnPool ?? Array.Empty<ExpeditionSpawnPoolEntry>();
            var totalWeight = 0;
            for (var index = 0; index < candidates.Length; index++)
            {
                if (candidates[index] != null && candidates[index].Role != ExpeditionEnemyRole.Flanker)
                {
                    totalWeight += candidates[index].Weight;
                }
            }

            if (totalWeight > 0)
            {
                var fallbackSeed = stage * 397 ^ wave * 31 ^ unitIndex;
                var random = new System.Random(randomSeed == 0 ? fallbackSeed : randomSeed);
                var roll = random.Next(totalWeight);
                for (var index = 0; index < candidates.Length; index++)
                {
                    var candidate = candidates[index];
                    if (candidate == null || candidate.Role == ExpeditionEnemyRole.Flanker)
                    {
                        continue;
                    }

                    if (roll < candidate.Weight)
                    {
                        return new ExpeditionEnemySpawn(candidate.Appearance, candidate.Role, boss);
                    }

                    roll -= candidate.Weight;
                }
            }

            var ranged = IsRangedSlot(unitIndex);
            return new ExpeditionEnemySpawn(
                ResolveAppearance(ranged),
                ranged ? ExpeditionEnemyRole.Ranged : ExpeditionEnemyRole.Melee,
                boss);
        }

        public int GetNinjaCountForWave(int wave)
        {
            if (NinjaCount <= 0 || WaveCount <= 0 || wave <= 0 || wave > WaveCount)
            {
                return 0;
            }

            if (NinjaCount <= WaveCount)
            {
                if (NinjaCount == 1)
                {
                    return wave == (WaveCount + 1) / 2 ? 1 : 0;
                }

                if (NinjaCount == 2 && WaveCount == 3)
                {
                    return wave == 1 || wave == 3 ? 1 : 0;
                }

                return wave <= NinjaCount ? 1 : 0;
            }

            var count = NinjaCount / WaveCount;
            var remainder = NinjaCount % WaveCount;
            if (remainder > 0 && wave > WaveCount - remainder)
            {
                count++;
            }

            return count;
        }

        private int GetNinjaCountBeforeWave(int wave)
        {
            var count = 0;
            for (var previous = 1; previous < wave; previous++)
            {
                count += GetNinjaCountForWave(previous);
            }

            return count;
        }

#if UNITY_EDITOR
        public static ExpeditionStageDefinition EditorCreate(
            string id,
            int minimum,
            int maximum,
            EnemyAppearanceGroup melee,
            EnemyAppearanceGroup ranged,
            int rangedInterval,
            params ExpeditionWaveDefinition[] waveDefinitions)
        {
            return new ExpeditionStageDefinition
            {
                definitionId = id?.Trim(),
                minimumStage = Mathf.Max(1, minimum),
                maximumStage = Mathf.Max(0, maximum),
                meleeAppearance = melee,
                rangedAppearance = ranged,
                rangedEveryUnits = Mathf.Max(0, rangedInterval),
                waves = waveDefinitions ?? Array.Empty<ExpeditionWaveDefinition>()
            };
        }

        public static ExpeditionStageDefinition EditorCreate(
            string id,
            int minimum,
            int maximum,
            int ninjas,
            ExpeditionSpawnPoolEntry[] pool,
            params ExpeditionWaveDefinition[] waveDefinitions)
        {
            return new ExpeditionStageDefinition
            {
                definitionId = id?.Trim(),
                minimumStage = Mathf.Max(1, minimum),
                maximumStage = Mathf.Max(0, maximum),
                spawnPool = pool ?? Array.Empty<ExpeditionSpawnPoolEntry>(),
                ninjaCount = Mathf.Clamp(ninjas, 0, 4),
                waves = waveDefinitions ?? Array.Empty<ExpeditionWaveDefinition>()
            };
        }
#endif
    }
}
