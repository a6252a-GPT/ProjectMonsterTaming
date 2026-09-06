using System;
using ProjectMT.Core.SceneFlow;
using ProjectMT.Contents.Framework;
using ProjectMT.Features.Attendance;
using ProjectMT.Features.Commander;
using ProjectMT.Features.CommanderSkill;
using ProjectMT.Features.Equipment;
using ProjectMT.Features.Expedition;
using ProjectMT.Features.Formation;
using ProjectMT.Features.GrowthDungeon;
using ProjectMT.Features.Inventory;
using ProjectMT.Features.Mailbox;
using ProjectMT.Shared.Combat;
using ProjectMT.Features.Quest;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattleSceneRoot : MonoBehaviour, ISceneRoot // 메인전투 씬 조립·수명 관리
    {
        [SerializeField] private SceneId sceneId = new SceneId("main_battle"); // 메인 씬 식별자
        [SerializeField] private ContentId foodRiotContentId = new ContentId("food_riot"); // Hosted 콘텐츠 ID
        [SerializeField] private ContentId castleRaidContentId = new ContentId("castle_raid"); // 별도 씬 콘텐츠 ID
        [SerializeField] private ContentId guardiansTowerContentId = new ContentId("guardians_tower"); // 08.06 안건준 추가 - 수호자의 탑 Hosted 콘텐츠 ID (식량 대소동과 별도)
        [SerializeField] private ContentId fallenCommanderContentId = new ContentId("fallen_commander"); // 군단장 보스전 Hosted 연결부
        [SerializeField] private ExpeditionController expedition; // 원정대 진행 담당
        [SerializeField, Range(0f, 1f)] private float monsterEmissionBrightnessScale = 0.6f; // 전투 몬스터 원본색은 유지하고 자체발광만 60%
        [SerializeField, Range(0f, 1f)] private float monsterVfxBrightnessScale =
            MonsterBasicAttackVfxPlayback.DefaultMainBattleBrightnessScale; // 전투 스킬 VFX 밝기도 60%
        [SerializeField] private MainBattleHostedContentRunner hostedRunner; // 성장 던전 전환 담당
        [SerializeField] private Button foodRiotButton; // 식량 대소동 입장 버튼
        [SerializeField] private Button castleRaidButton; // 군단의 역습 입장 버튼
        [SerializeField] private Button towerButton; // 08.06 안건준 추가 - 수호자의 탑 입장 버튼
        [SerializeField] private Button fallenCommanderButton; // 타락한 과거의 군단장 입장 버튼
        [SerializeField] private Button foodRiotSweepButton; // 식량 대소동 1회 소탕
        [SerializeField] private Button towerSweepButton; // 고대 수호수 1회 소탕
        [SerializeField] private TMP_Text foodRiotKeyText; // 식량 열쇠 현재/최대
        [SerializeField] private TMP_Text treasureSpiritKeyText; // 보물 정령 열쇠 현재/최대
        [SerializeField] private TMP_Text fallenCommanderKeyText; // 군단장 던전 열쇠 현재/최대
        [SerializeField] private TMP_Text towerKeyText; // 수호수 열쇠 현재/최대
        [SerializeField] private TMP_Text statusText; // 현재 플레이 상태
        [SerializeField] private FormationPageController formationPage; // 보유·편성 통합 화면
        [SerializeField] private MonsterManagementPageController monsterManagementPage; // 몬스터 성장 관리창
        [SerializeField] private GachaSystem gachaSystem; // 몬스터 뽑기 (없어도 씬 동작에는 영향 없음)
        [SerializeField] private ShopPageView shopPageView; // 상점 탭·재화 표시
        [SerializeField] private EquipmentPageController equipmentPage; // 08.10 안건준 추가 - 장비창(없어도 씬 동작에는 영향 없음)
        [SerializeField] private EquipmentSlotUpgradePanelController equipmentSlotUpgradePanel; // 장비 슬롯 강화 패널(없어도 씬 동작에는 영향 없음)
        [SerializeField] private ItemInventoryPageController itemInventoryPage; // 일반 아이템 인벤토리

        private MainSceneContext context; // 진행·콘텐츠 실행 권한
        private BattlePartySnapshot party; // 시드 부대 사진
        private MainBattleMonsterDragController monsterDrag; // 메인전투 직접 재배치 입력
        private MainBattleDragIndicatorPresenter dragIndicator; // 선택 몬스터·목적지 표시
        private MainBattleSpatialController spatialController; // 전투 간격·군단장 추종
        private MainBattleFormationPlacementController placementController; // 본부대 시작 위치 편집
        private MainBattleManagementUiController managementUi; // 관리창 상호 배타 제어
        private MainBattleHudProgressView hudProgressView; // 상단 계정·재화 표시
        private GrowthDungeonStageEntryController growthDungeonEntry; // 단계 선택·파밍 입장
        private CastleRaidStageSelectionController castleRaidEntry; // 군단의 역습 1~100 단계 선택
        private CommanderGrowthPageView commanderGrowthPage; // 저장 편성 총전투력 표시
        private CommanderSkillRuntime commanderSkillRuntime; // 군단장 스킬 실행
        private CommanderSkillHudView commanderSkillHud; // 우측 하단 AUTO·6슬롯 HUD
        private CommanderSkillPageController commanderSkillPage; // 군단장 스킬 장착·성장 관리창
        private CommanderSkillSummonController commanderSkillSummon; // 상점 전용 스킬 소환
        private AttendancePanelController attendancePanel; // 28일 출석 팝업
        private MailboxPanelController mailboxPanel; // 우편 목록·수령 팝업
        private HudQuickMenuController quickMenu; // 출석·우편 알림 배지
        private MainBattlePartyHudPresenter partyHud; // 좌하단 5인 파티 상태
        private CombatPowerIncreasePresenter combatPowerIncrease; // 총전투력 상승 피드백
        private CombatFeedbackPlayer mainBattleFeedback; // 메인전투 전용 일반 피격 밀림 조절
        private float trackedTotalPower; // 마지막 저장 확정 총전투력

        public SceneId SceneId => sceneId;
        public bool IsInitialized { get; private set; }

        public void Initialize(ISceneContext sceneContext)
        {
            if (IsInitialized)
            {
                return;
            }

            context = sceneContext as MainSceneContext;
            if (context == null)
            {
                throw new ArgumentException("MainSceneContext is required.", nameof(sceneContext));
            }

            if (expedition == null || hostedRunner == null || formationPage == null)
            {
                throw new InvalidOperationException("MainBattle runtime references are missing.");
            }

            party = context.Party;
            if (party == null || party.Units.Length == 0)
            {
                throw new InvalidOperationException("MainBattle party is missing.");
            }
            growthDungeonEntry = GetComponentInChildren<GrowthDungeonStageEntryController>(true);
            castleRaidEntry = GetComponentInChildren<CastleRaidStageSelectionController>(true);
            if (growthDungeonEntry == null)
            {
                foodRiotButton?.onClick.AddListener(OpenFoodRiot); // 신규 컨트롤러 누락 시 기존 진입 보존
                towerButton?.onClick.AddListener(OpenGuardiansTower);
                fallenCommanderButton?.onClick.AddListener(OpenFallenCommander);
                foodRiotSweepButton?.onClick.AddListener(SweepFoodRiot);
                towerSweepButton?.onClick.AddListener(SweepGuardiansTower);
            }

            castleRaidButton?.onClick.AddListener(OpenCastleRaid);
            var runtimeRoot = transform.Find("01_MainGameplayRoot/01_RuntimeRoot");
            var playerFormationAnchor = runtimeRoot?.Find("PlayerFormationAnchor");
            var commander = runtimeRoot?.Find("CommanderVisual");
            var enemySpawnAnchor = runtimeRoot?.Find("EnemySpawnAnchor");
            var formationGround = transform.Find("01_MainGameplayRoot/00_WorldRoot/Ground")?.GetComponent<Collider>();
            if (playerFormationAnchor == null || commander == null || enemySpawnAnchor == null ||
                formationGround == null)
            {
                throw new InvalidOperationException("MainBattle formation frame references are missing.");
            }

            expedition.Initialize(
                context.Progress,
                party,
                context.RewardPresentation,
                formationGround,
                context.ItemCatalog,
                commander,
                context.EquipmentBalanceConfig,
                playerFormationAnchor);
            expedition.CombatWorld?.SetUnitEmissionBrightnessScale(monsterEmissionBrightnessScale);
            expedition.CombatWorld?.SetMonsterVfxBrightnessScale(monsterVfxBrightnessScale);
            mainBattleFeedback = expedition.GetComponentInChildren<CombatFeedbackPlayer>(true);
            mainBattleFeedback?.SetRecoilScale(1f); // 먼 쿼터뷰에서도 평타 반응이 읽히게 유지
            var combatTuning = CombatImpactTuning.ActiveConfig;
            mainBattleFeedback?.ConfigureActualKnockback(
                combatTuning.MainBattleActualKnockbackDistanceMultiplier,
                combatTuning.MainBattleActualKnockbackMaxDistance,
                combatTuning.MainBattleActualKnockbackDurationMultiplier,
                combatTuning.MainBattleLightPostKnockbackStagger,
                combatTuning.MainBattleStandardPostKnockbackStagger,
                combatTuning.MainBattleHeavyPostKnockbackStagger);
            formationPage.PartyChanged += HandlePartyChanged;
            formationPage.OpenStateChanged += HandleFormationPageOpenStateChanged;
            formationPage.PositionFormationRequested += HandlePositionFormationRequested;
            formationPage.Configure(context.Progress, context.MonsterCatalog, context.RefreshParty);
            managementUi = GetComponentInChildren<MainBattleManagementUiController>(true);
            managementUi?.ConfigureFormationPage(formationPage);
            managementUi?.ConfigureMonsterCollectionData(context.Progress);
            ConfigureCommanderSkills(commander);
            growthDungeonEntry?.Configure(
                context.Progress,
                context.ContentLauncher,
                context.GrowthDungeonSweep,
                hostedRunner,
                () =>
                {
                    party = context.RefreshParty();
                    return party;
                },
                TryOpenContent,
                () => managementUi?.CloseAllPages(),
                SetStatus);
            castleRaidEntry?.Configure(
                context.Progress,
                context.ContentLauncher,
                castleRaidContentId,
                EnterCastleRaidStage,
                SetStatus);
            if (managementUi != null)
            {
                managementUi.GrowthDungeonPageOpened += RefreshGrowthDungeonUi;
            }
            RefreshGrowthDungeonUi();
            hudProgressView = GetComponentInChildren<MainBattleHudProgressView>(true);
            hudProgressView?.Configure(context.Progress);
            hudProgressView?.SetCombatPower(party.TotalPower);
            ResolveMonsterManagementPage()?.Configure(context.Progress, context.MonsterCatalog);
            if (monsterManagementPage != null)
            {
                monsterManagementPage.OpenStateChanged += HandleMonsterManagementPageOpenStateChanged;
            }
            ConfigureGachaSystem();
            ConfigureShopPageView();
            ConfigureEquipmentPage();
            ConfigureCommanderGrowthPage();
            ConfigureEquipmentSlotUpgrade();
            ConfigureItemInventory();
            attendancePanel = GetComponentInChildren<AttendancePanelController>(true);
            attendancePanel?.Configure(context.Progress, context.ItemCatalog);
            mailboxPanel = GetComponentInChildren<MailboxPanelController>(true);
            mailboxPanel?.Configure(context.Progress, context.ItemCatalog);
            quickMenu = GetComponentInChildren<HudQuickMenuController>(true);
            quickMenu?.ConfigureNotifications(context.Progress);
            partyHud = GetComponentInChildren<MainBattlePartyHudPresenter>(true);
            partyHud?.Configure(expedition);
            ConfigureMonsterDrag();
            ConfigureSpatialMovement();
            ConfigureFormationPlacement();
            combatPowerIncrease = GetComponentInChildren<CombatPowerIncreasePresenter>(true);
            trackedTotalPower = party.TotalPower;
            context.Progress.Changed += HandleProgressChanged;
            SetStatus("자동 전투");
            IsInitialized = true;
        }

        public void Shutdown()
        {
            if (placementController != null)
            {
                placementController.Completed -= HandlePlacementCompleted;
                placementController.Shutdown();
            }

            spatialController?.Shutdown();
            monsterDrag?.Shutdown();
            dragIndicator?.Shutdown();
            growthDungeonEntry?.Shutdown();
            castleRaidEntry?.Shutdown();
            foodRiotButton?.onClick.RemoveListener(OpenFoodRiot);
            castleRaidButton?.onClick.RemoveListener(OpenCastleRaid);
            towerButton?.onClick.RemoveListener(OpenGuardiansTower); // 08.06 안건준 추가
            fallenCommanderButton?.onClick.RemoveListener(OpenFallenCommander);
            foodRiotSweepButton?.onClick.RemoveListener(SweepFoodRiot);
            towerSweepButton?.onClick.RemoveListener(SweepGuardiansTower);
            foreach (var system in FindObjectsByType<GachaSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (system.gameObject.scene == gameObject.scene) system.Shutdown();
            ResolveShopPageView()?.Shutdown();
            hudProgressView?.Shutdown();
            commanderSkillHud?.Shutdown();
            commanderSkillPage?.Shutdown();
            commanderSkillSummon?.Shutdown();
            commanderSkillRuntime?.Shutdown();
            managementUi?.ConfigureFormationPage(null);
            managementUi?.ConfigureMonsterCollectionData(null);
            managementUi?.ConfigureEquipmentSlotUpgradePage(null);
            managementUi?.ConfigureInventoryPage(null);
            managementUi?.ConfigureCommanderSkillPage(null);
            quickMenu?.ConfigureNotifications(null);
            partyHud?.Configure(null);
            if (context != null)
            {
                context.Progress.Changed -= HandleProgressChanged;
            }
            attendancePanel?.Configure(null, null);
            mailboxPanel?.Configure(null, null);
            if (managementUi != null)
            {
                managementUi.GrowthDungeonPageOpened -= RefreshGrowthDungeonUi;
            }
            ResolveItemInventoryPage()?.Shutdown();
            if (formationPage != null)
            {
                formationPage.PartyChanged -= HandlePartyChanged;
                formationPage.OpenStateChanged -= HandleFormationPageOpenStateChanged;
                formationPage.PositionFormationRequested -= HandlePositionFormationRequested;
                formationPage.Shutdown();
            }

            if (monsterManagementPage != null)
            {
                monsterManagementPage.OpenStateChanged -= HandleMonsterManagementPageOpenStateChanged;
                monsterManagementPage.Shutdown();
            }

            if (hostedRunner != null && hostedRunner.IsOpen)
            {
                hostedRunner.CloseWithoutRestart(); // 씬 종료 중 원정대 재시작 금지
            }

            mainBattleFeedback?.SetRecoilScale(1f);
            mainBattleFeedback?.ConfigureActualKnockback(0f, 0f, 1f);
            mainBattleFeedback = null;
            expedition?.CombatWorld?.SetUnitEmissionBrightnessScale(1f);
            expedition?.CombatWorld?.SetMonsterVfxBrightnessScale(1f);
            expedition?.Shutdown();
            context = null;
            party = null;
            monsterDrag = null;
            dragIndicator = null;
            spatialController = null;
            placementController = null;
            managementUi = null;
            commanderGrowthPage = null;
            hudProgressView = null;
            growthDungeonEntry = null;
            commanderSkillHud = null;
            commanderSkillPage = null;
            commanderSkillRuntime = null;
            commanderSkillSummon = null;
            attendancePanel = null;
            mailboxPanel = null;
            quickMenu = null;
            partyHud = null;
            combatPowerIncrease?.Hide();
            combatPowerIncrease = null;
            trackedTotalPower = 0f;
            IsInitialized = false;
        }

        private void ConfigureCommanderSkills(Transform commander)
        {
            var catalog = Resources.Load<CommanderSkillCatalog>("CommanderSkills/CommanderSkillCatalog");
            var combatWorld = expedition.GetComponentInChildren<CombatWorld>(true);
            commanderSkillHud = GetComponentInChildren<CommanderSkillHudView>(true);
            var catalogError = catalog == null ? "Catalog asset is missing." : string.Empty;
            if (catalog == null || !catalog.TryValidate(out catalogError))
            {
                Debug.LogError($"Commander skill catalog is missing or invalid: {catalogError}", this);
                return;
            }

            commanderSkillSummon = GetComponentInChildren<CommanderSkillSummonController>(true);
            commanderSkillSummon?.Configure(context.Progress, catalog);

            if (combatWorld == null || commanderSkillHud == null)
            {
                Debug.LogWarning("Commander skill combat world or HUD is missing.", this);
                return;
            }

            commanderSkillRuntime = GetComponent<CommanderSkillRuntime>();
            if (commanderSkillRuntime == null)
            {
                commanderSkillRuntime = gameObject.AddComponent<CommanderSkillRuntime>(); // 신규 신 참조를 요구하지 않는 런타임 소유
            }

            var castAnimation = commander.GetComponent<CommanderSkillCastAnimationPresenter>();
            if (castAnimation == null)
                castAnimation = commander.gameObject.AddComponent<CommanderSkillCastAnimationPresenter>();
            castAnimation.Configure(commander.GetComponentInChildren<Animator>(true));

            commanderSkillRuntime.Configure(
                context.Progress,
                catalog,
                combatWorld,
                commander,
                () => managementUi != null && managementUi.IsAnyPageOpen,
                safePullGround: transform.Find("01_MainGameplayRoot/00_WorldRoot/Ground")?.GetComponent<Collider>(),
                animationPresenter: castAnimation);
            commanderSkillHud.Configure(context.Progress, catalog, commanderSkillRuntime);
            commanderSkillPage = GetComponentInChildren<CommanderSkillPageController>(true);
            commanderSkillPage?.Configure(context.Progress, catalog);
            managementUi?.ConfigureCommanderSkillPage(commanderSkillPage);
        }

        // GachaSystem은 비활성 MonsterShop에 두면 프리팹 오버라이드 참조가 Missing으로 깨질 수 있다.
        // 인스펙터 참조가 비어 있어도 같은 오브젝트/씬(비활성 포함)에서 다시 찾아 연결한다.
        private void ConfigureGachaSystem()
        {
            ResolveGachaSystem()?.Configure(context.Progress, context.MonsterCatalog);
            foreach (var system in FindObjectsByType<GachaSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (system != gachaSystem && system.gameObject.scene == gameObject.scene)
                    system.Configure(context.Progress, context.MonsterCatalog);
        }

        private GachaSystem ResolveGachaSystem()
        {
            if (gachaSystem != null)
            {
                return gachaSystem;
            }

            gachaSystem = GetComponent<GachaSystem>();
            if (gachaSystem == null)
            {
                gachaSystem = FindFirstObjectByType<GachaSystem>(FindObjectsInactive.Include);
            }

            return gachaSystem;
        }

        private void ConfigureShopPageView()
        {
            ResolveShopPageView()?.Configure(context.Progress);
        }

        private ShopPageView ResolveShopPageView()
        {
            if (shopPageView != null)
            {
                return shopPageView;
            }

            shopPageView = GetComponentInChildren<ShopPageView>(true);
            if (shopPageView == null)
            {
                shopPageView = FindFirstObjectByType<ShopPageView>(FindObjectsInactive.Include);
            }

            return shopPageView;
        }

        // 08.10 안건준 추가 - GachaSystem과 마찬가지로 인스펙터 참조가 비어 있어도 씬(비활성 포함)에서
        // 다시 찾아 연결한다.
        private void ConfigureEquipmentPage()
        {
            ResolveEquipmentPage()?.Configure(
                context.Progress,
                context.EquipmentBalanceConfig,
                RefreshPartyForNextRun);
        }

        private EquipmentPageController ResolveEquipmentPage()
        {
            if (equipmentPage != null)
            {
                return equipmentPage;
            }

            equipmentPage = GetComponentInChildren<EquipmentPageController>(true);
            if (equipmentPage == null)
            {
                equipmentPage = FindFirstObjectByType<EquipmentPageController>(FindObjectsInactive.Include);
            }

            return equipmentPage;
        }

        private void ConfigureCommanderGrowthPage()
        {
            if (context.CommanderGrowthConfig == null)
            {
                return;
            }

            commanderGrowthPage = GetComponentInChildren<CommanderGrowthPageView>(true);
            commanderGrowthPage?.Configure(
                context.Progress,
                context.CommanderGrowthConfig,
                context.EquipmentBalanceConfig,
                RefreshPartyForNextRun);
            commanderGrowthPage?.SetParty(party);
        }

        // 장비 슬롯 강화 패널을 진행 데이터 및 메인 관리 UI에 연결한다.
        private void ConfigureEquipmentSlotUpgrade()
        {
            var panel = ResolveEquipmentSlotUpgradePanel();
            panel?.Configure(context.Progress, RefreshPartyForNextRun);
            managementUi?.ConfigureEquipmentSlotUpgradePage(panel);
        }

        private EquipmentSlotUpgradePanelController ResolveEquipmentSlotUpgradePanel()
        {
            if (equipmentSlotUpgradePanel != null)
            {
                return equipmentSlotUpgradePanel;
            }

            equipmentSlotUpgradePanel = GetComponentInChildren<EquipmentSlotUpgradePanelController>(true);
            if (equipmentSlotUpgradePanel == null)
            {
                equipmentSlotUpgradePanel = FindFirstObjectByType<EquipmentSlotUpgradePanelController>(FindObjectsInactive.Include);
            }

            return equipmentSlotUpgradePanel;
        }

        private void ConfigureItemInventory()
        {
            var page = ResolveItemInventoryPage();
            page?.Configure(context.Progress, context.ItemCatalog);
            managementUi?.ConfigureInventoryPage(page);
        }

        private ItemInventoryPageController ResolveItemInventoryPage()
        {
            if (itemInventoryPage != null)
            {
                return itemInventoryPage;
            }

            itemInventoryPage = GetComponentInChildren<ItemInventoryPageController>(true);
            if (itemInventoryPage == null)
            {
                itemInventoryPage = FindFirstObjectByType<ItemInventoryPageController>(FindObjectsInactive.Include);
            }

            return itemInventoryPage;
        }

        private MonsterManagementPageController ResolveMonsterManagementPage()
        {
            if (monsterManagementPage != null)
            {
                return monsterManagementPage;
            }

            monsterManagementPage = GetComponentInChildren<MonsterManagementPageController>(true);
            if (monsterManagementPage == null)
            {
                monsterManagementPage = FindFirstObjectByType<MonsterManagementPageController>(
                    FindObjectsInactive.Include);
            }

            return monsterManagementPage;
        }

        private void ConfigureMonsterDrag()
        {
            var worldCamera = transform.Find("01_MainGameplayRoot/02_CameraRoot/MainBattleCamera")?.GetComponent<Camera>();
            var ground = transform.Find("01_MainGameplayRoot/00_WorldRoot/Ground")?.GetComponent<Collider>();
            if (worldCamera == null || ground == null)
            {
                throw new InvalidOperationException("MainBattle monster drag references are missing.");
            }

            monsterDrag = expedition.GetComponent<MainBattleMonsterDragController>();
            if (monsterDrag == null)
            {
                monsterDrag = expedition.gameObject.AddComponent<MainBattleMonsterDragController>();
            }

            dragIndicator = expedition.GetComponent<MainBattleDragIndicatorPresenter>();
            if (dragIndicator == null)
            {
                Debug.LogError("MainBattleRuntimeRoot requires an authored drag indicator presenter.", this);
                return;
            }

            dragIndicator.Configure(worldCamera);
            monsterDrag.Configure(
                worldCamera,
                ground,
                CanDragMonster,
                dragIndicator.ShowPreview,
                dragIndicator.ShowRelease,
                dragIndicator.HideImmediate);
        }

        private void ConfigureSpatialMovement()
        {
            var runtimeRoot = transform.Find("01_MainGameplayRoot/01_RuntimeRoot");
            var ground = transform.Find("01_MainGameplayRoot/00_WorldRoot/Ground")?.GetComponent<Collider>();
            var commander = runtimeRoot?.Find("CommanderVisual");
            var playerFormationAnchor = runtimeRoot?.Find("PlayerFormationAnchor");
            var enemySpawnAnchor = runtimeRoot?.Find("EnemySpawnAnchor");
            if (ground == null || commander == null || playerFormationAnchor == null || enemySpawnAnchor == null)
            {
                throw new InvalidOperationException("MainBattle spatial references are missing.");
            }

            spatialController = expedition.GetComponent<MainBattleSpatialController>();
            if (spatialController == null)
            {
                spatialController = expedition.gameObject.AddComponent<MainBattleSpatialController>();
            }

            spatialController.Configure(expedition, ground, commander, playerFormationAnchor, enemySpawnAnchor);
        }

        private void ConfigureFormationPlacement()
        {
            var gameplayRoot = transform.Find("01_MainGameplayRoot");
            var runtimeRoot = gameplayRoot?.Find("01_RuntimeRoot");
            var worldCamera = gameplayRoot?.Find("02_CameraRoot/MainBattleCamera")?.GetComponent<Camera>();
            var ground = gameplayRoot?.Find("00_WorldRoot/Ground")?.GetComponent<Collider>();
            var playerFormationAnchor = runtimeRoot?.Find("PlayerFormationAnchor");
            var commander = runtimeRoot?.Find("CommanderVisual");
            var enemySpawnAnchor = runtimeRoot?.Find("EnemySpawnAnchor");
            var uiRoot = gameplayRoot?.Find("04_UIRoot");
            var hudRoot = uiRoot?.Find("MainBattleHUD")?.gameObject;
            if (worldCamera == null || ground == null || playerFormationAnchor == null || commander == null ||
                enemySpawnAnchor == null || uiRoot == null || hudRoot == null)
            {
                throw new InvalidOperationException("MainBattle formation placement references are missing.");
            }

            placementController = expedition.GetComponent<MainBattleFormationPlacementController>();
            if (placementController == null)
            {
                throw new InvalidOperationException("MainBattle needs its authored formation placement controller.");
            }

            placementController.Completed += HandlePlacementCompleted;
            placementController.Configure(
                context.Progress,
                expedition,
                monsterDrag,
                worldCamera,
                ground,
                playerFormationAnchor,
                commander,
                uiRoot,
                hudRoot);
        }

        private bool CanDragMonster()
        {
            return IsInitialized && context != null && expedition != null && expedition.IsRunning &&
                   (placementController == null || !placementController.IsActive) &&
                   !context.ContentLauncher.IsRunning && (formationPage == null || !formationPage.IsOpen) &&
                   (monsterManagementPage == null || !monsterManagementPage.IsOpen) &&
                   (managementUi == null || !managementUi.IsAnyPageOpen);
        }

        private void OpenFoodRiot()
        {
            managementUi?.CloseAllPages(); // 카드 Page를 닫은 뒤 입장 가능 상태 검사
            if (!TryOpenContent())
            {
                return;
            }

            party = context.RefreshParty();
            if (context.ContentLauncher.TryGetGrowthDungeonState(foodRiotContentId, out var state) &&
                context.ContentLauncher.StartHosted(
                    foodRiotContentId,
                    party,
                    hostedRunner,
                    ContentRunMode.Challenge,
                    state.NextChallengeStage))
            {
                SetStatus($"식량 대소동 · {state.NextChallengeStage}단계 도전");
            }
        }

        // 08.06 안건준 추가 - 수호자의 탑 입장. 식량 대소동(OpenFoodRiot)과 동일한 방식이지만
        // 콘텐츠 ID·던전 Instance가 완전히 분리되어 있어 서로 겹치지 않는다.
        private void OpenGuardiansTower()
        {
            managementUi?.CloseAllPages();
            if (!TryOpenContent())
            {
                return;
            }

            party = context.RefreshParty();
            if (context.ContentLauncher.TryGetGrowthDungeonState(guardiansTowerContentId, out var state) &&
                context.ContentLauncher.StartHosted(
                    guardiansTowerContentId,
                    party,
                    hostedRunner,
                    ContentRunMode.Challenge,
                    state.NextChallengeStage))
            {
                SetStatus($"고대 수호수의 시련 · {state.NextChallengeStage}단계 도전");
            }
        }

        private void OpenFallenCommander()
        {
            managementUi?.CloseAllPages();
            if (!TryOpenContent())
            {
                return;
            }

            party = context.RefreshParty();
            if (context.ContentLauncher.StartHosted(fallenCommanderContentId, party, hostedRunner))
            {
                SetStatus("타락한 과거의 군단장");
            }
        }

        private async void SweepFoodRiot()
        {
            await SweepGrowthDungeonAsync(foodRiotContentId, "식량 대소동");
        }

        private async void SweepGuardiansTower()
        {
            await SweepGrowthDungeonAsync(guardiansTowerContentId, "고대 수호수의 시련");
        }

        private async System.Threading.Tasks.Task SweepGrowthDungeonAsync(ContentId contentId, string displayName)
        {
            if (!IsInitialized || context?.GrowthDungeonSweep == null ||
                context.ContentLauncher.IsRunning || context.GrowthDungeonSweep.IsBusy)
            {
                return;
            }

            SetStatus($"{displayName} 소탕 정산 중...");
            var saved = await context.GrowthDungeonSweep.TrySweepAsync(contentId);
            if (!IsInitialized || context == null)
            {
                return;
            }

            RefreshGrowthDungeonKeyUi();
            SetStatus(saved ? $"{displayName} 소탕 완료" : "클리어 기록 또는 열쇠를 확인하세요");
        }

        public void OpenCastleRaid()
        {
            managementUi?.CloseAllPages();
            if (!TryOpenContent())
            {
                return;
            }

            if (castleRaidEntry == null)
            {
                SetStatus("군단의 역습 단계 선택 UI를 불러오지 못했습니다");
                return;
            }

            castleRaidEntry.Open();
            SetStatus("군단의 역습 · 공략할 스테이지를 선택하세요");
        }

        private bool EnterCastleRaidStage(int stage)
        {
            if (!TryOpenContent())
            {
                return false;
            }

            party = context.RefreshParty();
            expedition.StopWithoutResult(); // 별도 씬 이동 전 무정산 종료
            if (context.ContentLauncher.StartSeparate(castleRaidContentId, party, stage))
            {
                SetStatus($"군단의 역습 · STAGE {stage:000} · 난이도 {CastleRaidStageRules.ResolveDifficulty(stage)}");
                _ = QuestRuntime.AdvanceAllOfConditionAsync(QuestConditionType.CastleRaidEnter, 1L);
                return true;
            }

            expedition.StartFromSavedMode(); // 입장 실패 시 메인 복구
            SetStatus("입장 상태가 변경되었습니다 · 스테이지를 다시 확인해 주세요");
            return false;
        }

        private bool TryOpenContent()
        {
            if (CanOpenContent())
            {
                return true;
            }

            if (IsInitialized && expedition != null && expedition.IsSettling)
            {
                SetStatus("전투 결과 정산 중입니다. 잠시 후 다시 시도하세요.");
            }
            else if (IsInitialized && formationPage != null && formationPage.IsOpen)
            {
                SetStatus("편성 화면을 닫은 뒤 콘텐츠에 입장하세요.");
            }
            else if (IsInitialized && monsterManagementPage != null && monsterManagementPage.IsOpen)
            {
                SetStatus("몬스터 관리 화면을 닫은 뒤 콘텐츠에 입장하세요.");
            }

            return false;
        }

        private bool CanOpenContent()
        {
            return IsInitialized && context != null && party != null &&
                   !context.ContentLauncher.IsRunning &&
                   (context.GrowthDungeonSweep == null || !context.GrowthDungeonSweep.IsBusy) &&
                   !expedition.IsSettling &&
                   (placementController == null || !placementController.IsActive) &&
                   (formationPage == null || !formationPage.IsOpen) &&
                   (monsterManagementPage == null || !monsterManagementPage.IsOpen); // 관리·콘텐츠 중복 입력 금지
        }

        private void HandleFormationPageOpenStateChanged(bool open)
        {
            if (open)
            {
                monsterManagementPage?.ClosePage();
            }
        }

        private void HandleMonsterManagementPageOpenStateChanged(bool open)
        {
            if (open)
            {
                formationPage?.ClosePage();
            }
        }

        private void HandlePositionFormationRequested()
        {
            if (!IsInitialized || context == null || expedition == null || placementController == null ||
                !expedition.IsRunning || expedition.IsSettling || context.ContentLauncher.IsRunning)
            {
                SetStatus("현재는 위치 편성을 시작할 수 없습니다");
                return;
            }

            formationPage?.ClosePage();
            monsterManagementPage?.ClosePage();
            managementUi?.CloseAllPages();
            spatialController?.ResetToStart();
            if (!placementController.Begin())
            {
                if (!expedition.IsRunning)
                {
                    expedition.StartFromSavedMode();
                }

                ConfigureMonsterDrag();
                SetStatus("위치 편성을 시작하지 못했습니다");
            }
        }

        private void HandlePlacementCompleted()
        {
            if (!IsInitialized || expedition == null)
            {
                return;
            }

            expedition.EndFormationPlacement();
            ConfigureMonsterDrag();
            expedition.StartFromSavedMode();
            SetStatus("자동 전투");
        }

        private void HandlePartyChanged(BattlePartySnapshot updatedParty)
        {
            if (updatedParty == null || updatedParty.Units.Length == 0)
            {
                return;
            }

            party = updatedParty;
            trackedTotalPower = updatedParty.TotalPower;
            hudProgressView?.SetCombatPower(trackedTotalPower);
            expedition.SetPartyForNextRun(updatedParty); // 현재 소환 유닛은 유지
            commanderGrowthPage?.SetParty(updatedParty);
            SetStatus("편성 저장 완료 · 다음 전투부터 적용");
        }

        private void HandleProgressChanged()
        {
            if (!IsInitialized || context == null)
            {
                return;
            }

            var updatedParty = context.RefreshParty();
            if (updatedParty == null || updatedParty.Units.Length == 0)
            {
                return;
            }

            var previousPower = trackedTotalPower;
            var currentPower = updatedParty.TotalPower;
            trackedTotalPower = currentPower;
            hudProgressView?.SetCombatPower(currentPower);
            party = updatedParty;
            expedition.SetPartyForNextRun(updatedParty);
            commanderGrowthPage?.SetParty(updatedParty);
            if (currentPower > previousPower + 0.5f)
            {
                combatPowerIncrease?.ShowIncrease(previousPower, currentPower);
            }
        }

        private void RefreshPartyForNextRun()
        {
            if (context != null)
            {
                HandlePartyChanged(context.RefreshParty());
            }
        }

        private void RefreshGrowthDungeonKeyUi()
        {
            if (context == null)
            {
                return;
            }

            var items = context.Progress.View.Items;
            SetKeyText(foodRiotKeyText, items, ItemIds.FoodRiotKey);
            SetKeyText(treasureSpiritKeyText, items, ItemIds.TreasureSpiritKey);
            SetKeyText(fallenCommanderKeyText, items, ItemIds.FallenCommanderKey);
            SetKeyText(towerKeyText, items, ItemIds.GuardiansTowerKey);

            var sweepBusy = context.GrowthDungeonSweep != null && context.GrowthDungeonSweep.IsBusy;
            if (foodRiotSweepButton != null)
            {
                foodRiotSweepButton.interactable = !sweepBusy &&
                    context.ContentLauncher.TryGetGrowthDungeonState(foodRiotContentId, out var foodState) &&
                    foodState.CanSweep;
            }

            if (towerSweepButton != null)
            {
                towerSweepButton.interactable = !sweepBusy &&
                    context.ContentLauncher.TryGetGrowthDungeonState(guardiansTowerContentId, out var towerState) &&
                    towerState.CanSweep;
            }
        }

        private void RefreshGrowthDungeonUi()
        {
            RefreshGrowthDungeonKeyUi();
            growthDungeonEntry?.Refresh();
        }

        private static void SetKeyText(TMP_Text target, ItemInventoryView items, string itemId)
        {
            if (target == null)
            {
                return;
            }

            items.TryGetQuantity(itemId, out var quantity);
            target.text = $"{Math.Min(quantity, GrowthDungeonDailyKeyRules.MaximumQuantity)} / {GrowthDungeonDailyKeyRules.MaximumQuantity}";
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            ExpeditionController expeditionController,
            MainBattleHostedContentRunner runner,
            Button foodButton,
            Button castleButton,
            TMP_Text status,
            FormationPageController formationController = null,
            GachaSystem gacha = null,
            MonsterManagementPageController managementController = null,
            ShopPageView shopView = null,
            Button guardiansTowerButton = null,
            Button fallenCommanderEntryButton = null)
        {
            expedition = expeditionController;
            hostedRunner = runner;
            foodRiotButton = foodButton;
            castleRaidButton = castleButton;
            statusText = status;
            formationPage = formationController;
            gachaSystem = gacha;
            monsterManagementPage = managementController;
            shopPageView = shopView;
            towerButton = guardiansTowerButton; // 08.06 안건준 추가
            fallenCommanderButton = fallenCommanderEntryButton;
        }

        public void EditorConfigureGrowthDungeonSettlementUi(
            Button foodSweep,
            Button guardiansSweep,
            TMP_Text foodKey,
            TMP_Text treasureKey,
            TMP_Text fallenCommanderKey,
            TMP_Text guardiansKey)
        {
            foodRiotSweepButton = foodSweep;
            towerSweepButton = guardiansSweep;
            foodRiotKeyText = foodKey;
            treasureSpiritKeyText = treasureKey;
            fallenCommanderKeyText = fallenCommanderKey;
            towerKeyText = guardiansKey;
        }
#endif
    }
}
