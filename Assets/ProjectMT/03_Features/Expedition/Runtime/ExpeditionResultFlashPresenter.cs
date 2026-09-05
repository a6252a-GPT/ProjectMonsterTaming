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

        private static readonly Color32 ClearAccentColor = new Color32(197, 169, 96, 255);
        private static readonly Color32 FailureAccentColor = new Color32(169, 92, 82, 255);

        [SerializeField] private RectTransform rootRect;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private Image leftAccent;
        [SerializeField] private Image rightAccent;
        private Coroutine playRoutine;

        public float SequenceDuration => FadeInSeconds + HoldSeconds + FadeOutSeconds;
        public bool IsVisible => gameObject.activeSelf && canvasGroup != null && canvasGroup.alpha > 0f;

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

        private void Show(string title, string detail, Color32 accentColor)
        {
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

    }
}
