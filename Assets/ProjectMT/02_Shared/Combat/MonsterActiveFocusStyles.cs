using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public enum MonsterActiveFocusStyle { Flash, Spotlight, SnapSlow, DarkSnap, LightPillar, EnergyBurst, ClassicDim }

    public static class MonsterActiveFocusStyles
    {
        public static MonsterActiveFocusStyle Current { get; set; } = MonsterActiveFocusStyle.Flash;
        public static readonly string[] Labels =
        {
            "1 · 전신 발광", "2 · 암전 스포트", "3 · 순간 슬로우",
            "4 · 암전 + 슬로우", "5 · 빛기둥", "6 · 에너지 폭발", "7 · 기존 암전 + 발광"
        };
        public static float Lead(MonsterActiveFocusStyle style) => (style == MonsterActiveFocusStyle.Flash || style == MonsterActiveFocusStyle.ClassicDim) ? 0f :
            style == MonsterActiveFocusStyle.LightPillar ? 0.22f : 0.18f;
        public static float Dim(MonsterActiveFocusStyle style) => style switch
        {
            MonsterActiveFocusStyle.ClassicDim => 0.6f,
            MonsterActiveFocusStyle.Spotlight => 0.65f,
            MonsterActiveFocusStyle.DarkSnap => 0.65f,
            MonsterActiveFocusStyle.LightPillar => 0.48f,
            MonsterActiveFocusStyle.EnergyBurst => 0.56f,
            _ => 0f
        };
        public static bool Slows(MonsterActiveFocusStyle style) =>
            style == MonsterActiveFocusStyle.SnapSlow || style == MonsterActiveFocusStyle.DarkSnap ||
            style == MonsterActiveFocusStyle.EnergyBurst;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Current = MonsterActiveFocusStyle.Flash; }
    }
}
