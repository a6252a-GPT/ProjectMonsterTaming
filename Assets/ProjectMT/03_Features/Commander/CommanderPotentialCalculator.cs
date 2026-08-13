using System.Collections.Generic;
using ProjectMT.Features.Equipment;
using ProjectMT.Shared.Commander;
using ProjectMT.Shared.Equipment;

namespace ProjectMT.Features.Commander
{
    // 08.13 안건준 추가 - 채워진 잠재능력 슬롯들을 장비와 동일한 EquipmentStatContribution 형태로 바꿔준다.
    // 이 형태로 만들어두면 EquipmentLegionBonusCalculator가 장비 옵션과 완전히 같은 방식으로 합산할 수 있다.
    public static class CommanderPotentialCalculator
    {
        public static List<EquipmentStatContribution> GetContributions(CommanderPotentialView view)
        {
            var result = new List<EquipmentStatContribution>();
            for (var i = 0; i < CommanderPotentialData.SlotCount; i++)
            {
                var slot = view.GetSlot(i);
                if (!slot.HasValue)
                {
                    continue;
                }

                result.AddRange(EquipmentOptionInfo.ResolveContributions(slot.OptionType, slot.Value));
            }

            return result;
        }
    }
}
