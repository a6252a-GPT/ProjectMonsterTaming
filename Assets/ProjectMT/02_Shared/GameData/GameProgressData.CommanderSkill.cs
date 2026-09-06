using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.Items;

namespace ProjectMT.Shared.GameData
{
    public sealed partial class GameProgressData
    {
        internal CommanderSkillSummonReceipt CommanderSkillSummonReceipt { get; private set; }
        internal bool NeedsCommanderAwakeningMigration => commanderSkills != null && commanderSkills.NeedsAwakeningMigration;

        internal bool TryMigrateCommanderAwakening(CommanderSkillBalanceConfig balance)
        {
            var next = commanderSkills.Clone();
            if (!next.TryMigrateAwakening(balance, out var gold) || !TryGrantCommanderConversionGold(gold)) return false;
            commanderSkills = next;
            return true;
        }

        private bool TryGrantCommanderConversionGold(long gold)
        {
            if (gold < 0) return false;
            if (gold == 0) return true;
            if (!ItemInventoryTransactions.TryGrantCoreBalance(items, ItemIds.Gold, gold, out var granted)) return false;
            items = granted;
            return true;
        }
    }

    public sealed partial class GameProgressChange
    {
        internal bool HasAwakenCommanderSkill { get; private set; }
        internal string CommanderSkillToAwakenId { get; private set; }
        internal int ExpectedCommanderSkillStar { get; private set; }
        internal int ExpectedCommanderSkillDuplicates { get; private set; }
        public static GameProgressChange AwakenCommanderSkill(string skillId, int expectedStar, int expectedDuplicates) =>
            new GameProgressChange
            {
                HasAwakenCommanderSkill = true,
                CommanderSkillToAwakenId = skillId?.Trim() ?? string.Empty,
                ExpectedCommanderSkillStar = expectedStar,
                ExpectedCommanderSkillDuplicates = expectedDuplicates
            };
    }
}
