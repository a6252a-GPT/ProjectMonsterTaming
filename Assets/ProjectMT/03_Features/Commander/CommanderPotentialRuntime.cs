using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectMT.Shared.Commander;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;

namespace ProjectMT.Features.Commander
{
    // 군단장 잠재능력 슬롯 조회·최초 배정 파사드(GameProgressData 연동). EquipmentSlotUpgradeRuntime과 동일한 구조다.
    public static class CommanderPotentialRuntime
    {
        // "수호자의 힘" 단계 = 해금된 잠재능력 슬롯 수(1단계=1슬롯 ... 5단계=5슬롯).
        public static int UnlockedSlotCount => IsReady ? progress.View.CommanderPotential.Stage : 1;

        private static IGameProgressService progress;
        private static bool rollInFlight;
        private static bool rerollInFlight;
        private static bool lockToggleInFlight;

        public static event Action Changed;

        public static void Configure(IGameProgressService progressService)
        {
            if (progress != null)
            {
                progress.Changed -= HandleProgressChanged;
            }

            progress = progressService;

            if (progress != null)
            {
                progress.Changed += HandleProgressChanged;
            }

            Changed?.Invoke();
        }

        private static bool IsReady => progress != null && progress.IsLoaded;

        private static void HandleProgressChanged() => Changed?.Invoke();

        // 슬롯 하나(0-based)의 현재 상태.
        public static CommanderPotentialSlotView GetSlot(int index) =>
            IsReady ? progress.View.CommanderPotential.GetSlot(index) : default;

        // 5슬롯 전체 읽기 전용 값(군단장 스탯 합산 등에서 사용).
        public static CommanderPotentialView GetView() =>
            IsReady ? progress.View.CommanderPotential : default;

        // "수호자의 힘" 현재 단계/경험치. PotentialText·PotentialSlider 표시용.
        public static int Stage => IsReady ? progress.View.CommanderPotential.Stage : 1;
        public static long Experience => IsReady ? progress.View.CommanderPotential.Experience : 0L;
        public static long ExperiencePerStage => CommanderPotentialData.ExperiencePerStage;
        public static bool IsMaxStage => Stage >= CommanderPotentialData.MaxStage;

        // 0~1 사이 진행률(슬라이더 fillAmount 등에 바로 사용).
        public static float ExperienceRatio01
        {
            get
            {
                if (ExperiencePerStage <= 0)
                {
                    return 0f;
                }

                var ratio = (float)Experience / ExperiencePerStage;
                if (ratio < 0f)
                {
                    return 0f;
                }

                return ratio > 1f ? 1f : ratio;
            }
        }

        // "잠재 능력 변경"에 필요한 잠재능력 강화석 보유량.
        public static long StoneBalance =>
            IsReady && progress.View.Items.TryGetQuantity(ItemIds.LegionPotentialUpgradeStone, out var quantity)
                ? quantity
                : 0L;

        // (값이 있고 잠기지 않은) 또는 (해금됐지만 아직 비어있는) 슬롯이 하나라도 있어야
        // "잠재 능력 변경" 버튼을 쓸 수 있다.
        public static bool HasRerollableSlot()
        {
            if (!IsReady)
            {
                return false;
            }

            var unlockedCount = UnlockedSlotCount;
            for (var i = 0; i < CommanderPotentialData.SlotCount; i++)
            {
                var slot = GetSlot(i);
                if (slot.HasValue ? !slot.Locked : i < unlockedCount)
                {
                    return true;
                }
            }

            return false;
        }

        // 오직 "첫 번째 슬롯"만 자동으로 채운다. 2단계 이후 새로 해금되는 슬롯은 개방만 되고
        // 옵션은 비어있는 상태로 유지되며(자동 배정 없음), 이미 값이 있으면 아무 것도 하지 않는다.
        public static async Task EnsureInitialRollAsync()
        {
            if (!IsReady || rollInFlight || GetSlot(0).HasValue)
            {
                return;
            }

            rollInFlight = true;
            try
            {
                var roll = CommanderPotentialOptionTable.Roll(EquipmentBalanceConfig.RuntimeDefault);
                await progress.TryApplyAndSaveAsync(
                    GameProgressChange.AssignCommanderPotentialSlot(0, roll.Type, roll.Grade, roll.Value));
            }
            finally
            {
                rollInFlight = false;
            }
        }

        // "잠재 능력 변경"(ButtonArea_2) 버튼: 강화석 1개를 소모해 잠기지 않은 채워진 슬롯은 전부
        // 완전히 새로 뽑고(옵션 종류·등급·수치 모두 다시 추첨), 해금됐지만 아직 비어있는("대기 중")
        // 슬롯은 이 클릭으로 처음 배정된다. 잠긴 슬롯과 아직 해금되지 않은 슬롯은 건너뛴다.
        public static async Task<bool> TryRerollAsync()
        {
            if (!IsReady || rerollInFlight)
            {
                return false;
            }

            var balance = EquipmentBalanceConfig.RuntimeDefault;
            var rng = new Random(); // 슬롯마다 새로 만들면 같은 틱에 같은 값이 나올 수 있어 하나만 만들어 공유한다.
            var unlockedCount = UnlockedSlotCount;
            var entries = new List<CommanderPotentialRerollEntry>(CommanderPotentialData.SlotCount);
            for (var i = 0; i < CommanderPotentialData.SlotCount; i++)
            {
                var slot = GetSlot(i);
                if (slot.HasValue)
                {
                    if (slot.Locked)
                    {
                        continue; // 잠긴 슬롯은 재추첨 대상에서 제외
                    }
                }
                else if (i >= unlockedCount)
                {
                    continue; // 아직 해금되지 않은 슬롯은 대상이 아님
                }

                var roll = CommanderPotentialOptionTable.Roll(balance, rng);
                entries.Add(new CommanderPotentialRerollEntry(i, roll.Type, roll.Grade, roll.Value));
            }

            if (entries.Count == 0)
            {
                return false; // 재추첨/배정 대상 슬롯 없음(전부 잠겨있거나 아직 해금되지 않음)
            }

            rerollInFlight = true;
            try
            {
                return await progress.TryApplyAndSaveAsync(GameProgressChange.RerollCommanderPotentialSlots(entries));
            }
            finally
            {
                rerollInFlight = false;
            }
        }

        // "옵션 스탯 변경"(ButtonArea_1) 버튼: 강화석 1개를 소모해 채워진 슬롯들의 옵션 종류·등급은
        // 그대로 두고 같은 등급 범위 안에서 수치만 다시 뽑는다. 잠금은 옵션 자체가 바뀌는 "잠재 능력 변경"만
        // 막는 용도라, 잠긴 슬롯도 여기서는 대상이 된다(값이 있으면 잠금 여부와 무관하게 적용).
        public static async Task<bool> TryRerollValueAsync()
        {
            if (!IsReady || rerollInFlight)
            {
                return false;
            }

            var balance = EquipmentBalanceConfig.RuntimeDefault;
            var rng = new Random();
            var entries = new List<CommanderPotentialRerollEntry>(CommanderPotentialData.SlotCount);
            for (var i = 0; i < CommanderPotentialData.SlotCount; i++)
            {
                var slot = GetSlot(i);
                if (!slot.HasValue)
                {
                    continue;
                }

                var range = CommanderPotentialOptionTable.GetOption(slot.OptionType, slot.Grade, balance);
                var newValue = range.MinValue + (float)rng.NextDouble() * (range.MaxValue - range.MinValue);
                entries.Add(new CommanderPotentialRerollEntry(i, slot.OptionType, slot.Grade, newValue));
            }

            if (entries.Count == 0)
            {
                return false; // 대상 슬롯 없음(전부 비어있음)
            }

            rerollInFlight = true;
            try
            {
                return await progress.TryApplyAndSaveAsync(GameProgressChange.RerollCommanderPotentialValues(entries));
            }
            finally
            {
                rerollInFlight = false;
            }
        }

        // 자물쇠 아이콘 클릭: 값이 있는 슬롯의 잠금 상태를 반대로 토글한다.
        public static async Task<bool> ToggleLockAsync(int index)
        {
            if (!IsReady || lockToggleInFlight)
            {
                return false;
            }

            var slot = GetSlot(index);
            if (!slot.HasValue)
            {
                return false; // 빈 슬롯은 잠글 필요 없음
            }

            lockToggleInFlight = true;
            try
            {
                return await progress.TryApplyAndSaveAsync(
                    GameProgressChange.SetCommanderPotentialLocked(index, slot.Locked, !slot.Locked));
            }
            finally
            {
                lockToggleInFlight = false;
            }
        }
    }
}
