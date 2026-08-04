using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class ShopCategoryMenu : MonoBehaviour // 다이아/콘텐츠 버튼 → 상점 화면 전환
    {
        [Header("카테고리 버튼")]
        [SerializeField] private Button buyGemsButton; // BuyGemsButton → DiamondShop
        [SerializeField] private Button contentButton; // ContentButton → ContentShop

        [Header("상점 화면 (하나만 켜짐)")]
        [SerializeField] private GameObject monsterShop;
        [SerializeField] private GameObject skillShop;
        [SerializeField] private GameObject diamondShop; // 다이아 구매 화면
        [SerializeField] private GameObject contentShop; // 콘텐츠 화면
        [SerializeField] private GameObject packageShop;
        [SerializeField] private GameObject monthlySubscriptionShop;

        private void Awake()
        {
            buyGemsButton?.onClick.AddListener(ShowDiamondShop);
            contentButton?.onClick.AddListener(ShowContentShop);
        }

        private void OnDestroy()
        {
            buyGemsButton?.onClick.RemoveListener(ShowDiamondShop);
            contentButton?.onClick.RemoveListener(ShowContentShop);
        }

        private void ShowDiamondShop()
        {
            ShowOnly(diamondShop);
        }

        private void ShowContentShop()
        {
            ShowOnly(contentShop);
        }

        private void ShowOnly(GameObject show) // 인스펙터에 넣은 상점 중 하나만 활성화
        {
            SetActive(monsterShop, show);
            SetActive(skillShop, show);
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
