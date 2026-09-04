using System.Collections;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class DemoMimicAI : MonoBehaviour, IDamageable, IIceSlowable
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
        [SerializeField] private float maxHealth = DemoIceCombat.MimicHealth;
        [SerializeField] private float deathDestroyDelay = 2f;

        private NavMeshAgent agent;
        private Transform commanderTarget;
        private Transform currentTarget;
        private MimicState currentState = MimicState.Chase;
        private Vector3 initialScale;
        private float currentHealth;
        private float lastAttackTime;
        private Vector3 lastChaseDestination;
        private bool isDead;
        private float iceSlowUntil;

        public bool IsAlive => !isDead;
        public Transform TargetTransform => transform;
        public Vector3 Position => transform.position;

        public void Initialize(Transform commander, float difficultyMultiplier = 1f)
        {
            commanderTarget = commander;
            var multiplier = Mathf.Max(1f, difficultyMultiplier);
            maxHealth = DemoIceCombat.MimicHealth;
            currentHealth = maxHealth;
            attackDamage *= multiplier;
            lastAttackTime = Time.time;
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            initialScale = transform.localScale;
            DemoUrpParticleRemapper.Remap(gameObject);
            DisableTongueVfx();
        }

        private void DisableTongueVfx()
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                if (current == null || current == transform)
                {
                    continue;
                }

                if (current.name.IndexOf("Tongue", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                current.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            DemoCombatRoster.Register(this);
        }

        private void OnDisable()
        {
            DemoCombatRoster.Unregister(this);
        }

        private void Start()
        {
            currentHealth = maxHealth;
            agent.speed = moveSpeed;
            agent.autoTraverseOffMeshLink = false;
        }

        private void Update()
        {
            if (isDead || DemoDungeonController.IsGameplayPaused)
            {
                return;
            }

            if (!DemoNavMeshUtil.TryEnsureOnNavMesh(agent))
            {
                return;
            }

            if (agent != null)
            {
                agent.speed = moveSpeed * (Time.time < iceSlowUntil ? 0.5f : 1f);
            }

            EvaluateTarget();
            ApplyBounce();

            if (currentTarget == null)
            {
                agent.isStopped = true;
                return;
            }

            float distance = DemoNavMeshUtil.PlanarSqrDistance(transform.position, currentTarget.position);
            distance = Mathf.Sqrt(distance);

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

        public void ApplyMoveSlow(float duration)
        {
            if (isDead || duration <= 0f)
            {
                return;
            }

            iceSlowUntil = Mathf.Max(iceSlowUntil, Time.time + duration);
            DemoIceSlowVfx.Play(transform, iceSlowUntil - Time.time);
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.speed = moveSpeed * 0.5f;
            }
        }

        private void EvaluateTarget()
        {
            Transform closestFollower = DemoCombatRoster.FindNearestAlly(transform.position, detectionRange, true);
            if (closestFollower != null)
            {
                currentTarget = closestFollower;
                return;
            }

            if (commanderTarget != null)
            {
                float commanderSqr = DemoNavMeshUtil.PlanarSqrDistance(transform.position, commanderTarget.position);
                if (commanderSqr <= detectionRange * detectionRange ||
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
            DemoNavMeshUtil.SetDestinationIfMoved(agent, currentTarget.position, ref lastChaseDestination);
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

            ApplyAttackDamage();
            DemoDungeonAudio.PlayMimic(transform.position);
        }

        private void ApplyAttackDamage()
        {
            if (currentTarget == null)
            {
                return;
            }

            DemoCombatTargetUtil.DamageAlly(currentTarget, attackDamage, transform.position);
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

        private void ApplyBounce()
        {
            if (agent.velocity.sqrMagnitude <= 0.01f)
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
            DemoIceSlowVfx.Stop(transform);

            agent.isStopped = true;
            agent.enabled = false;

            Collider collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            DemoDungeonController.Active?.AddKillCount();
            StartCoroutine(DestroyAfterDelay());
        }

        private IEnumerator DestroyAfterDelay()
        {
            yield return new WaitForSeconds(deathDestroyDelay);
            Destroy(gameObject);
        }
    }
}
