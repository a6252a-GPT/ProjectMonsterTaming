using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Quest
{
    // 튜토리얼 클릭 대상만 밝게 남기고 화면의 나머지 영역을 암전하는 런타임 스포트라이트.
    // 셰이더/고정 좌표 대신 대상 RectTransform의 현재 화면 사각형을 매 프레임 추적한다.
    [DisallowMultipleComponent]
    internal sealed class QuestTutorialSpotlight : MonoBehaviour
    {
        private const int OverlaySortingOrder = 30000;
        private const int HintSortingOrder = OverlaySortingOrder + 1;
        private const float HolePadding = 10f;
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.68f);

        private static QuestTutorialSpotlight instance;
        private readonly RectTransform[] masks = new RectTransform[4];
        private RectTransform root;
        private Object owner;
        private RectTransform target;

        internal static void Show(Object requestedOwner, RectTransform requestedTarget)
        {
            if (requestedOwner == null || requestedTarget == null || !requestedTarget.gameObject.activeInHierarchy)
            {
                Hide(requestedOwner);
                return;
            }

            EnsureInstance();
            instance.owner = requestedOwner;
            instance.target = requestedTarget;
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

        internal static void EnsureHintAboveOverlay(GameObject hint)
        {
            if (hint == null)
            {
                return;
            }

            var canvas = hint.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = hint.AddComponent<Canvas>();
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = HintSortingOrder;
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

            var overlay = new GameObject(
                "QuestTutorialSpotlight",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            overlay.hideFlags = HideFlags.DontSave;
            instance = overlay.AddComponent<QuestTutorialSpotlight>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (root == null)
            {
                Build();
            }
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

        private void Build()
        {
            root = transform as RectTransform;
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = OverlaySortingOrder;

            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            for (var index = 0; index < masks.Length; index++)
            {
                var maskObject = new GameObject(
                    $"DimMask_{index}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                maskObject.transform.SetParent(root, false);
                masks[index] = maskObject.GetComponent<RectTransform>();
                var image = maskObject.GetComponent<Image>();
                image.color = DimColor;
                image.raycastTarget = false;
            }
        }

        private void RefreshLayout()
        {
            if (root == null || target == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
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

            SetRect(masks[0], bounds.xMin, xMin, bounds.yMin, bounds.yMax); // left
            SetRect(masks[1], xMax, bounds.xMax, bounds.yMin, bounds.yMax); // right
            SetRect(masks[2], xMin, xMax, bounds.yMin, yMin); // bottom
            SetRect(masks[3], xMin, xMax, yMax, bounds.yMax); // top
        }

        private static void SetRect(RectTransform rect, float xMin, float xMax, float yMin, float yMax)
        {
            if (rect == null)
            {
                return;
            }

            var width = Mathf.Max(0f, xMax - xMin);
            var height = Mathf.Max(0f, yMax - yMin);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.gameObject.SetActive(width > 0.01f && height > 0.01f);
        }
    }
}
