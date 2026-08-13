using ProjectMT.Shared.Equipment;
using UnityEngine;

namespace ProjectMT.Features.Equipment
{
    // 08.10 안건준 수정 - 장비 등급 enum은 저장 데이터가 참조할 수 있도록 Shared 어셈블리로 옮겼다
    // (ProjectMT.Shared.Equipment.EquipmentGrade). 표시 이름·색상·드랍 확률·옵션 배율 등 기획 정보만 여기서 관리한다.
    // 색상 규칙(요청 사항): 일반-초록색, 희귀-파란색, 영웅-노란색, 전설-보라색, 신화-빨간색.
    public static class EquipmentGradeInfo
    {
        // 등급 순서대로 정렬된 배열. 인덱스는 EquipmentGrade의 순서와 일치한다.
        private static readonly EquipmentGrade[] OrderedGrades =
        {
            EquipmentGrade.Common, EquipmentGrade.Rare, EquipmentGrade.Epic,
            EquipmentGrade.Legendary, EquipmentGrade.Mythic
        };

        public static string GetDisplayName(EquipmentGrade grade)
        {
            switch (grade)
            {
                case EquipmentGrade.Common: return "일반";
                case EquipmentGrade.Rare: return "희귀";
                case EquipmentGrade.Epic: return "영웅";
                case EquipmentGrade.Legendary: return "전설";
                case EquipmentGrade.Mythic: return "신화";
                default: return grade.ToString();
            }
        }

        public static Color GetColor(EquipmentGrade grade)
        {
            switch (grade)
            {
                case EquipmentGrade.Common: return new Color(0.35f, 0.85f, 0.35f); // 초록색
                case EquipmentGrade.Rare: return new Color(0.30f, 0.55f, 1f); // 파란색
                case EquipmentGrade.Epic: return new Color(1f, 0.85f, 0.15f); // 노란색
                case EquipmentGrade.Legendary: return new Color(0.65f, 0.35f, 0.95f); // 보라색
                case EquipmentGrade.Mythic: return new Color(0.95f, 0.20f, 0.20f); // 빨간색
                default: return Color.white;
            }
        }

        // 설정 자산의 등급 확률표를 기준으로 0~100 난수에 해당하는 등급을 뽑는다.
        public static EquipmentGrade RollWeighted(float roll100)
        {
            return RollWeighted(roll100, EquipmentBalanceConfig.RuntimeDefault);
        }

        public static EquipmentGrade RollWeighted(float roll100, EquipmentBalanceConfig balance)
        {
            if (balance == null)
            {
                throw new System.ArgumentNullException(nameof(balance));
            }

            var totalWeight = 0f;
            for (var i = 0; i < OrderedGrades.Length; i++)
            {
                totalWeight += balance.GetDropWeight(OrderedGrades[i]);
            }

            var roll = Mathf.Clamp(roll100, 0f, 99.9999f) * totalWeight / 100f;
            var accumulated = 0f;
            for (var i = 0; i < OrderedGrades.Length; i++)
            {
                accumulated += balance.GetDropWeight(OrderedGrades[i]);
                if (roll < accumulated)
                {
                    return OrderedGrades[i];
                }
            }

            return OrderedGrades[OrderedGrades.Length - 1];
        }
    }
}
