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

        // 등급별 드랍 확률(%). 합계 100.
        private static readonly float[] DropWeights = { 68f, 20f, 8f, 3f, 1f };

        // 08.10 안건준 추가 - 문서 4.1 기준: 부위 고정(핵심) 능력치에 배정되는 "군단장 기본 스탯 대비 비율(%)" 예산.
        private static readonly float[] CoreStatBudgetPercent = { 3f, 5f, 8f, 12f, 18f };

        // 08.10 안건준 추가 - 문서 4.3 기준: 랜덤 추가 옵션 수치에 곱해지는 등급 배율.
        private static readonly float[] RandomOptionGradeMultiplier = { 1.0f, 1.5f, 2.2f, 3.2f, 4.5f };

        // 08.10 안건준 추가 - 문서 4.3 기준: 등급별 랜덤 추가 옵션 개수.
        private static readonly int[] RandomOptionCount = { 1, 1, 2, 3, 4 };

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

        public static float GetDropWeight(EquipmentGrade grade)
        {
            var index = (int)grade;
            return index >= 0 && index < DropWeights.Length ? DropWeights[index] : 0f;
        }

        public static float GetCoreStatBudgetPercent(EquipmentGrade grade)
        {
            var index = (int)grade;
            return index >= 0 && index < CoreStatBudgetPercent.Length ? CoreStatBudgetPercent[index] : 0f;
        }

        public static float GetRandomOptionGradeMultiplier(EquipmentGrade grade)
        {
            var index = (int)grade;
            return index >= 0 && index < RandomOptionGradeMultiplier.Length ? RandomOptionGradeMultiplier[index] : 1f;
        }

        public static int GetRandomOptionCount(EquipmentGrade grade)
        {
            var index = (int)grade;
            return index >= 0 && index < RandomOptionCount.Length ? RandomOptionCount[index] : 0;
        }

        // 등급 확률표(68/20/8/3/1)를 기준으로 0~100 난수(roll100)에 해당하는 등급을 뽑는다.
        public static EquipmentGrade RollWeighted(float roll100)
        {
            var accumulated = 0f;
            for (var i = 0; i < OrderedGrades.Length; i++)
            {
                accumulated += DropWeights[i];
                if (roll100 < accumulated)
                {
                    return OrderedGrades[i];
                }
            }

            return OrderedGrades[OrderedGrades.Length - 1];
        }
    }
}
