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
            var mainUnits = new List<BattleUnitSnapshot>(MonsterRosterData.MainPartySlotCount);
            var reserveUnits = new List<BattleUnitSnapshot>(MonsterRosterData.ReservePartySlotCount);
            var addedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AppendPartyUnits(roster.MainPartySlots, roster, legionBonus, addedIds, mainUnits);
            AppendPartyUnits(roster.ReservePartySlots, roster, legionBonus, addedIds, reserveUnits);

            if (mainUnits.Count == 0)
            {
                throw new InvalidOperationException("The saved main party has no valid owned monster.");
            }

            return new BattlePartySnapshot(mainUnits.ToArray(), reserveUnits.ToArray());
        }

        private void AppendPartyUnits(
            IReadOnlyList<string> slots,
            MonsterRosterView roster,
            LegionStatBonus legionBonus,
            HashSet<string> addedIds,
            List<BattleUnitSnapshot> destination)
        {
            for (var index = 0; index < slots.Count; index++)
            {
                var monsterId = slots[index];
                if (string.IsNullOrWhiteSpace(monsterId) || !roster.Owns(monsterId) ||
                    !addedIds.Add(monsterId) || !catalog.TryGet(monsterId, out var definition) ||
                    !roster.TryGetOwnedMonster(monsterId, out var owned))
                {
                    continue;
                }

                destination.Add(new BattleUnitSnapshot(
                    monsterId,
                    ResolveStats(definition, owned.Level, owned.AscensionLevel, legionBonus),
                    definition.VisualTint,
                    definition.RuntimeAssetKey,
                    definition.RuntimeAssetSet,
                    ResolveUnlockedAbilityIds(definition, owned.AscensionLevel)));
            }
        }

        private static UnitStatsSnapshot ResolveStats(
            MonsterDefinition definition,
            int level,
            int ascensionLevel,
            LegionStatBonus legionBonus)
        {
            var levelMultiplier = MonsterLevelRules.GetStatMultiplier(level);
            var ascensionModifier = definition.RuntimeAssetSet?.AscensionProfile != null
                ? definition.RuntimeAssetSet.AscensionProfile.ResolveStatModifier(ascensionLevel)
                : default;
            var attackSpeed = Mathf.Max(
                0.01f,
                definition.AttackSpeed * levelMultiplier * (1f + ascensionModifier.AttackSpeedRate) *
                (1f + legionBonus.AttackSpeedRate));
            var projectileSpeed = RangedProjectileSpeed;
            var action = definition.RuntimeAssetSet?.CombatProfile?.Action as ProjectileActionDefinition;
            if (action != null)
            {
                projectileSpeed = action.Speed;
            }

            return new UnitStatsSnapshot
            {
                maxHealth = definition.MaxHealth * levelMultiplier * (1f + ascensionModifier.HealthRate) *
                            (1f + legionBonus.HealthRate),
                damage = definition.AttackPower * levelMultiplier * (1f + ascensionModifier.AttackRate) *
                         (1f + legionBonus.AttackRate),
                defense = definition.Defense * levelMultiplier * (1f + ascensionModifier.DefenseRate) *
                          (1f + legionBonus.DefenseRate),
                moveSpeed = definition.MoveSpeed * levelMultiplier * (1f + ascensionModifier.MoveSpeedRate) *
                            (1f + legionBonus.MoveSpeedRate),
                attackRange = definition.AttackRange * levelMultiplier * (1f + ascensionModifier.AttackRangeRate) *
                              (1f + legionBonus.AttackRangeRate),
                attackInterval = 1f / attackSpeed,
                projectileSpeed = definition.Ranged ? projectileSpeed : 0f,
                ranged = definition.Ranged
            };
        }

        private static string[] ResolveUnlockedAbilityIds(
            MonsterDefinition definition,
            int ascensionLevel)
        {
            var ascension = definition.RuntimeAssetSet?.AscensionProfile;
            return ascension == null
                ? Array.Empty<string>()
                : ascension.ResolveUnlockedAbilityIds(ascensionLevel);
        }
    }
}
