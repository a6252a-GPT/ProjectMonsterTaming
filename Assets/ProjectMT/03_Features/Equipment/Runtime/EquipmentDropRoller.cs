using System;
using System.Collections.Generic;

namespace ProjectMT.Features.Equipment
{
    // 08.09 안건준 추가 - 장비 드랍 판정. 작업 문서 규칙:
    // 대상 스테이지(10, 15, 20 ...)를 클리어하면 장비를 정확히 6개 생성하고,
    // 6개 각각에 대해 부위(1/6 균등)와 등급(68/20/8/3/1%)을 서로 독립적으로 판정한다.
    public static class EquipmentDropRoller
    {
        public const int DropCount = 6;

        public static List<EquipmentDefinition> RollDrop(EquipmentCatalog catalog, Random random = null)
        {
            var rng = random ?? new Random();
            var results = new List<EquipmentDefinition>(DropCount);
            for (var i = 0; i < DropCount; i++)
            {
                var part = EquipmentPartInfo.RollUniform((float)rng.NextDouble());
                var grade = EquipmentGradeInfo.RollWeighted((float)(rng.NextDouble() * 100.0));
                var definition = catalog.GetDefinitionForPart(part, grade);
                if (definition != null)
                {
                    results.Add(definition);
                }
            }

            return results;
        }
    }
}
