using System;
using UnityEngine;

namespace ProjectMT.Features.Settings
{
    [Serializable]
    public sealed class LocalSettingsData
    {
        public const int CurrentVersion = 2;

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
        public bool accountSessionInitialized;
        public bool accountLoggedIn;
        public string accountProvider = "guest";
        public string guestUserId;

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
            accountProvider = string.IsNullOrWhiteSpace(accountProvider)
                ? "guest"
                : accountProvider.Trim().ToLowerInvariant();
            guestUserId = string.IsNullOrWhiteSpace(guestUserId)
                ? Guid.NewGuid().ToString("N")
                : guestUserId.Trim();
        }
    }

    public static class LocalSettingsStore
    {
        private const string PlayerPrefsKey = "ProjectMT.LocalSettings.v1";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedFrameRateBeforeSceneLoad()
        {
            ApplyTargetFrameRate();
        }

        public static int ApplyTargetFrameRate()
        {
            var targetFrameRate = Load().targetFrameRate;
            Application.targetFrameRate = targetFrameRate;
            return targetFrameRate;
        }

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

    public static class AccountSessionStore // 외부 인증 전 로컬 게스트 세션 기준
    {
        public static LocalSettingsData Prepare(bool hadExistingSave)
        {
            var data = LocalSettingsStore.Load();
            if (!data.accountSessionInitialized)
            {
                data.accountSessionInitialized = true;
                data.accountLoggedIn = hadExistingSave; // 기존 플레이어는 최초 1회 자동 로그인 승계
                data.accountProvider = "guest";
                LocalSettingsStore.Save(data);
            }

            return data;
        }

        public static LocalSettingsData LoginAsGuest()
        {
            var data = LocalSettingsStore.Load();
            data.accountSessionInitialized = true;
            data.accountLoggedIn = true;
            data.accountProvider = "guest";
            LocalSettingsStore.Save(data);
            return data;
        }

        public static LocalSettingsData Logout()
        {
            var data = LocalSettingsStore.Load();
            data.accountSessionInitialized = true;
            data.accountLoggedIn = false;
            data.accountProvider = "guest";
            LocalSettingsStore.Save(data);
            return data;
        }

        public static bool IsLoggedIn => LocalSettingsStore.Load().accountLoggedIn;

        public static string GuestUserId => LocalSettingsStore.Load().guestUserId;

        public static string FormatUserId(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return "-";
            }

            var normalized = userId.Trim();
            return normalized.Length <= 16
                ? normalized.ToUpperInvariant()
                : $"{normalized[..8].ToUpperInvariant()}-{normalized[^8..].ToUpperInvariant()}";
        }
    }
}
