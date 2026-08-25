using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [Serializable]
    public sealed class HexCastleCatalogEntry
    {
        [SerializeField] private string stageId;
        [SerializeField] private HexCastleTheme theme;
        [SerializeField, Range(2, 4)] private int defenseLayerCount;
        [SerializeField] private HexCastleStageLayout layout;
        [SerializeField] private GameObject bakedStagePrefab;

        public HexCastleCatalogEntry(string id, HexCastleStageLayout stageLayout, GameObject stagePrefab)
        {
            stageId = id;
            layout = stageLayout;
            bakedStagePrefab = stagePrefab;
            theme = stageLayout != null ? stageLayout.Theme : default;
            defenseLayerCount = stageLayout != null ? stageLayout.DefenseLayerCount : 0;
        }

        public string StageId => stageId;
        public HexCastleTheme Theme => theme;
        public int DefenseLayerCount => defenseLayerCount;
        public HexCastleStageLayout Layout => layout;
        public GameObject BakedStagePrefab => bakedStagePrefab;
    }

    [CreateAssetMenu(
        fileName = "HexCastleCatalog",
        menuName = "ProjectMT/Castle Raid Hex/Stage Catalog")]
    public sealed class HexCastleCatalog : ScriptableObject
    {
        [SerializeField] private List<HexCastleCatalogEntry> entries = new List<HexCastleCatalogEntry>();

        public IReadOnlyList<HexCastleCatalogEntry> Entries => entries;

        public HexCastleStageLayout Find(HexCastleTheme theme, int defenseLayerCount)
        {
            return FindEntry(theme, defenseLayerCount)?.Layout;
        }

        public HexCastleCatalogEntry FindEntry(HexCastleTheme theme, int defenseLayerCount)
        {
            return FindEntries(theme, defenseLayerCount).FirstOrDefault();
        }

        public IReadOnlyList<HexCastleCatalogEntry> FindEntries(
            HexCastleTheme theme,
            int defenseLayerCount)
        {
            return entries
                .Where(entry => entry != null && entry.Theme == theme &&
                                entry.DefenseLayerCount == defenseLayerCount &&
                                entry.Layout != null && entry.BakedStagePrefab != null)
                .OrderBy(entry => entry.Layout.Seed)
                .ThenBy(entry => entry.StageId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public HexCastleCatalogEntry FindEntry(string stageId)
        {
            if (string.IsNullOrWhiteSpace(stageId))
            {
                return null;
            }

            return entries.FirstOrDefault(entry => entry != null &&
                string.Equals(entry.StageId, stageId, StringComparison.OrdinalIgnoreCase));
        }

        public HexCastleCatalogEntry FindNextEntry(
            HexCastleTheme theme,
            int defenseLayerCount,
            string currentStageId)
        {
            var candidates = FindEntries(theme, defenseLayerCount);
            if (candidates.Count == 0)
            {
                return null;
            }

            var currentIndex = candidates
                .Select((entry, index) => new { entry, index })
                .Where(value => string.Equals(
                    value.entry.StageId,
                    currentStageId,
                    StringComparison.OrdinalIgnoreCase))
                .Select(value => value.index)
                .DefaultIfEmpty(-1)
                .First();
            return candidates[(currentIndex + 1) % candidates.Count];
        }

        public void Upsert(string stageId, HexCastleStageLayout layout, GameObject bakedStagePrefab = null)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            var existing = entries.FirstOrDefault(entry =>
                string.Equals(entry.StageId, stageId, StringComparison.OrdinalIgnoreCase));
            if (bakedStagePrefab == null)
            {
                bakedStagePrefab = existing?.BakedStagePrefab;
            }
            entries.RemoveAll(entry =>
                string.Equals(entry.StageId, stageId, StringComparison.OrdinalIgnoreCase));
            entries.Add(new HexCastleCatalogEntry(stageId, layout, bakedStagePrefab));
            entries = entries
                .OrderBy(entry => entry.StageId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
