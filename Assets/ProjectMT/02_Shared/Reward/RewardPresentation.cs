using System;
using System.Collections.Generic;
using ProjectMT.Shared.Items;
using UnityEngine;

namespace ProjectMT.Shared.Reward
{
    public enum RewardPresentationKind // 지급 로직과 분리된 표시 종류
    {
        Gold,
        CommanderExperience,
        Item
    }

    public readonly struct RewardPresentationItem // 저장 성공 뒤 보여줄 읽기 전용 값
    {
        public RewardPresentationItem(
            RewardPresentationKind kind,
            string label,
            long amount,
            string itemId = null,
            Sprite icon = null,
            int equipmentLevel = 0,
            string equipmentInstanceId = null)
        {
            Kind = kind;
            Label = label ?? string.Empty;
            Amount = Math.Max(0L, amount);
            ItemId = itemId ?? string.Empty;
            Icon = icon;
            EquipmentLevel = Math.Max(0, equipmentLevel);
            EquipmentInstanceId = equipmentInstanceId ?? string.Empty;
        }

        public RewardPresentationKind Kind { get; }
        public string Label { get; }
        public long Amount { get; }
        public string ItemId { get; }
        public Sprite Icon { get; }
        public int EquipmentLevel { get; }
        public string EquipmentInstanceId { get; }
        public bool IsEquipment => EquipmentLevel > 0 && !string.IsNullOrEmpty(EquipmentInstanceId);
        public bool IsValid => Amount > 0L;
    }

    public sealed class RewardPresentationRequest // 실제 지급 권한이 없는 연출 전용 묶음
    {
        private readonly RewardPresentationItem[] items;

        public RewardPresentationRequest(params RewardPresentationItem[] rewardItems)
        {
            if (rewardItems == null || rewardItems.Length == 0)
            {
                items = Array.Empty<RewardPresentationItem>();
                return;
            }

            var validItems = new List<RewardPresentationItem>(rewardItems.Length);
            for (var i = 0; i < rewardItems.Length; i++)
            {
                if (rewardItems[i].IsValid)
                {
                    validItems.Add(rewardItems[i]);
                }
            }

            items = validItems.ToArray();
        }

        public IReadOnlyList<RewardPresentationItem> Items => items;
        public bool IsEmpty => items.Length == 0;

        public static RewardPresentationRequest Gold(long amount, ItemCatalog itemCatalog = null)
        {
            return new RewardPresentationRequest(
                new RewardPresentationItem(
                    RewardPresentationKind.Gold,
                    "골드",
                    amount,
                    ItemIds.Gold,
                    ResolveItemIcon(itemCatalog, ItemIds.Gold)));
        }

        public static RewardPresentationRequest FromBundle(
            RewardBundle bundle,
            ItemCatalog itemCatalog = null)
        {
            if (bundle == null)
            {
                return new RewardPresentationRequest();
            }

            var rewardItems = new List<RewardPresentationItem>(2 + bundle.Items.Count)
            {
                new RewardPresentationItem(
                    RewardPresentationKind.Gold,
                    "골드",
                    bundle.Gold,
                    ItemIds.Gold,
                    ResolveItemIcon(itemCatalog, ItemIds.Gold)),
                new RewardPresentationItem(
                    RewardPresentationKind.CommanderExperience,
                    "군단장 경험치",
                    bundle.CommanderExperience)
            };
            for (var index = 0; index < bundle.Items.Count; index++)
            {
                var itemReward = bundle.Items[index];
                if (!itemReward.IsValid)
                {
                    continue;
                }

                var label = ItemIds.GetFallbackDisplayName(itemReward.ItemId);
                Sprite icon = null;
                if (itemCatalog != null && itemCatalog.TryGet(itemReward.ItemId, out var definition))
                {
                    label = definition.DisplayName;
                    icon = definition.Icon;
                }

                rewardItems.Add(new RewardPresentationItem(
                    RewardPresentationKind.Item,
                    label,
                    itemReward.Amount,
                    itemReward.ItemId,
                    icon));
            }

            return new RewardPresentationRequest(rewardItems.ToArray());
        }

        private static Sprite ResolveItemIcon(ItemCatalog itemCatalog, string itemId)
        {
            return itemCatalog != null && itemCatalog.TryGet(itemId, out var definition)
                ? definition.Icon
                : null;
        }
    }

    public interface IRewardPresentationPlayer // 저장 성공 뒤 Bootstrap이 호출하는 표현 계약
    {
        void PlayConfirmed(RewardPresentationRequest request);

        // HUD 버튼 클릭 위치 등 기본 SpawnAnchor 대신 특정 지점에서 시작하는 연출.
        void PlayConfirmed(RewardPresentationRequest request, Vector3 worldSpawnPosition);
    }
}
