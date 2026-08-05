using System;
using ProjectMT.Contents.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class ContentFinishFeedbackPresenter : MonoBehaviour, IContentFinishFeedback // 저장 실패 공통 재시도 UI
    {
        [SerializeField] private GameObject panelRoot; // 입력을 막는 중앙 안내 패널
        [SerializeField] private TMP_Text messageText; // 저장 상태 문구
        [SerializeField] private Button retryButton; // 실패 뒤에만 표시

        private Action retry; // 현재 실패 건의 단일 재시도

        private void Awake()
        {
            retryButton?.onClick.AddListener(HandleRetry);
            Hide();
        }

        private void OnDestroy()
        {
            retryButton?.onClick.RemoveListener(HandleRetry);
        }

        public void ShowSaving()
        {
            retry = null;
            SetMessage("진행 정보를 저장하는 중입니다.");
            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(false);
            }

            panelRoot?.SetActive(true);
        }

        public void ShowSaveFailed(Action retryAction)
        {
            retry = retryAction;
            SetMessage("진행 정보를 저장하지 못했습니다.");
            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(true);
                retryButton.interactable = retryAction != null;
            }

            panelRoot?.SetActive(true);
        }

        public void Hide()
        {
            retry = null;
            panelRoot?.SetActive(false);
        }

        private void HandleRetry()
        {
            var retryAction = retry;
            retry = null; // 연속 터치 차단
            if (retryButton != null)
            {
                retryButton.interactable = false;
            }

            retryAction?.Invoke();
        }

        private void SetMessage(string message)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(GameObject root, TMP_Text message, Button button)
        {
            panelRoot = root;
            messageText = message;
            retryButton = button;
        }
#endif
    }
}
