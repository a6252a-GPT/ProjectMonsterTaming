using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.EditorTools.MonsterMaker;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    public sealed partial class MonsterMakerV2Window
    {
        private void ConfigureCatalogList()
        {
            catalogList.itemsSource = visibleEntries;
            catalogList.selectionType = SelectionType.Single;
            catalogList.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            catalogList.fixedItemHeight = CatalogRowHeight;
            catalogList.makeItem = MakeCatalogItem;
            catalogList.bindItem = BindCatalogItem;
            catalogList.selectionChanged += OnCatalogSelectionChanged;
        }

        private void ShowAllDraftMenu()
        {
            var drafts = AssetDatabase.FindAssets("t:MonsterMakerDraft")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterMakerDraft>)
                .Where(candidate => candidate != null)
                .OrderBy(candidate => candidate.MonsterId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var menu = new GenericMenu();
            if (drafts.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("제작 원본이 없습니다."));
            }
            else
            {
                foreach (var candidate in drafts)
                {
                    var captured = candidate;
                    var id = string.IsNullOrWhiteSpace(candidate.MonsterId)
                        ? candidate.name
                        : candidate.MonsterId;
                    var displayName = string.IsNullOrWhiteSpace(candidate.DisplayName)
                        ? candidate.name
                        : candidate.DisplayName;
                    var label = $"[{SanitizeMenuLabel(id)}] {SanitizeMenuLabel(displayName)}";
                    menu.AddItem(
                        new GUIContent(label),
                        candidate == state?.SourceDraft,
                        () => OpenDraftInternal(captured));
                }
            }

            menu.DropDown(openDraftButton.worldBound);
        }

        private void ToggleCatalog()
        {
            catalogExpanded = !catalogExpanded;
            ApplyCatalogVisibility();
        }

        private void ApplyCatalogVisibility()
        {
            if (catalogPanel == null || catalogToggleButton == null)
            {
                return;
            }

            catalogPanel.style.display = catalogExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            catalogToggleButton.text = catalogExpanded ? "목록 접기" : "목록 열기";
            rootVisualElement.EnableInClassList("maker-root--catalog-collapsed", !catalogExpanded);
            minSize = new Vector2(
                catalogExpanded ? MinimumWindowWidth : MinimumWindowWidth - 238f,
                MinimumWindowHeight);
        }

        private static string SanitizeMenuLabel(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "이름 미지정"
                : value.Replace('/', '／');
        }

        private static VisualElement MakeCatalogItem()
        {
            var row = new VisualElement();
            row.AddToClassList("catalog-row");
            var portrait = new Image { name = "row-portrait", scaleMode = ScaleMode.ScaleToFit };
            portrait.AddToClassList("catalog-row__portrait");
            row.Add(portrait);

            var textArea = new VisualElement();
            textArea.AddToClassList("catalog-row__text");
            var name = new Label { name = "row-name" };
            name.AddToClassList("catalog-row__name");
            var meta = new Label { name = "row-meta" };
            meta.AddToClassList("catalog-row__meta");
            var itemState = new Label { name = "row-state" };
            itemState.AddToClassList("catalog-row__state");
            textArea.Add(name);
            textArea.Add(meta);
            textArea.Add(itemState);
            row.Add(textArea);
            return row;
        }

        private void BindCatalogItem(VisualElement element, int index)
        {
            if (index < 0 || index >= visibleEntries.Count)
            {
                return;
            }

            var entry = visibleEntries[index];
            var portrait = element.Q<Image>("row-portrait");
            portrait.sprite = entry.Definition.Portrait;
            portrait.style.display = entry.Definition.Portrait == null
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            element.Q<Label>("row-name").text =
                $"{entry.DisplayIndex:00}  {entry.Definition.DisplayName}";
            element.Q<Label>("row-meta").text =
                $"{entry.Definition.MonsterId}  ·  {GetRarityLabel(entry.Rarity)}";
            element.Q<Label>("row-state").text =
                entry.Draft == null ? "기존 호환 · 제작 원본 없음" : "Maker 수정 가능";
            element.tooltip =
                $"{entry.Definition.DisplayName}\n" +
                $"ID: {entry.Definition.MonsterId}\n" +
                $"{GetRarityLabel(entry.Rarity)} · " +
                (entry.Draft == null ? "제작 원본 없음" : "제작 원본 편집 가능");
            element.EnableInClassList("catalog-row--legacy", entry.Draft == null);
            RemoveRarityClasses(element);
            element.AddToClassList(GetRarityClass(entry.Rarity));
        }

        private void OnCatalogSelectionChanged(IEnumerable<object> selection)
        {
            if (suppressCatalogSelection || isBuildingUi)
            {
                return;
            }

            foreach (var item in selection)
            {
                if (item is CatalogEntry entry)
                {
                    SelectEntry(entry, true);
                    return;
                }
            }
        }

        private void SelectEntry(CatalogEntry entry, bool askBeforeLeaving)
        {
            if (entry == null)
            {
                return;
            }

            if (entry.Draft == null)
            {
                RestoreCatalogSelection();
                selectedDefinition = entry.Definition;
                pingButton.SetEnabled(true);
                catalogStatus.text = "제작 원본 없음 · Definition만 Project에서 확인 가능";
                Selection.activeObject = entry.Definition;
                EditorGUIUtility.PingObject(entry.Definition);
                ShowNotification(new GUIContent(
                    "기존 호환 Monster입니다. 제작 원본은 자동 생성하지 않습니다."));
                return;
            }

            if (state?.SourceDraft == entry.Draft)
            {
                selectedDefinition = entry.Definition;
                pingButton.SetEnabled(true);
                return;
            }

            if (askBeforeLeaving && !CanLeaveCurrentDraft())
            {
                RestoreCatalogSelection();
                return;
            }

            selectedDefinition = entry.Definition;
            pingButton.SetEnabled(true);
            state.Load(entry.Draft);
            BindCurrentDraft();
            catalogStatus.text = $"{entry.Definition.MonsterId} · 독립 V2 작업 사본";
        }

        private void ReloadCatalog(bool preserveCurrent)
        {
            if (catalogList == null)
            {
                return;
            }

            var previousId = preserveCurrent ? ResolveCurrentMonsterId() : string.Empty;
            allEntries.Clear();
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(
                MonsterMakerAssetWriter.MonsterCatalogPath);
            var rarityCatalog = AssetDatabase.LoadAssetAtPath<MonsterRarityCatalog>(
                MonsterMakerAssetWriter.MonsterRarityCatalogPath);

            if (catalog != null)
            {
                for (var index = 0; index < catalog.Definitions.Count; index++)
                {
                    var definition = catalog.Definitions[index];
                    if (definition == null)
                    {
                        continue;
                    }

                    MonsterRarity? rarity = null;
                    if (rarityCatalog != null &&
                        rarityCatalog.TryGetRarity(definition.MonsterId, out var value))
                    {
                        rarity = value;
                    }

                    var draft = AssetDatabase.LoadAssetAtPath<MonsterMakerDraft>(
                        MonsterMakerAssetWriter.BuildDraftPath(definition.MonsterId));
                    allEntries.Add(new CatalogEntry(definition, draft, rarity, index + 1));
                }
            }

            selectedDefinition = FindDefinition(previousId);
            ApplySearch(searchField?.value);
            UpdateSortButtons();
            catalogStatus.text = catalog == null
                ? "운영 MonsterCatalog를 찾지 못했습니다."
                : $"운영 Catalog {allEntries.Count}종 · V2 독립 편집기";
        }

        private void OpenInitialDraft()
        {
            var activeDraft = Selection.activeObject as MonsterMakerDraft;
            var target = activeDraft != null
                ? allEntries.FirstOrDefault(entry => entry.Draft == activeDraft)
                : allEntries.FirstOrDefault(entry => entry.Draft != null);
            if (target != null)
            {
                SelectEntry(target, false);
                SelectCatalogEntry(target);
                return;
            }

            state.CreateNew();
            selectedDefinition = null;
            BindCurrentDraft();
        }

        private void SetCatalogSortMode(CatalogSortMode mode)
        {
            catalogSortMode = mode;
            ApplySearch(searchField?.value);
            UpdateSortButtons();
        }

        private void ApplySearch(string rawQuery)
        {
            if (catalogList == null)
            {
                return;
            }

            var selectedId = ResolveCurrentMonsterId();
            var query = rawQuery?.Trim() ?? string.Empty;
            IEnumerable<CatalogEntry> source = allEntries;
            if (catalogSortMode == CatalogSortMode.Rarity)
            {
                source = source
                    .OrderByDescending(entry => entry.Rarity.HasValue ? (int)entry.Rarity.Value : -1)
                    .ThenBy(entry => entry.DisplayIndex);
            }

            visibleEntries.Clear();
            visibleEntries.AddRange(source.Where(entry =>
                string.IsNullOrEmpty(query) ||
                entry.Definition.MonsterId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                entry.Definition.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));

            suppressCatalogSelection = true;
            catalogList.Rebuild();
            catalogCount.text = string.IsNullOrEmpty(query)
                ? $"{visibleEntries.Count}종"
                : $"{visibleEntries.Count}/{allEntries.Count}종";
            var isEmpty = visibleEntries.Count == 0;
            catalogEmpty.style.display = isEmpty ? DisplayStyle.Flex : DisplayStyle.None;
            catalogList.style.display = isEmpty ? DisplayStyle.None : DisplayStyle.Flex;
            if (isEmpty)
            {
                catalogList.ClearSelection();
            }
            else
            {
                var selectedIndex = visibleEntries.FindIndex(entry =>
                    string.Equals(
                        entry.Definition.MonsterId,
                        selectedId,
                        StringComparison.OrdinalIgnoreCase));
                if (selectedIndex >= 0)
                {
                    catalogList.SetSelectionWithoutNotify(new[] { selectedIndex });
                    catalogList.ScrollToItem(selectedIndex);
                }
                else
                {
                    catalogList.ClearSelection();
                }
            }

            suppressCatalogSelection = false;
        }

        private void UpdateSortButtons()
        {
            sortDefaultButton?.EnableInClassList(
                "segmented-button--active",
                catalogSortMode == CatalogSortMode.Default);
            sortRarityButton?.EnableInClassList(
                "segmented-button--active",
                catalogSortMode == CatalogSortMode.Rarity);
        }

        private void RestoreCatalogSelection()
        {
            SelectCatalogEntry(allEntries.Find(entry => entry.Draft == state?.SourceDraft));
        }

        private void SelectCatalogEntry(CatalogEntry entry)
        {
            suppressCatalogSelection = true;
            if (entry == null)
            {
                catalogList.ClearSelection();
            }
            else
            {
                var index = visibleEntries.IndexOf(entry);
                if (index >= 0)
                {
                    catalogList.SetSelectionWithoutNotify(new[] { index });
                    catalogList.ScrollToItem(index);
                }
            }

            suppressCatalogSelection = false;
        }

        private string ResolveCurrentMonsterId()
        {
            if (state?.WorkingDraft != null && !state.IsNew)
            {
                return state.WorkingDraft.MonsterId;
            }

            return selectedDefinition == null ? string.Empty : selectedDefinition.MonsterId;
        }

        private MonsterDefinition FindDefinition(string monsterId)
        {
            return string.IsNullOrWhiteSpace(monsterId)
                ? null
                : allEntries.Select(entry => entry.Definition).FirstOrDefault(definition =>
                    string.Equals(
                        definition.MonsterId,
                        monsterId,
                        StringComparison.OrdinalIgnoreCase));
        }

        private void PingSelectedDefinition()
        {
            if (selectedDefinition == null)
            {
                return;
            }

            Selection.activeObject = selectedDefinition;
            EditorGUIUtility.PingObject(selectedDefinition);
        }

        private static void RemoveRarityClasses(VisualElement element)
        {
            for (var index = 0; index < RarityClasses.Length; index++)
            {
                element.RemoveFromClassList(RarityClasses[index]);
            }
        }

        private static string GetRarityClass(MonsterRarity? rarity)
        {
            return rarity switch
            {
                MonsterRarity.Rare => "rarity-rare",
                MonsterRarity.Epic => "rarity-epic",
                MonsterRarity.Legendary => "rarity-legendary",
                MonsterRarity.Mythic => "rarity-mythic",
                _ => "rarity-common"
            };
        }

        private static string GetRarityLabel(MonsterRarity? rarity)
        {
            return rarity switch
            {
                MonsterRarity.Common => "일반",
                MonsterRarity.Rare => "희귀",
                MonsterRarity.Epic => "영웅",
                MonsterRarity.Legendary => "전설",
                MonsterRarity.Mythic => "신화",
                _ => "등급 미지정"
            };
        }
    }
}
