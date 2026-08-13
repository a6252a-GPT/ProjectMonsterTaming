using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.TreasureSpirit
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class FollowerAI : MonoBehaviour
    {
        // enum 이름을 State로 정의하여 변수 타입과 일치시킴
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
        [SerializeField] private float attackRange = 1.5f;          // 공격 가능 거리
        [SerializeField] private float followOffsetDistance = 1.0f; // 군단장과의 유지 거리

        [Header("전투 설정")]
        [SerializeField] private float attackDamage = 20f;
        [SerializeField] private float attackCooldown = 1.2f;
        private float lastAttackTime;

        [Header("타겟 참조")]
        [SerializeField] private Transform commander;
        [SerializeField] private GuardAI targetGuard;

        private NavMeshAgent agent;
        private Animator animator;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();

            // NavMeshAgent가 자체적으로 회전을 제어하도록 보장
            if (agent != null)
            {
                agent.updateRotation = true;
                agent.updatePosition = true;
            }
        }

        public void Initialize(Transform commanderTransform)
        {
            commander = commanderTransform;
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
                    return; // 군단장을 못 찾으면 리턴
                }
            }

            if (animator != null)
            {
                animator.SetFloat("Speed", agent.velocity.magnitude);
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

        // 가장 가까운 경비병 탐색
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

        private Vector3 lastCommanderPosition; // 군단장 이전 위치 기록용

        private void FollowCommanderBehavior()
        {
            if (commander == null || agent.pathPending) return;

            float distanceToCommander = Vector3.Distance(transform.position, commander.position);

            if (distanceToCommander > followOffsetDistance)
            {
                agent.isStopped = false;

                // 군단장과의 거리가 멀어지면 순간 속도를 높여 바로 따라잡음
                if (distanceToCommander > 4.0f)
                {
                    agent.speed = 7.0f; // 부스터 속도
                }
                else
                {
                    agent.speed = 5.5f; // 기본 이동 속도
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

        // 경비병 추적
        private void ChaseGuardBehavior()
        {
            if (targetGuard == null) return;

            agent.isStopped = false;
            agent.SetDestination(targetGuard.transform.position);
        }

        // 경비병 공격
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
                if (animator != null)
                {
                    animator.SetTrigger("Attack");
                }

                if (targetGuard != null)
                {
                    targetGuard.TakeDamage(attackDamage);
                    Debug.Log($"⚔️ 팔로워가 {targetGuard.gameObject.name}을(를) 공격했습니다!");
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