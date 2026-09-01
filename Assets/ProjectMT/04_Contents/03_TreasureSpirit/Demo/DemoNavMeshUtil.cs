using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class DemoNavMeshUtil
    {
        public static bool TryEnsureOnNavMesh(NavMeshAgent agent, float sampleDistance = 8f)
        {
            if (agent == null)
            {
                return false;
            }

            if (!agent.enabled)
            {
                agent.enabled = true;
            }

            if (!agent.isActiveAndEnabled)
            {
                return false;
            }

            if (agent.isOnNavMesh)
            {
                return true;
            }

            if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }

            return agent.isOnNavMesh;
        }

        public static float PlanarSqrDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        public static void SetDestinationIfMoved(
            NavMeshAgent agent,
            Vector3 destination,
            ref Vector3 lastDestination,
            float minPlanarSqr = 0.25f)
        {
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            {
                return;
            }

            bool needsPath = !agent.hasPath && !agent.pathPending;
            if (!needsPath && PlanarSqrDistance(destination, lastDestination) < minPlanarSqr)
            {
                return;
            }

            lastDestination = destination;
            agent.SetDestination(destination);
        }
    }
}
