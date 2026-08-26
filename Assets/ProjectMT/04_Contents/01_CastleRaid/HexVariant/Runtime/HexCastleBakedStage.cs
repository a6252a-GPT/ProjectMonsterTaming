using System;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleBakedStage : MonoBehaviour // 승인 Layout과 영구 NavMesh의 정식 Stage 루트
    {
        [SerializeField] private HexCastleStageLayout layout;
        [SerializeField] private NavMeshSurface navigationSurface;
        [SerializeField] private Bounds worldBounds;
        [SerializeField] private int blockedCellCount;

        public HexCastleStageLayout Layout => layout;
        public NavMeshSurface NavigationSurface => navigationSurface;
        public Bounds WorldBounds => worldBounds;
        public int BlockedCellCount => blockedCellCount;
        public bool IsComplete =>
            layout != null && navigationSurface != null && navigationSurface.navMeshData != null &&
            blockedCellCount > 0;

#if UNITY_EDITOR
        public void EditorConfigure(
            HexCastleStageLayout approvedLayout,
            NavMeshSurface surface,
            Bounds bounds)
        {
            layout = approvedLayout != null
                ? approvedLayout
                : throw new ArgumentNullException(nameof(approvedLayout));
            navigationSurface = surface != null
                ? surface
                : throw new ArgumentNullException(nameof(surface));
            worldBounds = bounds;
            blockedCellCount = layout.BuildLayout().Cells.Values.Count(value => value.InitialBlocked);
        }
#endif
    }
}
