using System;
using UnityEngine;

namespace ProjectMT.Features.OfflineReward
{
    [Serializable]
    public sealed class OfflineRewardRateEntry // 원정대 단계 구간별 임시 방치 보상률
    {
        [SerializeField, Min(1)] private int minimumStage = 1;
        [SerializeField, Min(0)] private long goldPerMinute = 10L;
        [SerializeField, Min(0)] private long commanderExperiencePerMinute = 5L;
        [SerializeField, Min(1)] private int upgradeStoneIntervalSeconds = 600;
        [SerializeField, Min(1)] private int rewardMultiplierBasisPoints = 10000;

        public int MinimumStage => Math.Max(1, minimumStage);
        public long GoldPerMinute => Math.Max(0L, goldPerMinute);
        public long CommanderExperiencePerMinute => Math.Max(0L, commanderExperiencePerMinute);
        public int UpgradeStoneIntervalSeconds => Math.Max(1, upgradeStoneIntervalSeconds);
        public int RewardMultiplierBasisPoints => Math.Max(1, rewardMultiplierBasisPoints);

        public static OfflineRewardRateEntry Create(
            int stage,
            long goldRate,
            long experienceRate,
            int stoneInterval)
        {
            return new OfflineRewardRateEntry
            {
                minimumStage = Math.Max(1, stage),
                goldPerMinute = Math.Max(0L, goldRate),
                commanderExperiencePerMinute = Math.Max(0L, experienceRate),
                upgradeStoneIntervalSeconds = Math.Max(1, stoneInterval)
            };
        }

        public static OfflineRewardRateEntry CreateScaled(int stage, int multiplierBasisPoints)
        {
            return new OfflineRewardRateEntry
            {
                minimumStage = Math.Max(1, stage),
                rewardMultiplierBasisPoints = Math.Max(1, multiplierBasisPoints)
            };
        }
    }

    [CreateAssetMenu(menuName = "ProjectMT/Offline Reward/Config", fileName = "OfflineRewardConfig")]
    public sealed class OfflineRewardConfig : ScriptableObject // 방치 시간·단계별 임시 밸런스 원본
    {
        [SerializeField, Min(1)] private int balanceVersion = 1;
        [SerializeField, Min(60)] private int minimumOfflineSeconds = 60;
        [SerializeField, Min(60)] private int maximumAccumulationSeconds = 43200;
        [SerializeField, Min(1)] private long baseGoldPerMinute = 5L;
        [SerializeField, Min(1)] private long baseCommanderExperiencePerMinute = 1L;
        [SerializeField, Min(1)] private long baseUpgradeStonePerMinute = 1L;
        [SerializeField, Range(0, 10000)] private int baseEquipmentChanceBasisPointsPerMinute = 100;
        [SerializeField] private OfflineRewardRateEntry[] rates = Array.Empty<OfflineRewardRateEntry>();

        public int BalanceVersion => Math.Max(1, balanceVersion);
        public int MinimumOfflineSeconds => Math.Max(60, minimumOfflineSeconds);
        public int MaximumAccumulationSeconds => Math.Max(MinimumOfflineSeconds, maximumAccumulationSeconds);
        public bool UsesScaledRewards => BalanceVersion >= 2;
        public long BaseGoldPerMinute => Math.Max(1L, baseGoldPerMinute);
        public long BaseCommanderExperiencePerMinute => Math.Max(1L, baseCommanderExperiencePerMinute);
        public long BaseUpgradeStonePerMinute => Math.Max(1L, baseUpgradeStonePerMinute);
        public int BaseEquipmentChanceBasisPointsPerMinute =>
            Math.Clamp(baseEquipmentChanceBasisPointsPerMinute, 0, 10000);

        public bool TryResolveRate(int stage, out OfflineRewardRateEntry rate)
        {
            rate = null;
            var normalizedStage = Math.Max(1, stage);
            if (rates == null)
            {
                return false;
            }

            for (var index = 0; index < rates.Length; index++)
            {
                var candidate = rates[index];
                if (candidate == null || candidate.MinimumStage > normalizedStage)
                {
                    break;
                }

                rate = candidate;
            }

            return rate != null;
        }

        public bool TryValidate(out string error)
        {
            if (minimumOfflineSeconds < 60 || maximumAccumulationSeconds < minimumOfflineSeconds)
            {
                error = "Offline reward time range is invalid.";
                return false;
            }

            if (UsesScaledRewards &&
                (baseGoldPerMinute <= 0L || baseCommanderExperiencePerMinute <= 0L ||
                 baseUpgradeStonePerMinute <= 0L ||
                 baseEquipmentChanceBasisPointsPerMinute < 0 ||
                 baseEquipmentChanceBasisPointsPerMinute > 10000))
            {
                error = "Scaled offline reward base rates are invalid.";
                return false;
            }

            if (rates == null || rates.Length == 0 || rates[0] == null || rates[0].MinimumStage != 1)
            {
                error = "Offline reward rates must begin at stage 1.";
                return false;
            }

            var previousStage = 0;
            for (var index = 0; index < rates.Length; index++)
            {
                var rate = rates[index];
                if (rate == null || rate.MinimumStage <= previousStage ||
                    (rate.GoldPerMinute <= 0L && rate.CommanderExperiencePerMinute <= 0L) ||
                    rate.UpgradeStoneIntervalSeconds <= 0 ||
                    (UsesScaledRewards && rate.RewardMultiplierBasisPoints <= 0))
                {
                    error = $"Offline reward rate is invalid. Index={index}";
                    return false;
                }

                previousStage = rate.MinimumStage;
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            int version,
            int minimumSeconds,
            int maximumSeconds,
            params OfflineRewardRateEntry[] stageRates)
        {
            balanceVersion = Math.Max(1, version);
            minimumOfflineSeconds = Math.Max(60, minimumSeconds);
            maximumAccumulationSeconds = Math.Max(minimumOfflineSeconds, maximumSeconds);
            rates = stageRates ?? Array.Empty<OfflineRewardRateEntry>();
        }

        public void EditorConfigureScaled(
            int version,
            int minimumSeconds,
            int maximumSeconds,
            long goldPerMinute,
            long experiencePerMinute,
            long stonePerMinute,
            int equipmentChanceBasisPoints,
            params OfflineRewardRateEntry[] stageRates)
        {
            balanceVersion = Math.Max(2, version);
            minimumOfflineSeconds = Math.Max(60, minimumSeconds);
            maximumAccumulationSeconds = Math.Max(minimumOfflineSeconds, maximumSeconds);
            baseGoldPerMinute = Math.Max(1L, goldPerMinute);
            baseCommanderExperiencePerMinute = Math.Max(1L, experiencePerMinute);
            baseUpgradeStonePerMinute = Math.Max(1L, stonePerMinute);
            baseEquipmentChanceBasisPointsPerMinute = Math.Clamp(equipmentChanceBasisPoints, 0, 10000);
            rates = stageRates ?? Array.Empty<OfflineRewardRateEntry>();
        }
#endif
    }
}
