using System;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.CastleRaid
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CastleTarget), typeof(NavMeshAgent))]
    public sealed class CastleDefenderUnit : MonoBehaviour // 정식 적 외형을 쓰는 이동 수비대
    {
        private const float DestinationRefreshInterval = 0.16f;
        private const float DestinationChangeThreshold = 0.2f;
        private const float NavMeshRecoveryInterval = 0.5f;

        private CastleRaidController controller;
        private CastleTarget castleTarget;
        private NavMeshAgent agent;
        private System.Random patrolRandom;
        private Vector3 homeLocalPosition;
        private Vector3 patrolDestination;
        private Vector3 lastRequestedDestination;
        private float moveSpeed;
        private float detectionRange;
        private float attackRange;
        private float attackDamage;
        private float attackInterval;
        private float patrolRadius;
        private float attackCooldown;
        private float destinationRefreshCooldown;
        private float navigationRecoveryCooldown;
        private bool hasPatrolDestination;
        private bool runtimeInitialized;

        public bool RuntimeInitialized => runtimeInitialized;
        public bool IsMoving => agent != null && agent.enabled && agent.isOnNavMesh &&
                                !agent.isStopped && (agent.hasPath || agent.pathPending);

        public void Configure(
            CastleRaidController raidController,
            CastleTarget target,
            int movementSeed,
            float speed,
            float awarenessRange,
            float hitRange,
            float damage,
            float hitInterval,
            float homePatrolRadius)
        {
            controller = raidController != null
                ? raidController
                : throw new ArgumentNullException(nameof(raidController));
            castleTarget = target != null ? target : throw new ArgumentNullException(nameof(target));
            agent = GetComponent<NavMeshAgent>();
            homeLocalPosition = transform.localPosition;
            patrolRandom = new System.Random(movementSeed);
            moveSpeed = Mathf.Max(0.1f, speed);
            detectionRange = Mathf.Max(0.1f, awarenessRange);
            attackRange = Mathf.Max(0.35f, hitRange);
            attackDamage = Mathf.Max(0f, damage);
            attackInterval = Mathf.Max(0.1f, hitInterval);
            patrolRadius = Mathf.Max(0f, homePatrolRadius);

            agent.speed = moveSpeed;
            agent.acceleration = Mathf.Max(8f, moveSpeed * 5f);
            agent.angularSpeed = 540f;
            agent.stoppingDistance = Mathf.Max(0.15f, attackRange * 0.82f);
            agent.autoBraking = true;
            agent.autoRepath = true;
        }

        public void InitializeRuntime()
        {
            ShutdownRuntime();
            if (controller == null || castleTarget == null || agent == null)
            {
                return;
            }

            transform.localPosition = homeLocalPosition;
            agent.enabled = true;
            if (!TryPlaceOnNavMesh())
            {
                agent.enabled = false;
                Debug.LogWarning($"Castle defender could not reach NavMesh. Target={name}", this);
                return;
            }

            patrolRandom ??= new System.Random(GetInstanceID());
            attackCooldown = (float)patrolRandom.NextDouble() * attackInterval;
            destinationRefreshCooldown = 0f;
            navigationRecoveryCooldown = 0f;
            hasPatrolDestination = false;
            runtimeInitialized = true;
            castleTarget.Destroyed += HandleDestroyed;
        }

        public void Tick(float deltaTime)
        {
            if (!runtimeInitialized || castleTarget == null || !castleTarget.IsAlive)
            {
                return;
            }

            deltaTime = Mathf.Max(0f, deltaTime);
            attackCooldown = Mathf.Max(0f, attackCooldown - deltaTime);
            destinationRefreshCooldown = Mathf.Max(0f, destinationRefreshCooldown - deltaTime);
            navigationRecoveryCooldown = Mathf.Max(0f, navigationRecoveryCooldown - deltaTime);
            if (!EnsureOnNavMesh())
            {
                return;
            }

            var victim = controller.FindNearestAliveUnit(transform.position, detectionRange);
            if (victim == null)
            {
                TickPatrol();
                return;
            }

            hasPatrolDestination = false;
            var targetPosition = victim.transform.position;
            var planarOffset = targetPosition - transform.position;
            planarOffset.y = 0f;
            if (planarOffset.sqrMagnitude > attackRange * attackRange)
            {
                RequestDestination(targetPosition);
                return;
            }

            StopMoving();
            FaceTowards(targetPosition, deltaTime);
            if (attackCooldown > 0f ||
                controller.IsTurretLineBlocked(transform.position, victim.TurretHitPoint, 0.04f))
            {
                return; // 성벽을 사이에 둔 근접 공격은 허용하지 않는다
            }

            attackCooldown = attackInterval;
            victim.ApplyDefenseDamage(attackDamage, victim.TurretHitPoint, castleTarget);
        }

        public void ShutdownRuntime()
        {
            if (castleTarget != null)
            {
                castleTarget.Destroyed -= HandleDestroyed;
            }

            runtimeInitialized = false;
            hasPatrolDestination = false;
            StopMoving();
        }

        private void TickPatrol()
        {
            if (patrolRadius <= 0f)
            {
                StopMoving();
                return;
            }

            if (hasPatrolDestination &&
                (transform.position - patrolDestination).sqrMagnitude > 0.2f * 0.2f)
            {
                RequestDestination(patrolDestination);
                return;
            }

            hasPatrolDestination = TryResolvePatrolDestination(out patrolDestination);
            if (hasPatrolDestination)
            {
                RequestDestination(patrolDestination, true);
            }
            else
            {
                StopMoving();
            }
        }

        private bool TryResolvePatrolDestination(out Vector3 destination)
        {
            var homeWorldPosition = transform.parent == null
                ? homeLocalPosition
                : transform.parent.TransformPoint(homeLocalPosition);
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var angle = (float)patrolRandom.NextDouble() * Mathf.PI * 2f;
                var radius = Mathf.Lerp(patrolRadius * 0.35f, patrolRadius, (float)patrolRandom.NextDouble());
                var candidate = homeWorldPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (NavMesh.SamplePosition(candidate, out var hit, 0.8f, agent.areaMask))
                {
                    destination = hit.position;
                    return true;
                }
            }

            destination = default;
            return false;
        }

        private void RequestDestination(Vector3 destination, bool force = false)
        {
            if (!agent.isOnNavMesh || !force && destinationRefreshCooldown > 0f &&
                PlanarDistanceSquared(lastRequestedDestination, destination) <=
                DestinationChangeThreshold * DestinationChangeThreshold)
            {
                return;
            }

            destinationRefreshCooldown = DestinationRefreshInterval;
            lastRequestedDestination = destination;
            agent.isStopped = false;
            if (!agent.SetDestination(destination))
            {
                StopMoving();
            }
        }

        private bool EnsureOnNavMesh()
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                return true;
            }

            if (navigationRecoveryCooldown > 0f)
            {
                return false;
            }

            navigationRecoveryCooldown = NavMeshRecoveryInterval;
            agent.enabled = true;
            return TryPlaceOnNavMesh();
        }

        private bool TryPlaceOnNavMesh()
        {
            if (!NavMesh.SamplePosition(transform.position, out var hit, 1.5f, agent.areaMask))
            {
                return false;
            }

            return agent.Warp(hit.position);
        }

        private void FaceTowards(Vector3 targetPosition, float deltaTime)
        {
            var direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var desired = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, 540f * deltaTime);
        }

        private void StopMoving()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return;
            }

            agent.isStopped = true;
            agent.ResetPath();
        }

        private void HandleDestroyed(CastleTarget destroyedTarget)
        {
            ShutdownRuntime();
            if (agent != null)
            {
                agent.enabled = false;
            }
        }

        private static float PlanarDistanceSquared(Vector3 left, Vector3 right)
        {
            var offset = left - right;
            offset.y = 0f;
            return offset.sqrMagnitude;
        }
    }
}
