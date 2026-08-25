using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Formation
{
    [DisallowMultipleComponent]
    public sealed class MonsterRosterListView : MonoBehaviour // 보유 카드 스크롤·재사용 풀
    {
        public const int MaxCardCount = 100;

        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private MonsterCardView cardPrefab;

        private readonly List<MonsterCardView> cards = new List<MonsterCardView>();
        private TMP_Text countLabel;
        private bool countLabelResolved;

        public IReadOnlyList<MonsterCardView> Cards
        {
            get
            {
                CacheAuthoredCards();
                return cards;
            }
        }

        public RectTransform ContentRoot => contentRoot;

        private void Awake()
        {
            CacheAuthoredCards();
        }

        public int EnsureCardCount(int requestedCount)
        {
            CacheAuthoredCards();
            var visibleCount = Mathf.Clamp(requestedCount, 0, MaxCardCount);
            if (cardPrefab != null && contentRoot != null)
            {
                while (cards.Count < visibleCount)
                {
                    var card = Instantiate(cardPrefab, contentRoot);
                    card.name = $"MonsterCard_{cards.Count + 1:000}";
                    cards.Add(card);
                }
            }

            var availableCount = Mathf.Min(visibleCount, cards.Count);
            for (var index = 0; index < cards.Count; index++)
            {
                var card = cards[index];
                if (card != null)
                {
                    card.gameObject.SetActive(index < availableCount);
                }
            }

            if (contentRoot != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(contentRoot);
            }

            return availableCount;
        }

        // 등급별 도감 진행률 표시. 루트 하위의 "Count" 자식 오브젝트를 찾아 텍스트만 갱신한다.
        public void SetCountText(string text)
        {
            if (!countLabelResolved)
            {
                countLabelResolved = true;
                countLabel = transform.Find("Count")?.GetComponent<TMP_Text>();
            }

            if (countLabel != null)
            {
                countLabel.text = text;
            }
        }

        public void SetCardsInteractable(bool interactable)
        {
            CacheAuthoredCards();
            for (var index = 0; index < cards.Count; index++)
            {
                if (cards[index] != null && cards[index].gameObject.activeSelf)
                {
                    cards[index].SetInteractable(interactable);
                }
            }
        }

        public void ResetScrollPosition()
        {
            if (scrollRect == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;
        }

        private void CacheAuthoredCards()
        {
            if (cards.Count > 0 || contentRoot == null)
            {
                return;
            }

            foreach (Transform child in contentRoot)
            {
                var card = child.GetComponent<MonsterCardView>();
                if (card != null)
                {
                    cards.Add(card);
                }
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            ScrollRect rosterScrollRect,
            RectTransform rosterContentRoot,
            MonsterCardView monsterCardPrefab)
        {
            scrollRect = rosterScrollRect;
            contentRoot = rosterContentRoot;
            cardPrefab = monsterCardPrefab;
            cards.Clear();
            CacheAuthoredCards();
        }
#endif
    }
}
