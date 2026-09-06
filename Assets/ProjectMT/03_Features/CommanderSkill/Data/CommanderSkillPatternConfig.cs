using System;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    public enum CommanderSkillPatternType { Single, Burst, Barrage, Pulse, PersistentArea, Chain }
    [Serializable]
    public sealed class CommanderSkillPatternConfig
    {
        [SerializeField] private CommanderSkillPatternType type;
        [SerializeField, Min(1)] private int repeatCount = 1;
        [SerializeField, Min(0f)] private float repeatInterval;
        [SerializeField, Min(0.01f)] private float duration = 1f;
        [SerializeField, Min(0.01f)] private float tickInterval = 1f;
        [SerializeField, Min(0f)] private float randomRadius;
        [SerializeField] private bool firstBarrageHitAtTarget;
        [SerializeField, Min(1)] private int chainCount = 1;
        [SerializeField, Min(0.1f)] private float chainRadius = 4f;
        public CommanderSkillPatternType Type => type;
        public int RepeatCount => Mathf.Max(1, repeatCount);
        public float RepeatInterval => Mathf.Max(0f, repeatInterval);
        public float Duration => Mathf.Max(0.01f, duration);
        public float TickInterval => Mathf.Max(0.01f, tickInterval);
        public float RandomRadius => Mathf.Max(0f, randomRadius);
        public bool FirstBarrageHitAtTarget => firstBarrageHitAtTarget;
        public int ChainCount => Mathf.Max(1, chainCount);
        public float ChainRadius => Mathf.Max(0.1f, chainRadius);
        public bool TryValidate(out string error)
        {
            if (!Enum.IsDefined(typeof(CommanderSkillPatternType), type)) { error = "Pattern type is invalid."; return false; }
            if (type is CommanderSkillPatternType.Burst or CommanderSkillPatternType.Barrage or CommanderSkillPatternType.Pulse)
            {
                if (repeatCount < 1 || !FiniteNonNegative(repeatInterval) || (type == CommanderSkillPatternType.Barrage && !FiniteNonNegative(randomRadius)))
                { error = "Pattern repeat values are invalid."; return false; }
            }
            else if (type == CommanderSkillPatternType.PersistentArea)
            {
                if (!FinitePositive(duration) || !FinitePositive(tickInterval) || tickInterval > duration)
                { error = "Persistent area timing is invalid."; return false; }
            }
            else if (type == CommanderSkillPatternType.Chain && (chainCount < 1 || !FinitePositive(chainRadius) || !FiniteNonNegative(repeatInterval)))
            { error = "Chain values are invalid."; return false; }
            error = string.Empty; return true;
        }
#if UNITY_EDITOR
        public void EditorConfigure(CommanderSkillPatternType patternType, int hits, float interval, float areaDuration,
            float areaTickInterval, float spreadRadius, int links, float linkRadius,
            bool guaranteeFirstBarrageHitAtTarget = false)
        {
            type = patternType; repeatCount = Mathf.Max(1, hits); repeatInterval = Mathf.Max(0f, interval);
            duration = Mathf.Max(0.01f, areaDuration); tickInterval = Mathf.Max(0.01f, areaTickInterval);
            randomRadius = Mathf.Max(0f, spreadRadius); chainCount = Mathf.Max(1, links); chainRadius = Mathf.Max(0.1f, linkRadius);
            firstBarrageHitAtTarget = guaranteeFirstBarrageHitAtTarget;
        }
#endif
        private static bool FinitePositive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool FiniteNonNegative(float value) => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
