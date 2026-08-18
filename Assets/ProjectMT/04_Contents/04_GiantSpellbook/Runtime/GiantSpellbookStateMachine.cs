using System;
using System.Collections.Generic;
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

        private readonly List<UnitActor> areaTargets = new();
        private readonly List<UnitActor> stunnedFollowers = new();

        private CombatWorld combatWorld; // 공격과 유닛 관리 담당 CombatWorld
        private UnitActor bossActor; // 보스 유닛
        private Transform commanderRoot; // 군단장 위치
        private List<UnitActor> followerActors; // 군단장 추종 유닛들
        private BossState currentState = BossState.Idle;
        private float stateRemainingTime; // 현재 상태에서 남은 시간
        private float attackCooldownRemaining; // 다음 공격까지 남은 시간
        private float handSlamCooldownRemaining;
        private float markStrikeCooldownRemaining;
        private int normalAttackCount; // 일반 공격 횟수
        private bool isActive; // 상태 머신 활성화 여부
        private bool isWideBurstLockingFollowers; // WideBurst 공격 중 추종 유닛 위치 고정 여부
        private UnitActor singleTarget;
        private Vector3 attackPosition; // 공격이 발생할 위치
        private GameObject telegraphObject; // 공격 범위 표시용 오브젝트
        private float telegraphBaseDiameter;
        private GameObject attackTelegraphPrefab;
        private Action<bool> wideBurstMovementLockChanged;

        private float attackInterval;
        private float handSlamRange;
        private float handSlamCooldown;
        private float handSlamCastTime;
        private float handSlamRadius;
        private float handSlamStunDuration;
        private float followerStunRemaining;
        private float markStrikeCooldown;
        private float markStrikeCastTime;
        private float markStrikeRadius;
        private float wideBurstCastTime;
        private float wideBurstStartRadius;
        private float wideBurstRadius;
        private float wideBurstStunDuration;
        private float wideBurstStunRemaining;
        private int normalAttacksBeforeWide;
        private float damage;

        //FSM에 필요한 설정값들을 초기화하는 메서드
        public void Configure(
            CombatWorld world,
            UnitActor boss,
            Transform commander,
            List<UnitActor> followers,
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
            float attackDamage,
            GameObject telegraphPrefab,
            Action<bool> movementLockChanged)
        {
            Shutdown();

            combatWorld = world;
            bossActor = boss;
            commanderRoot = commander;
            followerActors = followers;

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
            damage = Mathf.Max(0.01f, attackDamage);
            attackTelegraphPrefab = telegraphPrefab;
            wideBurstMovementLockChanged = movementLockChanged;
            attackCooldownRemaining = attackInterval;
            currentState = BossState.Idle;
            isActive = combatWorld != null && bossActor != null && followerActors != null;
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
            TickFollowerStun(deltaTime);
            TickWideBurstStun(deltaTime);

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
            wideBurstStunRemaining = 0f;
            wideBurstMovementLockChanged?.Invoke(false);
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
            ReleaseStunnedFollowers();
            wideBurstMovementLockChanged?.Invoke(false);
            combatWorld = null;
            bossActor = null;
            commanderRoot = null;
            followerActors = null;
            attackTelegraphPrefab = null;
            areaTargets.Clear();
            attackCooldownRemaining = 0f;
            handSlamCooldownRemaining = 0f;
            markStrikeCooldownRemaining = 0f;
            wideBurstStunRemaining = 0f;
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
            ReleaseStunnedFollowers();
            wideBurstStunRemaining = 0f;
            wideBurstMovementLockChanged?.Invoke(false);
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
            if (currentState == BossState.BasicAttack &&
                singleTarget != null &&
                singleTarget.IsAlive)
            {
                attackPosition = singleTarget.transform.position;
                if (telegraphObject != null)
                {
                    telegraphObject.transform.position = new Vector3(
                        attackPosition.x,
                        attackPosition.y + 0.03f,
                        attackPosition.z);
                }
            }
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
                    ApplySingleTargetDamage(singleTarget);
                    FinishNormalAttack();
                    break;
                case BossState.HandSlam:
                    ApplyAreaDamage(attackPosition, handSlamRadius, true);
                    handSlamCooldownRemaining = handSlamCooldown;
                    FinishNormalAttack();
                    break;
                case BossState.MarkStrike:
                    ApplyAreaDamage(attackPosition, markStrikeRadius);
                    markStrikeCooldownRemaining = markStrikeCooldown;
                    FinishNormalAttack();
                    break;
                case BossState.WideBurst:
                    var burstPosition = GetBossPosition();
                    var commanderHit = commanderRoot != null &&
                        IsPositionInArea(commanderRoot.position, burstPosition, wideBurstRadius);
                    ApplyAreaDamage(
                        burstPosition,
                        wideBurstRadius,
                        !commanderHit,
                        wideBurstStunDuration);
                    if (commanderHit)
                    {
                        LockFollowers();
                        wideBurstStunRemaining = wideBurstStunDuration;
                        wideBurstMovementLockChanged?.Invoke(true);
                    }
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
            singleTarget = FindNearestFollower();
            attackPosition = singleTarget != null ? singleTarget.transform.position : GetBossPosition();
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
            singleTarget = FindNearestFollower();
            attackPosition = singleTarget != null ? singleTarget.transform.position : GetBossPosition();
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
            if (wideBurstStunRemaining <= 0f)
            {
                ReleaseFollowers();
            }
            stateRemainingTime = 0f;
            attackCooldownRemaining = attackInterval;
            singleTarget = null;
            ChangeState(BossState.Idle);
        }

        private void ClearAttackRuntime()
        {
            DestroyTelegraph();
            ReleaseFollowers();
            stateRemainingTime = 0f;
            singleTarget = null;
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

        private UnitActor FindNearestFollower()
        {
            if (followerActors == null || bossActor == null)
            {
                return null;
            }

            UnitActor nearest = null;
            var nearestDistance = float.PositiveInfinity;
            for (var i = 0; i < followerActors.Count; i++)
            {
                var candidate = followerActors[i];
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                var distance = (candidate.transform.position - GetBossPosition()).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private Vector3 GetNearestFollowerPosition()
        {
            var target = FindNearestFollower();
            return target != null ? target.transform.position : GetBossPosition();
        }

        private Vector3 GetBossPosition()
        {
            return bossActor != null ? bossActor.transform.position : Vector3.zero;
        }

        private void ApplySingleTargetDamage(UnitActor target)
        {
            if (target != null && target.IsAlive)
            {
                combatWorld.ApplyMonsterDamage(bossActor, target.Health, damage);
            }
        }

        private bool ApplyAreaDamage(
            Vector3 center,
            float radius,
            bool applyStun = false,
            float stunDuration = 0f)
        {
            areaTargets.Clear();
            combatWorld.CollectUnits(UnitTeam.Player, center, radius, 64, areaTargets);
            var hitAnyTarget = false;
            for (var i = 0; i < areaTargets.Count; i++)
            {
                var target = areaTargets[i];
                if (target != null && target.IsAlive)
                {
                    hitAnyTarget = true;
                    combatWorld.ApplyMonsterDamage(bossActor, target.Health, damage);
                    if (applyStun)
                    {
                        StunFollower(target, stunDuration);
                    }
                }
            }

            return hitAnyTarget;
        }

        private void StunFollower(UnitActor follower, float duration = 0f)
        {
            if (follower == null || !follower.IsAlive || followerActors == null || !followerActors.Contains(follower))
            {
                return;
            }

            if (!stunnedFollowers.Contains(follower))
            {
                if (!follower.BeginManualReposition())
                {
                    return;
                }

                stunnedFollowers.Add(follower);
            }

            var stunTime = duration > 0f ? duration : handSlamStunDuration;
            followerStunRemaining = Mathf.Max(followerStunRemaining, stunTime);
        }

        private void TickFollowerStun(float deltaTime)
        {
            if (stunnedFollowers.Count == 0)
            {
                return;
            }

            followerStunRemaining -= deltaTime;
            if (followerStunRemaining > 0f)
            {
                return;
            }

            ReleaseStunnedFollowers();
        }

        private void ReleaseStunnedFollowers()
        {
            for (var i = 0; i < stunnedFollowers.Count; i++)
            {
                stunnedFollowers[i]?.EndManualReposition();
            }

            stunnedFollowers.Clear();
            followerStunRemaining = 0f;
        }

        private void TickWideBurstStun(float deltaTime)
        {
            if (wideBurstStunRemaining <= 0f)
            {
                return;
            }

            wideBurstStunRemaining -= deltaTime;
            if (wideBurstStunRemaining <= 0f)
            {
                ReleaseFollowers();
                wideBurstMovementLockChanged?.Invoke(false);
            }
        }

        private void LockFollowers()
        {
            if (isWideBurstLockingFollowers || followerActors == null)
            {
                return;
            }

            for (var i = 0; i < followerActors.Count; i++)
            {
                followerActors[i]?.BeginManualReposition();
            }

            isWideBurstLockingFollowers = true;
        }

        private void ReleaseFollowers()
        {
            if (!isWideBurstLockingFollowers || followerActors == null)
            {
                return;
            }

            for (var i = 0; i < followerActors.Count; i++)
            {
                followerActors[i]?.EndManualReposition();
            }

            isWideBurstLockingFollowers = false;
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
