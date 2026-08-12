using ProjectMT.Shared.Items;
using UnityEngine;

namespace ProjectMT.Features.WorldDrops
{
    public readonly struct WorldItemDropRequest // 드랍 원본과 표시를 잇는 최소 요청값
    {
        public WorldItemDropRequest(string itemId, long quantity, Vector3 position)
        {
            ItemId = itemId?.Trim() ?? string.Empty;
            Quantity = quantity;
            Position = position;
        }

        public string ItemId { get; }
        public long Quantity { get; }
        public Vector3 Position { get; }

        public bool IsValid =>
            new ItemAmount(ItemId, Quantity).IsValid &&
            IsFinite(Position.x) &&
            IsFinite(Position.y) &&
            IsFinite(Position.z);

        public ItemAmount ToItemAmount()
        {
            return new ItemAmount(ItemId, Quantity);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
