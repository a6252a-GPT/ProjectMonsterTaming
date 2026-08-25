using System;
using ProjectMT.Contents.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class CastleRaidGridModeDialog : MonoBehaviour // 사각·육각 입장 선택 팝업
    {
        public static readonly ContentVariantId SquareVariant = new ContentVariantId("square");
        public static readonly ContentVariantId HexVariant = new ContentVariantId("hex");

        [SerializeField] private GameObject dialogRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button squareButton;
        [SerializeField] private Button hexButton;
        [SerializeField] private Button cancelButton;

        private Action<ContentVariantId> selected;
        private bool listenersBound;

        public bool IsOpen => ResolveRoot().activeSelf;

        private void Awake()
        {
            BindListeners();
        }

        public void Show(Action<ContentVariantId> onSelected)
        {
            BindListeners();
            selected = onSelected;
            if (titleText != null)
            {
                titleText.text = "군단의 역습 전장 선택";
            }

            ResolveRoot().SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            selected = null;
            ResolveRoot().SetActive(false);
        }

        private void Select(ContentVariantId variantId)
        {
            if (!IsOpen)
            {
                return;
            }

            var callback = selected;
            Hide();
            callback?.Invoke(variantId);
        }

        private void BindListeners()
        {
            if (listenersBound)
            {
                return;
            }

            if (squareButton == null || hexButton == null || cancelButton == null)
            {
                throw new InvalidOperationException("군단의 역습 전장 선택 버튼이 불완전합니다.");
            }

            squareButton.onClick.AddListener(() => Select(SquareVariant));
            hexButton.onClick.AddListener(() => Select(HexVariant));
            cancelButton.onClick.AddListener(Hide);
            listenersBound = true;
        }

        private GameObject ResolveRoot()
        {
            return dialogRoot != null ? dialogRoot : gameObject;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            GameObject root,
            TMP_Text title,
            Button square,
            Button hex,
            Button cancel)
        {
            dialogRoot = root;
            titleText = title;
            squareButton = square;
            hexButton = hex;
            cancelButton = cancel;
        }
#endif
    }
}
