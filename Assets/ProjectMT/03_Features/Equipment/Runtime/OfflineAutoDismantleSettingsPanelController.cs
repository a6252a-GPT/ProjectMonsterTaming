using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Equipment
{
    [DisallowMultipleComponent]
    public sealed class OfflineAutoDismantleSettingsPanelController : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Button[] policyButtons;
        [SerializeField] private Image[] policyButtonBackgrounds;
        [SerializeField] private GameObject[] selectedMarks;
        [SerializeField] private TMP_Text currentSelectionText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Color normalButtonColor = new Color32(65, 64, 69, 255);
        [SerializeField] private Color selectedButtonColor = new Color32(110, 170, 60, 255);

        private static readonly OfflineAutoDismantlePolicy[] Policies =
        {
            OfflineAutoDismantlePolicy.Off,
            OfflineAutoDismantlePolicy.Common,
            OfflineAutoDismantlePolicy.Rare,
            OfflineAutoDismantlePolicy.Epic
        };

        private IGameProgressService progress;
        private bool requestInFlight;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            closeButton?.onClick.AddListener(Close);
            if (policyButtons == null)
            {
                return;
            }

            for (var index = 0; index < policyButtons.Length && index < Policies.Length; index++)
            {
                var capturedIndex = index;
                policyButtons[index]?.onClick.AddListener(() => SelectPolicy(capturedIndex));
            }
        }

        private void OnEnable()
        {
            Refresh();
        }

        public void Configure(IGameProgressService progressService)
        {
            progress = progressService;
            Refresh();
        }

        public void Open()
        {
            UIPanelPopAnimator.RequestOpen(gameObject);
            transform.SetAsLastSibling();
            Refresh();
        }

        public void Close()
        {
            if (!requestInFlight)
            {
                UIPanelPopAnimator.RequestClose(gameObject);
            }
        }

        private async void SelectPolicy(int index)
        {
            if (requestInFlight || progress == null || !progress.IsLoaded ||
                index < 0 || index >= Policies.Length)
            {
                return;
            }

            var expected = progress.View.Equipment.OfflineAutoDismantlePolicy;
            var next = Policies[index];
            if (expected == next)
            {
                Refresh();
                return;
            }

            requestInFlight = true;
            SetStatus("저장 중...");
            SetInteractable(false);
            var saved = await progress.TryApplyAndSaveAsync(
                GameProgressChange.SetOfflineAutoDismantlePolicy(expected, next));
            if (this == null)
            {
                return; // Scene 전환 중 파괴된 팝업의 지연 완료는 무시
            }

            requestInFlight = false;
            SetStatus(saved ? "설정이 저장되었습니다." : "저장하지 못했습니다. 다시 시도해 주세요.");
            Refresh();
        }

        private void Refresh()
        {
            var policy = progress != null && progress.IsLoaded
                ? progress.View.Equipment.OfflineAutoDismantlePolicy
                : OfflineAutoDismantlePolicy.Common;
            if (currentSelectionText != null)
            {
                currentSelectionText.text =
                    $"현재 설정  ·  {OfflineAutoDismantlePolicyInfo.GetDisplayName(policy)}\n" +
                    "현재 장착보다 좋은 장비는 부위별 1개 보관";
            }

            for (var index = 0; index < Policies.Length; index++)
            {
                var selected = Policies[index] == policy;
                if (selectedMarks != null && index < selectedMarks.Length && selectedMarks[index] != null)
                {
                    selectedMarks[index].SetActive(selected);
                }

                if (policyButtonBackgrounds != null && index < policyButtonBackgrounds.Length &&
                    policyButtonBackgrounds[index] != null)
                {
                    policyButtonBackgrounds[index].color = selected ? selectedButtonColor : normalButtonColor;
                }
            }

            SetInteractable(!requestInFlight && progress != null && progress.IsLoaded);
        }

        private void SetInteractable(bool value)
        {
            if (closeButton != null)
            {
                closeButton.interactable = !requestInFlight;
            }
            if (policyButtons == null)
            {
                return;
            }

            for (var index = 0; index < policyButtons.Length; index++)
            {
                if (policyButtons[index] != null)
                {
                    policyButtons[index].interactable = value;
                }
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message ?? string.Empty;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Button close,
            Button[] buttons,
            Image[] backgrounds,
            GameObject[] marks,
            TMP_Text current,
            TMP_Text status)
        {
            closeButton = close;
            policyButtons = buttons;
            policyButtonBackgrounds = backgrounds;
            selectedMarks = marks;
            currentSelectionText = current;
            statusText = status;
        }
#endif
    }
}
