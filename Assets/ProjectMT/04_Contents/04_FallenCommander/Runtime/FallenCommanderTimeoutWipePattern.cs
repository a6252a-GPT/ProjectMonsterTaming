using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    // 제한시간 전멸기의 경고시간·연출·피해·런타임 정리를 전담한다.
    public sealed class FallenCommanderTimeoutWipePattern
    {
        private static readonly Color TelegraphColor =
            new Color(0.85f, 0.05f, 0.18f, 0.85f);

        private FallenCommanderTimeoutWipeData data;
        private UnitActor bossActor;
        private Transform commanderRoot;
        private HealthComponent commanderHealth;
        private FallenCommanderBossAnimationPresenter animationPresenter;
        private Transform effectParent;
        private GameObject startVfxInstance;
        private FallenCommanderTelegraphView telegraph;
        private float startedRealtime = -1f;
        private float telegraphHoldDuration;

        public bool IsActive { get; private set; }
        public float RemainingWarningTime { get; private set; }
        public float WarningDuration { get; private set; }
        public float ResultDelay { get; private set; }

        // 전멸기 데이터와 런타임 참조를 저장하고 시전 단계 연출을 시작한다.
        public bool Begin(
            FallenCommanderTimeoutWipeData wipeData,
            float fallbackWarningDuration,
            float fallbackResultDelay,
            UnitActor boss,
            Transform commander,
            HealthComponent health,
            FallenCommanderBossAnimationPresenter animations,
            Transform parent)
        {
            Cancel();
            if (commander == null || health == null)
            {
                return false;
            }

            data = wipeData;
            bossActor = boss;
            commanderRoot = commander;
            commanderHealth = health;
            animationPresenter = animations;
            effectParent = parent;
            WarningDuration = data == null
                ? Mathf.Max(0f, fallbackWarningDuration)
                : data.WarningDuration;
            telegraphHoldDuration = data?.TelegraphHoldDuration ?? 0f;
            ResultDelay = data == null
                ? Mathf.Max(0f, fallbackResultDelay)
                : data.ResultDelay;
            RemainingWarningTime = WarningDuration + telegraphHoldDuration;
            startedRealtime = Time.realtimeSinceStartup;
            IsActive = true;

            animationPresenter?.PlayPreCast(
                data?.PreCastMotion,
                playbackSpeed: data?.PreCastMotionSpeed ?? 1f,
                normalizedStart: data?.PreCastMotionStart ?? 0f,
                normalizedEnd: data?.PreCastMotionEnd ?? 1f);
            startVfxInstance = FallenCommanderAttackEffectPlayer.PlayStart(
                data?.Effects,
                bossActor == null ? Vector3.zero : bossActor.transform.position,
                bossActor == null ? Vector3.forward : bossActor.transform.forward,
                effectParent,
                bossActor == null ? null : bossActor.transform,
                commanderRoot);
            telegraph = FallenCommanderTelegraphView.CreateCircle(
                data?.TelegraphPrefab,
                effectParent,
                bossActor == null ? Vector3.zero : bossActor.transform.position,
                data?.Radius ?? 0.1f,
                TelegraphColor);
            telegraph?.SetProgress(0f);
            return true;
        }

        // 실제 시간 기준 경고시간을 갱신하고 전멸 피해 적용 완료 여부를 반환한다.
        public bool Tick(float unscaledDeltaTime)
        {
            if (!IsActive)
            {
                return false;
            }

            RemainingWarningTime = Mathf.Max(
                0f,
                RemainingWarningTime - Mathf.Max(0f, unscaledDeltaTime));
            var fillRemaining = Mathf.Max(
                0f,
                RemainingWarningTime - telegraphHoldDuration);
            telegraph?.SetProgress(WarningDuration <= 0f
                ? 1f
                : 1f - fillRemaining / WarningDuration);
            if (RemainingWarningTime > 0f)
            {
                return false;
            }

            Resolve();
            return true;
        }

        // 발동 연출을 재생하고 군단장의 남은 하트를 모두 제거한다.
        private void Resolve()
        {
            DestroyTelegraph();
            DestroyStartVfx();
            animationPresenter?.Play(
                data?.CastMotion,
                stopAfterMotion: true,
                durationOverride: data?.CastMotionDuration ?? 0f,
                playbackSpeed: data?.CastMotionSpeed ?? 1f,
                normalizedStart: data?.CastMotionStart ?? 0f,
                normalizedEnd: data?.CastMotionEnd ?? 1f);
            FallenCommanderAttackEffectPlayer.PlayResolve(
                data?.Effects,
                bossActor == null ? Vector3.zero : bossActor.transform.position,
                bossActor == null ? Vector3.forward : bossActor.transform.forward,
                effectParent,
                bossActor == null ? null : bossActor.transform,
                commanderRoot);

            if (commanderHealth != null && commanderHealth.IsAlive)
            {
                FallenCommanderAttackEffectPlayer.PlayHit(
                    data?.Effects,
                    commanderRoot.position,
                    bossActor == null ? Vector3.forward : bossActor.transform.forward,
                    effectParent,
                    bossActor == null ? null : bossActor.transform,
                    commanderRoot);
            }

            while (commanderHealth != null && commanderHealth.IsAlive)
            {
                commanderHealth.ApplyDamage(new DamageRequest(
                    bossActor,
                    1f,
                    commanderRoot.position));
            }

            IsActive = false;
            RemainingWarningTime = 0f;
            ReleaseRuntimeReferences();
        }

        // 진행 중인 전멸기와 시작 VFX 및 런타임 참조를 초기 상태로 정리한다.
        public void Cancel()
        {
            DestroyTelegraph();
            DestroyStartVfx();
            IsActive = false;
            RemainingWarningTime = 0f;
            WarningDuration = 0f;
            telegraphHoldDuration = 0f;
            ResultDelay = 0f;
            startedRealtime = -1f;
            data = null;
            ReleaseRuntimeReferences();
        }

        // 전멸기 시작부터 결과 반환까지의 실제 경과시간을 한 번 반환하고 기록을 초기화한다.
        public float ConsumeElapsedRealtime()
        {
            if (startedRealtime < 0f)
            {
                return -1f;
            }

            var elapsed = Time.realtimeSinceStartup - startedRealtime;
            startedRealtime = -1f;
            return elapsed;
        }

        // 시작 단계에 생성한 VFX가 남아 있으면 즉시 제거한다.
        private void DestroyStartVfx()
        {
            if (startVfxInstance != null)
            {
                Object.Destroy(startVfxInstance);
                startVfxInstance = null;
            }
        }

        // 진행 중인 전멸기 공격 범위가 남아 있으면 즉시 제거한다.
        private void DestroyTelegraph()
        {
            if (telegraph != null)
            {
                Object.Destroy(telegraph.gameObject);
                telegraph = null;
            }
        }

        // 패턴 종료 후 외부 런타임 오브젝트 참조만 해제한다.
        private void ReleaseRuntimeReferences()
        {
            bossActor = null;
            commanderRoot = null;
            commanderHealth = null;
            animationPresenter = null;
            effectParent = null;
        }
    }
}
