using System;
using ProjectMT.Shared.Combat;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public sealed partial class UnitActor
    {
        public float AdvanceForBasicAttack(
            Vector3 destination,
            float maxDistance,
            float stopDistance,
            float visualDuration)
        {
            if (!IsAlive || !combatReady || isManuallyHeld || maxDistance <= 0f)
            {
                return 0f;
            }

            var start = transform.position;
            var dashDestination = MonsterBasicAttackProfile.ResolveDashDestination(
                start,
                destination,
                maxDistance,
                stopDistance);
            var movement = dashDestination - start;
            movement.y = 0f;
            var advance = movement.magnitude;
            if (advance <= 0f)
            {
                return 0f;
            }

            if (!TryDashForAttack(dashDestination))
            {
                return 0f;
            }
            var direction = movement / advance;
            visualFeedback?.PlayAttackLunge(direction, Mathf.Min(0.3f, advance * 0.35f), visualDuration);
            return advance;
        }

        private void StartAttack(IDamageable target)
        {
            if (target == null || !target.IsAlive || world == null)
            {
                return;
            }

            var effectiveStats = GetEffectiveStats();
            attackCooldown = Mathf.Max(0.05f, effectiveStats.attackInterval);
            monsterSkillRuntime.NotifyBasicAttackPerformed(); // 다단 타격이어도 행동 한 번당 기력 한 번
            if (runtimeAssetSet != null && animationDriver != null && animationDriver.IsReady)
            {
                actionTarget = target; // normalizedTime 0 Marker도 같은 고정 타깃 사용
                attackActionRunning = true;
                var basicAttackProfile = runtimeAssetSet.CombatProfile?.Action?.BasicAttackProfile;
                var breathDuration = basicAttackProfile != null && basicAttackProfile.UsesBreathDurationContract
                    ? basicAttackProfile.BreathDuration
                    : 0f;
                if (animationDriver.TryBeginAttack(
                        effectiveStats.attackInterval,
                        ++nextActionSequenceId,
                        HandleAttackMarker,
                        breathDuration))
                {
                    world.PlayMonsterSfx(
                        runtimeAssetSet.FeedbackProfile?.BasicAttackVoice,
                        transform.position);
                    var startFeedback = animationDriver.CurrentAttackStartFeedback ??
                                        runtimeAssetSet.FeedbackProfile?.AttackStart;
                    world.PlayMonsterFeedback(
                        startFeedback,
                        animationDriver,
                        null,
                        runtimeAssetSet.BodyProfile?.VfxScale ?? 1f);
                    DispatchBasicAttackMotionVfx(true);
                    return;
                }

                actionTarget = null;
                attackActionRunning = false;
            }

            combatAnimation?.PlayAttack();
            var component = target as Component;
            var targetActor = component != null ? component.GetComponent<UnitActor>() : null;
            if (targetActor != null)
            {
                world.Attack(this, targetActor, effectiveStats); // Runtime Asset 없는 레거시 호환 경로
            }
            else
            {
                world.AttackDamageable(this, target, effectiveStats);
            }
        }

        private void TickAttackAction(float deltaTime)
        {
            if (actionTarget != null && actionTarget.IsAlive)
            {
                FaceTowards(actionTarget.Position, deltaTime);
            }

            if (animationDriver == null || animationDriver.TickAttack(deltaTime, HandleAttackMarker))
            {
                DispatchBasicAttackMotionVfx(false);
                attackActionRunning = false;
                actionTarget = null;
                animationDriver?.PlayIdle(true);
            }
        }

        private void DispatchBasicAttackMotionVfx(bool begin)
        {
            var profile = runtimeAssetSet?.CombatProfile?.Action?.BasicAttackProfile;
            if (profile == null || world == null || animationDriver == null)
            {
                return;
            }

            var origin = animationDriver.AttackOrigin.position;
            var hitPoint = actionTarget?.Position ?? origin;
            var forward = hitPoint - origin;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = transform.forward;
            }
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();
            var areaCenter = profile.Center switch
            {
                MonsterBasicAttackCenter.Source => origin,
                MonsterBasicAttackCenter.Forward => origin + forward * profile.ForwardOffset,
                _ => hitPoint
            };
            var context = new MonsterBasicAttackVfxContext(
                world,
                profile,
                runtimeAssetSet.FeedbackProfile,
                this,
                actionTarget,
                animationDriver,
                null,
                null,
                origin,
                hitPoint,
                areaCenter,
                Quaternion.LookRotation(forward, Vector3.up));
            if (begin)
            {
                MonsterBasicAttackVfxRuntime.BeginMotion(context);
            }
            else
            {
                MonsterBasicAttackVfxRuntime.EndMotion(context);
            }
        }

        private void HandleAttackMarker(int markerIndex, MonsterAttackMarker marker)
        {
            if (!attackActionRunning)
            {
                return;
            }

            if (actionTarget == null || !actionTarget.IsAlive || runtimeAssetSet == null)
            {
                return;
            }

            if (runtimeAssetSet.CombatProfile?.Action is ProjectileActionDefinition projectileAction)
            {
                var launchDirection = actionTarget.Position - transform.position;
                VisualFeedback?.PlayAttackRecoil(
                    launchDirection,
                    projectileAction.LaunchRecoilDistance,
                    projectileAction.LaunchRecoilDuration);
            }

            world?.ExecuteMonsterAction(
                this,
                actionTarget,
                GetEffectiveStats(),
                runtimeAssetSet,
                marker,
                animationDriver);
        }

        private bool ShouldDeferBasicAttackForActive()
        {
            return monsterSkillRuntime.IsActiveFocusQueued &&
                   world != null &&
                   world.ShouldDeferMonsterBasicAttack(this);
        }
    }
}
