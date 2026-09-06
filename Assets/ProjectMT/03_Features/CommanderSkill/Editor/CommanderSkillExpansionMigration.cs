using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectMT.Shared.CommanderSkill;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill.Editor
{
    public static class CommanderSkillExpansionMigration
    {
        private const string Root = "Assets/ProjectMT/03_Features/CommanderSkill/Resources/CommanderSkills";
        private const string CatalogPath = Root + "/CommanderSkillCatalog.asset";

        [MenuItem("Tools/ProjectMT/Commander Skill/Migrate Approved Support Four")]
        public static void Migrate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CommanderSkillCatalog>(CatalogPath);
            if (catalog == null || !catalog.TryValidate(out var validation))
                throw new InvalidOperationException("군단장 Catalog 검증 실패");
            var sources = catalog.Skills.Where(s => s != null && CommanderSkillSupportAuthoring.IsSupportId(s.SkillId)).ToArray();
            if (sources.Length != 4) throw new InvalidOperationException("효과형 전환 대상 4종이 모두 필요합니다.");
            if (sources.All(s => s is CommanderEffectSkillDefinition)) return;
            if (sources.Any(s => s is not CommanderAttackSkillDefinition))
                throw new InvalidOperationException("부분 전환 상태입니다. 기존 작업을 덮어쓰지 않습니다.");
            var originals = sources.Select(AssetDatabase.GetAssetPath).ToArray();
            var sourceSet = new HashSet<string>(originals, StringComparer.Ordinal);
            var protectedPaths = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) &&
                (p.EndsWith(".asset") || p.EndsWith(".prefab") || p.EndsWith(".unity")) &&
                p != CatalogPath && !sourceSet.Contains(p));
            foreach (var path in protectedPaths)
                if (AssetDatabase.GetDependencies(path, false).Any(sourceSet.Contains))
                    throw new InvalidOperationException("별도 참조자 확인이 필요합니다: " + path);

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var backupDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../..",
                "ProjectMT 개인파일/Backups/CommanderSkillExpansion_" + stamp));
            Directory.CreateDirectory(backupDirectory);
            var backupPaths = originals.Concat(new[] { CatalogPath, AssetDatabase.GetAssetPath(catalog.BalanceConfig),
                AssetDatabase.GetAssetPath(catalog.SummonConfig) })
                .Concat(AssetDatabase.FindAssets("t:CommanderMarkEffectDefinition", new[] { Root })
                    .Select(AssetDatabase.GUIDToAssetPath)).Distinct().ToArray();
            AssetDatabase.ExportPackage(backupPaths, Path.Combine(backupDirectory, "BeforeSupportFour.unitypackage"),
                ExportPackageOptions.Default);
            var staging = Root + "/SupportMigration_" + stamp;
            if (AssetDatabase.IsValidFolder(staging)) throw new InvalidOperationException("이관 경로가 이미 존재합니다.");
            AssetDatabase.CreateFolder(Root, "SupportMigration_" + stamp);
            var previousDefinitions = catalog.Skills.ToArray();
            var balanceJson = EditorJsonUtility.ToJson(catalog.BalanceConfig);
            var owned = new List<ScriptableObject>();
            var replacements = new CommanderEffectSkillDefinition[4];
            var oldPaths = new string[4];
            var newPaths = new string[4];
            var committed = false;
            try
            {
                for (var index = 0; index < sources.Length; index++)
                {
                    var local = new List<ScriptableObject>();
                    try { replacements[index] = CommanderSkillSupportAuthoring.Create(sources[index], local); }
                    finally { owned.AddRange(local); }
                    newPaths[index] = staging + "/New_" + sources[index].SkillId + ".asset";
                    AssetDatabase.CreateAsset(replacements[index], newPaths[index]);
                    foreach (var subasset in local.Where(o => o != replacements[index]))
                    {
                        subasset.hideFlags = HideFlags.HideInHierarchy;
                        AssetDatabase.AddObjectToAsset(subasset, replacements[index]);
                    }
                    AssetDatabase.SaveAssetIfDirty(replacements[index]);
                }
                var definitions = previousDefinitions.Select(s =>
                {
                    var index = Array.IndexOf(sources, s);
                    return index < 0 ? s : replacements[index];
                }).ToArray();
                catalog.EditorConfigure(catalog.BalanceConfig, catalog.SummonConfig, definitions);
                MigrateGrowth(catalog.BalanceConfig);
                if (!catalog.TryValidate(out validation)) throw new InvalidOperationException(validation);
                for (var index = 0; index < sources.Length; index++)
                {
                    oldPaths[index] = staging + "/Old_" + sources[index].SkillId + ".asset";
                    Move(originals[index], oldPaths[index]);
                    Move(newPaths[index], originals[index]);
                }
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog.BalanceConfig);
                AssetDatabase.SaveAssetIfDirty(catalog);
                if (!catalog.TryValidate(out validation)) throw new InvalidOperationException(validation);
                committed = true;
            }
            finally
            {
                if (!committed)
                {
                    for (var index = 0; index < sources.Length; index++)
                    {
                        if (replacements[index] != null && AssetDatabase.GetAssetPath(replacements[index]) == originals[index])
                            Move(originals[index], newPaths[index]);
                        if (!string.IsNullOrEmpty(oldPaths[index]) && AssetDatabase.LoadMainAssetAtPath(oldPaths[index]) != null)
                            Move(oldPaths[index], originals[index]);
                    }
                    catalog.EditorConfigure(catalog.BalanceConfig, catalog.SummonConfig, previousDefinitions);
                    EditorJsonUtility.FromJsonOverwrite(balanceJson, catalog.BalanceConfig);
                    EditorUtility.SetDirty(catalog.BalanceConfig);
                    EditorUtility.SetDirty(catalog);
                    AssetDatabase.SaveAssetIfDirty(catalog.BalanceConfig);
                    AssetDatabase.SaveAssetIfDirty(catalog);
                }
                foreach (var value in owned)
                    if (value != null && !AssetDatabase.Contains(value)) UnityEngine.Object.DestroyImmediate(value);
                AssetDatabase.DeleteAsset(staging); // 이 실행이 만든 경로만 정리. 이전 자산은 외부 패키지에 보존
            }
            Debug.Log("COMMANDER_SUPPORT_FOUR_MIGRATED backup=" + backupDirectory);
        }

        public static void MigrateGrowth(CommanderSkillBalanceConfig balance)
        {
            foreach (var rule in balance.SkillRules)
            {
                var ratio = rule.CopyRatioCurve() ?? AnimationCurve.Linear(1f, 1f, rule.MaxLevel, 1.5f);
                var control = rule.CopyControlCurve() ?? AnimationCurve.Linear(1f, 1f, rule.MaxLevel, 1.25f);
                rule.SetSupportCurves(ratio, control);
            }
            EditorUtility.SetDirty(balance);
        }

        private static void Move(string source, string destination)
        {
            var error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
    }
}
