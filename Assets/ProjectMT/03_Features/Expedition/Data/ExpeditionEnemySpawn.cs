using System;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectMT.Features.Expedition
{
    public enum ExpeditionEnemyRole // 외형이 결정하는 원정대 전투 역할
    {
        Melee,
        Ranged,
        Flanker
    }

    [Serializable]
    public sealed class ExpeditionSpawnPoolEntry // 한 단계의 적 종류·출현 비율·등급 범위
    {
        [SerializeField] private EnemyAppearanceGroup appearance;
        [SerializeField, FormerlySerializedAs("weight"), Range(0f, 100f)] private float percentage = 100f;
        [SerializeField] private MonsterRarity minimumRarity = MonsterRarity.Common;
        [SerializeField] private MonsterRarity maximumRarity = MonsterRarity.Common;

        public EnemyAppearanceGroup Appearance => appearance;
        public ExpeditionEnemyRole Role => ResolveRole(appearance);
        public float Percentage => Mathf.Clamp(percentage, 0f, 100f);
        public MonsterRarity MinimumRarity => (MonsterRarity)Mathf.Clamp(
            (int)minimumRarity, (int)MonsterRarity.Common, (int)MonsterRarity.Mythic);
        public MonsterRarity MaximumRarity => (MonsterRarity)Mathf.Clamp(
            Mathf.Max((int)MinimumRarity, (int)maximumRarity),
            (int)MonsterRarity.Common,
            (int)MonsterRarity.Mythic);

        public MonsterRarity ResolveRarity(System.Random random, bool chooseMaximum)
        {
            if (chooseMaximum || MinimumRarity == MaximumRarity) return MaximumRarity;
            var minimum = (int)MinimumRarity;
            var maximum = (int)MaximumRarity;
            return (MonsterRarity)(random?.Next(minimum, maximum + 1) ?? minimum);
        }

        public static ExpeditionEnemyRole ResolveRole(EnemyAppearanceGroup group) => group switch
        {
            EnemyAppearanceGroup.MageTier1 or
            EnemyAppearanceGroup.MageTier2 or
            EnemyAppearanceGroup.MageTier3 => ExpeditionEnemyRole.Ranged,
            EnemyAppearanceGroup.Ninja => ExpeditionEnemyRole.Flanker,
            _ => ExpeditionEnemyRole.Melee
        };

#if UNITY_EDITOR
        public ExpeditionSpawnPoolEntry EditorCopyWithBalance(
            float spawnPercentage,
            MonsterRarity rarityMinimum,
            MonsterRarity rarityMaximum)
        {
            return EditorCreate(appearance, spawnPercentage, rarityMinimum, rarityMaximum);
        }

        public static ExpeditionSpawnPoolEntry EditorCreate(
            EnemyAppearanceGroup group,
            float spawnPercentage,
            MonsterRarity rarityMinimum,
            MonsterRarity rarityMaximum)
        {
            var minimum = (MonsterRarity)Mathf.Clamp(
                (int)rarityMinimum, (int)MonsterRarity.Common, (int)MonsterRarity.Mythic);
            var maximum = (MonsterRarity)Mathf.Clamp(
                Mathf.Max((int)minimum, (int)rarityMaximum),
                (int)MonsterRarity.Common,
                (int)MonsterRarity.Mythic);
            return new ExpeditionSpawnPoolEntry
            {
                appearance = group,
                percentage = Mathf.Clamp(spawnPercentage, 0f, 100f),
                minimumRarity = minimum,
                maximumRarity = maximum
            };
        }

        public static ExpeditionSpawnPoolEntry EditorCreate(
            EnemyAppearanceGroup group,
            ExpeditionEnemyRole ignoredLegacyRole,
            int legacyWeight)
        {
            return EditorCreate(group, Mathf.Max(0, legacyWeight), MonsterRarity.Common, MonsterRarity.Common);
        }
#endif
    }

    public readonly struct ExpeditionEnemySpawn // 실제 한 슬롯의 외형·역할·등급·보스 명세
    {
        public ExpeditionEnemySpawn(
            EnemyAppearanceGroup appearance,
            ExpeditionEnemyRole role,
            bool isBoss,
            int ninjaOrdinal = -1,
            MonsterRarity rarity = MonsterRarity.Common,
            float waveHealthMultiplier = 1f,
            float waveDamageMultiplier = 1f,
            float waveDefenseMultiplier = 1f)
        {
            Appearance = appearance;
            Role = role;
            IsBoss = isBoss;
            NinjaOrdinal = ninjaOrdinal;
            Rarity = rarity;
            WaveHealthMultiplier = Mathf.Max(0.01f, waveHealthMultiplier);
            WaveDamageMultiplier = Mathf.Max(0.01f, waveDamageMultiplier);
            WaveDefenseMultiplier = Mathf.Max(0f, waveDefenseMultiplier);
        }

        public EnemyAppearanceGroup Appearance { get; }
        public ExpeditionEnemyRole Role { get; }
        public bool IsBoss { get; }
        public int NinjaOrdinal { get; }
        public MonsterRarity Rarity { get; }
        public float WaveHealthMultiplier { get; }
        public float WaveDamageMultiplier { get; }
        public float WaveDefenseMultiplier { get; }
        public bool IsRanged => Role == ExpeditionEnemyRole.Ranged;
        public bool IsNinja => Role == ExpeditionEnemyRole.Flanker;
    }

    public static class ExpeditionEnemyRarityRules // 적 등급별 능력치 배율
    {
        public static float GetHealthMultiplier(MonsterRarity rarity) => rarity switch
        {
            MonsterRarity.Rare => 1.15f,
            MonsterRarity.Epic => 1.35f,
            MonsterRarity.Legendary => 1.65f,
            MonsterRarity.Mythic => 2f,
            _ => 1f
        };

        public static float GetDamageMultiplier(MonsterRarity rarity) => rarity switch
        {
            MonsterRarity.Rare => 1.08f,
            MonsterRarity.Epic => 1.16f,
            MonsterRarity.Legendary => 1.28f,
            MonsterRarity.Mythic => 1.42f,
            _ => 1f
        };

        public static float GetDefenseMultiplier(MonsterRarity rarity) => rarity switch
        {
            MonsterRarity.Rare => 1.08f,
            MonsterRarity.Epic => 1.16f,
            MonsterRarity.Legendary => 1.28f,
            MonsterRarity.Mythic => 1.42f,
            _ => 1f
        };
    }
}
