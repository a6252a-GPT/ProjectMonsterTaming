using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    public abstract class CommanderSkillEffectDefinition : ScriptableObject // 효과 데이터 공통 경계
    {
        [SerializeField] private string effectId;

        public string EffectId => effectId?.Trim() ?? string.Empty;

        public virtual bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(EffectId))
            {
                error = "Effect id is empty.";
                return false;
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        protected void EditorConfigureId(string id)
        {
            effectId = id?.Trim() ?? string.Empty;
        }
#endif
    }
}
