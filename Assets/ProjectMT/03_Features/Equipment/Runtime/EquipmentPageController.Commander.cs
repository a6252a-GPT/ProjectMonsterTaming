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
        // ---------------------------------------------------------------
        // 자동 장착 버튼
        // ---------------------------------------------------------------

        private void BuildEquipButton()
        {
            if (equipButtonRoot == null)
            {
                return;
            }

            equipButton = EnsureButton(equipButtonRoot);
            equipButtonText = equipButtonRoot.GetComponentInChildren<TMP_Text>(true);
            equipButton.onClick.AddListener(HandleEquipButtonClicked);
        }

        private async void HandleEquipButtonClicked()
        {
            if (currentMode != EquipmentPageMode.Equip || requestInFlight)
            {
                return;
            }

            requestInFlight = true;
            RefreshSelection();
            try
            {
                if (await EquipmentInventoryRuntime.TryAutoEquipAsync())
                {
                    combatInputSaved?.Invoke();
                }
            }
            finally
            {
                requestInFlight = false;
                RefreshAll();
            }
        }

        // ---------------------------------------------------------------
        // 군단장 장착 슬롯 6개 (WeaponSlot 등) - 장착 중인 장비 아이콘을 표시한다.
        // ---------------------------------------------------------------

        // 미장착 상태는 인벤토리의 빈 슬롯("+")과 완전히 같은 모습으로 보여야 한다. 군단장 슬롯에도
        // 인벤토리와 동일한 구조로 Add_1("+" 표시)이 이미 있으므로, 그걸 그대로 켜고/끄는 방식으로 맞춘다.
        private void RefreshCommanderSlots()
        {
            foreach (var pair in commanderSlots)
            {
                var slotTransform = pair.Value;
                if (slotTransform == null)
                {
                    continue;
                }

                var itemFrame = slotTransform.Find("ItemFrame_01");
                if (itemFrame == null)
                {
                    continue;
                }

                var commanderLevelText = slotTransform.Find("Text_Level")?.GetComponent<TMP_Text>();

                var icon = itemFrame.Find("Item")?.GetComponent<Image>();
                var normalArea = itemFrame.Find("NormalArea");
                var addIndicator = itemFrame.Find("Add_1")?.gameObject;
                if (icon == null)
                {
                    continue;
                }

                if (EquipmentInventoryRuntime.TryGetEquipped(pair.Key, out var equipped))
                {
                    if (addIndicator != null)
                    {
                        addIndicator.SetActive(false);
                    }

                    if (normalArea != null)
                    {
                        normalArea.gameObject.SetActive(true);
                    }

                    icon.gameObject.SetActive(true);
                    partIconSprites.TryGetValue(pair.Key, out var fallbackSprite);
                    icon.sprite = EquipmentLevelIconResolver.Resolve(
                        pair.Key,
                        equipped.ItemLevel,
                        fallbackSprite ?? equipped.Definition.Icon);
                    icon.color = Color.white; // 아이콘은 고유 색 그대로 유지
                    if (commanderLevelText != null)
                    {
                        commanderLevelText.text = $"Lv.{equipped.ItemLevel}";
                        commanderLevelText.gameObject.SetActive(true);
                    }

                    ApplyFrameVariant(normalArea, equipped.Grade); // 등급에 맞는 기존 프레임(테두리)으로 교체
                }
                else
                {
                    if (normalArea != null)
                    {
                        normalArea.gameObject.SetActive(false);
                    }

                    icon.gameObject.SetActive(false);
                    if (commanderLevelText != null)
                    {
                        commanderLevelText.text = string.Empty;
                        commanderLevelText.gameObject.SetActive(false);
                    }

                    if (addIndicator != null)
                    {
                        addIndicator.SetActive(true); // 인벤토리 빈 슬롯과 동일한 "+" 표시
                    }
                }
            }
        }

        private void RefreshCommanderStats()
        {
            var stats = EquipmentLegionBonusCalculator.CalculateTotal();
            var summaryTypes = new[] { EquipmentStatType.AttackPower, EquipmentStatType.MaxHealth,
                EquipmentStatType.Defense, EquipmentStatType.AttackSpeed, EquipmentStatType.MoveSpeed,
                EquipmentStatType.CriticalRate };
            for (var index = 0; index < equipmentStatSummaryValues.Length && index < summaryTypes.Length; index++)
            {
                if (equipmentStatSummaryValues[index] != null)
                    equipmentStatSummaryValues[index].text = FormatStatValue(summaryTypes[index], stats.GetValue(summaryTypes[index]));
            }


            if (statGrid != null)
            {
                for (var i = 0; i < statGrid.childCount; i++)
                {
                    var card = statGrid.GetChild(i);
                    var label = card.Find("Label")?.GetComponent<TMP_Text>();
                    var value = card.Find("Value")?.GetComponent<TMP_Text>();
                    if (label == null || value == null)
                    {
                        continue;
                    }

                    var statType = ResolveStatType(label.text);
                    value.text = FormatStatValue(statType, stats.GetValue(statType));
                }
            }

            if (commanderSummaryText != null)
            {
                const string powerText = "군단 장비 보너스";
                commanderSummaryText.text = PowerPattern.IsMatch(commanderSummaryText.text)
                    ? PowerPattern.Replace(commanderSummaryText.text, powerText)
                    : powerText;
            }
        }

        // StatGrid 카드는 6개(공격력/체력/방어력/공격속도/이동속도/치명타)뿐이라 나머지 7개 능력치
        // (치피/스킬·보스·일반 몬스터 피해/쿨감/방관/피해감소)는 아직 표시할 카드가 없다. 카드가
        // 추가되면 이 매핑에 항목을 늘리면 된다.
        private static EquipmentStatType ResolveStatType(string labelText)
        {
            if (labelText.Contains("공격속도")) return EquipmentStatType.AttackSpeed;
            if (labelText.Contains("이동속도")) return EquipmentStatType.MoveSpeed;
            if (labelText.Contains("공격")) return EquipmentStatType.AttackPower;
            if (labelText.Contains("방어")) return EquipmentStatType.Defense;
            if (labelText.Contains("치명")) return EquipmentStatType.CriticalRate;
            return EquipmentStatType.MaxHealth; // "체력"
        }

        private static string FormatStatValue(EquipmentStatType statType, float value)
        {
            return $"+{value:0.#}%";
        }
    }
}
