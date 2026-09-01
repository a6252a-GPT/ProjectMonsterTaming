using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    // 제한시간 전멸기의 경고시간·연출·피해·런타임 정리를 전담한다.
    public sealed class FallenCommanderTimeoutWipePattern
    {
        private static readonly Color TelegraphColor =
            FallenCommanderTelegraphPalette.Danger;

        private FallenCommanderTimeoutWipeData data;
        private UnitActor bossActor;
        private Transform commanderRoot;
        private HealthComponent commanderHealth;
        private FallenCommanderBossAnimationPresenter animationPresenter;
        private Transform effectParent;
        private Transform groundAnchor;
        private GameObject startVfxInstance;
        private FallenCommanderTelegraphView telegraph;
        private float startedRealtime = -1f;
        private float telegraphHoldDuration;
        private Vector3 bossStartPosition;
        private bool hasBossStartPosition;
        private FallenCommanderTimeoutBossPositionLock bossPositionLock;
        private bool isDamagePending;
        private bool hasAppliedDamage;
        private bool isDescending;
        private float damageDelayRemaining;
        private float castMotionRemaining;
        private float descentElapsed;

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
            groundAnchor = effectParent == null ? null : effectParent.Find("Ground");
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
            if (bossActor != null)
            {
                bossStartPosition = bossActor.transform.position;
                hasBossStartPosition = true;
                bossPositionLock = bossActor.GetComponent<FallenCommanderTimeoutBossPositionLock>();
                if (bossPositionLock == null)
                {
                    bossPositionLock = bossActor.gameObject.AddComponent<
                        FallenCommanderTimeoutBossPositionLock>();
                }

                bossPositionLock.SetPosition(bossStartPosition);
            }

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
                commanderRoot,
                ground: groundAnchor,
                clampHeightToGround: data?.ClampVfxToGround ?? true);
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

            var safeDeltaTime = Mathf.Max(0f, unscaledDeltaTime);
            if (isDescending)
            {
                TickDescent(safeDeltaTime);
                return !IsActive;
            }

            RemainingWarningTime = Mathf.Max(
                0f,
                RemainingWarningTime - safeDeltaTime);
            UpdateBossRise();
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

            if (!isDamagePending)
            {
                BeginResolve();
            }

            damageDelayRemaining = Mathf.Max(
                0f,
                damageDelayRemaining - safeDeltaTime);
            castMotionRemaining = Mathf.Max(0f, castMotionRemaining - safeDeltaTime);
            if (!hasAppliedDamage && damageDelayRemaining <= 0f)
            {
                ApplyDamage();
                hasAppliedDamage = true;
            }

            if (castMotionRemaining > 0f || !hasAppliedDamage)
            {
                return false;
            }

            BeginDescent();
            return !IsActive;
        }

        // 발동 연출을 재생한 뒤 설정된 시간만큼 피해 판정을 기다린다.
        private void BeginResolve()
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
                commanderRoot,
                ground: groundAnchor,
                clampHeightToGround: data?.ClampVfxToGround ?? true);
            isDamagePending = true;
            damageDelayRemaining = data?.DamageDelay ?? 0f;
            castMotionRemaining = data?.CastMotionDuration ?? 0f;
            SetBossPosition(GetPeakPosition());
        }

        // 전멸 범위는 전체 전장이므로 지연 후 군단장의 남은 하트를 모두 제거한다.
        private void ApplyDamage()
        {
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

        }

        // 진행 중인 전멸기와 시작 VFX 및 런타임 참조를 초기 상태로 정리한다.
        public void Cancel()
        {
            RestoreBossPosition();
            DestroyTelegraph();
            DestroyStartVfx();
            IsActive = false;
            RemainingWarningTime = 0f;
            WarningDuration = 0f;
            telegraphHoldDuration = 0f;
            ResultDelay = 0f;
            startedRealtime = -1f;
            isDamagePending = false;
            hasAppliedDamage = false;
            isDescending = false;
            damageDelayRemaining = 0f;
            castMotionRemaining = 0f;
            descentElapsed = 0f;
            data = null;
            ReleaseRuntimeReferences();
        }

        private void UpdateBossRise()
        {
            if (!hasBossStartPosition || bossActor == null || data == null)
            {
                return;
            }

            var totalDuration = Mathf.Max(0.01f, WarningDuration + telegraphHoldDuration);
            var progress = Mathf.Clamp01(1f - RemainingWarningTime / totalDuration);
            var curveProgress = data.RiseCurve == null
                ? Mathf.SmoothStep(0f, 1f, progress)
                : Mathf.Clamp01(data.RiseCurve.Evaluate(progress));
            SetBossPosition(bossStartPosition +
                Vector3.up * data.RiseHeight * curveProgress);
        }

        private void BeginDescent()
        {
            isDescending = true;
            descentElapsed = 0f;
            if (data?.DescentDuration <= 0f)
            {
                SetBossPosition(bossStartPosition);
                Complete();
            }
        }

        private void TickDescent(float deltaTime)
        {
            if (!hasBossStartPosition || bossActor == null)
            {
                Complete();
                return;
            }

            var duration = data?.DescentDuration ?? 0f;
            descentElapsed = Mathf.Min(duration, descentElapsed + deltaTime);
            var progress = duration <= 0f
                ? 1f
                : Mathf.Clamp01(descentElapsed / duration);
            var curveProgress = data?.DescentCurve == null
                ? Mathf.SmoothStep(0f, 1f, progress)
                : Mathf.Clamp01(data.DescentCurve.Evaluate(progress));
            SetBossPosition(Vector3.Lerp(
                GetPeakPosition(),
                bossStartPosition,
                curveProgress));
            if (progress >= 1f)
            {
                Complete();
            }
        }

        private void Complete()
        {
            SetBossPosition(bossStartPosition);
            ReleaseBossPositionLock();
            IsActive = false;
            RemainingWarningTime = 0f;
            isDamagePending = false;
            hasAppliedDamage = false;
            isDescending = false;
            damageDelayRemaining = 0f;
            castMotionRemaining = 0f;
            descentElapsed = 0f;
        }

        private void RestoreBossPosition()
        {
            if (hasBossStartPosition && bossActor != null)
            {
                SetBossPosition(bossStartPosition);
            }

            ReleaseBossPositionLock();

            bossStartPosition = Vector3.zero;
            hasBossStartPosition = false;
        }

        private Vector3 GetPeakPosition()
        {
            return bossStartPosition + Vector3.up * (data?.RiseHeight ?? 0f);
        }

        private void SetBossPosition(Vector3 position)
        {
            if (bossPositionLock != null)
            {
                bossPositionLock.SetPosition(position);
                return;
            }

            if (bossActor != null)
            {
                bossActor.transform.position = position;
            }
        }

        private void ReleaseBossPositionLock()
        {
            if (bossPositionLock != null)
            {
                Object.Destroy(bossPositionLock);
                bossPositionLock = null;
            }
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
            ReleaseBossPositionLock();
            bossStartPosition = Vector3.zero;
            hasBossStartPosition = false;
            bossActor = null;
            commanderRoot = null;
            commanderHealth = null;
            animationPresenter = null;
            effectParent = null;
            groundAnchor = null;
        }
    }
}
