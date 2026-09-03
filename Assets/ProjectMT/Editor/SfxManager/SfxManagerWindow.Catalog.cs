using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Audio;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.Audio
{
    public sealed partial class SfxManagerWindow
    {
        private const string CatalogDirectory = "Assets/ProjectMT/06_Audio/SFX/Catalogs";
        private const string CatalogPath = CatalogDirectory + "/SfxCatalog_ProjectMT.asset";
        private const string ProjectAudioRoot = "Assets/ProjectMT/06_Audio/SFX";

        private void EnsureCatalogAndSynchronize(bool userInitiated)
        {
            EnsureFolder(CatalogDirectory);
            var created = false;
            catalog = AssetDatabase.LoadAssetAtPath<SfxCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = CreateInstance<SfxCatalog>();
                catalog.name = "SfxCatalog_ProjectMT";
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                created = true;
            }

            var discovered = FindAllProjectCues();
            var changed = catalog.EditorSynchronize(discovered, ResolveInitialCategory);
            if (created || changed)
            {
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }

            if (userInitiated)
            {
                var imported = catalog.Entries.Count(entry => entry?.Cue != null);
                SetStatus($"프로젝트 Cue를 다시 확인했습니다. 현재 {imported}개가 등록되어 있습니다.");
                RefreshAll();
            }
        }

        private static IReadOnlyList<SfxCue> FindAllProjectCues()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddPaths(paths, AssetDatabase.FindAssets("t:SfxCue", new[] { "Assets/ProjectMT" }));
            AddPaths(
                paths,
                AssetDatabase.FindAssets(
                    "t:MonsterFeedbackProfile",
                    new[] { "Assets/ProjectMT/02_Shared/Unit/Data/Monsters" }));
            AddPaths(
                paths,
                AssetDatabase.FindAssets(
                    "t:CommanderSkillDefinition",
                    new[] { "Assets/ProjectMT/03_Features/CommanderSkill/Resources" }));

            var cues = new HashSet<SfxCue>();
            foreach (var path in paths)
            {
                foreach (var cue in AssetDatabase.LoadAllAssetsAtPath(path).OfType<SfxCue>())
                {
                    if (cue != null)
                    {
                        cues.Add(cue);
                    }
                }
            }

            return cues.OrderBy(cue => cue.name, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static void AddPaths(ISet<string> paths, IEnumerable<string> guids)
        {
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    paths.Add(path);
                }
            }
        }

        private static string ResolveInitialCategory(SfxCue cue)
        {
            if (cue == null)
            {
                return SfxCatalog.UnassignedCategoryId;
            }

            var path = AssetDatabase.GetAssetPath(cue).Replace('\\', '/').ToLowerInvariant();
            var name = cue.name.ToLowerInvariant();
            if (path.Contains("/commanderskill/")) return "commander_skill";
            if (path.Contains("/01_castleraid/") || path.Contains("/sfx/castleraid/")) return "castle_raid";
            if (path.Contains("/02_foodriot/")) return "food_riot";
            if (path.Contains("/03_treasurespirit/")) return "treasure_spirit";
            if (path.Contains("/04_fallencommander/")) return "fallen_commander";
            if (path.Contains("/05_guardiantrial/")) return "guardian_trial";
            if (path.Contains("/03_features/mainbattle/") || path.Contains("/sfx/mainbattle/")) return "main_battle";
            if (path.Contains("/expedition/") || path.Contains("/sfx/expedition/")) return "expedition";
            if (path.Contains("/02_shared/ui/") || path.Contains("/sfx/ui/")) return "ui";

            if (path.Contains("/data/monsters/") || path.Contains("/sfx/monsters/"))
            {
                return name.Contains("active") || name.Contains("skill")
                    ? "monster_active"
                    : "monster_basic";
            }

            if (path.Contains("/sfx/common/")) return "common";
            return SfxCatalog.UnassignedCategoryId;
        }

        private void CreateNewCue()
        {
            if (catalog == null)
            {
                EnsureCatalogAndSynchronize(false);
            }

            var categoryId = selectedCategoryId == AllCategoryId
                ? SfxCatalog.UnassignedCategoryId
                : selectedCategoryId;
            var category = FindCategory(categoryId) ?? FindCategory(SfxCatalog.UnassignedCategoryId);
            var folder = BuildCategoryFolder(category);
            EnsureFolder(folder);

            var path = EditorUtility.SaveFilePanelInProject(
                "새 SFX Cue",
                "SFX_NewCue",
                "asset",
                "새 Cue의 이름과 저장 위치를 정하세요. AudioClip 원본은 이후 관리창에서 추가합니다.",
                folder);
            if (string.IsNullOrWhiteSpace(path))
            {
                SetStatus("새 Cue 만들기를 취소했습니다.");
                return;
            }

            var cue = CreateInstance<SfxCue>();
            cue.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(cue, path);
            catalog.EditorSynchronize(new[] { cue }, _ => categoryId);
            catalog.EditorSetCategory(cue, categoryId);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            selectedCue = cue;
            selectedCategoryId = categoryId;
            searchText = string.Empty;
            searchField?.SetValueWithoutNotify(string.Empty);
            RefreshAll();
            SetStatus($"{cue.name} Cue를 만들었습니다. 사운드를 드래그해서 추가하세요.");
        }

        private void AssignCategory(string categoryId)
        {
            if (catalog == null || selectedCue == null)
            {
                return;
            }

            Undo.RecordObject(catalog, "SFX Cue 영역 변경");
            if (!catalog.EditorSetCategory(selectedCue, categoryId))
            {
                return;
            }

            EditorUtility.SetDirty(catalog);
            RefreshCategoryButtons();
            RefreshFilteredEntries();
            RefreshDetails();
            SetStatus($"{selectedCue.name}의 영역을 ‘{FindCategory(categoryId)?.DisplayName}’으로 변경했습니다.");
        }

        private void SaveAllChanges()
        {
            if (catalog == null)
            {
                SetStatus("저장할 SFX Catalog가 없습니다.", false, true);
                return;
            }

            if (!catalog.TryValidate(out var error))
            {
                SetStatus($"저장 차단: {error}", false, true);
                return;
            }

            if (spaceCatalog == null || !spaceCatalog.TryValidate(out error))
            {
                SetStatus($"저장 차단: {error}", false, true);
                return;
            }

            var categoryIds = new HashSet<string>(
                catalog.Categories.Where(category => category != null).Select(category => category.Id),
                StringComparer.Ordinal);
            var invalidSpace = spaceCatalog.Entries.FirstOrDefault(
                entry => entry != null && !categoryIds.Contains(entry.CategoryId));
            if (invalidSpace != null)
            {
                SetStatus($"저장 차단: {invalidSpace.Id}의 Cue 영역을 찾을 수 없습니다.", false, true);
                return;
            }

            AssetDatabase.SaveAssets();
            RefreshAll();
            SetStatus("SFX 공간 결정, Cue Catalog, 변경된 Cue를 저장했습니다.");
        }

        private static string BuildCategoryFolder(SfxCatalogCategory category)
        {
            if (category == null || string.IsNullOrWhiteSpace(category.FolderName))
            {
                return ProjectAudioRoot + "/Unassigned";
            }

            return ProjectAudioRoot + "/" + category.FolderName.Trim('/');
        }

        private static void EnsureFolder(string folderPath)
        {
            var parts = folderPath.Replace('\\', '/').Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private SfxCatalogEntry FindSelectedEntry()
        {
            return catalog?.EditorFindEntry(selectedCue);
        }

        private static int CountPlayableClips(SfxCue cue)
        {
            return cue?.Clips?.Count(clip => clip != null) ?? 0;
        }

        private static string FormatVolume(Vector2 range)
        {
            var low = Mathf.RoundToInt(Mathf.Min(range.x, range.y) * 100f);
            var high = Mathf.RoundToInt(Mathf.Max(range.x, range.y) * 100f);
            return low == high ? $"{low}%" : $"{low}~{high}%";
        }

        private static string BuildCompactPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "저장 경로 없음";
            }

            const string prefix = "Assets/ProjectMT/";
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(prefix.Length)
                : path;
        }
    }
}
