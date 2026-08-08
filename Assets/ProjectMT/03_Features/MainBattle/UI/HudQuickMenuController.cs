using ProjectMT.Features.Formation;
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
        [SerializeField] private Button commanderButton;
        [SerializeField] private Button castleRaidButton;
        [SerializeField] private Button modeButton;

        [Header("실제 기능")]
        [SerializeField] private MainBattleManagementUiController managementUi;
        [SerializeField] private FormationPageController formationPage;
        [SerializeField] private ShopCategoryMenu shopCategoryMenu;
        [SerializeField] private MainBattleSceneRoot sceneRoot;

        public bool IsOpen => expandedRoot != null && expandedRoot.activeSelf;

        private void Awake()
        {
            ResolveRuntimeReferences();
            contentButton?.onClick.AddListener(OpenContent);
            summonButton?.onClick.AddListener(OpenSummon);
            shopButton?.onClick.AddListener(OpenShop);
            menuButton?.onClick.AddListener(ToggleMenu);
            monsterGrowthButton?.onClick.AddListener(OpenMonsterGrowth);
            formationButton?.onClick.AddListener(OpenFormation);
            equipmentButton?.onClick.AddListener(OpenEquipment);
            commanderButton?.onClick.AddListener(OpenCommander);
            castleRaidButton?.onClick.AddListener(OpenCastleRaid);
            modeButton?.onClick.AddListener(CloseMenu);
            SetMenuOpen(false);
        }

        private void OnDestroy()
        {
            contentButton?.onClick.RemoveListener(OpenContent);
            summonButton?.onClick.RemoveListener(OpenSummon);
            shopButton?.onClick.RemoveListener(OpenShop);
            menuButton?.onClick.RemoveListener(ToggleMenu);
            monsterGrowthButton?.onClick.RemoveListener(OpenMonsterGrowth);
            formationButton?.onClick.RemoveListener(OpenFormation);
            equipmentButton?.onClick.RemoveListener(OpenEquipment);
            commanderButton?.onClick.RemoveListener(OpenCommander);
            castleRaidButton?.onClick.RemoveListener(OpenCastleRaid);
            modeButton?.onClick.RemoveListener(CloseMenu);
        }

        public void CloseMenu()
        {
            SetMenuOpen(false);
        }

        private void ToggleMenu()
        {
            SetMenuOpen(!IsOpen);
        }

        private void SetMenuOpen(bool open)
        {
            outsideTapRoot?.SetActive(open);
            expandedRoot?.SetActive(open);
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

        private void OpenCommander()
        {
            CloseMenu();
            managementUi?.OpenCommanderGrowthPage();
        }

        private void OpenCastleRaid()
        {
            CloseMenu();
            sceneRoot?.OpenCastleRaid();
        }

        private void ResolveRuntimeReferences()
        {
            managementUi ??= FindFirstObjectByType<MainBattleManagementUiController>(FindObjectsInactive.Include);
            formationPage ??= FindFirstObjectByType<FormationPageController>(FindObjectsInactive.Include);
            shopCategoryMenu ??= FindFirstObjectByType<ShopCategoryMenu>(FindObjectsInactive.Include);
            sceneRoot ??= FindFirstObjectByType<MainBattleSceneRoot>(FindObjectsInactive.Include);
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
            Button openCommanderButton,
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
            commanderButton = openCommanderButton;
            castleRaidButton = openCastleRaidButton;
            modeButton = currentModeButton;
        }
#endif
    }
}
