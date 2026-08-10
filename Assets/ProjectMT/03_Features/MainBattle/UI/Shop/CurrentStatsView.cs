using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class CurrentStatsView : MonoBehaviour // 현재 능력치 패널에 GrowthCalculator 결과 표시
    {
        [Header("데이터 연결")]
        [SerializeField] private GrowthCalculator growthCalculator; // 강화 값을 받아올 계산기

        [Header("상단 정보 (선택)")]
        [SerializeField] private TMP_Text levelText; // "LV. 1 (0%)" 표기용 (비워두면 무시)
        [SerializeField] private TMP_Text combatPowerText; // "전투력 :" 표기용 (비워두면 무시)

        [Header("표시 형식")]
        [SerializeField] private bool valueOnly; // 라벨이 분리된 카드에서는 값만 표시

        [Header("능력치 6종")]
        [SerializeField] private TMP_Text healthText; // 체력 표시
        [SerializeField] private TMP_Text attackText; // 공격력 표시
        [SerializeField] private TMP_Text defenseText; // 방어력 표시
        [SerializeField] private TMP_Text attackRangeText; // 사거리 표시
        [SerializeField] private TMP_Text attackSpeedText; // 공격속도 표시
        [SerializeField] private TMP_Text moveSpeedText; // 이동속도 표시

        private void OnEnable()
        {
            if (growthCalculator == null)
            {
                Debug.LogError("CurrentStatsView: growthCalculator reference is missing.", this); // 필수 참조 누락 경고
                return;
            }

            growthCalculator.BonusChanged += OnBonusChanged; // 강화될 때마다 UI 갱신
            Refresh(growthCalculator.CurrentBonus); // 패널이 켜질 때 최신값으로 바로 표시
        }

        private void OnDisable()
        {
            if (growthCalculator != null)
            {
                growthCalculator.BonusChanged -= OnBonusChanged; // 꺼질 때 구독 해제
            }
        }

        private void OnBonusChanged(LegionStatBonus bonus)
        {
            Refresh(bonus); // GrowthCalculator가 보낸 최신 보너스로 텍스트 갱신
        }

        private void Refresh(LegionStatBonus bonus)
        {
            // 라벨까지 포함한 형식으로 표기 (예: "체력 : 0.10")
            SetStatText(healthText, "체력", bonus.HealthRate);
            SetStatText(attackText, "공격력", bonus.AttackRate);
            SetStatText(defenseText, "방어력", bonus.DefenseRate);
            SetStatText(attackRangeText, "사거리", bonus.AttackRangeRate);
            SetStatText(attackSpeedText, "공격속도", bonus.AttackSpeedRate);
            SetStatText(moveSpeedText, "이동속도", bonus.MoveSpeedRate);

            // 전투력은 아직 공식 확정 전이라 6종 합을 임시 표시
            if (combatPowerText != null)
            {
                var tempPower =
                    bonus.HealthRate +
                    bonus.AttackRate +
                    bonus.DefenseRate +
                    bonus.AttackRangeRate +
                    bonus.AttackSpeedRate +
                    bonus.MoveSpeedRate;
                combatPowerText.text = $"전투력 : {tempPower:0.00}";
            }
        }

        // 군단장 레벨 표기는 나중에 CommanderProgressView 연동 시 사용
        public void SetCommanderLevel(int level, float progressPercent)
        {
            if (levelText == null)
            {
                return;
            }

            levelText.text = $"LV. {level} ({progressPercent:0}%)";
        }

        private void SetStatText(TMP_Text text, string label, float value)
        {
            if (text != null)
            {
                text.text = valueOnly ? $"{value:0.00}" : $"{label} : {value:0.00}"; // 카드형은 값만 표시
            }
        }
    }
}
