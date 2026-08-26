using System;
using UnityEngine;
using ProjectMT.Contents.TreasureSpirit;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    [DisallowMultipleComponent]
    public sealed class BakedDungeonLoader : MonoBehaviour
    {
        [Header("베이크된 던전 프리팹")]
        [SerializeField] private GameObject[] dungeonMapPrefabs;

        [Header("맵 순환 (1→2→3→4→5→1)")]
        [SerializeField] private int startMapIndex;

        [Header("룸 마커 스폰 (Chest_pt / Guard_pt)")]
        [SerializeField] private GameObject chestPrefab;
        [SerializeField] private GameObject mimicPrefab;
        [SerializeField] private GameObject guardPrefab;
        [SerializeField] private int guardsPerRoom = 2;
        [SerializeField] private float guardSpreadDistance = 1.5f;
        [SerializeField] private float chestHeightOffset = 0.05f;

        [Header("엔드룸")]
        [SerializeField] private GameObject prisonPrefab;
        [SerializeField] private GameObject prisonContentPrefab;
        [SerializeField] private Vector3 prisonContentLocalOffset = new Vector3(0f, 0f, -1f);
        [SerializeField] private float prisonYawOffset = -90f;

        [Header("플레이어 / 카메라")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private float playerSpawnHeightOffset = 0.05f;

        [Header("렌더링")]
        [Tooltip("베이크 맵 횃불/포인트 라이트 섀도우를 끄면 URP shadow atlas 경고를 방지합니다.")]
        [SerializeField] private bool disablePunctualLightShadows = true;

        private GameObject activeMapInstance;
        private BakedDungeonMapMetadata activeMetadata;
        private bool hasKey;

        private static int nextMapIndex = -1;

        public bool HasKey => hasKey;
        public event Action KeyGranted;
        public GameObject ActiveMapInstance => activeMapInstance;

        public void LoadNextMap()
        {
            ClearMap();

            GameObject[] prefabs = ResolvePrefabList();
            if (prefabs == null || prefabs.Length == 0)
            {
                Debug.LogError("[BakedDungeonLoader] dungeonMapPrefabs가 비어 있습니다.");
                return;
            }

            if (nextMapIndex < 0)
            {
                nextMapIndex = Mathf.Clamp(startMapIndex, 0, prefabs.Length - 1);
            }

            if (!TrySelectValidPrefab(prefabs, out int selectedIndex, out GameObject prefabRoot))
            {
                Debug.LogError("[BakedDungeonLoader] 로드할 수 있는 던전 맵 프리팹이 없습니다.");
                return;
            }

            nextMapIndex = (selectedIndex + 1) % prefabs.Length;

            activeMapInstance = Instantiate(
                prefabRoot,
                transform.position,
                transform.rotation,
                transform);
            activeMapInstance.name = $"{prefabRoot.name}_Runtime";
            PrepareLoadedMap();

            Debug.Log(
                $"[BakedDungeonLoader] 맵 로드: {prefabRoot.name} (index={selectedIndex + 1}/{prefabs.Length}, 다음={nextMapIndex + 1}) " +
                $"Start_pt={DemoMapUtil.CollectMarkers(activeMapInstance.transform, DemoMapUtil.StartMarkerName).Count} " +
                $"Prison_pt={DemoMapUtil.CollectMarkers(activeMapInstance.transform, DemoMapUtil.PrisonMarkerName).Count} " +
                $"Chest_pt={DemoMapUtil.CollectChestMarkers(activeMapInstance.transform).Count} " +
                $"Guard_pt={DemoMapUtil.CollectMarkers(activeMapInstance.transform, DemoMapUtil.GuardMarkerName).Count}");
        }

        private void PrepareLoadedMap()
        {
            if (activeMapInstance == null)
            {
                return;
            }

            PrepareMapVisuals(activeMapInstance);
            DemoWallHeightAdjuster.Apply(activeMapInstance.transform);
            DemoJunctionSeamFiller.Install(activeMapInstance.transform);
            DemoSawbladeTrapInstaller.Install(activeMapInstance.transform);
            DemoArrowTrapInstaller.Install(activeMapInstance.transform);
            DemoFirePillarTrapInstaller.Install(activeMapInstance.transform);

            activeMetadata = activeMapInstance.GetComponent<BakedDungeonMapMetadata>();
            if (activeMetadata == null)
            {
                activeMetadata = activeMapInstance.GetComponentInChildren<BakedDungeonMapMetadata>();
            }

            activeMetadata?.ResolvePlayerSpawn(activeMapInstance.transform);

            if (disablePunctualLightShadows)
            {
                DisablePunctualLightShadows(activeMapInstance);
            }

            hasKey = false;
        }

        public void PlaceCommander()
        {
            PlacePlayer();
            SetupCameraFollow();
        }

        public void SpawnRoomContents()
        {
            if (activeMapInstance == null)
            {
                return;
            }

            DemoRoomContentSpawner.Spawn(
                activeMapInstance.transform,
                chestPrefab,
                mimicPrefab,
                guardPrefab,
                guardsPerRoom,
                guardSpreadDistance,
                chestHeightOffset,
                playerTransform,
                this);
        }

        public void SpawnEndRoomPrison(DemoDungeonController controller)
        {
            if (activeMapInstance == null)
            {
                Debug.LogWarning("[BakedDungeonLoader] 맵 인스턴스가 없어 PF_Prison을 스폰할 수 없습니다.");
                return;
            }

            GameObject prefab = ResolvePrisonPrefab();
            DemoEndRoomSpawner.SpawnPrison(
                activeMapInstance.transform,
                prefab,
                ResolvePrisonContentPrefab(),
                prisonContentLocalOffset,
                prisonYawOffset,
                this,
                controller);
        }

        public void ClearMap()
        {
            hasKey = false;
            activeMetadata = null;
            ClearRuntimeMap();
        }

        private void ClearRuntimeMap()
        {
            if (activeMapInstance != null)
            {
                DemoNavMeshBuilder.DestroyExistingProxies(activeMapInstance.transform);
                if (Application.isPlaying)
                {
                    Destroy(activeMapInstance);
                }
                else
                {
                    DestroyImmediate(activeMapInstance);
                }

                activeMapInstance = null;
            }

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (!child.name.Contains("DungeonGenerator_Baked") && !child.name.EndsWith("_Runtime"))
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void PrepareMapVisuals(GameObject mapRoot)
        {
            if (mapRoot == null)
            {
                return;
            }

            Transform[] transforms = mapRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null)
                {
                    transforms[i].gameObject.isStatic = false;
                }
            }

            Renderer[] renderers = mapRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled = true;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;

                MeshRenderer meshRenderer = renderer as MeshRenderer;
                if (meshRenderer != null)
                {
                    meshRenderer.receiveGI = ReceiveGI.LightProbes;
                }
            }
        }

        public void GrantKey()
        {
            if (hasKey)
            {
                return;
            }

            hasKey = true;
            KeyGranted?.Invoke();
        }

        private GameObject ResolvePrisonPrefab()
        {
            if (prisonPrefab != null)
            {
                return prisonPrefab;
            }

#if UNITY_EDITOR
            const string prisonGuid = "2ab4f89d17ba59444a915f7c43d85e0f";
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(prisonGuid);
            prisonPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prisonPrefab != null)
            {
                Debug.LogWarning("[BakedDungeonLoader] prisonPrefab 참조가 비어 있어 PF_Prison을 자동 연결했습니다.");
            }
#endif

            return prisonPrefab;
        }

        private GameObject ResolvePrisonContentPrefab()
        {
            return prisonContentPrefab;
        }

        private GameObject[] ResolvePrefabList()
        {
            GameObject[] defaults = BakedDungeonPrefabRegistry.LoadDefaultPrefabs();
            if (!HasValidPrefabList(dungeonMapPrefabs))
            {
                if (HasValidPrefabList(defaults))
                {
                    Debug.LogWarning("[BakedDungeonLoader] Inspector 참조가 깨져 기본 DungeonBakes 프리팹을 사용합니다.");
                    dungeonMapPrefabs = defaults;
                    return defaults;
                }

                return dungeonMapPrefabs;
            }

            if (!HasValidPrefabList(defaults))
            {
                return dungeonMapPrefabs;
            }

            for (int i = 0; i < dungeonMapPrefabs.Length && i < defaults.Length; i++)
            {
                if (!TryResolvePrefab(dungeonMapPrefabs[i], out _))
                {
                    dungeonMapPrefabs[i] = defaults[i];
                }
            }

            return dungeonMapPrefabs;
        }

        private static bool HasValidPrefabList(GameObject[] prefabs)
        {
            if (prefabs == null || prefabs.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < prefabs.Length; i++)
            {
                if (TryResolvePrefab(prefabs[i], out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolvePrefab(UnityEngine.Object source, out GameObject prefabRoot)
        {
            prefabRoot = source as GameObject;
            if (prefabRoot != null)
            {
                return true;
            }

            if (source is Component component)
            {
                prefabRoot = component.gameObject;
                return prefabRoot != null;
            }

            return false;
        }

        private void PlacePlayer()
        {
            if (playerTransform == null || activeMapInstance == null)
            {
                return;
            }

            Vector3 spawnPosition;
            if (activeMetadata != null && activeMetadata.PlayerSpawnPoint != null)
            {
                spawnPosition = activeMetadata.PlayerSpawnPoint.position;
                spawnPosition.y += playerSpawnHeightOffset;
            }
            else if (!DemoSpawnResolver.TryGetSpawnPosition(activeMapInstance.transform, playerSpawnHeightOffset, out spawnPosition))
            {
                spawnPosition = activeMapInstance.transform.position + Vector3.up * playerSpawnHeightOffset;
            }

            DemoSpawnResolver.TrySnapToNavMesh(ref spawnPosition, 12f);
            DemoCommanderPlacement.PlaceOnSurface(playerTransform, spawnPosition, playerSpawnHeightOffset);
        }

        private void SetupCameraFollow()
        {
            if (playerTransform == null)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            MazeCameraFollow cameraFollow = mainCamera.GetComponent<MazeCameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.BindTarget(playerTransform, true);
            }
        }

        private bool TrySelectValidPrefab(
            GameObject[] prefabs,
            out int selectedIndex,
            out GameObject prefabRoot)
        {
            selectedIndex = -1;
            prefabRoot = null;

            int startIndex = Mathf.Clamp(nextMapIndex, 0, prefabs.Length - 1);
            for (int offset = 0; offset < prefabs.Length; offset++)
            {
                int index = (startIndex + offset) % prefabs.Length;
                if (TryResolvePrefab(prefabs[index], out prefabRoot))
                {
                    selectedIndex = index;
                    return true;
                }
            }

            return false;
        }

        private static void DisablePunctualLightShadows(GameObject mapRoot)
        {
            Light[] lights = mapRoot.GetComponentsInChildren<Light>(true);
            int disabledCount = 0;

            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light == null || light.type == LightType.Directional)
                {
                    continue;
                }

                if (light.shadows == LightShadows.None)
                {
                    continue;
                }

                light.shadows = LightShadows.None;
                disabledCount++;
            }

            if (disabledCount > 0)
            {
                Debug.Log($"[BakedDungeonLoader] 포인트/스팟 라이트 섀도우 {disabledCount}개 비활성화 (URP shadow atlas 경고 방지)");
            }
        }
    }
}
