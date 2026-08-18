using System;
using System.Collections.Generic;
using ProjectMT.Shared.Items;
using UnityEngine;

namespace ProjectMT.Shared.GameData
{
    public static class GrowthDungeonStageRules // 성장 던전 공통 단계 규칙
    {
        public const float DifficultyStatGrowthPerStage = 0.05f; // 1단계 기준으로 단계당 전투 핵심 수치 +5%p

        public static bool IsValidStage(int stage)
        {
            return stage >= 1;
        }

        public static int ResolveNextChallengeStage(int highestClearedStage)
        {
            var highest = Math.Max(0, highestClearedStage);
            return highest < int.MaxValue ? highest + 1 : int.MaxValue;
        }

        public static float ResolveDifficultyMultiplier(int stage)
        {
            var difficultyLevel = stage <= 1 ? 0 : stage - 1;
            return 1f + DifficultyStatGrowthPerStage * difficultyLevel;
        }
    }

    public static class GrowthDungeonProgressIds // 저장에서 사용하는 콘텐츠 고정 ID
    {
        public const string FoodRiot = "food_riot";
        public const string TreasureSpirit = "treasure_spirit";
        public const string GiantSpellbook = "giant_spellbook";
        public const string GuardiansTower = "guardians_tower";
    }

    [Serializable]
    public sealed class GrowthDungeonProgressEntryData // 콘텐츠별 최고 클리어 단계
    {
        [SerializeField] private string contentId;
        [SerializeField, Min(0)] private int highestClearedStage;

        public string ContentId => contentId?.Trim() ?? string.Empty;
        public int HighestClearedStage => Math.Max(0, highestClearedStage);

        internal GrowthDungeonProgressEntryData()
        {
        }

        internal GrowthDungeonProgressEntryData(string id, int stage)
        {
            contentId = id?.Trim();
            highestClearedStage = Math.Max(0, stage);
        }

        internal GrowthDungeonProgressEntryData Clone()
        {
            return new GrowthDungeonProgressEntryData
            {
                contentId = ContentId,
                highestClearedStage = HighestClearedStage
            };
        }

        internal void RecordClear(int stage)
        {
            highestClearedStage = Math.Max(HighestClearedStage, stage);
        }
    }

    [Serializable]
    public sealed class GrowthDungeonProgressData // 성장 던전 단계·일일 열쇠 기준일
    {
        [SerializeField] private long lastDailyKeyPeriod = -1L;
        [SerializeField] private List<GrowthDungeonProgressEntryData> entries = new List<GrowthDungeonProgressEntryData>();

        public long LastDailyKeyPeriod => Math.Max(-1L, lastDailyKeyPeriod);

        public static GrowthDungeonProgressData CreateDefault()
        {
            return new GrowthDungeonProgressData();
        }

        public GrowthDungeonProgressData Clone()
        {
            var clone = new GrowthDungeonProgressData
            {
                lastDailyKeyPeriod = LastDailyKeyPeriod,
                entries = new List<GrowthDungeonProgressEntryData>()
            };
            if (entries != null)
            {
                for (var index = 0; index < entries.Count; index++)
                {
                    if (entries[index] != null)
                    {
                        clone.entries.Add(entries[index].Clone());
                    }
                }
            }

            return clone;
        }

        public int GetHighestClearedStage(string contentId)
        {
            var canonicalId = contentId?.Trim();
            if (string.IsNullOrEmpty(canonicalId) || entries == null)
            {
                return 0;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry != null && string.Equals(entry.ContentId, canonicalId, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.HighestClearedStage;
                }
            }

            return 0;
        }

        internal bool RecordClear(string contentId, int stage)
        {
            var canonicalId = contentId?.Trim();
            if (string.IsNullOrEmpty(canonicalId) || !GrowthDungeonStageRules.IsValidStage(stage))
            {
                return false;
            }

            entries ??= new List<GrowthDungeonProgressEntryData>();
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry != null && string.Equals(entry.ContentId, canonicalId, StringComparison.OrdinalIgnoreCase))
                {
                    entry.RecordClear(stage);
                    return true;
                }
            }

            entries.Add(new GrowthDungeonProgressEntryData(canonicalId, stage));
            return true;
        }

        internal bool TryAdvanceDailyKeyPeriod(long expectedPeriod, long nextPeriod)
        {
            if (LastDailyKeyPeriod != expectedPeriod || nextPeriod <= LastDailyKeyPeriod)
            {
                return false;
            }

            lastDailyKeyPeriod = nextPeriod;
            return true;
        }

        internal void Repair()
        {
            lastDailyKeyPeriod = Math.Max(-1L, lastDailyKeyPeriod);
            entries ??= new List<GrowthDungeonProgressEntryData>();
            for (var index = entries.Count - 1; index >= 0; index--)
            {
                var entry = entries[index];
                if (entry == null || string.IsNullOrEmpty(entry.ContentId) || entry.HighestClearedStage <= 0)
                {
                    entries.RemoveAt(index);
                    continue;
                }

                for (var earlier = 0; earlier < index; earlier++)
                {
                    if (string.Equals(entries[earlier]?.ContentId, entry.ContentId, StringComparison.OrdinalIgnoreCase))
                    {
                        entries[earlier]?.RecordClear(entry.HighestClearedStage);
                        entries.RemoveAt(index);
                        break;
                    }
                }
            }
        }

        internal GrowthDungeonProgressView CreateView()
        {
            return new GrowthDungeonProgressView(this, entries);
        }
    }

    public readonly struct GrowthDungeonProgressEntryView
    {
        public GrowthDungeonProgressEntryView(string contentId, int highestClearedStage)
        {
            ContentId = contentId ?? string.Empty;
            HighestClearedStage = Math.Max(0, highestClearedStage);
        }

        public string ContentId { get; }
        public int HighestClearedStage { get; }
    }

    public readonly struct GrowthDungeonProgressView // UI·입장 검증용 읽기 전용 값
    {
        private readonly GrowthDungeonProgressEntryView[] entries;

        internal GrowthDungeonProgressView(
            GrowthDungeonProgressData data,
            IReadOnlyList<GrowthDungeonProgressEntryData> source)
        {
            LastDailyKeyPeriod = data?.LastDailyKeyPeriod ?? -1L;
            if (source == null || source.Count == 0)
            {
                entries = Array.Empty<GrowthDungeonProgressEntryView>();
                return;
            }

            entries = new GrowthDungeonProgressEntryView[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                entries[index] = new GrowthDungeonProgressEntryView(
                    source[index]?.ContentId,
                    source[index]?.HighestClearedStage ?? 0);
            }
        }

        public long LastDailyKeyPeriod { get; }
        public IReadOnlyList<GrowthDungeonProgressEntryView> Entries =>
            entries ?? Array.Empty<GrowthDungeonProgressEntryView>();

        public int GetHighestClearedStage(string contentId)
        {
            var canonicalId = contentId?.Trim();
            if (string.IsNullOrEmpty(canonicalId) || entries == null)
            {
                return 0;
            }

            for (var index = 0; index < entries.Length; index++)
            {
                if (string.Equals(entries[index].ContentId, canonicalId, StringComparison.OrdinalIgnoreCase))
                {
                    return entries[index].HighestClearedStage;
                }
            }

            return 0;
        }
    }

    public static class GrowthDungeonDailyKeyRules // KST 05:00 일일 충전 규칙
    {
        public const int RechargeAmount = 3;
        public const int MaximumQuantity = 3;
        public const int ResetHourKst = 5;

        private static readonly string[] keyItemIds =
        {
            ItemIds.FoodRiotKey,
            ItemIds.TreasureSpiritKey,
            ItemIds.GiantSpellbookKey,
            ItemIds.GuardiansTowerKey
        };

        public static IReadOnlyList<string> KeyItemIds => keyItemIds;

        public static long GetPeriodId(DateTime utcNow)
        {
            var utc = utcNow.Kind == DateTimeKind.Utc ? utcNow : utcNow.ToUniversalTime();
            var resetAdjusted = utc.AddHours(9 - ResetHourKst); // KST 날짜에서 05:00을 경계로 이동
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(resetAdjusted.Date - epoch.Date).TotalDays;
        }

        public static long GetRechargedQuantity(long currentQuantity)
        {
            var current = Math.Max(0L, currentQuantity);
            return Math.Min(MaximumQuantity, current + RechargeAmount);
        }
    }
}
