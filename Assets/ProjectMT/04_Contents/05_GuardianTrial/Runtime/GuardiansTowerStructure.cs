using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.GuardianTrial
{
    // 08.06 안건준 추가 - 수호자의 탑 방어 건물 (체력 100 + 체력 게이지). 네 모서리 Wall 오브젝트에 부착.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitActor))]
    public sealed class GuardiansTowerStructure : MonoBehaviour
    {
        [SerializeField] private UnitActor unitActor; // 체력·피격 판정 담당
        [SerializeField] private Image healthFillImage; // Type=Filled 체력 게이지 (없어도 동작)
        [SerializeField, Min(1f)] private float maxHealth = 100f; // 요청: 건물 체력 100

        public bool IsAlive => unitActor != null && unitActor.IsAlive;

        private void Awake()
        {
            if (unitActor == null)
            {
                unitActor = GetComponent<UnitActor>();
            }
        }

        // 판 시작마다 호출: 체력을 100으로 되돌리고 CombatWorld의 공격 대상 목록에 등록한다.
        public void Initialize(CombatWorld combatWorld)
        {
            if (unitActor == null || combatWorld == null)
            {
                return;
            }

            unitActor.Health.Damaged -= HandleHealthChanged; // 재시작 시 중복 구독 방지
            unitActor.Health.Died -= HandleHealthChanged;

            var stats = new UnitStatsSnapshot
            {
                maxHealth = maxHealth,
                damage = 0f,
                defense = 0f,
                moveSpeed = 0f,
                attackRange = 0f,
                attackInterval = 1f,
                projectileSpeed = 0f,
                ranged = false
            };
            var request = new UnitSpawnRequest(
                "guardians_tower_structure",
                stats,
                UnitTeam.Player, // 적군 자동 타깃 탐색(FindNearestOpponent)에 걸리는 아군 진영 목표
                canMove: false,
                canAttack: false);
            unitActor.Initialize(request, combatWorld, combatWorld.Feedback);
            unitActor.Health.Damaged += HandleHealthChanged;
            unitActor.Health.Died += HandleHealthChanged;
            SetFillAmount(1f);
        }

        public void Shutdown()
        {
            if (unitActor != null && unitActor.Health != null)
            {
                unitActor.Health.Damaged -= HandleHealthChanged;
                unitActor.Health.Died -= HandleHealthChanged;
            }
        }

        private void HandleHealthChanged(DamageReport report)
        {
            var health = unitActor == null ? null : unitActor.Health;
            if (health == null || health.MaxHealth <= 0f)
            {
                return;
            }

            SetFillAmount(health.CurrentHealth / health.MaxHealth);
        }

        private void SetFillAmount(float ratio)
        {
            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = Mathf.Clamp01(ratio);
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(UnitActor actor, Image fillImage)
        {
            unitActor = actor;
            healthFillImage = fillImage;
        }
#endif
    }
}
