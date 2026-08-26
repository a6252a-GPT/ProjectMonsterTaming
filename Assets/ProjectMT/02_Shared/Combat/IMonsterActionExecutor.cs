using ProjectMT.Shared.Unit;

namespace ProjectMT.Shared.Combat
{
    public readonly struct MonsterActionExecutionContext // Marker 한 번의 고정 실행 입력
    {
        public MonsterActionExecutionContext(
            CombatWorld world,
            UnitActor source,
            IDamageable primaryTarget,
            UnitStatsSnapshot stats,
            MonsterRuntimeAssetSet assetSet,
            MonsterAttackMarker marker,
            MonsterAnimationDriver animationDriver)
        {
            World = world;
            Source = source;
            PrimaryTarget = primaryTarget;
            Stats = stats;
            AssetSet = assetSet;
            Marker = marker;
            AnimationDriver = animationDriver;
        }

        public CombatWorld World { get; }
        public UnitActor Source { get; }
        public IDamageable PrimaryTarget { get; }
        public UnitStatsSnapshot Stats { get; }
        public MonsterRuntimeAssetSet AssetSet { get; }
        public MonsterAttackMarker Marker { get; }
        public MonsterAnimationDriver AnimationDriver { get; }
        public float Damage => Stats.damage *
                               (AssetSet?.CombatProfile?.Action?.BasicAttackProfile == null
                                   ? Marker?.PowerRatio ?? 1f
                                   : 1f);
    }

    public interface IMonsterActionExecutor
    {
        bool Execute(MonsterActionExecutionContext context);
    }
}
