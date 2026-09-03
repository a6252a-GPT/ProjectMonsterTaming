using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Equipment
{
    public sealed partial class EquipmentPageController
    {
        private void BuildDismantleControls()
        {
            if (dismantleGradeButtonRoot != null)
            {
                dismantleGradeButton = EnsureButton(dismantleGradeButtonRoot);
                dismantleGradeButtonText = dismantleGradeButtonRoot.GetComponentInChildren<TMP_Text>(true);
                dismantleGradeButton.onClick.AddListener(CycleDismantleGradeThreshold);
            }

            if (dismantleAutoSelectButtonRoot != null)
            {
                dismantleAutoSelectButton = EnsureButton(dismantleAutoSelectButtonRoot);
                dismantleAutoSelectButtonText = dismantleAutoSelectButtonRoot.GetComponentInChildren<TMP_Text>(true);
                dismantleAutoSelectButton.onClick.AddListener(ToggleDismantleAutoSelection);
            }

            if (dismantleButtonRoot != null)
            {
                dismantleButton = EnsureButton(dismantleButtonRoot);
                dismantleButtonText = dismantleButtonRoot.GetComponentInChildren<TMP_Text>(true);
                dismantleButton.onClick.AddListener(HandleDismantleButtonClicked);
            }

            if (dismantleClearButtonRoot != null)
            {
                dismantleClearButton = EnsureButton(dismantleClearButtonRoot);
                dismantleClearButton.onClick.AddListener(() =>
                {
                    ClearDismantleSelection();
                    RefreshSelection();
                });
            }

            if (offlineAutoDismantleOpenButtonRoot != null)
            {
                offlineAutoDismantleOpenButton = EnsureButton(offlineAutoDismantleOpenButtonRoot);
                offlineAutoDismantleOpenButtonText =
                    offlineAutoDismantleOpenButtonRoot.GetComponentInChildren<TMP_Text>(true);
                offlineAutoDismantleOpenButton.onClick.AddListener(OpenOfflineAutoDismantleSettings);
            }

            if (dismantleConfirmCancelButton != null)
            {
                dismantleConfirmCancelButton.onClick.AddListener(CloseDismantleConfirmation);
            }

            if (dismantleConfirmAcceptButton != null)
            {
                dismantleConfirmAcceptButton.onClick.AddListener(HandleDismantleConfirmed);
            }
        }

        private void BuildLockButton()
        {
            if (lockButtonRoot == null)
            {
                return;
            }

            lockButton = EnsureButton(lockButtonRoot);
            lockButtonText = lockButtonRoot.GetComponentInChildren<TMP_Text>(true);
            lockButton.onClick.AddListener(HandleLockButtonClicked);
        }

        private void CycleDismantleGradeThreshold()
        {
            if (requestInFlight)
            {
                return;
            }

            dismantleGradeThreshold = (EquipmentGrade)(((int)dismantleGradeThreshold + 1) % 5);
            ClearDismantleSelection();
            RefreshSelection();
        }

        private void OpenOfflineAutoDismantleSettings()
        {
            if (requestInFlight || offlineAutoDismantleSettingsPanel == null)
            {
                return;
            }

            offlineAutoDismantleSettingsPanel.Configure(progress);
            offlineAutoDismantleSettingsPanel.Open();
        }

        private void ToggleDismantleAutoSelection()
        {
            if (requestInFlight)
            {
                return;
            }

            if (dismantleSelection.Count > 0)
            {
                ClearDismantleSelection();
                RefreshSelection();
                return;
            }

            var candidates = EquipmentInventoryRuntime.GetDismantleCandidateIds(dismantleGradeThreshold);
            for (var index = 0; index < candidates.Count; index++)
            {
                dismantleSelection.Add(candidates[index]);
            }

            if (dismantleSelection.Count > 0)
            {
                selectedInstanceId = null;
            }

            CloseDismantleConfirmation();
            RefreshSelection();
        }

        private async void HandleLockButtonClicked()
        {
            if (requestInFlight || currentMode != EquipmentPageMode.Equip ||
                string.IsNullOrEmpty(selectedInstanceId) ||
                !EquipmentInventoryRuntime.TryGetItem(selectedInstanceId, out var item))
            {
                return;
            }

            requestInFlight = true;
            RefreshSelection();
            try
            {
                await EquipmentInventoryRuntime.TrySetLockedAsync(selectedInstanceId, !item.IsLocked);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                requestInFlight = false;
                RefreshAll();
            }
        }

        private void HandleDismantleButtonClicked()
        {
            PruneDismantleSelection();
            if (requestInFlight || currentMode != EquipmentPageMode.Dismantle || dismantleSelection.Count == 0)
            {
                RefreshSelection();
                return;
            }

            OpenDismantleConfirmation();
        }

        private async void HandleDismantleConfirmed()
        {
            PruneDismantleSelection();
            if (requestInFlight || currentMode != EquipmentPageMode.Dismantle || dismantleSelection.Count == 0)
            {
                CloseDismantleConfirmation();
                RefreshSelection();
                return;
            }

            var targets = dismantleSelection.ToArray();
            requestInFlight = true;
            CloseDismantleConfirmation();
            RefreshSelection();
            try
            {
                if (await EquipmentInventoryRuntime.TryDismantleAsync(targets))
                {
                    dismantleSelection.Clear();
                    selectedInstanceId = null;
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                requestInFlight = false;
                CloseDismantleConfirmation();
                RefreshAll();
            }
        }

        private void OpenDismantleConfirmation()
        {
            if (dismantleConfirmRoot == null)
            {
                HandleDismantleConfirmed();
                return;
            }

            if (dismantleConfirmSummaryText != null)
            {
                dismantleConfirmSummaryText.text =
                    $"선택 장비 {dismantleSelection.Count}개를 분해하고\n장비 슬롯 강화석 {CalculateSelectedDismantleReward():N0}개를 획득합니다.";
            }

            dismantleConfirmRoot.SetActive(true);
        }

        private void ClearDismantleSelection()
        {
            dismantleSelection.Clear();
            CloseDismantleConfirmation();
        }

        private void CloseDismantleConfirmation()
        {
            if (dismantleConfirmRoot != null)
            {
                dismantleConfirmRoot.SetActive(false);
            }
        }

        private void PruneDismantleSelection()
        {
            dismantleSelection.RemoveWhere(instanceId =>
                !EquipmentInventoryRuntime.TryGetItem(instanceId, out var item) || item.IsEquipped || item.IsLocked);
            if (dismantleSelection.Count == 0)
            {
                CloseDismantleConfirmation();
            }
        }

        private long CalculateSelectedDismantleReward()
        {
            var result = 0L;
            foreach (var instanceId in dismantleSelection)
            {
                if (EquipmentInventoryRuntime.TryGetItem(instanceId, out var item))
                {
                    result += EquipmentDismantleRules.GetUpgradeStoneAmount(item.Grade);
                }
            }

            return result;
        }

        private void RefreshDismantleSummary()
        {
            var stoneAmount = CalculateSelectedDismantleReward();
            if (dismantleSummaryCountText != null)
            {
                dismantleSummaryCountText.text = $"선택 장비 {dismantleSelection.Count}개";
            }

            if (dismantleSummaryRewardText != null)
            {
                dismantleSummaryRewardText.text = $"획득 강화석 {stoneAmount:N0}개";
            }

            if (dismantleBottomSummaryText != null)
            {
                dismantleBottomSummaryText.text = $"선택 {dismantleSelection.Count}개 / 강화석 {stoneAmount:N0}개";
            }

            var selectedItems = dismantleSelection
                .Select(instanceId => EquipmentInventoryRuntime.TryGetItem(instanceId, out var item) ? item : default)
                .Where(item => !string.IsNullOrEmpty(item.InstanceId))
                .Take(dismantlePreviewSlots.Count)
                .ToList();
            for (var index = 0; index < dismantlePreviewSlots.Count; index++)
            {
                var preview = dismantlePreviewSlots[index];
                var visible = index < selectedItems.Count;
                preview.Root.SetActive(visible);
                if (!visible)
                {
                    if (preview.LevelText != null)
                    {
                        preview.LevelText.text = string.Empty;
                        preview.LevelText.gameObject.SetActive(false);
                    }

                    continue;
                }

                var item = selectedItems[index];
                if (preview.Icon != null && partIconSprites.TryGetValue(item.Part, out var icon))
                {
                    preview.Icon.sprite = icon;
                    preview.Icon.color = Color.white;
                }

                if (preview.Frame != null)
                {
                    preview.Frame.color = GetDismantlePreviewColor(item.Grade);
                }

                if (preview.LevelText != null)
                {
                    preview.LevelText.text = $"Lv.{item.ItemLevel}";
                    preview.LevelText.gameObject.SetActive(true);
                }
            }
        }

        // 분해 미리보기 배경색. 인벤토리 프레임에서 뽑은 실제 색을 우선 쓰고, 없으면 팔레트 근사값을 쓴다.
        private Color GetDismantlePreviewColor(EquipmentGrade grade)
        {
            if (FrameVariantSuffixByGrade.TryGetValue(grade, out var suffix)
                && frameVariantSwatchColors.TryGetValue(suffix, out var sampledColor))
            {
                return sampledColor;
            }

            return ItemGradeFramePalette.GetColor(grade);
        }

        private void RefreshDismantleControls()
        {
            var isDismantleMode = currentMode == EquipmentPageMode.Dismantle;
            if (dismantleGradeButtonRoot != null)
            {
                dismantleGradeButtonRoot.gameObject.SetActive(isDismantleMode);
            }

            if (dismantleGradeButtonText != null)
            {
                dismantleGradeButtonText.text = $"{EquipmentGradeInfo.GetDisplayName(dismantleGradeThreshold)} 이하";
            }

            if (dismantleGradeButton != null)
            {
                dismantleGradeButton.interactable = isDismantleMode && !requestInFlight;
            }

            if (dismantleAutoSelectButtonRoot != null)
            {
                dismantleAutoSelectButtonRoot.gameObject.SetActive(isDismantleMode);
            }

            if (dismantleAutoSelectButtonText != null)
            {
                dismantleAutoSelectButtonText.text = dismantleSelection.Count > 0 ? "선택 해제" : "이하 전체 선택";
            }

            if (dismantleAutoSelectButton != null)
            {
                dismantleAutoSelectButton.interactable = isDismantleMode && !requestInFlight;
            }

            if (dismantleButtonRoot != null)
            {
                dismantleButtonRoot.gameObject.SetActive(isDismantleMode);
            }

            if (dismantleButton != null)
            {
                dismantleButton.interactable = isDismantleMode && dismantleSelection.Count > 0 && !requestInFlight;
            }

            if (dismantleButtonText != null)
            {
                dismantleButtonText.text = requestInFlight
                    ? "처리 중"
                    : "분해";
            }

            if (dismantleClearButton != null)
            {
                dismantleClearButton.interactable = isDismantleMode && dismantleSelection.Count > 0 && !requestInFlight;
            }

            if (offlineAutoDismantleOpenButtonRoot != null)
            {
                offlineAutoDismantleOpenButtonRoot.gameObject.SetActive(isDismantleMode);
            }

            if (offlineAutoDismantleOpenButtonText != null)
            {
                var policy = progress != null && progress.IsLoaded
                    ? progress.View.Equipment.OfflineAutoDismantlePolicy
                    : OfflineAutoDismantlePolicy.Common;
                offlineAutoDismantleOpenButtonText.text =
                    $"방치 설정\n{OfflineAutoDismantlePolicyInfo.GetDisplayName(policy)}";
            }

            if (offlineAutoDismantleOpenButton != null)
            {
                offlineAutoDismantleOpenButton.interactable =
                    isDismantleMode && !requestInFlight && progress != null && progress.IsLoaded;
            }
        }
    }
}
