using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Pooling;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatFeedbackPlayer : MonoBehaviour, ICombatFeedbackPlayer // 공용 피격·사망 연출 재생
    {
        [SerializeField] private ScenePoolScope poolScope; // VFX 재사용 창고
        [SerializeField] private GameObject hitVfxPrefab; // 공용 타격 이펙트
        [SerializeField] private CameraImpulseRig cameraImpulse; // 카메라 흔들림 장치
        [SerializeField] private FloatingNumberPresenter floatingNumbers; // 풀링 피해 숫자
        [SerializeField] private SfxPool sfxPool; // 현재 전투 범위 SFX Voice 풀
        [SerializeField] private SfxCue hitSfx; // 일반 피격음
        [SerializeField] private SfxCue deathSfx; // 사망음
        [SerializeField] private SfxCue weakClimaxSfx; // 약한 클라이맥스음
        [SerializeField] private SfxCue strongClimaxSfx; // 강한 클라이맥스음
        [SerializeField, Min(1)] private int maxHitVfxPerFrame = 6; // 프레임당 VFX 상한
        [SerializeField, Min(1)] private int maxCameraImpulsesPerFrame = 1; // 프레임당 흔들림 상한

        private int hitVfxThisFrame;
        private int impulsesThisFrame;
        private float strongestImpulseThisFrame; // 같은 프레임의 더 강한 요청은 승격 허용

        private void Awake()
        {
            if (floatingNumbers == null)
            {
                floatingNumbers = GetComponent<FloatingNumberPresenter>();
            }

            if (sfxPool == null)
            {
                sfxPool = GetComponent<SfxPool>();
            }
        }

        public void PlayHit(UnitActor target, DamageReport report)
        {
            target?.VisualFeedback?.PlayHit();
            floatingNumbers?.ShowDamage(target, report);
            sfxPool?.Play(hitSfx, report.Request.HitPoint);
            if (poolScope != null && hitVfxPrefab != null && hitVfxThisFrame < maxHitVfxPerFrame)
            {
                hitVfxThisFrame++; // 과도한 동시 연출 제한
                var instance = poolScope.Rent(hitVfxPrefab, report.Request.HitPoint, Quaternion.identity);
                instance?.GetComponent<SeedFeedbackVfx>()?.Play(poolScope, new Color(1f, 0.88f, 0.35f), 0.22f, 0.25f);
            }
        }

        public void PlayDeath(UnitActor target, DamageReport report)
        {
            target?.VisualFeedback?.PlayDeath();
            sfxPool?.Play(deathSfx, report.Request.HitPoint);
            PlayImpulse(0.08f);
        }

        public void PlayClimax(Vector3 position, CombatClimaxStrength strength)
        {
            var isStrong = strength == CombatClimaxStrength.Strong;
            var color = isStrong
                ? new Color(1f, 0.45f, 0.15f)
                : new Color(1f, 0.78f, 0.25f);
            var duration = isStrong ? 0.6f : 0.34f;
            var size = isStrong ? 0.8f : 0.38f;
            var impulse = isStrong ? 0.24f : 0.1f;
            sfxPool?.Play(isStrong ? strongClimaxSfx : weakClimaxSfx, position);
            if (poolScope != null && hitVfxPrefab != null)
            {
                var instance = poolScope.Rent(hitVfxPrefab, position, Quaternion.identity);
                instance?.GetComponent<SeedFeedbackVfx>()?.Play(poolScope, color, duration, size);
            }

            PlayImpulse(impulse);
        }

        public void PlayDamage(Vector3 position, float amount, FloatingNumberStyle style, int mergeKey)
        {
            floatingNumbers?.Queue(position, amount, style, mergeKey); // 비 UnitActor 대상의 확정 피해 표시
            sfxPool?.Play(hitSfx, position);
        }

        private void PlayImpulse(float strength)
        {
            strength = Mathf.Max(0f, strength);
            if (cameraImpulse == null || strength <= 0f)
            {
                return;
            }

            if (impulsesThisFrame >= maxCameraImpulsesPerFrame && strength <= strongestImpulseThisFrame)
            {
                return;
            }

            if (impulsesThisFrame < maxCameraImpulsesPerFrame)
            {
                impulsesThisFrame++;
            }

            strongestImpulseThisFrame = Mathf.Max(strongestImpulseThisFrame, strength);
            cameraImpulse.Impulse(strength);
        }

        private void LateUpdate()
        {
            hitVfxThisFrame = 0; // 다음 프레임 예산 복구
            impulsesThisFrame = 0; // 다음 프레임 예산 복구
            strongestImpulseThisFrame = 0f;
        }

#if UNITY_EDITOR
        public void EditorConfigure(ScenePoolScope pool, GameObject hitVfx, CameraImpulseRig impulse)
        {
            poolScope = pool;
            hitVfxPrefab = hitVfx;
            cameraImpulse = impulse;
        }

        public void EditorConfigureExtensions(FloatingNumberPresenter numbers, SfxPool audioPool)
        {
            floatingNumbers = numbers;
            sfxPool = audioPool;
        }
#endif
    }
}
