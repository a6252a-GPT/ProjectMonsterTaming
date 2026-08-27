using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.Expedition
{
    [DisallowMultipleComponent]
    public sealed class NinjaFlankController : MonoBehaviour // 측면으로 빠진 뒤 플레이어 후방을 노린다
    {
        private const float SideExitDistance = 3.2f;
        private const float RearOffset = 1.5f;
        private const float RearSideOffset = 1.25f;
        private const float TargetHoldSeconds = 8f;

        private UnitActor actor;
        private UnitActor target;
        private Vector3 battleForward;
        private Vector3 battleRight;
        private Vector3 sideWaypoint;
        private float sideSign;
        private bool approachingRear;

        public void Configure(UnitActor owner, UnitActor rearTarget, Vector3 forward, int ninjaOrdinal)
        {
            actor = owner;
            target = rearTarget;
            battleForward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
            battleRight = Vector3.Cross(Vector3.up, battleForward).normalized;
            sideSign = ninjaOrdinal % 2 == 0 ? -1f : 1f; // 2기는 좌우, 3기는 2+1, 4기는 2+2
            sideWaypoint = actor.transform.position + battleRight * (SideExitDistance * sideSign);
            actor.SetCombatReady(false);
            actor.AnimationDriver?.PlayMove();
        }

        private void Update()
        {
            if (actor == null || !actor.IsAlive)
            {
                Destroy(this);
                return;
            }

            if (target == null || !target.IsAlive)
            {
                CompleteFlank();
                return;
            }

            var destination = approachingRear
                ? target.transform.position - battleForward * RearOffset +
                  battleRight * (RearSideOffset * sideSign)
                : sideWaypoint;
            destination.y = actor.transform.position.y;
            var delta = destination - actor.transform.position;
            delta.y = 0f;
            var distance = delta.magnitude;
            if (distance <= 0.12f)
            {
                if (!approachingRear)
                {
                    approachingRear = true;
                    return;
                }

                CompleteFlank();
                return;
            }

            var flankSpeed = Mathf.Max(0.1f, actor.EffectiveStats.moveSpeed * 1.22f);
            var step = Mathf.Min(distance, flankSpeed * Time.deltaTime);
            actor.transform.position += delta.normalized * step;
            actor.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            actor.AnimationDriver?.PlayMove();
        }

        private void CompleteFlank()
        {
            actor.SetCombatReady(true);
            if (target != null && target.IsAlive && target.Health != null)
            {
                actor.ForceTarget(target.Health, TargetHoldSeconds);
            }

            actor.AnimationDriver?.PlayIdle(true);
            Destroy(this);
        }
    }
}
