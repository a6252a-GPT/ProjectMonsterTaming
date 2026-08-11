using System.Collections.Generic;
using ProjectMT.Shared.Equipment;

namespace ProjectMT.Features.Equipment
{
    // 08.10 안건준 수정 - 문서("17_능력치_성장_장비_계산_규칙") 기준으로 능력치 종류를 6종 → 13종으로 확장했다.
    // AttackPower/MaxHealth/Defense/AttackSpeed/MoveSpeed 는 "군단장 기본 스탯 대비 %" 보너스로 누적되고,
    // 나머지(치명타 확률·피해, 스킬/보스/일반 몬스터 피해, 쿨감, 방관, 피해감소)는 절대값(%p 또는 %)이 그대로 더해진다.
    public enum EquipmentStatType
    {
        AttackPower, // 공격력 (기본 스탯 대비 %)
        MaxHealth, // 체력 (기본 스탯 대비 %)
        Defense, // 방어력 (기본 스탯 대비 %)
        AttackSpeed, // 공격속도 (기본 스탯 대비 %)
        MoveSpeed, // 이동속도 (기본 스탯 대비 %)
        CriticalRate, // 치명타 확률 (%p 절대값)
        CriticalDamage, // 치명타 피해 (%p 절대값)
        SkillDamage, // 스킬 피해 (% 절대값)
        BossDamage, // 보스 피해 (% 절대값)
        NormalMonsterDamage, // 일반 몬스터 피해 (% 절대값)
        SkillCooldownReduction, // 스킬 쿨타임 감소 (%p 절대값)
        DefensePenetration, // 방어 관통률 (%p 절대값)
        DamageReduction // 피해 감소율 (%p 절대값)
    }

    // 08.10 안건준 추가 - 능력치 한 건의 기여분. IsRelativeToBase가 true면 "군단장 기본 스탯 × (값/100)"만큼
    // 더해지는 상대值(%)이고, false면 값 그대로(절대 %p 또는 %) 더해진다.
    public readonly struct EquipmentStatContribution
    {
        public EquipmentStatContribution(EquipmentStatType statType, float value, bool isRelativeToBase)
        {
            StatType = statType;
            Value = value;
            IsRelativeToBase = isRelativeToBase;
        }

        public EquipmentStatType StatType { get; }
        public float Value { get; }
        public bool IsRelativeToBase { get; }
    }

    // 08.10 안건준 재작성 - 부위별 "핵심 능력치" 산출 규칙(문서 4.1·4.2 기준).
    // - 무기/투구/갑옷/하의: 등급별 핵심 능력치 예산(%) × 부위별 분배 비율 = 능력치별 상대(%) 보너스.
    // - 장갑/장신구: 예산 분배가 아니라 등급별 고정표(4.2) 값을 그대로 사용한다.
    public static class EquipmentGradeStatTable
    {
        // 부위별 분배 비율(문서 4.1 "부위별 고정 주 능력치"). 무기/투구/갑옷/하의에만 적용된다.
        private static readonly Dictionary<EquipmentPart, (EquipmentStatType Stat, float Ratio)[]> CoreRatioByPart =
            new Dictionary<EquipmentPart, (EquipmentStatType Stat, float Ratio)[]>
            {
                { EquipmentPart.Weapon, new[] { (EquipmentStatType.AttackPower, 1.00f) } },
                {
                    EquipmentPart.Helmet, new[]
                    {
                        (EquipmentStatType.MaxHealth, 0.70f),
                        (EquipmentStatType.Defense, 0.20f),
                        (EquipmentStatType.AttackPower, 0.10f)
                    }
                },
                {
                    EquipmentPart.Armor, new[]
                    {
                        (EquipmentStatType.Defense, 0.70f),
                        (EquipmentStatType.MaxHealth, 0.20f),
                        (EquipmentStatType.AttackPower, 0.10f)
                    }
                },
                {
                    EquipmentPart.Boots, new[]
                    {
                        (EquipmentStatType.MaxHealth, 0.40f),
                        (EquipmentStatType.Defense, 0.40f),
                        (EquipmentStatType.AttackPower, 0.20f)
                    }
                }
            };

        public static string GetStatDisplayName(EquipmentStatType statType)
        {
            switch (statType)
            {
                case EquipmentStatType.AttackPower: return "공격력";
                case EquipmentStatType.MaxHealth: return "체력";
                case EquipmentStatType.Defense: return "방어력";
                case EquipmentStatType.AttackSpeed: return "공격속도";
                case EquipmentStatType.MoveSpeed: return "이동속도";
                case EquipmentStatType.CriticalRate: return "치명타 확률";
                case EquipmentStatType.CriticalDamage: return "치명타 피해";
                case EquipmentStatType.SkillDamage: return "스킬 피해";
                case EquipmentStatType.BossDamage: return "보스 피해";
                case EquipmentStatType.NormalMonsterDamage: return "일반 몬스터 피해";
                case EquipmentStatType.SkillCooldownReduction: return "스킬 쿨타임 감소";
                case EquipmentStatType.DefensePenetration: return "방어 관통률";
                case EquipmentStatType.DamageReduction: return "피해 감소율";
                default: return statType.ToString();
            }
        }

        // 부위 + 등급 하나가 제공하는 "핵심 능력치" 목록(1~2개)을 계산한다.
        public static List<EquipmentStatContribution> GetCoreStatContributions(
            EquipmentPart part,
            EquipmentGrade grade)
        {
            return GetCoreStatContributions(part, grade, EquipmentBalanceConfig.RuntimeDefault);
        }

        public static List<EquipmentStatContribution> GetCoreStatContributions(
            EquipmentPart part,
            EquipmentGrade grade,
            EquipmentBalanceConfig balance)
        {
            if (balance == null)
            {
                throw new System.ArgumentNullException(nameof(balance));
            }

            var result = new List<EquipmentStatContribution>();

            if (CoreRatioByPart.TryGetValue(part, out var ratios))
            {
                var budgetPercent = balance.GetCoreStatBudgetPercent(grade);
                foreach (var (stat, ratio) in ratios)
                {
                    result.Add(new EquipmentStatContribution(stat, budgetPercent * ratio, isRelativeToBase: true));
                }

                return result;
            }

            if (part == EquipmentPart.Glove)
            {
                result.Add(new EquipmentStatContribution(
                    EquipmentStatType.CriticalRate, balance.GetGloveCriticalRatePercent(grade), isRelativeToBase: false));
                result.Add(new EquipmentStatContribution(
                    EquipmentStatType.CriticalDamage, balance.GetGloveCriticalDamagePercent(grade), isRelativeToBase: false));
                return result;
            }

            if (part == EquipmentPart.Ring)
            {
                result.Add(new EquipmentStatContribution(
                    EquipmentStatType.AttackSpeed, balance.GetRingAttackSpeedPercent(grade), isRelativeToBase: true));
                result.Add(new EquipmentStatContribution(
                    EquipmentStatType.MoveSpeed, balance.GetRingMoveSpeedPercent(grade), isRelativeToBase: true));
                return result;
            }

            return result;
        }
    }
}
