using UnityEngine;

namespace ProjectMT.Features.Equipment
{
    // 08.09 안건준 추가 - 장비 등급 5종. 몬스터 등급과 동일하게 Uncommon 없이 5단계만 사용한다.
    public enum EquipmentGrade
    {
        Common, // 일반
        Rare, // 희귀
        Epic, // 영웅
        Legendary, // 전설
        Mythic // 신화
    }

    // 08.09 안건준 추가/수정 - 장비 등급별 표시 이름·색상·드랍 확률(%)을 한 곳에서 관리한다.
    // 색상 규칙(요청 사항): 일반-초록색, 희귀-파란색, 영웅-노란색, 전설-보라색, 신화-빨간색
    // (일반 등급은 처음엔 흰색이었으나, 배경과 구분이 잘 안 돼서 초록색으로 변경했다.)
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
