using System.Collections;
using TMPro;
using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class CombatPowerIncreasePresenter : MonoBehaviour // 저장 확정 뒤 총전투력 상승 알림
    {
        private const float CardRestingY = 320f;

        [SerializeField] private GameObject displayRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text deltaText;
        [SerializeField] private TMP_Text totalText;
        [SerializeField, Min(0.05f)] private float revealSeconds = 0.18f;
        [SerializeField, Min(0.05f)] private float countSeconds = 0.21f;
        [SerializeField, Min(0.1f)] private float holdSeconds = 0.9f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.24f;

        private Coroutine routine;
        private Vector2 restingPosition;
        private bool restingPositionCaptured;

        public void ShowIncrease(float previous, float current)
        {
            var delta = Mathf.RoundToInt(current - previous);
            if (delta <= 0 || displayRoot == null || canvasGroup == null || card == null)
            {
                return;
            }

            if (routine != null)
            {
                StopCoroutine(routine);
            }

            CenterCard();

            if (deltaText != null)
            {
                deltaText.text = "+0";
            }

            if (totalText != null)
            {
                var previousValue = Mathf.RoundToInt(previous);
                totalText.text = $"{previousValue:N0} → {previousValue:N0}";
            }

            routine = StartCoroutine(Play(
                Mathf.RoundToInt(previous),
                Mathf.RoundToInt(current),
                delta));
        }

        public void Hide()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            if (card != null && restingPositionCaptured)
            {
                card.anchoredPosition = restingPosition;
                card.localScale = Vector3.one;
            }

            displayRoot?.SetActive(false);
        }

        private IEnumerator Play(int previous, int current, int delta)
        {
            displayRoot.SetActive(true);
            transform.SetAsLastSibling();
            canvasGroup.alpha = 0f;
            var startPosition = restingPosition + Vector2.up * 12f;
            card.anchoredPosition = startPosition;
            var startScale = new Vector3(0.94f, 0.94f, 1f);
            card.localScale = startScale;
            var elapsed = 0f;
            while (elapsed < revealSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / revealSeconds);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                canvasGroup.alpha = eased;
                card.localScale = Vector3.LerpUnclamped(startScale, Vector3.one, eased);
                card.anchoredPosition = Vector2.LerpUnclamped(startPosition, restingPosition, eased);
                yield return null;
            }

            card.anchoredPosition = restingPosition;
            card.localScale = Vector3.one;

            elapsed = 0f;
            while (elapsed < countSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / countSeconds);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                SetNumbers(
                    Mathf.RoundToInt(Mathf.Lerp(0f, delta, eased)),
                    previous,
                    Mathf.RoundToInt(Mathf.Lerp(previous, current, eased)));
                yield return null;
            }

            SetNumbers(delta, previous, current);
            yield return new WaitForSecondsRealtime(holdSeconds);
            elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / fadeSeconds);
                canvasGroup.alpha = 1f - t;
                yield return null;
            }

            routine = null;
            displayRoot.SetActive(false);
        }

        private void CenterCard()
        {
            if (card == null)
            {
                return;
            }

            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = new Vector2(0f, CardRestingY);
            restingPosition = card.anchoredPosition;
            restingPositionCaptured = true;
        }

        private void SetNumbers(int delta, int previous, int current)
        {
            if (deltaText != null)
            {
                deltaText.text = $"+{Mathf.Max(0, delta):N0}";
            }

            if (totalText != null)
            {
                totalText.text = $"{Mathf.Max(0, previous):N0} → {Mathf.Max(0, current):N0}";
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            GameObject root,
            CanvasGroup group,
            RectTransform cardRoot,
            TMP_Text delta,
            TMP_Text total)
        {
            displayRoot = root;
            canvasGroup = group;
            card = cardRoot;
            deltaText = delta;
            totalText = total;
            restingPositionCaptured = false;
        }
#endif
    }
}
