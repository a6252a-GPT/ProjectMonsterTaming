using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    public sealed class FallenCommanderBlackHolePattern
    {
        private enum PatternState
        {
            Inactive,
            Warning,
            Active
        }

        private FallenCommanderAttackData attack;
        private FallenCommanderAttackEffectData endEffects;
        private float activeDuration;
        private float coreRadius;
        private float spawnMinDistance;
        private float spawnMaxDistance;
        private float outerPullSpeed;
        private float innerPullSpeed;
        private AnimationCurve pullStrengthCurve;
        private Vector3 arenaCenter;
        private Vector2 arenaHalfExtents;
        private Color telegraphColor;

        private UnitActor bossActor;
        private Transform commanderRoot;
        private HealthComponent commanderHealth;
        private FallenCommanderBossAnimationPresenter animationPresenter;
        private Transform effectParent;
        private PatternState state;
        private float remainingTime;
        private bool hasDamagedCommander;
        private GameObject warningVfxInstance;
        private GameObject activeVfxInstance;

        public bool IsActive => state != PatternState.Inactive;
        public Vector3 CenterPosition { get; private set; }
        public FallenCommanderTelegraphView ActiveTelegraph { get; private set; }

        // 인스펙터에서 조정한 블랙홀 범위·흡입·연출 데이터를 런타임 모듈에 저장한다.
        public void Configure(
            FallenCommanderAttackData attackData,
            float duration,
            float damageRadius,
            float minimumSpawnDistance,
            float maximumSpawnDistance,
            float edgePullSpeed,
            float centerPullSpeed,
            AnimationCurve strengthCurve,
            Vector3 movementCenter,
            Vector2 movementHalfExtents,
            FallenCommanderAttackEffectData endingEffects,
            Color warningColor)
        {
            attack = attackData;
            activeDuration = Mathf.Max(0.1f, duration);
            coreRadius = Mathf.Max(0.1f, damageRadius);
            spawnMinDistance = Mathf.Max(0f, minimumSpawnDistance);
            spawnMaxDistance = Mathf.Max(spawnMinDistance, maximumSpawnDistance);
            outerPullSpeed = Mathf.Max(0f, edgePullSpeed);
            innerPullSpeed = Mathf.Max(outerPullSpeed, centerPullSpeed);
            pullStrengthCurve = strengthCurve;
            arenaCenter = movementCenter;
            arenaHalfExtents = new Vector2(
                Mathf.Max(0.1f, movementHalfExtents.x),
                Mathf.Max(0.1f, movementHalfExtents.y));
            endEffects = endingEffects;
            telegraphColor = warningColor;
        }

        // 플레이어 근처의 안전하게 보정된 위치에 블랙홀 경고를 시작한다.
        public bool Begin(
            UnitActor boss,
            Transform commander,
            HealthComponent health,
            CombatWorld combatWorld,
            FallenCommanderBossAnimationPresenter animations,
            Transform parent)
        {
            Cancel();
            if (attack == null ||
                attack.TelegraphPrefab == null ||
                boss == null ||
                commander == null ||
                health == null ||
                combatWorld == null ||
                animations == null)
            {
                return false;
            }

            bossActor = boss;
            commanderRoot = commander;
            commanderHealth = health;
            animationPresenter = animations;
            effectParent = parent;
            CenterPosition = ResolveSpawnPosition(commander.position);
            remainingTime = Mathf.Max(0.1f, attack.WarningDuration);
            hasDamagedCommander = false;
            state = PatternState.Warning;

            animationPresenter.Play(
                attack.PreCastMotion,
                playbackSpeed: attack.PreCastMotionSpeed);
            warningVfxInstance = FallenCommanderAttackEffectPlayer.PlayStart(
                attack.Effects,
                CenterPosition,
                Vector3.forward,
                effectParent);
            ActiveTelegraph = FallenCommanderTelegraphView.CreateCircle(
                attack.TelegraphPrefab,
                effectParent,
                CenterPosition,
                attack.Radius,
                telegraphColor);
            if (ActiveTelegraph != null)
            {
                return true;
            }

            Cancel();
            return false;
        }

        // 경고 진행도와 활성 중 흡입을 갱신하고 자연 종료 여부를 반환한다.
        public bool Tick(float deltaTime)
        {
            if (!IsActive)
            {
                return true;
            }

            var safeDeltaTime = Mathf.Max(0f, deltaTime);
            remainingTime = Mathf.Max(0f, remainingTime - safeDeltaTime);
            if (state == PatternState.Warning)
            {
                var warningDuration = Mathf.Max(0.1f, attack.WarningDuration);
                ActiveTelegraph?.SetProgress(1f - remainingTime / warningDuration);
                if (remainingTime <= 0f)
                {
                    BeginPulling();
                }

                return false;
            }

            PullCommander(safeDeltaTime);
            TryDamageCommander();
            if (remainingTime > 0f)
            {
                return false;
            }

            Complete();
            return true;
        }

        // 블랙홀 활성 연출과 공격 모션을 시작한다.
        private void BeginPulling()
        {
            state = PatternState.Active;
            remainingTime = activeDuration;
            ActiveTelegraph?.SetProgress(1f);
            DestroyEffect(ref warningVfxInstance);
            animationPresenter.Play(
                attack.CastMotion,
                stopAfterMotion: true,
                durationOverride: attack.CastMotionDuration,
                playbackSpeed: attack.CastMotionSpeed);
            activeVfxInstance = FallenCommanderAttackEffectPlayer.PlayResolve(
                attack.Effects,
                CenterPosition,
                Vector3.forward,
                effectParent);
        }

        // 중심에 가까울수록 강해지는 속도로 범위 안의 군단장을 끌어당긴다.
        private void PullCommander(float deltaTime)
        {
            if (commanderRoot == null ||
                commanderHealth == null ||
                !commanderHealth.IsAlive)
            {
                return;
            }

            var offset = CenterPosition - commanderRoot.position;
            offset.y = 0f;
            var distance = offset.magnitude;
            var outerRadius = Mathf.Max(coreRadius + 0.1f, attack.Radius);
            if (distance <= 0.001f || distance > outerRadius)
            {
                return;
            }

            var centerRatio = Mathf.Clamp01(
                (outerRadius - distance) / (outerRadius - coreRadius));
            var curvedRatio = pullStrengthCurve == null
                ? centerRatio
                : Mathf.Clamp01(pullStrengthCurve.Evaluate(centerRatio));
            var pullSpeed = Mathf.Lerp(outerPullSpeed, innerPullSpeed, curvedRatio);
            var pullDistance = Mathf.Min(distance, pullSpeed * deltaTime);
            commanderRoot.position += offset / distance * pullDistance;
        }

        // 중심부에 처음 들어온 순간에만 하트 한 칸 피해를 적용한다.
        private void TryDamageCommander()
        {
            if (hasDamagedCommander ||
                commanderRoot == null ||
                commanderHealth == null ||
                !commanderHealth.IsAlive)
            {
                return;
            }

            var offset = CenterPosition - commanderRoot.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > coreRadius * coreRadius)
            {
                return;
            }

            hasDamagedCommander = true;
            commanderHealth.ApplyDamage(new DamageRequest(
                bossActor,
                1f,
                commanderRoot.position));
        }

        // 활성 시간이 끝난 블랙홀의 종료 연출을 재생하고 임시 범위를 정리한다.
        private void Complete()
        {
            DestroyEffect(ref activeVfxInstance);
            FallenCommanderAttackEffectPlayer.PlayResolve(
                endEffects,
                CenterPosition,
                Vector3.forward,
                effectParent);
            ReleaseRuntimeState();
        }

        // 중단·브레이크·씬 종료 시 남은 블랙홀 범위와 참조를 즉시 정리한다.
        public void Cancel()
        {
            ReleaseRuntimeState();
        }

        // 플레이어 주변 후보 위치를 이동 가능 영역 안으로 보정한다.
        private Vector3 ResolveSpawnPosition(Vector3 commanderPosition)
        {
            var angle = Random.Range(0f, Mathf.PI * 2f);
            var distance = Random.Range(spawnMinDistance, spawnMaxDistance);
            var candidate = commanderPosition + new Vector3(
                Mathf.Cos(angle) * distance,
                0f,
                Mathf.Sin(angle) * distance);
            var outerRadius = Mathf.Max(coreRadius + 0.1f, attack.Radius);
            var allowedX = Mathf.Max(0f, arenaHalfExtents.x - outerRadius);
            var allowedZ = Mathf.Max(0f, arenaHalfExtents.y - outerRadius);
            candidate.x = Mathf.Clamp(
                candidate.x,
                arenaCenter.x - allowedX,
                arenaCenter.x + allowedX);
            candidate.z = Mathf.Clamp(
                candidate.z,
                arenaCenter.z - allowedZ,
                arenaCenter.z + allowedZ);
            candidate.y = arenaCenter.y;
            return candidate;
        }

        // 생성한 범위 오브젝트와 런타임 참조만 제거한다.
        private void ReleaseRuntimeState()
        {
            DestroyEffect(ref warningVfxInstance);
            DestroyEffect(ref activeVfxInstance);

            if (ActiveTelegraph != null)
            {
                Object.Destroy(ActiveTelegraph.gameObject);
                ActiveTelegraph = null;
            }

            state = PatternState.Inactive;
            remainingTime = 0f;
            hasDamagedCommander = false;
            bossActor = null;
            commanderRoot = null;
            commanderHealth = null;
            animationPresenter = null;
            effectParent = null;
        }

        // 블랙홀 모듈이 직접 생성한 진행 중 VFX만 안전하게 제거한다.
        private static void DestroyEffect(ref GameObject instance)
        {
            if (instance != null)
            {
                Object.Destroy(instance);
                instance = null;
            }
        }
    }
}
