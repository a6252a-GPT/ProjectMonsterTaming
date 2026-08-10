using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum MonsterCombatType // 제작자가 고르는 세 가지 공통 전투 역할
    {
        Melee,
        Ranged,
        Special
    }

    public abstract class MonsterActionDefinition : ScriptableObject // 공격 종류별 작은 실행 데이터
    {
        public abstract MonsterCombatType CombatType { get; }
        public abstract bool TryValidate(out string error);
    }
}
