using System;
using System.Collections;
using System.Collections.Generic;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    [DisallowMultipleComponent]
    public sealed class CommanderSkillRuntime : MonoBehaviour // 쿨타임·자동사용·투사체 수명 관리
    {
        private const float AutoScanInterval = 0.1f;
        private const string CommanderSkillVoiceResourcePath =
            "Audio/CommanderVoice/SFX_CommanderSkillVoice";
        private readonly float[] cooldownRemaining = new float[CommanderSkillSlotRules.SlotCount];
        private readonly float[] cooldownDuration = new float[CommanderSkillSlotRules.SlotCount];
        private readonly List<ActivePattern> activePatterns = new List<ActivePattern>(8);
        private readonly List<UnitActor> patternTargets = new List<UnitActor>(64);
        private readonly List<ActiveMark> activeMarks = new List<ActiveMark>(16);
        private readonly List<ActiveMark> markTickBuffer = new List<ActiveMark>(16);
        private readonly List<ActiveGlobalModifier> activeModifiers = new List<ActiveGlobalModifier>(4);
        private readonly HashSet<GameObject> activeTransientFeedback = new HashSet<GameObject>();
        private readonly HashSet<GameObject> activeProjectiles = new HashSet<GameObject>();

        private IGameProgressService progress;
        private CommanderSkillCatalog catalog;
        private CombatWorld world;
        private ICommanderSkillCombatGateway combat;
        private ICommanderSkillFeedbackGateway feedback;
        private CommanderSkillEffectRunner effectRunner;
        private readonly List<ICommanderSkillExecutor> executors = new List<ICommanderSkillExecutor>(3);
        private Transform castOrigin;
        private Func<bool> isInputBlocked;
        private Func<float> externalDamageMultiplier;
        private CommanderSkillProgressView progressView;
        private float autoScanRemaining;
        private int castingSlot = -1;
        private CommanderSkillDefinition castingDefinition;
        private float castingRemaining;
        private float castingDuration;
        private float castingMultiplier;
        private bool configured;
        private SfxCue commanderSkillVoice;

        public bool IsPaused => world == null || world.IsPaused || (isInputBlocked?.Invoke() ?? false);
        public bool IsConfigured => configured;
        public bool IsCasting => castingSlot >= 0 && castingDefinition != null;
        public int CastingSlot => IsCasting ? castingSlot : -1;
        public float CastingRemaining => IsCasting ? Mathf.Max(0f, castingRemaining) : 0f;
        public float CastingDuration => IsCasting ? Mathf.Max(0f, castingDuration) : 0f;

        public void Configure(
            IGameProgressService progressService,
            CommanderSkillCatalog skillCatalog,
            CombatWorld combatWorld,
            Transform origin,
            Func<bool> inputBlocked = null,
            Func<float> damageMultiplier = null)
        {
            Shutdown();
            var catalogError = skillCatalog == null ? "Catalog asset is missing." : string.Empty;
            if (skillCatalog == null || !skillCatalog.TryValidate(out catalogError))
            {
                Debug.LogError($"Commander skill catalog is invalid: {catalogError}", skillCatalog);
                return;
            }

            progress = progressService;
            catalog = skillCatalog;
            world = combatWorld;
            castOrigin = origin;
            isInputBlocked = inputBlocked;
            externalDamageMultiplier = damageMultiplier;
            commanderSkillVoice = Resources.Load<SfxCue>(CommanderSkillVoiceResourcePath);
            var gateway = world == null ? null : new CommanderSkillCombatGateway(world);
            combat = gateway;
            feedback = gateway;
            effectRunner = gateway == null
                ? null
                : new CommanderSkillEffectRunner(
                    gateway,
                    new CommanderAreaDamageEffectHandler(gateway),
                    new CommanderUnitEffectHandler(gateway),
                    new CommanderMarkEffectHandler(this),
                    new CommanderRecordedHitDamageEffectHandler(gateway));
            executors.Clear();
            activePatterns.Clear();
            ClearMarks(false);
            activeModifiers.Clear();
            executors.Add(new CommanderAttackSkillExecutor());
            executors.Add(new CommanderEffectSkillExecutor());
            configured = progress != null && catalog != null && world != null && castOrigin != null;
            if (progress != null)
            {
                progress.Changed += RefreshProgress;
            }

            RefreshProgress();
        }

        public void Shutdown()
        {
            if (progress != null)
            {
                progress.Changed -= RefreshProgress;
            }

            StopAllCoroutines();
            for (var index = 0; index < activePatterns.Count; index++)
                ReturnPersistentPatternFeedback(activePatterns[index]);
            ReturnOwnedRuntimeObjects();
            activePatterns.Clear();
            ClearMarks(false);
            activeModifiers.Clear();
            progress = null;
            catalog = null;
            world = null;
            combat = null;
            feedback = null;
            effectRunner = null;
            executors.Clear();
            castOrigin = null;
            isInputBlocked = null;
            externalDamageMultiplier = null;
            configured = false;
            autoScanRemaining = 0f;
            ClearPendingCast();
            for (var index = 0; index < cooldownRemaining.Length; index++)
            {
                cooldownRemaining[index] = 0f;
                cooldownDuration[index] = 0f;
            }
        }

        public bool TryCastSlot(int slotIndex)
        {
            if (!configured || IsPaused || combat == null || !combat.IsReady ||
                slotIndex < 0 || slotIndex >= CommanderSkillSlotRules.SlotCount ||
                IsCasting ||
                !progressView.IsSlotUnlocked(slotIndex) || cooldownRemaining[slotIndex] > 0f ||
                !catalog.TryGet(progressView.GetEquippedSkillId(slotIndex), out var definition))
            {
                return false;
            }

            var executor = FindExecutor(definition);
            if (executor == null)
            {
                return false;
            }

            if (definition.CastTime > 0f &&
                combat.FindTarget(castOrigin.position, definition.Targeting) == null)
            {
                return false; // 대상 없는 빈 캐스팅을 시작하지 않음. 발동 때도 대상은 다시 검증한다.
            }

            var multiplier = GetEffectMultiplier(definition.SkillId, GetOwnedSkillLevel(definition.SkillId)) *
                             Mathf.Max(0f, externalDamageMultiplier?.Invoke() ?? 1f);
            if (definition.CastTime > 0f)
            {
                castingSlot = slotIndex;
                castingDefinition = definition;
                castingDuration = definition.CastTime;
                castingRemaining = definition.CastTime;
                castingMultiplier = multiplier;
                PlayCastingFeedback(definition, castOrigin.position, castOrigin.rotation);
                PlayCommanderSkillVoice();
                return true;
            }

            var activated = TryActivate(slotIndex, definition, executor, multiplier);
            if (activated)
            {
                PlayCommanderSkillVoice();
            }

            return activated;
        }

        public float GetCooldownRemaining(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < cooldownRemaining.Length
                ? Mathf.Max(0f, cooldownRemaining[slotIndex])
                : 0f;
        }

        public float GetCooldownDuration(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < cooldownDuration.Length
                ? Mathf.Max(0f, cooldownDuration[slotIndex])
                : 0f;
        }

        internal int ResolveImpact(
            CommanderSkillDefinition definition,
            CommanderSkillImpactContext impact,
            float effectMultiplier)
        {
            if (!configured || definition == null)
            {
                return 0;
            }

            var appliedCount = effectRunner?.Apply(definition, impact, effectMultiplier) ?? 0;
            PlayFeedback(
                definition.ImpactVfxPrefab,
                definition.ImpactVfxLifetime,
                definition.ImpactSfx,
                impact.Position,
                Quaternion.LookRotation(impact.Forward, Vector3.up),
                definition.ImpactVfxLocalOffset,
                definition.ImpactVfxLocalEuler,
                definition.ImpactVfxScale);
            return appliedCount;
        }

        internal bool TryStartPattern(CommanderSkillDefinition definition, float multiplier)
        {
            if (!configured || definition == null || castOrigin == null || combat == null) return false;
            var target = combat.FindTarget(castOrigin.position, definition.Targeting);
            if (target == null) return false;
            var config = definition.Pattern;
            var total = config.Type switch
            {
                CommanderSkillPatternType.PersistentArea => Mathf.Max(1, Mathf.CeilToInt(config.Duration / config.TickInterval)),
                CommanderSkillPatternType.Chain => config.ChainCount,
                CommanderSkillPatternType.Burst or CommanderSkillPatternType.Barrage or CommanderSkillPatternType.Pulse => config.RepeatCount,
                _ => 1
            };
            var interval = config.Type switch
            {
                CommanderSkillPatternType.PersistentArea => config.TickInterval,
                _ => config.RepeatInterval
            };
            var state = new ActivePattern(definition, multiplier, target, total, interval);
            if (!ExecutePatternHit(state)) return false;
            ApplyGlobalModifiers(definition);
            state.Executed++;
            if (definition.Pattern.Type == CommanderSkillPatternType.PersistentArea)
                StartPersistentPatternFeedback(state);
            if (state.Executed < state.Total || definition.Pattern.Type == CommanderSkillPatternType.PersistentArea)
                activePatterns.Add(state);
            return true;
        }

        internal int ApplyCommanderMark(CommanderSkillDefinition source, CommanderMarkEffectDefinition mark,
            CommanderSkillImpactContext impact, float multiplier)
        {
            if (mark == null || combat == null) return 0;
            patternTargets.Clear();
            if (mark.Scope == CommanderSkillEffectScope.PrimaryTarget)
            {
                if (impact.PrimaryTarget == null) return 0;
                patternTargets.Add(impact.PrimaryTarget);
            }
            else if (mark.Scope == CommanderSkillEffectScope.ImpactTargets) combat.CollectLastDamageTargets(patternTargets);
            else combat.CollectTargets(impact.Position, source.Targeting, mark.Radius, patternTargets);
            var applied = 0;
            for (var index = 0; index < patternTargets.Count && applied < mark.MaxTargets; index++)
            {
                var target = patternTargets[index];
                if (target?.Health == null || !target.IsAlive) continue;
                var existing = FindMark(target, mark.MarkId);
                if (existing != null)
                {
                    existing.Source = source;
                    existing.Multiplier = multiplier;
                    existing.Stacks = Mathf.Min(mark.MaxStacks, existing.Stacks + 1);
                    if (mark.RefreshDurationOnApply) existing.Remaining = mark.Duration;
                    PlayMarkFeedback(mark.OnStack, target, impact.Position, false);
                    if (mark.TriggerType == CommanderMarkTriggerType.StackReached && existing.Stacks >= mark.RequiredStacks)
                        TriggerMark(existing);
                }
                else
                {
                    var active = new ActiveMark(source, mark, target, multiplier);
                    active.Damaged = report => OnMarkedDamaged(active, report);
                    active.Died = report => OnMarkedDied(active, report);
                    target.Health.Damaged += active.Damaged;
                    target.Health.Died += active.Died;
                    active.LoopVfx = PlayMarkFeedback(mark.Loop, target, impact.Position, true);
                    activeMarks.Add(active);
                    PlayMarkFeedback(mark.OnApply, target, impact.Position, false);
                    if (mark.TriggerType == CommanderMarkTriggerType.StackReached && mark.RequiredStacks <= 1)
                        TriggerMark(active);
                }
                applied++;
            }
            return applied;
        }

        internal void ReturnProjectile(GameObject projectile)
        {
            if (projectile == null) return;
            activeProjectiles.Remove(projectile);
            feedback?.Return(projectile);
        }

        internal void PlayCastFeedback(
            CommanderSkillDefinition definition,
            Vector3 position,
            Quaternion rotation)
        {
            if (definition == null)
            {
                return;
            }

            PlayFeedback(
                definition.CastVfxPrefab,
                definition.CastVfxLifetime,
                definition.CastSfx,
                position,
                rotation,
                definition.CastVfxLocalOffset,
                definition.CastVfxLocalEuler,
                definition.CastVfxScale);
        }

        internal void PlayCastingFeedback(
            CommanderSkillDefinition definition,
            Vector3 position,
            Quaternion rotation)
        {
            if (definition == null)
            {
                return;
            }

            PlayFeedback(
                definition.CastingVfxPrefab,
                definition.CastingVfxLifetime,
                definition.CastingSfx,
                position,
                rotation,
                definition.CastingVfxLocalOffset,
                definition.CastingVfxLocalEuler,
                definition.CastingVfxScale);
        }

        private void Update()
        {
            if (!configured || IsPaused)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            TickPatterns(deltaTime);
            TickMarks(deltaTime);
            TickGlobalModifiers(deltaTime);
            var cooldownRecovery = ResolveCooldownRecoveryMultiplier();
            for (var index = 0; index < cooldownRemaining.Length; index++)
            {
                cooldownRemaining[index] = Mathf.Max(0f, cooldownRemaining[index] - deltaTime * cooldownRecovery);
            }

            if (TickPendingCast(deltaTime))
            {
                return;
            }

            if (IsCasting)
            {
                return;
            }

            if (!progressView.AutoUseEnabled)
            {
                return;
            }

            autoScanRemaining -= deltaTime;
            if (autoScanRemaining > 0f)
            {
                return;
            }

            autoScanRemaining = AutoScanInterval;
            CommanderSkillPriority.TryUseFirstReadySlot(
                progressView,
                cooldownRemaining,
                catalog,
                TryCastSlot);
        }

        private void RefreshProgress()
        {
            progressView = progress?.View.CommanderSkills ?? default;
        }

        private void TickPatterns(float deltaTime)
        {
            for (var index = activePatterns.Count - 1; index >= 0; index--)
            {
                var state = activePatterns[index];
                var elapsed = Mathf.Max(0f, deltaTime);
                state.Remaining -= elapsed;
                if (state.Definition.Pattern.Type == CommanderSkillPatternType.PersistentArea)
                    state.LifetimeRemaining -= elapsed;
                while (state.Remaining <= 0f && state.Executed < state.Total)
                {
                    if (!ExecutePatternHit(state)) { state.Executed = state.Total; break; }
                    state.Executed++;
                    state.Remaining += Mathf.Max(0.0001f, state.Interval);
                }
                var complete = state.Definition.Pattern.Type == CommanderSkillPatternType.PersistentArea
                    ? state.LifetimeRemaining <= 0f
                    : state.Executed >= state.Total;
                if (complete)
                {
                    ReturnPersistentPatternFeedback(state);
                    activePatterns.RemoveAt(index);
                }
            }
        }

        private void TickMarks(float deltaTime)
        {
            markTickBuffer.Clear();
            markTickBuffer.AddRange(activeMarks);
            for (var index = markTickBuffer.Count - 1; index >= 0; index--)
            {
                var mark = markTickBuffer[index];
                if (!activeMarks.Contains(mark)) continue;
                mark.Remaining -= deltaTime;
                mark.Cooldown = Mathf.Max(0f, mark.Cooldown - deltaTime);
                if (mark.Target == null || !mark.Target.IsAlive) { RemoveMark(mark, false); continue; }
                if (mark.Remaining > 0f) continue;
                if (mark.Definition.TriggerType == CommanderMarkTriggerType.Expire) TriggerMark(mark);
                if (activeMarks.Contains(mark)) RemoveMark(mark, true);
            }
            markTickBuffer.Clear();
        }

        private void OnMarkedDamaged(ActiveMark mark, DamageReport report)
        {
            if (mark.IsTriggering || !activeMarks.Contains(mark) || !mark.Definition.Counts(report.Request.Origin)) return;
            mark.Hits++;
            if (mark.Definition.RecordHitCount)
            {
                mark.RecordedHits++;
                mark.RecordedDamage += report.AppliedDamage;
            }
            if (mark.Definition.TriggerType == CommanderMarkTriggerType.HitCount && mark.Hits >= ResolveRequiredHits(mark.Definition))
                TriggerMark(mark);
        }

        private void OnMarkedDied(ActiveMark mark, DamageReport report)
        {
            if (!activeMarks.Contains(mark)) return;
            if (mark.Definition.TriggerType == CommanderMarkTriggerType.Death) TriggerMark(mark);
            if (activeMarks.Contains(mark)) RemoveMark(mark, false);
        }

        private void TriggerMark(ActiveMark mark, bool broadcastMarkTriggered = true)
        {
            if (mark == null || mark.IsTriggering || !activeMarks.Contains(mark) || mark.Cooldown > 0f || mark.Target == null) return;
            mark.IsTriggering = true;
            try
            {
                mark.Cooldown = mark.Definition.TriggerCooldown;
                var target = mark.Target;
                var position = mark.Target.transform.position + Vector3.up * 0.45f;
                PlayMarkFeedback(mark.Definition.OnTrigger, mark.Target, position, false);
                effectRunner?.Apply(mark.Source, mark.Definition.EffectsOnTrigger,
                    new CommanderSkillImpactContext(castOrigin.position, mark.Target, position,
                        position - castOrigin.position, CombatDamageOrigin.CommanderMarkTrigger,
                        mark.RecordedHits, mark.RecordedDamage),
                    mark.Multiplier * ResolveMarkTriggerDamageMultiplier());
                if (mark.Definition.ConsumeOnTrigger) RemoveMark(mark, true);
                else { mark.Hits = 0; mark.Stacks = 0; }
                if (broadcastMarkTriggered) NotifyMarkTriggered(mark, target);
            }
            finally { mark.IsTriggering = false; }
        }

        private void NotifyMarkTriggered(ActiveMark sourceMark, UnitActor target)
        {
            var markTriggeredBuffer = new List<ActiveMark>(8);
            for (var index = 0; index < activeMarks.Count; index++)
            {
                var candidate = activeMarks[index];
                if (candidate != sourceMark && candidate.Target == target &&
                    candidate.Definition.TriggerType == CommanderMarkTriggerType.MarkTriggered)
                    markTriggeredBuffer.Add(candidate);
            }
            for (var index = 0; index < markTriggeredBuffer.Count; index++)
                if (activeMarks.Contains(markTriggeredBuffer[index])) TriggerMark(markTriggeredBuffer[index], false);
            markTriggeredBuffer.Clear();
        }

        private void ApplyGlobalModifiers(CommanderSkillDefinition source)
        {
            if (source?.Effects == null) return;
            for (var effectIndex = 0; effectIndex < source.Effects.Count; effectIndex++)
            {
                if (source.Effects[effectIndex] is not CommanderGlobalModifierEffectDefinition definition) continue;
                ActiveGlobalModifier existing = null;
                for (var index = 0; index < activeModifiers.Count; index++)
                    if (activeModifiers[index].Source == source && activeModifiers[index].Definition == definition)
                    { existing = activeModifiers[index]; break; }
                if (existing != null) existing.Remaining = definition.Duration;
                else activeModifiers.Add(new ActiveGlobalModifier(source, definition));
            }
        }

        private void TickGlobalModifiers(float deltaTime)
        {
            for (var index = activeModifiers.Count - 1; index >= 0; index--)
            {
                activeModifiers[index].Remaining -= Mathf.Max(0f, deltaTime);
                if (activeModifiers[index].Remaining <= 0f) activeModifiers.RemoveAt(index);
            }
        }

        private int ResolveRequiredHits(CommanderMarkEffectDefinition mark)
        {
            var multiplier = 1f;
            for (var index = 0; index < activeModifiers.Count; index++)
                multiplier *= activeModifiers[index].Definition.MarkRequiredHitsMultiplier;
            return Mathf.Max(1, Mathf.CeilToInt(mark.RequiredHits * multiplier));
        }

        private float ResolveMarkTriggerDamageMultiplier()
        {
            var multiplier = 1f;
            for (var index = 0; index < activeModifiers.Count; index++)
                multiplier *= activeModifiers[index].Definition.MarkTriggerDamageMultiplier;
            return multiplier;
        }

        private float ResolveCooldownRecoveryMultiplier()
        {
            var multiplier = 1f;
            for (var index = 0; index < activeModifiers.Count; index++)
                multiplier *= activeModifiers[index].Definition.CooldownRecoveryMultiplier;
            return multiplier;
        }

        private ActiveMark FindMark(UnitActor target, string markId)
        {
            for (var index = 0; index < activeMarks.Count; index++)
                if (activeMarks[index].Target == target && activeMarks[index].Definition.MarkId == markId) return activeMarks[index];
            return null;
        }

        private GameObject PlayMarkFeedback(CommanderMarkFeedbackSlot slot, UnitActor target, Vector3 hitPoint, bool persistent)
        {
            if (slot == null || target == null) return null;
            var parent = slot.Anchor switch
            {
                CommanderMarkFeedbackAnchor.TargetRoot or CommanderMarkFeedbackAnchor.TargetCenter or
                    CommanderMarkFeedbackAnchor.TargetFeet => target.transform,
                CommanderMarkFeedbackAnchor.CasterRoot => castOrigin,
                _ => null
            };
            var position = slot.Anchor switch
            {
                CommanderMarkFeedbackAnchor.TargetRoot => target.transform.position,
                CommanderMarkFeedbackAnchor.TargetCenter => target.transform.position + Vector3.up * 0.45f,
                CommanderMarkFeedbackAnchor.TargetFeet => target.transform.position,
                CommanderMarkFeedbackAnchor.HitPoint or CommanderMarkFeedbackAnchor.WorldPosition => hitPoint,
                CommanderMarkFeedbackAnchor.CasterRoot => castOrigin.position,
                _ => hitPoint
            };
            var offset = parent == null ? slot.LocalOffset : parent.TransformVector(slot.LocalOffset);
            var rotation = parent == null
                ? Quaternion.Euler(slot.LocalEuler)
                : parent.rotation * Quaternion.Euler(slot.LocalEuler);
            position += offset;
            feedback?.PlaySfx(slot.Sfx, position);
            if (slot.VfxPrefab == null || feedback == null) return null;
            var instance = feedback.Rent(slot.VfxPrefab, position, rotation);
            if (instance == null) return null;
            MonsterBasicAttackVfxPlayback.ApplyInstanceScale(instance, slot.VfxPrefab.transform.localScale * slot.Scale);
            if (parent != null) instance.transform.SetParent(parent, true);
            if (persistent)
            {
                return instance;
            }
            activeTransientFeedback.Add(instance);
            StartCoroutine(ReturnFeedbackAfter(instance, slot.Lifetime));
            return instance;
        }

        private void RemoveMark(ActiveMark mark, bool playRemove)
        {
            if (mark == null || !activeMarks.Remove(mark)) return;
            if (mark.Target?.Health != null)
            {
                mark.Target.Health.Damaged -= mark.Damaged;
                mark.Target.Health.Died -= mark.Died;
                if (playRemove) PlayMarkFeedback(mark.Definition.OnRemove, mark.Target, mark.Target.transform.position, false);
            }
            if (mark.LoopVfx != null) { mark.LoopVfx.transform.SetParent(null, true); feedback?.Return(mark.LoopVfx); }
        }

        private void ClearMarks(bool playRemove)
        {
            for (var index = activeMarks.Count - 1; index >= 0; index--) RemoveMark(activeMarks[index], playRemove);
        }

        private bool ExecutePatternHit(ActivePattern state)
        {
            var definition = state.Definition;
            var persistent = definition.Pattern.Type == CommanderSkillPatternType.PersistentArea;
            var target = persistent
                ? state.Target != null && state.Target.IsAlive ? state.Target : null
                : ResolvePatternTarget(state);
            if (!persistent && target == null) return false;
            var start = castOrigin.position + Vector3.up * 1.15f;
            var destination = state.FixedPosition;
            if (definition.Pattern.Type is not CommanderSkillPatternType.PersistentArea)
                destination = target.transform.position + Vector3.up * 0.45f;
            if (definition.Pattern.Type == CommanderSkillPatternType.Barrage)
            {
                var offset = UnityEngine.Random.insideUnitCircle * definition.Pattern.RandomRadius;
                destination += new Vector3(offset.x, 0f, offset.y);
            }
            var direction = destination - start;
            var rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : Quaternion.identity;
            var origin = definition.Pattern.Type == CommanderSkillPatternType.PersistentArea
                ? CombatDamageOrigin.CommanderPeriodic
                : CombatDamageOrigin.CommanderSkill;
            if (definition is CommanderAttackSkillDefinition attack &&
                attack.DeliveryModule == MonsterBasicAttackDeliveryModule.Projectile)
            {
                var projectileObject = feedback.Rent(attack.ProjectilePrefab, start, rotation);
                var projectile = projectileObject == null ? null : projectileObject.GetComponent<CommanderSkillProjectile>();
                if (projectile == null) { feedback.Return(projectileObject); return false; }
                activeProjectiles.Add(projectileObject);
                projectile.Launch(this, attack, target, destination, state.Multiplier,
                    definition.Pattern.Type != CommanderSkillPatternType.Barrage);
            }
            else
            {
                ResolveImpact(definition, new CommanderSkillImpactContext(start, target, destination, direction, origin), state.Multiplier);
            }
            if (!persistent || state.Executed == 0) PlayCastFeedback(definition, start, rotation);
            state.LastPosition = destination;
            if (target != null) state.HitIds.Add(target.GetInstanceID());
            return true;
        }

        private UnitActor ResolvePatternTarget(ActivePattern state)
        {
            if (state.Definition.Pattern.Type == CommanderSkillPatternType.Chain && state.Executed > 0)
            {
                combat.CollectTargets(state.LastPosition, state.Definition.Targeting,
                    state.Definition.Pattern.ChainRadius, patternTargets);
                for (var index = 0; index < patternTargets.Count; index++)
                {
                    var candidate = patternTargets[index];
                    if (candidate != null && candidate.IsAlive && !state.HitIds.Contains(candidate.GetInstanceID()))
                        return state.Target = candidate;
                }
                return null;
            }
            if (state.Target == null || !state.Target.IsAlive)
                state.Target = combat.FindTarget(castOrigin.position, state.Definition.Targeting);
            return state.Target;
        }

        private sealed class ActivePattern
        {
            public ActivePattern(CommanderSkillDefinition definition, float multiplier, UnitActor target, int total, float interval)
            {
                Definition = definition; Multiplier = multiplier; Target = target; Total = total; Interval = interval;
                Remaining = interval; FixedPosition = target.transform.position + Vector3.up * 0.45f;
                LifetimeRemaining = definition.Pattern.Type == CommanderSkillPatternType.PersistentArea
                    ? definition.Pattern.Duration
                    : 0f;
            }
            public readonly CommanderSkillDefinition Definition;
            public readonly float Multiplier;
            public readonly int Total;
            public readonly float Interval;
            public readonly Vector3 FixedPosition;
            public readonly HashSet<int> HitIds = new HashSet<int>();
            public UnitActor Target;
            public Vector3 LastPosition;
            public int Executed;
            public float Remaining;
            public float LifetimeRemaining;
            public GameObject PersistentVfx;
        }

        private void StartPersistentPatternFeedback(ActivePattern state)
        {
            var definition = state?.Definition;
            if (definition?.PersistentVfxPrefab == null || feedback == null) return;
            var parent = definition.PersistentVfxAnchor switch
            {
                CommanderMarkFeedbackAnchor.CasterRoot => castOrigin,
                CommanderMarkFeedbackAnchor.TargetRoot or CommanderMarkFeedbackAnchor.TargetCenter or
                    CommanderMarkFeedbackAnchor.TargetFeet => state.Target == null ? null : state.Target.transform,
                _ => null
            };
            var position = definition.PersistentVfxAnchor switch
            {
                CommanderMarkFeedbackAnchor.CasterRoot => castOrigin.position,
                CommanderMarkFeedbackAnchor.TargetCenter when state.Target != null =>
                    state.Target.transform.position + Vector3.up * 0.45f,
                CommanderMarkFeedbackAnchor.TargetRoot or CommanderMarkFeedbackAnchor.TargetFeet when state.Target != null =>
                    state.Target.transform.position,
                _ => state.FixedPosition
            };
            var rotation = parent == null
                ? Quaternion.Euler(definition.PersistentVfxLocalEuler)
                : parent.rotation * Quaternion.Euler(definition.PersistentVfxLocalEuler);
            var offset = parent == null
                ? definition.PersistentVfxLocalOffset
                : parent.TransformVector(definition.PersistentVfxLocalOffset);
            state.PersistentVfx = feedback.Rent(definition.PersistentVfxPrefab, position + offset, rotation);
            if (state.PersistentVfx == null) return;
            MonsterBasicAttackVfxPlayback.ApplyInstanceScale(state.PersistentVfx,
                definition.PersistentVfxPrefab.transform.localScale * definition.PersistentVfxScale);
            if (parent != null) state.PersistentVfx.transform.SetParent(parent, true);
        }

        private void ReturnPersistentPatternFeedback(ActivePattern state)
        {
            if (state?.PersistentVfx == null) return;
            state.PersistentVfx.transform.SetParent(null, true);
            feedback?.Return(state.PersistentVfx);
            state.PersistentVfx = null;
        }

        private sealed class ActiveMark
        {
            public ActiveMark(CommanderSkillDefinition source, CommanderMarkEffectDefinition definition, UnitActor target, float multiplier)
            { Source = source; Definition = definition; Target = target; Multiplier = multiplier; Remaining = definition.Duration; Stacks = 1; }
            public CommanderSkillDefinition Source;
            public readonly CommanderMarkEffectDefinition Definition;
            public readonly UnitActor Target;
            public float Multiplier;
            public Action<DamageReport> Damaged;
            public Action<DamageReport> Died;
            public GameObject LoopVfx;
            public float Remaining;
            public float Cooldown;
            public bool IsTriggering;
            public int Hits;
            public int Stacks;
            public int RecordedHits;
            public float RecordedDamage;
        }

        private sealed class ActiveGlobalModifier
        {
            public ActiveGlobalModifier(CommanderSkillDefinition source, CommanderGlobalModifierEffectDefinition definition)
            { Source = source; Definition = definition; Remaining = definition.Duration; }
            public readonly CommanderSkillDefinition Source;
            public readonly CommanderGlobalModifierEffectDefinition Definition;
            public float Remaining;
        }

        private int GetOwnedSkillLevel(string skillId)
        {
            var owned = progressView.OwnedSkills;
            for (var index = 0; index < owned.Count; index++)
            {
                if (owned[index].SkillId == skillId)
                {
                    return owned[index].Level;
                }
            }

            return 1;
        }

        private float GetEffectMultiplier(string skillId, int level)
        {
            return catalog != null && catalog.BalanceConfig.TryGetRule(skillId, out var rule)
                ? rule.GetDamageMultiplier(level)
                : 1f;
        }

        private ICommanderSkillExecutor FindExecutor(CommanderSkillDefinition definition)
        {
            for (var index = 0; index < executors.Count; index++)
            {
                if (executors[index].Supports(definition))
                {
                    return executors[index];
                }
            }

            return null;
        }

        private bool TryActivate(
            int slotIndex,
            CommanderSkillDefinition definition,
            ICommanderSkillExecutor executor,
            float multiplier)
        {
            var context = new CommanderSkillExecutionContext(
                this,
                combat,
                feedback,
                castOrigin,
                multiplier);
            if (executor == null || !executor.TryExecute(definition, context))
            {
                return false; // 대상·전달 실패 시 쿨타임과 피드백을 시작하지 않음
            }

            cooldownDuration[slotIndex] = definition.Cooldown;
            cooldownRemaining[slotIndex] = definition.Cooldown;
            return true;
        }

        private bool TickPendingCast(float deltaTime)
        {
            if (!IsCasting)
            {
                return false;
            }

            castingRemaining = Mathf.Max(0f, castingRemaining - Mathf.Max(0f, deltaTime));
            if (castingRemaining > 0f)
            {
                return false;
            }

            var slotIndex = castingSlot;
            var definition = castingDefinition;
            var multiplier = castingMultiplier;
            ClearPendingCast();

            if (slotIndex < 0 || slotIndex >= CommanderSkillSlotRules.SlotCount ||
                definition == null || catalog == null ||
                !catalog.TryGet(progressView.GetEquippedSkillId(slotIndex), out var current) ||
                current != definition)
            {
                return true;
            }

            TryActivate(slotIndex, definition, FindExecutor(definition), multiplier);
            return true;
        }

        private void ClearPendingCast()
        {
            castingSlot = -1;
            castingDefinition = null;
            castingRemaining = 0f;
            castingDuration = 0f;
            castingMultiplier = 0f;
        }

        private void PlayFeedback(
            GameObject vfxPrefab,
            float lifetime,
            ProjectMT.Shared.Audio.SfxCue sfx,
            Vector3 position,
            Quaternion rotation,
            Vector3 localOffset,
            Vector3 localEuler,
            float scale)
        {
            var resolvedPosition = position + rotation * localOffset;
            var resolvedRotation = rotation * Quaternion.Euler(localEuler);
            feedback?.PlaySfx(sfx, resolvedPosition);
            if (vfxPrefab == null || feedback == null)
            {
                return;
            }

            var instance = feedback.Rent(vfxPrefab, resolvedPosition, resolvedRotation);
            if (instance != null)
            {
                activeTransientFeedback.Add(instance);
                MonsterBasicAttackVfxPlayback.ApplyInstanceScale(
                    instance,
                    vfxPrefab.transform.localScale * Mathf.Max(0.01f, scale));
                StartCoroutine(ReturnFeedbackAfter(instance, lifetime));
            }
        }

        private void PlayCommanderSkillVoice()
        {
            if (castOrigin != null)
            {
                feedback?.PlaySfx(commanderSkillVoice, castOrigin.position);
            }
        }

        private IEnumerator ReturnFeedbackAfter(GameObject instance, float delay)
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, delay));
            ReturnTransientFeedback(instance);
        }

        private void ReturnTransientFeedback(GameObject instance)
        {
            if (instance == null || !activeTransientFeedback.Remove(instance)) return;
            instance.transform.SetParent(null, true);
            feedback?.Return(instance);
        }

        private void ReturnOwnedRuntimeObjects()
        {
            var gateway = feedback;
            foreach (var projectile in activeProjectiles)
            {
                if (projectile != null) gateway?.Return(projectile);
            }
            activeProjectiles.Clear();
            foreach (var instance in activeTransientFeedback)
            {
                if (instance == null) continue;
                instance.transform.SetParent(null, true);
                gateway?.Return(instance);
            }
            activeTransientFeedback.Clear();
        }

        private void OnDestroy()
        {
            Shutdown();
        }
    }
}
