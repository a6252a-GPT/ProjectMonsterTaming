using System;
using System.Collections.Generic;
using System.Linq;
using MoreMountains.Feedbacks;
using ProjectMT.Integrations.Feel;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectMT.EditorTools.MonsterMaker
{
    internal static class BasicAttackFeelPresetUtility // FEEL 데모 조합을 ProjectMT 프리셋으로 이식
    {
        internal const string PresetRoot = "Assets/ProjectMT/05_Art/FeelPresets/BasicAttack";
        internal const string ProductionPresetRoot = PresetRoot + "/Production";
        internal const string UserProfileRoot = PresetRoot + "/Profiles";
        internal const string ReferencePresetRoot = PresetRoot + "/Reference";
        internal const string FormalLabScenePath =
            "Assets/ProjectMT/00_Scenes/DEV_FEEL_BasicAttackImpactLab.unity";
        internal const string PlayerDemoPath =
            "Assets/ThirdParty/추가에셋2/Feel/MMFeedbacks/Demos/MMFeedbacksDemo/MMF_PlayerDemo.unity";
        private const string BarbarianEnemyPath =
            "Assets/ThirdParty/추가에셋2/Feel/FeelDemos/Barbarians/Prefabs/FeelBarbarianEnemy.prefab";
        private const string TacticalScenePath =
            "Assets/ThirdParty/추가에셋2/Feel/FeelDemos/Tactical/FeelTactical.unity";

        private static readonly ProductionPresetDefinition[] ProductionPresets =
        {
            new ProductionPresetDefinition(
                "직접 타격",
                "BAFeel_DirectHit.prefab"),
            new ProductionPresetDefinition(
                "횡베기 타격",
                "BAFeel_SweepHit.prefab"),
            new ProductionPresetDefinition(
                "관통 타격",
                "BAFeel_PierceHit.prefab"),
            new ProductionPresetDefinition(
                "내려찍기 타격",
                "BAFeel_SlamHit.prefab"),
            new ProductionPresetDefinition(
                "폭발 타격",
                "BAFeel_BlastHit.prefab"),
            new ProductionPresetDefinition(
                "연속 타격",
                "BAFeel_RapidHit.prefab"),
            new ProductionPresetDefinition(
                "파동 타격",
                "BAFeel_WaveHit.prefab")
        };

        private static readonly DemoPresetDefinition[] CombatDemoPresets =
        {
            new DemoPresetDefinition(
                "FEEL 원본 · Barbarian 피격",
                "BAFeel_FEEL5_BarbarianHit.prefab",
                BarbarianEnemyPath,
                "DamageFeedback",
                null,
                "MMF_Position"),
            new DemoPresetDefinition(
                "FEEL 원본 · Tactical 탄성 타격",
                "BAFeel_FEEL5_TacticalImpact.prefab",
                TacticalScenePath,
                "ShootFeedbacks",
                "TacticalUnit - Feedback Powered Floating Text - Channel 2/Tactical/ShootFeedbacks",
                "MMF_Scale",
                "MMF_RotationSpring",
                "MMF_SquashAndStretchSpring")
        };

        [MenuItem("Tools/ProjectMT/Monster Maker/FEEL 프리셋/MMF Player 데모 위치 보기")]
        internal static void PingPlayerDemo()
        {
            var demo = AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayerDemoPath);
            if (demo == null)
            {
                EditorUtility.DisplayDialog("FEEL 데모", $"데모 씬을 찾지 못했습니다.\n{PlayerDemoPath}", "확인");
                return;
            }

            Selection.activeObject = demo;
            EditorGUIUtility.PingObject(demo);
        }

        [MenuItem("Tools/ProjectMT/Monster Maker/FEEL 프리셋/전투 데모 프리셋 가져오기")]
        private static void ImportCombatDemoPresetsFromMenu()
        {
            var presets = ImportCombatDemoPresets();
            if (presets.Length == 0)
            {
                return;
            }

            Selection.objects = presets;
            EditorGUIUtility.PingObject(presets[0]);
            EditorUtility.DisplayDialog(
                "FEEL 전투 프리셋",
                $"FEEL 5.9.1 데모 조합 {presets.Length}종을 ProjectMT 프리셋으로 가져왔습니다.\n" +
                "VFX·Audio·Cinemachine·Hit Stop·후처리는 분리했습니다.",
                "확인");
        }

        internal static FeelProfileOption[] LoadFeelProfileOptions(GameObject current = null)
        {
            var options = new List<FeelProfileOption>
            {
                new FeelProfileOption(null, "없음")
            };
            var added = new HashSet<GameObject>();

            foreach (var definition in ProductionPresets)
            {
                var profile = AssetDatabase.LoadAssetAtPath<GameObject>(definition.OutputPath);
                AddProfileOption(options, added, profile, $"기본 제공 · {definition.DisplayName}");
            }

            AddFolderProfiles(options, added, UserProfileRoot, "내 프로필");
            AddFolderProfiles(options, added, PresetRoot, "참고 프로필", false);

            if (current != null && !added.Contains(current))
            {
                AddProfileOption(options, added, current, $"기존 연결 · {ProfileDisplayName(current)}");
            }
            return options.ToArray();
        }

        private static void AddFolderProfiles(
            ICollection<FeelProfileOption> options,
            ISet<GameObject> added,
            string root,
            string group,
            bool includeSubfolders = true)
        {
            if (!AssetDatabase.IsValidFolder(root))
            {
                return;
            }

            var profiles = AssetDatabase.FindAssets("t:Prefab", new[] { root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => includeSubfolders ||
                               string.Equals(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'), root, StringComparison.Ordinal))
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(IsValidFeelProfile)
                .OrderBy(ProfileDisplayName, StringComparer.Ordinal);
            foreach (var profile in profiles)
            {
                AddProfileOption(options, added, profile, $"{group} · {ProfileDisplayName(profile)}");
            }
        }

        private static void AddProfileOption(
            ICollection<FeelProfileOption> options,
            ISet<GameObject> added,
            GameObject profile,
            string label)
        {
            if (!IsValidFeelProfile(profile) || !added.Add(profile))
            {
                return;
            }
            options.Add(new FeelProfileOption(profile, label));
        }

        private static bool IsValidFeelProfile(GameObject profile)
        {
            if (profile == null)
            {
                return false;
            }
            var runtime = profile.GetComponent(typeof(IBasicAttackFeelRuntime)) as IBasicAttackFeelRuntime;
            return runtime?.IsBasicAttackFeelConfigured == true;
        }

        private static string ProfileDisplayName(GameObject profile)
        {
            if (profile == null)
            {
                return "없음";
            }
            return profile.name.StartsWith("BAFeel_", StringComparison.Ordinal)
                ? profile.name.Substring("BAFeel_".Length)
                : profile.name;
        }

        internal static void OpenFormalLab()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(FormalLabScenePath);
            if (scene == null)
            {
                EditorUtility.DisplayDialog("FEEL 타격감 연구실", $"정식 씬을 찾지 못했습니다.\n{FormalLabScenePath}", "확인");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            EditorSceneManager.OpenScene(FormalLabScenePath, OpenSceneMode.Single);
        }

        internal static GameObject[] ImportCombatDemoPresets()
        {
            EnsureFolder(ReferencePresetRoot);
            var imported = new List<GameObject>(CombatDemoPresets.Length);
            foreach (var definition in CombatDemoPresets)
            {
                var preset = ImportCombatDemoPreset(definition);
                if (preset != null)
                {
                    imported.Add(preset);
                }
            }

            AssetDatabase.SaveAssets();
            return imported.ToArray();
        }

        internal static GameObject[] LoadImportedCombatDemoPresets()
        {
            return CombatDemoPresets
                .Select(definition => AssetDatabase.LoadAssetAtPath<GameObject>(definition.OutputPath))
                .Where(prefab => prefab != null)
                .ToArray();
        }

        internal static string[] LoadImportedCombatDemoPresetLabels()
        {
            return CombatDemoPresets
                .Where(definition => AssetDatabase.LoadAssetAtPath<GameObject>(definition.OutputPath) != null)
                .Select(definition => definition.DisplayName)
                .ToArray();
        }

        private static GameObject ImportCombatDemoPreset(DemoPresetDefinition definition)
        {
            GameObject sourceRoot = null;
            Scene openedScene = default;
            var openedAdditively = false;
            try
            {
                MMF_Player sourcePlayer;
                if (definition.SourcePath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    sourceRoot = PrefabUtility.LoadPrefabContents(definition.SourcePath);
                    sourcePlayer = sourceRoot
                        .GetComponentsInChildren<MMF_Player>(true)
                        .FirstOrDefault(candidate => candidate.gameObject.name == definition.PlayerName);
                }
                else
                {
                    openedScene = SceneManager.GetSceneByPath(definition.SourcePath);
                    if (!openedScene.IsValid() || !openedScene.isLoaded)
                    {
                        openedScene = EditorSceneManager.OpenScene(
                            definition.SourcePath,
                            OpenSceneMode.Additive);
                        openedAdditively = true;
                    }

                    var sourceObject = FindSceneObject(openedScene, definition.HierarchyPath);
                    sourcePlayer = sourceObject != null ? sourceObject.GetComponent<MMF_Player>() : null;
                }

                if (sourcePlayer == null)
                {
                    Debug.LogError(
                        $"[BasicAttackFeelPresetUtility] FEEL 데모 원본을 찾지 못했습니다. " +
                        $"Source={definition.SourcePath}, Player={definition.PlayerName}");
                    return null;
                }

                return SaveFilteredPlayer(definition, sourcePlayer);
            }
            finally
            {
                if (sourceRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(sourceRoot);
                }
                if (openedAdditively && openedScene.IsValid() && openedScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(openedScene, true);
                }
            }
        }

        private static GameObject SaveFilteredPlayer(
            DemoPresetDefinition definition,
            MMF_Player sourcePlayer)
        {
            var root = UnityEngine.Object.Instantiate(sourcePlayer.gameObject);
            root.name = System.IO.Path.GetFileNameWithoutExtension(definition.OutputFileName);
            try
            {
                root.transform.SetParent(null);
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                root.hideFlags = HideFlags.None;

                foreach (var child in root.transform.Cast<Transform>().ToArray())
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }

                var player = root.GetComponent<MMF_Player>();
                foreach (var component in root.GetComponents<Component>())
                {
                    if (component != null && !(component is Transform) && component != player)
                    {
                        UnityEngine.Object.DestroyImmediate(component);
                    }
                }

                var allowedTypes = new HashSet<string>(definition.AllowedFeedbackTypes);
                var copiedFeedbacks = player.FeedbacksList?
                    .Where(feedback => feedback != null && allowedTypes.Contains(feedback.GetType().Name))
                    .ToList() ?? new List<MMF_Feedback>();
                if (copiedFeedbacks.Count == 0)
                {
                    Debug.LogError(
                        $"[BasicAttackFeelPresetUtility] 가져올 FEEL 항목이 없습니다. " +
                        $"Preset={definition.DisplayName}");
                    return null;
                }

                var targetReference = new MMF_ReferenceHolder
                {
                    Label = "Runtime Target",
                    ForceReferenceOnAll = true
                };
                copiedFeedbacks.Insert(0, targetReference);
                player.FeedbacksList = copiedFeedbacks;
                player.AutoPlayOnEnable = false;
                player.AutoPlayOnStart = false;
                player.AutoInitialization = false;
                player.InitializationMode = MMFeedbacks.InitializationModes.Script;
                player.StopFeedbacksOnDisable = true;
                player.RestoreInitialValuesOnDisable = true;

                var adapter = root.AddComponent<BasicAttackFeelRuntimeAdapter>();
                adapter.EditorConfigure(player, targetReference);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, definition.OutputPath);
                if (prefab != null)
                {
                    Debug.Log(
                        $"[BasicAttackFeelPresetUtility] FEEL 데모 프리셋 가져오기 완료: " +
                        $"{definition.DisplayName} -> {definition.OutputPath}");
                }
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject FindSceneObject(Scene scene, string hierarchyPath)
        {
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(hierarchyPath))
            {
                return null;
            }

            var segments = hierarchyPath.Split('/');
            var root = scene.GetRootGameObjects()
                .FirstOrDefault(candidate => candidate.name == segments[0]);
            if (root == null)
            {
                return null;
            }

            var current = root.transform;
            for (var index = 1; index < segments.Length; index++)
            {
                current = current.Find(segments[index]);
                if (current == null)
                {
                    return null;
                }
            }
            return current.gameObject;
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }
                current = next;
            }
        }

        private sealed class ProductionPresetDefinition
        {
            internal ProductionPresetDefinition(
                string displayName,
                string outputFileName)
            {
                DisplayName = displayName;
                OutputFileName = outputFileName;
            }

            internal string DisplayName { get; }
            internal string OutputFileName { get; }
            internal string OutputPath => $"{ProductionPresetRoot}/{OutputFileName}";
        }

        private sealed class DemoPresetDefinition
        {
            internal DemoPresetDefinition(
                string displayName,
                string outputFileName,
                string sourcePath,
                string playerName,
                string hierarchyPath,
                params string[] allowedFeedbackTypes)
            {
                DisplayName = displayName;
                OutputFileName = outputFileName;
                SourcePath = sourcePath;
                PlayerName = playerName;
                HierarchyPath = hierarchyPath;
                AllowedFeedbackTypes = allowedFeedbackTypes;
            }

            internal string DisplayName { get; }
            internal string OutputFileName { get; }
            internal string OutputPath => $"{ReferencePresetRoot}/{OutputFileName}";
            internal string SourcePath { get; }
            internal string PlayerName { get; }
            internal string HierarchyPath { get; }
            internal string[] AllowedFeedbackTypes { get; }
        }

        internal readonly struct FeelProfileOption
        {
            internal FeelProfileOption(GameObject profile, string label)
            {
                Profile = profile;
                Label = label;
            }

            internal GameObject Profile { get; }
            internal string Label { get; }
        }
    }
}
