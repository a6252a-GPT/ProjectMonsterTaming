using System;
using System.Collections.Generic;
using ProjectMT.Features.Commander;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.Quest;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Quest
{
    // 퀘스트 트래커의 통합 행동 버튼 + "ClickImage" 크기 펄스 연출 전담.
    // 지금 추적 중인 메인 퀘스트(QuestHudTrackerView와 동일한 소스)를 진행할 수 있는 관리 화면으로
    // 바로 이동시켜 튜토리얼처럼 안내한다. QuestHudTrackerView.Awake()에서 자동으로 붙여준다.
    [DisallowMultipleComponent]
    public sealed class QuestHudNavigationController : MonoBehaviour
    {
        private const float VisibilityPollInterval = 0.2f;
        private const QuestType TrackedType = QuestType.Main;
        // 손가락 이미지의 끝이 버튼 모서리에 닿도록 하는 시각 보정값이다. 절대 화면 좌표가 아니라
        // 실제 버튼의 우하단을 기준으로 적용되므로 HUD 배치와 크기가 바뀌어도 버튼을 따라간다.
        private static readonly Vector2 UnifiedActionHintOffset = new Vector2(17f, -34f);

        // questId -> 이동 동작. 대상 화면이 없는 퀘스트(원정대 클리어, 몬스터 처치 등 전투 중 자연 달성형)는
        // 매핑에서 제외해 버튼 클릭이 아무 동작도 하지 않고 손가락 힌트도 뜨지 않게 한다.
        private static readonly Dictionary<string, Action<QuestHudNavigationController>> DestinationByQuestId =
            new Dictionary<string, Action<QuestHudNavigationController>>(StringComparer.OrdinalIgnoreCase)
            {
                ["quest_002_monster_summon"] = c => c.OpenShop(),
                ["quest_010_monster_formation"] = c => c.OpenFormation(),
                ["quest_003_monster_level_up"] = c => c.OpenMonsterManagement(),
                ["quest_004_commander_level_up"] = c => c.OpenCommanderGrowthStats(),
                ["quest_005_commander_potential_upgrade"] = c => c.OpenCommanderGrowthPotential(),
                ["quest_006_monster_ascension"] = c => c.OpenMonsterManagement(),
                ["quest_007_equipment_equip"] = c => c.OpenCommanderEquipment(),
                ["quest_008_equipment_enhance"] = c => c.OpenEquipmentSlotUpgrade(), // 실제 표시명은 "장비 슬롯 강화"
                ["quest_009_monster_owned_count"] = c => c.OpenShop(),
                ["quest_011_growth_dungeon_enter"] = c => c.OpenGrowthDungeon(),
                ["quest_012_castle_raid_enter"] = c => c.OpenExpandedMenu(),
                ["quest_013_equipment_dismantle"] = c => c.OpenCommanderEquipment(),
                ["quest_014_monster_level_reach"] = c => c.OpenMonsterManagement(),
                ["quest_015_commander_level_reach"] = c => c.OpenCommanderGrowthStats(),
                ["quest_016_commander_health_level_reach"] = c => c.OpenCommanderGrowthStats(),
                ["quest_017_commander_attack_level_reach"] = c => c.OpenCommanderGrowthStats(),
                ["quest_018_commander_defense_level_reach"] = c => c.OpenCommanderGrowthStats(),
                ["quest_019_commander_power_reach"] = c => c.OpenCommanderGrowthStats(),
                ["quest_020_equipment_slot_upgrade_reach"] = c => c.OpenEquipmentSlotUpgrade(),
                ["quest_021_monster_summon_repeat"] = c => c.OpenShop(),
                ["quest_022_commander_potential_unlock_count"] = c => c.OpenCommanderGrowthPotential(),
                ["quest_024_monster_owned_count_repeat"] = c => c.OpenShop(),
            };

        [SerializeField] private Button questMoveButton;
        [SerializeField] private GameObject clickImage;
        [SerializeField] private GameObject questText;

        // 다른 페이지들의 클릭 힌트(QuestClickPointHintController)가 손가락 아이콘을 복제해서 쓸 원본.
        public GameObject ClickImageTemplate => clickImage;

        private MainBattleManagementUiController managementUi;
        private HudQuickMenuController hudMenu;
        private QuestClickPointHintController pageHintController;
        private QuestHudTrackerView trackerView;
        private ShopCategoryMenu shopCategoryMenu;

        private string trackedQuestId;
        private bool hasDestination;
        private bool completedAwaitingClaim;
        private float tickTimer;
        private string tutorialShopQuestId;
        private Transform tutorialShopResult;
        private bool tutorialShopSawResults;
        private bool tutorialShopGoalComplete;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            QuestRuntime.Changed += RefreshTrackedDestination;
            if (managementUi != null)
            {
                managementUi.AnyPageOpenChanged += HandleAnyPageOpenChanged;
            }
            ResetClickHintSession();
            RefreshTrackedDestination();
            tickTimer = VisibilityPollInterval; // 켜지는 즉시 한 번 더 반영
        }

        private void OnDisable()
        {
            tutorialShopQuestId = null;
            QuestRuntime.Changed -= RefreshTrackedDestination;
            if (managementUi != null)
            {
                managementUi.AnyPageOpenChanged -= HandleAnyPageOpenChanged;
            }
            clickImage?.SetActive(false);
            QuestTutorialSpotlight.Hide(this);
            if (questMoveButton != null)
            {
                questMoveButton.onClick.RemoveListener(HandleMoveClicked);
            }

            trackerView?.SetNavigationAvailable(false);

            ResetClickHintSession();
        }

        // 페이지 내부 클릭 힌트(QuestClickPointHintController)가 떠 있는지는 실시간으로 바뀌므로
        // QuestRuntime 이벤트와 별개로 페이지 힌트와 버튼 위치를 주기적으로 다시 반영한다.
        private void Update()
        {
            tickTimer += Time.unscaledDeltaTime;
            if (tickTimer < VisibilityPollInterval)
            {
                return;
            }

            tickTimer = 0f;
            RefreshTutorialShopTransition();
            if (hasDestination)
            {
                PositionClickImage(completedAwaitingClaim);
            }
            ApplyClickImageVisibility();
            ApplyQuestTextVisibility();
        }

        private void ResolveReferences()
        {
            var root = FindTrackerRoot();
            if (questMoveButton == null)
            {
                questMoveButton = FindButton(root, "QuestMove");
            }

            if (clickImage == null)
            {
                clickImage = FindDeep(root, "ClickImage")?.gameObject;
            }


            if (questText == null)
            {
                questText = FindDeep(root, "QuestText")?.gameObject;
            }

            trackerView ??= GetComponent<QuestHudTrackerView>();

            // MainBattleHUD 전체가 SetActive로 껐다 켜지는 경우가 있어(QuestHudTrackerView와 동일한 사유),
            // 리스너는 매번(OnEnable마다) Remove 후 Add로 다시 보장한다.
            if (questMoveButton != null)
            {
                questMoveButton.onClick.RemoveListener(HandleMoveClicked);
                questMoveButton.onClick.AddListener(HandleMoveClicked);
            }

            managementUi ??= FindFirstObjectByType<MainBattleManagementUiController>(FindObjectsInactive.Include);
            hudMenu ??= FindFirstObjectByType<HudQuickMenuController>(FindObjectsInactive.Include);
            pageHintController ??= FindFirstObjectByType<QuestClickPointHintController>(FindObjectsInactive.Include);
            shopCategoryMenu ??= FindFirstObjectByType<ShopCategoryMenu>(FindObjectsInactive.Include);
        }

        private void RefreshTrackedDestination()
        {
            // definition/progressView는 이 if 조건 안에서만 대입이 보장되므로, 사용도 같은 블록 안에서 끝낸다
            // (밖으로 꺼내 별도 문장에서 쓰면 CS0165 "미할당 변수 사용" 컴파일 에러가 난다).
            // progressView는 TryGetTrackedQuest가 내부적으로 실시간 재계산한 값이라, 이걸 그대로 써야 한다.
            // QuestRuntime.CanClaimReward(questId)는 저장된 진행도만 보기 때문에, 군단장 레벨 등
            // "실시간 값 기준" 임계 조건 퀘스트에서는 화면엔 완료로 보여도 false가 나오는 불일치가 있었다.
            if (QuestRuntime.IsReady &&
                QuestRuntime.TryGetTrackedQuest(TrackedType, out var definition, out var progressView))
            {
                // 완료했지만 보상을 아직 안 받은 상태면 다음 할 일은 "이동"이 아니라 "보상 수령"이라
                // 이동 매핑 여부와 상관없이 ClickImage를 실제 RewardButton 쪽으로 옮겨 계속 보여준다.
                var completedAwaitingClaim = progressView.Completed && !progressView.RewardClaimed;
                var nextTrackedQuestId = completedAwaitingClaim || DestinationByQuestId.ContainsKey(definition.QuestId.Value)
                    ? definition.QuestId.Value
                    : null;
                if (!string.Equals(trackedQuestId, nextTrackedQuestId, StringComparison.OrdinalIgnoreCase) ||
                    this.completedAwaitingClaim != completedAwaitingClaim)
                {
                    ResetClickHintSession();
                }

                hasDestination = !string.IsNullOrEmpty(nextTrackedQuestId);
                trackedQuestId = nextTrackedQuestId;
                this.completedAwaitingClaim = completedAwaitingClaim;

                if (hasDestination)
                {
                    PositionClickImage(completedAwaitingClaim);
                }
            }
            else
            {
                ResetClickHintSession();
                hasDestination = false;
                trackedQuestId = null;
                completedAwaitingClaim = false;
            }

            trackerView?.SetNavigationAvailable(hasDestination && !completedAwaitingClaim);

            ApplyClickImageVisibility();
            ApplyQuestTextVisibility();
        }

        // 페이지 안 정확한 버튼 힌트(QuestClickPointHintController)가 표시되는 동안은 화면에 손가락 아이콘
        // 두 개가 동시에 보이면 헷갈리므로, 메인 화면 쪽(이 트래커) 힌트는 꺼둔다.
        private void ApplyClickImageVisibility()
        {
            if (clickImage == null)
            {
                return;
            }

            if (!hasDestination || QuestRuntime.IsTutorialDismissed(new QuestId(trackedQuestId)))
            {
                clickImage.SetActive(false);
                QuestTutorialSpotlight.Hide(this);
                return;
            }

            // 페이지 열림 이벤트 직후에는 내부 힌트 컨트롤러의 다음 갱신을 기다리지 않고
            // 메인 트래커 힌트를 즉시 끈다.
            // 창이 닫혀 실제 보상을 누를 수 있을 때 HUD 안내를 재개한다.
            if (managementUi != null && managementUi.IsAnyPageOpen)
            {
                clickImage.SetActive(false);
                QuestTutorialSpotlight.Hide(this);
                return;
            }

            if (HasAnyPageHintVisible())
            {
                clickImage.SetActive(false);
                QuestTutorialSpotlight.Hide(this);
                return;
            }

            if (!QuestTutorialInteraction.CanInteract(ResolveClickInteractionTarget()))
            {
                clickImage.SetActive(false);
                QuestTutorialSpotlight.Hide(this);
                return;
            }

            QuestTutorialSpotlight.Show(this, ResolveClickVisualTarget(),
                completedAwaitingClaim ? "완료한 퀘스트의 보상을 받아주세요" : "퀘스트를 누르면 목표 화면으로 이동해요");
            clickImage.SetActive(true);
        }

        private void ResetClickHintSession()
        {
            clickImage?.SetActive(false);
            QuestTutorialSpotlight.Hide(this);
        }

        private static string BuildTutorialHintId(string questId, string stepKey) =>
            $"quest_tutorial:{questId}:{stepKey}";

        private bool HasAnyPageHintVisible()
        {
            if (pageHintController != null && pageHintController.HasVisibleHint)
            {
                return true;
            }

            // 씬 재초기화·가산 로드처럼 동일 타입이 잠시 둘 이상 존재해도, 실제로 표시 중인
            // 페이지 힌트가 하나라도 있으면 HUD 손가락을 숨겨 중복 안내를 막는다.
            var controllers = FindObjectsByType<QuestClickPointHintController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < controllers.Length; index++)
            {
                var controller = controllers[index];
                if (controller != null && controller.HasVisibleHint)
                {
                    pageHintController = controller;
                    return true;
                }
            }

            return false;
        }

        private void HandleAnyPageOpenChanged(bool pageOpen)
        {
            if (!pageOpen)
            {
                tutorialShopQuestId = null;
                pageHintController?.DismissShownTutorial();
            }
            ApplyClickImageVisibility();
            ApplyQuestTextVisibility();
        }

        private void ApplyQuestTextVisibility()
        {
            if (questText != null)
            {
                // 전투/처치처럼 이동할 패널이 없는 퀘스트에는 "퀘스트 이동" 안내 문구를 숨긴다.
                questText.SetActive(hasDestination && !completedAwaitingClaim);
            }
        }

        private void PositionClickImage(bool completedAwaitingClaim)
        {
            if (clickImage == null)
            {
                QuestTutorialSpotlight.Hide(this);
                return;
            }

            var rect = clickImage.GetComponent<RectTransform>();
            var target = ResolveClickVisualTarget();
            if (rect == null || target == null || rect.parent is not RectTransform parent)
            {
                return;
            }

            var canvas = parent.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var screenPoint = RectTransformUtility.WorldToScreenPoint(camera, corners[3]);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, camera, out var localPoint))
            {
                return;
            }

            var currentLocalPosition = rect.localPosition;
            rect.localPosition = new Vector3(
                localPoint.x + UnifiedActionHintOffset.x,
                localPoint.y + UnifiedActionHintOffset.y,
                currentLocalPosition.z);
        }

        private RectTransform ResolveClickVisualTarget()
        {
            // QuestMove는 카드 위에 겹친 실제 입력 영역이라 카드보다 좁을 수 있다.
            // 테두리와 손가락은 사용자가 보는 전체 퀘스트 카드 외곽을 기준으로 둔다.
            return trackerView?.QuestActionButton?.transform as RectTransform
                   ?? questMoveButton?.transform as RectTransform;
        }

        private RectTransform ResolveClickInteractionTarget()
        {
            var action = trackerView?.QuestActionButton?.transform as RectTransform;
            var move = questMoveButton?.transform as RectTransform;
            // 가시성 검사는 실제로 Raycast를 받는 겹침 영역을 기준으로 한다.
            if (action != null && QuestTutorialInteraction.CanInteract(action)) return action;
            if (move != null && QuestTutorialInteraction.CanInteract(move)) return move;
            return action ?? move;
        }

        private void HandleMoveClicked()
        {
            if (!hasDestination || string.IsNullOrEmpty(trackedQuestId))
            {
                return;
            }

            if (completedAwaitingClaim)
            {
                trackerView?.RequestTrackedAction();
                return;
            }

            if (DestinationByQuestId.TryGetValue(trackedQuestId, out var openDestination))
            {
                openDestination(this);
                // 페이지가 열린 같은 프레임에 메인 트래커 힌트를 끈다. 다음 Update를 기다리면
                // 외부 힌트와 페이지 내부 힌트가 잠깐 겹쳐 보일 수 있다.
                ApplyClickImageVisibility();
            }
        }

        public void RequestTrackedNavigation()
        {
            HandleMoveClicked();
        }

        // ---------------------------------------------------------------
        // 대상 화면 열기
        // ---------------------------------------------------------------

        // 몬스터 뽑기 관련 퀘스트는 상점을 열 때 항상 몬스터 뽑기 탭(PF_ShopPage의 기본 상태)으로 강제 전환한다.
        // OpenShopPage()만 부르면 플레이어가 최근에 열어 둔 다른 상점 탭(다이아 등)이 그대로 보이는 문제가 있었다.
        private void OpenShop()
        {
            managementUi?.OpenShopPage();
            shopCategoryMenu?.ShowMonsterShop();
            tutorialShopQuestId = trackedQuestId;
            tutorialShopResult = FindDeep(managementUi?.ShopPageRoot?.transform, "GachaResultOverlay");
            tutorialShopSawResults = false;
            tutorialShopGoalComplete = false;
        }

        private void RefreshTutorialShopTransition()
        {
            if (string.IsNullOrEmpty(tutorialShopQuestId)) return;
            if (managementUi == null || managementUi.ShopPageRoot == null || !managementUi.ShopPageRoot.activeInHierarchy)
            {
                tutorialShopQuestId = null;
                return;
            }
            if (!QuestRuntime.IsReady) return;
            tutorialShopGoalComplete |= QuestRuntime.GetProgress(new QuestId(tutorialShopQuestId)).Completed;
            if (QuestRuntime.TryGetTrackedQuest(TrackedType, out var definition, out var progress))
                tutorialShopGoalComplete |= definition.QuestId.Value == tutorialShopQuestId && progress.Completed;
            var resultsVisible = tutorialShopResult != null && tutorialShopResult.gameObject.activeInHierarchy;
            tutorialShopSawResults |= resultsVisible;
            if (!tutorialShopGoalComplete || !tutorialShopSawResults || resultsVisible) return;
            tutorialShopQuestId = null;
            managementUi.CloseAllPages(); // 결과 닫힘 완료 후 상점 닫힘 애니메이션 시작
        }

        private void OpenMonsterManagement() => managementUi?.OpenMonsterManagementPage();

        private void OpenCommanderEquipment() => managementUi?.OpenEquipmentPage();

        private void OpenEquipmentSlotUpgrade() => managementUi?.OpenEquipmentSlotUpgradePage();

        private void OpenGrowthDungeon() => managementUi?.OpenGrowthDungeonPage();

        private void OpenExpandedMenu() => hudMenu?.OpenExpandedMenu();

        private void OpenFormation() => hudMenu?.OpenFormationPage();

        private void OpenCommanderGrowthStats() => managementUi?.OpenCommanderGrowthPage();

        private void OpenCommanderGrowthPotential()
        {
            managementUi?.OpenCommanderGrowthPage();
            // OpenCommanderGrowthPage가 SetActive(true)를 동기로 실행해 OnEnable(능력치 탭 기본 선택)까지
            // 이 시점에 이미 끝나 있으므로, 곧바로 잠재능력 탭으로 덮어써도 안전하다.
            var growthView = FindFirstObjectByType<CommanderGrowthPageView>(FindObjectsInactive.Include);
            growthView?.SelectPotentialTab();
        }

        // ---------------------------------------------------------------
        // 참조 탐색 (QuestHudTrackerView와 동일한 이름 기반 탐색 방식)
        // ---------------------------------------------------------------

        private Transform FindTrackerRoot()
        {
            var current = transform;
            while (current != null)
            {
                if (current.name.IndexOf("QuestTracker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    current.name.IndexOf("HudMissionCard", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return current;
                }

                current = current.parent;
            }

            return transform.parent != null ? transform.parent : transform;
        }

        private static Transform FindDeep(Transform root, string objectName)
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

        private static Button FindButton(Transform root, string objectName)
        {
            return FindDeep(root, objectName)?.GetComponent<Button>();
        }
    }
}
