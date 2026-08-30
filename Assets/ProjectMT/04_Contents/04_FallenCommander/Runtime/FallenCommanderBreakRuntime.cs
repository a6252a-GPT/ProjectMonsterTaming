using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    public sealed class FallenCommanderBreakRuntime
    {
        private const float BreakGaugeDamageScale = 5f;

        public bool IsBroken { get; private set; }
        public float CurrentGauge { get; private set; }
        public float RemainingTime { get; private set; }
        public float RemainingGauge(float maxGauge) =>
            Mathf.Max(0f, Mathf.Max(1f, maxGauge) - CurrentGauge);

        public bool ApplyHit(
            float maxGauge,
            float gaugePerHit,
            float attackPowerMultiplier,
            float phaseMultiplier)
        {
            if (IsBroken)
            {
                return false;
            }

            var safeMaxGauge = Mathf.Max(1f, maxGauge);
            var damage = Mathf.Max(0f, gaugePerHit) *
                Mathf.Max(0f, attackPowerMultiplier) *
                BreakGaugeDamageScale *
                Mathf.Max(0f, phaseMultiplier);
            CurrentGauge = Mathf.Min(safeMaxGauge, CurrentGauge + damage);
            return CurrentGauge >= safeMaxGauge;
        }

        public void Enter(float duration)
        {
            IsBroken = true;
            RemainingTime = Mathf.Max(0f, duration);
        }

        public bool Tick(float deltaTime)
        {
            if (!IsBroken)
            {
                return false;
            }

            RemainingTime = Mathf.Max(0f, RemainingTime - deltaTime);
            return RemainingTime <= 0f;
        }

        public void Exit()
        {
            IsBroken = false;
            RemainingTime = 0f;
            CurrentGauge = 0f;
        }

        public void Reset()
        {
            Exit();
        }
    }
}
