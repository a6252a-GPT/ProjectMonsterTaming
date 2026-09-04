using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal interface IIceSlowable
    {
        void ApplyMoveSlow(float duration);
    }

    internal static class DemoIceCombat
    {
        public const float ArrowDamage = 40f;
        public const int MimicHitsToKill = 3;
        public const int GuardHitsToKill = 4;
        public const float MimicHealth = ArrowDamage * MimicHitsToKill;
        public const float GuardHealth = ArrowDamage * GuardHitsToKill;
    }

    internal static class DemoCombatRoster
    {
        private static readonly List<IDamageable> enemies = new List<IDamageable>(16);
        private static readonly List<Transform> allies = new List<Transform>(8);

        public static void Register(IDamageable enemy)
        {
            if (enemy == null || enemies.Contains(enemy))
            {
                return;
            }

            enemies.Add(enemy);
        }

        public static void Unregister(IDamageable enemy)
        {
            enemies.Remove(enemy);
        }

        public static bool IsEnemy(IDamageable enemy)
        {
            return enemy != null && enemies.Contains(enemy);
        }

        public static void RegisterAlly(Transform body)
        {
            if (body == null || allies.Contains(body))
            {
                return;
            }

            allies.Add(body);
        }

        public static void UnregisterAlly(Transform body)
        {
            allies.Remove(body);
        }

        public static void Clear()
        {
            enemies.Clear();
            allies.Clear();
            DemoCombatTargetUtil.ClearResolveCache();
        }

        public static IDamageable FindNearest(Vector3 origin, float range)
        {
            IDamageable nearest = null;
            float nearestSqr = range * range;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                IDamageable enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    enemies.RemoveAt(i);
                    continue;
                }

                float sqr = DemoNavMeshUtil.PlanarSqrDistance(origin, enemy.Position);
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = enemy;
                }
            }

            return nearest;
        }

        public static T FindNearest<T>(Vector3 origin, float range) where T : class, IDamageable
        {
            T nearest = null;
            float nearestSqr = range * range;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                IDamageable enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    enemies.RemoveAt(i);
                    continue;
                }

                if (!(enemy is T typed))
                {
                    continue;
                }

                float sqr = DemoNavMeshUtil.PlanarSqrDistance(origin, typed.Position);
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = typed;
                }
            }

            return nearest;
        }

        public static Transform FindNearestAlly(Vector3 origin, float range, bool followersOnly)
        {
            Transform nearest = null;
            float nearestSqr = range * range;
            for (int i = allies.Count - 1; i >= 0; i--)
            {
                Transform ally = allies[i];
                if (ally == null)
                {
                    allies.RemoveAt(i);
                    continue;
                }

                if (followersOnly && DemoCombatTargetUtil.IsPlayer(ally))
                {
                    continue;
                }

                float sqr = DemoNavMeshUtil.PlanarSqrDistance(origin, ally.position);
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = ally;
                }
            }

            return nearest;
        }
    }
}
