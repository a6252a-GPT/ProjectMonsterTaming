using System;
using ProjectMT.Shared.Equipment;

namespace ProjectMT.Features.Equipment
{
    // 08.10 안건준 재작성 - 군단장 능력치 13종(장비 합산 결과 포함). 문서 규칙: 장비 능력치는
    // "장착한 군단장에게만" 적용하며, 몬스터/편성 부대 능력치에는 절대 반영하지 않는다.
    //
    // 공격력·체력·방어력·공격속도·이동속도는 "군단장 기본 스탯 대비 %" 보너스가 누적된 뒤 기본값에 곱해지고,
    // 나머지(치명타 확률/피해, 스킬·보스·일반 몬스터 피해, 쿨감, 방관, 피해감소)는 절대값이 그대로 더해진다.
    public struct CommanderEquipmentStats
    {
        public float AttackPower;
        public float MaxHealth;
        public float Defense;
        public float AttackSpeed;
        public float MoveSpeed;
        public float CriticalRate;
        public float CriticalDamage;
        public float SkillDamage;
        public float BossDamage;
        public float NormalMonsterDamage;
        public float SkillCooldownReduction;
        public float DefensePenetration;
        public float DamageReduction;

        // 총전투력 - 아직 기획 확정 공식이 없어 임시 가중치로 계산한다(추후 조정 가능).
        public float EstimatePower()
        {
            return MaxHealth * 0.4f
                   + AttackPower * 4f * AttackSpeed
                   + Defense * 2f
                   + MoveSpeed * 10f
                   + CriticalRate * 3f
                   + CriticalDamage * 0.5f;
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
                case EquipmentStatType.CriticalDamage: return CriticalDamage;
                case EquipmentStatType.SkillDamage: return SkillDamage;
                case EquipmentStatType.BossDamage: return BossDamage;
                case EquipmentStatType.NormalMonsterDamage: return NormalMonsterDamage;
                case EquipmentStatType.SkillCooldownReduction: return SkillCooldownReduction;
                case EquipmentStatType.DefensePenetration: return DefensePenetration;
                case EquipmentStatType.DamageReduction: return DamageReduction;
                default: return 0f;
            }
        }
    }

    // 08.10 안건준 재작성 - "장착 장비(핵심 능력치 + 랜덤 옵션) → 상대(%)/절대값 보너스 누적 → 캡 적용
    // → 군단장 능력치" 흐름의 계산 지점. 아직 군단장 기본 능력치를 관리하는 별도 시스템이 없어,
    // 여기서 임시 기본값을 정의한다(실제 기획 수치가 정해지면 이 기본값만 교체하면 된다).
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
            CriticalRate = 5f,
            CriticalDamage = 0f,
            SkillDamage = 0f,
            BossDamage = 0f,
            NormalMonsterDamage = 0f,
            SkillCooldownReduction = 0f,
            DefensePenetration = 0f,
            DamageReduction = 0f
        };

        // 문서 4.4 "능력치 상한". 사거리 캡은 장비 옵션 목록에 사거리가 없어 여기서는 다루지 않는다.
        private const float CriticalRateCap = 75f;
        private const float CriticalDamageCap = 300f;
        private const float AttackSpeedBonusPercentCap = 50f;
        private const float MoveSpeedBonusPercentCap = 30f;
        private const float SkillCooldownReductionCap = 40f;
        private const float DefensePenetrationCap = 80f;
        private const float DamageReductionCap = 70f;

        // 현재 장착 중인 장비 전체를 합산한 군단장 최종 능력치.
        // 몬스터/편성 부대 스탯과는 완전히 분리된 별도 계산이라 서로 영향을 주지 않는다.
        public static CommanderEquipmentStats CalculateTotal()
        {
            var percentBonus = new float[Enum.GetValues(typeof(EquipmentStatType)).Length];
            var flatBonus = new float[percentBonus.Length];

            foreach (EquipmentPart part in Enum.GetValues(typeof(EquipmentPart)))
            {
                if (!EquipmentInventoryRuntime.TryGetEquipped(part, out var item) || item.Definition == null)
                {
                    continue;
                }

                Accumulate(item.Definition.CoreStatContributions, percentBonus, flatBonus);

                var options = item.Instance?.RandomOptions;
                if (options != null)
                {
                    for (var i = 0; i < options.Count; i++)
                    {
                        var contributions = EquipmentOptionInfo.ResolveContributions(options[i].Type, options[i].Value);
                        Accumulate(contributions, percentBonus, flatBonus);
                    }
                }
            }

            var total = BaseStats;
            total.AttackPower += BaseStats.AttackPower * GetPercent(percentBonus, EquipmentStatType.AttackPower) / 100f;
            total.MaxHealth += BaseStats.MaxHealth * GetPercent(percentBonus, EquipmentStatType.MaxHealth) / 100f;
            total.Defense += BaseStats.Defense * GetPercent(percentBonus, EquipmentStatType.Defense) / 100f;
            total.AttackSpeed += BaseStats.AttackSpeed *
                                  Math.Min(GetPercent(percentBonus, EquipmentStatType.AttackSpeed), AttackSpeedBonusPercentCap) / 100f;
            total.MoveSpeed += BaseStats.MoveSpeed *
                                Math.Min(GetPercent(percentBonus, EquipmentStatType.MoveSpeed), MoveSpeedBonusPercentCap) / 100f;

            total.CriticalRate = Math.Min(
                BaseStats.CriticalRate + GetFlat(flatBonus, EquipmentStatType.CriticalRate), CriticalRateCap);
            total.CriticalDamage = Math.Min(
                BaseStats.CriticalDamage + GetFlat(flatBonus, EquipmentStatType.CriticalDamage), CriticalDamageCap);
            total.SkillDamage = BaseStats.SkillDamage + GetFlat(flatBonus, EquipmentStatType.SkillDamage);
            total.BossDamage = BaseStats.BossDamage + GetFlat(flatBonus, EquipmentStatType.BossDamage);
            total.NormalMonsterDamage = BaseStats.NormalMonsterDamage + GetFlat(flatBonus, EquipmentStatType.NormalMonsterDamage);
            total.SkillCooldownReduction = Math.Min(
                BaseStats.SkillCooldownReduction + GetFlat(flatBonus, EquipmentStatType.SkillCooldownReduction), SkillCooldownReductionCap);
            total.DefensePenetration = Math.Min(
                BaseStats.DefensePenetration + GetFlat(flatBonus, EquipmentStatType.DefensePenetration), DefensePenetrationCap);
            total.DamageReduction = Math.Min(
                BaseStats.DamageReduction + GetFlat(flatBonus, EquipmentStatType.DamageReduction), DamageReductionCap);

            return total;
        }

        private static void Accumulate(
            System.Collections.Generic.IReadOnlyList<EquipmentStatContribution> contributions,
            float[] percentBonus,
            float[] flatBonus)
        {
            if (contributions == null)
            {
                return;
            }

            for (var i = 0; i < contributions.Count; i++)
            {
                var contribution = contributions[i];
                var index = (int)contribution.StatType;
                if (contribution.IsRelativeToBase)
                {
                    percentBonus[index] += contribution.Value;
                }
                else
                {
                    flatBonus[index] += contribution.Value;
                }
            }
        }

        private static float GetPercent(float[] percentBonus, EquipmentStatType statType) => percentBonus[(int)statType];
        private static float GetFlat(float[] flatBonus, EquipmentStatType statType) => flatBonus[(int)statType];
    }
}
