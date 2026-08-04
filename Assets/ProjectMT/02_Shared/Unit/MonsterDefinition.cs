using System;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Definition", fileName = "MonsterDefinition")]
    public sealed class MonsterDefinition : ScriptableObject // 몬스터 한 종류의 고정 전투 데이터
    {
        [SerializeField] private string monsterId;
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float attackPower = 10f;
        [SerializeField] private float defense;
        [SerializeField] private float attackSpeed = 1f; // 초당 공격 횟수
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float attackRange = 1f;
        [SerializeField] private bool ranged;

        public string MonsterId => monsterId;
        public float MaxHealth => maxHealth;
        public float AttackPower => attackPower;
        public float Defense => defense;
        public float AttackSpeed => attackSpeed;
        public float MoveSpeed => moveSpeed;
        public float AttackRange => attackRange;
        public bool Ranged => ranged;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(monsterId))
            {
                error = $"Monster ID is blank. Asset={name}";
                return false;
            }

            if (maxHealth <= 0f || attackPower < 0f || defense < 0f ||
                attackSpeed <= 0f || moveSpeed < 0f || attackRange <= 0f)
            {
                error = $"Monster stats are invalid. Monster={monsterId}";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            float health,
            float attack,
            float defenseValue,
            float attacksPerSecond,
            float movementSpeed,
            float range,
            bool isRanged)
        {
            monsterId = id?.Trim();
            maxHealth = health;
            attackPower = attack;
            defense = defenseValue;
            attackSpeed = attacksPerSecond;
            moveSpeed = movementSpeed;
            attackRange = range;
            ranged = isRanged;
        }
#endif
    }
}
