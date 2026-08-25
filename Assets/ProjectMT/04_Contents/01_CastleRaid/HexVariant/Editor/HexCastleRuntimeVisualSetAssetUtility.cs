using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public static class HexCastleRuntimeVisualSetAssetUtility // 절차 생성에 필요한 순수 Visual 참조만 보관한다
    {
        public const string AssetPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Resources/HexCastleRuntimeVisualSet.asset";

        private const string KayKitRoot =
            "Assets/ThirdParty/04_환경맵/KayKit - Forest Nature Pack (for Unity)/KayKit - Forest Nature Pack (for Unity)/Packs/KayKit - Medieval Hexagon Pack (for Unity)/Prefabs";
        private const string DerivedRoot =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Art/Derived/KayKitDoubleSided/Prefabs/";
        private const string TurretHeadRoot =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/Prefabs/TurretHeads/";
        private const string MaterialPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Art/Materials/MAT_CRHex_KayKitWall_Spring.mat";
        private const string TrapSourceRoot = "Assets/ThirdParty/추가에셋2/Traps";
        private const string TrapMaterialRoot =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Art/Materials/Traps";

        [MenuItem("JC Tool/군단의 역습 육각/절차 생성 Visual Set 갱신")]
        public static HexCastleVisualSet LoadOrCreate()
        {
            var asset = AssetDatabase.LoadAssetAtPath<HexCastleVisualSet>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HexCastleVisualSet>();
                asset.name = "HexCastleRuntimeVisualSet";
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            asset.EditorConfigureRuntime(
                "KayKitSpringProcedural",
                Load<GameObject>(DerivedRoot + "PF_CRHex_WallStraight_DoubleSided.prefab"),
                Load<GameObject>(DerivedRoot + "PF_CRHex_WallCornerA_DoubleSided.prefab"),
                Load<GameObject>(DerivedRoot + "PF_CRHex_WallCornerB_DoubleSided.prefab"),
                Load<GameObject>(DerivedRoot + "PF_CRHex_WallStub_DoubleSided.prefab"),
                Load<GameObject>(DerivedRoot + "PF_CRHex_Gate_Closed_DoubleSided.prefab"),
                Load<GameObject>(DerivedRoot + "PF_CRHex_Gate_Open_DoubleSided.prefab"),
                Load<GameObject>(KayKitRoot + "/buildings/blue/building_tower_A_blue.prefab"),
                Load<GameObject>(KayKitRoot + "/buildings/blue/building_castle_blue.prefab"),
                Load<Material>(MaterialPath),
                CreateBuildingEntries(),
                CreateTurretHeadEntries(),
                CreateTrapEntries());
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        public static HexCastleVisualSet Load()
        {
            return AssetDatabase.LoadAssetAtPath<HexCastleVisualSet>(AssetPath);
        }

        private static IEnumerable<HexCastleBuildingVisualEntry> CreateBuildingEntries()
        {
            yield return Building("building_barracks_blue", "blue");
            yield return Building("building_tent_blue", "blue");
            yield return Building("building_archeryrange_blue", "blue");
            yield return Building("building_church_blue", "blue");
            yield return Building("building_market_yellow", "yellow");
            yield return Building("building_blacksmith_green", "green");
            yield return Building("building_mine_red", "red");
            yield return Building("building_tower_base_blue", "blue");
            yield return Building("building_stage_B", "neutral");
            yield return Building("building_stage_C", "neutral");
            yield return Building("building_home_A_blue", "blue");
            yield return Building("building_home_B_blue", "blue");
            yield return Building("building_shrine_blue", "blue");
            yield return Building("building_townhall_blue", "blue");
            yield return Building("building_windmill_blue", "blue");
        }

        private static IEnumerable<HexCastleTurretHeadVisualEntry> CreateTurretHeadEntries()
        {
            yield return Turret(HexCastleTurretWeaponKind.Cannon, 1, "Cannon");
            yield return Turret(HexCastleTurretWeaponKind.Cannon, 2, "Cannon");
            yield return Turret(HexCastleTurretWeaponKind.Cannon, 3, "Cannon");
            yield return Turret(HexCastleTurretWeaponKind.Ballista, 1, "Ballista");
            yield return Turret(HexCastleTurretWeaponKind.Ballista, 2, "Ballista");
            yield return Turret(HexCastleTurretWeaponKind.Fireball, 1, "Fireball");
            yield return Turret(HexCastleTurretWeaponKind.Fireball, 2, "Fireball");
            yield return Turret(HexCastleTurretWeaponKind.Fireball, 3, "Fireball");
        }

        private static IEnumerable<HexCastleTrapVisualEntry> CreateTrapEntries()
        {
            yield return Trap(
                HexCastleTrapType.Snare,
                "BearSnare",
                1,
                "Trap_01");
            yield return Trap(
                HexCastleTrapType.SpikePlate,
                "RisingSpikes",
                3,
                "Traps_03New");
            yield return Trap(
                HexCastleTrapType.SpikePlate,
                "SawBlades",
                2,
                "Dimanic_01");
            yield return Trap(
                HexCastleTrapType.SpikePlate,
                "SpikePress",
                4,
                "Dynamic");
        }

        private static HexCastleBuildingVisualEntry Building(string id, string colorFolder)
        {
            return HexCastleBuildingVisualEntry.Create(
                id,
                Load<GameObject>($"{KayKitRoot}/buildings/{colorFolder}/{id}.prefab"));
        }

        private static HexCastleTurretHeadVisualEntry Turret(
            HexCastleTurretWeaponKind weaponKind,
            int level,
            string family)
        {
            return HexCastleTurretHeadVisualEntry.Create(
                weaponKind,
                level,
                Load<GameObject>($"{TurretHeadRoot}PF_CR_TurretHead_{family}_Lv{level}.prefab"));
        }

        private static HexCastleTrapVisualEntry Trap(
            HexCastleTrapType trapType,
            string variantId,
            int sourceIndex,
            string animationStateName)
        {
            return HexCastleTrapVisualEntry.Create(
                trapType,
                variantId,
                Load<GameObject>($"{TrapSourceRoot}/Prefabs/Trap_{sourceIndex:00}.prefab"),
                LoadOrCreateTrapMaterial(sourceIndex),
                animationStateName);
        }

        private static Material LoadOrCreateTrapMaterial(int sourceIndex)
        {
            EnsureFolder(TrapMaterialRoot);
            var path = $"{TrapMaterialRoot}/MAT_CRHex_Trap_{sourceIndex:00}_URP.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                         throw new InvalidOperationException("URP Lit Shader를 찾지 못했습니다.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = $"MAT_CRHex_Trap_{sourceIndex:00}_URP"
                };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            var textureRoot = $"{TrapSourceRoot}/Textures/Trap_{sourceIndex:00}";
            var baseColorPath = ResolveTrapTexturePath(sourceIndex, textureRoot, "BaseColor");
            var normalPath = ResolveTrapTexturePath(sourceIndex, textureRoot, "Normal");
            var metallicSmoothnessPath = ResolveTrapTexturePath(sourceIndex, textureRoot, "MetallicSmoothness");
            var occlusionPath = ResolveTrapTexturePath(sourceIndex, textureRoot, "Occlusion");
            ConfigureTextureImporter(normalPath, TextureImporterType.NormalMap, false);
            ConfigureTextureImporter(metallicSmoothnessPath, TextureImporterType.Default, false);
            ConfigureTextureImporter(occlusionPath, TextureImporterType.Default, false);

            material.SetTexture("_BaseMap", Load<Texture2D>(baseColorPath));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", Load<Texture2D>(normalPath));
            material.SetFloat("_BumpScale", 1f);
            material.SetTexture("_MetallicGlossMap", Load<Texture2D>(metallicSmoothnessPath));
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 0.36f);
            material.SetTexture("_OcclusionMap", Load<Texture2D>(occlusionPath));
            material.SetFloat("_OcclusionStrength", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static string ResolveTrapTexturePath(int sourceIndex, string textureRoot, string textureKind)
        {
            if (sourceIndex == 1)
            {
                switch (textureKind)
                {
                    case "BaseColor":
                        return textureRoot + "/tpar_01_bc.tga";
                    case "Normal":
                        return textureRoot + "/tpar_01_n.tga";
                    case "MetallicSmoothness":
                        return textureRoot + "/tpar_01_m_s.tga";
                    case "Occlusion":
                        return textureRoot + "/tpar_01_ao.tga";
                }
            }

            var suffix = sourceIndex == 2
                ? new[] { "5", "7", string.Empty, "8" }
                : sourceIndex == 3
                    ? new[] { "17", "19", "18", "20" }
                    : new[] { "29", "31", "30", "32" };
            switch (textureKind)
            {
                case "BaseColor":
                    return $"{textureRoot}/PBR_tpar_01_bc_{suffix[0]}.tga";
                case "Normal":
                    return $"{textureRoot}/PBR_tpar_01_n_{suffix[1]}.tga";
                case "MetallicSmoothness":
                    return sourceIndex == 2
                        ? textureRoot + "/PBR_tpar_02_m_s_.tga"
                        : $"{textureRoot}/PBR_tpar_01_m_s_{suffix[2]}.tga";
                case "Occlusion":
                    return $"{textureRoot}/PBR_tpar_01_ao_{suffix[3]}.tga";
                default:
                    throw new ArgumentOutOfRangeException(nameof(textureKind), textureKind, null);
            }
        }

        private static void ConfigureTextureImporter(
            string path,
            TextureImporterType textureType,
            bool sRgb)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter ??
                           throw new InvalidOperationException($"Trap TextureImporter가 없습니다: {path}");
            if (importer.textureType == textureType && importer.sRGBTexture == sRgb)
            {
                return;
            }

            importer.textureType = textureType;
            importer.sRGBTexture = sRgb;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var separator = path.LastIndexOf('/');
            if (separator <= 0)
            {
                throw new InvalidOperationException($"생성할 폴더 경로가 잘못됐습니다: {path}");
            }

            var parent = path.Substring(0, separator);
            var name = path.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path) ??
                   throw new InvalidOperationException($"Hex 절차 생성 Visual 자산이 없습니다: {path}");
        }
    }
}
