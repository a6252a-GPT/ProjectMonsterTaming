using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleWallVisualModule : MonoBehaviour // 성벽 모델의 순수 소켓 정보다
    {
        [SerializeField] private HexCastleWallVisualKind visualKind;
        [SerializeField, Range(0, 5)] private int sourceStartDirection = 3;
        [SerializeField, Range(0, 5)] private int sourceEndDirection;
        [SerializeField] private Bounds localBounds;
        [SerializeField] private Transform startSocket;
        [SerializeField] private Transform endSocket;
        [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();

        public HexCastleWallVisualKind VisualKind => visualKind;
        public int SourceStartDirection => sourceStartDirection;
        public int SourceEndDirection => sourceEndDirection;
        public Bounds LocalBounds => localBounds;
        public Transform StartSocket => startSocket;
        public Transform EndSocket => endSocket;
        public IReadOnlyList<Renderer> Renderers => renderers;

        public bool HasValidSocketContract(float tolerance = 0.0001f)
        {
            if (startSocket == null || endSocket == null ||
                startSocket.parent != transform || endSocket.parent != transform)
            {
                return false;
            }

            return Vector3.Distance(
                       startSocket.localPosition,
                       HexSpatialContract.GetEdgeMidpoint(sourceStartDirection)) <= tolerance &&
                   Vector3.Distance(
                       endSocket.localPosition,
                       HexSpatialContract.GetEdgeMidpoint(sourceEndDirection)) <= tolerance;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            HexCastleWallVisualKind kind,
            int startDirection,
            int endDirection,
            Bounds bounds,
            Transform targetStartSocket,
            Transform targetEndSocket,
            Renderer[] targetRenderers)
        {
            visualKind = kind;
            sourceStartDirection = PositiveModulo(startDirection, 6);
            sourceEndDirection = PositiveModulo(endDirection, 6);
            localBounds = bounds;
            startSocket = targetStartSocket;
            endSocket = targetEndSocket;
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
