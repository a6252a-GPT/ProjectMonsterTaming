namespace ProjectMT.Shared.Equipment
{
    // 08.10 안건준 추가 - 장비 등급 5종. 저장 데이터(EquipmentInstanceData)가 참조하므로 Shared 어셈블리에 둔다.
    // 표시 이름·색상·등급 배율 등 UI/기획 정보는 ProjectMT.Features.Equipment.EquipmentGradeInfo가 담당한다.
    public enum EquipmentGrade
    {
        Common,
        Rare,
        Epic,
        Legendary,
        Mythic
    }
}
