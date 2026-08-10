using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.GrowthDungeon
{
    public class MazeGenerator : MonoBehaviour
    {
        [Header("미로 크기 (홀수 권장)")]
        [SerializeField] private int width = 15;
        [SerializeField] private int height = 15;

        [Header("규격 설정")]
        [SerializeField] private float cellSize = 3.0f;
        [SerializeField] private float wallHeight = 2.0f;
        [SerializeField] private float wallThickness = 0.2f;
        [SerializeField] private float floorY = 0.1f;

        [Header("프리팹 연결")]
        [SerializeField] private GameObject wallPrefab;
        [SerializeField] private GameObject floorPrefab;
        [SerializeField] private GameObject startPointPrefab;
        [SerializeField] private GameObject prisonPrefab;

        [Header("보물상자 관련")]
        [SerializeField] private GameObject treasureChestPrefab;
        [SerializeField] private GameObject mimicPrefab;

        private int[,] grid;

        public void GenerateMaze()
        {
            ClearMaze();

            if (width % 2 == 0) width++;
            if (height % 2 == 0) height++;

            grid = new int[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    grid[x, z] = 1;
                }
            }

            CarvePath(1, 1);

            // 입구 / 출구 통로
            grid[1, 0] = 0;
            grid[width - 2, height - 1] = 0;

            Build3DMaze();

            // ===== 런타임 NavMesh 베이크 추가 =====
            BakeNavMeshRuntime();

            // 보물상자 5개 생성
            SpawnTreasureChests(5);
        }

        private void CarvePath(int x, int z)
        {
            grid[x, z] = 0;

            List<Vector2Int> directions = new List<Vector2Int>
            {
                new Vector2Int(0, 2),
                new Vector2Int(0, -2),
                new Vector2Int(2, 0),
                new Vector2Int(-2, 0)
            };

            Shuffle(directions);

            foreach (var dir in directions)
            {
                int nx = x + dir.x;
                int nz = z + dir.y;

                if (nx > 0 && nx < width - 1 &&
                    nz > 0 && nz < height - 1 &&
                    grid[nx, nz] == 1)
                {
                    grid[x + dir.x / 2, z + dir.y / 2] = 0;
                    CarvePath(nx, nz);
                }
            }
        }

        private void Build3DMaze()
        {
            float wallCenterY = floorY + (wallHeight * 0.5f);

            // ===== 전체 바닥 =====
            if (floorPrefab != null)
            {
                float totalWidth = width * cellSize;
                float totalHeight = height * cellSize;

                Vector3 centerPos = new Vector3(
                    (totalWidth - cellSize) * 0.5f,
                    floorY,
                    (totalHeight - cellSize) * 0.5f);

                GameObject floorObj = Instantiate(
                    floorPrefab,
                    centerPos,
                    Quaternion.identity,
                    transform);

                Vector3 currentScale = floorObj.transform.localScale;
                floorObj.transform.localScale = new Vector3(
                    totalWidth,
                    currentScale.y,
                    totalHeight);
            }

            // ===== 벽 생성 =====
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    if (grid[x, z] == 1 && wallPrefab != null)
                    {
                        Vector3 wallPos = new Vector3(
                            x * cellSize,
                            wallCenterY,
                            z * cellSize);

                        GameObject wallObj = Instantiate(
                            wallPrefab,
                            wallPos,
                            Quaternion.identity,
                            transform);

                        bool connectHorizontal =
                            (x > 0 && grid[x - 1, z] == 1) ||
                            (x < width - 1 && grid[x + 1, z] == 1);

                        bool connectVertical =
                            (z > 0 && grid[x, z - 1] == 1) ||
                            (z < height - 1 && grid[x, z + 1] == 1);

                        float scaleX = connectHorizontal
                            ? (cellSize + wallThickness)
                            : wallThickness;

                        float scaleZ = connectVertical
                            ? (cellSize + wallThickness)
                            : wallThickness;

                        wallObj.transform.localScale = new Vector3(
                            scaleX,
                            wallHeight,
                            scaleZ);
                    }
                }
            }

            // ===== 시작점 =====
            if (startPointPrefab != null)
            {
                Vector3 startPos = new Vector3(
                    1 * cellSize,
                    floorY + 0.5f,
                    0 * cellSize);

                GameObject startObj = Instantiate(
                    startPointPrefab,
                    startPos,
                    Quaternion.identity,
                    transform);

                if (startObj.GetComponent<DungeonStarterController>() == null)
                {
                    startObj.AddComponent<DungeonStarterController>();
                }

                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    MazeCameraFollow camFollow =
                        mainCam.GetComponent<MazeCameraFollow>();

                    if (camFollow != null)
                    {
                        camFollow.target = startObj.transform;
                    }
                }
            }

            // ===== 감옥(출구 포함) =====
            if (prisonPrefab != null)
            {
                Vector3 prisonPos = new Vector3(
                    (width - 2) * cellSize,
                    floorY,
                    (height - 1) * cellSize);

                GameObject prison = Instantiate(
                    prisonPrefab,
                    prisonPos,
                    Quaternion.identity,
                    transform);

                MazeExitArea exitArea =
                    prison.GetComponentInChildren<MazeExitArea>();

                if (exitArea != null)
                {
                    exitArea.Init(this);
                }
                else
                {
                    Debug.LogWarning("PrisonPrefab 안에 MazeExitArea가 없습니다.");
                }
            }
        }

        private void BakeNavMeshRuntime()
        {
            // 런타임 NavMesh 데이터 동적 생성
            NavMeshData navMeshData = new NavMeshData();
            NavMesh.AddNavMeshData(navMeshData);

            NavMeshBuildSettings buildSettings = NavMesh.GetSettingsByID(0);
            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();

            // 현재 MazeGenerator 하위의 모든 콜라이더를 NavMesh 소스로 수집
            NavMeshBuilder.CollectSources(
                transform,
                LayerMask.GetMask("Default"),
                NavMeshCollectGeometry.PhysicsColliders,
                0,
                new List<NavMeshBuildMarkup>(),
                sources
            );

            // Bounds 계산 (미로 전체 크기)
            Bounds worldBounds = new Bounds(
                transform.position + new Vector3(width * cellSize * 0.5f, 0, height * cellSize * 0.5f),
                new Vector3(width * cellSize, wallHeight * 2f, height * cellSize)
            );

            // 실제 NavMesh 빌드
            NavMeshBuilder.UpdateNavMeshData(
                navMeshData,
                buildSettings,
                sources,
                worldBounds
            );

            Debug.Log("🌐 런타임 NavMesh 베이크 완료!");
        }

        private void SpawnTreasureChests(int count)
        {
            if (treasureChestPrefab == null) return;

            List<Vector2Int> emptyPaths = new List<Vector2Int>();

            for (int x = 1; x < width - 1; x++)
            {
                for (int z = 1; z < height - 1; z++)
                {
                    if (grid[x, z] == 0)
                    {
                        // 시작 위치 제외
                        if (x == 1 && z == 1)
                            continue;

                        emptyPaths.Add(new Vector2Int(x, z));
                    }
                }
            }

            Shuffle(emptyPaths);

            int spawnCount = Mathf.Min(count, emptyPaths.Count);

            // 5개 중 1개만 열쇠
            int keyChestIndex = Random.Range(0, spawnCount);

            for (int i = 0; i < spawnCount; i++)
            {
                Vector2Int cell = emptyPaths[i];

                Vector3 chestPos = new Vector3(
                    cell.x * cellSize,
                    floorY + 0.05f,
                    cell.y * cellSize);

                GameObject chestObj = Instantiate(
                    treasureChestPrefab,
                    chestPos,
                    Quaternion.identity,
                    transform);

                Collider col = chestObj.GetComponent<Collider>();
                if (col == null)
                {
                    col = chestObj.AddComponent<BoxCollider>();
                }

                col.isTrigger = true;

                bool containsKey = (i == keyChestIndex);

                ChestTriggerHandler handler =
                    chestObj.AddComponent<ChestTriggerHandler>();

                handler.ContainsKey = containsKey;

                handler.OnOpened = (player) =>
                {
                    OnChestOpened(
                        chestObj.transform.position,
                        player,
                        containsKey);
                };
            }
        }

        private void OnChestOpened(
            Vector3 spawnPos,
            Transform playerTransform,
            bool containsKey)
        {
            // ===== 열쇠 상자 =====
            if (containsKey)
            {
                if (playerTransform != null)
                {
                    DungeonStarterController player = playerTransform.GetComponent<DungeonStarterController>();
                    if (player != null)
                    {
                        player.HasKey = true;
                        Debug.Log("🗝 열쇠를 획득했습니다!");
                    }
                }

                return;
            }

            // ===== 미믹 상자 =====
            if (mimicPrefab != null)
            {
                Quaternion mimicRotation = Quaternion.identity;

                if (playerTransform != null)
                {
                    Vector3 lookDir = playerTransform.position - spawnPos;
                    lookDir.y = 0;

                    if (lookDir != Vector3.zero)
                    {
                        mimicRotation = Quaternion.LookRotation(lookDir);
                    }
                }

                Instantiate(
                    mimicPrefab,
                    spawnPos,
                    mimicRotation,
                    transform);

                Debug.Log("👹 미믹 출현!");
            }
        }

        public void ClearMaze()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;

                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                T temp = list[i];
                int randomIndex = Random.Range(i, list.Count);
                list[i] = list[randomIndex];
                list[randomIndex] = temp;
            }
        }
    }

    public class ChestTriggerHandler : MonoBehaviour
    {
        public bool ContainsKey;

        public System.Action<Transform> OnOpened;

        private bool isOpened = false;

        private void OnTriggerEnter(Collider other)
        {
            if (isOpened) return;

            if (other.GetComponent<DungeonStarterController>() == null)
                return;

            isOpened = true;

            OnOpened?.Invoke(other.transform);

            Destroy(gameObject);
        }
    }
}