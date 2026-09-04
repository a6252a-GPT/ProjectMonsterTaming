using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.CommanderSkill
{
    public static class CommanderSkillIds // 저장·콘텐츠에서 공유하는 고정 ID
    {
        public const string Fireball = "commander_skill_fireball";
        public const string IceCrystalOrb = "commander_skill_ice_crystal_orb";
    }

    public static class CommanderSkillSlotRules // HUD와 저장이 공유하는 슬롯 규칙
    {
        public const int SlotCount = 6;
        public const int InitialUnlockedCount = 2;
    }

    public static class CommanderSkillLevelRules // 구 호출부용 기본 밸런스 호환 경계
    {
        public const int MaxLevel = 200;
        public const int RequiredDuplicateCount = 1;
        public const float DamagePerLevel = 0.02f;

        public static float GetDamageMultiplier(int level)
        {
            return CommanderSkillBalanceConfig.RuntimeDefault.TryGetRule(
                    CommanderSkillIds.Fireball,
                    out var rule)
                ? rule.GetDamageMultiplier(level)
                : 1f + (Mathf.Clamp(level, 1, MaxLevel) - 1) * DamagePerLevel;
        }
    }

    [Serializable]
    public sealed class OwnedCommanderSkillData
    {
        [SerializeField] private string skillId;
        [SerializeField] private int level = 1;
        [SerializeField] private int awakeningLevel;
        [SerializeField] private int duplicateCount;

        public string SkillId => skillId ?? string.Empty;
        public int Level => level;
        public int AwakeningLevel => awakeningLevel;
        public int DuplicateCount => duplicateCount;

        private OwnedCommanderSkillData()
        {
        }

        internal OwnedCommanderSkillData(string id)
        {
            skillId = id?.Trim() ?? string.Empty;
        }

        internal OwnedCommanderSkillData Clone()
        {
            return new OwnedCommanderSkillData(skillId)
            {
                level = level,
                awakeningLevel = awakeningLevel,
                duplicateCount = duplicateCount
            };
        }

        internal void Repair(CommanderSkillGrowthRule rule = null)
        {
            skillId = skillId?.Trim() ?? string.Empty;
            level = Mathf.Clamp(level, 1, rule?.MaxLevel ?? CommanderSkillLevelRules.MaxLevel);
            awakeningLevel = Mathf.Max(0, awakeningLevel);
            duplicateCount = Mathf.Max(0, duplicateCount);
        }

        internal void AddDuplicate()
        {
            duplicateCount = duplicateCount == int.MaxValue ? int.MaxValue : duplicateCount + 1;
        }

        internal bool TryLevelUp(
            int expectedLevel,
            CommanderSkillGrowthRule rule)
        {
            var levelCap = rule?.MaxLevel ?? CommanderSkillLevelRules.MaxLevel;
            if (level != expectedLevel || level >= levelCap)
            {
                return false;
            }

            level++;
            return true;
        }
    }

    [Serializable]
    public sealed class CommanderSkillProgressData // 군단장 스킬 보유·장착·자동사용 저장
    {
        [SerializeField] private List<OwnedCommanderSkillData> ownedSkills = CreateInitialOwnedSkills();
        [SerializeField] private string[] equippedSkillIds = CreateInitialEquippedSkills();
        [SerializeField] private bool[] unlockedSlots = CreateInitialUnlockedSlots();
        [SerializeField] private bool autoUseEnabled = true;
        [SerializeField] private int summonLevel = 1;
        [SerializeField] private int summonCount;

        internal bool IsAutoUseEnabled => autoUseEnabled;
        internal int SummonLevel => summonLevel;
        internal int SummonCount => summonCount;

        public static CommanderSkillProgressData CreateDefault()
        {
            return new CommanderSkillProgressData();
        }

        public CommanderSkillProgressData Clone(
            CommanderSkillBalanceConfig balanceConfig = null,
            CommanderSkillSummonConfig summonConfig = null)
        {
            var clone = new CommanderSkillProgressData
            {
                ownedSkills = new List<OwnedCommanderSkillData>(),
                equippedSkillIds = CopyEquipped(equippedSkillIds),
                unlockedSlots = CopyUnlocked(unlockedSlots),
                autoUseEnabled = autoUseEnabled,
                summonLevel = summonLevel,
                summonCount = summonCount
            };

            if (ownedSkills != null)
            {
                for (var index = 0; index < ownedSkills.Count; index++)
                {
                    if (ownedSkills[index] != null)
                    {
                        clone.ownedSkills.Add(ownedSkills[index].Clone());
                    }
                }
            }

            clone.Repair(balanceConfig, summonConfig);
            return clone;
        }

        internal bool TrySetAutoUse(bool expectedValue, bool newValue)
        {
            if (autoUseEnabled != expectedValue)
            {
                return false;
            }

            autoUseEnabled = newValue;
            return true;
        }

        internal bool TryEquip(
            int slotIndex,
            string expectedSkillId,
            string newSkillId,
            CommanderSkillBalanceConfig balanceConfig = null)
        {
            if (slotIndex < 0 || slotIndex >= CommanderSkillSlotRules.SlotCount ||
                unlockedSlots == null || slotIndex >= unlockedSlots.Length || !unlockedSlots[slotIndex])
            {
                return false;
            }

            expectedSkillId = expectedSkillId?.Trim() ?? string.Empty;
            newSkillId = newSkillId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(newSkillId) ||
                !string.Equals(equippedSkillIds[slotIndex] ?? string.Empty, expectedSkillId, StringComparison.Ordinal) ||
                !IsOwned(newSkillId) ||
                !(balanceConfig ?? CommanderSkillBalanceConfig.RuntimeDefault).TryGetRule(newSkillId, out _))
            {
                return false;
            }

            var existingSlot = -1;
            for (var index = 0; index < equippedSkillIds.Length; index++)
            {
                if (string.Equals(equippedSkillIds[index], newSkillId, StringComparison.Ordinal))
                {
                    existingSlot = index;
                    break;
                }
            }

            if (existingSlot == slotIndex)
            {
                return false;
            }

            if (existingSlot >= 0)
            {
                equippedSkillIds[existingSlot] = equippedSkillIds[slotIndex] ?? string.Empty; // 중복 대신 슬롯 교환
            }

            equippedSkillIds[slotIndex] = newSkillId;
            return true;
        }

        internal bool TryRecordSummon(
            int expectedSummonCount,
            string skillId,
            CommanderSkillBalanceConfig balanceConfig = null,
            CommanderSkillSummonConfig summonConfig = null)
        {
            return TryRecordSummons(
                expectedSummonCount,
                new[] { skillId },
                balanceConfig,
                summonConfig);
        }

        internal bool TryRecordSummons(
            int expectedSummonCount,
            IReadOnlyList<string> skillIds,
            CommanderSkillBalanceConfig balanceConfig = null,
            CommanderSkillSummonConfig summonConfig = null)
        {
            var balance = balanceConfig ?? CommanderSkillBalanceConfig.RuntimeDefault;
            var summon = summonConfig ?? CommanderSkillSummonConfig.RuntimeDefault;
            if (summonCount != expectedSummonCount || skillIds == null || skillIds.Count == 0 ||
                skillIds.Count > 1000)
            {
                return false;
            }

            var simulatedCount = summonCount;
            for (var index = 0; index < skillIds.Count; index++)
            {
                var skillId = skillIds[index]?.Trim() ?? string.Empty;
                var summonLevel = summon.GetSummonLevel(simulatedCount);
                if (!balance.TryGetRule(skillId, out _) || !summon.IsSkillAvailable(skillId, summonLevel))
                {
                    return false;
                }

                simulatedCount = simulatedCount == int.MaxValue ? int.MaxValue : simulatedCount + 1;
            }

            for (var resultIndex = 0; resultIndex < skillIds.Count; resultIndex++)
            {
                var skillId = skillIds[resultIndex].Trim();
                OwnedCommanderSkillData owned = null;
                for (var ownedIndex = 0; ownedIndex < ownedSkills.Count; ownedIndex++)
                {
                    if (ownedSkills[ownedIndex] != null && ownedSkills[ownedIndex].SkillId == skillId)
                    {
                        owned = ownedSkills[ownedIndex];
                        break;
                    }
                }

                if (owned == null)
                {
                    ownedSkills.Add(new OwnedCommanderSkillData(skillId));
                }
                else
                {
                    owned.AddDuplicate();
                }
            }

            summonCount = simulatedCount;
            summonLevel = summon.GetSummonLevel(summonCount);
            return true;
        }

        internal bool TryLevelUp(
            string skillId,
            int expectedLevel,
            CommanderSkillBalanceConfig balanceConfig = null)
        {
            var balance = balanceConfig ?? CommanderSkillBalanceConfig.RuntimeDefault;
            skillId = skillId?.Trim() ?? string.Empty;
            if (!balance.TryGetRule(skillId, out var rule))
            {
                return false;
            }

            for (var index = 0; index < ownedSkills.Count; index++)
            {
                var owned = ownedSkills[index];
                if (owned != null && owned.SkillId == skillId)
                {
                    return owned.TryLevelUp(expectedLevel, rule);
                }
            }

            return false;
        }

        internal void Repair(
            CommanderSkillBalanceConfig balanceConfig = null,
            CommanderSkillSummonConfig summonConfig = null)
        {
            var balance = balanceConfig ?? CommanderSkillBalanceConfig.RuntimeDefault;
            var summon = summonConfig ?? CommanderSkillSummonConfig.RuntimeDefault;
            ownedSkills ??= new List<OwnedCommanderSkillData>();
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = ownedSkills.Count - 1; index >= 0; index--)
            {
                var owned = ownedSkills[index];
                balance.TryGetRule(owned?.SkillId, out var rule);
                owned?.Repair(rule);
                if (owned == null || string.IsNullOrWhiteSpace(owned.SkillId) || !uniqueIds.Add(owned.SkillId))
                {
                    ownedSkills.RemoveAt(index);
                }
            }

            EnsureOwned(CommanderSkillIds.Fireball);
            EnsureOwned(CommanderSkillIds.IceCrystalOrb);
            uniqueIds.Add(CommanderSkillIds.Fireball);
            uniqueIds.Add(CommanderSkillIds.IceCrystalOrb);
            equippedSkillIds = CopyEquipped(equippedSkillIds);
            unlockedSlots = CopyUnlocked(unlockedSlots);
            for (var index = 0; index < CommanderSkillSlotRules.InitialUnlockedCount; index++)
            {
                unlockedSlots[index] = true;
            }

            var equippedIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < equippedSkillIds.Length; index++)
            {
                var id = equippedSkillIds[index]?.Trim() ?? string.Empty;
                equippedSkillIds[index] = unlockedSlots[index] && uniqueIds.Contains(id) &&
                                          balance.TryGetRule(id, out _) && equippedIds.Add(id)
                    ? id
                    : string.Empty;
            }

            summonCount = Mathf.Max(0, summonCount);
            summonLevel = summon.GetSummonLevel(summonCount);
        }

        internal CommanderSkillProgressView CreateView()
        {
            return new CommanderSkillProgressView(this, ownedSkills, equippedSkillIds, unlockedSlots);
        }

        private void EnsureOwned(string skillId)
        {
            for (var index = 0; index < ownedSkills.Count; index++)
            {
                if (ownedSkills[index] != null && ownedSkills[index].SkillId == skillId)
                {
                    return;
                }
            }

            ownedSkills.Add(new OwnedCommanderSkillData(skillId));
        }

        private bool IsOwned(string skillId)
        {
            for (var index = 0; index < ownedSkills.Count; index++)
            {
                if (ownedSkills[index] != null && ownedSkills[index].SkillId == skillId)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<OwnedCommanderSkillData> CreateInitialOwnedSkills()
        {
            return new List<OwnedCommanderSkillData>
            {
                new OwnedCommanderSkillData(CommanderSkillIds.Fireball),
                new OwnedCommanderSkillData(CommanderSkillIds.IceCrystalOrb)
            };
        }

        private static string[] CreateInitialEquippedSkills()
        {
            return new[]
            {
                CommanderSkillIds.Fireball,
                CommanderSkillIds.IceCrystalOrb,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty
            };
        }

        private static bool[] CreateInitialUnlockedSlots()
        {
            return new[] { true, true, false, false, false, false };
        }

        private static string[] CopyEquipped(IReadOnlyList<string> source)
        {
            var copy = new string[CommanderSkillSlotRules.SlotCount];
            if (source == null)
            {
                return copy;
            }

            for (var index = 0; index < copy.Length && index < source.Count; index++)
            {
                copy[index] = source[index]?.Trim() ?? string.Empty;
            }

            return copy;
        }

        private static bool[] CopyUnlocked(IReadOnlyList<bool> source)
        {
            var copy = new bool[CommanderSkillSlotRules.SlotCount];
            if (source == null)
            {
                return copy;
            }

            for (var index = 0; index < copy.Length && index < source.Count; index++)
            {
                copy[index] = source[index];
            }

            return copy;
        }
    }

    public static class CommanderSkillSummonRules // 구 호출부용 기본 소환 SO 호환 경계
    {
        public static int GetLevel(int accumulatedCount)
        {
            return CommanderSkillSummonConfig.RuntimeDefault.GetSummonLevel(accumulatedCount);
        }

        public static string RollSkillId(System.Random random, int summonLevel)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            return CommanderSkillSummonConfig.RuntimeDefault.RollSkillId(random, summonLevel);
        }

        internal static bool IsCurrentPoolSkill(string skillId)
        {
            return CommanderSkillSummonConfig.RuntimeDefault.IsSkillAvailable(
                skillId,
                CommanderSkillSummonConfig.RuntimeDefault.MaxSummonLevel);
        }
    }

    public readonly struct OwnedCommanderSkillView
    {
        internal OwnedCommanderSkillView(OwnedCommanderSkillData data)
        {
            SkillId = data.SkillId;
            Level = data.Level;
            AwakeningLevel = data.AwakeningLevel;
            DuplicateCount = data.DuplicateCount;
        }

        public string SkillId { get; }
        public int Level { get; }
        public int AwakeningLevel { get; }
        public int DuplicateCount { get; }
    }

    public readonly struct CommanderSkillProgressView
    {
        private readonly OwnedCommanderSkillView[] ownedSkills;
        private readonly string[] equippedSkillIds;
        private readonly bool[] unlockedSlots;

        internal CommanderSkillProgressView(
            CommanderSkillProgressData data,
            IReadOnlyList<OwnedCommanderSkillData> sourceOwned,
            IReadOnlyList<string> sourceEquipped,
            IReadOnlyList<bool> sourceUnlocked)
        {
            ownedSkills = new OwnedCommanderSkillView[sourceOwned?.Count ?? 0];
            for (var index = 0; index < ownedSkills.Length; index++)
            {
                ownedSkills[index] = new OwnedCommanderSkillView(sourceOwned[index]);
            }

            equippedSkillIds = new string[CommanderSkillSlotRules.SlotCount];
            unlockedSlots = new bool[CommanderSkillSlotRules.SlotCount];
            for (var index = 0; index < CommanderSkillSlotRules.SlotCount; index++)
            {
                equippedSkillIds[index] = sourceEquipped != null && index < sourceEquipped.Count
                    ? sourceEquipped[index] ?? string.Empty
                    : string.Empty;
                unlockedSlots[index] = sourceUnlocked != null && index < sourceUnlocked.Count && sourceUnlocked[index];
            }

            AutoUseEnabled = data != null && data.IsAutoUseEnabled;
            SummonLevel = data == null ? 1 : data.SummonLevel;
            SummonCount = data == null ? 0 : data.SummonCount;
        }

        public IReadOnlyList<OwnedCommanderSkillView> OwnedSkills =>
            ownedSkills ?? Array.Empty<OwnedCommanderSkillView>();
        public bool AutoUseEnabled { get; }
        public int SummonLevel { get; }
        public int SummonCount { get; }

        public bool IsSlotUnlocked(int slotIndex)
        {
            return unlockedSlots != null && slotIndex >= 0 && slotIndex < unlockedSlots.Length && unlockedSlots[slotIndex];
        }

        public string GetEquippedSkillId(int slotIndex)
        {
            return equippedSkillIds != null && slotIndex >= 0 && slotIndex < equippedSkillIds.Length
                ? equippedSkillIds[slotIndex] ?? string.Empty
                : string.Empty;
        }
    }
}
