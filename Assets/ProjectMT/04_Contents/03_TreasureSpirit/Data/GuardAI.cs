using System.Collections;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.TreasureSpirit
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class GuardAI : MonoBehaviour, IDamageable, Demo.IIceSlowable
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
        [SerializeField] private float attackDamage = 15f;
        [SerializeField] private int attackMotionCount = 3;
        private float lastAttackTime;
        private Vector3 lastChaseDestination;
        private int speedHash;
        private float lastAnimSpeed = -1f;

        [Header("배회 범위 설정")]
        [SerializeField] private float patrolRadius = 8.0f;
        [SerializeField] private float waitAtPatrolPoint = 2.0f;
        private float patrolTimer;
        private Vector3 patrolOrigin;
        private bool usePatrolOrigin;

        [Header("체력 설정")]
        [SerializeField] private float maxHealth = Demo.DemoIceCombat.GuardHealth;
        private float currentHealth;
        [SerializeField] private float deathDestroyDelay = 3.0f;
        private bool isDead;
        private float iceSlowUntil;

        public bool IsAlive => !isDead;
        public Vector3 Position => transform.position;

        [Header("타겟 설정")]
        [SerializeField] private Transform commanderTarget; // 기본 군단장(플레이어)
        private Transform currentTarget;                  // 현재 조준 중인 최종 타깃(팔로워 우선)

        private NavMeshAgent agent;
        private Animator animator;
        private bool animatorParamsCached;
        private bool hasSpeedParameter;
        private bool hasAttackIndexParameter;
        private bool hasAttackTrigger;
        private bool hasDieTrigger;

        private bool IsAgentReady()
        {
            return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
            CacheAnimatorParameters();
        }

        private void OnEnable()
        {
            Demo.DemoCombatRoster.Register(this);
        }

        private void OnDisable()
        {
            Demo.DemoCombatRoster.Unregister(this);
        }

        private void Start()
        {
            currentHealth = maxHealth;
            RefreshMoveSpeed();
            SetRandomPatrolDestination();
        }

        public void SetTargetPlayer(Transform target)
        {
            commanderTarget = target;
        }

        public void ConfigureDifficulty(float difficultyMultiplier) // 성장 단계 전투 배율 적용
        {
            var multiplier = Mathf.Max(1f, difficultyMultiplier);
            maxHealth = Demo.DemoIceCombat.GuardHealth;
            currentHealth = maxHealth;
            attackDamage *= multiplier;
            lastAttackTime = Time.time;
        }

        public void SetPatrolBounds(Vector3 center, float radius)
        {
            patrolOrigin = center;
            patrolRadius = Mathf.Clamp(radius, 1f, 6f);
            usePatrolOrigin = true;
        }

        private void Update()
        {
            if (isDead || Demo.DemoDungeonController.IsGameplayPaused)
            {
                return;
            }

            if (!Demo.DemoNavMeshUtil.TryEnsureOnNavMesh(agent))
            {
                return;
            }

            RefreshMoveSpeed();

            if (hasSpeedParameter && animator != null)
            {
                float speed = agent.velocity.magnitude;
                if (Mathf.Abs(speed - lastAnimSpeed) > 0.05f)
                {
                    lastAnimSpeed = speed;
                    animator.SetFloat(speedHash, speed);
                }
            }

            // ★ 실시간 타깃 평가 (팔로워 우선 감지)
            EvaluateTarget();

            if (currentTarget == null)
            {
                if (currentState != GuardState.Patrol)
                {
                    currentState = GuardState.Patrol;
                    RefreshMoveSpeed();
                    SetRandomPatrolDestination();
                }
                PatrolBehavior();
                return;
            }

            float distanceToTarget = Mathf.Sqrt(Demo.DemoNavMeshUtil.PlanarSqrDistance(transform.position, currentTarget.position));

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
            Transform closestFollower = Demo.DemoCombatRoster.FindNearestAlly(transform.position, detectionRange, true);
            if (closestFollower != null)
            {
                currentTarget = closestFollower;
                return;
            }

            if (commanderTarget != null)
            {
                float distToCommanderSqr = Demo.DemoNavMeshUtil.PlanarSqrDistance(transform.position, commanderTarget.position);
                if (distToCommanderSqr <= detectionRange * detectionRange || currentState == GuardState.Chase || currentState == GuardState.Attack)
                {
                    currentTarget = commanderTarget;
                    return;
                }
            }

            currentTarget = null;
        }

        private void UpdatePatrolState(float distanceToTarget)
        {
            if (distanceToTarget <= detectionRange)
            {
                currentState = GuardState.Chase;
                RefreshMoveSpeed();
                return;
            }

            PatrolBehavior();
        }

        private void UpdateChaseState(float distanceToTarget)
        {
            if (distanceToTarget > loseTargetRange)
            {
                currentState = GuardState.Patrol;
                RefreshMoveSpeed();
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
            RefreshMoveSpeed();
            Demo.DemoNavMeshUtil.SetDestinationIfMoved(agent, currentTarget.position, ref lastChaseDestination);
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
            if (!IsAgentReady())
            {
                return;
            }

            agent.isStopped = false;
            if (agent.pathPending)
            {
                return;
            }

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
            if (!IsAgentReady())
            {
                return;
            }

            agent.isStopped = false;
            Vector3 origin = usePatrolOrigin ? patrolOrigin : transform.position;
            Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
            randomDirection.y = 0f;
            randomDirection += origin;

            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, patrolRadius * 0.5f, NavMesh.AllAreas))
            {
                if (usePatrolOrigin && Demo.DemoNavMeshUtil.PlanarSqrDistance(hit.position, patrolOrigin) > patrolRadius * patrolRadius)
                {
                    return;
                }

                agent.SetDestination(hit.position);
            }
        }

        private void CacheAnimatorParameters()
        {
            if (animatorParamsCached)
            {
                return;
            }

            animatorParamsCached = true;
            if (animator == null)
            {
                return;
            }

            hasSpeedParameter = HasAnimatorParameter("Speed");
            speedHash = Animator.StringToHash("Speed");
            hasAttackIndexParameter = HasAnimatorParameter("AttackIndex");
            hasAttackTrigger = HasAnimatorParameter("Attack");
            hasDieTrigger = HasAnimatorParameter("Die");
        }

        private bool HasAnimatorParameter(string parameterName)
        {
            if (animator == null || string.IsNullOrEmpty(parameterName))
            {
                return false;
            }

            int hash = Animator.StringToHash(parameterName);
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == hash)
                {
                    return true;
                }
            }

            return false;
        }

        private void PerformAttack()
        {
            if (hasAttackTrigger)
            {
                if (hasAttackIndexParameter)
                {
                    int randomAttackIndex = Random.Range(0, attackMotionCount);
                    animator.SetInteger("AttackIndex", randomAttackIndex);
                }

                animator.SetTrigger("Attack");
            }

            ApplyAttackDamage();
            Demo.DemoDungeonAudio.PlayGuardAttack(transform.position);
        }

        private void ApplyAttackDamage()
        {
            if (currentTarget == null)
            {
                return;
            }

            Demo.DemoCombatTargetUtil.DamageAlly(currentTarget, attackDamage, transform.position);
        }

        public float ReceiveDamage(UnitActor source, float amount)
        {
            if (!IsAlive)
            {
                return 0f;
            }

            var before = currentHealth;
            TakeDamage(amount);
            return Mathf.Max(0f, before - currentHealth);
        }

        public void TakeDamage(float damage)
        {
            if (isDead)
            {
                return;
            }

            currentHealth -= damage;

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        public void ApplyMoveSlow(float duration)
        {
            if (isDead || duration <= 0f)
            {
                return;
            }

            iceSlowUntil = Mathf.Max(iceSlowUntil, Time.time + duration);
            Demo.DemoIceSlowVfx.Play(transform, iceSlowUntil - Time.time);
            RefreshMoveSpeed();
        }

        private void RefreshMoveSpeed()
        {
            if (!IsAgentReady())
            {
                return;
            }

            float baseSpeed = currentState == GuardState.Chase || currentState == GuardState.Attack
                ? chaseSpeed
                : patrolSpeed;
            agent.speed = baseSpeed * (Time.time < iceSlowUntil ? 0.5f : 1f);
        }

        private void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            currentState = GuardState.Dead;
            Demo.DemoIceSlowVfx.Stop(transform);

            agent.isStopped = true;
            agent.enabled = false;

            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }

            if (hasDieTrigger && animator != null)
            {
                animator.SetTrigger("Die");
            }

            StartCoroutine(DestroyAfterDelay());
            Demo.DemoDungeonController.Active?.AddKillCount();
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