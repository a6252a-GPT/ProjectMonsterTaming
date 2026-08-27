using System.Collections.Generic;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    public sealed class HexMonsterPassiveRuntime // Hex 공격 유닛이 공용 패시브 수치를 해석하는 어댑터
    {
        private const float PeriodicInterval = 0.35f;

        private HexCastleAssaultUnit owner;
        private HexCastleAssaultWorld world;
        private GenericMonsterPassiveSkill skill;
        private int monsterLevel = 1;
        private int basicHitCount;
        private int continuousTargetId;
        private int continuousHits;
        private float periodicRemaining;
        private float cooldownRemaining;
        private float attackBuffRate;
        private float attackBuffRemaining;
        private float attackSpeedRate;
        private float attackSpeedRemaining;
        private float damageReductionRate;
        private float damageReductionRemaining;
        private float shieldAmount;
        private float shieldRemaining;
        private bool frontlineBondActive;
        private readonly HashSet<int> couragePresentedRecipients = new HashSet<int>();

        public float AttackDamageMultiplier => attackBuffRemaining > 0f ? 1f + attackBuffRate : 1f;
        public float AttackSpeedRate => attackSpeedRemaining > 0f ? attackSpeedRate : 0f;
        public float ShieldAmount => shieldRemaining > 0f ? shieldAmount : 0f;

        public void Initialize(
            HexCastleAssaultUnit unit,
            HexCastleAssaultWorld assaultWorld,
            MonsterPassiveSkill passive,
            int level,
            UnitEntryReason entryReason)
        {
            Shutdown();
            owner = unit;
            world = assaultWorld;
            monsterLevel = Mathf.Max(1, level);
            skill = passive as GenericMonsterPassiveSkill;
            if (skill != null && !skill.AuthoringEnabled)
            {
                skill = null;
            }
            ApplyEntryPassive(entryReason);
        }

        public void Shutdown()
        {
            owner = null;
            world = null;
            skill = null;
            monsterLevel = 1;
            basicHitCount = 0;
            continuousTargetId = 0;
            continuousHits = 0;
            periodicRemaining = 0f;
            cooldownRemaining = 0f;
            attackBuffRate = 0f;
            attackBuffRemaining = 0f;
            attackSpeedRate = 0f;
            attackSpeedRemaining = 0f;
            damageReductionRate = 0f;
            damageReductionRemaining = 0f;
            shieldAmount = 0f;
            shieldRemaining = 0f;
            frontlineBondActive = false;
            couragePresentedRecipients.Clear();
        }

        public void Tick(float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaTime);
            attackBuffRemaining = Mathf.Max(0f, attackBuffRemaining - deltaTime);
            attackSpeedRemaining = Mathf.Max(0f, attackSpeedRemaining - deltaTime);
            damageReductionRemaining = Mathf.Max(0f, damageReductionRemaining - deltaTime);
            shieldRemaining = Mathf.Max(0f, shieldRemaining - deltaTime);
            if (attackBuffRemaining <= 0f)
            {
                attackBuffRate = 0f;
            }
            if (attackSpeedRemaining <= 0f)
            {
                attackSpeedRate = 0f;
            }
            if (damageReductionRemaining <= 0f)
            {
                damageReductionRate = 0f;
            }
            if (shieldRemaining <= 0f)
            {
                shieldAmount = 0f;
            }
            if (skill == null || owner == null || world == null || !owner.IsAlive)
            {
                return;
            }

            periodicRemaining -= deltaTime;
            if (periodicRemaining > 0f)
            {
                return;
            }
            periodicRemaining = PeriodicInterval;
            switch (skill.RuntimeKind)
            {
                case GenericMonsterPassiveRuntimeKind.CrisisDefense:
                    if (cooldownRemaining <= 0f && owner.HealthRatio <= skill.Threshold)
                    {
                        ApplyDamageReduction(skill.ResolvePrimary(monsterLevel), skill.Duration);
                        cooldownRemaining = skill.Cooldown;
                        QueueStatus(owner, "피해 감소!", CombatStatusTextStyle.DamageReduction);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.FrontlineBond:
                    var bonded = CountNearbyAllies(skill.Radius) >= 2;
                    if (bonded)
                    {
                        ApplyDamageReduction(skill.ResolvePrimary(monsterLevel), 0.55f);
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

        public float ResolveAttackInterval(float baseInterval)
        {
            return Mathf.Max(0.05f, baseInterval / Mathf.Max(0.01f, 1f + AttackSpeedRate));
        }

        public float ResolveOutgoingDamage(float amount, HexCastleAssaultTarget target)
        {
            var resolved = Mathf.Max(0f, amount) * AttackDamageMultiplier;
            if (skill == null || owner == null || !target.IsAlive)
            {
                return resolved;
            }
            switch (skill.RuntimeKind)
            {
                case GenericMonsterPassiveRuntimeKind.RhythmPower:
                    if ((basicHitCount + 1) % skill.TriggerCount == 0)
                    {
                        resolved *= 1f + skill.ResolvePrimary(monsterLevel);
                        world.MarkPassiveEnhancedDamage(target);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.LowHealthHunter:
                    if (target.MaxHealth > 0f && target.CurrentHealth / target.MaxHealth <= skill.Threshold)
                    {
                        resolved *= 1f + skill.ResolvePrimary(monsterLevel);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.LongRangeAim:
                    if (PlanarDistance(owner.transform.position, target.Position) >= skill.Threshold)
                    {
                        resolved *= 1f + skill.ResolvePrimary(monsterLevel);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.ThreatMark:
                    if (target.Kind == HexCastleAssaultTargetKind.Defender ||
                        target.Structure != null && target.Structure.TurretWeaponKind != HexCastleTurretWeaponKind.None)
                    {
                        resolved *= 1f + skill.ResolvePrimary(monsterLevel);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.ImpactStrike:
                    if ((basicHitCount + 1) % skill.TriggerCount == 0 &&
                        target.Kind != HexCastleAssaultTargetKind.Defender)
                    {
                        resolved *= 1f + skill.ResolvePrimary(monsterLevel);
                    }
                    break;
            }
            return resolved;
        }

        public float ResolveIncomingDamage(float amount, out float absorbedByShield)
        {
            var resolved = Mathf.Max(0f, amount);
            if (damageReductionRemaining > 0f)
            {
                resolved *= Mathf.Clamp01(1f - damageReductionRate);
            }
            absorbedByShield = 0f;
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

        public void NotifyBasicAttackHit(HexCastleAssaultTarget target, bool destroyed)
        {
            if (skill == null || owner == null || world == null || target.InstanceId == 0)
            {
                return;
            }
            basicHitCount++;
            if (continuousTargetId == target.InstanceId)
            {
                continuousHits++;
            }
            else
            {
                continuousTargetId = target.InstanceId;
                continuousHits = 1;
            }

            switch (skill.RuntimeKind)
            {
                case GenericMonsterPassiveRuntimeKind.RhythmPower:
                    if (IsNthHit())
                    {
                        QueueStatus(owner, "강화!", CombatStatusTextStyle.Enhanced);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.SameTargetHaste:
                    ApplyAttackSpeedBuff(
                        skill.ResolvePrimary(monsterLevel) * Mathf.Clamp(continuousHits, 1, skill.MaxStacks),
                        skill.Duration);
                    if (continuousHits <= skill.MaxStacks)
                    {
                        QueueStatus(owner, "가속!", CombatStatusTextStyle.Haste);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.ImpactStrike:
                    if (IsNthHit())
                    {
                        target.Defender?.TryApplyPassiveStagger(skill.Duration);
                        QueueStatus(owner, "충격!", CombatStatusTextStyle.Impact);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.FractureMark:
                    if (continuousHits % skill.TriggerCount == 0)
                    {
                        world.ApplyPassiveExposure(target, skill.ResolvePrimary(monsterLevel), skill.Duration);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.ThreatMark:
                    if (target.Kind == HexCastleAssaultTargetKind.Defender ||
                        target.Structure != null && target.Structure.TurretWeaponKind != HexCastleTurretWeaponKind.None)
                    {
                        world.ApplyPassiveExposure(target, skill.ResolveSecondary(monsterLevel), skill.Duration);
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.HealingShot:
                    if (IsNthHit())
                    {
                        var ally = FindLowestHealthAlly();
                        if (ally != null)
                        {
                            QueueHeal(
                                ally,
                                ally.HealPassive(owner.BaseAttackDamage * skill.ResolvePrimary(monsterLevel)));
                        }
                    }
                    break;
                case GenericMonsterPassiveRuntimeKind.KillHeal:
                    if (destroyed && cooldownRemaining <= 0f)
                    {
                        cooldownRemaining = skill.Cooldown;
                        QueueHeal(
                            owner,
                            owner.HealPassive(owner.MaxHealth * skill.ResolvePrimary(monsterLevel)));
                    }
                    break;
            }
        }

        public void ApplyAttackBuff(float rate, float duration)
        {
            if (rate <= 0f || duration <= 0f)
            {
                return;
            }
            attackBuffRate = Mathf.Max(attackBuffRate, rate);
            attackBuffRemaining = Mathf.Max(attackBuffRemaining, duration);
        }

        public void ApplyAttackSpeedBuff(float rate, float duration)
        {
            if (rate <= 0f || duration <= 0f)
            {
                return;
            }
            attackSpeedRate = Mathf.Max(attackSpeedRate, rate);
            attackSpeedRemaining = Mathf.Max(attackSpeedRemaining, duration);
        }

        public void ApplyDamageReduction(float rate, float duration)
        {
            if (rate <= 0f || duration <= 0f)
            {
                return;
            }
            damageReductionRate = Mathf.Max(damageReductionRate, Mathf.Clamp01(rate));
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

        private void ApplyEntryPassive(UnitEntryReason entryReason)
        {
            if (skill == null || owner == null)
            {
                return;
            }
            if (skill.RuntimeKind == GenericMonsterPassiveRuntimeKind.FirstWave)
            {
                ApplyAttackBuff(skill.ResolvePrimary(monsterLevel), skill.Duration);
                QueueStatus(owner, "공격력 상승!", CombatStatusTextStyle.AttackUp);
                return;
            }
            if (entryReason == UnitEntryReason.InitialDeployment)
            {
                return;
            }
            if (skill.RuntimeKind == GenericMonsterPassiveRuntimeKind.EmergencyEntry)
            {
                var scale = entryReason == UnitEntryReason.CastleManualDeployment ? 0.5f : 1f;
                GrantShield(owner.MaxHealth * skill.ResolvePrimary(monsterLevel) * scale, skill.Duration);
                QueueStatus(owner, "보호막!", CombatStatusTextStyle.Shield);
            }
        }

        private int CountNearbyAllies(float radius)
        {
            var count = 0;
            var units = world.RegisteredUnits;
            for (var index = 0; index < units.Count; index++)
            {
                var candidate = units[index];
                if (candidate != null && candidate != owner && candidate.IsAlive &&
                    PlanarDistance(owner.transform.position, candidate.transform.position) <= radius)
                {
                    count++;
                }
            }
            return count;
        }

        private HexCastleAssaultUnit FindLowestHealthAlly()
        {
            HexCastleAssaultUnit selected = null;
            var lowestRatio = float.PositiveInfinity;
            var units = world.RegisteredUnits;
            for (var index = 0; index < units.Count; index++)
            {
                var candidate = units[index];
                if (candidate != null && candidate.IsAlive && candidate.HealthRatio < lowestRatio)
                {
                    selected = candidate;
                    lowestRatio = candidate.HealthRatio;
                }
            }
            return selected;
        }

        private void ApplyCourageAura()
        {
            var units = world.RegisteredUnits;
            for (var index = 0; index < units.Count; index++)
            {
                var candidate = units[index];
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }
                candidate.PassiveRuntime.ApplyAttackBuff(skill.ResolvePrimary(monsterLevel), 0.55f);
                if (couragePresentedRecipients.Add(candidate.GetInstanceID()))
                {
                    QueueStatus(candidate, "공격력 상승!", CombatStatusTextStyle.AttackUp);
                }
            }
        }

        private void QueueStatus(
            HexCastleAssaultUnit unit,
            string text,
            CombatStatusTextStyle style)
        {
            world?.QueuePassiveStatus(unit, text, style);
        }

        private void QueueHeal(HexCastleAssaultUnit unit, float amount)
        {
            world?.QueuePassiveHeal(unit, amount);
        }

        private bool IsNthHit()
        {
            return basicHitCount > 0 && basicHitCount % skill.TriggerCount == 0;
        }

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }
    }
}
