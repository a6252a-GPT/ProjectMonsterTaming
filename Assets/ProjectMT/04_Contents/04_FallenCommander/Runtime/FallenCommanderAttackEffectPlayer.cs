using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    // 공격 데이터에 지정된 일회성 VFX와 SFX를 시전·적중 시점에 재생한다.
    public static class FallenCommanderAttackEffectPlayer
    {
        // 공격 시전 슬롯에 지정된 VFX와 SFX를 재생한다.
        public static void PlayStart(
            FallenCommanderAttackEffectData effects,
            Vector3 position,
            Vector3 direction,
            Transform parent)
        {
            if (effects == null)
            {
                return;
            }

            PlayVfx(effects.StartVfxPrefab, position, direction, parent);
            PlaySfx(effects.StartSfx, position, effects.SfxVolume);
        }

        // 공격 적중 슬롯에 지정된 VFX와 SFX를 재생한다.
        public static void PlayResolve(
            FallenCommanderAttackEffectData effects,
            Vector3 position,
            Vector3 direction,
            Transform parent)
        {
            if (effects == null)
            {
                return;
            }

            PlayVfx(effects.ResolveVfxPrefab, position, direction, parent);
            PlaySfx(effects.ResolveSfx, position, effects.SfxVolume);
        }

        // VFX 프리팹을 공격 방향으로 생성하고 파티클 재생시간 뒤 제거한다.
        private static void PlayVfx(
            GameObject prefab,
            Vector3 position,
            Vector3 direction,
            Transform parent)
        {
            if (prefab == null)
            {
                return;
            }

            var rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : Quaternion.identity;
            var instance = Object.Instantiate(prefab, position, rotation, parent);
            Object.Destroy(instance, ResolveLifetime(instance));
        }

        // 지정된 AudioClip을 공격 위치에서 일회성으로 재생한다.
        private static void PlaySfx(AudioClip clip, Vector3 position, float volume)
        {
            if (clip == null)
            {
                return;
            }

            EnsureAudioListener();
            AudioSource.PlayClipAtPoint(
                clip,
                position,
                Mathf.Clamp01(volume));
        }

        // 활성 AudioListener가 없을 때만 현재 카메라에 런타임 Listener를 추가한다.
        private static void EnsureAudioListener()
        {
            if (Object.FindFirstObjectByType<AudioListener>() != null)
            {
                return;
            }

            var activeCamera = Camera.main ?? Object.FindFirstObjectByType<Camera>();
            if (activeCamera != null)
            {
                activeCamera.gameObject.AddComponent<AudioListener>();
            }
        }

        // 자식 파티클이 끝날 때까지 유지할 최대 시간을 계산한다.
        private static float ResolveLifetime(GameObject instance)
        {
            var lifetime = 2f;
            foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                lifetime = Mathf.Max(
                    lifetime,
                    main.duration + main.startLifetime.constantMax);
            }

            return Mathf.Clamp(lifetime, 0.1f, 10f);
        }
    }
}
