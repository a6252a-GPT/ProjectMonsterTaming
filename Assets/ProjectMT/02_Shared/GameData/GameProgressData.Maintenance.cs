using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Commander;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.Gacha;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using ProjectMT.Shared.Stats;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectMT.Shared.GameData
{
    public sealed partial class GameProgressData
    {
        internal void Repair(
            CommanderSkillBalanceConfig commanderSkillBalanceConfig = null,
            CommanderSkillSummonConfig commanderSkillSummonConfig = null)
        {
            if (lastClearedStage > ExpeditionMaximumStage && hardLastClearedStage <= 0)
            {
                hardLastClearedStage = Math.Min(
                    ExpeditionMaximumStage,
                    lastClearedStage - ExpeditionMaximumStage);
                hardCurrentChallengeStage = Math.Min(
                    ExpeditionMaximumStage,
                    Math.Max(hardLastClearedStage + 1, currentChallengeStage - ExpeditionMaximumStage));
                lastClearedStage = ExpeditionMaximumStage; // 구버전 101+ 진행을 하드 단계로 이관
                expeditionDifficulty = ExpeditionDifficulty.Hard;
            }

            lastClearedStage = Math.Max(0, Math.Min(ExpeditionMaximumStage, lastClearedStage));
            currentChallengeStage = lastClearedStage >= ExpeditionMaximumStage
                ? ExpeditionMaximumStage
                : Math.Min(ExpeditionMaximumStage, Math.Max(lastClearedStage + 1, currentChallengeStage));
            hardLastClearedStage = Math.Max(0, Math.Min(ExpeditionMaximumStage, hardLastClearedStage));
            hardCurrentChallengeStage = hardLastClearedStage >= ExpeditionMaximumStage
                ? ExpeditionMaximumStage
                : Math.Min(ExpeditionMaximumStage, Math.Max(hardLastClearedStage + 1, hardCurrentChallengeStage));
            gold = Math.Max(0L, gold);
            foodRiotBestKills = Math.Max(0, foodRiotBestKills);
            guardiansTowerBestKills = Math.Max(0, guardiansTowerBestKills); // 08.06 안건준 추가
            guardiansTowerDifficultyLevel = Math.Max(0, guardiansTowerDifficultyLevel); // 08.07 안건준 추가
            castleRaidHighestClearedStage = Math.Clamp(
                castleRaidHighestClearedStage,
                0,
                CastleRaidStageRules.MaximumStage);
            if (castleRaidFirstClear && castleRaidHighestClearedStage == 0)
            {
                castleRaidHighestClearedStage = CastleRaidStageRules.MinimumStage;
            }
            castleRaidFirstClear = castleRaidHighestClearedStage > 0;
            commander ??= CommanderProgressData.CreateDefault();
            commander.Repair();
            monsters ??= MonsterRosterData.CreateDefault();
            monsters.Repair();
            mainBattleFormation ??= MainBattleFormationData.CreateDefault();
            mainBattleFormation.Repair();
            ascensionCurrency = Math.Max(0, ascensionCurrency);
            gachaPity ??= GachaPityData.CreateDefault();
            gachaPity.Repair();
            equipment ??= EquipmentSaveData.CreateDefault(); // 08.10 안건준 추가
            equipment.Repair();
            items ??= ItemInventoryData.CreateDefault();
            items.Repair();
            itemViewCache = null;
            growthDungeons ??= GrowthDungeonProgressData.CreateDefault();
            growthDungeons.Repair();
            offlineRewards ??= OfflineRewardProgressData.CreateDefault();
            offlineRewards.Repair();
            attendance ??= AttendanceProgressData.CreateDefault();
            attendance.Repair();
            mail ??= MailProgressData.CreateDefault();
            mail.Repair();
            equipmentSlotUpgrade ??= EquipmentSlotUpgradeData.CreateDefault();
            equipmentSlotUpgrade.Repair();
            commanderPotential ??= CommanderPotentialData.CreateDefault();
            commanderPotential.Repair();
            commanderLegionGrowth ??= CommanderLegionGrowthData.CreateDefault();
            commanderLegionGrowth.Repair();
            commanderSkills ??= CommanderSkillProgressData.CreateDefault();
            commanderSkills.Repair(commanderSkillBalanceConfig, commanderSkillSummonConfig);

            if (!IsValidExpeditionDifficulty(expeditionDifficulty) ||
                (expeditionDifficulty == ExpeditionDifficulty.Hard &&
                 lastClearedStage < ExpeditionMaximumStage))
            {
                expeditionDifficulty = ExpeditionDifficulty.Normal;
            }

            if (!IsValidExpeditionMode(expeditionMode) ||
                (ActiveLastClearedStage == 0 && expeditionMode == ExpeditionRunMode.Repeat))
            {
                expeditionMode = ExpeditionRunMode.Challenge; // 손상값·클리어 전 반복 복구
            }

            RepairQuest();
        }

        internal void Repair(
            CommanderGrowthConfig commanderGrowthConfig,
            CommanderSkillBalanceConfig commanderSkillBalanceConfig = null,
            CommanderSkillSummonConfig commanderSkillSummonConfig = null)
        {
            Repair(commanderSkillBalanceConfig, commanderSkillSummonConfig);
            commander.Repair(commanderGrowthConfig);
            commanderLegionGrowth.Repair(commanderGrowthConfig);
        }

        internal void MigrateFromVersion(int sourceDataVersion)
        {
            if (sourceDataVersion <= 9)
            {
                items ??= ItemInventoryData.CreateDefault(); // v9 이전 저장에는 일반 아이템 필드가 없음
            }

            if (sourceDataVersion <= 10)
            {
                items ??= ItemInventoryData.CreateDefault();
                items.Repair();
                items.MergeLegacyCoreBalance(ItemIds.Gold, Math.Max(0L, gold));
                items.MergeLegacyCoreBalance(ItemIds.AscensionStone, Math.Max(0, ascensionCurrency));
                coreBalanceMigrationCompleted = true; // 백업값은 보존하고 원본 권한만 ItemInventory로 이동
            }

            if (sourceDataVersion <= 20)
            {
                monsters ??= MonsterRosterData.CreateDefault();
                monsters.MigrateRetiredMonsterIds(); // 퇴역·개명 ID의 보유 진행과 편성을 현재 정식 ID로 보존
            }

            if (sourceDataVersion <= 21 && castleRaidFirstClear && castleRaidHighestClearedStage == 0)
            {
                castleRaidHighestClearedStage = CastleRaidStageRules.MinimumStage;
            }

            if (sourceDataVersion <= 12)
            {
                growthDungeons ??= GrowthDungeonProgressData.CreateDefault();
                if (foodRiotBestKills > 0)
                {
                    growthDungeons.RecordClear(GrowthDungeonProgressIds.FoodRiot, 1); // 기존 기록이 있으면 1단계 클리어로 승격
                }

                if (guardiansTowerDifficultyLevel > 0)
                {
                    growthDungeons.RecordClear(
                        GrowthDungeonProgressIds.GuardiansTower,
                        guardiansTowerDifficultyLevel);
                }
            }

            if (sourceDataVersion <= 13)
            {
                offlineRewards = OfflineRewardProgressData.CreateDefault(); // 기존 저장은 소급 보상 없이 현재부터 기록
            }

            if (sourceDataVersion <= 15)
            {
                commanderLegionGrowth ??= CommanderLegionGrowthData.CreateDefault();
                commanderLegionGrowth.SetMigratedTrainingPoints(Math.Max(0, (commander?.Level ?? 1) - 1));
            }

            if (sourceDataVersion <= 17)
            {
                commanderSkills = CommanderSkillProgressData.CreateDefault(); // 기존 저장은 초기 2스킬로 시작
            }

            if (sourceDataVersion <= 18)
            {
                attendance = AttendanceProgressData.CreateDefault();
                mail = MailProgressData.CreateDefault();
            }


            if (sourceDataVersion <= 19)
            {
                equipment ??= EquipmentSaveData.CreateDefault();
                equipment.MigrateOfflineAutoDismantlePolicy(); // 기존 계정은 안전한 기본값으로 시작
            }

            Repair();
        }

        private static bool IsValidExpeditionMode(ExpeditionRunMode mode)
        {
            return mode == ExpeditionRunMode.Challenge || mode == ExpeditionRunMode.Repeat;
        }

        private static bool IsValidExpeditionDifficulty(ExpeditionDifficulty difficulty)
        {
            return difficulty == ExpeditionDifficulty.Normal || difficulty == ExpeditionDifficulty.Hard;
        }
    }
}
