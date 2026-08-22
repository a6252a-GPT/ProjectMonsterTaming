using ProjectMT.Features.Formation;
using ProjectMT.Features.Quest;
using ProjectMT.Features.Settings;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class HudQuickMenuController : MonoBehaviour // 고정 바로가기와 확장 메뉴 제어
    {
        [Header("고정 바로가기")]
        [SerializeField] private Button contentButton;
        [SerializeField] private Button summonButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button attendanceButton;
        [SerializeField] private Button mailboxButton;
        [SerializeField] private GameObject attendanceBadge;
        [SerializeField] private GameObject mailboxBadge;
        [SerializeField] private Button menuButton;
        [SerializeField] private GameObject menuIcon;
        [SerializeField] private GameObject closeIcon;
        [SerializeField] private TMP_Text menuLabelText;

        [Header("확장 메뉴")]
        [SerializeField] private GameObject outsideTapRoot;
        [SerializeField] private GameObject expandedRoot;
        [SerializeField] private Button monsterGrowthButton;
        [SerializeField] private Button formationButton;
        [SerializeField] private Button equipmentButton;
        [SerializeField] private Button inventoryButton;
        [SerializeField] private Button equipmentSlotUpgradeButton;
        [SerializeField] private Button commanderButton;
        [SerializeField] private Button skillButton;
        [SerializeField] private Button missionButton;
        [SerializeField] private Button castleRaidButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button modeButton;

        [Header("실제 기능")]
        [SerializeField] private MainBattleManagementUiController managementUi;
        [SerializeField] private FormationPageController formationPage;
        [SerializeField] private ShopCategoryMenu shopCategoryMenu;
        [SerializeField] private MainBattleSceneRoot sceneRoot;
        [SerializeField] private DailyMissionPanelView questPanel;

        private IGameProgressService progress;

        public bool IsOpen => expandedRoot != null && expandedRoot.activeSelf;

        private void Awake()
        {
            ResolveRuntimeReferences();
            if (managementUi != null)
            {
                managementUi.AnyPageOpenChanged += HandleAnyPageOpenChanged;
            }
            contentButton?.onClick.AddListener(OpenContent);
            summonButton?.onClick.AddListener(OpenSummon);
            shopButton?.onClick.AddListener(OpenShop);
            attendanceButton?.onClick.AddListener(OpenAttendance);
            mailboxButton?.onClick.AddListener(OpenMailbox);
            menuButton?.onClick.AddListener(ToggleMenu);
            monsterGrowthButton?.onClick.AddListener(OpenMonsterGrowth);
            formationButton?.onClick.AddListener(OpenFormation);
            equipmentButton?.onClick.AddListener(OpenEquipment);
            inventoryButton?.onClick.AddListener(OpenInventory);
            equipmentSlotUpgradeButton?.onClick.AddListener(OpenEquipmentSlotUpgrade);
            commanderButton?.onClick.AddListener(OpenCommander);
            skillButton?.onClick.AddListener(OpenCommanderSkill);
            missionButton?.onClick.AddListener(OpenMission);
            castleRaidButton?.onClick.AddListener(OpenCastleRaid);
            settingsButton?.onClick.AddListener(OpenSettings);
            modeButton?.onClick.AddListener(CloseMenu);
            SetMenuOpen(false);

            UIButtonClickPunch.EnsureOn(contentButton?.gameObject);
            UIButtonClickPunch.EnsureOn(summonButton?.gameObject);
            UIButtonClickPunch.EnsureOn(shopButton?.gameObject);
            UIButtonClickPunch.EnsureOn(attendanceButton?.gameObject);
            UIButtonClickPunch.EnsureOn(mailboxButton?.gameObject);
            UIButtonClickPunch.EnsureOn(menuButton?.gameObject);
        }

        private void OnDestroy()
        {
            if (managementUi != null)
            {
                managementUi.AnyPageOpenChanged -= HandleAnyPageOpenChanged;
            }
            contentButton?.onClick.RemoveListener(OpenContent);
            summonButton?.onClick.RemoveListener(OpenSummon);
            shopButton?.onClick.RemoveListener(OpenShop);
            attendanceButton?.onClick.RemoveListener(OpenAttendance);
            mailboxButton?.onClick.RemoveListener(OpenMailbox);
            menuButton?.onClick.RemoveListener(ToggleMenu);
            monsterGrowthButton?.onClick.RemoveListener(OpenMonsterGrowth);
            formationButton?.onClick.RemoveListener(OpenFormation);
            equipmentButton?.onClick.RemoveListener(OpenEquipment);
            inventoryButton?.onClick.RemoveListener(OpenInventory);
            equipmentSlotUpgradeButton?.onClick.RemoveListener(OpenEquipmentSlotUpgrade);
            commanderButton?.onClick.RemoveListener(OpenCommander);
            skillButton?.onClick.RemoveListener(OpenCommanderSkill);
            missionButton?.onClick.RemoveListener(OpenMission);
            castleRaidButton?.onClick.RemoveListener(OpenCastleRaid);
            settingsButton?.onClick.RemoveListener(OpenSettings);
            modeButton?.onClick.RemoveListener(CloseMenu);
            ConfigureNotifications(null);
        }

        public void CloseMenu()
        {
            SetMenuOpen(false);
        }

        // 상점 등 다른 페이지가 열려 있는 동안에는 고정 바로가기 버튼이 그 뒤에서 눌리지 않도록 막는다.
        private void HandleAnyPageOpenChanged(bool anyPageOpen)
        {
            var interactable = !anyPageOpen;
            SetButtonInteractable(contentButton, interactable);
            SetButtonInteractable(summonButton, interactable);
            SetButtonInteractable(shopButton, interactable);
            SetButtonInteractable(attendanceButton, interactable);
            SetButtonInteractable(mailboxButton, interactable);
            SetButtonInteractable(menuButton, interactable);
            if (anyPageOpen)
            {
                CloseMenu();
            }
        }

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private void ToggleMenu()
        {
            SetMenuOpen(!IsOpen);
        }

        private void SetMenuOpen(bool open)
        {
            outsideTapRoot?.SetActive(open);
            if (open)
            {
                UIPanelPopAnimator.RequestOpen(expandedRoot);
            }
            else
            {
                UIPanelPopAnimator.RequestClose(expandedRoot);
            }

            menuIcon?.SetActive(!open);
            closeIcon?.SetActive(open);
            if (menuLabelText != null)
            {
                menuLabelText.text = open ? "닫기" : "메뉴";
            }
        }

        private void OpenContent()
        {
            CloseMenu();
            managementUi?.OpenGrowthDungeonPage();
        }

        private void OpenSummon()
        {
            CloseMenu();
            managementUi?.OpenShopPage();
            shopCategoryMenu?.ShowMonsterShop();
        }

        private void OpenShop()
        {
            CloseMenu();
            managementUi?.OpenShopPage();
            shopCategoryMenu?.ShowDiamondShop();
        }

        private void OpenMonsterGrowth()
        {
            CloseMenu();
            managementUi?.OpenMonsterManagementPage();
        }

        private void OpenFormation()
        {
            CloseMenu();
            managementUi?.CloseAllPages();
            formationPage?.OpenPage();
        }

        private void OpenEquipment()
        {
            CloseMenu();
            managementUi?.OpenEquipmentPage();
        }

        private void OpenInventory()
        {
            CloseMenu();
            managementUi?.OpenInventoryPage();
        }

        private void OpenEquipmentSlotUpgrade()
        {
            CloseMenu();
            managementUi?.OpenEquipmentSlotUpgradePage();
        }

        private void OpenCommander()
        {
            CloseMenu();
            managementUi?.OpenCommanderGrowthPage();
        }

        private void OpenCommanderSkill()
        {
            CloseMenu();
            managementUi?.OpenCommanderSkillPage();
        }

        private void OpenMission()
        {
            CloseMenu();
            formationPage?.ClosePage();
            PrepareRuntimeQuestPanel();
            if (questPanel != null)
            {
                if (managementUi != null)
                {
                    managementUi.OpenQuestPage();
                }
                else
                {
                    questPanel.Open();
                }
            }
            else
            {
                Debug.LogWarning("[Quest][UI] 정식 퀘스트 패널을 찾지 못했습니다.", this);
            }
        }

        private void OpenCastleRaid()
        {
            CloseMenu();
            sceneRoot?.OpenCastleRaid();
        }

        private void OpenAttendance()
        {
            CloseMenu();
            managementUi?.OpenAttendancePage();
        }

        private void OpenMailbox()
        {
            CloseMenu();
            managementUi?.OpenMailboxPage();
        }

        private void OpenSettings()
        {
            CloseMenu();
            managementUi?.OpenSettingsPage();
        }

        private void ResolveRuntimeReferences()
        {
            managementUi ??= FindFirstObjectByType<MainBattleManagementUiController>(FindObjectsInactive.Include);
            formationPage ??= FindFirstObjectByType<FormationPageController>(FindObjectsInactive.Include);
            shopCategoryMenu ??= FindFirstObjectByType<ShopCategoryMenu>(FindObjectsInactive.Include);
            sceneRoot ??= FindFirstObjectByType<MainBattleSceneRoot>(FindObjectsInactive.Include);
            questPanel ??= FindFirstObjectByType<DailyMissionPanelView>(FindObjectsInactive.Include);
            PrepareRuntimeQuestPanel();
        }

        private void PrepareRuntimeQuestPanel()
        {
            if (managementUi == null || questPanel == null)
            {
                return;
            }

            if (!questPanel.transform.IsChildOf(managementUi.transform))
            {
                questPanel = QuestPanelRuntimeFactory.Create(questPanel, managementUi.transform);
            }

            managementUi.ConfigureQuestPage(questPanel);
        }

        public void ConfigureNotifications(IGameProgressService progressService)
        {
            if (progress != null)
            {
                progress.Changed -= RefreshBadges;
            }

            progress = progressService;
            if (progress != null)
            {
                progress.Changed += RefreshBadges;
            }

            RefreshBadges();
        }

        private void RefreshBadges()
        {
            attendanceBadge?.SetActive(progress != null && progress.View.Attendance.HasPendingReward);
            mailboxBadge?.SetActive(progress != null && progress.View.Mail.Count > 0);
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Button openContentButton,
            Button openSummonButton,
            Button openShopButton,
            Button toggleMenuButton,
            GameObject closedMenuIcon,
            GameObject openedMenuIcon,
            TMP_Text menuLabel,
            GameObject outsideRoot,
            GameObject menuRoot,
            Button openMonsterGrowthButton,
            Button openFormationButton,
            Button openEquipmentButton,
            Button openInventoryButton,
            Button openEquipmentSlotUpgradeButton,
            Button openCommanderButton,
            Button openSkillButton,
            Button openCastleRaidButton,
            Button currentModeButton)
        {
            contentButton = openContentButton;
            summonButton = openSummonButton;
            shopButton = openShopButton;
            menuButton = toggleMenuButton;
            menuIcon = closedMenuIcon;
            closeIcon = openedMenuIcon;
            menuLabelText = menuLabel;
            outsideTapRoot = outsideRoot;
            expandedRoot = menuRoot;
            monsterGrowthButton = openMonsterGrowthButton;
            formationButton = openFormationButton;
            equipmentButton = openEquipmentButton;
            inventoryButton = openInventoryButton;
            equipmentSlotUpgradeButton = openEquipmentSlotUpgradeButton;
            commanderButton = openCommanderButton;
            skillButton = openSkillButton;
            castleRaidButton = openCastleRaidButton;
            modeButton = currentModeButton;
        }

        public void EditorConfigureSettings(Button openSettingsButton)
        {
            settingsButton = openSettingsButton;
        }

        public void EditorConfigureQuest(Button openMissionButton, DailyMissionPanelView panel)
        {
            missionButton = openMissionButton;
            questPanel = panel;
        }

        public void EditorConfigureRewards(
            Button openAttendanceButton,
            Button openMailboxButton,
            GameObject attendanceNotification,
            GameObject mailboxNotification)
        {
            attendanceButton = openAttendanceButton;
            mailboxButton = openMailboxButton;
            attendanceBadge = attendanceNotification;
            mailboxBadge = mailboxNotification;
        }
#endif
    }
}
