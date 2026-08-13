using System;
using UnityEngine;

namespace ProjectMT.Shared.Items
{
    [Serializable]
    public struct ItemAmount // 보상·드랍·우편이 공유하는 ID·수량 값
    {
        [SerializeField] private string itemId;
        [SerializeField] private long amount;

        public ItemAmount(string itemId, long amount)
        {
            this.itemId = itemId?.Trim();
            this.amount = amount;
        }

        public string ItemId => itemId ?? string.Empty;
        public long Amount => amount;
        public bool IsValid => !string.IsNullOrWhiteSpace(itemId) && amount > 0L;
    }
}
