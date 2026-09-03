using System.Collections.Generic;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class DemoCombatTargetUtil
    {
        private static readonly Dictionary<int, Transform> resolvedAlly = new Dictionary<int, Transform>(128);
        private static readonly HashSet<int> resolvedNonAlly = new HashSet<int>();

        public static bool TryResolveAlly(Component source, out Transform body)
        {
            body = null;
            if (source == null)
            {
                return false;
            }

            int id = source.GetInstanceID();
            if (resolvedAlly.TryGetValue(id, out body))
            {
                if (IsLivingAlly(body))
                {
                    return true;
                }

                resolvedAlly.Remove(id);
                body = null;
            }
            else if (resolvedNonAlly.Contains(id))
            {
                return false;
            }

            if (TryFindAlly(source, out body))
            {
                resolvedAlly[id] = body;
                return true;
            }

            resolvedNonAlly.Add(id);
            return false;
        }

        public static void ClearResolveCache()
        {
            resolvedAlly.Clear();
            resolvedNonAlly.Clear();
        }

        public static bool IsPlayer(Transform body)
        {
            return body != null && body.TryGetComponent(out PlayerCharacterController _);
        }

        public static bool TryConsumeHit(Dictionary<int, float> nextHitTime, int targetId, float cooldown)
        {
            if (nextHitTime.TryGetValue(targetId, out float readyAt) && Time.time < readyAt)
            {
                return false;
            }

            nextHitTime[targetId] = Time.time + cooldown;
            return true;
        }

        public static void DamageAlly(Transform body, float damage, Vector3 hitOrigin)
        {
            if (body == null || damage <= 0f || DemoDungeonController.IsGameplayPaused)
            {
                return;
            }

            if (body.TryGetComponent(out PlayerCharacterController player))
            {
                player.TakeDamage(damage, hitOrigin);
                return;
            }

            if (body.TryGetComponent(out UnitActor actor) && actor.Team == UnitTeam.Player && actor.Health != null)
            {
                actor.Health.ApplyDamage(new DamageRequest(null, damage, hitOrigin));
                return;
            }

            if (body.TryGetComponent(out FollowerAI follower))
            {
                follower.TakeDamage(damage);
            }
        }

        private static bool TryFindAlly(Component source, out Transform body)
        {
            PlayerCharacterController player = source.GetComponentInParent<PlayerCharacterController>();
            if (player != null)
            {
                body = player.transform;
                return true;
            }

            UnitActor actor = source.GetComponentInParent<UnitActor>();
            if (actor != null && actor.Team == UnitTeam.Player && actor.IsAlive)
            {
                body = actor.transform;
                return true;
            }

            FollowerAI follower = source.GetComponentInParent<FollowerAI>();
            if (follower != null)
            {
                body = follower.transform;
                return true;
            }

            body = null;
            return false;
        }

        private static bool IsLivingAlly(Transform body)
        {
            if (body == null)
            {
                return false;
            }

            if (body.TryGetComponent(out PlayerCharacterController _))
            {
                return true;
            }

            if (body.TryGetComponent(out UnitActor actor))
            {
                return actor.IsAlive && actor.Team == UnitTeam.Player;
            }

            return body.TryGetComponent(out FollowerAI _);
        }
    }
}
