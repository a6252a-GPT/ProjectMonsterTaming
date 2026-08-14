using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;
using UnityEngine;

namespace ProjectMT.Shared.GameData
{
    [Serializable]
    public sealed class OfflineRewardReceiptData // 지급 완료 뒤 확인 대기 중인 방치 정산 영수증
    {
        [SerializeField] private string receiptId;
        [SerializeField] private string settledFromUtc;
        [SerializeField] private string settledToUtc;
        [SerializeField] private long elapsedSeconds;
        [SerializeField] private int basisStage = 1;
        [SerializeField] private long gold;
        [SerializeField] private long commanderExperience;
        [SerializeField] private long upgradeStone;
        [SerializeField] private long equipmentSlotUpgradeStone;
        [SerializeField] private long commanderSkillUpgradeStone;
        [SerializeField] private long legionPotentialUpgradeStone;
        [SerializeField] private long goldPerMinute;
        [SerializeField] private long commanderExperiencePerMinute;
        [SerializeField] private int upgradeStoneIntervalSeconds;
        [SerializeField] private int rewardMultiplierBasisPoints = 10000;
        [SerializeField] private int equipmentChanceBasisPointsPerMinute;
        [SerializeField] private List<EquipmentInstanceData> equipmentRewards = new List<EquipmentInstanceData>();
        [SerializeField] private List<string> existingAutoDismantleInstanceIds = new List<string>();
        [SerializeField] private int rolledEquipmentCount;
        [SerializeField] private int autoDismantledEquipmentCount;
        [SerializeField] private long autoDismantleUpgradeStone;
        [SerializeField] private long existingAutoDismantleUpgradeStone;
        [SerializeField] private bool capped;
        [SerializeField] private int balanceVersion = 1;

        public string ReceiptId => receiptId ?? string.Empty;
        public string SettledFromUtc => settledFromUtc ?? string.Empty;
        public string SettledToUtc => settledToUtc ?? string.Empty;
        public long ElapsedSeconds => Math.Max(0L, elapsedSeconds);
        public int BasisStage => Math.Max(1, basisStage);
        public long Gold => Math.Max(0L, gold);
        public long CommanderExperience => Math.Max(0L, commanderExperience);
        public long UpgradeStone => Math.Max(0L, upgradeStone);
        public long EquipmentSlotUpgradeStone => BalanceVersion >= 2
            ? Math.Max(0L, equipmentSlotUpgradeStone)
            : UpgradeStone;
        public long CommanderSkillUpgradeStone => Math.Max(0L, commanderSkillUpgradeStone);
        public long LegionPotentialUpgradeStone => Math.Max(0L, legionPotentialUpgradeStone);
        public long TotalEquipmentSlotUpgradeStone =>
            EquipmentSlotUpgradeStone <= long.MaxValue - AutoDismantleUpgradeStone
                ? EquipmentSlotUpgradeStone + AutoDismantleUpgradeStone
                : long.MaxValue;
        public long GoldPerMinute => Math.Max(0L, goldPerMinute);
        public long CommanderExperiencePerMinute => Math.Max(0L, commanderExperiencePerMinute);
        public int UpgradeStoneIntervalSeconds => Math.Max(1, upgradeStoneIntervalSeconds);
        public int RewardMultiplierBasisPoints => Math.Max(1, rewardMultiplierBasisPoints);
        public int EquipmentChanceBasisPointsPerMinute => Math.Max(0, equipmentChanceBasisPointsPerMinute);
        public IReadOnlyList<EquipmentInstanceData> EquipmentRewards =>
            equipmentRewards ?? (IReadOnlyList<EquipmentInstanceData>)Array.Empty<EquipmentInstanceData>();
        public IReadOnlyList<string> ExistingAutoDismantleInstanceIds =>
            existingAutoDismantleInstanceIds ?? (IReadOnlyList<string>)Array.Empty<string>();
        public int RolledEquipmentCount => Math.Max(0, rolledEquipmentCount);
        public int AutoDismantledEquipmentCount => Math.Max(0, autoDismantledEquipmentCount);
        public long AutoDismantleUpgradeStone => Math.Max(0L, autoDismantleUpgradeStone);
        public long ExistingAutoDismantleUpgradeStone => Math.Max(0L, existingAutoDismantleUpgradeStone);
        public bool Capped => capped;
        public int BalanceVersion => Math.Max(1, balanceVersion);

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(ReceiptId) &&
            TryParseUtc(SettledFromUtc, out var fromUtc) &&
            TryParseUtc(SettledToUtc, out var toUtc) &&
            toUtc > fromUtc &&
            ElapsedSeconds > 0L &&
            HasValidRewardBreakdown() &&
            (Gold > 0L || CommanderExperience > 0L || UpgradeStone > 0L ||
             AutoDismantleUpgradeStone > 0L || EquipmentRewards.Count > 0);

        public static OfflineRewardReceiptData Create(
            string id,
            DateTime fromUtc,
            DateTime toUtc,
            long rewardedSeconds,
            int stage,
            long goldAmount,
            long experienceAmount,
            long stoneAmount,
            long goldRate,
            long experienceRate,
            int stoneInterval,
            bool wasCapped,
            int configVersion)
        {
            return new OfflineRewardReceiptData
            {
                receiptId = id?.Trim(),
                settledFromUtc = NormalizeUtc(fromUtc),
                settledToUtc = NormalizeUtc(toUtc),
                elapsedSeconds = Math.Max(0L, rewardedSeconds),
                basisStage = Math.Max(1, stage),
                gold = Math.Max(0L, goldAmount),
                commanderExperience = Math.Max(0L, experienceAmount),
                upgradeStone = Math.Max(0L, stoneAmount),
                goldPerMinute = Math.Max(0L, goldRate),
                commanderExperiencePerMinute = Math.Max(0L, experienceRate),
                upgradeStoneIntervalSeconds = Math.Max(1, stoneInterval),
                capped = wasCapped,
                balanceVersion = Math.Max(1, configVersion)
            };
        }

        public static OfflineRewardReceiptData Create(
            string id,
            DateTime fromUtc,
            DateTime toUtc,
            long rewardedSeconds,
            int stage,
            long goldAmount,
            long experienceAmount,
            long equipmentSlotStoneAmount,
            long commanderSkillStoneAmount,
            long legionPotentialStoneAmount,
            long goldRate,
            long experienceRate,
            int multiplierBasisPoints,
            int equipmentChanceBasisPoints,
            IReadOnlyList<EquipmentInstanceData> keptEquipment,
            IReadOnlyList<string> existingDismantleIds,
            int equipmentRollCount,
            int dismantledEquipmentCount,
            long dismantleStoneAmount,
            long existingDismantleStoneAmount,
            bool wasCapped,
            int configVersion)
        {
            var totalRandomStones = TryAdd(
                equipmentSlotStoneAmount,
                commanderSkillStoneAmount,
                legionPotentialStoneAmount,
                out var summedStones)
                ? summedStones
                : 0L;
            var receipt = new OfflineRewardReceiptData
            {
                receiptId = id?.Trim(),
                settledFromUtc = NormalizeUtc(fromUtc),
                settledToUtc = NormalizeUtc(toUtc),
                elapsedSeconds = Math.Max(0L, rewardedSeconds),
                basisStage = Math.Max(1, stage),
                gold = Math.Max(0L, goldAmount),
                commanderExperience = Math.Max(0L, experienceAmount),
                upgradeStone = totalRandomStones,
                equipmentSlotUpgradeStone = Math.Max(0L, equipmentSlotStoneAmount),
                commanderSkillUpgradeStone = Math.Max(0L, commanderSkillStoneAmount),
                legionPotentialUpgradeStone = Math.Max(0L, legionPotentialStoneAmount),
                goldPerMinute = Math.Max(0L, goldRate),
                commanderExperiencePerMinute = Math.Max(0L, experienceRate),
                upgradeStoneIntervalSeconds = 60,
                rewardMultiplierBasisPoints = Math.Max(1, multiplierBasisPoints),
                equipmentChanceBasisPointsPerMinute = Math.Max(0, equipmentChanceBasisPoints),
                equipmentRewards = CloneEquipment(keptEquipment),
                existingAutoDismantleInstanceIds = CloneIds(existingDismantleIds),
                rolledEquipmentCount = Math.Max(0, equipmentRollCount),
                autoDismantledEquipmentCount = Math.Max(0, dismantledEquipmentCount),
                autoDismantleUpgradeStone = Math.Max(0L, dismantleStoneAmount),
                existingAutoDismantleUpgradeStone = Math.Max(0L, existingDismantleStoneAmount),
                capped = wasCapped,
                balanceVersion = Math.Max(2, configVersion)
            };
            return receipt;
        }

        public OfflineRewardReceiptData Clone()
        {
            return new OfflineRewardReceiptData
            {
                receiptId = receiptId,
                settledFromUtc = settledFromUtc,
                settledToUtc = settledToUtc,
                elapsedSeconds = elapsedSeconds,
                basisStage = basisStage,
                gold = gold,
                commanderExperience = commanderExperience,
                upgradeStone = upgradeStone,
                equipmentSlotUpgradeStone = equipmentSlotUpgradeStone,
                commanderSkillUpgradeStone = commanderSkillUpgradeStone,
                legionPotentialUpgradeStone = legionPotentialUpgradeStone,
                goldPerMinute = goldPerMinute,
                commanderExperiencePerMinute = commanderExperiencePerMinute,
                upgradeStoneIntervalSeconds = upgradeStoneIntervalSeconds,
                rewardMultiplierBasisPoints = rewardMultiplierBasisPoints,
                equipmentChanceBasisPointsPerMinute = equipmentChanceBasisPointsPerMinute,
                equipmentRewards = CloneEquipment(equipmentRewards),
                existingAutoDismantleInstanceIds = CloneIds(existingAutoDismantleInstanceIds),
                rolledEquipmentCount = rolledEquipmentCount,
                autoDismantledEquipmentCount = autoDismantledEquipmentCount,
                autoDismantleUpgradeStone = autoDismantleUpgradeStone,
                existingAutoDismantleUpgradeStone = existingAutoDismantleUpgradeStone,
                capped = capped,
                balanceVersion = balanceVersion
            };
        }

        private bool HasValidRewardBreakdown()
        {
            if (BalanceVersion < 2)
            {
                return true;
            }

            if (!TryAdd(
                    EquipmentSlotUpgradeStone,
                    CommanderSkillUpgradeStone,
                    LegionPotentialUpgradeStone,
                    out var randomStoneTotal) ||
                randomStoneTotal != UpgradeStone ||
                ExistingAutoDismantleUpgradeStone > AutoDismantleUpgradeStone ||
                AutoDismantledEquipmentCount > RolledEquipmentCount + ExistingAutoDismantleInstanceIds.Count ||
                EquipmentRewards.Count + AutoDismantledEquipmentCount != RolledEquipmentCount + ExistingAutoDismantleInstanceIds.Count)
            {
                return false;
            }

            var knownEquipmentIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < EquipmentRewards.Count; index++)
            {
                var equipment = EquipmentRewards[index]?.Clone();
                if (equipment == null || !equipment.IsValidForAcquire() ||
                    !knownEquipmentIds.Add(equipment.InstanceId))
                {
                    return false;
                }
            }

            var knownExistingIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < ExistingAutoDismantleInstanceIds.Count; index++)
            {
                var id = ExistingAutoDismantleInstanceIds[index]?.Trim();
                if (string.IsNullOrEmpty(id) || !knownExistingIds.Add(id))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryAdd(long first, long second, long third, out long result)
        {
            result = 0L;
            if (first < 0L || second < 0L || third < 0L ||
                first > long.MaxValue - second)
            {
                return false;
            }

            var firstTwo = first + second;
            if (firstTwo > long.MaxValue - third)
            {
                return false;
            }

            result = firstTwo + third;
            return true;
        }

        private static List<EquipmentInstanceData> CloneEquipment(IReadOnlyList<EquipmentInstanceData> source)
        {
            var result = new List<EquipmentInstanceData>(source?.Count ?? 0);
            if (source == null)
            {
                return result;
            }

            for (var index = 0; index < source.Count; index++)
            {
                if (source[index] != null)
                {
                    result.Add(source[index].Clone());
                }
            }

            return result;
        }

        private static List<string> CloneIds(IReadOnlyList<string> source)
        {
            var result = new List<string>(source?.Count ?? 0);
            if (source == null)
            {
                return result;
            }

            for (var index = 0; index < source.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(source[index]))
                {
                    result.Add(source[index].Trim());
                }
            }

            return result;
        }

        private static string NormalizeUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("O");
        }

        public static bool TryParseUtc(string value, out DateTime utc)
        {
            if (DateTime.TryParse(
                    value,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var parsed))
            {
                utc = parsed.ToUniversalTime();
                return true;
            }

            utc = default;
            return false;
        }
    }

    [Serializable]
    public sealed class OfflineRewardProgressData // 방치 시작점과 미확인 정산 결과 저장
    {
        [SerializeField] private string lastActiveUtc;
        [SerializeField] private int lastActiveStage = 1;
        [SerializeField] private List<OfflineRewardReceiptData> pendingReceipts = new List<OfflineRewardReceiptData>();

        public string LastActiveUtc => lastActiveUtc ?? string.Empty;
        public int LastActiveStage => Math.Max(1, lastActiveStage);
        public IReadOnlyList<OfflineRewardReceiptData> PendingReceipts =>
            pendingReceipts ?? (IReadOnlyList<OfflineRewardReceiptData>)Array.Empty<OfflineRewardReceiptData>();

        public static OfflineRewardProgressData CreateDefault()
        {
            return new OfflineRewardProgressData();
        }

        public OfflineRewardProgressData Clone()
        {
            var clone = new OfflineRewardProgressData
            {
                lastActiveUtc = lastActiveUtc,
                lastActiveStage = lastActiveStage,
                pendingReceipts = new List<OfflineRewardReceiptData>(pendingReceipts?.Count ?? 0)
            };
            if (pendingReceipts != null)
            {
                for (var index = 0; index < pendingReceipts.Count; index++)
                {
                    if (pendingReceipts[index] != null)
                    {
                        clone.pendingReceipts.Add(pendingReceipts[index].Clone());
                    }
                }
            }

            return clone;
        }

        internal void Repair()
        {
            lastActiveStage = Math.Max(1, lastActiveStage);
            if (!OfflineRewardReceiptData.TryParseUtc(lastActiveUtc, out _))
            {
                lastActiveUtc = string.Empty;
            }

            pendingReceipts ??= new List<OfflineRewardReceiptData>();
            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = pendingReceipts.Count - 1; index >= 0; index--)
            {
                var receipt = pendingReceipts[index];
                if (receipt == null || !receipt.IsValid || !knownIds.Add(receipt.ReceiptId))
                {
                    pendingReceipts.RemoveAt(index); // 손상·중복 영수증만 제거
                }
            }
        }

        internal bool TryMarkInactive(string expectedLastActiveUtc, DateTime nextUtc, int stage)
        {
            if (!MatchesExpected(expectedLastActiveUtc))
            {
                return false;
            }

            lastActiveUtc = nextUtc.ToUniversalTime().ToString("O");
            lastActiveStage = Math.Max(1, stage);
            return true;
        }

        internal bool TrySettle(
            string expectedLastActiveUtc,
            DateTime nextUtc,
            int nextStage,
            OfflineRewardReceiptData receipt)
        {
            if (!MatchesExpected(expectedLastActiveUtc) || receipt == null || !receipt.IsValid ||
                !OfflineRewardReceiptData.TryParseUtc(expectedLastActiveUtc, out var expectedUtc) ||
                nextUtc.ToUniversalTime() <= expectedUtc)
            {
                return false;
            }

            pendingReceipts ??= new List<OfflineRewardReceiptData>();
            for (var index = 0; index < pendingReceipts.Count; index++)
            {
                if (string.Equals(pendingReceipts[index]?.ReceiptId, receipt.ReceiptId, StringComparison.Ordinal))
                {
                    return false; // 같은 정산 ID 재지급 차단
                }
            }

            pendingReceipts.Add(receipt.Clone());
            lastActiveUtc = nextUtc.ToUniversalTime().ToString("O");
            lastActiveStage = Math.Max(1, nextStage);
            return true;
        }

        internal bool TryAcknowledge(IReadOnlyList<string> receiptIds)
        {
            if (receiptIds == null || receiptIds.Count == 0 || pendingReceipts == null)
            {
                return false;
            }

            var targets = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < receiptIds.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(receiptIds[index]))
                {
                    targets.Add(receiptIds[index].Trim());
                }
            }

            var removed = 0;
            for (var index = pendingReceipts.Count - 1; index >= 0; index--)
            {
                if (pendingReceipts[index] != null && targets.Contains(pendingReceipts[index].ReceiptId))
                {
                    pendingReceipts.RemoveAt(index);
                    removed++;
                }
            }

            return removed == targets.Count && removed > 0;
        }

        private bool MatchesExpected(string expectedLastActiveUtc)
        {
            return string.Equals(
                LastActiveUtc,
                expectedLastActiveUtc?.Trim() ?? string.Empty,
                StringComparison.Ordinal);
        }
    }

    public readonly struct OfflineRewardReceiptView // UI에 전달할 불변 정산 결과
    {
        public OfflineRewardReceiptView(OfflineRewardReceiptData data)
        {
            ReceiptId = data?.ReceiptId ?? string.Empty;
            ElapsedSeconds = data?.ElapsedSeconds ?? 0L;
            BasisStage = data?.BasisStage ?? 1;
            Gold = data?.Gold ?? 0L;
            CommanderExperience = data?.CommanderExperience ?? 0L;
            UpgradeStone = data?.UpgradeStone ?? 0L;
            EquipmentSlotUpgradeStone = data?.EquipmentSlotUpgradeStone ?? 0L;
            CommanderSkillUpgradeStone = data?.CommanderSkillUpgradeStone ?? 0L;
            LegionPotentialUpgradeStone = data?.LegionPotentialUpgradeStone ?? 0L;
            GoldPerMinute = data?.GoldPerMinute ?? 0L;
            CommanderExperiencePerMinute = data?.CommanderExperiencePerMinute ?? 0L;
            UpgradeStoneIntervalSeconds = data?.UpgradeStoneIntervalSeconds ?? 1;
            RewardMultiplierBasisPoints = data?.RewardMultiplierBasisPoints ?? 10000;
            EquipmentChanceBasisPointsPerMinute = data?.EquipmentChanceBasisPointsPerMinute ?? 0;
            EquipmentRewards = CloneEquipment(data?.EquipmentRewards);
            RolledEquipmentCount = data?.RolledEquipmentCount ?? 0;
            AutoDismantledEquipmentCount = data?.AutoDismantledEquipmentCount ?? 0;
            AutoDismantleUpgradeStone = data?.AutoDismantleUpgradeStone ?? 0L;
            Capped = data?.Capped ?? false;
            BalanceVersion = data?.BalanceVersion ?? 1;
        }

        public string ReceiptId { get; }
        public long ElapsedSeconds { get; }
        public int BasisStage { get; }
        public long Gold { get; }
        public long CommanderExperience { get; }
        public long UpgradeStone { get; }
        public long EquipmentSlotUpgradeStone { get; }
        public long CommanderSkillUpgradeStone { get; }
        public long LegionPotentialUpgradeStone { get; }
        public long GoldPerMinute { get; }
        public long CommanderExperiencePerMinute { get; }
        public int UpgradeStoneIntervalSeconds { get; }
        public int RewardMultiplierBasisPoints { get; }
        public int EquipmentChanceBasisPointsPerMinute { get; }
        public IReadOnlyList<EquipmentInstanceData> EquipmentRewards { get; }
        public int RolledEquipmentCount { get; }
        public int AutoDismantledEquipmentCount { get; }
        public long AutoDismantleUpgradeStone { get; }
        public bool Capped { get; }
        public int BalanceVersion { get; }

        private static IReadOnlyList<EquipmentInstanceData> CloneEquipment(
            IReadOnlyList<EquipmentInstanceData> source)
        {
            var result = new EquipmentInstanceData[source?.Count ?? 0];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = source[index]?.Clone();
            }

            return result;
        }
    }

    public readonly struct OfflineRewardProgressView // 외부에는 복사된 시간·영수증만 공개
    {
        private readonly OfflineRewardReceiptView[] pendingReceipts;

        public OfflineRewardProgressView(OfflineRewardProgressData data)
        {
            LastActiveUtc = data?.LastActiveUtc ?? string.Empty;
            LastActiveStage = data?.LastActiveStage ?? 1;
            var source = data?.PendingReceipts;
            pendingReceipts = new OfflineRewardReceiptView[source?.Count ?? 0];
            for (var index = 0; index < pendingReceipts.Length; index++)
            {
                pendingReceipts[index] = new OfflineRewardReceiptView(source[index]);
            }
        }

        public string LastActiveUtc { get; }
        public int LastActiveStage { get; }
        public IReadOnlyList<OfflineRewardReceiptView> PendingReceipts =>
            pendingReceipts ?? Array.Empty<OfflineRewardReceiptView>();
        public bool HasLastActive => OfflineRewardReceiptData.TryParseUtc(LastActiveUtc, out _);
    }
}
