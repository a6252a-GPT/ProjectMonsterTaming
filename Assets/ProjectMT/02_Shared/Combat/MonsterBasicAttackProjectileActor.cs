using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public sealed class MonsterBasicAttackProjectileVolley // 한 발동 안에서 다중 투사체 중복 타격 방지
    {
        private readonly HashSet<int> hitTargetIds = new HashSet<int>();

        public int HitCount => hitTargetIds.Count;

        public bool TryClaim(UnitActor target, int maxTargets)
        {
            return target != null && hitTargetIds.Count < Mathf.Max(1, maxTargets) &&
                   hitTargetIds.Add(target.GetInstanceID());
        }
    }

    [DisallowMultipleComponent]
    public sealed class MonsterBasicAttackProjectileActor : MonoBehaviour // 직선·유도·왕복·파동 공용 이동체
    {
        private readonly List<UnitActor> nearbyTargets = new List<UnitActor>();
        private readonly HashSet<int> passHitTargetIds = new HashSet<int>();

        private CombatWorld world;
        private UnitActor source;
        private IDamageable primaryTarget;
        private ProjectileActionDefinition action;
        private MonsterBasicAttackProfile profile;
        private MonsterBasicAttackProjectileVolley volley;
        private MonsterFeedbackCue impactFeedback;
        private GameObject projectileFeelInstance;
        private Vector3 origin;
        private Vector3 targetPosition;
        private Vector3 direction;
        private float baseDamage;
        private float attackRange;
        private float impactVfxScale = 1f;
        private float resolvedSpeed;
        private float resolvedHitRadius;
        private float remainingLifetime;
        private float traveled;
        private int passIndex;
        private bool returning;
        private bool running;

        public void Launch(
            CombatWorld combatWorld,
            UnitActor owner,
            IDamageable target,
            ProjectileActionDefinition actionDefinition,
            MonsterBasicAttackProfile basicAttackProfile,
            float damage,
            float sourceAttackRange,
            Vector3 launchOrigin,
            Vector3 initialTargetPosition,
            Vector3 launchDirection,
            MonsterBasicAttackProjectileVolley sharedVolley,
            MonsterFeedbackCue feedback,
            float vfxScale)
        {
            world = combatWorld;
            source = owner;
            primaryTarget = target;
            action = actionDefinition;
            profile = basicAttackProfile;
            volley = sharedVolley ?? new MonsterBasicAttackProjectileVolley();
            impactFeedback = feedback;
            baseDamage = Mathf.Max(0f, damage);
            attackRange = Mathf.Max(0.2f, sourceAttackRange);
            impactVfxScale = Mathf.Max(0.01f, vfxScale);
            origin = launchOrigin;
            targetPosition = initialTargetPosition;
            launchDirection.y = 0f;
            direction = launchDirection.sqrMagnitude < 0.0001f
                ? transform.forward
                : launchDirection.normalized;
            resolvedSpeed = actionDefinition != null ? actionDefinition.ResolvedSpeed : 0f;
            resolvedHitRadius = actionDefinition != null ? actionDefinition.ResolvedHitRadius : 0f;
            remainingLifetime = actionDefinition != null ? actionDefinition.ResolvedLifetime : 0f;
            traveled = 0f;
            passIndex = 0;
            returning = false;
            passHitTargetIds.Clear();
            running = world != null && source != null && action != null && profile != null;
            AttachProjectileFeel(profile?.ProjectileFeel);
        }

        private void LateUpdate()
        {
            if (running && projectileFeelInstance != null)
            {
                SyncProjectileFeel(profile?.ProjectileFeel);
            }
        }

        private void Update()
        {
            if (!running || world == null || action == null || profile == null || source == null)
            {
                ReturnToPool();
                return;
            }

            if (world.IsPaused)
            {
                return;
            }

            remainingLifetime -= Time.deltaTime;
            if (remainingLifetime <= 0f || !source.IsAlive)
            {
                ReturnToPool();
                return;
            }

            switch (profile.ProjectileTravel)
            {
                case MonsterBasicAttackProjectileTravel.Homing:
                    TickHoming(Time.deltaTime);
                    break;
                case MonsterBasicAttackProjectileTravel.Returning:
                    TickReturning(Time.deltaTime);
                    break;
                default:
                    TickStraight(Time.deltaTime);
                    break;
            }
        }

        private void TickHoming(float deltaTime)
        {
            if (primaryTarget == null || !primaryTarget.IsAlive)
            {
                ReturnToPool();
                return;
            }

            targetPosition = primaryTarget.Position + Vector3.up * 0.4f;
            MoveTowards(targetPosition, deltaTime);
            if ((transform.position - targetPosition).sqrMagnitude > 0.04f)
            {
                return;
            }

            var applied = profile.CollisionModule == MonsterBasicAttackCollisionModule.AreaImpact
                ? ApplyAreaImpact(targetPosition, 0)
                : ApplyPrimaryImpact(0);
            if (applied)
            {
                PlayImpactFeedback(targetPosition, ResolveTargetGameObject(primaryTarget));
            }
            ReturnToPool();
        }

        private void TickStraight(float deltaTime)
        {
            var previous = transform.position;
            var remainingRange = Mathf.Max(0f, profile.ResolveRange(attackRange) - traveled);
            var step = Mathf.Min(resolvedSpeed * deltaTime, remainingRange);
            transform.position += direction * step;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            traveled += step;
            var hitAny = ApplyPassContacts(previous, transform.position, 0);
            if ((profile.StopOnFirstTarget && hitAny) ||
                volley.HitCount >= profile.MaxTargets ||
                traveled >= profile.ResolveRange(attackRange))
            {
                if (volley.HitCount == 0 && profile.ProjectileCount <= 1)
                {
                    ApplyPrimaryFallback(0);
                }
                ReturnToPool();
            }
        }

        private void TickReturning(float deltaTime)
        {
            var destination = returning
                ? source.transform.position + Vector3.up * 0.4f
                : targetPosition;
            var previous = transform.position;
            MoveTowards(destination, deltaTime);
            traveled += Vector3.Distance(previous, transform.position);
            ApplyPassContacts(previous, transform.position, passIndex);
            if ((transform.position - destination).sqrMagnitude > 0.04f)
            {
                return;
            }

            if (!returning)
            {
                ApplyPrimaryFallback(0);
                returning = true;
                passIndex = 1;
                passHitTargetIds.Clear();
                return;
            }

            ReturnToPool();
        }

        private void MoveTowards(Vector3 destination, float deltaTime)
        {
            var movement = destination - transform.position;
            if (movement.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(movement.normalized, Vector3.up);
            }
            transform.position = Vector3.MoveTowards(transform.position, destination, resolvedSpeed * deltaTime);
        }

        private bool ApplyPassContacts(Vector3 segmentStart, Vector3 segmentEnd, int damagePass)
        {
            var opponentTeam = source.Team == UnitTeam.Player ? UnitTeam.Enemy : UnitTeam.Player;
            var recipeRadius = profile.DeliveryModule == MonsterBasicAttackDeliveryModule.TravelingArea
                ? profile.Radius
                : profile.ProjectileCollisionRadius;
            var hitRadius = Mathf.Max(recipeRadius, profile.LineWidth * 0.5f, resolvedHitRadius);
            var segment = segmentEnd - segmentStart;
            segment.y = 0f;
            world.CollectUnitsInLine(
                opponentTeam,
                segmentStart,
                segment,
                segment.magnitude,
                hitRadius * 2f,
                profile.MaxTargets + passHitTargetIds.Count,
                nearbyTargets);
            var applied = false;
            for (var index = 0; index < nearbyTargets.Count; index++)
            {
                var target = nearbyTargets[index];
                if (target == null || !passHitTargetIds.Add(target.GetInstanceID()))
                {
                    continue;
                }

                if (profile.ProjectileCount > 1 && !volley.TryClaim(target, profile.MaxTargets))
                {
                    continue;
                }

                if (profile.ProjectileCount <= 1 && volley.HitCount < profile.MaxTargets)
                {
                    volley.TryClaim(target, profile.MaxTargets);
                }

                var ratio = IsPrimary(target) ? 1f : profile.SecondaryDamageRatio;
                var feelTarget = ResolveTargetGameObject(target.Health);
                var feelOwnsTargetMotion = world.WillPlayBasicAttackFeelTargetMotion(
                    profile.ImpactFeel,
                    feelTarget,
                    ResolveFeelIntensity());
                if (world.ApplyMonsterDamage(
                        source,
                        target.Health,
                        baseDamage * profile.ResolveDamageRatio(damagePass) * ratio,
                        ResolveFeelTargetMotionFlags(feelOwnsTargetMotion)))
                {
                    PlayImpactFeedback(target.transform.position + Vector3.up * 0.4f, feelTarget);
                    applied = true;
                }
            }

            return applied;
        }

        private bool ApplyPrimaryImpact(int damagePass)
        {
            if (primaryTarget == null || !primaryTarget.IsAlive)
            {
                return false;
            }

            var actor = ResolveActor(primaryTarget);
            if (actor != null)
            {
                passHitTargetIds.Add(actor.GetInstanceID());
                volley.TryClaim(actor, profile.MaxTargets);
            }
            var feelTarget = ResolveTargetGameObject(primaryTarget);
            var feelOwnsTargetMotion = world.WillPlayBasicAttackFeelTargetMotion(
                profile.ImpactFeel,
                feelTarget,
                ResolveFeelIntensity());
            return world.ApplyMonsterDamage(
                source,
                primaryTarget,
                baseDamage * profile.ResolveDamageRatio(damagePass),
                ResolveFeelTargetMotionFlags(feelOwnsTargetMotion));
        }

        private bool ApplyPrimaryFallback(int damagePass)
        {
            var actor = ResolveActor(primaryTarget);
            if (actor != null && passHitTargetIds.Contains(actor.GetInstanceID()))
            {
                return false;
            }

            var applied = ApplyPrimaryImpact(damagePass);
            if (applied)
            {
                PlayImpactFeedback(
                    primaryTarget.Position + Vector3.up * 0.4f,
                    ResolveTargetGameObject(primaryTarget));
            }
            return applied;
        }

        private bool ApplyAreaImpact(Vector3 center, int damagePass)
        {
            var opponentTeam = source.Team == UnitTeam.Player ? UnitTeam.Enemy : UnitTeam.Player;
            world.CollectUnits(opponentTeam, center, profile.Radius, profile.MaxTargets, nearbyTargets);
            var primaryActor = ResolveActor(primaryTarget);
            if (primaryActor != null)
            {
                var primaryIndex = nearbyTargets.IndexOf(primaryActor);
                if (primaryIndex >= 0)
                {
                    nearbyTargets.RemoveAt(primaryIndex);
                }
                nearbyTargets.Insert(0, primaryActor);
                if (nearbyTargets.Count > profile.MaxTargets)
                {
                    nearbyTargets.RemoveAt(nearbyTargets.Count - 1);
                }
            }

            var applied = false;
            var feelTarget = ResolveTargetGameObject(primaryTarget);
            var feelOwnsTargetMotion = world.WillPlayBasicAttackFeelTargetMotion(
                profile.ImpactFeel,
                feelTarget,
                ResolveFeelIntensity());
            for (var index = 0; index < nearbyTargets.Count; index++)
            {
                var target = nearbyTargets[index];
                var ratio = target == primaryActor ? 1f : profile.SecondaryDamageRatio;
                applied |= world.ApplyMonsterDamage(
                    source,
                    target.Health,
                    baseDamage * profile.ResolveDamageRatio(damagePass) * ratio,
                    ResolveFeelTargetMotionFlags(feelOwnsTargetMotion && target == primaryActor));
            }

            if (primaryActor == null && primaryTarget != null && primaryTarget.IsAlive)
            {
                applied |= world.ApplyMonsterDamage(
                    source,
                    primaryTarget,
                    baseDamage * profile.ResolveDamageRatio(damagePass));
            }
            return applied;
        }

        private bool IsPrimary(UnitActor target)
        {
            return target != null && target == ResolveActor(primaryTarget);
        }

        private static UnitActor ResolveActor(IDamageable target)
        {
            var component = target as Component;
            return component != null ? component.GetComponent<UnitActor>() : null;
        }

        private static DamageFeedbackFlags ResolveFeelTargetMotionFlags(bool feelOwnsTargetMotion)
        {
            return feelOwnsTargetMotion
                ? DamageFeedbackFlags.BasicAttackFeelTargetMotion
                : DamageFeedbackFlags.None;
        }

        private static GameObject ResolveTargetGameObject(IDamageable target)
        {
            var actor = ResolveActor(target);
            if (actor == null)
            {
                return (target as Component)?.gameObject;
            }

            var visual = actor.transform.Find("Visual") ?? actor.transform.Find("VisualRoot");
            return visual != null ? visual.gameObject : actor.gameObject;
        }

        private void PlayImpactFeedback(Vector3 position, GameObject target)
        {
            world?.PlayMonsterFeedbackAt(
                impactFeedback,
                position,
                transform.rotation,
                impactVfxScale);
            world?.PlayBasicAttackFeelAt(
                profile?.ImpactFeel,
                position,
                transform.rotation,
                impactVfxScale,
                target,
                ResolveFeelIntensity());
        }

        private void AttachProjectileFeel(BasicAttackFeelCue cue)
        {
            ReleaseProjectileFeel();
            if (world == null || cue == null || !cue.HasFeel)
            {
                return;
            }

            var position = transform.TransformPoint(cue.LocalPosition);
            var rotation = transform.rotation * cue.LocalRotation;
            projectileFeelInstance = world.RentMonsterObject(cue.Prefab, position, rotation);
            if (projectileFeelInstance == null)
            {
                return;
            }

            projectileFeelInstance.transform.localScale = cue.Prefab.transform.localScale *
                cue.Scale * Mathf.Max(0.01f, impactVfxScale);
            var visual = transform.Find("Visual") ?? transform.Find("VisualRoot");
            world.PlayBasicAttackFeelRuntime(
                projectileFeelInstance,
                visual != null ? visual.gameObject : gameObject,
                ResolveFeelIntensity());
        }

        private float ResolveFeelIntensity()
        {
            return source?.RuntimeAssetSet?.CombatProfile?.ImpactStrength switch
            {
                MonsterImpactStrength.Light => 0.62f,
                MonsterImpactStrength.Heavy => 1.45f,
                _ => 1f
            };
        }

        private void SyncProjectileFeel(BasicAttackFeelCue cue)
        {
            if (cue == null || projectileFeelInstance == null)
            {
                return;
            }

            projectileFeelInstance.transform.SetPositionAndRotation(
                transform.TransformPoint(cue.LocalPosition),
                transform.rotation * cue.LocalRotation);
        }

        private void ReleaseProjectileFeel()
        {
            if (projectileFeelInstance == null)
            {
                return;
            }

            var instance = projectileFeelInstance;
            projectileFeelInstance = null;
            world?.ReturnMonsterObject(instance);
        }

        private void OnDisable()
        {
            ReleaseProjectileFeel();
            running = false;
            world = null;
            source = null;
            primaryTarget = null;
            action = null;
            profile = null;
            volley = null;
            impactFeedback = null;
            resolvedSpeed = 0f;
            resolvedHitRadius = 0f;
            remainingLifetime = 0f;
            nearbyTargets.Clear();
            passHitTargetIds.Clear();
        }

        private void ReturnToPool()
        {
            if (!running)
            {
                return;
            }

            running = false;
            var owner = world;
            ReleaseProjectileFeel();
            world = null;
            owner?.ReturnMonsterObject(gameObject);
        }
    }
}
