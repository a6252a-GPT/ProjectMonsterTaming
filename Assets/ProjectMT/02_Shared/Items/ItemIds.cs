using System;
using System.Collections.Generic;

namespace ProjectMT.Shared.Items
{
    public static class ItemIds // 저장·보상·UI가 공유하는 정식 일반 아이템 ID
    {
        public const string Gold = "currency_gold";
        public const string Diamond = "currency_diamond";
        public const string AscensionStone = "currency_ascension_stone";

        public const string EquipmentSlotUpgradeStone = "material_equipment_slot_upgrade_stone";
        public const string CommanderSkillUpgradeStone = "material_commander_skill_upgrade_stone";
        public const string LegionPotentialUpgradeStone = "material_legion_potential_upgrade_stone";

        public const string MonsterSummonTicket = "ticket_monster_summon";
        public const string CommanderSkillSummonTicket = "ticket_commander_skill_summon";

        public const string FoodRiotKey = "key_food_riot";
        public const string TreasureSpiritKey = "key_treasure_spirit";
        public const string GiantSpellbookKey = "key_giant_spellbook";
        public const string GuardiansTowerKey = "key_guardians_tower";

        public const string GoldPouch = "consumable_gold_pouch";

        private static readonly string[] requiredCatalogIds =
        {
            Gold,
            Diamond,
            AscensionStone,
            EquipmentSlotUpgradeStone,
            CommanderSkillUpgradeStone,
            LegionPotentialUpgradeStone,
            MonsterSummonTicket,
            CommanderSkillSummonTicket,
            FoodRiotKey,
            TreasureSpiritKey,
            GiantSpellbookKey,
            GuardiansTowerKey,
            GoldPouch
        };

        public static IReadOnlyList<string> RequiredCatalogIds => requiredCatalogIds;

        public static bool TryGetCoreBalanceId(string itemId, out string canonicalId)
        {
            if (string.Equals(itemId?.Trim(), Gold, StringComparison.OrdinalIgnoreCase))
            {
                canonicalId = Gold;
                return true;
            }

            if (string.Equals(itemId?.Trim(), Diamond, StringComparison.OrdinalIgnoreCase))
            {
                canonicalId = Diamond;
                return true;
            }

            if (string.Equals(itemId?.Trim(), AscensionStone, StringComparison.OrdinalIgnoreCase))
            {
                canonicalId = AscensionStone;
                return true;
            }

            canonicalId = string.Empty;
            return false;
        }

        public static bool TryGetRequiredCategory(string itemId, out ItemCategory category)
        {
            switch (itemId?.Trim())
            {
                case Gold:
                case Diamond:
                case AscensionStone:
                    category = ItemCategory.Currency;
                    return true;
                case EquipmentSlotUpgradeStone:
                case CommanderSkillUpgradeStone:
                case LegionPotentialUpgradeStone:
                    category = ItemCategory.UpgradeMaterial;
                    return true;
                case MonsterSummonTicket:
                case CommanderSkillSummonTicket:
                    category = ItemCategory.SummonTicket;
                    return true;
                case FoodRiotKey:
                case TreasureSpiritKey:
                case GiantSpellbookKey:
                case GuardiansTowerKey:
                    category = ItemCategory.DungeonKey;
                    return true;
                case GoldPouch:
                    category = ItemCategory.Consumable;
                    return true;
                default:
                    category = default;
                    return false;
            }
        }

        public static string GetFallbackDisplayName(string itemId)
        {
            switch (itemId?.Trim())
            {
                case Gold:
                    return "골드";
                case Diamond:
                    return "다이아";
                case AscensionStone:
                    return "돌파석";
                case EquipmentSlotUpgradeStone:
                    return "장비 슬롯 강화석";
                case CommanderSkillUpgradeStone:
                    return "스킬 강화석";
                case LegionPotentialUpgradeStone:
                    return "잠재능력 강화석";
                case MonsterSummonTicket:
                    return "몬스터 소환권";
                case CommanderSkillSummonTicket:
                    return "군단장 스킬 소환권";
                case FoodRiotKey:
                    return "식량 대소동 열쇠";
                case TreasureSpiritKey:
                    return "보물 정령 열쇠";
                case GiantSpellbookKey:
                    return "거대 마도서 열쇠";
                case GuardiansTowerKey:
                    return "고대 수호수 열쇠";
                case GoldPouch:
                    return "골드 주머니";
                default:
                    return itemId ?? string.Empty;
            }
        }
    }
}
