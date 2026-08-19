using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    [DisallowMultipleComponent]
    public sealed class CastleTurretProjectile : MonoBehaviour // 풀 재사용 직선·관통·폭발 투사체
    {
        private readonly HashSet<int> hitUnitIds = new HashSet<int>();
        private CastleRaidController controller;
        private CastleTurretRuntime sourceTurret;
        private CastleTurretAttackProfile profile;
        private CastleTurretAttackProfileData attack;
        private Vector3 direction;
        private Vector3 targetPoint;
        private float targetDistance;
        private float travelledDistance;
        private float remainingLifetime;
        private int remainingPierces;
        private bool configured;

        public void Configure(
            CastleRaidController raidController,
            CastleTurretRuntime source,
            CastleTurretAttackProfile attackProfile,
            Vector3 travelDirection,
            Vector3 aimedPoint)
        {
            controller = raidController;
            sourceTurret = source;
            profile = attackProfile;
            attack = attackProfile.Data;
            direction = travelDirection.sqrMagnitude <= 0.0001f ? transform.forward : travelDirection.normalized;
            targetPoint = aimedPoint;
            targetDistance = Vector3.Distance(transform.position, targetPoint);
            travelledDistance = 0f;
            remainingLifetime = attack.projectileLifetime;
            remainingPierces = Mathf.Max(1, attack.pierceCount);
            hitUnitIds.Clear();
            configured = controller != null && profile != null;
            RestartParticles();
        }

        private void Update()
        {
            if (!configured || controller == null || !controller.IsRunning)
            {
                ReturnToPool();
                return;
            }

            var step = attack.projectileSpeed * Time.deltaTime;
            var from = transform.position;
            var to = from + direction * step;
            if (attack.impactType == CastleTurretImpactType.Pierce)
            {
                ProcessPiercingHits(from, to);
                if (!configured)
                {
                    return;
                }
            }
            else if (controller.TryFindFirstTurretHit(
                         from,
                         to,
                         attack.projectileHitRadius,
                         hitUnitIds,
                         out var directTarget,
                         out var hitPoint))
            {
                if (attack.impactType == CastleTurretImpactType.ExplosionArea)
                {
                    Explode(hitPoint);
                }
                else
                {
                    if (controller.ApplyTurretDamage(directTarget, attack.baseDamage, hitPoint, sourceTurret))
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
            remainingLifetime -= Time.deltaTime;
            if (attack.impactType != CastleTurretImpactType.Pierce && travelledDistance >= targetDistance)
            {
                if (attack.impactType == CastleTurretImpactType.ExplosionArea)
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
                if (attack.impactType == CastleTurretImpactType.ExplosionArea)
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
            while (remainingPierces > 0 && controller.TryFindFirstTurretHit(
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
                if (controller.ApplyTurretDamage(target, damage, hitPoint, sourceTurret))
                {
                    sourceTurret?.ReportHit(damage);
                }
                controller.PlayTurretCue(attack.hitSfx, hitPoint);
                remainingPierces--;
            }

            if (remainingPierces <= 0)
            {
                ReturnToPool();
            }
        }

        private void Explode(Vector3 position)
        {
            controller.ApplyTurretAreaDamage(position, attack.explosionRadius, attack.baseDamage, sourceTurret);
            PlayImpact(position, true);
            ReturnToPool();
        }

        private void PlayImpact(Vector3 position, bool explosion)
        {
            if (attack.impactVfxPrefab != null)
            {
                var instance = controller.RentTurretObject(attack.impactVfxPrefab, position, Quaternion.identity);
                if (instance != null)
                {
                    var lifetime = instance.GetComponent<CastleTurretVfxLifetime>();
                    if (lifetime == null)
                    {
                        lifetime = instance.AddComponent<CastleTurretVfxLifetime>();
                    }

                    lifetime.Play(controller, attack.impactVfxLifetime, attack.impactVfxScale);
                }
            }

            controller.PlayTurretCue(explosion ? attack.explosionSfx : attack.hitSfx, position);
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
            controller?.ReturnTurretObject(gameObject);
            controller = null;
            sourceTurret = null;
            profile = null;
            hitUnitIds.Clear();
        }

        private void OnDisable()
        {
            configured = false;
            controller = null;
            sourceTurret = null;
            profile = null;
            hitUnitIds.Clear();
        }
    }

    [DisallowMultipleComponent]
    public sealed class CastleTurretVfxLifetime : MonoBehaviour // 풀 VFX 자동 반환
    {
        private CastleRaidController controller;
        private float returnAt;
        private bool playing;

        public void Play(CastleRaidController owner, float lifetime, float scale)
        {
            controller = owner;
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
                controller?.ReturnTurretObject(gameObject);
            }
        }

        private void OnDisable()
        {
            playing = false;
            controller = null;
        }
    }
}
