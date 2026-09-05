using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattleFormationPlacementHudView : MonoBehaviour
    {
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private MainBattlePlacementDimGraphic dim;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text unsavedLabel;

        public RectTransform SafeArea => safeArea;
        internal MainBattlePlacementDimGraphic Dim => dim;
        public Button SaveButton => saveButton;
        public Button ResetButton => resetButton;
        public TMP_Text StatusLabel => statusLabel;
        public TMP_Text UnsavedLabel => unsavedLabel;
    }
}
