using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public static class HexCastleStageBaker
    {
        public const string PrefabFolder =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Prefabs/Baked";
        public const string AssetFolder =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data/Baked";

        [MenuItem("JC Tool/군단의 역습 육각/정식 A-I 승인 Stage Bake")]
        public static void BakeApprovedFormalThemes()
        {
            var rules = HexCastleThemeOneRulesAssetUtility.Load();
            HexCastleAssetWriter.EnsureStageApprovalReady(rules);
            var catalog = HexCastleAssetWriter.LoadCatalog() ??
                          throw new InvalidOperationException("육각 성 승인 Catalog가 없습니다.");
            var layouts = catalog.Entries
                .Where(entry => entry.Layout != null &&
                                HexCastleSilhouettePlanner.SupportedThemes.Contains(entry.Theme) &&
                                entry.DefenseLayerCount >= 2 && entry.DefenseLayerCount <= 4)
                .OrderBy(entry => entry.Theme)
                .ThenBy(entry => entry.Layout.Seed)
                .ThenBy(entry => entry.DefenseLayerCount)
                .Select(entry => entry.Layout)
                .ToArray();
            if (layouts.Length == 0)
            {
                throw new InvalidOperationException("Bake할 정식 A~I 승인 Layout이 없습니다.");
            }

            var scene = SceneManager.GetActiveScene();
            var wasDirty = scene.isDirty;
            var previousSelection = Selection.objects;
            EnsureFolder(PrefabFolder);
            EnsureFolder(AssetFolder);
            try
            {
                foreach (var layout in layouts)
                {
                    Bake(layout, rules, catalog);
                }

                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log($"[Hex Formal Theme Bake] {layouts.Length}개 Stage Prefab 생성 완료");
            }
            finally
            {
                HexCastleFoundationVisualGate.Remove(scene);
                Selection.objects = previousSelection.Where(value => value != null).ToArray();
                if (!wasDirty && scene.isDirty)
                {
                    typeof(EditorSceneManager)
                        .GetMethod("ClearSceneDirtiness", System.Reflection.BindingFlags.Static |
                                                          System.Reflection.BindingFlags.NonPublic)
                        ?.Invoke(null, new object[] { scene });
                }
            }
        }

        private static void Bake(
            HexCastleStageLayout layout,
            HexCastleThemeOneRules rules,
            HexCastleCatalog catalog)
        {
            var regenerated = new HexCastleGenerationPipeline().GenerateFoundation(
                layout.Seed,
                layout.DefenseLayerCount,
                layout.Theme,
                rules.Tuning);
            if (!regenerated.Validation.IsValid ||
                !string.Equals(regenerated.Layout.LayoutSignature, layout.LayoutSignature, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{layout.StageId} 승인 Layout 재생성 Hash가 다릅니다.");
            }

            HexCastleFoundationVisualGate.Create(
                layout.Seed,
                layout.DefenseLayerCount,
                layout.Theme,
                rules.Tuning,
                false);
            var source = GameObject.Find(HexCastleFoundationVisualGate.RootName) ??
                         throw new InvalidOperationException("정식 육각 성 조립 Root를 찾지 못했습니다.");
            var previewScene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = Object.Instantiate(source);
                var themeCode = HexCastleThemeCatalog.ResolveCode(layout.Theme);
                var themeToken = layout.Theme == HexCastleTheme.CentralCompartment
                    ? "T1"
                    : $"T{themeCode}";
                root.name = $"PF_CRHex_Stage_{themeToken}_{layout.DefenseLayerCount}W_{layout.Seed}";
                SceneManager.MoveGameObjectToScene(root, previewScene);
                PreparePersistentRoot(root);

                var themeFolder = layout.Theme == HexCastleTheme.CentralCompartment
                    ? "Theme1"
                    : $"Theme{themeCode}";
                var stageAssetFolder = $"{AssetFolder}/{themeFolder}/{layout.StageId}";
                if (AssetDatabase.IsValidFolder(stageAssetFolder))
                {
                    AssetDatabase.DeleteAsset(stageAssetFolder);
                }
                EnsureFolder(stageAssetFolder);
                PersistTransientResources(root, stageAssetFolder);

                var bounds = ResolveBounds(root);
                var bakedStage = root.AddComponent<HexCastleBakedStage>();
                bakedStage.EditorConfigure(layout, bounds);
                var prefabPath = $"{PrefabFolder}/{root.name}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException($"{layout.StageId} Stage Prefab 저장에 실패했습니다.");
                }

                catalog.Upsert(layout.StageId, layout, prefab);
            }
            finally
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void PreparePersistentRoot(GameObject root)
        {
            var previewState = root.GetComponent<HexCastleGenerationScenePreviewState>();
            if (previewState != null)
            {
                Object.DestroyImmediate(previewState);
            }

            var monsterScale = root.transform.Find("02_ActualMonsterScale");
            if (monsterScale != null)
            {
                Object.DestroyImmediate(monsterScale.gameObject);
            }

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.hideFlags = HideFlags.None;
            }
        }

        private static void PersistTransientResources(GameObject root, string folder)
        {
            var meshReferences = root.GetComponentsInChildren<MeshFilter>(true)
                .Select(value => value.sharedMesh)
                .Concat(root.GetComponentsInChildren<MeshCollider>(true).Select(value => value.sharedMesh))
                .Where(value => value != null && !AssetDatabase.Contains(value))
                .Distinct()
                .ToArray();
            var persistentMeshes = new Dictionary<Mesh, Mesh>();
            foreach (var mesh in meshReferences)
            {
                var clone = Object.Instantiate(mesh);
                clone.name = mesh.name;
                AssetDatabase.CreateAsset(clone, $"{folder}/{Sanitize(mesh.name)}.asset");
                persistentMeshes.Add(mesh, clone);
            }

            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null && persistentMeshes.TryGetValue(filter.sharedMesh, out var mesh))
                {
                    filter.sharedMesh = mesh;
                }
            }
            foreach (var collider in root.GetComponentsInChildren<MeshCollider>(true))
            {
                if (collider.sharedMesh != null && persistentMeshes.TryGetValue(collider.sharedMesh, out var mesh))
                {
                    collider.sharedMesh = mesh;
                }
            }

            var materials = root.GetComponentsInChildren<Renderer>(true)
                .SelectMany(value => value.sharedMaterials)
                .Where(value => value != null && !AssetDatabase.Contains(value))
                .Distinct()
                .ToArray();
            var persistentMaterials = new Dictionary<Material, Material>();
            foreach (var material in materials)
            {
                var clone = new Material(material) { name = material.name };
                AssetDatabase.CreateAsset(clone, $"{folder}/{Sanitize(material.name)}.mat");
                persistentMaterials.Add(material, clone);
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var rendererMaterials = renderer.sharedMaterials;
                var changed = false;
                for (var index = 0; index < rendererMaterials.Length; index++)
                {
                    if (rendererMaterials[index] != null &&
                        persistentMaterials.TryGetValue(rendererMaterials[index], out var material))
                    {
                        rendererMaterials[index] = material;
                        changed = true;
                    }
                }
                if (changed)
                {
                    renderer.sharedMaterials = rendererMaterials;
                }
            }
        }

        private static Bounds ResolveBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.zero);
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }
            return bounds;
        }

        private static string Sanitize(string value)
        {
            return string.Concat((value ?? "Asset").Select(character =>
                Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var separator = path.LastIndexOf('/');
            if (separator <= 0) throw new InvalidOperationException($"잘못된 폴더 경로: {path}");
            var parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
