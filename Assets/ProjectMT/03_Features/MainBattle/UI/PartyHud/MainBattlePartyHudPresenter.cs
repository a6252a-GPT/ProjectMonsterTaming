using System.Collections.Generic;
using ProjectMT.Features.Expedition;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattlePartyHudPresenter : MonoBehaviour // 원정대 5칸과 HUD 슬롯 연결
    {
        public const int SlotCount = 5;

        [SerializeField] private MainBattlePartyHudSlotView[] slots = new MainBattlePartyHudSlotView[SlotCount];

        private readonly List<UnitActor> unitBuffer = new List<UnitActor>(SlotCount);
        private readonly UnitActor[] mappedUnits = new UnitActor[SlotCount];
        private ExpeditionController expedition;
        private int observedRunSequence = int.MinValue;

        public IReadOnlyList<MainBattlePartyHudSlotView> Slots => slots;
        public bool IsConfigured => expedition != null;

        private void LateUpdate()
        {
            RefreshNow();
        }

        public void Configure(ExpeditionController expeditionController)
        {
            expedition = expeditionController;
            observedRunSequence = int.MinValue;
            ClearSlots();
            RefreshNow();
        }

        public void RefreshNow()
        {
            if (expedition == null || slots == null)
            {
                return;
            }

            if (observedRunSequence != expedition.RunSequence)
            {
                observedRunSequence = expedition.RunSequence;
                ClearSlots();
            }

            for (var index = 0; index < mappedUnits.Length; index++)
            {
                mappedUnits[index] = null;
            }

            expedition.CollectPlayerUnits(unitBuffer);
            for (var index = 0; index < unitBuffer.Count; index++)
            {
                var actor = unitBuffer[index];
                if (actor != null && expedition.TryGetPlayerSlot(actor, out var slotIndex) &&
                    slotIndex >= 0 && slotIndex < mappedUnits.Length)
                {
                    mappedUnits[slotIndex] = actor;
                }
            }

            for (var index = 0; index < slots.Length; index++)
            {
                var slot = slots[index];
                if (slot == null)
                {
                    continue;
                }

                if (index < mappedUnits.Length && mappedUnits[index] != null)
                {
                    slot.Bind(mappedUnits[index]);
                }
                else
                {
                    slot.ShowMissing();
                }
            }
        }

        private void ClearSlots()
        {
            if (slots == null)
            {
                return;
            }

            for (var index = 0; index < slots.Length; index++)
            {
                slots[index]?.ClearForNewRun();
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(MainBattlePartyHudSlotView[] slotViews)
        {
            slots = slotViews ?? new MainBattlePartyHudSlotView[SlotCount];
            ClearSlots();
        }
#endif
    }
}
