using System.Collections.Generic;

namespace ProjectMT.Shared.Items
{
    public static class ItemInventoryTransactions // 저장 후보가 재사용할 순수 복제·검증 경계
    {
        public static bool TryGrant(
            ItemInventoryData source,
            IReadOnlyList<ItemAmount> grants,
            ItemCatalog catalog,
            out ItemInventoryData candidate)
        {
            candidate = (source ?? ItemInventoryData.CreateDefault()).Clone();
            candidate.Repair();
            if (candidate.TryGrant(grants, catalog))
            {
                return true;
            }

            candidate = null;
            return false;
        }

        public static bool TryGrantCoreBalance(
            ItemInventoryData source,
            string itemId,
            long amount,
            out ItemInventoryData candidate)
        {
            candidate = (source ?? ItemInventoryData.CreateDefault()).Clone();
            candidate.Repair();
            if (candidate.TryGrantCoreBalance(itemId, amount))
            {
                return true;
            }

            candidate = null;
            return false;
        }

        public static bool TrySpend(
            ItemInventoryData source,
            IReadOnlyList<ItemAmount> costs,
            ItemCatalog catalog,
            out ItemInventoryData candidate)
        {
            candidate = (source ?? ItemInventoryData.CreateDefault()).Clone();
            candidate.Repair();
            if (candidate.TrySpend(costs, catalog))
            {
                return true;
            }

            candidate = null;
            return false;
        }

        public static bool TryDiscard(
            ItemInventoryData source,
            ItemCatalog catalog,
            string itemId,
            long quantity,
            long expectedQuantity,
            out ItemInventoryData candidate)
        {
            candidate = null;
            if (catalog == null ||
                !catalog.TryGet(itemId, out var definition) ||
                !definition.IsDiscardable)
            {
                return false;
            }

            var working = (source ?? ItemInventoryData.CreateDefault()).Clone();
            working.Repair();
            if (!working.TryRemove(itemId, quantity, expectedQuantity))
            {
                return false;
            }

            candidate = working;
            return true;
        }

        public static bool TryUse(
            ItemInventoryData source,
            ItemCatalog catalog,
            string itemId,
            long quantity,
            long expectedQuantity,
            out ItemInventoryData candidate,
            out ItemUseResult result)
        {
            candidate = null;
            result = null;
            if (catalog == null ||
                !catalog.TryGet(itemId, out var definition) ||
                !definition.IsUsable ||
                quantity <= 0L ||
                (quantity > 1L && !definition.AllowMultiUse) ||
                !definition.UseEffect.TryCreateResult(quantity, out result, out _) ||
                result == null || result.IsEmpty)
            {
                result = null;
                return false;
            }

            var working = (source ?? ItemInventoryData.CreateDefault()).Clone();
            working.Repair();
            if (!working.TryRemove(itemId, quantity, expectedQuantity))
            {
                result = null;
                return false;
            }

            candidate = working;
            return true;
        }
    }
}
