using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectMT.Features.Equipment;
using ProjectMT.Features.MainBattle;
using ProjectMT.Features.Quest;
using ProjectMT.Features.WorldDrops;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.Reward;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Expedition
{
    public sealed partial class ExpeditionController
    {
        private void ConfigureWorldItemDrops(ItemCatalog itemCatalog, Transform pickupTarget)
        {
            if (worldItemDrops != null)
            {
                worldItemDrops.ItemsConfirmed -= HandleWorldItemsConfirmed;
            }

            var visualCatalog = profile == null ? null : profile.WorldItemDropVisualCatalog;
            if (itemCatalog == null || visualCatalog == null || pickupTarget == null)
            {
                worldItemDrops = null;
                return;
            }

            worldItemDrops = GetComponentInChildren<WorldItemDropRuntime>(true);
            if (worldItemDrops == null)
            {
                worldItemDrops = WorldItemDropRuntime.Create(
                    transform,
                    progress,
                    itemCatalog,
                    visualCatalog,
                    pickupTarget,
                    Camera.main);
            }
            else
            {
                worldItemDrops.Initialize(progress, itemCatalog, visualCatalog, pickupTarget, Camera.main);
            }

            worldItemDrops.ItemsConfirmed -= HandleWorldItemsConfirmed;
            worldItemDrops.ItemsConfirmed += HandleWorldItemsConfirmed;
        }

        private void ConfigureEquipmentWorldDrops(Transform pickupTarget)
        {
            if (equipmentWorldDrops != null)
            {
                equipmentWorldDrops.EquipmentConfirmed -= HandleEquipmentConfirmed;
            }

            var visualCatalog = profile == null ? null : profile.EquipmentDropChestVisualCatalog;
            if (visualCatalog == null || pickupTarget == null)
            {
                equipmentWorldDrops = null;
                return;
            }

            equipmentWorldDrops = GetComponentInChildren<EquipmentWorldDropRuntime>(true);
            if (equipmentWorldDrops == null)
            {
                equipmentWorldDrops = EquipmentWorldDropRuntime.Create(
                    transform,
                    progress,
                    visualCatalog,
                    pickupTarget,
                    Camera.main);
            }
            else
            {
                equipmentWorldDrops.Initialize(progress, visualCatalog, pickupTarget, Camera.main);
            }

            equipmentWorldDrops.EquipmentConfirmed -= HandleEquipmentConfirmed;
            equipmentWorldDrops.EquipmentConfirmed += HandleEquipmentConfirmed;
        }

        private void TrySpawnNormalEnemyEquipment(Vector3 position)
        {
            if (!running || profile == null || equipmentWorldDrops == null ||
                equipmentWorldDrops.AvailableCapacity <= 0 || equipmentBalanceConfig == null)
            {
                return;
            }

            equipmentRandom ??= new System.Random();
            if (!profile.ShouldDropNormalEnemyEquipment((float)equipmentRandom.NextDouble()))
            {
                return;
            }

            var basisStage = ExpeditionEquipmentLevelResolver.ResolveRunStage(currentDifficulty, currentStage);
            var instance = EquipmentDropRoller.RollSingle(equipmentBalanceConfig, basisStage, equipmentRandom);
            equipmentWorldDrops.TrySpawn(new EquipmentWorldDropRequest(instance, position));
        }

        private void CollectAllWorldDrops()
        {
            worldItemDrops?.CollectAllActive();
            equipmentWorldDrops?.CollectAllActive();
        }

        private async Task FlushWorldDropsCheckpointAsync()
        {
            var itemDrops = worldItemDrops;
            var equipmentDrops = equipmentWorldDrops;
            if ((itemDrops == null || itemDrops.PendingItemTypeCount == 0) &&
                (equipmentDrops == null || equipmentDrops.PendingCount == 0))
            {
                return;
            }

            var itemFlush = itemDrops == null || itemDrops.PendingItemTypeCount == 0
                ? Task.FromResult(true)
                : itemDrops.FlushAsync();
            var equipmentFlush = equipmentDrops == null || equipmentDrops.PendingCount == 0
                ? Task.FromResult(true)
                : equipmentDrops.FlushAsync(); // Shutdown 참조 해제 전 두 저장 계약을 먼저 고정
            var itemSaved = await itemFlush;
            var equipmentSaved = await equipmentFlush;
            if ((!itemSaved || !equipmentSaved) && this != null)
            {
                Debug.LogWarning("월드 드랍 획득분 저장을 다음 체크포인트에서 다시 시도합니다.");
            }
        }

        private void HandleWorldItemsConfirmed(IReadOnlyList<ItemAmount> items)
        {
            if (items == null || items.Count == 0 || rewardPresentation == null)
            {
                return;
            }

            rewardPresentation.PlayConfirmed(
                RewardPresentationRequest.FromBundle(
                    new RewardBundle(0L, 0L, items),
                    itemCatalog));
        }

        private void HandleEquipmentConfirmed(IReadOnlyList<EquipmentInstanceData> equipment)
        {
            if (equipment == null || equipment.Count == 0 || rewardPresentation == null)
            {
                return;
            }

            var items = new List<RewardPresentationItem>(equipment.Count);
            for (var index = 0; index < equipment.Count; index++)
            {
                var instance = equipment[index];
                if (instance == null)
                {
                    continue;
                }

                var label = $"{EquipmentGradeInfo.GetDisplayName(instance.Grade)} " +
                            EquipmentPartInfo.GetDisplayName(instance.Part);
                Sprite icon = null;
                if (EquipmentInventoryRuntime.TryGetItem(instance.InstanceId, out var item) &&
                    item.Definition != null)
                {
                    label = item.Definition.DisplayName;
                    icon = item.Definition.Icon;
                }
                icon = EquipmentLevelIconResolver.Resolve(instance.Part, instance.ItemLevel, icon);

                items.Add(new RewardPresentationItem(
                    RewardPresentationKind.Item,
                    label,
                    1L,
                    icon: icon,
                    equipmentLevel: instance.ItemLevel,
                    equipmentInstanceId: instance.InstanceId));
            }

            if (items.Count > 0)
            {
                rewardPresentation.PlayConfirmed(new RewardPresentationRequest(items.ToArray()));
            }
        }
    }
}
