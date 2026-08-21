using System;
using UnityEngine;

namespace ProjectMT.Shared.Audio
{
    public static class AudioRuntimeSettings // 전역 AudioSource·Mixer 공통 음량 기준
    {
        public static float BgmVolume { get; private set; } = 1f;
        public static float SfxVolume { get; private set; } = 1f;
        public static bool VibrationEnabled { get; private set; } = true;

        public static event Action Changed;

        public static void Apply(float bgm, float sfx, bool vibration)
        {
            BgmVolume = Mathf.Clamp01(bgm);
            SfxVolume = Mathf.Clamp01(sfx);
            VibrationEnabled = vibration;
            Changed?.Invoke();
        }

        public static float ToDecibels(float linear)
        {
            return Mathf.Clamp(20f * Mathf.Log10(Mathf.Max(0.0001f, linear)), -80f, 0f);
        }
    }
}
