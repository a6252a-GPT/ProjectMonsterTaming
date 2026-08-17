using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectMT.Contents.CastleRaid.Generation
{
    public sealed class CastleGenerationCandidate // 메모리 안에서만 존재하는 미승인 후보
    {
        private readonly List<CastlePlacementData> placements;
        private readonly List<CastleCompartmentData> compartments;

        internal CastleGenerationCandidate(
            int candidateSeed,
            int generationRulesVersion,
            int width,
            int height,
            CastleLayoutTheme layoutTheme,
            CastleStructureVariant structureVariant,
            string structureHash,
            string layoutHash,
            IEnumerable<CastleCompartmentData> generatedCompartments,
            IEnumerable<CastlePlacementData> generatedPlacements,
            int requestedDefenseLayers = CastleGenerationRules.MinimumDefenseLayerCount)
        {
            Seed = candidateSeed;
            RulesVersion = generationRulesVersion;
            GridWidth = width;
            GridHeight = height;
            Theme = layoutTheme;
            StructureVariant = structureVariant;
            RequestedDefenseLayerCount = requestedDefenseLayers;
            StructureHash = structureHash ?? string.Empty;
            LayoutHash = layoutHash ?? string.Empty;
            compartments = generatedCompartments?.Select(compartment => compartment.Clone()).ToList()
                           ?? new List<CastleCompartmentData>();
            placements = generatedPlacements?.Select(placement => placement.Clone()).ToList()
                         ?? new List<CastlePlacementData>();
            Validation = new CastleGenerationValidationReport(Array.Empty<CastleGenerationValidationIssue>());
            Difficulty = new CastleDifficultyReport(false, 0f, 0f, 0f, 0f, 0f, -1f, -1f, -1f, Array.Empty<string>());
        }

        public int Seed { get; }
        public int RulesVersion { get; }
        public int GridWidth { get; }
        public int GridHeight { get; }
        public CastleLayoutTheme Theme { get; }
        public CastleStructureVariant StructureVariant { get; }
        public int RequestedDefenseLayerCount { get; }
        public string StructureHash { get; }
        public string LayoutHash { get; }
        public IReadOnlyList<CastleCompartmentData> Compartments => compartments;
        public IReadOnlyList<CastlePlacementData> Placements => placements;
        public CastleGenerationValidationReport Validation { get; private set; }
        public CastleDifficultyReport Difficulty { get; private set; }
        public int PalaceExposedSideCount { get; private set; }
        public int ProtectionDepth { get; private set; }
        public float Compactness { get; private set; }

        internal void SetStructuralMetrics(int exposedSideCount, int protectionDepth, float compactness)
        {
            PalaceExposedSideCount = Math.Max(0, exposedSideCount);
            ProtectionDepth = Math.Max(0, protectionDepth);
            Compactness = Math.Max(0f, compactness);
        }

        internal void SetReports(CastleGenerationValidationReport validation, CastleDifficultyReport difficulty)
        {
            Validation = validation ?? throw new ArgumentNullException(nameof(validation));
            Difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
        }
    }
}
