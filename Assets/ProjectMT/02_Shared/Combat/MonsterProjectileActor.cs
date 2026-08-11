using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    [DisallowMultipleComponent]
    public sealed class MonsterProjectileActor : MonoBehaviour // 정식 Monster 단일·관통·범위 투사체
    {
        private readonly List<UnitActor> nearbyTargets = new List<UnitActor>();
        private readonly HashSet<int> hitTargetIds = new HashSet<int>();

        private CombatWorld world;
        private UnitActor source;
        private IDamageable primaryTarget;
        private ProjectileActionDefinition action;
        private MonsterFeedbackCue impactFeedback;
        private Vector3 targetPosition;
        private Vector3 direction;
        private float damage;
        private float impactVfxScale = 1f;
        private float remainingLifetime;
        private bool running;

        public void Launch(
            CombatWorld combatWorld,
            UnitActor owner,
            IDamageable target,
            ProjectileActionDefinition definition,
            float amount,
            Vector3 initialTargetPosition,
            MonsterFeedbackCue feedback = null,
            float vfxScale = 1f)
        {
            world = combatWorld;
            source = owner;
            primaryTarget = target;
            action = definition;
            impactFeedback = feedback;
            damage = Mathf.Max(0f, amount);
            impactVfxScale = Mathf.Max(0.01f, vfxScale);
            targetPosition = initialTargetPosition;
            direction = targetPosition == transform.position
                ? transform.forward
                : (targetPosition - transform.position).normalized;
            remainingLifetime = definition != null ? definition.Lifetime : 0f;
            hitTargetIds.Clear();
            running = world != null && source != null && action != null;
        }

        private void Update()
        {
            if (!running || world == null || action == null)
            {
                ReturnToPool();
                return;
            }

            if (world.IsPaused)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            remainingLifetime -= deltaTime;
            if (remainingLifetime <= 0f)
            {
                ReturnToPool();
                return;
            }

            if (action.Mode == MonsterProjectileAttackMode.Piercing)
            {
                TickPiercing(deltaTime);
                return;
            }

            if (primaryTarget == null || !primaryTarget.IsAlive)
            {
                ReturnToPool();
                return;
            }

            targetPosition = primaryTarget.Position + Vector3.up * 0.4f;
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                action.Speed * deltaTime);
            if ((transform.position - targetPosition).sqrMagnitude > 0.04f)
            {
                return;
            }

            if (action.Mode == MonsterProjectileAttackMode.Area)
            {
                if (ApplyAreaImpact(targetPosition))
                {
                    PlayImpactFeedback(targetPosition);
                }
            }
            else if (world.ApplyMonsterDamage(source, primaryTarget, damage))
            {
                PlayImpactFeedback(targetPosition);
            }

            ReturnToPool();
        }

        private void TickPiercing(float deltaTime)
        {
            transform.position += direction * (action.Speed * deltaTime);
            var opponentTeam = source.Team == UnitTeam.Player ? UnitTeam.Enemy : UnitTeam.Player;
            world.CollectUnits(
                opponentTeam,
                transform.position,
                action.HitRadius,
                action.MaxPiercingTargets,
                nearbyTargets);
            for (var index = 0; index < nearbyTargets.Count; index++)
            {
                var target = nearbyTargets[index];
                if (target == null || !hitTargetIds.Add(target.GetInstanceID()))
                {
                    continue;
                }

                if (world.ApplyMonsterDamage(source, target.Health, damage))
                {
                    PlayImpactFeedback(target.transform.position + Vector3.up * 0.4f);
                }

                if (hitTargetIds.Count >= action.MaxPiercingTargets)
                {
                    ReturnToPool();
                    return;
                }
            }
        }

        private bool ApplyAreaImpact(Vector3 center)
        {
            var opponentTeam = source.Team == UnitTeam.Player ? UnitTeam.Enemy : UnitTeam.Player;
            world.CollectUnits(
                opponentTeam,
                center,
                action.ImpactRadius,
                action.MaxImpactTargets,
                nearbyTargets);
            var applied = false;
            for (var index = 0; index < nearbyTargets.Count; index++)
            {
                applied |= world.ApplyMonsterDamage(source, nearbyTargets[index].Health, damage);
            }

            var component = primaryTarget as Component;
            if (component != null && component.GetComponent<UnitActor>() == null &&
                nearbyTargets.Count < action.MaxImpactTargets)
            {
                applied |= world.ApplyMonsterDamage(source, primaryTarget, damage);
            }

            return applied;
        }

        private void PlayImpactFeedback(Vector3 position)
        {
            world?.PlayMonsterFeedbackAt(
                impactFeedback,
                position,
                Quaternion.identity,
                impactVfxScale);
        }

        private void OnDisable()
        {
            running = false;
            world = null;
            source = null;
            primaryTarget = null;
            action = null;
            impactFeedback = null;
            impactVfxScale = 1f;
            nearbyTargets.Clear();
            hitTargetIds.Clear();
        }

        private void ReturnToPool()
        {
            if (!running)
            {
                return;
            }

            running = false;
            var owner = world;
            world = null;
            owner?.ReturnMonsterObject(gameObject);
        }
    }
}
