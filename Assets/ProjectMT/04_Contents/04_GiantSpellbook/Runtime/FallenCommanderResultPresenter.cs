using System;
using ProjectMT.Contents.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.FallenCommander
{
    [DisallowMultipleComponent]
    public sealed class FallenCommanderResultPresenter : MonoBehaviour
    {
        private GameObject panel;
        private Text titleText;
        private Text resultText;
        private Button exitButton;
        private Font uiFont;

        public static FallenCommanderResultPresenter Create(Transform owner)
        {
            var root = new GameObject("FallenCommanderResultPresenter_Runtime");
            root.transform.SetParent(owner, false);

            var presenter = root.AddComponent<FallenCommanderResultPresenter>();
            presenter.uiFont = FindContentFont(owner);
            presenter.Build();
            return presenter;
        }

        public void Show(
            ContentOutcome outcome,
            int score,
            float remainingTime,
            Action onExit)
        {
            var seconds = Mathf.CeilToInt(Mathf.Max(0f, remainingTime));
            titleText.text = outcome == ContentOutcome.Complete
                ? "전투 승리"
                : "전투 패배";
            resultText.text =
                $"최종 점수  {score}\n남은 시간  {seconds / 60:00}:{seconds % 60:00}";

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

            panel = CreateObject("ResultPanel", transform);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var backdrop = panel.AddComponent<Image>();
            backdrop.color = new Color(0.015f, 0.02f, 0.035f, 0.92f);

            var card = CreateObject("ResultCard", panel.transform);
            var cardRect = card.AddComponent<RectTransform>();
            SetRect(cardRect, Vector2.zero, new Vector2(760f, 430f));
            var cardImage = card.AddComponent<Image>();
            cardImage.color = new Color(0.055f, 0.075f, 0.11f, 0.98f);

            titleText = CreateText(
                "Title",
                card.transform,
                52,
                TextAnchor.MiddleCenter);
            SetRect(
                titleText.rectTransform,
                new Vector2(0f, 120f),
                new Vector2(660f, 80f));

            resultText = CreateText(
                "ResultText",
                card.transform,
                34,
                TextAnchor.MiddleCenter);
            SetRect(
                resultText.rectTransform,
                new Vector2(0f, 15f),
                new Vector2(660f, 120f));

            var buttonObject = CreateObject("ExitButton", card.transform);
            var buttonRect = buttonObject.AddComponent<RectTransform>();
            SetRect(
                buttonRect,
                new Vector2(0f, -130f),
                new Vector2(280f, 70f));
            var buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = new Color(1f, 0.68f, 0.08f, 1f);
            exitButton = buttonObject.AddComponent<Button>();
            exitButton.targetGraphic = buttonImage;

            var buttonLabel = CreateText(
                "Label",
                buttonObject.transform,
                28,
                TextAnchor.MiddleCenter);
            buttonLabel.text = "나가기";
            buttonLabel.color = Color.black;
            var labelRect = buttonLabel.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            panel.SetActive(false);
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
