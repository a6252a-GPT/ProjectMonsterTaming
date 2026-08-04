using System;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class GrowthCalculator : MonoBehaviour // 능력치 6종 강화 계산·UI 반영
    {
        [Header("최대강화")]
        [SerializeField] private int maxGrowthLevel = 1000; // 스탯별 강화 가능한 최고 레벨

        [Header("강화당 증가량 (밸런싱 전 임시값)")]
        [SerializeField] private float growthRatePerLevel = 0.1f; // 레벨 1당 증가하는 보너스 비율 (임시 0.1)

        [Header("군단장 레벨/경험치 표시")]
        [SerializeField] private TMP_Text commanderLevelText; // UpgradePanel의 LevelText (군단장 LV. / 경험치%)
        [SerializeField] private long experienceForFullPercent = 1000; // 경험치 원본 숫자 → 100%로 환산할 때 분모  (임시값)

        [Header("체력")]
        [SerializeField] private Button healthButton; // 체력 강화 버튼
        [SerializeField] private TMP_Text healthLevelText; // 체력 레벨 표시 텍스트

        [Header("공격력")]
        [SerializeField] private Button attackButton; // 공격력 강화 버튼
        [SerializeField] private TMP_Text attackLevelText; // 공격력 레벨 표시 텍스트

        [Header("방어력")]
        [SerializeField] private Button defenseButton; // 방어력 강화 버튼
        [SerializeField] private TMP_Text defenseLevelText; // 방어력 레벨 표시 텍스트

        [Header("공격 속도")]
        [SerializeField] private Button attackSpeedButton; // 공격 속도 강화 버튼
        [SerializeField] private TMP_Text attackSpeedLevelText; // 공격 속도 레벨 표시 텍스트

        [Header("이동 속도")]
        [SerializeField] private Button moveSpeedButton; // 이동 속도 강화 버튼
        [SerializeField] private TMP_Text moveSpeedLevelText; // 이동 속도 레벨 표시 텍스트

        [Header("사거리")]
        [SerializeField] private Button attackRangeButton; // 사거리 강화 버튼
        [SerializeField] private TMP_Text attackRangeLevelText; // 사거리 레벨 표시 텍스트

        private int healthLevel = 1; // 체력 현재 강화 레벨 (기본 LV.1)
        private int attackLevel = 1; // 공격력 현재 강화 레벨
        private int defenseLevel = 1; // 방어력 현재 강화 레벨
        private int attackSpeedLevel = 1; // 공격 속도 현재 강화 레벨
        private int moveSpeedLevel = 1; // 이동 속도 현재 강화 레벨
        private int attackRangeLevel = 1; // 사거리 현재 강화 레벨

        private CommanderProgressView commanderProgress = new CommanderProgressView(1, 0L); // 군단장 레벨/경험치 원본
        private bool commanderInitialized; // Initialize 호출 여부

        public LegionStatBonus CurrentBonus { get; private set; } // 최신 계산된 강화 보너스 6종
        public int CommanderLevel => commanderProgress.Level; // 현재 군단장 레벨
        public float CommanderExperiencePercent => ConvertExperienceToPercent(commanderProgress.Experience); // 0~100% 환산값

        public event Action<LegionStatBonus> BonusChanged; // 강화 결과가 필요한 다른 시스템(전투 반영 등)이 구독

        private void Awake()
        {
            // 버튼 6개에 각 스탯 강화 함수 연결
            healthButton?.onClick.AddListener(GrowHealth);
            attackButton?.onClick.AddListener(GrowAttack);
            defenseButton?.onClick.AddListener(GrowDefense);
            attackSpeedButton?.onClick.AddListener(GrowAttackSpeed);
            moveSpeedButton?.onClick.AddListener(GrowMoveSpeed);
            attackRangeButton?.onClick.AddListener(GrowAttackRange);
        }

        private void Start()
        {
            if (!commanderInitialized)
            {
                // 아직 외부에서 Initialize를 안 불렀으면 기본값(Lv.1 / Exp.0)으로 표시
                ApplyCommanderProgress(new CommanderProgressView(1, 0L));
            }

            RecalculateBonus(); // 시작 시 기본값(LV.1 기준)으로 LegionStatBonus 계산
            RefreshAllLevelTexts(); // 모든 레벨 텍스트를 "LV. 1"로 표기
            RefreshCommanderLevelText(); // 군단장 LV / 경험치% 표기
        }

        private void OnDestroy()
        {
            // 오브젝트 파괴 시 이벤트 해제로 누수 방지
            healthButton?.onClick.RemoveListener(GrowHealth);
            attackButton?.onClick.RemoveListener(GrowAttack);
            defenseButton?.onClick.RemoveListener(GrowDefense);
            attackSpeedButton?.onClick.RemoveListener(GrowAttackSpeed);
            moveSpeedButton?.onClick.RemoveListener(GrowMoveSpeed);
            attackRangeButton?.onClick.RemoveListener(GrowAttackRange);
        }

        private void OnValidate()
        {
            // Inspector에서 잘못된 값(0 이하 최대강화, 음수 증가량) 입력 방지
            maxGrowthLevel = Mathf.Max(1, maxGrowthLevel);
            growthRatePerLevel = Mathf.Max(0f, growthRatePerLevel);
            experienceForFullPercent = Math.Max(1L, experienceForFullPercent); // 0 나누기 방지
        }

        // CommanderProgressView에서 Level/Experience 원본만 받아 UI에 반영한다.
        // Experience는 long 숫자이므로 여기서 0~100%로 환산한다.
        public void Initialize(CommanderProgressView progress)
        {
            ApplyCommanderProgress(progress);
            RefreshCommanderLevelText();
        }

        private void ApplyCommanderProgress(CommanderProgressView progress)
        {
            commanderProgress = progress;
            commanderInitialized = true;
        }

        // 경험치 원본 숫자 → 0~100% 환산
        // 예: experienceForFullPercent=1000 이고 Experience=250 이면 25%
        public float ConvertExperienceToPercent(long experience)
        {
            if (experienceForFullPercent <= 0L)
            {
                return 0f;
            }

            var percent = (float)experience / experienceForFullPercent * 100f;
            return Mathf.Clamp(percent, 0f, 100f);
        }

        private void RefreshCommanderLevelText()
        {
            if (commanderLevelText == null)
            {
                return;
            }

            var percent = ConvertExperienceToPercent(commanderProgress.Experience);
            commanderLevelText.text = $"군단장 LV. {commanderProgress.Level} ({percent:0}%)";
        }

        // 버튼 6개 각각의 클릭 콜백 - 공통 Grow 로직에 해당 스탯 레벨/텍스트만 전달
        private void GrowHealth() => Grow(ref healthLevel, healthLevelText);
        private void GrowAttack() => Grow(ref attackLevel, attackLevelText);
        private void GrowDefense() => Grow(ref defenseLevel, defenseLevelText);
        private void GrowAttackSpeed() => Grow(ref attackSpeedLevel, attackSpeedLevelText);
        private void GrowMoveSpeed() => Grow(ref moveSpeedLevel, moveSpeedLevelText);
        private void GrowAttackRange() => Grow(ref attackRangeLevel, attackRangeLevelText);

        private void Grow(ref int level, TMP_Text levelText)
        {
            if (level >= maxGrowthLevel)
            {
                return; // 최대치 도달 시 더 이상 증가 안 함
            }

            level++; // 레벨 1 증가
            RefreshLevelText(levelText, level); // 해당 스탯 텍스트를 "LV. {새 레벨}"로 갱신
            RecalculateBonus(); // 레벨 변경을 반영해 LegionStatBonus 재계산
        }

        private void RecalculateBonus()
        {
            // 각 스탯 레벨을 (레벨-1) * 증가량 공식으로 환산해 LegionStatBonus로 묶음
            CurrentBonus = new LegionStatBonus(
                (healthLevel - 1) * growthRatePerLevel,
                (attackLevel - 1) * growthRatePerLevel,
                (defenseLevel - 1) * growthRatePerLevel,
                (attackSpeedLevel - 1) * growthRatePerLevel,
                (moveSpeedLevel - 1) * growthRatePerLevel,
                (attackRangeLevel - 1) * growthRatePerLevel);

            BonusChanged?.Invoke(CurrentBonus); // 구독 중인 다른 시스템에 최신값 전달
        }

        private void RefreshAllLevelTexts()
        {
            // 시작 시점에 6개 텍스트 전부 현재 레벨로 초기 표기
            RefreshLevelText(healthLevelText, healthLevel);
            RefreshLevelText(attackLevelText, attackLevel);
            RefreshLevelText(defenseLevelText, defenseLevel);
            RefreshLevelText(attackSpeedLevelText, attackSpeedLevel);
            RefreshLevelText(moveSpeedLevelText, moveSpeedLevel);
            RefreshLevelText(attackRangeLevelText, attackRangeLevel);
        }

        private static void RefreshLevelText(TMP_Text text, int level)
        {
            if (text != null)
            {
                text.text = $"LV. {level}"; // 요청한 표기 형식 고정
            }
        }
    }
}
