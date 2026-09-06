using System;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
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
        private const int SoulPage = 6;

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

        [Header("테스트 다이아 지급")]
        [SerializeField] private Button[] diamondTestGrantButtons = Array.Empty<Button>();

        private IGameProgressService progress;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly long[] DiamondTestGrantAmounts = { 10L, 50L, 200L, 400L, 800L, 1000L };
        private UnityAction[] diamondTestGrantActions = Array.Empty<UnityAction>();
        private bool diamondGrantPending;
#endif

        private void Awake()
        {
            AddListeners();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddDiamondTestGrantListeners();
#endif
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RemoveDiamondTestGrantListeners();
#endif
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RefreshDiamondTestGrantButtons();
#endif
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
            AddListener(summonSubTabButtons, 2, ShowSoulPage);
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
            RemoveListener(summonSubTabButtons, 2, ShowSoulPage);
            RemoveListener(packageSubTabButtons, 0, ShowPackagePage);
            RemoveListener(packageSubTabButtons, 1, ShowMonthlyPage);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void AddDiamondTestGrantListeners()
        {
            var count = Mathf.Min(diamondTestGrantButtons?.Length ?? 0, DiamondTestGrantAmounts.Length);
            diamondTestGrantActions = new UnityAction[count];
            for (var index = 0; index < count; index++)
            {
                var button = diamondTestGrantButtons[index];
                var amount = DiamondTestGrantAmounts[index];
                UnityAction action = () => GrantDiamondsForTest(amount);
                diamondTestGrantActions[index] = action;
                button?.onClick.AddListener(action);
            }

            RefreshDiamondTestGrantButtons();
        }

        private void RemoveDiamondTestGrantListeners()
        {
            var count = Mathf.Min(diamondTestGrantButtons?.Length ?? 0, diamondTestGrantActions.Length);
            for (var index = 0; index < count; index++)
            {
                diamondTestGrantButtons[index]?.onClick.RemoveListener(diamondTestGrantActions[index]);
            }

            diamondTestGrantActions = Array.Empty<UnityAction>();
        }

        private async void GrantDiamondsForTest(long amount)
        {
            if (diamondGrantPending || progress == null || amount <= 0L)
            {
                return;
            }

            diamondGrantPending = true;
            RefreshDiamondTestGrantButtons();
            try
            {
                var granted = await progress.TryApplyAndSaveAsync(
                    GameProgressChange.GrantItems(new ItemAmount(ItemIds.Diamond, amount)));
                if (!granted)
                {
                    Debug.LogWarning($"Test diamond grant was rejected. Amount={amount}", this);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                diamondGrantPending = false;
                RefreshDiamondTestGrantButtons();
            }
        }

        private void RefreshDiamondTestGrantButtons()
        {
            var interactable = progress != null && !diamondGrantPending;
            var count = Mathf.Min(diamondTestGrantButtons?.Length ?? 0, DiamondTestGrantAmounts.Length);
            for (var index = 0; index < count; index++)
            {
                if (diamondTestGrantButtons[index] != null)
                {
                    diamondTestGrantButtons[index].interactable = interactable;
                }
            }
        }
#endif

        private void ShowMonsterPage()
        {
            ShowPage(MonsterPage, mainTabIndex: 0, summonSubTabIndex: 0, packageSubTabIndex: -1);
        }

        private void ShowSkillPage()
        {
            ShowPage(SkillPage, mainTabIndex: 0, summonSubTabIndex: 1, packageSubTabIndex: -1);
        }

        private void ShowSoulPage()
        {
            ShowPage(SoulPage, mainTabIndex: 0, summonSubTabIndex: 2, packageSubTabIndex: -1);
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
                diamondValueText.text = progress == null
                    ? "다이아  —"
                    : $"다이아  {progress.View.Diamond:N0}";
            }

            if (ascensionCurrencyValueText != null)
            {
                ascensionCurrencyValueText.text = progress == null
                    ? "영혼석  —"
                    : $"영혼석  {progress.View.AscensionCurrency:N0}";
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
            TMP_Text ascensionCurrency,
            Button[] diamondGrantButtons = null)
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
            diamondTestGrantButtons = diamondGrantButtons ?? Array.Empty<Button>();
        }
#endif
    }
}
