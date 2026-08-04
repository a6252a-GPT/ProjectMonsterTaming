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

        [Header("관리 Page")]
        [SerializeField] private GameObject commanderGrowthPage;
        [SerializeField] private GameObject shopPage;
        [SerializeField] private Button shopCloseButton;

        private int defaultSiblingIndex = -1;

        public bool IsAnyPageOpen =>
            (commanderGrowthPage != null && commanderGrowthPage.activeSelf) ||
            (shopPage != null && shopPage.activeSelf);

        private void Awake()
        {
            if (commanderGrowthButton == null || shopButton == null ||
                commanderGrowthPage == null || shopPage == null || shopCloseButton == null)
            {
                Debug.LogError("MainBattleManagementUiController: UI 참조가 비어 있습니다.", this);
                return;
            }

            defaultSiblingIndex = transform.GetSiblingIndex();
            CloseAllPages();
            commanderGrowthButton.onClick.AddListener(ToggleCommanderGrowthPage);
            shopButton.onClick.AddListener(OpenShopPage);
            shopCloseButton.onClick.AddListener(CloseShopPage);
        }

        private void OnDestroy()
        {
            commanderGrowthButton?.onClick.RemoveListener(ToggleCommanderGrowthPage);
            shopButton?.onClick.RemoveListener(OpenShopPage);
            shopCloseButton?.onClick.RemoveListener(CloseShopPage);
        }

        public void CloseAllPages()
        {
            commanderGrowthPage?.SetActive(false);
            shopPage?.SetActive(false);
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

        private void OpenShopPage()
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

        private void BringToFront()
        {
            transform.SetAsLastSibling();
        }

        private void RestoreHudOrder()
        {
            if (defaultSiblingIndex < 0 || transform.parent == null)
            {
                return;
            }

            transform.SetSiblingIndex(Mathf.Min(defaultSiblingIndex, transform.parent.childCount - 1));
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
#endif
    }
}
