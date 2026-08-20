namespace ProjectMT.Shared.Quest
{
    // 퀘스트 보상 수령 시 해금되는 기능 대상(기능명세서 6.4 기준 고정 목록).
    public enum QuestUnlockTarget
    {
        MonsterSummon, // 몬스터 뽑기
        Formation, // 부대 편성
        MonsterUpgrade, // 몬스터 강화
        Equipment, // 장비
        EquipmentSlotUpgrade, // 슬롯 강화
        CommanderPotential, // 잠재능력
        SpecialContent, // 특수 콘텐츠
        DailyWeeklyQuest // 일일·주간 퀘스트
    }
}
