using System;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class CastleRaidStageSelectionController : MonoBehaviour // 군단의 역습 1~100 단계 선택 화면
    {
        [Header("Navigation")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button enterButton;
        [SerializeField] private TMP_Text enterButtonLabel;

        [Header("1~100 Stage Tower")]
        [SerializeField] private ScrollRect stageScrollRect;
        [SerializeField] private Button[] stageButtons = Array.Empty<Button>();
        [SerializeField] private TMP_Text[] stageNumberLabels = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] stageRewardLabels = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] stageStateLabels = Array.Empty<TMP_Text>();

        [Header("Details")]
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private Image progressFill;
        [SerializeField] private TMP_Text selectedStageLabel;
        [SerializeField] private TMP_Text selectedFrontLabel;
        [SerializeField] private TMP_Text selectedThemeLabel;
        [SerializeField] private TMP_Text rewardLabel;
        [SerializeField] private TMP_Text clearStateLabel;

        [Header("GUI Presentation")]
        [SerializeField] private bool polishedPresentation;
        [SerializeField] private TMP_Text diamondValueLabel;
        [SerializeField] private TMP_Text ticketValueLabel;
        [SerializeField] private Image[] selectionFrames = Array.Empty<Image>();
        [SerializeField] private Image[] statePlates = Array.Empty<Image>();
        [SerializeField] private CanvasGroup enterButtonSkin;
        [SerializeField] private TMP_Text progressCountLabel;
        [SerializeField] private TMP_Text visibleFrontLabel;
        [SerializeField] private Image[] rowStatusIcons = Array.Empty<Image>();
        [SerializeField] private Sprite clearedStatusIcon;
        [SerializeField] private Sprite challengeStatusIcon;
        [SerializeField] private Sprite lockedStatusIcon;

        [Header("Preview")]
        [SerializeField] private bool previewWithoutRuntime;
        [SerializeField, Range(0, CastleRaidStageRules.MaximumStage)] private int previewHighestClearedStage = 26;
        [SerializeField, Range(1, CastleRaidStageRules.MaximumStage)] private int previewSelectedStage = 27;

        private readonly Color clearedStageColor = new Color32(37, 85, 82, 255);
        private readonly Color challengeStageColor = new Color32(171, 113, 39, 255);
        private readonly Color replayStageColor = new Color32(45, 71, 94, 255);
        private readonly Color lockedStageColor = new Color32(31, 37, 46, 225);
        private readonly Color selectedStageColor = new Color32(214, 163, 67, 255);

        private UnityAction[] stageActions = Array.Empty<UnityAction>();
        private IGameProgressService progress;
        private IContentLauncher launcher;
        private ContentId contentId;
        private Func<int, bool> enterStage;
        private Action<string> statusChanged;
        private CastleRaidEntryState state;
        private int selectedStage = 1;
        private bool listenersBound;
        private bool configured;

        public int SelectedStage => selectedStage;
        public int SelectedDifficulty => CastleRaidStageRules.ResolveDifficulty(selectedStage);

        private void Awake()
        {
            BindListeners();
        }

        private void OnEnable()
        {
            BindListeners();
            Refresh();
        }

        private void OnDestroy()
        {
            UnbindListeners();
            Shutdown();
        }

        public void Configure(
            IGameProgressService progressService,
            IContentLauncher contentLauncher,
            ContentId targetContentId,
            Func<int, bool> enterSelectedStage,
            Action<string> setStatus)
        {
            Shutdown();
            progress = progressService ?? throw new ArgumentNullException(nameof(progressService));
            launcher = contentLauncher ?? throw new ArgumentNullException(nameof(contentLauncher));
            contentId = targetContentId;
            enterStage = enterSelectedStage ?? throw new ArgumentNullException(nameof(enterSelectedStage));
            statusChanged = setStatus;
            progress.Changed += Refresh;
            configured = true;
            Refresh();
        }

        public void Shutdown()
        {
            if (progress != null)
            {
                progress.Changed -= Refresh;
            }

            progress = null;
            launcher = null;
            enterStage = null;
            statusChanged = null;
            configured = false;
        }

        public void Open()
        {
            if (configured && launcher.TryGetCastleRaidState(contentId, out var currentState))
            {
                state = currentState;
                selectedStage = currentState.NextChallengeStage;
            }
            else
            {
                selectedStage = Mathf.Clamp(previewSelectedStage, 1, CastleRaidStageRules.MaximumStage);
            }

            UIPanelPopAnimator.RequestOpen(gameObject);
            transform.SetAsLastSibling();
            Refresh();
            ScrollToSelectedStage();
        }

        public void Close()
        {
            UIPanelPopAnimator.RequestClose(gameObject);
        }

        public void Refresh()
        {
            if (configured && launcher != null && launcher.TryGetCastleRaidState(contentId, out var currentState))
            {
                state = currentState;
            }
            else if (previewWithoutRuntime)
            {
                state = new CastleRaidEntryState(
                    new ContentId("castle_raid"),
                    "군단의 역습",
                    previewHighestClearedStage);
            }
            else
            {
                return;
            }

            selectedStage = Mathf.Clamp(selectedStage, 1, CastleRaidStageRules.MaximumStage);
            RefreshStageButtons();
            RefreshDetails();
        }

        private void SelectStageSlot(int slotIndex)
        {
            var stage = slotIndex + CastleRaidStageRules.MinimumStage;
            if (!CastleRaidStageRules.IsValidStage(stage))
            {
                return;
            }

            selectedStage = stage;
            Refresh();
        }

        private void EnterSelectedStage()
        {
            if (!state.IsSelectable(selectedStage))
            {
                statusChanged?.Invoke($"STAGE {selectedStage:000}은 이전 단계를 클리어해야 열립니다");
                return;
            }

            if (enterStage != null && enterStage(selectedStage))
            {
                Close();
            }
        }

        private void RefreshStageButtons()
        {
            for (var index = 0; index < stageButtons.Length; index++)
            {
                var stage = index + CastleRaidStageRules.MinimumStage;
                var button = stageButtons[index];
                if (button == null)
                {
                    continue;
                }

                var cleared = stage <= state.HighestClearedStage;
                var nextChallenge = state.HasChallengeStage && stage == state.NextChallengeStage;
                var selectable = state.IsSelectable(stage);
                var selected = stage == selectedStage;
                button.interactable = true; // 잠긴 단계도 보상 미리보기 선택은 허용
                button.image.color = selected
                    ? selectedStageColor
                    : cleared ? clearedStageColor
                    : nextChallenge ? challengeStageColor
                    : selectable ? replayStageColor : lockedStageColor;

                if (polishedPresentation)
                {
                    button.image.color = selected ? new Color32(67, 48, 47, 255)
                        : new Color32(34, 33, 39, 255);
                    if (index < rowStatusIcons.Length && rowStatusIcons[index] != null)
                    {
                        rowStatusIcons[index].sprite = cleared ? clearedStatusIcon : nextChallenge ? challengeStatusIcon : lockedStatusIcon;
                        rowStatusIcons[index].rectTransform.sizeDelta = nextChallenge ? new Vector2(12f, 12f) : new Vector2(24f, 24f);
                        rowStatusIcons[index].color = cleared ? new Color32(57, 190, 180, 255)
                            : nextChallenge ? new Color32(211, 157, 67, 255) : new Color32(108, 108, 115, 255);
                    }
                    if (index < selectionFrames.Length && selectionFrames[index] != null)
                        selectionFrames[index].enabled = selected;
                    if (index < statePlates.Length && statePlates[index] != null)
                        statePlates[index].color = cleared ? new Color32(32, 78, 73, 255)
                            : nextChallenge ? new Color32(122, 79, 35, 255) : new Color32(48, 46, 49, 255);
                }

                if (index < stageNumberLabels.Length && stageNumberLabels[index] != null)
                {
                    stageNumberLabels[index].text = $"STAGE {stage:000}";
                    stageNumberLabels[index].color = selectable || selected
                        ? Color.white
                        : new Color(0.55f, 0.58f, 0.62f, 1f);
                    if (polishedPresentation)
                        stageNumberLabels[index].color = selectable || selected
                            ? new Color32(241, 231, 211, 255) : new Color32(133, 132, 137, 255);
                }

                if (index < stageRewardLabels.Length && stageRewardLabels[index] != null)
                {
                    stageRewardLabels[index].text =
                        $"<pos=0>다이아 {CastleRaidStageRules.ResolveDiamondReward(stage):N0}" +
                        $"<pos=165>소환권 {CastleRaidStageRules.ResolveMonsterSummonTicketReward(stage):N0}";
                    stageRewardLabels[index].color = selectable || selected
                        ? new Color32(255, 224, 151, 255)
                        : new Color32(136, 132, 120, 255);
                    if (polishedPresentation)
                    {
                        stageRewardLabels[index].text =
                            $"<pos=0>{CastleRaidStageRules.ResolveDiamondReward(stage):N0}" +
                            $"<pos=138>{CastleRaidStageRules.ResolveMonsterSummonTicketReward(stage):N0}";
                        stageRewardLabels[index].color = selectable || selected
                            ? new Color32(241, 231, 211, 255) : new Color32(133, 132, 137, 255);
                    }
                }

                if (index < stageStateLabels.Length && stageStateLabels[index] != null)
                {
                    stageStateLabels[index].text = cleared ? "완료" : nextChallenge ? "도전" : "잠김";
                    stageStateLabels[index].color = cleared
                        ? new Color32(128, 238, 210, 255)
                        : nextChallenge ? new Color32(255, 226, 153, 255)
                        : new Color32(145, 151, 160, 255);
                }
            }
        }

        private void ScrollToSelectedStage()
        {
            if (stageScrollRect == null || stageButtons.Length <= 1)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            if (polishedPresentation && stageScrollRect.content != null)
            {
                var firstVisible = Mathf.Max(0, selectedStage - 2);
                var row = stageButtons[firstVisible].GetComponent<RectTransform>();
                var offset = selectedStage <= 2 ? 0f : -row.anchoredPosition.y - row.rect.height * 0.5f;
                var available = stageScrollRect.content.rect.height - stageScrollRect.viewport.rect.height;
                stageScrollRect.verticalNormalizedPosition = available <= 0f ? 1f : 1f - Mathf.Clamp01(offset / available);
                RefreshVisibleFrontHeader(Vector2.zero);
                return;
            }
            var normalizedIndex = (selectedStage - CastleRaidStageRules.MinimumStage) /
                                  (float)(stageButtons.Length - 1);
            stageScrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(normalizedIndex);
        }

        private void RefreshDetails()
        {
            var difficulty = CastleRaidStageRules.ResolveDifficulty(selectedStage);
            var cleared = selectedStage <= state.HighestClearedStage;
            var nextChallenge = state.HasChallengeStage && selectedStage == state.NextChallengeStage;
            var selectable = state.IsSelectable(selectedStage);
            var themeIndex = (selectedStage - 1) % 9 + 1;

            if (progressLabel != null)
            {
                progressLabel.text = $"공략 진척도  {state.HighestClearedStage:000} / {CastleRaidStageRules.MaximumStage:000}";
            }
            if (polishedPresentation && progressLabel != null) progressLabel.text = "공략 진행도";
            if (progressCountLabel != null)
                progressCountLabel.text = $"{state.HighestClearedStage:000} / {CastleRaidStageRules.MaximumStage:000}";
            if (progressFill != null)
            {
                var normalizedProgress = state.HighestClearedStage /
                                         (float)CastleRaidStageRules.MaximumStage;
                progressFill.fillAmount = normalizedProgress;
                var progressRect = progressFill.rectTransform;
                progressRect.anchorMax = new Vector2(normalizedProgress, 1f);
                progressRect.offsetMin = Vector2.zero;
                progressRect.offsetMax = Vector2.zero;
            }
            if (selectedStageLabel != null)
            {
                selectedStageLabel.text = $"STAGE {selectedStage:000}";
            }
            if (selectedFrontLabel != null)
            {
                selectedFrontLabel.text = $"전선 {CastleRaidStageRules.ResolveFirstStage(difficulty):000}-" +
                                          $"{CastleRaidStageRules.ResolveLastStage(difficulty):000}";
            }
            if (selectedThemeLabel != null)
            {
                selectedThemeLabel.text = polishedPresentation ? $"절차 요새 {themeIndex:00}" : $"절차 요새 {themeIndex:00} · 스테이지 고유 전장";
            }
            if (rewardLabel != null)
            {
                rewardLabel.text =
                    $"다이아 {CastleRaidStageRules.ResolveDiamondReward(selectedStage):N0}\n" +
                    $"소환권 {CastleRaidStageRules.ResolveMonsterSummonTicketReward(selectedStage):N0}";
            }
            if (diamondValueLabel != null)
                diamondValueLabel.text = CastleRaidStageRules.ResolveDiamondReward(selectedStage).ToString("N0");
            if (ticketValueLabel != null)
                ticketValueLabel.text = CastleRaidStageRules.ResolveMonsterSummonTicketReward(selectedStage).ToString("N0");
            if (clearStateLabel != null)
            {
                clearStateLabel.text = cleared
                    ? "클리어 완료 · 최초 보상 획득 완료"
                    : nextChallenge
                        ? "신규 도전 · 최초 클리어 보상 획득 가능"
                        : "잠김 · 이전 스테이지를 먼저 클리어하세요";
                clearStateLabel.color = cleared
                    ? new Color32(128, 238, 210, 255)
                    : nextChallenge ? new Color32(255, 226, 153, 255) : new Color32(180, 184, 191, 255);
                if (polishedPresentation)
                    clearStateLabel.text = cleared ? "최초 보상 획득 완료 · 재도전 가능"
                        : nextChallenge ? "첫 클리어 시 보상을 획득합니다"
                        : "이전 스테이지 클리어 후 도전 가능";
            }
            if (enterButton != null)
            {
                enterButton.interactable = selectable;
                enterButton.image.color = selectable
                    ? new Color32(167, 116, 42, 255)
                    : new Color32(55, 59, 65, 255);
                if (polishedPresentation)
                    enterButton.image.color = selectable ? new Color32(119, 55, 65, 255) : new Color32(48, 46, 49, 255);
                if (enterButtonSkin != null) enterButtonSkin.alpha = selectable ? 1f : 0.15f;
            }
            if (enterButtonLabel != null)
            {
                enterButtonLabel.text = !selectable ? "잠김" : cleared ? "재도전" : "공략 시작";
            }
        }

        private void RefreshVisibleFrontHeader(Vector2 unused)
        {
            if (visibleFrontLabel == null || stageScrollRect == null) return;
            var top = stageScrollRect.content.anchoredPosition.y;
            var firstStage = 1;
            for (var index = 0; index < stageButtons.Length; index++)
            {
                var row = stageButtons[index].GetComponent<RectTransform>();
                firstStage = index + 1;
                if (-row.anchoredPosition.y + row.rect.height * 0.5f > top) break;
            }
            var difficulty = CastleRaidStageRules.ResolveDifficulty(firstStage);
            visibleFrontLabel.text = $"전선 {CastleRaidStageRules.ResolveFirstStage(difficulty):000}-{CastleRaidStageRules.ResolveLastStage(difficulty):000}";
        }

        private void BindListeners()
        {
            if (listenersBound)
            {
                return;
            }

            stageScrollRect?.onValueChanged.AddListener(RefreshVisibleFrontHeader);
            closeButton?.onClick.AddListener(Close);
            enterButton?.onClick.AddListener(EnterSelectedStage);
            UIButtonClickPunch.EnsureOn(closeButton?.gameObject);
            UIButtonClickPunch.EnsureOn(enterButton?.gameObject);

            stageActions = new UnityAction[stageButtons.Length];
            for (var index = 0; index < stageButtons.Length; index++)
            {
                var captured = index;
                stageActions[index] = () => SelectStageSlot(captured);
                stageButtons[index]?.onClick.AddListener(stageActions[index]);
                UIButtonClickPunch.EnsureOn(stageButtons[index]?.gameObject);
            }

            listenersBound = true;
        }

        private void UnbindListeners()
        {
            if (!listenersBound)
            {
                return;
            }

            stageScrollRect?.onValueChanged.RemoveListener(RefreshVisibleFrontHeader);
            closeButton?.onClick.RemoveListener(Close);
            enterButton?.onClick.RemoveListener(EnterSelectedStage);
            for (var index = 0; index < stageButtons.Length && index < stageActions.Length; index++)
            {
                stageButtons[index]?.onClick.RemoveListener(stageActions[index]);
            }

            listenersBound = false;
        }

#if UNITY_EDITOR
        public void EditorSetPreview(int highestClearedStage, int stage)
        {
            previewWithoutRuntime = true;
            previewHighestClearedStage = Mathf.Clamp(highestClearedStage, 0, CastleRaidStageRules.MaximumStage);
            previewSelectedStage = Mathf.Clamp(stage, 1, CastleRaidStageRules.MaximumStage);
            selectedStage = previewSelectedStage;
            Refresh();
            ScrollToSelectedStage();
        }

        public void EditorConfigure(
            Button close,
            Button enter,
            TMP_Text enterLabel,
            ScrollRect stageScroll,
            Button[] stages,
            TMP_Text[] stageNumbers,
            TMP_Text[] stageRewards,
            TMP_Text[] stageStates,
            TMP_Text progressText,
            Image progressBarFill,
            TMP_Text stageLabel,
            TMP_Text frontLabel,
            TMP_Text themeLabel,
            TMP_Text rewardText,
            TMP_Text stateLabel,
            bool enablePreview,
            int previewHighest,
            int previewStage)
        {
            closeButton = close;
            enterButton = enter;
            enterButtonLabel = enterLabel;
            stageScrollRect = stageScroll;
            stageButtons = stages ?? Array.Empty<Button>();
            stageNumberLabels = stageNumbers ?? Array.Empty<TMP_Text>();
            stageRewardLabels = stageRewards ?? Array.Empty<TMP_Text>();
            stageStateLabels = stageStates ?? Array.Empty<TMP_Text>();
            progressLabel = progressText;
            progressFill = progressBarFill;
            selectedStageLabel = stageLabel;
            selectedFrontLabel = frontLabel;
            selectedThemeLabel = themeLabel;
            rewardLabel = rewardText;
            clearStateLabel = stateLabel;
            previewWithoutRuntime = enablePreview;
            previewHighestClearedStage = Mathf.Clamp(previewHighest, 0, CastleRaidStageRules.MaximumStage);
            previewSelectedStage = Mathf.Clamp(previewStage, 1, CastleRaidStageRules.MaximumStage);
            selectedStage = previewSelectedStage;
        }
#endif
    }
}
