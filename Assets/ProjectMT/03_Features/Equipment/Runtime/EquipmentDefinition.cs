using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Equipment;
using UnityEngine;

namespace ProjectMT.Features.Equipment
{
    // "부위 + 등급" 조합이 결정하는 고정 정보(아이콘, 표시 이름, 핵심 능력치).
    // 부위별로 능력치가 1~3개로 갈리기 때문에 StatType/StatValue 단일 값이 아니라 CoreStatContributions
    // 목록을 갖는다. 랜덤 추가 옵션은 부위+등급과 무관하게 인스턴스마다 다르므로 여기에는 포함되지
    // 않는다(EquipmentInstanceData.RandomOptions 참고).
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
            CoreStatContributions = EquipmentGradeStatTable.GetCoreStatContributions(part, grade);
            // 예: "일반 무기" - 문서 규칙상 장비 종류 키는 부위+등급 조합이므로 이름도 등급+부위로 통일한다.
            DisplayName = $"{EquipmentGradeInfo.GetDisplayName(grade)} {baseDisplayName}";
        }

        // 부위+등급 조합 키. 더 이상 보유 중첩 기준으로 쓰이지 않지만(인스턴스마다 개별 보관),
        // 카탈로그에서 "이 부위+등급의 고정 정보"를 가리키는 식별자로는 계속 쓰인다.
        public string Key => $"{Part}_{Grade}";

        public string BaseItemId { get; }
        public string DisplayName { get; }
        public EquipmentPart Part { get; }
        public EquipmentGrade Grade { get; }
        public Sprite Icon { get; }
        public IReadOnlyList<EquipmentStatContribution> CoreStatContributions { get; }

        // 상세 정보 영역에 표시할 핵심 능력치 요약. 여러 능력치면 줄바꿈으로 나열한다.
        public string GetCoreStatSummary()
        {
            return string.Join("\n", CoreStatContributions.Select(FormatContribution));
        }

        // 기본옵션(핵심 능력치)도 추가 랜덤 옵션과 동일하게 전부 "%"로 표시한다(절대값/상대값 구분 없이 항상 "%").
        private static string FormatContribution(EquipmentStatContribution contribution)
        {
            var statName = EquipmentGradeStatTable.GetStatDisplayName(contribution.StatType);
            return $"{statName} +{contribution.Value:0.0}%";
        }
    }
}
