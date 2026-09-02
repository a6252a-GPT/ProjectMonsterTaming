using System;
using ProjectMT.Shared.Combat;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public sealed partial class UnitActor
    {
        public void SetFollowAnchor(Transform anchor, Vector3 offset, float detectionRange, float leashRange)
        {
            if (!ReferenceEquals(followAnchor, anchor))
            {
                hasLastAnchorPosition = false; // 08.07 안건준 추가 - 추종 대상이 바뀌면 이동 여부를 새로 측정
            }

            followAnchor = anchor;
            followOffset = offset;
            followDetectionRange = Mathf.Max(0.5f, detectionRange);
            followLeashRange = Mathf.Max(followDetectionRange, leashRange);
        }

        public void ClearFollowAnchor()
        {
            followAnchor = null;
            followOffset = Vector3.zero;
            hasLastAnchorPosition = false; // 08.07 안건준 추가 - 다음 추종 대상 기준으로 새로 측정하도록 초기화
        }

        public void SetCombatBehavior(UnitCombatBehavior behavior)
        {
            combatBehavior = behavior;
            SetCombatTarget(null);
            retargetCooldown = 0f; // 새 역할은 다음 Tick부터 즉시 반영
        }

        public void SetCombatReady(bool ready)
        {
            combatReady = ready;
            SetCombatTarget(null);
            retargetCooldown = 0f;
            if (ready)
            {
                return;
            }

            attackActionRunning = false;
            actionTarget = null;
            CancelCombatHitReaction();
            animationDriver?.PlayIdle(true); // 입장 이동은 콘텐츠 Controller가 별도로 재생
        }

        // 08.07 안건준 추가 - 콘텐츠 전용 스크립트가 일정 시간 동안 이 유닛의 공격을 특정 대상에 강제한다.
        // 아무도 호출하지 않으면 forcedTarget이 항상 null이라 기존 자동 전투(FindNearestOpponent) 동작에는
        // 전혀 영향이 없다. 유지 시간이 끝나거나 대상이 사라지면 자동으로 원래 탐색 방식으로 복귀한다.
        // (예: 수호자의 탑에서 군단장이 방어 건물 근처로 오면 아군이 적보다 건물을 먼저 공격하게 함)
        public void ForceTarget(IDamageable target, float holdSeconds)
        {
            if (target == null || !target.IsAlive)
            {
                return;
            }

            forcedTarget = target;
            forcedTargetTimer = Mathf.Max(0f, holdSeconds);
        }

        // 08.07 안건준 추가 - 지금 이 유닛이 target을 강제 공격 대상으로 삼고 있는지 확인.
        // 콘텐츠 쪽(예: 수호자의 탑 겹침 방지)에서 "공격 중인 대상에는 밀어내기를 적용하지 않는다"처럼
        // 판단할 때 쓴다. 아무도 호출하지 않으면 기존 동작에 영향이 없다.
        public bool IsForcedTargeting(IDamageable target)
        {
            return target != null && ReferenceEquals(forcedTarget, target);
        }

        // 08.07 안건준 추가 - 콘텐츠 전용 버프(예: 수호자의 탑 4번 건물의 적 이동 속도 버프)가 이동 속도를
        // 일시적으로 배율 조정할 때 쓴다. 아무도 호출하지 않으면 항상 1배라 기존 동작에 영향이 없다.
        public void SetMoveSpeedMultiplier(float multiplier)
        {
            moveSpeedMultiplier = Mathf.Max(0.01f, multiplier);
        }

        public bool BeginManualReposition()
        {
            if (!IsAlive || isManuallyHeld)
            {
                return false;
            }

            isManuallyHeld = true;
            CancelCombatHitReaction();
            SetCombatTarget(null); // 잡힌 동안 자기 이동·공격·재탐색만 정지
            return true;
        }

        public void EndManualReposition()
        {
            isManuallyHeld = false;
            SetCombatTarget(null);
            retargetCooldown = 0f; // 착지 직후 새 위치에서 다시 탐색
        }

        public bool TryDashForAttack(Vector3 destination)
        {
            if (!IsAlive || !combatReady || isManuallyHeld) return false;
            destination.y = transform.position.y;
            transform.position = destination;
            return true;
        }

        public bool TryApplyActivePull(Vector3 center, float distance, float duration)
        {
            var direction = center - transform.position;
            direction.y = 0f;
            var centerDistance = direction.magnitude;
            var travelDistance = Mathf.Min(distance, Mathf.Max(0f, centerDistance - 0.15f));
            return TryBeginCombatKnockback(
                direction,
                travelDistance,
                duration,
                0f,
                allowPlayerTarget: true,
                maximumDistance: MonsterActiveHitEffect.MaximumPullDistance,
                maximumDuration: MonsterActiveHitEffect.MaximumPullDuration,
                replaceOngoing: true);
        }

        [Obsolete("기본공격과 액티브 이동은 TryDashForAttack 단일 계약을 사용합니다.")]
        public bool TryTeleportForActive(Vector3 destination)
        {
            return TryDashForAttack(destination);
        }

        // 08.07 안건준 추가 - 강제 지정된 대상(IDamageable)을 향해 이동·공격한다.
        // 일반 Target 탐색·추종 로직과는 별개로 동작하며, 유지 시간이 끝나면 자동으로 원래 로직에 넘어간다.
        private void TickForcedTarget(float deltaTime)
        {
            Target = null; // 강제 지정 중에는 일반 Target 탐색 결과를 사용하지 않음
            var distance = PlanarDistance(transform.position, forcedTarget.Position);
            if (distance > Mathf.Max(0.2f, GetEffectiveStats().attackRange))
            {
                MoveTowards(forcedTarget.Position, deltaTime);
                return;
            }

            FaceTowards(forcedTarget.Position, deltaTime);
            if (canAttack && attackCooldown <= 0f && !ShouldDeferBasicAttackForActive())
            {
                StartAttack(forcedTarget); // 정식은 같은 Marker 경로, 레거시는 기존 구조물 공격
            }
            else
            {
                animationDriver?.PlayIdle();
            }
        }

        // 08.07 안건준 추가 - 추종 기준점(군단장)의 프레임 간 이동 거리를 재서 "지금 걷고 있는지" 판단한다.
        // 별도의 이동 컨트롤러 참조 없이, followAnchor의 위치 변화만으로 계산해서 어떤 콘텐츠에서도 그대로 쓸 수 있다.
        private bool IsAnchorMoving(float deltaTime)
        {
            var currentAnchorPosition = followAnchor.position;
            if (!hasLastAnchorPosition || deltaTime <= 0f)
            {
                lastAnchorPosition = currentAnchorPosition;
                hasLastAnchorPosition = true;
                return false;
            }

            var speed = PlanarDistance(currentAnchorPosition, lastAnchorPosition) / deltaTime;
            lastAnchorPosition = currentAnchorPosition;
            return speed > AnchorMovingSpeedThreshold;
        }

        private void MoveTowards(Vector3 destination, float deltaTime)
        {
            var effectiveStats = GetEffectiveStats();
            if (!canMove || effectiveStats.moveSpeed <= 0f)
            {
                animationDriver?.PlayIdle();
                return;
            }

            destination.y = transform.position.y;
            transform.position = Vector3.MoveTowards(transform.position, destination, effectiveStats.moveSpeed * deltaTime);
            FaceTowards(destination, deltaTime);
            animationDriver?.PlayMove();
        }

        private float ResolvePreferredRange(UnitActor target, float attackRange)
        {
            var configuredRange = attackRange * combatBehavior.PreferredRangeRatio;
            if (IsRanged || target == null)
            {
                return configuredRange;
            }

            var bodyRange = (BodyRadius + target.BodyRadius) * 0.9f;
            return Mathf.Min(attackRange * 0.94f, Mathf.Max(configuredRange, bodyRange));
        }

        private void SetCombatTarget(UnitActor target)
        {
            if (Target == target)
            {
                return;
            }

            Target = target;
        }

        private void MoveAwayFrom(Vector3 dangerPosition, float deltaTime)
        {
            var direction = transform.position - dangerPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = -transform.forward;
                direction.y = 0f;
            }

            MoveTowards(transform.position + direction.normalized, deltaTime);
        }

        private void FaceTowards(Vector3 destination, float deltaTime)
        {
            var direction = destination - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 12f * deltaTime);
        }

#if UNITY_EDITOR
        public void EditorConfigureReferences(
            HealthComponent healthComponent,
            UnitVisualFeedback feedbackComponent,
            MonsterAnimationDriver driver = null)
        {
            health = healthComponent;
            visualFeedback = feedbackComponent;
            animationDriver = driver;
        }
#endif

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }
    }
}
