using UnityEngine;

namespace ProjectMT.Features.Expedition
{
    [CreateAssetMenu(menuName = "ProjectMT/Expedition/Enemy Stage Appearance Set", fileName = "EnemyStageAppearanceSet")]
    public sealed class EnemyStageAppearanceSet : ScriptableObject // 단계별 모듈러 적 선택표
    {
        [SerializeField] private GameObject peasantPrefab;
        [SerializeField] private GameObject knightTier1Prefab;
        [SerializeField] private GameObject knightTier2Prefab;
        [SerializeField] private GameObject knightTier3Prefab;
        [SerializeField] private GameObject mageTier1Prefab;
        [SerializeField] private GameObject mageTier2Prefab;
        [SerializeField] private GameObject mageTier3Prefab;

        public bool IsConfigured => peasantPrefab != null &&
                                    knightTier1Prefab != null && knightTier2Prefab != null && knightTier3Prefab != null &&
                                    mageTier1Prefab != null && mageTier2Prefab != null && mageTier3Prefab != null;

        public bool IsRangedSlot(int stage, int unitIndex)
        {
            return Mathf.Max(1, stage) >= 11 && Mathf.Max(0, unitIndex) % 4 == 3; // 농부 이후 기존 원거리 밀도 유지
        }

        public EnemyAppearanceGroup ResolveGroup(int stage, bool ranged)
        {
            stage = Mathf.Max(1, stage);
            if (stage <= 10)
            {
                return EnemyAppearanceGroup.Peasant;
            }

            var tier = Mathf.Min(3, (stage - 1) / 10);
            if (ranged)
            {
                return tier == 1 ? EnemyAppearanceGroup.MageTier1 :
                    tier == 2 ? EnemyAppearanceGroup.MageTier2 : EnemyAppearanceGroup.MageTier3;
            }

            return tier == 1 ? EnemyAppearanceGroup.KnightTier1 :
                tier == 2 ? EnemyAppearanceGroup.KnightTier2 : EnemyAppearanceGroup.KnightTier3;
        }

        public GameObject ResolvePrefab(int stage, bool ranged)
        {
            return ResolvePrefab(ResolveGroup(stage, ranged));
        }

        public GameObject ResolvePrefab(EnemyAppearanceGroup group)
        {
            return group switch
            {
                EnemyAppearanceGroup.Peasant => peasantPrefab,
                EnemyAppearanceGroup.KnightTier1 => knightTier1Prefab,
                EnemyAppearanceGroup.KnightTier2 => knightTier2Prefab,
                EnemyAppearanceGroup.KnightTier3 => knightTier3Prefab,
                EnemyAppearanceGroup.MageTier1 => mageTier1Prefab,
                EnemyAppearanceGroup.MageTier2 => mageTier2Prefab,
                EnemyAppearanceGroup.MageTier3 => mageTier3Prefab,
                _ => peasantPrefab
            };
        }
    }
}
