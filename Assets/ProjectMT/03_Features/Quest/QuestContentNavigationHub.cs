using System;
using System.Collections.Generic;
using ProjectMT.Shared.Quest;

namespace ProjectMT.Features.Quest
{
    // 진행 중인 퀘스트 카드(아이콘)를 눌렀을 때 "지금 어디를 눌러야 하는지" 직접 이동시켜주는 연결 지점.
    // Quest 쪽 코드가 MainBattle의 구체적인 화면 전환 로직을 알 필요 없이,
    // 화면을 소유한 쪽(MainBattleSceneRoot 등)이 QuestConditionType별 이동 동작을 등록해두면 된다.
    public static class QuestContentNavigationHub
    {
        private static readonly Dictionary<QuestConditionType, Action> Handlers =
            new Dictionary<QuestConditionType, Action>();

        public static void Register(QuestConditionType conditionType, Action handler)
        {
            if (handler == null)
            {
                return;
            }

            Handlers[conditionType] = handler;
        }

        public static void Unregister(QuestConditionType conditionType, Action handler)
        {
            if (handler == null)
            {
                return;
            }

            if (Handlers.TryGetValue(conditionType, out var existing) && existing == handler)
            {
                Handlers.Remove(conditionType);
            }
        }

        public static bool CanNavigate(QuestConditionType conditionType)
        {
            return Handlers.ContainsKey(conditionType);
        }

        public static bool TryNavigate(QuestConditionType conditionType)
        {
            if (!Handlers.TryGetValue(conditionType, out var handler) || handler == null)
            {
                return false;
            }

            handler.Invoke();
            return true;
        }
    }
}
