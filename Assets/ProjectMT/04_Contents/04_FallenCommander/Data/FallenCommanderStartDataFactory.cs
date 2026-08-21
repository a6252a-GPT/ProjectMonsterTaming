using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    // 공통 Factory 규격을 유지하되, 타락한 과거의 군단장은 편성을 소비하지 않는 군단장 단독 보스전이다.
    [CreateAssetMenu(
        menuName = "ProjectMT/Content/Fallen Commander/Start Data Factory",
        fileName = "FallenCommanderStartDataFactory")]
    public sealed class FallenCommanderStartDataFactory : ContentStartDataFactory
    {
        public override IContentStartData Create(BattlePartySnapshot party)
        {
            return new FallenCommanderStartData();
        }
    }
}
