using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.GiantSpellbook
{
    // ContentFlow의 공통 Factory 규격은 유지하되, 거대 마도서는 편성을 소비하지 않는 군단장 단독 보스전이다.
    [CreateAssetMenu(
        menuName = "ProjectMT/Content/Giant Spellbook/Start Data Factory",
        fileName = "GiantSpellbookStartDataFactory")]
    public sealed class GiantSpellbookStartDataFactory : ContentStartDataFactory
    {
        public override IContentStartData Create(BattlePartySnapshot party)
        {
            return new GiantSpellbookStartData();
        }
    }
}
