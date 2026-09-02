using System;
using System.Collections.Generic;
using ProjectMT.Features.WorldDrops;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.Expedition
{
    [CreateAssetMenu(menuName = "ProjectMT/Expedition/Seed Profile", fileName = "ExpeditionSeedProfile")]
    public sealed class ExpeditionSeedProfile : ScriptableObject // 원정대 단계·웨이브·드랍 원본
    {
        [SerializeField, Min(0.1f)] private float waveIntervalSeconds = 2f; // 다음 웨이브 출현 간격
        [SerializeField, Min(1f)] private float challengeTimeLimitSeconds = 45f; // 도전 제한시간
        [SerializeField, Min(0.1f)] private float resultDelaySeconds = 0.8f; // 결과 표시 대기
        [SerializeField, Min(1f)] private float enemyBaseHealth = 28f; // 1단계 적 체력
        [SerializeField, Min(0.1f)] private float enemyBaseDamage = 4f; // 1단계 적 공격력
        [SerializeField, Min(0f)] private float enemyHealthGrowthPerStage = 0.11f; // 단계당 체력 증가율
        [SerializeField, Min(0f)] private float enemyDamageGrowthPerStage = 0.07f; // 단계당 공격 증가율
        [SerializeField, Min(0.2f)] private float enemyMeleeAttackRange = 2f; // 근접 적 기본 사거리
        [SerializeField, Min(0.2f)] private float enemyRangedAttackRange = 4.1f; // 원거리 적 기본 사거리

        [Header("Enemy Arrival")]
        [SerializeField, Min(0.5f)] private float enemyEntryDistance = 3.2f;
        [SerializeField, Min(0f)] private float enemySpawnIntervalSeconds = 0.16f;
        [SerializeField, Min(0.1f)] private float enemyMarchDurationSeconds = 1f;
        [SerializeField, Range(1f, 2f)] private float enemyFormationSpread = 1.3f;
        [SerializeField, Min(0f)] private float reinforcementWarningSeconds = 0.6f;
        [SerializeField, Min(0f)] private float reinforcementMinimumDelaySeconds = 3f;
        [SerializeField, Min(0.1f)] private float reinforcementForceDelaySeconds = 10f;
        [SerializeField, Range(0f, 1f)] private float reinforcementAliveRatio = 0.4f;

        [Header("Boss Stage")]
        [SerializeField, Min(1)] private int bossStageInterval = 5;
        [SerializeField, Min(1f)] private float bossHealthMultiplier = 10f;
        [SerializeField, Min(1f)] private float bossVisualScaleMultiplier = 2.5f;

        [Header("Stage Table")]
        [SerializeField] private ExpeditionStageDefinition[] stages = Array.Empty<ExpeditionStageDefinition>();

        [Header("World Drops Fallback")]
        [SerializeField] private WorldItemDropVisualCatalog worldItemDropVisualCatalog; // 일반 아이템 외형표
        [SerializeField] private string enemyWorldDropItemId = ItemIds.Gold; // 적 1기당 시드 드랍
        [SerializeField, Min(1)] private int enemyWorldDropQuantity = 1; // 최종 밸런스 전 최소 수량

        [Header("Equipment Drops")]
        [SerializeField] private EquipmentDropChestVisualCatalog equipmentDropChestVisualCatalog; // 등급별 상자 외형
        [SerializeField, Range(0f, 1f)] private float normalEnemyEquipmentDropChance = 0.05f; // 일반 적 임시 파밍값

        public float WaveIntervalSeconds => waveIntervalSeconds;
        public float ChallengeTimeLimitSeconds => challengeTimeLimitSeconds;
        public float ResultDelaySeconds => resultDelaySeconds;
        public WorldItemDropVisualCatalog WorldItemDropVisualCatalog => worldItemDropVisualCatalog;
        public string EnemyWorldDropItemId => enemyWorldDropItemId ?? string.Empty;
        public int EnemyWorldDropQuantity => Mathf.Max(1, enemyWorldDropQuantity);
        public EquipmentDropChestVisualCatalog EquipmentDropChestVisualCatalog => equipmentDropChestVisualCatalog;
        public float NormalEnemyEquipmentDropChance => Mathf.Clamp01(normalEnemyEquipmentDropChance);
        public float EnemyEntryDistance => Mathf.Max(0.5f, enemyEntryDistance);
        public float EnemySpawnIntervalSeconds => Mathf.Max(0f, enemySpawnIntervalSeconds);
        public float EnemyMarchDurationSeconds => Mathf.Max(0.1f, enemyMarchDurationSeconds);
        public float EnemyFormationSpread => Mathf.Clamp(enemyFormationSpread, 1f, 2f);
        public float ReinforcementWarningSeconds => Mathf.Max(0f, reinforcementWarningSeconds);
        public float ReinforcementMinimumDelaySeconds => Mathf.Max(0f, reinforcementMinimumDelaySeconds);
        public float ReinforcementForceDelaySeconds => Mathf.Max(
            ReinforcementMinimumDelaySeconds,
            reinforcementForceDelaySeconds);
        public float ReinforcementAliveRatio => Mathf.Clamp01(reinforcementAliveRatio);
        public int BossStageInterval => Mathf.Max(1, bossStageInterval);
        public float BossHealthMultiplier => Mathf.Max(1f, bossHealthMultiplier);
        public float BossVisualScaleMultiplier => Mathf.Max(1f, bossVisualScaleMultiplier);

        public bool IsBossStage(int stage)
        {
            return stage > 0 && stage % BossStageInterval == 0;
        }

        public bool ShouldDropNormalEnemyEquipment(float roll)
        {
            return NormalEnemyEquipmentDropChance >= 1f || Mathf.Clamp01(roll) < NormalEnemyEquipmentDropChance;
        }

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
            if (IsBossStage(stage))
            {
                return false;
            }

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

        public ExpeditionEnemySpawn ResolveSpawn(int stage, int wave, int unitIndex, int randomSeed)
        {
            var enemyCount = GetEnemyCount(stage, wave);
            if (TryResolveStage(stage, out var definition))
            {
                return definition.ResolveSpawn(
                    stage,
                    wave,
                    unitIndex,
                    enemyCount,
                    IsBossStage(stage),
                    randomSeed);
            }

            var ranged = IsRangedSlot(stage, unitIndex);
            var boss = IsBossStage(stage) &&
                       wave == GetWaveCount(stage) &&
                       unitIndex == Mathf.Max(0, enemyCount - 1);
            return new ExpeditionEnemySpawn(
                ResolveAppearance(stage, ranged),
                ranged ? ExpeditionEnemyRole.Ranged : ExpeditionEnemyRole.Melee,
                boss);
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
            var result = new UnitStatsSnapshot
            {
                maxHealth = enemyBaseHealth * (1f + enemyHealthGrowthPerStage * stageOffset) *
                            ResolveEnemyHealthTierMultiplier(stage),
                damage = enemyBaseDamage * (1f + enemyDamageGrowthPerStage * stageOffset),
                moveSpeed = ranged ? 2.28f : 2.58f,
                attackRange = ranged ? enemyRangedAttackRange : enemyMeleeAttackRange,
                attackInterval = ranged ? 1.2f : 1f,
                projectileSpeed = ranged ? 8f : 0f,
                ranged = ranged
            };
            FloorEnemyCombatStats(ref result);
            if (IsBossStage(stage))
            {
                result.maxHealth = Mathf.Floor(result.maxHealth * BossHealthMultiplier);
            }

            return result;
        }

        public UnitStatsSnapshot CreateEnemyStats(
            int stage,
            ExpeditionDifficulty difficulty,
            ExpeditionEnemySpawn spawn)
        {
            stage = Mathf.Clamp(stage, 1, ExpeditionCampaignRules.MaximumStage);
            var ranged = spawn.IsRanged;
            var normalStageOffset = stage - 1;
            var health = enemyBaseHealth * (1f + enemyHealthGrowthPerStage * normalStageOffset) *
                         ResolveEnemyHealthTierMultiplier(stage);
            var damage = enemyBaseDamage * (1f + enemyDamageGrowthPerStage * normalStageOffset);
            if (difficulty == ExpeditionDifficulty.Hard)
            {
                var normal100Health = enemyBaseHealth *
                                      (1f + enemyHealthGrowthPerStage * (ExpeditionCampaignRules.MaximumStage - 1)) *
                                      ResolveEnemyHealthTierMultiplier(ExpeditionCampaignRules.MaximumStage);
                var normal100Damage = enemyBaseDamage *
                                      (1f + enemyDamageGrowthPerStage * (ExpeditionCampaignRules.MaximumStage - 1));
                health = normal100Health * 1.6f * (1f + 0.08f * normalStageOffset);
                damage = normal100Damage * 1.3f * (1f + 0.055f * normalStageOffset);
            }

            var result = new UnitStatsSnapshot
            {
                maxHealth = health,
                damage = damage,
                moveSpeed = ranged ? 2.28f : 2.58f,
                attackRange = ranged ? enemyRangedAttackRange : enemyMeleeAttackRange,
                attackInterval = ranged ? 1.2f : 1f,
                projectileSpeed = ranged ? 8f : 0f,
                ranged = ranged
            };

            ApplyUpperKnightStats(spawn.Appearance, ref result);
            if (spawn.IsNinja)
            {
                result.maxHealth *= 0.7f;
                result.damage *= 1.1f;
                result.moveSpeed = 2.58f * 1.6f;
                result.attackRange = enemyMeleeAttackRange;
                result.attackInterval = 0.85f;
                result.projectileSpeed = 0f;
                result.ranged = false;
            }

            if (difficulty == ExpeditionDifficulty.Hard)
            {
                result.moveSpeed *= 1.08f;
            }

            FloorEnemyCombatStats(ref result);
            if (spawn.IsBoss)
            {
                result.maxHealth = Mathf.Floor(result.maxHealth * BossHealthMultiplier);
            }

            return result;
        }

        private static float ResolveEnemyHealthTierMultiplier(int stage)
        {
            if (stage <= 15)
            {
                return 1f;
            }

            if (stage <= 30)
            {
                return 2f;
            }

            if (stage <= 45)
            {
                return 3.5f;
            }

            if (stage <= 60)
            {
                return 8f;
            }

            if (stage <= 75)
            {
                return 18f;
            }

            if (stage <= 90)
            {
                return 43f;
            }

            return 47f;
        }

        private static void FloorEnemyCombatStats(ref UnitStatsSnapshot stats)
        {
            stats.maxHealth = Mathf.Max(1f, Mathf.Floor(stats.maxHealth));
            stats.damage = Mathf.Max(1f, Mathf.Floor(stats.damage));
        }

        private static void ApplyUpperKnightStats(EnemyAppearanceGroup appearance, ref UnitStatsSnapshot stats)
        {
            switch (appearance)
            {
                case EnemyAppearanceGroup.UpperKnightLower:
                    stats.maxHealth *= 1.08f;
                    stats.damage *= 1.05f;
                    break;
                case EnemyAppearanceGroup.UpperKnightMid:
                    stats.maxHealth *= 1.18f;
                    stats.damage *= 1.1f;
                    break;
                case EnemyAppearanceGroup.UpperKnightHigh:
                    stats.maxHealth *= 1.3f;
                    stats.damage *= 1.18f;
                    break;
                case EnemyAppearanceGroup.UpperKnightFinal:
                    stats.maxHealth *= 1.45f;
                    stats.damage *= 1.28f;
                    break;
            }
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

        public void EditorConfigureEquipmentDrops(
            EquipmentDropChestVisualCatalog visualCatalog,
            float dropChance)
        {
            equipmentDropChestVisualCatalog = visualCatalog;
            normalEnemyEquipmentDropChance = Mathf.Clamp01(dropChance);
        }

        public void EditorConfigureBoss(int interval, float healthMultiplier, float visualScaleMultiplier)
        {
            bossStageInterval = Mathf.Max(1, interval);
            bossHealthMultiplier = Mathf.Max(1f, healthMultiplier);
            bossVisualScaleMultiplier = Mathf.Max(1f, visualScaleMultiplier);
        }

        public void EditorConfigureArrival(
            float entryDistance,
            float spawnInterval,
            float marchDuration,
            float warningSeconds,
            float minimumDelay,
            float forceDelay,
            float aliveRatio,
            float formationSpread = 1.3f)
        {
            enemyEntryDistance = Mathf.Max(0.5f, entryDistance);
            enemySpawnIntervalSeconds = Mathf.Max(0f, spawnInterval);
            enemyMarchDurationSeconds = Mathf.Max(0.1f, marchDuration);
            enemyFormationSpread = Mathf.Clamp(formationSpread, 1f, 2f);
            reinforcementWarningSeconds = Mathf.Max(0f, warningSeconds);
            reinforcementMinimumDelaySeconds = Mathf.Max(0f, minimumDelay);
            reinforcementForceDelaySeconds = Mathf.Max(reinforcementMinimumDelaySeconds, forceDelay);
            reinforcementAliveRatio = Mathf.Clamp01(aliveRatio);
        }
#endif
    }

    public static class ExpeditionStageRules // 원정대 적 수·진형 공식
    {
        public const int LegacyWaveCount = 2; // 구버전 데이터 Fallback
        public const int WaveCount = LegacyWaveCount; // 기존 호출·테스트 호환
        public const int FallbackEnemiesPerWave = 8; // 데이터 누락 시에도 현재 수량 유지
        public const int FormationColumns = 4; // 한 행 최대 인원
        public const float FormationSpacing = 0.85f; // 유닛 간격
        public const int NinjaStartStage = 20; // 암살자 최초 출현 단계
        public const float EntryScatterSideDistance = 0.35f; // 같은 입장점 내 좌우 산개
        public const float EntryScatterForwardDistance = 0.2f; // 같은 입장점 내 앞뒤 산개

        public static int GetEnemiesPerWave(int stage)
        {
            return GetLegacyEnemiesPerWave(stage);
        }

        public static int GetLegacyEnemiesPerWave(int stage)
        {
            return FallbackEnemiesPerWave;
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

        public static Vector2 GetEntryScatterOffset(int unitIndex)
        {
            unitIndex = Mathf.Max(0, unitIndex);
            var side = unitIndex % 3 - 1;
            var forward = unitIndex / 3 % 3 - 1;
            return new Vector2(
                side * EntryScatterSideDistance,
                forward * EntryScatterForwardDistance);
        }

        public static Vector3 ResolveBattleForward(
            Vector3 playerFormationOrigin,
            Vector3 enemyFormationAnchor,
            Vector3 fallbackForward)
        {
            var forward = enemyFormationAnchor - playerFormationOrigin;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = fallbackForward;
                forward.y = 0f;
            }

            return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
        }
    }

    public static class ExpeditionCampaignRules // 일반 100 이후 하드 1로 전환하는 진행 규칙
    {
        public const int MaximumStage = 100;

        public static int ToProgressStage(ExpeditionDifficulty difficulty, int stage)
        {
            var localStage = Mathf.Clamp(stage, 0, MaximumStage);
            return difficulty == ExpeditionDifficulty.Hard ? MaximumStage + localStage : localStage;
        }
    }
}
