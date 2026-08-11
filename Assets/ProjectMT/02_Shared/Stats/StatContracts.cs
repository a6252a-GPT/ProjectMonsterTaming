using System;
using UnityEngine;

namespace ProjectMT.Shared.Stats
{
    public enum StatId // 성장 출처가 함께 사용하는 공용 능력치 식별자
    {
        MaxHealth,
        AttackPower,
        Defense,
        AttackSpeed,
        MoveSpeed,
        AttackRange,
        CriticalRate,
        CriticalDamage,
        SkillDamage,
        BossDamage,
        NormalMonsterDamage,
        SkillCooldownReduction,
        DefensePenetration,
        DamageReduction
    }

    public enum StatOperation // 공용 계산 순서와 일치하는 연산 종류
    {
        Flat,
        AdditiveRate,
        FinalMultiplier
    }

    [Serializable]
    public struct StatModifier // 저장 원본을 전투 능력치로 바꾸는 공용 입력
    {
        [SerializeField] private StatId statId;
        [SerializeField] private StatOperation operation;
        [SerializeField] private float value;
        [SerializeField] private string sourceId;

        public StatModifier(StatId statId, StatOperation operation, float value, string sourceId)
        {
            this.statId = statId;
            this.operation = operation;
            this.value = value;
            this.sourceId = sourceId?.Trim() ?? string.Empty;
        }

        public StatId StatId => statId;
        public StatOperation Operation => operation;
        public float Value => value;
        public string SourceId => sourceId ?? string.Empty;

        public bool IsValid
        {
            get
            {
                if (!Enum.IsDefined(typeof(StatId), statId) ||
                    !Enum.IsDefined(typeof(StatOperation), operation) ||
                    float.IsNaN(value) || float.IsInfinity(value))
                {
                    return false;
                }

                return operation != StatOperation.FinalMultiplier || value > 0f;
            }
        }
    }
}
