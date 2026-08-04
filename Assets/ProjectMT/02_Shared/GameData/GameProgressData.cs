using System;
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
    public sealed class GameProgressData // 시드 사용자 진행 원본
    {
        [SerializeField] private int currentChallengeStage = 1; // 현재 도전 단계
        [SerializeField] private int lastClearedStage; // 마지막 성공 단계
        [SerializeField] private ExpeditionRunMode expeditionMode = ExpeditionRunMode.Challenge; // 저장된 실행 모드
        [SerializeField] private int temporaryGold; // 시드 임시 재화
        [FormerlySerializedAs("vegetableRiotBestKills")]
        [SerializeField] private int foodRiotBestKills; // 식량 대소동 최고 처치
        [SerializeField] private bool castleRaidFirstClear; // 군단의 역습 첫 승리
        [SerializeField] private CommanderProgressData commander = CommanderProgressData.CreateDefault(); // 군단장 성장값

        public int CurrentChallengeStage => currentChallengeStage;
        public int LastClearedStage => lastClearedStage;
        public ExpeditionRunMode ExpeditionMode => expeditionMode;
        public int TemporaryGold => temporaryGold;
        public int FoodRiotBestKills => foodRiotBestKills;
        public bool CastleRaidFirstClear => castleRaidFirstClear;
        public CommanderProgressView Commander => new CommanderProgressView(commander);

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
                temporaryGold = temporaryGold,
                foodRiotBestKills = foodRiotBestKills,
                castleRaidFirstClear = castleRaidFirstClear,
                commander = commander?.Clone() ?? CommanderProgressData.CreateDefault()
            };
        }

        internal bool TryApply(GameProgressChange change)
        {
            if (change == null)
            {
                return false;
            }

            if (change.HasExpeditionMode)
            {
                expeditionMode = change.ExpeditionMode;
            }

            if (change.ChallengeVictoryStage > 0)
            {
                if (change.ChallengeVictoryStage != currentChallengeStage)
                {
                    return false; // 현재 도전 단계만 승리 인정
                }

                lastClearedStage = Math.Max(lastClearedStage, change.ChallengeVictoryStage);
                currentChallengeStage = Math.Max(currentChallengeStage, change.ChallengeVictoryStage + 1);
            }

            if (change.TemporaryGoldDelta != 0)
            {
                var nextGold = (long)temporaryGold + change.TemporaryGoldDelta; // int 범위 밖 연산 방지
                if (nextGold < 0 || nextGold > int.MaxValue)
                {
                    return false;
                }

                temporaryGold = (int)nextGold;
            }

            if (change.FoodRiotBestKills >= 0)
            {
                foodRiotBestKills = Math.Max(foodRiotBestKills, change.FoodRiotBestKills);
            }

            if (change.MarkCastleRaidCleared)
            {
                castleRaidFirstClear = true;
            }

            Repair(); // 변경 후 불변식 재확인
            return true;
        }

        internal void Repair()
        {
            currentChallengeStage = Math.Max(1, currentChallengeStage);
            lastClearedStage = Math.Max(0, Math.Min(lastClearedStage, currentChallengeStage - 1));
            temporaryGold = Math.Max(0, temporaryGold);
            foodRiotBestKills = Math.Max(0, foodRiotBestKills);
            commander ??= CommanderProgressData.CreateDefault();
            commander.Repair();

            if (lastClearedStage == 0 && expeditionMode == ExpeditionRunMode.Repeat)
            {
                expeditionMode = ExpeditionRunMode.Challenge; // 클리어 전 반복 모드 금지
            }
        }
    }

    public readonly struct GameProgressView // UI·전투용 읽기 전용 복사값
    {
        public GameProgressView(GameProgressData data)
        {
            CurrentChallengeStage = data.CurrentChallengeStage;
            LastClearedStage = data.LastClearedStage;
            ExpeditionMode = data.ExpeditionMode;
            TemporaryGold = data.TemporaryGold;
            FoodRiotBestKills = data.FoodRiotBestKills;
            CastleRaidFirstClear = data.CastleRaidFirstClear;
            Commander = data.Commander;
        }

        public int CurrentChallengeStage { get; }
        public int LastClearedStage { get; }
        public ExpeditionRunMode ExpeditionMode { get; }
        public int TemporaryGold { get; }
        public int FoodRiotBestKills { get; }
        public bool CastleRaidFirstClear { get; }
        public CommanderProgressView Commander { get; }
    }

    public sealed class GameProgressChange // 한 번에 검증할 진행 변경 묶음
    {
        private GameProgressChange()
        {
            FoodRiotBestKills = -1; // 최고기록 미변경 표식
        }

        internal bool HasExpeditionMode { get; private set; }
        internal ExpeditionRunMode ExpeditionMode { get; private set; }
        internal int ChallengeVictoryStage { get; private set; }
        internal int TemporaryGoldDelta { get; private set; }
        internal int FoodRiotBestKills { get; private set; }
        internal bool MarkCastleRaidCleared { get; private set; }

        public static GameProgressChange SetExpeditionMode(ExpeditionRunMode mode)
        {
            return new GameProgressChange
            {
                HasExpeditionMode = true,
                ExpeditionMode = mode
            };
        }

        public static GameProgressChange RecordChallengeVictory(int stage) // 도전 승리 요청
        {
            return new GameProgressChange
            {
                ChallengeVictoryStage = stage
            };
        }

        public static GameProgressChange RecordFoodRiot(int killCount, int temporaryGoldReward) // 식량 대소동 결과 요청
        {
            return new GameProgressChange
            {
                FoodRiotBestKills = Math.Max(0, killCount),
                TemporaryGoldDelta = Math.Max(0, temporaryGoldReward)
            };
        }

        public static GameProgressChange RecordCastleRaidClear() // 성 파괴 기록 요청
        {
            return new GameProgressChange
            {
                MarkCastleRaidCleared = true
            };
        }
    }
}
