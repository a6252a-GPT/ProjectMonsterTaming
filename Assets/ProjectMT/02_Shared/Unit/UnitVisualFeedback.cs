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
        private Color visualTint = Color.white; // 몬스터 Definition 색상 배율
        private Vector3 baseScale; // 원래 크기
        private float pulseRemaining; // 남은 연출 시간
        private float pulseDuration; // 전체 연출 시간
        private float pulseStrength; // 크기 변화 세기
        private Color pulseColor; // 펄스 강조색
        private bool baseStateReady; // Editor Preview에서도 같은 펄스 초기값 보장
        private Transform reactionRoot; // 판정 루트와 분리된 Visual 반동 대상
        private Vector3 reactionBaseLocalPosition;
        private Vector3 recoilStartOffset;
        private Vector3 recoilDirection;
        private float recoilDistance;
        private float recoilDuration;
        private float recoilElapsed;
        private float recoilDelay;
        private bool holdDeathRecoil;

        private void Awake()
        {
            EnsureBaseState();
            RefreshRenderers();
        }

        public void RefreshRenderers() // 모듈러 외형 교체 뒤 피격 대상 다시 수집
        {
            EnsureBaseState();
            renderers = GetComponentsInChildren<Renderer>(true);
            ResolveReactionRoot();
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

            ResetVisual();
        }

        private void OnEnable()
        {
            EnsureReady();
            ResetVisual();
        }

        public void PlayHit()
        {
            EnsureReady();
            if (pulseRemaining <= 0f)
            {
                PlayPulse(new Color(1f, 0.35f, 0.28f), 0.12f, 0.09f); // 연타 중 섬광 재시작 스냅 방지
            }
        }

        public void PlayImpact(
            Vector3 worldDirection,
            float distance,
            float duration,
            float delay,
            bool killed)
        {
            PlayHit();
            if (reactionRoot == null || worldDirection.sqrMagnitude < 0.0001f || distance <= 0f)
            {
                return;
            }

            if (recoilDistance > 0f && distance <= recoilDistance &&
                recoilElapsed < recoilDuration * 0.75f)
            {
                return; // 진행 중인 같은 강도 이하 반동은 중첩·재시작하지 않음
            }

            worldDirection.y = 0f;
            var parent = reactionRoot.parent;
            recoilDirection = parent != null
                ? parent.InverseTransformDirection(worldDirection.normalized)
                : worldDirection.normalized;
            recoilDirection.y = 0f;
            recoilDirection.Normalize();
            recoilStartOffset = reactionRoot.localPosition - reactionBaseLocalPosition;
            recoilDistance = Mathf.Max(recoilDistance, distance); // 연타는 거리 누적 없이 강한 값만 유지
            recoilDuration = Mathf.Max(0.05f, duration);
            recoilElapsed = 0f;
            recoilDelay = Mathf.Max(recoilDelay, delay);
            holdDeathRecoil |= killed;
        }

        public void PlayDeath()
        {
            EnsureReady();
            PlayPulse(new Color(1f, 0.85f, 0.25f), DeathPulseDurationSeconds, 0.22f);
        }

        public void SetTint(Color tint)
        {
            EnsureReady();
            visualTint = tint.a <= 0f ? Color.white : tint;
            ResetVisual();
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
            Tick(Time.deltaTime);
            TickRecoil(Time.unscaledDeltaTime);
        }

        public bool Tick(float deltaTime) // Runtime과 Maker Preview가 같은 펄스 곡선 사용
        {
            EnsureReady();
            if (pulseRemaining <= 0f)
            {
                return false;
            }

            pulseRemaining = Mathf.Max(0f, pulseRemaining - Mathf.Max(0f, deltaTime));
            var ratio = 1f - pulseRemaining / pulseDuration;
            var bell = Mathf.Sin(ratio * Mathf.PI); // 시작·끝이 부드러운 곡선
            transform.localScale = baseScale * (1f + bell * pulseStrength);
            SetColor(Color.Lerp(Color.white, pulseColor, bell * 0.85f));
            if (pulseRemaining <= 0f)
            {
                ResetPulse();
            }

            return pulseRemaining > 0f;
        }

        private void SetColor(Color multiplier)
        {
            if (renderers == null || baseColors == null)
            {
                return;
            }

            for (var i = 0; i < renderers.Length; i++)
            {
                var targetRenderer = renderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                var color = baseColors[i] * visualTint * multiplier;
                color.a = baseColors[i].a;
                block.Clear();
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                targetRenderer.SetPropertyBlock(block); // 공유 Material 유지
            }
        }

        private void ResetVisual()
        {
            EnsureBaseState();
            ResetPulse();
            if (reactionRoot != null)
            {
                reactionRoot.localPosition = reactionBaseLocalPosition;
            }

            recoilStartOffset = Vector3.zero;
            recoilDirection = Vector3.zero;
            recoilDistance = 0f;
            recoilDuration = 0f;
            recoilElapsed = 0f;
            recoilDelay = 0f;
            holdDeathRecoil = false;
            SetColor(Color.white);
        }

        private void ResetPulse()
        {
            pulseRemaining = 0f;
            transform.localScale = baseScale;
            SetColor(Color.white);
        }

        private void TickRecoil(float deltaTime)
        {
            if (reactionRoot == null || recoilDistance <= 0f)
            {
                return;
            }

            if (recoilDelay > 0f)
            {
                recoilDelay = Mathf.Max(0f, recoilDelay - Mathf.Max(0f, deltaTime));
                return;
            }

            recoilElapsed = Mathf.Min(recoilDuration, recoilElapsed + Mathf.Max(0f, deltaTime));
            var ratio = recoilDuration <= 0f ? 1f : recoilElapsed / recoilDuration;
            float offsetRatio;
            if (ratio < 0.24f)
            {
                var pushRatio = Mathf.SmoothStep(0f, 1f, ratio / 0.24f);
                var targetOffset = recoilDirection * recoilDistance;
                reactionRoot.localPosition = reactionBaseLocalPosition +
                                              Vector3.Lerp(recoilStartOffset, targetOffset, pushRatio);
                return; // 현재 위치에서 시작해 연타 방향 전환도 스냅 없이 연결
            }

            if (holdDeathRecoil)
            {
                offsetRatio = 1f; // 사망 동작 중 원위치 스냅 방지
            }
            else
            {
                offsetRatio = 1f - Mathf.SmoothStep(0f, 1f, (ratio - 0.24f) / 0.76f); // 한 번만 복귀
            }

            reactionRoot.localPosition = reactionBaseLocalPosition + recoilDirection * (recoilDistance * offsetRatio);
            if (ratio >= 1f && !holdDeathRecoil)
            {
                reactionRoot.localPosition = reactionBaseLocalPosition;
                recoilStartOffset = Vector3.zero;
                recoilDistance = 0f;
            }
        }

        private void ResolveReactionRoot()
        {
            var resolved = transform.Find("Visual") ?? transform.Find("VisualRoot");
            if (resolved == reactionRoot)
            {
                return;
            }

            if (reactionRoot != null)
            {
                reactionRoot.localPosition = reactionBaseLocalPosition;
            }

            reactionRoot = resolved;
            reactionBaseLocalPosition = reactionRoot != null ? reactionRoot.localPosition : Vector3.zero;
        }

        private void EnsureReady()
        {
            EnsureBaseState();
            ResolveReactionRoot();
            if (renderers == null || baseColors == null || renderers.Length != baseColors.Length)
            {
                RefreshRenderers();
            }
        }

        private void EnsureBaseState()
        {
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }

            if (baseStateReady)
            {
                return;
            }

            baseScale = transform.localScale;
            baseStateReady = true;
        }
    }
}
