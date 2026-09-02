using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    [DisallowMultipleComponent]
    public sealed class CommanderSkillProjectile : MonoBehaviour // 지정 대상 추적·충돌 전담
    {
        private CommanderSkillRuntime owner;
        private CommanderAttackSkillDefinition definition;
        private UnitActor target;
        private Vector3 startPosition;
        private Vector3 targetPosition;
        private float elapsed;
        private float duration;
        private float damageMultiplier = 1f;
        private bool running;

        public void Launch(
            CommanderSkillRuntime runtime,
            CommanderAttackSkillDefinition skill,
            UnitActor targetUnit,
            Vector3 destination,
            float skillDamageMultiplier)
        {
            owner = runtime;
            definition = skill;
            target = targetUnit;
            startPosition = transform.position;
            targetPosition = destination;
            elapsed = 0f;
            duration = Mathf.Max(0.08f, Vector3.Distance(startPosition, targetPosition) / skill.ProjectileSpeed);
            damageMultiplier = Mathf.Max(1f, skillDamageMultiplier);
            running = owner != null && definition != null;
        }

        private void Update()
        {
            if (!running || owner == null || definition == null)
            {
                owner?.ReturnProjectile(gameObject);
                return;
            }

            if (owner.IsPaused)
            {
                return;
            }

            if (target != null && target.IsAlive)
            {
                targetPosition = target.transform.position + Vector3.up * 0.45f;
            }

            elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(elapsed / duration);
            var position = Vector3.Lerp(startPosition, targetPosition, progress);
            if (definition.Trajectory == CommanderSkillTrajectory.Arc)
            {
                position.y += Mathf.Sin(progress * Mathf.PI) * definition.ArcHeight;
            }

            var direction = position - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            transform.position = position;
            if (progress < 1f)
            {
                return;
            }

            running = false;
            owner.ResolveImpact(
                definition,
                new CommanderSkillImpactContext(
                    startPosition,
                    target,
                    targetPosition,
                    targetPosition - startPosition),
                damageMultiplier);
            owner.ReturnProjectile(gameObject);
        }

        private void OnDisable()
        {
            running = false;
            owner = null;
            definition = null;
            target = null;
            damageMultiplier = 1f;
        }
    }
}
