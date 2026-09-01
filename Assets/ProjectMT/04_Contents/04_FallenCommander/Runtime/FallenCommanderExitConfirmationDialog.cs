using System;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.FallenCommander
{
    [DisallowMultipleComponent]
    public sealed class FallenCommanderExitConfirmationDialog : MonoBehaviour
    {
        [SerializeField] private Button giveUpButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button closeButton;

        public event Action GiveUpRequested;
        public event Action RetryRequested;

        public void Initialize()
        {
            giveUpButton?.onClick.RemoveListener(HandleGiveUp);
            giveUpButton?.onClick.AddListener(HandleGiveUp);
            retryButton?.onClick.RemoveListener(HandleRetry);
            retryButton?.onClick.AddListener(HandleRetry);
            closeButton?.onClick.RemoveListener(Close);
            closeButton?.onClick.AddListener(Close);
            Close();
        }

        public void Release()
        {
            giveUpButton?.onClick.RemoveListener(HandleGiveUp);
            retryButton?.onClick.RemoveListener(HandleRetry);
            closeButton?.onClick.RemoveListener(Close);
            GiveUpRequested = null;
            RetryRequested = null;
            Close();
        }

        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            Release();
        }

        private void HandleGiveUp()
        {
            GiveUpRequested?.Invoke();
        }

        private void HandleRetry()
        {
            RetryRequested?.Invoke();
        }
    }
}
