using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Shared.Audio
{
    [Serializable]
    public sealed class SfxCatalogCategory // 관리툴의 영역 분류
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string folderName;
        [SerializeField] private Color accentColor = new Color(0.25f, 0.65f, 0.82f, 1f);

        public string Id => id;
        public string DisplayName => displayName;
        public string FolderName => folderName;
        public Color AccentColor => accentColor;

#if UNITY_EDITOR
        public SfxCatalogCategory(string categoryId, string label, string folder, Color accent)
        {
            id = categoryId;
            displayName = label;
            folderName = folder;
            accentColor = accent;
        }
#endif
    }

    [Serializable]
    public sealed class SfxCatalogEntry // 기존 Cue 참조와 영역만 보존
    {
        [SerializeField] private SfxCue cue;
        [SerializeField] private string categoryId;

        public SfxCue Cue => cue;
        public string CategoryId => categoryId;

#if UNITY_EDITOR
        public SfxCatalogEntry(SfxCue sourceCue, string sourceCategoryId)
        {
            cue = sourceCue;
            categoryId = sourceCategoryId;
        }

        public void EditorSetCategory(string value)
        {
            categoryId = value;
        }
#endif
    }

    [CreateAssetMenu(menuName = "ProjectMT/Audio/SFX Catalog", fileName = "SfxCatalog")]
    public sealed class SfxCatalog : ScriptableObject // 기존 Cue를 변경하지 않는 관리 인덱스
    {
        public const string UnassignedCategoryId = "unassigned";

        [SerializeField] private List<SfxCatalogCategory> categories = new List<SfxCatalogCategory>();
        [SerializeField] private List<SfxCatalogEntry> entries = new List<SfxCatalogEntry>();

        public IReadOnlyList<SfxCatalogCategory> Categories => categories;
        public IReadOnlyList<SfxCatalogEntry> Entries => entries;

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (categories == null || categories.Count == 0)
            {
                error = "SFX 카테고리가 없습니다.";
                return false;
            }

            var categoryIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < categories.Count; index++)
            {
                var category = categories[index];
                if (category == null || string.IsNullOrWhiteSpace(category.Id))
                {
                    error = $"카테고리 {index + 1}의 ID가 비어 있습니다.";
                    return false;
                }

                if (!categoryIds.Add(category.Id))
                {
                    error = $"카테고리 ID가 중복되었습니다: {category.Id}";
                    return false;
                }
            }

            var cues = new HashSet<SfxCue>();
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null || entry.Cue == null)
                {
                    error = $"SFX 목록 {index + 1}에 Cue가 없습니다.";
                    return false;
                }

                if (!cues.Add(entry.Cue))
                {
                    error = $"같은 Cue가 중복 등록되었습니다: {entry.Cue.name}";
                    return false;
                }

                if (!categoryIds.Contains(entry.CategoryId))
                {
                    error = $"{entry.Cue.name}의 카테고리를 찾을 수 없습니다: {entry.CategoryId}";
                    return false;
                }
            }

            return true;
        }

#if UNITY_EDITOR
        public bool EditorEnsureDefaultCategories()
        {
            categories ??= new List<SfxCatalogCategory>();
            entries ??= new List<SfxCatalogEntry>();

            var changed = false;
            foreach (var preset in BuildDefaultCategories())
            {
                if (categories.Any(category => category != null && category.Id == preset.Id))
                {
                    continue;
                }

                categories.Add(preset);
                changed = true;
            }

            return changed;
        }

        public bool EditorSynchronize(
            IEnumerable<SfxCue> discoveredCues,
            Func<SfxCue, string> categoryResolver)
        {
            entries ??= new List<SfxCatalogEntry>();
            var changed = EditorEnsureDefaultCategories();

            var unique = new HashSet<SfxCue>();
            for (var index = entries.Count - 1; index >= 0; index--)
            {
                var entry = entries[index];
                if (entry == null || entry.Cue == null || !unique.Add(entry.Cue))
                {
                    entries.RemoveAt(index);
                    changed = true;
                }
            }

            if (discoveredCues == null)
            {
                return changed;
            }

            var validCategoryIds = new HashSet<string>(
                categories.Where(category => category != null).Select(category => category.Id),
                StringComparer.Ordinal);
            foreach (var cue in discoveredCues.Where(cue => cue != null).Distinct())
            {
                if (unique.Contains(cue))
                {
                    continue;
                }

                var categoryId = categoryResolver?.Invoke(cue);
                if (string.IsNullOrWhiteSpace(categoryId) || !validCategoryIds.Contains(categoryId))
                {
                    categoryId = UnassignedCategoryId;
                }

                entries.Add(new SfxCatalogEntry(cue, categoryId));
                unique.Add(cue);
                changed = true;
            }

            return changed;
        }

        public bool EditorSetCategory(SfxCue cue, string categoryId)
        {
            if (cue == null || string.IsNullOrWhiteSpace(categoryId) ||
                !categories.Any(category => category != null && category.Id == categoryId))
            {
                return false;
            }

            var entry = entries.FirstOrDefault(candidate => candidate != null && candidate.Cue == cue);
            if (entry == null || entry.CategoryId == categoryId)
            {
                return false;
            }

            entry.EditorSetCategory(categoryId);
            return true;
        }

        public SfxCatalogEntry EditorFindEntry(SfxCue cue)
        {
            return entries.FirstOrDefault(entry => entry != null && entry.Cue == cue);
        }

        private static IEnumerable<SfxCatalogCategory> BuildDefaultCategories()
        {
            yield return Category(UnassignedCategoryId, "미분류", "Unassigned", 0.45f, 0.49f, 0.55f);
            yield return Category("common", "공용", "Common", 0.25f, 0.67f, 0.82f);
            yield return Category("ui", "UI · 시스템", "UI", 0.38f, 0.76f, 0.66f);
            yield return Category("main_battle", "메인 전투", "MainBattle", 0.87f, 0.58f, 0.30f);
            yield return Category("expedition", "원정대", "Expedition", 0.30f, 0.63f, 0.86f);
            yield return Category("monster_basic", "몬스터 기본공격", "Monsters/BasicAttack", 0.82f, 0.46f, 0.35f);
            yield return Category("monster_active", "몬스터 액티브", "Monsters/Active", 0.78f, 0.36f, 0.56f);
            yield return Category("commander_skill", "군단장 스킬", "CommanderSkills", 0.62f, 0.48f, 0.88f);
            yield return Category("castle_raid", "군단의 역습", "CastleRaid", 0.83f, 0.67f, 0.32f);
            yield return Category("food_riot", "식량 대소동", "FoodRiot", 0.52f, 0.72f, 0.34f);
            yield return Category("treasure_spirit", "보물 정령", "TreasureSpirit", 0.86f, 0.70f, 0.28f);
            yield return Category("fallen_commander", "타락한 군단장", "FallenCommander", 0.72f, 0.36f, 0.40f);
            yield return Category("guardian_trial", "고대 수호수", "GuardianTrial", 0.34f, 0.66f, 0.54f);
        }

        private static SfxCatalogCategory Category(
            string id,
            string label,
            string folder,
            float red,
            float green,
            float blue)
        {
            return new SfxCatalogCategory(id, label, folder, new Color(red, green, blue, 1f));
        }
#endif
    }
}
