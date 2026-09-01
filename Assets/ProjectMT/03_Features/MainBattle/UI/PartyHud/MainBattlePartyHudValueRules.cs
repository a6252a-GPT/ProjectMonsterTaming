using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    public readonly struct MainBattlePartyHudEnergyState
    {
        public MainBattlePartyHudEnergyState(bool hasCapacity, bool hasColoredFill, float fillRatio)
        {
            HasCapacity = hasCapacity;
            HasColoredFill = hasColoredFill;
            FillRatio = Mathf.Clamp01(fillRatio);
        }

        public bool HasCapacity { get; }
        public bool HasColoredFill { get; }
        public float FillRatio { get; }
    }

    public static class MainBattlePartyHudValueRules // HUD 표시값을 런타임 수치와 분리해 검증
    {
        public const float EnergyGlowStartRatio = 0.8f;
        public const float EnergyGlowPulseRatio = 0.9f;

        public static MainBattlePartyHudEnergyState ResolveEnergy(float currentEnergy, float capacity)
        {
            var safeCapacity = Mathf.Max(0f, capacity);
            var safeEnergy = Mathf.Clamp(currentEnergy, 0f, safeCapacity);
            var hasCapacity = safeCapacity > 0.0001f;
            var ratio = hasCapacity ? safeEnergy / safeCapacity : 0f;
            return new MainBattlePartyHudEnergyState(hasCapacity, hasCapacity && safeEnergy > 0.0001f, ratio);
        }

        public static float ResolveHealthRatio(float currentHealth, float maxHealth)
        {
            return maxHealth > 0.0001f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        }

        public static float ResolveEnergyGlowIntensity(MainBattlePartyHudEnergyState state)
        {
            if (!state.HasColoredFill || state.FillRatio < EnergyGlowStartRatio)
            {
                return 0f;
            }

            var ramp = Mathf.InverseLerp(EnergyGlowStartRatio, EnergyGlowPulseRatio, state.FillRatio);
            return Mathf.Lerp(0.25f, 1f, ramp); // 80%에서 은은하게 시작해 90%에 완전히 켠다.
        }

        public static bool ShouldPulseEnergy(MainBattlePartyHudEnergyState state)
        {
            return state.HasColoredFill && state.FillRatio >= EnergyGlowPulseRatio;
        }

        public static bool ShouldPlayDamageFeedback(bool hasPreviousSample, float previousHealth, float currentHealth)
        {
            return hasPreviousSample && currentHealth < previousHealth - 0.0001f;
        }
    }
}
