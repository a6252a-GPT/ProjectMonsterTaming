using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.Items;

namespace ProjectMT.Shared.UI
{
    public static class ItemGradeFramePalette // 아이템·장비 공통 등급 프레임 규칙
    {
        public const string FrameVariantPrefix = "ItemFrame_01_Normal_";
        public const string CommonSuffix = "Gray";

        public static string GetSuffix(ItemGrade grade)
        {
            return grade switch
            {
                ItemGrade.Common => CommonSuffix,
                ItemGrade.Rare => "Blue",
                ItemGrade.Epic => "Plum",
                ItemGrade.Legendary => "Yellow",
                ItemGrade.Mythic => "Red",
                _ => CommonSuffix
            };
        }

        public static string GetSuffix(EquipmentGrade grade)
        {
            return grade switch
            {
                EquipmentGrade.Common => CommonSuffix,
                EquipmentGrade.Rare => "Blue",
                EquipmentGrade.Epic => "Plum",
                EquipmentGrade.Legendary => "Yellow",
                EquipmentGrade.Mythic => "Red",
                _ => CommonSuffix
            };
        }

        public static string GetFrameName(ItemGrade grade)
        {
            return FrameVariantPrefix + GetSuffix(grade);
        }

        public static string GetFrameName(EquipmentGrade grade)
        {
            return FrameVariantPrefix + GetSuffix(grade);
        }
    }
}
