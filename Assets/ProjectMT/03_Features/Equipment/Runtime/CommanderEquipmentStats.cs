namespace ProjectMT.Features.Equipment
{
    // 08.09 안건준 추가 - 군단장 능력치 6종(장비 합산 결과 포함). 문서 규칙: 장비 능력치는
    // "장착한 군단장에게만" 적용하며, 몬스터/편성 부대 능력치에는 절대 반영하지 않는다.
    public struct CommanderEquipmentStats
    {
        public float AttackPower;
        public float MaxHealth;
        public float Defense;
        public float AttackSpeed;
        public float MoveSpeed;
        public float CriticalRate;

        // 총전투력 - 아직 기획 확정 공식이 없어 임시 가중치로 계산한다(추후 조정 가능).
        public float EstimatePower()
        {
            return MaxHealth * 0.4f
                   + AttackPower * 4f * AttackSpeed
                   + Defense * 2f
                   + MoveSpeed * 10f
                   + CriticalRate * 3f;
        }

        public float GetValue(EquipmentStatType statType)
        {
            switch (statType)
            {
                case EquipmentStatType.AttackPower: return AttackPower;
                case EquipmentStatType.MaxHealth: return MaxHealth;
                case EquipmentStatType.Defense: return Defense;
                case EquipmentStatType.AttackSpeed: return AttackSpeed;
                case EquipmentStatType.MoveSpeed: return MoveSpeed;
                case EquipmentStatType.CriticalRate: return CriticalRate;
                default: return 0f;
            }
        }

        private void AddValue(EquipmentStatType statType, float amount)
        {
            switch (statType)
            {
                case EquipmentStatType.AttackPower: AttackPower += amount; break;
                case EquipmentStatType.MaxHealth: MaxHealth += amount; break;
                case EquipmentStatType.Defense: Defense += amount; break;
                case EquipmentStatType.AttackSpeed: AttackSpeed += amount; break;
                case EquipmentStatType.MoveSpeed: MoveSpeed += amount; break;
                case EquipmentStatType.CriticalRate: CriticalRate += amount; break;
            }
        }

        public static CommanderEquipmentStats operator +(CommanderEquipmentStats stats, (EquipmentStatType type, float value) bonus)
        {
            stats.AddValue(bonus.type, bonus.value);
            return stats;
        }
    }

    // 08.09 안건준 추가 - "장착 장비 데이터 → 장비 능력치 합산 → 군단장 능력치" 흐름의 계산 지점.
    // 아직 군단장 기본 능력치를 관리하는 별도 시스템이 없어, 여기서 임시 기본값을 정의한다.
    // (실제 기획 수치가 정해지면 이 기본값만 교체하면 된다. 장비 계산 로직은 그대로 재사용 가능)
    public static class CommanderEquipmentStatsCalculator
    {
        // 군단장 기본 능력치(장비 미장착 상태) - 임시값. 실제 기획 수치로 교체 가능.
        public static readonly CommanderEquipmentStats BaseStats = new CommanderEquipmentStats
        {
            AttackPower = 50f,
            MaxHealth = 500f,
            Defense = 20f,
            AttackSpeed = 1f,
            MoveSpeed = 3f,
            CriticalRate = 5f
        };

        // 현재 장착 중인 장비 전체를 합산한 군단장 최종 능력치.
        // 몬스터/편성 부대 스탯과는 완전히 분리된 별도 계산이라 서로 영향을 주지 않는다.
        public static CommanderEquipmentStats CalculateTotal()
        {
            var total = BaseStats;
            foreach (EquipmentPart part in System.Enum.GetValues(typeof(EquipmentPart)))
            {
                var equipped = EquipmentInventoryRuntime.GetEquippedStack(part);
                if (equipped == null)
                {
                    continue;
                }

                total += (equipped.Definition.StatType, equipped.Definition.StatValue);
            }

            return total;
        }
    }
}
