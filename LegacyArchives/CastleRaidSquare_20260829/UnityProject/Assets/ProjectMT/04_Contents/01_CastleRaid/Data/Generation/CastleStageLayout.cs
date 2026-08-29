using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid.Generation
{
    [CreateAssetMenu(
        menuName = "ProjectMT/Castle Raid/Generation/Approved Stage Layout",
        fileName = "CastleStageLayout")]
    public sealed class CastleStageLayout : ScriptableObject // 승인된 Seed 배치를 고정하는 Stage 원본
    {
        [SerializeField] private string stageId;
        [SerializeField] private int seed;
        [SerializeField, Min(1)] private int rulesVersion = 1;
        [SerializeField, Min(1)] private int gridWidth;
        [SerializeField, Min(1)] private int gridHeight;
        [SerializeField] private CastleLayoutTheme layoutTheme;
        [SerializeField] private CastleStructureVariant structureVariant;
        [SerializeField, Range(2, 4)] private int requestedDefenseLayerCount = 2;
        [SerializeField] private string structureHash;
        [SerializeField] private string layoutHash;
        [SerializeField, Min(0)] private int palaceExposedSideCount;
        [SerializeField, Min(0)] private int protectionDepth;
        [SerializeField, Min(0f)] private float compactness;
        [SerializeField] private CastleDifficultyReport difficulty;
        [SerializeField] private List<CastleCompartmentData> compartments = new List<CastleCompartmentData>();
        [SerializeField] private List<CastlePlacementData> placements = new List<CastlePlacementData>();

        public string StageId => stageId;
        public int Seed => seed;
        public int RulesVersion => rulesVersion;
        public int GridWidth => gridWidth;
        public int GridHeight => gridHeight;
        public CastleLayoutTheme LayoutTheme => layoutTheme;
        public CastleStructureVariant StructureVariant => structureVariant;
        public int RequestedDefenseLayerCount => requestedDefenseLayerCount;
        public string StructureHash => structureHash;
        public string LayoutHash => layoutHash;
        public int PalaceExposedSideCount => palaceExposedSideCount;
        public int ProtectionDepth => protectionDepth;
        public float Compactness => compactness;
        public CastleDifficultyReport Difficulty => difficulty;
        public IReadOnlyList<CastleCompartmentData> Compartments => compartments;
        public IReadOnlyList<CastlePlacementData> Placements => placements;

#if UNITY_EDITOR
        public void EditorStore(string id, CastleGenerationCandidate candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (!candidate.Validation.IsValid || !candidate.Difficulty.HasClearPath)
            {
                throw new InvalidOperationException("검수를 통과하고 왕궁 경로가 있는 후보만 승인할 수 있습니다.");
            }

            stageId = id ?? string.Empty;
            seed = candidate.Seed;
            rulesVersion = candidate.RulesVersion;
            gridWidth = candidate.GridWidth;
            gridHeight = candidate.GridHeight;
            layoutTheme = candidate.Theme;
            structureVariant = candidate.StructureVariant;
            requestedDefenseLayerCount = candidate.RequestedDefenseLayerCount;
            structureHash = candidate.StructureHash;
            layoutHash = candidate.LayoutHash;
            palaceExposedSideCount = candidate.PalaceExposedSideCount;
            protectionDepth = candidate.ProtectionDepth;
            compactness = candidate.Compactness;
            difficulty = candidate.Difficulty;
            compartments = candidate.Compartments.Select(compartment => compartment.Clone()).ToList();
            placements = candidate.Placements.Select(placement => placement.Clone()).ToList();
        }
#endif
    }
}
