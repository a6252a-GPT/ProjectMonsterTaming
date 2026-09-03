using System;
using UnityEngine;

namespace ProjectMT.Features.Expedition
{
    [Serializable]
    public sealed class ExpeditionEnemyTypeBalance
    {
        [SerializeField] private EnemyAppearanceGroup group;
        [SerializeField, Min(0.01f)] private float healthMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float damageMultiplier = 1f;
        [SerializeField, Min(0f)] private float defenseMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float attackInterval = 1f;
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.58f;
        [SerializeField, Min(0.2f)] private float attackRange = 3.5f;

        public ExpeditionEnemyTypeBalance(
            EnemyAppearanceGroup group,
            float healthMultiplier,
            float damageMultiplier,
            float defenseMultiplier,
            float attackInterval,
            float moveSpeed,
            float attackRange)
        {
            this.group = group;
            this.healthMultiplier = Mathf.Max(0.01f, healthMultiplier);
            this.damageMultiplier = Mathf.Max(0.01f, damageMultiplier);
            this.defenseMultiplier = Mathf.Max(0f, defenseMultiplier);
            this.attackInterval = Mathf.Max(0.1f, attackInterval);
            this.moveSpeed = Mathf.Max(0.1f, moveSpeed);
            this.attackRange = Mathf.Max(0.2f, attackRange);
        }

        public EnemyAppearanceGroup Group => group;
        public ExpeditionEnemyRole Role => ExpeditionSpawnPoolEntry.ResolveRole(group);
        public float HealthMultiplier => Mathf.Max(0.01f, healthMultiplier);
        public float DamageMultiplier => Mathf.Max(0.01f, damageMultiplier);
        public float DefenseMultiplier => Mathf.Max(0f, defenseMultiplier);
        public float AttackInterval => Mathf.Max(0.1f, attackInterval);
        public float AttacksPerSecond => 1f / AttackInterval;
        public float MoveSpeed => Mathf.Max(0.1f, moveSpeed);
        public float AttackRange => Mathf.Max(0.2f, attackRange);
        public float ProjectileSpeed => Role == ExpeditionEnemyRole.Ranged ? 8f : 0f;

        public static ExpeditionEnemyTypeBalance CreateDefault(
            EnemyAppearanceGroup group,
            float meleeRange,
            float rangedRange)
        {
            var role = ExpeditionSpawnPoolEntry.ResolveRole(group);
            var health = 1f;
            var damage = 1f;
            var defense = 1f;
            var interval = role == ExpeditionEnemyRole.Ranged ? 1.2f : 1f;
            var speed = role == ExpeditionEnemyRole.Ranged ? 2.28f : 2.58f;
            var range = role == ExpeditionEnemyRole.Ranged ? rangedRange : meleeRange;

            switch (group)
            {
                case EnemyAppearanceGroup.UpperKnightLower:
                    health = 1.08f;
                    damage = 1.05f;
                    defense = 1.05f;
                    break;
                case EnemyAppearanceGroup.UpperKnightMid:
                    health = 1.18f;
                    damage = 1.1f;
                    defense = 1.1f;
                    break;
                case EnemyAppearanceGroup.UpperKnightHigh:
                    health = 1.3f;
                    damage = 1.18f;
                    defense = 1.18f;
                    break;
                case EnemyAppearanceGroup.UpperKnightFinal:
                    health = 1.45f;
                    damage = 1.28f;
                    defense = 1.28f;
                    break;
                case EnemyAppearanceGroup.Ninja:
                    health = 0.7f;
                    damage = 1.1f;
                    defense = 0.8f;
                    interval = 0.85f;
                    speed = 2.58f * 1.6f;
                    range = meleeRange;
                    break;
            }

            return new ExpeditionEnemyTypeBalance(
                group, health, damage, defense, interval, speed, range);
        }
    }
}
