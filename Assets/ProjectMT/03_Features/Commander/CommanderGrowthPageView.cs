using System;
using System.Collections.Generic;
using ProjectMT.Features.Equipment;
using ProjectMT.Features.MainBattle;
using ProjectMT.Features.Quest;
using ProjectMT.Shared.Commander;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.Stats;
using ProjectMT.Shared.UI;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Commander
{
    [DisallowMultipleComponent]
    public sealed class CommanderGrowthPageView : MonoBehaviour
    {
        [SerializeField] private Button closeButton;

        [Header("군단장 성장")]
        [SerializeField] private TMP_Text commanderLevelText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text experienceText;
        [SerializeField] private Slider experienceSlider;
        [SerializeField] private Button levelUpButton;
        [SerializeField] private TMP_Text levelUpButtonText;
        [SerializeField] private GameObject levelUpReadyBadge;
        [SerializeField] private TMP_Text goldText;

        // 능력치/잠재능력 탭 전환 및 잠재능력 행 표시(이름 기반 탐색, 인스펙터 연결 불필요).
        private sealed class PotentialRowRefs
        {
            public TMP_Text TextLevel;
            public TMP_Text UpTextLevel; // 최종 UI에서는 01~05 슬롯 번호
            public TMP_Text ResultValue;
            public TMP_Text LockStateText;
            public Image GradeBadge;
            public Button ProtectButton;
            public TMP_Text Text;
            public List<TMP_Text> ExtraTexts; // 프리팹 복제 과정에서 남은 목업 문구("Unlocks at..." 등) 정리용
            public GameObject IconOn; // 자물쇠 켜짐 오브젝트(더 이상 직접 켜지 않음, 스프라이트만 빌려 씀)
            public GameObject IconOff; // 자물쇠 아이콘을 실제로 보여주는 오브젝트(항상 이거 하나만 켜둔다)
            public Image LockIcon; // IconOff의 Image. 잠금 상태에 따라 스프라이트만 갈아 끼운다.
            public Sprite LockedSprite; // IconOn에 원래 물려있던 "잠김" 스프라이트
            public Sprite UnlockedSprite; // IconOff에 원래 물려있던 "해제" 스프라이트
            public GameObject Lock; // 아직 해금 단계에 도달하지 않은 슬롯을 가리는 오버레이
        }

        private Button statsTabButton;
        private GameObject statsTabFocus;
        private Button potentialTabButton;
        private GameObject potentialTabFocus;
        private GameObject growthScrollView;
        private GameObject commanderLevelProgress;
        private GameObject potentialPanel;
        private TMP_Text potentialSummaryText; // "잠재능력 강화석  N" 보유량 표시
        private TMP_Text potentialText; // 잠재 각성 단계 표시
        private TMP_Text potentialNextText;
        private TMP_Text topGoldValueText;
        private TMP_Text topTrainingValueText;
        private TMP_Text topPotentialValueText;
        private Slider potentialSlider; // 위 경험치를 시각적으로 보여주는 슬라이더
        private readonly PotentialRowRefs[] potentialRows = new PotentialRowRefs[CommanderPotentialData.SlotCount];

        // 퀘스트 클릭 힌트에서 현재 열려 있는 탭을 판단할 때 사용한다.
        public bool IsPotentialTabSelected => potentialPanel != null && potentialPanel.activeInHierarchy;
        public Button QuestStatsTabButton => statsTabButton;
        public Button QuestPotentialTabButton => potentialTabButton;

        private IGameProgressService progress;
        private CommanderGrowthConfig config;
        private Action combatInputSaved;
        private GrowthCalculator growthCalculator;
        private BattlePartySnapshot party;
        private bool savePending;

        private void Awake()
        {
            closeButton?.onClick.AddListener(Close);
            levelUpButton?.onClick.AddListener(LevelUp);

            CachePotentialReferences();
            statsTabButton?.onClick.AddListener(() => SelectGrowthTab(false));
            potentialTabButton?.onClick.AddListener(() => SelectGrowthTab(true));
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
            SelectGrowthTab(false); // 창을 열 때마다 능력치 탭이 기본으로 보이도록 초기화
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            Unsubscribe(); // 자식 UI가 먼저 파괴돼도 서비스 구독부터 정리
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
            }

            if (levelUpButton != null)
            {
                levelUpButton.onClick.RemoveListener(LevelUp);
            }
        }

        public void Configure(
            IGameProgressService progressService,
            CommanderGrowthConfig growthConfig,
            EquipmentBalanceConfig equipmentBalanceConfig = null,
            Action onCombatInputSaved = null)
        {
            Unsubscribe();
            progress = progressService;
            config = growthConfig;
            combatInputSaved = onCombatInputSaved;
            growthCalculator ??= GetComponent<GrowthCalculator>();
            growthCalculator?.Configure(progressService, growthConfig, onCombatInputSaved);
            CommanderPotentialRuntime.Configure(progressService, equipmentBalanceConfig);
            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            Refresh();
        }

        public void SetParty(BattlePartySnapshot snapshot)
        {
            party = snapshot;
            Refresh();
        }

        public void Close()
        {
            UIPanelPopAnimator.RequestClose(gameObject);
        }

        // 퀘스트 이동 등 외부에서 잠재능력 탭으로 바로 전환해야 할 때 사용.
        public void SelectPotentialTab()
        {
            SelectGrowthTab(true);
        }

        private void Refresh()
        {
            if (this == null || progress == null || !progress.IsLoaded || config == null)
            {
                return;
            }

            var progressView = progress.View;
            var commander = progressView.Commander;
            var requirement = config.GetExperienceRequirement(commander.Level);
            var progressRatio = config.GetProgressRatio(commander.Level, commander.Experience);
            var progress01 = config.GetProgress01(commander.Level, commander.Experience);
            var isMaxLevel = commander.Level >= config.MaxLevel;
            var canLevelUp = !isMaxLevel && config.CanLevelUp(commander.Level, commander.Experience);

            if (topGoldValueText != null && topTrainingValueText != null)
            {
                SetText(topGoldValueText, progressView.Gold.ToString("N0"));
                SetText(topTrainingValueText, progressView.CommanderLegionGrowth.UnspentTrainingPoints.ToString("N0"));
                SetText(goldText, string.Empty);
            }
            else
            {
                SetText(
                    goldText,
                    $"보유 골드  {progressView.Gold:N0}  |  훈련 포인트  " +
                    $"{progressView.CommanderLegionGrowth.UnspentTrainingPoints:N0}");
            }
            SetText(
                commanderLevelText,
                $"군단장 LV. {commander.Level:N0} ({FormatPercent(progressRatio)})  ·  " +
                $"전투력 {(party?.TotalPower ?? 0f):N0}");
            SetText(levelText, $"LV. {commander.Level:N0}");
            SetText(
                experienceText,
                isMaxLevel
                    ? "MAX"
                    : $"{commander.Experience:N0} / {requirement:N0} ({FormatPercent(progressRatio)})");
            if (experienceSlider != null)
            {
                experienceSlider.SetValueWithoutNotify(progress01);
            }

            if (levelUpButton != null)
            {
                levelUpButton.interactable = canLevelUp && !savePending;
            }

            SetText(levelUpButtonText, isMaxLevel ? "MAX" : "레벨 업");
            SetActiveSafe(levelUpReadyBadge, canLevelUp);
            NormalizeTopResourceLayout();
        }

        private void Subscribe()
        {
            if (progress != null)
            {
                progress.Changed -= Refresh;
                progress.Changed += Refresh;
            }

            CommanderPotentialRuntime.Changed -= RefreshPotentialPanel;
            CommanderPotentialRuntime.Changed += RefreshPotentialPanel;
            KeepPotentialTabAvailable();
        }

        private void Unsubscribe()
        {
            if (progress != null)
            {
                progress.Changed -= Refresh;
            }

            CommanderPotentialRuntime.Changed -= RefreshPotentialPanel;
        }

        private async void LevelUp()
        {
            if (savePending || progress == null || !progress.IsLoaded || config == null)
            {
                return;
            }

            var commander = progress.View.Commander;
            if (!config.CanLevelUp(commander.Level, commander.Experience))
            {
                Refresh();
                return;
            }

            savePending = true;
            Refresh();
            try
            {
                var saved = await progress.TryApplyAndSaveAsync(GameProgressChange.LevelUpCommander(commander.Level));
                if (saved)
                {
                    _ = QuestRuntime.AdvanceAllOfConditionAsync(QuestConditionType.CommanderLevelUp, 1L);
                }
            }
            finally
            {
                savePending = false;
                Refresh();
            }
        }

        private static string FormatPercent(double ratio) => $"{Math.Max(0d, ratio) * 100d:0.0}%";

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        // "단계 개방 필요" 안내문이 기존 목업 폰트 크기(36 등)로 인해 줄바꿈/잘림이 나는 것을 막기 위해
        // 자동 크기 조절을 활성화해서 항상 한 줄에 들어오도록 한다.
        private static void SetTextFit(TMP_Text target, string value, float minSize = 12f, float maxSize = 24f)
        {
            if (target == null)
            {
                return;
            }

            target.text = value;
            target.enableAutoSizing = true;
            target.fontSizeMin = minSize;
            target.fontSizeMax = maxSize;
        }

        // ---------------------------------------------------------------
        // 능력치 / 잠재능력 탭 전환
        // ---------------------------------------------------------------

        // 능력치 탭이 기본, 잠재능력 탭을 누르면 능력치 쪽 콘텐츠를 숨기고 PotentialPanel을 보여준다.
        private void SelectGrowthTab(bool potential)
        {
            NormalizeGrowthTabLayout(statsTabButton?.transform, potentialTabButton?.transform);
            SetActiveSafe(statsTabFocus, !potential);
            SetActiveSafe(potentialTabFocus, potential);
            SetActiveSafe(growthScrollView, !potential);
            SetActiveSafe(commanderLevelProgress, !potential);
            SetActiveSafe(potentialPanel, potential);

            if (potential)
            {
                RefreshPotentialPanel();
                TriggerInitialPotentialRoll();
            }
        }

        private void KeepPotentialTabAvailable()
        {
            if (potentialTabButton == null)
            {
                return;
            }

            potentialTabButton.interactable = true;
            var lockBadge = FindDeep(potentialTabButton.transform, "LockBadge");
            SetActiveSafe(lockBadge != null ? lockBadge.gameObject : null, false);
        }

        private async void TriggerInitialPotentialRoll()
        {
            if (await CommanderPotentialRuntime.EnsureInitialRollAsync())
            {
                combatInputSaved?.Invoke();
            }
        }

        // "잠재 능력 변경" 버튼(ButtonArea_2): 강화석 1개 소모 후 잠기지 않은 슬롯을 완전히 새 옵션으로 재추첨.
        // 이 클릭으로 "수호자의 힘" 단계가 올라 슬롯이 새로 해금되어도 그 자리는 비어있는 채로 둔다(자동 배정 없음).
        private async void TriggerPotentialReroll()
        {
            if (await CommanderPotentialRuntime.TryRerollAsync())
            {
                combatInputSaved?.Invoke();
            }
        }

        // "옵션 스탯 변경" 버튼(ButtonArea_1): 강화석 1개 소모 후 옵션 종류·등급은 유지하고 수치만 다시 뽑는다.
        private async void TriggerPotentialValueReroll()
        {
            if (await CommanderPotentialRuntime.TryRerollValueAsync())
            {
                combatInputSaved?.Invoke();
            }
        }

        // ---------------------------------------------------------------
        // 잠재능력 행(GrowthRow_one ~ Five) 표시
        // ---------------------------------------------------------------

        private void RefreshPotentialPanel()
        {
            SetText(potentialSummaryText, $"잠재능력 강화석  {CommanderPotentialRuntime.StoneBalance:N0}");
            SetText(topPotentialValueText, CommanderPotentialRuntime.StoneBalance.ToString("N0"));

            // "수호자의 힘" 진행도: 잠재 능력 변경 1회당 +10, 100이 되면 다음 단계로 승급하며 슬롯이 추가 해금된다.
            var stage = CommanderPotentialRuntime.Stage;
            var experience = CommanderPotentialRuntime.Experience;
            var requirement = CommanderPotentialRuntime.ExperiencePerStage;
            var ratio = CommanderPotentialRuntime.ExperienceRatio01;
            SetText(potentialText, $"잠재 각성 단계 {stage:N0} / {CommanderPotentialData.MaxStage:N0}");
            SetText(potentialNextText, CommanderPotentialRuntime.IsMaxStage
                ? "최대 각성 단계"
                : $"다음 슬롯까지  {experience:N0} / {requirement:N0}");
            if (potentialSlider != null)
            {
                potentialSlider.SetValueWithoutNotify(ratio);
            }

            for (var i = 0; i < potentialRows.Length; i++)
            {
                RefreshPotentialRow(i);
            }
        }

        private void RefreshPotentialRow(int index)
        {
            var row = potentialRows[index];
            if (row == null)
            {
                return;
            }

            var slot = CommanderPotentialRuntime.GetSlot(index);
            SetText(row.UpTextLevel, (index + 1).ToString("00"));
            if (row.ProtectButton != null)
            {
                row.ProtectButton.interactable = slot.HasValue;
            }

            if (slot.HasValue)
            {
                var range = CommanderPotentialOptionTable.GetOption(slot.OptionType, slot.Grade);
                var statName = EquipmentOptionInfo.GetDisplayName(slot.OptionType);
                SetText(row.TextLevel, CommanderPotentialOptionTable.GetGradeDisplayName(slot.Grade));
                SetTextFit(row.Text,
                    $"{statName}\n<color=#9E96A1>범위 {range.MinValue:0.0} ~ {range.MaxValue:0.0}%</color>", 12f, 20f);
                SetText(row.ResultValue, $"+{slot.Value:0.0}%");
                SetText(row.LockStateText, slot.Locked ? "보호 ON" : "보호 OFF");
                ApplyPotentialGradeVisual(row, slot.Grade);
                SetActiveSafe(row.Lock, false);
            }
            else
            {
                var unlocked = index < CommanderPotentialRuntime.UnlockedSlotCount;
                var requiredStage = index + 1;
                var ordinals = new[] { "첫", "두", "세", "네", "다섯" };
                var lockedMessage = unlocked
                    ? $"{ordinals[index]} 번째 잠재 슬롯\n<color=#77717B>옵션 배정 대기 중</color>"
                    : $"{ordinals[index]} 번째 잠재 슬롯\n<color=#77717B>각성 {requiredStage}단계에서 해방</color>";

                SetText(row.TextLevel, string.Empty);
                SetTextFit(row.Text, lockedMessage, 12f, 18f);
                SetText(row.ResultValue, string.Empty);
                SetText(row.LockStateText, unlocked ? "대기" : "잠김");
                if (row.GradeBadge != null)
                {
                    row.GradeBadge.color = new Color32(48, 44, 53, 255);
                }
                SetActiveSafe(row.Lock, true);
            }

            if (row.ExtraTexts != null)
            {
                for (var i = 0; i < row.ExtraTexts.Count; i++)
                {
                    SetText(row.ExtraTexts[i], string.Empty);
                }
            }

            if (row.LockIcon != null)
            {
                row.LockIcon.sprite = slot.Locked && row.LockedSprite != null ? row.LockedSprite : row.UnlockedSprite;
            }

            SetActiveSafe(row.IconOn, false);
            SetActiveSafe(row.IconOff, true);
        }

        private static void ApplyPotentialGradeVisual(PotentialRowRefs row, EquipmentGrade grade)
        {
            var color = grade switch
            {
                EquipmentGrade.Common => new Color32(111, 180, 103, 255),
                EquipmentGrade.Rare => new Color32(77, 140, 255, 255),
                EquipmentGrade.Epic => new Color32(204, 99, 224, 255),
                EquipmentGrade.Legendary => new Color32(232, 195, 64, 255),
                EquipmentGrade.Mythic => new Color32(238, 72, 82, 255),
                _ => new Color32(190, 184, 194, 255)
            };

            if (row.TextLevel != null) row.TextLevel.color = color;
            if (row.ResultValue != null) row.ResultValue.color = color;
            if (row.GradeBadge != null)
            {
                // Color32 채널(0~255)을 Color 생성자(0~1)에 그대로 넣으면 값이 포화되어
                // 등급 배지가 흰색으로 렌더링된다. 먼저 정규화한 뒤 어두운 등급색을 만든다.
                var normalizedColor = (Color)color;
                row.GradeBadge.color = new Color(
                    normalizedColor.r * 0.35f,
                    normalizedColor.g * 0.35f,
                    normalizedColor.b * 0.35f,
                    1f);
            }
        }
        // ---------------------------------------------------------------
        // 참조 탐색 (이름 기반, 프리팹 내부 구조를 몰라도 안전하게 찾을 수 있도록 재귀 탐색한다)
        // ---------------------------------------------------------------

        private void CachePotentialReferences()
        {
            var statsTabTransform = FindDeep(transform, "StatsTab");
            var potentialTabTransform = FindDeep(transform, "PotentialTab");
            NormalizeGrowthTabLayout(statsTabTransform, potentialTabTransform);

            if (statsTabTransform != null)
            {
                statsTabButton = EnsureButton(statsTabTransform);
                statsTabFocus = FindDeep(statsTabTransform, "Focus")?.gameObject;
            }

            if (potentialTabTransform != null)
            {
                potentialTabButton = EnsureButton(potentialTabTransform);
                potentialTabFocus = FindDeep(potentialTabTransform, "Focus")?.gameObject;
            }

            growthScrollView = FindDeep(transform, "GrowthScrollView_MOBILE")?.gameObject;
            commanderLevelProgress = FindDeep(transform, "CommanderLevelProgress")?.gameObject;
            potentialPanel = FindDeep(transform, "PotentialPanel")?.gameObject;
            topGoldValueText = FindDeep(transform, "TopGoldValue")?.GetComponent<TMP_Text>();
            topTrainingValueText = FindDeep(transform, "TopTrainingValue")?.GetComponent<TMP_Text>();
            topPotentialValueText = FindDeep(transform, "TopPotentialValue")?.GetComponent<TMP_Text>();
            NormalizeTopResourceLayout();

            // "잠재능력 강화석  N" 보유량 표시. 오브젝트 자체에 TMP_Text가 있을 수도, 자식에 있을 수도 있어 둘 다 시도한다.
            var potentialSummaryTransform = FindDeep(transform, "PotentialSummary");
            potentialSummaryText = potentialSummaryTransform?.GetComponent<TMP_Text>()
                ?? potentialSummaryTransform?.GetComponentInChildren<TMP_Text>(true);
            if (potentialSummaryTransform != null && potentialSummaryText == null)
            {
                Debug.LogWarning("CommanderGrowthPageView: PotentialSummary 안에서 텍스트 컴포넌트를 찾지 못했습니다.", this);
            }

            // "수호자의 힘" 단계/경험치 텍스트·슬라이더.
            var potentialTextTransform = FindDeep(transform, "PotentialText");
            potentialText = potentialTextTransform?.GetComponent<TMP_Text>()
                ?? potentialTextTransform?.GetComponentInChildren<TMP_Text>(true);
            if (potentialTextTransform != null && potentialText == null)
            {
                Debug.LogWarning("CommanderGrowthPageView: PotentialText 안에서 텍스트 컴포넌트를 찾지 못했습니다.", this);
            }
            potentialNextText = FindDeep(transform, "PotentialNextText")?.GetComponent<TMP_Text>();

            var potentialSliderTransform = FindDeep(transform, "PotentialSlider");
            potentialSlider = potentialSliderTransform?.GetComponent<Slider>();
            if (potentialSliderTransform != null && potentialSlider == null)
            {
                Debug.LogWarning("CommanderGrowthPageView: PotentialSlider에 Slider 컴포넌트가 없습니다.", this);
            }

            // "옵션 스탯 변경" 버튼: 강화석 1개를 소모해 옵션 종류·등급은 유지하고 수치만 재추첨한다.
            var valueRerollButtonTransform = FindDeep(transform, "ButtonArea_1");
            if (valueRerollButtonTransform != null)
            {
                EnsureButton(valueRerollButtonTransform).onClick.AddListener(TriggerPotentialValueReroll);
            }
            else
            {
                Debug.LogWarning("CommanderGrowthPageView: ButtonArea_1(옵션 스탯 변경) 오브젝트를 찾지 못했습니다.", this);
            }

            // "잠재 능력 변경" 버튼: 강화석 1개를 소모해 잠기지 않은 슬롯들을 새 옵션으로 재추첨한다.
            var rerollButtonTransform = FindDeep(transform, "ButtonArea_2");
            if (rerollButtonTransform != null)
            {
                EnsureButton(rerollButtonTransform).onClick.AddListener(TriggerPotentialReroll);
            }
            else
            {
                Debug.LogWarning("CommanderGrowthPageView: ButtonArea_2(잠재 능력 변경) 오브젝트를 찾지 못했습니다.", this);
            }

            CachePotentialRow(0, "GrowthRow_one");
            CachePotentialRow(1, "GrowthRow_Two");
            CachePotentialRow(2, "GrowthRow_Three");
            CachePotentialRow(3, "GrowthRow_Four");
            CachePotentialRow(4, "GrowthRow_Five");

            if (statsTabButton == null || potentialTabButton == null)
            {
                Debug.LogWarning("CommanderGrowthPageView: StatsTab/PotentialTab 오브젝트를 찾지 못했습니다.", this);
            }

            if (potentialPanel == null)
            {
                Debug.LogWarning("CommanderGrowthPageView: PotentialPanel 오브젝트를 찾지 못했습니다.", this);
            }
        }

        private static void ShiftPotentialRowContentLeft(Transform rowTransform)
        {
            const float offsetX = -14f;
            var contentNames = new[]
            {
                "UpText_Level", "GradeBadge", "Text_Level", "Text", "ResultValue",
                "ProtectSurface", "LockStateText", "IconOn", "IconOff"
            };

            for (var i = 0; i < contentNames.Length; i++)
            {
                ShiftAnchoredX(FindDeep(rowTransform, contentNames[i]) as RectTransform, offsetX);
            }

            var lockOverlay = FindDeep(rowTransform, "Lock");
            ShiftAnchoredX(FindDeep(lockOverlay, "Icon") as RectTransform, offsetX);
        }

        private static void ShiftAnchoredX(RectTransform rect, float offsetX)
        {
            if (rect != null)
            {
                rect.anchoredPosition += new Vector2(offsetX, 0f);
            }
        }

        private void CachePotentialRow(int index, string rowName)
        {
            var rowTransform = FindDeep(transform, rowName);
            if (rowTransform == null)
            {
                Debug.LogWarning($"CommanderGrowthPageView: {rowName} 오브젝트를 찾지 못했습니다.", this);
                return;
            }

            ShiftPotentialRowContentLeft(rowTransform);

            // "Text" 이름이 없으면 TMP 기본 이름("Text (TMP)")으로 복제된 행일 수 있으므로 대체 탐색한다.
            var textLevelComponent = FindDeep(rowTransform, "Text_Level")?.GetComponent<TMP_Text>();
            var upTextLevelComponent = FindDeep(rowTransform, "UpText_Level")?.GetComponent<TMP_Text>();
            var textComponent = FindDeep(rowTransform, "Text")?.GetComponent<TMP_Text>()
                ?? FindDeep(rowTransform, "Text (TMP)")?.GetComponent<TMP_Text>();
            var resultValueComponent = FindDeep(rowTransform, "ResultValue")?.GetComponent<TMP_Text>();
            var lockStateComponent = FindDeep(rowTransform, "LockStateText")?.GetComponent<TMP_Text>();
            var gradeBadgeImage = FindDeep(rowTransform, "GradeBadge")?.GetComponent<Image>();
            var protectSurfaceTransform = FindDeep(rowTransform, "ProtectSurface");
            var protectButton = protectSurfaceTransform != null ? EnsureButton(protectSurfaceTransform) : null;
            if (lockStateComponent != null)
            {
                lockStateComponent.raycastTarget = false;
            }

            // 행 안에 우리가 쓰는 텍스트(Text_Level, UpText_Level, Text) 말고 다른 TMP_Text가 남아있으면
            // 프리팹 복제 과정에서 남은 목업 문구(예: "Unlocks at Attack Speed Lv200")일 가능성이 커서
            // 매번 비워주는 대상으로 따로 모아둔다.
            var extraTexts = new List<TMP_Text>();
            var allTexts = rowTransform.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < allTexts.Length; i++)
            {
                var candidate = allTexts[i];
                if (candidate == textLevelComponent || candidate == upTextLevelComponent || candidate == textComponent ||
                    candidate == resultValueComponent || candidate == lockStateComponent)
                {
                    continue;
                }

                extraTexts.Add(candidate);
            }

            var iconOnTransform = FindDeep(rowTransform, "IconOn");
            var iconOffTransform = FindDeep(rowTransform, "IconOff");
            var iconOnImage = iconOnTransform?.GetComponent<Image>();
            var iconOffImage = iconOffTransform?.GetComponent<Image>();

            // "IconOn"/"IconOff" 두 오브젝트를 각각 켜고 끄는 방식은 한쪽이 프리팹 설정
            // 때문에 화면에 안 보이는 문제가 있었다. 이제 "IconOff" 오브젝트 하나만 항상 켜두고, 원래 각
            // 오브젝트에 물려있던 스프라이트(잠김/해제)만 상황에 맞게 갈아 끼우는 방식으로 바꿨다.
            // 클릭 판정은 그대로 두 오브젝트 모두에 걸어둔다(리스너 자체는 오브젝트가 꺼져 있어도 등록에 문제없다).
            if (iconOnTransform != null)
            {
                EnsureButton(iconOnTransform).onClick.AddListener(() => ToggleSlotLock(index));
                iconOnTransform.gameObject.SetActive(false); // 더 이상 별도로 켜지 않는다.
            }

            if (iconOffTransform != null)
            {
                EnsureButton(iconOffTransform).onClick.AddListener(() => ToggleSlotLock(index));
            }

            if (protectButton != null)
            {
                protectButton.onClick.AddListener(() => ToggleSlotLock(index));
            }

            potentialRows[index] = new PotentialRowRefs
            {
                TextLevel = textLevelComponent,
                UpTextLevel = upTextLevelComponent,
                Text = textComponent,
                ResultValue = resultValueComponent,
                LockStateText = lockStateComponent,
                GradeBadge = gradeBadgeImage,
                ProtectButton = protectButton,
                ExtraTexts = extraTexts,
                IconOn = iconOnTransform?.gameObject,
                IconOff = iconOffTransform?.gameObject,
                LockIcon = iconOffImage,
                LockedSprite = iconOnImage != null ? iconOnImage.sprite : null,
                UnlockedSprite = iconOffImage != null ? iconOffImage.sprite : null,
                Lock = FindDeep(rowTransform, "Lock")?.gameObject
            };
        }

        private async void ToggleSlotLock(int index)
        {
            await CommanderPotentialRuntime.ToggleLockAsync(index);
        }

        private static void SetActiveSafe(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private void NormalizeTopResourceLayout()
        {
            var bar = FindDeep(transform, "TopResourceBar") as RectTransform;
            if (bar == null)
            {
                return;
            }

            SetFixedRect(bar, bar.anchoredPosition, new Vector2(570f, 50f));
            NormalizeResourceCell(bar, "TopGoldIcon", topGoldValueText, -190f);
            NormalizeResourceCell(bar, "TopTrainingIcon", topTrainingValueText, 0f);
            NormalizeResourceCell(bar, "TopPotentialIcon", topPotentialValueText, 190f);
            NormalizeDivider(bar, "WalletDivider_1", -95f);
            NormalizeDivider(bar, "WalletDivider_2", 95f);
        }

        private static void NormalizeResourceCell(
            Transform bar,
            string iconName,
            TMP_Text valueText,
            float centerX)
        {
            var icon = FindDeep(bar, iconName) as RectTransform;
            if (icon == null || valueText == null)
            {
                return;
            }

            const float iconSize = 26f;
            const float iconOffsetX = -52f;
            const float valueOffsetX = 18f;
            const float valueWidth = 100f;

            SetFixedRect(icon, new Vector2(centerX + iconOffsetX, 0f), new Vector2(iconSize, iconSize));
            SetFixedRect(
                valueText.rectTransform,
                new Vector2(centerX + valueOffsetX, 0f),
                new Vector2(valueWidth, 32f));
            valueText.enableWordWrapping = false;
            valueText.enableAutoSizing = false;
            valueText.alignment = TextAlignmentOptions.Center;
            valueText.overflowMode = TextOverflowModes.Overflow;
        }

        private static void NormalizeDivider(Transform bar, string name, float x)
        {
            var divider = FindDeep(bar, name) as RectTransform;
            SetFixedRect(divider, new Vector2(x, 0f), new Vector2(1f, 36f));
        }

        private static void NormalizeGrowthTabLayout(Transform statsTab, Transform potentialTab)
        {
            var menu = statsTab?.parent as RectTransform ?? potentialTab?.parent as RectTransform;
            if (menu != null)
            {
                var layout = menu.GetComponent<HorizontalLayoutGroup>();
                if (layout != null)
                {
                    layout.enabled = false;
                }

                SetFixedRect(menu, new Vector2(0f, 293f), new Vector2(570f, 50f));
            }

            NormalizeGrowthTab(statsTab, -144.5f);
            NormalizeGrowthTab(potentialTab, 144.5f);
        }

        private static void NormalizeGrowthTab(Transform tab, float x)
        {
            if (tab == null)
            {
                return;
            }

            tab.gameObject.SetActive(true);
            SetFixedRect(tab as RectTransform, new Vector2(x, 0f), new Vector2(281f, 50f));
            var label = tab.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                var rect = label.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(-16f, -16f);
                rect.localScale = Vector3.one;
                label.enableWordWrapping = false;
                label.enableAutoSizing = false;
                label.fontSize = 20f;
                label.alignment = TextAlignmentOptions.Center;
                label.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        private static void SetFixedRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static Button EnsureButton(Transform target)
        {
            // 이미지로만 만들어둔 오브젝트는 Raycast Target이 꺼져있으면
            // Button을 붙여도 클릭 판정을 받지 못한다. 켜져 있도록 강제한다.
            var graphic = target.GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.raycastTarget = true;
            }

            var button = target.GetComponent<Button>();
            if (button == null)
            {
                button = target.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None; // 목업 비주얼을 그대로 유지, 클릭 판정만 추가
            }

            button.interactable = true;
            if (button.targetGraphic == null && graphic != null)
            {
                button.targetGraphic = graphic;
            }

            return button;
        }

        private static Transform FindDeep(Transform root, string childName)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].name == childName)
                {
                    return all[i];
                }
            }

            return null;
        }
    }
}

