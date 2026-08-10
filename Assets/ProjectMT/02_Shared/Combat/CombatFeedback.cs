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
        void PlayClimax(Vector3 position, CombatClimaxStrength strength); // 주요 처치 파괴 강조 연출
        // 08.07 안건준 추가 - UnitActor가 아닌 대상(건물 등 IDamageable)이 입은 확정 피해를 숫자로 표시.
        // CombatFeedbackPlayer.PlayDamage는 이미 구현되어 있었으나 인터페이스에는 빠져 있어 UnitActor에서
        // 구체 타입 캐스팅 없이 호출할 수 있도록 계약에 추가한다.
        void PlayDamage(Vector3 position, float amount, FloatingNumberStyle style, int mergeKey);
    }

}
