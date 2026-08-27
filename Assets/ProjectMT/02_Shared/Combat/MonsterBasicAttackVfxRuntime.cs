using System;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public static class MonsterBasicAttackVfxPlayback // Pool·Preview가 같은 내부 시작점·속도를 사용
    {
        public static void ApplyInstanceScale(GameObject root, Vector3 localScale)
        {
            if (root == null)
            {
                return;
            }

            root.transform.localScale = localScale;
            var particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var index = 0; index < particles.Length; index++)
            {
                var main = particles[index].main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }

        public static void RestartAtOffset(
            GameObject root,
            float playbackOffset,
            bool continuePlaying = true,
            float playbackSpeed = 1f)
        {
            if (root == null)
            {
                return;
            }

            var trails = root.GetComponentsInChildren<TrailRenderer>(true);
            for (var index = 0; index < trails.Length; index++)
            {
                trails[index].Clear();
            }

            var offset = Mathf.Max(0f, playbackOffset);
            var speed = SanitizePlaybackSpeed(playbackSpeed);
            var playbackState = root.GetComponent<MonsterBasicAttackVfxPlaybackState>() ??
                                root.AddComponent<MonsterBasicAttackVfxPlaybackState>();
            var particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var index = 0; index < particles.Length; index++)
            {
                var particle = particles[index];
                var main = particle.main;
                var authoredSpeed = playbackState.ResolveAuthoredSpeed(particle);
                main.simulationSpeed = authoredSpeed; // 시작점은 Vendor 원본 시간축으로 탐색
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Simulate(offset, false, true, true);
                main.simulationSpeed = authoredSpeed * speed;
                if (continuePlaying)
                {
                    particle.Play(false);
                }
                else
                {
                    particle.Pause(false);
                }
            }
        }

        private static float SanitizePlaybackSpeed(float speed)
        {
            return float.IsNaN(speed) || float.IsInfinity(speed)
                ? 1f
                : Mathf.Max(0.01f, speed);
        }

        public static void Simulate(GameObject root, float deltaTime)
        {
            if (root == null || deltaTime <= 0f)
            {
                return;
            }

            var particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var index = 0; index < particles.Length; index++)
            {
                particles[index].Simulate(deltaTime, false, false, true);
            }
        }
    }

    internal sealed class MonsterBasicAttackVfxPlaybackState : MonoBehaviour // Pool 재사용에도 Vendor 원본 속도를 보존
    {
        private readonly Dictionary<int, float> authoredSpeeds = new Dictionary<int, float>();

        public float ResolveAuthoredSpeed(ParticleSystem particle)
        {
            if (particle == null)
            {
                return 1f;
            }

            var id = particle.GetInstanceID();
            if (authoredSpeeds.TryGetValue(id, out var speed))
            {
                return speed;
            }

            speed = Mathf.Max(0f, particle.main.simulationSpeed);
            authoredSpeeds.Add(id, speed);
            return speed;
        }
    }

    public readonly struct MonsterBasicAttackVfxContext // 고정 이벤트에 필요한 최소 공간 정보
    {
        public MonsterBasicAttackVfxContext(
            CombatWorld world,
            MonsterBasicAttackProfile profile,
            MonsterFeedbackProfile feedback,
            UnitActor source,
            IDamageable target,
            MonsterAnimationDriver driver,
            Transform projectile,
            string socketOverride,
            Vector3 origin,
            Vector3 hitPoint,
            Vector3 areaCenter,
            Quaternion rotation,
            int damageStage = 0)
        {
            World = world;
            Profile = profile;
            Feedback = feedback;
            Source = source;
            Target = target;
            Driver = driver;
            Projectile = projectile;
            SocketOverride = socketOverride;
            Origin = origin;
            HitPoint = hitPoint;
            AreaCenter = areaCenter;
            Rotation = rotation;
            DamageStage = damageStage;
            SequenceId = driver?.ActionSequenceId ?? 0;
            MotionId = driver?.CurrentMotionId ?? string.Empty;
        }

        public CombatWorld World { get; }
        public MonsterBasicAttackProfile Profile { get; }
        public MonsterFeedbackProfile Feedback { get; }
        public UnitActor Source { get; }
        public IDamageable Target { get; }
        public MonsterAnimationDriver Driver { get; }
        public Transform Projectile { get; }
        public string SocketOverride { get; }
        public Vector3 Origin { get; }
        public Vector3 HitPoint { get; }
        public Vector3 AreaCenter { get; }
        public Quaternion Rotation { get; }
        public int DamageStage { get; }
        public int SequenceId { get; }
        public string MotionId { get; }
    }

    public static class MonsterBasicAttackVfxRuntime // Preview와 같은 슬롯 선택 규칙을 실행
    {
        public static bool Dispatch(
            MonsterBasicAttackVfxEvent eventType,
            in MonsterBasicAttackVfxContext context)
        {
            if (context.World == null || context.Profile == null || context.Feedback == null ||
                context.Source == null)
            {
                return false;
            }

            var registry = ResolveRegistry(context.Source);
            var played = false;
            var slots = context.Profile.VfxSlots;
            for (var index = 0; index < slots.Count; index++)
            {
                var slot = slots[index];
                if (slot == null || slot.EventType != eventType ||
                    !MonsterBasicAttackVfxResolver.TryResolvePresentation(
                        context.Feedback.BasicAttackVfxBindings,
                        context.Profile.AttackId,
                        slot,
                        context.MotionId,
                        out var binding))
                {
                    continue;
                }

                if (binding.HasSound && binding.Sfx != null &&
                    registry.TryClaim(slot, context, "sfx"))
                {
                    ResolveAnchor(
                        slot.Anchor,
                        context,
                        out _,
                        out var soundPosition,
                        out var soundRotation);
                    context.World.PlayMonsterSfx(
                        binding.Sfx,
                        soundPosition + soundRotation * binding.LocalPosition);
                    played = true;
                }

                // 이동체 외형은 Projectile Actor가 직접 소유하므로 여기서는 SFX만 재생한다.
                if (slot.IsDeliveryVisual || !binding.IsAssigned)
                {
                    continue;
                }

                var timingOffset = slot.ClampTimingOffset(binding.EventTimingOffset);
                if (timingOffset < 0f &&
                    eventType == MonsterBasicAttackVfxEvent.RecipeExecute &&
                    registry.HasClaim(slot, context, "vfx"))
                {
                    continue;
                }
                if (!registry.TryClaim(slot, context, "vfx"))
                {
                    continue;
                }

                if (timingOffset > 0f)
                {
                    var scheduledContext = context;
                    registry.Schedule(
                        timingOffset,
                        () => PlayVfxNow(slot, binding, scheduledContext, registry),
                        slot.EndPolicy,
                        context.SequenceId,
                        context.Projectile);
                    played = true;
                    continue;
                }

                played |= PlayVfxNow(slot, binding, context, registry);
            }
            return played;
        }

        public static void BeginMotion(in MonsterBasicAttackVfxContext context)
        {
            var registry = ResolveRegistry(context.Source);
            registry?.BeginSequence(context.SequenceId);
            Dispatch(MonsterBasicAttackVfxEvent.MotionStart, context);
            ScheduleRecipeLeadVfx(context, registry);
        }

        public static void EndMotion(in MonsterBasicAttackVfxContext context)
        {
            Dispatch(MonsterBasicAttackVfxEvent.MotionEnd, context);
            ResolveRegistry(context.Source)?.Release(
                MonsterBasicAttackVfxEndPolicy.MotionEnd,
                context.SequenceId,
                null);
        }

        public static void EndDelivery(in MonsterBasicAttackVfxContext context)
        {
            Dispatch(MonsterBasicAttackVfxEvent.DeliveryEnd, context);
            ResolveRegistry(context.Source)?.Release(
                MonsterBasicAttackVfxEndPolicy.DeliveryEnd,
                context.SequenceId,
                context.Projectile);
        }

        private static MonsterBasicAttackVfxRegistry ResolveRegistry(UnitActor source)
        {
            if (source == null)
            {
                return null;
            }
            return source.GetComponent<MonsterBasicAttackVfxRegistry>() ??
                   source.gameObject.AddComponent<MonsterBasicAttackVfxRegistry>();
        }

        private static void ScheduleRecipeLeadVfx(
            in MonsterBasicAttackVfxContext context,
            MonsterBasicAttackVfxRegistry registry)
        {
            if (registry == null || context.Profile == null || context.Feedback == null ||
                context.Driver == null ||
                !context.Driver.TryGetNextAttackMarkerDelay(out var markerDelay))
            {
                return;
            }

            var slots = context.Profile?.VfxSlots;
            if (slots == null)
            {
                return;
            }

            for (var index = 0; index < slots.Count; index++)
            {
                var slot = slots[index];
                if (slot == null ||
                    slot.EventType != MonsterBasicAttackVfxEvent.RecipeExecute ||
                    slot.IsDeliveryVisual ||
                    !MonsterBasicAttackVfxResolver.TryResolvePresentation(
                        context.Feedback.BasicAttackVfxBindings,
                        context.Profile.AttackId,
                        slot,
                        context.MotionId,
                        out var binding) ||
                    !binding.IsAssigned)
                {
                    continue;
                }

                var timingOffset = slot.ClampTimingOffset(binding.EventTimingOffset);
                if (timingOffset >= 0f || !registry.TryClaim(slot, context, "vfx"))
                {
                    continue;
                }

                var delay = Mathf.Max(0f, markerDelay + timingOffset);
                if (delay <= 0.0001f)
                {
                    PlayVfxNow(slot, binding, context, registry);
                    continue;
                }

                var scheduledContext = context;
                registry.Schedule(
                    delay,
                    () => PlayVfxNow(slot, binding, scheduledContext, registry),
                    slot.EndPolicy,
                    context.SequenceId,
                    context.Projectile);
            }
        }

        private static bool PlayVfxNow(
            MonsterBasicAttackVfxSlot slot,
            MonsterBasicAttackVfxBinding binding,
            in MonsterBasicAttackVfxContext context,
            MonsterBasicAttackVfxRegistry registry)
        {
            if (slot == null || binding == null || !binding.IsAssigned ||
                context.World == null || context.Source == null)
            {
                return false;
            }

            ResolveAnchor(
                slot.Anchor,
                context,
                out var anchor,
                out var position,
                out var rotation);
            var parent = slot.Attachment == MonsterBasicAttackVfxAttachment.FollowAnchor
                ? anchor
                : null;
            var instance = context.World.SpawnBasicAttackVfx(
                binding,
                position,
                rotation,
                parent,
                context.Source.RuntimeAssetSet?.BodyProfile?.VfxScale ?? 1f);
            if (instance == null)
            {
                return false;
            }

            if (slot.EndPolicy is MonsterBasicAttackVfxEndPolicy.Timed or
                MonsterBasicAttackVfxEndPolicy.ParticleDuration)
            {
                context.World.ScheduleMonsterObjectReturn(instance, binding.Lifetime);
            }
            else
            {
                registry?.Track(
                    context.World,
                    instance,
                    slot.EndPolicy,
                    context.SequenceId,
                    context.Projectile);
            }
            return true;
        }

        private static void ResolveAnchor(
            MonsterBasicAttackVfxAnchor anchorKind,
            in MonsterBasicAttackVfxContext context,
            out Transform anchor,
            out Vector3 position,
            out Quaternion rotation)
        {
            anchor = null;
            position = context.Origin;
            rotation = context.Rotation;
            switch (anchorKind)
            {
                case MonsterBasicAttackVfxAnchor.SourceRoot:
                    anchor = context.Source?.transform;
                    break;
                case MonsterBasicAttackVfxAnchor.AttackOrigin:
                    anchor = context.Driver?.AttackOrigin ?? context.Source?.transform;
                    break;
                case MonsterBasicAttackVfxAnchor.MarkerSocket:
                    anchor = context.Driver?.ResolveSocket(context.SocketOverride) ??
                             context.Source?.transform;
                    break;
                case MonsterBasicAttackVfxAnchor.ProjectileRoot:
                    anchor = context.Projectile;
                    break;
                case MonsterBasicAttackVfxAnchor.TargetRoot:
                    anchor = (context.Target as Component)?.transform;
                    break;
                case MonsterBasicAttackVfxAnchor.HitPoint:
                    position = context.HitPoint;
                    return;
                case MonsterBasicAttackVfxAnchor.AreaCenter:
                    position = context.AreaCenter;
                    return;
                case MonsterBasicAttackVfxAnchor.TrajectoryOrigin:
                    position = context.Origin;
                    return;
            }

            if (anchor != null)
            {
                position = anchor.position;
                rotation = anchor.rotation;
            }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class MonsterBasicAttackVfxRegistry : MonoBehaviour // 반복 억제와 종료 수명 소유
    {
        private readonly HashSet<string> claims = new HashSet<string>();
        private readonly List<TrackedVfx> tracked = new List<TrackedVfx>();
        private readonly List<PendingAction> pending = new List<PendingAction>();

        public void BeginSequence(int sequenceId)
        {
            claims.Clear();
            ReleaseAll(MonsterBasicAttackVfxEndPolicy.MotionEnd);
        }

        public bool TryClaim(
            MonsterBasicAttackVfxSlot slot,
            in MonsterBasicAttackVfxContext context,
            string channel)
        {
            return claims.Add(BuildClaimKey(slot, context, channel));
        }

        public bool HasClaim(
            MonsterBasicAttackVfxSlot slot,
            in MonsterBasicAttackVfxContext context,
            string channel)
        {
            return claims.Contains(BuildClaimKey(slot, context, channel));
        }

        public void Schedule(
            float delay,
            Action action,
            MonsterBasicAttackVfxEndPolicy endPolicy,
            int sequenceId,
            Transform delivery)
        {
            if (action == null)
            {
                return;
            }
            if (delay <= 0f)
            {
                action.Invoke();
                return;
            }
            pending.Add(new PendingAction(
                Time.time + delay,
                action,
                endPolicy,
                sequenceId,
                delivery));
        }

        private static string BuildClaimKey(
            MonsterBasicAttackVfxSlot slot,
            in MonsterBasicAttackVfxContext context,
            string channel)
        {
            var suffix = slot.Multiplicity switch
            {
                MonsterBasicAttackVfxMultiplicity.PerProjectile =>
                    context.Projectile == null ? "none" : context.Projectile.GetInstanceID().ToString(),
                MonsterBasicAttackVfxMultiplicity.PerTargetHit =>
                    $"{ResolveTargetId(context.Target)}:{context.DamageStage}",
                MonsterBasicAttackVfxMultiplicity.PerDamageStage => context.DamageStage.ToString(),
                _ => "once"
            };
            return $"{context.SequenceId}|{slot.SlotId}|{channel}|{suffix}";
        }

        private void Update()
        {
            for (var index = pending.Count - 1; index >= 0; index--)
            {
                var item = pending[index];
                if (Time.time + 0.0001f < item.ExecuteAt)
                {
                    continue;
                }
                pending.RemoveAt(index);
                item.Action?.Invoke();
            }
        }

        public void Track(
            CombatWorld world,
            GameObject instance,
            MonsterBasicAttackVfxEndPolicy policy,
            int sequenceId,
            Transform delivery)
        {
            tracked.Add(new TrackedVfx(world, instance, policy, sequenceId, delivery));
        }

        public void Release(
            MonsterBasicAttackVfxEndPolicy policy,
            int sequenceId,
            Transform delivery)
        {
            CancelPending(policy, sequenceId, delivery, false);
            for (var index = tracked.Count - 1; index >= 0; index--)
            {
                var item = tracked[index];
                var deliveryMatches = policy != MonsterBasicAttackVfxEndPolicy.DeliveryEnd ||
                                      item.Delivery == delivery;
                if (item.Policy != policy || item.SequenceId != sequenceId || !deliveryMatches)
                {
                    continue;
                }
                item.World?.ReturnMonsterObject(item.Instance);
                tracked.RemoveAt(index);
            }
        }

        private void ReleaseAll(MonsterBasicAttackVfxEndPolicy policy)
        {
            CancelPending(policy, 0, null, true);
            for (var index = tracked.Count - 1; index >= 0; index--)
            {
                var item = tracked[index];
                if (item.Policy != policy)
                {
                    continue;
                }
                item.World?.ReturnMonsterObject(item.Instance);
                tracked.RemoveAt(index);
            }
        }

        private void CancelPending(
            MonsterBasicAttackVfxEndPolicy policy,
            int sequenceId,
            Transform delivery,
            bool everySequence)
        {
            for (var index = pending.Count - 1; index >= 0; index--)
            {
                var item = pending[index];
                var deliveryMatches = policy != MonsterBasicAttackVfxEndPolicy.DeliveryEnd ||
                                      item.Delivery == delivery;
                if (item.EndPolicy != policy ||
                    !everySequence && item.SequenceId != sequenceId ||
                    !deliveryMatches)
                {
                    continue;
                }
                pending.RemoveAt(index);
            }
        }

        private void OnDisable()
        {
            for (var index = tracked.Count - 1; index >= 0; index--)
            {
                tracked[index].World?.ReturnMonsterObject(tracked[index].Instance);
            }
            tracked.Clear();
            pending.Clear();
            claims.Clear();
        }

        private static int ResolveTargetId(IDamageable target)
        {
            return target is Component component ? component.GetInstanceID() : 0;
        }

        private readonly struct TrackedVfx
        {
            public TrackedVfx(
                CombatWorld world,
                GameObject instance,
                MonsterBasicAttackVfxEndPolicy policy,
                int sequenceId,
                Transform delivery)
            {
                World = world;
                Instance = instance;
                Policy = policy;
                SequenceId = sequenceId;
                Delivery = delivery;
            }

            public CombatWorld World { get; }
            public GameObject Instance { get; }
            public MonsterBasicAttackVfxEndPolicy Policy { get; }
            public int SequenceId { get; }
            public Transform Delivery { get; }
        }

        private readonly struct PendingAction
        {
            public PendingAction(
                float executeAt,
                Action action,
                MonsterBasicAttackVfxEndPolicy endPolicy,
                int sequenceId,
                Transform delivery)
            {
                ExecuteAt = executeAt;
                Action = action;
                EndPolicy = endPolicy;
                SequenceId = sequenceId;
                Delivery = delivery;
            }

            public float ExecuteAt { get; }
            public Action Action { get; }
            public MonsterBasicAttackVfxEndPolicy EndPolicy { get; }
            public int SequenceId { get; }
            public Transform Delivery { get; }
        }
    }
}
