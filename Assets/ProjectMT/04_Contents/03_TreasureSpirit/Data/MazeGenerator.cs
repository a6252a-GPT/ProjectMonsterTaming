using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.TreasureSpirit
{
    public class MazeGenerator : MonoBehaviour
    {
        [Header("미로 크기 (홀수 권장)")]
        [SerializeField] private int width = 15;
        [SerializeField] private int height = 15;

        [Header("규격 설정")]
        [SerializeField] private float cellSize = 3.0f;
        [SerializeField] private float wallHeight = 2.0f;
        [SerializeField] private float wallThickness = 0.5f;
        [SerializeField] private float floorY = 0.1f;

        [Header("프리팹 및 오브젝트 연결")]
        [SerializeField] private GameObject wallPrefab;
        [SerializeField] private GameObject floorPrefab;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private GameObject prisonPrefab;        // 감옥 외형 프리팹
        [SerializeField] private GameObject prisonContentPrefab; // 감옥 내부에 들어갈 외형 프리팹

        [Header("적 경비병 설정")]
        [SerializeField] private GameObject guardPrefab;
        [SerializeField] private int guardCount = 5;

        [Header("보물상자 관련")]
        [SerializeField] private GameObject treasureChestPrefab;
        [SerializeField] private GameObject mimicPrefab;
        [SerializeField] private int treasureChestCount = 3;

        [Header("스폰 안전 구역 설정")]
        [SerializeField] private int exclusionRadius = 2; // 입구/감옥 주변 스폰 금지 거리 (셀 단위)

        private bool hasKey;
        public bool HasKey => hasKey;

        private int[,] grid;
        private NavMeshData navMeshData;
        private NavMeshDataInstance navMeshDataInstance;

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

            grid[1, 0] = 0; // 입구
            grid[width - 2, height - 1] = 0; // 출구 (감옥)

            Build3DMaze();

            BakeNavMeshRuntime();

            // 입구 및 감옥 주변이 제외된 스폰 가능 위치 추출
            List<Vector2Int> availablePathCells = GetEmptyPathCells();

            // 1. 보물상자 생성
            SpawnTreasureChests(treasureChestCount, availablePathCells);

            // 2. 경비병 생성
            SpawnGuards(availablePathCells);
        }

        private List<Vector2Int> GetEmptyPathCells()
        {
            List<Vector2Int> emptyPaths = new List<Vector2Int>();

            Vector2Int startCell = new Vector2Int(1, 1);
            Vector2Int prisonCell = new Vector2Int(width - 2, height - 2);

            for (int x = 1; x < width - 1; x++)
            {
                for (int z = 1; z < height - 1; z++)
                {
                    if (grid[x, z] == 0)
                    {
                        Vector2Int currentCell = new Vector2Int(x, z);

                        if (Vector2Int.Distance(currentCell, startCell) <= exclusionRadius)
                            continue;

                        if (Vector2Int.Distance(currentCell, prisonCell) <= exclusionRadius)
                            continue;

                        emptyPaths.Add(currentCell);
                    }
                }
            }

            Shuffle(emptyPaths);
            return emptyPaths;
        }

        private void SpawnTreasureChests(int count, List<Vector2Int> availableCells)
        {
            if (treasureChestPrefab == null || availableCells == null) return;

            int spawnCount = Mathf.Min(count, availableCells.Count);
            int keyChestIndex = Random.Range(0, spawnCount);

            for (int i = 0; i < spawnCount; i++)
            {
                Vector2Int cell = availableCells[0];
                availableCells.RemoveAt(0);

                Vector3 chestPos = new Vector3(cell.x * cellSize, floorY + 0.05f, cell.y * cellSize);

                GameObject chestObj = Instantiate(treasureChestPrefab, chestPos, Quaternion.identity, transform);

                BoxCollider boxCol = chestObj.GetComponent<BoxCollider>();
                if (boxCol == null) boxCol = chestObj.AddComponent<BoxCollider>();
                boxCol.isTrigger = true;
                boxCol.size = new Vector3(1.8f, 1.8f, 1.8f);

                bool containsKey = (i == keyChestIndex);

                ChestTriggerHandler handler = chestObj.AddComponent<ChestTriggerHandler>();
                handler.ContainsKey = containsKey;
                handler.PlayerTransform = playerTransform;

                handler.OnOpened = (player) =>
                {
                    OnChestOpened(chestObj.transform.position, player, containsKey);
                };
            }
        }

        private void SpawnGuards(List<Vector2Int> availableCells)
        {
            if (guardPrefab == null || availableCells == null) return;

            int spawnCount = Mathf.Min(guardCount, availableCells.Count);

            for (int i = 0; i < spawnCount; i++)
            {
                Vector2Int cell = availableCells[0];
                availableCells.RemoveAt(0);

                Vector3 spawnPos = new Vector3(
                    cell.x * cellSize,
                    floorY,
                    cell.y * cellSize);

                if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    spawnPos = hit.position;
                }

                GameObject guardObj = Instantiate(
                    guardPrefab,
                    spawnPos,
                    Quaternion.Euler(0, Random.Range(0f, 360f), 0),
                    transform);

                GuardAI guardAI = guardObj.GetComponent<GuardAI>();
                if (guardAI != null && playerTransform != null)
                {
                    guardAI.SetTargetPlayer(playerTransform);
                }
            }
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

            // 바닥 생성
            if (floorPrefab != null)
            {
                float totalWidth = width * cellSize;
                float totalHeight = height * cellSize;

                Vector3 centerPos = new Vector3(
                    (totalWidth - cellSize) * 0.5f,
                    floorY,
                    (totalHeight - cellSize) * 0.5f);

                GameObject floorObj = Instantiate(floorPrefab, centerPos, Quaternion.identity, transform);
                Vector3 currentScale = floorObj.transform.localScale;
                floorObj.transform.localScale = new Vector3(totalWidth, currentScale.y, totalHeight);
            }

            // 벽 생성
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    if (grid[x, z] == 1 && wallPrefab != null)
                    {
                        Vector3 wallPos = new Vector3(x * cellSize, wallCenterY, z * cellSize);
                        GameObject wallObj = Instantiate(wallPrefab, wallPos, Quaternion.identity, transform);

                        bool connectHorizontal = (x > 0 && grid[x - 1, z] == 1) || (x < width - 1 && grid[x + 1, z] == 1);
                        bool connectVertical = (z > 0 && grid[x, z - 1] == 1) || (z < height - 1 && grid[x, z + 1] == 1);

                        float scaleX = connectHorizontal ? (cellSize + wallThickness) : wallThickness;
                        float scaleZ = connectVertical ? (cellSize + wallThickness) : wallThickness;

                        wallObj.transform.localScale = new Vector3(scaleX, wallHeight, scaleZ);
                    }
                }
            }

            // 시작 지점으로 플레이어 이동
            if (playerTransform != null)
            {
                Vector3 startPos = new Vector3(1 * cellSize, floorY + 0.5f, 0 * cellSize);

                CharacterController cc = playerTransform.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                playerTransform.position = startPos;

                if (cc != null) cc.enabled = true;

                PlayerCharacterController playerController = playerTransform.GetComponent<PlayerCharacterController>();
                if (playerController != null)
                {
                    playerController.SetMapBounds(width, height, cellSize, padding: 0.5f);
                }

                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    MazeCameraFollow camFollow = mainCam.GetComponent<MazeCameraFollow>();
                    if (camFollow != null)
                    {
                        camFollow.target = playerTransform;
                    }
                }
            }

            // ★ 감옥 전체(내용물 포함) z축으로 한 칸(-1f) 뒤로 배치
            if (prisonPrefab != null)
            {
                Vector3 prisonPos = new Vector3((width - 2) * cellSize, floorY, (height - 1) * cellSize + 2f);
                GameObject prisonObj = Instantiate(prisonPrefab, prisonPos, Quaternion.identity, transform);

                // 감옥 문 트리거 설정
                PrisonDoor door = prisonObj.GetComponentInChildren<PrisonDoor>(true);
                if (door != null)
                {
                    door.gameObject.SetActive(true);
                    var col = door.GetComponent<Collider>();
                    if (col == null)
                    {
                        col = door.gameObject.AddComponent<BoxCollider>();
                    }
                    col.isTrigger = true;
                }

                // 내용물 위치
                if (prisonContentPrefab != null)
                {
                    Vector3 contentPos = prisonPos + new Vector3(0f, 0f, -1f);
                    Instantiate(prisonContentPrefab, contentPos, Quaternion.identity, prisonObj.transform);
                }
            }
        }

        private void BakeNavMeshRuntime()
        {
            if (navMeshDataInstance.valid)
            {
                NavMesh.RemoveNavMeshData(navMeshDataInstance);
            }

            navMeshData = new NavMeshData();
            navMeshDataInstance = NavMesh.AddNavMeshData(navMeshData);

            NavMeshBuildSettings buildSettings = NavMesh.GetSettingsByID(0);
            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();

            NavMeshBuilder.CollectSources(
                transform,
                LayerMask.GetMask("Default"),
                NavMeshCollectGeometry.PhysicsColliders,
                0,
                new List<NavMeshBuildMarkup>(),
                sources
            );

            Bounds worldBounds = new Bounds(
                transform.position + new Vector3(width * cellSize * 0.5f, 0, height * cellSize * 0.5f),
                new Vector3(width * cellSize, wallHeight * 2f, height * cellSize)
            );

            NavMeshBuilder.UpdateNavMeshData(navMeshData, buildSettings, sources, worldBounds);
        }

        private void OnChestOpened(Vector3 spawnPos, Transform player, bool containsKey)
        {
            if (containsKey)
            {
                hasKey = true;
                Debug.Log("🗝 열쇠를 획득했습니다!");
                return;
            }

            if (mimicPrefab != null)
            {
                Quaternion mimicRotation = Quaternion.identity;
                if (player != null)
                {
                    Vector3 lookDir = player.position - spawnPos;
                    lookDir.y = 0;
                    if (lookDir != Vector3.zero) mimicRotation = Quaternion.LookRotation(lookDir);
                }

                Instantiate(mimicPrefab, spawnPos, mimicRotation, transform);
                Debug.Log("👹 미믹 출현!");
            }
        }

        public void ClearMaze()
        {
            hasKey = false;

            if (navMeshDataInstance.valid)
            {
                NavMesh.RemoveNavMeshData(navMeshDataInstance);
            }

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
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
        public Transform PlayerTransform;

        public System.Action<Transform> OnOpened;

        private bool isOpened = false;

        private void OnTriggerEnter(Collider other)
        {
            if (isOpened) return;

            bool isPlayer = false;

            if (PlayerTransform != null)
            {
                isPlayer = other.transform.IsChildOf(PlayerTransform) || other.transform.root == PlayerTransform;
            }

            if (!isPlayer && (other.CompareTag("Player") || other.transform.root.CompareTag("Player")))
            {
                isPlayer = true;
            }

            if (isPlayer)
            {
                isOpened = true;
                OnOpened?.Invoke(other.transform);
                Destroy(gameObject);
            }
        }
    }
}