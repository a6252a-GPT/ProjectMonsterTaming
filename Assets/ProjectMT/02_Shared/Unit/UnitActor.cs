using System;
using ProjectMT.Shared.Combat;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum UnitTeam // 전투 진영
    {
        Player,
        Enemy
    }

    public readonly struct UnitSpawnRequest // 유닛 한 기 생성 명세
    {
        public UnitSpawnRequest(
            string unitId,
            UnitStatsSnapshot stats,
            UnitTeam team,
            bool canMove = true,
            bool canAttack = true,
            float fixedDamagePerHit = 0f)
        {
            UnitId = unitId ?? string.Empty;
            Stats = stats;
            Team = team;
            CanMove = canMove;
            CanAttack = canAttack;
            FixedDamagePerHit = fixedDamagePerHit;
        }

        public string UnitId { get; }
        public UnitStatsSnapshot Stats { get; }
        public UnitTeam Team { get; }
        public bool CanMove { get; }
        public bool CanAttack { get; }
        public float FixedDamagePerHit { get; } // 콘텐츠 고정 피해값
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(HealthComponent))]
    public sealed class UnitActor : MonoBehaviour // 공용 이동·공격 유닛
    {
        [SerializeField] private HealthComponent health; // 체력 부품
        [SerializeField] private UnitVisualFeedback visualFeedback; // 피격 시각 연출

        private CombatWorld world; // 현재 전투 영역
        private ICombatFeedbackPlayer feedback; // 공용 연출 계약
        private UnitStatsSnapshot stats; // 이번 실행 능력치
        private bool canMove; // 이동 허용 여부
        private bool canAttack; // 공격 허용 여부
        private float attackCooldown; // 다음 공격 대기
        private float retargetCooldown; // 타깃 재탐색 대기
        private Transform followAnchor; // 추종 기준 대상
        private Vector3 followOffset; // 대형 내 위치
        private float followDetectionRange; // 추종 중 탐지 거리
        private float followLeashRange; // 기준점 복귀 거리

        public string UnitId { get; private set; }
        public UnitTeam Team { get; private set; }
        public HealthComponent Health => health;
        public UnitVisualFeedback VisualFeedback => visualFeedback;
        public UnitActor Target { get; private set; }
        public bool IsAlive => health != null && health.IsAlive;

        public event Action<UnitActor> Died;

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }

            if (visualFeedback == null)
            {
                visualFeedback = GetComponent<UnitVisualFeedback>();
            }
        }

        public void Initialize(UnitSpawnRequest request, CombatWorld combatWorld, ICombatFeedbackPlayer feedbackPlayer)
        {
            Shutdown(); // 풀 재사용 전 이전 연결 정리
            UnitId = request.UnitId;
            Team = request.Team;
            stats = request.Stats;
            canMove = request.CanMove;
            canAttack = request.CanAttack;
            world = combatWorld;
            feedback = feedbackPlayer;
            attackCooldown = UnityEngine.Random.Range(0f, Mathf.Max(0.05f, stats.attackInterval * 0.35f)); // 동시 공격 분산
            retargetCooldown = UnityEngine.Random.Range(0f, 0.2f);
            health.Initialize(stats.maxHealth, request.FixedDamagePerHit);
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
            world?.Register(this);
        }

        public void SetFollowAnchor(Transform anchor, Vector3 offset, float detectionRange, float leashRange)
        {
            followAnchor = anchor;
            followOffset = offset;
            followDetectionRange = Mathf.Max(0.5f, detectionRange);
            followLeashRange = Mathf.Max(followDetectionRange, leashRange);
        }

        public void ClearFollowAnchor()
        {
            followAnchor = null;
            followOffset = Vector3.zero;
        }

        public void Tick(float deltaTime)
        {
            if (!IsAlive || world == null)
            {
                return;
            }

            attackCooldown = Mathf.Max(0f, attackCooldown - deltaTime);
            retargetCooldown -= deltaTime;

            if (followAnchor != null)
            {
                var anchorPosition = followAnchor.position + followOffset;
                if (PlanarDistance(transform.position, anchorPosition) > followLeashRange)
                {
                    Target = null; // 멀어지면 전투보다 복귀 우선
                    MoveTowards(anchorPosition, deltaTime);
                    return;
                }
            }

            if (Target == null || !Target.IsAlive || retargetCooldown <= 0f)
            {
                var range = followAnchor == null ? float.PositiveInfinity : followDetectionRange;
                Target = world.FindNearestOpponent(this, range); // 일정 간격으로 최근접 적 탐색
                retargetCooldown = 0.2f;
            }

            if (Target == null)
            {
                if (followAnchor != null)
                {
                    MoveTowards(followAnchor.position + followOffset, deltaTime);
                }

                return;
            }

            var distance = PlanarDistance(transform.position, Target.transform.position);
            if (distance > Mathf.Max(0.2f, stats.attackRange))
            {
                MoveTowards(Target.transform.position, deltaTime);
                return;
            }

            FaceTowards(Target.transform.position, deltaTime);
            if (canAttack && attackCooldown <= 0f)
            {
                attackCooldown = Mathf.Max(0.05f, stats.attackInterval);
                world.Attack(this, Target, stats); // 근접·원거리 분기는 World 소유
            }
        }

        public void Shutdown()
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Died -= HandleDied;
            }

            world?.Unregister(this);
            world = null;
            feedback = null;
            Target = null;
            followAnchor = null;
            Died = null; // 풀 재사용 전 외부 구독 제거
        }

        private void MoveTowards(Vector3 destination, float deltaTime)
        {
            if (!canMove || stats.moveSpeed <= 0f)
            {
                return;
            }

            destination.y = transform.position.y;
            transform.position = Vector3.MoveTowards(transform.position, destination, stats.moveSpeed * deltaTime);
            FaceTowards(destination, deltaTime);
        }

        private void FaceTowards(Vector3 destination, float deltaTime)
        {
            var direction = destination - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 12f * deltaTime);
        }

        private void HandleDamaged(DamageReport report)
        {
            feedback?.PlayHit(this, report);
        }

        private void HandleDied(DamageReport report)
        {
            feedback?.PlayDeath(this, report);
            Died?.Invoke(this);
            world?.NotifyDeath(this); // 연출 뒤 풀 반환 요청
        }

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }
    }
}
