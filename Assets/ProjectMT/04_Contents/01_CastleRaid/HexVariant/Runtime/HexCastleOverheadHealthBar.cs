using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleOverheadHealthBar : MonoBehaviour // 육각 전투 대상 공통 피격 HP바
    {
        public const float DamageVisibleSeconds = 1.35f;
        public static readonly Color FriendlyColor = new Color(0.12f, 0.82f, 0.24f, 1f);
        public static readonly Color HostileColor = new Color(0.92f, 0.1f, 0.08f, 1f);

        private const float TopPadding = 0.2f;

        private static GameObject visualPrefab;
        private Vector3 authoredWorldScale;
        private Vector2 authoredFillSize;
        private GameObject visualRoot;
        private RectTransform visualRect;
        private RectTransform fillRect;
        private Image fillImage;
        private Camera targetCamera;
        private float hideAt;
        private float heightOffset = 1f;
        private float fillRatio;

        public bool IsVisible => visualRoot != null && visualRoot.activeSelf;
        public float FillRatio => fillRatio;
        public Color FillColor => fillImage == null ? Color.clear : fillImage.color;

        public static HexCastleOverheadHealthBar ShowDamage(
            Transform owner,
            HealthComponent healthSource)
        {
            if (owner == null || healthSource == null)
            {
                return null;
            }

            if (!owner.TryGetComponent<HexCastleOverheadHealthBar>(out var healthBar))
            {
                healthBar = owner.gameObject.AddComponent<HexCastleOverheadHealthBar>();
            }

            healthBar.Show(
                healthSource.CurrentHealth,
                healthSource.MaxHealth,
                healthSource.IsAlive,
                HostileColor);
            return healthBar;
        }

        public static HexCastleOverheadHealthBar ShowDamage(
            Transform owner,
            float currentHealth,
            float maximumHealth,
            bool friendly)
        {
            if (owner == null || maximumHealth <= 0f)
            {
                return null;
            }

            if (!owner.TryGetComponent<HexCastleOverheadHealthBar>(out var healthBar))
            {
                healthBar = owner.gameObject.AddComponent<HexCastleOverheadHealthBar>();
            }

            healthBar.Show(
                currentHealth,
                maximumHealth,
                currentHealth > 0f,
                friendly ? FriendlyColor : HostileColor);
            return healthBar;
        }

        public static Vector3 ResolveWorldAnchor(Transform owner)
        {
            if (owner == null)
            {
                return Vector3.zero;
            }

            var renderers = owner.GetComponentsInChildren<Renderer>(true);
            var highestPoint = owner.position.y + 0.8f;
            for (var index = 0; index < renderers.Length; index++)
            {
                var targetRenderer = renderers[index];
                if (targetRenderer == null || !targetRenderer.enabled ||
                    targetRenderer is ParticleSystemRenderer ||
                    targetRenderer is TrailRenderer ||
                    targetRenderer is LineRenderer)
                {
                    continue;
                }

                highestPoint = Mathf.Max(highestPoint, targetRenderer.bounds.max.y);
            }

            var offset = Mathf.Clamp(highestPoint - owner.position.y + TopPadding, 0.8f, 4.5f);
            return owner.position + Vector3.up * offset;
        }

        public void HideImmediately()
        {
            hideAt = 0f;
            if (visualRoot != null)
            {
                visualRoot.SetActive(false);
            }

            enabled = false;
        }

        private void Update()
        {
            if (IsVisible && Time.time >= hideAt)
            {
                HideImmediately();
            }
        }

        private void LateUpdate()
        {
            if (!IsVisible || visualRect == null)
            {
                return;
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            visualRect.position = transform.position + Vector3.up * heightOffset;
            if (targetCamera != null)
            {
                visualRect.rotation = targetCamera.transform.rotation;
            }

            ApplyFixedWorldScale();
        }

        private void OnDisable()
        {
            if (visualRoot != null)
            {
                visualRoot.SetActive(false);
            }
        }

        private void Show(
            float currentHealth,
            float maximumHealth,
            bool alive,
            Color color)
        {
            EnsureVisual();
            fillImage.color = color;
            fillRatio = maximumHealth <= 0f
                ? 0f
                : Mathf.Clamp01(currentHealth / maximumHealth);
            fillRect.sizeDelta = new Vector2(authoredFillSize.x * fillRatio, authoredFillSize.y);
            heightOffset = ResolveHeightOffset();
            if (!alive || fillRatio <= 0f)
            {
                HideImmediately();
                return;
            }

            hideAt = Time.time + DamageVisibleSeconds;
            enabled = true;
            visualRoot.SetActive(true);
            LateUpdate();
        }

        private void EnsureVisual()
        {
            if (visualRoot != null)
            {
                return;
            }

            if (visualPrefab == null)
            {
                visualPrefab = Resources.Load<GameObject>("PF_HexCastleHealthBar");
            }
            if (visualPrefab == null)
            {
                throw new System.InvalidOperationException("Missing authored HexCastle health bar prefab.");
            }

            visualRoot = Instantiate(visualPrefab, transform, false);
            visualRoot.name = "HexCastleHealthBar";
            visualRect = visualRoot.GetComponent<RectTransform>();
            fillRect = visualRect.Find("Fill") as RectTransform;
            fillImage = fillRect.GetComponent<Image>();
            authoredWorldScale = visualRect.localScale;
            authoredFillSize = fillRect.sizeDelta;
        }

        private float ResolveHeightOffset()
        {
            return ResolveWorldAnchor(transform).y - transform.position.y;
        }

        private void ApplyFixedWorldScale()
        {
            var parentScale = transform.lossyScale;
            visualRect.localScale = new Vector3(
                authoredWorldScale.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                authoredWorldScale.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                authoredWorldScale.z / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
        }
    }
}
