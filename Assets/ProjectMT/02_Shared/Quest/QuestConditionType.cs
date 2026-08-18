namespace ProjectMT.Shared.Quest
{
    // 퀘스트 진행(목표 수치 갱신) 조건 종류. 새 조건이 필요하면 값만 추가하면 된다.
    // 실제 진행도 갱신(이벤트 연결)은 6.2 단계에서 각 시스템과 연결한다.
    public enum QuestConditionType
    {
        MonsterKill, // 몬스터 처치
        MonsterSummon, // 몬스터 뽑기
        EquipmentEquip, // 장비 장착
        EquipmentEnhance, // 장비 강화(슬롯 강화 포함)
        ExpeditionClear, // 원정대 클리어
        MonsterOwnedCount, // 몬스터 보유 수량
        CommanderLevelUp, // 군단장 성장(레벨업)
        CommanderPotentialUpgrade, // 잠재능력 강화
        MonsterLevelUp, // 몬스터 레벨업
        MonsterAscension // 몬스터 돌파
    }
}
