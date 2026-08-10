using System;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Formation
{
    [DisallowMultipleComponent]
    public sealed class MonsterCardView : MonoBehaviour // 보유 목록·편성 슬롯 공용 카드
    {
        private static readonly Color32 CardSurfaceColor = new Color32(0x22, 0x24, 0x2B, 0xFF);

        [SerializeField] private Button button;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private GameObject levelBadge;
        [SerializeField] private GameObject assignmentBadge;
        [SerializeField] private TMP_Text assignmentLabel;
        [SerializeField] private GameObject breakthroughReadyBadge;
        [SerializeField] private GameObject selectionFrame;

        [Header("Rarity & Ascension")]
        [SerializeField] private MonsterRarityCatalog rarityCatalog;
        [SerializeField] private Image rarityBackground;
        [SerializeField] private Image rarityAura;
        [SerializeField] private Image rarityInnerBorder;
        [SerializeField] private Image rarityHighlight;
        [SerializeField] private Image[] ascensionStars = Array.Empty<Image>();
        [SerializeField] private Sprite ascensionFilledStar;
        [SerializeField] private Sprite ascensionEmptyStar;

        private string monsterId;
        private Action<string> selectedAction;

        internal MonsterRarityCatalog RarityCatalog => rarityCatalog;

        private void Awake()
        {
            button?.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            button?.onClick.RemoveListener(HandleClicked);
        }

        public void BindMonster(
            MonsterDefinition definition,
            OwnedMonsterView owned,
            bool selected,
            string assignment,
            Action<string> onSelected)
        {
            if (definition == null)
            {
                BindEmpty("정보 없음");
                return;
            }

            monsterId = owned.MonsterId;
            selectedAction = onSelected;
            if (portraitImage != null)
            {
                portraitImage.sprite = definition.Portrait;
                portraitImage.color = definition.Portrait == null
                    ? new Color(0.2f, 0.24f, 0.3f, 1f)
                    : Color.white;
            }

            SetText(nameLabel, definition.DisplayName);
            SetNameVisible(false);
            SetText(levelLabel, $"Lv. {owned.Level}");
            levelBadge?.SetActive(true);
            var rarity = MonsterRarity.Common;
            rarityCatalog?.TryGetRarity(definition.MonsterId, out rarity);
            ApplyRarity(rarity);
            ApplyAscension(owned.AscensionLevel);
            var hasAssignment = !string.IsNullOrEmpty(assignment);
            assignmentBadge?.SetActive(hasAssignment);
            SetText(assignmentLabel, assignment);
            breakthroughReadyBadge?.SetActive(
                owned.AscensionMaterialCount > 0 &&
                !MonsterAscension.IsMaxAscension(owned.AscensionLevel));
            selectionFrame?.SetActive(selected);
            if (button != null)
            {
                button.interactable = true;
            }
        }

        public void BindEmpty(string label)
        {
            monsterId = null;
            selectedAction = null;
            if (portraitImage != null)
            {
                portraitImage.sprite = null;
                portraitImage.color = new Color(0.1f, 0.12f, 0.16f, 0.75f);
            }

            SetText(nameLabel, label);
            SetNameVisible(true);
            SetText(levelLabel, string.Empty);
            levelBadge?.SetActive(false);
            ApplyRarity(MonsterRarity.Common);
            ApplyAscension(0);
            assignmentBadge?.SetActive(false);
            breakthroughReadyBadge?.SetActive(false);
            selectionFrame?.SetActive(false);
            if (button != null)
            {
                button.interactable = false;
            }
        }

        private void ApplyRarity(MonsterRarity rarity)
        {
            GetRarityPalette(rarity, out _, out var innerBorder, out var highlight);
            SetColor(rarityBackground, CardSurfaceColor);
            SetColor(rarityInnerBorder, innerBorder);
            SetColor(rarityHighlight, highlight);

            var hasPortrait = portraitImage != null && portraitImage.sprite != null;
            if (hasPortrait)
            {
                portraitImage.color = Color.white;
            }

            if (rarityAura != null)
            {
                GetRarityAuraStyle(rarity, out var auraScale, out var auraAlpha);
                Color auraColor = highlight;
                auraColor.a = auraAlpha;
                rarityAura.color = auraColor;
                rarityAura.rectTransform.localScale = new Vector3(auraScale, auraScale, 1f);
                rarityAura.gameObject.SetActive(hasPortrait);
            }
        }

        private static void GetRarityAuraStyle(
            MonsterRarity rarity,
            out float scale,
            out float alpha)
        {
            switch (rarity)
            {
                case MonsterRarity.Rare:
                    scale = 1f;
                    alpha = 0.66f;
                    return;
                case MonsterRarity.Epic:
                    scale = 1f;
                    alpha = 0.74f;
                    return;
                case MonsterRarity.Legendary:
                case MonsterRarity.Mythic:
                    scale = 1f;
                    alpha = 0.82f;
                    return;
                default:
                    scale = 1f;
                    alpha = 0.58f;
                    return;
            }
        }

        private void ApplyAscension(int ascensionLevel)
        {
            var filledCount = Mathf.Clamp(ascensionLevel, 0, MonsterAscension.MaxAscensionLevel);
            for (var index = 0; index < ascensionStars.Length; index++)
            {
                var star = ascensionStars[index];
                if (star == null)
                {
                    continue;
                }

                star.sprite = index < filledCount ? ascensionFilledStar : ascensionEmptyStar;
                star.color = Color.white;
                star.enabled = star.sprite != null;
            }
        }

        private void SetNameVisible(bool visible)
        {
            if (nameLabel != null)
            {
                nameLabel.gameObject.SetActive(visible);
            }
        }

        internal static void GetRarityPalette(
            MonsterRarity rarity,
            out Color32 background,
            out Color32 innerBorder,
            out Color32 highlight)
        {
            switch (rarity)
            {
                case MonsterRarity.Rare:
                    background = new Color32(0x31, 0x5E, 0xA2, 0xFF);
                    innerBorder = new Color32(0x39, 0x76, 0xC9, 0xFF);
                    highlight = new Color32(0x42, 0xA1, 0xCE, 0xFF);
                    return;
                case MonsterRarity.Epic:
                    background = new Color32(0x84, 0x45, 0xB0, 0xFF);
                    innerBorder = new Color32(0xB5, 0x5D, 0xD6, 0xFF);
                    highlight = new Color32(0xD6, 0x93, 0xEF, 0xFF);
                    return;
                case MonsterRarity.Legendary:
                    background = new Color32(0xCA, 0xB0, 0x46, 0xFF);
                    innerBorder = new Color32(0xE7, 0xD3, 0x4A, 0xFF);
                    highlight = new Color32(0xFF, 0xF3, 0xA5, 0xFF);
                    return;
                case MonsterRarity.Mythic:
                    background = new Color32(0x9B, 0x1F, 0x1B, 0xFF);
                    innerBorder = new Color32(0xD6, 0x37, 0x35, 0xFF);
                    highlight = new Color32(0xFF, 0x6D, 0x73, 0xFF);
                    return;
                default:
                    background = new Color32(0x54, 0x51, 0x4D, 0xFF);
                    innerBorder = new Color32(0x70, 0x71, 0x70, 0xFF);
                    highlight = new Color32(0x8C, 0x8E, 0x8C, 0xFF);
                    return;
            }
        }

        private static void SetColor(Graphic target, Color color)
        {
            if (target != null)
            {
                target.color = color;
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (button != null && !string.IsNullOrEmpty(monsterId))
            {
                button.interactable = interactable;
            }
        }

        private void HandleClicked()
        {
            if (!string.IsNullOrEmpty(monsterId))
            {
                selectedAction?.Invoke(monsterId);
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Button cardButton,
            Image portrait,
            TMP_Text monsterName,
            TMP_Text level,
            GameObject badge,
            TMP_Text badgeText,
            GameObject selectedFrame,
            Image aura = null,
            GameObject breakthroughBadge = null)
        {
            button = cardButton;
            portraitImage = portrait;
            nameLabel = monsterName;
            levelLabel = level;
            assignmentBadge = badge;
            assignmentLabel = badgeText;
            breakthroughReadyBadge = breakthroughBadge;
            selectionFrame = selectedFrame;
            rarityAura = aura;
        }

        public void EditorPreview(MonsterRarity rarity, int ascensionLevel)
        {
            ApplyRarity(rarity);
            ApplyAscension(ascensionLevel);
        }
#endif
    }
}
