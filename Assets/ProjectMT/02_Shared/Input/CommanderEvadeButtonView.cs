using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Shared.Input
{
    [DisallowMultipleComponent]
    public sealed class CommanderEvadeButtonView : MonoBehaviour // 직접조작 콘텐츠 회피 버튼
    {
        [SerializeField] private CommanderMoveController moveController;
        [SerializeField] private Button evadeButton;
        [SerializeField] private CanvasGroup canvasGroup;

        private void Awake()
        {
            ResolveController();
        }

        private void OnEnable()
        {
            ResolveController();
            if (evadeButton != null)
            {
                evadeButton.onClick.RemoveListener(HandleEvadeClicked);
                evadeButton.onClick.AddListener(HandleEvadeClicked);
            }

            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void OnDisable()
        {
            if (evadeButton != null)
            {
                evadeButton.onClick.RemoveListener(HandleEvadeClicked);
            }
        }

        private void HandleEvadeClicked()
        {
            moveController?.TryEvade();
            Refresh();
        }

        private void Refresh()
        {
            var visible = moveController != null && moveController.IsInputEnabled;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }

            if (evadeButton != null)
            {
                evadeButton.interactable = visible && !moveController.IsEvading;
            }
        }

        private void ResolveController()
        {
            if (moveController == null)
            {
                moveController = transform.root.GetComponentInChildren<CommanderMoveController>(true);
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            CommanderMoveController controller,
            Button button,
            CanvasGroup group)
        {
            if (evadeButton != null)
            {
                evadeButton.onClick.RemoveListener(HandleEvadeClicked);
            }

            moveController = controller;
            evadeButton = button;
            canvasGroup = group;
            if (isActiveAndEnabled && evadeButton != null)
            {
                evadeButton.onClick.AddListener(HandleEvadeClicked);
            }

            Refresh();
        }
#endif
    }
}
