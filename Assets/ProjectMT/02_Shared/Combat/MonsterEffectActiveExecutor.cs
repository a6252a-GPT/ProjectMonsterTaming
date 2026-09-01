using System;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public sealed class MonsterEffectActiveExecutor // 효과 묶음과 지연 효과를 순서대로 실행
    {
        private sealed class TrackedVfx
        {
            public GameObject Instance;
            public MonsterActivePresentationEndPolicy EndPolicy;
        }

        private readonly List<UnitActor> targets = new List<UnitActor>();
        private readonly List<UnitActor> candidates = new List<UnitActor>();
        private readonly List<PendingEffect> pendingEffects = new List<PendingEffect>();
        private readonly List<PeriodicHeal> periodicHeals = new List<PeriodicHeal>();
        private readonly List<TrackedVfx> trackedVfx = new List<TrackedVfx>();
        private UnitActor owner;
        private CombatWorld world;
        private MonsterEffectActiveSkill skill;
        private UnitActor primaryTarget;
        private int groupIndex;
        private float groupDelay;
        private bool running;

        public bool IsRunning => running;
        public bool HasLingering =>
            pendingEffects.Count > 0 || periodicHeals.Count > 0 || trackedVfx.Count > 0;
        public int CompletedGroupCount => Mathf.Clamp(groupIndex, 0, skill?.Groups.Count ?? 0);

        public bool Begin(
            UnitActor caster,
            CombatWorld combatWorld,
            MonsterEffectActiveSkill active,
            UnitActor initialTarget)
        {
            ResetExecution();
            if (caster == null || combatWorld == null || active == null || active.Groups.Count == 0 ||
                !caster.IsAlive)
            {
                return false;
            }

            owner = caster;
            world = combatWorld;
            skill = active;
            primaryTarget = initialTarget;
            groupIndex = 0;
            groupDelay = active.Groups[0]?.DelayAfterPrevious ?? 0f;
            running = true;
            return true;
        }

        public bool Tick(float deltaTime)
        {
            TickLingering(deltaTime);
            if (!running) return true;

            groupDelay -= Mathf.Max(0f, deltaTime);
            var safety = 0;
            while (running && groupDelay <= 0f && safety++ < 32)
            {
                var groups = skill.Groups;
                if (groupIndex >= groups.Count)
                {
                    running = pendingEffects.Count > 0;
                    break;
                }

                var group = groups[groupIndex++];
                if (group != null)
                {
                    ExecuteGroup(group);
                }

                if (groupIndex >= groups.Count)
                {
                    running = pendingEffects.Count > 0;
                    break;
                }

                groupDelay += groups[groupIndex]?.DelayAfterPrevious ?? 0f;
                if (groupDelay > 0f) break;
            }

            return !running;
        }

        public void TickLingering(float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            for (var index = pendingEffects.Count - 1; index >= 0; index--)
            {
                var pending = pendingEffects[index];
                pending.RemainingDelay -= deltaTime;
                if (pending.RemainingDelay > 0f) continue;
                ApplyEffect(pending.Effect, pending.Target, pending.Amount);
                pendingEffects.RemoveAt(index);
            }

            for (var index = periodicHeals.Count - 1; index >= 0; index--)
            {
                var heal = periodicHeals[index];
                heal.RemainingDuration -= deltaTime;
                heal.NextTick -= deltaTime;
                if (heal.Target == null || !heal.Target.IsAlive || heal.RemainingDuration <= 0f)
                {
                    periodicHeals.RemoveAt(index);
                    continue;
                }
                while (heal.NextTick <= 0f && heal.RemainingDuration > 0f)
                {
                    ApplyInstantHeal(heal.Target, heal.Amount);
                    heal.NextTick += heal.Interval;
                }
            }

            if (running && groupIndex >= (skill?.Groups.Count ?? 0) && pendingEffects.Count == 0)
            {
                running = false;
            }
        }

        public void Reset()
        {
            ResetExecution();
            periodicHeals.Clear();
        }

        private void ResetExecution()
        {
            ReleaseTrackedVfx(null);
            owner = null;
            world = null;
            skill = null;
            primaryTarget = null;
            groupIndex = 0;
            groupDelay = 0f;
            running = false;
            targets.Clear();
            candidates.Clear();
            pendingEffects.Clear();
            trackedVfx.Clear();
        }

        private void ExecuteGroup(MonsterEffectActiveGroup group)
        {
            ResolveTargets(group, targets);
            if (targets.Count == 0) return;

            PlayPresentation(group, targets, MonsterActivePresentationEvent.MotionStart);
            PlayPresentation(group, targets, MonsterActivePresentationEvent.Launch);
            var sourceDamage = owner.EffectiveStats.damage;
            var sourceMaxHealth = owner.Health.MaxHealth;
            for (var targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                var target = targets[targetIndex];
                for (var effectIndex = 0; effectIndex < group.Effects.Count; effectIndex++)
                {
                    var effect = group.Effects[effectIndex];
                    if (effect == null) continue;
                    var amount = ResolveEffectAmount(effect, target, sourceDamage, sourceMaxHealth);
                    if (effect.Delay > 0f)
                    {
                        pendingEffects.Add(new PendingEffect(effect, target, amount, effect.Delay));
                    }
                    else
                    {
                        ApplyEffect(effect, target, amount);
                    }
                }
            }
            PlayPresentation(group, targets, MonsterActivePresentationEvent.Impact);
            PlayPresentation(group, targets, MonsterActivePresentationEvent.AreaResolved);
            PlayPresentation(group, targets, MonsterActivePresentationEvent.StepEnd);
            ReleaseTrackedVfx(MonsterActivePresentationEndPolicy.StepEnd);
        }

        private void ResolveTargets(MonsterEffectActiveGroup group, List<UnitActor> destination)
        {
            destination.Clear();
            if (owner == null || world == null || group == null) return;

            switch (group.Target)
            {
                case MonsterSkillTargetType.Self:
                    destination.Add(owner);
                    break;
                case MonsterSkillTargetType.LowestHealthAlly:
                    CollectTeam(owner.Team, owner.transform.position, float.PositiveInfinity, 256);
                    AddSelected(destination, candidates, CompareLowestHealth);
                    break;
                case MonsterSkillTargetType.HighestAttackAlly:
                    CollectTeam(owner.Team, owner.transform.position, float.PositiveInfinity, 256);
                    AddSelected(destination, candidates, CompareHighestAttack);
                    break;
                case MonsterSkillTargetType.NearbyAllies:
                    CollectTeam(owner.Team, owner.transform.position, Mathf.Max(0.01f, group.Radius),
                        group.MaxTargets);
                    AddCandidates(destination, candidates, group.IncludeCaster);
                    break;
                case MonsterSkillTargetType.AllAllies:
                    CollectTeam(owner.Team, owner.transform.position, float.PositiveInfinity, group.MaxTargets);
                    AddCandidates(destination, candidates, group.IncludeCaster);
                    break;
                case MonsterSkillTargetType.TargetAreaEnemies:
                    var center = ResolveEnemyPrimary()?.transform.position ?? owner.transform.position;
                    CollectTeam(OpponentTeam, center, Mathf.Max(0.01f, group.Radius), group.MaxTargets);
                    AddCandidates(destination, candidates, true);
                    break;
                case MonsterSkillTargetType.FarthestEnemy:
                    CollectTeam(OpponentTeam, owner.transform.position, float.PositiveInfinity, 256);
                    AddSelected(destination, candidates, CompareFarthest);
                    break;
                case MonsterSkillTargetType.LowestHealthEnemy:
                    CollectTeam(OpponentTeam, owner.transform.position, float.PositiveInfinity, 256);
                    AddSelected(destination, candidates, CompareLowestHealth);
                    break;
                case MonsterSkillTargetType.HighestAttackEnemy:
                    CollectTeam(OpponentTeam, owner.transform.position, float.PositiveInfinity, 256);
                    AddSelected(destination, candidates, CompareHighestAttack);
                    break;
                case MonsterSkillTargetType.RangedEnemyFirst:
                    var ranged = world.FindOpponent(owner, float.PositiveInfinity, UnitTargetPriority.RangedFirst);
                    if (ranged != null) destination.Add(ranged);
                    break;
                default:
                    var enemy = ResolveEnemyPrimary();
                    if (enemy != null) destination.Add(enemy);
                    break;
            }
        }

        private void CollectTeam(UnitTeam team, Vector3 center, float radius, int maxTargets)
        {
            world.CollectUnits(team, center, radius, Mathf.Clamp(maxTargets, 1, 256), candidates);
        }

        private void AddCandidates(List<UnitActor> destination, List<UnitActor> source, bool includeCaster)
        {
            for (var index = 0; index < source.Count; index++)
            {
                var candidate = source[index];
                if (candidate != null && candidate.IsAlive && (includeCaster || candidate != owner))
                {
                    destination.Add(candidate);
                }
            }
        }

        private static void AddSelected(
            List<UnitActor> destination,
            List<UnitActor> source,
            Comparison<UnitActor> comparison)
        {
            if (source.Count == 0) return;
            source.Sort(comparison);
            if (source[0] != null) destination.Add(source[0]);
        }

        private int CompareFarthest(UnitActor left, UnitActor right)
        {
            var leftDistance = (left.transform.position - owner.transform.position).sqrMagnitude;
            var rightDistance = (right.transform.position - owner.transform.position).sqrMagnitude;
            return rightDistance.CompareTo(leftDistance);
        }

        private static int CompareLowestHealth(UnitActor left, UnitActor right)
        {
            var leftRatio = left.Health.MaxHealth <= 0f ? 1f : left.Health.CurrentHealth / left.Health.MaxHealth;
            var rightRatio = right.Health.MaxHealth <= 0f ? 1f : right.Health.CurrentHealth / right.Health.MaxHealth;
            return leftRatio.CompareTo(rightRatio);
        }

        private static int CompareHighestAttack(UnitActor left, UnitActor right)
        {
            return right.EffectiveStats.damage.CompareTo(left.EffectiveStats.damage);
        }

        private UnitActor ResolveEnemyPrimary()
        {
            if (primaryTarget != null && primaryTarget.IsAlive && primaryTarget.Team == OpponentTeam)
            {
                return primaryTarget;
            }
            return world.FindNearestOpponent(owner, float.PositiveInfinity);
        }

        private void ApplyEffect(MonsterSkillEffect effect, UnitActor target, float amount)
        {
            if (effect == null || target == null || !target.IsAlive) return;
            var duration = effect.Duration;

            switch (effect.Type)
            {
                case MonsterSkillEffectType.Heal:
                    ApplyInstantHeal(target, amount);
                    if (duration > 0f && effect.RepeatInterval > 0f)
                    {
                        var periodicId = BuildEffectRuntimeId(effect);
                        RemovePeriodicHeal(target, periodicId);
                        periodicHeals.Add(new PeriodicHeal(
                            periodicId,
                            target,
                            amount,
                            duration,
                            Mathf.Max(0.05f, effect.RepeatInterval)));
                    }
                    break;
                case MonsterSkillEffectType.Shield:
                    target.SkillRuntime.GrantShield(owner.ScaleSupportOutput(amount), duration);
                    break;
                case MonsterSkillEffectType.AttackBuff:
                    ApplyStatEffect(target, effect, new MonsterStatModifier(0f, amount, 0f, 0f, 0f, 0f));
                    break;
                case MonsterSkillEffectType.DefenseBuff:
                    ApplyStatEffect(target, effect, new MonsterStatModifier(0f, 0f, amount, 0f, 0f, 0f));
                    break;
                case MonsterSkillEffectType.AttackSpeedBuff:
                    ApplyStatEffect(target, effect, new MonsterStatModifier(0f, 0f, 0f, amount, 0f, 0f));
                    break;
                case MonsterSkillEffectType.AttackDebuff:
                    ApplyStatEffect(target, effect, new MonsterStatModifier(0f, -amount, 0f, 0f, 0f, 0f));
                    break;
                case MonsterSkillEffectType.DefenseDebuff:
                    ApplyStatEffect(target, effect, new MonsterStatModifier(0f, 0f, -amount, 0f, 0f, 0f));
                    break;
                case MonsterSkillEffectType.AttackSpeedDebuff:
                    ApplyStatEffect(target, effect, new MonsterStatModifier(0f, 0f, 0f, -amount, 0f, 0f));
                    break;
                case MonsterSkillEffectType.MoveSpeedDebuff:
                    ApplyStatEffect(target, effect, new MonsterStatModifier(0f, 0f, 0f, 0f, -amount, 0f));
                    break;
                case MonsterSkillEffectType.DamageReduction:
                    target.SkillRuntime.ApplyDamageReduction(amount, duration);
                    break;
                case MonsterSkillEffectType.DamageReflect:
                    target.SkillRuntime.ApplyDamageReflect(amount, duration);
                    break;
                case MonsterSkillEffectType.Cleanse:
                    target.TryCleanseOneDebuff();
                    break;
                case MonsterSkillEffectType.Mark:
                    target.SkillRuntime.ApplyExposure(amount, duration);
                    break;
                case MonsterSkillEffectType.Slow:
                    target.ApplyActiveSlow(Mathf.Clamp(amount, 0f, 0.95f), duration);
                    break;
                case MonsterSkillEffectType.Stun:
                    target.TryApplyActiveStun(duration);
                    break;
                case MonsterSkillEffectType.Pull:
                    target.TryApplyActivePull(owner.transform.position, amount, Mathf.Max(0.05f, duration));
                    break;
                case MonsterSkillEffectType.Taunt:
                    target.ForceTarget(owner.Health, duration);
                    break;
                case MonsterSkillEffectType.EnergyGain:
                    target.SkillRuntime.GrantActiveEnergy(amount);
                    break;
                case MonsterSkillEffectType.EnergyDrain:
                    target.SkillRuntime.DrainActiveEnergy(amount);
                    break;
            }
        }

        private void ApplyStatEffect(
            UnitActor target,
            MonsterSkillEffect effect,
            MonsterStatModifier modifier)
        {
            var stackPolicy = effect.StackPolicy == MonsterSkillStackPolicy.StrongestWins
                ? MonsterBuffStackPolicy.ReplaceIfStronger
                : MonsterBuffStackPolicy.RefreshDuration;
            target.ApplyMonsterBuff(
                BuildEffectRuntimeId(effect),
                modifier,
                effect.Duration,
                stackPolicy);
        }

        private string BuildEffectRuntimeId(MonsterSkillEffect effect) =>
            $"active_{skill.SkillId}_{effect.EffectId}";

        private void RemovePeriodicHeal(UnitActor target, string effectRuntimeId)
        {
            for (var index = periodicHeals.Count - 1; index >= 0; index--)
            {
                var current = periodicHeals[index];
                if (current.Target == target && string.Equals(
                        current.EffectRuntimeId,
                        effectRuntimeId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    periodicHeals.RemoveAt(index);
                }
            }
        }

        private void ApplyInstantHeal(UnitActor target, float amount)
        {
            if (target?.Health == null || amount <= 0f) return;
            var before = target.Health.CurrentHealth;
            target.Health.Heal(owner.ScaleSupportOutput(amount));
            var applied = target.Health.CurrentHealth - before;
            if (applied > 0f)
            {
                world.Feedback?.PlayFloatingNumber(
                    target.transform.position,
                    applied,
                    FloatingNumberStyle.Heal,
                    target.GetInstanceID());
            }
        }

        private static float ResolveEffectAmount(
            MonsterSkillEffect effect,
            UnitActor target,
            float sourceDamage,
            float sourceMaxHealth)
        {
            var magnitude = effect.ResolveMagnitude(UnityEngine.Random.value);
            return effect.ValueSource switch
            {
                MonsterSkillValueSource.Flat => magnitude,
                MonsterSkillValueSource.MaxHealthRatio => sourceMaxHealth * magnitude,
                MonsterSkillValueSource.TargetMaxHealthRatio => target.Health.MaxHealth * magnitude,
                MonsterSkillValueSource.TargetMissingHealthRatio =>
                    Mathf.Max(0f, target.Health.MaxHealth - target.Health.CurrentHealth) * magnitude,
                MonsterSkillValueSource.TargetEnergyCapacityRatio => target.SkillRuntime.EnergyCapacity * magnitude,
                _ => sourceDamage * magnitude
            };
        }

        private void PlayPresentation(
            MonsterEffectActiveGroup group,
            IReadOnlyList<UnitActor> resolvedTargets,
            MonsterActivePresentationEvent presentationEvent)
        {
            var presentation = skill.ResolvePresentation(group.GroupId);
            if (presentation == null) return;

            for (var slotIndex = 0; slotIndex < presentation.Slots.Count; slotIndex++)
            {
                var slot = presentation.Slots[slotIndex];
                if (slot == null || slot.Timing != presentationEvent ||
                    slot.Feedback == null || !slot.Feedback.HasAnyFeedback) continue;
                var occurrenceCount = ResolvePresentationCount(slot, resolvedTargets);
                for (var occurrence = 0; occurrence < occurrenceCount; occurrence++)
                {
                    var target = ResolvePresentationTarget(slot, resolvedTargets, occurrence);
                    var position = ResolvePresentationPosition(slot.Anchor, target, resolvedTargets);
                    var rotation = ResolvePresentationRotation(position);
                    var parent = slot.Attachment == MonsterActivePresentationAttachment.FollowAnchor
                        ? ResolvePresentationParent(slot.Anchor, target)
                        : null;
                    PlaySlot(slot, position, rotation, parent);
                }
            }
        }

        private static int ResolvePresentationCount(
            MonsterActiveAttackPresentationCueBinding slot,
            IReadOnlyList<UnitActor> resolvedTargets)
        {
            var targetsCount = resolvedTargets?.Count ?? 0;
            if (targetsCount <= 0) return 0;
            if (slot.Multiplicity == MonsterActivePresentationMultiplicity.PerTargetHit)
            {
                return targetsCount;
            }
            return slot.Multiplicity == MonsterActivePresentationMultiplicity.ContinuousUntilEnd &&
                   MonsterEffectActiveVfxCompatibility.IsTargetAnchor(slot.Anchor)
                ? targetsCount
                : 1;
        }

        private static UnitActor ResolvePresentationTarget(
            MonsterActiveAttackPresentationCueBinding slot,
            IReadOnlyList<UnitActor> resolvedTargets,
            int occurrence)
        {
            if (resolvedTargets == null || resolvedTargets.Count == 0) return null;
            if (slot.Multiplicity == MonsterActivePresentationMultiplicity.OncePerStep)
            {
                return resolvedTargets[0];
            }
            return resolvedTargets[Mathf.Clamp(occurrence, 0, resolvedTargets.Count - 1)];
        }

        private void PlaySlot(
            MonsterActiveAttackPresentationCueBinding slot,
            Vector3 position,
            Quaternion rotation,
            Transform parent)
        {
            var bodyScale = owner.RuntimeAssetSet?.BodyProfile?.VfxScale ?? 1f;
            var instance = world.SpawnMonsterActiveVfx(
                slot.Feedback,
                position,
                rotation,
                parent,
                bodyScale);
            if (instance == null) return;
            if (slot.Feedback.VfxPrefab != null)
            {
                MonsterBasicAttackVfxPlayback.ApplyInstanceScale(
                    instance,
                    slot.Feedback.VfxPrefab.transform.localScale *
                    slot.Feedback.Scale * Mathf.Max(0.01f, bodyScale));
            }
            if (slot.EndPolicy == MonsterActivePresentationEndPolicy.StepEnd)
            {
                trackedVfx.Add(new TrackedVfx
                {
                    Instance = instance,
                    EndPolicy = slot.EndPolicy
                });
                return;
            }
            world.ScheduleMonsterObjectReturn(
                instance,
                slot.UseDuration ? slot.Duration : slot.Feedback.VfxLifetime);
        }

        private Vector3 ResolvePresentationPosition(
            MonsterActivePresentationAnchor anchor,
            UnitActor target,
            IReadOnlyList<UnitActor> resolvedTargets)
        {
            var root = owner.transform;
            var attackOrigin = owner.AnimationDriver?.AttackOrigin ?? root;
            return anchor switch
            {
                MonsterActivePresentationAnchor.CasterRoot => root.position,
                MonsterActivePresentationAnchor.AttackOrigin or
                    MonsterActivePresentationAnchor.MarkerSocket => attackOrigin.position,
                MonsterActivePresentationAnchor.TargetPoint or
                    MonsterActivePresentationAnchor.TargetRoot =>
                    target == null ? ResolveAreaCenter(resolvedTargets) : target.transform.position,
                MonsterActivePresentationAnchor.HitPoint =>
                    target?.AnimationDriver?.HitCenter?.position ??
                    target?.transform.position ??
                    ResolveAreaCenter(resolvedTargets),
                MonsterActivePresentationAnchor.AreaCenter => ResolveAreaCenter(resolvedTargets),
                _ => root.position
            };
        }

        private Transform ResolvePresentationParent(
            MonsterActivePresentationAnchor anchor,
            UnitActor target)
        {
            return anchor switch
            {
                MonsterActivePresentationAnchor.CasterRoot => owner.transform,
                MonsterActivePresentationAnchor.AttackOrigin or
                    MonsterActivePresentationAnchor.MarkerSocket =>
                    owner.AnimationDriver?.AttackOrigin ?? owner.transform,
                MonsterActivePresentationAnchor.TargetPoint or
                    MonsterActivePresentationAnchor.TargetRoot =>
                    target?.transform,
                MonsterActivePresentationAnchor.HitPoint =>
                    target?.AnimationDriver?.HitCenter ?? target?.transform,
                _ => null
            };
        }

        private Quaternion ResolvePresentationRotation(Vector3 position)
        {
            var forward = position - owner.transform.position;
            forward.y = 0f;
            return forward.sqrMagnitude < 0.0001f
                ? owner.transform.rotation
                : Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private static Vector3 ResolveAreaCenter(IReadOnlyList<UnitActor> resolvedTargets)
        {
            if (resolvedTargets == null || resolvedTargets.Count == 0) return Vector3.zero;
            var total = Vector3.zero;
            var count = 0;
            for (var index = 0; index < resolvedTargets.Count; index++)
            {
                if (resolvedTargets[index] == null) continue;
                total += resolvedTargets[index].transform.position;
                count++;
            }
            return count > 0 ? total / count : Vector3.zero;
        }

        private void ReleaseTrackedVfx(MonsterActivePresentationEndPolicy? policy)
        {
            for (var index = trackedVfx.Count - 1; index >= 0; index--)
            {
                var tracked = trackedVfx[index];
                if (policy.HasValue && tracked.EndPolicy != policy.Value) continue;
                if (tracked.Instance != null) world?.ReturnMonsterObject(tracked.Instance);
                trackedVfx.RemoveAt(index);
            }
        }

        private UnitTeam OpponentTeam => owner.Team == UnitTeam.Player ? UnitTeam.Enemy : UnitTeam.Player;

        private sealed class PendingEffect
        {
            public PendingEffect(MonsterSkillEffect effect, UnitActor target, float amount, float delay)
            {
                Effect = effect;
                Target = target;
                Amount = amount;
                RemainingDelay = delay;
            }

            public MonsterSkillEffect Effect { get; }
            public UnitActor Target { get; }
            public float Amount { get; }
            public float RemainingDelay { get; set; }
        }

        private sealed class PeriodicHeal
        {
            public PeriodicHeal(
                string effectRuntimeId,
                UnitActor target,
                float amount,
                float duration,
                float interval)
            {
                EffectRuntimeId = effectRuntimeId;
                Target = target;
                Amount = amount;
                RemainingDuration = duration;
                Interval = interval;
                NextTick = interval;
            }

            public string EffectRuntimeId { get; }
            public UnitActor Target { get; }
            public float Amount { get; }
            public float RemainingDuration { get; set; }
            public float Interval { get; }
            public float NextTick { get; set; }
        }
    }
}
