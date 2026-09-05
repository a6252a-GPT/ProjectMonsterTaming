using System;
using System.Collections.Generic;
using ProjectMT.Contents.Framework;
using ProjectMT.Features.Quest;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.UI;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.GrowthDungeon
{
    public static class GrowthDungeonEntryRules // 단계 선택을 실행 모드와 비용 조건으로 변환
    {
        public static int ClampStage(GrowthDungeonEntryState state, int requestedStage)
        {
            return Mathf.Clamp(requestedStage, 1, state.MaximumSelectableStage);
        }

        public static ContentRunMode ResolveMode(GrowthDungeonEntryState state, int selectedStage)
        {
            return state.HasChallengeStage &&
                   ClampStage(state, selectedStage) == state.NextChallengeStage
                ? ContentRunMode.Challenge
                : ContentRunMode.Farming;
        }

        public static bool CanEnter(GrowthDungeonEntryState state, int selectedStage, bool runtimeAvailable)
        {
            if (!runtimeAvailable)
            {
                return false;
            }

            var stage = ClampStage(state, selectedStage);
            return (state.HasChallengeStage && stage == state.NextChallengeStage) ||
                   (stage <= state.HighestClearedStage && state.KeyQuantity > 0L);
        }
    }

    [DisallowMultipleComponent]
    public sealed class GrowthDungeonStageEntryController : MonoBehaviour // 성장 던전 입장 팝업 조율
    {

        private sealed class DungeonBinding
        {
            public DungeonBinding(
                ContentId contentId,
                string fallbackName,
                bool runtimeAvailable,
                GameObject popupPrefab)
            {
                ContentId = contentId;
                FallbackName = fallbackName;
                RuntimeAvailable = runtimeAvailable;
                PopupPrefab = popupPrefab;
            }

            public ContentId ContentId { get; }
            public string FallbackName { get; }
            public bool RuntimeAvailable { get; }
            public GameObject PopupPrefab { get; }
        }

        private static readonly ContentId FoodRiotId = new ContentId("food_riot");
        private static readonly ContentId TreasureSpiritId = new ContentId("treasure_spirit");
        private static readonly ContentId FallenCommanderId = new ContentId("fallen_commander");
        private static readonly ContentId GuardiansTowerId = new ContentId("guardians_tower");

        [Header("카드 버튼")]
        [SerializeField] private Button foodRiotEnterButton;
        [SerializeField] private Button foodRiotSweepButton;
        [SerializeField] private Button treasureSpiritEnterButton;
        [SerializeField] private Button treasureSpiritSweepButton;
        [SerializeField] private Button fallenCommanderEnterButton;
        [SerializeField] private Button fallenCommanderSweepButton;
        [SerializeField] private Button guardiansTowerEnterButton;
        [SerializeField] private Button guardiansTowerSweepButton;

        [Header("입장 팝업 원본")]
        [SerializeField] private GameObject foodRiotPopupPrefab;
        [SerializeField] private GameObject treasureSpiritPopupPrefab;
        [SerializeField] private GameObject fallenCommanderPopupPrefab;
        [SerializeField] private GameObject guardiansTowerPopupPrefab;

        private readonly Dictionary<string, GrowthDungeonStageEntryPopupView> popupCache =
            new Dictionary<string, GrowthDungeonStageEntryPopupView>(StringComparer.OrdinalIgnoreCase);

        private IGameProgressService progress;
        private IContentLauncher launcher;
        private IGrowthDungeonSweepService sweepService;
        private IHostedContentRunner hostedRunner;
        private Func<BattlePartySnapshot> partyFactory;
        private Func<bool> canOpenContent;
        private Action closeManagementPages;
        private Action<string> statusChanged;
        private DungeonBinding currentBinding;
        private GrowthDungeonStageEntryPopupView currentPopup;
        private int selectedStage = 1;
        private bool sweepPending;
        private bool configured;

        private void Awake()
        {
            foodRiotEnterButton?.onClick.AddListener(OpenFoodRiot);
            foodRiotSweepButton?.onClick.AddListener(OpenFoodRiot);
            treasureSpiritEnterButton?.onClick.AddListener(OpenTreasureSpirit);
            treasureSpiritSweepButton?.onClick.AddListener(OpenTreasureSpirit);
            fallenCommanderEnterButton?.onClick.AddListener(OpenFallenCommander);
            fallenCommanderSweepButton?.onClick.AddListener(OpenFallenCommander);
            guardiansTowerEnterButton?.onClick.AddListener(OpenGuardiansTower);
            guardiansTowerSweepButton?.onClick.AddListener(OpenGuardiansTower);

            UIButtonClickPunch.EnsureOn(foodRiotEnterButton?.gameObject);
            UIButtonClickPunch.EnsureOn(treasureSpiritEnterButton?.gameObject);
            UIButtonClickPunch.EnsureOn(fallenCommanderEnterButton?.gameObject);
            UIButtonClickPunch.EnsureOn(guardiansTowerEnterButton?.gameObject);
            UIButtonClickSound.EnsureOn(foodRiotEnterButton?.gameObject);
            UIButtonClickSound.EnsureOn(treasureSpiritEnterButton?.gameObject);
            UIButtonClickSound.EnsureOn(fallenCommanderEnterButton?.gameObject);
            UIButtonClickSound.EnsureOn(guardiansTowerEnterButton?.gameObject);
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDisable()
        {
            ClosePopup();
        }

        private void OnDestroy()
        {
            foodRiotEnterButton?.onClick.RemoveListener(OpenFoodRiot);
            foodRiotSweepButton?.onClick.RemoveListener(OpenFoodRiot);
            treasureSpiritEnterButton?.onClick.RemoveListener(OpenTreasureSpirit);
            treasureSpiritSweepButton?.onClick.RemoveListener(OpenTreasureSpirit);
            fallenCommanderEnterButton?.onClick.RemoveListener(OpenFallenCommander);
            fallenCommanderSweepButton?.onClick.RemoveListener(OpenFallenCommander);
            guardiansTowerEnterButton?.onClick.RemoveListener(OpenGuardiansTower);
            guardiansTowerSweepButton?.onClick.RemoveListener(OpenGuardiansTower);
            Shutdown();
        }

        public void Configure(
            IGameProgressService progressService,
            IContentLauncher contentLauncher,
            IGrowthDungeonSweepService dungeonSweepService,
            IHostedContentRunner runner,
            Func<BattlePartySnapshot> currentPartyFactory,
            Func<bool> contentOpenGate,
            Action closePages,
            Action<string> setStatus)
        {
            Shutdown();
            progress = progressService ?? throw new ArgumentNullException(nameof(progressService));
            launcher = contentLauncher ?? throw new ArgumentNullException(nameof(contentLauncher));
            sweepService = dungeonSweepService;
            hostedRunner = runner ?? throw new ArgumentNullException(nameof(runner));
            partyFactory = currentPartyFactory ?? throw new ArgumentNullException(nameof(currentPartyFactory));
            canOpenContent = contentOpenGate;
            closeManagementPages = closePages;
            statusChanged = setStatus;
            progress.Changed += Refresh;
            configured = true;
            Refresh();
        }

        public void Shutdown()
        {
            ClosePopup();
            if (progress != null)
            {
                progress.Changed -= Refresh;
            }

            progress = null;
            launcher = null;
            sweepService = null;
            hostedRunner = null;
            partyFactory = null;
            canOpenContent = null;
            closeManagementPages = null;
            statusChanged = null;
            sweepPending = false;
            configured = false;
        }

        public void Refresh()
        {
            RefreshCard(foodRiotEnterButton, foodRiotSweepButton, FoodRiotId, true);
            RefreshCard(treasureSpiritEnterButton, treasureSpiritSweepButton, TreasureSpiritId, true);
            RefreshCard(fallenCommanderEnterButton, fallenCommanderSweepButton, FallenCommanderId, true);
            RefreshCard(guardiansTowerEnterButton, guardiansTowerSweepButton, GuardiansTowerId, true);
            RefreshPopup();
        }

        private void OpenFoodRiot()
        {
            Open(new DungeonBinding(FoodRiotId, "식량 대소동", true, foodRiotPopupPrefab));
        }

        private void OpenTreasureSpirit()
        {
            Open(new DungeonBinding(
                TreasureSpiritId,
                "보물 정령 숨바꼭질",
                true,
                treasureSpiritPopupPrefab));
        }

        private void OpenFallenCommander()
        {
            Open(new DungeonBinding(
                FallenCommanderId,
                "타락한 과거의 군단장",
                true,
                fallenCommanderPopupPrefab));
        }

        private void OpenGuardiansTower()
        {
            Open(new DungeonBinding(
                GuardiansTowerId,
                "고대 수호수의 시련",
                true,
                guardiansTowerPopupPrefab));
        }

        private void Open(DungeonBinding binding)
        {
            if (!configured || binding == null || binding.PopupPrefab == null)
            {
                statusChanged?.Invoke("성장 던전 입장 UI를 불러오지 못했습니다");
                return;
            }

            ClosePopup();
            currentBinding = binding;
            if (binding.RuntimeAvailable &&
                launcher != null &&
                launcher.TryGetGrowthDungeonState(binding.ContentId, out var state))
            {
                selectedStage = state.NextChallengeStage;
            }
            else
            {
                selectedStage = 1;
            }

            if (!popupCache.TryGetValue(binding.ContentId.Value, out currentPopup) || currentPopup == null)
            {
                var popupParent = transform.parent != null ? transform.parent : transform;
                var popupObject = Instantiate(binding.PopupPrefab, popupParent); // 정식 프리팹의 좌우 배치 사용
                popupObject.name = binding.PopupPrefab.name;
                currentPopup = popupObject.GetComponent<GrowthDungeonStageEntryPopupView>();

                currentPopup.Initialize(
                    SelectPreviousStage,
                    SelectNextStage,
                    StartSelectedStage,
                    SweepHighestStage,
                    () => ClosePopup());
                popupCache[binding.ContentId.Value] = currentPopup;
            }

            UIPanelPopAnimator.RequestOpen(currentPopup.gameObject, UIPanelPopStyle.Standard);
            currentPopup.transform.SetAsLastSibling();
            RefreshPopup();
        }

        private void SelectPreviousStage()
        {
            if (TryGetCurrentState(out var state))
            {
                selectedStage = GrowthDungeonEntryRules.ClampStage(state, selectedStage - 1);
                RefreshPopup();
            }
        }

        private void SelectNextStage()
        {
            if (TryGetCurrentState(out var state))
            {
                selectedStage = GrowthDungeonEntryRules.ClampStage(state, selectedStage + 1);
                RefreshPopup();
            }
        }

        private void StartSelectedStage()
        {
            if (!TryGetCurrentState(out var state) || currentBinding == null)
            {
                statusChanged?.Invoke("성장 던전 정보를 다시 확인해 주세요");
                return;
            }

            selectedStage = GrowthDungeonEntryRules.ClampStage(state, selectedStage);
            if (!GrowthDungeonEntryRules.CanEnter(state, selectedStage, currentBinding.RuntimeAvailable))
            {
                statusChanged?.Invoke(currentBinding.RuntimeAvailable
                    ? "파밍 열쇠가 부족합니다"
                    : "아직 준비 중인 콘텐츠입니다");
                RefreshPopup();
                return;
            }

            if (canOpenContent != null && !canOpenContent())
            {
                RefreshPopup();
                return;
            }

            var mode = GrowthDungeonEntryRules.ResolveMode(state, selectedStage);
            var party = partyFactory();
            if (party == null || party.Units.Length == 0 ||
                !launcher.StartHosted(
                    currentBinding.ContentId,
                    party,
                    hostedRunner,
                    mode,
                    selectedStage))
            {
                statusChanged?.Invoke("입장 상태가 변경되었습니다 · 다시 확인해 주세요");
                Refresh();
                return;
            }

            var displayName = state.DisplayName;
            var modeLabel = mode == ContentRunMode.Challenge ? "도전" : "파밍";
            ClosePopup(animate: false);
            closeManagementPages?.Invoke();
            statusChanged?.Invoke($"{displayName} · {selectedStage}단계 {modeLabel}");

            // 이 컨트롤러가 다루는 4개 성장 던전(식량 대소동·보물 정령·타락한 과거의 군단장·고대 수호수)
            // 중 아무 곳이나 입장에 성공하면 퀘스트 조건을 채운다.
            _ = QuestRuntime.AdvanceAllOfConditionAsync(QuestConditionType.GrowthDungeonEnter, 1L);
        }

        private async void SweepHighestStage()
        {
            if (sweepPending || currentBinding == null || !currentBinding.RuntimeAvailable ||
                sweepService == null || !TryGetCurrentState(out var state) || !state.CanSweep)
            {
                statusChanged?.Invoke(currentBinding != null && !currentBinding.RuntimeAvailable
                    ? "아직 준비 중인 콘텐츠입니다"
                    : "클리어 기록 또는 열쇠를 확인해 주세요");
                RefreshPopup();
                return;
            }

            if (canOpenContent != null && !canOpenContent())
            {
                RefreshPopup();
                return;
            }

            sweepPending = true;
            Refresh();
            statusChanged?.Invoke($"{state.DisplayName} · {state.HighestClearedStage}단계 소탕 정산 중...");
            var saved = false;
            try
            {
                saved = await sweepService.TrySweepAsync(currentBinding.ContentId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                sweepPending = false;
            }

            if (this == null)
            {
                return;
            }

            if (saved)
            {
                ClosePopup(animate: false);
                closeManagementPages?.Invoke();
                statusChanged?.Invoke($"{state.DisplayName} 소탕 완료");
            }
            else
            {
                statusChanged?.Invoke("클리어 기록 또는 열쇠를 확인해 주세요");
            }

            Refresh();
        }

        private bool TryGetCurrentState(out GrowthDungeonEntryState state)
        {
            state = default;
            return configured && currentBinding != null && currentBinding.RuntimeAvailable && launcher != null &&
                   launcher.TryGetGrowthDungeonState(currentBinding.ContentId, out state);
        }

        private void RefreshPopup()
        {
            if (currentPopup == null || currentBinding == null || !currentPopup.gameObject.activeSelf)
            {
                return;
            }

            var hasState = TryGetCurrentState(out var state);
            if (hasState)
            {
                selectedStage = GrowthDungeonEntryRules.ClampStage(state, selectedStage);
            }

            var busy = sweepPending || launcher == null || launcher.IsRunning ||
                       (sweepService != null && sweepService.IsBusy);
            currentPopup.Render(
                hasState ? state.DisplayName : currentBinding.FallbackName,
                hasState ? state : default,
                selectedStage,
                hasState && currentBinding.RuntimeAvailable,
                busy);
        }

        private void RefreshCard(
            Button enterButton,
            Button sweepButton,
            ContentId contentId,
            bool runtimeAvailable)
        {
            var busy = !configured || launcher == null || launcher.IsRunning || sweepPending ||
                       (sweepService != null && sweepService.IsBusy);
            var state = default(GrowthDungeonEntryState);
            var hasState = runtimeAvailable && configured && launcher != null &&
                           launcher.TryGetGrowthDungeonState(contentId, out state);
            if (enterButton != null)
            {
                enterButton.interactable = !busy && (runtimeAvailable ? hasState : true);
                SetButtonText(enterButton, runtimeAvailable ? "입장" : "준비 중");
            }

            if (sweepButton != null)
            {
                sweepButton.interactable = !busy && runtimeAvailable && hasState && state.CanSweep;
                SetButtonText(sweepButton, runtimeAvailable ? "소탕" : "준비 중");
            }
        }

        // animate=false는 던전 입장 등 화면 전환 중 조상이 함께 비활성화돼 닫힘 트윈이 끊기고
        // activeSelf=true로 남을 수 있는 경로 전용이다. 이 경로는 애니메이션 없이 즉시 닫는다.
        private void ClosePopup(bool animate = true)
        {
            if (currentPopup != null)
            {
                if (animate)
                {
                    UIPanelPopAnimator.RequestClose(currentPopup.gameObject);
                }
                else
                {
                    currentPopup.gameObject.SetActive(false);
                }
            }

            currentPopup = null;
            currentBinding = null;
        }

        private static void SetButtonText(Button button, string value)
        {
            var text = button == null ? null : button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = value;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Button foodEnter,
            Button foodSweep,
            Button treasureEnter,
            Button treasureSweep,
            Button fallenCommanderEnter,
            Button fallenCommanderSweep,
            Button guardiansEnter,
            Button guardiansSweep,
            GameObject foodPopup,
            GameObject treasurePopup,
            GameObject fallenCommanderPopup,
            GameObject guardiansPopup)
        {
            foodRiotEnterButton = foodEnter;
            foodRiotSweepButton = foodSweep;
            treasureSpiritEnterButton = treasureEnter;
            treasureSpiritSweepButton = treasureSweep;
            fallenCommanderEnterButton = fallenCommanderEnter;
            fallenCommanderSweepButton = fallenCommanderSweep;
            guardiansTowerEnterButton = guardiansEnter;
            guardiansTowerSweepButton = guardiansSweep;
            foodRiotPopupPrefab = foodPopup;
            treasureSpiritPopupPrefab = treasurePopup;
            fallenCommanderPopupPrefab = fallenCommanderPopup;
            guardiansTowerPopupPrefab = guardiansPopup;
        }
#endif
    }

}
