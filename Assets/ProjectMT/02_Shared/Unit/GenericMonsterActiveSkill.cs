using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Skill/Generic Active", fileName = "MS_Generic")]
    public sealed class GenericMonsterActiveSkill : MonsterActiveSkill
    {
        public override MonsterActiveExecutionKind ExecutionKind => MonsterActiveExecutionKind.Generic;

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            if (Recipe.Effects.Count > 3)
            {
                error = $"Generic active effect count exceeds the shared readability cap. Skill={SkillId}";
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
            int cost = 1000,
            Sprite skillIcon = null,
            float energyPerSecond = 40f,
            float energyPerBasicAttackHit = 120f,
            float energyPerDamageReceived = 80f)
        {
            EditorConfigureCommon(id, title, body, tier, skillRecipe, skillIcon);
            EditorSetEnergyCost(cost);
            EditorSetEnergyGeneration(energyPerSecond, energyPerBasicAttackHit, energyPerDamageReceived);
        }
#endif
    }
}
