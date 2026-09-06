using System;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleBakedStage : MonoBehaviour // 승인 Layout의 정식 Stage 루트
    {
        [SerializeField] private HexCastleStageLayout layout;
        [SerializeField] private Bounds worldBounds;
        [SerializeField] private int blockedCellCount;

        public HexCastleStageLayout Layout => layout;
        public Bounds WorldBounds => worldBounds;
        public int BlockedCellCount => blockedCellCount;
        public bool IsComplete =>
            layout != null && blockedCellCount > 0;

#if UNITY_EDITOR
        public void EditorConfigure(
            HexCastleStageLayout approvedLayout,
            Bounds bounds)
        {
            layout = approvedLayout != null
                ? approvedLayout
                : throw new ArgumentNullException(nameof(approvedLayout));
            worldBounds = bounds;
            blockedCellCount = layout.BuildLayout().Cells.Values.Count(value => value.InitialBlocked);
        }
#endif
    }
}
