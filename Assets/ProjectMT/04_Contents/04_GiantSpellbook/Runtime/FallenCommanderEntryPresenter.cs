using System;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.FallenCommander
{
    [DisallowMultipleComponent]
    public sealed class FallenCommanderEntryPresenter : MonoBehaviour
    {
        private GameObject panel;
        private Text dungeonNameText;
        private Text stageText;
        private Button enterButton;
        private Button exitButton;
        private Font uiFont;

        public static FallenCommanderEntryPresenter Create(Transform owner)
        {
            var existingPresenters = owner.GetComponentsInChildren<FallenCommanderEntryPresenter>(true);
            foreach (var existingPresenter in existingPresenters)
            {
                if (existingPresenter == null)
                {
                    continue;
                }

                existingPresenter.gameObject.SetActive(false);
                Destroy(existingPresenter.gameObject);
            }

            var root = new GameObject("FallenCommanderEntryPresenter_Runtime");
            root.transform.SetParent(owner, false);

            var presenter = root.AddComponent<FallenCommanderEntryPresenter>();
            presenter.uiFont = FindContentFont(owner);
            presenter.Build();
            return presenter;
        }

        public void Show(
            string dungeonName,
            int stage,
            Action onEnter,
            Action onExit)
        {
            dungeonNameText.text = dungeonName;
            stageText.text = $"현재 단계  {Mathf.Max(1, stage)}단계";

            enterButton.onClick.RemoveAllListeners();
            enterButton.onClick.AddListener(() =>
            {
                panel.SetActive(false);
                onEnter?.Invoke();
            });

            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(() =>
            {
                panel.SetActive(false);
                onExit?.Invoke();
            });

            panel.SetActive(true);
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            panel = CreateObject("EntryPanel", transform);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var backdrop = panel.AddComponent<Image>();
            backdrop.color = new Color(0.015f, 0.02f, 0.035f, 1f);

            var card = CreateObject("EntryCard", panel.transform);
            var cardRect = card.AddComponent<RectTransform>();
            SetRect(cardRect, Vector2.zero, new Vector2(820f, 470f));
            var cardImage = card.AddComponent<Image>();
            cardImage.color = new Color(0.055f, 0.075f, 0.11f, 0.98f);

            var categoryText = CreateText(
                "Category",
                card.transform,
                24,
                TextAnchor.MiddleCenter);
            categoryText.text = "성장 던전";
            categoryText.color = new Color(1f, 0.7f, 0.16f, 1f);
            SetRect(
                categoryText.rectTransform,
                new Vector2(0f, 165f),
                new Vector2(700f, 45f));

            dungeonNameText = CreateText(
                "DungeonName",
                card.transform,
                48,
                TextAnchor.MiddleCenter);
            SetRect(
                dungeonNameText.rectTransform,
                new Vector2(0f, 95f),
                new Vector2(720f, 80f));

            stageText = CreateText(
                "Stage",
                card.transform,
                32,
                TextAnchor.MiddleCenter);
            SetRect(
                stageText.rectTransform,
                new Vector2(0f, 15f),
                new Vector2(600f, 60f));

            enterButton = CreateButton(
                "EnterButton",
                card.transform,
                "입장하기",
                new Vector2(165f, -135f),
                new Color(1f, 0.68f, 0.08f, 1f),
                Color.black);

            exitButton = CreateButton(
                "ExitButton",
                card.transform,
                "나가기",
                new Vector2(-165f, -135f),
                new Color(0.22f, 0.26f, 0.32f, 1f),
                Color.white);

            panel.SetActive(false);
        }

        private Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 position,
            Color backgroundColor,
            Color textColor)
        {
            var buttonObject = CreateObject(name, parent);
            var buttonRect = buttonObject.AddComponent<RectTransform>();
            SetRect(buttonRect, position, new Vector2(280f, 74f));

            var buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = backgroundColor;

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;

            var buttonLabel = CreateText(
                "Label",
                buttonObject.transform,
                28,
                TextAnchor.MiddleCenter);
            buttonLabel.text = label;
            buttonLabel.color = textColor;
            var labelRect = buttonLabel.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return button;
        }

        private static GameObject CreateObject(string name, Transform parent)
        {
            var result = new GameObject(name);
            result.transform.SetParent(parent, false);
            return result;
        }

        private Text CreateText(
            string name,
            Transform parent,
            int fontSize,
            TextAnchor alignment)
        {
            var result = CreateObject(name, parent).AddComponent<Text>();
            result.font = uiFont;
            result.font ??= Font.CreateDynamicFontFromOSFont(
                "Malgun Gothic",
                fontSize);
            result.font ??= Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            result.fontSize = fontSize;
            result.alignment = alignment;
            result.color = Color.white;
            return result;
        }

        private static Font FindContentFont(Transform owner)
        {
            if (owner == null)
            {
                return null;
            }

            var texts = owner.GetComponentsInChildren<Text>(true);
            Font fallbackFont = null;
            foreach (var text in texts)
            {
                if (text.font == null)
                {
                    continue;
                }

                fallbackFont ??= text.font;
                if (!string.Equals(
                        text.font.name,
                        "LegacyRuntime",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return text.font;
                }
            }

            return fallbackFont;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
