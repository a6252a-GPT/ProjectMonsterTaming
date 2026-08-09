using System;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [Serializable]
    public struct MonsterStatModifier // 정식 Monster 능력치 비율 보정
    {
        [SerializeField] private float healthRate;
        [SerializeField] private float attackRate;
        [SerializeField] private float defenseRate;
        [SerializeField] private float attackSpeedRate;
        [SerializeField] private float moveSpeedRate;
        [SerializeField] private float attackRangeRate;

        public MonsterStatModifier(
            float healthRate,
            float attackRate,
            float defenseRate,
            float attackSpeedRate,
            float moveSpeedRate,
            float attackRangeRate)
        {
            this.healthRate = healthRate;
            this.attackRate = attackRate;
            this.defenseRate = defenseRate;
            this.attackSpeedRate = attackSpeedRate;
            this.moveSpeedRate = moveSpeedRate;
            this.attackRangeRate = attackRangeRate;
        }

        public float HealthRate => healthRate;
        public float AttackRate => attackRate;
        public float DefenseRate => defenseRate;
        public float AttackSpeedRate => attackSpeedRate;
        public float MoveSpeedRate => moveSpeedRate;
        public float AttackRangeRate => attackRangeRate;
        public bool HasNegativeRate => healthRate < 0f || attackRate < 0f || defenseRate < 0f ||
                                       attackSpeedRate < 0f || moveSpeedRate < 0f || attackRangeRate < 0f;
        public bool IsEmpty => Mathf.Approximately(healthRate, 0f) &&
                               Mathf.Approximately(attackRate, 0f) &&
                               Mathf.Approximately(defenseRate, 0f) &&
                               Mathf.Approximately(attackSpeedRate, 0f) &&
                               Mathf.Approximately(moveSpeedRate, 0f) &&
                               Mathf.Approximately(attackRangeRate, 0f);

        public static MonsterStatModifier operator +(
            MonsterStatModifier left,
            MonsterStatModifier right)
        {
            return new MonsterStatModifier(
                left.healthRate + right.healthRate,
                left.attackRate + right.attackRate,
                left.defenseRate + right.defenseRate,
                left.attackSpeedRate + right.attackSpeedRate,
                left.moveSpeedRate + right.moveSpeedRate,
                left.attackRangeRate + right.attackRangeRate);
        }
    }
}
