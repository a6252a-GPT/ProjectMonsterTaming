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
        [SerializeField, Min(1)] private int baseEnemyCount = 4;
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
        [SerializeField] private ExpeditionWaveDefinition[] waves = Array.Empty<ExpeditionWaveDefinition>();

        public string DefinitionId => definitionId?.Trim() ?? string.Empty;
        public int MinimumStage => Mathf.Max(1, minimumStage);
        public int MaximumStage => Mathf.Max(0, maximumStage);
        public int WaveCount => waves?.Length ?? 0;

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
#endif
    }
}
