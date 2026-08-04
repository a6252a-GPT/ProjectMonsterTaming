using System;
using System.Collections.Generic;
using ProjectMT.Shared.GameData;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public sealed class BattlePartySnapshotBuilder // 저장 편성을 전투 입력으로 해석
    {
        private const float RangedProjectileSpeed = 9f; // 현재 공용 원거리 투사체 속도

        private readonly MonsterCatalog catalog;

        public BattlePartySnapshotBuilder(MonsterCatalog monsterCatalog)
        {
            catalog = monsterCatalog ?? throw new ArgumentNullException(nameof(monsterCatalog));
            if (!catalog.TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }
        }

        public BattlePartySnapshot Build(GameProgressView progress, LegionStatBonus legionBonus = default)
        {
            var roster = progress.Monsters;
            var units = new List<BattleUnitSnapshot>(MonsterRosterData.MainPartySlotCount);
            var addedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var slots = roster.MainPartySlots;
            for (var index = 0; index < slots.Count; index++)
            {
                var monsterId = slots[index];
                if (string.IsNullOrWhiteSpace(monsterId) || !roster.Owns(monsterId) ||
                    !addedIds.Add(monsterId) || !catalog.TryGet(monsterId, out var definition))
                {
                    continue;
                }

                units.Add(new BattleUnitSnapshot(monsterId, ResolveStats(definition, legionBonus)));
            }

            if (units.Count == 0)
            {
                throw new InvalidOperationException("The saved main party has no valid owned monster.");
            }

            return new BattlePartySnapshot(units.ToArray());
        }

        private static UnitStatsSnapshot ResolveStats(
            MonsterDefinition definition,
            LegionStatBonus legionBonus)
        {
            var attackSpeed = Mathf.Max(
                0.01f,
                definition.AttackSpeed * (1f + legionBonus.AttackSpeedRate));
            return new UnitStatsSnapshot
            {
                maxHealth = definition.MaxHealth * (1f + legionBonus.HealthRate),
                damage = definition.AttackPower * (1f + legionBonus.AttackRate),
                defense = definition.Defense * (1f + legionBonus.DefenseRate),
                moveSpeed = definition.MoveSpeed * (1f + legionBonus.MoveSpeedRate),
                attackRange = definition.AttackRange * (1f + legionBonus.AttackRangeRate),
                attackInterval = 1f / attackSpeed,
                projectileSpeed = definition.Ranged ? RangedProjectileSpeed : 0f,
                ranged = definition.Ranged
            };
        }
    }
}
