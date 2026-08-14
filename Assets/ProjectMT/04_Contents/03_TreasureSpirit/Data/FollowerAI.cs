using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using ProjectMT.Shared.Unit; // SO 프로필 클래스 사용

namespace ProjectMT.Contents.TreasureSpirit
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class FollowerAI : MonoBehaviour
    {
        public enum State
        {
            FollowCommander, // 군단장 추적
            ChaseEnemy,      // 경비병(적) 추적
            AttackEnemy      // 경비병 공격
        }

        [Header("상태 관찰")]
        [SerializeField] private State currentState = State.FollowCommander;

        [Header("감지 및 거리 설정")]
        [SerializeField] private float detectEnemyRange = 6.0f;     // 적 감지 거리
        [SerializeField] private float attackRange = 1.5f;          // 공격 가능 거리 (SO 기반)
        [SerializeField] private float followOffsetDistance = 1.0f; // 군단장과의 유지 거리

        [Header("전투 및 이동 스탯 (SO 기반)")]
        [SerializeField] private float baseMoveSpeed = 3.5f;        // 기본 이동 속도 (MD)
        [SerializeField] private float attackDamage = 20f;          // 공격력 (MD)
        [SerializeField] private float attackCooldown = 1.0f;        // 공격 쿨타임 (MD AttackSpeed 기반)
        private float lastAttackTime;

        [Header("타겟 참조")]
        [SerializeField] private Transform commander;
        [SerializeField] private GuardAI targetGuard;

        [Header("SO 프로필 데이터")]
        private MonsterDefinition definition;
        private MonsterRuntimeAssetSet runtimeAssetSet;

        private NavMeshAgent agent;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();

            if (agent != null)
            {
                agent.updateRotation = true;
                agent.updatePosition = true;
            }
        }

        /// <summary>
        /// 스포너로부터 군단장 Transform과 SO 데이터 세트를 받아 동적으로 초기화합니다.
        /// </summary>
        public void Initialize(Transform commanderTransform, MonsterDefinition monsterDef, MonsterRuntimeAssetSet runtimeSet)
        {
            commander = commanderTransform;
            definition = monsterDef;
            runtimeAssetSet = runtimeSet;

            // SO 데이터 스탯 반영
            ApplyStatsFromProfile();
        }

        /// <summary>
        /// SO(MonsterDefinition, MonsterCombatProfile)의 public 프로퍼티 수치를 AI 스탯에 바인딩합니다.
        /// </summary>
        private void ApplyStatsFromProfile()
        {
            // 1. MD(MonsterDefinition) Public 프로퍼티 반영
            if (definition != null)
            {
                baseMoveSpeed = definition.MoveSpeed;   // public 프로퍼티
                attackDamage = definition.AttackPower; // public 프로퍼티
                attackRange = definition.AttackRange;   // public 프로퍼티

                if (definition.AttackSpeed > 0)
                {
                    attackCooldown = 1.0f / definition.AttackSpeed;
                }

                if (agent != null)
                {
                    agent.speed = baseMoveSpeed;
                }
            }

            // 2. MR(MonsterRuntimeAssetSet) 프로퍼티 참조
            MonsterRuntimeAssetSet targetRuntimeSet = runtimeAssetSet;
            if (targetRuntimeSet == null && definition != null)
            {
                targetRuntimeSet = definition.RuntimeAssetSet;
            }

            if (targetRuntimeSet != null && targetRuntimeSet.CombatProfile != null)
            {
                MonsterCombatProfile combatProfile = targetRuntimeSet.CombatProfile;
                if (combatProfile.Action is MeleeActionDefinition meleeAction)
                {
                    if (meleeAction.AreaRadius > 0)
                    {
                        attackRange = meleeAction.AreaRadius;
                    }
                }
            }
        }

        private void Update()
        {
            if (commander == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    commander = playerObj.transform;
                }
                else
                {
                    return;
                }
            }

            // 1. 주변 경비병(GuardAI) 탐색
            FindNearestGuard();

            // 2. 상태별 행동
            if (targetGuard != null)
            {
                float distanceToGuard = Vector3.Distance(transform.position, targetGuard.transform.position);

                if (distanceToGuard <= attackRange)
                {
                    currentState = State.AttackEnemy;
                    AttackBehavior();
                }
                else
                {
                    currentState = State.ChaseEnemy;
                    ChaseGuardBehavior();
                }
            }
            else
            {
                currentState = State.FollowCommander;
                FollowCommanderBehavior();
            }
        }

        private void FindNearestGuard()
        {
#pragma warning disable CS0618
            GuardAI[] guards = FindObjectsOfType<GuardAI>();
#pragma warning restore CS0618

            GuardAI nearest = null;
            float minDistance = detectEnemyRange;

            foreach (var guard in guards)
            {
                float distance = Vector3.Distance(transform.position, guard.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = guard;
                }
            }

            targetGuard = nearest;
        }

        private Vector3 lastCommanderPosition;

        private void FollowCommanderBehavior()
        {
            if (commander == null || agent.pathPending) return;

            float distanceToCommander = Vector3.Distance(transform.position, commander.position);

            if (distanceToCommander > followOffsetDistance)
            {
                agent.isStopped = false;

                // 군단장과 멀어지면 부스터 속도(기본 이동 속도의 1.4배 적용)
                if (distanceToCommander > 4.0f)
                {
                    agent.speed = baseMoveSpeed * 1.4f;
                }
                else
                {
                    agent.speed = baseMoveSpeed;
                }

                if (Vector3.SqrMagnitude(commander.position - lastCommanderPosition) > 0.1f)
                {
                    agent.SetDestination(commander.position);
                    lastCommanderPosition = commander.position;
                }
            }
            else
            {
                if (!agent.isStopped)
                {
                    agent.isStopped = true;
                }
            }
        }

        private void ChaseGuardBehavior()
        {
            if (targetGuard == null) return;

            agent.isStopped = false;
            agent.speed = baseMoveSpeed;
            agent.SetDestination(targetGuard.transform.position);
        }

        private void AttackBehavior()
        {
            agent.isStopped = true;

            if (targetGuard != null)
            {
                Vector3 lookDir = targetGuard.transform.position - transform.position;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
                }
            }

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                // TODO: 나중에 애니메이션 추가 시 Animator 파라미터 Trigger("Attack") 재연결 위치

                if (targetGuard != null)
                {
                    targetGuard.TakeDamage(attackDamage);
                    Debug.Log($"⚔️ 팔로워가 {targetGuard.gameObject.name}을(를) 공격했습니다! (적용 데미지: {attackDamage})");
                }

                lastAttackTime = Time.time;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, detectEnemyRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}