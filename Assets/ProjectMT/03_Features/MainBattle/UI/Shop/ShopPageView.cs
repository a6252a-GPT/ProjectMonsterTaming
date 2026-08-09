using System;
using ProjectMT.Shared.GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class ShopPageView : MonoBehaviour // 상점 탭·재화 표시 전용 View
    {
        private const int MonsterPage = 0;
        private const int SkillPage = 1;
        private const int DiamondPage = 2;
        private const int ContentPage = 3;
        private const int PackagePage = 4;
        private const int MonthlyPage = 5;

        [Header("메인 탭: 소환 / 다이아 / 콘텐츠 / 패키지")]
        [SerializeField] private Button[] mainTabButtons = Array.Empty<Button>();
        [SerializeField] private GameObject[] mainTabNormalVisuals = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] mainTabSelectedVisuals = Array.Empty<GameObject>();

        [Header("소환 하위 탭: 몬스터 / 군단장 스킬")]
        [SerializeField] private GameObject summonSubTabRoot;
        [SerializeField] private Button[] summonSubTabButtons = Array.Empty<Button>();
        [SerializeField] private GameObject[] summonSubTabNormalVisuals = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] summonSubTabSelectedVisuals = Array.Empty<GameObject>();

        [Header("패키지 하위 탭: 일반 / 월정액")]
        [SerializeField] private GameObject packageSubTabRoot;
        [SerializeField] private Button[] packageSubTabButtons = Array.Empty<Button>();
        [SerializeField] private GameObject[] packageSubTabNormalVisuals = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] packageSubTabSelectedVisuals = Array.Empty<GameObject>();

        [Header("페이지: 몬스터 / 스킬 / 다이아 / 콘텐츠 / 일반 / 월정액")]
        [SerializeField] private GameObject[] pages = Array.Empty<GameObject>();

        [Header("재화 표시")]
        [SerializeField] private TMP_Text goldValueText;
        [SerializeField] private TMP_Text diamondValueText;
        [SerializeField] private TMP_Text ascensionCurrencyValueText;

        private IGameProgressService progress;

        private void Awake()
        {
            AddListeners();
        }

        private void OnEnable()
        {
            if (pages != null && pages.Length > 0)
            {
                ShowMonsterPage();
            }

            RefreshCurrency();
        }

        private void OnDestroy()
        {
            RemoveListeners();
            UnsubscribeProgress();
        }

        public void Configure(IGameProgressService progressService)
        {
            if (progress == progressService)
            {
                RefreshCurrency();
                return;
            }

            UnsubscribeProgress();
            progress = progressService;
            if (progress != null)
            {
                progress.Changed += RefreshCurrency;
            }

            RefreshCurrency();
        }

        public void Shutdown()
        {
            UnsubscribeProgress();
        }

        private void AddListeners()
        {
            AddListener(mainTabButtons, 0, ShowMonsterPage);
            AddListener(mainTabButtons, 1, ShowDiamondPage);
            AddListener(mainTabButtons, 2, ShowContentPage);
            AddListener(mainTabButtons, 3, ShowPackagePage);
            AddListener(summonSubTabButtons, 0, ShowMonsterPage);
            AddListener(summonSubTabButtons, 1, ShowSkillPage);
            AddListener(packageSubTabButtons, 0, ShowPackagePage);
            AddListener(packageSubTabButtons, 1, ShowMonthlyPage);
        }

        private void RemoveListeners()
        {
            RemoveListener(mainTabButtons, 0, ShowMonsterPage);
            RemoveListener(mainTabButtons, 1, ShowDiamondPage);
            RemoveListener(mainTabButtons, 2, ShowContentPage);
            RemoveListener(mainTabButtons, 3, ShowPackagePage);
            RemoveListener(summonSubTabButtons, 0, ShowMonsterPage);
            RemoveListener(summonSubTabButtons, 1, ShowSkillPage);
            RemoveListener(packageSubTabButtons, 0, ShowPackagePage);
            RemoveListener(packageSubTabButtons, 1, ShowMonthlyPage);
        }

        private void ShowMonsterPage()
        {
            ShowPage(MonsterPage, mainTabIndex: 0, summonSubTabIndex: 0, packageSubTabIndex: -1);
        }

        private void ShowSkillPage()
        {
            ShowPage(SkillPage, mainTabIndex: 0, summonSubTabIndex: 1, packageSubTabIndex: -1);
        }

        private void ShowDiamondPage()
        {
            ShowPage(DiamondPage, mainTabIndex: 1, summonSubTabIndex: -1, packageSubTabIndex: -1);
        }

        private void ShowContentPage()
        {
            ShowPage(ContentPage, mainTabIndex: 2, summonSubTabIndex: -1, packageSubTabIndex: -1);
        }

        private void ShowPackagePage()
        {
            ShowPage(PackagePage, mainTabIndex: 3, summonSubTabIndex: -1, packageSubTabIndex: 0);
        }

        private void ShowMonthlyPage()
        {
            ShowPage(MonthlyPage, mainTabIndex: 3, summonSubTabIndex: -1, packageSubTabIndex: 1);
        }

        private void ShowPage(int pageIndex, int mainTabIndex, int summonSubTabIndex, int packageSubTabIndex)
        {
            for (var index = 0; index < pages.Length; index++)
            {
                pages[index]?.SetActive(index == pageIndex);
            }

            SetTabVisuals(mainTabNormalVisuals, mainTabSelectedVisuals, mainTabIndex);
            SetTabVisuals(summonSubTabNormalVisuals, summonSubTabSelectedVisuals, summonSubTabIndex);
            SetTabVisuals(packageSubTabNormalVisuals, packageSubTabSelectedVisuals, packageSubTabIndex);
            summonSubTabRoot?.SetActive(mainTabIndex == 0);
            packageSubTabRoot?.SetActive(mainTabIndex == 3);
        }

        private void RefreshCurrency()
        {
            if (goldValueText != null)
            {
                goldValueText.text = progress == null
                    ? "골드  —"
                    : $"골드  {progress.View.Gold:N0}";
            }

            if (diamondValueText != null)
            {
                diamondValueText.text = "보석  —"; // 다이아 저장·거래 계약은 아직 미구현
            }

            if (ascensionCurrencyValueText != null)
            {
                ascensionCurrencyValueText.text = progress == null
                    ? "돌파석  —"
                    : $"돌파석  {progress.View.AscensionCurrency:N0}";
            }
        }

        private void UnsubscribeProgress()
        {
            if (progress != null)
            {
                progress.Changed -= RefreshCurrency;
            }

            progress = null;
        }

        private static void SetTabVisuals(
            GameObject[] normalVisuals,
            GameObject[] selectedVisuals,
            int selectedIndex)
        {
            var count = Mathf.Max(normalVisuals?.Length ?? 0, selectedVisuals?.Length ?? 0);
            for (var index = 0; index < count; index++)
            {
                SetActive(normalVisuals, index, index != selectedIndex);
                SetActive(selectedVisuals, index, index == selectedIndex);
            }
        }

        private static void SetActive(GameObject[] targets, int index, bool active)
        {
            if (targets != null && index >= 0 && index < targets.Length && targets[index] != null)
            {
                targets[index].SetActive(active);
            }
        }

        private static void AddListener(Button[] buttons, int index, UnityEngine.Events.UnityAction action)
        {
            if (buttons != null && index >= 0 && index < buttons.Length && buttons[index] != null)
            {
                buttons[index].onClick.AddListener(action);
            }
        }

        private static void RemoveListener(Button[] buttons, int index, UnityEngine.Events.UnityAction action)
        {
            if (buttons != null && index >= 0 && index < buttons.Length && buttons[index] != null)
            {
                buttons[index].onClick.RemoveListener(action);
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Button[] mainButtons,
            GameObject[] mainNormal,
            GameObject[] mainSelected,
            GameObject summonSubRoot,
            Button[] summonButtons,
            GameObject[] summonNormal,
            GameObject[] summonSelected,
            GameObject packageSubRoot,
            Button[] packageButtons,
            GameObject[] packageNormal,
            GameObject[] packageSelected,
            GameObject[] pageRoots,
            TMP_Text gold,
            TMP_Text diamond,
            TMP_Text ascensionCurrency)
        {
            mainTabButtons = mainButtons ?? Array.Empty<Button>();
            mainTabNormalVisuals = mainNormal ?? Array.Empty<GameObject>();
            mainTabSelectedVisuals = mainSelected ?? Array.Empty<GameObject>();
            summonSubTabRoot = summonSubRoot;
            summonSubTabButtons = summonButtons ?? Array.Empty<Button>();
            summonSubTabNormalVisuals = summonNormal ?? Array.Empty<GameObject>();
            summonSubTabSelectedVisuals = summonSelected ?? Array.Empty<GameObject>();
            packageSubTabRoot = packageSubRoot;
            packageSubTabButtons = packageButtons ?? Array.Empty<Button>();
            packageSubTabNormalVisuals = packageNormal ?? Array.Empty<GameObject>();
            packageSubTabSelectedVisuals = packageSelected ?? Array.Empty<GameObject>();
            pages = pageRoots ?? Array.Empty<GameObject>();
            goldValueText = gold;
            diamondValueText = diamond;
            ascensionCurrencyValueText = ascensionCurrency;
        }
#endif
    }
}
