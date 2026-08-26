using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Skill/Mythic Dedicated Active", fileName = "MS_Mythic")]
    public sealed class MythicMonsterActiveSkill : MonsterActiveSkill
    {
        [SerializeField] private string dedicatedExecutorId;

        public string DedicatedExecutorId => dedicatedExecutorId?.Trim() ?? string.Empty;
        public override MonsterActiveExecutionKind ExecutionKind => MonsterActiveExecutionKind.DedicatedMythic;

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(DedicatedExecutorId))
            {
                error = $"Mythic dedicated active requires an executor ID. Skill={SkillId}";
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
            MonsterSkillRecipe skillRecipe,
            string executorId,
            int cost = 1000,
            Sprite skillIcon = null)
        {
            EditorConfigureCommon(id, title, body, MonsterSkillPresentationTier.Mythic, skillRecipe, skillIcon);
            EditorSetEnergyCost(cost);
            dedicatedExecutorId = executorId?.Trim();
        }
#endif
    }
}
