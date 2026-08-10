using UnityEngine;

namespace ProjectMT.Features.Equipment
{
    // 08.09 안건준 추가 - 카탈로그가 만들어내는 "장비 종류" 최종 정보 (부위 베이스 아이템 × 등급 1개 조합).
    // 동일 부위+등급이면 항상 동일한 Key를 가지므로, 보유 스택 합산 기준으로 그대로 사용한다.
    public sealed class EquipmentDefinition
    {
        public EquipmentDefinition(
            string baseItemId,
            string baseDisplayName,
            EquipmentPart part,
            EquipmentGrade grade,
            Sprite icon)
        {
            BaseItemId = baseItemId;
            Part = part;
            Grade = grade;
            Icon = icon;
            StatType = EquipmentGradeStatTable.GetStatType(part);
            StatValue = EquipmentGradeStatTable.GetStatValue(part, grade);
            // 예: "일반 무기" - 문서 규칙상 장비 종류 키는 부위+등급 조합이므로 이름도 등급+부위로 통일한다.
            DisplayName = $"{EquipmentGradeInfo.GetDisplayName(grade)} {baseDisplayName}";
        }

        // 장비 종류 키. 문서 규칙: "부위 + 등급" 조합으로만 구분한다 (베이스 아이템이 여러 개여도 동일 부위·등급이면 같은 키).
        public string Key => $"{Part}_{Grade}";

        public string BaseItemId { get; }
        public string DisplayName { get; }
        public EquipmentPart Part { get; }
        public EquipmentGrade Grade { get; }
        public EquipmentStatType StatType { get; }
        public float StatValue { get; }
        public Sprite Icon { get; }

        public string GetStatSummary()
        {
            var statName = EquipmentGradeStatTable.GetStatDisplayName(StatType);
            var valueText = StatType == EquipmentStatType.AttackSpeed || StatType == EquipmentStatType.MoveSpeed
                ? $"+{StatValue:0.0}"
                : $"+{StatValue:0}";
            return $"{statName} {valueText}";
        }
    }
}
