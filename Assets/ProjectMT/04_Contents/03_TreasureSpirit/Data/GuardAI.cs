using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.GrowthDungeon
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
        private float lastAttackTime;

        [Header("배회 범위 설정")]
        [SerializeField] private float patrolRadius = 8.0f;
        [SerializeField] private float waitAtPatrolPoint = 2.0f;
        private float patrolTimer;

        [Header("체력 설정")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;
        [SerializeField] private float deathDestroyDelay = 3.0f; // 사망 애니메이션 재생 시간 확보용
        private bool isDead;

        private NavMeshAgent agent;
        private Animator animator; // Animator 참조 추가
        private DungeonStarterController targetPlayer;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>(); // 자식 오프젝트의 Animator 수집
        }

        private void Start()
        {
            currentHealth = maxHealth;
            agent.speed = patrolSpeed;
            FindPlayer();
            SetRandomPatrolDestination();
        }

        private void Update()
        {
            if (isDead) return; // 사망 상태에서는 아무 로직도 실행하지 않음

            // 애니메이션 Speed 파라미터 갱신 (실제 이동 속도 연동)
            if (animator != null)
            {
                animator.SetFloat("Speed", agent.velocity.magnitude);
            }

            if (targetPlayer == null)
            {
                FindPlayer();
                PatrolBehavior();
                return;
            }

            float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.transform.position);

            switch (currentState)
            {
                case GuardState.Patrol:
                    UpdatePatrolState(distanceToPlayer);
                    break;

                case GuardState.Chase:
                    UpdateChaseState(distanceToPlayer);
                    break;

                case GuardState.Attack:
                    UpdateAttackState(distanceToPlayer);
                    break;
            }
        }

        private void UpdatePatrolState(float distanceToPlayer)
        {
            if (distanceToPlayer <= detectionRange)
            {
                currentState = GuardState.Chase;
                agent.speed = chaseSpeed;
                return;
            }

            PatrolBehavior();
        }

        private void UpdateChaseState(float distanceToPlayer)
        {
            if (distanceToPlayer > loseTargetRange)
            {
                currentState = GuardState.Patrol;
                agent.speed = patrolSpeed;
                SetRandomPatrolDestination();
                return;
            }

            if (distanceToPlayer <= attackRange)
            {
                currentState = GuardState.Attack;
                agent.isStopped = true;
                return;
            }

            agent.isStopped = false;
            agent.SetDestination(targetPlayer.transform.position);
        }

        private void UpdateAttackState(float distanceToPlayer)
        {
            if (distanceToPlayer > attackRange)
            {
                currentState = GuardState.Chase;
                agent.isStopped = false;
                return;
            }

            Vector3 lookDir = targetPlayer.transform.position - transform.position;
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
            Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
            randomDirection += transform.position;

            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }

        [Header("공격 모션 설정")]
        [SerializeField] private int attackMotionCount = 3; // 공격 모션 개수

        private void PerformAttack()
        {
            // 공격 애니메이션 트리거 실행 (여러 모션 중 랜덤 선택)
            if (animator != null)
            {
                int randomAttackIndex = Random.Range(0, attackMotionCount);
                animator.SetInteger("AttackIndex", randomAttackIndex);
                animator.SetTrigger("Attack");
            }

            Debug.Log($"⚔️ {gameObject.name}이(가) 플레이어를 공격했습니다!");
        }

        /// <summary>
        /// 외부(플레이어 공격 등)에서 데미지를 줄 때 호출하는 함수
        /// </summary>
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

            // 더 이상 이동/충돌하지 않도록 정리
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
        }

        private IEnumerator DestroyAfterDelay()
        {
            yield return new WaitForSeconds(deathDestroyDelay);
            Destroy(gameObject);
        }

        private void FindPlayer()
        {
            DungeonStarterController player = FindObjectOfType<DungeonStarterController>();
            if (player != null)
            {
                targetPlayer = player;
            }
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