using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [CreateAssetMenu(
        menuName = "ProjectMT/Castle Raid Hex/Start Data Factory",
        fileName = "HexCastleRaidStartDataFactory")]
    public sealed class HexCastleRaidStartDataFactory : ContentStartDataFactory
    {
        public override IContentStartData Create(BattlePartySnapshot party)
        {
            return new HexCastleRaidStartData(party);
        }
    }
}
