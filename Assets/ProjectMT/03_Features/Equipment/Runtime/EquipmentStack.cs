namespace ProjectMT.Features.Equipment
{
    // 08.09 안건준 추가 - 보유 중인 장비 스택 1개. 같은 부위+등급 장비는 전부 이 스택 하나에 합산된다.
    public sealed class EquipmentStack
    {
        public EquipmentStack(EquipmentDefinition definition, int totalQuantity)
        {
            Definition = definition;
            TotalQuantity = totalQuantity;
        }

        public string Key => Definition.Key;
        public EquipmentDefinition Definition { get; }
        public int TotalQuantity { get; set; }

        // 현재 이 장비 종류가 군단장에게 장착돼 있는지 여부 (부위 슬롯은 1개뿐이므로 bool로 충분하다).
        public bool IsEquipped { get; set; }
    }
}
