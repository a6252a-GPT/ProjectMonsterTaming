using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    public sealed class FogArea : MonoBehaviour
    {
        [SerializeField] private FogAreaState state = FogAreaState.Unexplored;
        [SerializeField] private Transform sourceRoot;
        [SerializeField] private Renderer[] veilRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private bool isCorridor;

        public FogAreaState State => state;
        public Transform SourceRoot => sourceRoot;
        public bool IsCorridor => isCorridor;
        public bool HasBeenVisited => state != FogAreaState.Unexplored;
        public Bounds WorldBounds { get; private set; }

        public void Initialize(Transform areaRoot, Renderer[] veils, Bounds worldBounds, bool corridor)
        {
            sourceRoot = areaRoot;
            veilRenderers = veils ?? System.Array.Empty<Renderer>();
            WorldBounds = worldBounds;
            isCorridor = corridor;
            ApplyState(FogAreaState.Unexplored);
        }

        public void SetState(FogAreaState nextState)
        {
            if (state == nextState)
            {
                return;
            }

            ApplyState(nextState);
        }

        public bool Contains(Vector3 worldPosition)
        {
            Vector3 point = worldPosition;
            point.y = WorldBounds.center.y;
            return WorldBounds.Contains(point);
        }

        private void ApplyState(FogAreaState nextState)
        {
            state = nextState;
            bool visible = state == FogAreaState.Visible;
            Color color = state == FogAreaState.Explored
                ? FogVeilUtility.ExploredColor
                : FogVeilUtility.UnexploredColor;

            for (int i = 0; i < veilRenderers.Length; i++)
            {
                Renderer veil = veilRenderers[i];
                if (veil == null)
                {
                    continue;
                }

                veil.enabled = !visible;
                if (!visible)
                {
                    FogVeilUtility.ApplyColor(veil, color);
                }
            }
        }
    }
}
