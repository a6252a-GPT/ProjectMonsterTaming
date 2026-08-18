using System;
using ProjectMT.Shared.CommanderSkill;

namespace ProjectMT.Features.CommanderSkill
{
    public static class CommanderSkillPriority // 자동 사용은 1번부터 첫 사용 가능 슬롯 선택
    {
        public static int TryUseFirstReadySlot(
            CommanderSkillProgressView progress,
            float[] cooldownRemaining,
            CommanderSkillCatalog catalog,
            Func<int, bool> tryUseSlot)
        {
            if (cooldownRemaining == null || catalog == null || tryUseSlot == null)
            {
                return -1;
            }

            for (var slotIndex = 0; slotIndex < CommanderSkillSlotRules.SlotCount; slotIndex++)
            {
                if (!IsReadySlot(progress, cooldownRemaining, catalog, slotIndex))
                {
                    continue;
                }

                if (tryUseSlot(slotIndex))
                {
                    return slotIndex;
                }
            }

            return -1;
        }

        public static int FindFirstReadySlot(
            CommanderSkillProgressView progress,
            float[] cooldownRemaining,
            CommanderSkillCatalog catalog)
        {
            if (cooldownRemaining == null || catalog == null)
            {
                return -1;
            }

            for (var slotIndex = 0; slotIndex < CommanderSkillSlotRules.SlotCount; slotIndex++)
            {
                if (IsReadySlot(progress, cooldownRemaining, catalog, slotIndex))
                {
                    return slotIndex;
                }
            }

            return -1;
        }

        private static bool IsReadySlot(
            CommanderSkillProgressView progress,
            float[] cooldownRemaining,
            CommanderSkillCatalog catalog,
            int slotIndex)
        {
            return progress.IsSlotUnlocked(slotIndex) &&
                   slotIndex < cooldownRemaining.Length && cooldownRemaining[slotIndex] <= 0f &&
                   catalog.TryGet(progress.GetEquippedSkillId(slotIndex), out _);
        }
    }
}
