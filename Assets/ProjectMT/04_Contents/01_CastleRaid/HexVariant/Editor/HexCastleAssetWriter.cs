using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public static class HexCastleAssetWriter
    {
        public const string ApprovedFolder =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data/Approved";
        public const string CatalogPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data/Catalogs/HexCastleCatalog.asset";

        public static HexCastleStageLayout SaveApproved(HexCastleCandidate candidate)
        {
            var layout = candidate?.Layout;
            var stageId = layout == null
                ? string.Empty
                : $"HEX_{ResolveThemeToken(layout.Theme)}_{layout.DefenseLayerCount}W_{layout.Seed}";
            return SaveApproved(candidate, stageId, LoadRequiredRules(), true);
        }

        public static HexCastleStageLayout SaveApproved(
            HexCastleCandidate candidate,
            string stageId,
            bool replaceExisting = false)
        {
            return SaveApproved(candidate, stageId, LoadRequiredRules(), replaceExisting);
        }

        public static HexCastleStageLayout SaveApproved(
            HexCastleCandidate candidate,
            string stageId,
            HexCastleThemeOneRules rules,
            bool replaceExisting = false)
        {
            ValidateCandidate(candidate);
            EnsureStageApprovalReady(rules);
            stageId = NormalizeStageId(stageId);
            EnsureFolder(ApprovedFolder);
            var catalog = LoadOrCreateCatalog(rules);
            var existingEntry = catalog.Entries.FirstOrDefault(entry =>
                string.Equals(entry.StageId, stageId, StringComparison.OrdinalIgnoreCase));
            if (!replaceExisting && existingEntry != null)
            {
                throw new InvalidOperationException($"StageId '{stageId}'는 이미 사용 중입니다.");
            }

            var path = existingEntry?.Layout != null
                ? AssetDatabase.GetAssetPath(existingEntry.Layout)
                : $"{ApprovedFolder}/HexStage_{stageId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<HexCastleStageLayout>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HexCastleStageLayout>();
                asset.name = Path.GetFileNameWithoutExtension(path);
                asset.Configure(candidate, stageId);
                AssetDatabase.CreateAsset(asset, path);
            }
            else
            {
                asset.Configure(candidate, stageId);
                EditorUtility.SetDirty(asset);
            }

            catalog.Upsert(stageId, asset);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return asset;
        }

        public static IReadOnlyList<HexCastleStageLayout> SaveBatch(
            IEnumerable<HexCastleCandidate> source,
            int startStageNumber)
        {
            return SaveBatch(source, startStageNumber, LoadRequiredRules());
        }

        public static IReadOnlyList<HexCastleStageLayout> SaveBatch(
            IEnumerable<HexCastleCandidate> source,
            int startStageNumber,
            HexCastleThemeOneRules rules)
        {
            EnsureStageApprovalReady(rules);
            var candidates = (source ?? Array.Empty<HexCastleCandidate>())
                .Where(candidate => candidate != null && candidate.Validation.IsValid)
                .OrderBy(candidate => candidate.Difficulty.Score)
                .ThenBy(candidate => candidate.Layout.Seed)
                .ToArray();
            if (candidates.Length == 0) return Array.Empty<HexCastleStageLayout>();

            var ids = candidates.Select((candidate, index) =>
                    $"HEX_{ResolveThemeToken(candidate.Layout.Theme)}_STAGE_{startStageNumber + index:000}")
                .ToArray();
            PreflightStageIds(ids, rules);
            var result = new List<HexCastleStageLayout>(candidates.Length);
            for (var index = 0; index < candidates.Length; index++)
            {
                result.Add(SaveApproved(candidates[index], ids[index], rules, false));
            }
            return result;
        }

        public static HexCastleCatalog LoadOrCreateCatalog()
        {
            return LoadOrCreateCatalog(LoadRequiredRules());
        }

        public static HexCastleCatalog LoadOrCreateCatalog(HexCastleThemeOneRules rules)
        {
            EnsureStageApprovalReady(rules);
            EnsureFolder(Path.GetDirectoryName(CatalogPath)?.Replace('\\', '/'));
            var catalog = AssetDatabase.LoadAssetAtPath<HexCastleCatalog>(CatalogPath);
            if (catalog != null) return catalog;
            catalog = ScriptableObject.CreateInstance<HexCastleCatalog>();
            catalog.name = "HexCastleCatalog";
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }

        public static void PreflightStageIds(IEnumerable<string> stageIds)
        {
            PreflightStageIds(stageIds, LoadRequiredRules());
        }

        public static void PreflightStageIds(
            IEnumerable<string> stageIds,
            HexCastleThemeOneRules rules)
        {
            EnsureStageApprovalReady(rules);
            var ids = stageIds.Select(NormalizeStageId).ToArray();
            if (ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Length)
            {
                throw new InvalidOperationException("일괄 승인 범위 내 StageId가 중복됩니다.");
            }
            var catalog = LoadOrCreateCatalog(rules);
            var duplicates = ids.Where(id => catalog.Entries.Any(entry =>
                    string.Equals(entry.StageId, id, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            if (duplicates.Length > 0)
            {
                throw new InvalidOperationException($"이미 사용 중인 StageId: {string.Join(", ", duplicates)}");
            }
        }

        public static HexCastleCatalog LoadCatalog()
        {
            return AssetDatabase.LoadAssetAtPath<HexCastleCatalog>(CatalogPath);
        }

        public static void EnsureStageApprovalReady(HexCastleThemeOneRules rules)
        {
            if (rules == null)
            {
                throw new InvalidOperationException("Theme 1 Rules 자산이 없어 StageLayout을 승인할 수 없습니다.");
            }

            if (!rules.CanApproveStageLayout)
            {
                throw new InvalidOperationException(
                    $"Theme 1은 현재 {rules.Readiness} 상태입니다. " +
                    "시각 승인과 임시 밸런스 확정이 끝나 StageReady가 된 뒤에만 StageLayout/Catalog를 저장할 수 있습니다.");
            }
        }

        private static void ValidateCandidate(HexCastleCandidate candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (!candidate.Validation.IsValid)
            {
                throw new InvalidOperationException(string.Join("\n", candidate.Validation.Errors));
            }

            if (!HexCastleSilhouettePlanner.SupportedThemes.Contains(candidate.Layout.Theme) ||
                candidate.Layout.RulesVersion < HexCastleFoundationGenerator.FoundationRulesVersionBase)
            {
                throw new InvalidOperationException(
                    "현재 승인 경로는 정식 A~I Foundation 후보만 허용합니다.");
            }
        }

        private static string ResolveThemeToken(HexCastleTheme theme)
        {
            return theme == HexCastleTheme.CentralCompartment
                ? "T1"
                : $"T{HexCastleThemeCatalog.ResolveCode(theme)}";
        }

        private static HexCastleThemeOneRules LoadRequiredRules()
        {
            return HexCastleThemeOneRulesAssetUtility.Load() ??
                   throw new InvalidOperationException("Theme 1 Rules 자산을 찾지 못했습니다.");
        }

        private static string NormalizeStageId(string stageId)
        {
            stageId = (stageId ?? string.Empty).Trim().ToUpperInvariant();
            if (stageId.Length == 0 || !Regex.IsMatch(stageId, "^[A-Z0-9_\\-]+$"))
            {
                throw new ArgumentException("StageId는 영문 대문자, 숫자, _, -만 사용할 수 있습니다.", nameof(stageId));
            }
            return stageId;
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path)) return;
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }
}
