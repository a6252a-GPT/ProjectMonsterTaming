using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    public enum CommanderSkillTargetTeam
    {
        Enemy,
        Ally
    }

    public enum CommanderSkillTargetSelection
    {
        Nearest,
        LowestHealth
    }

    [CreateAssetMenu(menuName = "ProjectMT/Commander Skill/Rules/Targeting", fileName = "CSTargeting_NearestEnemy")]
    public sealed class CommanderSkillTargetingDefinition : ScriptableObject // 대상 진영·선정 방식·거리 SO
    {
        [SerializeField] private CommanderSkillTargetTeam targetTeam = CommanderSkillTargetTeam.Enemy;
        [SerializeField] private CommanderSkillTargetSelection selection = CommanderSkillTargetSelection.Nearest;
        [SerializeField, Min(1f)] private float range = 100f;

        public CommanderSkillTargetTeam TargetTeam => targetTeam;
        public CommanderSkillTargetSelection Selection => selection;
        public float Range => Mathf.Max(1f, range);

        public bool TryValidate(out string error)
        {
            if (!System.Enum.IsDefined(typeof(CommanderSkillTargetTeam), targetTeam) ||
                !System.Enum.IsDefined(typeof(CommanderSkillTargetSelection), selection) ||
                range < 1f || float.IsNaN(range) || float.IsInfinity(range))
            {
                error = "Target team, selection, or range is invalid.";
                return false;
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            CommanderSkillTargetTeam team,
            CommanderSkillTargetSelection targetSelection,
            float targetRange)
        {
            targetTeam = team;
            selection = targetSelection;
            range = Mathf.Max(1f, targetRange);
        }
#endif
    }
}
