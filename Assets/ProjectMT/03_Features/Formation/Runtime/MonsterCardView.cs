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
        private const float NotOwnedCardAlpha = 100f / 255f; // 도감 미보유 카드 흐림 표시(배경·테두리·몬스터 공통)
        private const float DefaultAscensionStarY = -116f;
        private const float CollectionAscensionStarY = -108f;

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
        [SerializeField] private Image rarityBorderHighlight;
        [Header("Rarity Pattern Background")]
        [SerializeField] private Image rarityPatternBackground;
        [SerializeField] private Sprite commonPatternBackground;
        [SerializeField] private Sprite rarePatternBackground;
        [SerializeField] private Sprite epicPatternBackground;
        [SerializeField] private Sprite legendaryPatternBackground;
        [SerializeField] private Sprite mythicPatternBackground;
        [SerializeField] private Image[] ascensionStars = Array.Empty<Image>();
        [SerializeField] private Sprite ascensionFilledStar;
        [SerializeField] private Sprite ascensionEmptyStar;

        [Header("Monster Collection Reward")]
        [SerializeField] private GameObject collectionRewardRoot;
        [SerializeField] private Button collectionRewardButton;
        [SerializeField] private Image collectionRewardBackground;
        [SerializeField] private Image collectionRewardIcon;
        [SerializeField] private TMP_Text collectionRewardLabel;

        private string monsterId;
        private Action<string> selectedAction;
        private Action<string> collectionRewardAction;

        internal MonsterRarityCatalog RarityCatalog => rarityCatalog;
        public Button ClickButton => button;
        public bool IsAssigned => assignmentBadge != null && assignmentBadge.activeSelf;
        public bool IsBreakthroughReady => breakthroughReadyBadge != null && breakthroughReadyBadge.activeSelf;

        private void Awake()
        {
            button?.onClick.AddListener(HandleClicked);
            collectionRewardButton?.onClick.AddListener(HandleCollectionRewardClicked);
        }

        private void OnDestroy()
        {
            button?.onClick.RemoveListener(HandleClicked);
            collectionRewardButton?.onClick.RemoveListener(HandleCollectionRewardClicked);
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
            collectionRewardAction = null;
            collectionRewardRoot?.SetActive(false);
            if (portraitImage != null)
            {
                portraitImage.sprite = definition.Portrait;
                portraitImage.color = definition.Portrait == null
                    ? new Color(0.2f, 0.24f, 0.3f, 1f)
                    : Color.white;
            }

            SetText(nameLabel, definition.DisplayName);
            SetNameVisible(true);
            SetText(levelLabel, owned.Level.ToString());
            levelBadge?.SetActive(true);
            var rarity = MonsterRarity.Common;
            rarityCatalog?.TryGetRarity(definition.MonsterId, out rarity);
            ApplyRarity(rarity);
            ApplyCollectionStarLayout(false);
            ApplyAscension(owned.AscensionLevel);
            ApplyAssignment(assignment);
            breakthroughReadyBadge?.SetActive(
                owned.AscensionMaterialCount > 0 &&
                !MonsterAscension.IsMaxAscension(owned.AscensionLevel));
            selectionFrame?.SetActive(selected);
            if (button != null)
            {
                button.interactable = true;
            }
        }

        // 도감 목록 전용(레벨·편성 배지 없음). 미보유 몬스터는 흐리게 표시한다.
        public void BindCatalogEntry(
            MonsterDefinition definition,
            MonsterRarity rarity,
            bool isOwned,
            int ascensionLevel,
            bool fiveStarRewardClaimed,
            Action<string> onClaimFiveStarReward)
        {
            if (definition == null)
            {
                BindEmpty(string.Empty);
                return;
            }

            monsterId = definition.MonsterId;
            selectedAction = null;
            if (portraitImage != null)
            {
                portraitImage.sprite = definition.Portrait;
                portraitImage.color = definition.Portrait == null
                    ? new Color(0.2f, 0.24f, 0.3f, 1f)
                    : Color.white;
            }

            SetText(nameLabel, definition.DisplayName);
            SetNameVisible(true);
            SetText(levelLabel, string.Empty);
            levelBadge?.SetActive(false);
            ApplyRarity(rarity);
            ApplyCollectionStarLayout(true);
            ApplyAscension(isOwned ? ascensionLevel : 0);
            ApplyCollectionReward(
                isOwned,
                ascensionLevel,
                fiveStarRewardClaimed,
                onClaimFiveStarReward);
            assignmentBadge?.SetActive(false);
            breakthroughReadyBadge?.SetActive(false);
            selectionFrame?.SetActive(false);
            ApplyOwnershipAlpha(isOwned);
            if (button != null)
            {
                button.interactable = true;
            }
        }

        // ApplyRarity가 이미 정상 알파를 세팅해두므로, 미보유일 때만 카드 전체를 흐리게 낮춘다.
        private void ApplyOwnershipAlpha(bool isOwned)
        {
            if (isOwned)
            {
                return;
            }

            SetColor(portraitImage, new Color(NotOwnedCardAlpha, NotOwnedCardAlpha, NotOwnedCardAlpha, NotOwnedCardAlpha));
            SetAlpha(rarityBackground, NotOwnedCardAlpha);
            SetAlpha(rarityInnerBorder, NotOwnedCardAlpha);
            SetAlpha(rarityHighlight, NotOwnedCardAlpha);
            SetAlpha(rarityBorderHighlight, NotOwnedCardAlpha);
            SetAlpha(rarityPatternBackground, NotOwnedCardAlpha);
            SetAlpha(rarityAura, NotOwnedCardAlpha);
        }

        private static void SetAlpha(Graphic target, float alpha)
        {
            if (target == null)
            {
                return;
            }

            var color = target.color;
            color.a = alpha;
            target.color = color;
        }

        public void BindEmpty(string label)
        {
            monsterId = null;
            selectedAction = null;
            collectionRewardAction = null;
            collectionRewardRoot?.SetActive(false);
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
            ApplyCollectionStarLayout(false);
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
            GetRarityPalette(rarity, out var background, out var innerBorder, out var highlight);
            SetColor(rarityBackground, background);
            SetColor(rarityInnerBorder, innerBorder);
            SetColor(rarityHighlight, highlight);
            ApplyRarityBorderHighlight(rarity);
            ApplyRarityPatternBackground(rarity);

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

        // showStars가 false면(도감 미리보기 카드) 별을 아예 그리지 않는다.
        private void ApplyAscension(int ascensionLevel, bool showStars = true)
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
                star.enabled = showStars && star.sprite != null;
            }
        }

        private void ApplyAssignment(string assignment)
        {
            var hasAssignment = !string.IsNullOrEmpty(assignment);
            assignmentBadge?.SetActive(hasAssignment);
            if (!hasAssignment || assignmentBadge == null)
            {
                SetText(assignmentLabel, string.Empty);
                return;
            }

            var isReserve = assignment.StartsWith("예비", StringComparison.Ordinal);
            var spaceIndex = assignment.LastIndexOf(' ');
            var partyLabel = isReserve ? "예비" : "본대";
            SetText(assignmentLabel, spaceIndex >= 0 ? $"{partyLabel}\n{assignment.Substring(spaceIndex + 1)}" : partyLabel);
            foreach (var image in assignmentBadge.GetComponentsInChildren<Image>(true))
            {
                switch (image.name)
                {
                    case "Bg": image.color = isReserve ? new Color32(0x91, 0x5A, 0xE0, 0xFF) : new Color32(0x1B, 0x70, 0xD3, 0xFF); break;
                    case "InnerBorder": image.color = isReserve ? new Color32(0xC6, 0x79, 0xEF, 0xFF) : new Color32(0x21, 0x91, 0xDE, 0xFF); break;
                    case "Border": image.color = isReserve ? new Color32(0x00, 0x00, 0x00, 0xFF) : new Color32(0x00, 0x04, 0x08, 0xFF); break;
                }
            }
        }

        // 도감에서는 별 아래에 고정 보상 띠가 들어가므로 별만 살짝 올려 서로 겹치지 않게 한다.
        private void ApplyCollectionStarLayout(bool isCollection)
        {
            var targetY = isCollection ? CollectionAscensionStarY : DefaultAscensionStarY;
            foreach (var star in ascensionStars)
            {
                if (star == null)
                {
                    continue;
                }

                var rect = star.rectTransform;
                var position = rect.anchoredPosition;
                position.y = targetY;
                rect.anchoredPosition = position;
            }
        }

        private void ApplyCollectionReward(
            bool isOwned,
            int ascensionLevel,
            bool rewardClaimed,
            Action<string> onClaim)
        {
            collectionRewardRoot?.SetActive(true);
            var reachedFiveStar = isOwned && MonsterAscension.IsMaxAscension(ascensionLevel);
            var claimable = reachedFiveStar && !rewardClaimed;
            collectionRewardAction = claimable ? onClaim : null;

            if (collectionRewardButton != null)
            {
                collectionRewardButton.interactable = claimable;
            }

            if (!reachedFiveStar)
            {
                SetText(collectionRewardLabel, "5성  500");
                SetColor(collectionRewardBackground, new Color32(0x22, 0x23, 0x27, 0xEE));
                SetColor(collectionRewardLabel, new Color32(0xA5, 0xA8, 0xAF, 0xFF));
                SetColor(collectionRewardIcon, new Color(1f, 1f, 1f, 0.48f));
                return;
            }

            if (rewardClaimed)
            {
                SetText(collectionRewardLabel, "수령 완료");
                SetColor(collectionRewardBackground, new Color32(0x30, 0x31, 0x35, 0xF2));
                SetColor(collectionRewardLabel, new Color32(0xB8, 0xBA, 0xC0, 0xFF));
                SetColor(collectionRewardIcon, new Color(1f, 1f, 1f, 0.58f));
                return;
            }

            SetText(collectionRewardLabel, "500 받기");
            SetColor(collectionRewardBackground, new Color32(0x72, 0x4C, 0x16, 0xFF));
            SetColor(collectionRewardLabel, new Color32(0xFF, 0xEC, 0xB0, 0xFF));
            SetColor(collectionRewardIcon, Color.white);
        }
        private void SetNameVisible(bool visible)
        {
            if (nameLabel != null)
            {
                nameLabel.gameObject.SetActive(visible);
            }
        }

        // 뽑기 결과처럼 카드 본문 밖에 이름/수량을 따로 표시하는 화면에서는
        // 현재 PF_MonsterCard의 실제 참조를 사용해 카드 내부 문맥 배지를 숨긴다.
        internal void HideContextBadgesForResult()
        {
            SetNameVisible(false);
            levelBadge?.SetActive(false);
            assignmentBadge?.SetActive(false);
            breakthroughReadyBadge?.SetActive(false);
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

        private void ApplyRarityBorderHighlight(MonsterRarity rarity)
        {
            if (rarityBorderHighlight == null)
            {
                return;
            }

            var showSpecialBorder = rarity == MonsterRarity.Legendary || rarity == MonsterRarity.Mythic;
            var specialBorder = rarityBorderHighlight.transform.parent;
            (specialBorder != null ? specialBorder.gameObject : rarityBorderHighlight.gameObject).SetActive(showSpecialBorder);
            if (!showSpecialBorder)
            {
                return;
            }

            rarityBorderHighlight.color = rarity == MonsterRarity.Legendary
                ? new Color32(0xFF, 0xF3, 0xAD, 0xFF)
                : new Color32(0xF7, 0x4D, 0x52, 0xFF);
        }
        private void ApplyRarityPatternBackground(MonsterRarity rarity)
        {
            if (rarityPatternBackground == null)
            {
                return;
            }

            Sprite patternSprite;
            switch (rarity)
            {
                case MonsterRarity.Rare:
                    patternSprite = rarePatternBackground;
                    break;
                case MonsterRarity.Epic:
                    patternSprite = epicPatternBackground;
                    break;
                case MonsterRarity.Legendary:
                    patternSprite = legendaryPatternBackground;
                    break;
                case MonsterRarity.Mythic:
                    patternSprite = mythicPatternBackground;
                    break;
                default:
                    patternSprite = commonPatternBackground;
                    break;
            }

            rarityPatternBackground.sprite = patternSprite;
            rarityPatternBackground.color = Color.white;
            rarityPatternBackground.enabled = patternSprite != null;
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

        private void HandleCollectionRewardClicked()
        {
            if (!string.IsNullOrEmpty(monsterId))
            {
                collectionRewardAction?.Invoke(monsterId);
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


        public void EditorConfigureCollectionReward(
            GameObject rewardRoot,
            Button rewardButton,
            Image rewardBackground,
            Image rewardIcon,
            TMP_Text rewardLabel)
        {
            collectionRewardRoot = rewardRoot;
            collectionRewardButton = rewardButton;
            collectionRewardBackground = rewardBackground;
            collectionRewardIcon = rewardIcon;
            collectionRewardLabel = rewardLabel;
        }
#endif
    }
}

