using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    // 몬스터 ↔ 등급·스킬 매칭표. MonsterDefinition 자체는 수정하지 않고 별도 에셋으로 관리한다.
    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Rarity Catalog", fileName = "MonsterRarityCatalog")]
    public sealed class MonsterRarityCatalog : ScriptableObject
    {
        [Header("자동 동기화")]
        [SerializeField] private MonsterCatalog sourceCatalog; // 기준 몬스터 목록

        [Header("일반 ~ 영웅 (패시브 1개 고정)")]
        [SerializeField] private List<MonsterCommonRarityEntry> commonToEpicEntries = new List<MonsterCommonRarityEntry>();

        [Header("전설 · 신화 (패시브 1개 + 액티브 1개 고정)")]
        [SerializeField] private List<MonsterLegendaryRarityEntry> legendaryMythicEntries = new List<MonsterLegendaryRarityEntry>();

        public IReadOnlyList<MonsterCommonRarityEntry> CommonToEpicEntries => commonToEpicEntries;
        public IReadOnlyList<MonsterLegendaryRarityEntry> LegendaryMythicEntries => legendaryMythicEntries;

#if UNITY_EDITOR
        private void OnEnable()
        {
            SyncWithSourceCatalog(); // 에셋 로드 시 목록 동기화
        }

        private void OnValidate()
        {
            SyncWithSourceCatalog(); // 기준 카탈로그 변경 시 동기화
        }

        private void SyncWithSourceCatalog()
        {
            if (sourceCatalog == null)
            {
                return;
            }

            commonToEpicEntries ??= new List<MonsterCommonRarityEntry>();
            legendaryMythicEntries ??= new List<MonsterLegendaryRarityEntry>();

            var existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < commonToEpicEntries.Count; index++)
            {
                if (commonToEpicEntries[index]?.Monster != null)
                {
                    existingIds.Add(commonToEpicEntries[index].Monster.MonsterId);
                }
            }

            for (var index = 0; index < legendaryMythicEntries.Count; index++)
            {
                if (legendaryMythicEntries[index]?.Monster != null)
                {
                    existingIds.Add(legendaryMythicEntries[index].Monster.MonsterId);
                }
            }

            var definitions = sourceCatalog.Definitions;
            var blankSearchStart = 0;
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (definition == null || string.IsNullOrWhiteSpace(definition.MonsterId) ||
                    !existingIds.Add(definition.MonsterId))
                {
                    continue;
                }

                while (blankSearchStart < commonToEpicEntries.Count &&
                       commonToEpicEntries[blankSearchStart]?.Monster != null)
                {
                    blankSearchStart++;
                }

                if (blankSearchStart < commonToEpicEntries.Count)
                {
                    commonToEpicEntries[blankSearchStart].AssignMonster(definition);
                    blankSearchStart++;
                }
                else
                {
                    var newEntry = new MonsterCommonRarityEntry();
                    newEntry.AssignMonster(definition);
                    commonToEpicEntries.Add(newEntry);
                }
            }
        }
#endif

        public bool TryGetRarity(string monsterId, out MonsterRarity rarity)
        {
            if (!string.IsNullOrWhiteSpace(monsterId))
            {
                for (var index = 0; index < commonToEpicEntries.Count; index++)
                {
                    var entry = commonToEpicEntries[index];
                    if (entry?.Monster != null &&
                        string.Equals(entry.Monster.MonsterId, monsterId, StringComparison.OrdinalIgnoreCase))
                    {
                        rarity = entry.Rarity;
                        return true;
                    }
                }

                for (var index = 0; index < legendaryMythicEntries.Count; index++)
                {
                    var entry = legendaryMythicEntries[index];
                    if (entry?.Monster != null &&
                        string.Equals(entry.Monster.MonsterId, monsterId, StringComparison.OrdinalIgnoreCase))
                    {
                        rarity = entry.Rarity;
                        return true;
                    }
                }
            }

            rarity = MonsterRarity.Common;
            return false;
        }

        public IReadOnlyList<MonsterDefinition> GetMonstersOfRarity(MonsterRarity rarity)
        {
            var result = new List<MonsterDefinition>();
            for (var index = 0; index < commonToEpicEntries.Count; index++)
            {
                var entry = commonToEpicEntries[index];
                if (entry?.Monster != null && entry.Rarity == rarity)
                {
                    result.Add(entry.Monster);
                }
            }

            for (var index = 0; index < legendaryMythicEntries.Count; index++)
            {
                var entry = legendaryMythicEntries[index];
                if (entry?.Monster != null && entry.Rarity == rarity)
                {
                    result.Add(entry.Monster);
                }
            }

            return result;
        }

        public bool TryValidate(out string error)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < commonToEpicEntries.Count; index++)
            {
                var entry = commonToEpicEntries[index];
                if (entry == null)
                {
                    error = $"일반~영웅 목록에 비어있는 항목이 있습니다. Index={index}";
                    return false;
                }

                if (!entry.TryValidate(out error))
                {
                    return false;
                }

                if (!ids.Add(entry.Monster.MonsterId))
                {
                    error = $"Monster ID is duplicated in rarity catalog. Monster={entry.Monster.MonsterId}";
                    return false;
                }
            }

            for (var index = 0; index < legendaryMythicEntries.Count; index++)
            {
                var entry = legendaryMythicEntries[index];
                if (entry == null)
                {
                    error = $"전설·신화 목록에 비어있는 항목이 있습니다. Index={index}";
                    return false;
                }

                if (!entry.TryValidate(out error))
                {
                    return false;
                }

                if (!ids.Add(entry.Monster.MonsterId))
                {
                    error = $"Monster ID is duplicated in rarity catalog. Monster={entry.Monster.MonsterId}";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
