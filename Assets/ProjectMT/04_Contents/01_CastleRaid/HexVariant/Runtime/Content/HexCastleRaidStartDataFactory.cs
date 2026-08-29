using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectMT.Contents.CastleRaidHex
{
    [CreateAssetMenu(
        menuName = "ProjectMT/Castle Raid Hex/Start Data Factory",
        fileName = "HexCastleRaidStartDataFactory")]
    public sealed class HexCastleRaidStartDataFactory : ContentStartDataFactory
    {
        [FormerlySerializedAs("deploymentLimit")]
        [SerializeField, Range(1, 3)] private int summonsPerSlot = 3;

        public override IContentStartData Create(BattlePartySnapshot party)
        {
            return new HexCastleRaidStartData(party, summonsPerSlot);
        }
    }
}
