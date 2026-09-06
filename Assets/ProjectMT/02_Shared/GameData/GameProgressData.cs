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

    public enum ExpeditionDifficulty // 원정대 난이도 진행축
    {
        Normal,
        Hard
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
    public sealed partial class GameProgressData // 시드 사용자 진행 원본
    {
        private const int ExpeditionMaximumStage = 100;

        [SerializeField] private int currentChallengeStage = 1; // 현재 도전 단계
        [SerializeField] private int lastClearedStage; // 마지막 성공 단계
        [SerializeField] private int hardCurrentChallengeStage = 1; // 하드 현재 도전 단계
        [SerializeField] private int hardLastClearedStage; // 하드 마지막 성공 단계
        [SerializeField] private ExpeditionDifficulty expeditionDifficulty; // 일반 100 이후 하드 진행
        [SerializeField] private ExpeditionRunMode expeditionMode = ExpeditionRunMode.Challenge; // 저장된 실행 모드
        [FormerlySerializedAs("temporaryGold")]
        [SerializeField] private long gold; // v10 이하 잔액의 동결 백업
        [FormerlySerializedAs("vegetableRiotBestKills")]
        [SerializeField] private int foodRiotBestKills; // 식량 대소동 최고 처치
        [SerializeField] private int guardiansTowerBestKills; // 08.06 안건준 추가 - 수호자의 탑 최고 처치 (식량 대소동과 별도 집계)
        [SerializeField] private int guardiansTowerDifficultyLevel; // 08.07 안건준 추가 - 수호자의 탑 난이도(클리어할 때마다 1씩 증가, 적 수·건물 체력 스케일링에 사용)
        [SerializeField] private bool castleRaidFirstClear; // v21 이하 군단의 역습 첫 승리 호환값
        [SerializeField] private int castleRaidHighestClearedStage; // 군단의 역습 1~100 최고 클리어 단계
        [SerializeField] private CommanderProgressData commander = CommanderProgressData.CreateDefault(); // 군단장 성장값
        [SerializeField] private MonsterRosterData monsters = MonsterRosterData.CreateDefault(); // 보유·편성값
        [SerializeField] private MainBattleFormationData mainBattleFormation = MainBattleFormationData.CreateDefault(); // 본부대 시작 위치
        [SerializeField] private int ascensionCurrency; // v10 이하 잔액의 동결 백업
        [SerializeField] private GachaPityData gachaPity = GachaPityData.CreateDefault(); // 뽑기 천장 누적 카운터
        [SerializeField] private GachaPityData soulMonsterPity = GachaPityData.CreateDefault();
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

        public int CurrentChallengeStage => expeditionDifficulty == ExpeditionDifficulty.Hard
            ? ExpeditionMaximumStage + hardCurrentChallengeStage
            : currentChallengeStage;
        public int LastClearedStage => expeditionDifficulty == ExpeditionDifficulty.Hard
            ? ExpeditionMaximumStage + hardLastClearedStage
            : lastClearedStage;
        public int ActiveChallengeStage => expeditionDifficulty == ExpeditionDifficulty.Hard
            ? hardCurrentChallengeStage
            : currentChallengeStage;
        public int ActiveLastClearedStage => expeditionDifficulty == ExpeditionDifficulty.Hard
            ? hardLastClearedStage
            : lastClearedStage;
        public int NormalLastClearedStage => lastClearedStage;
        public int HardLastClearedStage => hardLastClearedStage;
        public ExpeditionDifficulty Difficulty => expeditionDifficulty;
        public bool HardUnlocked => lastClearedStage >= ExpeditionMaximumStage;
        public ExpeditionRunMode ExpeditionMode => expeditionMode;
        public long Gold => coreBalanceMigrationCompleted
            ? (items ?? ItemInventoryData.CreateDefault()).GetQuantity(ItemIds.Gold)
            : Math.Max(0L, gold);
        public long Diamond => (items ?? ItemInventoryData.CreateDefault()).GetQuantity(ItemIds.Diamond);
        public int FoodRiotBestKills => foodRiotBestKills;
        public int GuardiansTowerBestKills => guardiansTowerBestKills; // 08.06 안건준 추가
        public int GuardiansTowerDifficultyLevel => guardiansTowerDifficultyLevel; // 08.07 안건준 추가
        public bool CastleRaidFirstClear => castleRaidFirstClear || castleRaidHighestClearedStage > 0;
        public int CastleRaidHighestClearedStage => castleRaidHighestClearedStage;
        public CommanderProgressView Commander => new CommanderProgressView(commander);
        public MonsterRosterView Monsters => monsters?.CreateView() ?? MonsterRosterData.CreateDefault().CreateView();
        public MainBattleFormationView MainBattleFormation =>
            (mainBattleFormation ?? MainBattleFormationData.CreateDefault()).CreateView();
        public long AscensionCurrency => coreBalanceMigrationCompleted
            ? (items ?? ItemInventoryData.CreateDefault()).GetQuantity(ItemIds.AscensionStone)
            : Math.Max(0, ascensionCurrency);
        public GachaPityView GachaPity => new GachaPityView(gachaPity);
        public GachaPityView SoulMonsterPity => new GachaPityView(soulMonsterPity);
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
            var clone = new GameProgressData // 변경 전 후보 복사본
            {
                currentChallengeStage = currentChallengeStage,
                lastClearedStage = lastClearedStage,
                hardCurrentChallengeStage = hardCurrentChallengeStage,
                hardLastClearedStage = hardLastClearedStage,
                expeditionDifficulty = expeditionDifficulty,
                expeditionMode = expeditionMode,
                gold = gold,
                foodRiotBestKills = foodRiotBestKills,
                guardiansTowerBestKills = guardiansTowerBestKills, // 08.06 안건준 추가
                guardiansTowerDifficultyLevel = guardiansTowerDifficultyLevel, // 08.07 안건준 추가
                castleRaidFirstClear = castleRaidFirstClear,
                castleRaidHighestClearedStage = castleRaidHighestClearedStage,
                commander = commander?.Clone() ?? CommanderProgressData.CreateDefault(),
                monsters = monsters?.Clone() ?? MonsterRosterData.CreateDefault(),
                mainBattleFormation = mainBattleFormation?.Clone() ?? MainBattleFormationData.CreateDefault(),
                ascensionCurrency = ascensionCurrency,
                gachaPity = gachaPity?.Clone() ?? GachaPityData.CreateDefault(),
                soulMonsterPity = soulMonsterPity?.Clone() ?? GachaPityData.CreateDefault(),
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
            CopyQuestTo(clone);
            return clone;
        }

        partial void CopyQuestTo(GameProgressData clone);
        partial void RepairQuest();
        partial void RejectInvalidQuestClaim(GameProgressChange change, ref bool rejected);
        partial void RejectInvalidQuestDailyReset(GameProgressChange change, ref bool rejected);
        partial void RejectInvalidQuestWeeklyReset(GameProgressChange change, ref bool rejected);
        partial void ApplyQuestProgress(GameProgressChange change, ref bool rejected);
        partial void ApplyQuestClaim(GameProgressChange change);
        partial void ApplyQuestDailyReset(GameProgressChange change);
        partial void ApplyQuestWeeklyReset(GameProgressChange change);
        partial void ApplyQuestProgressReset(GameProgressChange change);
    }

    public readonly struct GameProgressView // UI·전투용 읽기 전용 복사값
    {
        public GameProgressView(GameProgressData data)
        {
            CurrentChallengeStage = data.CurrentChallengeStage;
            LastClearedStage = data.LastClearedStage;
            ActiveChallengeStage = data.ActiveChallengeStage;
            ActiveLastClearedStage = data.ActiveLastClearedStage;
            NormalLastClearedStage = data.NormalLastClearedStage;
            HardLastClearedStage = data.HardLastClearedStage;
            Difficulty = data.Difficulty;
            HardUnlocked = data.HardUnlocked;
            ExpeditionMode = data.ExpeditionMode;
            Gold = data.Gold;
            Diamond = data.Diamond;
            FoodRiotBestKills = data.FoodRiotBestKills;
            GuardiansTowerBestKills = data.GuardiansTowerBestKills; // 08.06 안건준 추가
            GuardiansTowerDifficultyLevel = data.GuardiansTowerDifficultyLevel; // 08.07 안건준 추가
            CastleRaidFirstClear = data.CastleRaidFirstClear;
            CastleRaidHighestClearedStage = data.CastleRaidHighestClearedStage;
            Commander = data.Commander;
            Monsters = data.Monsters;
            MainBattleFormation = data.MainBattleFormation;
            AscensionCurrency = data.AscensionCurrency;
            GachaPity = data.GachaPity;
            SoulMonsterPity = data.SoulMonsterPity;
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
        public int ActiveChallengeStage { get; }
        public int ActiveLastClearedStage { get; }
        public int NormalLastClearedStage { get; }
        public int HardLastClearedStage { get; }
        public ExpeditionDifficulty Difficulty { get; }
        public bool HardUnlocked { get; }
        public ExpeditionRunMode ExpeditionMode { get; }
        public long Gold { get; }
        public long Diamond { get; }
        public int FoodRiotBestKills { get; }
        public int GuardiansTowerBestKills { get; } // 08.06 안건준 추가
        public int GuardiansTowerDifficultyLevel { get; } // 08.07 안건준 추가
        public bool CastleRaidFirstClear { get; }
        public int CastleRaidHighestClearedStage { get; }
        public CommanderProgressView Commander { get; }
        public MonsterRosterView Monsters { get; }
        public MainBattleFormationView MainBattleFormation { get; }
        public long AscensionCurrency { get; } // 돌파석 보유량
        public GachaPityView GachaPity { get; } // 뽑기 천장 누적 카운터
        public GachaPityView SoulMonsterPity { get; }
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

    public sealed partial class GameProgressChange // 한 번에 검증할 진행 변경 묶음
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
        internal bool HasClaimMonsterCollectionFiveStarReward { get; private set; }
        internal string CollectionRewardMonsterId { get; private set; }
        internal bool HasAcknowledgeMonsterCollectionNew { get; private set; }
        internal string AcknowledgeCollectionMonsterId { get; private set; }
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
        internal int CastleRaidClearedStage { get; private set; }
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
        internal bool HasEquipItems { get; private set; }
        internal IReadOnlyList<string> EquipItemInstanceIds { get; private set; }
        internal bool HasUnequipItem { get; private set; }
        internal EquipmentPart UnequipItemPart { get; private set; }
        internal bool HasSetEquipmentLock { get; private set; }
        internal string EquipmentLockInstanceId { get; private set; }
        internal bool ExpectedEquipmentLockValue { get; private set; }
        internal bool EquipmentLockValue { get; private set; }
        internal bool HasDismantleEquipment { get; private set; }
        internal IReadOnlyList<string> DismantleEquipmentInstanceIds { get; private set; }
        internal bool HasSetOfflineAutoDismantlePolicy { get; private set; }
        internal OfflineAutoDismantlePolicy ExpectedOfflineAutoDismantlePolicy { get; private set; }
        internal OfflineAutoDismantlePolicy OfflineAutoDismantlePolicy { get; private set; }
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
        internal bool SuppressChangedNotification => HasMarkOfflineInactive || HasConsumeQuestTutorialHint;
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
    }
}
