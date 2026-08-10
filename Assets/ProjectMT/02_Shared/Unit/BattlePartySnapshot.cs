using System;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [Serializable]
    public struct LegionStatBonus // 군단 전체에 더할 성장 보너스 비율
    {
        [SerializeField] private float healthRate;
        [SerializeField] private float attackRate;
        [SerializeField] private float defenseRate;
        [SerializeField] private float attackSpeedRate;
        [SerializeField] private float moveSpeedRate;
        [SerializeField] private float attackRangeRate;

        public LegionStatBonus(
            float healthRate,
            float attackRate,
            float defenseRate,
            float attackSpeedRate,
            float moveSpeedRate,
            float attackRangeRate)
        {
            this.healthRate = Mathf.Max(0f, healthRate);
            this.attackRate = Mathf.Max(0f, attackRate);
            this.defenseRate = Mathf.Max(0f, defenseRate);
            this.attackSpeedRate = Mathf.Max(0f, attackSpeedRate);
            this.moveSpeedRate = Mathf.Max(0f, moveSpeedRate);
            this.attackRangeRate = Mathf.Max(0f, attackRangeRate);
        }

        public float HealthRate => healthRate;
        public float AttackRate => attackRate;
        public float DefenseRate => defenseRate;
        public float AttackSpeedRate => attackSpeedRate;
        public float MoveSpeedRate => moveSpeedRate;
        public float AttackRangeRate => attackRangeRate;
    }

    [Serializable]
    public struct UnitStatsSnapshot // 해석이 끝난 전투 능력치
    {
        public float maxHealth;
        public float damage;
        public float defense;
        public float moveSpeed;
        public float attackRange;
        public float attackInterval;
        public float projectileSpeed;
        public bool ranged;

        public float EstimatePower() // 시드 총전투력 추정
        {
            var durability = Mathf.Max(1f, maxHealth) * 0.4f;
            var offense = Mathf.Max(0f, damage) / Mathf.Max(0.1f, attackInterval) * 4f;
            return durability + offense;
        }
    }

    [Serializable]
    public sealed class BattleUnitSnapshot // 유닛 한 기의 전투 사진
    {
        [SerializeField] private string unitId; // 유닛 고정 식별자
        [SerializeField] private UnitStatsSnapshot stats; // 계산 완료 능력치
        [SerializeField] private Color visualTint = Color.white; // 전투 표시 색상
        [SerializeField] private string runtimeAssetKey; // 정식 Monster 실행 자산 키
        [SerializeField] private MonsterRuntimeAssetSet runtimeAssetSet; // 현재 Provider 해석 결과
        [SerializeField] private string[] unlockedAbilityIds; // 현재 돌파에서 해금된 2·4 Ability

        public BattleUnitSnapshot(
            string unitId,
            UnitStatsSnapshot stats,
            Color visualTint = default,
            string runtimeAssetKey = null,
            MonsterRuntimeAssetSet runtimeAssetSet = null,
            string[] unlockedAbilityIds = null)
        {
            this.unitId = unitId;
            this.stats = stats;
            this.visualTint = visualTint.a <= 0f ? Color.white : visualTint;
            this.runtimeAssetKey = runtimeAssetKey ?? string.Empty;
            this.runtimeAssetSet = runtimeAssetSet;
            this.unlockedAbilityIds = unlockedAbilityIds ?? Array.Empty<string>();
        }

        public string UnitId => unitId;
        public UnitStatsSnapshot Stats => stats;
        public Color VisualTint => visualTint.a <= 0f ? Color.white : visualTint;
        public string RuntimeAssetKey => runtimeAssetKey ?? string.Empty;
        public MonsterRuntimeAssetSet RuntimeAssetSet => runtimeAssetSet;
        public string[] UnlockedAbilityIds => unlockedAbilityIds ?? Array.Empty<string>();
    }

    [Serializable]
    public sealed class BattlePartySnapshot // 한 판에 전달할 부대 사진
    {
        [SerializeField] private BattleUnitSnapshot[] units; // 본부대 투입 목록
        [SerializeField] private BattleUnitSnapshot[] reserveUnits; // 예비 투입 순서
        [SerializeField] private float totalPower; // 편성 전체 전투력

        public BattlePartySnapshot(BattleUnitSnapshot[] units)
            : this(units, Array.Empty<BattleUnitSnapshot>())
        {
        }

        public BattlePartySnapshot(BattleUnitSnapshot[] units, BattleUnitSnapshot[] reserveUnits)
        {
            this.units = units ?? Array.Empty<BattleUnitSnapshot>();
            this.reserveUnits = reserveUnits ?? Array.Empty<BattleUnitSnapshot>();
            totalPower = 0f;
            AddPower(this.units);
            AddPower(this.reserveUnits);
        }

        public BattleUnitSnapshot[] Units => units ?? Array.Empty<BattleUnitSnapshot>();
        public BattleUnitSnapshot[] ReserveUnits => reserveUnits ?? Array.Empty<BattleUnitSnapshot>();
        public float TotalPower => totalPower;

        private void AddPower(BattleUnitSnapshot[] partyUnits)
        {
            foreach (var unit in partyUnits)
            {
                if (unit != null)
                {
                    totalPower += unit.Stats.EstimatePower();
                }
            }
        }
    }

    public static class SeedBattlePartySnapshotFactory // 고정 두부 5기 시드 생성
    {
        public static BattlePartySnapshot Create()
        {
            var melee = new UnitStatsSnapshot
            {
                maxHealth = 65f,
                damage = 10f,
                moveSpeed = 2.6f,
                attackRange = 1.05f,
                attackInterval = 0.8f,
                projectileSpeed = 0f,
                ranged = false
            };
            var ranged = new UnitStatsSnapshot
            {
                maxHealth = 48f,
                damage = 8f,
                moveSpeed = 2.35f,
                attackRange = 4.5f,
                attackInterval = 1f,
                projectileSpeed = 9f,
                ranged = true
            };

            return new BattlePartySnapshot(new[]
            {
                new BattleUnitSnapshot("tofu_01", melee),
                new BattleUnitSnapshot("tofu_02", melee),
                new BattleUnitSnapshot("tofu_03", melee),
                new BattleUnitSnapshot("tofu_04", ranged),
                new BattleUnitSnapshot("tofu_05", ranged)
            });
        }
    }
}
