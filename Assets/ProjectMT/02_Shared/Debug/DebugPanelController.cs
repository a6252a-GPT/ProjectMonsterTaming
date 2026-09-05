using System;
using System.Linq;
using ProjectMT.Shared.Unit;
using System.Threading.Tasks;
using ProjectMT.Shared.Combat;
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
        [SerializeField] private Button acquireAllItemsButton; // 일반 아이템 전체 획득
        [SerializeField] private TMP_Text acquireAllItemsLabel;
        [SerializeField] private Button sendRandomMailButton; // 보상 첨부 테스트 우편 발송
        [SerializeField] private TMP_Text sendRandomMailLabel;
        [SerializeField] private Button basicAttackAreaButton; // 기본공격 판정 표시 전환
        [SerializeField] private TMP_Text basicAttackAreaLabel;
        [SerializeField] private TMP_Text statusLabel; // 실행 결과 표시

        private const float ConfirmDuration = 4f;
        private Func<Task<bool>> resetSaveAction; // AppRoot가 제공하는 초기화 권한
        private Func<Task<string>> drawMonsterAction; // AppRoot가 제공하는 획득·저장 권한
        private Func<Task<string>> acquireEquipmentAction; // AppRoot가 제공하는 장비 획득 권한
        private Func<Task<string>> acquireAllItemsAction; // AppRoot가 제공하는 아이템 획득 권한
        private Func<Task<string>> sendRandomMailAction; // AppRoot가 제공하는 우편 발송·저장 권한
        private float confirmUntil;
        private bool isBusy;

        private void Awake()
        {
            toggleButton?.onClick.AddListener(TogglePanel);
            resetSaveButton?.onClick.AddListener(HandleResetSaveClicked);
            drawMonsterButton?.onClick.AddListener(HandleDrawMonsterClicked);
            acquireEquipmentButton?.onClick.AddListener(HandleAcquireEquipmentClicked);
            acquireAllItemsButton?.onClick.AddListener(HandleAcquireAllItemsClicked);
            sendRandomMailButton?.onClick.AddListener(HandleSendRandomMailClicked);
            basicAttackAreaButton?.onClick.AddListener(HandleBasicAttackAreaClicked);
            BuildFocusComparison();
            SetPanelOpen(false);
            ResetConfirmation();
            RefreshBasicAttackAreaLabel();
        }

        private void OnDestroy()
        {
            toggleButton?.onClick.RemoveListener(TogglePanel);
            resetSaveButton?.onClick.RemoveListener(HandleResetSaveClicked);
            drawMonsterButton?.onClick.RemoveListener(HandleDrawMonsterClicked);
            acquireEquipmentButton?.onClick.RemoveListener(HandleAcquireEquipmentClicked);
            acquireAllItemsButton?.onClick.RemoveListener(HandleAcquireAllItemsClicked);
            sendRandomMailButton?.onClick.RemoveListener(HandleSendRandomMailClicked);
            basicAttackAreaButton?.onClick.RemoveListener(HandleBasicAttackAreaClicked);
        }

        private void Update()
        {
            if (repeatFocusPreview && Time.unscaledTime >= nextFocusPreview) TryPreviewFocus();
            if (!isBusy && confirmUntil > 0f && Time.unscaledTime > confirmUntil)
            {
                ResetConfirmation(); // 확인 유효시간 종료
            }
        }

        public void Configure(
            Func<Task<bool>> resetAction,
            Func<Task<string>> monsterDrawAction = null,
            Func<Task<string>> equipmentAcquireAction = null,
            Func<Task<string>> allItemsAcquireAction = null,
            Func<Task<string>> randomMailSendAction = null)
        {
            resetSaveAction = resetAction;
            drawMonsterAction = monsterDrawAction;
            acquireEquipmentAction = equipmentAcquireAction;
            acquireAllItemsAction = allItemsAcquireAction;
            sendRandomMailAction = randomMailSendAction;
            if (drawMonsterButton != null)
            {
                drawMonsterButton.interactable = monsterDrawAction != null;
            }

            if (acquireEquipmentButton != null)
            {
                acquireEquipmentButton.interactable = equipmentAcquireAction != null;
            }

            if (acquireAllItemsButton != null)
            {
                acquireAllItemsButton.interactable = allItemsAcquireAction != null;
            }

            if (sendRandomMailButton != null)
            {
                sendRandomMailButton.interactable = randomMailSendAction != null;
            }
        }


        private readonly Button[] focusStyleButtons = new Button[7];
        private TMP_Text focusCasterLabel;
        private TMP_Text focusRepeatLabel;
        private UnitActor focusPreviewCaster;
        private bool repeatFocusPreview;
        private float nextFocusPreview;

        private void BuildFocusComparison()
        {
            if (panelRoot == null || basicAttackAreaButton == null) return;
            var root = new GameObject("ActiveFocusComparison", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(panelRoot.transform, false);
            root.GetComponent<Image>().color = new Color(0.035f, 0.055f, 0.085f, 0.97f);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-12f, 0f);
            rect.sizeDelta = new Vector2(330f, 498f);
            var heading = Instantiate(basicAttackAreaLabel, rect);
            heading.name = "FocusComparisonTitle";
            heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            heading.rectTransform.pivot = new Vector2(0.5f, 1f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            heading.rectTransform.sizeDelta = new Vector2(302f, 28f);
            heading.text = "액티브 연출 비교";
            heading.color = new Color(1f, 0.85f, 0.4f);
            for (var i = 0; i < focusStyleButtons.Length; i++)
            {
                var index = i;
                focusStyleButtons[i] = CreateFocusButton(rect, "FocusStyle" + i, -44f - i * 42f,
                    () => { MonsterActiveFocusStyles.Current = (MonsterActiveFocusStyle)index; RefreshFocusComparison(); });
            }
            var select = CreateFocusButton(rect, "FocusCaster", -344f, SelectNextFocusCaster);
            focusCasterLabel = select.GetComponentInChildren<TMP_Text>();
            var preview = CreateFocusButton(rect, "FocusPreview", -386f, () =>
            {
                TryPreviewFocus(); SetPanelOpen(false);
            });
            preview.GetComponentInChildren<TMP_Text>().text = "선택 몬스터 스킬 발동";
            var repeat = CreateFocusButton(rect, "FocusRepeat", -428f, () =>
            {
                repeatFocusPreview = !repeatFocusPreview;
                nextFocusPreview = 0f;
                RefreshFocusComparison();
                if (repeatFocusPreview) SetPanelOpen(false);
            });
            focusRepeatLabel = repeat.GetComponentInChildren<TMP_Text>();
            RefreshFocusComparison();
        }

        private Button CreateFocusButton(Transform parent, string name, float y, UnityEngine.Events.UnityAction action)
        {
            var button = Instantiate(basicAttackAreaButton, parent);
            button.name = name;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
            button.interactable = true;
            button.image.color = new Color(0.08f, 0.13f, 0.20f);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(302f, 36f);
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.fontSize = 19f;
            return button;
        }

        private UnitActor[] FocusCandidates() => FindObjectsByType<UnitActor>(FindObjectsSortMode.None)
            .Where(unit => unit.Team == UnitTeam.Player && unit.IsAlive && unit.IsCombatReady &&
                unit.SkillRuntime.ActiveSkill != null)
            .OrderBy(unit => unit.ActiveFocusPartySlotIndex).ToArray();

        private void SelectNextFocusCaster()
        {
            var candidates = FocusCandidates();
            if (candidates.Length == 0) { focusPreviewCaster = null; RefreshFocusComparison(); return; }
            var index = Array.IndexOf(candidates, focusPreviewCaster);
            focusPreviewCaster = candidates[(index + 1) % candidates.Length];
            RefreshFocusComparison();
        }

        private bool TryPreviewFocus()
        {
            if (focusPreviewCaster == null || !focusPreviewCaster.IsAlive ||
                !focusPreviewCaster.gameObject.activeInHierarchy) focusPreviewCaster = FocusCandidates().FirstOrDefault();
            if (focusPreviewCaster == null) { SetStatus("전투 중인 몬스터가 없습니다"); return false; }
            if (focusPreviewCaster.SkillRuntime.IsExecuting || focusPreviewCaster.SkillRuntime.IsActiveFocusQueued)
                return false;
            focusPreviewCaster.SkillRuntime.GrantActiveEnergy(focusPreviewCaster.SkillRuntime.EnergyCapacity);
            nextFocusPreview = Time.unscaledTime + 3f;
            RefreshFocusComparison();
            return true;
        }

        private void RefreshFocusComparison()
        {
            for (var i = 0; i < focusStyleButtons.Length; i++)
            {
                var button = focusStyleButtons[i];
                if (button == null) continue;
                var selected = (int)MonsterActiveFocusStyles.Current == i;
                button.GetComponentInChildren<TMP_Text>().text =
                    (selected ? "● " : "") + MonsterActiveFocusStyles.Labels[i];
                button.image.color = selected ? new Color(0.32f, 0.23f, 0.055f) : new Color(0.08f, 0.13f, 0.20f);
            }
            if (focusCasterLabel != null)
                focusCasterLabel.text = "대상: " + (focusPreviewCaster != null ? focusPreviewCaster.DisplayName : "첫 번째 몬스터") + " ›";
            if (focusRepeatLabel != null) focusRepeatLabel.text = repeatFocusPreview ? "반복 발동: ON · 누르면 종료" : "반복 발동: OFF";
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

        private async void HandleAcquireAllItemsClicked()
        {
            if (isBusy || acquireAllItemsAction == null)
            {
                return;
            }

            isBusy = true;
            ResetConfirmation(false);
            SetActionButtonsInteractable(false);
            SetStatus("아이템 확인 중...");
            try
            {
                var result = await acquireAllItemsAction();
                SetStatus(string.IsNullOrWhiteSpace(result) ? "현재 아이템을 받을 수 없습니다" : result);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                SetStatus("아이템 획득 실패");
            }
            finally
            {
                isBusy = false;
                SetActionButtonsInteractable(true);
            }
        }

        private async void HandleSendRandomMailClicked()
        {
            if (isBusy || sendRandomMailAction == null)
            {
                return;
            }

            isBusy = true;
            ResetConfirmation(false);
            SetActionButtonsInteractable(false);
            SetStatus("우편 생성 중...");
            try
            {
                var result = await sendRandomMailAction();
                SetStatus(string.IsNullOrWhiteSpace(result) ? "현재 우편을 보낼 수 없습니다" : result);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                SetStatus("우편 발송 실패");
            }
            finally
            {
                isBusy = false;
                SetActionButtonsInteractable(true);
            }
        }

        private void HandleBasicAttackAreaClicked()
        {
            var visible = !CombatWorld.MonsterBasicAttackHitAreasVisible;
            CombatWorld.SetMonsterBasicAttackHitAreasVisible(visible);
            RefreshBasicAttackAreaLabel();
            SetStatus(visible ? "기본공격 판정 표시 ON" : "기본공격 판정 표시 OFF");
        }

        private void RefreshBasicAttackAreaLabel()
        {
            if (basicAttackAreaLabel != null)
            {
                basicAttackAreaLabel.text = CombatWorld.MonsterBasicAttackHitAreasVisible
                    ? "기본공격 판정: ON"
                    : "기본공격 판정: OFF";
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

            if (acquireAllItemsButton != null)
            {
                acquireAllItemsButton.interactable = interactable && acquireAllItemsAction != null;
            }

            if (sendRandomMailButton != null)
            {
                sendRandomMailButton.interactable = interactable && sendRandomMailAction != null;
            }

            if (basicAttackAreaButton != null)
            {
                basicAttackAreaButton.interactable = interactable;
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
            TMP_Text equipmentAcquireText = null,
            Button allItemsAcquireButton = null,
            TMP_Text allItemsAcquireText = null,
            Button randomMailButton = null,
            TMP_Text randomMailText = null,
            Button attackAreaButton = null,
            TMP_Text attackAreaText = null)
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
            acquireAllItemsButton = allItemsAcquireButton;
            acquireAllItemsLabel = allItemsAcquireText;
            sendRandomMailButton = randomMailButton;
            sendRandomMailLabel = randomMailText;
            basicAttackAreaButton = attackAreaButton;
            basicAttackAreaLabel = attackAreaText;
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

            if (acquireAllItemsLabel != null)
            {
                acquireAllItemsLabel.text = "모든 아이템 1개씩 획득";
            }

            if (sendRandomMailLabel != null)
            {
                sendRandomMailLabel.text = "랜덤 우편 보내기";
            }

            RefreshBasicAttackAreaLabel();
        }
#endif
    }
}
