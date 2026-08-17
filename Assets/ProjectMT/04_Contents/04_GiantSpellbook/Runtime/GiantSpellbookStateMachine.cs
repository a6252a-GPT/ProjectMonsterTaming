using System.Collections.Generic;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.GiantSpellbook
{
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

        private CombatWorld combatWorld;
        private UnitActor bossActor;
        private Transform commanderRoot;
        private List<UnitActor> followerActors;
        private BossState currentState = BossState.Idle;
        private float stateRemainingTime;
        private float attackCooldownRemaining;
        private float handSlamCooldownRemaining;
        private float markStrikeCooldownRemaining;
        private int normalAttackCount;
        private bool isActive;
        private bool isWideBurstLockingFollowers;
        private UnitActor singleTarget;
        private Vector3 attackPosition;
        private GameObject telegraphObject;

        private float attackInterval;
        private float handSlamRange;
        private float handSlamCooldown;
        private float handSlamCastTime;
        private float handSlamRadius;
        private float markStrikeCooldown;
        private float markStrikeCastTime;
        private float markStrikeRadius;
        private float wideBurstCastTime;
        private float wideBurstRadius;
        private int normalAttacksBeforeWide;
        private float damage;

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
            float strikeCooldown,
            float strikeCastTime,
            float strikeRadius,
            float burstCastTime,
            float burstRadius,
            int attacksBeforeWide,
            float attackDamage)
        {
            Shutdown();

            combatWorld = world;
            bossActor = boss;
            commanderRoot = commander;
            followerActors = followers;
            attackInterval = Mathf.Max(0.1f, interval);
            handSlamRange = Mathf.Max(0.1f, slamRange);
            handSlamCooldown = Mathf.Max(0.1f, slamCooldown);
            handSlamCastTime = Mathf.Max(0.1f, slamCastTime);
            handSlamRadius = Mathf.Max(0.1f, slamRadius);
            markStrikeCooldown = Mathf.Max(0.1f, strikeCooldown);
            markStrikeCastTime = Mathf.Max(0.1f, strikeCastTime);
            markStrikeRadius = Mathf.Max(0.1f, strikeRadius);
            wideBurstCastTime = Mathf.Max(0.1f, burstCastTime);
            wideBurstRadius = Mathf.Max(0.1f, burstRadius);
            normalAttacksBeforeWide = Mathf.Max(1, attacksBeforeWide);
            damage = Mathf.Max(0.01f, attackDamage);
            attackCooldownRemaining = attackInterval;
            currentState = BossState.Idle;
            isActive = combatWorld != null && bossActor != null && followerActors != null;
        }

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

            deltaTime = Mathf.Max(0f, deltaTime);

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

        public void EnterBroken()
        {
            if (!isActive || currentState == BossState.Broken)
            {
                return;
            }

            ClearAttackRuntime();
            ChangeState(BossState.Broken);
        }

        public void ExitBroken()
        {
            if (!isActive || currentState != BossState.Broken)
            {
                return;
            }

            attackCooldownRemaining = attackInterval;
            ChangeState(BossState.Idle);
        }

        public void Shutdown()
        {
            ClearAttackRuntime();
            combatWorld = null;
            bossActor = null;
            commanderRoot = null;
            followerActors = null;
            areaTargets.Clear();
            attackCooldownRemaining = 0f;
            handSlamCooldownRemaining = 0f;
            markStrikeCooldownRemaining = 0f;
            normalAttackCount = 0;
            currentState = BossState.Idle;
            isActive = false;
        }

        private void TickIdle(float deltaTime)
        {
            attackCooldownRemaining = Mathf.Max(0f, attackCooldownRemaining - deltaTime);
            handSlamCooldownRemaining = Mathf.Max(0f, handSlamCooldownRemaining - deltaTime);
            markStrikeCooldownRemaining = Mathf.Max(0f, markStrikeCooldownRemaining - deltaTime);

            if (attackCooldownRemaining > 0f)
            {
                return;
            }

            var nextState = SelectNextState();
            if (nextState != BossState.Idle)
            {
                ChangeState(nextState);
            }
        }

        private void TickAttack(float deltaTime)
        {
            stateRemainingTime -= deltaTime;
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
                    ApplyAreaDamage(attackPosition, handSlamRadius);
                    handSlamCooldownRemaining = handSlamCooldown;
                    FinishNormalAttack();
                    break;
                case BossState.MarkStrike:
                    ApplyAreaDamage(attackPosition, markStrikeRadius);
                    markStrikeCooldownRemaining = markStrikeCooldown;
                    FinishNormalAttack();
                    break;
                case BossState.WideBurst:
                    ApplyAreaDamage(GetBossPosition(), wideBurstRadius);
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
        }

        private void BeginHandSlam()
        {
            stateRemainingTime = handSlamCastTime;
            attackPosition = GetNearestFollowerPosition();
            CreateTelegraph(attackPosition, handSlamRadius, new Color(1f, 0.35f, 0.1f, 0.65f));
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
            CreateTelegraph(attackPosition, wideBurstRadius, new Color(0.8f, 0.2f, 1f, 0.65f));
            LockFollowers();
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
            ReleaseFollowers();
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

        private void ApplyAreaDamage(Vector3 center, float radius)
        {
            areaTargets.Clear();
            combatWorld.CollectUnits(UnitTeam.Player, center, radius, 64, areaTargets);
            for (var i = 0; i < areaTargets.Count; i++)
            {
                var target = areaTargets[i];
                if (target != null && target.IsAlive)
                {
                    combatWorld.ApplyMonsterDamage(bossActor, target.Health, damage);
                }
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

        private void CreateTelegraph(Vector3 position, float radius, Color color)
        {
            DestroyTelegraph();
            telegraphObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            telegraphObject.name = "GiantSpellbookAttackTelegraph_Runtime";
            telegraphObject.transform.position = new Vector3(position.x, position.y + 0.03f, position.z);
            telegraphObject.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            Object.Destroy(telegraphObject.GetComponent<Collider>());

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                return;
            }

            var renderer = telegraphObject.GetComponent<Renderer>();
            renderer.material = new Material(shader);
            renderer.material.color = color;
        }

        private void DestroyTelegraph()
        {
            if (telegraphObject == null)
            {
                return;
            }

            var renderer = telegraphObject.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                Object.Destroy(renderer.material);
            }

            Object.Destroy(telegraphObject);
            telegraphObject = null;
        }
    }
}
