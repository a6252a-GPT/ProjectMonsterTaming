using System;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.GuardianTrial
{
    // 08.07 안건준 추가 - 건물(기둥)이 살아있는 동안 적에게 주는 버프 종류.
    // Wall_1~4 인스펙터에서 각자 다르게 지정한다. 08.07 안건준 수정 - 1~3번은 "적 캐릭터"에게 적용되는
    // 상시 버프이고, 4번(AttackBoost)은 08.07 안건준 재수정 - 건물이 파괴되는 순간 "아군"에게 1회
    // 발동되는 공격력 버프로 변경(기존 "살아있는 동안 적 증원" 방식 폐지).
    public enum GuardiansTowerStructureRole
    {
        Defense,     // 1번: 모든 적 방어력 상승 (받는 피해 경감)
        Health,      // 2번: 모든 적 최대 체력 상승
        Regen,       // 3번: 모든 적 초당 체력 회복
        AttackBoost  // 4번: 08.07 안건준 수정 - 파괴되면 아군 전체에게 공격력 2배 버프 발동(1회성)
    }

    // 08.06 안건준 추가 - 수호자의 탑 방어 건물 (체력 100 + 체력 게이지). 네 모서리 Wall 오브젝트에 부착.
    // 08.07 안건준 수정 - 몬스터가 이 건물을 공격하지 않도록 변경. CombatWorld(UnitActor)에는 등록하지 않고
    // HealthComponent만 표시용으로 사용한다. (몬스터는 기존처럼 플레이어/아군만 공격 대상으로 탐색)
    // 08.07 안건준 수정 - 체력 게이지를 Slider 타입으로 변경, 건물별 버프 역할(Role) 추가,
    // 파괴 알림용 Died 이벤트 추가. 실제 버프 효과(방어력·체력·회복·이동속도)는 모두 적 캐릭터에게만
    // 적용되며 GuardiansTowerStructureBuffs가 담당한다(이 건물 자신의 체력에는 영향 없음).
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HealthComponent))]
    public sealed class GuardiansTowerStructure : MonoBehaviour
    {
        [SerializeField] private HealthComponent health; // 체력 표시 전용, 전투 타깃 탐색에는 등록되지 않음
        [SerializeField] private Slider healthSlider; // 08.07 안건준 수정 - Image 채우기 대신 Slider 타입 체력 게이지
        [SerializeField, Min(1f)] private float maxHealth = 30f; // 08.07 안건준 수정 - 건물 기본 체력 30 (난이도 배율 적용 전 기준값, 인스펙터에서 조절 가능)
        [SerializeField] private GuardiansTowerStructureRole role; // 08.07 안건준 추가 - 이 건물이 주는 버프 종류
        [SerializeField] private TMP_Text roleLabel; // 08.07 안건준 추가 - 건물 위에 표시할 버프 종류 단어(방어/체력/회복/증원)
        // 08.07 안건준 수정 - 체력 게이지(Wall_N_Canvas)는 이제 기둥의 자식 오브젝트라서 기둥 위치를
        // 그대로 따라간다. 기둥이 비활성화되면 자동으로 같이 숨겨지지만, 파괴 연출을 명확히 하기 위해
        // 여기서도 명시적으로 꺼준다.
        [SerializeField] private GameObject healthBarRoot;

        public bool IsAlive => health != null && health.IsAlive;
        public GuardiansTowerStructureRole Role => role; // 08.07 안건준 추가
        public HealthComponent Health => health; // 08.07 안건준 추가 - 강제 지정 공격(아군 어그로)에서 참조

        // 08.07 안건준 추가 - 이 건물이 파괴되었을 때 알림(버프 해제 처리용). 재시작마다 Initialize에서 새로 구독한다.
        public event Action<GuardiansTowerStructure> Died;

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }
        }

        // 08.07 안건준 추가 - Damaged 이벤트 기반 갱신과 별개로, 매 프레임 현재 체력 비율을 게이지에 직접
        // 반영한다. 이벤트가 어떤 이유로든 누락되더라도(구독 시점, 실행 순서 등) 슬라이더가 항상 실제 체력과
        // 지연 없이 일치하도록 하기 위한 안전장치다. 계산 비용이 매우 적어 매 프레임 실행해도 문제 없다.
        private void Update()
        {
            if (health == null || !health.IsAlive || health.MaxHealth <= 0f)
            {
                return;
            }

            SetFillAmount(health.CurrentHealth / health.MaxHealth);
        }

        // 판 시작마다 호출: 체력을 (기준값 x 난이도 배율)로 되돌린다. CombatWorld에는 등록하지 않아
        // 몬스터의 공격 대상이 되지 않는다. healthMultiplier는 난이도 스케일링에 사용(기본 1배).
        public void Initialize(float healthMultiplier = 1f)
        {
            if (health == null)
            {
                return;
            }

            // 08.07 안건준 추가 - 이전 판에서 파괴되어 숨겨져 있었을 수 있으니 재입장/재시작 시 항상 다시 보이게 한다.
            gameObject.SetActive(true);
            if (healthBarRoot != null)
            {
                healthBarRoot.SetActive(true);
            }

            health.Damaged -= HandleHealthChanged; // 재시작 시 중복 구독 방지
            health.Died -= HandleHealthChanged;
            health.Died -= HandleDiedInternal;
            var scaledMaxHealth = maxHealth * Mathf.Max(0.01f, healthMultiplier); // 08.07 안건준 추가 - 난이도 배율 반영
            health.Initialize(scaledMaxHealth);
            health.Damaged += HandleHealthChanged;
            health.Died += HandleHealthChanged;
            health.Died += HandleDiedInternal;
            SetFillAmount(1f);
            if (roleLabel != null)
            {
                roleLabel.text = ResolveRoleLabel(role); // 08.07 안건준 추가 - 판 시작마다 버프 단어 갱신
            }
        }

        public void Shutdown()
        {
            if (health != null)
            {
                health.Damaged -= HandleHealthChanged;
                health.Died -= HandleHealthChanged;
                health.Died -= HandleDiedInternal;
            }

            Died = null; // 08.07 안건준 추가 - 재시작 전 외부 구독 정리
        }

        private void HandleDiedInternal(DamageReport report)
        {
            SetFillAmount(0f); // 08.07 안건준 추가 - 파괴 직전 마지막 체력 반영(0으로 표시)
            Died?.Invoke(this); // 08.07 안건준 추가 - 버프 매니저에 파괴 사실 통지
            // 08.07 안건준 추가 - 파괴되면 건물과 체력 게이지를 완전히 숨긴다. Destroy 대신 비활성화라서
            // 다음 판(Initialize)에서 다시 보이게 할 수 있다.
            if (healthBarRoot != null)
            {
                healthBarRoot.SetActive(false);
            }

            gameObject.SetActive(false);
        }

        private void HandleHealthChanged(DamageReport report)
        {
            if (health == null || health.MaxHealth <= 0f)
            {
                return;
            }

            SetFillAmount(health.CurrentHealth / health.MaxHealth);
        }

        private void SetFillAmount(float ratio)
        {
            if (healthSlider != null)
            {
                healthSlider.value = Mathf.Clamp01(ratio);
            }
        }

        // 08.07 안건준 추가 - 버프 종류를 화면에 표시할 짧은 단어로 변환.
        private static string ResolveRoleLabel(GuardiansTowerStructureRole structureRole)
        {
            switch (structureRole)
            {
                case GuardiansTowerStructureRole.Defense:
                    return "적 방어 버프 OFF";
                case GuardiansTowerStructureRole.Health:
                    return "적 체력 버프 OFF";
                case GuardiansTowerStructureRole.Regen:
                    return "적 회복 버프 OFF";
                case GuardiansTowerStructureRole.AttackBoost:
                    return "아군공격 버프 ON"; // 08.07 안건준 수정 - 파괴 시 아군 공격력 2배 버프(4번 건물)
                default:
                    return string.Empty;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            HealthComponent healthComponent,
            Slider slider,
            GuardiansTowerStructureRole structureRole,
            TMP_Text label = null,
            GameObject barRoot = null)
        {
            health = healthComponent;
            healthSlider = slider;
            role = structureRole;
            roleLabel = label;
            healthBarRoot = barRoot;
        }
#endif
    }
}
