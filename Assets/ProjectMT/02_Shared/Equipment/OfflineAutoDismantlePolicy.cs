namespace ProjectMT.Shared.Equipment
{
    public enum OfflineAutoDismantlePolicy // 방치 보상 신규 장비의 선분해 범위
    {
        Off = 0,
        Common = 1,
        Rare = 2,
        Epic = 3
    }

    public static class OfflineAutoDismantlePolicyInfo
    {
        public static bool IsValid(OfflineAutoDismantlePolicy policy) =>
            policy >= OfflineAutoDismantlePolicy.Off && policy <= OfflineAutoDismantlePolicy.Epic;

        public static bool TryGetMaximumGrade(
            OfflineAutoDismantlePolicy policy,
            out EquipmentGrade maximumGrade)
        {
            switch (policy)
            {
                case OfflineAutoDismantlePolicy.Common:
                    maximumGrade = EquipmentGrade.Common;
                    return true;
                case OfflineAutoDismantlePolicy.Rare:
                    maximumGrade = EquipmentGrade.Rare;
                    return true;
                case OfflineAutoDismantlePolicy.Epic:
                    maximumGrade = EquipmentGrade.Epic;
                    return true;
                default:
                    maximumGrade = default;
                    return false;
            }
        }

        public static string GetDisplayName(OfflineAutoDismantlePolicy policy)
        {
            return policy switch
            {
                OfflineAutoDismantlePolicy.Off => "사용 안 함",
                OfflineAutoDismantlePolicy.Common => "일반 이하",
                OfflineAutoDismantlePolicy.Rare => "희귀 이하",
                OfflineAutoDismantlePolicy.Epic => "영웅 이하",
                _ => "일반 이하"
            };
        }
    }
}
