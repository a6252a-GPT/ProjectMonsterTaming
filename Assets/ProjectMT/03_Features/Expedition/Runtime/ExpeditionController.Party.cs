using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectMT.Features.Equipment;
using ProjectMT.Features.MainBattle;
using ProjectMT.Features.Quest;
using ProjectMT.Features.WorldDrops;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.Reward;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Expedition
{
    public sealed partial class ExpeditionController
    {
        public void CollectActiveUnits(List<UnitActor> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            foreach (var pair in playerSlotByActor)
            {
                if (pair.Key != null && pair.Key.IsAlive && pair.Key.IsCombatReady)
                {
                    destination.Add(pair.Key);
                }
            }

            foreach (var pair in enemyWaveByActor)
            {
                if (pair.Key != null && pair.Key.IsAlive && pair.Key.IsCombatReady)
                {
                    destination.Add(pair.Key);
                }
            }
        }

        public void CollectPlayerUnits(List<UnitActor> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            foreach (var pair in playerSlotByActor)
            {
                if (pair.Key != null && pair.Key.IsAlive)
                {
                    destination.Add(pair.Key); // 위치 편성 중에도 파티 HUD는 실제 소환 유닛을 표시
                }
            }
        }

        public bool TryGetPlayerSlot(UnitActor actor, out int slotIndex)
        {
            if (actor != null && playerSlotByActor.TryGetValue(actor, out slotIndex))
            {
                return true;
            }

            slotIndex = -1;
            return false;
        }

        private void SpawnParty(bool placementMode)
        {
            var units = activeRunParty.Units;
            for (var i = 0; i < units.Length && i < 5; i++) // 시드 본부대 최대 5기
            {
                var formationOffset = ResolvePlayerFormationOffset(i);
                var position = ResolvePlayerSpawnPosition(i, formationOffset);
                var formationStats = MainBattleFormationBuffRules.ApplyStats(units[i].Stats, formationOffset);
                var request = new UnitSpawnRequest(
                    units[i].UnitId,
                    formationStats,
                    UnitTeam.Player,
                    canMove: !placementMode,
                    canAttack: !placementMode,
                    visualTint: units[i].VisualTint,
                    runtimeAssetSet: units[i].RuntimeAssetSet,
                    supportOutputMultiplier: MainBattleFormationBuffRules.GetSupportOutputMultiplier(formationOffset),
                    passiveSkill: units[i].PassiveSkill,
                    activeSkill: units[i].ActiveSkill,
                    monsterLevel: units[i].Level,
                    entryReason: UnitEntryReason.InitialDeployment,
                    displayName: units[i].DisplayName,
                    presentation: units[i].Presentation.WithPartySlot(i));
                var actor = combatWorld.SpawnUnit(playerUnitPrefab, request, position, Quaternion.identity);
                if (!placementMode && actor != null)
                {
                    actor.SkillRuntime.GrantActiveEnergy(MonsterActiveEnergyConfig.StageStartEnergy);
                }
                ApplyPlayerAIProfile(actor, units[i].UnitId);
                TrackPlayerUnit(actor, i);
            }
        }

        private Vector2 ResolvePlayerFormationOffset(int slotIndex)
        {
            if (progress != null &&
                progress.View.MainBattleFormation.TryGetSlotOffset(slotIndex, out var savedOffset))
            {
                return MainBattleFormationRules.IsHexPosition(savedOffset)
                    ? savedOffset
                    : MainBattleFormationRules.SnapToHex(savedOffset);
            }

            return MainBattleFormationRules.GetDefaultOffset(slotIndex);
        }

        private Vector3 ResolvePlayerSpawnPosition(int slotIndex, Vector2 formationOffset)
        {
            if (formationFrameConfigured)
            {
                var spawnY = ResolvePlayerSpawnHeight(slotIndex);
                return new Vector3(
                    formationOrigin.x + formationOffset.x,
                    spawnY,
                    formationOrigin.z + formationOffset.y);
            }

            if (playerSpawnPoints != null && slotIndex >= 0 && slotIndex < playerSpawnPoints.Length &&
                playerSpawnPoints[slotIndex] != null)
            {
                return playerSpawnPoints[slotIndex].position;
            }

            var fallbackOrigin = playerFormationAnchor == null ? transform.position : playerFormationAnchor.position;
            return new Vector3(
                fallbackOrigin.x + formationOffset.x,
                fallbackOrigin.y,
                fallbackOrigin.z + formationOffset.y);
        }

        private float ResolvePlayerSpawnHeight(int slotIndex)
        {
            if (playerSpawnPoints != null && slotIndex >= 0 && slotIndex < playerSpawnPoints.Length &&
                playerSpawnPoints[slotIndex] != null)
            {
                return playerSpawnPoints[slotIndex].position.y;
            }

            return playerFormationAnchor == null ? transform.position.y : playerFormationAnchor.position.y;
        }

        private void ApplyPlayerAIProfile(UnitActor actor, string monsterId)
        {
            if (actor != null && mainBattleAIProfiles != null &&
                mainBattleAIProfiles.TryResolve(monsterId, out var profile))
            {
                actor.SetCombatBehavior(profile.CreateBehavior());
            }
        }

        private static void ApplyEnemyAIProfile(UnitActor actor, bool ranged)
        {
            if (actor == null)
            {
                return;
            }

            actor.SetCombatBehavior(CombatImpactTuning.ActiveConfig.CreateMainBattleEnemyBehavior(ranged));
        }

        private void ConfigureFormationFrame(Collider formationGround)
        {
            formationFrameConfigured = false;
            if (playerFormationAnchor != null)
            {
                var anchorPosition = playerFormationAnchor.position;
                formationOrigin = new Vector3(anchorPosition.x, 0f, anchorPosition.z);
                formationFrameConfigured = true;
                return;
            }

            if (formationGround == null)
            {
                return;
            }

            var bounds = formationGround.bounds;
            formationOrigin = new Vector3(bounds.center.x, 0f, bounds.center.z);
            formationFrameConfigured = true;
        }

        private void TrackPlayerUnit(UnitActor actor, int slotIndex)
        {
            if (actor == null)
            {
                return;
            }

            playerSlotByActor[actor] = slotIndex;
            actor.Died += HandlePlayerUnitDied;
        }

        private void HandlePlayerUnitDied(UnitActor actor)
        {
            if (actor == null || !playerSlotByActor.TryGetValue(actor, out var slotIndex))
            {
                return;
            }

            actor.Died -= HandlePlayerUnitDied;
            playerSlotByActor.Remove(actor);
            if (!running)
            {
                return;
            }

            TryDeployNextReserve(slotIndex, actor.transform.position); // 쓰러진 자리로 순차 대타 투입
        }

        private bool TryDeployNextReserve(int slotIndex, Vector3 position)
        {
            var reserves = activeRunParty?.ReserveUnits ?? Array.Empty<BattleUnitSnapshot>();
            while (nextReserveIndex < reserves.Length)
            {
                var reserve = reserves[nextReserveIndex++];
                if (reserve == null)
                {
                    continue;
                }

                var request = new UnitSpawnRequest(
                    reserve.UnitId,
                    reserve.Stats,
                    UnitTeam.Player,
                    visualTint: reserve.VisualTint,
                    runtimeAssetSet: reserve.RuntimeAssetSet,
                    passiveSkill: reserve.PassiveSkill,
                    activeSkill: reserve.ActiveSkill,
                    monsterLevel: reserve.Level,
                    entryReason: UnitEntryReason.ReserveReplacement,
                    displayName: reserve.DisplayName,
                    presentation: reserve.Presentation.WithPartySlot(slotIndex));
                var actor = combatWorld.SpawnUnit(playerUnitPrefab, request, position, Quaternion.identity);
                if (actor == null)
                {
                    continue;
                }

                ApplyPlayerAIProfile(actor, reserve.UnitId);
                TrackPlayerUnit(actor, slotIndex);
                return true;
            }

            return false;
        }

        private void ResetPlayerTracking()
        {
            foreach (var pair in playerSlotByActor)
            {
                if (pair.Key != null)
                {
                    pair.Key.Died -= HandlePlayerUnitDied;
                }
            }

            playerSlotByActor.Clear();
            nextReserveIndex = 0;
        }
    }
}
