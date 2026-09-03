using System;
using ProjectMT.Features.WorldDrops;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleBattleHudView : MonoBehaviour
    {
        [Serializable]
        public sealed class DeploymentSlot
        {
            public RectTransform Root;
            public Image Portrait;
            public TMP_Text Count;
            public Image Selection;
            public CanvasGroup Visual;
            public Image Background;
            public Image RarityBorder;
            public Image RarityHighlight;
        }

        [Serializable]
        public sealed class DeploymentRarityStyle
        {
            public MonsterRarity Rarity;
            public Color Background;
            public Color Border;
            public Color Highlight;
        }

        public const float DeploymentSlotSpacing = 172f;

        [Header("Deployment Cards")]
        [SerializeField] private RectTransform deploymentDock;
        [SerializeField] private DeploymentSlot[] deploymentSlots = Array.Empty<DeploymentSlot>();
        [SerializeField] private DeploymentRarityStyle[] deploymentRarityStyles = Array.Empty<DeploymentRarityStyle>();

        [Header("Battle Clock")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private Image timerAccent;

        [Header("Failure")]
        [SerializeField] private GameObject failureRoot;
        [SerializeField] private TMP_Text failureReasonText;
        [SerializeField] private TMP_Text failureDetailText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button leaveButton;

        [Header("World Drop Catalogs")]
        [SerializeField] private ItemCatalog itemCatalog;
        [SerializeField] private WorldItemDropVisualCatalog itemDropVisualCatalog;
        [SerializeField] private EquipmentBalanceConfig equipmentBalanceConfig;
        [SerializeField] private EquipmentDropChestVisualCatalog equipmentDropVisualCatalog;

        private Action retryRequested;
        private Action leaveRequested;

        public ItemCatalog ItemCatalog => itemCatalog;
        public WorldItemDropVisualCatalog ItemDropVisualCatalog => itemDropVisualCatalog;
        public EquipmentBalanceConfig EquipmentBalanceConfig => equipmentBalanceConfig;
        public EquipmentDropChestVisualCatalog EquipmentDropVisualCatalog => equipmentDropVisualCatalog;
        public string DisplayedTimer => timerText == null ? string.Empty : timerText.text;
        public string DisplayedFailureReason => failureReasonText == null ? string.Empty : failureReasonText.text;
        public bool IsFailurePanelVisible => failureRoot != null && failureRoot.activeInHierarchy;
        public bool HasDeploymentPresentation => deploymentDock != null && deploymentSlots.Length > 0;
        public bool HasRuntimeBindings => timerText != null && failureRoot != null &&
                                          failureReasonText != null && failureDetailText != null &&
                                          retryButton != null && leaveButton != null;

        public void Bind(Action onRetry, Action onLeave)
        {
            Unbind();
            retryRequested = onRetry;
            leaveRequested = onLeave;
            retryButton?.onClick.AddListener(HandleRetry);
            leaveButton?.onClick.AddListener(HandleLeave);
            HideFailure();
        }

        public void Unbind()
        {
            retryButton?.onClick.RemoveListener(HandleRetry);
            leaveButton?.onClick.RemoveListener(HandleLeave);
            retryRequested = null;
            leaveRequested = null;
        }

        public void ConfigureDeployment(int visibleCount)
        {
            if (!HasDeploymentPresentation)
            {
                return;
            }

            var count = Mathf.Clamp(visibleCount, 0, deploymentSlots.Length);
            deploymentDock.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Max(220f, count * DeploymentSlotSpacing + 20f));
            for (var index = 0; index < deploymentSlots.Length; index++)
            {
                var slot = deploymentSlots[index];
                slot.Root.gameObject.SetActive(index < count);
                slot.Root.anchoredPosition = new Vector2(
                    (index - (count - 1) * 0.5f) * DeploymentSlotSpacing, -12f);
                slot.Selection.enabled = false;
            }
        }

        public void SetDeploymentSlot(int index, Sprite portrait, int remaining, bool selected,
            MonsterRarity rarity = MonsterRarity.Common, bool occupied = true)
        {
            if (!HasDeploymentPresentation || index < 0 || index >= deploymentSlots.Length)
            {
                return;
            }

            var slot = deploymentSlots[index];
            slot.Portrait.sprite = portrait;
            slot.Portrait.enabled = portrait != null;
            slot.Count.text = !occupied ? string.Empty : remaining > 0 ? $"×{remaining}" : "완료";
            slot.Count.fontSize = remaining > 0 ? 22f : 16f;
            slot.Selection.enabled = selected && remaining > 0;
            slot.Visual.alpha = !occupied ? 0.28f : remaining > 0 ? 1f : 0.42f;
            foreach (var style in deploymentRarityStyles)
            {
                if (style.Rarity != rarity) continue;
                if (slot.Background != null) slot.Background.color = style.Background;
                if (slot.RarityBorder != null) slot.RarityBorder.color = style.Border;
                if (slot.RarityHighlight != null) slot.RarityHighlight.color = style.Highlight;
                break;
            }
        }

        public void SetTimer(float seconds, bool running)
        {
            if (timerText == null)
            {
                return;
            }

            var wholeSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
            timerText.text = $"{wholeSeconds / 60:00}:{wholeSeconds % 60:00}";
            var urgent = running && wholeSeconds <= 30;
            timerText.color = urgent ? new Color32(255, 224, 174, 255) : Color.white;
            if (timerAccent != null)
            {
                timerAccent.color = urgent
                    ? new Color32(180, 55, 44, 255)
                    : new Color32(198, 145, 55, 255);
            }
        }

        public void ShowFailure(HexCastleRaidFailureReason reason, int stage)
        {
            if (failureRoot == null)
            {
                return;
            }

            failureRoot.SetActive(true);
            foreach (var slot in deploymentSlots)
            {
                slot.Selection.enabled = false;
            }
            if (failureReasonText != null)
            {
                failureReasonText.text = reason == HexCastleRaidFailureReason.TimeExpired
                    ? "제한 시간 초과"
                    : "공격 부대 전멸";
            }

            if (failureDetailText != null)
            {
                var stageLabel = stage > 0 ? $"STAGE {stage:000}" : "현재 요새";
                failureDetailText.text =
                    $"{stageLabel} 공략에 실패했습니다.\n같은 성과 같은 편성으로 비용 없이 다시 도전할 수 있습니다.";
            }
        }

        public void HideFailure()
        {
            failureRoot?.SetActive(false);
        }

        private void HandleRetry()
        {
            retryRequested?.Invoke();
        }

        private void HandleLeave()
        {
            leaveRequested?.Invoke();
        }

        private void OnDestroy()
        {
            Unbind();
        }

#if UNITY_EDITOR
        public void EditorConfigureDeployment(RectTransform dock, DeploymentSlot[] slots,
            DeploymentRarityStyle[] rarityStyles)
        {
            deploymentDock = dock;
            deploymentSlots = slots ?? Array.Empty<DeploymentSlot>();
            deploymentRarityStyles = rarityStyles ?? Array.Empty<DeploymentRarityStyle>();
        }

        public void EditorConfigure(
            TMP_Text battleTimer,
            Image battleTimerAccent,
            GameObject defeatRoot,
            TMP_Text defeatReason,
            TMP_Text defeatDetail,
            Button retry,
            Button leave,
            ItemCatalog catalog,
            WorldItemDropVisualCatalog dropCatalog,
            EquipmentBalanceConfig balance,
            EquipmentDropChestVisualCatalog equipmentDropCatalog)
        {
            timerText = battleTimer;
            timerAccent = battleTimerAccent;
            failureRoot = defeatRoot;
            failureReasonText = defeatReason;
            failureDetailText = defeatDetail;
            retryButton = retry;
            leaveButton = leave;
            itemCatalog = catalog;
            itemDropVisualCatalog = dropCatalog;
            equipmentBalanceConfig = balance;
            equipmentDropVisualCatalog = equipmentDropCatalog;
        }
#endif
    }
}
