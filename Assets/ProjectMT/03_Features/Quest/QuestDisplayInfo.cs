using System.Collections.Generic;
using System.Text;
using ProjectMT.Shared.Quest;

namespace ProjectMT.Features.Quest
{
    // 퀘스트 관련 enum의 한글 표시 이름. enum 자체는 저장 데이터가 참조하므로 Shared에 두고,
    // 표시 문구(기획 정보)만 EquipmentGradeInfo와 동일한 방식으로 Features에서 관리한다.
    public static class QuestTypeInfo
    {
        public static string GetDisplayName(QuestType type)
        {
            switch (type)
            {
                case QuestType.Main: return "메인";
                case QuestType.Daily: return "일일";
                case QuestType.Weekly: return "주간";
                default: return type.ToString();
            }
        }
    }

    public static class QuestConditionTypeInfo
    {
        public static string GetDisplayName(QuestConditionType type)
        {
            switch (type)
            {
                case QuestConditionType.MonsterKill: return "몬스터 처치";
                case QuestConditionType.MonsterSummon: return "몬스터 뽑기";
                case QuestConditionType.EquipmentEquip: return "장비 장착";
                case QuestConditionType.EquipmentEnhance: return "장비 강화";
                case QuestConditionType.ExpeditionClear: return "원정대 클리어";
                case QuestConditionType.MonsterOwnedCount: return "몬스터 보유";
                case QuestConditionType.CommanderLevelUp: return "군단장 성장";
                case QuestConditionType.CommanderPotentialUpgrade: return "잠재능력 강화";
                case QuestConditionType.MonsterLevelUp: return "몬스터 레벨업";
                case QuestConditionType.MonsterAscension: return "몬스터 돌파";
                default: return type.ToString();
            }
        }
    }

    public static class QuestUnlockTargetInfo
    {
        public static string GetDisplayName(QuestUnlockTarget target)
        {
            switch (target)
            {
                case QuestUnlockTarget.MonsterSummon: return "몬스터 뽑기";
                case QuestUnlockTarget.Formation: return "부대 편성";
                case QuestUnlockTarget.MonsterUpgrade: return "몬스터 강화";
                case QuestUnlockTarget.Equipment: return "장비";
                case QuestUnlockTarget.EquipmentSlotUpgrade: return "슬롯 강화";
                case QuestUnlockTarget.CommanderPotential: return "잠재능력";
                case QuestUnlockTarget.SpecialContent: return "특수 콘텐츠";
                case QuestUnlockTarget.DailyWeeklyQuest: return "일일·주간 퀘스트";
                default: return target.ToString();
            }
        }

        public static string GetDisplayName(IReadOnlyList<QuestUnlockTarget> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                return "없음";
            }

            var builder = new StringBuilder();
            for (var i = 0; i < targets.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(GetDisplayName(targets[i]));
            }

            return builder.ToString();
        }
    }
}
