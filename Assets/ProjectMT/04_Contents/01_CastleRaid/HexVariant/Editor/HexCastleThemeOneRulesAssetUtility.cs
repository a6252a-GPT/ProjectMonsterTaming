using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public static class HexCastleThemeOneRulesAssetUtility
    {
        private static readonly int[] RuntimeSeedPoolValues = { 10801, 10802, 10803 };

        public const string AssetPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data/Foundation/HexCastleTheme1Rules.asset";
        public const string LegacyAssetPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data/Foundation/HexCastleTheme1DraftRules.asset";

        public static IReadOnlyList<int> RuntimeSeedPool => RuntimeSeedPoolValues;

        public static HexCastleThemeOneRules Load()
        {
            return AssetDatabase.LoadAssetAtPath<HexCastleThemeOneRules>(AssetPath) ??
                   AssetDatabase.LoadAssetAtPath<HexCastleThemeOneRules>(LegacyAssetPath);
        }

        public static HexCastleThemeOneRules LoadOrCreate()
        {
            var existing = Load();
            if (existing != null)
            {
                MoveLegacyAssetIfNeeded(existing);
                return existing;
            }

            EnsureFolder("Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data");
            EnsureFolder("Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data/Foundation");
            var rules = ScriptableObject.CreateInstance<HexCastleThemeOneRules>();
            rules.ResetToDraftDefaults();
            AssetDatabase.CreateAsset(rules, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            return rules;
        }

        public static HexCastleThemeOneRules ApproveForStageGeneration()
        {
            var rules = LoadOrCreate();
            rules.Tuning.EditorApplyFormalizedGarrisonRules();
            rules.Tuning.Validate(2);
            rules.Tuning.Validate(3);
            rules.Tuning.Validate(4);
            rules.EditorSetReadiness(HexCastleThemeOneReadiness.StageReady);
            EditorUtility.SetDirty(rules);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            return rules;
        }

        [MenuItem("JC Tool/군단의 역습 육각/Theme 1 V1 승인 생성")]
        public static void ApproveThemeOneVersionOne()
        {
            var rules = ApproveForStageGeneration();
            var pipeline = new HexCastleGenerationPipeline();
            var approved = RuntimeSeedPool
                .SelectMany(seed => new[] { 2, 3, 4 }.Select(layers => pipeline.GenerateFoundation(
                    seed,
                    layers,
                    HexCastleTheme.CentralCompartment,
                    rules.Tuning)))
                .Select(candidate => HexCastleAssetWriter.SaveApproved(
                    candidate,
                    $"HEX_T1_{candidate.Layout.DefenseLayerCount}W_{candidate.Layout.Seed}",
                    rules,
                    true))
                .ToArray();

            Selection.activeObject = HexCastleAssetWriter.LoadCatalog();
            Debug.Log(
                $"[Hex Theme 1 승인] StageReady · Seed {string.Join(", ", RuntimeSeedPool)} · " +
                $"{string.Join(", ", approved.Select(value => value.StageId))}");
        }

        [MenuItem("JC Tool/군단의 역습 육각/정식 A-I 승인 Layout 생성")]
        public static void ApproveAllFormalThemes()
        {
            var rules = ApproveForStageGeneration();
            var pipeline = new HexCastleGenerationPipeline();
            var approved = HexCastleThemeCatalog.Themes
                .SelectMany(theme =>
                    (theme == HexCastleTheme.CentralCompartment
                            ? RuntimeSeedPool
                            : new[] { RuntimeSeedPool[0] })
                    .SelectMany(seed => new[] { 2, 3, 4 }.Select(layers =>
                        pipeline.GenerateFoundation(seed, layers, theme, rules.Tuning))))
                .Select(candidate =>
                {
                    var themeToken = candidate.Layout.Theme == HexCastleTheme.CentralCompartment
                        ? "T1"
                        : $"T{HexCastleThemeCatalog.ResolveCode(candidate.Layout.Theme)}";
                    return HexCastleAssetWriter.SaveApproved(
                        candidate,
                        $"HEX_{themeToken}_{candidate.Layout.DefenseLayerCount}W_{candidate.Layout.Seed}",
                        rules,
                        true);
                })
                .ToArray();

            Selection.activeObject = HexCastleAssetWriter.LoadCatalog();
            Debug.Log($"[Hex Formal Theme 승인] A~I Layout {approved.Length}개 생성 완료");
        }

        [MenuItem("JC Tool/군단의 역습 육각/Theme 1 Rules 선택")]
        public static void SelectRules()
        {
            var rules = LoadOrCreate();
            Selection.activeObject = rules;
            EditorGUIUtility.PingObject(rules);
        }

        private static void MoveLegacyAssetIfNeeded(HexCastleThemeOneRules rules)
        {
            var currentPath = AssetDatabase.GetAssetPath(rules);
            if (!string.Equals(currentPath, LegacyAssetPath, StringComparison.Ordinal) ||
                AssetDatabase.LoadMainAssetAtPath(AssetPath) != null)
            {
                return;
            }

            var error = AssetDatabase.MoveAsset(LegacyAssetPath, AssetPath);
            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException($"Theme 1 Rules 자산 정식 이름 변경에 실패했습니다: {error}");
            }

            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var separator = path.LastIndexOf('/');
            if (separator <= 0)
            {
                throw new InvalidOperationException($"Asset 폴더 경로가 잘못됐습니다: {path}");
            }

            var parent = path.Substring(0, separator);
            var name = path.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
