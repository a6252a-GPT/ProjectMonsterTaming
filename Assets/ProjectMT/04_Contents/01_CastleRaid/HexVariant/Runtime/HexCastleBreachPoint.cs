using System;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [Serializable]
    public sealed class HexCastleBreachPoint : MonoBehaviour
    {
        [SerializeField] private int defenseLayer;
        [SerializeField] private int direction;
        [SerializeField] private int q;
        [SerializeField] private int r;

        public int DefenseLayer => defenseLayer;
        public int Direction => direction;
        public HexCoordinates Coordinates => new HexCoordinates(q, r);

        public void Configure(int layer, int entryDirection, HexCoordinates coordinates)
        {
            defenseLayer = layer;
            direction = entryDirection;
            q = coordinates.Q;
            r = coordinates.R;
        }
    }
}
