using System;
using UnityEngine;

namespace ProjectMT.Features.Settings
{
    [Serializable]
    public sealed class LocalSettingsData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public bool sleepModeEnabled = true;
        public int sleepDelayMinutes = 5;
        public int qualityLevel = 1;
        public int targetFrameRate = 60;
        public bool damageNumbersVisible = true;
        public bool unitHealthBarsVisible = true;
        public float bgmVolume = 1f;
        public float sfxVolume = 1f;
        public bool vibrationEnabled = true;

        public LocalSettingsData Clone()
        {
            return JsonUtility.FromJson<LocalSettingsData>(JsonUtility.ToJson(this));
        }

        public void Normalize()
        {
            version = CurrentVersion;
            sleepDelayMinutes = sleepDelayMinutes is 1 or 3 or 5 or 10 ? sleepDelayMinutes : 5;
            qualityLevel = Mathf.Clamp(qualityLevel, 0, 2);
            targetFrameRate = targetFrameRate == 30 ? 30 : 60;
            bgmVolume = Mathf.Clamp01(bgmVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);
        }
    }

    public static class LocalSettingsStore
    {
        private const string PlayerPrefsKey = "ProjectMT.LocalSettings.v1";

        public static LocalSettingsData Load()
        {
            var json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            var data = string.IsNullOrWhiteSpace(json)
                ? CreateDefaults()
                : JsonUtility.FromJson<LocalSettingsData>(json) ?? CreateDefaults();
            data.Normalize();
            return data;
        }

        public static void Save(LocalSettingsData data)
        {
            if (data == null)
            {
                return;
            }

            data.Normalize();
            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static LocalSettingsData CreateDefaults()
        {
            return new LocalSettingsData();
        }
    }
}
