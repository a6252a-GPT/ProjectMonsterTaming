using ProjectMT.Shared.Equipment;
using UnityEngine;

namespace ProjectMT.Features.Equipment
{
    // 08.10 안건준 수정 - 장비 부위 enum은 저장 데이터가 참조할 수 있도록 Shared 어셈블리로 옮겼다
    // (ProjectMT.Shared.Equipment.EquipmentPart). 표시 이름·드랍 확률 등 기획 정보만 이 클래스에서 관리한다.
    // 08.10 안건준 수정 - 문서("17_능력치_성장_장비_계산_규칙") 기준으로 신발→하의, 반지(악세서리)→장신구로 표시명 변경.
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
                case EquipmentPart.Boots: return "하의";
                case EquipmentPart.Ring: return "장신구";
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
