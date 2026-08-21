using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.Items;
using UnityEngine;

namespace ProjectMT.Shared.UI
{
    public static class ItemGradeFramePalette // 아이템·장비 공통 등급 프레임 규칙
    {
        public const string FrameVariantPrefix = "ItemFrame_01_Normal_";
        public const string CommonSuffix = "Gray";

        // 등급별 단색(분해창 등 솔리드 컬러 UI용)
        private static readonly Color GraySolidColor = new Color32(99, 94, 90, 255);
        private static readonly Color BlueSolidColor = new Color32(52, 112, 175, 255);
        private static readonly Color PlumSolidColor = new Color32(126, 68, 159, 255);
        private static readonly Color YellowSolidColor = new Color32(194, 162, 62, 255);
        private static readonly Color RedSolidColor = new Color32(183, 55, 55, 255);

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

        public static Color GetColor(ItemGrade grade)
        {
            return GetColorBySuffix(GetSuffix(grade));
        }

        public static Color GetColor(EquipmentGrade grade)
        {
            return GetColorBySuffix(GetSuffix(grade));
        }

        private static Color GetColorBySuffix(string suffix)
        {
            return suffix switch
            {
                "Blue" => BlueSolidColor,
                "Plum" => PlumSolidColor,
                "Yellow" => YellowSolidColor,
                "Red" => RedSolidColor,
                _ => GraySolidColor
            };
        }
    }
}
