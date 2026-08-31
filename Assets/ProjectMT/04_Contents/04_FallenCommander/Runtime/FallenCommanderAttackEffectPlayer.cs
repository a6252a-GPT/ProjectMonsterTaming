using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    // 공격 데이터에 지정된 일회성 VFX와 SFX를 시전·발동·적중 시점에 각각 재생한다.
    public static class FallenCommanderAttackEffectPlayer
    {
        // 공격 시전 슬롯에 지정된 VFX와 SFX를 재생한다.
        public static GameObject PlayStart(
            FallenCommanderAttackEffectData effects,
            Vector3 position,
            Vector3 direction,
            Transform parent,
            Transform boss = null,
            Transform commander = null,
            Transform projectile = null,
            Transform ground = null,
            bool clampHeightToGround = false)
        {
            if (effects == null)
            {
                return null;
            }

            var context = CreatePlacementContext(
                position,
                direction,
                boss,
                commander,
                projectile,
                ground,
                clampHeightToGround);
            var placement = FallenCommanderEffectPlacementResolver.Resolve(
                effects,
                FallenCommanderEffectStage.Start,
                context);
            var instance = PlayVfx(
                effects.StartVfxPrefab,
                effects.StartVfxDuration,
                parent,
                placement);
            PlaySfx(
                effects.StartSfx,
                effects.StartSfxDuration,
                placement.AnchorPosition,
                effects.SfxVolume);
            return instance;
        }

        // 공격 발동 또는 해결 슬롯에 지정된 VFX와 SFX를 재생한다.
        public static GameObject PlayResolve(
            FallenCommanderAttackEffectData effects,
            Vector3 position,
            Vector3 direction,
            Transform parent,
            Transform boss = null,
            Transform commander = null,
            Transform projectile = null,
            Transform ground = null,
            bool clampHeightToGround = false)
        {
            var instance = PlayResolveVfx(
                effects,
                position,
                direction,
                parent,
                boss,
                commander,
                projectile,
                Vector3.one,
                ground,
                clampHeightToGround);
            PlayResolveSfx(
                effects,
                position,
                direction,
                boss,
                commander,
                projectile,
                ground,
                clampHeightToGround);
            return instance;
        }

        public static GameObject PlayResolveVfx(
            FallenCommanderAttackEffectData effects,
            Vector3 position,
            Vector3 direction,
            Transform parent,
            Transform boss = null,
            Transform commander = null,
            Transform projectile = null,
            Vector3 areaScale = default,
            Transform ground = null,
            bool clampHeightToGround = false)
        {
            if (effects == null)
            {
                return null;
            }

            var context = CreatePlacementContext(
                position,
                direction,
                boss,
                commander,
                projectile,
                ground,
                clampHeightToGround);
            var placement = FallenCommanderEffectPlacementResolver.Resolve(
                effects,
                FallenCommanderEffectStage.Resolve,
                context);
            return PlayVfx(
                effects.ResolveVfxPrefab,
                effects.ResolveVfxDuration,
                parent,
                placement,
                ResolveAreaScale(areaScale));
        }

        public static void PlayResolveSfx(
            FallenCommanderAttackEffectData effects,
            Vector3 position,
            Vector3 direction,
            Transform boss = null,
            Transform commander = null,
            Transform projectile = null,
            Transform ground = null,
            bool clampHeightToGround = false)
        {
            if (effects == null)
            {
                return;
            }

            var context = CreatePlacementContext(
                position,
                direction,
                boss,
                commander,
                projectile,
                ground,
                clampHeightToGround);
            var placement = FallenCommanderEffectPlacementResolver.Resolve(
                effects,
                FallenCommanderEffectStage.Resolve,
                context);
            PlaySfx(
                effects.ResolveSfx,
                effects.ResolveSfxDuration,
                placement.AnchorPosition,
                effects.SfxVolume);
        }

        // 실제 피해가 확정된 대상 위치에 독립된 적중 VFX와 SFX를 재생한다.
        public static GameObject PlayHit(
            FallenCommanderAttackEffectData effects,
            Vector3 position,
            Vector3 direction,
            Transform parent,
            Transform boss = null,
            Transform commander = null,
            Transform projectile = null)
        {
            if (effects == null)
            {
                return null;
            }

            var context = CreatePlacementContext(
                position,
                direction,
                boss,
                commander,
                projectile);
            var placement = FallenCommanderEffectPlacementResolver.Resolve(
                effects,
                FallenCommanderEffectStage.Hit,
                context);
            var instance = PlayVfx(
                effects.HitVfxPrefab,
                effects.HitVfxDuration,
                parent,
                placement);
            PlaySfx(
                effects.HitSfx,
                effects.HitSfxDuration,
                placement.AnchorPosition,
                effects.HitSfxVolume);
            return instance;
        }

        // VFX 프리팹을 공격 방향으로 생성하고 파티클 재생시간 뒤 제거한다.
        private static GameObject PlayVfx(
            GameObject prefab,
            float duration,
            Transform parent,
            FallenCommanderEffectPlacement placement,
            Vector3 areaScale = default)
        {
            if (prefab == null)
            {
                return null;
            }

            var instance = Object.Instantiate(
                prefab,
                placement.Position,
                placement.Rotation,
                parent);
            instance.transform.localScale = Vector3.Scale(
                instance.transform.localScale,
                Vector3.Scale(placement.Scale, ResolveAreaScale(areaScale)));
            RestartParticles(instance);
            Object.Destroy(instance, ResolveVfxLifetime(instance, duration));
            return instance;
        }

        // 프리팹 저장 당시의 파티클 재생 상태와 관계없이 생성 시점부터 연출을 시작한다.
        private static void RestartParticles(GameObject root)
        {
            var particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var index = 0; index < particles.Length; index++)
            {
                particles[index].Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
                particles[index].Play(true);
            }
        }

        private static Vector3 ResolveAreaScale(Vector3 scale)
        {
            return scale == Vector3.zero ? Vector3.one : scale;
        }

        // 런타임 Transform 참조를 공통 배치 계산기가 사용하는 값 형식 문맥으로 변환한다.
        private static FallenCommanderEffectPlacementContext CreatePlacementContext(
            Vector3 attackPosition,
            Vector3 attackDirection,
            Transform boss,
            Transform commander,
            Transform projectile,
            Transform ground = null,
            bool clampHeightToGround = false)
        {
            return new FallenCommanderEffectPlacementContext(
                attackPosition,
                attackDirection,
                boss == null ? (Vector3?)null : boss.position,
                commander == null ? (Vector3?)null : commander.position,
                projectile == null ? (Vector3?)null : projectile.position,
                ground == null ? (Vector3?)null : ground.position,
                clampHeightToGround);
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
