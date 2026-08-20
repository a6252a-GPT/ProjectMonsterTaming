using ProjectMT.Core.SceneFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class SceneLoadingOverlayPresenter : MonoBehaviour // 정식 씬 전환 공통 로딩 화면
    {
        [SerializeField] private GameObject displayRoot;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite[] backgrounds;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private RectTransform spinner;
        [SerializeField, Min(1f)] private float spinnerDegreesPerSecond = 160f;

        private int lastBackgroundIndex = -1;

        public void Show(SceneId destination)
        {
            SelectBackground();
            if (messageText != null)
            {
                messageText.text = destination.Value == "main_battle"
                    ? "원정대를 불러오는 중입니다..."
                    : "콘텐츠를 준비하는 중입니다...";
            }

            displayRoot?.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            displayRoot?.SetActive(false);
        }

        private void Update()
        {
            if (spinner != null && displayRoot != null && displayRoot.activeSelf)
            {
                spinner.Rotate(0f, 0f, -spinnerDegreesPerSecond * Time.unscaledDeltaTime);
            }
        }

        private void SelectBackground()
        {
            if (backgroundImage == null || backgrounds == null || backgrounds.Length == 0)
            {
                return;
            }

            var index = backgrounds.Length == 1 ? 0 : Random.Range(0, backgrounds.Length - 1);
            if (backgrounds.Length > 1 && index >= lastBackgroundIndex)
            {
                index++;
            }

            lastBackgroundIndex = index;
            backgroundImage.sprite = backgrounds[index];
        }

#if UNITY_EDITOR
        public void EditorConfigure(GameObject root, Image image, Sprite[] images, TMP_Text message, RectTransform spinnerRoot)
        {
            displayRoot = root;
            backgroundImage = image;
            backgrounds = images;
            messageText = message;
            spinner = spinnerRoot;
        }
#endif
    }
}
