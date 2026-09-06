using System;
using System.Collections.Generic;
using ProjectMT.Shared.CommanderSkill;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AP = ProjectMT.Features.CommanderSkill.CommanderSkillAwakeningParameter;

namespace ProjectMT.Features.CommanderSkill
{
    public sealed partial class CommanderSkillPageController
    {
        [SerializeField] private Sprite awakeningFilledStar;
        [SerializeField] private Sprite awakeningEmptyStar;
        [SerializeField] private Sprite filterNormalSprite;
        [SerializeField] private Sprite filterSelectedSprite;

        private Image[] detailStars;
        private TMP_Text awakeningSummaryText;
        private TMP_Text awakeningTitleText;
        private TMP_Text duplicateCostText;
        private TMP_Text levelCapText;
        private TMP_Text equippedCountText;
        private bool presentationCached;

        private static Color32 RarityColor(CommanderSkillRarity rarity) => rarity switch // 소환 결과와 같은 등급색
        {
            CommanderSkillRarity.Rare => new Color32(91, 178, 246, 255),
            CommanderSkillRarity.Epic => new Color32(187, 117, 246, 255),
            CommanderSkillRarity.Legendary => new Color32(255, 199, 80, 255),
            CommanderSkillRarity.Mythic => new Color32(255, 91, 133, 255),
            _ => new Color32(191, 202, 218, 255)
        };

        private static string BuildRarityCategory(CommanderSkillDefinition definition, bool detail = true)
        {
            var name = definition.Rarity switch
            {
                CommanderSkillRarity.Rare => "희귀", CommanderSkillRarity.Epic => "영웅",
                CommanderSkillRarity.Legendary => "전설", CommanderSkillRarity.Mythic => "신화", _ => "일반"
            };
            var category = GetCategoryLabel(definition.Category);
            if (!detail) category = category.Replace("형", string.Empty);
            return $"<color=#{ColorUtility.ToHtmlStringRGB(RarityColor(definition.Rarity))}>{name}</color><pos={ (detail ? 50 : 44) }>· {category}";
        }

        private static Image[] CacheStars(Transform root)
        {
            var starRoot = FindDeep(root, "AwakeningStars");
            if (starRoot == null) return Array.Empty<Image>();
            var result = new Image[5];
            for (var i = 0; i < result.Length; i++) result[i] = starRoot.Find($"Star_{i + 1}")?.GetComponent<Image>();
            return result;
        }

        private void RefreshStars(Image[] images, bool owned, int level)
        {
            if (images == null) return;
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i] == null) continue;
                images[i].enabled = owned;
                images[i].sprite = i < Mathf.Clamp(level, 0, 5) ? awakeningFilledStar : awakeningEmptyStar;
                images[i].color = i < level ? Color.white : new Color32(73, 66, 56, 255);
            }
        }

        private string BuildAwakeningAvailability(OwnedCommanderSkillView owned)
        {
            if (catalog?.BalanceConfig == null) return string.Empty;
            if (owned.AwakeningLevel >= catalog.BalanceConfig.MaxAwakening) return "최대 각성";
            return catalog.BalanceConfig.TryGetAwakeningCost(owned.AwakeningLevel, out var cost) && owned.DuplicateCount >= cost
                ? "각성 가능" : string.Empty;
        }

        private void RefreshGrowthPresentation(CommanderSkillProgressView view)
        {
            if (!presentationCached)
            {
                detailStars = CacheStars(FindDeep(transform, "SkillDetailPanel"));
                awakeningSummaryText = FindDeep(transform, "SelectedSkillAwakeningSummary")?.GetComponent<TMP_Text>();
                awakeningTitleText = FindDeep(transform, "SelectedSkillAwakeningTitle")?.GetComponent<TMP_Text>();
                duplicateCostText = FindDeep(transform, "SelectedSkillDuplicateCost")?.GetComponent<TMP_Text>();
                levelCapText = FindDeep(transform, "SelectedSkillLevelCap")?.GetComponent<TMP_Text>();
                equippedCountText = FindDeep(transform, "EquippedSkillCount")?.GetComponent<TMP_Text>();
                presentationCached = true;
            }
            var hasOwned = TryGetOwnedSkill(selectedSkillId, out var owned);
            RefreshStars(detailStars, hasOwned, owned.AwakeningLevel);
            var count = 0;
            for (var i = 0; i < slots.Length; i++) if (!string.IsNullOrEmpty(view.GetEquippedSkillId(i))) count++;
            if (equippedCountText != null) equippedCountText.text = $"{count} / {slots.Length}";
            if (levelCapText != null) levelCapText.text = hasOwned && TryGetGrowthRule(owned.SkillId, out var rule) ? $"/ {rule.MaxLevel}" : string.Empty;
            if (awakeningTitleText != null) awakeningTitleText.text = !hasOwned ? "각성" : owned.AwakeningLevel >= 5 ? "최대 각성" : "다음 각성";
            void Set(string name, string value) { var text = FindDeep(transform, name)?.GetComponent<TMP_Text>(); if (text != null) text.text = value; }
            Set("SelectedSkillAwakeningTarget", hasOwned && owned.AwakeningLevel < 5 ? $"{owned.AwakeningLevel + 1}성" : string.Empty);
            var hasDefinition = hasOwned && catalog.TryGet(owned.SkillId, out _);
            if (hasDefinition)
            {
                catalog.TryGet(owned.SkillId, out var definition);
                var next = BuildNextAwakening(definition, owned.AwakeningLevel);
                var split = next.IndexOf("   ", StringComparison.Ordinal);
                if (awakeningSummaryText != null) { awakeningSummaryText.text = split >= 0 ? next.Substring(0, split) : next; awakeningSummaryText.enableAutoSizing = true; awakeningSummaryText.fontSizeMin = 13; awakeningSummaryText.fontSizeMax = 19; }
                Set("SelectedSkillAwakeningValue", split >= 0 ? next.Substring(split + 3) : string.Empty);
                var growth = GetSupportGrowth(owned.SkillId, owned.Level);
                Set("SelectedSkillCooldown", $"{growth.Resolve(AP.Cooldown, definition.Cooldown):0.#}초");
                var repeated = definition.Pattern.RepeatCount > 1;
                Set("SelectedSkillMetricLabel", repeated ? "발사 횟수" : "사거리");
                Set("SelectedSkillMetricValue", repeated ? $"{growth.Resolve(AP.RepeatCount, definition.Pattern.RepeatCount):0}회" : $"{growth.Resolve(AP.TargetRange, definition.TargetRange):0.#}m");
            }
            else
            {
                if (awakeningSummaryText != null) awakeningSummaryText.text = "스킬을 선택해 주세요";
                foreach (var name in new[] { "SelectedSkillAwakeningValue", "SelectedSkillCooldown", "SelectedSkillMetricLabel", "SelectedSkillMetricValue" }) Set(name, string.Empty);
            }
            if (duplicateCostText != null)
            {
                var costAvailable = catalog.BalanceConfig.TryGetAwakeningCost(owned.AwakeningLevel, out var cost);
                duplicateCostText.text = !hasOwned ? string.Empty : !costAvailable ? "최대 각성" : $"중복 {owned.DuplicateCount:N0} / {cost:N0}";
                duplicateCostText.color = !costAvailable || owned.DuplicateCount >= cost
                    ? new Color32(207, 193, 149, 255) : new Color32(162, 151, 132, 255);
            }
        }

        private string BuildPresentationDescription(CommanderSkillDefinition definition, int level)
        {
            if (definition.SkillId == "CS_TrackingBlade")
            {
                foreach (var effect in definition.Effects)
                    if (effect is CommanderMarkEffectDefinition mark)
                        return $"<line-height=26>가까운 적에게 마력검을 연속 발사합니다.\n{GetSupportGrowth(definition.SkillId, level).Resolve(AP.MarkRequiredHits, mark.RequiredHits, mark.EffectId):0}회 타격 시 추가 단일 피해를 줍니다.</line-height>";
            }
            return "<size=15><line-height=22>" + definition.Description + "</line-height></size>";
        }

        private static string BuildNextAwakening(CommanderSkillDefinition definition, int star)
        {
            if (star >= 5) return "이후 중복 획득 시 골드로 전환됩니다.";
            if (definition.AwakeningStages.Count <= star) return "각성 특성 데이터 미설정";
            var previous = star > 0 ? definition.AwakeningStages[star - 1].CopyModifiers() : Array.Empty<CommanderSkillAwakeningModifier>();
            var before = definition.CaptureAwakening(star);
            var after = definition.CaptureAwakening(star + 1);
            var changes = new List<string>();
            foreach (var modifier in definition.AwakeningStages[star].CopyModifiers())
            {
                var unchanged = false;
                foreach (var old in previous)
                    if (old.Parameter == modifier.Parameter && old.TargetEffectId == modifier.TargetEffectId &&
                        old.Operation == modifier.Operation && old.Value == modifier.Value) { unchanged = true; break; }
                if (unchanged) continue;
                string label, unit;
                float baseValue;
                switch (modifier.Parameter)
                {
                    case AP.Cooldown: label = "쿨타임"; unit = "초"; baseValue = definition.Cooldown; break;
                    case AP.TargetRange: label = "사거리"; unit = "m"; baseValue = definition.TargetRange; break;
                    case AP.RepeatCount: label = definition.SkillId == "CS_TrackingBlade" ? "마력검 발사 횟수" : "발사 횟수"; unit = "회"; baseValue = definition.Pattern.RepeatCount; break;
                    case AP.ChainCount: label = "연쇄 대상"; unit = "명"; baseValue = definition.Pattern.ChainCount; break;
                    case AP.ChainRadius: label = "연쇄 반경"; unit = "m"; baseValue = definition.Pattern.ChainRadius; break;
                    default: return BuildAwakeningSummary(definition, star).Replace($"{star + 1}성: ", string.Empty);
                }
                var line = $"{label}   {before.Resolve(modifier.Parameter, baseValue, modifier.TargetEffectId):0.##} → {after.Resolve(modifier.Parameter, baseValue, modifier.TargetEffectId):0.##}{unit}";
                if (!changes.Contains(line)) changes.Add(line);
            }
            return changes.Count == 0 ? "각성 효과 유지" : string.Join("\n", changes);
        }
    }
}
