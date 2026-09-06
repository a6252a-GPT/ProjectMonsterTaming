using System.Collections;
using System.Collections.Generic;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Pooling;
using ProjectMT.Shared.Stats;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public sealed partial class CombatWorld
    {
        public void Attack(UnitActor source, UnitActor target, UnitStatsSnapshot stats)
        {
            if (source == null || target == null || !source.IsAlive || !target.IsAlive || !target.IsCombatReady)
            {
                return;
            }

            if (stats.ranged && projectilePrefab != null && poolScope != null) // 원거리 유닛은 투사체 우선
            {
                var instance = poolScope.Rent(projectilePrefab, source.transform.position + Vector3.up * 0.45f, Quaternion.identity, transform);
                var projectile = instance == null ? null : instance.GetComponent<ProjectileActor>();
                if (projectile != null)
                {
                    projectile.Launch(this, source, target, stats.damage, Mathf.Max(1f, stats.projectileSpeed));
                    return;
                }

                if (instance != null)
                {
                    poolScope.Return(instance);
                }
            }

            ApplyMonsterDamage(source, target.Health, stats.damage); // 투사체 실패 시에도 공용 피해 계산 사용
        }

        public bool ExecuteMonsterAction(
            UnitActor source,
            IDamageable target,
            UnitStatsSnapshot stats,
            MonsterRuntimeAssetSet assetSet,
            MonsterAttackMarker marker,
            MonsterAnimationDriver animationDriver)
        {
            if (source == null || target == null || assetSet?.CombatProfile == null || marker == null)
            {
                return false;
            }

            var context = new MonsterActionExecutionContext(
                this,
                source,
                target,
                stats,
                assetSet,
                marker,
                animationDriver);
            var basicAttackProfile = assetSet.CombatProfile.Action?.BasicAttackProfile;
            var executed = basicAttackProfile != null
                ? basicAttackExecutor.Execute(context)
                : assetSet.CombatProfile.CombatType switch
            {
                MonsterCombatType.Melee => meleeExecutor.Execute(context),
                MonsterCombatType.Ranged => projectileExecutor.Execute(context),
                MonsterCombatType.Special => specialExecutor.Execute(context),
                _ => false
            };

            var feedback = marker.FeedbackOverride;
            if (feedback == null)
            {
                feedback = assetSet.CombatProfile.CombatType == MonsterCombatType.Special
                    ? assetSet.FeedbackProfile?.Special
                    : assetSet.FeedbackProfile?.AttackMarker;
            }

            if (basicAttackProfile == null && assetSet.CombatProfile.CombatType != MonsterCombatType.Ranged)
            {
                PlayMonsterFeedback(
                    feedback,
                    animationDriver,
                    marker.SocketOverride,
                    assetSet.BodyProfile?.VfxScale ?? 1f);
            }

            return executed;
        }

        public bool ApplyMonsterDamage(
            UnitActor source,
            IDamageable target,
            float amount,
            DamageFeedbackFlags feedbackFlags = DamageFeedbackFlags.None)
        {
            return ApplyMonsterDamageInternal(source, target, amount, feedbackFlags, true);
        }

        public bool ApplyMonsterSkillDamage(
            UnitActor source,
            IDamageable target,
            float amount,
            DamageFeedbackFlags feedbackFlags = DamageFeedbackFlags.None)
        {
            var skillRate = source == null ? 1f : Mathf.Max(0f, 1f + source.EffectiveStats.skillDamageRate);
            return ApplyMonsterDamageInternal(source, target, amount * skillRate, feedbackFlags, false);
        }

        private bool ApplyMonsterDamageInternal(
            UnitActor source,
            IDamageable target,
            float amount,
            DamageFeedbackFlags feedbackFlags,
            bool applyOutgoingPassive,
            bool allowReflect = true)
        {
            if (source == null || target == null || !source.IsAlive || !target.IsAlive || amount <= 0f)
            {
                return false;
            }

            var component = target as Component;
            var targetActor = component != null ? component.GetComponent<UnitActor>() : null;
            if (applyOutgoingPassive && source.SkillRuntime.WillEnhanceNextBasicHit)
            {
                feedbackFlags |= DamageFeedbackFlags.PassiveEnhancedNumber;
            }
            var resolvedAmount = applyOutgoingPassive
                ? amount * source.SkillRuntime.ResolveOutgoingDamageMultiplier(targetActor)
                : amount;
            if (targetActor != null && !targetActor.IsCombatReady)
            {
                return false;
            }

            if (targetActor != null)
            {
                resolvedAmount = CombatDamageCalculator.Calculate(
                    resolvedAmount,
                    source.EffectiveStats,
                    targetActor.EffectiveStats,
                    sharedStatConfig ?? CombatStatConfig.RuntimeDefault,
                    Random.value).Amount;
                resolvedAmount = targetActor.SkillRuntime.ResolveIncomingDamage(
                    resolvedAmount,
                    out var shieldAbsorbed);
                if (resolvedAmount <= 0f && shieldAbsorbed > 0f)
                {
                    if (applyOutgoingPassive)
                    {
                        source.SkillRuntime.NotifyBasicAttackHit(true, targetActor);
                    }
                    return true;
                }
            }

            float appliedDamage;
            var health = component != null ? component.GetComponent<HealthComponent>() : null;
            if (health != null)
            {
                var hitPoint = target.Position + Vector3.up * 0.4f;
                health.ApplyDamage(
                    new DamageRequest(source, resolvedAmount, hitPoint, false, feedbackFlags,
                        applyOutgoingPassive ? CombatDamageOrigin.BasicAttack : CombatDamageOrigin.MonsterSkill),
                    out appliedDamage);
            }
            else
            {
                appliedDamage = target.ReceiveDamage(source, resolvedAmount);
            }
            if (appliedDamage <= 0f)
            {
                return false;
            }

            if (applyOutgoingPassive && targetActor != null)
            {
                source.SkillRuntime.NotifyBasicAttackHit(true, targetActor);
                if (!targetActor.IsAlive)
                {
                    source.SkillRuntime.NotifyTargetDestroyed();
                }
            }

            if (allowReflect && targetActor != null && targetActor != source &&
                targetActor.IsAlive && source.IsAlive)
            {
                var reflectedDamage = targetActor.SkillRuntime.ResolveReflectedDamage(appliedDamage);
                if (reflectedDamage > 0f)
                {
                    ApplyMonsterDamageInternal(
                        targetActor,
                        source.Health,
                        reflectedDamage,
                        DamageFeedbackFlags.None,
                        false,
                        false);
                }
            }

            if (targetActor == null)
            {
                feedbackPlayer?.PlayDamage(
                    target.Position,
                    appliedDamage,
                    FloatingNumberStyle.EnemyDamage,
                    target.GetHashCode(),
                    feedbackFlags);
            }

            return true;
        }

        // 08.07 안건준 추가 - UnitActor가 아닌 대상(예: 수호자의 탑 방어 건물 같은 IDamageable)을 공격할 때 쓰는 진입점.
        // 원거리 유닛이면 기존과 동일하게 투사체를 쏘고 도착 시 피해+숫자를 표시하며, 근접이면 즉시 피해+숫자를 표시한다.
        // 기존 Attack(UnitActor, UnitActor, ...)와 ProjectileActor.Launch()는 전혀 건드리지 않아 일반 전투에는 영향이 없다.
        public void AttackDamageable(UnitActor source, IDamageable target, UnitStatsSnapshot stats)
        {
            if (source == null || target == null || !source.IsAlive || !target.IsAlive)
            {
                return;
            }

            if (stats.ranged && projectilePrefab != null && poolScope != null) // 원거리 유닛은 투사체 우선
            {
                var instance = poolScope.Rent(projectilePrefab, source.transform.position + Vector3.up * 0.45f, Quaternion.identity, transform);
                var projectile = instance == null ? null : instance.GetComponent<ProjectileActor>();
                if (projectile != null)
                {
                    projectile.LaunchAtDamageable(this, source, target, stats.damage, Mathf.Max(1f, stats.projectileSpeed), feedbackPlayer);
                    return;
                }

                if (instance != null)
                {
                    poolScope.Return(instance);
                }
            }

            // 08.07 안건준 수정 - 적을 공격할 때(HealthComponent.Damaged의 report.AppliedDamage)와 동일하게,
            // 화면 숫자는 요청한 공격력이 아니라 "실제로 깎인 체력"을 표시하도록 통일했다.
            var appliedDamage = target.ReceiveDamage(source, stats.damage); // 투사체 실패 또는 근접이면 즉시 피해
            if (appliedDamage > 0f)
            {
                feedbackPlayer?.PlayDamage(target.Position, appliedDamage, FloatingNumberStyle.EnemyDamage, target.GetHashCode());
            }
        }

        public void NotifyDeath(UnitActor unit, float delay = 0.38f)
        {
            if (unit != null)
            {
                StartCoroutine(ReturnDeadUnit(unit, Mathf.Max(0.05f, delay))); // Death Clip 뒤 풀 반환
            }
        }

        private IEnumerator ReturnDeadUnit(UnitActor unit, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (unit == null)
            {
                yield break;
            }

            units.Remove(unit);
            unit.Shutdown();
            poolScope?.Return(unit.gameObject);
        }

        private static float ResolveHealthRatio(UnitActor actor)
        {
            return actor?.Health == null || actor.Health.MaxHealth <= 0f
                ? 1f
                : Mathf.Clamp01(actor.Health.CurrentHealth / actor.Health.MaxHealth);
        }
    }
}
