using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [DisallowMultipleComponent]
    public sealed class UnitVisualFeedback : MonoBehaviour // 유닛 피격·사망 펄스
    {
        public const float DeathPulseDurationSeconds = 0.34f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Renderer[] renderers; // 색상을 바꿀 모든 렌더러

        private MaterialPropertyBlock block; // Material 복제 없는 색상 변경
        private Color[] baseColors; // 렌더러별 원래 색
        private Vector3 baseScale; // 원래 크기
        private float pulseRemaining; // 남은 연출 시간
        private float pulseDuration; // 전체 연출 시간
        private float pulseStrength; // 크기 변화 세기
        private Color pulseColor; // 펄스 강조색

        private void Awake()
        {
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }

            block = new MaterialPropertyBlock();
            baseScale = transform.localScale;
            baseColors = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i] == null ? null : renderers[i].sharedMaterial;
                if (material != null && material.HasProperty(BaseColorId))
                {
                    baseColors[i] = material.GetColor(BaseColorId);
                }
                else if (material != null && material.HasProperty(ColorId))
                {
                    baseColors[i] = material.GetColor(ColorId);
                }
                else
                {
                    baseColors[i] = Color.white;
                }
            }
        }

        private void OnEnable()
        {
            ResetVisual();
        }

        public void PlayHit()
        {
            PlayPulse(new Color(1f, 0.35f, 0.28f), 0.12f, 0.09f);
        }

        public void PlayDeath()
        {
            PlayPulse(new Color(1f, 0.85f, 0.25f), DeathPulseDurationSeconds, 0.22f);
        }

        private void PlayPulse(Color color, float duration, float strength)
        {
            pulseColor = color;
            pulseDuration = Mathf.Max(0.02f, duration);
            pulseRemaining = pulseDuration;
            pulseStrength = strength;
        }

        private void Update()
        {
            if (pulseRemaining <= 0f)
            {
                return;
            }

            pulseRemaining = Mathf.Max(0f, pulseRemaining - Time.deltaTime);
            var ratio = 1f - pulseRemaining / pulseDuration;
            var bell = Mathf.Sin(ratio * Mathf.PI); // 시작·끝이 부드러운 곡선
            transform.localScale = baseScale * (1f + bell * pulseStrength);
            SetColor(Color.Lerp(Color.white, pulseColor, bell * 0.85f));
            if (pulseRemaining <= 0f)
            {
                ResetVisual();
            }
        }

        private void SetColor(Color multiplier)
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                var targetRenderer = renderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                var color = baseColors[i] * multiplier;
                color.a = baseColors[i].a;
                block.Clear();
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                targetRenderer.SetPropertyBlock(block); // 공유 Material 유지
            }
        }

        private void ResetVisual()
        {
            pulseRemaining = 0f;
            transform.localScale = baseScale;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                block.Clear();
                block.SetColor(BaseColorId, baseColors[i]);
                block.SetColor(ColorId, baseColors[i]);
                renderers[i].SetPropertyBlock(block);
            }
        }
    }
}
