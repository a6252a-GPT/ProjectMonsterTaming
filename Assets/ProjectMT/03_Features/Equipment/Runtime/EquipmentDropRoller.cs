using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;

namespace ProjectMT.Features.Equipment
{
    // 08.10 안건준 수정 - 장비 드랍 판정. 작업 문서 규칙(부위/등급 확률)은 그대로 유지하지만,
    // 이제는 "부위+등급" 정의가 아니라 랜덤 추가 옵션까지 확정된 고유 인스턴스(EquipmentInstanceData)를
    // 만들어낸다(문서 규칙: 같은 부위+등급이라도 옵션이 달라 아이템마다 고유하다).
    public static class EquipmentDropRoller
    {
        public const int DropCount = 6;

        public static List<EquipmentInstanceData> RollDrop(Random random = null)
        {
            var rng = random ?? new Random();
            var results = new List<EquipmentInstanceData>(DropCount);
            for (var i = 0; i < DropCount; i++)
            {
                var part = EquipmentPartInfo.RollUniform((float)rng.NextDouble());
                var grade = EquipmentGradeInfo.RollWeighted((float)(rng.NextDouble() * 100.0));
                var randomOptions = EquipmentRandomOptionRoller.Roll(grade, rng);
                results.Add(new EquipmentInstanceData(Guid.NewGuid().ToString("N"), part, grade, randomOptions));
            }

            return results;
        }
    }
}
