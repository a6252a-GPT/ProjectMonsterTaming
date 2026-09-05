using ProjectMT.Features.Formation;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class GachaResultItemView : MonoBehaviour // 뽑기 결과 한 장 표시
    {
        [SerializeField] private MonsterCardView monsterCard;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text rarityText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private GameObject newBadge;
        [SerializeField] private GameObject cardName;
        [SerializeField] private GameObject levelBadge;
        [SerializeField] private GameObject assignmentBadge;
        [SerializeField] private GameObject breakthroughReadyBadge;

        public MonsterRarity Rarity { get; private set; }

        private readonly System.Collections.Generic.List<CanvasGroup> frontGroups = new System.Collections.Generic.List<CanvasGroup>();
        private readonly System.Collections.Generic.List<float> frontAlphas = new System.Collections.Generic.List<float>();
        [SerializeField] private GameObject backFace;
        [SerializeField] private GachaSummonSealGraphic rarityBackGlow;
        public bool IsBackVisible => backFace != null && backFace.activeSelf;

        public void PrepareBack(Sprite sprite, bool showRarityGlow = false)
        {
            RestoreFront();
            var cardRect = monsterCard != null ? (RectTransform)monsterCard.transform : (RectTransform)transform;
            if (monsterCard != null)
                foreach (var candidate in monsterCard.GetComponentsInChildren<RectTransform>(true))
                    if (candidate.name == "CardFrameArea") { cardRect = candidate; break; }
            var corners = new Vector3[4];
            cardRect.GetWorldCorners(corners);
            var lower = transform.InverseTransformPoint(corners[0]);
            var upper = transform.InverseTransformPoint(corners[2]);
            if (backFace == null || rarityBackGlow == null)
                throw new System.InvalidOperationException("The result card needs its authored back face and glow.");
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.gameObject == backFace) continue;
                var group = child.GetComponent<CanvasGroup>();
                if (group == null)
                    throw new System.InvalidOperationException("A result card front needs an authored CanvasGroup: " + child.name);
                frontGroups.Add(group);
                frontAlphas.Add(group.alpha);
                group.alpha = 0f;
            }

            // 앞면 프레임의 실제 크기를 따라가며, 카드 자체의 모양/컴포넌트는 원본에 있다.
            var rect = (RectTransform)backFace.transform;
            rect.sizeDelta = new Vector2(upper.x - lower.x, upper.y - lower.y);
            rect.anchoredPosition = (lower + upper) * 0.5f;
            backFace.GetComponent<UnityEngine.UI.Image>().sprite = sprite;
            backFace.SetActive(true);
            rarityBackGlow.gameObject.SetActive(showRarityGlow && Rarity >= MonsterRarity.Legendary);
            rarityBackGlow.color = Rarity == MonsterRarity.Mythic
                ? new Color(1f, 0.22f, 0.38f, 0.025f)
                : new Color(1f, 0.76f, 0.25f, 0.025f);
        }

        public void ShowFront()
        {
            if (backFace != null) backFace.SetActive(false);
            for (var i = 0; i < frontGroups.Count; i++)
                if (frontGroups[i] != null) frontGroups[i].alpha = frontAlphas[i];
        }

        public void RestoreFront()
        {
            ShowFront();
            if (rarityBackGlow != null) rarityBackGlow.gameObject.SetActive(false);
            frontGroups.Clear();
            frontAlphas.Clear();
        }


        public void Bind(
            MonsterDefinition definition,
            OwnedMonsterView owned,
            MonsterRarity rarity,
            int count,
            bool isNew)
        {
            Rarity = rarity;
            monsterCard?.BindMonster(definition, owned, false, string.Empty, null);
            monsterCard?.SetInteractable(false);
            monsterCard?.HideContextBadgesForResult();
            if (cardName != null) cardName.SetActive(true); // 관리창의 이름 표시를 그대로 재사용한다.
            if (nameText != null)
            {
                nameText.text = definition == null ? "정보 없음" : definition.DisplayName;
            }

            if (rarityText != null)
            {
                rarityText.text = RarityLabel(rarity);
            }

            if (countText != null)
            {
                countText.text = $"× {Mathf.Max(1, count)}";
                countText.gameObject.SetActive(count > 1);
            }

            newBadge?.SetActive(isNew);
        }

        private static string RarityLabel(MonsterRarity rarity)
        {
            switch (rarity)
            {
                case MonsterRarity.Common: return "일반";
                case MonsterRarity.Rare: return "희귀";
                case MonsterRarity.Epic: return "영웅";
                case MonsterRarity.Legendary: return "전설";
                case MonsterRarity.Mythic: return "신화";
                default: return rarity.ToString();
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            MonsterCardView card,
            TMP_Text monsterName,
            TMP_Text rarityLabel,
            TMP_Text amount,
            GameObject acquiredBadge,
            GameObject cardNameObject = null,
            GameObject level = null,
            GameObject assignment = null,
            GameObject breakthroughReady = null)
        {
            monsterCard = card;
            nameText = monsterName;
            rarityText = rarityLabel;
            countText = amount;
            newBadge = acquiredBadge;
            cardName = cardNameObject;
            levelBadge = level;
            assignmentBadge = assignment;
            breakthroughReadyBadge = breakthroughReady;
        }
#endif
    }
}
