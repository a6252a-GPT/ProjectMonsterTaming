using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleWallStubVisualModule : MonoBehaviour // 타워 중심과 Cell Edge 사이의 짧은 순수 Visual 성벽이다
    {
        [SerializeField, Range(0, 5)] private int sourceDirection;
        [SerializeField, Min(0.01f)] private float towerJoinRadius = 0.4f;
        [SerializeField] private Bounds localBounds;
        [SerializeField] private Transform edgeSocket;
        [SerializeField] private Transform towerSocket;
        [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();

        public int SourceDirection => sourceDirection;
        public float TowerJoinRadius => towerJoinRadius;
        public Bounds LocalBounds => localBounds;
        public Transform EdgeSocket => edgeSocket;
        public Transform TowerSocket => towerSocket;
        public IReadOnlyList<Renderer> Renderers => renderers;

        public bool HasValidSocketContract(float tolerance = 0.0001f)
        {
            if (edgeSocket == null || towerSocket == null ||
                edgeSocket.parent != transform || towerSocket.parent != transform)
            {
                return false;
            }

            var direction = HexSpatialContract.ToWorld(
                HexCoordinates.Directions[sourceDirection]).normalized;
            return Vector3.Distance(
                       edgeSocket.localPosition,
                       HexSpatialContract.GetEdgeMidpoint(sourceDirection)) <= tolerance &&
                   Vector3.Distance(
                       towerSocket.localPosition,
                       direction * towerJoinRadius) <= tolerance;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            int direction,
            float joinRadius,
            Bounds bounds,
            Transform targetEdgeSocket,
            Transform targetTowerSocket,
            Renderer[] targetRenderers)
        {
            sourceDirection = PositiveModulo(direction, HexCoordinates.Directions.Length);
            towerJoinRadius = Mathf.Max(0.01f, joinRadius);
            localBounds = bounds;
            edgeSocket = targetEdgeSocket;
            towerSocket = targetTowerSocket;
            renderers = targetRenderers ?? Array.Empty<Renderer>();
        }
#endif

        private static int PositiveModulo(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
