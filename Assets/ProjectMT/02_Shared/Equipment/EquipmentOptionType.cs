namespace ProjectMT.Shared.Equipment
{
    // 08.10 안건준 추가 / 08.10 안건준 수정 - 장비 랜덤 추가 옵션 종류 (문서 "17_능력치_성장_장비_계산_규칙" 4.3 기준).
    // 문서상 "공격력·방어력·체력"과 "스킬·보스·일반 몬스터 피해"는 표에서는 한 줄씩이지만, 실제로는 각각
    // 서로 독립된 별도 옵션으로 나눠서 뽑히도록 확장했다(기획 확인 완료). 즉 "공격력·방어력·체력"은 한 번
    // 뽑힐 때 셋에 동시 적용되는 게 아니라, AttackPower/Defense/MaxHealth 3개 중 하나만 독립적으로 뽑힌다.
    // 저장 데이터(EquipmentOptionRollData)가 참조하므로 Shared 어셈블리에 둔다.
    public enum EquipmentOptionType
    {
        AttackPower, // 공격력
        Defense, // 방어력
        MaxHealth, // 체력
        AttackSpeed, // 공격속도
        MoveSpeed, // 이동속도
        CriticalRate, // 치명타 확률
        CriticalDamage, // 치명타 피해
        SkillDamage, // 스킬 피해
        BossDamage, // 보스 피해
        NormalMonsterDamage, // 일반 몬스터 피해
        SkillCooldownReduction, // 스킬 쿨타임 감소
        DefensePenetration, // 방어 관통률
        DamageReduction // 피해 감소율
    }
}
