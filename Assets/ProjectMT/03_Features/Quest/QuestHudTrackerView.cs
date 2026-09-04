using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.Reward;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Quest
{
    // 메인 HUD 퀘스트 카드(MissionText / QuestDescriptionText / StatusText / RewardIcon) 표시 전담.
    // QuestRuntime이 준비되면 현재 추적 중인 메인 퀘스트를 자동으로 갱신하고,
    // 완료된 퀘스트는 RewardButton을 눌러 직접 보상을 수령할 수 있게 한다.
    [DisallowMultipleComponent]
    public sealed class QuestHudTrackerView : MonoBehaviour
    {
        [SerializeField] private TMP_Text missionText;
        [SerializeField] private TMP_Text questDescriptionText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text rewardAmountText;
        [SerializeField] private Button rewardButton;
        [SerializeField] private QuestType trackedType = QuestType.Main;
        [SerializeField] private bool compactPresentation;
        [SerializeField] private CanvasGroup claimHighlight;

        private QuestId trackedQuestId;
        private bool canClaimTracked;
        private bool canNavigateTracked;
        private bool isClaiming;

        // 진행 중의 퀘스트 이동과 완료 후 보상 수령이 같은 카드 영역에서 동작한다.
        public Button QuestActionButton => rewardButton;

        // 기존 호출부 호환용. 이제 보상 전용 영역이 아니라 통합 액션 영역이다.
        public Button QuestRewardButton => rewardButton;

        private void Awake()
        {
            ResolveReferences();
            EnsureNavigationController();
        }

        // "QuestMove" 버튼/"ClickImage" 점멸(QuestHudNavigationController)과 각 페이지 내부 버튼 위치의
        // 클릭 힌트(QuestClickPointHintController)는 이 오브젝트(트래커 위젯 루트)에 자동으로 붙여서
        // 인스펙터에서 따로 부착할 필요가 없게 한다.
        private void EnsureNavigationController()
        {
            if (GetComponent<QuestHudNavigationController>() == null)
            {
                gameObject.AddComponent<QuestHudNavigationController>();
            }

            if (GetComponent<QuestClickPointHintController>() == null)
            {
                gameObject.AddComponent<QuestClickPointHintController>();
            }
        }

        private void OnEnable()
        {
            ResolveReferences();
            QuestRuntime.Changed += RefreshView;
            QuestProgressServiceHub.Ready += HandleProgressReady;
            ItemCatalogHub.Ready += HandleItemCatalogReady;
            RefreshView();
        }

        private void OnDisable()
        {
            QuestRuntime.Changed -= RefreshView;
            QuestProgressServiceHub.Ready -= HandleProgressReady;
            ItemCatalogHub.Ready -= HandleItemCatalogReady;
            if (rewardButton != null)
            {
                rewardButton.onClick.RemoveListener(HandleClaimClicked);
            }

            if (claimHighlight != null)
            {
                claimHighlight.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (claimHighlight != null && claimHighlight.gameObject.activeSelf)
            {
                claimHighlight.alpha = 0.65f + 0.2f * Mathf.Sin(Time.unscaledTime * 1.96f); // 수령 가능할 때만 완만한 강조
            }
        }

        private void HandleProgressReady(IGameProgressService _)
        {
            RefreshView();
        }

        private void HandleItemCatalogReady(ItemCatalog _)
        {
            RefreshView();
        }

        private void RefreshView()
        {
            ResolveReferences();

            if (!QuestRuntime.IsReady ||
                !QuestRuntime.TryGetTrackedQuest(trackedType, out var definition, out var progress))
            {
                canClaimTracked = false;
                UpdateClaimButtonState();
                Apply("메인 임무", "임무를 준비 중입니다.", "준비 중");
                ApplyRewardIcon(null);
                return;
            }

            trackedQuestId = definition.QuestId;
            canClaimTracked = progress.Completed && !progress.RewardClaimed;
            UpdateClaimButtonState();

            Apply(
                QuestMissionCategoryInfo.GetDisplayName(definition.ConditionType),
                compactPresentation ? ResolveDescription(definition) : FormatDescription(definition, progress),
                compactPresentation
                    ? $"{progress.CurrentProgress}/{QuestRuntime.ResolveTargetValue(definition)}"
                    : FormatStatus(progress));
            ApplyRewardIcon(definition);
        }

        // 인벤토리와 같은 ItemCatalog에서 아이콘·수량을 가져와 보상 아이템 이미지와 "X 10" 표기를 채운다.
        private void ApplyRewardIcon(QuestDefinition definition)
        {
            var rewardItem = ResolveRewardItem(definition, out var sprite);

            if (rewardIcon != null)
            {
                rewardIcon.sprite = sprite;
                rewardIcon.enabled = sprite != null;
            }

            SetText(rewardAmountText, rewardItem.Amount > 0L ? $"×{rewardItem.Amount:N0}" : string.Empty);
        }

        private static ItemAmount ResolveRewardItem(QuestDefinition definition, out Sprite sprite)
        {
            sprite = null;
            var catalog = ItemCatalogHub.Current;
            if (definition == null || definition.Reward == null || catalog == null)
            {
                return default;
            }

            // 보상 아이템이 여러 개면 대표로 첫 번째 유효 아이템의 아이콘·수량을 보여준다.
            var items = definition.Reward.Items;
            for (var index = 0; index < items.Count; index++)
            {
                if (items[index].IsValid && catalog.TryGet(items[index].ItemId, out var itemDefinition))
                {
                    sprite = itemDefinition.Icon;
                    return items[index];
                }
            }

            return default;
        }

        // 같은 HUD 카드 클릭으로 진행 중에는 퀘스트 이동을 요청하고, 완료 상태에서는 보상을 수령한다.
        private async void HandleClaimClicked()
        {
            if (isClaiming || !QuestRuntime.IsReady)
            {
                return;
            }

            if (!canClaimTracked)
            {
                if (canNavigateTracked)
                {
                    GetComponent<QuestHudNavigationController>()?.RequestTrackedNavigation();
                }

                return;
            }

            isClaiming = true;
            UpdateClaimButtonState();

            // 수령 성공 시 다음 퀘스트로 화면이 바로 바뀌므로, 지금 퀘스트 보상은 수령 전에 미리 스냅샷해 둔다.
            var claimedQuestId = trackedQuestId;
            var spawnPosition = rewardIcon != null ? rewardIcon.transform.position : transform.position;
            var presentation = BuildRewardPresentation(claimedQuestId);
            try
            {
                var claimed = await QuestRuntime.TryClaimRewardAsync(claimedQuestId);
                if (claimed && presentation != null)
                {
                    // 필드 아이템 획득과 동일한 연출(PF_RewardAcquireItem)을 RewardButton 위치에서 시작시킨다.
                    RewardPresentationHub.Current?.PlayConfirmed(presentation, spawnPosition);
                }
            }
            finally
            {
                isClaiming = false;
                UpdateClaimButtonState();
            }
        }

        private static RewardPresentationRequest BuildRewardPresentation(QuestId questId)
        {
            if (!QuestRuntime.TryGetDefinition(questId, out var definition) ||
                !definition.TryCreateRewardBundle(out var bundle))
            {
                return null;
            }

            return RewardPresentationRequest.FromBundle(bundle, ItemCatalogHub.Current);
        }

        private void UpdateClaimButtonState()
        {
            if (rewardButton != null)
            {
                rewardButton.interactable = (canClaimTracked || canNavigateTracked) && !isClaiming;
            }

            if (claimHighlight != null)
            {
                claimHighlight.interactable = false;
                claimHighlight.blocksRaycasts = false;
                claimHighlight.gameObject.SetActive(canClaimTracked && !isClaiming);
            }
        }

        public void SetNavigationAvailable(bool available)
        {
            canNavigateTracked = available;
            UpdateClaimButtonState();
        }

        public void RequestTrackedAction()
        {
            HandleClaimClicked();
        }

        private void Apply(string mission, string description, string status)
        {
            SetText(missionText, mission);
            SetText(questDescriptionText, description);
            SetText(statusText, status);
        }

        private static string FormatDescription(QuestDefinition definition, QuestProgressEntryView progress)
        {
            // {target} 토큰은 지금 사이클의 실제 목표 수치로 치환되어 문장 안에 바로 노출된다.
            var body = ResolveDescription(definition);
            // 반복 퀘스트는 사이클마다 목표가 올라가므로 definition.TargetValue(1회차 기준)가 아니라
            // 지금 사이클 기준으로 다시 계산된 값을 보여준다.
            return $"{body} ({progress.CurrentProgress}/{QuestRuntime.ResolveTargetValue(definition)})";
        }

        // 목표를 채우면 보상 수령 여부와 상관없이 "완료"로 표시하고, 그 전에는 "진행 중"으로 표시한다.
        private static string ResolveDescription(QuestDefinition definition)
        {
            var description = QuestRuntime.ResolveDescription(definition);
            return string.IsNullOrWhiteSpace(description) ? definition.DisplayName : description.Trim();
        }

        private static string FormatStatus(QuestProgressEntryView progress)
        {
            return progress.Completed ? "완료" : "진행 중";
        }

        private void ResolveReferences()
        {
            var root = FindTrackerRoot();
            if (missionText == null)
            {
                missionText = FindText(root, "MissionText");
            }

            if (questDescriptionText == null)
            {
                questDescriptionText = FindText(root, "QuestDescriptionText");
            }

            if (statusText == null)
            {
                statusText = FindText(root, "StatusText");
            }

            if (rewardIcon == null)
            {
                rewardIcon = FindImage(root, "RewardIcon");
            }

            if (rewardAmountText == null)
            {
                rewardAmountText = FindText(root, "EaText");
            }

            if (rewardButton == null)
            {
                rewardButton = FindButton(root, "RewardButton");
            }

            // 몬스터 배치 등 다른 기능이 MainBattleHUD 전체를 SetActive(false)/(true)로 껐다 켜면
            // OnDisable에서 떼어낸 리스너가 다시 안 붙어 버튼이 먹통이 되던 문제가 있었다.
            // rewardButton 자체는 최초 1회만 찾고, 리스너 연결은 매번(OnEnable·RefreshView마다)
            // Remove 후 Add로 다시 보장해서 몇 번을 껐다 켜도 항상 클릭이 살아있게 한다.
            if (rewardButton != null)
            {
                rewardButton.onClick.RemoveListener(HandleClaimClicked);
                rewardButton.onClick.AddListener(HandleClaimClicked);
            }

            UpdateClaimButtonState();
        }

        private Transform FindTrackerRoot()
        {
            var current = transform;
            while (current != null)
            {
                if (current.name.IndexOf("QuestTracker", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    current.name.IndexOf("HudMissionCard", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return current;
                }

                current = current.parent;
            }

            return transform.parent != null ? transform.parent : transform;
        }

        private static TMP_Text FindText(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i];
                if (candidate == null || candidate.name != objectName)
                {
                    continue;
                }

                var text = candidate.GetComponent<TMP_Text>();
                if (text != null)
                {
                    return text;
                }
            }

            return null;
        }

        private static Image FindImage(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i];
                if (candidate == null || candidate.name != objectName)
                {
                    continue;
                }

                var image = candidate.GetComponent<Image>();
                if (image != null)
                {
                    return image;
                }
            }

            return null;
        }

        private static Button FindButton(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i];
                if (candidate == null || candidate.name != objectName)
                {
                    continue;
                }

                var button = candidate.GetComponent<Button>();
                if (button != null)
                {
                    return button;
                }
            }

            return null;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target == null)
            {
                return;
            }

            if (!target.gameObject.activeSelf)
            {
                target.gameObject.SetActive(true);
            }

            target.enabled = true;
            target.text = value;
        }

#if UNITY_EDITOR
        public void EditorConfigureCompactPresentation(CanvasGroup highlight)
        {
            compactPresentation = true;
            claimHighlight = highlight;
        }

        public void EditorConfigure(
            TMP_Text mission,
            TMP_Text description,
            TMP_Text status,
            Image rewardIconImage = null,
            TMP_Text rewardAmount = null,
            Button rewardBtn = null)
        {
            missionText = mission;
            questDescriptionText = description;
            statusText = status;
            rewardIcon = rewardIconImage;
            rewardAmountText = rewardAmount;
            rewardButton = rewardBtn;
        }
#endif
    }
}
