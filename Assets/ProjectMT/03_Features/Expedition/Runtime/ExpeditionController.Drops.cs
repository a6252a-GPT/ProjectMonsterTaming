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
                return;
            }

            worldItemDrops.Initialize(progress, itemCatalog, visualCatalog, pickupTarget, Camera.main);
        }

        private void ConfigureEquipmentWorldDrops(Transform pickupTarget)
        {
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
                return;
            }

            equipmentWorldDrops.Initialize(progress, visualCatalog, pickupTarget, Camera.main);
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

            var instance = EquipmentDropRoller.RollSingle(equipmentBalanceConfig, equipmentRandom);
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
    }
}
