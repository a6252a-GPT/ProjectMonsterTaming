using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public interface ICombatFeedbackPlayer // 전투 연출 공통 계약
    {
        void PlayHit(UnitActor target, DamageReport report); // 피격 연출
        void PlayDeath(UnitActor target, DamageReport report); // 사망 연출
        void PlayClimax(Vector3 position); // 승리 강조 연출
    }

}
