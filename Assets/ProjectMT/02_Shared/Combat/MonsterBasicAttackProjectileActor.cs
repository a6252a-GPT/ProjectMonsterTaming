using System;
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
        private float resolvedRange;
        private float remainingLifetime;
        private float traveled;
        private int passIndex;
        private int deferredReturnPrimaryId;
        private bool returning;
        private bool running;
        private IReadOnlyList<MonsterBasicAttackVfxBinding> bindings;
        private string motionId;
        private int? sequenceId;
        private float playbackSpeed = 1f;
        private bool applyAsSkillDamage;
        private Action<UnitActor, Vector3> hitCallback;
        private Renderer[] hiddenCarrierRenderers = Array.Empty<Renderer>();
        private bool[] hiddenCarrierRendererStates = Array.Empty<bool>();

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
            float vfxScale,
            IReadOnlyList<MonsterBasicAttackVfxBinding> presentationBindings = null,
            string attackMotionId = null,
            int? attackSequenceId = null,
            float attackPlaybackSpeed = 1f,
            bool useSkillDamage = false,
            Action<UnitActor, Vector3> onHit = null,
            bool hideCarrierVisuals = false)
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
            bindings = presentationBindings;
            motionId = attackMotionId;
            sequenceId = attackSequenceId;
            playbackSpeed = float.IsNaN(attackPlaybackSpeed) || float.IsInfinity(attackPlaybackSpeed)
                ? 1f
                : Mathf.Max(0.05f, attackPlaybackSpeed);
            applyAsSkillDamage = useSkillDamage;
            hitCallback = onHit;
            origin = launchOrigin;
            targetPosition = initialTargetPosition;
            launchDirection.y = 0f;
            direction = launchDirection.sqrMagnitude < 0.0001f
                ? transform.forward
                : launchDirection.normalized;
            resolvedSpeed = actionDefinition != null
                ? actionDefinition.ResolvedSpeed
                : profile?.ProjectileSpeed ?? 0f;
            resolvedHitRadius = actionDefinition != null
                ? actionDefinition.ResolvedHitRadius
                : profile?.ProjectileCollisionRadius ?? 0f;
            resolvedRange = profile?.ResolveRange(attackRange) ?? attackRange;
            if (profile != null &&
                profile.ProjectileTravel == MonsterBasicAttackProjectileTravel.Straight &&
                profile.CollisionModule == MonsterBasicAttackCollisionModule.AreaImpact)
            {
                var targetOffset = initialTargetPosition - launchOrigin;
                targetOffset.y = 0f;
                var projectedDistance = Vector3.Dot(targetOffset, direction);
                var lateralOffset = targetOffset - direction * projectedDistance;
                if (projectedDistance > 0.01f &&
                    lateralOffset.sqrMagnitude <= Mathf.Pow(Mathf.Max(0.25f, resolvedHitRadius), 2f))
                {
                    resolvedRange = Mathf.Min(resolvedRange, projectedDistance);
                }
            }
            remainingLifetime = actionDefinition != null
                ? actionDefinition.ResolvedLifetime
                : profile?.ProjectileLifetime ?? 0f;
            traveled = 0f;
            passIndex = 0;
            deferredReturnPrimaryId = 0;
            returning = false;
            passHitTargetIds.Clear();
            running = world != null && source != null && profile != null;
            ConfigureCarrierVisibility(hideCarrierVisuals);
            AttachProjectileFeel(profile?.ProjectileFeel);
        }

        private void ConfigureCarrierVisibility(bool hide)
        {
            RestoreCarrierVisibility();
            if (!hide) return;
            hiddenCarrierRenderers = GetComponentsInChildren<Renderer>(true);
            hiddenCarrierRendererStates = new bool[hiddenCarrierRenderers.Length];
            for (var index = 0; index < hiddenCarrierRenderers.Length; index++)
            {
                var renderer = hiddenCarrierRenderers[index];
                if (renderer == null) continue;
                hiddenCarrierRendererStates[index] = renderer.enabled;
                renderer.enabled = false;
            }
        }

        private void RestoreCarrierVisibility()
        {
            var count = Mathf.Min(hiddenCarrierRenderers.Length, hiddenCarrierRendererStates.Length);
            for (var index = 0; index < count; index++)
            {
                if (hiddenCarrierRenderers[index] != null)
                    hiddenCarrierRenderers[index].enabled = hiddenCarrierRendererStates[index];
            }
            hiddenCarrierRenderers = Array.Empty<Renderer>();
            hiddenCarrierRendererStates = Array.Empty<bool>();
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
            if (!running || world == null || profile == null || source == null)
            {
                ReturnToPool();
                return;
            }

            if (world.IsPaused)
            {
                return;
            }

            var deltaTime = Time.deltaTime * world.GetMonsterActiveFocusTimeScale(source) * playbackSpeed;
            remainingLifetime -= deltaTime;
            if (remainingLifetime <= 0f || !source.IsAlive)
            {
                ReturnToPool();
                return;
            }

            switch (profile.ProjectileTravel)
            {
                case MonsterBasicAttackProjectileTravel.Homing:
                    TickHoming(deltaTime);
                    break;
                case MonsterBasicAttackProjectileTravel.Returning:
                    TickReturning(deltaTime);
                    break;
                default:
                    TickStraight(deltaTime);
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
            var remainingRange = Mathf.Max(0f, resolvedRange - traveled);
            var step = Mathf.Min(resolvedSpeed * deltaTime, remainingRange);
            transform.position += direction * step;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            traveled += step;
            if (profile.CollisionModule == MonsterBasicAttackCollisionModule.AreaImpact)
            {
                if (traveled + 0.0001f < resolvedRange)
                {
                    return;
                }

                var applied = ApplyAreaImpact(transform.position, 0);
                if (applied)
                {
                    PlayImpactFeedback(transform.position, ResolveTargetGameObject(primaryTarget));
                }
                ReturnToPool();
                return;
            }

            var hitAny = ApplyPassContacts(previous, transform.position, 0);
            if ((profile.StopOnFirstTarget && hitAny) ||
                volley.HitCount >= profile.MaxTargets ||
                traveled >= resolvedRange)
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
                MonsterBasicAttackVfxRuntime.Dispatch(
                    MonsterBasicAttackVfxEvent.DeliveryTurn,
                    CreateVfxContext(primaryTarget, transform.position, 0));
                returning = true;
                passIndex = 1;
                passHitTargetIds.Clear();
                var primaryActor = ResolveActor(primaryTarget);
                deferredReturnPrimaryId = primaryActor == null
                    ? 0
                    : primaryActor.GetInstanceID();
                if (deferredReturnPrimaryId != 0)
                {
                    // 반환을 시작한 같은 프레임에 주 대상 2타가 겹치지 않게 하고,
                    // 공용 Preview 계약과 같이 귀환 완료 시점에 두 번째 타격을 확정한다.
                    passHitTargetIds.Add(deferredReturnPrimaryId);
                }
                return;
            }

            if (deferredReturnPrimaryId != 0)
            {
                passHitTargetIds.Remove(deferredReturnPrimaryId);
                deferredReturnPrimaryId = 0;
            }
            ApplyPrimaryFallback(1);
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
                var hitPoint = target.transform.position + Vector3.up * 0.4f;
                if (ApplyDamage(
                        target.Health,
                        baseDamage * profile.ResolveDamageRatio(damagePass) * ratio,
                        ResolveDamageFeedbackFlags(feelOwnsTargetMotion),
                        hitPoint))
                {
                    DispatchTargetDamaged(target.Health, hitPoint, damagePass);
                    PlayImpactFeedback(hitPoint, feelTarget);
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
            var hitPoint = primaryTarget.Position + Vector3.up * 0.4f;
            var applied = ApplyDamage(
                primaryTarget,
                baseDamage * profile.ResolveDamageRatio(damagePass),
                ResolveDamageFeedbackFlags(feelOwnsTargetMotion),
                hitPoint);
            if (applied)
            {
                DispatchTargetDamaged(
                    primaryTarget,
                    hitPoint,
                    damagePass);
            }
            return applied;
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
                var hitPoint = target.transform.position + Vector3.up * 0.4f;
                var targetApplied = ApplyDamage(
                    target.Health,
                    baseDamage * profile.ResolveDamageRatio(damagePass) * ratio,
                    ResolveDamageFeedbackFlags(feelOwnsTargetMotion && target == primaryActor),
                    hitPoint);
                applied |= targetApplied;
                if (targetApplied)
                {
                    DispatchTargetDamaged(
                        target.Health,
                        hitPoint,
                        damagePass);
                }
            }

            if (primaryActor == null && primaryTarget != null && primaryTarget.IsAlive)
            {
                var primaryHitPoint = primaryTarget.Position + Vector3.up * 0.4f;
                var primaryApplied = ApplyDamage(
                    primaryTarget,
                    baseDamage * profile.ResolveDamageRatio(damagePass),
                    ResolveDamageFeedbackFlags(false),
                    primaryHitPoint);
                applied |= primaryApplied;
                if (primaryApplied)
                {
                    DispatchTargetDamaged(
                        primaryTarget,
                        primaryHitPoint,
                        damagePass);
                }
            }
            if (applied)
            {
                MonsterBasicAttackVfxRuntime.Dispatch(
                    MonsterBasicAttackVfxEvent.AreaResolved,
                    CreateVfxContext(primaryTarget, center, damagePass, center));
            }
            return applied;
        }

        private void DispatchTargetDamaged(IDamageable target, Vector3 hitPoint, int damagePass)
        {
            var context = CreateVfxContext(target, hitPoint, damagePass);
            MonsterBasicAttackVfxRuntime.Dispatch(MonsterBasicAttackVfxEvent.TargetDamaged, context);
            if (profile?.SequenceModule == MonsterBasicAttackSequenceModule.ReturnPasses)
            {
                MonsterBasicAttackVfxRuntime.Dispatch(
                    damagePass == 0
                        ? MonsterBasicAttackVfxEvent.OutboundTargetDamaged
                        : MonsterBasicAttackVfxEvent.ReturnTargetDamaged,
                    context);
            }
        }

        private MonsterBasicAttackVfxContext CreateVfxContext(
            IDamageable target,
            Vector3 hitPoint,
            int damageStage,
            Vector3? resolvedAreaCenter = null)
        {
            return new MonsterBasicAttackVfxContext(
                world,
                profile,
                source?.RuntimeAssetSet?.FeedbackProfile,
                source,
                target,
                source?.AnimationDriver,
                transform,
                null,
                origin,
                hitPoint,
                resolvedAreaCenter ?? targetPosition,
                transform.rotation,
                damageStage,
                bindings,
                motionId,
                sequenceId,
                playbackSpeed);
        }

        private bool ApplyDamage(
            IDamageable target,
            float amount,
            DamageFeedbackFlags flags,
            Vector3 hitPoint)
        {
            var applied = applyAsSkillDamage
                ? world.ApplyMonsterSkillDamage(source, target, amount, flags)
                : world.ApplyMonsterDamage(source, target, amount, flags);
            if (!applied || hitCallback == null)
            {
                return applied;
            }
            var actor = ResolveActor(target);
            if (actor != null)
            {
                hitCallback(actor, hitPoint);
            }
            return true;
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

        private DamageFeedbackFlags ResolveDamageFeedbackFlags(bool feelOwnsTargetMotion)
        {
            var flags = feelOwnsTargetMotion
                ? DamageFeedbackFlags.BasicAttackFeelTargetMotion
                : DamageFeedbackFlags.None;
            if ((profile?.HitCount ?? 1) > 1)
            {
                flags |= DamageFeedbackFlags.SeparateFloatingNumber;
            }
            return flags;
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
            RestoreCarrierVisibility();
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
            resolvedRange = 0f;
            remainingLifetime = 0f;
            deferredReturnPrimaryId = 0;
            bindings = null;
            motionId = null;
            sequenceId = null;
            playbackSpeed = 1f;
            applyAsSkillDamage = false;
            hitCallback = null;
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
            MonsterBasicAttackVfxRuntime.EndDelivery(
                CreateVfxContext(primaryTarget, transform.position, passIndex, transform.position));
            ReleaseProjectileFeel();
            RestoreCarrierVisibility();
            world = null;
            owner?.ReturnMonsterObject(gameObject);
        }
    }
}
