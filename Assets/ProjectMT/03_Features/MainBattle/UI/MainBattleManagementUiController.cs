using System;
using ProjectMT.Features.Attendance;
using ProjectMT.Features.CommanderSkill;
using ProjectMT.Features.Equipment;
using ProjectMT.Features.Formation;
using ProjectMT.Features.Inventory;
using ProjectMT.Features.Mailbox;
using ProjectMT.Features.Quest;
using ProjectMT.Features.Settings;
using ProjectMT.Shared.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattleManagementUiController : MonoBehaviour // 메인 HUD 버튼과 관리 Page 연결
    {
        [Header("메인 HUD 버튼")]
        [SerializeField] private Button commanderGrowthButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button monsterManagementButton;
        [SerializeField] private Button equipmentButton;
        [SerializeField] private Button growthDungeonButton;

        [Header("관리 Page")]
        [SerializeField] private GameObject commanderGrowthPage;
        [SerializeField] private GameObject shopPage;
        [SerializeField] private Button shopCloseButton;
        [SerializeField] private MonsterManagementPageController monsterManagementPage;
        [SerializeField] private GameObject equipmentPage;
        [SerializeField] private Button equipmentCloseButton;
        [SerializeField] private EquipmentSlotUpgradePanelController equipmentSlotUpgradePage;
        [SerializeField] private ItemInventoryPageController inventoryPage;
        [SerializeField] private CommanderSkillPageController commanderSkillPage;
        [SerializeField] private GameObject growthDungeonPage;
        [SerializeField] private Button growthDungeonCloseButton;
        [SerializeField] private SettingsPanelController settingsPage;
        [SerializeField] private AttendancePanelController attendancePage;
        [SerializeField] private MailboxPanelController mailboxPage;

        private DailyMissionPanelView questPage;

        private int defaultSiblingIndex = -1;
        private int defaultCanvasSortingOrder;
        private FormationPageController formationPage;
        private Canvas hudCanvas;
        private bool combatDisplaySuppressed;

        public event Action GrowthDungeonPageOpened;

        public bool IsAnyPageOpen =>
            (commanderGrowthPage != null && commanderGrowthPage.activeSelf) ||
            (shopPage != null && shopPage.activeSelf) ||
            (monsterManagementPage != null && monsterManagementPage.IsOpen) ||
            (equipmentPage != null && equipmentPage.activeSelf) ||
            (equipmentSlotUpgradePage != null && equipmentSlotUpgradePage.IsOpen) ||
            (inventoryPage != null && inventoryPage.IsOpen) ||
            (commanderSkillPage != null && commanderSkillPage.IsOpen) ||
            (growthDungeonPage != null && growthDungeonPage.activeSelf) ||
            (settingsPage != null && settingsPage.IsOpen) ||
            (attendancePage != null && attendancePage.IsOpen) ||
            (mailboxPage != null && mailboxPage.IsOpen) ||
            (questPage != null && questPage.IsOpen) ||
            (formationPage != null && formationPage.IsOpen);

        private void Awake()
        {
            if (commanderGrowthButton == null || shopButton == null ||
                commanderGrowthPage == null || shopPage == null || shopCloseButton == null ||
                equipmentButton == null || equipmentPage == null || equipmentCloseButton == null ||
                growthDungeonButton == null || growthDungeonPage == null || growthDungeonCloseButton == null)
            {
                Debug.LogError("MainBattleManagementUiController: UI 참조가 비어 있습니다.", this);
                return;
            }

            defaultSiblingIndex = transform.GetSiblingIndex();
            hudCanvas = GetComponentInParent<Canvas>();
            defaultCanvasSortingOrder = hudCanvas != null ? hudCanvas.sortingOrder : 0;
            CloseAllPages();
            if (settingsPage != null)
            {
                settingsPage.OpenStateChanged += HandleSettingsOpenStateChanged;
            }
            if (attendancePage != null)
            {
                attendancePage.OpenStateChanged += HandleAttendanceOpenStateChanged;
            }
            if (mailboxPage != null)
            {
                mailboxPage.OpenStateChanged += HandleMailboxOpenStateChanged;
            }

            commanderGrowthButton.onClick.AddListener(ToggleCommanderGrowthPage);
            shopButton.onClick.AddListener(OpenShopPage);
            shopCloseButton.onClick.AddListener(CloseShopPage);
            equipmentButton.onClick.AddListener(ToggleEquipmentPage);
            equipmentCloseButton.onClick.AddListener(CloseEquipmentPage);
            growthDungeonButton.onClick.AddListener(ToggleGrowthDungeonPage);
            growthDungeonCloseButton.onClick.AddListener(CloseGrowthDungeonPage);
            if (monsterManagementButton != null && monsterManagementPage != null)
            {
                monsterManagementButton.onClick.AddListener(OpenMonsterManagementPage);
                monsterManagementPage.OpenStateChanged += HandleMonsterManagementOpenStateChanged;
            }
        }

        private void OnDestroy()
        {
            SetCombatDisplaySuppressed(false);
            RestoreCanvasOrder();
            commanderGrowthButton?.onClick.RemoveListener(ToggleCommanderGrowthPage);
            shopButton?.onClick.RemoveListener(OpenShopPage);
            shopCloseButton?.onClick.RemoveListener(CloseShopPage);
            monsterManagementButton?.onClick.RemoveListener(OpenMonsterManagementPage);
            equipmentButton?.onClick.RemoveListener(ToggleEquipmentPage);
            equipmentCloseButton?.onClick.RemoveListener(CloseEquipmentPage);
            growthDungeonButton?.onClick.RemoveListener(ToggleGrowthDungeonPage);
            growthDungeonCloseButton?.onClick.RemoveListener(CloseGrowthDungeonPage);
            ConfigureEquipmentSlotUpgradePage(null);
            ConfigureInventoryPage(null);
            ConfigureCommanderSkillPage(null);
            ConfigureSettingsPage(null);
            ConfigureAttendancePage(null);
            ConfigureMailboxPage(null);
            ConfigureQuestPage(null);
            if (monsterManagementPage != null)
            {
                monsterManagementPage.OpenStateChanged -= HandleMonsterManagementOpenStateChanged;
            }

            ConfigureFormationPage(null);
        }

        private void OnDisable()
        {
            SetCombatDisplaySuppressed(false);
            RestoreCanvasOrder();
        }

        private void LateUpdate()
        {
            var shouldSuppress = IsAnyPageOpen;
            if (combatDisplaySuppressed != shouldSuppress)
            {
                SetCombatDisplaySuppressed(shouldSuppress);
            }
        }

        public void CloseAllPages()
        {
            formationPage?.ClosePage();
            CloseManagementPages();
        }

        public void OpenCommanderGrowthPage()
        {
            CloseAllPages();
            commanderGrowthPage?.SetActive(true);
            BringToFront();
        }

        public void OpenEquipmentPage()
        {
            CloseAllPages();
            equipmentPage?.SetActive(true);
            BringToFront();
        }

        public void OpenEquipmentSlotUpgradePage()
        {
            CloseAllPages();
            equipmentSlotUpgradePage?.Open();
        }

        public void ConfigureEquipmentSlotUpgradePage(EquipmentSlotUpgradePanelController page)
        {
            if (equipmentSlotUpgradePage == page)
            {
                equipmentSlotUpgradePage?.Close();
                return;
            }

            if (equipmentSlotUpgradePage != null)
            {
                equipmentSlotUpgradePage.OpenStateChanged -= HandleEquipmentSlotUpgradeOpenStateChanged;
            }

            equipmentSlotUpgradePage = page;
            if (equipmentSlotUpgradePage != null)
            {
                equipmentSlotUpgradePage.OpenStateChanged += HandleEquipmentSlotUpgradeOpenStateChanged;
                equipmentSlotUpgradePage.Close();
            }
        }

        public void OpenInventoryPage()
        {
            CloseAllPages();
            inventoryPage?.Open();
        }

        public void OpenCommanderSkillPage()
        {
            CloseAllPages();
            commanderSkillPage?.Open();
        }

        public void ConfigureCommanderSkillPage(CommanderSkillPageController page)
        {
            if (commanderSkillPage == page)
            {
                commanderSkillPage?.Close();
                return;
            }

            if (commanderSkillPage != null)
            {
                commanderSkillPage.OpenStateChanged -= HandleCommanderSkillOpenStateChanged;
            }

            commanderSkillPage = page;
            if (commanderSkillPage != null)
            {
                commanderSkillPage.OpenStateChanged += HandleCommanderSkillOpenStateChanged;
                commanderSkillPage.Close();
            }
        }

        public void ConfigureInventoryPage(ItemInventoryPageController page)
        {
            if (inventoryPage == page)
            {
                inventoryPage?.Close();
                return;
            }

            if (inventoryPage != null)
            {
                inventoryPage.OpenStateChanged -= HandleInventoryOpenStateChanged;
            }

            inventoryPage = page;
            if (inventoryPage != null)
            {
                inventoryPage.OpenStateChanged += HandleInventoryOpenStateChanged;
                inventoryPage.Close();
            }
        }

        public void OpenGrowthDungeonPage()
        {
            CloseAllPages();
            growthDungeonPage?.SetActive(true);
            GrowthDungeonPageOpened?.Invoke();
            BringToFront();
        }

        public void OpenSettingsPage()
        {
            CloseAllPages();
            settingsPage?.Open();
        }

        public void OpenAttendancePage()
        {
            CloseAllPages();
            attendancePage?.Open();
        }

        public void OpenMailboxPage()
        {
            CloseAllPages();
            mailboxPage?.Open();
        }

        public void OpenQuestPage()
        {
            CloseAllPages();
            questPage?.Open();
            if (questPage != null)
            {
                BringToFront();
            }
        }

        public void ConfigureSettingsPage(SettingsPanelController page)
        {
            if (settingsPage == page)
            {
                settingsPage?.Close();
                return;
            }

            if (settingsPage != null)
            {
                settingsPage.OpenStateChanged -= HandleSettingsOpenStateChanged;
            }

            settingsPage = page;
            if (settingsPage != null)
            {
                settingsPage.OpenStateChanged += HandleSettingsOpenStateChanged;
                settingsPage.Close();
            }
        }

        public void ConfigureAttendancePage(AttendancePanelController page)
        {
            if (attendancePage == page)
            {
                attendancePage?.Close();
                return;
            }

            if (attendancePage != null)
            {
                attendancePage.OpenStateChanged -= HandleAttendanceOpenStateChanged;
            }

            attendancePage = page;
            if (attendancePage != null)
            {
                attendancePage.OpenStateChanged += HandleAttendanceOpenStateChanged;
                attendancePage.Close();
            }
        }

        public void ConfigureMailboxPage(MailboxPanelController page)
        {
            if (mailboxPage == page)
            {
                mailboxPage?.Close();
                return;
            }

            if (mailboxPage != null)
            {
                mailboxPage.OpenStateChanged -= HandleMailboxOpenStateChanged;
            }

            mailboxPage = page;
            if (mailboxPage != null)
            {
                mailboxPage.OpenStateChanged += HandleMailboxOpenStateChanged;
                mailboxPage.Close();
            }
        }

        public void ConfigureQuestPage(DailyMissionPanelView page)
        {
            if (questPage == page)
            {
                return;
            }

            if (questPage != null)
            {
                questPage.OpenStateChanged -= HandleQuestPageOpenStateChanged;
            }

            questPage = page;
            if (questPage != null)
            {
                questPage.OpenStateChanged += HandleQuestPageOpenStateChanged;
                questPage.Close();
            }
        }

        public void ConfigureFormationPage(FormationPageController page)
        {
            if (formationPage == page)
            {
                return;
            }

            if (formationPage != null)
            {
                formationPage.OpenStateChanged -= HandleFormationPageOpenStateChanged;
            }

            formationPage = page;
            if (formationPage != null)
            {
                formationPage.OpenStateChanged += HandleFormationPageOpenStateChanged;
                if (formationPage.IsOpen)
                {
                    CloseManagementPages();
                }
            }
        }

        private void CloseManagementPages()
        {
            commanderGrowthPage?.SetActive(false);
            shopPage?.SetActive(false);
            monsterManagementPage?.ClosePage();
            equipmentPage?.SetActive(false);
            equipmentSlotUpgradePage?.Close();
            inventoryPage?.Close();
            commanderSkillPage?.Close();
            growthDungeonPage?.SetActive(false);
            settingsPage?.Close();
            attendancePage?.Close();
            mailboxPage?.Close();
            questPage?.Close();
            RestoreHudOrder();
        }

        private void ToggleCommanderGrowthPage()
        {
            var shouldOpen = commanderGrowthPage != null && !commanderGrowthPage.activeSelf;
            CloseAllPages();
            commanderGrowthPage?.SetActive(shouldOpen);
            if (shouldOpen)
            {
                BringToFront();
            }
        }

        public void OpenShopPage()
        {
            CloseAllPages();
            shopPage?.SetActive(true);
            BringToFront();
        }

        private void CloseShopPage()
        {
            shopPage?.SetActive(false);
            RestoreHudOrder();
        }

        public void OpenMonsterManagementPage()
        {
            CloseAllPages();
            monsterManagementPage?.OpenPage();
        }

        private void ToggleEquipmentPage()
        {
            var shouldOpen = equipmentPage != null && !equipmentPage.activeSelf;
            CloseAllPages();
            equipmentPage?.SetActive(shouldOpen);
            if (shouldOpen)
            {
                BringToFront();
            }
        }

        private void CloseEquipmentPage()
        {
            equipmentPage?.SetActive(false);
            RestoreHudOrder();
        }

        private void ToggleGrowthDungeonPage()
        {
            var shouldOpen = growthDungeonPage != null && !growthDungeonPage.activeSelf;
            CloseAllPages();
            growthDungeonPage?.SetActive(shouldOpen);
            if (shouldOpen)
            {
                GrowthDungeonPageOpened?.Invoke();
                BringToFront();
            }
        }

        private void CloseGrowthDungeonPage()
        {
            growthDungeonPage?.SetActive(false);
            RestoreHudOrder();
        }

        private void HandleMonsterManagementOpenStateChanged(bool open)
        {
            if (open)
            {
                BringToFront();
            }
            else if ((commanderGrowthPage == null || !commanderGrowthPage.activeSelf) &&
                     (shopPage == null || !shopPage.activeSelf) &&
                     (equipmentPage == null || !equipmentPage.activeSelf) &&
                     (equipmentSlotUpgradePage == null || !equipmentSlotUpgradePage.IsOpen) &&
                     (inventoryPage == null || !inventoryPage.IsOpen) &&
                     (commanderSkillPage == null || !commanderSkillPage.IsOpen) &&
                     (growthDungeonPage == null || !growthDungeonPage.activeSelf) &&
                     (settingsPage == null || !settingsPage.IsOpen) &&
                     (attendancePage == null || !attendancePage.IsOpen) &&
                     (mailboxPage == null || !mailboxPage.IsOpen) &&
                     (questPage == null || !questPage.IsOpen))
            {
                RestoreHudOrder();
            }
        }

        private void HandleEquipmentSlotUpgradeOpenStateChanged(bool open)
        {
            if (open)
            {
                BringToFront();
            }
            else if (!IsAnyPageOpen)
            {
                RestoreHudOrder();
            }
        }

        private void HandleInventoryOpenStateChanged(bool open)
        {
            if (open)
            {
                BringToFront();
            }
            else if (!IsAnyPageOpen)
            {
                RestoreHudOrder();
            }
        }

        private void HandleCommanderSkillOpenStateChanged(bool open)
        {
            if (open)
            {
                BringToFront();
            }
            else if (!IsAnyPageOpen)
            {
                RestoreHudOrder();
            }
        }

        private void HandleSettingsOpenStateChanged(bool open)
        {
            if (open)
            {
                BringToFront();
            }
            else if (!IsAnyPageOpen)
            {
                RestoreHudOrder();
            }
        }

        private void HandleAttendanceOpenStateChanged(bool open)
        {
            if (open)
            {
                BringToFront();
            }
            else if (!IsAnyPageOpen)
            {
                RestoreHudOrder();
            }
        }

        private void HandleMailboxOpenStateChanged(bool open)
        {
            if (open)
            {
                BringToFront();
            }
            else if (!IsAnyPageOpen)
            {
                RestoreHudOrder();
            }
        }

        private void HandleQuestPageOpenStateChanged(bool open)
        {
            if (open)
            {
                BringToFront();
            }
            else if (!IsAnyPageOpen)
            {
                RestoreHudOrder();
            }
        }

        private void HandleFormationPageOpenStateChanged(bool open)
        {
            if (open)
            {
                CloseManagementPages();
            }
        }

        private void BringToFront()
        {
            transform.SetAsLastSibling();
            SetCombatDisplaySuppressed(true); // 관리 팝업 중 전투 표시 억제
            if (hudCanvas != null)
            {
                hudCanvas.sortingOrder = Math.Max(defaultCanvasSortingOrder, 100);
            }
        }

        private void RestoreHudOrder()
        {
            SetCombatDisplaySuppressed(false);
            var parent = transform.parent;
            if (defaultSiblingIndex < 0 || parent == null ||
                !gameObject.activeInHierarchy || !parent.gameObject.activeInHierarchy)
            {
                return;
            }

            transform.SetSiblingIndex(Mathf.Min(defaultSiblingIndex, parent.childCount - 1));
            RestoreCanvasOrder();
        }

        private void RestoreCanvasOrder()
        {
            if (hudCanvas != null)
            {
                hudCanvas.sortingOrder = defaultCanvasSortingOrder;
            }
        }

        private void SetCombatDisplaySuppressed(bool suppressed)
        {
            combatDisplaySuppressed = suppressed;
            foreach (var feedback in FindObjectsByType<CombatFeedbackPlayer>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                feedback.SetDisplaySuppressed(this, suppressed);
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Button growthButton,
            Button openShopButton,
            GameObject growthPage,
            GameObject shopPageRoot,
            Button closeShopButton)
        {
            commanderGrowthButton = growthButton;
            shopButton = openShopButton;
            commanderGrowthPage = growthPage;
            shopPage = shopPageRoot;
            shopCloseButton = closeShopButton;
        }


        public void EditorConfigureMonsterManagement(
            Button managementButton,
            MonsterManagementPageController managementPage)
        {
            monsterManagementButton = managementButton;
            monsterManagementPage = managementPage;
        }

        public void EditorConfigureUiOnlyPages(
            Button openEquipmentButton,
            GameObject equipmentPageRoot,
            Button closeEquipmentButton,
            Button openGrowthDungeonButton,
            GameObject growthDungeonPageRoot,
            Button closeGrowthDungeonButton)
        {
            equipmentButton = openEquipmentButton;
            equipmentPage = equipmentPageRoot;
            equipmentCloseButton = closeEquipmentButton;
            growthDungeonButton = openGrowthDungeonButton;
            growthDungeonPage = growthDungeonPageRoot;
            growthDungeonCloseButton = closeGrowthDungeonButton;
        }

        public void EditorConfigureRewardPages(
            AttendancePanelController attendance,
            MailboxPanelController mailbox)
        {
            attendancePage = attendance;
            mailboxPage = mailbox;
        }
#endif
    }
}
