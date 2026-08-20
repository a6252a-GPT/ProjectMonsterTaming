using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    public sealed class FallenCommanderBossStateMachine
    {
        private enum BossState
        {
            Idle,
            HandSlam,
            MarkStrike,
            WideBurst,
            LineStrike,
            Broken,
            Dead
        }

        private CombatWorld combatWorld;
        private UnitActor bossActor;
        private Transform commanderRoot;
        private HealthComponent commanderHealth;
        private FallenCommanderBossFacingSmoother bossFacingSmoother;
        private FallenCommanderBossAnimationPresenter animationPresenter;
        private AnimationClip breakMotion;
        private float breakMotionDuration;

        private BossState currentState;
        private float attackInterval;
        private float attackCooldownRemaining;
        private float markStrikeStunDuration;
        private float basicAttackCastTime;
        private float basicAttackRadius;
        private float wideBurstCastTime;
        private float wideBurstRadius;
        private float wideBurstStunDuration;
        private float lineStrikeCastTime;
        private float lineStrikeWidth;
        private float lineStrikeLength;
        private float lineStrikeStunDuration;
        private float commanderStunRemaining;
        private System.Action<bool> commanderStunChanged;
        private bool isCommanderStunned;
        private bool isActive;

        private FallenCommanderAttackData basicAttackMotion;
        private FallenCommanderAttackData markStrikeMotion;
        private FallenCommanderAttackData wideBurstMotion;
        private FallenCommanderAttackData lineStrikeMotion;

        public bool IsCommanderStunned => isCommanderStunned;
        public float CommanderStunRemainingTime => commanderStunRemaining;

        // 위치 공격 범위로 사용할 프리팹
        private GameObject markStrikeTelegraphPrefab;

        // 현재 바닥에 생성되어 있는 범위 오브젝트
        private GameObject activeTelegraph;

        // 범위가 나타난 후 공격까지 걸리는 시간
        private float markStrikeCastTime;

        // 실제 공격 반지름
        private float markStrikeRadius;

        // 현재 공격이 끝날 때까지 남은 시간
        private float stateTimeRemaining;

        // 처음 지정한 군단장의 위치
        private Vector3 markStrikePosition;
        private Vector3 lineStrikeDirection;

        public void Configure(
            CombatWorld world,
            UnitActor boss,
            Transform commander,
            HealthComponent commanderTarget,
            float interval,
            FallenCommanderBossAnimationPresenter animations,
            AnimationClip brokenMotion,
            float brokenMotionDuration,
            FallenCommanderAttackData basicMotion,
            GameObject telegraphPrefab,
            FallenCommanderAttackData markMotion,
            FallenCommanderAttackData wideMotion,
            FallenCommanderAttackData lineMotion,
            System.Action<bool> stunChanged,
            FallenCommanderBossFacingSmoother facingSmoother)
        {
            Shutdown();

            combatWorld = world;
            bossActor = boss;
            commanderRoot = commander;
            commanderHealth = commanderTarget;
            bossFacingSmoother = facingSmoother;
            animationPresenter = animations;
            breakMotion = brokenMotion;
            this.breakMotionDuration = brokenMotionDuration;

            // 0초나 음수가 되는 것을 막기
            attackInterval = Mathf.Max(0.1f, interval);
            basicAttackMotion = basicMotion;
            basicAttackCastTime = Mathf.Max(0.1f, basicMotion == null ? 0f : basicMotion.WarningDuration);
            basicAttackRadius = Mathf.Max(0.1f, basicMotion == null ? 0f : basicMotion.Radius);
            markStrikeTelegraphPrefab = telegraphPrefab;
            markStrikeMotion = markMotion;
            markStrikeCastTime = Mathf.Max(0.1f, markMotion == null ? 0f : markMotion.WarningDuration);
            markStrikeRadius = Mathf.Max(0.1f, markMotion == null ? 0f : markMotion.Radius);
            markStrikeStunDuration = Mathf.Max(0.1f, markMotion == null ? 0f : markMotion.StunDuration);
            wideBurstMotion = wideMotion;
            wideBurstCastTime = Mathf.Max(0.1f, wideMotion == null ? 0f : wideMotion.WarningDuration);
            wideBurstRadius = Mathf.Max(0.1f, wideMotion == null ? 0f : wideMotion.Radius);
            wideBurstStunDuration = Mathf.Max(0f, wideMotion == null ? 0f : wideMotion.StunDuration);
            lineStrikeMotion = lineMotion;
            lineStrikeCastTime = Mathf.Max(0.1f, lineMotion == null ? 0f : lineMotion.WarningDuration);
            lineStrikeWidth = Mathf.Max(0.1f, lineMotion == null ? 0f : lineMotion.Width);
            lineStrikeLength = Mathf.Max(0.1f, lineMotion == null ? 0f : lineMotion.Length);
            lineStrikeStunDuration = Mathf.Max(0f, lineMotion == null ? 0f : lineMotion.StunDuration);
            commanderStunChanged = stunChanged;

            attackCooldownRemaining = attackInterval;
            currentState = BossState.Idle;

            isActive =
                combatWorld != null &&
                bossActor != null &&
                commanderRoot != null &&
                commanderHealth != null &&
                markStrikeTelegraphPrefab != null &&
                animationPresenter != null &&
                bossFacingSmoother != null &&
                commanderStunChanged != null;
        }

        public void Tick(float deltaTime)
        {
            if (!isActive ||
                bossActor == null ||
                !bossActor.IsAlive ||
                commanderHealth == null ||
                !commanderHealth.IsAlive)
            {
                return;
            }

            TickCommanderStun(deltaTime);

            switch (currentState)
            {
                case BossState.Idle:
                    TickIdle(deltaTime);
                    break;

                case BossState.HandSlam:
                case BossState.MarkStrike:
                case BossState.WideBurst:
                case BossState.LineStrike:
                    TickAttack(deltaTime);
                    break;

                case BossState.Broken:
                case BossState.Dead:
                    break;
            }
        }

        public void EnterBroken()
        {
            if (!isActive || currentState == BossState.Broken)
            {
                return;
            }

            DestroyActiveTelegraph();
            animationPresenter?.StopPlayback();
            animationPresenter?.Play(
                breakMotion,
                stopAfterMotion: true,
                durationOverride: breakMotionDuration);
            stateTimeRemaining = 0f;
            currentState = BossState.Broken;
            bossFacingSmoother.SetTrackingEnabled(false);
            bossActor.ForceTarget(
                bossActor.Health,
                float.PositiveInfinity);
        }

        public void ExitBroken()
        {
            if (!isActive || currentState != BossState.Broken)
            {
                return;
            }

            attackCooldownRemaining = attackInterval;
            currentState = BossState.Idle;
            animationPresenter?.StopPlayback();
            bossActor.ForceTarget(
                commanderHealth,
                float.PositiveInfinity);
            bossFacingSmoother.SetTrackingEnabled(true);
        }

        public void Shutdown()
        {
            DestroyActiveTelegraph();
            ReleaseCommanderStun();
            animationPresenter?.Stop();

            isActive = false;
            combatWorld = null;
            bossActor = null;
            commanderRoot = null;
            commanderHealth = null;
            bossFacingSmoother = null;
            animationPresenter = null;
            breakMotion = null;
            breakMotionDuration = 0f;
            markStrikeTelegraphPrefab = null;
            commanderStunChanged = null;
            attackCooldownRemaining = 0f;
            commanderStunRemaining = 0f;
            markStrikeStunDuration = 0f;
            stateTimeRemaining = 0f;
            markStrikePosition = Vector3.zero;
            lineStrikeDirection = Vector3.zero;
            currentState = BossState.Idle;
        }

        private void TickIdle(float deltaTime)
        {
            attackCooldownRemaining =
                Mathf.Max(0f, attackCooldownRemaining - deltaTime);

            if (attackCooldownRemaining > 0f)
            {
                return;
            }

            switch (Random.Range(0, 4))
            {
                case 0:
                    BeginBasicAttack();
                    break;
                case 1:
                    BeginMarkStrike();
                    break;
                case 2:
                    BeginWideBurst();
                    break;
                default:
                    BeginLineStrike();
                    break;
            }
        }

        private void BeginBasicAttack()
        {
            BeginCircleAttack(
                BossState.HandSlam,
                bossActor.transform.position,
                basicAttackCastTime,
                basicAttackRadius,
                basicAttackMotion);
        }

        private void BeginMarkStrike()
        {
            // 공격을 시작한 순간의 군단장 위치를 저장
            markStrikePosition = commanderRoot.position;

            // 범위 표시가 바닥에 묻히는 것을 막기 위해 아주 조금 올린다.
            markStrikePosition.y += 0.02f;

            // 위치 공격 시전 시간
            stateTimeRemaining = markStrikeCastTime;

            // FSM의 현재 상태를 위치 공격으로 변경
            currentState = BossState.MarkStrike;
            PauseBossTracking();
            animationPresenter.Play(markStrikeMotion?.PreCastMotion);

            // 이전 공격 표시 제거
            DestroyActiveTelegraph();

            // 저장한 위치에 공격 범위 프리팹을 생성
            activeTelegraph = Object.Instantiate(
                markStrikeTelegraphPrefab,
                markStrikePosition,
                Quaternion.identity);

            // 반지름을 실제 오브젝트의 지름으로 변환
            var telegraphScale = activeTelegraph.transform.localScale;
            telegraphScale.x = markStrikeRadius * 2f;
            telegraphScale.z = markStrikeRadius * 2f;
            activeTelegraph.transform.localScale = telegraphScale;
        }

        private void BeginWideBurst()
        {
            BeginCircleAttack(
                BossState.WideBurst,
                bossActor.transform.position,
                wideBurstCastTime,
                wideBurstRadius,
                wideBurstMotion);
        }

        private void BeginLineStrike()
        {
            var origin = bossActor.transform.position;
            lineStrikeDirection = commanderRoot.position - origin;
            lineStrikeDirection.y = 0f;
            if (lineStrikeDirection.sqrMagnitude < 0.001f)
            {
                lineStrikeDirection = bossActor.transform.forward;
            }

            lineStrikeDirection.Normalize();
            var center = origin + lineStrikeDirection * (lineStrikeLength * 0.5f);
            center.y += 0.02f;

            stateTimeRemaining = lineStrikeCastTime;
            currentState = BossState.LineStrike;
            PauseBossTracking();
            animationPresenter.Play(lineStrikeMotion?.PreCastMotion);
            DestroyActiveTelegraph();

            activeTelegraph = Object.Instantiate(
                markStrikeTelegraphPrefab,
                center,
                Quaternion.LookRotation(lineStrikeDirection, Vector3.up));
            activeTelegraph.transform.localScale =
                new Vector3(lineStrikeWidth, 1f, lineStrikeLength);
        }

        private void BeginCircleAttack(
            BossState state,
            Vector3 position,
            float castTime,
            float radius,
            FallenCommanderAttackData motion)
        {
            position.y += 0.02f;
            markStrikePosition = position;
            stateTimeRemaining = castTime;
            currentState = state;
            PauseBossTracking();
            animationPresenter.Play(motion?.PreCastMotion);
            DestroyActiveTelegraph();

            activeTelegraph = Object.Instantiate(
                markStrikeTelegraphPrefab,
                position,
                Quaternion.identity);
            var scale = activeTelegraph.transform.localScale;
            scale.x = radius * 2f;
            scale.z = radius * 2f;
            activeTelegraph.transform.localScale = scale;
        }

        private void TickAttack(float deltaTime)
        {
            stateTimeRemaining =
                Mathf.Max(0f, stateTimeRemaining - deltaTime);

            if (stateTimeRemaining > 0f)
            {
                return;
            }

            if (IsCommanderInsideCurrentAttack())
            {
                var stunDuration = GetCurrentStunDuration();
                if (stunDuration > 0f)
                {
                    LockCommanderStun(stunDuration);
                }

                combatWorld.AttackDamageable(
                    bossActor,
                    commanderHealth,
                    bossActor.EffectiveStats);

                if (!isActive)
                {
                    return;
                }
            }

            var motion = GetCurrentMotion();
            animationPresenter.Play(
                motion?.CastMotion,
                stopAfterMotion: true,
                durationOverride: motion == null ? 0f : motion.CastMotionDuration);

            DestroyActiveTelegraph();
            attackCooldownRemaining = attackInterval;
            currentState = BossState.Idle;
            ResumeBossTracking();
        }

        private bool IsCommanderInsideCurrentAttack()
        {
            if (currentState == BossState.LineStrike)
            {
                var offset = commanderRoot.position - bossActor.transform.position;
                offset.y = 0f;
                var forwardDistance = Vector3.Dot(offset, lineStrikeDirection);
                var sideDistance = Mathf.Abs(Vector3.Dot(
                    offset,
                    Vector3.Cross(Vector3.up, lineStrikeDirection)));
                return forwardDistance >= 0f &&
                    forwardDistance <= lineStrikeLength &&
                    sideDistance <= lineStrikeWidth * 0.5f;
            }

            var radius = currentState == BossState.HandSlam
                ? basicAttackRadius
                : currentState == BossState.WideBurst
                    ? wideBurstRadius
                    : markStrikeRadius;
            return IsCommanderInsideCircle(radius);
        }

        private float GetCurrentStunDuration()
        {
            return currentState == BossState.MarkStrike
                ? markStrikeStunDuration
                : currentState == BossState.WideBurst
                    ? wideBurstStunDuration
                    : currentState == BossState.LineStrike
                        ? lineStrikeStunDuration
                        : 0f;
        }

        private FallenCommanderAttackData GetCurrentMotion()
        {
            return currentState == BossState.HandSlam
                ? basicAttackMotion
                : currentState == BossState.MarkStrike
                    ? markStrikeMotion
                    : currentState == BossState.WideBurst
                        ? wideBurstMotion
                        : lineStrikeMotion;
        }

        private void PauseBossTracking()
        {
            bossFacingSmoother.SetTrackingEnabled(false);
            bossActor.ForceTarget(
                bossActor.Health,
                float.PositiveInfinity);
        }

        private void ResumeBossTracking()
        {
            bossActor.ForceTarget(
                commanderHealth,
                float.PositiveInfinity);
            bossFacingSmoother.SetTrackingEnabled(true);
        }

        private void LockCommanderStun(float duration)
        {
            commanderStunRemaining = Mathf.Max(
                commanderStunRemaining,
                Mathf.Max(0.1f, duration));

            if (isCommanderStunned)
            {
                return;
            }

            isCommanderStunned = true;
            commanderStunChanged?.Invoke(true);
        }

        private void TickCommanderStun(float deltaTime)
        {
            if (!isCommanderStunned)
            {
                return;
            }

            commanderStunRemaining = Mathf.Max(
                0f,
                commanderStunRemaining - Mathf.Max(0f, deltaTime));

            if (commanderStunRemaining > 0f)
            {
                return;
            }

            ReleaseCommanderStun();
        }

        private void ReleaseCommanderStun()
        {
            commanderStunRemaining = 0f;

            if (!isCommanderStunned)
            {
                return;
            }

            isCommanderStunned = false;
            commanderStunChanged?.Invoke(false);
        }

        // 범위 안에 있는지 검사
        private bool IsCommanderInsideMarkStrike()
        {
            return IsCommanderInsideCircle(markStrikeRadius);
        }

        private bool IsCommanderInsideCircle(float radius)
        {
            var offset = commanderRoot.position - markStrikePosition;
            offset.y = 0f;

            return offset.sqrMagnitude <=
                radius * radius;
        }

        private void DestroyActiveTelegraph()
        {
            if (activeTelegraph == null)
            {
                return;
            }

            Object.Destroy(activeTelegraph);
            activeTelegraph = null;
        }
    }
}
