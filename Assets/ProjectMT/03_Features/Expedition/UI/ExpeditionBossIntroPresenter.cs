using System.Collections;
using TMPro;
using UnityEngine;

namespace ProjectMT.Features.Expedition
{
    [DisallowMultipleComponent]
    public sealed class ExpeditionBossIntroPresenter : MonoBehaviour // 5단위 보스 등장 전 경고 연출
    {
        [SerializeField] private GameObject displayRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform banner;
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField, Min(0.05f)] private float revealSeconds = 0.3f;
        [SerializeField, Min(0.1f)] private float holdSeconds = 0.75f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.3f;

        public IEnumerator Play(int stage)
        {
            if (displayRoot == null || canvasGroup == null || banner == null)
            {
                yield break;
            }

            if (stageText != null)
            {
                stageText.text = $"STAGE {Mathf.Max(1, stage)}";
            }

            if (titleText != null)
            {
                titleText.text = "BOSS";
            }

            displayRoot.SetActive(true);
            transform.SetAsLastSibling();
            canvasGroup.alpha = 0f;
            banner.localScale = new Vector3(1.35f, 0.72f, 1f);
            var elapsed = 0f;
            while (elapsed < revealSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / revealSeconds);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                canvasGroup.alpha = eased;
                banner.localScale = Vector3.LerpUnclamped(new Vector3(1.35f, 0.72f, 1f), Vector3.one, eased);
                yield return null;
            }

            banner.localScale = Vector3.one;
            canvasGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(holdSeconds);

            elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / fadeSeconds);
                canvasGroup.alpha = 1f - t;
                banner.localScale = Vector3.LerpUnclamped(Vector3.one, new Vector3(1.08f, 1.08f, 1f), t);
                yield return null;
            }

            Hide();
        }

        public void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            if (banner != null)
            {
                banner.localScale = Vector3.one;
            }

            displayRoot?.SetActive(false);
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            GameObject root,
            CanvasGroup group,
            RectTransform bannerRoot,
            TMP_Text stage,
            TMP_Text title)
        {
            displayRoot = root;
            canvasGroup = group;
            banner = bannerRoot;
            stageText = stage;
            titleText = title;
        }
#endif
    }
}
