using System.Collections.Generic;
using ProjectMT.Shared.Equipment;

namespace ProjectMT.Features.Equipment
{
    // 08.10 안건준 추가 - 랜덤 추가 옵션 11종의 기준값·표시 이름·능력치 변환 규칙(문서 4.3 기준).
    public static class EquipmentOptionInfo
    {
        // 롤 순서 그대로 나열 - 가중치 랜덤 추첨 시 이 배열을 순회한다.
        // 08.10 안건준 수정 - "공격력·방어력·체력"을 하나로 묶은 옵션이 아니라 3개의 독립 옵션으로 분리했다.
        public static readonly EquipmentOptionType[] AllTypes =
        {
            EquipmentOptionType.AttackPower,
            EquipmentOptionType.Defense,
            EquipmentOptionType.MaxHealth,
            EquipmentOptionType.AttackSpeed,
            EquipmentOptionType.MoveSpeed,
            EquipmentOptionType.CriticalRate,
            EquipmentOptionType.CriticalDamage,
            EquipmentOptionType.SkillDamage,
            EquipmentOptionType.BossDamage,
            EquipmentOptionType.NormalMonsterDamage,
            EquipmentOptionType.SkillCooldownReduction,
            EquipmentOptionType.DefensePenetration,
            EquipmentOptionType.DamageReduction
        };

        // 문서 4.3 "옵션 기준값". AttackPower/Defense/MaxHealth/AttackSpeed/MoveSpeed 는 기본 스탯 대비 %,
        // 나머지는 %p(치확·치피·쿨감·방관·피해감소) 또는 % 절대값(스킬/보스/일반 몬스터 피해).
        public static float GetBaseValue(EquipmentOptionType type)
        {
            return GetBaseValue(type, EquipmentBalanceConfig.RuntimeDefault);
        }

        public static float GetBaseValue(EquipmentOptionType type, EquipmentBalanceConfig balance)
        {
            if (balance == null)
            {
                throw new System.ArgumentNullException(nameof(balance));
            }

            return balance.GetOptionBaseValuePercent(type);
        }

        public static string GetDisplayName(EquipmentOptionType type)
        {
            switch (type)
            {
                case EquipmentOptionType.AttackPower: return "공격력";
                case EquipmentOptionType.Defense: return "방어력";
                case EquipmentOptionType.MaxHealth: return "체력";
                case EquipmentOptionType.AttackSpeed: return "공격속도";
                case EquipmentOptionType.MoveSpeed: return "이동속도";
                case EquipmentOptionType.CriticalRate: return "치명타 확률";
                case EquipmentOptionType.CriticalDamage: return "치명타 피해";
                case EquipmentOptionType.SkillDamage: return "스킬 피해";
                case EquipmentOptionType.BossDamage: return "보스 피해";
                case EquipmentOptionType.NormalMonsterDamage: return "일반 몬스터 피해";
                case EquipmentOptionType.SkillCooldownReduction: return "스킬 쿨타임 감소";
                case EquipmentOptionType.DefensePenetration: return "방어 관통률";
                case EquipmentOptionType.DamageReduction: return "피해 감소율";
                default: return type.ToString();
            }
        }

        // 옵션 확정값 = 기준값 × 등급 배율 × Random(0.8, 1.2). 호출자가 이미 계산한 magnitude를 그대로
        // 능력치 기여분 목록으로 바꿔준다. 각 옵션은 스탯 1개에만 독립적으로 적용된다.
        public static List<EquipmentStatContribution> ResolveContributions(EquipmentOptionType type, float magnitude)
        {
            var result = new List<EquipmentStatContribution>();
            switch (type)
            {
                case EquipmentOptionType.AttackPower:
                    result.Add(new EquipmentStatContribution(EquipmentStatType.AttackPower, magnitude, true));
                    break;
                case EquipmentOptionType.Defense:
                    result.Add(new EquipmentStatContribution(EquipmentStatType.Defense, magnitude, true));
                    break;
                case EquipmentOptionType.MaxHealth:
                    result.Add(new EquipmentStatContribution(EquipmentStatType.MaxHealth, magnitude, true));
                    break;
                case EquipmentOptionType.AttackSpeed:
                    result.Add(new EquipmentStatContribution(EquipmentStatType.AttackSpeed, magnitude, true));
                    break;
                case EquipmentOptionType.MoveSpeed:
                    result.Add(new EquipmentStatContribution(EquipmentStatType.MoveSpeed, magnitude, true));
                    break;
                case EquipmentOptionType.CriticalRate:
                    result.Add(new EquipmentStatContribution(EquipmentStatType.CriticalRate, magnitude, false));
                    break;
                case EquipmentOptionType.CriticalDamage:
                    result.Add(new EquipmentStatContribution(EquipmentStatType.CriticalDamage, magnitude, false));
                    break;
                case EquipmentOptionType.SkillDamage:
                    result.Add(new EquipmentStatContribution(EquipmentStatType.SkillDamage, magnitude, false));
                    break;
                case EquipmentOptionType.BossDamage:
                    result.Add(new EquipmentStatContribution(EquipmentStatType.BossDamage, magnitude, false));
                    break;
                case EquipmentOptionType.NormalMonsterDamage:
                    result.Add(new EquipmentStatContribution(EquipmentStatType.NormalMonsterDamage, magnitude, false));
                    break;
                case EquipmentOptionType.SkillCooldownReduction:
                    result.Add(new EquipmentStatContribution(EquipmentStatType.SkillCooldownReduction, magnitude, false));
                    break;
                case EquipmentOptionType.DefensePenetration:
                    result.Add(new EquipmentStatContribution(EquipmentStatType.DefensePenetration, magnitude, false));
                    break;
                case EquipmentOptionType.DamageReduction:
                    result.Add(new EquipmentStatContribution(EquipmentStatType.DamageReduction, magnitude, false));
                    break;
            }

            return result;
        }

        // 인벤토리 상세 표시용 - "+1.4%" 형식으로 옵션 한 건을 요약한다.
        // 08.10 안건준 수정 - "%p" 표기가 헷갈린다는 요청으로 추가 랜덤 옵션은 전부 "%"로 통일한다.
        public static string FormatOption(EquipmentOptionType type, float value)
        {
            return $"{GetDisplayName(type)} +{value:0.0}%";
        }
    }
}
