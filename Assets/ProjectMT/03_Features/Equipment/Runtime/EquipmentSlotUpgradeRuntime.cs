using System;
using System.Threading.Tasks;
using ProjectMT.Features.Quest;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Quest;

namespace ProjectMT.Features.Equipment
{
    // 장비 부위 슬롯의 영구 강화 레벨 조회·강화 요청 파사드(GameProgressData 연동).
    public static class EquipmentSlotUpgradeRuntime
    {
        private static IGameProgressService progress;

        // 슬롯 레벨이 바뀔 때(강화/로드 완료 등) 알림. UI가 구독해 새로 그린다.
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

        // 부위별 현재 슬롯 레벨(0부터 시작, "+N" 표시값과 동일).
        public static int GetLevel(EquipmentPart part) => IsReady ? progress.View.EquipmentSlotUpgrade.GetLevel(part) : 0;

        // 전체 부위 레벨 합("TotalText" 표시용).
        public static int TotalLevel => IsReady ? progress.View.EquipmentSlotUpgrade.TotalLevel : 0;

        // 장비 슬롯 강화석 보유량.
        public static long EnhancementStoneBalance =>
            IsReady && progress.View.Items.TryGetQuantity(ItemIds.EquipmentSlotUpgradeStone, out var quantity)
                ? quantity
                : 0L;

        // 현재 레벨 기준 공격력/체력/방어력 보너스(%). 장갑·장신구처럼 미지원 부위는 항상 Zero.
        public static EquipmentSlotUpgradeBonus GetBonus(EquipmentPart part) =>
            EquipmentSlotUpgradeCalculator.GetBonus(part, GetLevel(part));

        public static long GetNextGoldCost(EquipmentPart part) => EquipmentSlotUpgradeCalculator.GetNextGoldCost(GetLevel(part));

        public static int GetNextStoneCost(EquipmentPart part) => EquipmentSlotUpgradeCalculator.GetNextStoneCost(GetLevel(part));

        // 지정한 부위의 슬롯을 +1 강화한다. 비용 확인·차감은 GameProgressData.TryApply에서 처리된다.
        public static async Task<bool> TryUpgradeAsync(EquipmentPart part)
        {
            if (!IsReady || !EquipmentSlotUpgradeCalculator.IsSlotUpgradeSupported(part))
            {
                return false;
            }

            var expectedLevel = GetLevel(part);
            var saved = await progress.TryApplyAndSaveAsync(GameProgressChange.UpgradeEquipmentSlot(part, expectedLevel));
            if (saved)
            {
                _ = QuestRuntime.AdvanceAllOfConditionAsync(QuestConditionType.EquipmentEnhance, 1L);
            }

            return saved;
        }
    }
}
