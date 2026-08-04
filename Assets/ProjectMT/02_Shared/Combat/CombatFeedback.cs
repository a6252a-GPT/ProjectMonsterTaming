using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public enum CombatClimaxStrength // 클라이맥스 연출 강도
    {
        Weak,
        Strong
    }

    public interface ICombatFeedbackPlayer // 전투 연출 공통 계약
    {
        void PlayHit(UnitActor target, DamageReport report); // 피격 연출
        void PlayDeath(UnitActor target, DamageReport report); // 사망 연출
        void PlayClimax(Vector3 position, CombatClimaxStrength strength); // 주요 처치·파괴 강조 연출
    }

}
