using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ProjectMT.Contents.TreasureSpirit;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal sealed class DemoChestQuizOverlay : MonoBehaviour
    {
        private const int OverlaySortOrder = 80;
        private const float MemoryPreviewSeconds = 3f;

        private static DemoChestQuizOverlay activeOverlay;

        private Action onSolved;
        private Action onClosedWithoutSolve;
        private PlayerCharacterController playerMove;
        private TMP_FontAsset font;
        private TMP_Text titleText;
        private TMP_Text promptText;
        private TMP_Text feedbackText;
        private Button[] choiceButtons = Array.Empty<Button>();
        private TMP_Text[] choiceLabels = Array.Empty<TMP_Text>();
        private List<DemoChestQuizQuestion> questions;
        private int questionIndex;
        private bool solved;
        private bool closing;
        private bool waitingForMemory;
        private Coroutine memoryRoutine;

        public static bool IsOpen => activeOverlay != null;

        public static void Show(
            DemoDungeonDifficulty difficulty,
            Transform player,
            Action solved,
            Action closedWithoutSolve)
        {
            HideActive();

            GameObject root = new GameObject("DemoChestQuizOverlay", typeof(RectTransform));
            DemoChestQuizOverlay overlay = root.AddComponent<DemoChestQuizOverlay>();
            overlay.onSolved = solved;
            overlay.onClosedWithoutSolve = closedWithoutSolve;
            overlay.playerMove = ResolvePlayer(player);
            overlay.Build(difficulty);
            overlay.playerMove?.SetInputEnabled(false);
            DemoDungeonController.Active?.SetGameplayPaused(true);
            DemoDungeonAudio.PlayQuizUi();
            activeOverlay = overlay;
        }

        private static PlayerCharacterController ResolvePlayer(Transform player)
        {
            if (player != null)
            {
                PlayerCharacterController fromTransform = player.GetComponent<PlayerCharacterController>();
                if (fromTransform != null)
                {
                    return fromTransform;
                }
            }

            Transform activePlayer = DemoDungeonController.Active != null
                ? DemoDungeonController.Active.PlayerTransform
                : null;
            return activePlayer != null ? activePlayer.GetComponent<PlayerCharacterController>() : null;
        }

        public static void HideActive()
        {
            if (activeOverlay == null)
            {
                return;
            }

            activeOverlay.Close(false);
        }

        private void Build(DemoDungeonDifficulty difficulty)
        {
            questions = DemoChestQuizCatalog.CreateRound(difficulty);
            font = ResolveFont();
            EnsureEventSystem();

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortOrder;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            RectTransform rootRect = (RectTransform)transform;
            rootRect.sizeDelta = Vector2.zero;

            CreateDimmer(rootRect);
            RectTransform panel = CreatePanel(rootRect);

            titleText = CreateLayoutText(
                "Title",
                panel,
                font,
                32f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                44f);
            titleText.text = "보물상자 퀴즈";
            titleText.color = new Color(1f, 0.86f, 0.45f);

            promptText = CreateLayoutText(
                "Prompt",
                panel,
                font,
                30f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                150f);
            promptText.overflowMode = TextOverflowModes.Ellipsis;

            BuildChoiceButtons(panel);

            feedbackText = CreateLayoutText(
                "Feedback",
                panel,
                font,
                22f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                36f);
            feedbackText.text = "정답을 고르면 열쇠를 얻습니다.";
            feedbackText.color = new Color(0.82f, 0.82f, 0.86f);

            ShowCurrentQuestion();
        }

        private void BuildChoiceButtons(RectTransform panel)
        {
            choiceButtons = new Button[3];
            choiceLabels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                int choiceIndex = i;
                GameObject buttonObject = new GameObject($"Choice_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
                RectTransform rect = buttonObject.GetComponent<RectTransform>();
                rect.SetParent(panel, false);

                LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
                layout.minHeight = 64f;
                layout.preferredHeight = 64f;

                Image image = buttonObject.GetComponent<Image>();
                image.sprite = CreateWhiteSprite();
                image.color = new Color(0.16f, 0.18f, 0.24f, 1f);
                image.raycastTarget = true;

                Button button = buttonObject.GetComponent<Button>();
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.92f, 0.94f, 1f, 1f);
                colors.pressedColor = new Color(0.78f, 0.8f, 0.86f, 1f);
                colors.selectedColor = Color.white;
                colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
                button.colors = colors;
                button.targetGraphic = image;
                button.onClick.AddListener(() => OnChoiceClicked(choiceIndex));

                TMP_Text label = CreateLayoutText(
                    "Label",
                    rect,
                    font,
                    24f,
                    FontStyles.Normal,
                    TextAlignmentOptions.Center,
                    0f);
                label.raycastTarget = false;
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(16f, 4f);
                labelRect.offsetMax = new Vector2(-16f, -4f);

                choiceButtons[i] = button;
                choiceLabels[i] = label;
            }
        }

        private void ShowCurrentQuestion()
        {
            if (questions == null || questionIndex >= questions.Count)
            {
                CompleteSolved();
                return;
            }

            DemoChestQuizQuestion question = questions[questionIndex];
            if (question.HasMemorizePhase)
            {
                if (memoryRoutine != null)
                {
                    StopCoroutine(memoryRoutine);
                }

                memoryRoutine = StartCoroutine(ShowMemoryThenChoices(question));
                return;
            }

            ShowChoices(question);
        }

        private IEnumerator ShowMemoryThenChoices(DemoChestQuizQuestion question)
        {
            waitingForMemory = true;
            SetChoicesVisible(false);
            promptText.fontSize = 52f;
            promptText.text = question.MemorizeText;
            feedbackText.text = $"{Mathf.RoundToInt(MemoryPreviewSeconds)}초 동안 숫자를 기억하세요.";
            feedbackText.color = new Color(0.95f, 0.82f, 0.4f);
            yield return new WaitForSecondsRealtime(MemoryPreviewSeconds);
            if (closing)
            {
                yield break;
            }

            waitingForMemory = false;
            memoryRoutine = null;
            promptText.fontSize = 30f;
            ShowChoices(question);
        }

        private void ShowChoices(DemoChestQuizQuestion question)
        {
            promptText.text = question.Prompt;
            feedbackText.text = "정답을 고르면 열쇠를 얻습니다.";
            feedbackText.color = new Color(0.82f, 0.82f, 0.86f);
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                bool visible = i < question.Choices.Length;
                choiceButtons[i].gameObject.SetActive(visible);
                choiceButtons[i].interactable = visible;
                if (visible)
                {
                    choiceLabels[i].text = question.Choices[i];
                }
            }
        }

        private void SetChoicesVisible(bool visible)
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                choiceButtons[i].gameObject.SetActive(visible);
                choiceButtons[i].interactable = visible;
            }
        }

        private void OnChoiceClicked(int choiceIndex)
        {
            if (closing || waitingForMemory || questions == null || questionIndex >= questions.Count)
            {
                return;
            }

            DemoChestQuizQuestion question = questions[questionIndex];
            DemoDungeonAudio.PlayQuizUi();
            if (choiceIndex == question.CorrectIndex)
            {
                feedbackText.text = "정답!";
                feedbackText.color = new Color(0.45f, 0.9f, 0.55f);
                questionIndex++;
                if (questionIndex >= questions.Count)
                {
                    CompleteSolved();
                    return;
                }

                ShowCurrentQuestion();
                return;
            }

            feedbackText.text = "오답입니다. 다시 고르세요.";
            feedbackText.color = new Color(0.95f, 0.42f, 0.38f);
        }

        private void CompleteSolved()
        {
            if (solved)
            {
                return;
            }

            solved = true;
            Close(true);
        }

        private void Close(bool success)
        {
            if (closing)
            {
                return;
            }

            closing = true;
            waitingForMemory = false;
            if (memoryRoutine != null)
            {
                StopCoroutine(memoryRoutine);
                memoryRoutine = null;
            }

            if (activeOverlay == this)
            {
                activeOverlay = null;
            }

            DemoDungeonController controller = DemoDungeonController.Active;
            bool dungeonRunning = controller != null && controller.IsRunning;
            controller?.SetGameplayPaused(false);
            if (dungeonRunning)
            {
                playerMove?.SetInputEnabled(true);
            }

            Action solvedCallback = onSolved;
            Action closedCallback = onClosedWithoutSolve;
            onSolved = null;
            onClosedWithoutSolve = null;
            Destroy(gameObject);

            if (success)
            {
                solvedCallback?.Invoke();
            }
            else
            {
                closedCallback?.Invoke();
            }
        }

        private static void CreateDimmer(RectTransform parent)
        {
            GameObject dimmer = new GameObject("Dimmer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = dimmer.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = dimmer.GetComponent<Image>();
            image.sprite = CreateWhiteSprite();
            image.color = new Color(0f, 0f, 0f, 0.62f);
            image.raycastTarget = true;
        }

        private static RectTransform CreatePanel(RectTransform parent)
        {
            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(760f, 620f);
            Image image = panel.GetComponent<Image>();
            image.sprite = CreateWhiteSprite();
            image.color = new Color(0.08f, 0.08f, 0.1f, 0.96f);
            image.raycastTarget = true;

            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 28, 28);
            layout.spacing = 18;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        private static TMP_Text CreateLayoutText(
            string objectName,
            Transform parent,
            TMP_FontAsset fontAsset,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            float preferredHeight)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
            tmp.font = fontAsset;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;

            if (preferredHeight > 0f)
            {
                LayoutElement layout = textObject.AddComponent<LayoutElement>();
                layout.minHeight = preferredHeight;
                layout.preferredHeight = preferredHeight;
            }

            return tmp;
        }

        private static TMP_FontAsset ResolveFont()
        {
            TMP_Text existing = FindFirstObjectByType<TMP_Text>();
            return existing != null ? existing.font : null;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem.hideFlags = HideFlags.DontSave;
        }

        private static Sprite CreateWhiteSprite()
        {
            Texture2D texture = Texture2D.whiteTexture;
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }
    }
}
