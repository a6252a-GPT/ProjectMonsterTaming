using System;
using System.Collections;
using System.Collections.Generic;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectMT.Contents.CastleRaid
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class GeneratedCastleRuntimeStage : MonoBehaviour // 생성 배치를 기존 침공 전투에 연결
    {
        [SerializeField] private CastleRaidController raidController;
        [SerializeField] private CastleRaidCameraController cameraController;
        [SerializeField] private CastleDeploymentZone deploymentZone;
        [SerializeField] private Transform innerEntry;
        [SerializeField] private CastleTarget[] targets;
        [SerializeField] private NavMeshSurface navigationSurface;
        [SerializeField] private Collider[] targetColliders;
        [SerializeField] private NavMeshObstacle[] targetObstacles;
        [SerializeField] private Vector2 worldCenter;
        [SerializeField] private Vector2 worldSize = new Vector2(20f, 20f);
        [SerializeField, Min(1f)] private float defaultCameraSize = 8.5f;
        [SerializeField, Min(0.1f)] private float minimumCameraSize = 5f;
        [SerializeField, Min(1f)] private float maximumCameraSize = 11.5f;
        [SerializeField] private bool buildNavigationOnAwake = true;
        [SerializeField] private bool initializeSeedPlaytest;
        [SerializeField] private MonsterCatalog playtestMonsterCatalog;

#if UNITY_EDITOR
        [SerializeField] private GameObject previewHiddenStage;
        [SerializeField] private bool previewHiddenStageWasActive;
        [SerializeField] private bool previewHiddenStageHadActiveOverride;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private float previousOrthographicSize;
        [SerializeField] private Vector3 previousCameraPosition;
        [SerializeField] private bool previewPresentationRestored;
#endif

        private DebugContentExit debugExit;
        private bool ownsPlaytestContext;

        public CastleTarget[] Targets => targets;
        public CastleDeploymentZone DeploymentZone => deploymentZone;
        public Vector2 WorldCenter => worldCenter;
        public Vector2 WorldSize => worldSize;

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ValidateReferences();
            if (buildNavigationOnAwake)
            {
                BuildNavigation();
            }

            cameraController?.ConfigureRuntimeBounds(
                worldCenter,
                worldSize,
                defaultCameraSize,
                minimumCameraSize,
                maximumCameraSize);
            raidController.ConfigureRuntimeStage(deploymentZone, innerEntry, targets);
        }

        private IEnumerator Start()
        {
#if UNITY_EDITOR
            yield return null; // 정식 ContentFlow가 먼저 초기화할 기회를 준다
            if (!initializeSeedPlaytest || raidController == null || raidController.IsRunning)
            {
                yield break;
            }

            debugExit = new DebugContentExit();
            debugExit.Exited += HandlePlaytestExit;
            var playtestParty = CreatePlaytestParty();
            if (playtestParty == null || playtestParty.Units.Length == 0)
            {
                Debug.LogError("Generated Castle playtest could not resolve a formal Monster party.", this);
                yield break;
            }

            var startData = new CastleRaidStartData(playtestParty, 3);
            var context = new ContentContext(
                new ContentRunInfo(
                    new ContentId("castle_raid"),
                    "generated_playtest",
                    ContentRunMode.SeedTest),
                startData,
                debugExit);
            raidController.Initialize(context);
            ownsPlaytestContext = true;
            Debug.Log("Generated Castle playtest started with the seed party.", this);
#else
            yield break;
#endif
        }

        private void OnDestroy()
        {
            if (debugExit != null)
            {
                debugExit.Exited -= HandlePlaytestExit;
            }

            if (ownsPlaytestContext && raidController != null)
            {
                raidController.Shutdown();
            }

            if (Application.isPlaying && navigationSurface != null)
            {
                navigationSurface.RemoveData();
            }

#if UNITY_EDITOR
            RestorePreviewPresentation();
#endif
        }

        private void HandlePlaytestExit(ContentOutcome outcome, IContentResultData result)
        {
            Debug.Log($"Generated Castle playtest finished. Outcome={outcome}", this);
        }

#if UNITY_EDITOR
        private BattlePartySnapshot CreatePlaytestParty()
        {
            if (playtestMonsterCatalog == null)
            {
                return null;
            }

            var definitions = new List<MonsterDefinition>(5);
            var preferredIds = new[] { "lumi_01", "shell_01", "aru_01", "ru_01", "lucy_01" };
            foreach (var monsterId in preferredIds)
            {
                if (playtestMonsterCatalog.TryGet(monsterId, out var definition) &&
                    HasFormalVisual(definition))
                {
                    definitions.Add(definition);
                }
            }

            if (definitions.Count == 0)
            {
                foreach (var definition in playtestMonsterCatalog.Definitions)
                {
                    if (HasFormalVisual(definition))
                    {
                        definitions.Add(definition);
                        if (definitions.Count >= 5)
                        {
                            break;
                        }
                    }
                }
            }

            var units = new BattleUnitSnapshot[Mathf.Min(5, definitions.Count)];
            for (var index = 0; index < units.Length; index++)
            {
                var definition = definitions[index];
                units[index] = new BattleUnitSnapshot(
                    definition.MonsterId,
                    new UnitStatsSnapshot
                    {
                        maxHealth = definition.MaxHealth,
                        damage = definition.AttackPower,
                        defense = definition.Defense,
                        moveSpeed = definition.MoveSpeed,
                        attackRange = definition.AttackRange,
                        attackInterval = 1f / Mathf.Max(0.01f, definition.AttackSpeed),
                        projectileSpeed = definition.Ranged ? 9f : 0f,
                        ranged = definition.Ranged,
                        criticalDamageMultiplier = 1.5f
                    },
                    definition.VisualTint,
                    definition.RuntimeAssetKey,
                    definition.RuntimeAssetSet,
                    displayName: definition.DisplayName);
            }

            return new BattlePartySnapshot(units);
        }

        private static bool HasFormalVisual(MonsterDefinition definition)
        {
            return definition != null && definition.RuntimeAssetSet != null &&
                   definition.RuntimeAssetSet.VisualAdapterPrefab != null;
        }
#endif

        private void ValidateReferences()
        {
            if (raidController == null || deploymentZone == null || innerEntry == null ||
                targets == null || targets.Length == 0 || navigationSurface == null)
            {
                throw new InvalidOperationException("생성 Castle Stage의 런타임 참조가 완성되지 않았습니다.");
            }
        }

        private void BuildNavigation()
        {
            var colliderStates = CaptureStates(targetColliders);
            var obstacleStates = CaptureStates(targetObstacles);
            try
            {
                SetEnabled(targetObstacles, false);
                SetEnabled(targetColliders, false);
                navigationSurface.BuildNavMesh(); // 평지 NavMesh를 먼저 만들고 구조물은 Carving으로 막는다
            }
            finally
            {
                RestoreStates(targetColliders, colliderStates);
                RestoreStates(targetObstacles, obstacleStates);
            }
        }

        private static bool[] CaptureStates<T>(T[] components)
            where T : Behaviour
        {
            if (components == null)
            {
                return Array.Empty<bool>();
            }

            var result = new bool[components.Length];
            for (var index = 0; index < components.Length; index++)
            {
                result[index] = components[index] != null && components[index].enabled;
            }

            return result;
        }

        private static bool[] CaptureStates(Collider[] colliders)
        {
            if (colliders == null)
            {
                return Array.Empty<bool>();
            }

            var result = new bool[colliders.Length];
            for (var index = 0; index < colliders.Length; index++)
            {
                result[index] = colliders[index] != null && colliders[index].enabled;
            }

            return result;
        }

        private static void SetEnabled<T>(T[] components, bool enabled)
            where T : Behaviour
        {
            if (components == null)
            {
                return;
            }

            foreach (var component in components)
            {
                if (component != null)
                {
                    component.enabled = enabled;
                }
            }
        }

        private static void SetEnabled(Collider[] colliders, bool enabled)
        {
            if (colliders == null)
            {
                return;
            }

            foreach (var collider in colliders)
            {
                if (collider != null)
                {
                    collider.enabled = enabled;
                }
            }
        }

        private static void RestoreStates<T>(T[] components, bool[] states)
            where T : Behaviour
        {
            if (components == null)
            {
                return;
            }

            for (var index = 0; index < components.Length && index < states.Length; index++)
            {
                if (components[index] != null)
                {
                    components[index].enabled = states[index];
                }
            }
        }

        private static void RestoreStates(Collider[] colliders, bool[] states)
        {
            if (colliders == null)
            {
                return;
            }

            for (var index = 0; index < colliders.Length && index < states.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = states[index];
                }
            }
        }

        public void Configure(
            CastleRaidController controller,
            CastleRaidCameraController raidCamera,
            CastleDeploymentZone zone,
            Transform palaceEntry,
            CastleTarget[] castleTargets,
            NavMeshSurface surface,
            Collider[] colliders,
            NavMeshObstacle[] obstacles,
            Vector2 boundsCenter,
            Vector2 boundsSize,
            float cameraSize,
            float minimumSize,
            float maximumSize,
            MonsterCatalog monsterCatalog = null,
            bool seedPlaytest = false)
        {
            raidController = controller;
            cameraController = raidCamera;
            deploymentZone = zone;
            innerEntry = palaceEntry;
            targets = castleTargets ?? Array.Empty<CastleTarget>();
            navigationSurface = surface;
            targetColliders = colliders ?? Array.Empty<Collider>();
            targetObstacles = obstacles ?? Array.Empty<NavMeshObstacle>();
            worldCenter = boundsCenter;
            worldSize = Vector2.Max(Vector2.one, boundsSize);
            defaultCameraSize = Mathf.Max(1f, cameraSize);
            minimumCameraSize = Mathf.Max(0.1f, minimumSize);
            maximumCameraSize = Mathf.Max(defaultCameraSize, maximumSize);
            playtestMonsterCatalog = monsterCatalog;
            buildNavigationOnAwake = true;
            initializeSeedPlaytest = seedPlaytest;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            CastleRaidController controller,
            CastleRaidCameraController raidCamera,
            CastleDeploymentZone zone,
            Transform palaceEntry,
            CastleTarget[] castleTargets,
            NavMeshSurface surface,
            Collider[] colliders,
            NavMeshObstacle[] obstacles,
            Vector2 boundsCenter,
            Vector2 boundsSize,
            float cameraSize,
            float minimumSize,
            float maximumSize,
            MonsterCatalog monsterCatalog,
            bool seedPlaytest = true)
        {
            Configure(
                controller,
                raidCamera,
                zone,
                palaceEntry,
                castleTargets,
                surface,
                colliders,
                obstacles,
                boundsCenter,
                boundsSize,
                cameraSize,
                minimumSize,
                maximumSize,
                monsterCatalog,
                seedPlaytest);
        }

        public void EditorPreparePreviewPresentation(
            GameObject stage,
            Camera targetCamera,
            Vector3 groundCenter,
            float orthographicSize)
        {
            previewHiddenStage = stage;
            previewHiddenStageWasActive = stage != null && stage.activeSelf;
            previewHiddenStageHadActiveOverride = HasActiveOverride(stage);
            previewCamera = targetCamera;
            previewPresentationRestored = false;

            if (previewHiddenStageWasActive)
            {
                stage.SetActive(false);
            }

            if (previewCamera == null || !previewCamera.orthographic)
            {
                return;
            }

            previousOrthographicSize = previewCamera.orthographicSize;
            previousCameraPosition = previewCamera.transform.position;
            MoveGroundCenterTo(previewCamera, groundCenter);
            previewCamera.orthographicSize = Mathf.Max(1f, orthographicSize);
        }

        public void RestorePreviewPresentation()
        {
            if (previewPresentationRestored)
            {
                return;
            }

            previewPresentationRestored = true;
            if (previewHiddenStage != null && previewHiddenStage.activeSelf != previewHiddenStageWasActive)
            {
                previewHiddenStage.SetActive(previewHiddenStageWasActive);
            }

            if (previewHiddenStage != null && !previewHiddenStageHadActiveOverride)
            {
                RevertActiveOverride(previewHiddenStage);
            }

            if (previewCamera != null && previewCamera.orthographic)
            {
                previewCamera.orthographicSize = previousOrthographicSize;
                previewCamera.transform.position = previousCameraPosition;
            }
        }

        private static bool HasActiveOverride(GameObject stage)
        {
            if (stage == null || !PrefabUtility.IsPartOfPrefabInstance(stage))
            {
                return false;
            }

            var serializedStage = new SerializedObject(stage);
            return serializedStage.FindProperty("m_IsActive")?.prefabOverride == true;
        }

        private static void RevertActiveOverride(GameObject stage)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(stage))
            {
                return;
            }

            var serializedStage = new SerializedObject(stage);
            var activeProperty = serializedStage.FindProperty("m_IsActive");
            if (activeProperty?.prefabOverride == true)
            {
                PrefabUtility.RevertPropertyOverride(activeProperty, InteractionMode.AutomatedAction);
            }
        }

        private static void MoveGroundCenterTo(Camera targetCamera, Vector3 destination)
        {
            var forward = targetCamera.transform.forward;
            if (Mathf.Abs(forward.y) < 0.001f)
            {
                return;
            }

            var distance = (destination.y - targetCamera.transform.position.y) / forward.y;
            var currentCenter = targetCamera.transform.position + forward * distance;
            targetCamera.transform.position += new Vector3(
                destination.x - currentCenter.x,
                0f,
                destination.z - currentCenter.z);
        }
#endif
    }
}
