using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.CommanderSkill
{
    [DisallowMultipleComponent]
    public sealed class CommanderSkillHudView : MonoBehaviour // 우측 하단 AUTO·6슬롯 HUD
    {
        [Header("Auto")]
        [SerializeField] private Button autoButton;
        [SerializeField] private Image autoRing;
        [SerializeField] private TMP_Text autoText;
        [SerializeField] private TMP_Text autoStateText;

        [Header("Slots")]
        [SerializeField] private Button[] slotButtons = new Button[CommanderSkillSlotRules.SlotCount];
        [SerializeField] private Image[] slotIcons = new Image[CommanderSkillSlotRules.SlotCount];
        [SerializeField] private Image[] cooldownFills = new Image[CommanderSkillSlotRules.SlotCount];
        [SerializeField] private TMP_Text[] cooldownTexts = new TMP_Text[CommanderSkillSlotRules.SlotCount];
        [SerializeField] private GameObject[] lockRoots = new GameObject[CommanderSkillSlotRules.SlotCount];
        [SerializeField] private Image[] slotRings;

        private static readonly Color AutoEnabledColor = new Color32(222, 213, 178, 255);
        private static readonly Color AutoDisabledColor = new Color32(111, 118, 126, 255);
        private IGameProgressService progress;
        private CommanderSkillCatalog catalog;
        private CommanderSkillRuntime runtime;
        private bool autoSaveRunning;
        private int lifetimeVersion;

        private void OnEnable()
        {
            SubscribeProgress();
            if (progress != null)
            {
                RefreshStaticView();
            }
        }

        private void OnDisable()
        {
            lifetimeVersion++;
            autoSaveRunning = false;
            UnsubscribeProgress();
        }

        public void Configure(
            IGameProgressService progressService,
            CommanderSkillCatalog skillCatalog,
            CommanderSkillRuntime skillRuntime)
        {
            Shutdown();
            progress = progressService;
            catalog = skillCatalog;
            runtime = skillRuntime;
            SubscribeProgress();

            autoButton?.onClick.AddListener(ToggleAutoUse);
            for (var index = 0; index < slotButtons.Length; index++)
            {
                var slotIndex = index;
                slotButtons[index]?.onClick.AddListener(() => runtime?.TryCastSlot(slotIndex));
            }

            RefreshStaticView();
        }

        public void Shutdown()
        {
            lifetimeVersion++;
            UnsubscribeProgress();

            if (autoButton != null)
            {
                autoButton.onClick.RemoveAllListeners();
            }

            if (slotButtons != null)
            {
                for (var index = 0; index < slotButtons.Length; index++)
                {
                    if (slotButtons[index] != null)
                    {
                        slotButtons[index].onClick.RemoveAllListeners();
                    }
                }
            }

            progress = null;
            catalog = null;
            runtime = null;
            autoSaveRunning = false;
        }

        private void Update()
        {
            if (runtime == null || cooldownFills == null || cooldownTexts == null)
            {
                return;
            }

            for (var index = 0; index < CommanderSkillSlotRules.SlotCount; index++)
            {
                var isCasting = runtime.IsCasting && runtime.CastingSlot == index;
                var remaining = isCasting ? runtime.CastingRemaining : runtime.GetCooldownRemaining(index);
                var duration = isCasting ? runtime.CastingDuration : runtime.GetCooldownDuration(index);
                if (index < cooldownFills.Length && cooldownFills[index] != null)
                {
                    cooldownFills[index].fillAmount = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
                    cooldownFills[index].gameObject.SetActive(remaining > 0.02f);
                }

                if (index < cooldownTexts.Length && cooldownTexts[index] != null)
                {
                    cooldownTexts[index].text = remaining > 0.02f
                        ? isCasting
                            ? $"시전\n{remaining:0.0}"
                            : (remaining >= 10f ? Mathf.CeilToInt(remaining).ToString() : remaining.ToString("0.0"))
                        : string.Empty;
                }
            }
        }

        private async void ToggleAutoUse()
        {
            var progressService = progress;
            if (progressService == null || autoSaveRunning)
            {
                return;
            }

            var requestVersion = lifetimeVersion;
            var current = progressService.View.CommanderSkills.AutoUseEnabled;
            autoSaveRunning = true;
            if (autoButton != null)
            {
                autoButton.interactable = false;
            }

            try
            {
                await progressService.TryApplyAndSaveAsync(
                    GameProgressChange.SetCommanderSkillAutoUse(current, !current));
            }
            finally
            {
                if (IsCurrentRequest(requestVersion))
                {
                    autoSaveRunning = false;
                    if (autoButton != null)
                    {
                        autoButton.interactable = true;
                    }

                    RefreshStaticView();
                }
            }
        }

        private void RefreshStaticView()
        {
            if (this == null)
            {
                return;
            }

            if (progress == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            var view = progress.View.CommanderSkills;
            var autoColor = view.AutoUseEnabled ? AutoEnabledColor : AutoDisabledColor;
            if (autoRing != null)
            {
                autoRing.color = autoColor;
            }

            if (autoText != null)
            {
                autoText.color = autoColor;
                autoText.text = autoStateText != null ? "AUTO" : view.AutoUseEnabled ? "AUTO" : "OFF";
            }

            if (autoStateText != null)
            {
                autoStateText.color = autoColor;
                autoStateText.text = view.AutoUseEnabled ? "ON" : "OFF";
            }

            for (var index = 0; index < CommanderSkillSlotRules.SlotCount; index++)
            {
                var unlocked = view.IsSlotUnlocked(index);
                var skillId = view.GetEquippedSkillId(index);
                CommanderSkillDefinition definition = null;
                var hasSkill = catalog != null && catalog.TryGet(skillId, out definition);
                if (slotRings != null && index < slotRings.Length && slotRings[index] != null)
                {
                    slotRings[index].color = unlocked && hasSkill
                        ? new Color32(222, 199, 144, 255)
                        : new Color32(96, 117, 122, 160);
                }
                if (index < lockRoots.Length && lockRoots[index] != null)
                {
                    lockRoots[index].SetActive(!unlocked);
                }

                if (index < slotIcons.Length && slotIcons[index] != null)
                {
                    slotIcons[index].sprite = hasSkill ? definition.Icon : null;
                    slotIcons[index].enabled = hasSkill;
                    slotIcons[index].color = unlocked ? Color.white : new Color(0.28f, 0.3f, 0.34f, 0.72f);
                }

                if (index < slotButtons.Length && slotButtons[index] != null)
                {
                    slotButtons[index].interactable = unlocked && hasSkill;
                }
            }
        }

        private bool IsCurrentRequest(int requestVersion)
        {
            return this != null && isActiveAndEnabled && requestVersion == lifetimeVersion;
        }

        private void SubscribeProgress()
        {
            if (progress == null || !isActiveAndEnabled)
            {
                return;
            }

            progress.Changed -= RefreshStaticView;
            progress.Changed += RefreshStaticView;
        }

        private void UnsubscribeProgress()
        {
            if (progress != null)
            {
                progress.Changed -= RefreshStaticView;
            }
        }

        private void OnDestroy()
        {
            Shutdown();
        }

#if UNITY_EDITOR
        public void EditorConfigurePresentation(TMP_Text autoState, Image[] rings)
        {
            autoStateText = autoState;
            slotRings = rings;
        }

        public void EditorConfigure(
            Button autoToggle,
            Image autoBorder,
            TMP_Text autoLabel,
            Button[] buttons,
            Image[] icons,
            Image[] fills,
            TMP_Text[] timerTexts,
            GameObject[] locks)
        {
            autoButton = autoToggle;
            autoRing = autoBorder;
            autoText = autoLabel;
            slotButtons = buttons;
            slotIcons = icons;
            cooldownFills = fills;
            cooldownTexts = timerTexts;
            lockRoots = locks;
        }
#endif
    }
}
