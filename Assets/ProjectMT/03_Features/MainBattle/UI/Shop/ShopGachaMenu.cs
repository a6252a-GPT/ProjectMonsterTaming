using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class ShopGachaMenu : MonoBehaviour // 뽑기 하위 메뉴 토글 + 상점 화면 전환
    {
        [Header("뽑기 카테고리")]
        [SerializeField] private Button gachaButton; // "뽑기" 버튼 - 하위 메뉴 열고 닫기
        [SerializeField] private GameObject gachaSubMenu; // 비활성화되어 있는 GachaSubMenu
        [SerializeField] private RectTransform gachaCategory; // MonsterCategory RectTransform
        [SerializeField] private RectTransform leftPanelPoint; // LeftPanelPoint - 레이아웃 강제 갱신용

        [Header("카테고리 높이")]
        [SerializeField] private float collapsedCategoryHeight = 100f; // 하위 메뉴 접었을 때 MonsterCategory 높이
        [SerializeField] private float expandedCategoryHeight = 280f; // 펼쳤을 때 (버튼100 + 간격10 + 서브170)

        [Header("하위 뽑기 버튼")]
        [SerializeField] private Button monsterGachaButton; // 몬스터 뽑기 버튼
        [SerializeField] private Button commanderSkillGachaButton; // 군단장 스킬 뽑기 버튼
        [SerializeField] private Button soulGachaButton;

        [Header("상점 화면 (하나만 켜짐)")]
        [SerializeField] private GameObject monsterShop; // 몬스터 → 이 화면 켜기
        [SerializeField] private GameObject skillShop; // 스킬 → 이 화면 켜기
        [SerializeField] private GameObject soulShop;
        [SerializeField] private GameObject diamondShop;
        [SerializeField] private GameObject contentShop;
        [SerializeField] private GameObject packageShop;
        [SerializeField] private GameObject monthlySubscriptionShop;

        private void Awake()
        {
            gachaButton?.onClick.AddListener(ToggleGachaSubMenu);
            monsterGachaButton?.onClick.AddListener(ShowMonsterShop);
            commanderSkillGachaButton?.onClick.AddListener(ShowSkillShop);
            soulGachaButton?.onClick.AddListener(ShowSoulShop);
        }

        private void Start()
        {
            if (gachaSubMenu != null)
            {
                gachaSubMenu.SetActive(false); // 시작 시 하위 메뉴는 접힌 상태
            }

            RefreshCategoryHeight(false); // 기본 배치: 뽑기 / 다이아 / 콘텐츠 / 패키지
            ShowMonsterShop();
        }

        private void OnDestroy()
        {
            gachaButton?.onClick.RemoveListener(ToggleGachaSubMenu);
            monsterGachaButton?.onClick.RemoveListener(ShowMonsterShop);
            commanderSkillGachaButton?.onClick.RemoveListener(ShowSkillShop);
            soulGachaButton?.onClick.RemoveListener(ShowSoulShop);
        }

        private void ToggleGachaSubMenu()
        {
            if (gachaSubMenu == null)
            {
                return;
            }

            var open = !gachaSubMenu.activeSelf;
            gachaSubMenu.SetActive(open);
            RefreshCategoryHeight(open);
        }

        private void RefreshCategoryHeight(bool expanded)
        {
            var height = expanded ? expandedCategoryHeight : collapsedCategoryHeight;

            // LeftPanelPoint는 자식 Height를 제어하지 않으므로, MonsterCategory 높이만 직접 바꾼다.
            if (gachaCategory != null)
            {
                var size = gachaCategory.sizeDelta;
                size.y = height;
                gachaCategory.sizeDelta = size;

                var layout = gachaCategory.GetComponent<LayoutElement>();
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

        private void ShowMonsterShop()
        {
            ShowOnly(monsterShop);
        }

        private void ShowSkillShop()
        {
            ShowOnly(skillShop);
        }

        private void ShowSoulShop() => ShowOnly(soulShop);

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
