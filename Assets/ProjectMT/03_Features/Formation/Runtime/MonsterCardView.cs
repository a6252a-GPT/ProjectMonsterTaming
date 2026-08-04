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
        [SerializeField] private Button button;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private GameObject assignmentBadge;
        [SerializeField] private TMP_Text assignmentLabel;
        [SerializeField] private GameObject selectionFrame;

        private string monsterId;
        private Action<string> selectedAction;

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
                    : ClampTint(definition.VisualTint);
            }

            SetText(nameLabel, definition.DisplayName);
            SetText(levelLabel, $"Lv. {owned.Level}");
            var hasAssignment = !string.IsNullOrEmpty(assignment);
            assignmentBadge?.SetActive(hasAssignment);
            SetText(assignmentLabel, assignment);
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
            SetText(levelLabel, string.Empty);
            assignmentBadge?.SetActive(false);
            selectionFrame?.SetActive(false);
            if (button != null)
            {
                button.interactable = false;
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (button != null && !string.IsNullOrEmpty(monsterId))
            {
                button.interactable = interactable;
            }
        }

        public void ApplyFont(TMP_FontAsset font)
        {
            if (font == null)
            {
                return;
            }

            foreach (var label in GetComponentsInChildren<TMP_Text>(true))
            {
                label.font = font;
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

        private static Color ClampTint(Color tint)
        {
            return new Color(
                Mathf.Clamp01(tint.r),
                Mathf.Clamp01(tint.g),
                Mathf.Clamp01(tint.b),
                1f);
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Button cardButton,
            Image portrait,
            TMP_Text monsterName,
            TMP_Text level,
            GameObject badge,
            TMP_Text badgeText,
            GameObject selectedFrame)
        {
            button = cardButton;
            portraitImage = portrait;
            nameLabel = monsterName;
            levelLabel = level;
            assignmentBadge = badge;
            assignmentLabel = badgeText;
            selectionFrame = selectedFrame;
        }
#endif
    }
}
