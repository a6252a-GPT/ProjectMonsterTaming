using System;
using ProjectMT.Shared.Stats;
using UnityEngine;

namespace ProjectMT.Shared.Commander
{
    public enum CommanderLegionStat // 군단장 성장창의 군단 공용 강화 6종
    {
        MaxHealth,
        AttackPower,
        Defense,
        AttackSpeed,
        MoveSpeed,
        AttackRange
    }

    [Serializable]
    public sealed class CommanderLegionGrowthData // 군단 공용 강화 저장 원본
    {
        [SerializeField] private int healthLevel;
        [SerializeField] private int attackLevel;
        [SerializeField] private int defenseLevel;
        [SerializeField] private int attackSpeedLevel;
        [SerializeField] private int moveSpeedLevel;
        [SerializeField] private int attackRangeLevel;
        [SerializeField] private int unspentTrainingPoints;

        public int UnspentTrainingPoints => unspentTrainingPoints;

        public static CommanderLegionGrowthData CreateDefault() => new CommanderLegionGrowthData();

        public CommanderLegionGrowthData Clone()
        {
            return new CommanderLegionGrowthData
            {
                healthLevel = healthLevel,
                attackLevel = attackLevel,
                defenseLevel = defenseLevel,
                attackSpeedLevel = attackSpeedLevel,
                moveSpeedLevel = moveSpeedLevel,
                attackRangeLevel = attackRangeLevel,
                unspentTrainingPoints = unspentTrainingPoints
            };
        }

        public int GetLevel(CommanderLegionStat stat)
        {
            return stat switch
            {
                CommanderLegionStat.MaxHealth => healthLevel,
                CommanderLegionStat.AttackPower => attackLevel,
                CommanderLegionStat.Defense => defenseLevel,
                CommanderLegionStat.AttackSpeed => attackSpeedLevel,
                CommanderLegionStat.MoveSpeed => moveSpeedLevel,
                CommanderLegionStat.AttackRange => attackRangeLevel,
                _ => 0
            };
        }

        internal bool TryLevelUp(CommanderLegionStat stat, int expectedLevel, int maxLevel)
        {
            if (!Enum.IsDefined(typeof(CommanderLegionStat), stat) || expectedLevel < 0 || maxLevel < 1 ||
                GetLevel(stat) != expectedLevel || expectedLevel >= maxLevel)
            {
                return false;
            }

            switch (stat)
            {
                case CommanderLegionStat.MaxHealth: healthLevel++; break;
                case CommanderLegionStat.AttackPower: attackLevel++; break;
                case CommanderLegionStat.Defense: defenseLevel++; break;
                case CommanderLegionStat.AttackSpeed: attackSpeedLevel++; break;
                case CommanderLegionStat.MoveSpeed: moveSpeedLevel++; break;
                case CommanderLegionStat.AttackRange: attackRangeLevel++; break;
                default: return false;
            }

            return true;
        }

        internal bool TrySpendTrainingPoints(int amount)
        {
            if (amount <= 0 || unspentTrainingPoints < amount)
            {
                return false;
            }

            unspentTrainingPoints -= amount;
            return true;
        }

        internal void GrantTrainingPoints(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            unspentTrainingPoints = unspentTrainingPoints > int.MaxValue - amount
                ? int.MaxValue
                : unspentTrainingPoints + amount;
        }

        internal void SetMigratedTrainingPoints(int amount)
        {
            unspentTrainingPoints = Math.Max(unspentTrainingPoints, Math.Max(0, amount));
        }

        internal void Repair(CommanderGrowthConfig config = null)
        {
            healthLevel = RepairLevel(CommanderLegionStat.MaxHealth, healthLevel, config);
            attackLevel = RepairLevel(CommanderLegionStat.AttackPower, attackLevel, config);
            defenseLevel = RepairLevel(CommanderLegionStat.Defense, defenseLevel, config);
            attackSpeedLevel = RepairLevel(CommanderLegionStat.AttackSpeed, attackSpeedLevel, config);
            moveSpeedLevel = RepairLevel(CommanderLegionStat.MoveSpeed, moveSpeedLevel, config);
            attackRangeLevel = RepairLevel(CommanderLegionStat.AttackRange, attackRangeLevel, config);
            unspentTrainingPoints = Math.Max(0, unspentTrainingPoints);
        }

        internal CommanderLegionGrowthView CreateView() => new CommanderLegionGrowthView(this);

        private static int RepairLevel(CommanderLegionStat stat, int level, CommanderGrowthConfig config)
        {
            level = Math.Max(0, level);
            return config == null ? level : Math.Min(level, config.GetLegionGrowthMaxLevel(stat));
        }
    }

    public readonly struct CommanderLegionGrowthView // UI·Snapshot용 읽기 전용 값
    {
        internal CommanderLegionGrowthView(CommanderLegionGrowthData data)
        {
            HealthLevel = data?.GetLevel(CommanderLegionStat.MaxHealth) ?? 0;
            AttackLevel = data?.GetLevel(CommanderLegionStat.AttackPower) ?? 0;
            DefenseLevel = data?.GetLevel(CommanderLegionStat.Defense) ?? 0;
            AttackSpeedLevel = data?.GetLevel(CommanderLegionStat.AttackSpeed) ?? 0;
            MoveSpeedLevel = data?.GetLevel(CommanderLegionStat.MoveSpeed) ?? 0;
            AttackRangeLevel = data?.GetLevel(CommanderLegionStat.AttackRange) ?? 0;
            UnspentTrainingPoints = Math.Max(0, data?.UnspentTrainingPoints ?? 0);
        }

        public int HealthLevel { get; }
        public int AttackLevel { get; }
        public int DefenseLevel { get; }
        public int AttackSpeedLevel { get; }
        public int MoveSpeedLevel { get; }
        public int AttackRangeLevel { get; }
        public int UnspentTrainingPoints { get; }

        public int GetLevel(CommanderLegionStat stat)
        {
            return stat switch
            {
                CommanderLegionStat.MaxHealth => HealthLevel,
                CommanderLegionStat.AttackPower => AttackLevel,
                CommanderLegionStat.Defense => DefenseLevel,
                CommanderLegionStat.AttackSpeed => AttackSpeedLevel,
                CommanderLegionStat.MoveSpeed => MoveSpeedLevel,
                CommanderLegionStat.AttackRange => AttackRangeLevel,
                _ => 0
            };
        }
    }
}
