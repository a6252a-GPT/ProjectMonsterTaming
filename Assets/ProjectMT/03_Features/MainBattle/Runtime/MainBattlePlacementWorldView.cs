using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattlePlacementWorldView : MonoBehaviour
    {
        [SerializeField] private LineRenderer hexTemplate;
        [SerializeField] private LineRenderer ringTemplate;
        [SerializeField] private LineRenderer selectedHex;

        public LineRenderer HexTemplate => hexTemplate;
        public LineRenderer RingTemplate => ringTemplate;
        public LineRenderer SelectedHex => selectedHex;
    }
}
