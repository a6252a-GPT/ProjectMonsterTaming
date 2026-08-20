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
    public enum ExpeditionRunMode // 원정대 진행 방식
    {
        Challenge,
        Repeat
    }

    [Serializable]
    public sealed class CommanderProgressData // 군단장 성장 저장 원본
    {
        [SerializeField] private int level = 1;
        [SerializeField] private long experience;

        public int Level => level;
        public long Experience => experience;

        public static CommanderProgressData CreateDefault()
        {
            return new CommanderProgressData();
        }

        public CommanderProgressData Clone()
        {
            return new CommanderProgressData
            {
                level = level,
                experience = experience
            };
        }

        internal void Repair()
        {
            level = Math.Max(1, level);
            experience = Math.Max(0L, experience);
        }

        internal void Repair(CommanderGrowthConfig config)
        {
            Repair();
            if (config == null)
            {
                return;
            }

            level = Math.Min(level, config.MaxLevel);
            if (level >= config.MaxLevel)
            {
                experience = 0L;
            }
        }

        internal void GrantExperience(long amount, CommanderGrowthConfig config)
        {
            if (amount <= 0L || config == null)
            {
                return;
            }

            if (level >= config.MaxLevel)
            {
                return;
            }

            experience = experience > long.MaxValue - amount
                ? long.MaxValue
                : experience + amount;
        }

        internal bool TryLevelUp(int expectedLevel, CommanderGrowthConfig config)
        {
            if (config == null || level != expectedLevel ||
                !config.TryResolveLevelUp(level, experience, out var nextLevel, out var remainingExperience))
            {
                return false;
            }

            level = nextLevel;
            experience = remainingExperience;
            return true;
        }
    }

    public readonly struct CommanderProgressView // 성장 기능에 전달할 읽기 전용 값
    {
        public CommanderProgressView(CommanderProgressData data)
            : this(data?.Level ?? 1, data?.Experience ?? 0L)
        {
        }

        public CommanderProgressView(int level, long experience) // 기능 계산·테스트용 입력
        {
            Level = Math.Max(1, level);
            Experience = Math.Max(0L, experience);
        }

        public int Level { get; }
        public long Experience { get; }
    }

    [Serializable]
    public sealed class GachaPityData // 뽑기 확률 보정용 누적 카운터 (등급별 천장 판정)
    {
        [SerializeField] private int pullsSinceRareOrBetter; // 희귀 확정 보정용 (10뽑마다)
        [SerializeField] private int pullsSinceEpicOrBetter; // 영웅 천장 (30뽑)
        [SerializeField] private int pullsSinceLegendaryOrBetter; // 전설 천장 (100뽑)
        [SerializeField] private int pullsSinceMythicOrBetter; // 신화 천장 (300뽑)

        public int PullsSinceRareOrBetter => pullsSinceRareOrBetter;
        public int PullsSinceEpicOrBetter => pullsSinceEpicOrBetter;
        public int PullsSinceLegendaryOrBetter => pullsSinceLegendaryOrBetter;
        public int PullsSinceMythicOrBetter => pullsSinceMythicOrBetter;

        public static GachaPityData CreateDefault()
        {
            return new GachaPityData();
        }

        public GachaPityData Clone()
        {
            return new GachaPityData
            {
                pullsSinceRareOrBetter = pullsSinceRareOrBetter,
                pullsSinceEpicOrBetter = pullsSinceEpicOrBetter,
                pullsSinceLegendaryOrBetter = pullsSinceLegendaryOrBetter,
                pullsSinceMythicOrBetter = pullsSinceMythicOrBetter
            };
        }

        internal void Repair()
        {
            pullsSinceRareOrBetter = Math.Max(0, pullsSinceRareOrBetter);
            pullsSinceEpicOrBetter = Math.Max(0, pullsSinceEpicOrBetter);
            pullsSinceLegendaryOrBetter = Math.Max(0, pullsSinceLegendaryOrBetter);
            pullsSinceMythicOrBetter = Math.Max(0, pullsSinceMythicOrBetter);
        }

        // 이번 뽑기에서 나온 등급을 반영해 각 천장 카운터를 갱신한다.
        // 그 등급 이상이 나왔으면 해당 카운터는 0으로, 아니면 1 증가.
        internal void RegisterPull(MonsterRarity rarity)
        {
            pullsSinceRareOrBetter = rarity >= MonsterRarity.Rare ? 0 : pullsSinceRareOrBetter + 1;
            pullsSinceEpicOrBetter = rarity >= MonsterRarity.Epic ? 0 : pullsSinceEpicOrBetter + 1;
            pullsSinceLegendaryOrBetter = rarity >= MonsterRarity.Legendary ? 0 : pullsSinceLegendaryOrBetter + 1;
            pullsSinceMythicOrBetter = rarity >= MonsterRarity.Mythic ? 0 : pullsSinceMythicOrBetter + 1;
        }
    }

    public readonly struct GachaPityView // 뽑기 확률 계산에 전달할 읽기 전용 천장 카운터
    {
        public GachaPityView(GachaPityData data)
        {
            PullsSinceRareOrBetter = Math.Max(0, data?.PullsSinceRareOrBetter ?? 0);
            PullsSinceEpicOrBetter = Math.Max(0, data?.PullsSinceEpicOrBetter ?? 0);
            PullsSinceLegendaryOrBetter = Math.Max(0, data?.PullsSinceLegendaryOrBetter ?? 0);
            PullsSinceMythicOrBetter = Math.Max(0, data?.PullsSinceMythicOrBetter ?? 0);
        }

        public int PullsSinceRareOrBetter { get; }
        public int PullsSinceEpicOrBetter { get; }
        public int PullsSinceLegendaryOrBetter { get; }
        public int PullsSinceMythicOrBetter { get; }
    }

    [Serializable]
    public sealed class GameProgressData // 시드 사용자 진행 원본
    {
        [SerializeField] private int currentChallengeStage = 1; // 현재 도전 단계
        [SerializeField] private int lastClearedStage; // 마지막 성공 단계
        [SerializeField] private ExpeditionRunMode expeditionMode = ExpeditionRunMode.Challenge; // 저장된 실행 모드
        [FormerlySerializedAs("temporaryGold")]
        [SerializeField] private long gold; // v10 이하 잔액의 동결 백업
        [FormerlySerializedAs("vegetableRiotBestKills")]
        [SerializeField] private int foodRiotBestKills; // 식량 대소동 최고 처치
        [SerializeField] private int guardiansTowerBestKills; // 08.06 안건준 추가 - 수호자의 탑 최고 처치 (식량 대소동과 별도 집계)
        [SerializeField] private int guardiansTowerDifficultyLevel; // 08.07 안건준 추가 - 수호자의 탑 난이도(클리어할 때마다 1씩 증가, 적 수·건물 체력 스케일링에 사용)
        [SerializeField] private bool castleRaidFirstClear; // 군단의 역습 첫 승리
        [SerializeField] private CommanderProgressData commander = CommanderProgressData.CreateDefault(); // 군단장 성장값
        [SerializeField] private MonsterRosterData monsters = MonsterRosterData.CreateDefault(); // 보유·편성값
        [SerializeField] private MainBattleFormationData mainBattleFormation = MainBattleFormationData.CreateDefault(); // 본부대 시작 위치
        [SerializeField] private int ascensionCurrency; // v10 이하 잔액의 동결 백업
        [SerializeField] private GachaPityData gachaPity = GachaPityData.CreateDefault(); // 뽑기 천장 누적 카운터
        [SerializeField] private EquipmentSaveData equipment = EquipmentSaveData.CreateDefault(); // 08.10 안건준 추가 - 장비 보유·장착 저장
        [SerializeField] private ItemInventoryData items = ItemInventoryData.CreateDefault(); // 일반 아이템 보유 수량
        [SerializeField] private GrowthDungeonProgressData growthDungeons = GrowthDungeonProgressData.CreateDefault(); // 성장 던전 단계·열쇠 기준일
        [SerializeField] private OfflineRewardProgressData offlineRewards = OfflineRewardProgressData.CreateDefault(); // 방치 시작·정산 영수증
        [SerializeField] private AttendanceProgressData attendance = AttendanceProgressData.CreateDefault(); // 28일 누적 출석
        [SerializeField] private MailProgressData mail = MailProgressData.CreateDefault(); // 미수령 우편
        [SerializeField] private bool coreBalanceMigrationCompleted = true; // v11 이관 재실행 방지
        [NonSerialized] private ItemInventoryView? itemViewCache; // 변경 전까지 목록 복사 재사용
        [SerializeField] private EquipmentSlotUpgradeData equipmentSlotUpgrade = EquipmentSlotUpgradeData.CreateDefault(); // 부위 슬롯 영구 강화 레벨(장비 보유·장착과 별도 저장)
        [SerializeField] private CommanderPotentialData commanderPotential = CommanderPotentialData.CreateDefault(); // 군단장 잠재능력 5슬롯
        [SerializeField] private CommanderLegionGrowthData commanderLegionGrowth = CommanderLegionGrowthData.CreateDefault(); // 군단 공용 6종 강화
        [SerializeField] private CommanderSkillProgressData commanderSkills = CommanderSkillProgressData.CreateDefault(); // 군단장 스킬 보유·장착·자동사용

        public int CurrentChallengeStage => currentChallengeStage;
        public int LastClearedStage => lastClearedStage;
        public ExpeditionRunMode ExpeditionMode => expeditionMode;
        public long Gold => coreBalanceMigrationCompleted
            ? (items ?? ItemInventoryData.CreateDefault()).GetQuantity(ItemIds.Gold)
            : Math.Max(0L, gold);
        public long Diamond => (items ?? ItemInventoryData.CreateDefault()).GetQuantity(ItemIds.Diamond);
        public int FoodRiotBestKills => foodRiotBestKills;
        public int GuardiansTowerBestKills => guardiansTowerBestKills; // 08.06 안건준 추가
        public int GuardiansTowerDifficultyLevel => guardiansTowerDifficultyLevel; // 08.07 안건준 추가
        public bool CastleRaidFirstClear => castleRaidFirstClear;
        public CommanderProgressView Commander => new CommanderProgressView(commander);
        public MonsterRosterView Monsters => monsters?.CreateView() ?? MonsterRosterData.CreateDefault().CreateView();
        public MainBattleFormationView MainBattleFormation =>
            (mainBattleFormation ?? MainBattleFormationData.CreateDefault()).CreateView();
        public long AscensionCurrency => coreBalanceMigrationCompleted
            ? (items ?? ItemInventoryData.CreateDefault()).GetQuantity(ItemIds.AscensionStone)
            : Math.Max(0, ascensionCurrency);
        public GachaPityView GachaPity => new GachaPityView(gachaPity);
        public EquipmentSaveDataView Equipment => (equipment ?? EquipmentSaveData.CreateDefault()).CreateView(); // 08.10 안건준 추가
        public ItemInventoryView Items
        {
            get
            {
                itemViewCache ??= (items ?? ItemInventoryData.CreateDefault()).CreateView();
                return itemViewCache.Value;
            }
        }
        public GrowthDungeonProgressView GrowthDungeons =>
            (growthDungeons ?? GrowthDungeonProgressData.CreateDefault()).CreateView();
        public OfflineRewardProgressView OfflineRewards =>
            new OfflineRewardProgressView(offlineRewards ?? OfflineRewardProgressData.CreateDefault());
        public AttendanceProgressView Attendance =>
            (attendance ?? AttendanceProgressData.CreateDefault()).CreateView();
        public MailProgressView Mail =>
            (mail ?? MailProgressData.CreateDefault()).CreateView();
        public EquipmentSlotUpgradeView EquipmentSlotUpgrade =>
            (equipmentSlotUpgrade ?? EquipmentSlotUpgradeData.CreateDefault()).CreateView();
        public CommanderPotentialView CommanderPotential =>
            (commanderPotential ?? CommanderPotentialData.CreateDefault()).CreateView();
        public CommanderLegionGrowthView CommanderLegionGrowth =>
            (commanderLegionGrowth ?? CommanderLegionGrowthData.CreateDefault()).CreateView();
        public CommanderSkillProgressView CommanderSkills =>
            (commanderSkills ?? CommanderSkillProgressData.CreateDefault()).CreateView();

        public static GameProgressData CreateDefault()
        {
            return new GameProgressData();
        }

        public GameProgressData Clone(
            CommanderSkillBalanceConfig commanderSkillBalanceConfig = null,
            CommanderSkillSummonConfig commanderSkillSummonConfig = null)
        {
            return new GameProgressData // 변경 전 후보 복사본
            {
                currentChallengeStage = currentChallengeStage,
                lastClearedStage = lastClearedStage,
                expeditionMode = expeditionMode,
                gold = gold,
                foodRiotBestKills = foodRiotBestKills,
                guardiansTowerBestKills = guardiansTowerBestKills, // 08.06 안건준 추가
                guardiansTowerDifficultyLevel = guardiansTowerDifficultyLevel, // 08.07 안건준 추가
                castleRaidFirstClear = castleRaidFirstClear,
                commander = commander?.Clone() ?? CommanderProgressData.CreateDefault(),
                monsters = monsters?.Clone() ?? MonsterRosterData.CreateDefault(),
                mainBattleFormation = mainBattleFormation?.Clone() ?? MainBattleFormationData.CreateDefault(),
                ascensionCurrency = ascensionCurrency,
                gachaPity = gachaPity?.Clone() ?? GachaPityData.CreateDefault(),
                equipment = equipment?.Clone() ?? EquipmentSaveData.CreateDefault(), // 08.10 안건준 추가
                items = items?.Clone() ?? ItemInventoryData.CreateDefault(),
                growthDungeons = growthDungeons?.Clone() ?? GrowthDungeonProgressData.CreateDefault(),
                offlineRewards = offlineRewards?.Clone() ?? OfflineRewardProgressData.CreateDefault(),
                attendance = attendance?.Clone() ?? AttendanceProgressData.CreateDefault(),
                mail = mail?.Clone() ?? MailProgressData.CreateDefault(),
                coreBalanceMigrationCompleted = coreBalanceMigrationCompleted,
                equipmentSlotUpgrade = equipmentSlotUpgrade?.Clone() ?? EquipmentSlotUpgradeData.CreateDefault(),
                commanderPotential = commanderPotential?.Clone() ?? CommanderPotentialData.CreateDefault(),
                commanderLegionGrowth = commanderLegionGrowth?.Clone() ?? CommanderLegionGrowthData.CreateDefault(),
                commanderSkills = commanderSkills?.Clone(
                    commanderSkillBalanceConfig,
                    commanderSkillSummonConfig) ?? CommanderSkillProgressData.CreateDefault()
            };
        }

        internal bool TryApply(
            GameProgressChange change,
            CommanderGrowthConfig commanderGrowthConfig = null,
            ItemCatalog itemCatalog = null,
            EquipmentBalanceConfig equipmentBalanceConfig = null,
            CommanderSkillBalanceConfig commanderSkillBalanceConfig = null,
            CommanderSkillSummonConfig commanderSkillSummonConfig = null)
        {
            if (change == null)
            {
                return false;
            }

            if (change.HasStandaloneItemGrant &&
                (change.Rewards == null || change.Rewards.IsEmpty))
            {
                return false;
            }

            if (change.HasExpeditionMode && !IsValidExpeditionMode(change.ExpeditionMode))
            {
                return false;
            }

            if (change.MarkCastleRaidCleared && castleRaidFirstClear &&
                change.Rewards != null && !change.Rewards.IsEmpty)
            {
                return false; // 최초 보상 중복 지급 차단
            }

            if (change.HasGrowthDungeonDailyKeyRefresh &&
                !TryApplyGrowthDungeonDailyKeyRefresh(change, itemCatalog))
            {
                return false;
            }

            if (change.HasAttendanceRefresh && !TryApplyAttendanceRefresh(change))
            {
                return false;
            }

            if (change.HasAttendanceClaim &&
                !TryApplyAttendanceClaim(change, commanderGrowthConfig, itemCatalog))
            {
                return false;
            }

            if (change.HasAddMail && !TryApplyMailAdd(change))
            {
                return false;
            }

            if (change.HasCleanupExpiredMail && !TryApplyMailCleanup(change))
            {
                return false;
            }

            if (change.HasClaimMail && !TryApplyMailClaim(change, commanderGrowthConfig, itemCatalog))
            {
                return false;
            }

            if ((change.HasMarkOfflineInactive || change.HasSettleOfflineReward || change.HasAcknowledgeOfflineRewards) &&
                !TryApplyOfflineRewardProgress(change))
            {
                return false;
            }

            if (change.HasSetCommanderSkillAutoUse)
            {
                commanderSkills ??= CommanderSkillProgressData.CreateDefault();
                if (!commanderSkills.TrySetAutoUse(
                        change.ExpectedCommanderSkillAutoUse,
                        change.NewCommanderSkillAutoUse))
                {
                    return false;
                }
            }

            if (change.HasEquipCommanderSkill)
            {
                commanderSkills ??= CommanderSkillProgressData.CreateDefault();
                if (!commanderSkills.TryEquip(
                        change.CommanderSkillSlotIndex,
                        change.ExpectedCommanderSkillId,
                        change.NewCommanderSkillId,
                        commanderSkillBalanceConfig))
                {
                    return false;
                }
            }

            if (change.HasRecordCommanderSkillSummon)
            {
                commanderSkills ??= CommanderSkillProgressData.CreateDefault();
                var summonConfig = commanderSkillSummonConfig ?? CommanderSkillSummonConfig.RuntimeDefault;
                var summonedSkillIds = change.SummonedCommanderSkillIds;
                if (summonedSkillIds == null || summonedSkillIds.Count == 0)
                {
                    return false;
                }

                if (change.CommanderSkillSummonRequiresPayment)
                {
                    if (!summonConfig.TryGetOffer(
                            change.CommanderSkillSummonDrawCount,
                            out var offer) ||
                        offer.DrawCount != summonedSkillIds.Count)
                    {
                        return false;
                    }

                    var availableTickets = items.GetQuantity(summonConfig.TicketItemId);
                    var payment = summonConfig.CalculatePayment(offer, availableTickets);
                    var costs = new List<ItemAmount>(2);
                    if (payment.TicketCost > 0)
                    {
                        costs.Add(new ItemAmount(summonConfig.TicketItemId, payment.TicketCost));
                    }

                    if (payment.DiamondCost > 0L)
                    {
                        costs.Add(new ItemAmount(ItemIds.Diamond, payment.DiamondCost));
                    }

                    if (!ItemInventoryTransactions.TrySpend(
                            items,
                            costs,
                            itemCatalog,
                            out var spentCommanderSkillPayment))
                    {
                        return false;
                    }

                    items = spentCommanderSkillPayment; // 소환권·다이아·결과를 같은 후보에서만 반영
                }

                if (!commanderSkills.TryRecordSummons(
                        change.ExpectedCommanderSkillSummonCount,
                        summonedSkillIds,
                        commanderSkillBalanceConfig,
                        summonConfig))
                {
                    return false;
                }
            }

            if (change.HasLevelUpCommanderSkill)
            {
                commanderSkills ??= CommanderSkillProgressData.CreateDefault();
                if (!commanderSkills.TryLevelUp(
                        change.CommanderSkillToLevelUpId,
                        change.ExpectedCommanderSkillLevel,
                        change.ExpectedCommanderSkillDuplicateCount,
                        commanderSkillBalanceConfig))
                {
                    return false;
                }
            }

            if (change.ItemCosts != null && change.ItemCosts.Count > 0)
            {
                if (!ItemInventoryTransactions.TrySpend(items, change.ItemCosts, itemCatalog, out var spentItems))
                {
                    return false;
                }

                items = spentItems; // 보상과 같은 후보 데이터에서 먼저 비용 차감
            }

            if (change.HasExpeditionMode)
            {
                expeditionMode = change.ExpeditionMode;
            }

            if (change.HasExpeditionFirstClear && !TryApplyExpeditionFirstClear(change))
            {
                return false;
            }

            if (change.HasExpeditionRepeatClear && !TryApplyExpeditionRepeatClear(change))
            {
                return false;
            }

            if (!TryApplyRewards(change.Rewards, commanderGrowthConfig, itemCatalog))
            {
                return false;
            }

            if (change.HasSettleOfflineReward &&
                !TryApplyOfflineEquipmentSettlement(change.OfflineReceipt))
            {
                return false;
            }

            if (change.FoodRiotBestKills >= 0)
            {
                foodRiotBestKills = Math.Max(foodRiotBestKills, change.FoodRiotBestKills);
            }

            if (change.GuardiansTowerBestKills >= 0) // 08.06 안건준 추가
            {
                guardiansTowerBestKills = Math.Max(guardiansTowerBestKills, change.GuardiansTowerBestKills);
            }

            if (change.HasGrowthDungeonClear)
            {
                growthDungeons ??= GrowthDungeonProgressData.CreateDefault();
                if (!growthDungeons.RecordClear(change.GrowthDungeonContentId, change.GrowthDungeonClearedStage))
                {
                    return false;
                }
            }

            if (change.IncrementGuardiansTowerDifficulty) // 08.07 안건준 추가 - 클리어할 때마다 난이도 1 증가
            {
                guardiansTowerDifficultyLevel = Math.Max(0, guardiansTowerDifficultyLevel + 1);
            }

            if (change.MarkCastleRaidCleared)
            {
                castleRaidFirstClear = true;
            }

            if (change.CommanderExperience > 0L)
            {
                commander ??= CommanderProgressData.CreateDefault();
                commander.GrantExperience(change.CommanderExperience, commanderGrowthConfig);
            }

            if (change.HasLevelUpCommander)
            {
                commander ??= CommanderProgressData.CreateDefault();
                if (!commander.TryLevelUp(change.ExpectedCommanderLevel, commanderGrowthConfig))
                {
                    return false;
                }

                commanderLegionGrowth ??= CommanderLegionGrowthData.CreateDefault();
                commanderLegionGrowth.GrantTrainingPoints(1); // 저장된 수동 레벨업 1회당 1포인트
            }

            if (change.HasUpgradeCommanderLegionStat)
            {
                var growthConfig = commanderGrowthConfig ?? CommanderGrowthConfig.RuntimeDefault;
                commanderLegionGrowth ??= CommanderLegionGrowthData.CreateDefault();
                var nextGrowth = commanderLegionGrowth.Clone();
                var currentLevel = nextGrowth.GetLevel(change.CommanderLegionStatToUpgrade);
                var maxLevel = growthConfig.GetLegionGrowthMaxLevel(change.CommanderLegionStatToUpgrade);
                if (currentLevel != change.ExpectedCommanderLegionStatLevel || currentLevel >= maxLevel)
                {
                    return false;
                }

                var nextItems = items;
                if (growthConfig.UsesGoldForLegionGrowth(change.CommanderLegionStatToUpgrade))
                {
                    var goldCost = growthConfig.GetLegionGrowthGoldCost(
                        change.CommanderLegionStatToUpgrade,
                        currentLevel);
                    if (!ItemInventoryTransactions.TrySpend(
                            items,
                            new[] { new ItemAmount(ItemIds.Gold, goldCost) },
                            itemCatalog,
                            out nextItems))
                    {
                        return false;
                    }
                }
                else if (!nextGrowth.TrySpendTrainingPoints(
                             growthConfig.GetLegionGrowthTrainingPointCost(
                                 change.CommanderLegionStatToUpgrade,
                                 currentLevel)))
                {
                    return false;
                }

                if (!nextGrowth.TryLevelUp(
                        change.CommanderLegionStatToUpgrade,
                        change.ExpectedCommanderLegionStatLevel,
                        maxLevel))
                {
                    return false;
                }

                items = nextItems;
                commanderLegionGrowth = nextGrowth;
            }

            if (change.HasAcquireMonster &&
                (string.IsNullOrEmpty(change.AcquireMonsterId) ||
                 !monsters.TryAcquire(change.AcquireMonsterId)))
            {
                return false;
            }

            if (change.HasFormationChange)
            {
                if (string.IsNullOrEmpty(change.FormationMonsterId) ||
                    (!change.RemoveFromFormation &&
                     change.TargetParty != MonsterPartyKind.Main &&
                     change.TargetParty != MonsterPartyKind.Reserve))
                {
                    return false;
                }

                var formationChanged = change.RemoveFromFormation
                    ? monsters.TryUnassign(change.FormationMonsterId)
                    : monsters.TryAssign(change.FormationMonsterId, change.TargetParty);
                if (!formationChanged)
                {
                    return false;
                }
            }

            if (change.HasMainBattleFormation &&
                (change.MainBattleFormationOffsets == null ||
                 !mainBattleFormation.TrySet(change.MainBattleFormationOffsets)))
            {
                return false;
            }

            if (change.HasLevelUpMonster)
            {
                if (string.IsNullOrEmpty(change.LevelUpMonsterId) ||
                    !monsters.TryGetOwned(change.LevelUpMonsterId, out var owned) ||
                    owned.Level != change.ExpectedMonsterLevel ||
                    !MonsterLevelRules.TryGetNextLevelCost(owned.Level, out var cost) ||
                    !ItemInventoryTransactions.TrySpend(
                        items,
                        new[] { new ItemAmount(ItemIds.Gold, cost) },
                        itemCatalog,
                        out var spentItems) ||
                    !monsters.TryLevelUp(change.LevelUpMonsterId, change.ExpectedMonsterLevel))
                {
                    return false;
                }

                items = spentItems; // 레벨 증가와 같은 후보 데이터에서 차감
            }

            if (change.HasAscendMonster &&
                (string.IsNullOrEmpty(change.AscendMonsterId) ||
                 !monsters.TryAscend(change.AscendMonsterId, change.ExpectedAscensionLevel)))
            {
                return false;
            }

            if (change.GachaPulls != null)
            {
                if (change.GachaPulls.Count == 0)
                {
                    return false;
                }

                for (var index = 0; index < change.GachaPulls.Count; index++)
                {
                    var pull = change.GachaPulls[index];
                    if (!TryApplyGachaPull(pull.MonsterId, pull.Rarity))
                    {
                        return false;
                    }
                }
            }
            else if (change.HasGachaPull &&
                     !TryApplyGachaPull(change.GachaPullMonsterId, change.GachaPullRarity))
            {
                return false;
            }

            // 08.10 안건준 추가 - 장비 획득/장착/해제
            if (change.HasAcquireEquipment)
            {
                equipment ??= EquipmentSaveData.CreateDefault();
                if (!equipment.TryAcquire(change.AcquireEquipmentInstances))
                {
                    return false;
                }
            }

            if (change.HasEquipItem &&
                (string.IsNullOrEmpty(change.EquipItemInstanceId) ||
                 !(equipment ??= EquipmentSaveData.CreateDefault()).TryEquip(change.EquipItemInstanceId)))
            {
                return false;
            }

            if (change.HasUnequipItem &&
                !(equipment ??= EquipmentSaveData.CreateDefault()).TryUnequip(change.UnequipItemPart))
            {
                return false;
            }

            if (change.HasSetEquipmentLock &&
                (string.IsNullOrEmpty(change.EquipmentLockInstanceId) ||
                 !(equipment ??= EquipmentSaveData.CreateDefault()).TrySetLocked(
                     change.EquipmentLockInstanceId,
                     change.ExpectedEquipmentLockValue,
                     change.EquipmentLockValue)))
            {
                return false;
            }

            if (change.HasDismantleEquipment)
            {
                equipment ??= EquipmentSaveData.CreateDefault();
                if (!equipment.TryDismantle(change.DismantleEquipmentInstanceIds, out var upgradeStoneAmount) ||
                    !TryApplyRewards(
                        RewardBundle.FromItems(new ItemAmount(ItemIds.EquipmentSlotUpgradeStone, upgradeStoneAmount)),
                        commanderGrowthConfig,
                        itemCatalog))
                {
                    return false;
                }
            }

            if (change.HasDiscardItem)
            {
                if (!ItemInventoryTransactions.TryDiscard(
                        items,
                        itemCatalog,
                        change.ItemId,
                        change.ItemQuantity,
                        change.ExpectedItemQuantity,
                        out var discardedItems))
                {
                    return false;
                }

                items = discardedItems;
            }

            if (change.HasUseItem)
            {
                if (!ItemInventoryTransactions.TryUse(
                        items,
                        itemCatalog,
                        change.ItemId,
                        change.ItemQuantity,
                        change.ExpectedItemQuantity,
                        out var consumedItems,
                        out var useResult))
                {
                    return false;
                }

                items = consumedItems;
                if (!TryApplyRewards(useResult.Rewards, commanderGrowthConfig, itemCatalog))
                {
                    return false;
                }
            }

            // 장비 부위 슬롯 영구 강화(+1): 비용 확인 → 재화 차감 → 레벨 증가 순서로 처리한다.
            if (change.HasUpgradeEquipmentSlot)
            {
                equipmentSlotUpgrade ??= EquipmentSlotUpgradeData.CreateDefault();
                var currentLevel = equipmentSlotUpgrade.GetLevel(change.UpgradeEquipmentSlotPart);
                if (currentLevel != change.ExpectedEquipmentSlotLevel)
                {
                    return false; // 요청 시점과 처리 시점 사이에 레벨이 달라짐(중복 클릭 등)
                }

                var goldCost = EquipmentSlotUpgradeCostRules.GetNextGoldCost(currentLevel);
                var stoneCost = EquipmentSlotUpgradeCostRules.GetNextStoneCost(currentLevel);
                var costs = new[]
                {
                    new ItemAmount(ItemIds.Gold, goldCost),
                    new ItemAmount(ItemIds.EquipmentSlotUpgradeStone, stoneCost)
                };

                if (!ItemInventoryTransactions.TrySpend(items, costs, itemCatalog, out var slotUpgradeItems))
                {
                    return false; // 재화 부족
                }

                if (!equipmentSlotUpgrade.TryLevelUp(change.UpgradeEquipmentSlotPart, change.ExpectedEquipmentSlotLevel))
                {
                    return false;
                }

                items = slotUpgradeItems;
            }

            // 잠재능력 슬롯 최초 배정. 이미 값이 있으면 실패해 중복 배정을 막는다.
            if (change.HasAssignCommanderPotentialSlot)
            {
                commanderPotential ??= CommanderPotentialData.CreateDefault();
                if (!commanderPotential.TryAssignSlot(
                        change.CommanderPotentialSlotIndex,
                        change.CommanderPotentialOptionType,
                        change.CommanderPotentialGrade,
                        change.CommanderPotentialValue))
                {
                    return false;
                }
            }

            // "잠재 능력 변경": 강화석 1개 차감 후 잠기지 않은 대상 슬롯들을 새 옵션으로 교체.
            // 해금됐지만 아직 비어있는("대기 중") 슬롯도 이 버튼으로 함께 처음 배정된다(값 있는 슬롯은 교체, 빈 슬롯은 신규 배정).
            if (change.HasRerollCommanderPotentialSlots)
            {
                var entries = change.CommanderPotentialRerollEntries;
                if (entries == null || entries.Count == 0)
                {
                    return false;
                }

                commanderPotential ??= CommanderPotentialData.CreateDefault();

                // 유효 슬롯·잠금·중복 대상·같은 옵션 최대 2개를 차감 전에 한 번에 검증한다.
                if (!commanderPotential.CanApplyOptionReroll(entries, 2))
                {
                    return false;
                }

                if (!ItemInventoryTransactions.TrySpend(
                        items,
                        new[] { new ItemAmount(ItemIds.LegionPotentialUpgradeStone, 1) },
                        itemCatalog,
                        out var rerollItems))
                {
                    return false; // 잠재능력 강화석 부족
                }

                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var target = commanderPotential.GetSlot(entry.SlotIndex);
                    if (target != null && target.HasValue)
                    {
                        commanderPotential.TryReplaceSlot(entry.SlotIndex, entry.OptionType, entry.Grade, entry.Value);
                    }
                    else
                    {
                        commanderPotential.TryAssignSlot(entry.SlotIndex, entry.OptionType, entry.Grade, entry.Value);
                    }
                }

                // "잠재 능력 변경" 1회 성공마다 "수호자의 힘" 경험치 +10, 100 채우면 다음 단계로 승급.
                commanderPotential.AddExperience(CommanderPotentialData.ExperiencePerReroll);

                items = rerollItems;
            }

            // "옵션 스탯 변경": 강화석 1개 차감 후 옵션 종류·등급은 그대로 두고 수치만 교체.
            // 잠금은 옵션 자체가 바뀌는 "잠재 능력 변경"만 막는 용도라, 여기서는 잠긴 슬롯도 대상이 된다(값만 있으면 됨).
            if (change.HasRerollCommanderPotentialValues)
            {
                var valueEntries = change.CommanderPotentialValueRerollEntries;
                if (valueEntries == null || valueEntries.Count == 0)
                {
                    return false;
                }

                commanderPotential ??= CommanderPotentialData.CreateDefault();

                if (!commanderPotential.CanApplyValueReroll(
                        valueEntries,
                        equipmentBalanceConfig ?? EquipmentBalanceConfig.RuntimeDefault))
                {
                    return false;
                }

                if (!ItemInventoryTransactions.TrySpend(
                        items,
                        new[] { new ItemAmount(ItemIds.LegionPotentialUpgradeStone, 1) },
                        itemCatalog,
                        out var valueRerollItems))
                {
                    return false; // 잠재능력 강화석 부족
                }

                for (var i = 0; i < valueEntries.Count; i++)
                {
                    var entry = valueEntries[i];
                    if (!commanderPotential.TryRerollSlotValue(entry.SlotIndex, entry.Value))
                    {
                        return false;
                    }
                }

                // "옵션 스탯 변경" 1회 성공마다 "수호자의 힘" 경험치 +10.
                commanderPotential.AddExperience(CommanderPotentialData.ExperiencePerReroll);

                items = valueRerollItems;
            }

            // 자물쇠 아이콘 클릭으로 재추첨 대상 제외 여부를 토글. 잠그는 경우에만 강화석을
            // 소모하며(해제는 무료), 이미 잠긴 슬롯 수에 따라 비용이 1→2→4→8→16개로 2배씩 늘어난다.
            if (change.HasSetCommanderPotentialLocked)
            {
                commanderPotential ??= CommanderPotentialData.CreateDefault();

                var lockTarget = commanderPotential.GetSlot(change.CommanderPotentialLockSlotIndex);
                if (lockTarget == null || !lockTarget.HasValue ||
                    lockTarget.Locked != change.ExpectedCommanderPotentialLocked)
                {
                    return false;
                }

                if (change.NewCommanderPotentialLocked)
                {
                    var alreadyLockedCount = commanderPotential.CountLockedSlots();
                    var lockCost = CommanderPotentialData.GetLockStoneCost(alreadyLockedCount);
                    if (!ItemInventoryTransactions.TrySpend(
                            items,
                            new[] { new ItemAmount(ItemIds.LegionPotentialUpgradeStone, lockCost) },
                            itemCatalog,
                            out var lockItems))
                    {
                        return false; // 잠재능력 강화석 부족
                    }

                    items = lockItems;
                }

                commanderPotential.TrySetSlotLocked(
                    change.CommanderPotentialLockSlotIndex,
                    change.ExpectedCommanderPotentialLocked,
                    change.NewCommanderPotentialLocked);
            }

            Repair(
                commanderGrowthConfig,
                commanderSkillBalanceConfig,
                commanderSkillSummonConfig); // 변경 후 불변식 재확인
            return true;
        }

        private bool TryApplyGrowthDungeonDailyKeyRefresh(
            GameProgressChange change,
            ItemCatalog itemCatalog)
        {
            if (itemCatalog == null || change.GrowthDungeonDailyKeyTargets == null ||
                change.GrowthDungeonDailyKeyTargets.Count != GrowthDungeonDailyKeyRules.KeyItemIds.Count)
            {
                return false;
            }

            growthDungeons ??= GrowthDungeonProgressData.CreateDefault();
            var candidateItems = (items ?? ItemInventoryData.CreateDefault()).Clone();
            for (var index = 0; index < change.GrowthDungeonDailyKeyTargets.Count; index++)
            {
                var target = change.GrowthDungeonDailyKeyTargets[index];
                if (!target.IsValid || target.Amount > GrowthDungeonDailyKeyRules.MaximumQuantity ||
                    !string.Equals(
                        target.ItemId,
                        GrowthDungeonDailyKeyRules.KeyItemIds[index],
                        StringComparison.OrdinalIgnoreCase) ||
                    !candidateItems.TrySetQuantity(target.ItemId, target.Amount, itemCatalog))
                {
                    return false;
                }
            }

            if (!growthDungeons.TryAdvanceDailyKeyPeriod(
                    change.ExpectedGrowthDungeonDailyKeyPeriod,
                    change.GrowthDungeonDailyKeyPeriod))
            {
                return false;
            }

            items = candidateItems;
            itemViewCache = null;
            return true;
        }

        private bool TryApplyAttendanceRefresh(GameProgressChange change)
        {
            attendance ??= AttendanceProgressData.CreateDefault();
            var candidate = attendance.Clone();
            if (!candidate.TryRefresh(change.ExpectedAttendancePeriod, change.AttendancePeriod))
            {
                return false;
            }

            attendance = candidate;
            return true;
        }

        private bool TryApplyAttendanceClaim(
            GameProgressChange change,
            CommanderGrowthConfig commanderGrowthConfig,
            ItemCatalog itemCatalog)
        {
            if (change.AttendanceReward == null || change.AttendanceReward.IsEmpty)
            {
                return false;
            }

            attendance ??= AttendanceProgressData.CreateDefault();
            var candidate = attendance.Clone();
            if (!candidate.TryClaim(change.ExpectedAttendanceDay, change.ExpectedAttendanceClaimPeriod) ||
                !TryApplyRewards(change.AttendanceReward, commanderGrowthConfig, itemCatalog))
            {
                return false;
            }

            attendance = candidate;
            return true;
        }

        private bool TryApplyMailAdd(GameProgressChange change)
        {
            mail ??= MailProgressData.CreateDefault();
            var candidate = mail.Clone();
            if (!candidate.TryAdd(change.MailToAdd))
            {
                return false;
            }

            mail = candidate;
            return true;
        }

        private bool TryApplyMailCleanup(GameProgressChange change)
        {
            mail ??= MailProgressData.CreateDefault();
            var candidate = mail.Clone();
            if (candidate.RemoveExpired(change.MailOperationUtc) <= 0)
            {
                return false;
            }

            mail = candidate;
            return true;
        }

        private bool TryApplyMailClaim(
            GameProgressChange change,
            CommanderGrowthConfig commanderGrowthConfig,
            ItemCatalog itemCatalog)
        {
            mail ??= MailProgressData.CreateDefault();
            var candidate = mail.Clone();
            if (!candidate.TryCreateClaim(
                    change.MailClaimIds,
                    change.MailOperationUtc,
                    out var rewards,
                    out var normalizedIds) ||
                !TryApplyRewards(rewards, commanderGrowthConfig, itemCatalog) ||
                !candidate.TryRemoveClaimed(normalizedIds))
            {
                return false;
            }

            mail = candidate;
            return true;
        }

        private bool TryApplyRewards(
            RewardBundle rewards,
            CommanderGrowthConfig commanderGrowthConfig,
            ItemCatalog itemCatalog)
        {
            if (rewards == null || rewards.IsEmpty)
            {
                return true;
            }

            var candidateItems = items ?? ItemInventoryData.CreateDefault();
            if (rewards.Gold > 0L)
            {
                if (!ItemInventoryTransactions.TryGrantCoreBalance(
                        candidateItems,
                        ItemIds.Gold,
                        rewards.Gold,
                        out var withGold))
                {
                    return false;
                }

                candidateItems = withGold;
            }

            if (rewards.Items.Count > 0)
            {
                if (!ItemInventoryTransactions.TryGrant(
                        candidateItems,
                        rewards.Items,
                        itemCatalog,
                        out var withItems))
                {
                    return false;
                }

                candidateItems = withItems;
            }

            commander ??= CommanderProgressData.CreateDefault();
            commander.GrantExperience(rewards.CommanderExperience, commanderGrowthConfig);
            items = candidateItems;

            return true;
        }

        private bool TryApplyOfflineRewardProgress(GameProgressChange change)
        {
            var operationCount = (change.HasMarkOfflineInactive ? 1 : 0) +
                                 (change.HasSettleOfflineReward ? 1 : 0) +
                                 (change.HasAcknowledgeOfflineRewards ? 1 : 0);
            if (operationCount != 1)
            {
                return false;
            }

            offlineRewards ??= OfflineRewardProgressData.CreateDefault();
            if (change.HasMarkOfflineInactive)
            {
                return (change.Rewards == null || change.Rewards.IsEmpty) &&
                       offlineRewards.TryMarkInactive(
                           change.ExpectedOfflineLastActiveUtc,
                           change.OfflineNextActiveUtc,
                           change.OfflineNextActiveStage);
            }

            if (change.HasAcknowledgeOfflineRewards)
            {
                return (change.Rewards == null || change.Rewards.IsEmpty) &&
                       offlineRewards.TryAcknowledge(change.OfflineReceiptIds);
            }

            return MatchesOfflineReceipt(change.OfflineReceipt, change.Rewards) &&
                   offlineRewards.TrySettle(
                       change.ExpectedOfflineLastActiveUtc,
                       change.OfflineNextActiveUtc,
                       change.OfflineNextActiveStage,
                       change.OfflineReceipt);
        }

        private static bool MatchesOfflineReceipt(
            OfflineRewardReceiptData receipt,
            RewardBundle rewards)
        {
            if (receipt == null || !receipt.IsValid || rewards == null || rewards.IsEmpty ||
                receipt.Gold != rewards.Gold ||
                receipt.CommanderExperience != rewards.CommanderExperience)
            {
                return false;
            }

            var expected = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            AddExpectedItem(expected, ItemIds.EquipmentSlotUpgradeStone, receipt.TotalEquipmentSlotUpgradeStone);
            AddExpectedItem(expected, ItemIds.CommanderSkillUpgradeStone, receipt.CommanderSkillUpgradeStone);
            AddExpectedItem(expected, ItemIds.LegionPotentialUpgradeStone, receipt.LegionPotentialUpgradeStone);
            if (rewards.Items.Count != expected.Count)
            {
                return false;
            }

            var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < rewards.Items.Count; index++)
            {
                var item = rewards.Items[index];
                if (!item.IsValid || !matched.Add(item.ItemId) ||
                    !expected.TryGetValue(item.ItemId, out var amount) || amount != item.Amount)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryApplyOfflineEquipmentSettlement(OfflineRewardReceiptData receipt)
        {
            if (receipt == null || !receipt.IsValid)
            {
                return false;
            }

            equipment ??= EquipmentSaveData.CreateDefault();
            if (receipt.ExistingAutoDismantleInstanceIds.Count > 0)
            {
                if (!equipment.TryDismantle(
                        receipt.ExistingAutoDismantleInstanceIds,
                        out var existingDismantleStones) ||
                    existingDismantleStones != receipt.ExistingAutoDismantleUpgradeStone)
                {
                    return false;
                }
            }
            else if (receipt.ExistingAutoDismantleUpgradeStone != 0L)
            {
                return false;
            }

            return receipt.EquipmentRewards.Count == 0 ||
                   equipment.TryAcquire(receipt.EquipmentRewards);
        }

        private static void AddExpectedItem(
            IDictionary<string, long> expected,
            string itemId,
            long amount)
        {
            if (amount > 0L)
            {
                expected.Add(itemId, amount);
            }
        }

        private bool TryApplyExpeditionFirstClear(GameProgressChange change)
        {
            if (expeditionMode != ExpeditionRunMode.Challenge ||
                change.ExpeditionFirstClearStage != currentChallengeStage ||
                change.Rewards == null || change.Rewards.IsEmpty)
            {
                return false;
            }

            lastClearedStage = Math.Max(lastClearedStage, change.ExpeditionFirstClearStage);
            currentChallengeStage = Math.Max(currentChallengeStage, change.ExpeditionFirstClearStage + 1);
            return true;
        }

        private bool TryApplyExpeditionRepeatClear(GameProgressChange change)
        {
            return expeditionMode == ExpeditionRunMode.Repeat &&
                   lastClearedStage > 0 &&
                   change.ExpeditionRepeatClearStage == lastClearedStage &&
                   change.Rewards != null &&
                   !change.Rewards.IsEmpty;
        }

        // 뽑기 한 번의 결과를 반영한다: 천장 카운터 갱신 + (신규 획득 / 중복 재료 / 전용 재화) 중 하나.
        private bool TryApplyGachaPull(string monsterId, MonsterRarity rarity)
        {
            if (string.IsNullOrEmpty(monsterId))
            {
                return false;
            }

            gachaPity ??= GachaPityData.CreateDefault();
            gachaPity.RegisterPull(rarity);

            if (!monsters.TryGetOwned(monsterId, out var existingOwned))
            {
                return monsters.TryAcquire(monsterId); // 최초 획득 = 0돌파
            }

            if (!MonsterAscension.IsMaxAscension(existingOwned.AscensionLevel) &&
                monsters.TryAddAscensionMaterial(monsterId))
            {
                return true; // 중복 획득은 수동 돌파에 쓸 재료로 보관
            }

            // 이미 최대 돌파이거나 최대 돌파까지 필요한 재료를 모두 보유한 뒤의 초과 중복이다.
            if (!ItemInventoryTransactions.TryGrantCoreBalance(
                    items,
                    ItemIds.AscensionStone,
                    1L,
                    out var ascensionItems))
            {
                return false;
            }

            items = ascensionItems;
            return true;
        }

        internal void Repair(
            CommanderSkillBalanceConfig commanderSkillBalanceConfig = null,
            CommanderSkillSummonConfig commanderSkillSummonConfig = null)
        {
            currentChallengeStage = Math.Max(1, currentChallengeStage);
            lastClearedStage = Math.Max(0, Math.Min(lastClearedStage, currentChallengeStage - 1));
            gold = Math.Max(0L, gold);
            foodRiotBestKills = Math.Max(0, foodRiotBestKills);
            guardiansTowerBestKills = Math.Max(0, guardiansTowerBestKills); // 08.06 안건준 추가
            guardiansTowerDifficultyLevel = Math.Max(0, guardiansTowerDifficultyLevel); // 08.07 안건준 추가
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

            if (!IsValidExpeditionMode(expeditionMode) ||
                (lastClearedStage == 0 && expeditionMode == ExpeditionRunMode.Repeat))
            {
                expeditionMode = ExpeditionRunMode.Challenge; // 손상값·클리어 전 반복 복구
            }
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

            if (sourceDataVersion <= 16)
            {
                monsters ??= MonsterRosterData.CreateDefault();
                monsters.MigrateRetiredMonsterIds(); // 두부·스파이크 보유 진행과 편성을 현재 정식 ID로 보존
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

            Repair();
        }

        private static bool IsValidExpeditionMode(ExpeditionRunMode mode)
        {
            return mode == ExpeditionRunMode.Challenge || mode == ExpeditionRunMode.Repeat;
        }
    }

    public readonly struct GameProgressView // UI·전투용 읽기 전용 복사값
    {
        public GameProgressView(GameProgressData data)
        {
            CurrentChallengeStage = data.CurrentChallengeStage;
            LastClearedStage = data.LastClearedStage;
            ExpeditionMode = data.ExpeditionMode;
            Gold = data.Gold;
            Diamond = data.Diamond;
            FoodRiotBestKills = data.FoodRiotBestKills;
            GuardiansTowerBestKills = data.GuardiansTowerBestKills; // 08.06 안건준 추가
            GuardiansTowerDifficultyLevel = data.GuardiansTowerDifficultyLevel; // 08.07 안건준 추가
            CastleRaidFirstClear = data.CastleRaidFirstClear;
            Commander = data.Commander;
            Monsters = data.Monsters;
            MainBattleFormation = data.MainBattleFormation;
            AscensionCurrency = data.AscensionCurrency;
            GachaPity = data.GachaPity;
            Equipment = data.Equipment; // 08.10 안건준 추가
            Items = data.Items;
            GrowthDungeons = data.GrowthDungeons;
            OfflineRewards = data.OfflineRewards;
            Attendance = data.Attendance;
            Mail = data.Mail;
            EquipmentSlotUpgrade = data.EquipmentSlotUpgrade;
            CommanderPotential = data.CommanderPotential;
            CommanderLegionGrowth = data.CommanderLegionGrowth;
            CommanderSkills = data.CommanderSkills;
        }

        public int CurrentChallengeStage { get; }
        public int LastClearedStage { get; }
        public ExpeditionRunMode ExpeditionMode { get; }
        public long Gold { get; }
        public long Diamond { get; }
        public int FoodRiotBestKills { get; }
        public int GuardiansTowerBestKills { get; } // 08.06 안건준 추가
        public int GuardiansTowerDifficultyLevel { get; } // 08.07 안건준 추가
        public bool CastleRaidFirstClear { get; }
        public CommanderProgressView Commander { get; }
        public MonsterRosterView Monsters { get; }
        public MainBattleFormationView MainBattleFormation { get; }
        public long AscensionCurrency { get; } // 돌파석 보유량
        public GachaPityView GachaPity { get; } // 뽑기 천장 누적 카운터
        public EquipmentSaveDataView Equipment { get; } // 08.10 안건준 추가 - 장비 보유·장착 상태
        public ItemInventoryView Items { get; } // 일반 아이템 보유 수량
        public GrowthDungeonProgressView GrowthDungeons { get; } // 성장 던전 단계·열쇠 기준일
        public OfflineRewardProgressView OfflineRewards { get; } // 방치 시작점·확인 대기 정산
        public AttendanceProgressView Attendance { get; } // 28일 누적 출석
        public MailProgressView Mail { get; } // 미수령 우편
        public EquipmentSlotUpgradeView EquipmentSlotUpgrade { get; } // 부위 슬롯 영구 강화 레벨
        public CommanderPotentialView CommanderPotential { get; } // 군단장 잠재능력 5슬롯
        public CommanderLegionGrowthView CommanderLegionGrowth { get; } // 군단 공용 6종 강화
        public CommanderSkillProgressView CommanderSkills { get; } // 군단장 스킬 장착·자동사용
    }

    public sealed class GameProgressChange // 한 번에 검증할 진행 변경 묶음
    {
        private GameProgressChange()
        {
            FoodRiotBestKills = -1; // 최고기록 미변경 표식
            GuardiansTowerBestKills = -1; // 08.06 안건준 추가 - 최고기록 미변경 표식
        }

        internal bool HasExpeditionMode { get; private set; }
        internal ExpeditionRunMode ExpeditionMode { get; private set; }
        internal bool HasExpeditionFirstClear { get; private set; }
        internal int ExpeditionFirstClearStage { get; private set; }
        internal bool HasExpeditionRepeatClear { get; private set; }
        internal int ExpeditionRepeatClearStage { get; private set; }
        internal RewardBundle Rewards { get; private set; }
        internal IReadOnlyList<ItemAmount> ItemCosts { get; private set; }
        internal bool HasGrowthDungeonClear { get; private set; }
        internal string GrowthDungeonContentId { get; private set; }
        internal int GrowthDungeonClearedStage { get; private set; }
        internal bool HasGrowthDungeonDailyKeyRefresh { get; private set; }
        internal long ExpectedGrowthDungeonDailyKeyPeriod { get; private set; }
        internal long GrowthDungeonDailyKeyPeriod { get; private set; }
        internal IReadOnlyList<ItemAmount> GrowthDungeonDailyKeyTargets { get; private set; }
        internal bool HasAttendanceRefresh { get; private set; }
        internal long ExpectedAttendancePeriod { get; private set; }
        internal long AttendancePeriod { get; private set; }
        internal bool HasAttendanceClaim { get; private set; }
        internal int ExpectedAttendanceDay { get; private set; }
        internal long ExpectedAttendanceClaimPeriod { get; private set; }
        internal RewardBundle AttendanceReward { get; private set; }
        internal bool HasAddMail { get; private set; }
        internal MailEntryData MailToAdd { get; private set; }
        internal bool HasCleanupExpiredMail { get; private set; }
        internal bool HasClaimMail { get; private set; }
        internal IReadOnlyList<string> MailClaimIds { get; private set; }
        internal DateTime MailOperationUtc { get; private set; }
        internal int FoodRiotBestKills { get; private set; }
        internal int GuardiansTowerBestKills { get; private set; } // 08.06 안건준 추가
        internal bool IncrementGuardiansTowerDifficulty { get; private set; } // 08.07 안건준 추가
        internal bool MarkCastleRaidCleared { get; private set; }
        internal bool HasAcquireMonster { get; private set; }
        internal string AcquireMonsterId { get; private set; }
        internal bool HasFormationChange { get; private set; }
        internal string FormationMonsterId { get; private set; }
        internal MonsterPartyKind TargetParty { get; private set; }
        internal bool RemoveFromFormation { get; private set; }
        internal bool HasMainBattleFormation { get; private set; }
        internal Vector2[] MainBattleFormationOffsets { get; private set; }
        internal bool HasLevelUpMonster { get; private set; }
        internal string LevelUpMonsterId { get; private set; }
        internal int ExpectedMonsterLevel { get; private set; }
        internal bool HasAscendMonster { get; private set; }
        internal string AscendMonsterId { get; private set; }
        internal int ExpectedAscensionLevel { get; private set; }
        internal bool HasGachaPull { get; private set; }
        internal string GachaPullMonsterId { get; private set; }
        internal MonsterRarity GachaPullRarity { get; private set; }
        internal IReadOnlyList<GachaPullRecord> GachaPulls { get; private set; }
        internal bool HasAcquireEquipment { get; private set; } // 08.10 안건준 추가
        internal List<EquipmentInstanceData> AcquireEquipmentInstances { get; private set; }
        internal bool HasEquipItem { get; private set; }
        internal string EquipItemInstanceId { get; private set; }
        internal bool HasUnequipItem { get; private set; }
        internal EquipmentPart UnequipItemPart { get; private set; }
        internal bool HasSetEquipmentLock { get; private set; }
        internal string EquipmentLockInstanceId { get; private set; }
        internal bool ExpectedEquipmentLockValue { get; private set; }
        internal bool EquipmentLockValue { get; private set; }
        internal bool HasDismantleEquipment { get; private set; }
        internal IReadOnlyList<string> DismantleEquipmentInstanceIds { get; private set; }
        internal bool HasStandaloneItemGrant { get; private set; }
        internal bool HasDiscardItem { get; private set; }
        internal bool HasUseItem { get; private set; }
        internal string ItemId { get; private set; }
        internal long ItemQuantity { get; private set; }
        internal long ExpectedItemQuantity { get; private set; }
        internal long CommanderExperience { get; private set; }
        internal bool HasLevelUpCommander { get; private set; }
        internal int ExpectedCommanderLevel { get; private set; }
        internal bool HasUpgradeCommanderLegionStat { get; private set; }
        internal CommanderLegionStat CommanderLegionStatToUpgrade { get; private set; }
        internal int ExpectedCommanderLegionStatLevel { get; private set; }
        internal bool HasUpgradeEquipmentSlot { get; private set; }
        internal EquipmentPart UpgradeEquipmentSlotPart { get; private set; }
        internal int ExpectedEquipmentSlotLevel { get; private set; }
        internal bool HasAssignCommanderPotentialSlot { get; private set; }
        internal int CommanderPotentialSlotIndex { get; private set; }
        internal EquipmentOptionType CommanderPotentialOptionType { get; private set; }
        internal EquipmentGrade CommanderPotentialGrade { get; private set; }
        internal float CommanderPotentialValue { get; private set; }
        internal bool HasRerollCommanderPotentialSlots { get; private set; } // "잠재 능력 변경"
        internal IReadOnlyList<CommanderPotentialRerollEntry> CommanderPotentialRerollEntries { get; private set; }
        internal bool HasRerollCommanderPotentialValues { get; private set; } // "옵션 스탯 변경"(잠금 무시)
        internal IReadOnlyList<CommanderPotentialRerollEntry> CommanderPotentialValueRerollEntries { get; private set; }
        internal bool HasSetCommanderPotentialLocked { get; private set; } // 자물쇠 아이콘 토글
        internal int CommanderPotentialLockSlotIndex { get; private set; }
        internal bool ExpectedCommanderPotentialLocked { get; private set; }
        internal bool NewCommanderPotentialLocked { get; private set; }
        internal bool HasMarkOfflineInactive { get; private set; }
        internal bool HasSettleOfflineReward { get; private set; }
        internal bool HasAcknowledgeOfflineRewards { get; private set; }
        internal string ExpectedOfflineLastActiveUtc { get; private set; }
        internal DateTime OfflineNextActiveUtc { get; private set; }
        internal int OfflineNextActiveStage { get; private set; }
        internal OfflineRewardReceiptData OfflineReceipt { get; private set; }
        internal IReadOnlyList<string> OfflineReceiptIds { get; private set; }
        internal bool SuppressChangedNotification => HasMarkOfflineInactive; // Pause·Quit 저장은 파괴 중 UI를 갱신하지 않음
        internal bool HasSetCommanderSkillAutoUse { get; private set; }
        internal bool ExpectedCommanderSkillAutoUse { get; private set; }
        internal bool NewCommanderSkillAutoUse { get; private set; }
        internal bool HasEquipCommanderSkill { get; private set; }
        internal int CommanderSkillSlotIndex { get; private set; }
        internal string ExpectedCommanderSkillId { get; private set; }
        internal string NewCommanderSkillId { get; private set; }
        internal bool HasRecordCommanderSkillSummon { get; private set; }
        internal int ExpectedCommanderSkillSummonCount { get; private set; }
        internal IReadOnlyList<string> SummonedCommanderSkillIds { get; private set; }
        internal bool CommanderSkillSummonRequiresPayment { get; private set; }
        internal int CommanderSkillSummonDrawCount { get; private set; }
        internal bool HasLevelUpCommanderSkill { get; private set; }
        internal string CommanderSkillToLevelUpId { get; private set; }
        internal int ExpectedCommanderSkillLevel { get; private set; }
        internal int ExpectedCommanderSkillDuplicateCount { get; private set; }

        public static GameProgressChange SetExpeditionMode(ExpeditionRunMode mode)
        {
            return new GameProgressChange
            {
                HasExpeditionMode = true,
                ExpeditionMode = mode
            };
        }

        public static GameProgressChange MarkOfflineInactive(
            string expectedLastActiveUtc,
            DateTime inactiveUtc,
            int stage)
        {
            return new GameProgressChange
            {
                HasMarkOfflineInactive = true,
                ExpectedOfflineLastActiveUtc = expectedLastActiveUtc?.Trim() ?? string.Empty,
                OfflineNextActiveUtc = inactiveUtc.ToUniversalTime(),
                OfflineNextActiveStage = Math.Max(1, stage)
            };
        }

        public static GameProgressChange SettleOfflineReward(
            string expectedLastActiveUtc,
            DateTime settledToUtc,
            int nextStage,
            OfflineRewardReceiptData receipt,
            RewardBundle rewards)
        {
            return new GameProgressChange
            {
                HasSettleOfflineReward = true,
                ExpectedOfflineLastActiveUtc = expectedLastActiveUtc?.Trim() ?? string.Empty,
                OfflineNextActiveUtc = settledToUtc.ToUniversalTime(),
                OfflineNextActiveStage = Math.Max(1, nextStage),
                OfflineReceipt = receipt?.Clone(),
                Rewards = rewards ?? RewardBundle.Empty
            };
        }

        public static GameProgressChange AcknowledgeOfflineRewards(IReadOnlyList<string> receiptIds)
        {
            var ids = new List<string>(receiptIds?.Count ?? 0);
            if (receiptIds != null)
            {
                for (var index = 0; index < receiptIds.Count; index++)
                {
                    if (!string.IsNullOrWhiteSpace(receiptIds[index]))
                    {
                        ids.Add(receiptIds[index].Trim());
                    }
                }
            }

            return new GameProgressChange
            {
                HasAcknowledgeOfflineRewards = true,
                OfflineReceiptIds = ids
            };
        }

        public static GameProgressChange RecordExpeditionFirstClear(
            int stage,
            RewardBundle rewards) // 최초 진행과 보상을 함께 기록
        {
            return new GameProgressChange
            {
                HasExpeditionFirstClear = true,
                ExpeditionFirstClearStage = stage,
                Rewards = rewards
            };
        }

        public static GameProgressChange RecordExpeditionRepeatClear(
            int stage,
            RewardBundle rewards) // 반복 보상만 기록
        {
            return new GameProgressChange
            {
                HasExpeditionRepeatClear = true,
                ExpeditionRepeatClearStage = stage,
                Rewards = rewards
            };
        }

        public static GameProgressChange RecordFoodRiot(int killCount, RewardBundle rewards) // 식량 대소동 결과 요청
        {
            return new GameProgressChange
            {
                FoodRiotBestKills = Math.Max(0, killCount),
                Rewards = rewards
            };
        }

        public bool TryAttachGrowthDungeonSettlement(
            string contentId,
            int clearedStage,
            bool recordClear,
            string keyItemId = null)
        {
            if ((recordClear && (HasGrowthDungeonClear || string.IsNullOrWhiteSpace(contentId) || clearedStage <= 0)) ||
                (!string.IsNullOrWhiteSpace(keyItemId) && ItemCosts != null))
            {
                return false;
            }

            if (recordClear)
            {
                HasGrowthDungeonClear = true;
                GrowthDungeonContentId = contentId.Trim();
                GrowthDungeonClearedStage = clearedStage;
            }

            if (!string.IsNullOrWhiteSpace(keyItemId))
            {
                ItemCosts = new[] { new ItemAmount(keyItemId.Trim(), 1L) };
            }

            return recordClear || ItemCosts != null;
        }

        public static GameProgressChange RefreshGrowthDungeonDailyKeys(
            long expectedPeriod,
            long nextPeriod,
            params ItemAmount[] targetQuantities)
        {
            return new GameProgressChange
            {
                HasGrowthDungeonDailyKeyRefresh = true,
                ExpectedGrowthDungeonDailyKeyPeriod = expectedPeriod,
                GrowthDungeonDailyKeyPeriod = nextPeriod,
                GrowthDungeonDailyKeyTargets = targetQuantities == null
                    ? Array.Empty<ItemAmount>()
                    : (ItemAmount[])targetQuantities.Clone()
            };
        }

        public static GameProgressChange RefreshAttendance(long expectedPeriod, long nextPeriod)
        {
            return new GameProgressChange
            {
                HasAttendanceRefresh = true,
                ExpectedAttendancePeriod = expectedPeriod,
                AttendancePeriod = nextPeriod
            };
        }

        public static GameProgressChange ClaimAttendance(
            int expectedDay,
            long expectedPeriod,
            RewardBundle reward)
        {
            return new GameProgressChange
            {
                HasAttendanceClaim = true,
                ExpectedAttendanceDay = expectedDay,
                ExpectedAttendanceClaimPeriod = expectedPeriod,
                AttendanceReward = reward ?? RewardBundle.Empty
            };
        }

        public static GameProgressChange AddMail(MailEntryData mail)
        {
            return new GameProgressChange
            {
                HasAddMail = true,
                MailToAdd = mail?.Clone()
            };
        }

        public static GameProgressChange CleanupExpiredMail(DateTime utcNow)
        {
            return new GameProgressChange
            {
                HasCleanupExpiredMail = true,
                MailOperationUtc = NormalizeUtc(utcNow)
            };
        }

        public static GameProgressChange ClaimMail(DateTime utcNow, params string[] mailIds)
        {
            return new GameProgressChange
            {
                HasClaimMail = true,
                MailOperationUtc = NormalizeUtc(utcNow),
                MailClaimIds = mailIds == null ? Array.Empty<string>() : (string[])mailIds.Clone()
            };
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        }

        // 08.06 안건준 추가 - 수호자의 탑 결과 요청 (식량 대소동과 별도 최고기록 집계)
        // 08.07 안건준 추가 - 성공적으로 클리어했을 때만 난이도를 1 올려서 다음 판 적 수·건물 체력 스케일링에 사용한다.
        // 08.07 안건준 수정 - 실패(전멸·시간초과)한 판까지 난이도가 오르면 테스트를 반복할수록 건물 체력이
        // 끝없이 불어나 버려서, cleared가 true일 때만 난이도를 올리도록 수정했다.
        public static GameProgressChange RecordGuardiansTowerClear(int killCount, bool cleared, RewardBundle rewards)
        {
            return RecordGuardiansTowerClear(killCount, cleared, cleared, rewards);
        }

        public static GameProgressChange RecordGuardiansTowerClear(
            int killCount,
            bool cleared,
            bool advanceDifficulty,
            RewardBundle rewards)
        {
            return new GameProgressChange
            {
                GuardiansTowerBestKills = Math.Max(0, killCount),
                IncrementGuardiansTowerDifficulty = cleared && advanceDifficulty,
                Rewards = rewards
            };
        }

        public static GameProgressChange RecordCastleRaidClear() // 성 파괴 기록 요청
        {
            return RecordCastleRaidClear(RewardBundle.Empty);
        }

        public static GameProgressChange RecordCastleRaidClear(RewardBundle rewards)
        {
            return new GameProgressChange
            {
                MarkCastleRaidCleared = true,
                Rewards = rewards ?? RewardBundle.Empty
            };
        }

        public static GameProgressChange GrantCommanderExperience(long amount)
        {
            return new GameProgressChange
            {
                CommanderExperience = Math.Max(0L, amount)
            };
        }

        public static GameProgressChange GrantItems(params ItemAmount[] itemRewards)
        {
            return new GameProgressChange
            {
                HasStandaloneItemGrant = true,
                Rewards = RewardBundle.FromItems(itemRewards)
            };
        }

        public static GameProgressChange DiscardItem(string itemId, long quantity, long expectedQuantity)
        {
            return new GameProgressChange
            {
                HasDiscardItem = true,
                ItemId = itemId?.Trim(),
                ItemQuantity = quantity,
                ExpectedItemQuantity = expectedQuantity
            };
        }

        public static GameProgressChange UseItem(string itemId, long quantity, long expectedQuantity)
        {
            return new GameProgressChange
            {
                HasUseItem = true,
                ItemId = itemId?.Trim(),
                ItemQuantity = quantity,
                ExpectedItemQuantity = expectedQuantity
            };
        }

        public static GameProgressChange LevelUpCommander(int expectedLevel)
        {
            return new GameProgressChange
            {
                HasLevelUpCommander = true,
                ExpectedCommanderLevel = expectedLevel
            };
        }

        public static GameProgressChange UpgradeCommanderLegionStat(
            CommanderLegionStat stat,
            int expectedLevel)
        {
            return new GameProgressChange
            {
                HasUpgradeCommanderLegionStat = true,
                CommanderLegionStatToUpgrade = stat,
                ExpectedCommanderLegionStatLevel = expectedLevel
            };
        }

        public static GameProgressChange AcquireMonster(string monsterId) // 첫 보유 추가 요청
        {
            return new GameProgressChange
            {
                HasAcquireMonster = true,
                AcquireMonsterId = monsterId?.Trim()
            };
        }

        public static GameProgressChange AssignMonster(string monsterId, MonsterPartyKind targetParty)
        {
            return new GameProgressChange
            {
                HasFormationChange = true,
                FormationMonsterId = monsterId?.Trim(),
                TargetParty = targetParty
            };
        }

        public static GameProgressChange UnassignMonster(string monsterId)
        {
            return new GameProgressChange
            {
                HasFormationChange = true,
                FormationMonsterId = monsterId?.Trim(),
                RemoveFromFormation = true
            };
        }

        public static GameProgressChange SetMainBattleFormation(Vector2[] slotOffsets)
        {
            Vector2[] copy = null;
            if (slotOffsets != null)
            {
                copy = new Vector2[slotOffsets.Length];
                Array.Copy(slotOffsets, copy, slotOffsets.Length);
            }

            return new GameProgressChange
            {
                HasMainBattleFormation = true,
                MainBattleFormationOffsets = copy
            };
        }

        public static GameProgressChange LevelUpMonster(string monsterId, int expectedLevel)
        {
            return new GameProgressChange
            {
                HasLevelUpMonster = true,
                LevelUpMonsterId = monsterId?.Trim(),
                ExpectedMonsterLevel = expectedLevel
            };
        }

        public static GameProgressChange AscendMonster(string monsterId, int expectedAscensionLevel)
        {
            return new GameProgressChange
            {
                HasAscendMonster = true,
                AscendMonsterId = monsterId?.Trim(),
                ExpectedAscensionLevel = expectedAscensionLevel
            };
        }

        // 뽑기 결과 한 건 반영 요청. 신규면 획득, 중복이면 돌파 재료(초과분은 전용 재화),
        // 그리고 천장 카운터까지 한 번에 갱신된다 (GameProgressData.TryApplyGachaPull 참고).
        public static GameProgressChange RecordGachaPull(string monsterId, MonsterRarity rarity)
        {
            return new GameProgressChange
            {
                HasGachaPull = true,
                GachaPullMonsterId = monsterId?.Trim(),
                GachaPullRarity = rarity
            };
        }

        public static GameProgressChange RecordGachaPulls(
            IReadOnlyList<GachaPullRecord> pulls,
            IReadOnlyList<ItemAmount> itemCosts)
        {
            var pullCopy = new List<GachaPullRecord>(pulls?.Count ?? 0);
            if (pulls != null)
            {
                for (var index = 0; index < pulls.Count; index++)
                {
                    pullCopy.Add(pulls[index]);
                }
            }

            var costCopy = new List<ItemAmount>(itemCosts?.Count ?? 0);
            if (itemCosts != null)
            {
                for (var index = 0; index < itemCosts.Count; index++)
                {
                    costCopy.Add(itemCosts[index]);
                }
            }

            return new GameProgressChange
            {
                GachaPulls = pullCopy,
                ItemCosts = costCopy
            };
        }

        // 08.10 안건준 추가 - 장비 드랍 결과(최대 6개)를 인벤토리에 추가 요청.
        public static GameProgressChange AcquireEquipment(List<EquipmentInstanceData> instances)
        {
            return new GameProgressChange
            {
                HasAcquireEquipment = true,
                AcquireEquipmentInstances = instances ?? new List<EquipmentInstanceData>()
            };
        }

        // 지정한 인스턴스를 장착한다. 같은 부위에 이미 장착 중인 장비가 있으면 자동으로 교체된다.
        public static GameProgressChange EquipItem(string instanceId)
        {
            return new GameProgressChange
            {
                HasEquipItem = true,
                EquipItemInstanceId = instanceId?.Trim()
            };
        }

        public static GameProgressChange UnequipItem(EquipmentPart part)
        {
            return new GameProgressChange
            {
                HasUnequipItem = true,
                UnequipItemPart = part
            };
        }

        public static GameProgressChange SetEquipmentLock(
            string instanceId,
            bool expectedValue,
            bool nextValue)
        {
            return new GameProgressChange
            {
                HasSetEquipmentLock = true,
                EquipmentLockInstanceId = instanceId?.Trim(),
                ExpectedEquipmentLockValue = expectedValue,
                EquipmentLockValue = nextValue
            };
        }

        public static GameProgressChange DismantleEquipment(IReadOnlyList<string> instanceIds)
        {
            var copiedIds = new List<string>(instanceIds?.Count ?? 0);
            if (instanceIds != null)
            {
                for (var index = 0; index < instanceIds.Count; index++)
                {
                    copiedIds.Add(instanceIds[index]?.Trim());
                }
            }

            return new GameProgressChange
            {
                HasDismantleEquipment = true,
                DismantleEquipmentInstanceIds = copiedIds
            };
        }

        // 장비 부위 슬롯을 +1 강화한다.
        public static GameProgressChange UpgradeEquipmentSlot(EquipmentPart part, int expectedLevel)
        {
            return new GameProgressChange
            {
                HasUpgradeEquipmentSlot = true,
                UpgradeEquipmentSlotPart = part,
                ExpectedEquipmentSlotLevel = expectedLevel
            };
        }

        // 군단장 잠재능력 슬롯에 랜덤으로 뽑힌 옵션 1개를 최초로 배정한다.
        // 이미 값이 있는 슬롯이면 TryApply에서 실패 처리된다.
        public static GameProgressChange AssignCommanderPotentialSlot(
            int slotIndex,
            EquipmentOptionType optionType,
            EquipmentGrade grade,
            float value)
        {
            return new GameProgressChange
            {
                HasAssignCommanderPotentialSlot = true,
                CommanderPotentialSlotIndex = slotIndex,
                CommanderPotentialOptionType = optionType,
                CommanderPotentialGrade = grade,
                CommanderPotentialValue = value
            };
        }

        // "잠재 능력 변경": 강화석 1개를 소모해 잠기지 않은 슬롯들을 새로 뽑은 옵션으로 교체한다.
        // 추첨(랜덤)은 호출 전에 이미 끝나 있고, 여기서는 그 결과를 결정론적으로 반영만 한다.
        public static GameProgressChange RerollCommanderPotentialSlots(
            IReadOnlyList<CommanderPotentialRerollEntry> entries)
        {
            return new GameProgressChange
            {
                HasRerollCommanderPotentialSlots = true,
                CommanderPotentialRerollEntries = entries
            };
        }

        // "옵션 스탯 변경": 강화석 1개를 소모해 옵션 종류·등급은 유지하고 수치만 다시 뽑는다.
        // 잠금은 옵션 자체가 바뀌는 "잠재 능력 변경"만 막는 용도라 잠긴 슬롯도 여기서는 대상이 된다.
        public static GameProgressChange RerollCommanderPotentialValues(
            IReadOnlyList<CommanderPotentialRerollEntry> entries)
        {
            return new GameProgressChange
            {
                HasRerollCommanderPotentialValues = true,
                CommanderPotentialValueRerollEntries = entries
            };
        }

        // 잠재능력 슬롯의 자물쇠 아이콘 클릭 시 잠금/해제를 토글한다.
        public static GameProgressChange SetCommanderPotentialLocked(int slotIndex, bool expectedLocked, bool newLocked)
        {
            return new GameProgressChange
            {
                HasSetCommanderPotentialLocked = true,
                CommanderPotentialLockSlotIndex = slotIndex,
                ExpectedCommanderPotentialLocked = expectedLocked,
                NewCommanderPotentialLocked = newLocked
            };
        }

        public static GameProgressChange SetCommanderSkillAutoUse(bool expectedValue, bool newValue)
        {
            return new GameProgressChange
            {
                HasSetCommanderSkillAutoUse = true,
                ExpectedCommanderSkillAutoUse = expectedValue,
                NewCommanderSkillAutoUse = newValue
            };
        }

        public static GameProgressChange EquipCommanderSkill(
            int slotIndex,
            string expectedSkillId,
            string newSkillId)
        {
            return new GameProgressChange
            {
                HasEquipCommanderSkill = true,
                CommanderSkillSlotIndex = slotIndex,
                ExpectedCommanderSkillId = expectedSkillId?.Trim() ?? string.Empty,
                NewCommanderSkillId = newSkillId?.Trim() ?? string.Empty
            };
        }

        public static GameProgressChange RecordCommanderSkillSummon(
            int expectedSummonCount,
            string summonedSkillId)
        {
            return new GameProgressChange
            {
                HasRecordCommanderSkillSummon = true,
                ExpectedCommanderSkillSummonCount = expectedSummonCount,
                SummonedCommanderSkillIds = new[] { summonedSkillId?.Trim() ?? string.Empty }
            };
        }

        public static GameProgressChange RecordPaidCommanderSkillSummons(
            int expectedSummonCount,
            int drawCount,
            IReadOnlyList<string> summonedSkillIds)
        {
            var copiedIds = summonedSkillIds == null
                ? Array.Empty<string>()
                : summonedSkillIds.Select(id => id?.Trim() ?? string.Empty).ToArray();
            return new GameProgressChange
            {
                HasRecordCommanderSkillSummon = true,
                ExpectedCommanderSkillSummonCount = expectedSummonCount,
                SummonedCommanderSkillIds = copiedIds,
                CommanderSkillSummonRequiresPayment = true,
                CommanderSkillSummonDrawCount = drawCount
            };
        }

        public static GameProgressChange LevelUpCommanderSkill(
            string skillId,
            int expectedLevel,
            int expectedDuplicateCount)
        {
            return new GameProgressChange
            {
                HasLevelUpCommanderSkill = true,
                CommanderSkillToLevelUpId = skillId?.Trim() ?? string.Empty,
                ExpectedCommanderSkillLevel = expectedLevel,
                ExpectedCommanderSkillDuplicateCount = expectedDuplicateCount
            };
        }
    }
}
