using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class ShopPackageMenu : MonoBehaviour // 패키지 하위 메뉴 토글 + 상점 화면 전환
    {
        [Header("패키지 카테고리")]
        [SerializeField] private Button contentButton; // "패키지" 버튼 - 하위 메뉴 열고 닫기
        [SerializeField] private GameObject contentSubMenu; // 비활성화되어 있는 ContentSubMenu
        [SerializeField] private RectTransform packageCategory; // PackageCategory RectTransform
        [SerializeField] private RectTransform leftPanelPoint; // LeftPanelPoint - 레이아웃 강제 갱신용

        [Header("카테고리 높이")]
        [SerializeField] private float collapsedCategoryHeight = 100f; // 하위 메뉴 접었을 때 PackageCategory 높이
        [SerializeField] private float expandedCategoryHeight = 280f; // 펼쳤을 때 (버튼100 + 간격10 + 서브170)

        [Header("하위 패키지 버튼")]
        [SerializeField] private Button standardPackageButton; // 일반 패키지 버튼
        [SerializeField] private Button monthlySubscriptionShopButton; // 월정액 버튼

        [Header("상점 화면 (하나만 켜짐)")]
        [SerializeField] private GameObject monsterShop;
        [SerializeField] private GameObject skillShop;
        [SerializeField] private GameObject soulShop;
        [SerializeField] private GameObject diamondShop;
        [SerializeField] private GameObject contentShop;
        [SerializeField] private GameObject packageShop; // 일반 → 이 화면 켜기
        [SerializeField] private GameObject monthlySubscriptionShop; // 월정액 → 이 화면 켜기

        private void Awake()
        {
            contentButton?.onClick.AddListener(ToggleContentSubMenu);
            standardPackageButton?.onClick.AddListener(ShowPackageShop);
            monthlySubscriptionShopButton?.onClick.AddListener(ShowMonthlySubscriptionShop);
        }

        private void Start()
        {
            if (contentSubMenu != null)
            {
                contentSubMenu.SetActive(false); // 시작 시 하위 메뉴는 접힌 상태
            }

            RefreshCategoryHeight(false);
        }

        private void OnDestroy()
        {
            contentButton?.onClick.RemoveListener(ToggleContentSubMenu);
            standardPackageButton?.onClick.RemoveListener(ShowPackageShop);
            monthlySubscriptionShopButton?.onClick.RemoveListener(ShowMonthlySubscriptionShop);
        }

        private void ToggleContentSubMenu()
        {
            if (contentSubMenu == null)
            {
                return;
            }

            var open = !contentSubMenu.activeSelf;
            contentSubMenu.SetActive(open);
            RefreshCategoryHeight(open);
        }

        private void RefreshCategoryHeight(bool expanded)
        {
            var height = expanded ? expandedCategoryHeight : collapsedCategoryHeight;

            // LeftPanelPoint는 자식 Height를 제어하지 않으므로, PackageCategory 높이만 직접 바꾼다.
            if (packageCategory != null)
            {
                var size = packageCategory.sizeDelta;
                size.y = height;
                packageCategory.sizeDelta = size;

                var layout = packageCategory.GetComponent<LayoutElement>();
                if (layout != null)
                {
                    layout.minHeight = height;
                    layout.preferredHeight = height;
                }
            }

            if (leftPanelPoint != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(leftPanelPoint);
            }
        }

        private void ShowPackageShop()
        {
            ShowOnly(packageShop);
        }

        private void ShowMonthlySubscriptionShop()
        {
            ShowOnly(monthlySubscriptionShop);
        }

        private void ShowOnly(GameObject show) // 인스펙터에 넣은 상점 중 하나만 활성화
        {
            SetActive(monsterShop, show);
            SetActive(skillShop, show);
            SetActive(soulShop, show);
            SetActive(diamondShop, show);
            SetActive(contentShop, show);
            SetActive(packageShop, show);
            SetActive(monthlySubscriptionShop, show);
        }

        private static void SetActive(GameObject shop, GameObject show)
        {
            if (shop != null)
            {
                shop.SetActive(shop == show);
            }
        }
    }
}
