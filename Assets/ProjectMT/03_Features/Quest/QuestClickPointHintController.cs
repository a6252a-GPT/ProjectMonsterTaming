using System;
using System.Collections.Generic;
using DG.Tweening;
using ProjectMT.Features.Commander;
using ProjectMT.Features.Equipment;
using ProjectMT.Features.Formation;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.Quest;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Quest
{
    // 각 관리 페이지 안의 정확한 버튼 위치("ClickPoint")에 손가락 아이콘("ClickImage")을 붙여
    // 미완료 목표의 실제 버튼을 안내한다. QuestHudNavigationController의
    // 트래커 힌트와 별개로, 지금 추적 중인
    // 퀘스트와 연결된 페이지가 실제로 열려 있을 때만(activeInHierarchy) 표시된다.
    // 정식 HUD 카드 프리팹에 부착되어 있다.
    [DisallowMultipleComponent]
    public sealed class QuestClickPointHintController : MonoBehaviour
    {
        private const float RefreshInterval = 0.2f; // 탭 전환·스크롤 등 페이지 내부 상태를 주기적으로 재확인
        private const float HighlightPadding = 8f;
        private const float HintFadeDuration = 0.32f;
        private const QuestType TrackedType = QuestType.Main;

        private static readonly string[] ShopQuestIds =
        {
            "quest_002_monster_summon", "quest_009_monster_owned_count", "quest_021_monster_summon_repeat",
            "quest_024_monster_owned_count_repeat"
        };

        private static readonly string[] CommanderLevelUpQuestIds =
        {
            "quest_004_commander_level_up", "quest_015_commander_level_reach"
        };

        private static readonly string[] PotentialQuestIds =
        {
            "quest_005_commander_potential_upgrade", "quest_022_commander_potential_unlock_count"
        };

        private static readonly string[] MonsterLevelUpQuestIds =
        {
            "quest_003_monster_level_up", "quest_014_monster_level_reach"
        };

        private MainBattleManagementUiController managementUi;
        private QuestHudNavigationController navigationController;
        private CommanderGrowthPageView commanderGrowthView;
        private MonsterManagementPageController monsterManagementView;
        private FormationPageController formationView;
        private EquipmentPageController equipmentView;
        private EquipmentSlotUpgradePanelController equipmentSlotUpgradeView;
        private HudQuickMenuController hudMenu;
        private GameObject hintTemplate;
        private Transform equipmentPageRoot;
        private Button equipmentEquipButton;
        private Button equipmentDismantleTabButton;
        private Button equipmentDismantleAutoSelectButton;
        private Button equipmentDismantleButton;

        private bool referencesResolved;
        private Transform shopClickPoint;
        private Transform commanderLevelUpClickPoint;
        private Transform statHealthClickPoint;
        private Transform statAttackClickPoint;
        private Transform statDefenseClickPoint;
        private Transform potentialClickPoint;
        private Transform monsterLevelUpClickPoint;
        private Transform monsterBreakthroughClickPoint;
        private Transform potentialTabButton;
        private Transform statsTabButton;
        private Transform castleRaidButton;
        private readonly List<Transform> commanderPowerGrowthTargets = new List<Transform>();
        private readonly List<Transform> dungeonEnterClickPoints = new List<Transform>();
        private bool shopHintTargetsButton;

        // ClickPoint별로 한 번 만든 힌트 인스턴스를 재사용한다(매번 Instantiate/Destroy하지 않음).
        private readonly Dictionary<Transform, GameObject> hintInstances = new Dictionary<Transform, GameObject>();
        private readonly Dictionary<Transform, GameObject> highlightInstances = new Dictionary<Transform, GameObject>();
        private readonly Dictionary<Transform, Sequence> hintFadeSequences = new Dictionary<Transform, Sequence>();
        private readonly Dictionary<Transform, bool> requestedHintVisibility = new Dictionary<Transform, bool>();
        private readonly HashSet<Transform> activeHintTargets = new HashSet<Transform>();
        private RectTransform activeSpotlightTarget;
        private string activeStepKey;
        private Transform resultOverlay;
        private Button resultCloseButton;
        private string statusMessage;
        private string shownTutorialQuestId;
        private bool tutorialPageWasOpen;

        // ClickPoint별로 "표시가 시작된 시각"을 기록해 각자 독립적으로 10초 후 꺼지게 한다.
        private readonly Dictionary<Transform, float> hintShownAt = new Dictionary<Transform, float>();
        private readonly Dictionary<Transform, string> activeHintIds = new Dictionary<Transform, string>();

        // 상점이 열리면 몬스터 소환 페이지가 이미 선택되어 있으므로, 접힌 하위 메뉴 버튼이 아니라
        // MonsterShop의 실제 OneButton(1회)을 우선 가리킨다. 실제 뽑기 버튼을 누르면 힌트를 종료한다.
        private Button monsterGachaButton;
        private readonly List<Button> monsterGachaActionButtons = new List<Button>();
        private string lastTrackedQuestId;
        private float tickTimer;

        // 추적 퀘스트의 대상 페이지가 열려 있는 동안 true다(10초 표시 종료 여부와 무관).
        // QuestHudNavigationController가 이 값을 보고 메인 화면 트래커 힌트를 숨긴다.
        public bool HasVisibleHint { get; private set; }

        private void Awake()
        {
            managementUi = FindFirstObjectByType<MainBattleManagementUiController>(FindObjectsInactive.Include);
            navigationController = FindFirstObjectByType<QuestHudNavigationController>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            QuestRuntime.Changed += ForceRefreshNow;
            hintShownAt.Clear();
            activeHintIds.Clear();
            lastTrackedQuestId = null;
            tickTimer = RefreshInterval; // 켜지는 즉시 한 번 갱신
        }

        private void OnDisable()
        {
            QuestRuntime.Changed -= ForceRefreshNow;
            HideAllHints();
            RemoveMonsterGachaListeners();
            referencesResolved = false;
            hintShownAt.Clear();
            activeHintIds.Clear();
            lastTrackedQuestId = null;
            HasVisibleHint = false;
            QuestTutorialSpotlight.Hide(this);
        }

        internal void DismissShownTutorial()
        {
            if (string.IsNullOrEmpty(shownTutorialQuestId)) return;
            QuestRuntime.DismissTutorial(new QuestId(shownTutorialQuestId));
            shownTutorialQuestId = null;
            HideAllHints();
            HasVisibleHint = false;
        }

        private void ForceRefreshNow()
        {
            tickTimer = RefreshInterval;
        }

        private void Update()
        {
            tickTimer += Time.unscaledDeltaTime;
            if (tickTimer < RefreshInterval)
            {
                return;
            }

            tickTimer = 0f;
            Refresh();
        }

        private void Refresh()
        {
            EnsureReferencesResolved();
            var pageOpen = managementUi != null && managementUi.IsAnyPageOpen;
            if (tutorialPageWasOpen && !pageOpen) DismissShownTutorial();
            tutorialPageWasOpen = pageOpen;

            var trackedQuestId = (string)null;
            var completedAwaitingClaim = false;
            if (QuestRuntime.IsReady &&
                QuestRuntime.TryGetTrackedQuest(TrackedType, out var definition, out var progressView))
            {
                trackedQuestId = definition.QuestId.Value;
                completedAwaitingClaim = progressView.Completed && !progressView.RewardClaimed;
            }

            if (!string.Equals(lastTrackedQuestId, trackedQuestId, StringComparison.OrdinalIgnoreCase))
            {
                shownTutorialQuestId = null;
                ResetHintSessions();
                lastTrackedQuestId = trackedQuestId;
            }

            if (QuestRuntime.IsTutorialDismissed(new QuestId(trackedQuestId)))
            {
                HideAllHints();
                HasVisibleHint = false;
                return;
            }

            // 완료 후 보상 대기 상태는 패널 내부가 아니라 HUD의 "완료" 버튼으로 안내한다.
            // 이 프레임에 실제 대상이 된 힌트만 남겨 페이드 애니메이션을 중간에 끊지 않는다.
            activeHintTargets.Clear();
            activeSpotlightTarget = null;
            activeStepKey = null;
            statusMessage = null;
            var pageQuestId = completedAwaitingClaim ? null : trackedQuestId;
            var anyPageTargetActive = false;
            var showingResults = resultOverlay != null && resultOverlay.gameObject.activeInHierarchy;
            var revealing = showingResults && resultOverlay.GetComponent<GachaRevealSequence>() is { IsPlaying: true };
            anyPageTargetActive |= ApplyHint(resultCloseButton,
                Matches(trackedQuestId, ShopQuestIds) && showingResults && !revealing,
                "shop_result", true);
            anyPageTargetActive |= ApplyHint(
                shopClickPoint,
                Matches(pageQuestId, ShopQuestIds) && !showingResults,
                "shop_gacha",
                shopHintTargetsButton);

            var potentialQuest = Matches(pageQuestId, PotentialQuestIds);
            var growthStatsQuest = Matches(pageQuestId, CommanderLevelUpQuestIds) ||
                                   Matches(pageQuestId, "quest_016_commander_health_level_reach") ||
                                   Matches(pageQuestId, "quest_017_commander_attack_level_reach") ||
                                   Matches(pageQuestId, "quest_018_commander_defense_level_reach") ||
                                   Matches(pageQuestId, "quest_019_commander_power_reach");
            var isPotentialTabSelected = commanderGrowthView != null && commanderGrowthView.IsPotentialTabSelected;
            anyPageTargetActive |= ApplyHint(
                commanderGrowthView?.QuestPotentialTabButton?.transform ?? potentialTabButton,
                potentialQuest && !isPotentialTabSelected,
                "commander_potential_tab",
                true);
            anyPageTargetActive |= ApplyHint(
                commanderGrowthView?.QuestStatsTabButton?.transform ?? statsTabButton,
                growthStatsQuest && isPotentialTabSelected,
                "commander_stats_tab",
                true);
            anyPageTargetActive |= ApplyHint(
                potentialClickPoint, potentialQuest && isPotentialTabSelected,
                "commander_potential_action");
            anyPageTargetActive |= ApplyHint(commanderLevelUpClickPoint,
                Matches(pageQuestId, CommanderLevelUpQuestIds) && !isPotentialTabSelected,
                "commander_level_up");
            anyPageTargetActive |= ApplyHint(statHealthClickPoint,
                Matches(pageQuestId, "quest_016_commander_health_level_reach") && !isPotentialTabSelected,
                "commander_health", true);
            anyPageTargetActive |= ApplyHint(statAttackClickPoint,
                Matches(pageQuestId, "quest_017_commander_attack_level_reach") && !isPotentialTabSelected,
                "commander_attack", true);
            anyPageTargetActive |= ApplyHint(statDefenseClickPoint,
                Matches(pageQuestId, "quest_018_commander_defense_level_reach") && !isPotentialTabSelected,
                "commander_defense", true);
            var commanderPowerQuest = Matches(pageQuestId, "quest_019_commander_power_reach");
            anyPageTargetActive |= ApplyHint(
                commanderPowerQuest ? ResolveCommanderPowerGrowthTarget() : null,
                commanderPowerQuest && !isPotentialTabSelected,
                "commander_power",
                true);

            anyPageTargetActive |= ApplyHint(
                monsterLevelUpClickPoint, Matches(pageQuestId, MonsterLevelUpQuestIds), "monster_level_up");

            var breakthroughQuest = Matches(pageQuestId, "quest_006_monster_ascension");
            var breakthroughTabActive = monsterManagementView != null && monsterManagementView.IsBreakthroughTabActive;
            var breakthroughCardSelected = monsterManagementView != null &&
                                            monsterManagementView.HasQuestBreakthroughCardSelection;
            anyPageTargetActive |= ApplyHint(monsterManagementView?.QuestBreakthroughTabButton,
                breakthroughQuest && !breakthroughTabActive, "monster_breakthrough_tab", true);
            anyPageTargetActive |= ApplyHint(monsterManagementView?.QuestBreakthroughCandidateButton,
                breakthroughQuest && breakthroughTabActive && !breakthroughCardSelected,
                "monster_breakthrough_candidate", true);
            anyPageTargetActive |= ApplyHint(monsterBreakthroughClickPoint,
                breakthroughQuest && breakthroughTabActive && breakthroughCardSelected,
                "monster_breakthrough_action");

            var formationQuest = Matches(pageQuestId, "quest_010_monster_formation");
            var formationCardSelected = formationView != null && formationView.HasQuestFormationCardSelection;
            anyPageTargetActive |= ApplyHint(formationView?.QuestFormationCandidateButton,
                formationQuest && formationView != null && formationView.IsOpen && !formationCardSelected,
                "formation_candidate", true);
            anyPageTargetActive |= ApplyHint(formationView?.QuestFormationActionButton,
                formationQuest && formationView != null && formationView.IsOpen &&
                (formationCardSelected || formationView.QuestFormationCandidateButton == null),
                "formation_action", true);

            var equipmentPageOpen = equipmentPageRoot != null && equipmentPageRoot.gameObject.activeInHierarchy;
            var equipDetailButton = equipmentView?.QuestEquipDetailButton;
            anyPageTargetActive |= ApplyHint(equipDetailButton ?? equipmentView?.QuestEquipCandidateButton,
                Matches(pageQuestId, "quest_007_equipment_equip") && equipmentPageOpen,
                equipDetailButton != null ? "equipment_equip" : "equipment_candidate", true);

            var slotUpgradeQuest = Matches(pageQuestId, "quest_008_equipment_enhance") ||
                                   Matches(pageQuestId, "quest_020_equipment_slot_upgrade_reach");
            var equipmentPartSelected = equipmentSlotUpgradeView != null && equipmentSlotUpgradeView.HasSelectedPart;
            anyPageTargetActive |= ApplyHint(equipmentSlotUpgradeView?.QuestFirstPartButton,
                slotUpgradeQuest && !equipmentPartSelected, "slot_part", true);
            anyPageTargetActive |= ApplyHint(equipmentSlotUpgradeView?.QuestUpgradeButton,
                slotUpgradeQuest && equipmentPartSelected, "slot_upgrade", true);

            var dismantleQuest = Matches(pageQuestId, "quest_013_equipment_dismantle");
            var dismantleMode = equipmentView != null && equipmentView.IsDismantleMode;
            var dismantleStep = equipmentView != null ? equipmentView.QuestDismantleHintStep : 0;
            anyPageTargetActive |= ApplyHint(
                equipmentView?.QuestDismantleModeTabButton ?? equipmentDismantleTabButton,
                dismantleQuest && equipmentPageOpen && !dismantleMode, "dismantle_tab", true);
            anyPageTargetActive |= ApplyHint(
                equipmentView?.QuestDismantleAutoSelectButton ?? equipmentDismantleAutoSelectButton,
                dismantleQuest && equipmentPageOpen && dismantleMode && dismantleStep <= 1,
                "dismantle_auto", true);
            anyPageTargetActive |= ApplyHint(
                equipmentView?.QuestDismantleActionButton ?? equipmentDismantleButton,
                dismantleQuest && equipmentPageOpen && dismantleMode && dismantleStep >= 2,
                "dismantle_action", true);

            // 보이는 입장 버튼 하나만 안내하며 다른 콘텐츠 선택을 막지 않는다.
            var dungeonActive = Matches(pageQuestId, "quest_011_growth_dungeon_enter");
            for (var i = 0; i < dungeonEnterClickPoints.Count; i++)
            {
                var point = dungeonEnterClickPoints[i];
                // 성장 던전 목록은 스크롤 패널이라, 지금 뷰포트 안에 실제로 보이는 슬롯에만 힌트를 띄운다.
                var visible = dungeonActive && IsVisibleWithinScrollView(point as RectTransform);
                var placeOnBottomRight = point != null && point.name == "EnterButton_GUIPro";
                anyPageTargetActive |= ApplyHint(point, visible, "growth_dungeon_enter", placeOnBottomRight);
            }

            anyPageTargetActive |= ApplyHint(castleRaidButton,
                Matches(pageQuestId, "quest_012_castle_raid_enter"), "castle_raid", true);

            if (completedAwaitingClaim && !showingResults && managementUi != null && managementUi.IsAnyPageOpen)
            {
                foreach (var button in managementUi.GetComponentsInChildren<Button>())
                {
                    if (button.name.IndexOf("Close", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (ApplyHint(button, true, "page_close", true)) { anyPageTargetActive = true; break; }
                }
            }

            HideHintsOutsideActiveTargets();
            HasVisibleHint = anyPageTargetActive;
            if (activeSpotlightTarget != null)
            {
                if (pageOpen) shownTutorialQuestId = trackedQuestId;
                QuestTutorialSpotlight.Show(this, activeSpotlightTarget,
                    statusMessage ?? QuestTutorialInteraction.Message(activeStepKey), statusMessage == null);
            }
            else
            {
                QuestTutorialSpotlight.Hide(this);
            }
        }

        private static bool Matches(string trackedQuestId, string questId)
        {
            return !string.IsNullOrEmpty(trackedQuestId) &&
                   string.Equals(trackedQuestId, questId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Matches(string trackedQuestId, IReadOnlyList<string> questIds)
        {
            if (string.IsNullOrEmpty(trackedQuestId))
            {
                return false;
            }

            for (var i = 0; i < questIds.Count; i++)
            {
                if (string.Equals(trackedQuestId, questIds[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        // clickPoint가 없거나 지금 화면상 비활성 상태(다른 탭·페이지가 닫힘 등)면 숨긴다.
        // 반환값은 페이지가 열려 있고 해당 ClickPoint가 활성 대상인지이며, 실제 손가락 표시 여부와는
        // 무관하게 외부 트래커를 숨기는 데 사용한다.
        private bool ApplyHint(Button button, bool shouldShow, string stepKey, bool placeOnBottomRight)
        {
            return ApplyHint(button != null ? button.transform : null, shouldShow, stepKey, placeOnBottomRight);
        }

        private bool ApplyHint(Transform clickPoint, bool shouldShow, string stepKey)
        {
            return ApplyHint(clickPoint, shouldShow, stepKey, false);
        }

        private bool ApplyHint(Transform clickPoint, bool shouldShow, string stepKey, bool placeOnBottomRight)
        {
            if (clickPoint == null)
            {
                return false;
            }

            var resolvedTarget = ResolveHighlightTarget(clickPoint);
            var isActive = shouldShow && activeSpotlightTarget == null &&
                IsVisibleWithinScrollView(resolvedTarget) && QuestTutorialInteraction.IsVisible(resolvedTarget);
            if (!isActive)
            {
                HideHint(clickPoint);
                hintShownAt.Remove(clickPoint);
                activeHintIds.Remove(clickPoint);
                return false;
            }

            activeHintTargets.Add(clickPoint);
            activeSpotlightTarget = resolvedTarget;
            activeStepKey = stepKey;
            var potentialAtGoal = Matches(lastTrackedQuestId, "quest_022_commander_potential_unlock_count") && CommanderPotentialRuntime.IsMaxStage;
            var potentialBlocked = stepKey == "commander_potential_action" &&
                (potentialAtGoal || CommanderPotentialRuntime.StoneBalance <= 0 || !CommanderPotentialRuntime.HasRerollableSlot());
            if (!QuestTutorialInteraction.CanInteract(resolvedTarget) || potentialBlocked)
            {
                HideHint(clickPoint);
                statusMessage = stepKey == "shop_gacha" ? "소환권과 재화가 준비되면 계속할 수 있어요" :
                    potentialAtGoal ? "이미 최대 각성 단계예요" :
                    stepKey == "commander_potential_action" ? "강화석과 잠재능력 보호 상태를 확인해주세요" :
                    stepKey == "formation_action" ? "현재 편성 인원과 선택 상태를 확인해주세요" :
                    "필요 재료와 현재 성장 단계를 확인해주세요";
                return true;
            }

            var instance = GetOrCreateHint(clickPoint, placeOnBottomRight);
            PositionHint(instance != null ? instance.transform as RectTransform : null,
                resolvedTarget,
                true);
            // 상점 몬스터 뽑기 퀘스트는 손가락만 표시하고 노란 테두리는 사용하지 않는다.
            var showHighlight = false; // 공통 강조선 하나만 사용
            var highlight = showHighlight ? GetOrCreateHighlight(clickPoint) : null;
            if (!showHighlight)
            {
                HideHighlight(clickPoint);
            }
            PositionHighlight(highlight != null ? highlight.transform as RectTransform : null, clickPoint);
            instance?.transform.SetAsLastSibling();
            ApplyFadedVisibility(clickPoint, instance, highlight, true);
            return true;
        }

        private GameObject GetOrCreateHint(Transform clickPoint, bool placeOnBottomRight)
        {
            if (hintInstances.TryGetValue(clickPoint, out var existing) && existing != null)
            {
                return existing;
            }

            var template = ResolveTemplate();
            if (template == null)
            {
                return null;
            }

            var parentCanvas = clickPoint.GetComponentInParent<Canvas>();
            var visualParent = parentCanvas != null
                ? parentCanvas.transform as RectTransform
                : clickPoint.parent as RectTransform;
            if (visualParent == null)
            {
                return null;
            }

            // 버튼 내부에 넣으면 행·마스크·버튼 배경의 뒤로 밀릴 수 있으므로, 같은 페이지 Canvas의
            // 최상단 자식으로 복제한다. 원본 ClickImage의 Canvas 정렬 설정을 상속한다.
            var instance = Instantiate(template, visualParent);
            instance.name = "ClickImage";
            if (instance.transform is RectTransform rect)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = rect.anchorMin;
                rect.anchoredPosition = Vector2.zero;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;

                // 확장 메뉴의 군단의 역습 힌트는 버튼 자체가 작으므로 일반 손가락의 절반 크기만 사용한다.
                if (clickPoint.name == "CastleRaidButton")
                {
                    rect.sizeDelta *= 0.5f;
                }
            }

            // 페이지 Canvas의 마지막 자식으로 두고, 힌트는 어떤 클릭도 가로채지 않게 만든다.
            instance.transform.SetAsLastSibling();
            instance.GetComponent<QuestTutorialFingerPulse>()?.Rebase();
            hintInstances[clickPoint] = instance;
            return instance;
        }

        private static void PositionHint(RectTransform hint, RectTransform target, bool placeOnBottomRight)
        {
            if (hint == null || target == null || hint.parent is not RectTransform parent)
            {
                return;
            }

            var canvas = parent.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var worldPoint = placeOnBottomRight ? corners[3] : target.TransformPoint(target.rect.center);
            var screenPoint = RectTransformUtility.WorldToScreenPoint(camera, worldPoint);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, camera, out var localPoint))
            {
                hint.anchoredPosition = localPoint + GetHintOffset(target, placeOnBottomRight);
            }
        }

        private static Vector2 GetHintOffset(RectTransform target, bool placeOnBottomRight)
        {
            if (!placeOnBottomRight)
            {
                return Vector2.zero;
            }

            // 몬스터 관리의 '돌파' 탭은 손가락 끝이 버튼 오른쪽 아래에 닿도록 일반 버튼보다 아래에 배치한다.
            if (target.name.Contains("BreakthroughTab", StringComparison.OrdinalIgnoreCase))
            {
                return new Vector2(-18f, -34f);
            }

            // 입장·자동 장착·메인 편성은 손가락 끝이 버튼의 오른쪽 아래에 닿도록 아래로 내린다.
            if (target.name == "EnterButton_GUIPro" ||
                target.name == "EquipButton" ||
                target.name.Contains("FormationConfirm", StringComparison.OrdinalIgnoreCase))
            {
                return new Vector2(-18f, -34f);
            }

            return new Vector2(-18f, -34f);
        }

        private GameObject GetOrCreateHighlight(Transform clickPoint)
        {
            if (highlightInstances.TryGetValue(clickPoint, out var existing) && existing != null)
            {
                return existing;
            }

            var target = ResolveHighlightTarget(clickPoint);
            if (target == null)
            {
                return null;
            }

            var parentCanvas = target.GetComponentInParent<Canvas>();
            var visualParent = parentCanvas != null
                ? parentCanvas.transform as RectTransform
                : target.parent as RectTransform;
            if (visualParent == null)
            {
                return null;
            }

            var prefab = Resources.Load<GameObject>("UI/PF_QuestButtonHighlight");
            if (prefab == null)
            {
                Debug.LogError("Quest button highlight prefab is missing.", this);
                return null;
            }

            var frame = Instantiate(prefab, visualParent);
            frame.name = "QuestButtonHighlight";

            frame.transform.SetAsLastSibling();
            highlightInstances[clickPoint] = frame;
            return frame;
        }

        private static RectTransform ResolveHighlightTarget(Transform clickPoint)
        {
            if (clickPoint == null)
            {
                return null;
            }

            // 군단장 성장 행은 ClickPoint가 작은 보조 좌표이므로 실제 버튼 외곽(ButtonArea)을 강조한다.
            for (var current = clickPoint; current != null; current = current.parent)
            {
                if (current.name == "ButtonArea")
                {
                    return current as RectTransform;
                }
            }

            var button = clickPoint.GetComponentInParent<Button>();
            return button != null ? button.transform as RectTransform : clickPoint as RectTransform;
        }

        private static void PositionHighlight(RectTransform highlight, Transform clickPoint)
        {
            var target = ResolveHighlightTarget(clickPoint);
            if (highlight == null || target == null || highlight.parent is not RectTransform parent)
            {
                return;
            }

            var canvas = parent.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var bottomLeftScreen = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var topRightScreen = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, bottomLeftScreen, camera, out var bottomLeft) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, topRightScreen, camera, out var topRight))
            {
                return;
            }

            highlight.anchoredPosition = (bottomLeft + topRight) * 0.5f;
            highlight.sizeDelta = new Vector2(
                Mathf.Abs(topRight.x - bottomLeft.x) + HighlightPadding * 2f,
                Mathf.Abs(topRight.y - bottomLeft.y) + HighlightPadding * 2f);
        }

        private GameObject ResolveTemplate()
        {
            if (hintTemplate != null)
            {
                return hintTemplate;
            }

            navigationController ??= FindFirstObjectByType<QuestHudNavigationController>(FindObjectsInactive.Include);
            hintTemplate = navigationController != null ? navigationController.ClickImageTemplate : null;
            return hintTemplate;
        }

        private void HideAllHints()
        {
            foreach (var instance in hintInstances.Values)
            {
                if (instance != null)
                {
                    instance.SetActive(false);
                }
            }

            foreach (var highlight in highlightInstances.Values)
            {
                if (highlight != null)
                {
                    highlight.SetActive(false);
                }
            }

            foreach (var sequence in hintFadeSequences.Values)
            {
                sequence?.Kill();
            }

            hintFadeSequences.Clear();
            requestedHintVisibility.Clear();
            activeHintTargets.Clear();
            activeSpotlightTarget = null;
            QuestTutorialSpotlight.Hide(this);
        }

        private void HideHint(Transform clickPoint)
        {
            if (clickPoint != null && hintFadeSequences.TryGetValue(clickPoint, out var sequence))
            {
                sequence?.Kill();
                hintFadeSequences.Remove(clickPoint);
            }

            if (clickPoint != null)
            {
                requestedHintVisibility.Remove(clickPoint);
            }

            if (clickPoint != null && hintInstances.TryGetValue(clickPoint, out var instance) && instance != null)
            {
                instance.SetActive(false);
            }

            HideHighlight(clickPoint);
        }

        private void HideHighlight(Transform clickPoint)
        {
            if (clickPoint != null && highlightInstances.TryGetValue(clickPoint, out var highlight) && highlight != null)
            {
                highlight.SetActive(false);
            }
        }

        private void HideHintsOutsideActiveTargets()
        {
            foreach (var target in hintInstances.Keys)
            {
                if (!activeHintTargets.Contains(target))
                {
                    HideHint(target);
                }
            }
        }

        private void ApplyFadedVisibility(Transform clickPoint, GameObject hint, GameObject highlight, bool visible)
        {
            if (hint == null && highlight == null)
            {
                return;
            }

            var hasRequestedVisibility = requestedHintVisibility.TryGetValue(clickPoint, out var currentVisibility);
            if (hasRequestedVisibility && currentVisibility == visible)
            {
                return;
            }

            if (!hasRequestedVisibility)
            {
                SetImageAlpha(hint, 0f);
                SetImageAlpha(highlight, 0f);
            }

            requestedHintVisibility[clickPoint] = visible;
            hint?.SetActive(true);
            highlight?.SetActive(true);

            if (hintFadeSequences.TryGetValue(clickPoint, out var previous))
            {
                previous?.Kill();
            }

            var targetAlpha = visible ? 1f : 0f;
            var sequence = DOTween.Sequence().SetUpdate(true);
            JoinFade(sequence, hint, targetAlpha);
            JoinFade(sequence, highlight, targetAlpha);
            sequence.SetEase(Ease.InOutSine);
            hintFadeSequences[clickPoint] = sequence;
        }

        private static void JoinFade(Sequence sequence, GameObject target, float alpha)
        {
            if (sequence == null || target == null)
            {
                return;
            }

            var images = target.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                var image = images[i];
                sequence.Join(DOTween.To(
                    () => image.color.a,
                    value =>
                    {
                        var color = image.color;
                        color.a = value;
                        image.color = color;
                    },
                    alpha,
                    HintFadeDuration));
            }
        }

        private static void SetImageAlpha(GameObject target, float alpha)
        {
            if (target == null)
            {
                return;
            }

            var images = target.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                var color = images[i].color;
                color.a = alpha;
                images[i].color = color;
            }
        }

        private void ResetHintSessions()
        {
            HideAllHints();
            hintShownAt.Clear();
            activeHintIds.Clear();
        }

        private static string BuildTutorialHintId(string questId, string stepKey) =>
            $"quest_tutorial:{questId}:page:{stepKey}";

        // ---------------------------------------------------------------
        // 스크롤 뷰포트 가시성 체크(성장 던전 목록 전용)
        // ---------------------------------------------------------------

        private static bool IsVisibleWithinScrollView(RectTransform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return false;
            }

            var mask = target.GetComponentInParent<RectMask2D>();
            var viewport = mask != null ? mask.rectTransform : null;
            if (viewport == null)
            {
                var scrollRect = target.GetComponentInParent<ScrollRect>();
                if (scrollRect != null)
                {
                    viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
                }
            }

            if (viewport == null)
            {
                return true; // 스크롤/마스크 조상이 없으면 항상 화면에 있는 것으로 간주
            }

            var viewportCorners = new Vector3[4];
            viewport.GetWorldCorners(viewportCorners);
            var targetCorners = new Vector3[4];
            target.GetWorldCorners(targetCorners);
            // 일부라도 잘린 카드에는 손가락·테두리를 표시하지 않는다. 클릭 가능한 버튼 전체가
            // 스크롤 뷰포트 안에 들어왔을 때만 표시한다.
            return targetCorners[0].x >= viewportCorners[0].x && targetCorners[2].x <= viewportCorners[2].x &&
                   targetCorners[0].y >= viewportCorners[0].y && targetCorners[2].y <= viewportCorners[2].y;
        }

        // ---------------------------------------------------------------
        // 참조 탐색 (이름 기반, 페이지가 비활성 상태여도 자식은 그대로 찾아진다)
        // ---------------------------------------------------------------

        private void EnsureReferencesResolved()
        {
            if (referencesResolved)
            {
                return;
            }

            managementUi ??= FindFirstObjectByType<MainBattleManagementUiController>(FindObjectsInactive.Include);
            if (managementUi == null)
            {
                return; // 다음 틱에 다시 시도
            }

            commanderGrowthView ??= FindFirstObjectByType<CommanderGrowthPageView>(FindObjectsInactive.Include);
            monsterManagementView ??= FindFirstObjectByType<MonsterManagementPageController>(FindObjectsInactive.Include);
            formationView ??= FindFirstObjectByType<FormationPageController>(FindObjectsInactive.Include);
            equipmentPageRoot ??= FindDeep(managementUi.transform, "PF_CommanderEquipmentPage");
            equipmentView ??= equipmentPageRoot != null
                ? equipmentPageRoot.GetComponentInChildren<EquipmentPageController>(true)
                : FindFirstObjectByType<EquipmentPageController>(FindObjectsInactive.Include);
            equipmentSlotUpgradeView ??= FindFirstObjectByType<EquipmentSlotUpgradePanelController>(FindObjectsInactive.Include);
            hudMenu ??= FindFirstObjectByType<HudQuickMenuController>(FindObjectsInactive.Include);

            // 장비 페이지는 같은 컨트롤러 타입이 다른 화면에도 있어, 퀘스트 힌트 대상은 반드시
            // CommanderEquipmentPage 아래의 정확한 버튼 이름으로 직접 찾는다.
            equipmentEquipButton = equipmentView?.QuestAutoEquipButton ?? FindButton(equipmentPageRoot, "EquipButton");
            equipmentDismantleTabButton = FindButton(equipmentPageRoot, "DismantleModeTabButton");
            equipmentDismantleAutoSelectButton = FindButton(equipmentPageRoot, "DismantleAutoSelectButton");
            equipmentDismantleButton = FindButton(equipmentPageRoot, "DismantleButton");

            var shopRoot = managementUi.ShopPageRoot?.transform;
            resultOverlay = FindDeep(shopRoot, "GachaResultOverlay");
            resultCloseButton = FindButton(shopRoot, "ResultCloseButton");
            var legacyShopClickPoint = FindDeep(shopRoot, "ClickPoint");
            monsterGachaButton = FindButton(shopRoot, "MonsterGachaButton");
            monsterGachaActionButtons.Clear();
            var monsterShopRoot = FindDeep(managementUi.ShopPageRoot?.transform, "MonsterShop");
            AddMonsterGachaActionButtons(monsterShopRoot, "OneButton");
            AddMonsterGachaActionButtons(monsterShopRoot, "TwoButton");
            var preferredGachaAction = monsterGachaActionButtons.Count > 0
                ? monsterGachaActionButtons[0]
                : null;
            // 상점 레이아웃이 바뀌어도 손가락이 옛 패널 좌표나 이미 선택된 탭에 남지 않도록
            // 실제 소환 동작 버튼을 기준으로 한다. 구형 UI만 기존 진입 버튼/ClickPoint로 폴백한다.
            shopClickPoint = preferredGachaAction != null
                ? preferredGachaAction.transform
                : monsterGachaButton != null
                    ? monsterGachaButton.transform
                    : legacyShopClickPoint;
            shopHintTargetsButton = preferredGachaAction != null || monsterGachaButton != null;

            var growthRoot = managementUi.CommanderGrowthPageRoot?.transform;
            commanderLevelUpClickPoint = FindClickPointUnder(growthRoot, "LevelUpButton");
            // 성장 행의 ClickPoint는 ButtonArea의 형제라 테두리 기준으로 쓸 수 없다.
            // 실제 클릭 버튼 영역을 직접 사용해 손가락과 강조선이 동일한 버튼을 가리키게 한다.
            statHealthClickPoint = FindDeep(FindDeep(growthRoot, "GrowthRow_Health"), "ButtonArea");
            statAttackClickPoint = FindDeep(FindDeep(growthRoot, "GrowthRow_Attack"), "ButtonArea");
            statDefenseClickPoint = FindDeep(FindDeep(growthRoot, "GrowthRow_Defense"), "ButtonArea");
            commanderPowerGrowthTargets.Clear();
            AddCommanderPowerGrowthTarget(growthRoot, "GrowthRow_Health");
            AddCommanderPowerGrowthTarget(growthRoot, "GrowthRow_Attack");
            AddCommanderPowerGrowthTarget(growthRoot, "GrowthRow_Defense");
            AddCommanderPowerGrowthTarget(growthRoot, "GrowthRow_AttackSpeed");
            AddCommanderPowerGrowthTarget(growthRoot, "GrowthRow_MoveSpeed");
            AddCommanderPowerGrowthTarget(growthRoot, "GrowthRow_AttackRange");
            potentialClickPoint = FindClickPointUnder(growthRoot, "ButtonArea_2");
            potentialTabButton = commanderGrowthView != null
                ? commanderGrowthView.QuestPotentialTabButton?.transform
                : FindDeep(growthRoot, "PotentialTab");
            statsTabButton = commanderGrowthView != null
                ? commanderGrowthView.QuestStatsTabButton?.transform
                : FindDeep(growthRoot, "StatsTab");

            var monsterRoot = managementUi.MonsterManagementPageRoot?.transform;
            // 몬스터 관리 원본에 저장된 버튼별 안내 기준점을 사용한다.
            monsterLevelUpClickPoint = FindClickPointUnder(monsterRoot, "LevelUpActionButton");
            monsterBreakthroughClickPoint = FindClickPointUnder(monsterRoot, "BreakthroughActionButton");

            var dungeonRoot = managementUi.GrowthDungeonPageRoot?.transform;
            var enterButtons = FindAllDeep(dungeonRoot, "EnterButton_GUIPro");
            dungeonEnterClickPoints.Clear();
            for (var i = 0; i < enterButtons.Count; i++)
            {
                // 카드 내부의 보조 ClickPoint가 아니라 실제 입장 버튼 자체를 기준으로 한다.
                dungeonEnterClickPoints.Add(enterButtons[i]);
            }

            // 확장 메뉴 안의 군단의 역습 버튼만 찾는다. HUD 고정 바로가기와 이름이 같아도 메뉴 루트를
            // 기준으로 탐색하므로 퀘스트 이동으로 펼친 메뉴의 버튼을 정확히 가리킨다.
            castleRaidButton = hudMenu != null ? FindDeep(hudMenu.transform, "CastleRaidButton") : null;

            referencesResolved = true;
        }

        private void AddCommanderPowerGrowthTarget(Transform growthRoot, string rowName)
        {
            var target = FindDeep(FindDeep(growthRoot, rowName), "ButtonArea");
            if (target != null)
            {
                commanderPowerGrowthTargets.Add(target);
            }
        }

        private Transform ResolveCommanderPowerGrowthTarget()
        {
            // 전투력 퀘스트는 특정 능력치 하나가 아니라 전체 성장으로 달성한다. 현재 실제로 누를 수 있는
            // 강화 버튼을 우선 안내하고, 모두 비활성이면 첫 번째 보이는 버튼을 안내해 화면 목적지는 유지한다.
            for (var i = 0; i < commanderPowerGrowthTargets.Count; i++)
            {
                var target = commanderPowerGrowthTargets[i];
                var button = target != null ? target.GetComponentInChildren<Button>(true) : null;
                if (target != null && target.gameObject.activeInHierarchy && button != null && button.interactable)
                {
                    return target;
                }
            }

            for (var i = 0; i < commanderPowerGrowthTargets.Count; i++)
            {
                var target = commanderPowerGrowthTargets[i];
                if (target != null && target.gameObject.activeInHierarchy)
                {
                    return target;
                }
            }

            return null;
        }

        private void HandleMonsterGachaClicked()
        {
            ForceRefreshNow(); // 성공·결과창 상태로 전환 여부를 판단
        }

        private void AddMonsterGachaActionButtons(Transform root, string buttonName)
        {
            var buttons = FindAllDeep(root, buttonName);
            for (var i = 0; i < buttons.Count; i++)
            {
                var button = buttons[i].GetComponent<Button>();
                if (button == null || monsterGachaActionButtons.Contains(button))
                {
                    continue;
                }

                button.onClick.RemoveListener(HandleMonsterGachaClicked);
                button.onClick.AddListener(HandleMonsterGachaClicked);
                monsterGachaActionButtons.Add(button);
            }
        }

        private void RemoveMonsterGachaListeners()
        {
            if (monsterGachaButton != null)
            {
                monsterGachaButton.onClick.RemoveListener(HandleMonsterGachaClicked);
            }

            for (var i = 0; i < monsterGachaActionButtons.Count; i++)
            {
                monsterGachaActionButtons[i]?.onClick.RemoveListener(HandleMonsterGachaClicked);
            }

            monsterGachaActionButtons.Clear();
        }

        private static Button FindButton(Transform root, string objectName)
        {
            var target = FindDeep(root, objectName);
            return target != null ? target.GetComponent<Button>() : null;
        }

        private static Transform FindClickPointUnder(Transform root, string parentName)
        {
            var parent = FindDeep(root, parentName);
            return parent != null ? FindDeep(parent, "ClickPoint") : null;
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

        private static List<Transform> FindAllDeep(Transform root, string objectName)
        {
            var result = new List<Transform>();
            if (root == null)
            {
                return result;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == objectName)
                {
                    result.Add(transforms[i]);
                }
            }

            return result;
        }
    }
}
