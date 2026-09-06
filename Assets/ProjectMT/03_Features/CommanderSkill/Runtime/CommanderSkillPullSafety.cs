using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    public sealed class CommanderSkillCastState
    {
        internal CommanderSkillCastState(long id, Vector3 origin) { Id = id; Origin = origin; }
        internal long Id { get; }
        internal Vector3 Origin { get; }
        internal readonly HashSet<int> PulledTargets = new HashSet<int>();
    }

    public static class CommanderSkillPullSafety
    {
        public static float ResolveDistance(Vector3 position, Vector3 center, float maximum, float clearance)
        {
            var delta = center - position; delta.y = 0f;
            return Mathf.Min(maximum, Mathf.Max(0f, delta.magnitude - clearance));
        }

        public static bool IsSafe(Collider ground, UnitActor target, Vector3 destination)
        {
            if (ground == null || !ground.enabled || target == null || !target.CanMove || !target.IsAlive ||
                !target.IsCombatReady || target.Team != UnitTeam.Enemy || target.IsBoss || target.IsManuallyHeld ||
                target.IsKnockedBack || target.IsActiveAirborne || target.IsActiveStunned ||
                target.EffectiveStats.moveSpeed <= 0f) return false;
            var radius = target.BodyRadius;
            var start = target.transform.position;
            destination.y = start.y;
            var delta = destination - start;
            for (var sample = 0; sample <= 8; sample++)
            {
                var point = Vector3.Lerp(start, destination, sample / 8f);
                foreach (var offset in new[] { Vector3.zero, Vector3.left * radius, Vector3.right * radius,
                    Vector3.forward * radius, Vector3.back * radius })
                    if (!ground.Raycast(new Ray(point + offset + Vector3.up * 3f, Vector3.down), out var hit, 6f) ||
                        hit.normal.y < 0.7f || Mathf.Abs(hit.point.y - start.y) > 0.5f) return false;
            }
            var probe = start + Vector3.up * (radius + 0.05f);
            foreach (var overlap in Physics.OverlapSphere(probe, radius, ~0, QueryTriggerInteraction.Ignore))
                if (Blocks(overlap)) return false;
            foreach (var hit in Physics.SphereCastAll(probe, radius, delta.normalized, delta.magnitude,
                ~0, QueryTriggerInteraction.Ignore))
                if (Blocks(hit.collider)) return false;
            return true;

            bool Blocks(Collider collider) => collider != null && collider != ground &&
                collider.GetComponentInParent<UnitActor>() == null;
        }
    }
}
