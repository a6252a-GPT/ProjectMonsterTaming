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
            Broken,
            Dead
        }

        private CombatWorld combatWorld;
        private UnitActor bossActor;
        private Transform commanderRoot;
        private HealthComponent commanderHealth;
        private FallenCommanderBossFacingSmoother bossFacingSmoother;

        private BossState currentState;
        private float attackInterval;
        private float attackCooldownRemaining;
        private float markStrikeStunDuration;
        private float commanderStunRemaining;
        private System.Action<bool> commanderStunChanged;
        private bool isCommanderStunned;
        private bool isActive;

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

        public void Configure(
            CombatWorld world,
            UnitActor boss,
            Transform commander,
            HealthComponent commanderTarget,
            float interval,
            GameObject telegraphPrefab,
            float castTime,
            float radius,
            float stunDuration,
            System.Action<bool> stunChanged,
            FallenCommanderBossFacingSmoother facingSmoother)
        {
            Shutdown();

            combatWorld = world;
            bossActor = boss;
            commanderRoot = commander;
            commanderHealth = commanderTarget;
            bossFacingSmoother = facingSmoother;

            // 0초나 음수가 되는 것을 막기
            attackInterval = Mathf.Max(0.1f, interval);
            markStrikeTelegraphPrefab = telegraphPrefab;
            markStrikeCastTime = Mathf.Max(0.1f, castTime);
            markStrikeRadius = Mathf.Max(0.1f, radius);
            markStrikeStunDuration = Mathf.Max(0.1f, stunDuration);
            commanderStunChanged = stunChanged;

            attackCooldownRemaining = attackInterval;
            currentState = BossState.Idle;

            isActive =
                combatWorld != null &&
                bossActor != null &&
                commanderRoot != null &&
                commanderHealth != null &&
                markStrikeTelegraphPrefab != null &&
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
                    break;

                case BossState.MarkStrike:
                    TickMarkStrike(deltaTime);
                    break;

                case BossState.WideBurst:
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
            bossActor.ForceTarget(
                commanderHealth,
                float.PositiveInfinity);
            bossFacingSmoother.SetTrackingEnabled(true);
        }

        public void Shutdown()
        {
            DestroyActiveTelegraph();
            ReleaseCommanderStun();

            isActive = false;
            combatWorld = null;
            bossActor = null;
            commanderRoot = null;
            commanderHealth = null;
            bossFacingSmoother = null;
            markStrikeTelegraphPrefab = null;
            commanderStunChanged = null;
            attackCooldownRemaining = 0f;
            commanderStunRemaining = 0f;
            markStrikeStunDuration = 0f;
            stateTimeRemaining = 0f;
            markStrikePosition = Vector3.zero;
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

            BeginMarkStrike();
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
            bossFacingSmoother.SetTrackingEnabled(false);

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

        private void TickMarkStrike(float deltaTime)
        {
            stateTimeRemaining =
                Mathf.Max(0f, stateTimeRemaining - deltaTime);

            if (stateTimeRemaining > 0f)
            {
                return;
            }

            if (IsCommanderInsideMarkStrike())
            {
                LockCommanderStun(markStrikeStunDuration);
                combatWorld.AttackDamageable(
                    bossActor,
                    commanderHealth,
                    bossActor.EffectiveStats);

                if (!isActive)
                {
                    return;
                }
            }

            DestroyActiveTelegraph();
            attackCooldownRemaining = attackInterval;
            currentState = BossState.Idle;
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
            var offset = commanderRoot.position - markStrikePosition;
            offset.y = 0f;

            return offset.sqrMagnitude <=
                markStrikeRadius * markStrikeRadius;
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
