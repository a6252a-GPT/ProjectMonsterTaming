using UnityEngine;

namespace ProjectMT.Features.Equipment
{
    // 08.09 안건준 추가 - 장비 부위 6종. 부위별 드랍 확률은 전부 동일(1/6)하다.
    public enum EquipmentPart
    {
        Weapon, // 무기 - 공격력
        Helmet, // 투구 - 최대 체력
        Armor, // 갑옷 - 방어력
        Glove, // 장갑 - 공격속도
        Boots, // 신발 - 이동속도
        Ring // 반지(악세서리) - 치명타
    }

    // 08.09 안건준 추가 - 장비 부위 관련 고정 정보(표시 이름 등)를 한 곳에서 관리한다.
    public static class EquipmentPartInfo
    {
        public const int PartCount = 6;

        public static string GetDisplayName(EquipmentPart part)
        {
            switch (part)
            {
                case EquipmentPart.Weapon: return "무기";
                case EquipmentPart.Helmet: return "투구";
                case EquipmentPart.Armor: return "갑옷";
                case EquipmentPart.Glove: return "장갑";
                case EquipmentPart.Boots: return "신발";
                case EquipmentPart.Ring: return "반지";
                default: return part.ToString();
            }
        }

        // 부위별 균등 확률(1/6)로 무작위 부위 하나를 뽑는다. 0~1 난수(roll)를 그대로 받는다.
        public static EquipmentPart RollUniform(float roll01)
        {
            var index = Mathf.Clamp(Mathf.FloorToInt(roll01 * PartCount), 0, PartCount - 1);
            return (EquipmentPart)index;
        }
    }
}
