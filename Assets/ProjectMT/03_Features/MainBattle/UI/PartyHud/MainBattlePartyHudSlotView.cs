using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattlePartyHudSlotView : MonoBehaviour // 몬스터 한 칸의 분리 요소 표시
    {
        private static readonly Color ActiveStarColor = new Color32(255, 205, 65, 255);
        private static readonly Color InactiveStarColor = new Color32(103, 108, 118, 255);
        private static readonly Color MissionGlowColor = new Color32(119, 212, 173, 255);
        private static readonly Color MissionBorderColor = new Color32(131, 205, 179, 255);
        private static readonly Color MissionBackgroundTint = new Color32(30, 66, 57, 250);

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private MainBattlePartyHudPortraitGraphic portrait;
        [SerializeField] private MainBattlePartyHudRoundedGraphic healthFill;
        [SerializeField] private RectTransform healthFillRect;
        [SerializeField] private MainBattlePartyHudRoundedGraphic energyTrack;
        [SerializeField] private MainBattlePartyHudRoundedGraphic energyFill;
        [SerializeField] private RectTransform energyFillRect;
        [SerializeField] private MainBattlePartyHudRoundedGraphic energyGlow;
        [SerializeField] private MainBattlePartyHudRoundedGraphic energyGlowOuter;
        [SerializeField] private MainBattlePartyHudRoundedGraphic infoPanelBorder;
        [SerializeField] private MainBattlePartyHudRoundedGraphic infoPanelBackground;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private MainBattlePartyHudStarGraphic[] stars = new MainBattlePartyHudStarGraphic[5];

        private UnitActor actor;
        private bool hasPresentation;
        private bool hasHealthSample;
        private float previousHealth;
        private IMainBattlePartyHudDamageFeedback damageFeedback;
        private MonsterBattlePresentationSnapshot lastPresentation;
        private Color infoPanelBorderBaseColor;
        private Color infoPanelBackgroundBaseColor;
        private bool hasPanelBaseColors;

        public UnitActor Actor => actor;
        public bool HasPresentation => hasPresentation;
        public bool EnergyColoredFillVisible => energyFill != null && energyFill.gameObject.activeSelf;
        public float EnergyFillRatio => energyFillRect != null ? energyFillRect.anchorMax.x : 0f;
        public float HealthFillRatio => healthFillRect != null ? healthFillRect.anchorMax.x : 0f;
        public bool EnergyGlowVisible => energyGlow != null && energyGlow.gameObject.activeSelf;
        public float EnergyGlowIntensity { get; private set; }
        public int DamageFeedbackPlayCount { get; private set; }
        public TMP_Text LevelText => levelText;
        public MainBattlePartyHudStarGraphic[] Stars => stars;

        private void Awake()
        {
            SetInputBlocking(false);
            ResolveDamageFeedback();
            CapturePanelBaseColors();
        }

        public void Bind(UnitActor unit)
        {
            if (unit == null)
            {
                ShowMissing();
                return;
            }

            if (actor != unit)
            {
                ResetDamageTracking();
                actor = unit;
                lastPresentation = unit.Presentation;
                hasPresentation = lastPresentation.Portrait != null || lastPresentation.HasProgression;
                ApplyPresentation(lastPresentation);
            }

            RefreshValues();
        }

        public void ClearForNewRun()
        {
            ResetDamageTracking();
            actor = null;
            hasPresentation = false;
            lastPresentation = default;
            if (portrait != null)
            {
                portrait.Sprite = null;
            }

            if (levelText != null)
            {
                levelText.text = "Lv. -";
            }

            SetStars(0);
            SetFill(healthFillRect, healthFill, 0f, false);
            SetEnergy(MainBattlePartyHudValueRules.ResolveEnergy(0f, 0f));
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0.34f;
            }
        }

        public void ShowMissing()
        {
            if (actor != null)
            {
                ResetDamageTracking();
            }
            actor = null;
            SetFill(healthFillRect, healthFill, 0f, false);
            SetEnergy(MainBattlePartyHudValueRules.ResolveEnergy(0f, 0f));
            if (canvasGroup != null)
            {
                canvasGroup.alpha = hasPresentation ? 0.55f : 0.34f;
            }
        }

        public void RefreshValues()
        {
            if (actor == null)
            {
                ShowMissing();
                return;
            }

            var health = actor.Health;
            var currentHealth = health != null ? health.CurrentHealth : 0f;
            if (MainBattlePartyHudValueRules.ShouldPlayDamageFeedback(
                    hasHealthSample,
                    previousHealth,
                    currentHealth))
            {
                PlayDamageFeedback();
            }
            previousHealth = currentHealth;
            hasHealthSample = health != null;

            var healthRatio = health != null
                ? MainBattlePartyHudValueRules.ResolveHealthRatio(currentHealth, health.MaxHealth)
                : 0f;
            SetFill(healthFillRect, healthFill, healthRatio, healthRatio > 0f);

            var skillRuntime = actor.SkillRuntime;
            var energyState = skillRuntime != null
                ? MainBattlePartyHudValueRules.ResolveEnergy(skillRuntime.Energy, skillRuntime.EnergyCapacity)
                : MainBattlePartyHudValueRules.ResolveEnergy(0f, 0f);
            SetEnergy(energyState);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = actor.IsAlive ? 1f : 0.55f;
            }
        }

        private void ApplyPresentation(MonsterBattlePresentationSnapshot presentation)
        {
            if (portrait != null)
            {
                portrait.Sprite = presentation.Portrait;
            }

            if (levelText != null)
            {
                levelText.text = $"Lv. {(presentation.HasProgression ? presentation.Level : 1)}";
            }

            SetStars(presentation.HasProgression ? presentation.AscensionLevel : 0);
        }

        private void SetStars(int ascensionLevel)
        {
            var activeCount = Mathf.Clamp(ascensionLevel, 0, stars?.Length ?? 0);
            if (stars == null)
            {
                return;
            }

            for (var index = 0; index < stars.Length; index++)
            {
                if (stars[index] != null)
                {
                    stars[index].color = index < activeCount ? ActiveStarColor : InactiveStarColor;
                }
            }
        }

        private void SetEnergy(MainBattlePartyHudEnergyState state)
        {
            if (energyTrack != null)
            {
                energyTrack.color = new Color32(100, 106, 115, 255); // 기력 없음은 회색 트랙만 표시
            }

            SetFill(energyFillRect, energyFill, state.FillRatio, state.HasColoredFill);
            SetEnergyGlow(state);
        }

        private void SetEnergyGlow(MainBattlePartyHudEnergyState state)
        {
            CapturePanelBaseColors();
            var intensity = MainBattlePartyHudValueRules.ResolveEnergyGlowIntensity(state);
            if (intensity <= 0f || energyGlow == null)
            {
                ResetEnergyGlowVisuals();
                return;
            }

            var pulse = 1f;
            var pulseScale = 1f;
            if (MainBattlePartyHudValueRules.ShouldPulseEnergy(state))
            {
                var wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 1.2f);
                pulse = Mathf.Lerp(0.82f, 1f, wave);
                pulseScale = Mathf.Lerp(1f, 1.018f, wave); // 임무 강조보다 튀지 않게 호흡 폭을 줄인다.
            }

            EnergyGlowIntensity = intensity * pulse;
            SetGlowGraphic(energyGlowOuter, 0.02f + 0.12f * EnergyGlowIntensity, pulseScale);
            SetGlowGraphic(energyGlow, 0.08f + 0.25f * EnergyGlowIntensity, pulseScale);

            if (infoPanelBorder != null)
            {
                var borderBlend = 0.2f + 0.45f * EnergyGlowIntensity;
                infoPanelBorder.color = Color.Lerp(
                    infoPanelBorderBaseColor,
                    MissionBorderColor,
                    borderBlend);
            }

            if (infoPanelBackground != null)
            {
                var backgroundBlend = 0.08f + 0.22f * EnergyGlowIntensity;
                infoPanelBackground.color = Color.Lerp(
                    infoPanelBackgroundBaseColor,
                    MissionBackgroundTint,
                    backgroundBlend);
            }
        }

        private static void SetGlowGraphic(
            MainBattlePartyHudRoundedGraphic glow,
            float alpha,
            float scale)
        {
            if (glow == null)
            {
                return;
            }

            glow.color = new Color(
                MissionGlowColor.r,
                MissionGlowColor.g,
                MissionGlowColor.b,
                Mathf.Clamp01(alpha));
            glow.rectTransform.localScale = Vector3.one * scale;
            glow.gameObject.SetActive(true);
        }

        private void ResetEnergyGlowVisuals()
        {
            EnergyGlowIntensity = 0f;
            ResetGlowGraphic(energyGlow);
            ResetGlowGraphic(energyGlowOuter);

            if (hasPanelBaseColors)
            {
                if (infoPanelBorder != null)
                {
                    infoPanelBorder.color = infoPanelBorderBaseColor;
                }

                if (infoPanelBackground != null)
                {
                    infoPanelBackground.color = infoPanelBackgroundBaseColor;
                }
            }
        }

        private static void ResetGlowGraphic(MainBattlePartyHudRoundedGraphic glow)
        {
            if (glow == null)
            {
                return;
            }

            glow.rectTransform.localScale = Vector3.one;
            glow.gameObject.SetActive(false);
        }

        private void CapturePanelBaseColors()
        {
            if (hasPanelBaseColors || infoPanelBorder == null || infoPanelBackground == null)
            {
                return;
            }

            infoPanelBorderBaseColor = infoPanelBorder.color;
            infoPanelBackgroundBaseColor = infoPanelBackground.color;
            hasPanelBaseColors = true;
        }

        private void PlayDamageFeedback()
        {
            ResolveDamageFeedback();
            if (damageFeedback == null || !damageFeedback.IsConfigured)
            {
                return;
            }

            damageFeedback.PlayDamageFeedback();
            DamageFeedbackPlayCount++;
        }

        private void ResolveDamageFeedback()
        {
            if (damageFeedback != null)
            {
                return;
            }

            var behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (var index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is IMainBattlePartyHudDamageFeedback feedback)
                {
                    damageFeedback = feedback;
                    return;
                }
            }
        }

        private void ResetDamageTracking()
        {
            hasHealthSample = false;
            previousHealth = 0f;
            ResolveDamageFeedback();
            damageFeedback?.ResetDamageFeedback();
        }

        private static void SetFill(
            RectTransform fillRect,
            MainBattlePartyHudRoundedGraphic fill,
            float ratio,
            bool visible)
        {
            if (fillRect != null)
            {
                var anchorMax = fillRect.anchorMax;
                anchorMax.x = Mathf.Clamp01(ratio);
                fillRect.anchorMax = anchorMax;
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
            }

            if (fill != null)
            {
                fill.gameObject.SetActive(visible);
            }
        }

        private void SetInputBlocking(bool block)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.interactable = block;
            canvasGroup.blocksRaycasts = block;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            CanvasGroup group,
            MainBattlePartyHudPortraitGraphic portraitGraphic,
            MainBattlePartyHudRoundedGraphic hpFill,
            RectTransform hpFillTransform,
            MainBattlePartyHudRoundedGraphic staminaTrack,
            MainBattlePartyHudRoundedGraphic staminaFill,
            RectTransform staminaFillTransform,
            TMP_Text level,
            MainBattlePartyHudStarGraphic[] starGraphics,
            MainBattlePartyHudRoundedGraphic staminaGlow = null,
            RectTransform feedbackVisualRoot = null,
            MainBattlePartyHudRoundedGraphic staminaOuterGlow = null,
            MainBattlePartyHudRoundedGraphic panelBorder = null,
            MainBattlePartyHudRoundedGraphic panelBackground = null)
        {
            canvasGroup = group;
            portrait = portraitGraphic;
            healthFill = hpFill;
            healthFillRect = hpFillTransform;
            energyTrack = staminaTrack;
            energyFill = staminaFill;
            energyFillRect = staminaFillTransform;
            energyGlow = staminaGlow;
            energyGlowOuter = staminaOuterGlow;
            infoPanelBorder = panelBorder;
            infoPanelBackground = panelBackground;
            visualRoot = feedbackVisualRoot;
            levelText = level;
            stars = starGraphics;
            hasPanelBaseColors = false;
            CapturePanelBaseColors();
            SetInputBlocking(false);
            ClearForNewRun();
        }
#endif
    }
}
