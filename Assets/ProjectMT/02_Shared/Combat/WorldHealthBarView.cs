using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Shared.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class WorldHealthBarView : MonoBehaviour // 화면 좌표형 일반 유닛 HP바
    {
        private const float LossDelay = 0.12f;
        private const float LossCatchUpSpeed = 2.8f;

        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image lossFill;
        [SerializeField] private Image currentFill;
        private Image energyBackground;
        private Image energyFill;

        private float currentRatio = 1f;
        private float lossRatio = 1f;
        private float lossDelayRemaining;

        public RectTransform RectTransform => rectTransform;
        public Color FillColor => currentFill != null ? currentFill.color : Color.clear;
        public float CurrentRatio => currentRatio;
        public bool IsShown => canvasGroup != null && canvasGroup.alpha > 0f;
        public float EnergyRatio => energyFill != null ? energyFill.fillAmount : 0f;
        public bool ShowsEnergy => energyBackground != null && energyBackground.gameObject.activeSelf;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (lossFill == null || lossRatio <= currentRatio)
            {
                return;
            }

            lossDelayRemaining = Mathf.Max(0f, lossDelayRemaining - Time.unscaledDeltaTime);
            if (lossDelayRemaining > 0f)
            {
                return;
            }

            lossRatio = Mathf.MoveTowards(lossRatio, currentRatio, LossCatchUpSpeed * Time.unscaledDeltaTime);
            lossFill.fillAmount = lossRatio;
        }

        private void OnDisable()
        {
            currentRatio = 1f;
            lossRatio = 1f;
            lossDelayRemaining = 0f;
            if (currentFill != null)
            {
                currentFill.fillAmount = 1f;
            }

            if (lossFill != null)
            {
                lossFill.fillAmount = 1f;
            }
            SetEnergy(0f, false);
        }

        public void Bind(Color color, float healthRatio)
        {
            ResolveReferences();
            currentFill.color = color;
            var lossColor = Color.Lerp(color, Color.white, 0.62f);
            lossColor.a = 0.88f;
            lossFill.color = lossColor;
            currentRatio = Mathf.Clamp01(healthRatio);
            lossRatio = currentRatio;
            currentFill.fillAmount = currentRatio;
            lossFill.fillAmount = lossRatio;
            SetScreenVisible(true);
        }

        public void SetHealthRatio(float healthRatio)
        {
            var next = Mathf.Clamp01(healthRatio);
            if (next < currentRatio)
            {
                lossRatio = Mathf.Max(lossRatio, currentRatio);
                lossDelayRemaining = LossDelay;
            }
            else if (next > lossRatio)
            {
                lossRatio = next;
                lossFill.fillAmount = lossRatio;
            }

            currentRatio = next;
            currentFill.fillAmount = currentRatio;
        }

        public void SetEnergy(float energyRatio, bool visible)
        {
            EnsureEnergyBar();
            energyBackground.gameObject.SetActive(visible);
            energyFill.gameObject.SetActive(visible);
            energyFill.fillAmount = Mathf.Clamp01(energyRatio);
        }

        public void SetScreenVisible(bool value)
        {
            ResolveReferences();
            canvasGroup.alpha = value ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void ResolveReferences()
        {
            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void EnsureEnergyBar()
        {
            if (energyBackground != null && energyFill != null) return;
            var backgroundObject = new GameObject(
                "ActiveEnergyBackground",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var backgroundRect = (RectTransform)backgroundObject.transform;
            backgroundRect.SetParent(transform, false);
            backgroundRect.anchorMin = new Vector2(0f, 0f);
            backgroundRect.anchorMax = new Vector2(1f, 0f);
            backgroundRect.pivot = new Vector2(0.5f, 1f);
            backgroundRect.anchoredPosition = new Vector2(0f, -2f);
            backgroundRect.sizeDelta = new Vector2(0f, 3f);
            energyBackground = backgroundObject.GetComponent<Image>();
            energyBackground.color = new Color(0.035f, 0.055f, 0.09f, 0.92f);
            energyBackground.raycastTarget = false;

            var fillObject = new GameObject(
                "ActiveEnergyFill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var fillRect = (RectTransform)fillObject.transform;
            fillRect.SetParent(backgroundRect, false);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.one;
            fillRect.offsetMax = -Vector2.one;
            energyFill = fillObject.GetComponent<Image>();
            energyFill.color = new Color(0.38f, 0.78f, 1f, 1f);
            energyFill.type = Image.Type.Filled;
            energyFill.fillMethod = Image.FillMethod.Horizontal;
            energyFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            energyFill.raycastTarget = false;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            RectTransform root,
            CanvasGroup group,
            Image delayedFill,
            Image healthFill)
        {
            rectTransform = root;
            canvasGroup = group;
            lossFill = delayedFill;
            currentFill = healthFill;
        }
#endif
    }
}
