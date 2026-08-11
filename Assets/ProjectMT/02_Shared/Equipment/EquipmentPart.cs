namespace ProjectMT.Shared.Equipment
{
    // 08.10 안건준 추가 - 장비 부위 6종. 저장 데이터(EquipmentInstanceData)가 참조하므로 Shared 어셈블리에 둔다.
    // 표시 이름·드랍 확률 등 UI/기획 정보는 ProjectMT.Features.Equipment.EquipmentPartInfo가 담당한다.
    public enum EquipmentPart
    {
        Weapon,
        Helmet,
        Armor,
        Glove,
        Boots,
        Ring
    }
}
