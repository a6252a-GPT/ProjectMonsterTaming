using ProjectMT.Contents.CastleRaid.Generation;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.CastleBake
{
    public static class CastleGenerationAssetFactory // 기본 템플릿·규칙 자산의 정식 생성점
    {
        public const string GenerationDataRoot = "Assets/ProjectMT/04_Contents/01_CastleRaid/Data/Generation";
        public const string TemplateRoot = GenerationDataRoot + "/Templates";
        public const string DefaultRulesPath = GenerationDataRoot + "/CastleGenerationRules_Default.asset";

        [MenuItem("JC Tool/Castle Raid/Create Or Update Default Generation Assets")]
        private static void CreateOrUpdateDefaultsFromMenu()
        {
            var rules = CreateOrUpdateDefaults();
            Selection.activeObject = rules;
            EditorGUIUtility.PingObject(rules);
            Debug.Log($"Castle Raid 기본 생성 자산을 갱신했습니다: {DefaultRulesPath}", rules);
        }

        public static CastleGenerationRules CreateOrUpdateDefaults()
        {
            EnsureFolder(GenerationDataRoot);
            EnsureFolder(TemplateRoot);

            var palace = CreateOrUpdateTemplate(
                "CastleDistrict_PalaceCore.asset",
                "palace_core_12x12",
                12,
                12,
                12,
                12,
                1,
                0,
                0,
                1f,
                true,
                false);
            var standard = CreateOrUpdateTemplate(
                "CastleDistrict_Standard.asset",
                "district_standard_6x6_10x10",
                6,
                10,
                6,
                10,
                1,
                1,
                3,
                1.35f,
                false,
                true);
            var outerStep = CreateOrUpdateTemplate(
                "CastleDistrict_OuterStep.asset",
                "district_outer_step_5x5_14x14",
                5,
                14,
                5,
                14,
                1,
                1,
                4,
                0.7f,
                false,
                true);
            var hexCell = CreateOrUpdateTemplate(
                "CastleDistrict_HexCell.asset",
                "district_hex_cell_7x5",
                7,
                7,
                5,
                5,
                1,
                1,
                2,
                1.2f,
                false,
                true);
            var hexQueen = CreateOrUpdateTemplate(
                "CastleDistrict_HexQueen.asset",
                CastleGenerationRules.HexQueenTemplateId,
                15,
                15,
                13,
                13,
                1,
                3,
                6,
                0.9f,
                false,
                false);
            var petal = CreateOrUpdateTemplate(
                "CastleDistrict_Petal.asset",
                CastleGenerationRules.PetalTemplateId,
                4,
                22,
                4,
                22,
                1,
                0,
                5,
                0.85f,
                false,
                true);
            var geometric = CreateOrUpdateTemplate(
                "CastleDistrict_Geometric.asset",
                CastleGenerationRules.GeometricTemplateId,
                4,
                30,
                4,
                30,
                1,
                0,
                5,
                0.85f,
                false,
                true);
            var wide = CreateOrUpdateTemplate(
                "CastleDistrict_Wide.asset",
                "district_wide_8x6_14x10",
                8,
                14,
                6,
                10,
                1,
                2,
                4,
                1f,
                false,
                true);
            var large = CreateOrUpdateTemplate(
                "CastleDistrict_Large.asset",
                "district_large_9x9_14x14",
                9,
                14,
                9,
                14,
                1,
                2,
                5,
                0.85f,
                false,
                true);

            var rules = AssetDatabase.LoadAssetAtPath<CastleGenerationRules>(DefaultRulesPath);
            if (rules == null)
            {
                rules = ScriptableObject.CreateInstance<CastleGenerationRules>();
                AssetDatabase.CreateAsset(rules, DefaultRulesPath);
            }

            rules.EditorConfigureDefaults(new[] { palace, standard, wide, large, outerStep, hexCell, hexQueen, petal, geometric });
            EditorUtility.SetDirty(rules);
            AssetDatabase.SaveAssets();
            return rules;
        }

        public static void EnsureFolder(string assetFolder)
        {
            var parts = assetFolder.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static CastleDistrictTemplate CreateOrUpdateTemplate(
            string fileName,
            string templateId,
            int minimumWidth,
            int maximumWidth,
            int minimumHeight,
            int maximumHeight,
            int wallLayers,
            int minimumPlacements,
            int maximumPlacements,
            float selectionWeight,
            bool palaceCore,
            bool supportsSpecialLoot)
        {
            var path = TemplateRoot + "/" + fileName;
            var template = AssetDatabase.LoadAssetAtPath<CastleDistrictTemplate>(path);
            if (template == null)
            {
                template = ScriptableObject.CreateInstance<CastleDistrictTemplate>();
                AssetDatabase.CreateAsset(template, path);
            }

            template.EditorConfigure(
                templateId,
                minimumWidth,
                maximumWidth,
                minimumHeight,
                maximumHeight,
                wallLayers,
                minimumPlacements,
                maximumPlacements,
                selectionWeight,
                palaceCore,
                supportsSpecialLoot);
            EditorUtility.SetDirty(template);
            return template;
        }
    }
}
