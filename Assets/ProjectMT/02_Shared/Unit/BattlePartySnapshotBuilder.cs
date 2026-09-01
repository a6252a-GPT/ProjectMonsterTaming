using System;
using System.Collections.Generic;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Stats;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public sealed class BattlePartySnapshotBuilder // 저장 편성을 전투 입력으로 해석
    {
        private const float RangedProjectileSpeed = 9f; // 현재 공용 원거리 투사체 속도

        private readonly MonsterCatalog catalog;
        private readonly MonsterRarityCatalog rarityCatalog;
        private readonly CombatStatConfig statConfig;

        public BattlePartySnapshotBuilder(MonsterCatalog monsterCatalog)
            : this(monsterCatalog, CombatStatConfig.RuntimeDefault)
        {
        }

        public BattlePartySnapshotBuilder(MonsterCatalog monsterCatalog, CombatStatConfig combatStatConfig)
            : this(monsterCatalog, null, combatStatConfig)
        {
        }

        public BattlePartySnapshotBuilder(
            MonsterCatalog monsterCatalog,
            MonsterRarityCatalog monsterRarityCatalog,
            CombatStatConfig combatStatConfig)
        {
            catalog = monsterCatalog ?? throw new ArgumentNullException(nameof(monsterCatalog));
            rarityCatalog = monsterRarityCatalog;
            statConfig = combatStatConfig ?? throw new ArgumentNullException(nameof(combatStatConfig));
            if (!catalog.TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            if (!statConfig.TryValidate(out error))
            {
                throw new InvalidOperationException(error);
            }
        }

        public BattlePartySnapshot Build(
            GameProgressView progress,
            IReadOnlyList<StatModifier> legionModifiers = null)
        {
            var roster = progress.Monsters;
            var mainUnits = new List<BattleUnitSnapshot>(MonsterRosterData.MainPartySlotCount);
            var reserveUnits = new List<BattleUnitSnapshot>(MonsterRosterData.ReservePartySlotCount);
            var addedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AppendPartyUnits(roster.MainPartySlots, roster, legionModifiers, addedIds, mainUnits, 0);
            AppendPartyUnits(
                roster.ReservePartySlots,
                roster,
                legionModifiers,
                addedIds,
                reserveUnits,
                MonsterRosterData.MainPartySlotCount);

            if (mainUnits.Count == 0)
            {
                throw new InvalidOperationException("The saved main party has no valid owned monster.");
            }

            return new BattlePartySnapshot(mainUnits.ToArray(), reserveUnits.ToArray(), statConfig);
        }

        private void AppendPartyUnits(
            IReadOnlyList<string> slots,
            MonsterRosterView roster,
            IReadOnlyList<StatModifier> legionModifiers,
            HashSet<string> addedIds,
            List<BattleUnitSnapshot> destination,
            int slotOffset)
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
                    ResolveStats(definition, owned.Level, owned.AscensionLevel, legionModifiers),
                    definition.VisualTint,
                    definition.RuntimeAssetKey,
                    definition.RuntimeAssetSet,
                    ResolveUnlockedAbilityIds(definition, owned.AscensionLevel),
                    definition.DisplayName,
                    ResolvePassiveSkill(monsterId),
                    ResolveActiveSkill(monsterId),
                    owned.Level,
                    ResolvePresentation(
                        definition,
                        monsterId,
                        slotOffset + index,
                        owned.Level,
                        owned.AscensionLevel)));
            }
        }

        private MonsterBattlePresentationSnapshot ResolvePresentation(
            MonsterDefinition definition,
            string monsterId,
            int partySlotIndex,
            int level,
            int ascensionLevel)
        {
            var rarity = rarityCatalog != null && rarityCatalog.TryGetRarity(monsterId, out var resolved)
                ? resolved
                : MonsterRarity.Common;
            return new MonsterBattlePresentationSnapshot(
                definition != null ? definition.Portrait : null,
                rarity,
                partySlotIndex,
                level,
                ascensionLevel);
        }

        private MonsterPassiveSkill ResolvePassiveSkill(string monsterId)
        {
            return rarityCatalog != null &&
                   rarityCatalog.TryGetSkillLoadout(monsterId, out var passive, out _)
                ? passive
                : null;
        }

        private MonsterActiveSkill ResolveActiveSkill(string monsterId)
        {
            return rarityCatalog != null &&
                   rarityCatalog.TryGetSkillLoadout(monsterId, out _, out var active)
                ? active
                : null;
        }

        private UnitStatsSnapshot ResolveStats(
            MonsterDefinition definition,
            int level,
            int ascensionLevel,
            IReadOnlyList<StatModifier> legionModifiers)
        {
            var levelMultiplier = MonsterLevelRules.GetStatMultiplier(level);
            var ascensionModifier = definition.RuntimeAssetSet?.AscensionProfile != null
                ? definition.RuntimeAssetSet.AscensionProfile.ResolveStatModifier(ascensionLevel)
                : default;
            var projectileSpeed = RangedProjectileSpeed;
            var action = definition.RuntimeAssetSet?.CombatProfile?.Action as ProjectileActionDefinition;
            if (action != null)
            {
                projectileSpeed = action.ResolvedSpeed;
            }

            var baseStats = new UnitStatsSnapshot
            {
                maxHealth = definition.MaxHealth * levelMultiplier,
                damage = definition.AttackPower * levelMultiplier,
                defense = definition.Defense * levelMultiplier,
                moveSpeed = definition.MoveSpeed,
                attackRange = definition.AttackRange,
                attackInterval = 1f / Mathf.Max(0.01f, definition.AttackSpeed),
                projectileSpeed = definition.Ranged ? projectileSpeed : 0f,
                ranged = definition.Ranged,
                criticalRate = statConfig.BaseCriticalRate,
                criticalDamageMultiplier = statConfig.BaseCriticalDamageMultiplier
            };

            var modifiers = new List<StatModifier>((legionModifiers?.Count ?? 0) + 6);
            if (legionModifiers != null)
            {
                for (var index = 0; index < legionModifiers.Count; index++)
                {
                    modifiers.Add(legionModifiers[index]);
                }
            }

            AppendAscensionModifiers(ascensionModifier, modifiers);
            return StatResolver.Resolve(baseStats, modifiers, statConfig);
        }

        private static void AppendAscensionModifiers(
            MonsterStatModifier modifier,
            List<StatModifier> destination)
        {
            AddRate(destination, StatId.MaxHealth, modifier.HealthRate, "ascension");
            AddRate(destination, StatId.AttackPower, modifier.AttackRate, "ascension");
            AddRate(destination, StatId.Defense, modifier.DefenseRate, "ascension");
            AddRate(destination, StatId.AttackSpeed, modifier.AttackSpeedRate, "ascension");
            AddRate(destination, StatId.MoveSpeed, modifier.MoveSpeedRate, "ascension");
            AddRate(destination, StatId.AttackRange, modifier.AttackRangeRate, "ascension");
        }

        private static void AddRate(
            List<StatModifier> destination,
            StatId statId,
            float value,
            string sourceId)
        {
            if (!Mathf.Approximately(value, 0f))
            {
                destination.Add(new StatModifier(statId, StatOperation.AdditiveRate, value, sourceId));
            }
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
