using MoreMountains.Feedbacks;
using ProjectMT.Shared.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Integrations.Feel
{
    [DisallowMultipleComponent]
    public sealed class MonsterActiveFocusFeelAdapter : MonoBehaviour, IMonsterActiveFocusFeedback
    {
        [SerializeField] private MMF_Player entryPlayer;
        [SerializeField] private MMF_Player releasePlayer;
        [SerializeField] private Image screenFlash;
        [SerializeField] private Image panelPulse;
        [SerializeField] private Image energySweep;
        [SerializeField] private Image releaseGlow;

        private bool entryInitialized;
        private bool releaseInitialized;

        private void Awake()
        {
            SetVisualsInvisible();
        }

        private void OnDisable()
        {
            StopImmediate();
        }

        public void PlayEnter(Color accentColor, bool isMythic)
        {
            StopImmediate();
            ConfigureEntryColors(accentColor, isMythic);
            Play(entryPlayer, ref entryInitialized);
        }

        public void PlayRelease(Color accentColor, bool isMythic)
        {
            ResetPlayer(entryPlayer, ref entryInitialized);
            ResetPlayer(releasePlayer, ref releaseInitialized);
            ConfigureReleaseColors(accentColor, isMythic);
            Play(releasePlayer, ref releaseInitialized);
        }

        public void StopImmediate()
        {
            ResetPlayer(entryPlayer, ref entryInitialized);
            ResetPlayer(releasePlayer, ref releaseInitialized);
            SetVisualsInvisible();
        }

        private void ConfigureEntryColors(Color accentColor, bool isMythic)
        {
            var flashColor = Color.Lerp(accentColor, Color.white, 0.42f);
            var sweepColor = Color.Lerp(accentColor, Color.white, 0.62f);
            ConfigureImageFeedback(
                entryPlayer,
                screenFlash,
                flashColor,
                isMythic ? 0.12f : 0.085f,
                0.18f);
            ConfigureImageFeedback(
                entryPlayer,
                panelPulse,
                accentColor,
                isMythic ? 0.16f : 0.11f,
                0.30f);
            ConfigureImageFeedback(
                entryPlayer,
                energySweep,
                sweepColor,
                isMythic ? 0.28f : 0.20f,
                0.42f);
        }

        private void ConfigureReleaseColors(Color accentColor, bool isMythic)
        {
            ConfigureImageFeedback(
                releasePlayer,
                releaseGlow,
                Color.Lerp(accentColor, Color.white, 0.3f),
                isMythic ? 0.10f : 0.07f,
                0.35f);
        }

        private static void ConfigureImageFeedback(
            MMF_Player player,
            Image target,
            Color color,
            float peakAlpha,
            float peakTime)
        {
            if (player?.FeedbacksList == null || target == null)
            {
                return;
            }

            color.a = 1f;
            foreach (var feedback in player.FeedbacksList)
            {
                if (feedback is MMF_Image imageFeedback && imageFeedback.BoundImage == target)
                {
                    imageFeedback.ColorOverTime = CreatePulseGradient(color, peakAlpha, peakTime);
                }
            }
        }

        private static Gradient CreatePulseGradient(Color color, float peakAlpha, float peakTime)
        {
            return new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, Mathf.Clamp01(peakTime)),
                    new GradientColorKey(color, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(Mathf.Clamp01(peakAlpha), Mathf.Clamp01(peakTime)),
                    new GradientAlphaKey(0f, 1f)
                }
            };
        }

        private static void Play(MMF_Player player, ref bool initialized)
        {
            if (player == null)
            {
                return;
            }
            player.Initialization(true);
            initialized = true;
            player.PlayFeedbacks(Vector3.zero, 1f);
        }

        private static void ResetPlayer(MMF_Player player, ref bool initialized)
        {
            if (player == null || !initialized)
            {
                return;
            }
            player.StopFeedbacks();
            player.RestoreInitialValues();
            player.ResetFeedbacks();
            initialized = false;
        }

        private void SetVisualsInvisible()
        {
            SetInvisible(screenFlash);
            SetInvisible(panelPulse);
            SetInvisible(energySweep);
            SetInvisible(releaseGlow);
        }

        private static void SetInvisible(Image image)
        {
            if (image == null)
            {
                return;
            }
            var color = image.color;
            color.a = 0f;
            image.color = color;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            MMF_Player configuredEntryPlayer,
            MMF_Player configuredReleasePlayer,
            Image configuredScreenFlash,
            Image configuredPanelPulse,
            Image configuredEnergySweep,
            Image configuredReleaseGlow)
        {
            entryPlayer = configuredEntryPlayer;
            releasePlayer = configuredReleasePlayer;
            screenFlash = configuredScreenFlash;
            panelPulse = configuredPanelPulse;
            energySweep = configuredEnergySweep;
            releaseGlow = configuredReleaseGlow;
            SetVisualsInvisible();
        }
#endif
    }
}
