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

        public void Bind(CommanderSkillDefinition definition, int quantity, bool isNew)
        {
            if (definition == null)
            {
                gameObject.SetActive(false);
                return;
            }

            var accent = ResolveAccent(definition.SkillId);
            if (frame != null)
            {
                frame.color = new Color(accent.r, accent.g, accent.b, 0.95f);
            }

            if (glow != null)
            {
                glow.color = new Color(accent.r, accent.g, accent.b, 0.30f);
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
            }

            newBadge?.SetActive(isNew);
            gameObject.SetActive(true);
        }

        private static Color ResolveAccent(string skillId)
        {
            return skillId == ProjectMT.Shared.CommanderSkill.CommanderSkillIds.IceCrystalOrb
                ? new Color32(76, 206, 241, 255)
                : new Color32(245, 125, 54, 255);
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
