using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Features.Expedition;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.ExpeditionBalance
{
    internal readonly struct ExpeditionWaveBalanceGlobalValues
    {
        public ExpeditionWaveBalanceGlobalValues(
            float baseHealth, float healthGrowth, float baseDamage, float damageGrowth,
            float baseDefense, float defenseGrowth,
            float meleeRange, float rangedRange, float challengeSeconds, float waveInterval,
            int bossInterval, float bossHealthMultiplier)
        {
            BaseHealth = baseHealth;
            HealthGrowth = healthGrowth;
            BaseDamage = baseDamage;
            DamageGrowth = damageGrowth;
            BaseDefense = baseDefense;
            DefenseGrowth = defenseGrowth;
            MeleeRange = meleeRange;
            RangedRange = rangedRange;
            ChallengeSeconds = challengeSeconds;
            WaveInterval = waveInterval;
            BossInterval = bossInterval;
            BossHealthMultiplier = bossHealthMultiplier;
        }

        public float BaseHealth { get; }
        public float HealthGrowth { get; }
        public float BaseDamage { get; }
        public float DamageGrowth { get; }
        public float BaseDefense { get; }
        public float DefenseGrowth { get; }
        public float MeleeRange { get; }
        public float RangedRange { get; }
        public float ChallengeSeconds { get; }
        public float WaveInterval { get; }
        public int BossInterval { get; }
        public float BossHealthMultiplier { get; }
    }

    internal readonly struct ExpeditionSpawnPoolBalanceValues
    {
        public ExpeditionSpawnPoolBalanceValues(
            EnemyAppearanceGroup appearance,
            float percentage,
            MonsterRarity minimumRarity,
            MonsterRarity maximumRarity)
        {
            Appearance = appearance;
            Percentage = percentage;
            MinimumRarity = minimumRarity;
            MaximumRarity = maximumRarity;
        }

        public EnemyAppearanceGroup Appearance { get; }
        public float Percentage { get; }
        public MonsterRarity MinimumRarity { get; }
        public MonsterRarity MaximumRarity { get; }
    }

    internal readonly struct ExpeditionWaveBalanceWaveValues
    {
        public ExpeditionWaveBalanceWaveValues(
            int enemyCount,
            float delay,
            float forwardOffset,
            float healthPercent,
            float damagePercent,
            float defensePercent,
            IReadOnlyList<ExpeditionSpawnPoolBalanceValues> spawnPool)
        {
            EnemyCount = enemyCount;
            Delay = delay;
            ForwardOffset = forwardOffset;
            HealthPercent = healthPercent;
            DamagePercent = damagePercent;
            DefensePercent = defensePercent;
            SpawnPool = spawnPool ?? Array.Empty<ExpeditionSpawnPoolBalanceValues>();
        }

        public int EnemyCount { get; }
        public float Delay { get; }
        public float ForwardOffset { get; }
        public float HealthPercent { get; }
        public float DamagePercent { get; }
        public float DefensePercent { get; }
        public IReadOnlyList<ExpeditionSpawnPoolBalanceValues> SpawnPool { get; }
    }

    internal sealed class ExpeditionWaveBalanceStageValues
    {
        public ExpeditionWaveBalanceStageValues(
            string definitionId,
            IReadOnlyList<ExpeditionWaveBalanceWaveValues> waves)
        {
            DefinitionId = definitionId ?? string.Empty;
            Waves = waves ?? Array.Empty<ExpeditionWaveBalanceWaveValues>();
        }

        public string DefinitionId { get; }
        public IReadOnlyList<ExpeditionWaveBalanceWaveValues> Waves { get; }
    }

    internal static class ExpeditionWaveBalanceAssetWriter
    {
        public const string ProfilePath =
            "Assets/ProjectMT/03_Features/Expedition/Data/ExpeditionSeedProfile_Seed.asset";

        public static ExpeditionSeedProfile LoadProfile() =>
            AssetDatabase.LoadAssetAtPath<ExpeditionSeedProfile>(ProfilePath);

        public static string CaptureSourceJson(ExpeditionSeedProfile profile) =>
            profile == null ? string.Empty : EditorJsonUtility.ToJson(profile);

        public static void Apply(
            ExpeditionSeedProfile profile,
            string expectedSourceJson,
            ExpeditionWaveBalanceGlobalValues globals,
            IReadOnlyList<ExpeditionWaveBalanceStageValues> stageRows)
        {
            ValidateOwnerAndVersion(profile, expectedSourceJson);
            ValidateGlobals(globals);
            if (stageRows == null || stageRows.Count != profile.Stages.Count)
                throw new InvalidOperationException("표의 단계 수가 운영 원본과 일치하지 않습니다.");
            for (var index = 0; index < stageRows.Count; index++)
                ValidateStage(profile.Stages[index], stageRows[index]);

            ApplyWithRollback(profile, "Apply Expedition Wave Balance Table", () =>
            {
                profile.EditorConfigureCombatBalance(
                    globals.BaseHealth, globals.HealthGrowth,
                    globals.BaseDamage, globals.DamageGrowth,
                    globals.BaseDefense, globals.DefenseGrowth,
                    globals.MeleeRange, globals.RangedRange,
                    globals.ChallengeSeconds, globals.WaveInterval);
                profile.EditorConfigureBoss(
                    globals.BossInterval,
                    globals.BossHealthMultiplier,
                    profile.BossVisualScaleMultiplier);

                for (var stageIndex = 0; stageIndex < stageRows.Count; stageIndex++)
                {
                    var sourceStage = profile.Stages[stageIndex];
                    var row = stageRows[stageIndex];
                    sourceStage.EditorConfigureWaveTable(BuildWaves(sourceStage, row.Waves, globals.WaveInterval));
                    sourceStage.EditorConfigureSpawnPool(Array.Empty<ExpeditionSpawnPoolEntry>());
                }
            });
        }

        public static void MigrateLegacyProfileToOneHundredStages()
        {
            var profile = LoadProfile();
            if (profile == null) throw new InvalidOperationException("원정대 Seed Profile을 찾을 수 없습니다.");
            var isExactOneHundred = profile.Stages.Count == ExpeditionCampaignRules.MaximumStage &&
                profile.Stages.Select((stage, index) => stage != null &&
                    stage.MinimumStage == index + 1 && stage.MaximumStage == index + 1).All(value => value);
            var everyWaveOwnsPool = isExactOneHundred && profile.Stages.All(stage =>
                stage.SpawnPool.Count == 0 &&
                Enumerable.Range(1, stage.WaveCount).All(wave =>
                    stage.TryGetWave(wave, out var definition) && definition != null && definition.HasSpawnPool));
            if (everyWaveOwnsPool) return;

            ApplyWithRollback(profile, "Migrate Expedition Wave-owned Spawn Pools", () =>
            {
                var stages = new ExpeditionStageDefinition[ExpeditionCampaignRules.MaximumStage];
                for (var stage = 1; stage <= ExpeditionCampaignRules.MaximumStage; stage++)
                {
                    if (!profile.TryResolveStage(stage, out var source) || source == null)
                        throw new InvalidOperationException($"원정대 {stage}단계 원본 구간이 없습니다.");
                    var waves = new ExpeditionWaveDefinition[Mathf.Max(1, source.WaveCount)];
                    for (var waveIndex = 0; waveIndex < waves.Length; waveIndex++)
                    {
                        if (!source.TryGetWave(waveIndex + 1, out var sourceWave) || sourceWave == null)
                            sourceWave = ExpeditionWaveDefinition.EditorCreate(
                                8, 0, waveIndex == 0 ? 0f : profile.WaveIntervalSeconds, waveIndex * 1.15f);
                        var pool = ResolveMigratedPool(profile, source, sourceWave, stage);
                        waves[waveIndex] = sourceWave.EditorCopyWithBalance(
                            sourceWave.BaseEnemyCount,
                            0,
                            sourceWave.SpawnDelaySeconds,
                            sourceWave.FormationForwardOffset,
                            sourceWave.HealthPercent,
                            sourceWave.DamagePercent,
                            sourceWave.DefensePercent,
                            pool);
                    }

                    stages[stage - 1] = ExpeditionStageDefinition.EditorCreate(
                        $"stage_{stage:000}", stage, stage,
                        Array.Empty<ExpeditionSpawnPoolEntry>(), waves);
                }
                profile.EditorConfigureStageTable(stages);
            });
        }

        private static ExpeditionSpawnPoolEntry[] ResolveMigratedPool(
            ExpeditionSeedProfile profile,
            ExpeditionStageDefinition source,
            ExpeditionWaveDefinition sourceWave,
            int stage)
        {
            var preferred = sourceWave.HasSpawnPool ? sourceWave.SpawnPool : source.SpawnPool;
            var existing = preferred.Where(entry => entry != null && entry.Percentage > 0f).ToArray();
            if (existing.Length > 0)
            {
                var total = existing.Sum(entry => entry.Percentage);
                return existing.Select(entry => ExpeditionSpawnPoolEntry.EditorCreate(
                    entry.Appearance,
                    entry.Percentage / total * 100f,
                    entry.MinimumRarity,
                    entry.MaximumRarity)).ToArray();
            }

            return BuildLegacyPool(profile, source, stage);
        }

        private static ExpeditionSpawnPoolEntry[] BuildLegacyPool(
            ExpeditionSeedProfile profile,
            ExpeditionStageDefinition source,
            int stage)
        {
            var normal = new List<EnemyAppearanceGroup> { source.ResolveAppearance(false) };
            if (source.ResolveAppearance(true) != source.ResolveAppearance(false))
                normal.Add(source.ResolveAppearance(true));
            var legacyNinjaPercentage = stage >= 20 && source.LegacyNinjaCount > 0
                ? Mathf.Clamp(source.LegacyNinjaCount * 100f / Mathf.Max(1, profile.GetTotalEnemies(stage)), 0f, 80f)
                : 0f;
            var normalBudget = 100f - legacyNinjaPercentage;
            var normalPercentage = normalBudget / normal.Count;
            var result = normal.Select(appearance => ExpeditionSpawnPoolEntry.EditorCreate(
                appearance, normalPercentage, MonsterRarity.Common, MonsterRarity.Common)).ToList();
            if (legacyNinjaPercentage > 0f)
            {
                result.Add(ExpeditionSpawnPoolEntry.EditorCreate(
                    EnemyAppearanceGroup.Ninja,
                    legacyNinjaPercentage,
                    MonsterRarity.Common,
                    MonsterRarity.Common));
            }
            return result.ToArray();
        }

        private static ExpeditionWaveDefinition[] BuildWaves(
            ExpeditionStageDefinition sourceStage,
            IReadOnlyList<ExpeditionWaveBalanceWaveValues> values,
            float defaultInterval)
        {
            var waves = new ExpeditionWaveDefinition[values.Count];
            for (var waveIndex = 0; waveIndex < waves.Length; waveIndex++)
            {
                var sourceWaveNumber = Mathf.Clamp(waveIndex + 1, 1, Mathf.Max(1, sourceStage.WaveCount));
                if (!sourceStage.TryGetWave(sourceWaveNumber, out var sourceWave) || sourceWave == null)
                    sourceWave = ExpeditionWaveDefinition.EditorCreate(
                        8, 0, waveIndex == 0 ? 0f : defaultInterval, waveIndex * 1.15f);
                var value = values[waveIndex];
                var pool = value.SpawnPool.Select(entry => ExpeditionSpawnPoolEntry.EditorCreate(
                    entry.Appearance, entry.Percentage, entry.MinimumRarity, entry.MaximumRarity));
                waves[waveIndex] = sourceWave.EditorCopyWithBalance(
                    value.EnemyCount,
                    0,
                    value.Delay,
                    value.ForwardOffset,
                    value.HealthPercent,
                    value.DamagePercent,
                    value.DefensePercent,
                    pool);
            }
            return waves;
        }

        private static void ValidateOwnerAndVersion(ExpeditionSeedProfile profile, string expectedSourceJson)
        {
            if (profile == null) throw new InvalidOperationException("원정대 Seed Profile을 찾을 수 없습니다.");
            if (!string.Equals(AssetDatabase.GetAssetPath(profile), ProfilePath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("운영 원정대 Seed Profile만 수정할 수 있습니다.");
            if (!string.Equals(CaptureSourceJson(profile), expectedSourceJson, StringComparison.Ordinal))
                throw new InvalidOperationException("표를 연 뒤 원정대 원본이 외부에서 변경되었습니다. 새로고침 후 다시 수정하세요.");
        }

        private static void ApplyWithRollback(ExpeditionSeedProfile profile, string undoName, Action apply)
        {
            var rollbackJson = CaptureSourceJson(profile);
            Undo.RecordObject(profile, undoName);
            try
            {
                apply();
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
            }
            catch
            {
                EditorJsonUtility.FromJsonOverwrite(rollbackJson, profile);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
                throw;
            }
        }

        private static void ValidateGlobals(ExpeditionWaveBalanceGlobalValues values)
        {
            RequireFinitePositive(values.BaseHealth, "1단계 적 체력");
            RequireFiniteNonNegative(values.HealthGrowth, "단계당 체력 증가율");
            RequireFinitePositive(values.BaseDamage, "1단계 적 공격력");
            RequireFiniteNonNegative(values.DamageGrowth, "단계당 공격 증가율");
            RequireFiniteNonNegative(values.BaseDefense, "1단계 적 방어력");
            RequireFiniteNonNegative(values.DefenseGrowth, "단계당 방어 증가율");
            RequireFinitePositive(values.MeleeRange, "근접 사거리");
            RequireFinitePositive(values.RangedRange, "원거리 사거리");
            RequireFinitePositive(values.ChallengeSeconds, "도전 제한시간");
            RequireFinitePositive(values.WaveInterval, "기본 웨이브 간격");
            if (values.BossInterval < 1) throw new InvalidOperationException("보스 간격은 1 이상이어야 합니다.");
            if (values.BossHealthMultiplier < 1f || !float.IsFinite(values.BossHealthMultiplier))
                throw new InvalidOperationException("보스 체력 배율은 1 이상의 유한값이어야 합니다.");
        }

        private static void ValidateStage(ExpeditionStageDefinition source, ExpeditionWaveBalanceStageValues values)
        {
            if (source == null || !string.Equals(source.DefinitionId, values.DefinitionId, StringComparison.Ordinal))
                throw new InvalidOperationException("단계 ID가 운영 원본과 일치하지 않습니다.");
            if (values.Waves.Count < 1 || values.Waves.Count > 3)
                throw new InvalidOperationException($"{source.DefinitionId}: 웨이브 수는 1~3이어야 합니다.");
            for (var index = 0; index < values.Waves.Count; index++)
            {
                var wave = values.Waves[index];
                if (wave.EnemyCount < 1)
                    throw new InvalidOperationException($"{source.DefinitionId} W{index + 1}: 적 수는 1 이상이어야 합니다.");
                RequireFiniteNonNegative(wave.Delay, $"{source.DefinitionId} W{index + 1} 지연");
                if (!float.IsFinite(wave.ForwardOffset))
                    throw new InvalidOperationException($"{source.DefinitionId} W{index + 1}: 전방 위치는 유한값이어야 합니다.");
                RequirePercent(wave.HealthPercent, $"{source.DefinitionId} W{index + 1} 체력");
                RequirePercent(wave.DamagePercent, $"{source.DefinitionId} W{index + 1} 공격");
                RequirePercent(wave.DefensePercent, $"{source.DefinitionId} W{index + 1} 방어", true);
                ValidatePool(source.DefinitionId, index + 1, wave.SpawnPool);
            }
        }

        private static void ValidatePool(
            string definitionId,
            int wave,
            IReadOnlyList<ExpeditionSpawnPoolBalanceValues> pool)
        {
            if (pool.Count == 0)
                throw new InvalidOperationException($"{definitionId} W{wave}: 적 구성 항목이 하나 이상 필요합니다.");
            var percentageTotal = 0f;
            for (var index = 0; index < pool.Count; index++)
            {
                var entry = pool[index];
                if (!float.IsFinite(entry.Percentage) || entry.Percentage < 0f || entry.Percentage > 100f)
                    throw new InvalidOperationException($"{definitionId} W{wave}: 출현 비율은 0~100%여야 합니다.");
                if (entry.MinimumRarity > entry.MaximumRarity)
                    throw new InvalidOperationException($"{definitionId} W{wave}: 최소 등급은 최대 등급보다 높을 수 없습니다.");
                percentageTotal += entry.Percentage;
            }
            if (Mathf.Abs(percentageTotal - 100f) > 0.05f)
                throw new InvalidOperationException($"{definitionId} W{wave}: 적 출현 비율 합계가 100%여야 합니다. 현재 {percentageTotal:0.##}%입니다.");
        }

        private static void RequirePercent(float value, string label, bool allowZero = false)
        {
            if (!float.IsFinite(value) || value > 1000f || (allowZero ? value < 0f : value <= 0f))
                throw new InvalidOperationException($"{label} 배율은 {(allowZero ? "0" : "0 초과")}~1000%여야 합니다.");
        }

        private static void RequireFinitePositive(float value, string label)
        {
            if (!float.IsFinite(value) || value <= 0f)
                throw new InvalidOperationException($"{label}은 0보다 큰 유한값이어야 합니다.");
        }

        private static void RequireFiniteNonNegative(float value, string label)
        {
            if (!float.IsFinite(value) || value < 0f)
                throw new InvalidOperationException($"{label}은 0 이상의 유한값이어야 합니다.");
        }
    }
}
