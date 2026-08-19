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
        MonsterAscension, // 몬스터 돌파
        MonsterFormation, // 몬스터 부대 배치
        GrowthDungeonEnter, // 성장 던전(식량 대소동 등) 입장
        CastleRaidEnter, // 군단의 역습 입장
        EquipmentDismantle, // 장비 분해
        MonsterLevelReach, // 보유 몬스터 중 최고 레벨 도달(누적 아님, 현재 값 기준)
        CommanderLevelReach, // 군단장 레벨 도달
        CommanderHealthLevelReach, // 군단 공용 강화 - 체력 레벨 도달
        CommanderAttackLevelReach, // 군단 공용 강화 - 공격력 레벨 도달
        CommanderDefenseLevelReach, // 군단 공용 강화 - 방어력 레벨 도달
        CommanderPowerReach, // 군단장 전투력 도달
        EquipmentSlotUpgradeReach, // 장비 슬롯 강화 중 최고 레벨 도달(부위 무관)
        CommanderPotentialUnlockCount // 잠재능력 슬롯 개방 개수 도달
    }
}
