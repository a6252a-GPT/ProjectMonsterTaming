namespace ProjectMT.Shared.Quest
{
    // 퀘스트 진행 방식 구분. 메인은 한 번에 1개만 진행(선행 퀘스트로 한 줄 연결),
    // 일일·주간은 여러 개가 동시에 진행되고 주기적으로 초기화된다(QUEST-07, 추후 구현).
    public enum QuestType
    {
        Main, // 메인 퀘스트
        Daily, // 일일 퀘스트
        Weekly // 주간 퀘스트
    }
}
