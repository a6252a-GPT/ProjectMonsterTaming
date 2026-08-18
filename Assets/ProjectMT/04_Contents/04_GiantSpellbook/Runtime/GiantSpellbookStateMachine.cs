using System;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectMT.Contents.GiantSpellbook
{
    public enum GiantSpellbookDebugAttack
    {
        BasicAttack,
        HandSlam,
        MarkStrike,
        WideBurst
    }

    public sealed class GiantSpellbookStateMachine
    {
        private enum BossState
        {
            Idle,
            BasicAttack,
            HandSlam,
            MarkStrike,
            WideBurst,
            Broken,
            Dead
        }

        private CombatWorld combatWorld; // 공격과 유닛 관리 담당 CombatWorld
        private UnitActor bossActor; // 보스 유닛
        private Transform commanderRoot; // 군단장 위치
        private BossState currentState = BossState.Idle;
        private float stateRemainingTime; // 현재 상태에서 남은 시간
        private float attackCooldownRemaining; // 다음 공격까지 남은 시간
        private float handSlamCooldownRemaining;
        private float markStrikeCooldownRemaining;
        private int normalAttackCount; // 일반 공격 횟수
        private bool isActive; // 상태 머신 활성화 여부
        private bool isCommanderMovementLocked;
        private float commanderMovementLockRemaining;
        private Vector3 attackPosition; // 공격이 발생할 위치
        private GameObject telegraphObject; // 공격 범위 표시용 오브젝트
        private float telegraphBaseDiameter;
        private GameObject attackTelegraphPrefab;
        private Action<bool> commanderMovementLockChanged;

        private float attackInterval;
        private float handSlamRange;
        private float handSlamCooldown;
        private float handSlamCastTime;
        private float handSlamRadius;
        private float handSlamStunDuration;
        private float markStrikeCooldown;
        private float markStrikeCastTime;
        private float markStrikeRadius;
        private float wideBurstCastTime;
        private float wideBurstStartRadius;
        private float wideBurstRadius;
        private float wideBurstStunDuration;
        private int normalAttacksBeforeWide;

        //FSM에 필요한 설정값들을 초기화하는 메서드
        public void Configure(
            CombatWorld world,
            UnitActor boss,
            Transform commander,
            float interval,
            float slamRange,
            float slamCooldown,
            float slamCastTime,
            float slamRadius,
            float slamStunDuration,
            float strikeCooldown,
            float strikeCastTime,
            float strikeRadius,
            float burstCastTime,
            float burstStartRadius,
            float burstRadius,
            float burstStunDuration,
            int attacksBeforeWide,
            GameObject telegraphPrefab,
            Action<bool> movementLockChanged)
        {
            Shutdown();

            combatWorld = world;
            bossActor = boss;
            commanderRoot = commander;

            // 간격이 0초가 되지 않도록 최소값을 설정
            attackInterval = Mathf.Max(0.1f, interval);
            handSlamRange = Mathf.Max(0.1f, slamRange);
            handSlamCooldown = Mathf.Max(0.1f, slamCooldown);
            handSlamCastTime = Mathf.Max(0.1f, slamCastTime);
            handSlamRadius = Mathf.Max(1.2f, slamRadius);
            handSlamStunDuration = Mathf.Max(0.1f, slamStunDuration);
            markStrikeCooldown = Mathf.Max(0.1f, strikeCooldown);
            markStrikeCastTime = Mathf.Max(0.1f, strikeCastTime);
            markStrikeRadius = Mathf.Max(0.1f, strikeRadius);
            wideBurstCastTime = Mathf.Max(0.1f, burstCastTime);
            wideBurstRadius = Mathf.Max(0.1f, burstRadius);
            wideBurstStartRadius = Mathf.Clamp(burstStartRadius, 0.1f, wideBurstRadius);
            wideBurstStunDuration = Mathf.Max(0.1f, burstStunDuration);
            normalAttacksBeforeWide = Mathf.Max(1, attacksBeforeWide);
            attackTelegraphPrefab = telegraphPrefab;
            commanderMovementLockChanged = movementLockChanged;
            attackCooldownRemaining = attackInterval;
            currentState = BossState.Idle;
            isActive = combatWorld != null && bossActor != null && commanderRoot != null;
        }

        // 상태 머신을 매 프레임마다 업데이트하는 메서드
        public void Tick(float deltaTime, bool isBroken)
        {
            if (!isActive || bossActor == null || !bossActor.IsAlive)
            {
                return;
            }

            if (isBroken)
            {
                EnterBroken();
                return;
            }

            if (currentState == BossState.Broken)
            {
                ExitBroken();
            }

            // deltaTime가 음수가 되지 않도록 보정
            deltaTime = Mathf.Max(0f, deltaTime);
            TickCommanderMovementLock(deltaTime);

            switch (currentState)
            {
                case BossState.Idle:
                    TickIdle(deltaTime);
                    break;
                case BossState.BasicAttack:
                case BossState.HandSlam:
                case BossState.MarkStrike:
                case BossState.WideBurst:
                    TickAttack(deltaTime);
                    break;
            }
        }

        // 브레이크 시작 함수
        public void EnterBroken()
        {
            if (!isActive || currentState == BossState.Broken)
            {
                return;
            }

            ClearAttackRuntime();
            ReleaseCommanderMovementLock();
            ChangeState(BossState.Broken);
        }

        public void ExitBroken()
        {
            if (!isActive || currentState != BossState.Broken)
            {
                return;
            }

            // 브레이크 상태에서 벗어나면 공격 쿨다운을 초기화하고 Idle 상태로 전환
            attackCooldownRemaining = attackInterval;
            ChangeState(BossState.Idle);
        }

        // 상태 머신을 종료하고 리소스를 해제하는 메서드
        // 던전이 종료되거나 나가기 버튼을 눌렀을 때 사용
        public void Shutdown()
        {
            ClearAttackRuntime();
            ReleaseCommanderMovementLock();
            combatWorld = null;
            bossActor = null;
            commanderRoot = null;
            attackTelegraphPrefab = null;
            commanderMovementLockChanged = null;
            attackCooldownRemaining = 0f;
            handSlamCooldownRemaining = 0f;
            markStrikeCooldownRemaining = 0f;
            normalAttackCount = 0;
            currentState = BossState.Idle;
            isActive = false;
        }

        public void DebugForceAttack(GiantSpellbookDebugAttack attack)
        {
            if (!isActive || bossActor == null || !bossActor.IsAlive || currentState == BossState.Broken)
            {
                return;
            }

            ClearAttackRuntime();
            ReleaseCommanderMovementLock();
            attackCooldownRemaining = 0f;

            switch (attack)
            {
                case GiantSpellbookDebugAttack.BasicAttack:
                    ChangeState(BossState.BasicAttack);
                    break;
                case GiantSpellbookDebugAttack.HandSlam:
                    ChangeState(BossState.HandSlam);
                    break;
                case GiantSpellbookDebugAttack.MarkStrike:
                    ChangeState(BossState.MarkStrike);
                    break;
                case GiantSpellbookDebugAttack.WideBurst:
                    ChangeState(BossState.WideBurst);
                    break;
            }
        }

        // 대기 상태에서 실행되는 함수
        private void TickIdle(float deltaTime)
        {

            attackCooldownRemaining = Mathf.Max(0f, attackCooldownRemaining - deltaTime);
            handSlamCooldownRemaining = Mathf.Max(0f, handSlamCooldownRemaining - deltaTime);
            markStrikeCooldownRemaining = Mathf.Max(0f, markStrikeCooldownRemaining - deltaTime);

            if (attackCooldownRemaining > 0f)
            {
                return;
            }

            // 공격 쿨다운이 끝나면 다음 공격 상태를 선택
            var nextState = SelectNextState();
            if (nextState != BossState.Idle)
            {
                ChangeState(nextState);
            }
        }

        // 현재 공격의 시전 시간이 끝났는지 확인하는 함수
        private void TickAttack(float deltaTime)
        {
            // 현재 공격에 남은 시전 시간을 감소
            stateRemainingTime -= deltaTime;

            if (currentState == BossState.WideBurst)
            {
                var progress = 1f - stateRemainingTime / wideBurstCastTime;
                UpdateTelegraphRadius(
                    Mathf.Lerp(
                        wideBurstStartRadius,
                        wideBurstRadius,
                        Mathf.Clamp01(progress)));
            }

            if (stateRemainingTime > 0f)
            {
                return;
            }

            switch (currentState)
            {
                case BossState.BasicAttack:
                    ApplyCommanderHit(attackPosition, 0.6f, 0.35f);
                    FinishNormalAttack();
                    break;
                case BossState.HandSlam:
                    ApplyCommanderHit(attackPosition, handSlamRadius, handSlamStunDuration);
                    handSlamCooldownRemaining = handSlamCooldown;
                    FinishNormalAttack();
                    break;
                case BossState.MarkStrike:
                    ApplyCommanderHit(attackPosition, markStrikeRadius, Mathf.Min(0.75f, handSlamStunDuration));
                    markStrikeCooldownRemaining = markStrikeCooldown;
                    FinishNormalAttack();
                    break;
                case BossState.WideBurst:
                    ApplyCommanderHit(GetBossPosition(), wideBurstRadius, wideBurstStunDuration);
                    normalAttackCount = 0;
                    FinishAttack();
                    break;
            }
        }

        private BossState SelectNextState()
        {
            if (normalAttackCount >= normalAttacksBeforeWide)
            {
                return BossState.WideBurst;
            }

            if (IsHandSlamInRange() && handSlamCooldownRemaining <= 0f)
            {
                return BossState.HandSlam;
            }

            if (markStrikeCooldownRemaining <= 0f)
            {
                return BossState.MarkStrike;
            }

            return BossState.BasicAttack;
        }

        private void BeginBasicAttack()
        {
            stateRemainingTime = 0.35f;
            attackPosition = commanderRoot != null ? commanderRoot.position : GetBossPosition();
            CreateTelegraph(attackPosition, 0.6f, new Color(1f, 0.85f, 0.1f, 0.65f));
        }

        private void BeginHandSlam()
        {
            stateRemainingTime = handSlamCastTime;
            attackPosition = GetRandomHandSlamPosition();
            CreateTelegraph(attackPosition, handSlamRadius, new Color(1f, 0.35f, 0.1f, 0.65f));
        }

        private Vector3 GetRandomHandSlamPosition()
        {
            var side = UnityEngine.Random.Range(0, 2) == 0 ? -1f : 1f;
            var bossTransform = bossActor != null ? bossActor.transform : null;
            if (bossTransform == null)
            {
                return GetBossPosition();
            }

            var sideOffset = bossTransform.right * (side * 2.4f);
            var forwardOffset = bossTransform.forward * 0.35f;
            return bossTransform.position + sideOffset + forwardOffset;
        }

        private void BeginMarkStrike()
        {
            stateRemainingTime = markStrikeCastTime;
            attackPosition = commanderRoot != null ? commanderRoot.position : GetBossPosition();
            CreateTelegraph(attackPosition, markStrikeRadius, new Color(0.35f, 0.75f, 1f, 0.65f));
        }

        private void BeginWideBurst()
        {
            stateRemainingTime = wideBurstCastTime;
            attackPosition = GetBossPosition();
            CreateTelegraph(
                attackPosition,
                wideBurstRadius,
                new Color(0.8f, 0.2f, 1f, 0.65f),
                wideBurstStartRadius);
        }

        private void ChangeState(BossState nextState)
        {
            currentState = nextState;

            switch (nextState)
            {
                case BossState.BasicAttack:
                    BeginBasicAttack();
                    break;
                case BossState.HandSlam:
                    BeginHandSlam();
                    break;
                case BossState.MarkStrike:
                    BeginMarkStrike();
                    break;
                case BossState.WideBurst:
                    BeginWideBurst();
                    break;
            }
        }

        private void FinishNormalAttack()
        {
            normalAttackCount++;
            FinishAttack();
        }

        private void FinishAttack()
        {
            DestroyTelegraph();
            stateRemainingTime = 0f;
            attackCooldownRemaining = attackInterval;
            ChangeState(BossState.Idle);
        }

        private void ClearAttackRuntime()
        {
            DestroyTelegraph();
            stateRemainingTime = 0f;
        }

        private bool IsHandSlamInRange()
        {
            if (commanderRoot == null)
            {
                return false;
            }

            var offset = commanderRoot.position - GetBossPosition();
            offset.y = 0f;
            return offset.sqrMagnitude <= handSlamRange * handSlamRange;
        }

        private bool IsPositionInArea(Vector3 position, Vector3 center, float radius)
        {
            var offset = position - center;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }

        private Vector3 GetBossPosition()
        {
            return bossActor != null ? bossActor.transform.position : Vector3.zero;
        }

        private bool ApplyCommanderHit(Vector3 center, float radius, float movementLockDuration)
        {
            if (commanderRoot == null || !IsPositionInArea(commanderRoot.position, center, radius))
            {
                return false;
            }

            LockCommanderMovement(movementLockDuration);
            return true;
        }

        private void LockCommanderMovement(float duration)
        {
            commanderMovementLockRemaining = Mathf.Max(commanderMovementLockRemaining, Mathf.Max(0.1f, duration));
            if (!isCommanderMovementLocked)
            {
                isCommanderMovementLocked = true;
                commanderMovementLockChanged?.Invoke(true);
            }
        }

        private void TickCommanderMovementLock(float deltaTime)
        {
            if (!isCommanderMovementLocked)
            {
                return;
            }

            commanderMovementLockRemaining -= deltaTime;
            if (commanderMovementLockRemaining > 0f)
            {
                return;
            }

            ReleaseCommanderMovementLock();
        }

        private void ReleaseCommanderMovementLock()
        {
            commanderMovementLockRemaining = 0f;
            if (!isCommanderMovementLocked)
            {
                return;
            }

            isCommanderMovementLocked = false;
            commanderMovementLockChanged?.Invoke(false);
        }

        private void CreateTelegraph(
            Vector3 position,
            float radius,
            Color color,
            float initialRadius = -1f)
        {
            DestroyTelegraph();
            telegraphBaseDiameter = 0f;

            if (attackTelegraphPrefab != null)
            {
                telegraphObject = Object.Instantiate(attackTelegraphPrefab);
            }
            else
            {
                telegraphObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            }

            telegraphObject.name = "GiantSpellbookAttackTelegraph_Runtime";
            telegraphObject.transform.position = new Vector3(position.x, position.y + 0.03f, position.z);
            telegraphObject.transform.localScale = Vector3.one;

            var collider = telegraphObject.GetComponentInChildren<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

            var renderer = telegraphObject.GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                return;
            }

            if (renderer.transform != telegraphObject.transform)
            {
                renderer.transform.localScale = Vector3.one;
            }

            telegraphBaseDiameter = Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z);
            if (telegraphBaseDiameter > 0f)
            {
                UpdateTelegraphRadius(initialRadius > 0f
                    ? Mathf.Min(initialRadius, radius)
                    : radius);
            }

            if (renderer.material == null && shader != null)
            {
                renderer.material = new Material(shader);
            }

            if (renderer.material != null)
            {
                renderer.material.color = color;
            }
        }

        private void DestroyTelegraph()
        {
            if (telegraphObject == null)
            {
                return;
            }

            var renderer = telegraphObject.GetComponentInChildren<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                Object.Destroy(renderer.material);
            }

            Object.Destroy(telegraphObject);
            telegraphObject = null;
            telegraphBaseDiameter = 0f;
        }

        private void UpdateTelegraphRadius(float radius)
        {
            if (telegraphObject == null || telegraphBaseDiameter <= 0f)
            {
                return;
            }

            var normalizedScale = radius * 2f / telegraphBaseDiameter;
            telegraphObject.transform.localScale = new Vector3(
                normalizedScale,
                0.05f,
                normalizedScale);
        }
    }
}
