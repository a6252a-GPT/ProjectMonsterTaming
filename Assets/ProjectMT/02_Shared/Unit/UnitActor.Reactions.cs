using System;
using ProjectMT.Shared.Combat;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public sealed partial class UnitActor
    {
        public void ApplyLocalHitStop(float duration)
        {
            duration = Mathf.Clamp(duration, 0f, 0.06f);
            if (duration <= 0f || !gameObject.activeInHierarchy)
            {
                return;
            }

            localHitStopRemaining = Mathf.Max(localHitStopRemaining, duration); // 연속 타격은 합산하지 않음
            SetLocalAnimationPaused(true);
        }

        public bool TryApplyCombatKnockback(
            Vector3 worldDirection,
            float distance,
            float duration,
            float postKnockbackStagger = 0f)
        {
            return TryBeginCombatKnockback(
                worldDirection,
                distance,
                duration,
                postKnockbackStagger,
                allowPlayerTarget: false);
        }

        private bool TryBeginCombatKnockback(
            Vector3 worldDirection,
            float distance,
            float duration,
            float postKnockbackStagger,
            bool allowPlayerTarget,
            float maximumDistance = 0.6f,
            float maximumDuration = 0.24f,
            bool replaceOngoing = false)
        {
            if ((!allowPlayerTarget && Team == UnitTeam.Player) ||
                !IsAlive || IsBoss || !combatReady || isManuallyHeld || distance <= 0f)
            {
                return false;
            }

            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            distance = Mathf.Clamp(distance, 0f, Mathf.Max(0f, maximumDistance));
            duration = Mathf.Clamp(duration, 0.05f, Mathf.Max(0.05f, maximumDuration));
            postKnockbackStagger = Mathf.Clamp(postKnockbackStagger, 0f, 0.3f);
            if (!replaceOngoing && IsKnockedBack && combatKnockbackElapsed < combatKnockbackDuration * 0.75f)
            {
                combatPostKnockbackStaggerDuration = Mathf.Max(
                    combatPostKnockbackStaggerDuration,
                    postKnockbackStagger);
                if (distance <= combatKnockbackDistance)
                {
                    return true; // 다단히트가 같은 밀림을 누적·재시작하지 않음
                }

                distance = Mathf.Max(0f, distance - combatKnockbackAppliedDistance); // 강한 요청만 남은 거리로 승격
            }

            combatKnockbackDirection = worldDirection.normalized;
            combatKnockbackDistance = distance;
            combatKnockbackDuration = duration;
            combatKnockbackElapsed = 0f;
            combatKnockbackAppliedDistance = 0f;
            combatPostKnockbackStaggerDuration = postKnockbackStagger;
            combatStaggerRemaining = 0f; // 새 타격은 남아 있던 이전 경직을 대체
            return distance > 0f;
        }

        public bool TryApplyActiveKnockback(
            Vector3 worldDirection,
            float distance,
            float duration,
            float postKnockbackStagger = 0f)
        {
            return TryBeginCombatKnockback(
                worldDirection,
                distance,
                duration,
                postKnockbackStagger,
                allowPlayerTarget: true);
        }

        public bool TryApplyActiveStun(float duration)
        {
            if (!IsAlive || IsBoss || !combatReady || duration <= 0f) return false;
            activeStunRemaining = Mathf.Max(activeStunRemaining, duration);
            if (runtimeAssetSet == null)
            {
                combatAnimation?.PlayStun(activeStunRemaining);
            }
            return true;
        }

        public bool TryApplyActiveAirborne(float height, float duration)
        {
            if (!IsAlive || IsBoss || !combatReady || height <= 0f || duration <= 0f) return false;
            if (activeAirborneDuration <= 0f) activeAirborneBaseY = transform.position.y;
            activeAirborneElapsed = 0f;
            activeAirborneHeight = Mathf.Max(activeAirborneHeight, height);
            activeAirborneDuration = Mathf.Max(activeAirborneDuration, duration);
            return true;
        }

        public bool TryApplyCombatStagger(float duration)
        {
            duration = Mathf.Clamp(duration, 0f, 0.5f);
            if (Team == UnitTeam.Player || !IsAlive || IsBoss || !combatReady || isManuallyHeld || duration <= 0f)
            {
                return false;
            }

            if (IsKnockedBack)
            {
                combatPostKnockbackStaggerDuration = Mathf.Max(combatPostKnockbackStaggerDuration, duration);
            }
            else
            {
                combatStaggerRemaining = Mathf.Max(combatStaggerRemaining, duration);
            }
            return true;
        }

        private bool TickLocalHitStop()
        {
            if (localHitStopRemaining <= 0f)
            {
                return false;
            }

            localHitStopRemaining = Mathf.Max(0f, localHitStopRemaining - Time.unscaledDeltaTime);
            if (localHitStopRemaining <= 0f)
            {
                SetLocalAnimationPaused(false);
                return false;
            }

            return true;
        }

        private bool TickCombatKnockback(float deltaTime)
        {
            if (!IsKnockedBack)
            {
                return false;
            }

            combatKnockbackElapsed = Mathf.Min(
                combatKnockbackDuration,
                combatKnockbackElapsed + Mathf.Max(0f, deltaTime));
            var ratio = combatKnockbackDuration <= 0f ? 1f : combatKnockbackElapsed / combatKnockbackDuration;
            var pushRatio = Mathf.Clamp01(ratio / 0.65f);
            var easedPush = 1f - Mathf.Pow(1f - pushRatio, 3f); // 앞 65%에 퍽 밀고 뒤 35%는 정지
            var desiredDistance = combatKnockbackDistance * easedPush;
            var stepDistance = Mathf.Max(0f, desiredDistance - combatKnockbackAppliedDistance);
            if (stepDistance > 0f)
            {
                var nextPosition = transform.position + combatKnockbackDirection * stepDistance;
                nextPosition.y = transform.position.y; // 실제 Y는 지형 기준을 유지
                transform.position = nextPosition;
                combatKnockbackAppliedDistance = desiredDistance;
            }

            if (ratio >= 1f)
            {
                combatStaggerRemaining = combatPostKnockbackStaggerDuration;
                CompleteCombatKnockback();
            }

            return true;
        }

        private bool TickCombatStagger(float deltaTime)
        {
            if (!IsHitStaggered)
            {
                return false;
            }

            combatStaggerRemaining = Mathf.Max(0f, combatStaggerRemaining - Mathf.Max(0f, deltaTime));
            return true;
        }

        private void CompleteCombatKnockback()
        {
            combatKnockbackDirection = Vector3.zero;
            combatKnockbackDistance = 0f;
            combatKnockbackDuration = 0f;
            combatKnockbackElapsed = 0f;
            combatKnockbackAppliedDistance = 0f;
            combatPostKnockbackStaggerDuration = 0f;
        }

        private void CancelCombatHitReaction()
        {
            CompleteCombatKnockback();
            combatStaggerRemaining = 0f;
        }

        private void SetLocalAnimationPaused(bool paused)
        {
            if (animationDriver != null)
            {
                animationDriver.SetLocallyPaused(paused);
                return;
            }

            if (paused)
            {
                if (fallbackAnimatorsPaused)
                {
                    return;
                }

                RefreshFallbackHitStopAnimators();
                for (var index = 0; index < fallbackHitStopAnimators.Length; index++)
                {
                    var animator = fallbackHitStopAnimators[index];
                    if (animator == null)
                    {
                        continue;
                    }

                    fallbackAnimatorSpeeds[index] = animator.speed;
                    animator.speed = 0f;
                }

                fallbackAnimatorsPaused = true;
                return;
            }

            if (!fallbackAnimatorsPaused)
            {
                return;
            }

            for (var index = 0; index < fallbackHitStopAnimators.Length; index++)
            {
                if (fallbackHitStopAnimators[index] != null)
                {
                    fallbackHitStopAnimators[index].speed = fallbackAnimatorSpeeds[index];
                }
            }

            fallbackAnimatorsPaused = false;
        }

        private void RefreshFallbackHitStopAnimators()
        {
            if (animationDriver != null || fallbackAnimatorsPaused || fallbackAnimatorsResolved)
            {
                return;
            }

            fallbackHitStopAnimators = GetComponentsInChildren<Animator>(true);
            fallbackAnimatorSpeeds = new float[fallbackHitStopAnimators.Length];
            fallbackAnimatorsResolved = true;
        }

        private void HandleDamaged(DamageReport report)
        {
            monsterSkillRuntime.NotifyDamaged(report);
            feedback?.PlayHit(this, report);
            if (!report.Killed && runtimeAssetSet == null)
            {
                combatAnimation?.PlayHit();
            }
            if (runtimeAssetSet != null)
            {
                world?.PlayMonsterFeedback(
                    runtimeAssetSet.FeedbackProfile?.HitReceived,
                    animationDriver,
                    runtimeAssetSet.BodyProfile?.HitCenterPath,
                    runtimeAssetSet.BodyProfile?.VfxScale ?? 1f);
            }
        }

        private void HandleDied(DamageReport report)
        {
            monsterSkillRuntime.Shutdown();
            feedback?.PlayDeath(this, report);
            attackActionRunning = false;
            actionTarget = null;
            CancelCombatHitReaction();
            var returnDelay = (animationDriver?.PlayDeath() ?? combatAnimation?.PlayDeath() ?? 0.38f) +
                              localHitStopRemaining;
            if (runtimeAssetSet != null)
            {
                world?.PlayMonsterFeedback(
                    runtimeAssetSet.FeedbackProfile?.Death,
                    animationDriver,
                    runtimeAssetSet.BodyProfile?.HitCenterPath,
                    runtimeAssetSet.BodyProfile?.VfxScale ?? 1f);
            }

            Died?.Invoke(this);
            world?.NotifyDeath(this, returnDelay); // Death Clip 종료 뒤 풀 반환
        }
    }
}
