using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    // 몬스터 등급 6단계. 숫자가 클수록 상위 등급 (Rare >= Uncommon 같은 비교에 사용).
    public enum MonsterRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Mythic
    }

    // 패시브 스킬 자리표시자. 실제 스킬 내용은 아직 미정이라
    // 이 클래스를 상속하는 구체 스킬 ScriptableObject를 나중에 스킬 시스템 쪽에서 만들면 된다.
    public abstract class MonsterPassiveSkill : ScriptableObject
    {
        [SerializeField] private string skillId;
        [SerializeField] private string displayName;

        public string SkillId => skillId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? skillId : displayName;
    }

    // 액티브 스킬 자리표시자. 전설·신화 등급 몬스터만 사용한다.
    public abstract class MonsterActiveSkill : ScriptableObject
    {
        [SerializeField] private string skillId;
        [SerializeField] private string displayName;

        public string SkillId => skillId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? skillId : displayName;
    }

    // 일반~영웅 등급 몬스터 한 종류. 패시브 스킬 1개만 고정으로 가진다 (액티브 칸 자체가 없음).
    [Serializable]
    public sealed class MonsterCommonRarityEntry
    {
        [SerializeField] private MonsterDefinition monster;
        [SerializeField] private MonsterRarity rarity = MonsterRarity.Common; // Common/Uncommon/Rare/Epic 중 하나
        [SerializeField] private MonsterPassiveSkill passiveSkill; // 고정 패시브 1개

        public MonsterDefinition Monster => monster;
        public MonsterRarity Rarity => rarity;
        public MonsterPassiveSkill PassiveSkill => passiveSkill;

        public bool TryValidate(out string error)
        {
            if (monster == null || string.IsNullOrWhiteSpace(monster.MonsterId))
            {
                error = "Monster Rarity Entry is missing its monster reference.";
                return false;
            }

            if (rarity == MonsterRarity.Legendary || rarity == MonsterRarity.Mythic)
            {
                error = $"전설·신화 등급은 이 목록이 아니라 전설·신화 목록에 넣어야 합니다. Monster={monster.MonsterId}";
                return false;
            }

            if (passiveSkill == null)
            {
                error = $"Monster Rarity Entry has no passive skill. Monster={monster.MonsterId}";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        // 카탈로그 자동 동기화 전용. 몬스터가 비어있는 줄에만 사용한다 (MonsterRarityCatalog.SyncWithSourceCatalog).
        internal void AssignMonster(MonsterDefinition value)
        {
            monster = value;
        }
#endif
    }

    // 전설·신화 등급 몬스터 한 종류. 패시브 1개 + 액티브 1개를 고정으로 가진다 (두 칸 모두 항상 존재).
    [Serializable]
    public sealed class MonsterLegendaryRarityEntry
    {
        [SerializeField] private MonsterDefinition monster;
        [SerializeField] private MonsterRarity rarity = MonsterRarity.Legendary; // Legendary/Mythic 중 하나
        [SerializeField] private MonsterPassiveSkill passiveSkill; // 고정 패시브 1개
        [SerializeField] private MonsterActiveSkill activeSkill; // 고정 액티브 1개

        public MonsterDefinition Monster => monster;
        public MonsterRarity Rarity => rarity;
        public MonsterPassiveSkill PassiveSkill => passiveSkill;
        public MonsterActiveSkill ActiveSkill => activeSkill;

        public bool TryValidate(out string error)
        {
            if (monster == null || string.IsNullOrWhiteSpace(monster.MonsterId))
            {
                error = "Monster Rarity Entry is missing its monster reference.";
                return false;
            }

            if (rarity != MonsterRarity.Legendary && rarity != MonsterRarity.Mythic)
            {
                error = $"일반~영웅 등급은 이 목록이 아니라 일반~영웅 목록에 넣어야 합니다. Monster={monster.MonsterId}";
                return false;
            }

            if (passiveSkill == null)
            {
                error = $"Monster Rarity Entry has no passive skill. Monster={monster.MonsterId}";
                return false;
            }

            if (activeSkill == null)
            {
                error = $"전설·신화 등급은 액티브 스킬도 필요합니다. Monster={monster.MonsterId}";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        internal void AssignMonster(MonsterDefinition value)
        {
            monster = value;
        }
#endif
    }

    // 몬스터 ↔ 등급·스킬 매칭표. MonsterCatalog와 같은 패턴의 등록부이지만
    // MonsterDefinition 자체는 수정하지 않기 위해 완전히 별도 에셋으로 둔다.
    // 일반~영웅과 전설·신화를 처음부터 별도 목록으로 나눠서, 스킬 칸 개수를 매번 선택할 필요 없이
    // 목록에 넣는 순간 패시브 1개(일반~영웅) 또는 패시브+액티브 1개씩(전설·신화)이 고정으로 준비된다.
    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Rarity Catalog", fileName = "MonsterRarityCatalog")]
    public sealed class MonsterRarityCatalog : ScriptableObject
    {
        [Header("자동 동기화")]
        [SerializeField] private MonsterCatalog sourceCatalog; // 여기에 MonsterCatalog를 연결하면 몬스터가 자동으로 아래 목록에 채워진다.

        [Header("일반 ~ 영웅 (패시브 1개 고정)")]
        [SerializeField] private List<MonsterCommonRarityEntry> commonToEpicEntries = new List<MonsterCommonRarityEntry>();

        [Header("전설 · 신화 (패시브 1개 + 액티브 1개 고정)")]
        [SerializeField] private List<MonsterLegendaryRarityEntry> legendaryMythicEntries = new List<MonsterLegendaryRarityEntry>();

        public IReadOnlyList<MonsterCommonRarityEntry> CommonToEpicEntries => commonToEpicEntries;
        public IReadOnlyList<MonsterLegendaryRarityEntry> LegendaryMythicEntries => legendaryMythicEntries;

#if UNITY_EDITOR
        private void OnEnable()
        {
            SyncWithSourceCatalog(); // 에셋이 로드될 때(선택될 때) 자동 동기화
        }

        private void OnValidate()
        {
            SyncWithSourceCatalog(); // 인스펙터에서 Source Catalog를 바꿨을 때도 즉시 동기화
        }

        // Source Catalog에 등록된 몬스터 중 아직 어느 목록에도 없는 몬스터를 "일반~영웅" 목록에 채워 넣는다.
        // 전설·신화로 만들 몬스터는 직접 그 목록으로 옮겨서 등급만 지정하면 된다.
        // 이미 지정해둔 항목은 건드리지 않고, 몬스터가 비어있는 빈 줄부터 먼저 채운 뒤 남으면 새 줄을 추가한다.
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
                    continue; // 몬스터 정보가 없거나 이미 등록된 몬스터
                }

                while (blankSearchStart < commonToEpicEntries.Count &&
                       commonToEpicEntries[blankSearchStart]?.Monster != null)
                {
                    blankSearchStart++;
                }

                if (blankSearchStart < commonToEpicEntries.Count)
                {
                    commonToEpicEntries[blankSearchStart].AssignMonster(definition); // 비어있던 줄부터 채움
                    blankSearchStart++;
                }
                else
                {
                    var newEntry = new MonsterCommonRarityEntry();
                    newEntry.AssignMonster(definition);
                    commonToEpicEntries.Add(newEntry); // 새로 발견된 몬스터는 일단 일반~영웅 목록에 추가
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

        // 해당 등급에 속한 몬스터 목록. 뽑기에서 등급을 먼저 정한 뒤, 이 안에서 랜덤으로 하나를 고른다.
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
