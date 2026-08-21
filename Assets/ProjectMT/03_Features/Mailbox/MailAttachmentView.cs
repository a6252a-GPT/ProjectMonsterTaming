using ProjectMT.Shared.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Mailbox
{
    [DisallowMultipleComponent]
    public sealed class MailAttachmentView : MonoBehaviour // 우편 첨부 아이템 표시
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text amountText;

        public void Bind(ItemAmount item, ItemCatalog itemCatalog)
        {
            gameObject.SetActive(true);
            ItemDefinition definition = null;
            var hasDefinition = itemCatalog != null && itemCatalog.TryGet(item.ItemId, out definition);
            if (icon != null)
            {
                icon.sprite = hasDefinition ? definition.Icon : null;
                icon.enabled = icon.sprite != null;
            }

            if (nameText != null)
            {
                nameText.text = hasDefinition ? definition.DisplayName : ItemIds.GetFallbackDisplayName(item.ItemId);
            }

            if (amountText != null)
            {
                amountText.text = $"× {item.Amount:N0}";
            }
        }

        public void Clear()
        {
            gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        public void EditorConfigure(Image itemIcon, TMP_Text itemName, TMP_Text amount)
        {
            icon = itemIcon;
            nameText = itemName;
            amountText = amount;
        }
#endif
    }
}
