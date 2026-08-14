using System;
using System.Collections.Generic;
using ProjectMT.Features.Equipment;
using ProjectMT.Shared.Equipment;

namespace ProjectMT.Features.Commander
{
    // 옵션 종류(13) x 등급(5) 조합 1개의 정보(값 범위 포함).
    public readonly struct CommanderPotentialOption
    {
        public CommanderPotentialOption(EquipmentOptionType type, EquipmentGrade grade, float minValue, float maxValue)
        {
            Type = type;
            Grade = grade;
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public EquipmentOptionType Type { get; }
        public EquipmentGrade Grade { get; }
        public float MinValue { get; }
        public float MaxValue { get; }
    }

    // 실제로 뽑힌 결과(등급+옵션 종류+확정값 1개).
    public readonly struct CommanderPotentialRollResult
    {
        public CommanderPotentialRollResult(EquipmentOptionType type, EquipmentGrade grade, float value)
        {
            Type = type;
            Grade = grade;
            Value = value;
        }

        public EquipmentOptionType Type { get; }
        public EquipmentGrade Grade { get; }
        public float Value { get; }
    }

    // 군단장 잠재능력 옵션표(문서 "잠재능력_옵션_정리.md" 기준).
    // 옵션 기준값·같은 등급 안의 랜덤 범위·옵션 종류 가중치는 장비 추가 옵션(EquipmentRandomOptionRoller)과
    // 완전히 동일한 공식을 그대로 재사용한다("장비처럼 등급이 같아도 일정 범위 안에서 랜덤값이 붙는다").
    // 등급이 뽑힐 확률만 장비 드랍 확률표와 다른, 잠재능력 전용표를 쓴다.
    public static class CommanderPotentialOptionTable
    {
        private static readonly EquipmentGrade[] OrderedGrades =
        {
            EquipmentGrade.Common, EquipmentGrade.Rare, EquipmentGrade.Epic,
            EquipmentGrade.Legendary, EquipmentGrade.Mythic
        };

        // 문서의 "등급 확률" 표. 인덱스는 OrderedGrades와 같은 순서(합계 100).
        private static readonly float[] GradeWeightPercent = { 50f, 30f, 15f, 4f, 1f };

        public static IReadOnlyList<EquipmentGrade> Grades => OrderedGrades;

        // 잠재능력 등급 표기: 일반/희귀/영웅/전설/신화.
        public static string GetGradeDisplayName(EquipmentGrade grade)
        {
            switch (grade)
            {
                case EquipmentGrade.Common: return "일반";
                case EquipmentGrade.Rare: return "희귀";
                case EquipmentGrade.Epic: return "영웅";
                case EquipmentGrade.Legendary: return "전설";
                case EquipmentGrade.Mythic: return "신화";
                default: return grade.ToString();
            }
        }

        public static float GetGradeWeightPercent(EquipmentGrade grade)
        {
            var index = Array.IndexOf(OrderedGrades, grade);
            return index >= 0 ? GradeWeightPercent[index] : 0f;
        }

        public static CommanderPotentialOption GetOption(EquipmentOptionType type, EquipmentGrade grade)
        {
            return GetOption(type, grade, EquipmentBalanceConfig.RuntimeDefault);
        }

        // 옵션 값 범위 = 기준값 x 등급 배율 x [최소, 최대] 랜덤 배율(장비 추가 옵션과 동일한 공식).
        public static CommanderPotentialOption GetOption(
            EquipmentOptionType type,
            EquipmentGrade grade,
            EquipmentBalanceConfig balance)
        {
            balance ??= EquipmentBalanceConfig.RuntimeDefault;
            var baseValue = EquipmentOptionInfo.GetBaseValue(type, balance);
            var gradeMultiplier = balance.GetRandomOptionGradeMultiplier(grade);
            var min = baseValue * gradeMultiplier * balance.MinimumRandomMultiplier;
            var max = baseValue * gradeMultiplier * balance.MaximumRandomMultiplier;
            return new CommanderPotentialOption(type, grade, min, max);
        }

        // 13개 옵션 x 5개 등급 = 65가지 조합 전체 목록(옵션표 확인·기획 검토용).
        public static List<CommanderPotentialOption> GetAllCombinations(EquipmentBalanceConfig balance = null)
        {
            balance ??= EquipmentBalanceConfig.RuntimeDefault;
            var types = EquipmentOptionInfo.AllTypes;
            var result = new List<CommanderPotentialOption>(types.Length * OrderedGrades.Length);
            for (var t = 0; t < types.Length; t++)
            {
                for (var g = 0; g < OrderedGrades.Length; g++)
                {
                    result.Add(GetOption(types[t], OrderedGrades[g], balance));
                }
            }

            return result;
        }

        // 등급은 잠재능력 전용 확률표로, 옵션 종류는 장비와 같은 그룹 가중치로 뽑고,
        // 최종 수치는 그 등급 범위 안에서 균등 난수로 정한다.
        public static CommanderPotentialRollResult Roll(EquipmentBalanceConfig balance = null, Random random = null)
        {
            balance ??= EquipmentBalanceConfig.RuntimeDefault;
            var rng = random ?? new Random();

            var grade = RollGrade(rng);
            var type = RollType(balance, rng);
            var option = GetOption(type, grade, balance);
            var value = option.MinValue + (float)rng.NextDouble() * (option.MaxValue - option.MinValue);
            return new CommanderPotentialRollResult(type, grade, value);
        }

        private static EquipmentGrade RollGrade(Random rng)
        {
            var totalWeight = 0f;
            for (var i = 0; i < GradeWeightPercent.Length; i++)
            {
                totalWeight += GradeWeightPercent[i];
            }

            var roll = (float)rng.NextDouble() * totalWeight;
            var accumulated = 0f;
            for (var i = 0; i < OrderedGrades.Length; i++)
            {
                accumulated += GradeWeightPercent[i];
                if (roll < accumulated)
                {
                    return OrderedGrades[i];
                }
            }

            return OrderedGrades[OrderedGrades.Length - 1];
        }

        private static EquipmentOptionType RollType(EquipmentBalanceConfig balance, Random rng)
        {
            var types = EquipmentOptionInfo.AllTypes;
            var totalWeight = 0f;
            for (var i = 0; i < types.Length; i++)
            {
                totalWeight += balance.GetOptionWeight(types[i]);
            }

            if (totalWeight <= 0f)
            {
                return types[0];
            }

            var roll = (float)rng.NextDouble() * totalWeight;
            var accumulated = 0f;
            for (var i = 0; i < types.Length; i++)
            {
                accumulated += balance.GetOptionWeight(types[i]);
                if (roll < accumulated)
                {
                    return types[i];
                }
            }

            return types[types.Length - 1];
        }
    }
}
