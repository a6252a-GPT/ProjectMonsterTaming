using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectMT.Features.Equipment;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.OfflineReward
{
    [DisallowMultipleComponent]
    public sealed class OfflineRewardPopupPresenter : MonoBehaviour // 정산 완료 영수증 표시·확인 저장
    {
        private const string FrameVariantPrefix = ItemGradeFramePalette.FrameVariantPrefix;
        private const string CommonFrameVariantSuffix = ItemGradeFramePalette.CommonSuffix;

        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text capText;
        [SerializeField] private TMP_Text goldRateText;
        [SerializeField] private TMP_Text experienceRateText;
        [SerializeField] private TMP_Text stoneRateText;
        [SerializeField] private TMP_Text equipmentRateText;
        [SerializeField] private TMP_Text autoDismantleRateText;
        [SerializeField] private TMP_Text goldRewardText;
        [SerializeField] private TMP_Text experienceRewardText;
        [SerializeField] private TMP_Text stoneRewardText;
        [SerializeField] private TMP_Text equipmentRewardText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text rewardListTitle;
        [SerializeField] private RectTransform rewardContent;
        [SerializeField] private ScrollRect rewardScrollRect;
        [SerializeField] private OfflineRewardItemSlotView rewardSlotTemplate;
        [SerializeField] private EquipmentCatalog equipmentCatalog;
        [SerializeField] private Sprite[] equipmentPartIcons = new Sprite[6];
        [SerializeField] private Transform frameVariantTemplateStorage;
        [SerializeField] private Sprite commanderExperienceIcon;
        [SerializeField] private Sprite fallbackRewardIcon;
        [SerializeField] private Button adButton;
        [SerializeField] private Button claimButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject displayRootOverride;
        [SerializeField] private GameObject mainPopupRoot;
        [SerializeField] private GameObject autoDismantleNoticeRoot;
        [SerializeField] private TMP_Text autoDismantleNoticeText;
        [SerializeField] private Button autoDismantleNoticeConfirmButton;

        private readonly List<OfflineRewardItemSlotView> rewardSlots = new List<OfflineRewardItemSlotView>();
        private readonly Dictionary<string, GameObject> frameVariantTemplates = new Dictionary<string, GameObject>();
        private Func<Task<bool>> acknowledge;
        private Action<OfflineRewardPresentation> confirmed;
        private OfflineRewardPresentation current;
        private ItemCatalog itemCatalog;
        private bool busy;
        private bool combatDisplaySuppressed;

        private GameObject DisplayRoot => displayRootOverride != null ? displayRootOverride : gameObject;
        public bool IsOpen => DisplayRoot.activeSelf;

        private void Awake()
        {
            CacheRewardSlots();
            CacheFrameVariantTemplates();
            adButton?.onClick.RemoveListener(HandleAdClicked);
            adButton?.onClick.AddListener(HandleAdClicked);
            claimButton?.onClick.RemoveListener(HandleClaimClicked);
            claimButton?.onClick.AddListener(HandleClaimClicked);
            closeButton?.onClick.RemoveListener(HandleClaimClicked);
            closeButton?.onClick.AddListener(HandleClaimClicked);
            autoDismantleNoticeConfirmButton?.onClick.RemoveListener(HandleNoticeConfirmed);
            autoDismantleNoticeConfirmButton?.onClick.AddListener(HandleNoticeConfirmed);
            if (adButton != null)
            {
                adButton.interactable = false; // 광고 SDK 연결 전 비활성 유지
            }

            DisplayRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            SetCombatDisplaySuppressed(false);
        }

        private void OnDisable()
        {
            SetCombatDisplaySuppressed(false);
        }

        private void LateUpdate()
        {
            var shouldSuppress = IsOpen;
            if (combatDisplaySuppressed != shouldSuppress)
            {
                SetCombatDisplaySuppressed(shouldSuppress);
            }
        }

        public void Show(
            OfflineRewardPresentation presentation,
            ItemCatalog catalog,
            Func<Task<bool>> acknowledgeRequest,
            Action<OfflineRewardPresentation> onConfirmed)
        {
            if (presentation == null)
            {
                return;
            }

            current = presentation;
            itemCatalog = catalog;
            acknowledge = acknowledgeRequest;
            confirmed = onConfirmed;
            busy = false;
            Bind(presentation);
            UIPanelPopAnimator.RequestOpen(DisplayRoot, UIPanelPopStyle.RewardPopup);

            // DisplayRoot는 기본 비활성이라 Awake()에서 EnsureOn을 부르면 초기화가 미뤄진다.
            // 막 활성화된 직후인 여기서 붙여야 클릭 연출이 확실히 동작한다.
            UIButtonClickPunch.EnsureOn(adButton?.gameObject);
            UIButtonClickPunch.EnsureOn(claimButton?.gameObject);
            UIButtonClickPunch.EnsureOn(closeButton?.gameObject);

            SetCombatDisplaySuppressed(true);
            var showNotice = presentation.AutoDismantledEquipmentCount > 0 &&
                             autoDismantleNoticeRoot != null;
            mainPopupRoot?.SetActive(!showNotice);
            autoDismantleNoticeRoot?.SetActive(showNotice);
            if (showNotice)
            {
                Set(
                    autoDismantleNoticeText,
                    "인벤토리가 가득 차 낮은 등급 장비가 자동 분해되었습니다.\n" +
                    $"분해 {presentation.AutoDismantledEquipmentCount:N0}개 · " +
                    $"장비 슬롯 강화석 +{presentation.AutoDismantleUpgradeStone:N0}");
            }
            if (claimButton != null)
            {
                claimButton.interactable = true;
            }

            if (closeButton != null)
            {
                closeButton.interactable = true;
            }
        }

        public void Hide()
        {
            HandleClaimClicked(); // X도 지급 완료 영수증을 확인 처리
        }

        private void HandleNoticeConfirmed()
        {
            autoDismantleNoticeRoot?.SetActive(false);
            mainPopupRoot?.SetActive(true);
        }

        private void Bind(OfflineRewardPresentation presentation)
        {
            Set(timeText, $"방치 시간  {FormatDuration(presentation.ElapsedSeconds)}");
            Set(
                stageText,
                presentation.MixedBasis
                    ? "최종 원정대 : 접속별 기준"
                    : $"최종 원정대 : {presentation.BasisStage}");
            Set(capText, presentation.Capped ? "최대 누적 시간이 적용되었습니다" : "정상 누적");
            Set(
                goldRateText,
                presentation.MixedBasis
                    ? $"+{presentation.Gold:N0} · 접속별 합산"
                    : $"+{presentation.Gold:N0} · {presentation.GoldPerMinute:N0}/60s");
            Set(
                experienceRateText,
                presentation.MixedBasis
                    ? $"+{presentation.CommanderExperience:N0} · 접속별 합산"
                    : $"+{presentation.CommanderExperience:N0} · {presentation.CommanderExperiencePerMinute:N0}/60s");
            Set(
                stoneRateText,
                presentation.MixedBasis
                    ? $"+{presentation.UpgradeStone:N0} · 접속별 합산"
                    : $"+{presentation.UpgradeStone:N0} · 무작위 1/60s");
            Set(
                equipmentRateText,
                presentation.MixedBasis
                    ? $"획득 {presentation.EquipmentRewards.Count:N0}개 · 접속별 합산"
                    : $"획득 {presentation.EquipmentRewards.Count:N0}개 · 분당 " +
                      $"{presentation.EquipmentChanceBasisPointsPerMinute / 100f:0.##}%");
            Set(
                autoDismantleRateText,
                presentation.AutoDismantledEquipmentCount > 0
                    ? $"분해 {presentation.AutoDismantledEquipmentCount:N0}개 · +{presentation.AutoDismantleUpgradeStone:N0}"
                    : "자동분해 없음");
            Set(goldRewardText, $"골드\n+{presentation.Gold:N0}");
            Set(experienceRewardText, $"군단장 경험치\n+{presentation.CommanderExperience:N0}");
            Set(stoneRewardText, $"무작위 강화석\n+{presentation.UpgradeStone:N0}");
            Set(
                equipmentRewardText,
                presentation.AutoDismantledEquipmentCount > 0
                    ? $"장비\n+{presentation.EquipmentRewards.Count:N0} · 자동분해 {presentation.AutoDismantledEquipmentCount:N0}"
                    : $"장비\n+{presentation.EquipmentRewards.Count:N0}");
            Set(statusText, "정산 저장 완료");
            BindRewardList(presentation);
        }

        private void BindRewardList(OfflineRewardPresentation presentation)
        {
            CacheRewardSlots();
            CacheFrameVariantTemplates();
            var request = presentation.CreateAcquirePresentation();
            var items = request?.Items;
            var itemCount = items?.Count ?? 0;
            var equipmentCount = presentation.EquipmentRewards.Count;
            var count = itemCount + equipmentCount;
            EnsureRewardSlotCount(count);
            for (var index = 0; index < rewardSlots.Count; index++)
            {
                var slot = rewardSlots[index];
                if (slot == null)
                {
                    continue;
                }

                var active = index < count;
                slot.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                if (index < itemCount)
                {
                    var item = items[index];
                    slot.Bind(
                        ResolveRewardIcon(item),
                        item.Amount,
                        item.Label,
                        ResolveItemFrame(item));
                }
                else
                {
                    BindEquipmentSlot(slot, presentation.EquipmentRewards[index - itemCount]);
                }
                slot.transform.SetSiblingIndex(index);
            }

            Set(rewardListTitle, "획득 보상");
            if (rewardContent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rewardContent);
            }

            if (rewardScrollRect != null)
            {
                rewardScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void CacheRewardSlots()
        {
            if (rewardContent == null)
            {
                return;
            }

            rewardSlots.Clear();
            rewardContent.GetComponentsInChildren(true, rewardSlots);
            if (rewardSlotTemplate == null && rewardSlots.Count > 0)
            {
                rewardSlotTemplate = rewardSlots[0];
            }
        }

        private void EnsureRewardSlotCount(int count)
        {
            if (rewardContent == null || rewardSlotTemplate == null)
            {
                return;
            }

            while (rewardSlots.Count < count)
            {
                var slot = Instantiate(rewardSlotTemplate, rewardContent, false);
                slot.gameObject.SetActive(true);
                rewardSlots.Add(slot);
            }
        }

        private void CacheFrameVariantTemplates()
        {
            if (frameVariantTemplates.Count > 0)
            {
                return;
            }

            var source = frameVariantTemplateStorage != null
                ? frameVariantTemplateStorage
                : rewardContent;
            if (source == null)
            {
                return;
            }

            var candidates = source.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < candidates.Length; index++)
            {
                var candidate = candidates[index];
                if (!candidate.name.StartsWith(FrameVariantPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var suffix = candidate.name.Substring(FrameVariantPrefix.Length);
                if (frameVariantTemplates.ContainsKey(suffix))
                {
                    continue;
                }

                frameVariantTemplates[suffix] = candidate.gameObject;
            }
        }

        private GameObject ResolveCommonItemFrame()
        {
            return frameVariantTemplates.TryGetValue(CommonFrameVariantSuffix, out var template)
                ? template
                : null;
        }

        private GameObject ResolveItemFrame(RewardPresentationItem item)
        {
            var itemId = item.Kind == RewardPresentationKind.Gold ? ItemIds.Gold : item.ItemId;
            if (!string.IsNullOrWhiteSpace(itemId) && itemCatalog != null &&
                itemCatalog.TryGet(itemId, out var definition))
            {
                var suffix = ItemGradeFramePalette.GetSuffix(definition.Grade);
                if (frameVariantTemplates.TryGetValue(suffix, out var template))
                {
                    return template;
                }
            }

            return ResolveCommonItemFrame();
        }

        private void BindEquipmentSlot(OfflineRewardItemSlotView slot, EquipmentInstanceData equipment)
        {
            if (slot == null || equipment == null)
            {
                return;
            }

            var definition = equipmentCatalog != null
                ? equipmentCatalog.GetDefinitionForPart(equipment.Part, equipment.Grade)
                : null;
            var partIndex = (int)equipment.Part;
            var partIcon = equipmentPartIcons != null && partIndex >= 0 && partIndex < equipmentPartIcons.Length
                ? equipmentPartIcons[partIndex]
                : null;
            slot.Bind(
                partIcon ?? definition?.Icon ?? fallbackRewardIcon,
                1L,
                definition?.DisplayName ?? EquipmentPartInfo.GetDisplayName(equipment.Part),
                ResolveEquipmentFrame(equipment.Grade));
        }

        private GameObject ResolveEquipmentFrame(EquipmentGrade grade)
        {
            var suffix = ItemGradeFramePalette.GetSuffix(grade);
            return frameVariantTemplates.TryGetValue(suffix, out var template)
                ? template
                : ResolveCommonItemFrame();
        }

        private Sprite ResolveRewardIcon(RewardPresentationItem item)
        {
            var itemId = item.Kind == RewardPresentationKind.Gold ? ItemIds.Gold : item.ItemId;
            if (!string.IsNullOrWhiteSpace(itemId) && itemCatalog != null &&
                itemCatalog.TryGet(itemId, out var definition) && definition.Icon != null)
            {
                return definition.Icon;
            }

            return item.Kind == RewardPresentationKind.CommanderExperience && commanderExperienceIcon != null
                ? commanderExperienceIcon
                : fallbackRewardIcon;
        }

        private async void HandleClaimClicked()
        {
            if (busy || acknowledge == null || current == null)
            {
                return;
            }

            busy = true;
            if (claimButton != null)
            {
                claimButton.interactable = false;
            }

            if (closeButton != null)
            {
                closeButton.interactable = false;
            }

            Set(statusText, "확인 상태 저장 중...");
            bool saved;
            try
            {
                saved = await acknowledge();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                saved = false;
            }

            busy = false;
            if (!saved)
            {
                Set(statusText, "저장하지 못했습니다. 다시 눌러주세요");
                if (claimButton != null)
                {
                    claimButton.interactable = true;
                }

                if (closeButton != null)
                {
                    closeButton.interactable = true;
                }

                return;
            }

            var completed = current;
            current = null;
            acknowledge = null;
            UIPanelPopAnimator.RequestClose(DisplayRoot, () =>
            {
                confirmed?.Invoke(completed);
                confirmed = null;
            });
        }

        private void SetCombatDisplaySuppressed(bool suppressed)
        {
            combatDisplaySuppressed = suppressed;
            foreach (var feedback in FindObjectsByType<CombatFeedbackPlayer>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                feedback.SetDisplaySuppressed(this, suppressed);
            }
        }

        private void HandleAdClicked()
        {
            Set(statusText, "광고 2배는 현재 준비 중입니다");
        }

        private static string FormatDuration(long totalSeconds)
        {
            totalSeconds = Math.Max(0L, totalSeconds);
            var hours = totalSeconds / 3600L;
            var minutes = totalSeconds % 3600L / 60L;
            var seconds = totalSeconds % 60L;
            return $"{hours:00}:{minutes:00}:{seconds:00}";
        }

        private static void Set(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigureDisplayRoot(GameObject displayRoot)
        {
            displayRootOverride = displayRoot;
        }

        public void EditorConfigure(
            TMP_Text offlineTime,
            TMP_Text basisStage,
            TMP_Text capState,
            TMP_Text goldRate,
            TMP_Text experienceRate,
            TMP_Text stoneRate,
            TMP_Text equipmentRate,
            TMP_Text autoDismantleRate,
            TMP_Text goldReward,
            TMP_Text experienceReward,
            TMP_Text stoneReward,
            TMP_Text equipmentReward,
            TMP_Text state,
            TMP_Text listTitle,
            RectTransform listContent,
            ScrollRect listScrollRect,
            OfflineRewardItemSlotView slotTemplate,
            EquipmentCatalog equipmentDefinitions,
            Sprite[] partIcons,
            Transform frameTemplates,
            Sprite experienceIcon,
            Sprite fallbackIcon,
            Button advertisement,
            Button claim,
            Button close,
            GameObject popupRoot,
            GameObject dismantleNotice,
            TMP_Text dismantleNoticeText,
            Button dismantleNoticeConfirm)
        {
            timeText = offlineTime;
            stageText = basisStage;
            capText = capState;
            goldRateText = goldRate;
            experienceRateText = experienceRate;
            stoneRateText = stoneRate;
            equipmentRateText = equipmentRate;
            autoDismantleRateText = autoDismantleRate;
            goldRewardText = goldReward;
            experienceRewardText = experienceReward;
            stoneRewardText = stoneReward;
            equipmentRewardText = equipmentReward;
            statusText = state;
            rewardListTitle = listTitle;
            rewardContent = listContent;
            rewardScrollRect = listScrollRect;
            rewardSlotTemplate = slotTemplate;
            equipmentCatalog = equipmentDefinitions;
            equipmentPartIcons = partIcons ?? new Sprite[6];
            frameVariantTemplateStorage = frameTemplates;
            commanderExperienceIcon = experienceIcon;
            fallbackRewardIcon = fallbackIcon;
            adButton = advertisement;
            claimButton = claim;
            closeButton = close;
            mainPopupRoot = popupRoot;
            autoDismantleNoticeRoot = dismantleNotice;
            autoDismantleNoticeText = dismantleNoticeText;
            autoDismantleNoticeConfirmButton = dismantleNoticeConfirm;
        }
#endif
    }
}
