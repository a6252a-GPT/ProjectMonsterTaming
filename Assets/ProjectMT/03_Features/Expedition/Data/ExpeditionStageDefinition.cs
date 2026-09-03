using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Features.WorldDrops;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.Serialization;

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
        [SerializeField, Range(1f, 1000f)] private float healthPercent = 100f;
        [SerializeField, Range(1f, 1000f)] private float damagePercent = 100f;
        [SerializeField, Range(0f, 1000f)] private float defensePercent = 100f;
        [SerializeField] private ExpeditionSpawnPoolEntry[] spawnPool = Array.Empty<ExpeditionSpawnPoolEntry>();
        [SerializeField] private ExpeditionDropDefinition[] drops = Array.Empty<ExpeditionDropDefinition>();

        public int BaseEnemyCount => Mathf.Max(1, baseEnemyCount);
        public int ExtraEnemyEveryStages => Mathf.Max(0, extraEnemyEveryStages);
        public float SpawnDelaySeconds => Mathf.Max(0f, spawnDelaySeconds);
        public float FormationForwardOffset => formationForwardOffset;
        public float HealthPercent => Mathf.Clamp(healthPercent, 1f, 1000f);
        public float DamagePercent => Mathf.Clamp(damagePercent, 1f, 1000f);
        public float DefensePercent => Mathf.Clamp(defensePercent, 0f, 1000f);
        public float HealthMultiplier => HealthPercent * 0.01f;
        public float DamageMultiplier => DamagePercent * 0.01f;
        public float DefenseMultiplier => DefensePercent * 0.01f;
        public IReadOnlyList<ExpeditionSpawnPoolEntry> SpawnPool =>
            spawnPool ?? Array.Empty<ExpeditionSpawnPoolEntry>();
        public bool HasSpawnPool => spawnPool != null && spawnPool.Any(entry => entry != null);
        public IReadOnlyList<ExpeditionDropDefinition> Drops => drops ?? Array.Empty<ExpeditionDropDefinition>();

        public int ResolveEnemyCount(int stage, int bandMinimumStage)
        {
            var count = Mathf.Max(1, baseEnemyCount);
            return extraEnemyEveryStages <= 0
                ? count
                : count + Mathf.Max(0, stage - Mathf.Max(1, bandMinimumStage)) / extraEnemyEveryStages;
        }

#if UNITY_EDITOR
        public ExpeditionWaveDefinition EditorCopyWithBalance(
            int enemyCount,
            int growthInterval,
            float delaySeconds,
            float forwardOffset)
        {
            return EditorCopyWithBalance(
                enemyCount, growthInterval, delaySeconds, forwardOffset,
                HealthPercent, DamagePercent, DefensePercent, SpawnPool);
        }

        public ExpeditionWaveDefinition EditorCopyWithBalance(
            int enemyCount,
            int growthInterval,
            float delaySeconds,
            float forwardOffset,
            float waveHealthPercent,
            float waveDamagePercent,
            float waveDefensePercent,
            IEnumerable<ExpeditionSpawnPoolEntry> waveSpawnPool)
        {
            return new ExpeditionWaveDefinition
            {
                baseEnemyCount = Mathf.Max(1, enemyCount),
                extraEnemyEveryStages = Mathf.Max(0, growthInterval),
                spawnDelaySeconds = Mathf.Max(0f, delaySeconds),
                formationForwardOffset = forwardOffset,
                healthPercent = Mathf.Clamp(waveHealthPercent, 1f, 1000f),
                damagePercent = Mathf.Clamp(waveDamagePercent, 1f, 1000f),
                defensePercent = Mathf.Clamp(waveDefensePercent, 0f, 1000f),
                spawnPool = waveSpawnPool?.Where(entry => entry != null).ToArray() ??
                    Array.Empty<ExpeditionSpawnPoolEntry>(),
                drops = drops == null ? Array.Empty<ExpeditionDropDefinition>() :
                    (ExpeditionDropDefinition[])drops.Clone()
            };
        }

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
                healthPercent = 100f,
                damagePercent = 100f,
                defensePercent = 100f,
                spawnPool = Array.Empty<ExpeditionSpawnPoolEntry>(),
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
        [SerializeField, FormerlySerializedAs("ninjaCount"), HideInInspector] private int legacyNinjaCount;
        [SerializeField] private ExpeditionWaveDefinition[] waves = Array.Empty<ExpeditionWaveDefinition>();

        public string DefinitionId => definitionId?.Trim() ?? string.Empty;
        public int MinimumStage => Mathf.Max(1, minimumStage);
        public int MaximumStage => Mathf.Max(0, maximumStage);
        public int WaveCount => waves?.Length ?? 0;
        public int LegacyNinjaCount => Mathf.Clamp(legacyNinjaCount, 0, 4);
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
            TryGetWave(wave, out var waveDefinition);
            var candidates = waveDefinition?.HasSpawnPool == true
                ? waveDefinition.SpawnPool
                : SpawnPool;
            var waveHealthMultiplier = waveDefinition?.HealthMultiplier ?? 1f;
            var waveDamageMultiplier = waveDefinition?.DamageMultiplier ?? 1f;
            var waveDefenseMultiplier = waveDefinition?.DefenseMultiplier ?? 1f;
            var totalPercentage = 0f;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate != null && candidate.Percentage > 0f &&
                    (!boss || candidate.Role != ExpeditionEnemyRole.Flanker))
                    totalPercentage += candidate.Percentage;
            }

            if (totalPercentage > 0f)
            {
                var fallbackSeed = stage * 397 ^ wave * 31 ^ unitIndex;
                var random = new System.Random(randomSeed == 0 ? fallbackSeed : randomSeed);
                var roll = (float)random.NextDouble() * totalPercentage;
                for (var index = 0; index < candidates.Count; index++)
                {
                    var candidate = candidates[index];
                    if (candidate == null || candidate.Percentage <= 0f ||
                        (boss && candidate.Role == ExpeditionEnemyRole.Flanker))
                    {
                        continue;
                    }

                    if (roll < candidate.Percentage)
                    {
                        return new ExpeditionEnemySpawn(
                            candidate.Appearance,
                            candidate.Role,
                            boss,
                            candidate.Role == ExpeditionEnemyRole.Flanker ? unitIndex : -1,
                            candidate.ResolveRarity(random, boss),
                            waveHealthMultiplier,
                            waveDamageMultiplier,
                            waveDefenseMultiplier);
                    }

                    roll -= candidate.Percentage;
                }
            }

            var ranged = IsRangedSlot(unitIndex);
            return new ExpeditionEnemySpawn(
                ResolveAppearance(ranged),
                ranged ? ExpeditionEnemyRole.Ranged : ExpeditionEnemyRole.Melee,
                boss,
                -1,
                MonsterRarity.Common,
                waveHealthMultiplier,
                waveDamageMultiplier,
                waveDefenseMultiplier);
        }

#if UNITY_EDITOR
        public void EditorConfigureWaveTable(params ExpeditionWaveDefinition[] waveDefinitions)
        {
            waves = waveDefinitions == null || waveDefinitions.Length == 0
                ? new[] { ExpeditionWaveDefinition.EditorCreate(8, 0, 0f, 0f) }
                : waveDefinitions;
        }

        public void EditorConfigureSpawnPool(params ExpeditionSpawnPoolEntry[] entries)
        {
            spawnPool = entries ?? Array.Empty<ExpeditionSpawnPoolEntry>();
            legacyNinjaCount = 0;
        }

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
            ExpeditionSpawnPoolEntry[] pool,
            params ExpeditionWaveDefinition[] waveDefinitions)
        {
            return new ExpeditionStageDefinition
            {
                definitionId = id?.Trim(),
                minimumStage = Mathf.Max(1, minimum),
                maximumStage = Mathf.Max(0, maximum),
                spawnPool = pool ?? Array.Empty<ExpeditionSpawnPoolEntry>(),
                waves = waveDefinitions ?? Array.Empty<ExpeditionWaveDefinition>()
            };
        }

        public static ExpeditionStageDefinition EditorCreate(
            string id,
            int minimum,
            int maximum,
            int legacyNinjas,
            ExpeditionSpawnPoolEntry[] pool,
            params ExpeditionWaveDefinition[] waveDefinitions)
        {
            var definition = EditorCreate(id, minimum, maximum, pool, waveDefinitions);
            definition.legacyNinjaCount = Mathf.Clamp(legacyNinjas, 0, 4);
            return definition;
        }
#endif
    }
}
