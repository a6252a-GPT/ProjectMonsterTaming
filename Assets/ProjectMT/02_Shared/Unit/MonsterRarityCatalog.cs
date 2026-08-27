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

        [Header("일반 ~ 영웅 (패시브 1칸, 스킬 구현 전 비움 허용)")]
        [SerializeField] private List<MonsterCommonRarityEntry> commonToEpicEntries = new List<MonsterCommonRarityEntry>();

        [Header("전설 · 신화 (패시브·액티브 각 1칸, 스킬 구현 전 비움 허용)")]
        [SerializeField] private List<MonsterLegendaryRarityEntry> legendaryMythicEntries = new List<MonsterLegendaryRarityEntry>();

        public IReadOnlyList<MonsterCommonRarityEntry> CommonToEpicEntries => commonToEpicEntries;
        public IReadOnlyList<MonsterLegendaryRarityEntry> LegendaryMythicEntries => legendaryMythicEntries;

#if UNITY_EDITOR
        public MonsterCatalog SourceCatalog => sourceCatalog;

        private void OnValidate()
        {
            SyncWithSourceCatalog(); // 기준 카탈로그 변경 시 동기화
        }

        // MonsterCatalog.OnValidate가 이 도감 카탈로그를 대신 갱신할 때 호출한다.
        internal void EditorSyncWithSourceCatalog()
        {
            SyncWithSourceCatalog();
        }

        private void SyncWithSourceCatalog()
        {
            if (sourceCatalog == null)
            {
                return;
            }

            commonToEpicEntries ??= new List<MonsterCommonRarityEntry>();
            legendaryMythicEntries ??= new List<MonsterLegendaryRarityEntry>();

            var definitions = sourceCatalog.Definitions;
            var validIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < definitions.Count; index++)
            {
                if (definitions[index] != null && !string.IsNullOrWhiteSpace(definitions[index].MonsterId))
                {
                    validIds.Add(definitions[index].MonsterId);
                }
            }

            // 카탈로그에서 빠진 몬스터는 도감 항목도 같이 제거한다(그 항목의 스킬 배정도 함께 사라짐).
            var changed = RemoveStaleEntries(commonToEpicEntries, validIds);
            changed |= RemoveStaleEntries(legendaryMythicEntries, validIds);

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

                changed = true;
            }

            if (changed)
            {
                UnityEditor.EditorUtility.SetDirty(this); // 다른 에셋(MonsterCatalog) 검증 중 갱신된 경우 대비
            }
        }

        // 리스트에서 더 이상 원본 카탈로그에 없는 몬스터를 가리키는 항목을 제거한다.
        // 몬스터가 원래 비어있던(아직 배정 전) 항목은 건드리지 않는다.
        private static bool RemoveStaleEntries<TEntry>(List<TEntry> entries, HashSet<string> validIds)
            where TEntry : class, IMonsterRarityEntry
        {
            var removed = false;
            for (var index = entries.Count - 1; index >= 0; index--)
            {
                var monster = entries[index]?.Monster;
                if (monster != null && !validIds.Contains(monster.MonsterId))
                {
                    entries.RemoveAt(index);
                    removed = true;
                }
            }

            return removed;
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

        public bool TryGetSkillLoadout(
            string monsterId,
            out MonsterPassiveSkill passiveSkill,
            out MonsterActiveSkill activeSkill)
        {
            if (!string.IsNullOrWhiteSpace(monsterId))
            {
                for (var index = 0; index < commonToEpicEntries.Count; index++)
                {
                    var entry = commonToEpicEntries[index];
                    if (entry?.Monster != null &&
                        string.Equals(entry.Monster.MonsterId, monsterId, StringComparison.OrdinalIgnoreCase))
                    {
                        passiveSkill = entry.PassiveSkill;
                        activeSkill = entry.ActiveSkill;
                        return passiveSkill != null || activeSkill != null;
                    }
                }

                for (var index = 0; index < legendaryMythicEntries.Count; index++)
                {
                    var entry = legendaryMythicEntries[index];
                    if (entry?.Monster != null &&
                        string.Equals(entry.Monster.MonsterId, monsterId, StringComparison.OrdinalIgnoreCase))
                    {
                        passiveSkill = entry.PassiveSkill;
                        activeSkill = entry.ActiveSkill;
                        return passiveSkill != null || activeSkill != null;
                    }
                }
            }

            passiveSkill = null;
            activeSkill = null;
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
            if (sourceCatalog == null)
            {
                error = "Monster Rarity Catalog requires a source MonsterCatalog.";
                return false;
            }

            if (commonToEpicEntries == null || legendaryMythicEntries == null)
            {
                error = "Monster Rarity Catalog entry lists are missing.";
                return false;
            }

            if (!sourceCatalog.TryValidate(out error))
            {
                return false;
            }

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

            var sourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var definitions = sourceCatalog.Definitions;
            for (var index = 0; index < definitions.Count; index++)
            {
                if (definitions[index] != null)
                {
                    sourceIds.Add(definitions[index].MonsterId);
                }
            }

            if (!ids.SetEquals(sourceIds))
            {
                var missing = new List<string>();
                var stale = new List<string>();
                foreach (var sourceId in sourceIds)
                {
                    if (!ids.Contains(sourceId))
                    {
                        missing.Add(sourceId);
                    }
                }

                foreach (var rarityId in ids)
                {
                    if (!sourceIds.Contains(rarityId))
                    {
                        stale.Add(rarityId);
                    }
                }

                error = $"Monster Rarity Catalog must exactly match its source Catalog. " +
                        $"Missing=[{string.Join(", ", missing)}], Stale=[{string.Join(", ", stale)}]";
                return false;
            }

            error = null;
            return true;
        }

        // 실제 스킬 도메인이 연결되는 시점에 사용하는 엄격 검사. 현재 제작 등록은 구조 검사만 통과시키고 누락 스킬은 경고로 남긴다.
        public bool TryValidateSkillReferences(out string error)
        {
            if (!TryValidate(out error))
            {
                return false;
            }

            for (var index = 0; index < commonToEpicEntries.Count; index++)
            {
                if (!commonToEpicEntries[index].TryValidateSkillReferences(out error))
                {
                    return false;
                }
            }

            for (var index = 0; index < legendaryMythicEntries.Count; index++)
            {
                if (!legendaryMythicEntries[index].TryValidateSkillReferences(out error))
                {
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
