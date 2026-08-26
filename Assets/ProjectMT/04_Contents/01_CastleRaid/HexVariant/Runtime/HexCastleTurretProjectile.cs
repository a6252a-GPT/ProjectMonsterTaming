using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleTurretProjectile : MonoBehaviour // Hex 전용 직선·관통·폭발 투사체
    {
        private readonly HashSet<int> hitUnitIds = new HashSet<int>();
        private HexCastleTurretCombatWorld world;
        private HexCastleTurretRuntime sourceTurret;
        private HexCastleTurretAttackProfile profile;
        private HexCastleTurretAttackProfileData attack;
        private Vector3 direction;
        private Vector3 targetPoint;
        private float targetDistance;
        private float maximumTravelDistance;
        private float travelledDistance;
        private float remainingLifetime;
        private int remainingPierces;
        private bool configured;

        public bool IsConfigured => configured;
        public int HitUnitCount => hitUnitIds.Count;

        public void Configure(
            HexCastleTurretCombatWorld combatWorld,
            HexCastleTurretRuntime source,
            HexCastleTurretAttackProfile attackProfile,
            Vector3 travelDirection,
            Vector3 aimedPoint)
        {
            world = combatWorld;
            sourceTurret = source;
            profile = attackProfile;
            attack = attackProfile == null ? default : attackProfile.Data;
            direction = travelDirection.sqrMagnitude <= 0.0001f
                ? transform.forward
                : travelDirection.normalized;
            targetPoint = aimedPoint;
            targetDistance = Vector3.Distance(transform.position, targetPoint);
            maximumTravelDistance = targetDistance + Mathf.Max(
                attack.projectileHitRadius * 2f,
                world == null ? 0.5f : world.AssaultCollisionRadius * 2f);
            travelledDistance = 0f;
            remainingLifetime = attack.projectileLifetime;
            remainingPierces = Mathf.Max(1, attack.pierceCount);
            hitUnitIds.Clear();
            configured = world != null && profile != null && profile.IsValid;
            RestartParticles();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (!configured || world == null || !world.IsRunning)
            {
                ReturnToPool();
                return;
            }

            deltaTime = Mathf.Max(0f, deltaTime);
            var step = attack.projectileSpeed * deltaTime;
            var from = transform.position;
            var to = from + direction * step;
            if (attack.impactType == HexCastleTurretImpactType.Pierce)
            {
                ProcessPiercingHits(from, to);
                if (!configured)
                {
                    return;
                }
            }
            else if (world.TryFindFirstAssaultHit(
                         from,
                         to,
                         attack.projectileHitRadius,
                         hitUnitIds,
                         out var directTarget,
                         out var hitPoint))
            {
                if (attack.impactType == HexCastleTurretImpactType.ExplosionArea)
                {
                    Explode(hitPoint);
                }
                else
                {
                    if (world.ApplyDamage(directTarget, attack.baseDamage, hitPoint, sourceTurret))
                    {
                        sourceTurret?.ReportHit(attack.baseDamage);
                    }

                    PlayImpact(hitPoint, false);
                    ReturnToPool();
                }

                return;
            }

            transform.position = to;
            travelledDistance += step;
            remainingLifetime -= deltaTime;
            if (attack.impactType == HexCastleTurretImpactType.Pierce &&
                travelledDistance >= maximumTravelDistance)
            {
                ReturnToPool();
                return;
            }

            if (attack.impactType != HexCastleTurretImpactType.Pierce && travelledDistance >= targetDistance)
            {
                if (attack.impactType == HexCastleTurretImpactType.ExplosionArea)
                {
                    Explode(targetPoint);
                }
                else
                {
                    PlayImpact(targetPoint, false);
                    ReturnToPool();
                }

                return;
            }

            if (remainingLifetime <= 0f)
            {
                if (attack.impactType == HexCastleTurretImpactType.ExplosionArea)
                {
                    Explode(transform.position);
                }
                else
                {
                    ReturnToPool();
                }
            }
        }

        private void ProcessPiercingHits(Vector3 from, Vector3 to)
        {
            while (remainingPierces > 0 && world.TryFindFirstAssaultHit(
                       from,
                       to,
                       attack.projectileHitRadius,
                       hitUnitIds,
                       out var target,
                       out var hitPoint))
            {
                var damage = hitUnitIds.Count == 0
                    ? attack.baseDamage
                    : attack.baseDamage * attack.piercingDamageRatio;
                hitUnitIds.Add(target.GetInstanceID());
                if (world.ApplyDamage(target, damage, hitPoint, sourceTurret))
                {
                    sourceTurret?.ReportHit(damage);
                }

                var playedHitSfx = world.PlayCue(attack.hitSfx, hitPoint);
                sourceTurret?.ReportImpactPresentation(false, playedHitSfx);
                remainingPierces--;
            }

            if (remainingPierces <= 0)
            {
                ReturnToPool();
            }
        }

        private void Explode(Vector3 position)
        {
            world.ApplyAreaDamage(position, attack.explosionRadius, attack.baseDamage, sourceTurret);
            PlayImpact(position, true);
            ReturnToPool();
        }

        private void PlayImpact(Vector3 position, bool explosion)
        {
            var playedVfx = false;
            if (attack.impactVfxPrefab != null)
            {
                var instance = world.RentObject(attack.impactVfxPrefab, position, Quaternion.identity);
                if (instance != null)
                {
                    var lifetime = instance.GetComponent<HexCastleTurretVfxLifetime>();
                    if (lifetime == null)
                    {
                        lifetime = instance.AddComponent<HexCastleTurretVfxLifetime>();
                    }

                    lifetime.Play(world, attack.impactVfxLifetime, attack.impactVfxScale);
                    playedVfx = true;
                }
            }

            var playedSfx = world.PlayCue(explosion ? attack.explosionSfx : attack.hitSfx, position);
            sourceTurret?.ReportImpactPresentation(playedVfx, playedSfx);
        }

        private void RestartParticles()
        {
            var particles = GetComponentsInChildren<ParticleSystem>(true);
            for (var index = 0; index < particles.Length; index++)
            {
                particles[index].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles[index].Play(true);
            }
        }

        private void ReturnToPool()
        {
            if (!configured)
            {
                return;
            }

            configured = false;
            var owner = world;
            world = null;
            sourceTurret = null;
            profile = null;
            hitUnitIds.Clear();
            owner?.ReturnObject(gameObject);
        }

        private void OnDisable()
        {
            configured = false;
            world = null;
            sourceTurret = null;
            profile = null;
            hitUnitIds.Clear();
        }
    }

    [DisallowMultipleComponent]
    public sealed class HexCastleTurretVfxLifetime : MonoBehaviour // 풀 VFX 자동 반환
    {
        private HexCastleTurretCombatWorld world;
        private float returnAt;
        private bool playing;

        public void Play(HexCastleTurretCombatWorld owner, float lifetime, float scale)
        {
            world = owner;
            returnAt = Time.time + Mathf.Max(0.05f, lifetime);
            transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
            var particles = GetComponentsInChildren<ParticleSystem>(true);
            for (var index = 0; index < particles.Length; index++)
            {
                particles[index].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles[index].Play(true);
            }

            playing = true;
        }

        private void Update()
        {
            if (playing && Time.time >= returnAt)
            {
                playing = false;
                world?.ReturnObject(gameObject);
            }
        }

        private void OnDisable()
        {
            playing = false;
            world = null;
        }
    }
}
