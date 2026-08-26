using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [DisallowMultipleComponent]
    public sealed class UnitVisualFeedback : MonoBehaviour // 유닛 피격·사망 펄스
    {
        public const float DeathPulseDurationSeconds = 0.34f;
        private const float RecoilPushEndRatio = 0.12f;
        private const float RecoilHoldEndRatio = 0.24f;
        private const float RecoilRecoverEndRatio = 0.62f;

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
        private Vector3 recoilOffset;
        private float recoilDistance;
        private float recoilHeight;
        private float recoilLiftOffset;
        private float recoilDuration;
        private float recoilElapsed;
        private float recoilDelay;
        private bool holdDeathRecoil;
        private Vector3 attackLungeStartOffset;
        private Vector3 attackLungeDirection;
        private Vector3 attackLungeOffset;
        private float attackLungeDistance;
        private float attackLungeDuration;
        private float attackLungeElapsed;

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
            float height,
            float duration,
            float delay,
            bool killed)
        {
            PlayHit();
            if (reactionRoot == null || worldDirection.sqrMagnitude < 0.0001f ||
                (distance <= 0f && height <= 0f))
            {
                return;
            }

            if ((recoilDistance > 0f || recoilHeight > 0f) &&
                distance <= recoilDistance && height <= recoilHeight &&
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
            recoilStartOffset = recoilOffset;
            recoilDistance = Mathf.Max(recoilDistance, distance); // 연타는 거리 누적 없이 강한 값만 유지
            recoilHeight = Mathf.Max(recoilHeight, height);
            recoilDuration = Mathf.Max(0.05f, duration);
            recoilElapsed = 0f;
            recoilDelay = Mathf.Max(recoilDelay, delay);
            holdDeathRecoil |= killed;
        }

        public void PlayAttackLunge(Vector3 worldDirection, float distance, float duration)
        {
            EnsureReady();
            if (reactionRoot == null || worldDirection.sqrMagnitude < 0.0001f || distance <= 0f)
            {
                return;
            }

            if (attackLungeDistance > 0f && distance <= attackLungeDistance &&
                attackLungeElapsed < attackLungeDuration * 0.75f)
            {
                return; // 범위 공격 연타는 첫 전진만 유지
            }

            worldDirection.y = 0f;
            var parent = reactionRoot.parent;
            attackLungeDirection = parent != null
                ? parent.InverseTransformDirection(worldDirection.normalized)
                : worldDirection.normalized;
            attackLungeDirection.y = 0f;
            attackLungeDirection.Normalize();
            attackLungeStartOffset = attackLungeOffset;
            attackLungeDistance = Mathf.Max(attackLungeDistance, distance);
            attackLungeDuration = Mathf.Max(0.06f, duration);
            attackLungeElapsed = 0f;
        }

        public void PlayAttackRecoil(Vector3 worldForward, float distance, float duration)
        {
            PlayAttackLunge(-worldForward, distance, duration); // 원거리 발사 순간 뒤로 짧게 반동
        }

        public void PlayDeath()
        {
            EnsureReady();
            CancelAttackLunge();
            ApplyReactionPose();
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
            TickAttackLunge(Time.unscaledDeltaTime);
            ApplyReactionPose();
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
            recoilOffset = Vector3.zero;
            recoilDistance = 0f;
            recoilHeight = 0f;
            recoilLiftOffset = 0f;
            recoilDuration = 0f;
            recoilElapsed = 0f;
            recoilDelay = 0f;
            holdDeathRecoil = false;
            attackLungeStartOffset = Vector3.zero;
            attackLungeDirection = Vector3.zero;
            attackLungeOffset = Vector3.zero;
            attackLungeDistance = 0f;
            attackLungeDuration = 0f;
            attackLungeElapsed = 0f;
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
            if (reactionRoot == null || (recoilDistance <= 0f && recoilHeight <= 0f))
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
            var targetOffset = recoilDirection * recoilDistance;
            if (ratio < RecoilPushEndRatio)
            {
                var pushRatio = Mathf.Clamp01(ratio / RecoilPushEndRatio);
                pushRatio = 1f - Mathf.Pow(1f - pushRatio, 3f); // 첫 프레임에 퍽 튕김
                recoilOffset = Vector3.Lerp(recoilStartOffset, targetOffset, pushRatio);
                UpdateRecoilLift(ratio);
                return;
            }

            if (holdDeathRecoil)
            {
                recoilOffset = targetOffset; // 사망 동작 중 원위치 스냅 방지
                recoilLiftOffset = Mathf.Sin(ratio * Mathf.PI) * recoilHeight;
                return;
            }

            if (ratio < RecoilHoldEndRatio)
            {
                recoilOffset = targetOffset; // 짧은 정점으로 타격 위치를 읽힘
                UpdateRecoilLift(ratio);
                return;
            }

            if (ratio < RecoilRecoverEndRatio)
            {
                var recoverRatio = Mathf.SmoothStep(
                    0f,
                    1f,
                    (ratio - RecoilHoldEndRatio) / (RecoilRecoverEndRatio - RecoilHoldEndRatio));
                recoilOffset = targetOffset * (1f - recoverRatio);
                UpdateRecoilLift(ratio);
                return;
            }

            recoilStartOffset = Vector3.zero;
            recoilDirection = Vector3.zero;
            recoilOffset = Vector3.zero;
            recoilDistance = 0f;
            recoilHeight = 0f;
            recoilLiftOffset = 0f;
            recoilDuration = 0f;
            recoilElapsed = 0f;
            recoilDelay = 0f;
        }

        private void UpdateRecoilLift(float ratio)
        {
            var liftRatio = Mathf.Clamp01(ratio / RecoilRecoverEndRatio);
            recoilLiftOffset = Mathf.Sin(liftRatio * Mathf.PI) * recoilHeight;
        }

        private void TickAttackLunge(float deltaTime)
        {
            if (reactionRoot == null || attackLungeDistance <= 0f)
            {
                return;
            }

            attackLungeElapsed = Mathf.Min(
                attackLungeDuration,
                attackLungeElapsed + Mathf.Max(0f, deltaTime));
            var ratio = attackLungeDuration <= 0f ? 1f : attackLungeElapsed / attackLungeDuration;
            if (ratio < 0.22f)
            {
                var pushRatio = Mathf.SmoothStep(0f, 1f, ratio / 0.22f);
                attackLungeOffset = Vector3.Lerp(
                    attackLungeStartOffset,
                    attackLungeDirection * attackLungeDistance,
                    pushRatio);
                return;
            }

            var recoverRatio = Mathf.SmoothStep(0f, 1f, (ratio - 0.22f) / 0.78f);
            attackLungeOffset = attackLungeDirection * (attackLungeDistance * (1f - recoverRatio));
            if (ratio >= 1f)
            {
                CancelAttackLunge();
            }
        }

        private void CancelAttackLunge()
        {
            attackLungeStartOffset = Vector3.zero;
            attackLungeDirection = Vector3.zero;
            attackLungeOffset = Vector3.zero;
            attackLungeDistance = 0f;
            attackLungeDuration = 0f;
            attackLungeElapsed = 0f;
        }

        private void ApplyReactionPose()
        {
            if (reactionRoot == null)
            {
                return;
            }

            reactionRoot.localPosition = reactionBaseLocalPosition + recoilOffset + attackLungeOffset +
                                         Vector3.up * recoilLiftOffset;
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
