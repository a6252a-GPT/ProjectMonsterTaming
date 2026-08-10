using ProjectMT.Features.Formation;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class GachaResultItemView : MonoBehaviour // 뽑기 결과 한 종류 표시
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

        public void Bind(
            MonsterDefinition definition,
            OwnedMonsterView owned,
            MonsterRarity rarity,
            int count,
            bool isNew)
        {
            monsterCard?.BindMonster(definition, owned, false, string.Empty, null);
            monsterCard?.SetInteractable(false);
            cardName?.SetActive(false);
            levelBadge?.SetActive(false);
            assignmentBadge?.SetActive(false);
            breakthroughReadyBadge?.SetActive(false);
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
