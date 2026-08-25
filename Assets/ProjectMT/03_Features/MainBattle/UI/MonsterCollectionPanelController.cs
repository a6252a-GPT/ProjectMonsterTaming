using System.Collections.Generic;
using ProjectMT.Features.Formation;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    // 도감 탭 전환·공격 타입 필터·등급별 목록 표시 담당. CollectionPanel에 런타임으로 부착된다.
    [DisallowMultipleComponent]
    public sealed class MonsterCollectionPanelController : MonoBehaviour
    {
        private enum AttackFilter
        {
            All = 0,
            Melee = 1,
            Ranged = 2
        }

        private static readonly MonsterRarity[] TabRarities =
        {
            MonsterRarity.Common,
            MonsterRarity.Rare,
            MonsterRarity.Epic,
            MonsterRarity.Legendary,
            MonsterRarity.Mythic
        };

        private static readonly string[] ListNames =
        {
            "CommonMonsterList", "RareMonsterList", "EpicMonsterList",
            "LegendaryMonsterList", "MythicMonsterList"
        };

        private static readonly string[] TabNames =
        {
            "CommonTab", "RareTab", "EpicTab", "LegendaryTab", "MythicTab"
        };

        // BoxMenu 하위 버튼. 인덱스가 AttackFilter 값과 그대로 대응한다.
        private static readonly string[] AttackButtonNames =
        {
            "AllAttack", "MeleeAttack", "RangedAttack"
        };

        // Focus 오브젝트 이름이 탭마다 다를 수 있어(DailyFocus·WeeklyFocus 등) 위치(3번째)로 찾는다.
        private const int FocusIndicatorChildIndex = 2;

        private readonly List<Button> tabButtons = new List<Button>();
        private readonly List<GameObject> tabFocusIndicators = new List<GameObject>();
        private readonly List<MonsterRosterListView> rosterLists = new List<MonsterRosterListView>();
        private readonly List<IReadOnlyList<MonsterDefinition>> monstersByRarity =
            new List<IReadOnlyList<MonsterDefinition>>();
        private readonly List<MonsterDefinition> filteredScratch = new List<MonsterDefinition>();

        private readonly List<Button> attackFilterButtons = new List<Button>();
        private readonly List<GameObject> attackFilterFocusIndicators = new List<GameObject>();

        private bool referencesResolved;
        private bool catalogLoaded;
        private int selectedTabIndex;
        private AttackFilter selectedAttackFilter = AttackFilter.All;
        private MonsterRosterView ownedRoster;

        public void Configure(MonsterRarityCatalog rarityCatalog, IGameProgressService progressService)
        {
            ResolveReferences();
            LoadCatalog(rarityCatalog);
            ownedRoster = progressService != null ? progressService.View.Monsters : default;
            RefreshCounts();
            selectedAttackFilter = AttackFilter.All;
            ApplyAttackFocusIndicators();
            SelectTab(0);
        }

        private void ResolveReferences()
        {
            if (referencesResolved)
            {
                return;
            }

            referencesResolved = true;

            var managementContent = transform.Find("ManagementContent");
            if (managementContent == null)
            {
                Debug.LogWarning("MonsterCollectionPanelController: ManagementContent를 찾을 수 없습니다.", this);
                return;
            }

            var tabsRoot = managementContent.Find("CollectionTabs");
            var boxMenuRoot = managementContent.Find("BoxMenu");

            for (var i = 0; i < ListNames.Length; i++)
            {
                var listTransform = managementContent.Find(ListNames[i]);
                rosterLists.Add(listTransform != null
                    ? listTransform.GetComponent<MonsterRosterListView>()
                    : null);
            }

            for (var i = 0; i < TabNames.Length; i++)
            {
                var tabTransform = tabsRoot != null ? tabsRoot.Find(TabNames[i]) : null;
                var tabButton = tabTransform != null ? tabTransform.GetComponent<Button>() : null;
                tabButtons.Add(tabButton);
                tabFocusIndicators.Add(FindFocusIndicator(tabTransform));

                var capturedIndex = i;
                tabButton?.onClick.AddListener(() => SelectTab(capturedIndex));
            }

            for (var i = 0; i < AttackButtonNames.Length; i++)
            {
                var buttonTransform = boxMenuRoot != null ? boxMenuRoot.Find(AttackButtonNames[i]) : null;
                var button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
                attackFilterButtons.Add(button);
                attackFilterFocusIndicators.Add(FindFocusIndicator(buttonTransform));

                var capturedFilter = (AttackFilter)i;
                button?.onClick.AddListener(() => SelectAttackFilter(capturedFilter));
            }
        }

        private static GameObject FindFocusIndicator(Transform buttonTransform)
        {
            return buttonTransform != null && buttonTransform.childCount > FocusIndicatorChildIndex
                ? buttonTransform.GetChild(FocusIndicatorChildIndex).gameObject
                : null;
        }

        private void LoadCatalog(MonsterRarityCatalog rarityCatalog)
        {
            if (catalogLoaded)
            {
                return;
            }

            if (rarityCatalog == null)
            {
                Debug.LogWarning(
                    "MonsterCollectionPanelController: Monster Rarity Catalog가 연결되지 않아 도감 목록을 채울 수 없습니다.",
                    this);
                return;
            }

            catalogLoaded = true;
            for (var i = 0; i < TabRarities.Length; i++)
            {
                monstersByRarity.Add(rarityCatalog.GetMonstersOfRarity(TabRarities[i]));
            }
        }

        // 등급별 "보유 / 전체" 표시. 탭·필터와 무관하게 패널을 열 때 한 번만 계산한다.
        private void RefreshCounts()
        {
            for (var i = 0; i < monstersByRarity.Count && i < rosterLists.Count; i++)
            {
                var list = monstersByRarity[i];
                var owned = 0;
                for (var j = 0; j < list.Count; j++)
                {
                    if (list[j] != null && ownedRoster.Owns(list[j].MonsterId))
                    {
                        owned++;
                    }
                }

                rosterLists[i]?.SetCountText($"{owned} / {list.Count}");
            }
        }

        private void SelectTab(int index)
        {
            selectedTabIndex = index;
            for (var i = 0; i < rosterLists.Count; i++)
            {
                tabFocusIndicators[i]?.SetActive(i == index);
                rosterLists[i]?.gameObject.SetActive(i == index);
            }

            RefreshActiveList();
        }

        private void SelectAttackFilter(AttackFilter filter)
        {
            selectedAttackFilter = filter;
            ApplyAttackFocusIndicators();
            RefreshActiveList();
        }

        private void ApplyAttackFocusIndicators()
        {
            for (var i = 0; i < attackFilterFocusIndicators.Count; i++)
            {
                attackFilterFocusIndicators[i]?.SetActive(i == (int)selectedAttackFilter);
            }
        }

        // 현재 탭만 보이므로, 탭·공격 필터가 바뀔 때마다 그 탭의 카드만 다시 계산해서 바인딩한다.
        private void RefreshActiveList()
        {
            if (selectedTabIndex >= rosterLists.Count || selectedTabIndex >= monstersByRarity.Count)
            {
                return;
            }

            var rosterList = rosterLists[selectedTabIndex];
            if (rosterList == null)
            {
                return;
            }

            var rarity = TabRarities[selectedTabIndex];
            var filtered = FilterByAttack(monstersByRarity[selectedTabIndex]);

            var visibleCount = rosterList.EnsureCardCount(filtered.Count);
            var cards = rosterList.Cards;
            for (var i = 0; i < visibleCount; i++)
            {
                var definition = filtered[i];
                var isOwned = definition != null && ownedRoster.Owns(definition.MonsterId);
                cards[i]?.BindCatalogEntry(definition, rarity, isOwned);
            }

            rosterList.ResetScrollPosition();
        }

        private List<MonsterDefinition> FilterByAttack(IReadOnlyList<MonsterDefinition> source)
        {
            filteredScratch.Clear();
            for (var i = 0; i < source.Count; i++)
            {
                var definition = source[i];
                if (definition == null)
                {
                    continue;
                }

                var matches = selectedAttackFilter switch
                {
                    AttackFilter.Melee => !definition.Ranged,
                    AttackFilter.Ranged => definition.Ranged,
                    _ => true
                };

                if (matches)
                {
                    filteredScratch.Add(definition);
                }
            }

            return filteredScratch;
        }
    }
}
