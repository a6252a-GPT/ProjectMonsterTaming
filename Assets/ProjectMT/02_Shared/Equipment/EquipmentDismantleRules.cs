namespace ProjectMT.Shared.Equipment
{
    public static class EquipmentDismantleRules // 임시 분해 보상표. 최종 밸런스 확정 전 단일 기준으로 사용
    {
        public static int GetUpgradeStoneAmount(EquipmentGrade grade)
        {
            return grade switch
            {
                EquipmentGrade.Common => 1,
                EquipmentGrade.Rare => 3,
                EquipmentGrade.Epic => 8,
                EquipmentGrade.Legendary => 20,
                EquipmentGrade.Mythic => 50,
                _ => 0
            };
        }
    }
}
