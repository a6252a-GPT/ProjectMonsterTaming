using System.Collections;
using TMPro;
using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class CombatPowerIncreasePresenter : MonoBehaviour // 저장 확정 뒤 총전투력 상승 알림
    {
        [SerializeField] private GameObject displayRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text deltaText;
        [SerializeField] private TMP_Text totalText;
        [SerializeField, Min(0.05f)] private float revealSeconds = 0.22f;
        [SerializeField, Min(0.1f)] private float holdSeconds = 1.15f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.28f;

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
                deltaText.text = $"전투력 +{delta:N0}";
            }

            if (totalText != null)
            {
                totalText.text = $"총 전투력 {Mathf.RoundToInt(current):N0}";
            }

            routine = StartCoroutine(Play());
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

        private IEnumerator Play()
        {
            displayRoot.SetActive(true);
            transform.SetAsLastSibling();
            canvasGroup.alpha = 0f;
            var startPosition = restingPosition + Vector2.up * 32f;
            card.anchoredPosition = startPosition;
            card.localScale = new Vector3(0.82f, 0.82f, 1f);
            var elapsed = 0f;
            while (elapsed < revealSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / revealSeconds);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                canvasGroup.alpha = eased;
                card.localScale = Vector3.LerpUnclamped(new Vector3(0.82f, 0.82f, 1f), Vector3.one, eased);
                card.anchoredPosition = Vector2.LerpUnclamped(startPosition, restingPosition, eased);
                yield return null;
            }

            card.anchoredPosition = restingPosition;
            card.localScale = Vector3.one;

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
            card.anchoredPosition = Vector2.zero;
            restingPosition = Vector2.zero;
            restingPositionCaptured = true;
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
