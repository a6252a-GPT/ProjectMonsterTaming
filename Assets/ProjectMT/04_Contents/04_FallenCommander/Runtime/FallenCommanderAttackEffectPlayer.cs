using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    // 공격 데이터에 지정된 일회성 VFX와 SFX를 시전·적중 시점에 재생한다.
    public static class FallenCommanderAttackEffectPlayer
    {
        // 공격 시전 슬롯에 지정된 VFX와 SFX를 재생한다.
        public static GameObject PlayStart(
            FallenCommanderAttackEffectData effects,
            Vector3 position,
            Vector3 direction,
            Transform parent)
        {
            if (effects == null)
            {
                return null;
            }

            var instance = PlayVfx(
                effects.StartVfxPrefab,
                effects.StartVfxDuration,
                position,
                direction,
                parent);
            PlaySfx(
                effects.StartSfx,
                effects.StartSfxDuration,
                position,
                effects.SfxVolume);
            return instance;
        }

        // 공격 적중 슬롯에 지정된 VFX와 SFX를 재생한다.
        public static GameObject PlayResolve(
            FallenCommanderAttackEffectData effects,
            Vector3 position,
            Vector3 direction,
            Transform parent)
        {
            if (effects == null)
            {
                return null;
            }

            var instance = PlayVfx(
                effects.ResolveVfxPrefab,
                effects.ResolveVfxDuration,
                position,
                direction,
                parent);
            PlaySfx(
                effects.ResolveSfx,
                effects.ResolveSfxDuration,
                position,
                effects.SfxVolume);
            return instance;
        }

        // VFX 프리팹을 공격 방향으로 생성하고 파티클 재생시간 뒤 제거한다.
        private static GameObject PlayVfx(
            GameObject prefab,
            float duration,
            Vector3 position,
            Vector3 direction,
            Transform parent)
        {
            if (prefab == null)
            {
                return null;
            }

            var rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : Quaternion.identity;
            var instance = Object.Instantiate(prefab, position, rotation, parent);
            Object.Destroy(instance, ResolveVfxLifetime(instance, duration));
            return instance;
        }

        // 지정된 AudioClip을 공격 위치에서 재생하고 설정된 유지시간 뒤 제거한다.
        private static void PlaySfx(
            AudioClip clip,
            float duration,
            Vector3 position,
            float volume)
        {
            if (clip == null)
            {
                return;
            }

            EnsureAudioListener();
            var audioObject = new GameObject($"FallenCommanderSfx_{clip.name}");
            audioObject.transform.position = position;

            var audioSource = audioObject.AddComponent<AudioSource>();
            audioSource.clip = clip;
            audioSource.volume = Mathf.Clamp01(volume);
            audioSource.spatialBlend = 1f;
            audioSource.Play();

            Object.Destroy(audioObject, ResolveSfxLifetime(clip, duration));
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

        // 설정값이 있으면 사용하고 없으면 자식 파티클의 최대 재생시간을 계산한다.
        private static float ResolveVfxLifetime(GameObject instance, float overrideDuration)
        {
            if (overrideDuration > 0f)
            {
                return Mathf.Max(0.01f, overrideDuration);
            }

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

        // 설정값이 있으면 클립 길이 안에서 제한하고 없으면 전체 클립 길이를 사용한다.
        private static float ResolveSfxLifetime(AudioClip clip, float overrideDuration)
        {
            var clipDuration = Mathf.Max(0.01f, clip.length);
            return overrideDuration > 0f
                ? Mathf.Clamp(overrideDuration, 0.01f, clipDuration)
                : clipDuration;
        }
    }
}
