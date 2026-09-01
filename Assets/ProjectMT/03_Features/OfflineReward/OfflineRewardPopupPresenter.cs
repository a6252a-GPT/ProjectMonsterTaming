using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectMT.Features.Equipment;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
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
        private const float ActionButtonDimmedAlpha = 76f / 255f; // 일일/주간 퀘스트 수령 완료 연출과 동일한 값
        private const float ActionButtonNormalAlpha = 1f;

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
        [SerializeField] private TMP_Text adCooldownTimeText; // 비워두면 adButton 하위 "TimeText"를 이름으로 자동 탐색
        [SerializeField] private Button claimButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private RewardedAdVideoOverlaySelector adVideoOverlay; // 영상별로 나뉜 AdVideoOverlay 중 하나를 골라 재생
        [SerializeField] private GameObject displayRootOverride;
        [SerializeField] private GameObject mainPopupRoot;
        [SerializeField] private GameObject autoDismantleNoticeRoot;
        [SerializeField] private TMP_Text autoDismantleNoticeText;
        [SerializeField] private Button autoDismantleNoticeConfirmButton;

        private readonly List<OfflineRewardItemSlotView> rewardSlots = new List<OfflineRewardItemSlotView>();
        private readonly Dictionary<string, GameObject> frameVariantTemplates = new Dictionary<string, GameObject>();
        private Func<Task<bool>> acknowledge;
        private Action<OfflineRewardPresentation> confirmed;
        private Func<OfflineRewardPresentation, Task<bool>> grantDoubleReward;
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
            ResolveAdCooldownTimeText();
            adButton?.onClick.RemoveListener(HandleAdClicked);
            adButton?.onClick.AddListener(HandleAdClicked);
            claimButton?.onClick.RemoveListener(HandleClaimClicked);
            claimButton?.onClick.AddListener(HandleClaimClicked);
            closeButton?.onClick.RemoveListener(HandleClaimClicked);
            closeButton?.onClick.AddListener(HandleClaimClicked);
            autoDismantleNoticeConfirmButton?.onClick.RemoveListener(HandleNoticeConfirmed);
            autoDismantleNoticeConfirmButton?.onClick.AddListener(HandleNoticeConfirmed);

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

            if (IsOpen && adCooldownTimeText != null && adCooldownTimeText.gameObject.activeSelf)
            {
                UpdateAdCooldownText();
            }
        }

        public void Show(
            OfflineRewardPresentation presentation,
            ItemCatalog catalog,
            Func<Task<bool>> acknowledgeRequest,
            Action<OfflineRewardPresentation> onConfirmed,
            Func<OfflineRewardPresentation, Task<bool>> grantDoubleRewardRequest = null)
        {
            if (presentation == null)
            {
                return;
            }

            current = presentation;
            itemCatalog = catalog;
            acknowledge = acknowledgeRequest;
            confirmed = onConfirmed;
            grantDoubleReward = grantDoubleRewardRequest;
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
            SetActionButtonsLocked(false); // 새 영수증을 띄울 때마다 그냥받기·2배받기 모두 밝게 초기화
            RefreshAdRewardAvailability();
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
            await ClaimAsync(grantBonus: false);
        }

        private async Task ClaimAsync(bool grantBonus)
        {
            if (busy || acknowledge == null || current == null)
            {
                if (grantBonus)
                {
                    SetActionButtonsLocked(false); // 광고 시청 완료 콜백인데 이미 상태가 꼬였다면 잠금만 풀어준다
                }

                return;
            }

            busy = true;
            SetActionButtonsLocked(true);
            if (closeButton != null)
            {
                closeButton.interactable = false;
            }

            if (grantBonus)
            {
                Set(statusText, "2배 보상을 지급하는 중입니다...");
                bool granted;
                try
                {
                    granted = grantDoubleReward != null && await grantDoubleReward(current);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    granted = false;
                }

                if (!granted)
                {
                    busy = false;
                    SetActionButtonsLocked(false);
                    if (closeButton != null)
                    {
                        closeButton.interactable = true;
                    }

                    Set(statusText, "2배 보상 지급에 실패했습니다. 다시 눌러주세요");
                    return;
                }

                OfflineRewardAdClaimStore.SaveLastClaimedPeriod(GrowthDungeonDailyKeyRules.GetPeriodId(DateTime.UtcNow));
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
                // 2배 보상은 이미 지급됐을 수 있으니(오늘 소진 처리됨) 그냥받기만 풀고,
                // 광고 버튼은 쿨다운 상태를 다시 확인해 중복 지급을 막는다.
                SetButtonLockedVisual(claimButton, false);
                SetButtonLockedVisual(adButton, IsAdRewardOnCooldown());
                if (closeButton != null)
                {
                    closeButton.interactable = true;
                }

                return;
            }

            var completed = current;
            var onConfirmed = confirmed;
            current = null;
            acknowledge = null;
            confirmed = null;
            grantDoubleReward = null;
            UIPanelPopAnimator.RequestClose(DisplayRoot, () =>
            {
                try
                {
                    onConfirmed?.Invoke(completed);
                }
                catch (Exception exception)
                {
                    // 팝업을 닫은 뒤 다음 화면(다음 영수증/출석 등)으로 넘어가는 콜백이라,
                    // 여기서 예외가 나도 로그만 남기고 삼켜서 이후 화면 흐름이 멈추지 않게 한다.
                    Debug.LogException(exception);
                }
            });
        }

        private void HandleAdClicked()
        {
            if (busy || current == null || adVideoOverlay == null || IsAdRewardOnCooldown())
            {
                return;
            }

            SetActionButtonsLocked(true);
            Set(statusText, "광고 영상을 재생합니다...");
            adVideoOverlay.Play(HandleAdWatchedFully, HandleAdSkipped);
        }

        private void HandleAdPreloadCompleted(bool succeeded)
        {
            if (!IsOpen || busy)
            {
                return;
            }

            SetButtonLockedVisual(adButton, !succeeded);
            if (!succeeded)
            {
                Set(statusText, "광고 영상을 준비하지 못했습니다. 일반 보상은 수령할 수 있습니다.");
            }
        }

        private void HandleAdSkipped()
        {
            // 시청을 중간에 취소한 것뿐이라 아직 아무것도 확정되지 않았다. 두 버튼 다시 선택 가능하게 복구.
            SetButtonLockedVisual(claimButton, false);
            SetButtonLockedVisual(adButton, IsAdRewardOnCooldown());
            Set(statusText, "정산 저장 완료");
        }

        private async void HandleAdWatchedFully()
        {
            await ClaimAsync(grantBonus: true);
        }

        // Inspector에 직접 연결하지 않아도, adButton(Button_01) 하위의 "TimeText" 이름으로 찾아 붙인다.
        private void ResolveAdCooldownTimeText()
        {
            if (adCooldownTimeText != null || adButton == null)
            {
                return;
            }

            var found = adButton.transform.Find("TimeText");
            if (found != null)
            {
                adCooldownTimeText = found.GetComponent<TMP_Text>();
            }
        }

        // 광고시청 2배 보상은 KST 05:00 기준 1일 1회만 허용한다. 이미 오늘 받았다면 버튼을 잠그고
        // TimeText에 다음 초기화까지 남은 시간을 표시하고, 아니라면 평소처럼 영상을 미리 준비한다.
        private void RefreshAdRewardAvailability()
        {
            var onCooldown = IsAdRewardOnCooldown();
            if (adCooldownTimeText != null)
            {
                adCooldownTimeText.gameObject.SetActive(onCooldown);
            }

            if (onCooldown)
            {
                SetButtonLockedVisual(adButton, true);
                UpdateAdCooldownText();
                return;
            }

            if (adVideoOverlay != null)
            {
                // 광고 버튼을 누른 뒤에야 영상을 Prepare하면 소리만 먼저 재생될 수 있다.
                // 팝업을 읽는 동안 미리 준비하고, 준비가 끝난 뒤에만 2배 보상 버튼을 연다.
                SetButtonLockedVisual(adButton, true);
                adVideoOverlay.PreloadNextClip(HandleAdPreloadCompleted);
            }
        }

        private static bool IsAdRewardOnCooldown()
        {
            var currentPeriod = GrowthDungeonDailyKeyRules.GetPeriodId(DateTime.UtcNow);
            var lastClaimedPeriod = OfflineRewardAdClaimStore.LoadLastClaimedPeriod();
            return lastClaimedPeriod >= currentPeriod;
        }

        private void UpdateAdCooldownText()
        {
            if (adCooldownTimeText == null)
            {
                return;
            }

            Set(adCooldownTimeText, FormatCooldownRemaining(GetTimeUntilNextAdReset(DateTime.UtcNow)));
        }

        // GrowthDungeonDailyKeyRules.GetPeriodId와 동일한 KST 05:00 경계 규칙으로 다음 리셋까지
        // 남은 시간을 계산한다(그 함수가 쓰는 것과 같은 +offsetHours 이동을 되돌려서 실제 UTC 시각을 구함).
        private static TimeSpan GetTimeUntilNextAdReset(DateTime utcNow)
        {
            var utc = utcNow.Kind == DateTimeKind.Utc ? utcNow : utcNow.ToUniversalTime();
            var offsetHours = 9 - GrowthDungeonDailyKeyRules.ResetHourKst;
            var shifted = utc.AddHours(offsetHours);
            var nextResetShifted = shifted.Date.AddDays(1);
            var nextResetUtc = nextResetShifted.AddHours(-offsetHours);
            var remaining = nextResetUtc - utc;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        private static string FormatCooldownRemaining(TimeSpan remaining)
        {
            var hours = Math.Max(0, (int)remaining.TotalHours);
            var minutes = Math.Max(0, remaining.Minutes);
            return $"남은시간 [ {hours:00} : {minutes:00} ]"; // TimeText에 미리 넣어둔 표기 형식과 동일하게 맞춤
        }

        // 진행중이든 광고 재생 중이든, 확정되기 전까지 다른 버튼으로 중복 처리되지 않도록 둘 다 잠그고
        // 일일/주간 퀘스트 수령 완료 연출과 같은 방식(알파 낮추기)으로 어둡게 만든다.
        private void SetActionButtonsLocked(bool locked)
        {
            SetButtonLockedVisual(claimButton, locked);
            SetButtonLockedVisual(adButton, locked);
        }

        private static void SetButtonLockedVisual(Button button, bool locked)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = !locked;
            var alpha = locked ? ActionButtonDimmedAlpha : ActionButtonNormalAlpha;
            var graphics = button.GetComponentsInChildren<Graphic>(true);
            for (var index = 0; index < graphics.Length; index++)
            {
                var graphic = graphics[index];
                var color = graphic.color;
                color.a = alpha;
                graphic.color = color;
            }
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
