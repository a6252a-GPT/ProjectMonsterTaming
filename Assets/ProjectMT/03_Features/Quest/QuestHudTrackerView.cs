using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Quest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Quest
{
    // 메인 HUD 퀘스트 카드(MissionText / QuestDescriptionText / StatusText / Icon) 표시 전담.
    // QuestRuntime이 준비되면 현재 추적 중인 메인 퀘스트를 자동으로 갱신하고,
    // 완료된 퀘스트는 아이콘을 눌러 직접 보상을 수령할 수 있게 한다.
    [DisallowMultipleComponent]
    public sealed class QuestHudTrackerView : MonoBehaviour
    {
        [SerializeField] private TMP_Text missionText;
        [SerializeField] private TMP_Text questDescriptionText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image claimIcon;
        [SerializeField] private QuestType trackedType = QuestType.Main;

        private Button claimButton;
        private QuestId trackedQuestId;
        private bool canClaimTracked;
        private bool isClaiming;
        private bool loggedMissingIcon;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            QuestRuntime.Changed += RefreshView;
            QuestProgressServiceHub.Ready += HandleProgressReady;
            RefreshView();
        }

        private void OnDisable()
        {
            QuestRuntime.Changed -= RefreshView;
            QuestProgressServiceHub.Ready -= HandleProgressReady;
            if (claimButton != null)
            {
                claimButton.onClick.RemoveListener(HandleIconClicked);
            }
        }

        private void HandleProgressReady(IGameProgressService _)
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
                return;
            }

            trackedQuestId = definition.QuestId;
            canClaimTracked = progress.Completed && !progress.RewardClaimed;
            UpdateClaimButtonState();

            Apply(
                QuestMissionCategoryInfo.GetDisplayName(definition.ConditionType),
                FormatDescription(definition, progress),
                FormatStatus(progress));
        }

        // 아이콘 클릭 → 현재 추적 중인 퀘스트 보상 수령 → QuestRuntime.Changed가 갱신을 자동으로 트리거해
        // 다음 퀘스트로 넘어간 화면이 곧바로 표시된다.
        private async void HandleIconClicked()
        {
            if (isClaiming || !canClaimTracked || !QuestRuntime.IsReady)
            {
                return;
            }

            isClaiming = true;
            UpdateClaimButtonState();
            try
            {
                await QuestRuntime.TryClaimRewardAsync(trackedQuestId);
            }
            finally
            {
                isClaiming = false;
                UpdateClaimButtonState();
            }
        }

        private void UpdateClaimButtonState()
        {
            if (claimButton != null)
            {
                claimButton.interactable = canClaimTracked && !isClaiming;
            }
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
            var resolvedDescription = QuestRuntime.ResolveDescription(definition);
            var body = string.IsNullOrWhiteSpace(resolvedDescription)
                ? definition.DisplayName
                : resolvedDescription.Trim();
            // 반복 퀘스트는 사이클마다 목표가 올라가므로 definition.TargetValue(1회차 기준)가 아니라
            // 지금 사이클 기준으로 다시 계산된 값을 보여준다.
            return $"{body} ({progress.CurrentProgress}/{QuestRuntime.ResolveTargetValue(definition)})";
        }

        // 목표를 채우면 보상 수령 여부와 상관없이 "완료"로 표시하고, 그 전에는 "진행 중"으로 표시한다.
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

            if (claimIcon == null)
            {
                claimIcon = FindImage(root, "Icon");
                if (claimIcon == null && !loggedMissingIcon)
                {
                    loggedMissingIcon = true;
                    Debug.LogWarning(
                        $"[Quest][HUD] \"Icon\" 오브젝트를 찾지 못해 보상 수령 버튼을 만들 수 없습니다 (root={root?.name}). " +
                        "HUD 카드 하위에 Icon이라는 이름의 Image가 있는지 확인하세요.", this);
                }
            }

            if (claimIcon != null && claimButton == null)
            {
                claimIcon.raycastTarget = true;
                claimButton = claimIcon.GetComponent<Button>();
                if (claimButton == null)
                {
                    claimButton = claimIcon.gameObject.AddComponent<Button>();
                }

                claimButton.targetGraphic = claimIcon;
            }

            // 몬스터 배치 등 다른 기능이 MainBattleHUD 전체를 SetActive(false)/(true)로 껐다 켜면
            // OnDisable에서 떼어낸 리스너가 다시 안 붙어 아이콘이 먹통이 되던 문제가 있었다.
            // claimButton 자체는 최초 1회만 찾아 만들고, 리스너 연결은 매번(OnEnable·RefreshView마다)
            // Remove 후 Add로 다시 보장해서 몇 번을 껐다 켜도 항상 클릭이 살아있게 한다.
            if (claimButton != null)
            {
                claimButton.onClick.RemoveListener(HandleIconClicked);
                claimButton.onClick.AddListener(HandleIconClicked);
                UpdateClaimButtonState();
            }
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
        public void EditorConfigure(TMP_Text mission, TMP_Text description, TMP_Text status, Image icon = null)
        {
            missionText = mission;
            questDescriptionText = description;
            statusText = status;
            claimIcon = icon;
        }
#endif
    }
}
