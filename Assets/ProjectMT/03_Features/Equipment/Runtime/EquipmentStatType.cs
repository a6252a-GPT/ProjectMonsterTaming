namespace ProjectMT.Features.Equipment
{
    // 08.09 안건준 추가 - 장비가 제공하는 능력치 종류. 부위마다 고정된 능력치 1종만 제공한다.
    public enum EquipmentStatType
    {
        AttackPower, // 공격력 (무기)
        MaxHealth, // 최대 체력 (투구)
        Defense, // 방어력 (갑옷)
        AttackSpeed, // 공격속도 (장갑)
        MoveSpeed, // 이동속도 (신발)
        CriticalRate // 치명타 (반지)
    }

    // 08.09 안건준 추가 - 부위별 고정 능력치 종류, 등급별 고정 수치표(작업 문서 확정 수치).
    public static class EquipmentGradeStatTable
    {
        public static EquipmentStatType GetStatType(EquipmentPart part)
        {
            switch (part)
            {
                case EquipmentPart.Weapon: return EquipmentStatType.AttackPower;
                case EquipmentPart.Helmet: return EquipmentStatType.MaxHealth;
                case EquipmentPart.Armor: return EquipmentStatType.Defense;
                case EquipmentPart.Glove: return EquipmentStatType.AttackSpeed;
                case EquipmentPart.Boots: return EquipmentStatType.MoveSpeed;
                case EquipmentPart.Ring: return EquipmentStatType.CriticalRate;
                default: return EquipmentStatType.AttackPower;
            }
        }

        public static string GetStatDisplayName(EquipmentStatType statType)
        {
            switch (statType)
            {
                case EquipmentStatType.AttackPower: return "공격력";
                case EquipmentStatType.MaxHealth: return "체력";
                case EquipmentStatType.Defense: return "방어력";
                case EquipmentStatType.AttackSpeed: return "공격속도";
                case EquipmentStatType.MoveSpeed: return "이동속도";
                case EquipmentStatType.CriticalRate: return "치명타";
                default: return statType.ToString();
            }
        }

        // 부위·등급별 고정 능력치 수치 (작업 문서 표 기준: Common/Rare/Epic/Legendary/Mythic 순).
        public static float GetStatValue(EquipmentPart part, EquipmentGrade grade)
        {
            var gradeIndex = (int)grade;
            switch (part)
            {
                case EquipmentPart.Weapon: return GetByIndex(gradeIndex, 10f, 20f, 30f, 40f, 50f);
                case EquipmentPart.Helmet: return GetByIndex(gradeIndex, 100f, 200f, 300f, 400f, 500f);
                case EquipmentPart.Armor: return GetByIndex(gradeIndex, 10f, 20f, 30f, 40f, 50f);
                case EquipmentPart.Glove: return GetByIndex(gradeIndex, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f);
                case EquipmentPart.Boots: return GetByIndex(gradeIndex, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f);
                case EquipmentPart.Ring: return GetByIndex(gradeIndex, 10f, 20f, 30f, 40f, 50f);
                default: return 0f;
            }
        }

        private static float GetByIndex(int index, float common, float rare, float epic, float legendary, float mythic)
        {
            switch (index)
            {
                case 0: return common;
                case 1: return rare;
                case 2: return epic;
                case 3: return legendary;
                case 4: return mythic;
                default: return common;
            }
        }
    }
}
