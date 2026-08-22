using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Skill/Generic Passive", fileName = "MP_Generic")]
    public sealed class GenericMonsterPassiveSkill : MonsterPassiveSkill
    {
        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            if (PresentationTier > MonsterSkillPresentationTier.Standard || Recipe.Effects.Count > 2)
            {
                error = $"Generic passive presentation/effect count exceeds the shared readability cap. Skill={SkillId}";
                return false;
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string title,
            string body,
            MonsterSkillPresentationTier tier,
            MonsterSkillRecipe skillRecipe,
            Sprite skillIcon = null)
        {
            EditorConfigureCommon(id, title, body, tier, skillRecipe, skillIcon);
        }
#endif
    }
}
