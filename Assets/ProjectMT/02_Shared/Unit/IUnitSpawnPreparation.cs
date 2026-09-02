namespace ProjectMT.Shared.Unit
{
    public interface IUnitSpawnPreparation // UnitActor 초기화 전 동적 외형 준비
    {
        bool PrepareForSpawn(UnitSpawnRequest request);
    }

    public interface IUnitCombatAnimation // 레거시 UnitActor 전투 동작 재생 계약
    {
        void PlayAttack();
        void PlayHit();
        void PlayStun(float duration);
        float PlayDeath();
    }
}
