using System;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.Reward;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Quest
{
    // 프로젝트 표준 임무 팝업에서 일일·주간 퀘스트(각 10개, ListItem_Mission_01~10)를 표시·수령 처리한다.
    // "DailyTab"/"WeeklyTab" 버튼으로 "DailyScrollRect"/"WeeklyScrollRect"를 전환하고,
    // 상단 달성 게이지(Slider_Step_Horizontal_01)는 지금 보고 있는 탭 기준으로 값을 바꿔서 재사용한다.
    // 패널 오브젝트 자체가 SetActive(true)로 열릴 때(OnEnable)마다 항상 "일일" 탭을 기본으로 다시 그린다.
    [DisallowMultipleComponent]
    public sealed class DailyMissionPanelView : MonoBehaviour
    {
        private const int SlotCount = 10;
        private const string ListItemNamePrefix = "ListItem_Mission_";

        // 상단 "달성 개수" 게이지(Slider_Step_Horizontal_01) 하위 마일스톤 아이콘 5개(LIst_1~5).
        // 지금 보고 있는 탭(일일/주간)의 보상 수령 개수가 2/4/6/8/10에 도달할 때마다 순서대로 켜진다.
        private const int StepMilestoneCount = 5;
        private const string StepMilestoneNamePrefix = "LIst_";
        private const float StepIconAlphaDefault = 1f; // 미달성 상태 아이콘 색상(255/255)
        private const float StepIconAlphaAchieved = 100f / 255f; // 달성 상태로 바뀌면 연하게(100/255)

        private static readonly int[] StepMilestoneThresholds = { 2, 4, 6, 8, 10 };

        private QuestType currentTab = QuestType.Daily;

        private readonly MissionSlot[] dailySlots = new MissionSlot[SlotCount];
        private readonly MissionSlot[] weeklySlots = new MissionSlot[SlotCount];
        private GameObject dailyFocusObject;
        private GameObject weeklyFocusObject;
        private GameObject achievementsFocusObject;
        private GameObject dailyScrollRectObject;
        private GameObject weeklyScrollRectObject;
        private Button dailyTabButton;
        private Button weeklyTabButton;
        private Button closeButton;
        private Button outsideCloseButton;
        private Button claimAllButton;
        private TMP_Text panelTitleText;
        private TMP_Text claimAllButtonText;
        private bool claimAllBusy;
        private bool slotsBuilt;

        [Header("보상 표시")]
        [SerializeField] private Sprite goldRewardIcon;

        public event Action<bool> OpenStateChanged;

        public bool IsOpen => gameObject.activeSelf;

        private Slider stepSlider;
        private readonly StepMilestone[] stepMilestones = new StepMilestone[StepMilestoneCount];
        private bool stepMilestonesBuilt;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            currentTab = QuestType.Daily;
            ResolveReferences();
            QuestRuntime.Changed += RefreshView;
            RefreshView();
            OpenStateChanged?.Invoke(true);
        }

        private void OnDisable()
        {
            QuestRuntime.Changed -= RefreshView;
            OpenStateChanged?.Invoke(false);
        }

        private void OnDestroy()
        {
            closeButton?.onClick.RemoveListener(HandleCloseClicked);
            outsideCloseButton?.onClick.RemoveListener(HandleCloseClicked);
            claimAllButton?.onClick.RemoveListener(HandleClaimAllClicked);
        }

        private void ResolveReferences()
        {
            // ScrollRect 오브젝트가 먼저 있어야 그 하위로 슬롯을 좁혀서 찾을 수 있어 탭 해석을 먼저 한다.
            ResolveTabObjects();
            BuildSlotsIfNeeded();
            BuildStepMilestonesIfNeeded();
            ResolveTabButtons();
            ResolveCloseButton();
            ResolveFooterControls();
        }

        // 리스트 항목 하위 오브젝트(제목·슬라이더·진행도 텍스트·버튼·스탬프) 탐색은 무거우므로 1회만 수행한다.
        // "ListItem_Mission_01~10"이라는 같은 이름이 DailyScrollRect·WeeklyScrollRect 밑에 각각 있어서,
        // 반드시 해당 ScrollRect 루트로 좁혀서 찾아야 서로 안 섞인다.
        private void BuildSlotsIfNeeded()
        {
            if (slotsBuilt || dailyScrollRectObject == null || weeklyScrollRectObject == null)
            {
                return;
            }

            BuildSlots(dailyScrollRectObject.transform, dailySlots);
            BuildSlots(weeklyScrollRectObject.transform, weeklySlots);

            slotsBuilt = true;
        }

        private void BuildSlots(Transform root, MissionSlot[] slots)
        {
            for (var i = 0; i < SlotCount; i++)
            {
                var itemName = ListItemNamePrefix + (i + 1).ToString("00");
                var itemTransform = FindChild(root, itemName);
                if (itemTransform == null)
                {
                    Debug.LogWarning($"[Quest][UI] \"{root.name}\" 밑에서 \"{itemName}\" 오브젝트를 찾지 못했습니다.", this);
                    continue;
                }

                var slot = new MissionSlot { Root = itemTransform.gameObject };
                slot.TitleText = FindText(itemTransform, "Text_Title");

                var sliderTransform = FindChild(itemTransform, "Slider_02_Orange");
                slot.ProgressSlider = sliderTransform != null ? sliderTransform.GetComponent<Slider>() : null;

                var progressTextTransform = sliderTransform != null
                    ? FindChild(sliderTransform, "ProgressText") ?? FindChild(sliderTransform, "Text (TMP)")
                    : null;
                slot.ProgressText = progressTextTransform != null ? progressTextTransform.GetComponent<TMP_Text>() : null;

                var rewardCountTransform = FindChild(itemTransform, "RewardCountText");
                slot.RewardCountText = rewardCountTransform != null
                    ? rewardCountTransform.GetComponent<TMP_Text>()
                    : null;
                var rewardIconTransform = rewardCountTransform != null
                    ? FindChild(rewardCountTransform.parent, "Icon")
                    : null;
                slot.RewardIcon = rewardIconTransform != null ? rewardIconTransform.GetComponent<Image>() : null;

                var claimDisabledTransform = FindChild(itemTransform, "Button_ClaimDisabled");
                slot.ClaimDisabledObject = claimDisabledTransform != null ? claimDisabledTransform.gameObject : null;

                var claimTransform = FindChild(itemTransform, "Button_Claim");
                if (claimTransform != null)
                {
                    slot.ClaimObject = claimTransform.gameObject;
                    slot.ClaimButton = claimTransform.GetComponent<Button>();
                }

                // 기획 스프레드시트에는 "Stampe"로 표기되어 있어(오탈자 추정) 두 이름을 모두 시도한다.
                var stampTransform = FindChild(itemTransform, "Stampe") ?? FindChild(itemTransform, "Stamp");
                slot.StampObject = stampTransform != null ? stampTransform.gameObject : null;

                slots[i] = slot;
            }
        }

        // ListItem_Mission_XX 안에도 같은 이름의 "Slider_02_Orange"/"Icon"이 있어서,
        // 단계 게이지 전용 루트(Slider_Step_Horizontal_01) 밑으로만 좁혀서 찾아야
        // 개별 퀘스트 슬롯의 슬라이더·아이콘과 섞이지 않는다.
        private void BuildStepMilestonesIfNeeded()
        {
            if (stepMilestonesBuilt)
            {
                return;
            }

            var stepRoot = FindChild(transform, "Slider_Step_Horizontal_01");
            if (stepRoot == null)
            {
                Debug.LogWarning("[Quest][UI] \"Slider_Step_Horizontal_01\" 오브젝트를 찾지 못했습니다.", this);
                return;
            }

            var sliderTransform = FindChild(stepRoot, "Slider_02_Orange");
            stepSlider = sliderTransform != null ? sliderTransform.GetComponent<Slider>() : null;

            // Group_IconFrame은 Slider_Step_Horizontal_01의 자식이거나 Top의 형제일 수 있어(정확한 중첩
            // 깊이가 불확실), 이름 충돌 위험이 없는 만큼 패널 전체(transform)에서 찾아 구조 차이에 안전하게 대응한다.
            var iconFrameRoot = FindChild(transform, "Group_IconFrame");
            for (var i = 0; i < StepMilestoneCount; i++)
            {
                var itemName = StepMilestoneNamePrefix + (i + 1);
                var itemTransform = iconFrameRoot != null ? FindChild(iconFrameRoot, itemName) : null;
                if (itemTransform == null)
                {
                    Debug.LogWarning($"[Quest][UI] \"{itemName}\" 오브젝트를 찾지 못했습니다.", this);
                    continue;
                }

                var iconTransform = FindChild(itemTransform, "Icon");
                var checkTransform = FindChild(itemTransform, "Check");

                stepMilestones[i] = new StepMilestone
                {
                    // Image·RawImage 등 Icon의 실제 컴포넌트 종류에 상관없이 색상 알파를 바꿀 수 있도록
                    // 공통 베이스 타입(Graphic)으로 잡는다.
                    IconGraphic = iconTransform != null ? iconTransform.GetComponent<Graphic>() : null,
                    CheckObject = checkTransform != null ? checkTransform.gameObject : null,
                    // 게이지가 실제로 이 아이콘 위치까지 채워지도록, 슬라이더 값 계산에 쓸 위치 기준으로 저장한다.
                    ItemRect = itemTransform as RectTransform
                };

                if (stepMilestones[i].IconGraphic != null)
                {
                    // 프리팹 원본에서 이 Image가 기본적으로 꺼져 있어(enabled: false) 항상 켜준다.
                    // 달성 여부 표시는 ApplyStepMilestones에서 알파값으로만 구분한다.
                    stepMilestones[i].IconGraphic.enabled = true;
                }
            }

            stepMilestonesBuilt = true;
        }

        private void ResolveTabObjects()
        {
            if (dailyFocusObject == null)
            {
                var found = FindChild(transform, "DailyFocus");
                dailyFocusObject = found != null ? found.gameObject : null;
            }

            if (weeklyFocusObject == null)
            {
                var found = FindChild(transform, "WeeklyFocus");
                weeklyFocusObject = found != null ? found.gameObject : null;
            }

            if (achievementsFocusObject == null)
            {
                var found = FindChild(transform, "AchievementsFocus");
                achievementsFocusObject = found != null ? found.gameObject : null;
            }

            if (dailyScrollRectObject == null)
            {
                var found = FindChild(transform, "DailyScrollRect");
                dailyScrollRectObject = found != null ? found.gameObject : null;
            }

            if (weeklyScrollRectObject == null)
            {
                var found = FindChild(transform, "WeeklyScrollRect");
                weeklyScrollRectObject = found != null ? found.gameObject : null;
            }
        }

        // "DailyTab"/"WeeklyTab" 버튼을 눌러 탭을 전환한다("AchievementsTab"은 아직 콘텐츠가 없어 다루지 않는다).
        private void ResolveTabButtons()
        {
            if (dailyTabButton == null)
            {
                var found = FindChild(transform, "DailyTab");
                dailyTabButton = found != null ? found.GetComponent<Button>() : null;
            }

            if (dailyTabButton != null)
            {
                dailyTabButton.onClick.RemoveListener(HandleDailyTabClicked);
                dailyTabButton.onClick.AddListener(HandleDailyTabClicked);
            }

            if (weeklyTabButton == null)
            {
                var found = FindChild(transform, "WeeklyTab");
                weeklyTabButton = found != null ? found.GetComponent<Button>() : null;
            }

            if (weeklyTabButton != null)
            {
                weeklyTabButton.onClick.RemoveListener(HandleWeeklyTabClicked);
                weeklyTabButton.onClick.AddListener(HandleWeeklyTabClicked);
            }
        }

        private void HandleDailyTabClicked()
        {
            if (currentTab == QuestType.Daily)
            {
                return;
            }

            currentTab = QuestType.Daily;
            RefreshView();
        }

        private void HandleWeeklyTabClicked()
        {
            if (currentTab == QuestType.Weekly)
            {
                return;
            }

            currentTab = QuestType.Weekly;
            RefreshView();
        }

        private void ResolveCloseButton()
        {
            if (closeButton == null)
            {
                var found = FindChild(transform, "Button_Close_02");
                closeButton = found != null ? found.GetComponent<Button>() : null;
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
                closeButton.onClick.AddListener(HandleCloseClicked);
            }

            if (outsideCloseButton == null)
            {
                var found = FindChild(transform, "InputBlocker");
                outsideCloseButton = found != null ? found.GetComponent<Button>() : null;
            }

            if (outsideCloseButton != null)
            {
                outsideCloseButton.onClick.RemoveListener(HandleCloseClicked);
                outsideCloseButton.onClick.AddListener(HandleCloseClicked);
            }
        }

        private void HandleCloseClicked()
        {
            Close();
        }

        public void Open()
        {
            UIPanelPopAnimator.RequestOpen(gameObject);
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            UIPanelPopAnimator.RequestClose(gameObject);
        }

        private void ResolveFooterControls()
        {
            if (panelTitleText == null)
            {
                var found = FindChild(transform, "PanelTitleText");
                panelTitleText = found != null ? found.GetComponent<TMP_Text>() : null;
            }

            if (claimAllButton == null)
            {
                var found = FindChild(transform, "Button_ClaimAll");
                claimAllButton = found != null ? found.GetComponent<Button>() : null;
                claimAllButtonText = found != null ? found.GetComponentInChildren<TMP_Text>(true) : null;
            }

            if (claimAllButton != null)
            {
                claimAllButton.onClick.RemoveListener(HandleClaimAllClicked);
                claimAllButton.onClick.AddListener(HandleClaimAllClicked);
            }
        }

        private async void HandleClaimAllClicked()
        {
            if (claimAllBusy || !QuestRuntime.IsReady)
            {
                return;
            }

            claimAllBusy = true;
            RefreshView();
            try
            {
                await QuestRuntime.TryClaimAllRewardsAsync(currentTab);
            }
            finally
            {
                claimAllBusy = false;
                RefreshView();
            }
        }

        private void RefreshView()
        {
            ResolveReferences();
            ApplyTabFocus();

            if (!QuestRuntime.IsReady)
            {
                return;
            }

            var dailyResult = RefreshSlots(QuestType.Daily, dailySlots);
            var weeklyResult = RefreshSlots(QuestType.Weekly, weeklySlots);

            var current = currentTab == QuestType.Daily ? dailyResult : weeklyResult;
            ApplyStepMilestones(current.achievedCount, current.totalCount);
            ApplyClaimAllState(current.claimableCount);
        }

        // 일일/주간 탭 중 지금 선택된 쪽의 배경·포커스·스크롤렉트를 활성화한다
        // (업적 탭은 아직 콘텐츠가 없어 항상 비활성으로 둔다).
        private void ApplyTabFocus()
        {
            var isDaily = currentTab == QuestType.Daily;

            if (panelTitleText != null)
            {
                panelTitleText.text = isDaily ? "일일 임무" : "주간 임무";
            }

            if (dailyFocusObject != null)
            {
                dailyFocusObject.SetActive(isDaily);
            }

            if (weeklyFocusObject != null)
            {
                weeklyFocusObject.SetActive(!isDaily);
            }

            if (achievementsFocusObject != null)
            {
                achievementsFocusObject.SetActive(false);
            }

            if (dailyScrollRectObject != null)
            {
                dailyScrollRectObject.SetActive(isDaily);
            }

            if (weeklyScrollRectObject != null)
            {
                weeklyScrollRectObject.SetActive(!isDaily);
            }
        }

        // 주어진 퀘스트 타입(일일/주간)의 슬롯 10개를 전부 갱신하고, 보상까지 수령 완료한 개수를 세어 돌려준다.
        // 지금 보고 있지 않은 탭도 항상 함께 갱신해서, 탭을 전환하는 순간 바로 최신 상태로 보이게 한다.
        private (int achievedCount, int totalCount, int claimableCount) RefreshSlots(QuestType type, MissionSlot[] slots)
        {
            var definitions = QuestRuntime.GetQuestsByType(type);
            var achievedCount = 0;
            var claimableCount = 0;
            for (var i = 0; i < SlotCount; i++)
            {
                var slot = slots[i];
                var definition = i < definitions.Count ? definitions[i] : null;

                // 달성 게이지·마일스톤은 목표 달성 시점이 아니라 "Button_Claim"으로 보상을 실제로
                // 수령한 시점에 맞춰 올라가야 하므로 RewardClaimed 기준으로 센다.
                if (definition != null && QuestRuntime.GetProgress(definition.QuestId).RewardClaimed)
                {
                    achievedCount++;
                }

                if (definition != null)
                {
                    var progress = QuestRuntime.GetProgress(definition.QuestId);
                    if (progress.Completed && !progress.RewardClaimed)
                    {
                        claimableCount++;
                    }
                }

                if (slot == null)
                {
                    continue;
                }

                ApplySlot(slot, definition);
            }

            return (achievedCount, definitions.Count, claimableCount);
        }

        private void ApplyClaimAllState(int claimableCount)
        {
            if (claimAllButton != null)
            {
                claimAllButton.interactable = !claimAllBusy && claimableCount > 0;
            }

            if (claimAllButtonText != null)
            {
                claimAllButtonText.text = claimAllBusy ? "수령 중..." : "모두 받기";
            }
        }

        // 완료(달성)한 퀘스트 개수만큼 상단 게이지를 채우고, 2/4/6/8/10개를 달성할 때마다
        // 해당 LIst_X의 아이콘을 연하게 바꾸고 체크 표시를 켠다.
        private void ApplyStepMilestones(int achievedCount, int totalCount)
        {
            ApplyStepSliderValue(achievedCount, totalCount);

            for (var i = 0; i < StepMilestoneCount; i++)
            {
                var milestone = stepMilestones[i];
                if (milestone == null)
                {
                    continue;
                }

                var reached = achievedCount >= StepMilestoneThresholds[i];

                if (milestone.IconGraphic != null)
                {
                    milestone.IconGraphic.enabled = true;
                    var color = milestone.IconGraphic.color;
                    color.a = reached ? StepIconAlphaAchieved : StepIconAlphaDefault;
                    milestone.IconGraphic.color = color;
                }

                if (milestone.CheckObject != null)
                {
                    milestone.CheckObject.SetActive(reached);
                }
            }
        }

        // achievedCount/totalCount 비율 대신, LIst_1~5 아이콘의 실제 월드 위치를 기준으로 슬라이더 값을 계산한다.
        // "첫 아이콘 → 마지막 아이콘" 벡터를 진행 축으로 삼아 트랙·아이콘 위치를 전부 그 축에 투영하므로,
        // 게이지가 가로·세로 어느 방향으로 배치돼 있어도(회전 포함) 항상 아이콘 위치까지 정확히 채워진다.
        // 아이콘 위치를 하나라도 못 구하면 비율 기반 방식(ApplyStepSliderValueByRatio)으로 대체한다.
        private void ApplyStepSliderValue(int achievedCount, int totalCount)
        {
            if (stepSlider == null)
            {
                return;
            }

            var trackRect = stepSlider.GetComponent<RectTransform>();
            if (trackRect == null || !TryGetMilestoneWorldPositions(out var milestoneWorldPos))
            {
                ApplyStepSliderValueByRatio(achievedCount, totalCount);
                return;
            }

            var axisStart = milestoneWorldPos[0];
            var axisVector = milestoneWorldPos[StepMilestoneCount - 1] - axisStart;
            var axisLength = axisVector.magnitude;
            if (axisLength <= 0.0001f)
            {
                ApplyStepSliderValueByRatio(achievedCount, totalCount);
                return;
            }

            var axisDirection = axisVector / axisLength;

            // 트랙(배경 전체) 네 모서리를 위 진행 축에 투영해서 실제 시작·끝 지점을 찾는다.
            var corners = new Vector3[4];
            trackRect.GetWorldCorners(corners);
            var trackMinProjection = float.MaxValue;
            var trackMaxProjection = float.MinValue;
            for (var i = 0; i < 4; i++)
            {
                var projection = Vector3.Dot(corners[i] - axisStart, axisDirection);
                trackMinProjection = Mathf.Min(trackMinProjection, projection);
                trackMaxProjection = Mathf.Max(trackMaxProjection, projection);
            }

            var trackSpan = trackMaxProjection - trackMinProjection;
            if (trackSpan <= 0.0001f)
            {
                ApplyStepSliderValueByRatio(achievedCount, totalCount);
                return;
            }

            var lastThreshold = StepMilestoneThresholds[StepMilestoneCount - 1];
            var clampedCount = Mathf.Clamp(achievedCount, 0, lastThreshold);

            var previousCount = 0;
            var previousProjection = trackMinProjection;
            var targetProjection = trackMinProjection;
            for (var i = 0; i < StepMilestoneCount; i++)
            {
                var thresholdCount = StepMilestoneThresholds[i];
                var thresholdProjection = Vector3.Dot(milestoneWorldPos[i] - axisStart, axisDirection);

                if (clampedCount <= thresholdCount)
                {
                    var segmentRange = thresholdCount - previousCount;
                    var t = segmentRange > 0 ? (clampedCount - previousCount) / (float)segmentRange : 1f;
                    targetProjection = Mathf.Lerp(previousProjection, thresholdProjection, t);
                    break;
                }

                previousCount = thresholdCount;
                previousProjection = thresholdProjection;
                targetProjection = thresholdProjection;
            }

            var fraction = Mathf.Clamp01((targetProjection - trackMinProjection) / trackSpan);
            stepSlider.wholeNumbers = false;
            stepSlider.minValue = 0f;
            stepSlider.maxValue = 1f;
            stepSlider.value = fraction;
        }

        private void ApplyStepSliderValueByRatio(int achievedCount, int totalCount)
        {
            stepSlider.wholeNumbers = false;
            stepSlider.minValue = 0f;
            stepSlider.maxValue = Math.Max(1, totalCount);
            stepSlider.value = achievedCount;
        }

        // LIst_1~5 각 아이콘 오브젝트의 중심 월드 위치(3D)를 순서대로 담아 돌려준다.
        // 하나라도 참조가 없으면(아직 못 찾음 등) false를 돌려줘서 호출부가 비율 기반 방식으로 대체하게 한다.
        private bool TryGetMilestoneWorldPositions(out Vector3[] worldPositions)
        {
            worldPositions = new Vector3[StepMilestoneCount];
            var corners = new Vector3[4];
            for (var i = 0; i < StepMilestoneCount; i++)
            {
                var milestone = stepMilestones[i];
                if (milestone?.ItemRect == null)
                {
                    return false;
                }

                milestone.ItemRect.GetWorldCorners(corners);
                worldPositions[i] = (corners[0] + corners[2]) * 0.5f;
            }

            return true;
        }

        private void ApplySlot(MissionSlot slot, QuestDefinition definition)
        {
            slot.Definition = definition;

            if (definition == null)
            {
                slot.Root.SetActive(false);
                return;
            }

            if (!slot.Root.activeSelf)
            {
                slot.Root.SetActive(true);
            }

            if (slot.TitleText != null)
            {
                slot.TitleText.text = ContainsHangul(definition.DisplayName)
                    ? definition.DisplayName
                    : QuestConditionTypeInfo.GetDisplayName(definition.ConditionType);
            }

            var progress = QuestRuntime.GetProgress(definition.QuestId);
            var targetValue = QuestRuntime.ResolveTargetValue(definition);
            var currentValue = Math.Min(progress.CurrentProgress, targetValue);

            if (slot.ProgressSlider != null)
            {
                slot.ProgressSlider.minValue = 0f;
                slot.ProgressSlider.maxValue = targetValue;
                slot.ProgressSlider.value = currentValue;
            }

            if (slot.ProgressText != null)
            {
                slot.ProgressText.text = $"{currentValue} / {targetValue}";
            }

            ApplyReward(slot, definition.Reward);

            var completed = progress.Completed;
            var claimed = progress.RewardClaimed;

            if (slot.ClaimDisabledObject != null)
            {
                slot.ClaimDisabledObject.SetActive(!completed);
            }

            if (slot.ClaimObject != null)
            {
                slot.ClaimObject.SetActive(completed && !claimed);
            }

            if (slot.StampObject != null)
            {
                slot.StampObject.SetActive(claimed);
            }

            if (slot.ClaimButton != null)
            {
                // 패널이 몇 번을 열려도, 갱신될 때마다 리스너가 항상 살아있도록 매번 다시 붙인다.
                slot.ClaimButton.onClick.RemoveListener(slot.HandleClaimClicked);
                slot.ClaimButton.onClick.AddListener(slot.HandleClaimClicked);

                // 다른 슬롯의 저장 완료로 이 슬롯까지 함께 갱신될 때, 지금 수령 요청이 진행 중이면
                // 버튼을 다시 눌러 중복 요청되지 않도록 interactable 되돌리기를 건너뛴다.
                if (!slot.IsClaiming)
                {
                    slot.ClaimButton.interactable = true;
                }
            }
        }

        private static Transform FindChild(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == objectName)
                {
                    return transforms[i];
                }
            }

            return null;
        }

        private static TMP_Text FindText(Transform root, string objectName)
        {
            var child = FindChild(root, objectName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private void ApplyReward(MissionSlot slot, RewardDefinition reward)
        {
            if (slot.RewardIcon != null && goldRewardIcon != null)
            {
                slot.RewardIcon.sprite = goldRewardIcon;
                slot.RewardIcon.preserveAspect = true;
            }

            if (slot.RewardCountText == null)
            {
                return;
            }

            if (reward == null)
            {
                slot.RewardCountText.text = "-";
                return;
            }

            slot.RewardCountText.text = reward.CommanderExperience > 0L
                ? $"{reward.Gold:N0}\nEXP {reward.CommanderExperience:N0}"
                : $"{reward.Gold:N0}";
        }

        private static bool ContainsHangul(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] >= '\uac00' && value[i] <= '\ud7a3')
                {
                    return true;
                }
            }

            return false;
        }

        // ListItem_Mission_XX 1개에 대응하는 UI 참조 캐시 + 수령 버튼 클릭 처리.
        private sealed class MissionSlot
        {
            public GameObject Root;
            public TMP_Text TitleText;
            public Slider ProgressSlider;
            public TMP_Text ProgressText;
            public Image RewardIcon;
            public TMP_Text RewardCountText;
            public GameObject ClaimDisabledObject;
            public GameObject ClaimObject;
            public Button ClaimButton;
            public GameObject StampObject;
            public QuestDefinition Definition;
            private bool isClaiming;

            public bool IsClaiming => isClaiming;

            public async void HandleClaimClicked()
            {
                if (isClaiming || Definition == null)
                {
                    return;
                }

                isClaiming = true;
                if (ClaimButton != null)
                {
                    ClaimButton.interactable = false;
                }

                try
                {
                    // 성공하면 QuestRuntime.Changed가 발생해 DailyMissionPanelView.RefreshView가
                    // 자동으로 다시 그린다(Claim 버튼 숨김 · Stamp 표시 · 상단 달성 게이지 갱신 포함).
                    await QuestRuntime.TryClaimRewardAsync(Definition.QuestId);
                }
                finally
                {
                    isClaiming = false;
                    if (ClaimButton != null)
                    {
                        ClaimButton.interactable = true;
                    }
                }
            }
        }

        // LIst_1~5 하나에 대응하는 마일스톤 아이콘·체크 표시 참조 캐시.
        private sealed class StepMilestone
        {
            public Graphic IconGraphic;
            public GameObject CheckObject;
            public RectTransform ItemRect;
        }
    }
}
