using System;

namespace ProjectMT.Features.CommanderSkill
{
    public static class CommanderSkillEconomyRules // 스킬 소환 경제 전용 환원표
    {
        public static int GetOverflowUpgradeStones(CommanderSkillRarity rarity) => rarity switch
        {
            CommanderSkillRarity.Common => 1,
            CommanderSkillRarity.Rare => 2,
            CommanderSkillRarity.Epic => 3,
            CommanderSkillRarity.Legendary => 4,
            CommanderSkillRarity.Mythic => 6,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, null)
        };
    }
}
