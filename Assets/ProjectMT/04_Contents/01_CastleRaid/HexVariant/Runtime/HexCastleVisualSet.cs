using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [Serializable]
    public sealed class HexCastleBuildingVisualEntry
    {
        [SerializeField] private string visualVariantId;
        [SerializeField] private GameObject prefab;

        public string VisualVariantId => visualVariantId ?? string.Empty;
        public GameObject Prefab => prefab;

        public static HexCastleBuildingVisualEntry Create(string id, GameObject targetPrefab)
        {
            return new HexCastleBuildingVisualEntry
            {
                visualVariantId = id,
                prefab = targetPrefab
            };
        }
    }

    [Serializable]
    public sealed class HexCastleTurretHeadVisualEntry
    {
        [SerializeField] private HexCastleTurretWeaponKind weaponKind;
        [SerializeField, Range(1, 3)] private int level = 1;
        [SerializeField] private GameObject prefab;

        public HexCastleTurretWeaponKind WeaponKind => weaponKind;
        public int Level => Mathf.Clamp(level, 1, 3);
        public GameObject Prefab => prefab;

        public static HexCastleTurretHeadVisualEntry Create(
            HexCastleTurretWeaponKind kind,
            int targetLevel,
            GameObject targetPrefab)
        {
            return new HexCastleTurretHeadVisualEntry
            {
                weaponKind = kind,
                level = targetLevel,
                prefab = targetPrefab
            };
        }
    }

    [Serializable]
    public sealed class HexCastleTrapVisualEntry
    {
        [SerializeField] private HexCastleTrapType trapType;
        [SerializeField] private string visualVariantId;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Material materialOverride;
        [SerializeField] private string animationStateName;

        public HexCastleTrapType TrapType => trapType;
        public string VisualVariantId => visualVariantId ?? string.Empty;
        public GameObject Prefab => prefab;
        public Material MaterialOverride => materialOverride;
        public string AnimationStateName => animationStateName ?? string.Empty;

        public static HexCastleTrapVisualEntry Create(
            HexCastleTrapType targetTrapType,
            string variantId,
            GameObject targetPrefab,
            Material targetMaterialOverride,
            string targetAnimationStateName)
        {
            return new HexCastleTrapVisualEntry
            {
                trapType = targetTrapType,
                visualVariantId = variantId ?? string.Empty,
                prefab = targetPrefab,
                materialOverride = targetMaterialOverride,
                animationStateName = targetAnimationStateName ?? string.Empty
            };
        }
    }

    [CreateAssetMenu(
        fileName = "HexCastleVisualSet",
        menuName = "ProjectMT/Castle Raid Hex/Visual Set")]
    public sealed class HexCastleVisualSet : ScriptableObject
    {
        public const string DefaultResourcesPath = "HexCastleRuntimeVisualSet";

        [SerializeField] private string visualThemeId = "KayKitSpring";
        [SerializeField] private GameObject groundTile;
        [SerializeField] private GameObject straightWall;
        [SerializeField] private GameObject cornerAInside;
        [SerializeField] private GameObject cornerAOutside;
        [SerializeField] private GameObject cornerBInside;
        [SerializeField] private GameObject cornerBOutside;
        [SerializeField] private GameObject straightGate;
        [SerializeField] private GameObject cornerAGate;
        [SerializeField] private GameObject wallStub;
        [SerializeField] private GameObject closedGate;
        [SerializeField] private GameObject openGate;
        [SerializeField] private GameObject towerOverlay;
        [SerializeField] private GameObject palace;
        [SerializeField] private Material kayKitMaterial;
        [SerializeField] private List<HexCastleBuildingVisualEntry> buildingVisuals =
            new List<HexCastleBuildingVisualEntry>();
        [SerializeField] private List<HexCastleTurretHeadVisualEntry> turretHeadVisuals =
            new List<HexCastleTurretHeadVisualEntry>();
        [SerializeField] private List<HexCastleTrapVisualEntry> trapVisuals =
            new List<HexCastleTrapVisualEntry>();

        public string VisualThemeId => visualThemeId;
        public GameObject GroundTile => groundTile;
        public GameObject TowerOverlay => towerOverlay;
        public GameObject Palace => palace;
        public GameObject WallStub => wallStub;
        public GameObject ClosedGate => closedGate != null ? closedGate : straightGate;
        public GameObject OpenGate => openGate;
        public Material KayKitMaterial => kayKitMaterial;
        public IReadOnlyList<HexCastleTrapVisualEntry> TrapVisuals => trapVisuals;
        public bool IsRuntimeComplete =>
            straightWall != null && cornerAOutside != null && cornerBOutside != null &&
            wallStub != null && ClosedGate != null && openGate != null &&
            towerOverlay != null && palace != null && kayKitMaterial != null &&
            buildingVisuals != null && buildingVisuals.Count > 0 &&
            buildingVisuals.All(value => value != null && value.Prefab != null &&
                                         !string.IsNullOrWhiteSpace(value.VisualVariantId)) &&
            turretHeadVisuals != null && turretHeadVisuals.Count > 0 &&
            turretHeadVisuals.All(value => value != null && value.Prefab != null) &&
            trapVisuals != null &&
            trapVisuals.Any(value => value != null && value.TrapType == HexCastleTrapType.Snare) &&
            trapVisuals.Any(value => value != null && value.TrapType == HexCastleTrapType.SpikePlate) &&
            trapVisuals.All(value => value != null && value.Prefab != null &&
                                     value.MaterialOverride != null &&
                                     !string.IsNullOrWhiteSpace(value.VisualVariantId) &&
                                     !string.IsNullOrWhiteSpace(value.AnimationStateName));

        public GameObject ResolveBuilding(string visualVariantId)
        {
            return buildingVisuals?.FirstOrDefault(value =>
                value != null && string.Equals(
                    value.VisualVariantId,
                    visualVariantId,
                    StringComparison.Ordinal))?.Prefab;
        }

        public GameObject ResolveTurretHead(HexCastleTurretWeaponKind weaponKind, int level)
        {
            return turretHeadVisuals?.FirstOrDefault(value =>
                value != null && value.WeaponKind == weaponKind &&
                value.Level == Mathf.Clamp(level, 1, 3))?.Prefab;
        }

        public HexCastleTrapVisualEntry ResolveTrapVisual(
            HexCastleTrapType trapType,
            string placementId)
        {
            var variants = trapVisuals?
                .Where(value => value != null && value.TrapType == trapType && value.Prefab != null)
                .OrderBy(value => value.VisualVariantId, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<HexCastleTrapVisualEntry>();
            if (variants.Length == 0)
            {
                return null;
            }

            unchecked
            {
                var hash = 17;
                var numericOrdinal = 0;
                var hasNumericOrdinal = false;
                foreach (var character in placementId ?? string.Empty)
                {
                    hash = hash * 31 + character;
                    if (char.IsDigit(character))
                    {
                        numericOrdinal = numericOrdinal * 10 + character - '0';
                        hasNumericOrdinal = true;
                    }
                }

                var indexSource = hasNumericOrdinal ? numericOrdinal : hash & int.MaxValue;
                return variants[indexSource % variants.Length];
            }
        }

        public GameObject ResolveWall(HexCastleWallVisualKind kind)
        {
            switch (kind)
            {
                case HexCastleWallVisualKind.Straight:
                    return straightWall;
                case HexCastleWallVisualKind.CornerAInside:
                    return cornerAInside;
                case HexCastleWallVisualKind.CornerAOutside:
                    return cornerAOutside;
                case HexCastleWallVisualKind.CornerBInside:
                    return cornerBInside;
                case HexCastleWallVisualKind.CornerBOutside:
                    return cornerBOutside;
                case HexCastleWallVisualKind.StraightGate:
                    return straightGate;
                case HexCastleWallVisualKind.CornerAGate:
                    return cornerAGate;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            GameObject targetGroundTile,
            GameObject targetStraightWall,
            GameObject targetCornerAInside,
            GameObject targetCornerAOutside,
            GameObject targetCornerBInside,
            GameObject targetCornerBOutside,
            GameObject targetStraightGate,
            GameObject targetCornerAGate,
            GameObject targetTowerOverlay,
            GameObject targetPalace)
        {
            visualThemeId = string.IsNullOrWhiteSpace(id) ? "KayKitSpring" : id.Trim();
            groundTile = targetGroundTile;
            straightWall = targetStraightWall;
            cornerAInside = targetCornerAInside;
            cornerAOutside = targetCornerAOutside;
            cornerBInside = targetCornerBInside;
            cornerBOutside = targetCornerBOutside;
            straightGate = targetStraightGate;
            cornerAGate = targetCornerAGate;
            towerOverlay = targetTowerOverlay;
            palace = targetPalace;
        }

        public void EditorConfigureRuntime(
            string id,
            GameObject targetStraightWall,
            GameObject targetCornerA,
            GameObject targetCornerB,
            GameObject targetWallStub,
            GameObject targetClosedGate,
            GameObject targetOpenGate,
            GameObject targetTowerOverlay,
            GameObject targetPalace,
            Material targetKayKitMaterial,
            IEnumerable<HexCastleBuildingVisualEntry> targetBuildingVisuals,
            IEnumerable<HexCastleTurretHeadVisualEntry> targetTurretHeadVisuals,
            IEnumerable<HexCastleTrapVisualEntry> targetTrapVisuals)
        {
            visualThemeId = string.IsNullOrWhiteSpace(id) ? "KayKitSpring" : id.Trim();
            straightWall = targetStraightWall;
            cornerAInside = targetCornerA;
            cornerAOutside = targetCornerA;
            cornerBInside = targetCornerB;
            cornerBOutside = targetCornerB;
            wallStub = targetWallStub;
            straightGate = targetClosedGate;
            closedGate = targetClosedGate;
            openGate = targetOpenGate;
            towerOverlay = targetTowerOverlay;
            palace = targetPalace;
            kayKitMaterial = targetKayKitMaterial;
            buildingVisuals = targetBuildingVisuals?.ToList() ?? new List<HexCastleBuildingVisualEntry>();
            turretHeadVisuals = targetTurretHeadVisuals?.ToList() ?? new List<HexCastleTurretHeadVisualEntry>();
            trapVisuals = targetTrapVisuals?.ToList() ?? new List<HexCastleTrapVisualEntry>();
        }
#endif
    }
}
