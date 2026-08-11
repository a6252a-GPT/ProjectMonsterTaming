using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.GrowthDungeon
{
    public class MazeGenerator : MonoBehaviour
    {
        [Header("미로 크기 (홀수 권장)")]
        [SerializeField] private int width = 15; //
        [SerializeField] private int height = 15; //

        [Header("규격 설정")]
        [SerializeField] private float cellSize = 3.0f; //
        [SerializeField] private float wallHeight = 2.0f; //
        [SerializeField] private float wallThickness = 0.2f; //[cite: 1]
        [SerializeField] private float floorY = 0.1f; //[cite: 1]

        [Header("프리팹 연결")]
        [SerializeField] private GameObject wallPrefab; //[cite: 1]
        [SerializeField] private GameObject floorPrefab; //[cite: 1]
        [SerializeField] private GameObject startPointPrefab; //[cite: 1]
        [SerializeField] private GameObject prisonPrefab; //[cite: 1]

        [Header("적 경비병 설정")]
        [SerializeField] private GameObject guardPrefab; // 적 경비병 프리팹[cite: 1]
        [SerializeField] private int guardCount = 5;    // 인스펙터에서 생성할 경비병 수 설정

        [Header("보물상자 관련")]
        [SerializeField] private GameObject treasureChestPrefab; //[cite: 1]
        [SerializeField] private GameObject mimicPrefab; //[cite: 1]

        private int[,] grid; //[cite: 1]

        public void GenerateMaze()
        {
            ClearMaze(); //[cite: 1]

            if (width % 2 == 0) width++; //[cite: 1]
            if (height % 2 == 0) height++; //[cite: 1]

            grid = new int[width, height]; //[cite: 1]

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    grid[x, z] = 1; //[cite: 1]
                }
            }

            CarvePath(1, 1); //[cite: 1]

            // 입구 / 출구 통로
            grid[1, 0] = 0; //[cite: 1]
            grid[width - 2, height - 1] = 0; //[cite: 1]

            Build3DMaze(); //[cite: 1]

            // ===== 런타임 NavMesh 베이크 추가 =====
            BakeNavMeshRuntime(); //[cite: 1]

            // 보물상자 5개 생성
            SpawnTreasureChests(5); //[cite: 1]

            // ===== 경비병 5~10명 무작위 생성 =====
            SpawnGuards();
        }

        private void SpawnGuards()
        {
            if (guardPrefab == null)
            {
                Debug.LogWarning("경비병 프리팹(guardPrefab)이 지정되지 않았습니다.");
                return;
            }

            List<Vector2Int> emptyPaths = new List<Vector2Int>();

            for (int x = 1; x < width - 1; x++)
            {
                for (int z = 1; z < height - 1; z++)
                {
                    if (grid[x, z] == 0)
                    {
                        if (x == 1 && z == 1)
                            continue;

                        emptyPaths.Add(new Vector2Int(x, z));
                    }
                }
            }

            Shuffle(emptyPaths);

            int spawnCount = Mathf.Min(guardCount, emptyPaths.Count);

            for (int i = 0; i < spawnCount; i++)
            {
                Vector2Int cell = emptyPaths[i];

                Vector3 spawnPos = new Vector3(
                    cell.x * cellSize,
                    floorY,
                    cell.y * cellSize);

                if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    spawnPos = hit.position;
                }

                Instantiate(
                    guardPrefab,
                    spawnPos,
                    Quaternion.Euler(0, Random.Range(0f, 360f), 0),
                    transform);
            }

            Debug.Log($"⚔️ 경비병 {spawnCount}명 생성 완료!");
        }

        private void CarvePath(int x, int z)
        {
            grid[x, z] = 0; //[cite: 1]

            List<Vector2Int> directions = new List<Vector2Int>
            {
                new Vector2Int(0, 2),
                new Vector2Int(0, -2),
                new Vector2Int(2, 0),
                new Vector2Int(-2, 0)
            }; //[cite: 1]

            Shuffle(directions); //[cite: 1]

            foreach (var dir in directions)
            {
                int nx = x + dir.x; //[cite: 1]
                int nz = z + dir.y; //[cite: 1]

                if (nx > 0 && nx < width - 1 &&
                    nz > 0 && nz < height - 1 &&
                    grid[nx, nz] == 1) //[cite: 1]
                {
                    grid[x + dir.x / 2, z + dir.y / 2] = 0; //[cite: 1]
                    CarvePath(nx, nz); //[cite: 1]
                }
            }
        }

        private void Build3DMaze()
        {
            float wallCenterY = floorY + (wallHeight * 0.5f); //[cite: 1]

            // ===== 전체 바닥 =====
            if (floorPrefab != null) //[cite: 1]
            {
                float totalWidth = width * cellSize; //[cite: 1]
                float totalHeight = height * cellSize; //[cite: 1]

                Vector3 centerPos = new Vector3(
                    (totalWidth - cellSize) * 0.5f,
                    floorY,
                    (totalHeight - cellSize) * 0.5f); //[cite: 1]

                GameObject floorObj = Instantiate(
                    floorPrefab,
                    centerPos,
                    Quaternion.identity,
                    transform); //[cite: 1]

                Vector3 currentScale = floorObj.transform.localScale; //[cite: 1]
                floorObj.transform.localScale = new Vector3(
                    totalWidth,
                    currentScale.y,
                    totalHeight); //[cite: 1]
            }

            // ===== 벽 생성 =====
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    if (grid[x, z] == 1 && wallPrefab != null) //[cite: 1]
                    {
                        Vector3 wallPos = new Vector3(
                            x * cellSize,
                            wallCenterY,
                            z * cellSize); //[cite: 1]

                        GameObject wallObj = Instantiate(
                            wallPrefab,
                            wallPos,
                            Quaternion.identity,
                            transform); //[cite: 1]

                        bool connectHorizontal =
                            (x > 0 && grid[x - 1, z] == 1) ||
                            (x < width - 1 && grid[x + 1, z] == 1); //[cite: 1]

                        bool connectVertical =
                            (z > 0 && grid[x, z - 1] == 1) ||
                            (z < height - 1 && grid[x, z + 1] == 1); //[cite: 1]

                        float scaleX = connectHorizontal
                            ? (cellSize + wallThickness)
                            : wallThickness; //[cite: 1]

                        float scaleZ = connectVertical
                            ? (cellSize + wallThickness)
                            : wallThickness; //[cite: 1]

                        wallObj.transform.localScale = new Vector3(
                            scaleX,
                            wallHeight,
                            scaleZ); //[cite: 1]
                    }
                }
            }

            // ===== 시작점 =====
            if (startPointPrefab != null) //[cite: 1]
            {
                Vector3 startPos = new Vector3(
                    1 * cellSize,
                    floorY + 0.5f,
                    0 * cellSize); //[cite: 1]

                GameObject startObj = Instantiate(
                    startPointPrefab,
                    startPos,
                    Quaternion.identity,
                    transform); //[cite: 1]

                if (startObj.GetComponent<DungeonStarterController>() == null) //[cite: 1]
                {
                    startObj.AddComponent<DungeonStarterController>(); //[cite: 1]
                }

                Camera mainCam = Camera.main; //[cite: 1]
                if (mainCam != null) //[cite: 1]
                {
                    MazeCameraFollow camFollow =
                        mainCam.GetComponent<MazeCameraFollow>(); //[cite: 1]

                    if (camFollow != null) //[cite: 1]
                    {
                        camFollow.target = startObj.transform; //[cite: 1]
                    }
                }
            }

            // ===== 감옥(출구 포함) =====
            if (prisonPrefab != null) //[cite: 1]
            {
                Vector3 prisonPos = new Vector3(
                    (width - 2) * cellSize,
                    floorY,
                    (height - 1) * cellSize); //[cite: 1]

                GameObject prison = Instantiate(
                    prisonPrefab,
                    prisonPos,
                    Quaternion.identity,
                    transform); //[cite: 1]

                MazeExitArea exitArea =
                    prison.GetComponentInChildren<MazeExitArea>(); //[cite: 1]

                if (exitArea != null) //[cite: 1]
                {
                    exitArea.Init(this); //[cite: 1]
                }
                else
                {
                    Debug.LogWarning("PrisonPrefab 안에 MazeExitArea가 없습니다."); //[cite: 1]
                }
            }
        }

        private void BakeNavMeshRuntime()
        {
            NavMeshData navMeshData = new NavMeshData(); //[cite: 1]
            NavMesh.AddNavMeshData(navMeshData); //[cite: 1]

            NavMeshBuildSettings buildSettings = NavMesh.GetSettingsByID(0); //[cite: 1]
            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>(); //[cite: 1]

            NavMeshBuilder.CollectSources(
                transform,
                LayerMask.GetMask("Default"),
                NavMeshCollectGeometry.PhysicsColliders,
                0,
                new List<NavMeshBuildMarkup>(),
                sources
            ); //[cite: 1]

            Bounds worldBounds = new Bounds(
                transform.position + new Vector3(width * cellSize * 0.5f, 0, height * cellSize * 0.5f),
                new Vector3(width * cellSize, wallHeight * 2f, height * cellSize)
            ); //[cite: 1]

            NavMeshBuilder.UpdateNavMeshData(
                navMeshData,
                buildSettings,
                sources,
                worldBounds
            ); //[cite: 1]

            Debug.Log("🌐 런타임 NavMesh 베이크 완료!"); //[cite: 1]
        }

        private void SpawnTreasureChests(int count)
        {
            if (treasureChestPrefab == null) return; //[cite: 1]

            List<Vector2Int> emptyPaths = new List<Vector2Int>(); //[cite: 1]

            for (int x = 1; x < width - 1; x++) //[cite: 1]
            {
                for (int z = 1; z < height - 1; z++) //[cite: 1]
                {
                    if (grid[x, z] == 0) //[cite: 1]
                    {
                        if (x == 1 && z == 1) //[cite: 1]
                            continue; //[cite: 1]

                        emptyPaths.Add(new Vector2Int(x, z)); //[cite: 1]
                    }
                }
            }

            Shuffle(emptyPaths); //[cite: 1]

            int spawnCount = Mathf.Min(count, emptyPaths.Count); //[cite: 1]
            int keyChestIndex = Random.Range(0, spawnCount); //[cite: 1]

            for (int i = 0; i < spawnCount; i++) //[cite: 1]
            {
                Vector2Int cell = emptyPaths[i]; //[cite: 1]

                Vector3 chestPos = new Vector3(
                    cell.x * cellSize,
                    floorY + 0.05f,
                    cell.y * cellSize); //[cite: 1]

                GameObject chestObj = Instantiate(
                    treasureChestPrefab,
                    chestPos,
                    Quaternion.identity,
                    transform); //[cite: 1]

                Collider col = chestObj.GetComponent<Collider>(); //[cite: 1]
                if (col == null) //[cite: 1]
                {
                    col = chestObj.AddComponent<BoxCollider>(); //[cite: 1]
                }

                col.isTrigger = true; //[cite: 1]

                bool containsKey = (i == keyChestIndex); //[cite: 1]

                ChestTriggerHandler handler =
                    chestObj.AddComponent<ChestTriggerHandler>(); //[cite: 1]

                handler.ContainsKey = containsKey; //[cite: 1]

                handler.OnOpened = (player) =>
                {
                    OnChestOpened(
                        chestObj.transform.position,
                        player,
                        containsKey); //[cite: 1]
                };
            }
        }

        private void OnChestOpened(
            Vector3 spawnPos,
            Transform playerTransform,
            bool containsKey) //[cite: 1]
        {
            if (containsKey) //[cite: 1]
            {
                if (playerTransform != null) //[cite: 1]
                {
                    DungeonStarterController player = playerTransform.GetComponent<DungeonStarterController>(); //[cite: 1]
                    if (player != null) //[cite: 1]
                    {
                        player.HasKey = true; //[cite: 1]
                        Debug.Log("🗝 열쇠를 획득했습니다!"); //[cite: 1]
                    }
                }

                return; //[cite: 1]
            }

            if (mimicPrefab != null) //[cite: 1]
            {
                Quaternion mimicRotation = Quaternion.identity; //[cite: 1]

                if (playerTransform != null) //[cite: 1]
                {
                    Vector3 lookDir = playerTransform.position - spawnPos; //[cite: 1]
                    lookDir.y = 0; //[cite: 1]

                    if (lookDir != Vector3.zero) //[cite: 1]
                    {
                        mimicRotation = Quaternion.LookRotation(lookDir); //[cite: 1]
                    }
                }

                Instantiate(
                    mimicPrefab,
                    spawnPos,
                    mimicRotation,
                    transform); //[cite: 1]

                Debug.Log("👹 미믹 출현!"); //[cite: 1]
            }
        }

        public void ClearMaze() //[cite: 1]
        {
            for (int i = transform.childCount - 1; i >= 0; i--) //[cite: 1]
            {
                GameObject child = transform.GetChild(i).gameObject; //[cite: 1]

                if (Application.isPlaying) //[cite: 1]
                {
                    Destroy(child); //[cite: 1]
                }
                else
                {
                    DestroyImmediate(child); //[cite: 1]
                }
            }
        }

        private void Shuffle<T>(List<T> list) //[cite: 1]
        {
            for (int i = 0; i < list.Count; i++) //[cite: 1]
            {
                T temp = list[i]; //[cite: 1]
                int randomIndex = Random.Range(i, list.Count); //[cite: 1]
                list[i] = list[randomIndex]; //[cite: 1]
                list[randomIndex] = temp; //[cite: 1]
            }
        }
    }

    public class ChestTriggerHandler : MonoBehaviour //[cite: 1]
    {
        public bool ContainsKey; //[cite: 1]

        public System.Action<Transform> OnOpened; //[cite: 1]

        private bool isOpened = false; //[cite: 1]

        private void OnTriggerEnter(Collider other) //[cite: 1]
        {
            if (isOpened) return; //[cite: 1]

            if (other.GetComponent<DungeonStarterController>() == null) //[cite: 1]
                return; //[cite: 1]

            isOpened = true; //[cite: 1]

            OnOpened?.Invoke(other.transform); //[cite: 1]

            Destroy(gameObject); //[cite: 1]
        }
    }
}