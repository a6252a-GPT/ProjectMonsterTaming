using System;
using UnityEngine;
using ProjectMT.Contents.TreasureSpirit;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    [DisallowMultipleComponent]
    public sealed class BakedDungeonLoader : MonoBehaviour
    {
        [Header("베이크된 던전 프리팹 (5개 등록)")]
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

            int selectedIndex = nextMapIndex;
            nextMapIndex = (nextMapIndex + 1) % prefabs.Length;

            GameObject selectedPrefab = prefabs[selectedIndex];

            if (!TryResolvePrefab(selectedPrefab, out GameObject prefabRoot))
            {
                Debug.LogError($"[BakedDungeonLoader] 인덱스 {selectedIndex} 프리팹 참조가 올바르지 않습니다.");
                return;
            }

            activeMapInstance = Instantiate(
                prefabRoot,
                transform.position,
                transform.rotation,
                transform);
            activeMapInstance.name = $"{prefabRoot.name}_Runtime";

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

            Debug.Log($"[BakedDungeonLoader] 맵 로드: {prefabRoot.name} (index={selectedIndex + 1}/{prefabs.Length}, 다음={nextMapIndex + 1})");
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

            if (activeMapInstance != null)
            {
                DemoNavMeshBuilder.DestroyExistingProxies(activeMapInstance.transform);
            }

            if (activeMapInstance == null)
            {
                return;
            }

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
            if (HasValidPrefabList(dungeonMapPrefabs))
            {
                return dungeonMapPrefabs;
            }

            GameObject[] defaults = BakedDungeonPrefabRegistry.LoadDefaultPrefabs();
            if (HasValidPrefabList(defaults))
            {
                Debug.LogWarning("[BakedDungeonLoader] Inspector 참조가 깨져 기본 DungeonBakes 프리팹을 사용합니다.");
                dungeonMapPrefabs = defaults;
                return defaults;
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
                return;
            }

            DemoSpawnResolver.TrySnapToNavMesh(ref spawnPosition, 6f);
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
                cameraFollow.target = playerTransform;
            }
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
