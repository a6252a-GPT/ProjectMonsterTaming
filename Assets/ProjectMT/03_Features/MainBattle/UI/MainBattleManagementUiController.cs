using ProjectMT.Features.Formation;
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
        [SerializeField] private GameObject growthDungeonPage;
        [SerializeField] private Button growthDungeonCloseButton;

        private int defaultSiblingIndex = -1;
        private FormationPageController formationPage;

        public bool IsAnyPageOpen =>
            (commanderGrowthPage != null && commanderGrowthPage.activeSelf) ||
            (shopPage != null && shopPage.activeSelf) ||
            (monsterManagementPage != null && monsterManagementPage.IsOpen) ||
            (equipmentPage != null && equipmentPage.activeSelf) ||
            (growthDungeonPage != null && growthDungeonPage.activeSelf) ||
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
            CloseAllPages();
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
            commanderGrowthButton?.onClick.RemoveListener(ToggleCommanderGrowthPage);
            shopButton?.onClick.RemoveListener(OpenShopPage);
            shopCloseButton?.onClick.RemoveListener(CloseShopPage);
            monsterManagementButton?.onClick.RemoveListener(OpenMonsterManagementPage);
            equipmentButton?.onClick.RemoveListener(ToggleEquipmentPage);
            equipmentCloseButton?.onClick.RemoveListener(CloseEquipmentPage);
            growthDungeonButton?.onClick.RemoveListener(ToggleGrowthDungeonPage);
            growthDungeonCloseButton?.onClick.RemoveListener(CloseGrowthDungeonPage);
            if (monsterManagementPage != null)
            {
                monsterManagementPage.OpenStateChanged -= HandleMonsterManagementOpenStateChanged;
            }

            ConfigureFormationPage(null);
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

        public void OpenGrowthDungeonPage()
        {
            CloseAllPages();
            growthDungeonPage?.SetActive(true);
            BringToFront();
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
            growthDungeonPage?.SetActive(false);
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
                     (growthDungeonPage == null || !growthDungeonPage.activeSelf))
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
        }

        private void RestoreHudOrder()
        {
            var parent = transform.parent;
            if (defaultSiblingIndex < 0 || parent == null ||
                !gameObject.activeInHierarchy || !parent.gameObject.activeInHierarchy)
            {
                return;
            }

            transform.SetSiblingIndex(Mathf.Min(defaultSiblingIndex, parent.childCount - 1));
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
#endif
    }
}
