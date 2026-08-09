namespace ProjectMT.Shared.Unit
{
    public interface IUnitSpawnPreparation // UnitActor 초기화 전 동적 외형 준비
    {
        bool PrepareForSpawn(UnitSpawnRequest request);
    }
}
