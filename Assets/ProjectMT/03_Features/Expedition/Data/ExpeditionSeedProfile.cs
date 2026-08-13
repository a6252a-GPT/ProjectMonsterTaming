using System;
using System.Collections.Generic;
using ProjectMT.Features.WorldDrops;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.Expedition
{
    [CreateAssetMenu(menuName = "ProjectMT/Expedition/Seed Profile", fileName = "ExpeditionSeedProfile")]
    public sealed class ExpeditionSeedProfile : ScriptableObject // 원정대 단계·웨이브·드랍 원본
    {
        [SerializeField, Min(0.1f)] private float waveIntervalSeconds = 2f; // 2웨이브 출현 간격
        [SerializeField, Min(1f)] private float challengeTimeLimitSeconds = 45f; // 도전 제한시간
        [SerializeField, Min(0.1f)] private float resultDelaySeconds = 0.8f; // 결과 표시 대기
        [SerializeField, Min(1f)] private float enemyBaseHealth = 28f; // 1단계 적 체력
        [SerializeField, Min(0.1f)] private float enemyBaseDamage = 4f; // 1단계 적 공격력
        [SerializeField, Min(0f)] private float enemyHealthGrowthPerStage = 0.11f; // 단계당 체력 증가율
        [SerializeField, Min(0f)] private float enemyDamageGrowthPerStage = 0.07f; // 단계당 공격 증가율

        [Header("Stage Table")]
        [SerializeField] private ExpeditionStageDefinition[] stages = Array.Empty<ExpeditionStageDefinition>();

        [Header("World Drops Fallback")]
        [SerializeField] private WorldItemDropVisualCatalog worldItemDropVisualCatalog; // 일반 아이템 외형표
        [SerializeField] private string enemyWorldDropItemId = ItemIds.Gold; // 적 1기당 시드 드랍
        [SerializeField, Min(1)] private int enemyWorldDropQuantity = 1; // 최종 밸런스 전 최소 수량

        public float WaveIntervalSeconds => waveIntervalSeconds;
        public float ChallengeTimeLimitSeconds => challengeTimeLimitSeconds;
        public float ResultDelaySeconds => resultDelaySeconds;
        public WorldItemDropVisualCatalog WorldItemDropVisualCatalog => worldItemDropVisualCatalog;
        public string EnemyWorldDropItemId => enemyWorldDropItemId ?? string.Empty;
        public int EnemyWorldDropQuantity => Mathf.Max(1, enemyWorldDropQuantity);

        public bool TryResolveStage(int stage, out ExpeditionStageDefinition definition)
        {
            if (stages != null)
            {
                for (var index = 0; index < stages.Length; index++)
                {
                    if (stages[index] != null && stages[index].Contains(stage))
                    {
                        definition = stages[index];
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

        public int GetWaveCount(int stage)
        {
            return TryResolveStage(stage, out var definition) && definition.WaveCount > 0
                ? definition.WaveCount
                : ExpeditionStageRules.LegacyWaveCount;
        }

        public int GetEnemyCount(int stage, int wave)
        {
            return TryResolveStage(stage, out var definition) && definition.TryGetWave(wave, out var waveDefinition)
                ? waveDefinition.ResolveEnemyCount(stage, definition.MinimumStage)
                : ExpeditionStageRules.GetLegacyEnemiesPerWave(stage);
        }

        public float GetWaveSpawnDelay(int stage, int wave)
        {
            return TryResolveStage(stage, out var definition) && definition.TryGetWave(wave, out var waveDefinition)
                ? waveDefinition.SpawnDelaySeconds
                : wave <= 1 ? 0f : WaveIntervalSeconds;
        }

        public float GetWaveForwardOffset(int stage, int wave)
        {
            return TryResolveStage(stage, out var definition) && definition.TryGetWave(wave, out var waveDefinition)
                ? waveDefinition.FormationForwardOffset
                : Mathf.Max(0, wave - 1) * 1.15f;
        }

        public int GetTotalEnemies(int stage)
        {
            var total = 0;
            var waveCount = GetWaveCount(stage);
            for (var wave = 1; wave <= waveCount; wave++)
            {
                total += GetEnemyCount(stage, wave);
            }

            return total;
        }

        public bool IsRangedSlot(int stage, int unitIndex)
        {
            return TryResolveStage(stage, out var definition)
                ? definition.IsRangedSlot(unitIndex)
                : Mathf.Max(1, stage) >= 11 && Mathf.Max(0, unitIndex) % 4 == 3;
        }

        public EnemyAppearanceGroup ResolveAppearance(int stage, bool ranged)
        {
            if (TryResolveStage(stage, out var definition))
            {
                return definition.ResolveAppearance(ranged);
            }

            if (stage <= 10)
            {
                return EnemyAppearanceGroup.Peasant;
            }

            var tier = Mathf.Min(3, (Mathf.Max(1, stage) - 1) / 10);
            return ranged
                ? tier == 1 ? EnemyAppearanceGroup.MageTier1 :
                  tier == 2 ? EnemyAppearanceGroup.MageTier2 : EnemyAppearanceGroup.MageTier3
                : tier == 1 ? EnemyAppearanceGroup.KnightTier1 :
                  tier == 2 ? EnemyAppearanceGroup.KnightTier2 : EnemyAppearanceGroup.KnightTier3;
        }

        public bool TryCreateEnemyWorldDrop(Vector3 position, out WorldItemDropRequest request)
        {
            request = new WorldItemDropRequest(EnemyWorldDropItemId, EnemyWorldDropQuantity, position);
            return worldItemDropVisualCatalog != null && request.IsValid;
        }

        public int CreateEnemyWorldDrops(
            int stage,
            int wave,
            Vector3 position,
            List<WorldItemDropRequest> destination)
        {
            if (destination == null || worldItemDropVisualCatalog == null)
            {
                return 0;
            }

            destination.Clear();
            if (TryResolveStage(stage, out var definition) && definition.TryGetWave(wave, out var waveDefinition))
            {
                var drops = waveDefinition.Drops;
                for (var index = 0; index < drops.Count; index++)
                {
                    if (drops[index] != null &&
                        drops[index].TryCreate(UnityEngine.Random.value, position, out var dropRequest))
                    {
                        destination.Add(dropRequest);
                    }
                }

                return destination.Count;
            }

            if (TryCreateEnemyWorldDrop(position, out var fallback))
            {
                destination.Add(fallback);
            }

            return destination.Count;
        }

        public UnitStatsSnapshot CreateEnemyStats(int stage, int unitIndex)
        {
            return CreateEnemyStats(stage, IsRangedSlot(stage, unitIndex));
        }

        public UnitStatsSnapshot CreateEnemyStats(int stage, bool ranged)
        {
            var stageOffset = Mathf.Max(0, stage - 1);
            return new UnitStatsSnapshot
            {
                maxHealth = enemyBaseHealth * (1f + enemyHealthGrowthPerStage * stageOffset),
                damage = enemyBaseDamage * (1f + enemyDamageGrowthPerStage * stageOffset),
                moveSpeed = ranged ? 1.9f : 2.15f,
                attackRange = ranged ? 4.1f : 1f,
                attackInterval = ranged ? 1.2f : 1f,
                projectileSpeed = ranged ? 8f : 0f,
                ranged = ranged
            };
        }

#if UNITY_EDITOR
        public void EditorConfigureStageTable(params ExpeditionStageDefinition[] definitions)
        {
            stages = definitions ?? Array.Empty<ExpeditionStageDefinition>();
        }

        public void EditorConfigureWorldDrops(
            WorldItemDropVisualCatalog visualCatalog,
            string itemId,
            int quantity)
        {
            worldItemDropVisualCatalog = visualCatalog;
            enemyWorldDropItemId = itemId?.Trim();
            enemyWorldDropQuantity = Mathf.Max(1, quantity);
        }
#endif
    }

    public static class ExpeditionStageRules // 원정대 적 수·진형 공식
    {
        public const int LegacyWaveCount = 2; // 구버전 데이터 Fallback
        public const int WaveCount = LegacyWaveCount; // 기존 호출·테스트 호환
        public const int FormationColumns = 4; // 한 행 최대 인원
        public const float FormationSpacing = 0.85f; // 유닛 간격

        public static int GetEnemiesPerWave(int stage)
        {
            return GetLegacyEnemiesPerWave(stage);
        }

        public static int GetLegacyEnemiesPerWave(int stage)
        {
            return 4 + (Mathf.Max(1, stage) - 1) / 10; // 10단계마다 한 마리 증가
        }

        public static int GetTotalEnemies(int stage)
        {
            return LegacyWaveCount * GetLegacyEnemiesPerWave(stage);
        }

        public static Vector2 GetFormationOffset(int unitIndex, int unitCount)
        {
            var row = unitIndex / FormationColumns;
            var column = unitIndex % FormationColumns;
            var rowStart = row * FormationColumns;
            var rowCount = Mathf.Min(FormationColumns, Mathf.Max(0, unitCount - rowStart));
            if (rowCount == 0)
            {
                return Vector2.zero;
            }

            var centeredColumn = column - (rowCount - 1) * 0.5f; // 덜 찬 마지막 행 중앙 정렬
            return new Vector2(centeredColumn * FormationSpacing, row * FormationSpacing);
        }
    }
}
