using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectMT.Features.Quest
{
    // 화면 밝기를 유지하면서 대상 테두리와 짧은 안내를 표시한다.
    // 셰이더/고정 좌표 대신 대상 RectTransform의 현재 화면 사각형을 매 프레임 추적한다.
    [DisallowMultipleComponent]
    internal sealed class QuestTutorialSpotlight : MonoBehaviour
    {
        private const float HolePadding = 10f;

        private static QuestTutorialSpotlight instance;
        [SerializeField] private RectTransform[] masks = new RectTransform[4];
        [SerializeField] private RectTransform root;
        private Object owner;
        private RectTransform target;
        [SerializeField] private RectTransform caption;
        [SerializeField] private TMP_Text captionText;
        private bool emphasizeTarget;

        internal static void Show(Object requestedOwner, RectTransform requestedTarget, string message = null, bool emphasize = true)
        {
            if (requestedOwner == null || requestedTarget == null || !requestedTarget.gameObject.activeInHierarchy)
            {
                Hide(requestedOwner);
                return;
            }

            EnsureInstance();
            if (instance == null) return;
            instance.owner = requestedOwner;
            instance.target = requestedTarget;
            instance.emphasizeTarget = emphasize;
            var fontSource = requestedTarget.GetComponentInChildren<TMP_Text>(true);
            if (fontSource == null && requestedOwner is Component component) fontSource = component.GetComponentInChildren<TMP_Text>(true);
            if (fontSource != null) instance.captionText.font = fontSource.font;
            instance.captionText.text = message ?? "여기를 눌러 진행하세요";
            instance.gameObject.SetActive(true);
            instance.RefreshLayout();
        }

        internal static void Hide(Object requestedOwner)
        {
            if (instance == null || instance.owner != requestedOwner)
            {
                return;
            }

            instance.owner = null;
            instance.target = null;
            instance.gameObject.SetActive(false);
        }

        private static void EnsureInstance()
        {
            if (instance != null)
            {
                return;
            }

            instance = FindFirstObjectByType<QuestTutorialSpotlight>(FindObjectsInactive.Include);
            if (instance != null)
            {
                return;
            }

            var prefab = Resources.Load<QuestTutorialSpotlight>("UI/PF_QuestTutorialSpotlight");
            if (prefab == null)
            {
                Debug.LogError("Quest tutorial spotlight prefab is missing.");
                return;
            }

            instance = Instantiate(prefab);
            instance.name = "QuestTutorialSpotlight";
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void LateUpdate()
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                owner = null;
                target = null;
                gameObject.SetActive(false);
                return;
            }

            RefreshLayout();
        }

        private void RefreshLayout()
        {
            if (root == null || target == null)
            {
                return;
            }

            var targetCanvas = target.GetComponentInParent<Canvas>();
            var camera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? targetCanvas.worldCamera
                : null;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var bottomLeftScreen = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var topRightScreen = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    root, bottomLeftScreen, null, out var bottomLeft) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    root, topRightScreen, null, out var topRight))
            {
                return;
            }

            var bounds = root.rect;
            var xMin = Mathf.Clamp(Mathf.Min(bottomLeft.x, topRight.x) - HolePadding, bounds.xMin, bounds.xMax);
            var xMax = Mathf.Clamp(Mathf.Max(bottomLeft.x, topRight.x) + HolePadding, bounds.xMin, bounds.xMax);
            var yMin = Mathf.Clamp(Mathf.Min(bottomLeft.y, topRight.y) - HolePadding, bounds.yMin, bounds.yMax);
            var yMax = Mathf.Clamp(Mathf.Max(bottomLeft.y, topRight.y) + HolePadding, bounds.yMin, bounds.yMax);

            SetRect(masks[0], xMin, xMin + 2f, yMin, yMax);
            SetRect(masks[1], xMax - 2f, xMax, yMin, yMax);
            SetRect(masks[2], xMin, xMax, yMin, yMin + 2f);
            SetRect(masks[3], xMin, xMax, yMax - 2f, yMax);
            foreach (var line in masks) line.gameObject.SetActive(emphasizeTarget);
            var width = Mathf.Min(460f, bounds.width - 32f);
            var x = Mathf.Clamp((xMin + xMax - width) * 0.5f, bounds.xMin + 16f, bounds.xMax - width - 16f);
            var y = yMax + 18f;
            if (y + 80f > bounds.yMax - 16f) y = yMin - 100f;
            y = Mathf.Clamp(y, bounds.yMin + 16f, bounds.yMax - 96f);
            SetRect(caption, x, x + width, y, y + 80f);
        }

        private static void SetRect(RectTransform rect, float xMin, float xMax, float yMin, float yMax)
        {
            if (rect == null)
            {
                return;
            }

            var width = Mathf.Max(0f, xMax - xMin);
            var height = Mathf.Max(0f, yMax - yMin);
            rect.anchoredPosition = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.gameObject.SetActive(width > 0.01f && height > 0.01f);
        }
    }
}
