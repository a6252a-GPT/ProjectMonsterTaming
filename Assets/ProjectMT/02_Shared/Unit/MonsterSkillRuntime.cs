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
        private float shieldAmount;
        private float shieldRemaining;
        private int remainingActiveHits;
        private int basicHitCount;
        private int continuousHits;
        private int monsterLevel = 1;
        private bool executingActive;

        public MonsterPassiveSkill PassiveSkill => passiveSkill;
        public MonsterActiveSkill ActiveSkill => activeSkill;
        public float Energy => energy;
        public float EnergyCapacity => activeSkill == null ? 0f : activeSkill.EnergyCost;
        public bool IsPassiveActive => outgoingRandomEffect != null && outgoingRandomRemaining > 0f;
        public bool IsExecuting => executingActive;
        public int RemainingActiveHits => remainingActiveHits;
        public float ShieldAmount => shieldRemaining > 0f ? shieldAmount : 0f;

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
            shieldAmount = 0f;
            shieldRemaining = 0f;
            remainingActiveHits = 0;
            basicHitCount = 0;
            continuousHits = 0;
            monsterLevel = 1;
            executingActive = false;
            unitBuffer.Clear();
        }

        public void Tick(float deltaTime, bool canBeginActive)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            TickPassiveDurations(deltaTime);
            TickGenericPassive(deltaTime);

            if (owner == null || world == null || activeSkill == null || !owner.IsAlive)
            {
                return;
            }

            if (executingActive)
            {
                TickExecutingActive(deltaTime);
                return;
            }

            energy = Mathf.Min(activeSkill.EnergyCost, energy + activeSkill.EnergyPerSecond * deltaTime);
            if (canBeginActive && energy >= activeSkill.EnergyCost)
            {
                TryBeginActive();
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

            AddEnergy(activeSkill?.EnergyPerBasicAttackHit ?? 0f);
            TryActivatePassive(MonsterSkillTriggerType.BasicAttackHit);
            basicHitCount++;
            UpdateContinuousTarget(hitTarget);
            if (genericSkill == null || owner == null || world == null || hitTarget == null)
            {
                return;
            }

            switch (genericSkill.RuntimeKind)
            {
                case GenericMonsterPassiveRuntimeKind.SameTargetHaste:
                    ApplySameTargetHaste();
                    break;
                case GenericMonsterPassiveRuntimeKind.RallySplash:
                    if (IsNthHit())
                    {
                        ApplySplash(hitTarget);
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
            AddEnergy(activeSkill?.EnergyPerDamageReceived ?? 0f);
            TryActivatePassive(MonsterSkillTriggerType.Damaged);
        }

        public void NotifyTargetDestroyed()
        {
            if (genericSkill == null || owner == null || cooldownRemaining > 0f ||
                genericSkill.RuntimeKind != GenericMonsterPassiveRuntimeKind.KillHeal)
            {
                return;
            }

            cooldownRemaining = genericSkill.Cooldown;
            owner.Health.Heal(owner.Health.MaxHealth * genericSkill.ResolvePrimary(monsterLevel));
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
                    if (nearbyAllies >= 2)
                    {
                        ApplyDamageReduction(genericSkill.ResolvePrimary(monsterLevel), 0.55f);
                    }
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
                return;
            }
            if (entryReason == UnitEntryReason.InitialDeployment)
            {
                return;
            }
            if (genericSkill.RuntimeKind == GenericMonsterPassiveRuntimeKind.EmergencyEntry)
            {
                GrantShield(owner.Health.MaxHealth * genericSkill.ResolvePrimary(monsterLevel), genericSkill.Duration);
                if (entryReason == UnitEntryReason.ReserveReplacement && world != null)
                {
                    var ally = FindLowestHealthAlly(false);
                    ally?.SkillRuntime.GrantShield(
                        ally.Health.MaxHealth * genericSkill.ResolveSecondary(monsterLevel),
                        genericSkill.Duration);
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
        }

        private void ApplySplash(UnitActor primaryTarget)
        {
            world.CollectUnits(primaryTarget.Team, primaryTarget.transform.position, genericSkill.Radius,
                genericSkill.MaxTargets + 1, unitBuffer);
            var applied = 0;
            var amount = owner.EffectiveStats.damage * genericSkill.ResolvePrimary(monsterLevel);
            for (var index = 0; index < unitBuffer.Count && applied < genericSkill.MaxTargets; index++)
            {
                var target = unitBuffer[index];
                if (target == null || target == primaryTarget)
                {
                    continue;
                }
                if (world.ApplyMonsterSkillDamage(owner, target.Health, amount))
                {
                    applied++;
                }
            }
        }

        private void HealLowestAlly()
        {
            var ally = FindLowestHealthAlly(true);
            if (ally != null)
            {
                ally.Health.Heal(owner.EffectiveStats.damage * genericSkill.ResolvePrimary(monsterLevel));
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

        private void TryBeginActive()
        {
            var recipe = activeSkill?.Recipe;
            if (recipe == null || recipe.Trigger != MonsterSkillTriggerType.EnergyMax)
            {
                return;
            }
            var target = ResolveActiveTarget(recipe.Target);
            if (target == null || !target.IsAlive || !target.IsCombatReady)
            {
                return;
            }
            MonsterSkillEffect damageEffect = null;
            var effects = recipe.Effects;
            for (var index = 0; index < effects.Count; index++)
            {
                if (effects[index]?.Type == MonsterSkillEffectType.Damage)
                {
                    damageEffect = effects[index];
                    break;
                }
            }
            if (damageEffect == null)
            {
                return;
            }
            activeTarget = target;
            activeDamageEffect = damageEffect;
            remainingActiveHits = damageEffect.RepeatCount;
            nextActiveHitDelay = damageEffect.Delay;
            energy = Mathf.Max(0f, energy - activeSkill.EnergyCost);
            executingActive = true;
            TickExecutingActive(0f);
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
            activeDamageEffect = null;
            activeTarget = null;
            remainingActiveHits = 0;
            nextActiveHitDelay = 0f;
        }
    }
}
