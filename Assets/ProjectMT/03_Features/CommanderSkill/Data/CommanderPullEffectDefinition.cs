using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    public enum CommanderSkillPullCenter { ImpactPosition, CastOrigin }

    [CreateAssetMenu(menuName = "ProjectMT/Commander Skill/Effects/Weak Pull", fileName = "CSEffect_Pull")]
    public sealed class CommanderPullEffectDefinition : CommanderSkillEffectDefinition
    {
        [SerializeField] private CommanderSkillPullCenter center;
        [SerializeField] private float distance = 0.6f;
        [SerializeField] private float duration = 0.2f;
        [SerializeField] private float stopDistance = 2f;
        [SerializeField] private int maxTargets = 4;
        public CommanderSkillPullCenter Center => center;
        public float Distance => distance;
        public float Duration => duration;
        public float StopDistance => stopDistance;
        public int MaxTargets => maxTargets;
        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error)) return false;
            if (!System.Enum.IsDefined(typeof(CommanderSkillPullCenter), center) ||
                !Finite(distance) || distance <= 0f || distance > 0.75f ||
                !Finite(duration) || duration < 0.05f || duration > 0.2f ||
                !Finite(stopDistance) || stopDistance < (center == CommanderSkillPullCenter.CastOrigin ? 2f : 0.5f) ||
                maxTargets < 1 || maxTargets > 6)
            { error = "약한 당김은 거리 ≤0.75m, 시간 0.05~0.2초, 최대6명 및 중심 여유거리를 지켜야 합니다."; return false; }
            error = string.Empty; return true;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
#if UNITY_EDITOR
        public void EditorConfigure(string id, CommanderSkillPullCenter mode, float meters, float seconds, float clearance, int targets)
        { EditorConfigureId(id); center = mode; distance = meters; duration = seconds; stopDistance = clearance; maxTargets = targets; }
#endif
    }
}
