using System;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    // 몬스터 등급 5단계. 숫자가 클수록 상위 등급 비교에 사용한다.
    public enum MonsterRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3,
        Mythic = 4
    }

    // MonsterRarityCatalog의 등급별 항목 공통 계약. 원본 카탈로그와의 동기화 로직에서 사용한다.
    public interface IMonsterRarityEntry
    {
        MonsterDefinition Monster { get; }
    }

    // 일반~영웅 등급 몬스터 한 종류. 영웅만 범용 액티브를 추가로 사용한다.
    [Serializable]
    public sealed class MonsterCommonRarityEntry : IMonsterRarityEntry
    {
        [SerializeField] private MonsterDefinition monster;
        [SerializeField] private MonsterRarity rarity = MonsterRarity.Common; // Common/Rare/Epic 중 하나
        [SerializeField] private MonsterPassiveSkill passiveSkill; // 고정 패시브 1개
        [SerializeField] private MonsterActiveSkill activeSkill; // 영웅 범용 액티브 1개

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

            if (rarity != MonsterRarity.Common &&
                rarity != MonsterRarity.Rare &&
                rarity != MonsterRarity.Epic)
            {
                error = $"일반·희귀·영웅 등급만 일반~영웅 목록에 넣을 수 있습니다. Monster={monster.MonsterId}";
                return false;
            }

            error = null;
            return true;
        }

        public bool TryValidateSkillReferences(out string error)
        {
            if (!TryValidate(out error))
            {
                return false;
            }

            if (passiveSkill == null)
            {
                error = $"Monster Rarity Entry has no passive skill. Monster={monster.MonsterId}";
                return false;
            }

            if (!passiveSkill.TryValidate(out error))
            {
                return false;
            }

            if (rarity == MonsterRarity.Epic && activeSkill == null)
            {
                error = $"영웅 등급은 범용 액티브 스킬이 필요합니다. Monster={monster.MonsterId}";
                return false;
            }

            if (activeSkill != null && !activeSkill.TryValidate(out error))
            {
                return false;
            }

            if (rarity != MonsterRarity.Epic && activeSkill != null)
            {
                error = $"일반·희귀 등급은 액티브 스킬을 사용할 수 없습니다. Monster={monster.MonsterId}";
                return false;
            }

            if (activeSkill != null && activeSkill.ExecutionKind == MonsterActiveExecutionKind.DedicatedMythic)
            {
                error = $"신화 전용 액티브는 영웅 이하 등급에 연결할 수 없습니다. Monster={monster.MonsterId}";
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

    // 전설·신화 등급 몬스터 한 종류. 두 스킬 칸은 스킬 시스템 구현 전까지 비워둘 수 있다.
    [Serializable]
    public sealed class MonsterLegendaryRarityEntry : IMonsterRarityEntry
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

            error = null;
            return true;
        }

        public bool TryValidateSkillReferences(out string error)
        {
            if (!TryValidate(out error))
            {
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

            if (!passiveSkill.TryValidate(out error) || !activeSkill.TryValidate(out error))
            {
                return false;
            }

            if (rarity != MonsterRarity.Mythic &&
                activeSkill.ExecutionKind == MonsterActiveExecutionKind.DedicatedMythic)
            {
                error = $"신화 전용 액티브는 신화 등급에만 연결할 수 있습니다. Monster={monster.MonsterId}";
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

}
