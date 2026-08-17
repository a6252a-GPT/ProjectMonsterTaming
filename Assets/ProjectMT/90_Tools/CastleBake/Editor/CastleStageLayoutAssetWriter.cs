using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectMT.Contents.CastleRaid.Generation;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.CastleBake
{
    public static class CastleStageLayoutAssetWriter // 승인 StageLayout의 중복·부분 저장 차단
    {
        public const string DefaultStageDraftRoot = "Assets/ProjectMT/98_Generated/CastleRaid/StageDrafts";

        public static CastleStageLayout Create(
            string outputRoot,
            string stageId,
            CastleGenerationCandidate candidate)
        {
            var created = CreateBatch(outputRoot, new[] { stageId }, new[] { candidate });
            return created[0];
        }

        public static IReadOnlyList<CastleStageLayout> CreateBatch(
            string outputRoot,
            IReadOnlyList<string> stageIds,
            IReadOnlyList<CastleGenerationCandidate> candidates)
        {
            if (stageIds == null)
            {
                throw new ArgumentNullException(nameof(stageIds));
            }

            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (stageIds.Count == 0 || stageIds.Count != candidates.Count)
            {
                throw new ArgumentException("StageId와 후보는 같은 수의 1개 이상 목록이어야 합니다.");
            }

            var normalizedRoot = NormalizeAssetRoot(outputRoot);
            CastleGenerationAssetFactory.EnsureFolder(normalizedRoot);
            var normalizedIds = new string[stageIds.Count];
            var paths = new string[stageIds.Count];
            var inputIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < stageIds.Count; index++)
            {
                var stageId = stageIds[index]?.Trim();
                if (string.IsNullOrWhiteSpace(stageId) || !inputIds.Add(stageId))
                {
                    throw new InvalidOperationException($"비어 있거나 중복된 StageId입니다: {stageId}");
                }

                var candidate = candidates[index];
                if (candidate == null || !candidate.Validation.IsValid || !candidate.Difficulty.HasClearPath)
                {
                    throw new InvalidOperationException($"검수를 통과하지 못한 후보는 저장할 수 없습니다: {stageId}");
                }

                normalizedIds[index] = stageId;
                paths[index] = ResolveLayoutPath(normalizedRoot, stageId, candidate);
                if (AssetDatabase.LoadAssetAtPath<CastleStageLayout>(paths[index]) != null)
                {
                    throw new InvalidOperationException($"같은 StageLayout 파일이 이미 있습니다: {paths[index]}");
                }
            }

            EnsureStageIdsAreUnique(normalizedRoot, inputIds);

            var created = new List<CastleStageLayout>(candidates.Count);
            try
            {
                for (var index = 0; index < candidates.Count; index++)
                {
                    var layout = ScriptableObject.CreateInstance<CastleStageLayout>();
                    layout.EditorStore(normalizedIds[index], candidates[index]);
                    AssetDatabase.CreateAsset(layout, paths[index]);
                    EditorUtility.SetDirty(layout);
                    created.Add(layout);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return created;
            }
            catch
            {
                foreach (var layout in created)
                {
                    var createdPath = AssetDatabase.GetAssetPath(layout);
                    if (!string.IsNullOrEmpty(createdPath))
                    {
                        AssetDatabase.DeleteAsset(createdPath); // 이번 호출의 생성분만 복구
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                throw;
            }
        }

        private static void EnsureStageIdsAreUnique(string outputRoot, ISet<string> requestedIds)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:CastleStageLayout", new[] { outputRoot }))
            {
                var layout = AssetDatabase.LoadAssetAtPath<CastleStageLayout>(AssetDatabase.GUIDToAssetPath(guid));
                if (layout != null && requestedIds.Contains(layout.StageId))
                {
                    throw new InvalidOperationException($"중복 StageId는 저장할 수 없습니다: {layout.StageId}");
                }
            }
        }

        private static string ResolveLayoutPath(
            string outputRoot,
            string stageId,
            CastleGenerationCandidate candidate)
        {
            var fileName = SanitizeFileName($"CastleStageLayout_{stageId}_Seed{candidate.Seed}") + ".asset";
            return outputRoot + "/" + fileName;
        }

        private static string NormalizeAssetRoot(string outputRoot)
        {
            var normalized = string.IsNullOrWhiteSpace(outputRoot)
                ? DefaultStageDraftRoot
                : outputRoot.Replace('\\', '/').TrimEnd('/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) || normalized.Contains(".."))
            {
                throw new InvalidOperationException("StageLayout 출력 경로는 Assets 아래의 상대 경로여야 합니다.");
            }

            return normalized;
        }

        private static string SanitizeFileName(string value)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Replace(' ', '_');
        }
    }
}
