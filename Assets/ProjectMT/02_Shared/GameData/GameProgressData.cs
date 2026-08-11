using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.Reward;
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
        [SerializeField] private long gold; // 정식 골드 잔액
        [FormerlySerializedAs("vegetableRiotBestKills")]
        [SerializeField] private int foodRiotBestKills; // 식량 대소동 최고 처치
        [SerializeField] private int guardiansTowerBestKills; // 08.06 안건준 추가 - 수호자의 탑 최고 처치 (식량 대소동과 별도 집계)
        [SerializeField] private int guardiansTowerDifficultyLevel; // 08.07 안건준 추가 - 수호자의 탑 난이도(클리어할 때마다 1씩 증가, 적 수·건물 체력 스케일링에 사용)
        [SerializeField] private bool castleRaidFirstClear; // 군단의 역습 첫 승리
        [SerializeField] private CommanderProgressData commander = CommanderProgressData.CreateDefault(); // 군단장 성장값
        [SerializeField] private MonsterRosterData monsters = MonsterRosterData.CreateDefault(); // 보유·편성값
        [SerializeField] private MainBattleFormationData mainBattleFormation = MainBattleFormationData.CreateDefault(); // 본부대 시작 위치
        [SerializeField] private int ascensionCurrency; // 돌파석 - 최대 돌파 이후 중복 뽑기 시 적립되는 전용 재화
        [SerializeField] private GachaPityData gachaPity = GachaPityData.CreateDefault(); // 뽑기 천장 누적 카운터
        [SerializeField] private EquipmentSaveData equipment = EquipmentSaveData.CreateDefault(); // 08.10 안건준 추가 - 장비 보유·장착 저장
        [SerializeField] private EquipmentSlotUpgradeData equipmentSlotUpgrade = EquipmentSlotUpgradeData.CreateDefault(); // 부위 슬롯 영구 강화 레벨(장비 보유·장착과 별도 저장)
        [SerializeField] private int equipmentSlotEnhancementStone; // 장비 슬롯 강화 전용 재화(스킬 강화석과 분리)

        public int CurrentChallengeStage => currentChallengeStage;
        public int LastClearedStage => lastClearedStage;
        public ExpeditionRunMode ExpeditionMode => expeditionMode;
        public long Gold => gold;
        public int FoodRiotBestKills => foodRiotBestKills;
        public int GuardiansTowerBestKills => guardiansTowerBestKills; // 08.06 안건준 추가
        public int GuardiansTowerDifficultyLevel => guardiansTowerDifficultyLevel; // 08.07 안건준 추가
        public bool CastleRaidFirstClear => castleRaidFirstClear;
        public CommanderProgressView Commander => new CommanderProgressView(commander);
        public MonsterRosterView Monsters => monsters?.CreateView() ?? MonsterRosterData.CreateDefault().CreateView();
        public MainBattleFormationView MainBattleFormation =>
            (mainBattleFormation ?? MainBattleFormationData.CreateDefault()).CreateView();
        public int AscensionCurrency => ascensionCurrency;
        public GachaPityView GachaPity => new GachaPityView(gachaPity);
        public EquipmentSaveDataView Equipment => (equipment ?? EquipmentSaveData.CreateDefault()).CreateView(); // 08.10 안건준 추가
        public EquipmentSlotUpgradeView EquipmentSlotUpgrade =>
            (equipmentSlotUpgrade ?? EquipmentSlotUpgradeData.CreateDefault()).CreateView();
        public int EquipmentSlotEnhancementStone => equipmentSlotEnhancementStone;

        public static GameProgressData CreateDefault()
        {
            return new GameProgressData();
        }

        public GameProgressData Clone()
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
                equipmentSlotUpgrade = equipmentSlotUpgrade?.Clone() ?? EquipmentSlotUpgradeData.CreateDefault(),
                equipmentSlotEnhancementStone = equipmentSlotEnhancementStone
            };
        }

        internal bool TryApply(GameProgressChange change)
        {
            if (change == null)
            {
                return false;
            }

            if (change.HasExpeditionMode && !IsValidExpeditionMode(change.ExpeditionMode))
            {
                return false;
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

            if (change.Rewards != null && !change.Rewards.IsEmpty)
            {
                if (gold > long.MaxValue - change.Rewards.Gold)
                {
                    return false;
                }

                gold += change.Rewards.Gold;
            }

            if (change.FoodRiotBestKills >= 0)
            {
                foodRiotBestKills = Math.Max(foodRiotBestKills, change.FoodRiotBestKills);
            }

            if (change.GuardiansTowerBestKills >= 0) // 08.06 안건준 추가
            {
                guardiansTowerBestKills = Math.Max(guardiansTowerBestKills, change.GuardiansTowerBestKills);
            }

            if (change.IncrementGuardiansTowerDifficulty) // 08.07 안건준 추가 - 클리어할 때마다 난이도 1 증가
            {
                guardiansTowerDifficultyLevel = Math.Max(0, guardiansTowerDifficultyLevel + 1);
            }

            if (change.MarkCastleRaidCleared)
            {
                castleRaidFirstClear = true;
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
                    gold < cost ||
                    !monsters.TryLevelUp(change.LevelUpMonsterId, change.ExpectedMonsterLevel))
                {
                    return false;
                }

                gold -= cost; // 레벨 증가와 같은 후보 데이터에서 차감
            }

            if (change.HasAscendMonster &&
                (string.IsNullOrEmpty(change.AscendMonsterId) ||
                 !monsters.TryAscend(change.AscendMonsterId, change.ExpectedAscensionLevel)))
            {
                return false;
            }

            if (change.HasGachaPull &&
                !TryApplyGachaPull(change.GachaPullMonsterId, change.GachaPullRarity))
            {
                return false;
            }

            // 08.10 안건준 추가 - 장비 획득/장착/해제
            if (change.HasAcquireEquipment)
            {
                equipment ??= EquipmentSaveData.CreateDefault();
                equipment.Acquire(change.AcquireEquipmentInstances);
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

            // 장비 부위 슬롯 영구 강화(+1): 비용 확인 → 재화 차감 → 레벨 증가 순서로 처리한다.
            if (change.HasUpgradeEquipmentSlot)
            {
                equipmentSlotUpgrade ??= EquipmentSlotUpgradeData.CreateDefault();
                var currentLevel = equipmentSlotUpgrade.GetLevel(change.UpgradeEquipmentSlotPart);
                if (currentLevel != change.ExpectedEquipmentSlotLevel)
                {
                    return false; // 요청 시점과 처리 시점 사이에 레벨이 달라짐(중복 클릭 등)
                }

                if (EquipmentSlotUpgradeCostRules.ChargeCurrencyOnUpgrade)
                {
                    var goldCost = EquipmentSlotUpgradeCostRules.GetNextGoldCost(currentLevel);
                    var stoneCost = EquipmentSlotUpgradeCostRules.GetNextStoneCost(currentLevel);
                    if (gold < goldCost || equipmentSlotEnhancementStone < stoneCost)
                    {
                        return false; // 재화 부족
                    }

                    gold -= goldCost;
                    equipmentSlotEnhancementStone -= stoneCost;
                }

                if (!equipmentSlotUpgrade.TryLevelUp(change.UpgradeEquipmentSlotPart, change.ExpectedEquipmentSlotLevel))
                {
                    return false;
                }
            }

            Repair(); // 변경 후 불변식 재확인
            return true;
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
            var nextCurrency = (long)ascensionCurrency + 1;
            if (nextCurrency > int.MaxValue)
            {
                return false;
            }

            ascensionCurrency = (int)nextCurrency;
            return true;
        }

        internal void Repair()
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
            equipmentSlotUpgrade ??= EquipmentSlotUpgradeData.CreateDefault();
            equipmentSlotUpgrade.Repair();
            equipmentSlotEnhancementStone = Math.Max(0, equipmentSlotEnhancementStone);

            if (!IsValidExpeditionMode(expeditionMode) ||
                (lastClearedStage == 0 && expeditionMode == ExpeditionRunMode.Repeat))
            {
                expeditionMode = ExpeditionRunMode.Challenge; // 손상값·클리어 전 반복 복구
            }
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
            EquipmentSlotUpgrade = data.EquipmentSlotUpgrade;
            EquipmentSlotEnhancementStone = data.EquipmentSlotEnhancementStone;
        }

        public int CurrentChallengeStage { get; }
        public int LastClearedStage { get; }
        public ExpeditionRunMode ExpeditionMode { get; }
        public long Gold { get; }
        public int FoodRiotBestKills { get; }
        public int GuardiansTowerBestKills { get; } // 08.06 안건준 추가
        public int GuardiansTowerDifficultyLevel { get; } // 08.07 안건준 추가
        public bool CastleRaidFirstClear { get; }
        public CommanderProgressView Commander { get; }
        public MonsterRosterView Monsters { get; }
        public MainBattleFormationView MainBattleFormation { get; }
        public int AscensionCurrency { get; } // 돌파석 보유량
        public GachaPityView GachaPity { get; } // 뽑기 천장 누적 카운터
        public EquipmentSaveDataView Equipment { get; } // 08.10 안건준 추가 - 장비 보유·장착 상태
        public EquipmentSlotUpgradeView EquipmentSlotUpgrade { get; } // 부위 슬롯 영구 강화 레벨
        public int EquipmentSlotEnhancementStone { get; } // 장비 슬롯 강화석 보유량
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
        internal bool HasAcquireEquipment { get; private set; } // 08.10 안건준 추가
        internal List<EquipmentInstanceData> AcquireEquipmentInstances { get; private set; }
        internal bool HasEquipItem { get; private set; }
        internal string EquipItemInstanceId { get; private set; }
        internal bool HasUnequipItem { get; private set; }
        internal EquipmentPart UnequipItemPart { get; private set; }
        internal bool HasUpgradeEquipmentSlot { get; private set; }
        internal EquipmentPart UpgradeEquipmentSlotPart { get; private set; }
        internal int ExpectedEquipmentSlotLevel { get; private set; }

        public static GameProgressChange SetExpeditionMode(ExpeditionRunMode mode)
        {
            return new GameProgressChange
            {
                HasExpeditionMode = true,
                ExpeditionMode = mode
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

        // 08.06 안건준 추가 - 수호자의 탑 결과 요청 (식량 대소동과 별도 최고기록 집계)
        // 08.07 안건준 추가 - 성공적으로 클리어했을 때만 난이도를 1 올려서 다음 판 적 수·건물 체력 스케일링에 사용한다.
        // 08.07 안건준 수정 - 실패(전멸·시간초과)한 판까지 난이도가 오르면 테스트를 반복할수록 건물 체력이
        // 끝없이 불어나 버려서, cleared가 true일 때만 난이도를 올리도록 수정했다.
        public static GameProgressChange RecordGuardiansTowerClear(int killCount, bool cleared, RewardBundle rewards)
        {
            return new GameProgressChange
            {
                GuardiansTowerBestKills = Math.Max(0, killCount),
                IncrementGuardiansTowerDifficulty = cleared,
                Rewards = rewards
            };
        }

        public static GameProgressChange RecordCastleRaidClear() // 성 파괴 기록 요청
        {
            return new GameProgressChange
            {
                MarkCastleRaidCleared = true
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
    }
}
