using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.TreasureSpirit
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class GuardAI : MonoBehaviour
    {
        public enum GuardState
        {
            Patrol,
            Chase,
            Attack,
            Dead
        }

        [Header("상태 정보")]
        [SerializeField] private GuardState currentState = GuardState.Patrol;

        [Header("감지 및 공격 거리")]
        [SerializeField] private float detectionRange = 7.0f;
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float loseTargetRange = 10.0f;

        [Header("속도 설정")]
        [SerializeField] private float patrolSpeed = 2.0f;
        [SerializeField] private float chaseSpeed = 3.8f;

        [Header("공격 설정")]
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private int attackMotionCount = 3;
        private float lastAttackTime;

        [Header("배회 범위 설정")]
        [SerializeField] private float patrolRadius = 8.0f;
        [SerializeField] private float waitAtPatrolPoint = 2.0f;
        private float patrolTimer;
        private Vector3 patrolOrigin;
        private bool usePatrolOrigin;

        [Header("체력 설정")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;
        [SerializeField] private float deathDestroyDelay = 3.0f;
        private bool isDead;

        [Header("타겟 설정")]
        [SerializeField] private Transform commanderTarget; // 기본 군단장(플레이어)
        private Transform currentTarget;                  // 현재 조준 중인 최종 타깃(팔로워 우선)

        private NavMeshAgent agent;
        private Animator animator;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
        }

        private void Start()
        {
            currentHealth = maxHealth;
            agent.speed = patrolSpeed;
            SetRandomPatrolDestination();
        }

        public void SetTargetPlayer(Transform target)
        {
            commanderTarget = target;
        }

        public void SetPatrolBounds(Vector3 center, float radius)
        {
            patrolOrigin = center;
            patrolRadius = Mathf.Clamp(radius, 1f, 6f);
            usePatrolOrigin = true;
        }

        private void Update()
        {
            if (isDead) return;

            if (animator != null)
            {
                animator.SetFloat("Speed", agent.velocity.magnitude);
            }

            // ★ 실시간 타깃 평가 (팔로워 우선 감지)
            EvaluateTarget();

            if (currentTarget == null)
            {
                if (currentState != GuardState.Patrol)
                {
                    currentState = GuardState.Patrol;
                    agent.speed = patrolSpeed;
                    SetRandomPatrolDestination();
                }
                PatrolBehavior();
                return;
            }

            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);

            switch (currentState)
            {
                case GuardState.Patrol:
                    UpdatePatrolState(distanceToTarget);
                    break;

                case GuardState.Chase:
                    UpdateChaseState(distanceToTarget);
                    break;

                case GuardState.Attack:
                    UpdateAttackState(distanceToTarget);
                    break;
            }
        }

        // ★ 팔로워 우선 타깃 탐색 메서드
        private void EvaluateTarget()
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRange);
            Transform closestFollower = null;
            float minFollowerDistance = float.MaxValue;

            foreach (var col in hitColliders)
            {
                bool isFollower = col.GetComponentInParent<FollowerAI>() != null;

                if (isFollower)
                {
                    float dist = Vector3.Distance(transform.position, col.transform.position);
                    if (dist < minFollowerDistance)
                    {
                        minFollowerDistance = dist;
                        closestFollower = col.transform;
                    }
                }
            }

            // 1순위: 감지 범위 내 팔로워가 있다면 팔로워를 타깃으로 설정
            if (closestFollower != null)
            {
                currentTarget = closestFollower;
                return;
            }

            // 2순위: 범위 내 팔로워가 없고 군단장이 있다면 군단장 타깃 지정
            if (commanderTarget != null)
            {
                float distToCommander = Vector3.Distance(transform.position, commanderTarget.position);
                if (distToCommander <= detectionRange || currentState == GuardState.Chase || currentState == GuardState.Attack)
                {
                    currentTarget = commanderTarget;
                    return;
                }
            }

            // 아무도 범위 내에 없으면 타깃 해제
            currentTarget = null;
        }

        private void UpdatePatrolState(float distanceToTarget)
        {
            if (distanceToTarget <= detectionRange)
            {
                currentState = GuardState.Chase;
                agent.speed = chaseSpeed;
                return;
            }

            PatrolBehavior();
        }

        private void UpdateChaseState(float distanceToTarget)
        {
            if (distanceToTarget > loseTargetRange)
            {
                currentState = GuardState.Patrol;
                agent.speed = patrolSpeed;
                SetRandomPatrolDestination();
                return;
            }

            if (distanceToTarget <= attackRange)
            {
                currentState = GuardState.Attack;
                agent.isStopped = true;
                return;
            }

            agent.isStopped = false;
            agent.SetDestination(currentTarget.position);
        }

        private void UpdateAttackState(float distanceToTarget)
        {
            if (distanceToTarget > attackRange)
            {
                currentState = GuardState.Chase;
                agent.isStopped = false;
                return;
            }

            Vector3 lookDir = currentTarget.position - transform.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
            }

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                PerformAttack();
                lastAttackTime = Time.time;
            }
        }

        private void PatrolBehavior()
        {
            if (agent.pathPending) return;

            if (agent.remainingDistance <= agent.stoppingDistance)
            {
                patrolTimer += Time.deltaTime;
                if (patrolTimer >= waitAtPatrolPoint)
                {
                    SetRandomPatrolDestination();
                    patrolTimer = 0f;
                }
            }
        }

        private void SetRandomPatrolDestination()
        {
            Vector3 origin = usePatrolOrigin ? patrolOrigin : transform.position;
            Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
            randomDirection.y = 0f;
            randomDirection += origin;

            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, patrolRadius * 0.5f, NavMesh.AllAreas))
            {
                if (usePatrolOrigin && GetHorizontalDistance(hit.position, patrolOrigin) > patrolRadius)
                {
                    return;
                }

                agent.SetDestination(hit.position);
            }
        }

        private static float GetHorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void PerformAttack()
        {
            if (animator != null)
            {
                int randomAttackIndex = Random.Range(0, attackMotionCount);
                animator.SetInteger("AttackIndex", randomAttackIndex);
                animator.SetTrigger("Attack");
            }

            string targetName = currentTarget != null ? currentTarget.name : "타겟";
            Debug.Log($"⚔️ {gameObject.name}이(가) [{targetName}]을(를) 공격했습니다!");
        }

        public void TakeDamage(float damage)
        {
            if (isDead) return;

            currentHealth -= damage;
            Debug.Log($"🛡️ {gameObject.name} 체력 감소: {currentHealth}/{maxHealth}");

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            isDead = true;
            currentState = GuardState.Dead;

            agent.isStopped = true;
            agent.enabled = false;

            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }

            if (animator != null)
            {
                animator.SetTrigger("Die");
            }

            Debug.Log($"💀 {gameObject.name}이(가) 사망했습니다.");

            StartCoroutine(DestroyAfterDelay());

            Demo.DemoDungeonController demoDungeonController = FindFirstObjectByType<Demo.DemoDungeonController>();
            if (demoDungeonController != null)
            {
                demoDungeonController.AddKillCount();
            }
        }

        private IEnumerator DestroyAfterDelay()
        {
            yield return new WaitForSeconds(deathDestroyDelay);
            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}