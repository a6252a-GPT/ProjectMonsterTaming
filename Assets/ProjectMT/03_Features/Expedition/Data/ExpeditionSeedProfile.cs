using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.Expedition
{
    [CreateAssetMenu(menuName = "ProjectMT/Expedition/Seed Profile", fileName = "ExpeditionSeedProfile")]
    public sealed class ExpeditionSeedProfile : ScriptableObject // 원정대 시드 밸런스
    {
        [SerializeField, Min(0.1f)] private float waveIntervalSeconds = 2f; // 2웨이브 출현 간격
        [SerializeField, Min(1f)] private float challengeTimeLimitSeconds = 45f; // 도전 제한시간
        [SerializeField, Min(0.1f)] private float resultDelaySeconds = 0.8f; // 결과 표시 대기
        [SerializeField, Min(1f)] private float enemyBaseHealth = 28f; // 1단계 적 체력
        [SerializeField, Min(0.1f)] private float enemyBaseDamage = 4f; // 1단계 적 공격력
        [SerializeField, Min(0f)] private float enemyHealthGrowthPerStage = 0.11f; // 단계당 체력 증가율
        [SerializeField, Min(0f)] private float enemyDamageGrowthPerStage = 0.07f; // 단계당 공격 증가율

        public float WaveIntervalSeconds => waveIntervalSeconds;
        public float ChallengeTimeLimitSeconds => challengeTimeLimitSeconds;
        public float ResultDelaySeconds => resultDelaySeconds;

        public UnitStatsSnapshot CreateEnemyStats(int stage, int unitIndex)
        {
            var ranged = unitIndex % 4 == 3; // 네 번째마다 원거리 배치
            return CreateEnemyStats(stage, ranged);
        }

        public UnitStatsSnapshot CreateEnemyStats(int stage, bool ranged)
        {
            var stageOffset = Mathf.Max(0, stage - 1);
            return new UnitStatsSnapshot
            {
                maxHealth = enemyBaseHealth * (1f + enemyHealthGrowthPerStage * stageOffset),
                damage = enemyBaseDamage * (1f + enemyDamageGrowthPerStage * stageOffset),
                moveSpeed = ranged ? 1.9f : 2.15f,
                attackRange = ranged ? 4.1f : 1f,
                attackInterval = ranged ? 1.2f : 1f,
                projectileSpeed = ranged ? 8f : 0f,
                ranged = ranged
            };
        }
    }

    public static class ExpeditionStageRules // 원정대 적 수·진형 공식
    {
        public const int WaveCount = 2; // 스테이지당 고정 웨이브
        public const int FormationColumns = 4; // 한 행 최대 인원
        public const float FormationSpacing = 0.85f; // 유닛 간격

        public static int GetEnemiesPerWave(int stage)
        {
            return 4 + (Mathf.Max(1, stage) - 1) / 10; // 10단계마다 한 마리 증가
        }

        public static int GetTotalEnemies(int stage)
        {
            return WaveCount * GetEnemiesPerWave(stage);
        }

        public static Vector2 GetFormationOffset(int unitIndex, int unitCount)
        {
            var row = unitIndex / FormationColumns;
            var column = unitIndex % FormationColumns;
            var rowStart = row * FormationColumns;
            var rowCount = Mathf.Min(FormationColumns, Mathf.Max(0, unitCount - rowStart));
            if (rowCount == 0)
            {
                return Vector2.zero;
            }

            var centeredColumn = column - (rowCount - 1) * 0.5f; // 덜 찬 마지막 행 중앙 정렬
            return new Vector2(centeredColumn * FormationSpacing, row * FormationSpacing);
        }
    }
}
