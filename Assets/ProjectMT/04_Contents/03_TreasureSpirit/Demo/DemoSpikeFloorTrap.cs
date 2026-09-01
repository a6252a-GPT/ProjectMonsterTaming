using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ProjectMT.Contents.TreasureSpirit;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    /// <summary>
    /// 상자 주변을 둘러싼 가시 바닥. 창(head_Spear)이 주기적으로 솟아올라 피해를 줍니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoSpikeFloorTrap : MonoBehaviour
    {
        private const int SpikeCount = 8;
        private const float RingRadius = 1.35f;
        private const float SpearScaleY = 0.7f;
        private const float SpearVisibleHeight = 0.6f;
        private const float TileSize = 0.275f;
        private const float TileHeight = 0.06f;
        private const string SpearAssetGuid = "8545d3a07a3c968409175fa3b304dd28";

        [SerializeField] private float damage = 20f;
        [SerializeField] private float hitCooldown = 0.5f;
        [SerializeField] private float hiddenDuration = 1.15f;
        [SerializeField] private float warningDuration = 0.4f;
        [SerializeField] private float activeDuration = 0.85f;

        private static GameObject cachedSpearPrefab;

        private Transform[] spikes = System.Array.Empty<Transform>();
        private Transform[] tiles = System.Array.Empty<Transform>();
        private Vector3[] hiddenPositions = System.Array.Empty<Vector3>();
        private Vector3[] activePositions = System.Array.Empty<Vector3>();
        private Renderer[] tileRenderers = System.Array.Empty<Renderer>();
        private readonly Dictionary<int, float> nextHitTime = new Dictionary<int, float>();
        private readonly Collider[] overlapHits = new Collider[24];
        private float elapsed;
        private Color tileIdleColor = new Color(0.18f, 0.16f, 0.16f);
        private Color tileWarningColor = new Color(0.85f, 0.2f, 0.12f);
        private Color tileActiveColor = new Color(0.55f, 0.08f, 0.08f);
        private Color appliedTileColor;
        private bool wasActive;
        private static readonly MaterialPropertyBlock tileColorBlock = new MaterialPropertyBlock();

        public static void SpawnAround(Transform parent, Transform mapRoot, Vector3 chestPosition, Quaternion roomRotation)
        {
            CacheSpearTemplate(mapRoot);
            Vector3 floor = SnapToFloor(chestPosition);
            GameObject root = new GameObject("SpikeFloor");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(floor, Quaternion.Euler(0f, roomRotation.eulerAngles.y, 0f));

            DemoSpikeFloorTrap trap = root.AddComponent<DemoSpikeFloorTrap>();
            trap.BuildRing();
        }

        private static void CacheSpearTemplate(Transform mapRoot)
        {
            if (cachedSpearPrefab != null)
            {
                return;
            }

            cachedSpearPrefab = LoadSpearPrefabAsset();
            if (cachedSpearPrefab != null)
            {
                return;
            }

            GameObject found = FindHeadSpearInMap(mapRoot);
            if (found == null)
            {
                return;
            }

            GameObject template = Object.Instantiate(found);
            template.name = "head_Spear_Template";
            template.SetActive(false);
            Object.DontDestroyOnLoad(template);
            DisableColliders(template);
            cachedSpearPrefab = template;
        }

        private static GameObject LoadSpearPrefabAsset()
        {
#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(SpearAssetGuid);
            if (!string.IsNullOrEmpty(path))
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
#endif
            return null;
        }

        private static GameObject FindHeadSpearInMap(Transform mapRoot)
        {
            if (mapRoot == null)
            {
                return null;
            }

            Transform[] transforms = mapRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                if (current != null && IsHeadSpearName(current.name))
                {
                    return current.gameObject;
                }
            }

            return null;
        }

        private static bool IsHeadSpearName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            string compact = objectName.Replace(" ", string.Empty).Replace("_", string.Empty);
            return compact.IndexOf("headspear", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void BuildRing()
        {
            List<Transform> spikeList = new List<Transform>(SpikeCount);
            List<Transform> tileTransforms = new List<Transform>(SpikeCount);
            List<Vector3> hiddenList = new List<Vector3>(SpikeCount);
            List<Vector3> activeList = new List<Vector3>(SpikeCount);
            List<Renderer> tileList = new List<Renderer>(SpikeCount);

            for (int i = 0; i < SpikeCount; i++)
            {
                float angle = i * (360f / SpikeCount) * Mathf.Deg2Rad;
                Vector3 localOffset = new Vector3(Mathf.Cos(angle) * RingRadius, 0f, Mathf.Sin(angle) * RingRadius);
                Vector3 world = transform.TransformPoint(localOffset);
                Vector3 floor = SnapSpikeToFloor(world);

                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = "SpikeTile";
                tile.transform.SetParent(transform, false);
                tile.transform.position = floor;
                tile.transform.localScale = new Vector3(TileSize, TileHeight, TileSize);
                SetColor(tile.GetComponent<Renderer>(), tileIdleColor);
                DisableColliders(tile);

                GameObject spike = CreateSpearVisual(spikeList.Count + 1);
                AlignSpearUpright(spike.transform);
                PlaceSpearCycle(spike.transform, floor, out Vector3 hidden, out Vector3 active);
                spike.transform.position = hidden;

                spikeList.Add(spike.transform);
                tileTransforms.Add(tile.transform);
                hiddenList.Add(hidden);
                activeList.Add(active);
                tileList.Add(tile.GetComponent<Renderer>());
            }

            spikes = spikeList.ToArray();
            tiles = tileTransforms.ToArray();
            hiddenPositions = hiddenList.ToArray();
            activePositions = activeList.ToArray();
            tileRenderers = tileList.ToArray();
        }

        private GameObject CreateSpearVisual(int index)
        {
            GameObject template = cachedSpearPrefab;
            if (template == null)
            {
                Debug.LogWarning("[DemoSpikeFloorTrap] head_Spear를 찾지 못해 기본 가시를 사용합니다.");
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fallback.name = $"Spike_{index}";
                fallback.transform.localScale = new Vector3(0.16f, 0.28f, 0.16f);
                DisableColliders(fallback);
                return fallback;
            }

            GameObject spike = Instantiate(template, transform, false);
            spike.name = $"head_Spear_{index}";
            spike.SetActive(true);
            DisableColliders(spike);

            Vector3 scale = spike.transform.localScale;
            scale.y = SpearScaleY;
            if (!template.scene.IsValid())
            {
                scale.x = 0.5f;
                scale.z = 0.5f;
            }

            spike.transform.localScale = scale;
            return spike;
        }

        private static void AlignSpearUpright(Transform spear)
        {
            if (!TryGetWorldBounds(spear, out Bounds bounds))
            {
                return;
            }

            Vector3 size = bounds.size;
            if (size.z >= size.y && size.z >= size.x)
            {
                spear.Rotate(90f, 0f, 0f, Space.Self);
            }
            else if (size.x >= size.y && size.x >= size.z)
            {
                spear.Rotate(0f, 0f, -90f, Space.Self);
            }
        }

        private static void PlaceSpearCycle(Transform spear, Vector3 floor, out Vector3 hidden, out Vector3 active)
        {
            spear.position = floor;
            if (!TryGetWorldBounds(spear, out Bounds bounds))
            {
                hidden = floor + Vector3.down * SpearScaleY;
                active = floor + Vector3.down * (SpearScaleY - SpearVisibleHeight);
                return;
            }

            float pivotToTop = bounds.max.y - spear.position.y;

            hidden = floor;
            hidden.y = floor.y - pivotToTop - 0.02f;

            active = floor;
            active.y = floor.y + SpearVisibleHeight - pivotToTop;
        }

        private static bool TryGetWorldBounds(Transform target, out Bounds bounds)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            bounds = new Bounds(target.position, Vector3.zero);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static Vector3 SnapToFloor(Vector3 position)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return position;
        }

        private static Vector3 SnapSpikeToFloor(Vector3 position)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 1.2f, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return position;
        }

        private void Update()
        {
            if (spikes.Length == 0)
            {
                return;
            }

            float cycle = hiddenDuration + warningDuration + activeDuration;
            elapsed += Time.deltaTime;
            float t = elapsed % cycle;

            bool warning = t >= hiddenDuration && t < hiddenDuration + warningDuration;
            bool active = t >= hiddenDuration + warningDuration;
            if (active && !wasActive)
            {
                DemoDungeonAudio.PlaySpike(transform.position);
            }

            wasActive = active;

            for (int i = 0; i < spikes.Length; i++)
            {
                Transform spike = spikes[i];
                if (spike == null)
                {
                    continue;
                }

                Vector3 target = active ? activePositions[i] : hiddenPositions[i];
                Vector3 current = spike.position;
                if ((current - target).sqrMagnitude > 0.0001f)
                {
                    spike.position = Vector3.Lerp(current, target, Time.deltaTime * 12f);
                }
            }

            Color tileColor = active ? tileActiveColor : (warning ? tileWarningColor : tileIdleColor);
            ApplyTileColor(tileColor);

            if (active)
            {
                DetectHits();
            }
        }

        private void DetectHits()
        {
            if (DemoCombatRoster.FindNearestAlly(transform.position, RingRadius + 1f, false) == null)
            {
                return;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position + Vector3.up * 0.6f,
                RingRadius + TileSize,
                overlapHits,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int h = 0; h < hitCount; h++)
            {
                TryHit(overlapHits[h]);
            }
        }

        private void TryHit(Collider other)
        {
            if (other == null || other.transform == transform || other.transform.IsChildOf(transform))
            {
                return;
            }

            if (!DemoCombatTargetUtil.TryResolveAlly(other, out Transform body))
            {
                return;
            }

            for (int i = 0; i < tiles.Length; i++)
            {
                Transform tile = tiles[i];
                if (!IsInsideTile(tile, body.position))
                {
                    continue;
                }

                if (!DemoCombatTargetUtil.TryConsumeHit(nextHitTime, body.GetInstanceID(), hitCooldown))
                {
                    return;
                }

                DemoCombatTargetUtil.DamageAlly(body, damage, tile != null ? tile.position : transform.position);
                return;
            }
        }

        private static bool IsInsideTile(Transform tile, Vector3 worldPosition)
        {
            if (tile == null)
            {
                return false;
            }

            Vector3 local = tile.InverseTransformPoint(worldPosition);
            float half = TileSize * 0.5f;
            return Mathf.Abs(local.x) <= half && Mathf.Abs(local.z) <= half;
        }

        private static void DisableColliders(GameObject target)
        {
            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        private void ApplyTileColor(Color color)
        {
            if (appliedTileColor == color)
            {
                return;
            }

            appliedTileColor = color;
            tileColorBlock.SetColor("_BaseColor", color);
            tileColorBlock.SetColor("_Color", color);
            for (int i = 0; i < tileRenderers.Length; i++)
            {
                Renderer renderer = tileRenderers[i];
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(tileColorBlock);
                }
            }
        }

        private static void SetColor(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.GetPropertyBlock(tileColorBlock);
            tileColorBlock.SetColor("_BaseColor", color);
            tileColorBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(tileColorBlock);
        }
    }
}
