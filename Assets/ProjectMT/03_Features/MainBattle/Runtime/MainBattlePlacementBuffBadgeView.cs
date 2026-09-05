using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattlePlacementBuffBadgeView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image accent;
        [SerializeField] private TMP_Text label;

        public Image Background => background;
        public Image Accent => accent;
        public TMP_Text Label => label;
    }
}
