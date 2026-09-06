using System;
using System.Collections.Generic;
using ProjectMT.Shared.Items;
using UnityEngine;

namespace ProjectMT.Shared.CommanderSkill
{
    public readonly struct CommanderSkillSummonPayment // 소환권 우선·부족분 다이아 결제값
    {
        public CommanderSkillSummonPayment(int ticketCost, long diamondCost)
        {
            TicketCost = Mathf.Max(0, ticketCost);
            DiamondCost = Math.Max(0L, diamondCost);
        }

        public int TicketCost { get; }
        public long DiamondCost { get; }

        public bool CanAfford(long diamondBalance)
        {
            return Math.Max(0L, diamondBalance) >= DiamondCost;
        }
    }

    [Serializable]
    public sealed class CommanderSkillSummonPoolEntry // 소환 단계별 스킬 가중치
    {
        [SerializeField] private string skillId;
        [SerializeField, Min(0)] private int weight = 100;

        public string SkillId => skillId?.Trim() ?? string.Empty;
        public int Weight => Mathf.Max(0, weight);

        public CommanderSkillSummonPoolEntry()
        {
        }

        internal CommanderSkillSummonPoolEntry(string id, int summonWeight)
        {
            skillId = id?.Trim() ?? string.Empty;
            weight = Mathf.Max(0, summonWeight);
        }

#if UNITY_EDITOR
        public void EditorConfigure(string id, int summonWeight)
        {
            skillId = id?.Trim() ?? string.Empty;
            weight = Mathf.Max(0, summonWeight);
        }
#endif
    }

    [Serializable]
    public sealed class CommanderSkillSummonLevelRule // 누적 횟수·단계별 전용 풀
    {
        [SerializeField, Min(0)] private int requiredAccumulatedCount;
        [SerializeField] private CommanderSkillSummonPoolEntry[] pool = Array.Empty<CommanderSkillSummonPoolEntry>();

        public int RequiredAccumulatedCount => Mathf.Max(0, requiredAccumulatedCount);
        public IReadOnlyList<CommanderSkillSummonPoolEntry> Pool =>
            pool ?? Array.Empty<CommanderSkillSummonPoolEntry>();

        public CommanderSkillSummonLevelRule()
        {
        }

        internal CommanderSkillSummonLevelRule(
            int requiredCount,
            params CommanderSkillSummonPoolEntry[] entries)
        {
            requiredAccumulatedCount = Mathf.Max(0, requiredCount);
            pool = entries ?? Array.Empty<CommanderSkillSummonPoolEntry>();
        }

#if UNITY_EDITOR
        public void EditorConfigure(int requiredCount, params CommanderSkillSummonPoolEntry[] entries)
        {
            requiredAccumulatedCount = Mathf.Max(0, requiredCount);
            pool = entries ?? Array.Empty<CommanderSkillSummonPoolEntry>();
        }
#endif
    }

    [Serializable]
    public sealed class CommanderSkillSummonOffer // 몬스터 뽑기와 무관한 전용 상품 단위
    {
        [SerializeField, Min(1)] private int drawCount = 10;
        [SerializeField, Min(1)] private int ticketCost = 10;

        public int DrawCount => Mathf.Max(1, drawCount);
        public int TicketCost => Mathf.Max(1, ticketCost);

        public CommanderSkillSummonOffer()
        {
        }

        internal CommanderSkillSummonOffer(int count, int cost)
        {
            drawCount = Mathf.Max(1, count);
            ticketCost = Mathf.Max(1, cost);
        }

#if UNITY_EDITOR
        public void EditorConfigure(int count, int cost)
        {
            drawCount = Mathf.Max(1, count);
            ticketCost = Mathf.Max(1, cost);
        }
#endif
    }

    [CreateAssetMenu(
        menuName = "ProjectMT/Commander Skill/Summon Config",
        fileName = "CommanderSkillSummonConfig")]
    public sealed class CommanderSkillSummonConfig : ScriptableObject // 전용 소환 풀·단계·상품 SO
    {
        private static CommanderSkillSummonConfig runtimeDefault;

        [SerializeField] private string ticketItemId = ItemIds.CommanderSkillSummonTicket;
        [SerializeField, Min(1)] private int diamondCostPerMissingTicket = 30;
        [SerializeField] private CommanderSkillSummonLevelRule[] levels =
            Array.Empty<CommanderSkillSummonLevelRule>();
        [SerializeField] private CommanderSkillSummonOffer[] offers =
            Array.Empty<CommanderSkillSummonOffer>();

        public static CommanderSkillSummonConfig RuntimeDefault
        {
            get
            {
                if (runtimeDefault != null)
                {
                    return runtimeDefault;
                }

                runtimeDefault = CreateInstance<CommanderSkillSummonConfig>();
                runtimeDefault.hideFlags = HideFlags.HideAndDontSave;
                runtimeDefault.ticketItemId = ItemIds.CommanderSkillSummonTicket;
                runtimeDefault.diamondCostPerMissingTicket = 30;
                runtimeDefault.levels = CreateDefaultLevels();
                runtimeDefault.offers = CreateDefaultOffers();
                return runtimeDefault;
            }
        }

        public string TicketItemId => string.IsNullOrWhiteSpace(ticketItemId)
            ? ItemIds.CommanderSkillSummonTicket
            : ticketItemId.Trim();
        public int DiamondCostPerMissingTicket => Mathf.Max(1, diamondCostPerMissingTicket);
        public IReadOnlyList<CommanderSkillSummonLevelRule> Levels =>
            levels ?? Array.Empty<CommanderSkillSummonLevelRule>();
        public IReadOnlyList<CommanderSkillSummonOffer> Offers =>
            offers ?? Array.Empty<CommanderSkillSummonOffer>();
        public int MaxSummonLevel => Mathf.Max(1, Levels.Count);

        public int GetSummonLevel(int accumulatedCount)
        {
            var count = Mathf.Max(0, accumulatedCount);
            var level = 1;
            for (var index = 1; index < Levels.Count; index++)
            {
                var rule = Levels[index];
                if (rule == null || count < rule.RequiredAccumulatedCount)
                {
                    break;
                }

                level = index + 1;
            }

            return level;
        }

        public int GetLevelStartCount(int summonLevel)
        {
            var index = Mathf.Clamp(summonLevel - 1, 0, Mathf.Max(0, Levels.Count - 1));
            return Levels.Count == 0 || Levels[index] == null
                ? 0
                : Levels[index].RequiredAccumulatedCount;
        }

        public bool TryGetNextLevelThreshold(int summonLevel, out int threshold)
        {
            var nextIndex = summonLevel;
            if (nextIndex < 0 || nextIndex >= Levels.Count || Levels[nextIndex] == null)
            {
                threshold = 0;
                return false;
            }

            threshold = Levels[nextIndex].RequiredAccumulatedCount;
            return true;
        }

        public bool TryGetOffer(int drawCount, out CommanderSkillSummonOffer offer)
        {
            for (var index = 0; index < Offers.Count; index++)
            {
                var candidate = Offers[index];
                if (candidate != null && candidate.DrawCount == drawCount)
                {
                    offer = candidate;
                    return true;
                }
            }

            offer = null;
            return false;
        }

        public CommanderSkillSummonPayment CalculatePayment(
            CommanderSkillSummonOffer offer,
            long availableTickets)
        {
            if (offer == null)
            {
                return default;
            }

            var ticketCost = (int)Math.Min(Math.Max(0L, availableTickets), offer.TicketCost);
            var missingTickets = offer.TicketCost - ticketCost;
            return new CommanderSkillSummonPayment(
                ticketCost,
                missingTickets * (long)DiamondCostPerMissingTicket);
        }

        public bool IsSkillAvailable(string skillId, int summonLevel)
        {
            var entries = GetPool(summonLevel);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry != null && entry.Weight > 0 &&
                    string.Equals(entry.SkillId, skillId?.Trim(), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public int GetTotalWeight(int summonLevel)
        {
            var total = 0L;
            var entries = GetPool(summonLevel);
            for (var index = 0; index < entries.Count; index++)
            {
                total += entries[index]?.Weight ?? 0;
            }

            return total <= 0L || total > int.MaxValue ? 0 : (int)total;
        }

        public IReadOnlyList<CommanderSkillSummonPoolEntry> GetPool(int summonLevel)
        {
            if (Levels.Count == 0)
            {
                return Array.Empty<CommanderSkillSummonPoolEntry>();
            }

            var index = Mathf.Clamp(summonLevel - 1, 0, Levels.Count - 1);
            return Levels[index]?.Pool ?? Array.Empty<CommanderSkillSummonPoolEntry>();
        }

        public string RollSkillId(System.Random random, int summonLevel)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            var totalWeight = GetTotalWeight(summonLevel);
            if (totalWeight <= 0)
            {
                return string.Empty;
            }

            var roll = random.Next(0, totalWeight);
            var entries = GetPool(summonLevel);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var weight = entry?.Weight ?? 0;
                if (roll < weight)
                {
                    return entry.SkillId;
                }

                roll -= weight;
            }

            return string.Empty;
        }

        public bool TryValidate(CommanderSkillBalanceConfig growthConfig, out string error)
        {
            var growth = growthConfig ?? CommanderSkillBalanceConfig.RuntimeDefault;
            if (string.IsNullOrWhiteSpace(TicketItemId))
            {
                error = "Commander skill summon ticket id is empty.";
                return false;
            }

            if (diamondCostPerMissingTicket <= 0)
            {
                error = "Commander skill summon diamond fallback cost must be positive.";
                return false;
            }

            if (levels == null || levels.Length == 0 || levels[0] == null ||
                levels[0].RequiredAccumulatedCount != 0)
            {
                error = "Commander skill summon levels must start at count zero.";
                return false;
            }

            var previousThreshold = -1;
            for (var levelIndex = 0; levelIndex < levels.Length; levelIndex++)
            {
                var level = levels[levelIndex];
                if (level == null || level.RequiredAccumulatedCount <= previousThreshold)
                {
                    error = $"Commander skill summon level {levelIndex + 1} threshold is invalid.";
                    return false;
                }

                previousThreshold = level.RequiredAccumulatedCount;
                if (level.Pool == null || level.Pool.Count == 0)
                {
                    error = $"Commander skill summon level {levelIndex + 1} pool is empty.";
                    return false;
                }

                var ids = new HashSet<string>(StringComparer.Ordinal);
                var totalWeight = 0L;
                for (var entryIndex = 0; entryIndex < level.Pool.Count; entryIndex++)
                {
                    var entry = level.Pool[entryIndex];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.SkillId) || entry.Weight <= 0 ||
                        !ids.Add(entry.SkillId) || !growth.TryGetRule(entry.SkillId, out _))
                    {
                        error = $"Commander skill summon level {levelIndex + 1} pool entry {entryIndex} is invalid.";
                        return false;
                    }

                    totalWeight += entry.Weight;
                }

                if (totalWeight <= 0L || totalWeight > int.MaxValue)
                {
                    error = $"Commander skill summon level {levelIndex + 1} total weight is invalid.";
                    return false;
                }
            }

            if (offers == null || offers.Length == 0)
            {
                error = "At least one commander skill summon offer is required.";
                return false;
            }

            var drawCounts = new HashSet<int>();
            for (var index = 0; index < offers.Length; index++)
            {
                var offer = offers[index];
                if (offer == null || offer.DrawCount <= 0 || offer.TicketCost <= 0 ||
                    !drawCounts.Add(offer.DrawCount))
                {
                    error = $"Commander skill summon offer {index} is invalid.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static CommanderSkillSummonLevelRule[] CreateDefaultLevels()
        {
            return new[]
            {
                CreateLevel(0),
                CreateLevel(30),
                CreateLevel(100)
            };
        }

        private static CommanderSkillSummonLevelRule CreateLevel(int threshold)
        {
            return new CommanderSkillSummonLevelRule(
                threshold,
                new CommanderSkillSummonPoolEntry(CommanderSkillIds.Starter, 100));
        }

        private static CommanderSkillSummonOffer[] CreateDefaultOffers()
        {
            return new[]
            {
                new CommanderSkillSummonOffer(1, 1),
                new CommanderSkillSummonOffer(10, 10),
                new CommanderSkillSummonOffer(30, 30)
            };
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string itemId,
            CommanderSkillSummonLevelRule[] levelRules,
            CommanderSkillSummonOffer[] summonOffers,
            int diamondFallbackCost = 30)
        {
            ticketItemId = itemId?.Trim() ?? string.Empty;
            diamondCostPerMissingTicket = Mathf.Max(1, diamondFallbackCost);
            levels = levelRules ?? Array.Empty<CommanderSkillSummonLevelRule>();
            offers = summonOffers ?? Array.Empty<CommanderSkillSummonOffer>();
        }
#endif
    }
}
