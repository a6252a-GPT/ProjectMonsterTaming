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
            public TMP_Text UpTextLevel; // "Text_Level"(등급 이름) 위에 붙는 "[등급]" 고정 라벨
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
        private TMP_Text potentialText; // "수호자의 힘" 단계/경험치 표시("1단계    0 / 100 (0.0%)")
        private Slider potentialSlider; // 위 경험치를 시각적으로 보여주는 슬라이더
        private readonly PotentialRowRefs[] potentialRows = new PotentialRowRefs[CommanderPotentialData.SlotCount];

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

            SetText(
                goldText,
                $"보유 골드  {progressView.Gold:N0}  |  훈련 포인트  " +
                $"{progressView.CommanderLegionGrowth.UnspentTrainingPoints:N0}");
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
            QuestRuntime.Changed -= RefreshPotentialUnlock;
            QuestRuntime.Changed += RefreshPotentialUnlock;
            RefreshPotentialUnlock();
        }

        private void Unsubscribe()
        {
            if (progress != null)
            {
                progress.Changed -= Refresh;
            }

            CommanderPotentialRuntime.Changed -= RefreshPotentialPanel;
            QuestRuntime.Changed -= RefreshPotentialUnlock;
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
            if (potential && !QuestRuntime.IsUnlocked(QuestUnlockTarget.CommanderPotential))
            {
                return;
            }

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

        private void RefreshPotentialUnlock()
        {
            if (potentialTabButton == null)
            {
                return;
            }

            var unlocked = QuestRuntime.IsUnlocked(QuestUnlockTarget.CommanderPotential);
            potentialTabButton.interactable = unlocked;
            var lockBadge = FindDeep(potentialTabButton.transform, "LockBadge");
            SetActiveSafe(lockBadge != null ? lockBadge.gameObject : null, !unlocked);
            if (!unlocked && potentialPanel != null && potentialPanel.activeSelf)
            {
                SelectGrowthTab(false);
            }
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

            // "수호자의 힘" 진행도: 잠재 능력 변경 1회당 +10, 100이 되면 다음 단계로 승급하며 슬롯이 추가 해금된다.
            var stage = CommanderPotentialRuntime.Stage;
            var experience = CommanderPotentialRuntime.Experience;
            var requirement = CommanderPotentialRuntime.ExperiencePerStage;
            var ratio = CommanderPotentialRuntime.ExperienceRatio01;
            SetText(potentialText, $"{stage}단계    {experience:N0} / {requirement:N0} ({ratio * 100d:0.0}%)");
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
            if (slot.HasValue)
            {
                var range = CommanderPotentialOptionTable.GetOption(slot.OptionType, slot.Grade);
                var statName = EquipmentOptionInfo.GetDisplayName(slot.OptionType);
                // "등급 : " 접두어 없이 등급 이름만("일반", "희귀" 등) 표기.
                // 접두어는 별도로 분리해둔 "UpText_Level"에 고정 라벨("[등급]")로 표시한다.
                SetText(row.UpTextLevel, "[등급]");
                SetText(row.TextLevel, CommanderPotentialOptionTable.GetGradeDisplayName(slot.Grade));
                // 적용값을 정수로 반올림하면 좁은 범위 안에서는 재추첨 전후가
                // 같은 숫자로 보여서 "옵션 스탯 변경"을 눌러도 안 바뀐 것처럼 보인다. 소수 1자리로 표시.
                SetTextFit(
                    row.Text,
                    $"{statName} (+{range.MinValue:0.0}% ~ {range.MaxValue:0.0}%)  +{slot.Value:0.0}%");
                SetActiveSafe(row.Lock, false);
            }
            else
            {
                var unlocked = index < CommanderPotentialRuntime.UnlockedSlotCount;
                var requiredStage = index + 1;
                var lockedMessage = unlocked ? "대기 중" : $"{requiredStage}단계 개방 필요";

                SetText(row.UpTextLevel, string.Empty);
                SetText(row.TextLevel, string.Empty);
                SetTextFit(row.Text, lockedMessage);
                SetActiveSafe(row.Lock, true);
            }

            // 프리팹 복제 과정에서 남아있는 목업 문구(예: "Unlocks at Attack Speed Lv200")가
            // 우리가 쓰는 텍스트 뒤에 겹쳐 보이는 문제를 막기 위해 그 외의 텍스트는 전부 비워둔다.
            if (row.ExtraTexts != null)
            {
                for (var i = 0; i < row.ExtraTexts.Count; i++)
                {
                    SetText(row.ExtraTexts[i], string.Empty);
                }
            }

            // "IconOn"/"IconOff" 두 오브젝트를 번갈아 켜고 끄는 방식은 한쪽이 프리팹
            // 설정(스프라이트가 없거나 렌더 순서 등) 때문에 안 보이는 경우가 있었다. 그래서 "IconOff" 오브젝트
            // 하나만 항상 켜두고, 그 안의 스프라이트만 잠금/해제 상태에 맞게 갈아 끼우는 방식으로 바꿨다.
            // 빈 슬롯은 잠글 수 없으므로 항상 "해제" 스프라이트로 유지된다.
            if (row.LockIcon != null)
            {
                row.LockIcon.sprite = slot.Locked && row.LockedSprite != null ? row.LockedSprite : row.UnlockedSprite;
            }

            SetActiveSafe(row.IconOn, false);
            SetActiveSafe(row.IconOff, true);
        }

        // ---------------------------------------------------------------
        // 참조 탐색 (이름 기반, 프리팹 내부 구조를 몰라도 안전하게 찾을 수 있도록 재귀 탐색한다)
        // ---------------------------------------------------------------

        private void CachePotentialReferences()
        {
            var statsTabTransform = FindDeep(transform, "StatsTab");
            var potentialTabTransform = FindDeep(transform, "PotentialTab");

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

        private void CachePotentialRow(int index, string rowName)
        {
            var rowTransform = FindDeep(transform, rowName);
            if (rowTransform == null)
            {
                Debug.LogWarning($"CommanderGrowthPageView: {rowName} 오브젝트를 찾지 못했습니다.", this);
                return;
            }

            // "Text" 이름이 없으면 TMP 기본 이름("Text (TMP)")으로 복제된 행일 수 있으므로 대체 탐색한다.
            var textLevelComponent = FindDeep(rowTransform, "Text_Level")?.GetComponent<TMP_Text>();
            var upTextLevelComponent = FindDeep(rowTransform, "UpText_Level")?.GetComponent<TMP_Text>();
            var textComponent = FindDeep(rowTransform, "Text")?.GetComponent<TMP_Text>()
                ?? FindDeep(rowTransform, "Text (TMP)")?.GetComponent<TMP_Text>();

            // 행 안에 우리가 쓰는 텍스트(Text_Level, UpText_Level, Text) 말고 다른 TMP_Text가 남아있으면
            // 프리팹 복제 과정에서 남은 목업 문구(예: "Unlocks at Attack Speed Lv200")일 가능성이 커서
            // 매번 비워주는 대상으로 따로 모아둔다.
            var extraTexts = new List<TMP_Text>();
            var allTexts = rowTransform.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < allTexts.Length; i++)
            {
                var candidate = allTexts[i];
                if (candidate == textLevelComponent || candidate == upTextLevelComponent || candidate == textComponent)
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

            potentialRows[index] = new PotentialRowRefs
            {
                TextLevel = textLevelComponent,
                UpTextLevel = upTextLevelComponent,
                Text = textComponent,
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
