using System;
using ProjectMT.Shared.Combat;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public sealed partial class UnitActor
    {
        // 08.07 안건준 추가 - 콘텐츠 전용 버프(예: 수호자의 탑 4번 건물 파괴 시 아군 공격력 2배)가 공격력을
        // 일시적으로 배율 조정할 때 쓴다. 아무도 호출하지 않으면 항상 1배라 기존 동작에 영향이 없다.
        public void SetDamageMultiplier(float multiplier)
        {
            damageMultiplier = Mathf.Max(0.01f, multiplier);
        }

        public void ApplyActiveBleed(UnitActor source, float attackPowerRatio, float duration, float tickInterval)
        {
            if (!IsAlive || source == null || attackPowerRatio <= 0f || duration <= 0f) return;
            activeBleedSource = source;
            activeBleedDamage = Mathf.Max(activeBleedDamage, source.EffectiveStats.damage * attackPowerRatio);
            activeBleedRemaining = Mathf.Max(activeBleedRemaining, duration);
            activeBleedInterval = Mathf.Max(0.05f, tickInterval);
            if (activeBleedTickRemaining <= 0f) activeBleedTickRemaining = activeBleedInterval;
        }

        public void ApplyActiveBurn(UnitActor source, float attackPowerRatio, float duration, float tickInterval)
        {
            if (!IsAlive || source == null || attackPowerRatio <= 0f || duration <= 0f) return;
            activeBurnSource = source;
            activeBurnDamage = Mathf.Max(activeBurnDamage, source.EffectiveStats.damage * attackPowerRatio);
            activeBurnRemaining = Mathf.Max(activeBurnRemaining, duration);
            activeBurnInterval = Mathf.Max(0.05f, tickInterval);
            if (activeBurnTickRemaining <= 0f) activeBurnTickRemaining = activeBurnInterval;
        }

        public void ApplyActiveSlow(float rate, float duration)
        {
            if (!IsAlive || rate <= 0f || rate >= 1f || duration <= 0f) return;
            activeSlowRate = Mathf.Max(activeSlowRate, rate);
            activeSlowRemaining = Mathf.Max(activeSlowRemaining, duration);
        }

        public bool TryCleanseOneDebuff()
        {
            if (!IsAlive) return false;
            if (activeStunRemaining > 0f)
            {
                activeStunRemaining = 0f;
                return true;
            }
            if (monsterSkillRuntime.TryCleanseExposure()) return true;

            var strongestIndex = -1;
            var strongestValue = 0f;
            for (var index = 0; index < monsterBuffs.Count; index++)
            {
                var value = GetNegativeModifierStrength(monsterBuffs[index].Modifier);
                if (value <= strongestValue) continue;
                strongestIndex = index;
                strongestValue = value;
            }
            if (strongestIndex >= 0)
            {
                monsterBuffs.RemoveAt(strongestIndex);
                RebuildMonsterBuffModifier();
                return true;
            }
            if (activeSlowRemaining > 0f)
            {
                activeSlowRemaining = 0f;
                activeSlowRate = 0f;
                return true;
            }
            if (activeBurnRemaining > 0f)
            {
                activeBurnRemaining = 0f;
                activeBurnTickRemaining = 0f;
                activeBurnDamage = 0f;
                activeBurnSource = null;
                return true;
            }
            if (activeBleedRemaining > 0f)
            {
                activeBleedRemaining = 0f;
                activeBleedTickRemaining = 0f;
                activeBleedDamage = 0f;
                activeBleedSource = null;
                return true;
            }
            return false;
        }

        private bool TickActiveStatusEffects(float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            activeSlowRemaining = Mathf.Max(0f, activeSlowRemaining - deltaTime);
            if (activeSlowRemaining <= 0f) activeSlowRate = 0f;

            if (activeBleedRemaining > 0f)
            {
                activeBleedRemaining = Mathf.Max(0f, activeBleedRemaining - deltaTime);
                activeBleedTickRemaining -= deltaTime;
                var safety = 0;
                while (activeBleedRemaining > 0f && activeBleedTickRemaining <= 0f && safety++ < 16)
                {
                    if (world == null || activeBleedSource == null || !activeBleedSource.IsAlive ||
                        !world.ApplyMonsterSkillDamage(activeBleedSource, health, activeBleedDamage))
                    {
                        activeBleedRemaining = 0f;
                        break;
                    }
                    activeBleedTickRemaining += activeBleedInterval;
                }
                if (activeBleedRemaining <= 0f)
                {
                    activeBleedSource = null;
                    activeBleedDamage = 0f;
                    activeBleedTickRemaining = 0f;
                }
            }

            if (activeBurnRemaining > 0f)
            {
                activeBurnRemaining = Mathf.Max(0f, activeBurnRemaining - deltaTime);
                activeBurnTickRemaining -= deltaTime;
                var safety = 0;
                while (activeBurnRemaining > 0f && activeBurnTickRemaining <= 0f && safety++ < 16)
                {
                    if (world == null || activeBurnSource == null || !activeBurnSource.IsAlive ||
                        !world.ApplyMonsterSkillDamage(activeBurnSource, health, activeBurnDamage))
                    {
                        activeBurnRemaining = 0f;
                        break;
                    }
                    activeBurnTickRemaining += activeBurnInterval;
                }
                if (activeBurnRemaining <= 0f)
                {
                    activeBurnSource = null;
                    activeBurnDamage = 0f;
                    activeBurnTickRemaining = 0f;
                }
            }

            if (!IsAlive) return true;
            if (activeAirborneDuration > 0f)
            {
                activeAirborneElapsed = Mathf.Min(activeAirborneDuration, activeAirborneElapsed + deltaTime);
                var ratio = activeAirborneElapsed / Mathf.Max(0.01f, activeAirborneDuration);
                var position = transform.position;
                position.y = activeAirborneBaseY + Mathf.Sin(ratio * Mathf.PI) * activeAirborneHeight;
                transform.position = position;
                if (ratio >= 1f)
                {
                    position.y = activeAirborneBaseY;
                    transform.position = position;
                    activeAirborneDuration = 0f;
                    activeAirborneElapsed = 0f;
                    activeAirborneHeight = 0f;
                }
                return true;
            }

            if (activeStunRemaining > 0f)
            {
                activeStunRemaining = Mathf.Max(0f, activeStunRemaining - deltaTime);
                return true;
            }
            return false;
        }

        private void ResetActiveStatusEffects()
        {
            if (activeAirborneDuration > 0f)
            {
                var position = transform.position;
                position.y = activeAirborneBaseY;
                transform.position = position;
            }
            activeStunRemaining = 0f;
            activeSlowRemaining = 0f;
            activeSlowRate = 0f;
            activeBleedRemaining = 0f;
            activeBleedTickRemaining = 0f;
            activeBleedInterval = 0f;
            activeBleedDamage = 0f;
            activeBleedSource = null;
            activeBurnRemaining = 0f;
            activeBurnTickRemaining = 0f;
            activeBurnInterval = 0f;
            activeBurnDamage = 0f;
            activeBurnSource = null;
            activeAirborneElapsed = 0f;
            activeAirborneDuration = 0f;
            activeAirborneHeight = 0f;
            activeAirborneBaseY = 0f;
        }

        // 08.07 안건준 추가 - damageMultiplier가 적용된 능력치 사본을 반환한다(원본 stats는 그대로 유지).
        // 배율이 항상 1이면 stats와 동일해서 기존 동작에 영향이 없다.
        private UnitStatsSnapshot GetEffectiveStats()
        {
            var effective = stats;
            effective.maxHealth *= Mathf.Max(0.01f, 1f + activeMonsterBuffModifier.HealthRate);
            effective.damage *= damageMultiplier *
                                Mathf.Max(0.01f, 1f + activeMonsterBuffModifier.AttackRate);
            effective.defense *= Mathf.Max(0.01f, 1f + activeMonsterBuffModifier.DefenseRate);
            effective.moveSpeed *= moveSpeedMultiplier *
                                   Mathf.Max(0.01f, 1f + activeMonsterBuffModifier.MoveSpeedRate) *
                                   Mathf.Clamp01(1f - activeSlowRate);
            effective.attackRange *= Mathf.Max(0.01f, 1f + activeMonsterBuffModifier.AttackRangeRate);
            effective.attackInterval /= Mathf.Max(0.01f, 1f + activeMonsterBuffModifier.AttackSpeedRate);
            return effective;
        }

        public void ApplyMonsterBuff(
            string effectId,
            MonsterStatModifier modifier,
            float duration,
            MonsterBuffStackPolicy stackPolicy)
        {
            if (string.IsNullOrWhiteSpace(effectId) || modifier.IsEmpty || duration <= 0f || !IsAlive)
            {
                return;
            }

            ActiveMonsterBuff existing = null;
            for (var index = 0; index < monsterBuffs.Count; index++)
            {
                if (string.Equals(monsterBuffs[index].EffectId, effectId, StringComparison.OrdinalIgnoreCase))
                {
                    existing = monsterBuffs[index];
                    break;
                }
            }

            if (existing == null)
            {
                monsterBuffs.Add(new ActiveMonsterBuff(effectId, modifier, duration));
            }
            else if (stackPolicy == MonsterBuffStackPolicy.RefreshDuration)
            {
                existing.Modifier = modifier;
                existing.RemainingTime = duration;
            }
            else if (GetModifierStrength(modifier) > GetModifierStrength(existing.Modifier))
            {
                existing.Modifier = modifier;
                existing.RemainingTime = duration;
            }
            else
            {
                existing.RemainingTime = Mathf.Max(existing.RemainingTime, duration);
            }

            RebuildMonsterBuffModifier();
        }

        public float ScaleSupportOutput(float amount)
        {
            return Mathf.Max(0f, amount) * supportOutputMultiplier;
        }

        private void TickMonsterBuffs(float deltaTime)
        {
            var changed = false;
            for (var index = monsterBuffs.Count - 1; index >= 0; index--)
            {
                var buff = monsterBuffs[index];
                buff.RemainingTime -= Mathf.Max(0f, deltaTime);
                if (buff.RemainingTime > 0f)
                {
                    continue;
                }

                monsterBuffs.RemoveAt(index);
                changed = true;
            }

            if (changed)
            {
                RebuildMonsterBuffModifier();
            }
        }

        private void RebuildMonsterBuffModifier()
        {
            activeMonsterBuffModifier = default;
            for (var index = 0; index < monsterBuffs.Count; index++)
            {
                activeMonsterBuffModifier += monsterBuffs[index].Modifier;
            }

            if (health != null && health.IsAlive)
            {
                var maxHealth = stats.maxHealth *
                                Mathf.Max(0.01f, 1f + activeMonsterBuffModifier.HealthRate);
                health.SetMaxHealth(maxHealth, true);
            }
        }

        private static float GetModifierStrength(MonsterStatModifier modifier)
        {
            return Mathf.Abs(modifier.HealthRate) +
                   Mathf.Abs(modifier.AttackRate) +
                   Mathf.Abs(modifier.DefenseRate) +
                   Mathf.Abs(modifier.AttackSpeedRate) +
                   Mathf.Abs(modifier.MoveSpeedRate) +
                   Mathf.Abs(modifier.AttackRangeRate);
        }

        private static float GetNegativeModifierStrength(MonsterStatModifier modifier)
        {
            return Mathf.Max(0f, -modifier.HealthRate) +
                   Mathf.Max(0f, -modifier.AttackRate) +
                   Mathf.Max(0f, -modifier.DefenseRate) +
                   Mathf.Max(0f, -modifier.AttackSpeedRate) +
                   Mathf.Max(0f, -modifier.MoveSpeedRate) +
                   Mathf.Max(0f, -modifier.AttackRangeRate);
        }
    }
}
