using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Expedition
{
    [DisallowMultipleComponent]
    public sealed class ExpeditionResultFlashPresenter : MonoBehaviour
    {
        public const float FadeInSeconds = 0.18f;
        public const float HoldSeconds = 0.72f;
        public const float FadeOutSeconds = 0.28f;

        private static readonly Color32 BackgroundColor = new Color32(10, 28, 24, 224);
        private static readonly Color32 ClearAccentColor = new Color32(197, 169, 96, 255);
        private static readonly Color32 FailureAccentColor = new Color32(169, 92, 82, 255);
        private static readonly Color32 TitleColor = new Color32(239, 229, 200, 255);
        private static readonly Color32 DetailColor = new Color32(201, 207, 201, 255);

        private RectTransform rootRect;
        private CanvasGroup canvasGroup;
        private TMP_Text titleText;
        private TMP_Text detailText;
        private Image leftAccent;
        private Image rightAccent;
        private Coroutine playRoutine;

        public float SequenceDuration => FadeInSeconds + HoldSeconds + FadeOutSeconds;
        public bool IsVisible => gameObject.activeSelf && canvasGroup != null && canvasGroup.alpha > 0f;

        public static ExpeditionResultFlashPresenter ResolveOrCreate(TMP_Text sourceText)
        {
            var canvas = sourceText != null ? sourceText.canvas : null;
            if (canvas == null)
            {
                return null;
            }

            var existing = canvas.GetComponentInChildren<ExpeditionResultFlashPresenter>(true);
            if (existing != null)
            {
                existing.EnsureConfigured(sourceText);
                existing.HideImmediate();
                return existing;
            }

            var root = new GameObject(
                "ExpeditionResultFlash",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(ExpeditionResultFlashPresenter));
            root.transform.SetParent(canvas.transform, false);

            var presenter = root.GetComponent<ExpeditionResultFlashPresenter>();
            presenter.EnsureConfigured(sourceText);
            presenter.HideImmediate();
            return presenter;
        }

        public float ShowClear(int clearedStage)
        {
            Show("원정대 클리어", $"{clearedStage}단계 공략을 완료했습니다", ClearAccentColor);
            return SequenceDuration;
        }

        public float ShowFailure(string detail)
        {
            Show("원정대 실패", detail, FailureAccentColor);
            return SequenceDuration;
        }

        public void HideImmediate()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            gameObject.SetActive(false);
        }

        private void EnsureConfigured(TMP_Text sourceText)
        {
            rootRect ??= transform as RectTransform;
            canvasGroup ??= GetComponent<CanvasGroup>();
            if (titleText == null || detailText == null || leftAccent == null || rightAccent == null)
            {
                BuildVisuals(sourceText);
            }
        }

        private void BuildVisuals(TMP_Text sourceText)
        {
            rootRect.anchorMin = new Vector2(0.075f, 0.5f);
            rootRect.anchorMax = new Vector2(0.925f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(0f, 150f);

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var background = CreateImage("Background", transform, BackgroundColor);
            Stretch(background.rectTransform);

            leftAccent = CreateImage("LeftAccent", transform, ClearAccentColor);
            ConfigureAccent(leftAccent.rectTransform, 0f);
            rightAccent = CreateImage("RightAccent", transform, ClearAccentColor);
            ConfigureAccent(rightAccent.rectTransform, 1f);

            titleText = CreateText("Title", transform, sourceText, 52f, FontStyles.Bold, TitleColor);
            SetRect(titleText.rectTransform, new Vector2(0.08f, 0.46f), new Vector2(0.92f, 0.91f));

            detailText = CreateText("Detail", transform, sourceText, 29f, FontStyles.Normal, DetailColor);
            SetRect(detailText.rectTransform, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.52f));
        }

        private void Show(string title, string detail, Color32 accentColor)
        {
            EnsureConfigured(null);
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
            }

            titleText.text = title;
            detailText.text = detail;
            leftAccent.color = accentColor;
            rightAccent.color = accentColor;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            playRoutine = StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            rootRect.localScale = new Vector3(0.985f, 0.985f, 1f);
            canvasGroup.alpha = 0f;

            var elapsed = 0f;
            while (elapsed < FadeInSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / FadeInSeconds);
                canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, progress);
                rootRect.localScale = Vector3.LerpUnclamped(
                    new Vector3(0.985f, 0.985f, 1f),
                    Vector3.one,
                    progress);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            rootRect.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(HoldSeconds);

            elapsed = 0f;
            while (elapsed < FadeOutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / FadeOutSeconds));
                yield return null;
            }

            canvasGroup.alpha = 0f;
            playRoutine = null;
            gameObject.SetActive(false);
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            var image = child.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            TMP_Text source,
            float fontSize,
            FontStyles fontStyle,
            Color color)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            var text = child.GetComponent<TextMeshProUGUI>();
            text.font = source != null ? source.font : null;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static void ConfigureAccent(RectTransform rect, float anchorX)
        {
            rect.anchorMin = new Vector2(anchorX, 0.5f);
            rect.anchorMax = new Vector2(anchorX, 0.5f);
            rect.pivot = new Vector2(anchorX, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(4f, 98f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
