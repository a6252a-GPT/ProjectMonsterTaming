using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    [CreateAssetMenu(menuName = "ProjectMT/Commander Skill/Effects/Recorded Hit Damage", fileName = "CSEffect_RecordedHitDamage")]
    public sealed class CommanderRecordedHitDamageEffectDefinition : CommanderSkillEffectDefinition
    {
        [SerializeField, Min(0f)] private float baseMultiplier = 0.4f;
        [SerializeField, Min(0f)] private float multiplierPerRecordedHit = 0.12f;
        [SerializeField, Min(0)] private int maximumRecordedHits = 20;

        public float BaseMultiplier => Mathf.Max(0f, baseMultiplier);
        public float MultiplierPerRecordedHit => Mathf.Max(0f, multiplierPerRecordedHit);
        public int MaximumRecordedHits => Mathf.Max(0, maximumRecordedHits);
        public float ResolveMultiplier(int recordedHits) =>
            BaseMultiplier + Mathf.Min(Mathf.Max(0, recordedHits), MaximumRecordedHits) * MultiplierPerRecordedHit;

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error)) return false;
            if (baseMultiplier < 0f || multiplierPerRecordedHit < 0f || maximumRecordedHits < 0 ||
                float.IsNaN(baseMultiplier) || float.IsInfinity(baseMultiplier) ||
                float.IsNaN(multiplierPerRecordedHit) || float.IsInfinity(multiplierPerRecordedHit))
            {
                error = $"{EffectId}: recorded hit damage values are invalid.";
                return false;
            }
            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(string id, float initialMultiplier, float perHit, int hitCap)
        {
            EditorConfigureId(id);
            baseMultiplier = Mathf.Max(0f, initialMultiplier);
            multiplierPerRecordedHit = Mathf.Max(0f, perHit);
            maximumRecordedHits = Mathf.Max(0, hitCap);
        }
#endif
    }
}
