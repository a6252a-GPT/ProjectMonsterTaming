using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Features.Equipment;
using ProjectMT.Shared.Equipment;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{

    // 같은 StartData를 쓰는 재도전 동안 셀·순번별 장비 결과를 고정한다.
    public sealed class HexEquipmentRewardContext
    {
        private readonly Dictionary<string, EquipmentInstanceData> rewards =
            new Dictionary<string, EquipmentInstanceData>(StringComparer.Ordinal);
        private int basisStage;

        public int BasisStage => basisStage;

        public void Initialize(GameProgressView progress)
        {
            if (basisStage == 0)
            {
                basisStage = ExpeditionEquipmentLevelResolver.ResolveHighestClearedStage(progress);
            }
        }

        public EquipmentInstanceData Resolve(
            HexCoordinates coordinates,
            int ordinal,
            int rollSeed,
            EquipmentBalanceConfig balance)
        {
            if (basisStage < 1)
            {
                throw new InvalidOperationException("장비 보상 기준 단계가 초기화되지 않았습니다.");
            }

            if (ordinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            }

            var key = $"{coordinates.Q}:{coordinates.R}:{rollSeed}:{ordinal}";
            if (!rewards.TryGetValue(key, out var instance))
            {
                var random = new System.Random(unchecked(rollSeed * 486187739 + ordinal * 16777619));
                instance = EquipmentDropRoller.RollSingle(balance, basisStage, random);
                rewards.Add(key, instance.Clone());
            }

            return instance.Clone();
        }
    }

    public sealed class HexCastleRaidStartData : IPartyDeploymentStartData // 육각 전장 부대 투입값
    {
        public const int DeploymentSlotCount = MonsterRosterData.MainPartySlotCount + MonsterRosterData.ReservePartySlotCount;

        public HexCastleRaidStartData(BattlePartySnapshot party)
        {
            Party = party;
            EquipmentRewards = new HexEquipmentRewardContext();
            DeploymentUnits = new BattleUnitSnapshot[DeploymentSlotCount];
            CopyDeploymentSlots(party?.Units, 0, MonsterRosterData.MainPartySlotCount);
            CopyDeploymentSlots(party?.ReserveUnits, MonsterRosterData.MainPartySlotCount,
                MonsterRosterData.ReservePartySlotCount);
            UnitSlotCount = DeploymentUnits.Any(unit => unit != null) ? DeploymentSlotCount : 0;
            SummonsPerSlot = UnitSlotCount == 0
                ? 1
                : Enumerable.Range(0, UnitSlotCount).Max(ResolveSummonsForSlot);
            DeploymentLimit = Enumerable.Range(0, UnitSlotCount).Sum(ResolveSummonsForSlot);
        }

        public BattlePartySnapshot Party { get; }
        public HexEquipmentRewardContext EquipmentRewards { get; }
        public BattleUnitSnapshot[] DeploymentUnits { get; }
        public int UnitSlotCount { get; }
        public int SummonsPerSlot { get; }
        public int DeploymentLimit { get; }

        public int ResolveSummonsForSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= UnitSlotCount || DeploymentUnits[slotIndex] == null)
            {
                return 0;
            }

            return ResolveSummonsForAscension(DeploymentUnits[slotIndex].Presentation.AscensionLevel);
        }

        private void CopyDeploymentSlots(BattleUnitSnapshot[] source, int offset, int capacity)
        {
            if (source == null) return;
            for (var index = 0; index < Mathf.Min(source.Length, capacity); index++)
            {
                var unit = source[index];
                if (unit == null) continue;
                var slot = unit.Presentation.HasPartySlot ? unit.Presentation.PartySlotIndex : offset + index;
                if (slot < offset || slot >= offset + capacity) slot = offset + index;
                if (DeploymentUnits[slot] != null)
                    slot = System.Array.FindIndex(DeploymentUnits, offset, capacity, value => value == null);
                if (slot >= 0) DeploymentUnits[slot] = unit;
            }
        }

        public static int ResolveSummonsForAscension(int ascensionLevel)
        {
            return Mathf.Clamp(ascensionLevel, 0, 5) switch
            {
                >= 5 => 3,
                >= 3 => 2,
                _ => 1
            };
        }
    }
}

