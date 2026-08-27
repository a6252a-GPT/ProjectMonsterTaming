using System.Collections;
using ProjectMT.Contents.TreasureSpirit;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class DemoMimicAI : MonoBehaviour
    {
        private enum MimicState
        {
            Chase,
            Attack,
            Dead
        }

        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float bounceSpeed = 10f;
        [SerializeField] private float bounceHeight = 0.2f;
        [SerializeField] private float detectionRange = 7f;
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float loseTargetRange = 10f;
        [SerializeField] private float attackCooldown = 1.2f;
        [SerializeField] private float attackDamage = 15f;
        [SerializeField] private float maxHealth = 80f;
        [SerializeField] private float deathDestroyDelay = 2f;

        private NavMeshAgent agent;
        private Transform commanderTarget;
        private Transform currentTarget;
        private MimicState currentState = MimicState.Chase;
        private Vector3 initialScale;
        private float currentHealth;
        private float lastAttackTime;
        private bool isDead;

        public bool IsAlive => !isDead;
        public Transform TargetTransform => transform;

        public void Initialize(Transform commander, float difficultyMultiplier = 1f)
        {
            commanderTarget = commander;
            var multiplier = Mathf.Max(1f, difficultyMultiplier);
            maxHealth *= multiplier;
            attackDamage *= multiplier;
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            initialScale = transform.localScale;
            DemoUrpParticleRemapper.Remap(gameObject);
        }

        private void Start()
        {
            currentHealth = maxHealth;
            agent.speed = moveSpeed;
            agent.autoTraverseOffMeshLink = false;
        }

        private bool TryEnsureAgentOnNavMesh()
        {
            if (agent == null)
            {
                return false;
            }

            if (!agent.enabled)
            {
                agent.enabled = true;
            }

            if (!agent.isActiveAndEnabled)
            {
                return false;
            }

            if (agent.isOnNavMesh)
            {
                return true;
            }

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }

            return agent.isOnNavMesh;
        }

        private void Update()
        {
            if (isDead)
            {
                return;
            }

            if (!TryEnsureAgentOnNavMesh())
            {
                return;
            }

            EvaluateTarget();
            ApplyBounce();

            if (currentTarget == null)
            {
                agent.isStopped = true;
                return;
            }

            float distance = Vector3.Distance(transform.position, currentTarget.position);

            switch (currentState)
            {
                case MimicState.Chase:
                    UpdateChase(distance);
                    break;
                case MimicState.Attack:
                    UpdateAttack(distance);
                    break;
            }
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

        private void EvaluateTarget()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange);
            Transform closestFollower = null;
            float closestFollowerDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null || hit.GetComponentInParent<FollowerAI>() == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, hit.transform.position);
                if (distance < closestFollowerDistance)
                {
                    closestFollowerDistance = distance;
                    closestFollower = hit.transform;
                }
            }

            if (closestFollower != null)
            {
                currentTarget = closestFollower;
                return;
            }

            if (commanderTarget != null)
            {
                float commanderDistance = Vector3.Distance(transform.position, commanderTarget.position);
                if (commanderDistance <= detectionRange ||
                    currentState == MimicState.Chase ||
                    currentState == MimicState.Attack)
                {
                    currentTarget = commanderTarget;
                    return;
                }
            }

            currentTarget = null;
        }

        private void UpdateChase(float distance)
        {
            if (distance > loseTargetRange)
            {
                currentTarget = null;
                agent.isStopped = true;
                return;
            }

            if (distance <= attackRange)
            {
                currentState = MimicState.Attack;
                agent.isStopped = true;
                return;
            }

            agent.isStopped = false;
            agent.SetDestination(currentTarget.position);
        }

        private void UpdateAttack(float distance)
        {
            if (distance > attackRange)
            {
                currentState = MimicState.Chase;
                agent.isStopped = false;
                return;
            }

            Vector3 lookDirection = currentTarget.position - transform.position;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(lookDirection),
                    Time.deltaTime * 10f);
            }

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                PerformAttack();
                lastAttackTime = Time.time;
            }
        }

        private void PerformAttack()
        {
            if (currentTarget == null)
            {
                return;
            }

            Debug.Log($"[DemoMimicAI] {currentTarget.name} 공격 ({attackDamage} 데미지)");
            ApplyAttackDamage();
        }

        private void ApplyAttackDamage()
        {
            if (currentTarget == null)
            {
                return;
            }

            PlayerCharacterController player = currentTarget.GetComponentInParent<PlayerCharacterController>();
            if (player != null)
            {
                player.TakeDamage(attackDamage, transform.position);
                return;
            }

            FollowerAI follower = currentTarget.GetComponentInParent<FollowerAI>();
            follower?.TakeDamage(attackDamage);
        }

        private void ApplyBounce()
        {
            if (agent.velocity.magnitude <= 0.1f)
            {
                return;
            }

            float bounce = Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;
            transform.localScale = initialScale + new Vector3(0f, bounce, 0f);
        }

        private void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            currentState = MimicState.Dead;
            currentTarget = null;

            agent.isStopped = true;
            agent.enabled = false;

            Collider collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            DemoDungeonController controller = FindFirstObjectByType<DemoDungeonController>();
            controller?.AddKillCount();

            Debug.Log("[DemoMimicAI] 미믹 처치");
            StartCoroutine(DestroyAfterDelay());
        }

        private IEnumerator DestroyAfterDelay()
        {
            yield return new WaitForSeconds(deathDestroyDelay);
            Destroy(gameObject);
        }
    }
}
