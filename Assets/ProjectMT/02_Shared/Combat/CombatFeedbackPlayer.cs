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
        [SerializeField, Min(1)] private int maxHitVfxPerFrame = 6; // 프레임당 VFX 상한
        [SerializeField, Min(1)] private int maxCameraImpulsesPerFrame = 1; // 프레임당 흔들림 상한

        private int hitVfxThisFrame;
        private int impulsesThisFrame;

        public void PlayHit(UnitActor target, DamageReport report)
        {
            target?.VisualFeedback?.PlayHit();
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
            PlayImpulse(0.08f);
        }

        public void PlayClimax(Vector3 position)
        {
            if (poolScope != null && hitVfxPrefab != null)
            {
                var instance = poolScope.Rent(hitVfxPrefab, position, Quaternion.identity);
                instance?.GetComponent<SeedFeedbackVfx>()?.Play(poolScope, new Color(1f, 0.45f, 0.15f), 0.6f, 0.8f);
            }

            PlayImpulse(0.24f); // 클라이맥스는 강하게 흔듦
        }

        private void PlayImpulse(float strength)
        {
            if (cameraImpulse == null || impulsesThisFrame >= maxCameraImpulsesPerFrame)
            {
                return;
            }

            impulsesThisFrame++;
            cameraImpulse.Impulse(strength);
        }

        private void LateUpdate()
        {
            hitVfxThisFrame = 0; // 다음 프레임 예산 복구
            impulsesThisFrame = 0; // 다음 프레임 예산 복구
        }

#if UNITY_EDITOR
        public void EditorConfigure(ScenePoolScope pool, GameObject hitVfx, CameraImpulseRig impulse)
        {
            poolScope = pool;
            hitVfxPrefab = hitVfx;
            cameraImpulse = impulse;
        }
#endif
    }
}
