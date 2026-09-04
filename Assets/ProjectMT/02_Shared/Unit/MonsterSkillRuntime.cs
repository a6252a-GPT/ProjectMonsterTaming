using System.Collections.Generic;
using ProjectMT.Shared.Combat;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public sealed class MonsterSkillRuntime // 유닛별 패시브·에너지·자동 액티브 상태
    {
        private const string PassiveHasteId = "passive_same_target_haste";
        private const string CourageAuraId = "passive_courage_aura";
        private const string FirstWaveId = "passive_first_wave";

        private readonly List<UnitActor> unitBuffer = new List<UnitActor>();
        private readonly HashSet<int> couragePresentedRecipients = new HashSet<int>();
        private readonly MonsterActiveAttackExecutor activeAttackExecutor = new MonsterActiveAttackExecutor();
        private readonly MonsterEffectActiveExecutor activeEffectExecutor = new MonsterEffectActiveExecutor();
        private UnitActor owner;
        private CombatWorld world;
        private MonsterPassiveSkill passiveSkill;
        private MonsterActiveSkill activeSkill;
        private GenericMonsterPassiveSkill genericSkill;
        private MonsterSkillEffect outgoingRandomEffect;
        private MonsterSkillEffect activeDamageEffect;
        private UnitActor activeTarget;
        private UnitActor continuousTarget;
        private float outgoingRandomRemaining;
        private float energy;
        private float nextActiveHitDelay;
        private float lastReceivedDamage;
        private float periodicRemaining;
        private float cooldownRemaining;
        private float exposureRate;
        private float exposureRemaining;
        private float damageReductionRate;
        private float damageReductionRemaining;
        private float damageReflectRate;
        private float damageReflectRemaining;
        private float shieldAmount;
        private float shieldRemaining;
        private float activeAttackExecutionElapsed;
        private float activeAttackExpectedDuration;
        private int remainingActiveHits;
        private int basicHitCount;
        private int continuousHits;
        private int monsterLevel = 1;
        private bool executingActive;
        private bool waitingForActiveFocus;
        private bool activeFocusQueued;
        private bool canArmActiveFocus;
        private bool activeFirstStepMotionStarted;
        private int activeCommitMarkerBaseline;
        private bool frontlineBondActive;

        public MonsterPassiveSkill PassiveSkill => passiveSkill;
        public MonsterActiveSkill ActiveSkill => activeSkill;
        public float Energy => energy;
        public float EnergyCapacity => activeSkill == null ? 0f : activeSkill.EnergyCost;
        public bool IsPassiveActive => outgoingRandomEffect != null && outgoingRandomRemaining > 0f;
        public bool IsExecuting => executingActive;
        public bool IsActiveFocusQueued => activeFocusQueued;
        public int RemainingActiveHits => activeAttackExecutor.IsRunning && activeSkill is MonsterAttackActiveSkill attack
            ? Mathf.Max(0, attack.Steps.Count - activeAttackExecutor.CompletedStepCount)
            : remainingActiveHits;
        public float ShieldAmount => shieldRemaining > 0f ? shieldAmount : 0f;
        public bool WillEnhanceNextBasicHit =>
            genericSkill != null &&
            genericSkill.RuntimeKind == GenericMonsterPassiveRuntimeKind.RhythmPower &&
            (basicHitCount + 1) % genericSkill.TriggerCount == 0;

        public void Initialize(
            UnitActor unit,
            CombatWorld combatWorld,
            MonsterPassiveSkill passive,
            MonsterActiveSkill active,
            int level = 1,
            UnitEntryReason entryReason = UnitEntryReason.InitialDeployment)
        {
            Shutdown();
            owner = unit;
            world = combatWorld;
            passiveSkill = passive;
            activeSkill = active;
            monsterLevel = Mathf.Max(1, level);
            genericSkill = passive as GenericMonsterPassiveSkill;
            if (genericSkill != null && !genericSkill.AuthoringEnabled)
            {
                genericSkill = null;
            }

            TryActivatePassive(MonsterSkillTriggerType.CombatJoin);
            TryActivatePassive(MonsterSkillTriggerType.CombatStart);
            ApplyEntryPassive(entryReason);
        }

        public void Shutdown()
        {
            world?.CancelMonsterActiveFocus(owner);
            owner = null;
            world = null;
            passiveSkill = null;
            activeSkill = null;
            genericSkill = null;
            outgoingRandomEffect = null;
            activeDamageEffect = null;
            activeTarget = null;
            continuousTarget = null;
            outgoingRandomRemaining = 0f;
            energy = 0f;
            nextActiveHitDelay = 0f;
            lastReceivedDamage = 0f;
            periodicRemaining = 0f;
            cooldownRemaining = 0f;
            exposureRate = 0f;
            exposureRemaining = 0f;
            damageReductionRate = 0f;
            damageReductionRemaining = 0f;
            damageReflectRate = 0f;
            damageReflectRemaining = 0f;
            shieldAmount = 0f;
            shieldRemaining = 0f;
            activeAttackExecutionElapsed = 0f;
            activeAttackExpectedDuration = 0f;
            remainingActiveHits = 0;
            basicHitCount = 0;
            continuousHits = 0;
            monsterLevel = 1;
            executingActive = false;
            waitingForActiveFocus = false;
            activeFocusQueued = false;
            canArmActiveFocus = false;
            activeFirstStepMotionStarted = false;
            activeCommitMarkerBaseline = 0;
            frontlineBondActive = false;
            unitBuffer.Clear();
            couragePresentedRecipients.Clear();
            activeAttackExecutor.Reset();
            activeEffectExecutor.Reset();
        }

        public void Tick(float deltaTime, bool canBeginActive)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            canArmActiveFocus = canBeginActive;
            if (!executingActive && activeEffectExecutor.HasLingering)
            {
                activeEffectExecutor.TickLingering(deltaTime);
            }
            TickPassiveDurations(deltaTime);
            TickGenericPassive(deltaTime);

            if (owner == null || world == null || activeSkill == null || !owner.IsAlive)
            {
                return;
            }

            if (executingActive)
            {
                if (activeSkill is MonsterAttackActiveSkill)
                {
                    activeAttackExecutionElapsed += deltaTime;
                }
                TickExecutingActive(deltaTime);
                return;
            }

            energy = Mathf.Min(
                activeSkill.EnergyCost,
                energy + MonsterActiveEnergyConfig.SharedEnergyPerSecond * deltaTime);
            if (!activeFocusQueued && energy >= activeSkill.EnergyCost &&
                owner.CanQueueMonsterActiveFocus)
            {
                TryQueueActive();
            }
        }

        public void NotifyBasicAttackHit(bool successful)
        {
            NotifyBasicAttackHit(successful, owner?.Target);
        }

        public void NotifyBasicAttackHit(bool successful, UnitActor hitTarget)
        {
            if (!successful)
            {
                return;
            }

            TryActivatePassive(MonsterSkillTriggerType.BasicAttackHit);
            basicHitCount++;
            UpdateContinuousTarget(hitTarget);
            if (genericSkill == null || owner == null || world == null || hitTarget == null)
            {
                return;
            }

            switch (genericSkill.RuntimeKind)
            {
                case GenericMonsterPassiveRuntimeKind.RhythmPower:
                    if (IsNthHit())
                    {
                        QueueStatus(owner, "강화!", CombatStatusTextStyle.Enhanced);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.SameTargetHaste:
                    ApplySameTargetHaste();
                    break;
                case GenericMonsterPassiveRuntimeKind.ImpactStrike:
                    if (IsNthHit())
                    {
                        hitTarget.TryApplyCombatStagger(genericSkill.Duration);
                        QueueStatus(owner, "충격!", CombatStatusTextStyle.Impact);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.FractureMark:
                    if (continuousHits % genericSkill.TriggerCount == 0)
                    {
                        hitTarget.SkillRuntime.ApplyExposure(
                            genericSkill.ResolvePrimary(monsterLevel),
                            genericSkill.Duration);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.ThreatMark:
                    if (hitTarget.IsRanged || hitTarget.IsBoss)
                    {
                        hitTarget.SkillRuntime.ApplyExposure(
                            genericSkill.ResolveSecondary(monsterLevel),
                            genericSkill.Duration);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.HealingShot:
                    if (IsNthHit())
                    {
                        HealLowestAlly();
                    }
                    break;
            }
        }

        public void NotifyDamaged(DamageReport report)
        {
            lastReceivedDamage = Mathf.Max(0f, report.AppliedDamage);
            TryActivatePassive(MonsterSkillTriggerType.Damaged);
        }

        public void NotifyBasicAttackPerformed()
        {
            AddEnergy(MonsterActiveEnergyConfig.SharedEnergyPerBasicAttack);
        }

        public void NotifyTargetDestroyed()
        {
            if (genericSkill == null || owner == null || cooldownRemaining > 0f ||
                genericSkill.RuntimeKind != GenericMonsterPassiveRuntimeKind.KillHeal)
            {
                return;
            }

            cooldownRemaining = genericSkill.Cooldown;
            var before = owner.Health.CurrentHealth;
            owner.Health.Heal(owner.Health.MaxHealth * genericSkill.ResolvePrimary(monsterLevel));
            QueueHeal(owner, owner.Health.CurrentHealth - before);
        }

        public float ResolveOutgoingDamageMultiplier()
        {
            return ResolveOutgoingDamageMultiplier(owner?.Target, Random.value);
        }

        public float ResolveOutgoingDamageMultiplier(float random01)
        {
            return ResolveOutgoingDamageMultiplier(owner?.Target, random01);
        }

        public float ResolveOutgoingDamageMultiplier(UnitActor target)
        {
            return ResolveOutgoingDamageMultiplier(target, Random.value);
        }

        public float ResolveIncomingDamage(float amount, out float absorbedByShield)
        {
            absorbedByShield = 0f;
            var resolved = Mathf.Max(0f, amount);
            if (exposureRemaining > 0f)
            {
                resolved *= 1f + exposureRate;
            }
            if (damageReductionRemaining > 0f)
            {
                resolved *= Mathf.Clamp01(1f - damageReductionRate);
            }
            if (shieldRemaining > 0f && shieldAmount > 0f)
            {
                absorbedByShield = Mathf.Min(shieldAmount, resolved);
                shieldAmount -= absorbedByShield;
                resolved -= absorbedByShield;
                if (shieldAmount <= 0f)
                {
                    shieldRemaining = 0f;
                }
            }
            return resolved;
        }

        public void ApplyExposure(float rate, float duration)
        {
            rate = Mathf.Max(0f, rate);
            duration = Mathf.Max(0f, duration);
            if (rate <= 0f || duration <= 0f)
            {
                return;
            }
            if (rate >= exposureRate || exposureRemaining <= 0f)
            {
                exposureRate = rate;
            }
            exposureRemaining = Mathf.Max(exposureRemaining, duration);
        }

        public bool TryCleanseExposure()
        {
            if (exposureRemaining <= 0f) return false;
            exposureRate = 0f;
            exposureRemaining = 0f;
            return true;
        }

        public void ApplyDamageReduction(float rate, float duration)
        {
            rate = Mathf.Clamp01(rate);
            duration = Mathf.Max(0f, duration);
            if (rate <= 0f || duration <= 0f)
            {
                return;
            }
            if (rate >= damageReductionRate || damageReductionRemaining <= 0f)
            {
                damageReductionRate = rate;
            }
            damageReductionRemaining = Mathf.Max(damageReductionRemaining, duration);
        }

        public void ApplyDamageReflect(float rate, float duration)
        {
            rate = Mathf.Clamp01(rate);
            duration = Mathf.Max(0f, duration);
            if (rate <= 0f || duration <= 0f) return;
            if (rate >= damageReflectRate || damageReflectRemaining <= 0f)
            {
                damageReflectRate = rate;
            }
            damageReflectRemaining = Mathf.Max(damageReflectRemaining, duration);
        }

        public float ResolveReflectedDamage(float appliedDamage)
        {
            return damageReflectRemaining > 0f
                ? Mathf.Max(0f, appliedDamage) * damageReflectRate
                : 0f;
        }

        public void GrantShield(float amount, float duration)
        {
            if (amount <= 0f || duration <= 0f)
            {
                return;
            }
            shieldAmount = Mathf.Max(shieldAmount, amount);
            shieldRemaining = Mathf.Max(shieldRemaining, duration);
        }

        private float ResolveOutgoingDamageMultiplier(UnitActor target, float random01)
        {
            var multiplier = IsPassiveActive ? outgoingRandomEffect.ResolveMagnitude(random01) : 1f;
            if (genericSkill == null || target == null)
            {
                return multiplier;
            }

            switch (genericSkill.RuntimeKind)
            {
                case GenericMonsterPassiveRuntimeKind.RhythmPower:
                    if ((basicHitCount + 1) % genericSkill.TriggerCount == 0)
                    {
                        multiplier *= 1f + genericSkill.ResolvePrimary(monsterLevel);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.LowHealthHunter:
                    if (target.Health != null && target.Health.MaxHealth > 0f &&
                        target.Health.CurrentHealth / target.Health.MaxHealth <= genericSkill.Threshold)
                    {
                        multiplier *= 1f + genericSkill.ResolvePrimary(monsterLevel);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.LongRangeAim:
                    var offset = target.transform.position - owner.transform.position;
                    offset.y = 0f;
                    if (offset.magnitude >= genericSkill.Threshold)
                    {
                        multiplier *= 1f + genericSkill.ResolvePrimary(monsterLevel);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.ThreatMark:
                    if (target.IsRanged || target.IsBoss)
                    {
                        multiplier *= 1f + genericSkill.ResolvePrimary(monsterLevel);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.ImpactStrike:
                    if ((basicHitCount + 1) % genericSkill.TriggerCount == 0 && target.IsBoss)
                    {
                        multiplier *= 1f + genericSkill.ResolvePrimary(monsterLevel);
                    }
                    break;
            }
            return multiplier;
        }

        private void TickPassiveDurations(float deltaTime)
        {
            outgoingRandomRemaining = Mathf.Max(0f, outgoingRandomRemaining - deltaTime);
            if (outgoingRandomRemaining <= 0f)
            {
                outgoingRandomEffect = null;
            }
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaTime);
            exposureRemaining = Mathf.Max(0f, exposureRemaining - deltaTime);
            if (exposureRemaining <= 0f)
            {
                exposureRate = 0f;
            }
            damageReductionRemaining = Mathf.Max(0f, damageReductionRemaining - deltaTime);
            if (damageReductionRemaining <= 0f)
            {
                damageReductionRate = 0f;
            }
            damageReflectRemaining = Mathf.Max(0f, damageReflectRemaining - deltaTime);
            if (damageReflectRemaining <= 0f)
            {
                damageReflectRate = 0f;
            }
            shieldRemaining = Mathf.Max(0f, shieldRemaining - deltaTime);
            if (shieldRemaining <= 0f)
            {
                shieldAmount = 0f;
            }
        }

        private void TickGenericPassive(float deltaTime)
        {
            if (genericSkill == null || owner == null || world == null || !owner.IsAlive)
            {
                return;
            }
            periodicRemaining -= deltaTime;
            if (periodicRemaining > 0f)
            {
                return;
            }
            periodicRemaining = 0.35f;
            switch (genericSkill.RuntimeKind)
            {
                case GenericMonsterPassiveRuntimeKind.CrisisDefense:
                    if (cooldownRemaining <= 0f && owner.Health.CurrentHealth / owner.Health.MaxHealth <= genericSkill.Threshold)
                    {
                        ApplyDamageReduction(genericSkill.ResolvePrimary(monsterLevel), genericSkill.Duration);
                        cooldownRemaining = genericSkill.Cooldown;
                        QueueStatus(owner, "피해 감소!", CombatStatusTextStyle.DamageReduction);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.FrontlineBond:
                    world.CollectUnits(owner.Team, owner.transform.position, genericSkill.Radius, 16, unitBuffer);
                    var nearbyAllies = 0;
                    for (var index = 0; index < unitBuffer.Count; index++)
                    {
                        if (unitBuffer[index] != owner)
                        {
                            nearbyAllies++;
                        }
                    }
                    var bonded = nearbyAllies >= 2;
                    if (bonded)
                    {
                        ApplyDamageReduction(genericSkill.ResolvePrimary(monsterLevel), 0.55f);
                        if (!frontlineBondActive)
                        {
                            QueueStatus(owner, "피해 감소!", CombatStatusTextStyle.DamageReduction);
                        }
                    }
                    frontlineBondActive = bonded;
                    break;
                case GenericMonsterPassiveRuntimeKind.CourageAura:
                    ApplyCourageAura();
                    break;
            }
        }

        private void ApplyEntryPassive(UnitEntryReason entryReason)
        {
            if (genericSkill == null || owner == null)
            {
                return;
            }
            if (genericSkill.RuntimeKind == GenericMonsterPassiveRuntimeKind.FirstWave)
            {
                owner.ApplyMonsterBuff(
                    FirstWaveId,
                    new MonsterStatModifier(
                        0f, genericSkill.ResolvePrimary(monsterLevel), 0f, 0f, 0f, 0f),
                    genericSkill.Duration,
                    MonsterBuffStackPolicy.RefreshDuration);
                QueueStatus(owner, "공격력 상승!", CombatStatusTextStyle.AttackUp);
                return;
            }
            if (entryReason == UnitEntryReason.InitialDeployment)
            {
                return;
            }
            if (genericSkill.RuntimeKind == GenericMonsterPassiveRuntimeKind.EmergencyEntry)
            {
                GrantShield(owner.Health.MaxHealth * genericSkill.ResolvePrimary(monsterLevel), genericSkill.Duration);
                QueueStatus(owner, "보호막!", CombatStatusTextStyle.Shield);
                if (entryReason == UnitEntryReason.ReserveReplacement && world != null)
                {
                    var ally = FindLowestHealthAlly(false);
                    if (ally != null)
                    {
                        ally.SkillRuntime.GrantShield(
                            ally.Health.MaxHealth * genericSkill.ResolveSecondary(monsterLevel),
                            genericSkill.Duration);
                        QueueStatus(ally, "보호막!", CombatStatusTextStyle.Shield);
                    }
                }
            }
        }

        private void ApplySameTargetHaste()
        {
            var stacks = Mathf.Clamp(continuousHits, 1, genericSkill.MaxStacks);
            owner.ApplyMonsterBuff(
                PassiveHasteId,
                new MonsterStatModifier(
                    0f, 0f, 0f,
                    genericSkill.ResolvePrimary(monsterLevel) * stacks,
                    0f, 0f),
                genericSkill.Duration,
                MonsterBuffStackPolicy.RefreshDuration);
            if (continuousHits <= genericSkill.MaxStacks)
            {
                QueueStatus(owner, "가속!", CombatStatusTextStyle.Haste);
            }
        }

        private void HealLowestAlly()
        {
            var ally = FindLowestHealthAlly(true);
            if (ally != null)
            {
                var before = ally.Health.CurrentHealth;
                ally.Health.Heal(owner.EffectiveStats.damage * genericSkill.ResolvePrimary(monsterLevel));
                QueueHeal(ally, ally.Health.CurrentHealth - before);
            }
        }

        private UnitActor FindLowestHealthAlly(bool includeOwner)
        {
            world.CollectUnits(owner.Team, owner.transform.position, float.PositiveInfinity, 256, unitBuffer);
            UnitActor selected = null;
            var lowestRatio = float.PositiveInfinity;
            for (var index = 0; index < unitBuffer.Count; index++)
            {
                var candidate = unitBuffer[index];
                if (candidate == null || !candidate.IsAlive || !includeOwner && candidate == owner)
                {
                    continue;
                }
                var ratio = candidate.Health.CurrentHealth / candidate.Health.MaxHealth;
                if (ratio < lowestRatio)
                {
                    selected = candidate;
                    lowestRatio = ratio;
                }
            }
            return selected;
        }

        private void ApplyCourageAura()
        {
            world.CollectUnits(owner.Team, owner.transform.position, float.PositiveInfinity, 256, unitBuffer);
            for (var index = 0; index < unitBuffer.Count; index++)
            {
                var candidate = unitBuffer[index];
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }
                candidate.ApplyMonsterBuff(
                    CourageAuraId,
                    new MonsterStatModifier(
                        0f, genericSkill.ResolvePrimary(monsterLevel), 0f, 0f, 0f, 0f),
                    0.55f,
                    MonsterBuffStackPolicy.ReplaceIfStronger);
                if (couragePresentedRecipients.Add(candidate.GetInstanceID()))
                {
                    QueueStatus(candidate, "공격력 상승!", CombatStatusTextStyle.AttackUp);
                }
            }
        }

        private void QueueStatus(UnitActor target, string text, CombatStatusTextStyle style)
        {
            if (target != null)
            {
                world?.Feedback?.PlayStatusText(
                    target.transform.position,
                    text,
                    style,
                    target.GetInstanceID());
            }
        }

        private void QueueHeal(UnitActor target, float amount)
        {
            if (target != null && amount > 0f)
            {
                world?.Feedback?.PlayFloatingNumber(
                    target.transform.position,
                    amount,
                    FloatingNumberStyle.Heal,
                    target.GetInstanceID());
            }
        }

        private void UpdateContinuousTarget(UnitActor hitTarget)
        {
            if (hitTarget != null && hitTarget == continuousTarget)
            {
                continuousHits++;
            }
            else
            {
                continuousTarget = hitTarget;
                continuousHits = hitTarget == null ? 0 : 1;
            }
        }

        private bool IsNthHit()
        {
            return basicHitCount > 0 && basicHitCount % genericSkill.TriggerCount == 0;
        }

        public void GrantActiveEnergy(float amount)
        {
            AddEnergy(amount); // 다음 Tick에서만 발동을 판정해 같은 프레임 연쇄 발동을 막습니다.
        }

        public void DrainActiveEnergy(float amount)
        {
            if (activeSkill != null && amount > 0f)
            {
                energy = Mathf.Max(0f, energy - amount);
            }
        }

        private void AddEnergy(float amount)
        {
            if (activeSkill != null && amount > 0f)
            {
                energy = Mathf.Min(activeSkill.EnergyCost, energy + amount);
            }
        }

        private void TryActivatePassive(MonsterSkillTriggerType trigger)
        {
            var recipe = passiveSkill?.Recipe;
            if (recipe == null || recipe.Trigger != trigger)
            {
                return;
            }
            var effects = recipe.Effects;
            for (var index = 0; index < effects.Count; index++)
            {
                var effect = effects[index];
                if (effect?.Type != MonsterSkillEffectType.OutgoingDamageRandomization)
                {
                    continue;
                }
                outgoingRandomEffect = effect;
                outgoingRandomRemaining = Mathf.Max(0.01f, effect.Duration);
                return;
            }
        }

        private void TryQueueActive()
        {
            var recipe = activeSkill?.Recipe;
            if (recipe == null || recipe.Trigger != MonsterSkillTriggerType.EnergyMax)
            {
                return;
            }
            var target = activeSkill is MonsterEffectActiveSkill effectActive
                ? ResolveEffectActiveTarget(effectActive)
                : ResolveActiveTarget(recipe.Target);
            if (target == null || !target.IsAlive || !target.IsCombatReady)
            {
                return;
            }

            var commitDelay = 0.24f;
            if (activeSkill is MonsterAttackActiveSkill assembledAttack)
            {
                var motionDuration = 0f;
                var rawCommitDelay = 0f;
                if (assembledAttack.Steps.Count > 0 && owner.AnimationDriver != null)
                {
                    owner.AnimationDriver.TryResolveActiveStepTiming(
                        assembledAttack.Steps[0].StepId,
                        assembledAttack.CommitNormalizedTime,
                        out motionDuration,
                        out rawCommitDelay,
                        assembledAttack.Steps[0].PlaybackSpeed);
                }
                commitDelay = motionDuration > 0f
                    ? Mathf.Clamp(rawCommitDelay, 0.08f, 1.2f)
                    : 0.24f;
            }
            else if (activeSkill is MonsterEffectActiveSkill assembledEffect)
            {
                var motionDuration = 0f;
                var rawCommitDelay = 0f;
                if (assembledEffect.Groups.Count > 0 && owner.AnimationDriver != null)
                {
                    owner.AnimationDriver.TryResolveActiveStepTiming(
                        assembledEffect.Groups[0].GroupId,
                        assembledEffect.CommitNormalizedTime,
                        out motionDuration,
                        out rawCommitDelay);
                }
                commitDelay = motionDuration > 0f
                    ? Mathf.Clamp(rawCommitDelay, 0.08f, 1.2f)
                    : 0.24f;
            }
            else
            {
                activeDamageEffect = null;
                var effects = recipe.Effects;
                for (var index = 0; index < effects.Count; index++)
                {
                    if (effects[index]?.Type == MonsterSkillEffectType.Damage)
                    {
                        activeDamageEffect = effects[index];
                        break;
                    }
                }
                if (activeDamageEffect == null)
                {
                    return;
                }
            }

            activeTarget = target;
            activeFocusQueued = true;
            activeFirstStepMotionStarted = false;
            activeCommitMarkerBaseline = 0;
            var config = MonsterActiveFocusPresentationConfig.Current;
            var preset = config != null
                ? config.ResolvePreset(owner.Presentation.Rarity)
                : default;
            var focusDuration = commitDelay + Mathf.Max(0.08f, preset.FadeOut);
            var isAttackActive = activeSkill is MonsterAttackActiveSkill;
            activeAttackExpectedDuration = isAttackActive
                ? Mathf.Max(
                    0.1f,
                    commitDelay +
                    (((MonsterAttackActiveSkill)activeSkill).SourceProfile?.EstimateDuration() ?? 0f))
                : 0f;
            if (world.RequestMonsterActiveFocus(
                    owner,
                    activeSkill,
                    ResolveQueuedActiveTarget,
                    CanArmQueuedActiveFocus,
                    BeginQueuedActiveFocus,
                    CommitQueuedActiveFocus,
                    CancelQueuedActiveFocus,
                    IsActiveCommitMarkerReached,
                    commitDelay,
                    focusDuration,
                    isAttackActive ? IsActiveExecutionComplete : null,
                    isAttackActive ? ResolveActiveAttackExecutionProgress : null))
            {
                return;
            }

            CancelQueuedActiveFocus();
        }

        private UnitActor ResolveQueuedActiveTarget()
        {
            if (activeTarget != null && activeTarget.IsAlive && activeTarget.IsCombatReady)
            {
                return activeTarget;
            }
            var recipe = activeSkill?.Recipe;
            activeTarget = activeSkill is MonsterEffectActiveSkill effectActive
                ? ResolveEffectActiveTarget(effectActive)
                : recipe != null
                    ? ResolveActiveTarget(recipe.Target)
                    : null;
            return activeTarget;
        }

        private bool CanArmQueuedActiveFocus()
        {
            return activeFocusQueued &&
                   !executingActive &&
                   owner != null &&
                   owner.CanArmMonsterActiveFocus &&
                   canArmActiveFocus &&
                   ResolveQueuedActiveTarget() != null;
        }

        private void BeginQueuedActiveFocus()
        {
            if (!activeFocusQueued || owner == null || !owner.IsAlive)
            {
                return;
            }
            executingActive = true;
            waitingForActiveFocus = true;
            activeAttackExecutionElapsed = 0f;
            activeFirstStepMotionStarted = false;
            activeCommitMarkerBaseline = owner.AnimationDriver != null
                ? owner.AnimationDriver.ActiveSkillCommitVersion
                : 0;
            if (activeSkill is MonsterAttackActiveSkill)
            {
                BeginAssembledAttackFocusMotion();
            }
            else if (activeSkill is MonsterEffectActiveSkill)
            {
                BeginAssembledEffectFocusMotion();
            }
            world.PlayMonsterSfx(
                owner.RuntimeAssetSet?.FeedbackProfile?.ActiveSkillVoice,
                owner.transform.position);
        }

        private bool CommitQueuedActiveFocus()
        {
            if (!activeFocusQueued || !executingActive || !waitingForActiveFocus ||
                ResolveQueuedActiveTarget() == null)
            {
                CancelQueuedActiveFocus();
                return false;
            }
            if (activeSkill is MonsterAttackActiveSkill)
            {
                return CommitAssembledAttack();
            }
            if (activeSkill is MonsterEffectActiveSkill)
            {
                return CommitAssembledEffect();
            }
            return CommitLegacyActive();
        }

        private bool IsActiveCommitMarkerReached()
        {
            return executingActive &&
                   waitingForActiveFocus &&
                   owner?.AnimationDriver != null &&
                   owner.AnimationDriver.ActiveSkillCommitVersion != activeCommitMarkerBaseline;
        }

        private bool IsActiveExecutionComplete()
        {
            return !executingActive && !waitingForActiveFocus;
        }

        private float ResolveActiveAttackExecutionProgress()
        {
            if (activeSkill is not MonsterAttackActiveSkill)
            {
                return 1f;
            }
            if (!executingActive && !waitingForActiveFocus)
            {
                return 1f;
            }
            if (activeAttackExpectedDuration <= 0f)
            {
                return 0f;
            }
            return Mathf.Min(
                0.999f,
                activeAttackExecutionElapsed / activeAttackExpectedDuration);
        }

        private UnitActor ResolveEffectActiveTarget(MonsterEffectActiveSkill effectActive)
        {
            if (effectActive?.SourceProfile?.Role != MonsterEffectActiveRole.Debuff)
            {
                return owner;
            }
            return owner.Target != null && owner.Target.IsAlive
                ? owner.Target
                : world.FindNearestOpponent(owner, float.PositiveInfinity);
        }
        private UnitActor ResolveActiveTarget(MonsterSkillTargetType targetType)
        {
            switch (targetType)
            {
                case MonsterSkillTargetType.CurrentTarget:
                    return owner.Target != null && owner.Target.IsAlive
                        ? owner.Target
                        : world.FindNearestOpponent(owner, float.PositiveInfinity);
                case MonsterSkillTargetType.LowestHealthEnemy:
                    return world.FindOpponent(owner, float.PositiveInfinity, UnitTargetPriority.LowestHealth);
                case MonsterSkillTargetType.RangedEnemyFirst:
                    return world.FindOpponent(owner, float.PositiveInfinity, UnitTargetPriority.RangedFirst);
                default:
                    return world.FindNearestOpponent(owner, float.PositiveInfinity);
            }
        }

        private void TickExecutingActive(float deltaTime)
        {
            if (!executingActive)
            {
                return;
            }
            if (activeSkill is MonsterAttackActiveSkill)
            {
                if (waitingForActiveFocus) return;
                if (activeAttackExecutor.Tick(deltaTime)) CompleteActive();
                return;
            }
            if (activeSkill is MonsterEffectActiveSkill)
            {
                if (waitingForActiveFocus) return;
                if (activeEffectExecutor.Tick(deltaTime)) CompleteActive();
                return;
            }
            nextActiveHitDelay -= Mathf.Max(0f, deltaTime);
            var safety = 0;
            while (executingActive && nextActiveHitDelay <= 0f && safety++ < 64)
            {
                if (activeTarget == null || !activeTarget.IsAlive || !activeTarget.IsCombatReady)
                {
                    CompleteActive();
                    return;
                }
                var amount = ResolveEffectAmount(activeDamageEffect, activeTarget, Random.value);
                world.ApplyMonsterSkillDamage(owner, activeTarget.Health, amount);
                remainingActiveHits--;
                if (remainingActiveHits <= 0)
                {
                    CompleteActive();
                    return;
                }
                nextActiveHitDelay += Mathf.Max(0.01f, activeDamageEffect.RepeatInterval);
            }
        }

        private float ResolveEffectAmount(MonsterSkillEffect effect, UnitActor target, float random01)
        {
            var magnitude = effect.ResolveMagnitude(random01);
            switch (effect.ValueSource)
            {
                case MonsterSkillValueSource.Flat:
                    return magnitude;
                case MonsterSkillValueSource.MaxHealthRatio:
                    return owner.Health.MaxHealth * magnitude;
                case MonsterSkillValueSource.TargetMaxHealthRatio:
                    return target.Health.MaxHealth * magnitude;
                case MonsterSkillValueSource.TargetMissingHealthRatio:
                    return Mathf.Max(0f, target.Health.MaxHealth - target.Health.CurrentHealth) * magnitude;
                case MonsterSkillValueSource.ReceivedDamageRatio:
                    return lastReceivedDamage * magnitude;
                default:
                    return owner.EffectiveStats.damage * magnitude;
            }
        }

        private void CompleteActive()
        {
            executingActive = false;
            waitingForActiveFocus = false;
            activeFocusQueued = false;
            activeFirstStepMotionStarted = false;
            activeCommitMarkerBaseline = 0;
            activeAttackExecutionElapsed = 0f;
            activeAttackExpectedDuration = 0f;
            activeAttackExecutor.Reset();
            activeDamageEffect = null;
            activeTarget = null;
            remainingActiveHits = 0;
            nextActiveHitDelay = 0f;
            world?.NotifyMonsterActiveExecutionComplete(owner);
        }

        private bool CommitAssembledAttack()
        {
            if (!executingActive || !(activeSkill is MonsterAttackActiveSkill assembledAttack) ||
                owner == null || world == null || !owner.IsAlive)
            {
                CompleteActive();
                return false;
            }
            if (!TryConsumeActiveEnergy())
            {
                CompleteActive();
                return false;
            }
            activeFocusQueued = false;
            waitingForActiveFocus = false;
            if (!activeAttackExecutor.Begin(
                    owner,
                    world,
                    assembledAttack,
                    activeTarget,
                    activeFirstStepMotionStarted))
            {
                RefundActiveEnergy();
                CompleteActive();
                return false;
            }
            TickExecutingActive(0f);
            return true;
        }

        private void BeginAssembledAttackFocusMotion()
        {
            if (!executingActive || !(activeSkill is MonsterAttackActiveSkill assembledAttack) ||
                assembledAttack.Steps.Count == 0 || owner?.AnimationDriver == null || !owner.IsAlive)
            {
                activeFirstStepMotionStarted = false;
                return;
            }
            var duration = owner.AnimationDriver.PlayActiveStep(
                assembledAttack.Steps[0].StepId,
                assembledAttack.CommitNormalizedTime,
                out _,
                assembledAttack.Steps[0].PlaybackSpeed);
            activeFirstStepMotionStarted = duration > 0f;
        }
        private bool CommitAssembledEffect()
        {
            if (!executingActive || !(activeSkill is MonsterEffectActiveSkill assembledEffect) ||
                owner == null || world == null || !owner.IsAlive)
            {
                CompleteActive();
                return false;
            }
            if (!TryConsumeActiveEnergy())
            {
                CompleteActive();
                return false;
            }
            activeFocusQueued = false;
            waitingForActiveFocus = false;
            if (!activeEffectExecutor.Begin(owner, world, assembledEffect, activeTarget))
            {
                RefundActiveEnergy();
                CompleteActive();
                return false;
            }
            TickExecutingActive(0f);
            if (!activeEffectExecutor.IsRunning)
            {
                CompleteActive(); // 지연 없는 효과형은 Commit 프레임에 실행 상태를 확실히 닫습니다.
            }
            return true;
        }

        private void BeginAssembledEffectFocusMotion()
        {
            if (!executingActive || !(activeSkill is MonsterEffectActiveSkill assembledEffect) ||
                assembledEffect.Groups.Count == 0 || owner?.AnimationDriver == null || !owner.IsAlive)
            {
                activeFirstStepMotionStarted = false;
                return;
            }
            var duration = owner.AnimationDriver.PlayActiveStep(
                assembledEffect.Groups[0].GroupId,
                assembledEffect.CommitNormalizedTime,
                out _);
            activeFirstStepMotionStarted = duration > 0f;
        }

        private bool CommitLegacyActive()
        {
            if (activeDamageEffect == null || owner == null || world == null ||
                activeTarget == null || !activeTarget.IsAlive || !TryConsumeActiveEnergy())
            {
                CompleteActive();
                return false;
            }

            activeFocusQueued = false;
            waitingForActiveFocus = false;
            remainingActiveHits = activeDamageEffect.RepeatCount;
            nextActiveHitDelay = activeDamageEffect.Delay;
            TickExecutingActive(0f);
            return true;
        }

        private bool TryConsumeActiveEnergy()
        {
            if (activeSkill == null || energy + 0.001f < activeSkill.EnergyCost)
            {
                return false;
            }
            energy = Mathf.Max(0f, energy - activeSkill.EnergyCost);
            return true;
        }

        private void RefundActiveEnergy()
        {
            if (activeSkill != null)
            {
                energy = Mathf.Min(activeSkill.EnergyCost, energy + activeSkill.EnergyCost);
            }
        }

        private void CancelQueuedActiveFocus()
        {
            var wasArmed = executingActive && waitingForActiveFocus;
            executingActive = false;
            waitingForActiveFocus = false;
            activeFocusQueued = false;
            activeFirstStepMotionStarted = false;
            activeCommitMarkerBaseline = 0;
            activeAttackExecutionElapsed = 0f;
            activeAttackExpectedDuration = 0f;
            activeAttackExecutor.Reset();
            activeDamageEffect = null;
            activeTarget = null;
            remainingActiveHits = 0;
            nextActiveHitDelay = 0f;
            if (wasArmed && owner != null && owner.IsAlive)
            {
                owner.AnimationDriver?.PlayIdle(true);
            }
        }
    }
}
