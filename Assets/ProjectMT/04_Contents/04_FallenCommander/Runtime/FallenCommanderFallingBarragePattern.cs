using System.Collections.Generic;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    public sealed class FallenCommanderFallingBarragePattern
    {
        private enum PatternState
        {
            Inactive,
            Warning,
            Falling,
            WaveInterval
        }

        private sealed class PooledProjectile
        {
            public GameObject Projectile;
            public FallenCommanderTelegraphView Telegraph;
            public bool InUse;
        }

        private sealed class FallingShot
        {
            public Vector3 Target;
            public float StartDelay;
            public PooledProjectile Pooled;
            public bool LandingPresented;
            public bool ImpactResolved;
            public bool Resolved;
        }

        private sealed class WaveDamageState
        {
            public bool HasDamaged;
        }

        private readonly List<PooledProjectile> pool = new();
        private readonly List<FallingShot> shots = new();
        private readonly List<Vector3> selectedPositions = new();

        private FallenCommanderFallingBarrageData data;
        private FallenCommanderFallingBarragePhaseData phaseData;
        private UnitActor bossActor;
        private Transform commanderRoot;
        private HealthComponent commanderHealth;
        private FallenCommanderBossAnimationPresenter animationPresenter;
        private Transform effectParent;
        private Vector3 arenaCenter;
        private PatternState state;
        private float waveElapsed;
        private float remainingTime;
        private float attackStartDelayRemaining;
        private int waveIndex;
        private bool castMotionPlayed;
        private WaveDamageState waveDamageState;
        private System.Action<float, System.Action> damageDelayScheduler;

        public bool IsActive => state != PatternState.Inactive;
        public FallenCommanderTelegraphView ActiveTelegraph
        {
            get
            {
                for (var index = 0; index < shots.Count; index++)
                {
                    if (shots[index].Pooled?.Telegraph != null)
                    {
                        return shots[index].Pooled.Telegraph;
                    }
                }

                return null;
            }
        }

        public bool Begin(
            FallenCommanderFallingBarrageData patternData,
            FallenCommanderFallingBarragePhaseData currentPhaseData,
            UnitActor boss,
            Transform commander,
            HealthComponent health,
            FallenCommanderBossAnimationPresenter animations,
            Transform parent,
            Vector3 battlefieldCenter,
            System.Action<float, System.Action> delayScheduler)
        {
            CancelActiveShots();
            if (patternData == null || !patternData.TryValidate(out _) ||
                currentPhaseData == null || boss == null || commander == null ||
                health == null || parent == null)
            {
                return false;
            }

            data = patternData;
            phaseData = currentPhaseData;
            bossActor = boss;
            commanderRoot = commander;
            commanderHealth = health;
            animationPresenter = animations;
            effectParent = parent;
            arenaCenter = battlefieldCenter;
            damageDelayScheduler = delayScheduler;
            waveIndex = 0;
            castMotionPlayed = false;

            EnsurePool(data.InitialPoolSize);
            state = PatternState.Warning;
            remainingTime = data.WarningMessageDuration;
            attackStartDelayRemaining = data.BarrageStartDelay;
            if (remainingTime <= 0f || attackStartDelayRemaining <= 0f)
            {
                BeginAttack();
            }

            return true;
        }

        private void BeginAttack()
        {
            FallenCommanderAttackEffectPlayer.PlayStart(
                data.Effects,
                bossActor.transform.position,
                bossActor.transform.forward,
                effectParent,
                bossActor.transform,
                commanderRoot);
            BeginWave();
        }

        public bool Tick(float deltaTime)
        {
            if (!IsActive)
            {
                return true;
            }

            var safeDeltaTime = Mathf.Max(0f, deltaTime);
            if (state == PatternState.Warning)
            {
                remainingTime = Mathf.Max(0f, remainingTime - safeDeltaTime);
                attackStartDelayRemaining = Mathf.Max(
                    0f,
                    attackStartDelayRemaining - safeDeltaTime);
                if (attackStartDelayRemaining <= 0f)
                {
                    BeginAttack();
                }

                return false;
            }

            if (state == PatternState.WaveInterval)
            {
                remainingTime = Mathf.Max(0f, remainingTime - safeDeltaTime);
                if (remainingTime <= 0f)
                {
                    BeginWave();
                }

                return false;
            }

            waveElapsed += safeDeltaTime;
            var allResolved = true;
            for (var index = 0; index < shots.Count; index++)
            {
                var shot = shots[index];
                if (shot.Resolved)
                {
                    continue;
                }

                allResolved = false;
                if (!shot.ImpactResolved && shot.Pooled == null &&
                    waveElapsed >= shot.StartDelay)
                {
                    ActivateShot(shot);
                }

                if (shot.ImpactResolved)
                {
                    continue;
                }

                if (shot.Pooled == null)
                {
                    continue;
                }

                var normalizedTime = Mathf.Clamp01(
                    (waveElapsed - shot.StartDelay - data.AirHoldDuration) /
                    phaseData.FallDuration);
                var progress = data.EvaluateFallProgress(normalizedTime);
                shot.Pooled.Projectile.transform.position =
                    data.EvaluateProjectilePosition(shot.Target, normalizedTime);
                shot.Pooled.Telegraph?.SetProgress(progress);
                if (!shot.LandingPresented && normalizedTime >= 1f)
                {
                    shot.LandingPresented = true;
                    FallenCommanderAttackEffectPlayer.PlayResolve(
                        data.ImpactEffects,
                        shot.Target,
                        bossActor == null ? Vector3.forward : bossActor.transform.forward,
                        effectParent,
                        bossActor == null ? null : bossActor.transform,
                        commanderRoot,
                        shot.Pooled?.Projectile == null ? null : shot.Pooled.Projectile.transform);
                }
                if (normalizedTime >= 1f && waveElapsed >= shot.StartDelay +
                    data.AirHoldDuration + phaseData.FallDuration +
                    data.TelegraphHoldDuration)
                {
                    ResolveShot(shot);
                }
            }

            if (!allResolved && !AreAllShotsResolved())
            {
                return false;
            }

            waveIndex++;
            if (waveIndex >= phaseData.WaveCount)
            {
                ReleaseRuntimeState();
                return true;
            }

            state = PatternState.WaveInterval;
            remainingTime = phaseData.WaveInterval;
            return false;
        }

        private void BeginWave()
        {
            CancelActiveShots();
            shots.Clear();
            selectedPositions.Clear();
            waveElapsed = 0f;
            waveDamageState = new WaveDamageState();

            for (var index = 0; index < data.ProjectileCount; index++)
            {
                var baseDelay = index * phaseData.SpawnInterval;
                var jitter = Random.Range(
                    -phaseData.SpawnTimeJitter,
                    phaseData.SpawnTimeJitter);
                shots.Add(new FallingShot
                {
                    Target = SelectImpactPosition(),
                    StartDelay = Mathf.Max(0f, baseDelay + jitter)
                });
            }

            state = PatternState.Falling;
        }

        private Vector3 SelectImpactPosition()
        {
            var extents = data.ArenaHalfExtents;
            var commanderPosition = commanderRoot == null
                ? arenaCenter
                : commanderRoot.position;
            var fallback = arenaCenter;
            for (var attempt = 0; attempt < 24; attempt++)
            {
                var candidate = arenaCenter + new Vector3(
                    Random.Range(-extents.x, extents.x),
                    0f,
                    Random.Range(-extents.y, extents.y));
                fallback = candidate;
                if (HorizontalSqrDistance(candidate, commanderPosition) <
                    data.CommanderSafetyRadius * data.CommanderSafetyRadius)
                {
                    continue;
                }

                var isSpaced = true;
                for (var index = 0; index < selectedPositions.Count; index++)
                {
                    if (HorizontalSqrDistance(candidate, selectedPositions[index]) <
                        data.MinimumSpacing * data.MinimumSpacing)
                    {
                        isSpaced = false;
                        break;
                    }
                }

                if (!isSpaced)
                {
                    continue;
                }

                selectedPositions.Add(candidate);
                return candidate;
            }

            selectedPositions.Add(fallback);
            return fallback;
        }

        private void ActivateShot(FallingShot shot)
        {
            shot.Pooled = Rent();
            if (shot.Pooled == null)
            {
                shot.Resolved = true;
                return;
            }

            var start = shot.Target + Vector3.up * data.SpawnHeight;
            shot.Pooled.Projectile.transform.SetPositionAndRotation(start, Quaternion.identity);
            shot.Pooled.Projectile.SetActive(true);
            RestartParticles(shot.Pooled.Projectile);
            animationPresenter?.PlayPreCast(
                data.PreCastMotion,
                playbackSpeed: data.PreCastMotionSpeed,
                normalizedStart: data.PreCastMotionStart,
                normalizedEnd: data.PreCastMotionEnd);
            if (shot.Pooled.Telegraph != null)
            {
                shot.Pooled.Telegraph.transform.SetPositionAndRotation(
                    shot.Target + Vector3.up * FallenCommanderTelegraphView.GroundOffset,
                    Quaternion.identity);
                shot.Pooled.Telegraph.gameObject.SetActive(true);
                shot.Pooled.Telegraph.SetProgress(0f);
            }
        }

        private void ResolveShot(FallingShot shot)
        {
            if (shot == null || shot.ImpactResolved)
            {
                return;
            }

            shot.ImpactResolved = true;
            if (!castMotionPlayed)
            {
                castMotionPlayed = true;
                animationPresenter?.Play(
                    data.CastMotion,
                    stopAfterMotion: true,
                    durationOverride: data.CastMotionDuration,
                    playbackSpeed: data.CastMotionSpeed,
                    normalizedStart: data.CastMotionStart,
                    normalizedEnd: data.CastMotionEnd);
            }
            FallenCommanderAttackEffectPlayer.PlayResolve(
                data.Effects,
                shot.Target,
                bossActor == null ? Vector3.forward : bossActor.transform.forward,
                effectParent,
                bossActor == null ? null : bossActor.transform,
                commanderRoot,
                shot.Pooled?.Projectile == null ? null : shot.Pooled.Projectile.transform);
            var attacker = bossActor;
            var target = commanderRoot;
            var targetHealth = commanderHealth;
            var delay = data.DamageDelay;
            var radius = data.ImpactRadius;
            var impactPosition = shot.Target;
            var damageState = waveDamageState;
            var effects = data.Effects;
            var parent = effectParent;
            ScheduleDamage(delay, () =>
            {
                if (damageState == null ||
                    damageState.HasDamaged ||
                    attacker == null ||
                    !attacker.IsAlive ||
                    target == null ||
                    targetHealth == null ||
                    !targetHealth.IsAlive)
                {
                    return;
                }

                var offset = target.position - impactPosition;
                offset.y = 0f;
                if (offset.sqrMagnitude > radius * radius)
                {
                    return;
                }

                damageState.HasDamaged = true;
                targetHealth.ApplyDamage(new DamageRequest(
                    attacker,
                    1f,
                    target.position));
                FallenCommanderAttackEffectPlayer.PlayHit(
                    effects,
                    target.position,
                    attacker.transform.forward,
                    parent,
                    attacker.transform,
                    target);
            });

            ReturnShot(shot);
        }

        private void ReturnShot(FallingShot shot)
        {
            if (shot == null || shot.Resolved)
            {
                return;
            }

            Return(shot.Pooled);
            shot.Pooled = null;
            shot.Resolved = true;
        }

        private bool IsCommanderInside(Vector3 impactPosition)
        {
            if (commanderRoot == null || commanderHealth == null || !commanderHealth.IsAlive)
            {
                return false;
            }

            return HorizontalSqrDistance(commanderRoot.position, impactPosition) <=
                data.ImpactRadius * data.ImpactRadius;
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

        private bool AreAllShotsResolved()
        {
            for (var index = 0; index < shots.Count; index++)
            {
                if (!shots[index].Resolved)
                {
                    return false;
                }
            }

            return true;
        }

        private void EnsurePool(int count)
        {
            while (pool.Count < count)
            {
                CreatePooledProjectile();
            }
        }

        private PooledProjectile Rent()
        {
            for (var index = 0; index < pool.Count; index++)
            {
                if (!pool[index].InUse)
                {
                    pool[index].InUse = true;
                    return pool[index];
                }
            }

            var created = CreatePooledProjectile();
            if (created != null)
            {
                created.InUse = true;
            }

            return created;
        }

        private PooledProjectile CreatePooledProjectile()
        {
            if (data?.ProjectilePrefab == null || data.TelegraphPrefab == null || effectParent == null)
            {
                return null;
            }

            var projectile = Object.Instantiate(data.ProjectilePrefab, effectParent);
            projectile.name = "FallingBarrageProjectile_Pooled";
            projectile.SetActive(false);
            var telegraph = FallenCommanderTelegraphView.CreateCircle(
                data.TelegraphPrefab,
                effectParent,
                arenaCenter,
                data.ImpactRadius,
                FallenCommanderTelegraphPalette.Danger);
            if (telegraph == null)
            {
                Object.Destroy(projectile);
                return null;
            }

            telegraph.gameObject.name = "FallingBarrageTelegraph_Pooled";
            telegraph.gameObject.SetActive(false);
            var pooled = new PooledProjectile
            {
                Projectile = projectile,
                Telegraph = telegraph
            };
            pool.Add(pooled);
            return pooled;
        }

        private static void RestartParticles(GameObject root)
        {
            var particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var index = 0; index < particleSystems.Length; index++)
            {
                particleSystems[index].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystems[index].Play(true);
            }
        }

        private static void StopEffects(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var index = 0; index < particleSystems.Length; index++)
            {
                particleSystems[index].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            var audioSources = root.GetComponentsInChildren<AudioSource>(true);
            for (var index = 0; index < audioSources.Length; index++)
            {
                audioSources[index].Stop();
            }
        }

        private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
        {
            var x = a.x - b.x;
            var z = a.z - b.z;
            return x * x + z * z;
        }

        private void Return(PooledProjectile pooled)
        {
            if (pooled == null)
            {
                return;
            }

            StopEffects(pooled.Projectile);
            if (pooled.Projectile != null)
            {
                pooled.Projectile.SetActive(false);
            }

            if (pooled.Telegraph != null)
            {
                pooled.Telegraph.SetProgress(0f);
                pooled.Telegraph.gameObject.SetActive(false);
            }

            pooled.InUse = false;
        }

        private void CancelActiveShots()
        {
            for (var index = 0; index < shots.Count; index++)
            {
                Return(shots[index].Pooled);
                shots[index].Pooled = null;
            }
        }

        public void Cancel()
        {
            CancelActiveShots();
            ReleaseRuntimeState();
        }

        public void Dispose()
        {
            Cancel();
            for (var index = 0; index < pool.Count; index++)
            {
                if (pool[index].Projectile != null)
                {
                    Object.Destroy(pool[index].Projectile);
                }

                if (pool[index].Telegraph != null)
                {
                    Object.Destroy(pool[index].Telegraph.gameObject);
                }
            }

            pool.Clear();
        }

        private void ReleaseRuntimeState()
        {
            shots.Clear();
            selectedPositions.Clear();
            data = null;
            phaseData = null;
            bossActor = null;
            commanderRoot = null;
            commanderHealth = null;
            animationPresenter = null;
            effectParent = null;
            arenaCenter = Vector3.zero;
            state = PatternState.Inactive;
            waveElapsed = 0f;
            remainingTime = 0f;
            attackStartDelayRemaining = 0f;
            waveIndex = 0;
            castMotionPlayed = false;
            waveDamageState = null;
        }
    }
}
