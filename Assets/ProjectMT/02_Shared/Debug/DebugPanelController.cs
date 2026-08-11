using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Shared.Debugging
{
    [DisallowMultipleComponent]
    public sealed class DebugPanelController : MonoBehaviour // 개발용 공통 도구 패널
    {
        [SerializeField] private GameObject panelRoot; // 접이식 도구 영역
        [SerializeField] private Button toggleButton; // 좌하단 열기 버튼
        [SerializeField] private TMP_Text toggleLabel; // 열림 상태 표시
        [SerializeField] private Button resetSaveButton; // 저장 초기화 버튼
        [SerializeField] private TMP_Text resetSaveLabel; // 확인 단계 표시
        [SerializeField] private Button drawMonsterButton; // 임시 무중복 몬스터 획득
        [SerializeField] private TMP_Text drawMonsterLabel;
        [SerializeField] private Button acquireEquipmentButton; // 임시 장비 획득
        [SerializeField] private TMP_Text acquireEquipmentLabel;
        [SerializeField] private TMP_Text statusLabel; // 실행 결과 표시

        private const float ConfirmDuration = 4f;
        private Func<Task<bool>> resetSaveAction; // AppRoot가 제공하는 초기화 권한
        private Func<Task<string>> drawMonsterAction; // AppRoot가 제공하는 획득·저장 권한
        private Func<Task<string>> acquireEquipmentAction; // AppRoot가 제공하는 장비 획득 권한
        private float confirmUntil;
        private bool isBusy;

        private void Awake()
        {
            toggleButton?.onClick.AddListener(TogglePanel);
            resetSaveButton?.onClick.AddListener(HandleResetSaveClicked);
            drawMonsterButton?.onClick.AddListener(HandleDrawMonsterClicked);
            acquireEquipmentButton?.onClick.AddListener(HandleAcquireEquipmentClicked);
            SetPanelOpen(false);
            ResetConfirmation();
        }

        private void OnDestroy()
        {
            toggleButton?.onClick.RemoveListener(TogglePanel);
            resetSaveButton?.onClick.RemoveListener(HandleResetSaveClicked);
            drawMonsterButton?.onClick.RemoveListener(HandleDrawMonsterClicked);
            acquireEquipmentButton?.onClick.RemoveListener(HandleAcquireEquipmentClicked);
        }

        private void Update()
        {
            if (!isBusy && confirmUntil > 0f && Time.unscaledTime > confirmUntil)
            {
                ResetConfirmation(); // 확인 유효시간 종료
            }
        }

        public void Configure(
            Func<Task<bool>> resetAction,
            Func<Task<string>> monsterDrawAction = null,
            Func<Task<string>> equipmentAcquireAction = null)
        {
            resetSaveAction = resetAction;
            drawMonsterAction = monsterDrawAction;
            acquireEquipmentAction = equipmentAcquireAction;
            if (drawMonsterButton != null)
            {
                drawMonsterButton.interactable = monsterDrawAction != null;
            }

            if (acquireEquipmentButton != null)
            {
                acquireEquipmentButton.interactable = equipmentAcquireAction != null;
            }
        }

        private void TogglePanel()
        {
            var open = panelRoot != null && !panelRoot.activeSelf;
            SetPanelOpen(open);
            if (!open)
            {
                ResetConfirmation();
            }
        }

        private void SetPanelOpen(bool open)
        {
            panelRoot?.SetActive(open);
            if (toggleLabel != null)
            {
                toggleLabel.text = open ? "디버그  -" : "디버그  +";
            }
        }

        private async void HandleResetSaveClicked()
        {
            if (isBusy)
            {
                return;
            }

            if (Time.unscaledTime > confirmUntil)
            {
                confirmUntil = Time.unscaledTime + ConfirmDuration;
                SetResetLabel("초기화 확인");
                SetStatus("다시 누르면 초기화됩니다");
                return;
            }

            isBusy = true;
            SetActionButtonsInteractable(false);
            SetResetLabel("초기화 중...");
            SetStatus(string.Empty);
            try
            {
                var completed = resetSaveAction != null && await resetSaveAction();
                SetStatus(completed ? "초기화 완료" : "현재 초기화할 수 없습니다");
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                SetStatus("초기화 실패");
            }
            finally
            {
                isBusy = false;
                SetActionButtonsInteractable(true);
                ResetConfirmation(false);
            }
        }

        private async void HandleDrawMonsterClicked()
        {
            if (isBusy || drawMonsterAction == null)
            {
                return;
            }

            isBusy = true;
            ResetConfirmation(false);
            SetActionButtonsInteractable(false);
            SetStatus("몬스터 확인 중...");
            try
            {
                var result = await drawMonsterAction();
                SetStatus(string.IsNullOrWhiteSpace(result) ? "현재 뽑을 수 없습니다" : result);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                SetStatus("몬스터 뽑기 실패");
            }
            finally
            {
                isBusy = false;
                SetActionButtonsInteractable(true);
            }
        }

        private async void HandleAcquireEquipmentClicked()
        {
            if (isBusy || acquireEquipmentAction == null)
            {
                return;
            }

            isBusy = true;
            ResetConfirmation(false);
            SetActionButtonsInteractable(false);
            SetStatus("장비 확인 중...");
            try
            {
                var result = await acquireEquipmentAction();
                SetStatus(string.IsNullOrWhiteSpace(result) ? "현재 장비를 받을 수 없습니다" : result);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                SetStatus("장비 획득 실패");
            }
            finally
            {
                isBusy = false;
                SetActionButtonsInteractable(true);
            }
        }

        private void ResetConfirmation(bool clearStatus = true)
        {
            confirmUntil = 0f;
            SetResetLabel("저장 데이터 초기화");
            if (clearStatus)
            {
                SetStatus(string.Empty);
            }
        }

        private void SetResetLabel(string text)
        {
            if (resetSaveLabel != null)
            {
                resetSaveLabel.text = text;
            }
        }

        private void SetStatus(string text)
        {
            if (statusLabel != null)
            {
                statusLabel.text = text;
            }
        }

        private void SetActionButtonsInteractable(bool interactable)
        {
            if (resetSaveButton != null)
            {
                resetSaveButton.interactable = interactable;
            }

            if (drawMonsterButton != null)
            {
                drawMonsterButton.interactable = interactable && drawMonsterAction != null;
            }

            if (acquireEquipmentButton != null)
            {
                acquireEquipmentButton.interactable = interactable && acquireEquipmentAction != null;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            GameObject toolsPanel,
            Button toggle,
            TMP_Text toggleText,
            Button resetButton,
            TMP_Text resetText,
            TMP_Text statusText,
            Button monsterDrawButton = null,
            TMP_Text monsterDrawText = null,
            Button equipmentAcquireButton = null,
            TMP_Text equipmentAcquireText = null)
        {
            panelRoot = toolsPanel;
            toggleButton = toggle;
            toggleLabel = toggleText;
            resetSaveButton = resetButton;
            resetSaveLabel = resetText;
            drawMonsterButton = monsterDrawButton;
            drawMonsterLabel = monsterDrawText;
            acquireEquipmentButton = equipmentAcquireButton;
            acquireEquipmentLabel = equipmentAcquireText;
            statusLabel = statusText;
            SetPanelOpen(false);
            ResetConfirmation();
            if (drawMonsterLabel != null)
            {
                drawMonsterLabel.text = "몬스터 뽑기 (중복 없음)";
            }

            if (acquireEquipmentLabel != null)
            {
                acquireEquipmentLabel.text = "장비 6개 획득";
            }
        }
#endif
    }
}
