using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    [DefaultExecutionOrder(200)]
    internal sealed class DemoFollowerNavBridge : MonoBehaviour
    {
        private const float DetectRange = 6.5f;
        private const float FollowStopDistance = 1.1f;

        private UnitActor actor;
        private NavMeshAgent agent;
        private Transform commander;
        private Vector3 followOffset;
        private Vector3 lastDestination;
        private float nextFollowerAttackSfxTime;

        public void Initialize(UnitActor unit, Transform commanderTransform, Vector3 offset)
        {
            if (actor != null)
            {
                actor.Died -= HandleDied;
            }

            actor = unit;
            commander = commanderTransform;
            followOffset = offset;
            agent = GetComponent<NavMeshAgent>();
            if (actor != null)
            {
                actor.Died += HandleDied;
            }

            DemoCombatRoster.RegisterAlly(transform);
        }

        private void OnEnable()
        {
            DemoCombatRoster.RegisterAlly(transform);
        }

        private void OnDisable()
        {
            StopAgent();
            DemoCombatRoster.UnregisterAlly(transform);
        }

        private void OnDestroy()
        {
            if (actor != null)
            {
                actor.Died -= HandleDied;
            }
        }

        private void Update()
        {
            if (actor == null || !actor.IsAlive || commander == null || !DemoNavMeshUtil.TryEnsureOnNavMesh(agent))
            {
                return;
            }

            IDamageable enemy = DemoCombatRoster.FindNearest(transform.position, DetectRange);
            float attackRange = Mathf.Max(0.85f, actor.EffectiveStats.attackRange);
            if (enemy != null)
            {
                actor.ForceTarget(enemy, 0.4f);
                float distance = Mathf.Sqrt(DemoNavMeshUtil.PlanarSqrDistance(transform.position, enemy.Position));
                if (distance <= attackRange * 0.92f)
                {
                    StopAgent();
                    if (CanControlAgent())
                    {
                        agent.updateRotation = false;
                    }

                    if (Time.time >= nextFollowerAttackSfxTime)
                    {
                        DemoDungeonAudio.PlayFollowerAttack(transform.position);
                        float interval = actor.EffectiveStats.attackInterval > 0.05f
                            ? actor.EffectiveStats.attackInterval
                            : 0.8f;
                        nextFollowerAttackSfxTime = Time.time + interval;
                    }
                }
                else
                {
                    SetAgentDestination(enemy.Position, actor.EffectiveStats.moveSpeed);
                    actor.AnimationDriver?.PlayMove();
                }

                return;
            }

            Vector3 followPoint = commander.position + followOffset;
            float toCommander = Mathf.Sqrt(DemoNavMeshUtil.PlanarSqrDistance(transform.position, followPoint));
            if (toCommander > FollowStopDistance)
            {
                SetAgentDestination(followPoint, actor.EffectiveStats.moveSpeed * (toCommander > 4f ? 1.4f : 1f));
                actor.AnimationDriver?.PlayMove();
            }
            else
            {
                StopAgent();
            }
        }

        private void HandleDied(UnitActor _)
        {
            DemoCombatRoster.UnregisterAlly(transform);
            StopAgent();
            if (agent != null)
            {
                agent.enabled = false;
            }

            Collider collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        private void StopAgent()
        {
            if (!CanControlAgent())
            {
                return;
            }

            agent.isStopped = true;
        }

        private void SetAgentDestination(Vector3 destination, float speed)
        {
            if (!CanControlAgent())
            {
                return;
            }

            agent.isStopped = false;
            agent.updateRotation = true;
            agent.speed = speed;
            DemoNavMeshUtil.SetDestinationIfMoved(agent, destination, ref lastDestination);
        }

        private bool CanControlAgent()
        {
            return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
        }
    }
}
