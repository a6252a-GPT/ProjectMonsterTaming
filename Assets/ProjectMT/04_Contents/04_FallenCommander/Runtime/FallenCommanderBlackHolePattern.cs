using System.Collections.Generic;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    public sealed class FallenCommanderBlackHolePattern
    {
        private enum PatternState
        {
            Warning,
            Active
        }

        private sealed class RuntimeHole
        {
            public Vector3 CenterPosition;
            public PatternState State;
            public float RemainingTime;
            public bool HasDamagedCommander;
            public FallenCommanderTelegraphView Telegraph;
            public GameObject WarningVfxInstance;
            public GameObject ActiveVfxInstance;
        }

        private readonly List<RuntimeHole> activeHoles = new();
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
        private System.Action<float, System.Action> damageDelayScheduler;

        public bool IsActive => activeHoles.Count > 0;
        public Vector3 CenterPosition => activeHoles.Count > 0
            ? activeHoles[0].CenterPosition
            : Vector3.zero;
        public FallenCommanderTelegraphView ActiveTelegraph => activeHoles.Count > 0
            ? activeHoles[0].Telegraph
            : null;

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
            Color warningColor,
            System.Action<float, System.Action> delayScheduler)
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
            damageDelayScheduler = delayScheduler;
        }

        public bool Begin(
            FallenCommanderBlackHolePhaseData phaseData,
            UnitActor boss,
            Transform commander,
            HealthComponent health,
            CombatWorld combatWorld,
            FallenCommanderBossAnimationPresenter animations,
            Transform parent)
        {
            Cancel();
            if (attack == null || attack.TelegraphPrefab == null || boss == null ||
                commander == null || health == null || combatWorld == null || animations == null)
            {
                return false;
            }

            bossActor = boss;
            commanderRoot = commander;
            commanderHealth = health;
            animationPresenter = animations;
            effectParent = parent;

            animationPresenter.PlayPreCast(
                attack.PreCastMotion,
                playbackSpeed: attack.PreCastMotionSpeed,
                normalizedStart: attack.PreCastMotionStart,
                normalizedEnd: attack.PreCastMotionEnd);

            var minimumCount = phaseData?.MinimumCount ?? 1;
            var maximumCount = phaseData?.MaximumCount ?? minimumCount;
            var holeCount = Random.Range(minimumCount, maximumCount + 1);
            var minimumSpacing = phaseData?.MinimumCoreSpacing ?? 0f;
            var centers = new List<Vector3>(holeCount);
            for (var index = 0; index < holeCount; index++)
            {
                var hole = new RuntimeHole
                {
                    CenterPosition = ResolveSpawnPosition(commander.position, centers, minimumSpacing),
                    State = PatternState.Warning,
                    RemainingTime = Mathf.Max(0.1f, attack.WarningDuration) +
                        attack.TelegraphHoldDuration
                };
                centers.Add(hole.CenterPosition);
                hole.WarningVfxInstance = FallenCommanderAttackEffectPlayer.PlayStart(
                    attack.Effects,
                    hole.CenterPosition,
                    Vector3.forward,
                    effectParent,
                    bossActor.transform,
                    commanderRoot);
                hole.Telegraph = FallenCommanderTelegraphView.CreateCircle(
                    attack.TelegraphPrefab,
                    effectParent,
                    hole.CenterPosition,
                    attack.Radius,
                    telegraphColor);
                if (hole.Telegraph == null)
                {
                    DestroyHole(hole, false);
                    Cancel();
                    return false;
                }

                activeHoles.Add(hole);
            }

            return true;
        }

        public bool Tick(float deltaTime)
        {
            if (!IsActive)
            {
                return true;
            }

            var safeDeltaTime = Mathf.Max(0f, deltaTime);
            for (var index = activeHoles.Count - 1; index >= 0; index--)
            {
                var hole = activeHoles[index];
                hole.RemainingTime = Mathf.Max(0f, hole.RemainingTime - safeDeltaTime);
                if (hole.State == PatternState.Warning)
                {
                    TickWarning(hole);
                }
                else
                {
                    PullCommander(hole, safeDeltaTime);
                }

                if (hole.RemainingTime > 0f)
                {
                    continue;
                }

                if (hole.State == PatternState.Warning)
                {
                    BeginPulling(hole);
                    continue;
                }

                DestroyHole(hole, true);
                activeHoles.RemoveAt(index);
            }

            TryDamageCommander();
            if (activeHoles.Count > 0)
            {
                return false;
            }

            ReleaseReferences();
            return true;
        }

        public void Cancel()
        {
            for (var index = 0; index < activeHoles.Count; index++)
            {
                DestroyHole(activeHoles[index], false);
            }

            activeHoles.Clear();
            ReleaseReferences();
        }

        private void TickWarning(RuntimeHole hole)
        {
            var warningDuration = Mathf.Max(0.1f, attack.WarningDuration);
            var fillRemaining = Mathf.Max(0f, hole.RemainingTime - attack.TelegraphHoldDuration);
            hole.Telegraph?.SetProgress(1f - fillRemaining / warningDuration);
        }

        private void BeginPulling(RuntimeHole hole)
        {
            var isFirstActiveHole = !HasActiveHole();
            hole.State = PatternState.Active;
            hole.RemainingTime = activeDuration;
            DestroyTelegraph(hole);
            DestroyEffect(ref hole.WarningVfxInstance);
            if (isFirstActiveHole)
            {
                animationPresenter.Play(
                    attack.CastMotion,
                    stopAfterMotion: true,
                    durationOverride: attack.CastMotionDuration,
                    playbackSpeed: attack.CastMotionSpeed,
                    normalizedStart: attack.CastMotionStart,
                    normalizedEnd: attack.CastMotionEnd);
            }

            hole.ActiveVfxInstance = FallenCommanderAttackEffectPlayer.PlayResolve(
                attack.Effects,
                hole.CenterPosition,
                Vector3.forward,
                effectParent,
                bossActor.transform,
                commanderRoot);
        }

        private bool HasActiveHole()
        {
            for (var index = 0; index < activeHoles.Count; index++)
            {
                if (activeHoles[index].State == PatternState.Active)
                {
                    return true;
                }
            }

            return false;
        }

        private void PullCommander(RuntimeHole hole, float deltaTime)
        {
            if (commanderRoot == null || commanderHealth == null || !commanderHealth.IsAlive)
            {
                return;
            }

            var offset = hole.CenterPosition - commanderRoot.position;
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

        private void TryDamageCommander()
        {
            if (commanderRoot == null || commanderHealth == null || !commanderHealth.IsAlive)
            {
                return;
            }

            List<Vector3> hitCenters = null;
            for (var index = 0; index < activeHoles.Count; index++)
            {
                var hole = activeHoles[index];
                if (hole.State != PatternState.Active || hole.HasDamagedCommander ||
                    !IsCommanderInCore(hole.CenterPosition, commanderRoot.position))
                {
                    continue;
                }

                hole.HasDamagedCommander = true;
                hitCenters ??= new List<Vector3>();
                hitCenters.Add(hole.CenterPosition);
            }

            if (hitCenters == null)
            {
                return;
            }

            var attacker = bossActor;
            var target = commanderRoot;
            var targetHealth = commanderHealth;
            var effects = attack.Effects;
            var delay = attack.DamageDelay;
            var parent = effectParent;
            ScheduleDamage(delay, () =>
            {
                if (attacker == null || !attacker.IsAlive || target == null ||
                    targetHealth == null || !targetHealth.IsAlive)
                {
                    return;
                }

                for (var index = 0; index < hitCenters.Count; index++)
                {
                    if (!IsCommanderInCore(hitCenters[index], target.position))
                    {
                        continue;
                    }

                    FallenCommanderAttackEffectPlayer.PlayHit(
                        effects,
                        target.position,
                        Vector3.forward,
                        parent,
                        attacker.transform,
                        target);
                    targetHealth.ApplyDamage(new DamageRequest(attacker, 1f, target.position));
                    return;
                }
            });
        }

        private bool IsCommanderInCore(Vector3 center, Vector3 commanderPosition)
        {
            var offset = center - commanderPosition;
            offset.y = 0f;
            return offset.sqrMagnitude <= coreRadius * coreRadius;
        }

        private void ScheduleDamage(float delay, System.Action apply)
        {
            if (damageDelayScheduler == null)
            {
                apply?.Invoke();
                return;
            }

            damageDelayScheduler.Invoke(Mathf.Max(0f, delay), apply);
        }

        private Vector3 ResolveSpawnPosition(
            Vector3 commanderPosition,
            IReadOnlyList<Vector3> existingCenters,
            float minimumSpacing)
        {
            var bestCandidate = ClampToArena(commanderPosition);
            var bestDistance = -1f;
            for (var attempt = 0; attempt < 24; attempt++)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var distance = Random.Range(spawnMinDistance, spawnMaxDistance);
                var candidate = commanderPosition + new Vector3(
                    Mathf.Cos(angle) * distance,
                    0f,
                    Mathf.Sin(angle) * distance);
                candidate = ClampToArena(candidate);
                var nearestDistance = GetNearestCenterDistance(candidate, existingCenters);
                if (nearestDistance >= minimumSpacing)
                {
                    return candidate;
                }

                if (nearestDistance > bestDistance)
                {
                    bestDistance = nearestDistance;
                    bestCandidate = candidate;
                }
            }

            return bestCandidate;
        }

        private Vector3 ClampToArena(Vector3 candidate)
        {
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

        private static float GetNearestCenterDistance(
            Vector3 candidate,
            IReadOnlyList<Vector3> existingCenters)
        {
            if (existingCenters == null || existingCenters.Count == 0)
            {
                return float.PositiveInfinity;
            }

            var nearestDistance = float.PositiveInfinity;
            for (var index = 0; index < existingCenters.Count; index++)
            {
                var offset = candidate - existingCenters[index];
                offset.y = 0f;
                nearestDistance = Mathf.Min(nearestDistance, offset.magnitude);
            }

            return nearestDistance;
        }

        private void DestroyHole(RuntimeHole hole, bool playEndEffect)
        {
            DestroyEffect(ref hole.WarningVfxInstance);
            DestroyEffect(ref hole.ActiveVfxInstance);
            DestroyTelegraph(hole);
            if (playEndEffect)
            {
                FallenCommanderAttackEffectPlayer.PlayResolve(
                    endEffects,
                    hole.CenterPosition,
                    Vector3.forward,
                    effectParent,
                    bossActor == null ? null : bossActor.transform,
                    commanderRoot);
            }
        }

        private static void DestroyTelegraph(RuntimeHole hole)
        {
            if (hole.Telegraph == null)
            {
                return;
            }

            Object.Destroy(hole.Telegraph.gameObject);
            hole.Telegraph = null;
        }

        private void ReleaseReferences()
        {
            bossActor = null;
            commanderRoot = null;
            commanderHealth = null;
            animationPresenter = null;
            effectParent = null;
        }

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
