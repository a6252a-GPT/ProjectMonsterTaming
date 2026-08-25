using System;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public static class HexCastleDerivedWallPrefabSetupUtility // 파생 양면 성벽·성문 FBX를 Scale 1 순수 Visual Prefab으로 정식화한다
    {
        private const string ModelRoot =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Art/Derived/KayKitDoubleSided/Models/";
        private const string PrefabRoot =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Art/Derived/KayKitDoubleSided/Prefabs/";
        private const string MaterialPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Art/Materials/MAT_CRHex_KayKitWall_Spring.mat";
        private const float StubTowerJoinRadius = 0.4f;

        private sealed class WallDefinition
        {
            public string Name;
            public string ModelPath;
            public string PrefabPath;
            public HexCastleWallVisualKind Kind;
            public int StartDirection;
            public int EndDirection;
            public bool IsStub;
        }

        private static readonly WallDefinition[] Definitions =
        {
            new WallDefinition
            {
                Name = "PF_CRHex_WallStraight_DoubleSided",
                ModelPath = ModelRoot + "SM_CRHex_WallStraight_DoubleSided.fbx",
                PrefabPath = PrefabRoot + "PF_CRHex_WallStraight_DoubleSided.prefab",
                Kind = HexCastleWallVisualKind.Straight,
                StartDirection = 3,
                EndDirection = 0
            },
            new WallDefinition
            {
                Name = "PF_CRHex_WallCornerA_DoubleSided",
                ModelPath = ModelRoot + "SM_CRHex_WallCornerA_DoubleSided.fbx",
                PrefabPath = PrefabRoot + "PF_CRHex_WallCornerA_DoubleSided.prefab",
                Kind = HexCastleWallVisualKind.CornerAOutside,
                StartDirection = 3,
                EndDirection = 5
            },
            new WallDefinition
            {
                Name = "PF_CRHex_WallCornerB_DoubleSided",
                ModelPath = ModelRoot + "SM_CRHex_WallCornerB_DoubleSided.fbx",
                PrefabPath = PrefabRoot + "PF_CRHex_WallCornerB_DoubleSided.prefab",
                Kind = HexCastleWallVisualKind.CornerBOutside,
                StartDirection = 3,
                EndDirection = 4
            },
            new WallDefinition
            {
                Name = "PF_CRHex_WallStub_DoubleSided",
                ModelPath = ModelRoot + "SM_CRHex_WallStub_DoubleSided.fbx",
                PrefabPath = PrefabRoot + "PF_CRHex_WallStub_DoubleSided.prefab",
                StartDirection = 0,
                IsStub = true
            },
            new WallDefinition
            {
                Name = "PF_CRHex_Gate_Closed_DoubleSided",
                ModelPath = ModelRoot + "Gates/SM_CRHex_Gate_Closed_DoubleSided.fbx",
                PrefabPath = PrefabRoot + "PF_CRHex_Gate_Closed_DoubleSided.prefab",
                Kind = HexCastleWallVisualKind.StraightGate,
                StartDirection = 3,
                EndDirection = 0
            },
            new WallDefinition
            {
                Name = "PF_CRHex_Gate_Open_DoubleSided",
                ModelPath = ModelRoot + "Gates/SM_CRHex_Gate_Open_DoubleSided.fbx",
                PrefabPath = PrefabRoot + "PF_CRHex_Gate_Open_DoubleSided.prefab",
                Kind = HexCastleWallVisualKind.StraightGate,
                StartDirection = 3,
                EndDirection = 0
            }
        };

        [MenuItem("JC Tool/Castle Raid Hex/Derived Wall/Rebuild Scale 1 Visual Prefabs")]
        public static void RebuildAll()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                throw new InvalidOperationException($"성벽 Material이 없습니다: {MaterialPath}");
            }

            foreach (var definition in Definitions)
            {
                ConfigureImporter(definition.ModelPath);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var definition in Definitions)
            {
                CreateOrUpdatePrefab(definition, material);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Hex Derived Wall] 성벽 4종 + 닫힘/열림 성문 Scale 1 Visual Prefab 정식화 완료");
        }

        private static void ConfigureImporter(string modelPath)
        {
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"파생 성벽 FBX Importer가 없습니다: {modelPath}");
            }

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.weldVertices = true;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static void CreateOrUpdatePrefab(WallDefinition definition, Material material)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(definition.ModelPath);
            if (model == null)
            {
                throw new InvalidOperationException($"파생 성벽 FBX가 없습니다: {definition.ModelPath}");
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(definition.PrefabPath) == null)
            {
                var temporaryRoot = new GameObject(definition.Name);
                try
                {
                    var visual = PrefabUtility.InstantiatePrefab(model) as GameObject;
                    if (visual == null)
                    {
                        throw new InvalidOperationException($"파생 성벽 모델 생성 실패: {definition.ModelPath}");
                    }

                    visual.name = "Visual";
                    visual.transform.SetParent(temporaryRoot.transform, false);
                    PrefabUtility.SaveAsPrefabAsset(temporaryRoot, definition.PrefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(temporaryRoot);
                }
            }

            var root = PrefabUtility.LoadPrefabContents(definition.PrefabPath);
            try
            {
                RemoveMissingScriptsRecursively(root);
                root.name = definition.Name;
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    throw new InvalidOperationException($"{definition.Name}에 Renderer가 없습니다.");
                }

                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterials = Enumerable
                        .Repeat(material, renderer.sharedMaterials.Length)
                        .ToArray();
                }

                RemoveDirectChild(root.transform, "Socket_Start");
                RemoveDirectChild(root.transform, "Socket_End");
                RemoveDirectChild(root.transform, "Socket_Edge");
                RemoveDirectChild(root.transform, "Socket_Tower");
                var bounds = CalculateLocalBounds(root.transform, renderers);
                if (definition.IsStub)
                {
                    var wallModule = root.GetComponent<HexCastleWallVisualModule>();
                    if (wallModule != null)
                    {
                        Object.DestroyImmediate(wallModule, true);
                    }

                    var edgeSocket = CreateSocket(
                        "Socket_Edge",
                        root.transform,
                        HexSpatialContract.GetEdgeMidpoint(definition.StartDirection));
                    var direction = HexSpatialContract.ToWorld(
                        HexCoordinates.Directions[definition.StartDirection]).normalized;
                    var towerSocket = CreateSocket(
                        "Socket_Tower",
                        root.transform,
                        direction * StubTowerJoinRadius);
                    var module = root.GetComponent<HexCastleWallStubVisualModule>() ??
                                 root.AddComponent<HexCastleWallStubVisualModule>();
                    module.EditorConfigure(
                        definition.StartDirection,
                        StubTowerJoinRadius,
                        bounds,
                        edgeSocket,
                        towerSocket,
                        renderers);
                }
                else
                {
                    var stubModule = root.GetComponent<HexCastleWallStubVisualModule>();
                    if (stubModule != null)
                    {
                        Object.DestroyImmediate(stubModule, true);
                    }

                    var startSocket = CreateSocket(
                        "Socket_Start",
                        root.transform,
                        HexSpatialContract.GetEdgeMidpoint(definition.StartDirection));
                    var endSocket = CreateSocket(
                        "Socket_End",
                        root.transform,
                        HexSpatialContract.GetEdgeMidpoint(definition.EndDirection));
                    var module = root.GetComponent<HexCastleWallVisualModule>() ??
                                 root.AddComponent<HexCastleWallVisualModule>();
                    module.EditorConfigure(
                        definition.Kind,
                        definition.StartDirection,
                        definition.EndDirection,
                        bounds,
                        startSocket,
                        endSocket,
                        renderers);
                }

                ValidatePureVisual(root);
                PrefabUtility.SaveAsPrefabAsset(root, definition.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform CreateSocket(string name, Transform parent, Vector3 localPosition)
        {
            var socket = new GameObject(name).transform;
            socket.SetParent(parent, false);
            socket.localPosition = localPosition;
            socket.localRotation = Quaternion.identity;
            socket.localScale = Vector3.one;
            return socket;
        }

        private static void RemoveDirectChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null && child.parent == parent)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void RemoveMissingScriptsRecursively(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
            }
        }

        private static Bounds CalculateLocalBounds(Transform root, Renderer[] renderers)
        {
            var initialized = false;
            var result = new Bounds();
            foreach (var renderer in renderers)
            {
                var bounds = renderer.bounds;
                var minimum = bounds.min;
                var maximum = bounds.max;
                for (var x = 0; x <= 1; x++)
                {
                    for (var y = 0; y <= 1; y++)
                    {
                        for (var z = 0; z <= 1; z++)
                        {
                            var world = new Vector3(
                                x == 0 ? minimum.x : maximum.x,
                                y == 0 ? minimum.y : maximum.y,
                                z == 0 ? minimum.z : maximum.z);
                            var local = root.InverseTransformPoint(world);
                            if (!initialized)
                            {
                                result = new Bounds(local, Vector3.zero);
                                initialized = true;
                            }
                            else
                            {
                                result.Encapsulate(local);
                            }
                        }
                    }
                }
            }

            return result;
        }

        private static void ValidatePureVisual(GameObject root)
        {
            if (root.transform.localScale != Vector3.one ||
                root.GetComponentInChildren<HealthComponent>(true) != null ||
                root.GetComponentInChildren<Collider>(true) != null ||
                root.GetComponentInChildren<NavMeshObstacle>(true) != null ||
                root.GetComponentInChildren<HexCastleCellRuntime>(true) != null)
            {
                throw new InvalidOperationException($"{root.name}에 Scale 1 또는 순수 Visual 계약 위반이 있습니다.");
            }

            var wall = root.GetComponent<HexCastleWallVisualModule>();
            var stub = root.GetComponent<HexCastleWallStubVisualModule>();
            if ((wall == null) == (stub == null) ||
                wall != null && !wall.HasValidSocketContract() ||
                stub != null && !stub.HasValidSocketContract())
            {
                throw new InvalidOperationException($"{root.name}의 성벽 Socket 계약이 잘못됐습니다.");
            }
        }
    }
}
