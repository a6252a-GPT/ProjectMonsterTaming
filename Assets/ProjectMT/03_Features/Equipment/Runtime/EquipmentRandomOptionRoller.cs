using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;

namespace ProjectMT.Features.Equipment
{
    // 08.10 안건준 추가 - 등급별 랜덤 추가 옵션 굴림. 요청사항:
    // - 옵션 13종(공격력·방어력·체력도 서로 독립된 옵션) 중에서 매 슬롯마다 독립적으로 뽑는다(같은 옵션이 여러 번 나와도 됨).
    // - 단, 같은 옵션이 이미 2번 나왔으면 다음 굴림에서 그 옵션이 또 나올 확률이 30% 낮아지고,
    //   3번 나왔으면 그 다음 굴림에서 또 30% 낮아진다(누적). 최대 옵션 개수(신화 4개) 기준으로 충분하다.
    // - 옵션 확정값 = 기준값 × 등급 배율 × Random(0.8, 1.2).
    public static class EquipmentRandomOptionRoller
    {
        private const float DuplicatePenaltyMultiplier = 0.7f; // 중복 시 확률 30% 감소
        private const float MinRandomMultiplier = 0.8f;
        private const float MaxRandomMultiplier = 1.2f;

        public static List<EquipmentOptionRollData> Roll(EquipmentGrade grade, Random random)
        {
            var rng = random ?? new Random();
            var optionCount = EquipmentGradeInfo.GetRandomOptionCount(grade);
            var gradeMultiplier = EquipmentGradeInfo.GetRandomOptionGradeMultiplier(grade);
            var pickedCount = new Dictionary<EquipmentOptionType, int>();
            var result = new List<EquipmentOptionRollData>(optionCount);

            for (var slot = 0; slot < optionCount; slot++)
            {
                var type = PickWeightedType(pickedCount, rng);
                pickedCount.TryGetValue(type, out var previousCount);
                pickedCount[type] = previousCount + 1;

                var randomMultiplier = MinRandomMultiplier + (float)rng.NextDouble() * (MaxRandomMultiplier - MinRandomMultiplier);
                var value = EquipmentOptionInfo.GetBaseValue(type) * gradeMultiplier * randomMultiplier;
                result.Add(new EquipmentOptionRollData(type, value));
            }

            return result;
        }

        // 이미 뽑힌 횟수(count)가 2 이상인 옵션은 가중치를 0.7^(count-1)만큼 낮춘다.
        // count 0·1일 때는 패널티가 없다(2번째까지는 자유롭게 중복 가능).
        private static EquipmentOptionType PickWeightedType(Dictionary<EquipmentOptionType, int> pickedCount, Random rng)
        {
            var types = EquipmentOptionInfo.AllTypes;
            var weights = new float[types.Length];
            var totalWeight = 0f;
            for (var i = 0; i < types.Length; i++)
            {
                pickedCount.TryGetValue(types[i], out var count);
                var penaltyExponent = Math.Max(0, count - 1);
                weights[i] = (float)Math.Pow(DuplicatePenaltyMultiplier, penaltyExponent);
                totalWeight += weights[i];
            }

            var roll = (float)rng.NextDouble() * totalWeight;
            var accumulated = 0f;
            for (var i = 0; i < types.Length; i++)
            {
                accumulated += weights[i];
                if (roll < accumulated)
                {
                    return types[i];
                }
            }

            return types[types.Length - 1];
        }
    }
}
