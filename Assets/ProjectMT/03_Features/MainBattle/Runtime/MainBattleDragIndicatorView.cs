using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattleDragIndicatorView : MonoBehaviour
    {
        [SerializeField] private LineRenderer selectionMarker;
        [SerializeField] private LineRenderer destinationRing;

        public LineRenderer SelectionMarker => selectionMarker;
        public LineRenderer DestinationRing => destinationRing;
    }
}
