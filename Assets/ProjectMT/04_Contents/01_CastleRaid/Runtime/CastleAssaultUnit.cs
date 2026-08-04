using System;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.CastleRaid
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(HealthComponent))]
    public sealed class CastleAssaultUnit : MonoBehaviour // NavMesh 기반 성 공격 유닛
    {
        [SerializeField] private NavMeshAgent agent; // 길찾기·이동 담당
        [SerializeField] private HealthComponent health; // 공용 체력 부품
        [SerializeField] private UnitVisualFeedback visualFeedback; // 피격·사망 연출

        private CastleRaidController controller; // 목표·공격 조율자
        private UnitStatsSnapshot stats; // 이번 실행 능력치
        private CastleTarget target; // 현재 공격 대상
        private Transform leasedSlot; // 대상 주변 공격 자리
        private float attackCooldown; // 다음 공격 대기

        public bool IsAlive => health != null && health.IsAlive;
        public CastleTarget Target => target;

        public event Action<CastleAssaultUnit> Died;
        public event Action<CastleAssaultUnit, DamageReport> Damaged;

        private void Awake()
        {
            ResolveReferences();
        }

        public void Initialize(BattleUnitSnapshot unit, CastleRaidController raidController)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            Shutdown(); // 풀 재사용 전 이전 연결 정리
            ResolveReferences();
            controller = raidController ?? throw new ArgumentNullException(nameof(raidController));
            stats = unit.Stats;
            attackCooldown = UnityEngine.Random.Range(0f, Mathf.Max(0.05f, stats.attackInterval * 0.4f)); // 동시 공격 분산
            health.Initialize(stats.maxHealth);
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;

            agent.speed = Mathf.Max(0.1f, stats.moveSpeed);
            agent.acceleration = 16f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = Mathf.Max(0.35f, stats.attackRange * 0.75f);
            agent.isStopped = false;
        }

        public void Tick(float deltaTime)
        {
            if (!IsAlive || controller == null)
            {
                return;
            }

            attackCooldown = Mathf.Max(0f, attackCooldown - deltaTime);
            var desiredTarget = controller.FindPriorityTarget(this); // 열린 경로 기준 목표 재평가
            if (desiredTarget != target)
            {
                SetTarget(desiredTarget);
            }

            if (target == null || !target.IsAlive)
            {
                StopMoving();
                return;
            }

            var destination = leasedSlot == null ? target.transform.position : leasedSlot.position; // 대여 자리를 우선
            var distance = PlanarDistance(transform.position, destination);
            var attackDistance = Mathf.Max(0.5f, stats.attackRange);
            if (distance > attackDistance)
            {
                MoveTo(destination);
                return;
            }

            StopMoving();
            FaceTowards(target.transform.position, deltaTime);
            if (attackCooldown <= 0f)
            {
                attackCooldown = Mathf.Max(0.1f, stats.attackInterval);
                controller.Attack(this, target, stats.damage);
            }
        }

        public void ApplyDefenderDamage(float damage, Vector3 hitPoint)
        {
            health?.ApplyDamage(new DamageRequest(null, damage, hitPoint));
        }

        public void Shutdown()
        {
            ReleaseTarget();
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Died -= HandleDied;
            }

            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }

            controller = null;
            Died = null;
            Damaged = null;
        }

        private void SetTarget(CastleTarget nextTarget)
        {
            ReleaseTarget();
            target = nextTarget;
            if (target != null)
            {
                target.AttackSlots?.TryLease(this, transform.position, out leasedSlot); // 대상 주변 빈 자리 확보
            }
        }

        private void ReleaseTarget()
        {
            if (target != null)
            {
                target.AttackSlots?.Release(this);
            }

            target = null;
            leasedSlot = null;
        }

        private void MoveTo(Vector3 destination)
        {
            if (!agent.isOnNavMesh)
            {
                return;
            }

            agent.isStopped = false;
            agent.SetDestination(destination); // NavMesh 경로 갱신
        }

        private void StopMoving()
        {
            if (!agent.isOnNavMesh)
            {
                return;
            }

            agent.isStopped = true;
            agent.ResetPath();
        }

        private void FaceTowards(Vector3 destination, float deltaTime)
        {
            var direction = destination - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 12f * deltaTime);
        }

        private void HandleDamaged(DamageReport report)
        {
            visualFeedback?.PlayHit();
            Damaged?.Invoke(this, report);
        }

        private void HandleDied(DamageReport report)
        {
            visualFeedback?.PlayDeath();
            ReleaseTarget();
            Died?.Invoke(this); // Controller가 연출 뒤 풀 반환
        }

        private void ResolveReferences()
        {
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }

            if (visualFeedback == null)
            {
                visualFeedback = GetComponent<UnitVisualFeedback>();
            }
        }

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }
    }
}
