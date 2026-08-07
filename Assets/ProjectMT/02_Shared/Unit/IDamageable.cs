using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    // 08.07 안건준 추가 - UnitActor가 아닌 대상(예: 수호자의 탑 방어 건물처럼 자체 이동·탐색 로직이 없는
    // 정적 구조물)도 유닛의 "강제 지정 공격 대상"이 될 수 있도록 만든 최소 계약.
    // 이 인터페이스를 아무도 사용하지 않으면 기존 유닛 간 전투(FindNearestOpponent 기반) 로직에는
    // 전혀 영향이 없다.
    public interface IDamageable
    {
        bool IsAlive { get; }
        Vector3 Position { get; }
        // 08.07 안건준 수정 - 적을 공격할 때와 동일하게 "실제로 깎인 체력"을 화면에 표시할 수 있도록
        // 반환값을 void에서 float(실제 적용된 피해량)로 변경했다. 이 인터페이스는 HealthComponent만
        // 구현하고 있어 다른 던전 코드에는 영향이 없다.
        float ReceiveDamage(UnitActor source, float amount);
    }
}
