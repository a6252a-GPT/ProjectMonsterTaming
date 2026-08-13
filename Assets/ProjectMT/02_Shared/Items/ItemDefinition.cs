using System;
using UnityEngine;

namespace ProjectMT.Shared.Items
{
    [CreateAssetMenu(menuName = "ProjectMT/Items/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : ScriptableObject // 일반 아이템 한 종류의 고정 정보
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [TextArea(2, 5)]
        [SerializeField] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private ItemCategory category;
        [SerializeField] private long maxQuantity = long.MaxValue;
        [SerializeField] private bool discardable;
        [SerializeField] private bool allowMultiUse;
        [SerializeField] private ItemUseEffect useEffect;
        [SerializeField] private int sortOrder;

        public string ItemId => itemId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ItemId : displayName;
        public string Description => description ?? string.Empty;
        public Sprite Icon => icon;
        public ItemCategory Category => category;
        public long MaxQuantity => maxQuantity;
        public bool IsUnique => maxQuantity == 1L;
        public bool IsStackable => maxQuantity > 1L;
        public bool IsDiscardable => discardable;
        public bool IsUsable => useEffect != null;
        public bool AllowMultiUse => IsUsable && IsStackable && allowMultiUse;
        public ItemUseEffect UseEffect => useEffect;
        public int SortOrder => sortOrder;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                error = $"Item ID is blank. Asset={name}";
                return false;
            }

            if (!Enum.IsDefined(typeof(ItemCategory), category))
            {
                error = $"Item category is invalid. Item={itemId}";
                return false;
            }

            if (maxQuantity <= 0L)
            {
                error = $"Item max quantity must be positive. Item={itemId}";
                return false;
            }

            if (allowMultiUse && (useEffect == null || maxQuantity <= 1L))
            {
                error = $"Multi-use requires a stackable usable item. Item={itemId}";
                return false;
            }

            if (useEffect != null && !useEffect.TryValidate(out error))
            {
                error = $"Item use effect is invalid. Item={itemId}, Error={error}";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string itemName,
            ItemCategory itemCategory,
            long maximumQuantity,
            bool canDiscard,
            bool canUseMultiple,
            ItemUseEffect effect,
            int order = 0)
        {
            itemId = id?.Trim();
            displayName = itemName?.Trim();
            category = itemCategory;
            maxQuantity = maximumQuantity;
            discardable = canDiscard;
            allowMultiUse = canUseMultiple;
            useEffect = effect;
            sortOrder = order;
        }

        public void EditorSetPresentation(string itemDescription, Sprite itemIcon = null)
        {
            description = itemDescription?.Trim();
            icon = itemIcon;
        }
#endif
    }
}
