using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.FoodRiot
{
    [DisallowMultipleComponent]
    public sealed class FoodRiotFoodVisual : MonoBehaviour, IUnitSpawnPreparation // 음식 외형·통통 이동 연출
    {
        [Header("Appearance")]
        [SerializeField] private Transform motionRoot; // 판정과 분리된 연출 루트
        [SerializeField] private Transform modelRoot; // 교체 Mesh 기준점
        [SerializeField] private MeshFilter meshFilter; // 선택 음식 Mesh
        [SerializeField] private MeshRenderer meshRenderer; // 선택 음식 Material
        [SerializeField] private GameObject[] foodPrefabs; // 음식 후보 원본
        [SerializeField, Min(0.1f)] private float targetMaxSize = 0.95f; // 음식 최대 축 크기
        [SerializeField] private float groundOffset = 0.02f; // 바닥 여유 높이

        [Header("Bounce")]
        [SerializeField, Range(0.35f, 0.8f)] private float hopDuration = 0.52f; // 한 번 통통 뛰는 시간
        [SerializeField, Range(0.05f, 0.5f)] private float hopHeight = 0.23f; // 최대 점프 높이
        [SerializeField, Range(0.05f, 0.3f)] private float landingRatio = 0.16f; // 착지 복구 구간
        [SerializeField, Range(0f, 0.4f)] private float squashAmount = 0.22f; // 착지 찌그러짐 강도
        [SerializeField, Range(0f, 15f)] private float leanAngle = 6f; // 공중 기울기

        private Vector3 baseMotionPosition;
        private Quaternion baseMotionRotation;
        private Vector3 baseMotionScale;
        private float runtimeHopDuration;
        private float runtimeHopHeight;
        private float cycleTime;
        private float rollDirection;
        private bool baseStateReady;
        private bool prepared;

        public string CurrentFoodName { get; private set; } = string.Empty;
        public Transform MotionRoot => motionRoot;
        public MeshRenderer FoodRenderer => meshRenderer;
        public int FoodPrefabCount => foodPrefabs?.Length ?? 0;

        private void Awake()
        {
            EnsureReferences();
            CaptureBaseState();
        }

        private void OnEnable()
        {
            ResetMotion();
        }

        private void Update()
        {
            TickVisual(Time.deltaTime);
        }

        public bool PrepareForSpawn(UnitSpawnRequest request)
        {
            EnsureReferences();
            CaptureBaseState();
            if (motionRoot == null || modelRoot == null || meshFilter == null || meshRenderer == null)
            {
                Debug.LogError("Food Riot food visual references are missing.", this);
                return false;
            }

            if (foodPrefabs == null || foodPrefabs.Length == 0)
            {
                Debug.LogError("Food Riot food prefabs are missing.", this);
                return false;
            }

            var seed = request.AppearanceSeed == 0 ? StableHash(request.UnitId) : request.AppearanceSeed;
            var startIndex = ResolveAppearanceIndex(request, seed);
            if (!TryApplyAppearance(startIndex))
            {
                return false;
            }

            runtimeHopDuration = hopDuration * Mathf.Lerp(0.88f, 1.12f, Hash01(seed, 17));
            runtimeHopHeight = hopHeight * Mathf.Lerp(0.85f, 1.15f, Hash01(seed, 31));
            rollDirection = Hash01(seed, 47) < 0.5f ? -1f : 1f;
            cycleTime = runtimeHopDuration * Hash01(seed, 61); // 개체별 통통 위상 분산
            prepared = true;
            TickVisual(0f);
            return true;
        }

        public void TickVisual(float deltaTime) // Runtime·검증에서 같은 곡선 사용
        {
            if (!prepared || motionRoot == null)
            {
                return;
            }

            cycleTime += Mathf.Max(0f, deltaTime);
            var duration = Mathf.Max(0.1f, runtimeHopDuration);
            var phase = Mathf.Repeat(cycleTime / duration, 1f);
            var contactRatio = Mathf.Clamp(landingRatio, 0.05f, 0.3f);
            var hop = 0f;
            var scale = Vector3.one;
            var pitch = 0f;

            if (phase < contactRatio)
            {
                var recovery = Mathf.SmoothStep(0f, 1f, phase / contactRatio);
                var compression = 1f - recovery;
                scale = new Vector3(
                    1f + compression * squashAmount * 0.65f,
                    1f - compression * squashAmount,
                    1f + compression * squashAmount * 0.65f);
                pitch = -leanAngle * compression * 0.35f;
            }
            else
            {
                var airPhase = (phase - contactRatio) / (1f - contactRatio);
                hop = Mathf.Sin(airPhase * Mathf.PI);
                var stretch = hop * squashAmount * 0.25f;
                scale = new Vector3(1f - stretch * 0.5f, 1f + stretch, 1f - stretch * 0.5f);
                pitch = leanAngle * hop;
            }

            var roll = Mathf.Sin(phase * Mathf.PI * 2f) * leanAngle * 0.35f * rollDirection;
            motionRoot.localPosition = baseMotionPosition + Vector3.up * (hop * runtimeHopHeight);
            motionRoot.localScale = Vector3.Scale(baseMotionScale, scale);
            motionRoot.localRotation = baseMotionRotation * Quaternion.Euler(pitch, 0f, roll);
        }

        private bool TryApplyAppearance(int startIndex)
        {
            for (var offset = 0; offset < foodPrefabs.Length; offset++)
            {
                var index = (startIndex + offset) % foodPrefabs.Length;
                var sourcePrefab = foodPrefabs[index];
                if (sourcePrefab == null)
                {
                    continue;
                }

                var sourceFilter = sourcePrefab.GetComponentInChildren<MeshFilter>(true);
                var sourceRenderer = sourcePrefab.GetComponentInChildren<MeshRenderer>(true);
                var sourceMesh = sourceFilter == null ? null : sourceFilter.sharedMesh;
                if (sourceMesh == null || sourceRenderer == null)
                {
                    continue;
                }

                meshFilter.sharedMesh = sourceMesh;
                meshRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
                meshRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                meshRenderer.receiveShadows = sourceRenderer.receiveShadows;
                NormalizeModel(sourceMesh.bounds);
                CurrentFoodName = sourcePrefab.name;
                GetComponent<UnitVisualFeedback>()?.RefreshRenderers();
                return true;
            }

            Debug.LogError("Food Riot food prefabs have no usable MeshRenderer.", this);
            return false;
        }

        private void NormalizeModel(Bounds bounds)
        {
            var largestAxis = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            var scale = targetMaxSize / Mathf.Max(0.001f, largestAxis);
            modelRoot.localRotation = Quaternion.identity;
            modelRoot.localScale = Vector3.one * scale;
            modelRoot.localPosition = new Vector3(
                -bounds.center.x * scale,
                groundOffset - bounds.min.y * scale,
                -bounds.center.z * scale);
        }

        private void OnDisable()
        {
            prepared = false;
            ResetMotion();
        }

        private void EnsureReferences()
        {
            if (motionRoot == null)
            {
                motionRoot = transform.Find("VisualMotionRoot");
            }

            if (modelRoot == null && motionRoot != null)
            {
                modelRoot = motionRoot.Find("FoodModel");
            }

            if (meshFilter == null && modelRoot != null)
            {
                meshFilter = modelRoot.GetComponent<MeshFilter>();
            }

            if (meshRenderer == null && modelRoot != null)
            {
                meshRenderer = modelRoot.GetComponent<MeshRenderer>();
            }
        }

        private void CaptureBaseState()
        {
            if (baseStateReady || motionRoot == null)
            {
                return;
            }

            baseMotionPosition = motionRoot.localPosition;
            baseMotionRotation = motionRoot.localRotation;
            baseMotionScale = motionRoot.localScale;
            baseStateReady = true;
        }

        private void ResetMotion()
        {
            if (!baseStateReady || motionRoot == null)
            {
                return;
            }

            motionRoot.localPosition = baseMotionPosition;
            motionRoot.localRotation = baseMotionRotation;
            motionRoot.localScale = baseMotionScale;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                var hash = (int)2166136261;
                foreach (var character in value ?? string.Empty)
                {
                    hash = (hash ^ character) * 16777619;
                }

                return hash == 0 ? 1 : hash;
            }
        }

        private int ResolveAppearanceIndex(UnitSpawnRequest request, int seed)
        {
            if (request.AppearanceSeed == 0 &&
                foodPrefabs.Length == 12 &&
                TryReadTrailingNumber(request.UnitId, out var sequence))
            {
                return (sequence * 5 + 8) % foodPrefabs.Length; // 첫 12기 중복 없는 섞인 순서
            }

            return (seed & int.MaxValue) % foodPrefabs.Length;
        }

        private static bool TryReadTrailingNumber(string value, out int number)
        {
            number = 0;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            var firstDigit = value.Length;
            while (firstDigit > 0 && char.IsDigit(value[firstDigit - 1]))
            {
                firstDigit--;
            }

            if (firstDigit == value.Length)
            {
                return false;
            }

            for (var index = firstDigit; index < value.Length; index++)
            {
                var digit = value[index] - '0';
                if (number > (int.MaxValue - digit) / 10)
                {
                    return false;
                }

                number = number * 10 + digit;
            }

            return true;
        }

        private static float Hash01(int seed, int salt)
        {
            unchecked
            {
                var value = (uint)(seed + salt * -1640531527);
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                value *= 3266489917u;
                value ^= value >> 16;
                return (value & 0x00ffffffu) / 16777215f;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Transform visualMotionRoot,
            Transform foodModelRoot,
            MeshFilter targetFilter,
            MeshRenderer targetRenderer,
            GameObject[] candidates)
        {
            motionRoot = visualMotionRoot;
            modelRoot = foodModelRoot;
            meshFilter = targetFilter;
            meshRenderer = targetRenderer;
            foodPrefabs = candidates;
            baseStateReady = false;
            CaptureBaseState();
            if (foodPrefabs != null && foodPrefabs.Length > 0)
            {
                TryApplyAppearance(0);
            }
        }
#endif
    }
}
