using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.FallenCommander
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class FallenCommanderExitConfirmationTrigger : MonoBehaviour
    {
        [SerializeField] private FallenCommanderExitConfirmationDialog dialog;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            button.onClick.RemoveListener(OpenDialog);
            button.onClick.AddListener(OpenDialog);
        }

        private void OnDisable()
        {
            button?.onClick.RemoveListener(OpenDialog);
        }

        private void OpenDialog()
        {
            dialog?.Open();
        }
    }
}
