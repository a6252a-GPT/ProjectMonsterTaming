using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    public enum HexCastleWallVisualKind
    {
        Straight,
        CornerAInside,
        CornerAOutside,
        CornerBInside,
        CornerBOutside,
        StraightGate,
        CornerAGate
    }

    public enum HexCastleStructureVisualKind
    {
        Palace,
        Building,
        DefenseBuilding,
        RewardBuilding
    }

    [DisallowMultipleComponent]
    public sealed class HexCastleTileVisualModule : MonoBehaviour // 타일 모델의 순수 Bounds 정보다
    {
        [SerializeField] private Bounds localBounds;
        [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();

        public Bounds LocalBounds => localBounds;
        public IReadOnlyList<Renderer> Renderers => renderers;

#if UNITY_EDITOR
        public void EditorConfigure(Bounds bounds, Renderer[] targetRenderers)
        {
            localBounds = bounds;
            renderers = targetRenderers ?? Array.Empty<Renderer>();
        }
#endif
    }

    [DisallowMultipleComponent]
    public sealed class HexCastleTowerVisualModule : MonoBehaviour // 탑 모델의 순수 Bounds 정보다
    {
        [SerializeField] private Bounds localBounds;
        [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();

        public Bounds LocalBounds => localBounds;
        public IReadOnlyList<Renderer> Renderers => renderers;

#if UNITY_EDITOR
        public void EditorConfigure(Bounds bounds, Renderer[] targetRenderers)
        {
            localBounds = bounds;
            renderers = targetRenderers ?? Array.Empty<Renderer>();
        }
#endif
    }

    [DisallowMultipleComponent]
    public sealed class HexCastleStructureVisualModule : MonoBehaviour // 건물 모델의 순수 Bounds 정보다
    {
        [SerializeField] private HexCastleStructureVisualKind visualKind;
        [SerializeField] private Bounds localBounds;
        [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();

        public HexCastleStructureVisualKind VisualKind => visualKind;
        public Bounds LocalBounds => localBounds;
        public IReadOnlyList<Renderer> Renderers => renderers;

#if UNITY_EDITOR
        public void EditorConfigure(
            HexCastleStructureVisualKind kind,
            Bounds bounds,
            Renderer[] targetRenderers)
        {
            visualKind = kind;
            localBounds = bounds;
            renderers = targetRenderers ?? Array.Empty<Renderer>();
        }
#endif
    }
}
