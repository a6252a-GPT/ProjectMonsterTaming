using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Shared.UI
{
    [DisallowMultipleComponent]
    public sealed class ContentClearOverlay : MonoBehaviour // 정산 전 클리어 확인창
    {
        [SerializeField] private TMP_Text titleText; // 클리어 제목
        [SerializeField] private TMP_Text summaryText; // 플레이 결과 요약
        [SerializeField] private TMP_Text rewardText; // 시드 보상 자리
        [SerializeField] private Button confirmButton; // 정산 진행 버튼

        private Action confirmAction; // 확인 뒤 실행할 정산
        private bool confirmed; // 중복 클릭 차단

        public bool IsVisible => gameObject.activeSelf;

        private void Awake()
        {
            confirmButton?.onClick.AddListener(Confirm);
        }

        private void OnDestroy()
        {
            confirmButton?.onClick.RemoveListener(Confirm);
        }

        public bool TryShow(string summary, string reward, Action onConfirm)
        {
            if (titleText == null || summaryText == null || rewardText == null || confirmButton == null ||
                onConfirm == null)
            {
                Debug.LogError("Content clear overlay references are missing.");
                return false;
            }

            titleText.text = "클리어";
            summaryText.text = string.IsNullOrWhiteSpace(summary) ? "콘텐츠 완료" : summary;
            rewardText.text = string.IsNullOrWhiteSpace(reward) ? "보상 연동 예정" : reward;
            confirmAction = onConfirm;
            confirmed = false;
            confirmButton.interactable = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling(); // 현재 UI의 가장 위에 표시
            return true;
        }

        public void Hide()
        {
            confirmAction = null;
            confirmed = false;
            if (confirmButton != null)
            {
                confirmButton.interactable = true;
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void Confirm()
        {
            if (confirmed)
            {
                return;
            }

            confirmed = true;
            confirmButton.interactable = false; // 첫 확인만 접수
            var action = confirmAction;
            confirmAction = null;
            gameObject.SetActive(false);
            action?.Invoke();
        }

#if UNITY_EDITOR
        public void EditorConfigure(TMP_Text title, TMP_Text summary, TMP_Text reward, Button confirm)
        {
            titleText = title;
            summaryText = summary;
            rewardText = reward;
            confirmButton = confirm;
        }
#endif
    }
}
