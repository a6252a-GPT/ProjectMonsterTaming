using TMPro;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleTrapFloatingLabel : MonoBehaviour // 함정 발동 종류를 월드 문자로 알린다
    {
        private const float DisplayDuration = 1.05f;
        private const float HeightOffset = 1.35f;
        private const float RiseDistance = 0.8f;

        private TMP_Text label;
        private Camera facingCamera;
        private Vector3 startPosition;
        private Color baseColor;
        private float elapsed;

        public HexCastleTrapType TrapType { get; private set; }
        public string DisplayText => label != null ? label.text : string.Empty;

        public static HexCastleTrapFloatingLabel Show(
            Transform parent,
            Vector3 worldPosition,
            HexCastleTrapType trapType,
            TMP_FontAsset font,
            Camera worldCamera)
        {
            var root = new GameObject($"TrapFloating_{trapType}");
            if (parent != null)
            {
                root.transform.SetParent(parent, true);
            }

            root.transform.position = worldPosition + Vector3.up * HeightOffset;
            var text = root.AddComponent<TextMeshPro>();
            text.font = font != null ? font : TMP_Settings.defaultFontAsset;
            text.text = ResolveDisplayText(trapType);
            text.fontSize = 3.8f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.sortingOrder = 190;
            text.outlineColor = new Color32(28, 18, 12, 255);
            text.outlineWidth = 0.18f;
            text.rectTransform.sizeDelta = new Vector2(6f, 1.2f);

            var view = root.AddComponent<HexCastleTrapFloatingLabel>();
            view.Configure(text, trapType, worldCamera);
            return view;
        }

        public static string ResolveDisplayText(HexCastleTrapType trapType)
        {
            return trapType switch
            {
                HexCastleTrapType.Snare => "덫!",
                HexCastleTrapType.SpikePlate => "가시 발판!",
                HexCastleTrapType.BlastMine => "폭발 지뢰!",
                _ => "함정!"
            };
        }

        public static Color ResolveColor(HexCastleTrapType trapType)
        {
            return trapType switch
            {
                HexCastleTrapType.Snare => new Color(1f, 0.68f, 0.18f, 1f),
                HexCastleTrapType.SpikePlate => new Color(1f, 0.30f, 0.22f, 1f),
                HexCastleTrapType.BlastMine => new Color(1f, 0.84f, 0.18f, 1f),
                _ => Color.white
            };
        }

        private void Configure(TMP_Text targetLabel, HexCastleTrapType trapType, Camera worldCamera)
        {
            label = targetLabel;
            TrapType = trapType;
            facingCamera = worldCamera != null ? worldCamera : Camera.main;
            startPosition = transform.position;
            baseColor = ResolveColor(trapType);
            var hidden = baseColor;
            hidden.a = 0f;
            label.color = hidden;
            transform.localScale = Vector3.one * 0.72f;
        }

        private void Update()
        {
            if (label == null)
            {
                Destroy(gameObject);
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            var ratio = Mathf.Clamp01(elapsed / DisplayDuration);
            var riseRatio = 1f - Mathf.Pow(1f - ratio, 3f);
            transform.position = startPosition + Vector3.up * (RiseDistance * riseRatio);
            if (facingCamera == null)
            {
                facingCamera = Camera.main;
            }
            if (facingCamera != null)
            {
                transform.rotation = facingCamera.transform.rotation;
            }

            var pop = ratio < 0.18f
                ? Mathf.Lerp(0.72f, 1.12f, Mathf.SmoothStep(0f, 1f, ratio / 0.18f))
                : Mathf.Lerp(1.12f, 0.96f, Mathf.SmoothStep(0f, 1f, (ratio - 0.18f) / 0.82f));
            transform.localScale = Vector3.one * pop;
            var color = baseColor;
            var enterAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(ratio / 0.08f));
            var exitAlpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((ratio - 0.68f) / 0.32f));
            color.a = Mathf.Min(enterAlpha, exitAlpha);
            label.color = color;

            if (ratio >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
