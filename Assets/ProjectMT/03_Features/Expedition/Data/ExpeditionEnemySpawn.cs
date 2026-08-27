using System;
using UnityEngine;

namespace ProjectMT.Features.Expedition
{
    public enum ExpeditionEnemyRole // 원정대 전투 역할
    {
        Melee,
        Ranged,
        Flanker
    }

    [Serializable]
    public sealed class ExpeditionSpawnPoolEntry // 구간별 일반 적 랜덤 풀 한 항목
    {
        [SerializeField] private EnemyAppearanceGroup appearance;
        [SerializeField] private ExpeditionEnemyRole role;
        [SerializeField, Min(1)] private int weight = 1;

        public EnemyAppearanceGroup Appearance => appearance;
        public ExpeditionEnemyRole Role => role;
        public int Weight => Mathf.Max(1, weight);

#if UNITY_EDITOR
        public static ExpeditionSpawnPoolEntry EditorCreate(
            EnemyAppearanceGroup group,
            ExpeditionEnemyRole enemyRole,
            int selectionWeight)
        {
            return new ExpeditionSpawnPoolEntry
            {
                appearance = group,
                role = enemyRole,
                weight = Mathf.Max(1, selectionWeight)
            };
        }
#endif
    }

    public readonly struct ExpeditionEnemySpawn // 실제 한 슬롯의 외형·역할·보스 명세
    {
        public ExpeditionEnemySpawn(
            EnemyAppearanceGroup appearance,
            ExpeditionEnemyRole role,
            bool isBoss,
            int ninjaOrdinal = -1)
        {
            Appearance = appearance;
            Role = role;
            IsBoss = isBoss;
            NinjaOrdinal = ninjaOrdinal;
        }

        public EnemyAppearanceGroup Appearance { get; }
        public ExpeditionEnemyRole Role { get; }
        public bool IsBoss { get; }
        public int NinjaOrdinal { get; }
        public bool IsRanged => Role == ExpeditionEnemyRole.Ranged;
        public bool IsNinja => Role == ExpeditionEnemyRole.Flanker;
    }
}
