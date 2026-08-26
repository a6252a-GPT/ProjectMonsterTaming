using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleFoundationPlayableStage : MonoBehaviour // 현재 Cell 기반 성을 Play Mode에서 검증한다
    {
        [SerializeField] private int seed = 10801;
        [SerializeField, Range(2, 4)] private int defenseLayerCount = 3;
        [SerializeField] private HexCastleTheme theme = HexCastleTheme.CentralCompartment;
        [SerializeField, Range(0, 5)] private int entryDirection;
        [SerializeField] private HexCastleThemeOneRules rules;
        [SerializeField] private bool sceneWasDirty;
        [SerializeField, Min(1)] private int assaultLaneCount = 3;
        [SerializeField, Min(0.1f)] private float assaultMoveSpeed = 2.8f;
        [SerializeField, Min(1f)] private float assaultDamage = 90f;
        [SerializeField, Min(0.05f)] private float assaultInterval = 0.45f;
        [SerializeField, Min(1f)] private float assaultHealth = 460f;
        [SerializeField] private HexCastleGarrisonCatalog garrisonCatalog;

        private readonly List<HexCastleAssaultUnit> assaultUnits = new List<HexCastleAssaultUnit>();
        private readonly List<HexCastleBarracksRuntime> barracksRuntimes =
            new List<HexCastleBarracksRuntime>();
        private HexCastleTurretCombatWorld combatWorld;
        private HexCastleGarrisonWorld garrisonWorld;
        private bool started;

        public int Seed => seed;
        public int DefenseLayerCount => defenseLayerCount;
        public HexCastleTheme Theme => theme;
        public bool SceneWasDirty => sceneWasDirty;
        public bool IsConfigured => rules != null && defenseLayerCount >= 2 && defenseLayerCount <= 4 &&
                                    HexCastleSilhouettePlanner.SupportedThemes.Contains(theme);
        public int AssaultUnitCount => assaultUnits.Count(value => value != null);
        public int BarracksRuntimeCount => barracksRuntimes.Count(value => value != null);
        public int GarrisonUnitCount => garrisonWorld == null ? 0 : garrisonWorld.AliveUnitCount;

        public void Configure(
            int targetSeed,
            int targetDefenseLayerCount,
            HexCastleThemeOneRules targetRules,
            bool targetSceneWasDirty,
            int targetEntryDirection = 0,
            HexCastleGarrisonCatalog targetGarrisonCatalog = null)
        {
            Configure(
                targetSeed,
                targetDefenseLayerCount,
                HexCastleTheme.CentralCompartment,
                targetRules,
                targetSceneWasDirty,
                targetEntryDirection,
                targetGarrisonCatalog);
        }

        public void Configure(
            int targetSeed,
            int targetDefenseLayerCount,
            HexCastleTheme targetTheme,
            HexCastleThemeOneRules targetRules,
            bool targetSceneWasDirty,
            int targetEntryDirection = 0,
            HexCastleGarrisonCatalog targetGarrisonCatalog = null)
        {
            seed = targetSeed;
            defenseLayerCount = Mathf.Clamp(targetDefenseLayerCount, 2, 4);
            theme = targetTheme;
            entryDirection = PositiveModulo(targetEntryDirection, HexCoordinates.Directions.Length);
            rules = targetRules != null
                ? targetRules
                : throw new ArgumentNullException(nameof(targetRules));
            sceneWasDirty = targetSceneWasDirty;
            garrisonCatalog = targetGarrisonCatalog;
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                StartAssault();
            }
        }

        [ContextMenu("칸 기반 공략 재시작")]
        public void StartAssault()
        {
            if (!Application.isPlaying || started || !IsConfigured)
            {
                return;
            }

            var candidate = new HexCastleGenerationPipeline().GenerateFoundation(
                seed,
                defenseLayerCount,
                theme,
                rules.Tuning);
            if (!candidate.Validation.IsValid)
            {
                throw new InvalidOperationException(string.Join("\n", candidate.Validation.Errors));
            }

            var runtimeCells = GetComponentsInChildren<HexCastleCellRuntime>(true)
                .ToDictionary(value => value.Coordinates);
            if (runtimeCells.Count != candidate.Layout.Cells.Count ||
                candidate.Layout.Cells.Keys.Any(coordinates => !runtimeCells.ContainsKey(coordinates)))
            {
                throw new InvalidOperationException(
                    "플레이 미리보기의 Cell 배치가 현재 시드의 결정론적 Layout과 다릅니다.");
            }

            foreach (var cell in runtimeCells.Values)
            {
                cell.InitializeState();
            }

            var cameraController = FindFirstObjectByType<HexCastleCameraController>();
            cameraController?.ConfigureBounds(
                candidate.Layout.BattlefieldRadius,
                HexSpatialContract.CellOuterRadius);
            combatWorld = GetComponent<HexCastleTurretCombatWorld>();
            if (combatWorld == null)
            {
                throw new InvalidOperationException("플레이 미리보기에 Hex 포탑 전투 World가 없습니다.");
            }

            SetupGarrisonRuntime(runtimeCells);
            ClearAssaultUnits();
            var unitsRoot = new GameObject("03_PlayableAssaultUnits").transform;
            unitsRoot.SetParent(transform, false);
            var planner = new HexRoutePlanner();
            var half = Mathf.Max(0, assaultLaneCount / 2);
            for (var lane = 0; lane < assaultLaneCount; lane++)
            {
                var offset = lane - half;
                var direction = PositiveModulo(entryDirection + offset, HexCoordinates.Directions.Length);
                var start = HexCoordinates.Directions[direction] * candidate.Layout.BattlefieldRadius;
                var route = planner.FindMinimumBreachRoute(candidate.Layout, start);
                if (route == null || !route.IsComplete)
                {
                    continue;
                }

                var unitObject = GameObject.CreatePrimitive(lane == half
                    ? PrimitiveType.Capsule
                    : PrimitiveType.Sphere);
                unitObject.name = $"PlayableAssaultUnit_{lane + 1:00}_D{direction}";
                unitObject.transform.SetParent(unitsRoot, false);
                unitObject.transform.localScale = lane == half
                    ? new Vector3(0.48f, 0.64f, 0.48f)
                    : Vector3.one * 0.58f;
                var collider = unitObject.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                var renderer = unitObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_BaseColor", lane == half
                        ? new Color(1f, 0.18f, 0.08f)
                        : new Color(1f, 0.48f, 0.08f));
                    block.SetColor("_Color", lane == half
                        ? new Color(1f, 0.18f, 0.08f)
                        : new Color(1f, 0.48f, 0.08f));
                    renderer.SetPropertyBlock(block);
                }

                var unit = unitObject.AddComponent<HexCastleAssaultUnit>();
                unit.ConfigureForCells(
                    route,
                    runtimeCells,
                    HexSpatialContract.CellOuterRadius,
                    transform.position,
                    assaultMoveSpeed + lane * 0.12f,
                    assaultDamage,
                    assaultInterval,
                    assaultHealth);
                assaultUnits.Add(unit);
            }

            combatWorld.RebuildRegistry(transform);
            started = true;
        }

        private void ClearAssaultUnits()
        {
            foreach (var unit in assaultUnits)
            {
                if (unit == null)
                {
                    continue;
                }

                combatWorld?.UnregisterAssaultUnit(unit);
                if (Application.isPlaying)
                {
                    Destroy(unit.gameObject);
                }
                else
                {
                    DestroyImmediate(unit.gameObject);
                }
            }

            assaultUnits.Clear();
        }

        private void SetupGarrisonRuntime(
            IReadOnlyDictionary<HexCoordinates, HexCastleCellRuntime> runtimeCells)
        {
            ClearGarrisonRuntime();
            garrisonCatalog ??= Resources.Load<HexCastleGarrisonCatalog>(
                HexCastleGarrisonCatalog.DefaultResourcesPath);
            if (garrisonCatalog == null || !garrisonCatalog.IsComplete)
            {
                throw new InvalidOperationException(
                    "Hex 병영의 기사·농부 정식 외형 카탈로그가 없거나 불완전합니다.");
            }

            garrisonWorld = GetComponent<HexCastleGarrisonWorld>();
            if (garrisonWorld == null)
            {
                garrisonWorld = gameObject.AddComponent<HexCastleGarrisonWorld>();
            }

            garrisonWorld.Configure(
                garrisonCatalog,
                runtimeCells,
                transform.position,
                HexSpatialContract.CellOuterRadius,
                seed,
                combatWorld,
                rules.Tuning);
            foreach (var cell in runtimeCells.Values
                         .Where(value => value != null &&
                                         (value.BuildingRole == HexCastleBuildingRole.KnightBarracks ||
                                          value.BuildingRole == HexCastleBuildingRole.FarmerBarracks))
                         .OrderBy(value => value.Coordinates))
            {
                var barracks = cell.GetComponent<HexCastleBarracksRuntime>();
                if (barracks == null)
                {
                    barracks = cell.gameObject.AddComponent<HexCastleBarracksRuntime>();
                }

                barracks.Configure(cell, garrisonWorld, rules.Tuning);
                barracksRuntimes.Add(barracks);
            }
        }

        private void ClearGarrisonRuntime()
        {
            foreach (var barracks in barracksRuntimes)
            {
                barracks?.Shutdown();
            }

            barracksRuntimes.Clear();
            garrisonWorld?.Shutdown();
        }

        private void OnDestroy()
        {
            ClearAssaultUnits();
            ClearGarrisonRuntime();
        }

        private static int PositiveModulo(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
