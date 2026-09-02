using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Shared.UI
{
    /// <summary>
    /// 관리 패널처럼 1920x1080 기준으로 제작된 UI를 화면 비율과 Safe Area 안에 맞춘다.
    /// 넓은 가로 화면에서는 높이를, 좁은 화면에서는 너비를 기준으로 맞춰 UI가 잘리지 않게 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileSafeAreaCanvasFitter : MonoBehaviour
    {
        private static readonly Vector2 DefaultReferenceResolution = new(1920f, 1080f);

        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private Vector2 referenceResolution = new(1920f, 1080f);

        private RectTransform targetRect;
        private Vector2 lastScreenSize;
        private Rect lastSafeArea;

        public static MobileSafeAreaCanvasFitter Ensure(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            var fitter = target.GetComponent<MobileSafeAreaCanvasFitter>();
            if (fitter == null)
            {
                fitter = target.AddComponent<MobileSafeAreaCanvasFitter>();
            }

            fitter.RefreshLayout();
            return fitter;
        }

        private void Awake()
        {
            CacheReferences();
            RefreshLayout();
        }

        private void OnEnable()
        {
            RefreshLayout();
        }

        private void Update()
        {
            var screenSize = new Vector2(Screen.width, Screen.height);
            if (screenSize != lastScreenSize || Screen.safeArea != lastSafeArea)
            {
                RefreshLayout();
            }
        }

        public void RefreshLayout()
        {
            CacheReferences();
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            var safeArea = Screen.safeArea;
            if (canvasScaler != null)
            {
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = referenceResolution == Vector2.zero
                    ? DefaultReferenceResolution
                    : referenceResolution;
                canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                canvasScaler.matchWidthOrHeight = CalculateFitMatch(safeArea.size, canvasScaler.referenceResolution);
            }

            if (targetRect != null)
            {
                CalculateSafeAreaAnchors(safeArea, Screen.width, Screen.height, out var anchorMin, out var anchorMax);
                targetRect.anchorMin = anchorMin;
                targetRect.anchorMax = anchorMax;
                targetRect.offsetMin = Vector2.zero;
                targetRect.offsetMax = Vector2.zero;
            }

            lastScreenSize = new Vector2(Screen.width, Screen.height);
            lastSafeArea = safeArea;
        }

        public static float CalculateFitMatch(Vector2 availableSize, Vector2 designResolution)
        {
            if (availableSize.x <= 0f || availableSize.y <= 0f ||
                designResolution.x <= 0f || designResolution.y <= 0f)
            {
                return 0.5f;
            }

            var availableAspect = availableSize.x / availableSize.y;
            var designAspect = designResolution.x / designResolution.y;
            return availableAspect >= designAspect ? 1f : 0f;
        }

        public static void CalculateSafeAreaAnchors(
            Rect safeArea,
            float screenWidth,
            float screenHeight,
            out Vector2 anchorMin,
            out Vector2 anchorMax)
        {
            if (screenWidth <= 0f || screenHeight <= 0f)
            {
                anchorMin = Vector2.zero;
                anchorMax = Vector2.one;
                return;
            }

            anchorMin = new Vector2(safeArea.xMin / screenWidth, safeArea.yMin / screenHeight);
            anchorMax = new Vector2(safeArea.xMax / screenWidth, safeArea.yMax / screenHeight);
        }

        private void CacheReferences()
        {
            targetRect ??= transform as RectTransform;
            canvasScaler ??= GetComponentInParent<CanvasScaler>();
        }
    }
}
