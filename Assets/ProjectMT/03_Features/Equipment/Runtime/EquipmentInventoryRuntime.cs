using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;

namespace ProjectMT.Features.Equipment
{
    // 08.10 안건준 추가 - 인벤토리 아이템 1개를 UI가 바로 쓸 수 있는 형태로 묶은 읽기 전용 값.
    // 보유 인스턴스(옵션 포함) + 부위·등급이 결정하는 고정 정보(아이콘·핵심 능력치) + 현재 장착 여부.
    public readonly struct EquipmentItemView
    {
        public EquipmentItemView(EquipmentInstanceData instance, EquipmentDefinition definition, bool isEquipped)
        {
            Instance = instance;
            Definition = definition;
            IsEquipped = isEquipped;
        }

        public EquipmentInstanceData Instance { get; }
        public EquipmentDefinition Definition { get; }
        public bool IsEquipped { get; }
        public bool IsLocked => Instance?.IsLocked ?? false;

        public string InstanceId => Instance?.InstanceId ?? string.Empty;
        public EquipmentPart Part => Instance?.Part ?? default;
        public EquipmentGrade Grade => Instance?.Grade ?? default;
    }

    // 08.10 안건준 재작성 - 보유 장비 인벤토리 + 장착 상태 조회용 파사드.
    //
    // 실제 데이터는 더 이상 이 클래스가 직접 들고 있지 않고 GameProgressData(EquipmentSaveData) 쪽에
    // 영구 저장된다. 이 클래스는 IGameProgressService를 통해 그 데이터를 읽기 쉬운 형태로 노출하고,
    // 변경 요청(획득/장착/해제)을 GameProgressChange로 변환해 저장까지 맡기는 역할만 한다.
    // "저장 데이터 초기화" 디버그 기능을 쓰면 장비도 함께 초기화된다(GameProgressData.CreateDefault() 참고).
    public static class EquipmentInventoryRuntime
    {
        public const int MaxTotalQuantity = EquipmentSaveData.MaxTotalQuantity;

        private static IGameProgressService progress;
        private static EquipmentCatalog catalog;
        private static EquipmentBalanceConfig balance;

        // 인벤토리·장착 상태가 바뀔 때(획득/장착/해제/로드 완료 등) 알림. UI가 구독해 새로 그린다.
        public static event Action Changed;

        public static void Configure(IGameProgressService progressService, EquipmentCatalog equipmentCatalog)
        {
            Configure(progressService, equipmentCatalog, EquipmentBalanceConfig.RuntimeDefault);
        }

        public static void Configure(
            IGameProgressService progressService,
            EquipmentCatalog equipmentCatalog,
            EquipmentBalanceConfig equipmentBalance)
        {
            if (progress != null)
            {
                progress.Changed -= HandleProgressChanged;
            }

            progress = progressService;
            catalog = equipmentCatalog;
            balance = equipmentBalance;

            if (progress != null)
            {
                progress.Changed += HandleProgressChanged;
            }

            Changed?.Invoke();
        }

        private static bool IsReady => progress != null && progress.IsLoaded && catalog != null && balance != null;

        private static void HandleProgressChanged() => Changed?.Invoke();

        public static IReadOnlyList<EquipmentItemView> GetItems()
        {
            if (!IsReady)
            {
                return Array.Empty<EquipmentItemView>();
            }

            var equipmentView = progress.View.Equipment;
            var instances = equipmentView.Instances;
            var result = new List<EquipmentItemView>(instances.Count);
            for (var i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                if (instance == null)
                {
                    continue;
                }

                var definition = catalog.GetDefinitionForPart(instance.Part, instance.Grade, balance);
                var equippedId = equipmentView.GetEquippedInstanceId(instance.Part);
                result.Add(new EquipmentItemView(instance, definition, equippedId == instance.InstanceId));
            }

            return result;
        }

        public static int TotalQuantity => IsReady ? progress.View.Equipment.Instances.Count : 0;

        public static bool TryGetItem(string instanceId, out EquipmentItemView item)
        {
            if (!string.IsNullOrEmpty(instanceId))
            {
                var items = GetItems();
                for (var i = 0; i < items.Count; i++)
                {
                    if (items[i].InstanceId == instanceId)
                    {
                        item = items[i];
                        return true;
                    }
                }
            }

            item = default;
            return false;
        }

        public static bool TryGetEquipped(EquipmentPart part, out EquipmentItemView item)
        {
            if (IsReady && progress.View.Equipment.TryGetEquipped(part, out var instance))
            {
                var definition = catalog.GetDefinitionForPart(instance.Part, instance.Grade, balance);
                item = new EquipmentItemView(instance, definition, true);
                return true;
            }

            item = default;
            return false;
        }

        public static bool IsPartEquipped(EquipmentPart part) => TryGetEquipped(part, out _);

        public static IReadOnlyList<string> GetDismantleCandidateIds(EquipmentGrade maximumGrade)
        {
            var items = GetItems();
            var result = new List<string>();
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                if (!item.IsEquipped && !item.IsLocked && item.Grade <= maximumGrade)
                {
                    result.Add(item.InstanceId);
                }
            }

            return result;
        }

        // 장비 드랍 결과(보통 6개)를 인벤토리에 추가한다(최대 보유 수량을 넘는 초과분은 조용히 버림).
        public static async Task<bool> TryAcquireDropAsync(List<EquipmentInstanceData> drops)
        {
            if (!IsReady || drops == null || drops.Count == 0)
            {
                return false;
            }

            return await progress.TryApplyAndSaveAsync(GameProgressChange.AcquireEquipment(drops));
        }

        // 지정한 인스턴스를 장착한다. 이미 그 부위에 다른 장비가 장착돼 있으면 자동으로 교체된다.
        public static async Task<bool> TryEquipAsync(string instanceId)
        {
            if (!IsReady || string.IsNullOrEmpty(instanceId))
            {
                return false;
            }

            return await progress.TryApplyAndSaveAsync(GameProgressChange.EquipItem(instanceId));
        }

        public static async Task<bool> TryUnequipAsync(EquipmentPart part)
        {
            if (!IsReady)
            {
                return false;
            }

            return await progress.TryApplyAndSaveAsync(GameProgressChange.UnequipItem(part));
        }

        public static async Task<bool> TrySetLockedAsync(string instanceId, bool nextValue)
        {
            if (!IsReady || !TryGetItem(instanceId, out var item))
            {
                return false;
            }

            if (item.IsLocked == nextValue)
            {
                return true;
            }

            return await progress.TryApplyAndSaveAsync(
                GameProgressChange.SetEquipmentLock(instanceId, item.IsLocked, nextValue));
        }

        public static async Task<bool> TryDismantleAsync(IReadOnlyCollection<string> instanceIds)
        {
            if (!IsReady || instanceIds == null || instanceIds.Count == 0)
            {
                return false;
            }

            var copiedIds = new List<string>(instanceIds.Count);
            foreach (var instanceId in instanceIds)
            {
                copiedIds.Add(instanceId);
            }

            return await progress.TryApplyAndSaveAsync(GameProgressChange.DismantleEquipment(copiedIds));
        }
    }
}
