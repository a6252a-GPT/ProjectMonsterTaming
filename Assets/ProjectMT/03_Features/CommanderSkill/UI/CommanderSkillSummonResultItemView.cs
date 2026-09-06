using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.CommanderSkill
{
    [DisallowMultipleComponent]
    public sealed class CommanderSkillSummonResultItemView : MonoBehaviour // 스킬 소환 결과 한 종류
    {
        [SerializeField] private Image frame;
        [SerializeField] private Image glow;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text quantityText;
        [SerializeField] private GameObject newBadge;

        [SerializeField] private CanvasGroup inscriptionFront;
        [SerializeField] private SkillInscriptionGraphic inscription;
        private Color accentColor;
        public CommanderSkillRarity Rarity { get; private set; }
        public bool IsInscribed { get; private set; } = true;
        public void SetInscription(float progress, float clock)
        {
            if (inscription == null || inscriptionFront == null) return;
            IsInscribed = progress >= 1f;
            inscription.gameObject.SetActive(!IsInscribed);
            inscription.Progress = progress;
            inscription.Clock = clock;
            inscription.color = Color.Lerp(new Color(.87f,.72f,.49f,.68f), accentColor, Mathf.Clamp01(progress*2));
            inscription.Redraw();
            float reveal = Mathf.Clamp01((progress-.67f)/.33f);
            inscriptionFront.alpha = reveal;
            float bounce = 1f + Mathf.Sin(reveal*Mathf.PI*3f)*Mathf.Exp(-reveal*4f)*.22f;
            inscriptionFront.transform.localScale = Vector3.one * (reveal <= 0 ? .82f : bounce);
        }

        public void Bind(CommanderSkillDefinition definition, int quantity, bool isNew)
        {
            if (definition == null)
            {
                gameObject.SetActive(false);
                return;
            }

            var accent = ResolveAccent(definition.Rarity);
            Rarity = definition.Rarity;
            accentColor = accent;
            if (inscription != null) inscription.Tier = (int)definition.Rarity;
            if (frame != null)
            {
                frame.color = Color.Lerp(new Color(.35f, .25f, .25f, 1f), accent, .72f);
            }

            if (glow != null)
            {
                glow.color = new Color(accent.r, accent.g, accent.b, 0.12f);
            }

            if (icon != null)
            {
                icon.sprite = definition.Icon;
                icon.enabled = definition.Icon != null;
                icon.preserveAspect = true;
            }

            if (nameText != null)
            {
                nameText.text = definition.DisplayName;
            }

            if (categoryText != null)
            {
                categoryText.text = definition.Category switch
                {
                    CommanderSkillCategory.Buff => "버프형",
                    CommanderSkillCategory.Debuff => "디버프형",
                    _ => "공격형"
                };
                categoryText.color = accent;
            }

            if (quantityText != null)
            {
                quantityText.text = $"×{Mathf.Max(1, quantity):N0}";
                quantityText.transform.parent.gameObject.SetActive(quantity > 1);
            }

            newBadge?.SetActive(isNew);
            gameObject.SetActive(true);
        }

        internal static Color ResolveAccent(CommanderSkillRarity rarity)
        {
            return rarity switch
            {
                CommanderSkillRarity.Rare => new Color32(91, 178, 246, 255),
                CommanderSkillRarity.Epic => new Color32(187, 117, 246, 255),
                CommanderSkillRarity.Legendary => new Color32(255, 199, 80, 255),
                CommanderSkillRarity.Mythic => new Color32(255, 91, 133, 255),
                _ => new Color32(191, 202, 218, 255)
            };
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Image resultFrame,
            Image resultGlow,
            Image resultIcon,
            TMP_Text resultName,
            TMP_Text resultCategory,
            TMP_Text resultQuantity,
            GameObject resultNewBadge)
        {
            frame = resultFrame;
            glow = resultGlow;
            icon = resultIcon;
            nameText = resultName;
            categoryText = resultCategory;
            quantityText = resultQuantity;
            newBadge = resultNewBadge;
        }
#endif
    }
}
