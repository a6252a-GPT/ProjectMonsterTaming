using System;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    [Serializable]
    public struct MonsterActiveFocusPreset // 등급별 집중 강도
    {
        [SerializeField, Range(0.05f, 1f)] private float otherUnitTimeScale;
        [SerializeField, Range(0.05f, 1f)] private float attackOtherUnitTimeScale;
        [SerializeField, Range(0.05f, 0.8f)] private float otherUnitSlowInDuration;
        [SerializeField, Range(0.5f, 3f)] private float otherUnitSlowHoldDuration;
        [SerializeField, Range(0.05f, 0.8f)] private float otherUnitSlowOutDuration;
        [SerializeField, Range(0f, 1.5f)] private float attackCameraHoldAfterCommitDuration;
        [SerializeField, Range(0.08f, 0.6f)] private float focusLead;
        [SerializeField, Range(0.05f, 0.3f)] private float fadeIn;
        [SerializeField, Range(0.05f, 0.5f)] private float fadeOut;
        [SerializeField, Range(0.5f, 3f)] private float minimumVisibleDuration;
        [SerializeField, Range(0.2f, 1f)] private float cameraReleaseDuration;
        [SerializeField, Range(0f, 0.8f)] private float dimAlpha;
        [SerializeField, Range(0f, 3f)] private float cameraMaxOffset;
        [SerializeField, Range(-8f, 0f)] private float cameraFovDelta;
        [SerializeField] private Color accentColor;

        public MonsterActiveFocusPreset(
            float otherUnitTimeScale,
            float attackOtherUnitTimeScale,
            float focusLead,
            float fadeIn,
            float fadeOut,
            float minimumVisibleDuration,
            float cameraReleaseDuration,
            float dimAlpha,
            float cameraMaxOffset,
            float cameraFovDelta,
            Color accentColor)
        {
            this.otherUnitTimeScale = Mathf.Clamp(otherUnitTimeScale, 0.05f, 1f);
            this.attackOtherUnitTimeScale = Mathf.Clamp(attackOtherUnitTimeScale, 0.05f, 1f);
            otherUnitSlowInDuration = 0.25f;
            otherUnitSlowHoldDuration = 1.8f;
            otherUnitSlowOutDuration = 0.45f;
            attackCameraHoldAfterCommitDuration = 0.45f;
            this.focusLead = Mathf.Clamp(focusLead, 0.08f, 0.6f);
            this.fadeIn = Mathf.Clamp(fadeIn, 0.05f, 0.3f);
            this.fadeOut = Mathf.Clamp(fadeOut, 0.05f, 0.5f);
            this.minimumVisibleDuration = Mathf.Clamp(minimumVisibleDuration, 0.5f, 3f);
            this.cameraReleaseDuration = Mathf.Clamp(cameraReleaseDuration, 0.2f, 1f);
            this.dimAlpha = Mathf.Clamp(dimAlpha, 0f, 0.8f);
            this.cameraMaxOffset = Mathf.Clamp(cameraMaxOffset, 0f, 3f);
            this.cameraFovDelta = Mathf.Clamp(cameraFovDelta, -8f, 0f);
            this.accentColor = accentColor.a <= 0f
                ? new Color(0.3f, 0.78f, 1f, 1f)
                : accentColor;
        }

        public float OtherUnitTimeScale => otherUnitTimeScale <= 0f ? 0.3f : otherUnitTimeScale;
        public float AttackOtherUnitTimeScale =>
            attackOtherUnitTimeScale <= 0f ? 0.18f : attackOtherUnitTimeScale;
        public float OtherUnitSlowInDuration =>
            otherUnitSlowInDuration <= 0f ? 0.25f : otherUnitSlowInDuration;
        public float OtherUnitSlowHoldDuration =>
            otherUnitSlowHoldDuration <= 0f ? 1.8f : otherUnitSlowHoldDuration;
        public float OtherUnitSlowOutDuration =>
            otherUnitSlowOutDuration <= 0f ? 0.45f : otherUnitSlowOutDuration;
        public float OtherUnitSlowTotalDuration =>
            OtherUnitSlowInDuration + OtherUnitSlowHoldDuration + OtherUnitSlowOutDuration;
        public float AttackCameraHoldAfterCommitDuration =>
            attackCameraHoldAfterCommitDuration <= 0f ? 0.45f : attackCameraHoldAfterCommitDuration;
        public float FocusLead => focusLead <= 0f ? 0.26f : focusLead;
        public float FadeIn => fadeIn <= 0f ? 0.12f : fadeIn;
        public float FadeOut => fadeOut <= 0f ? 0.12f : fadeOut;
        public float MinimumVisibleDuration => minimumVisibleDuration <= 0f ? 2f : minimumVisibleDuration;
        public float CameraReleaseDuration => cameraReleaseDuration <= 0f ? 0.48f : cameraReleaseDuration;
        public float DimAlpha => dimAlpha <= 0f ? 0.38f : dimAlpha;
        public float CameraMaxOffset => cameraMaxOffset <= 0f ? 1.2f : cameraMaxOffset;
        public float CameraFovDelta => cameraFovDelta >= 0f ? -1.5f : cameraFovDelta;
        public Color AccentColor => accentColor.a <= 0f
            ? new Color(0.3f, 0.78f, 1f, 1f)
            : accentColor;
    }

    [CreateAssetMenu(
        menuName = "ProjectMT/Combat/Monster Active Focus Presentation Config",
        fileName = "MonsterActiveFocusPresentationConfig")]
    public sealed class MonsterActiveFocusPresentationConfig : ScriptableObject // 액티브 집중 연출 공용 계약
    {
        private const string ResourcesPath = "MonsterActiveFocusPresentationConfig";
        public static MonsterActiveFocusPreset LegendaryDefault => new MonsterActiveFocusPreset(
            0.30f, 0.18f, 0.26f, 0.14f, 0.30f, 2f, 0.48f, 0.38f, 1.2f, -1.5f,
            new Color32(0xE7, 0xD3, 0x4A, 0xFF));
        public static MonsterActiveFocusPreset MythicDefault => new MonsterActiveFocusPreset(
            0.22f, 0.12f, 0.34f, 0.16f, 0.34f, 2f, 0.58f, 0.52f, 1.6f, -2.5f,
            new Color32(0xD6, 0x37, 0x35, 0xFF));

        [SerializeField] private TMP_FontAsset ownerFont;
        [SerializeField] private TMP_FontAsset skillFont;
        [SerializeField] private MonsterActiveFocusPresenter presenterPrefab;
        [SerializeField] private Material dimMaterialTemplate;
        [SerializeField] private Material cutInEdgeFadeMaterialTemplate;
        [SerializeField] private GameObject focusFeedbackPrefab;
        [SerializeField] private Sprite legendaryCutInBackground;
        [SerializeField] private Sprite mythicCutInBackground;
        [SerializeField] private MonsterActiveFocusPreset legendaryPreset = LegendaryDefault;
        [SerializeField] private MonsterActiveFocusPreset mythicPreset = MythicDefault;
        [SerializeField] private SfxCue legendaryStartSfx;
        [SerializeField] private SfxCue mythicStartSfx;
        [SerializeField] private GameObject legendaryHaloPrefab;
        [SerializeField] private GameObject mythicHaloPrefab;
        private static MonsterActiveFocusPresentationConfig cached;

        public TMP_FontAsset OwnerFont => ownerFont;
        public TMP_FontAsset SkillFont => skillFont != null ? skillFont : ownerFont;
        public MonsterActiveFocusPresenter PresenterPrefab => presenterPrefab;
        public Material DimMaterialTemplate => dimMaterialTemplate;
        public Material CutInEdgeFadeMaterialTemplate => cutInEdgeFadeMaterialTemplate;
        public GameObject FocusFeedbackPrefab => focusFeedbackPrefab;
        public Sprite LegendaryCutInBackground => legendaryCutInBackground;
        public Sprite MythicCutInBackground => mythicCutInBackground;
        public static MonsterActiveFocusPresentationConfig Current => cached != null
            ? cached
            : cached = Resources.Load<MonsterActiveFocusPresentationConfig>(ResourcesPath);

        public MonsterActiveFocusPreset ResolvePreset(MonsterRarity rarity)
        {
            return rarity == MonsterRarity.Mythic ? mythicPreset : legendaryPreset;
        }

        public Sprite ResolveCutInBackground(MonsterRarity rarity)
        {
            return rarity == MonsterRarity.Mythic
                ? mythicCutInBackground
                : legendaryCutInBackground;
        }

        public SfxCue ResolveStartSfx(MonsterRarity rarity)
        {
            return rarity == MonsterRarity.Mythic ? mythicStartSfx : legendaryStartSfx;
        }

        public GameObject ResolveHaloPrefab(MonsterRarity rarity)
        {
            return rarity == MonsterRarity.Mythic ? mythicHaloPrefab : legendaryHaloPrefab;
        }

        public bool TryValidate(out string error)
        {
            if (OwnerFont == null || SkillFont == null)
            {
                error = "몬스터 액티브 집중 배너의 한글 Font Asset이 비어 있습니다.";
                return false;
            }
            if (PresenterPrefab == null)
            {
                error = "몬스터 액티브 집중 HUD Prefab이 비어 있습니다.";
                return false;
            }
            if (DimMaterialTemplate == null)
            {
                error = "몬스터 액티브 집중 암전 Material이 비어 있습니다.";
                return false;
            }
            if (CutInEdgeFadeMaterialTemplate == null)
            {
                error = "몬스터 액티브 집중 배경의 3면 Feather Material이 비어 있습니다.";
                return false;
            }
            if (legendaryCutInBackground == null || mythicCutInBackground == null)
            {
                error = "몬스터 액티브 집중 배경의 전설·신화 Sprite가 비어 있습니다.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { cached = null; }

#if UNITY_EDITOR
        public void EditorConfigure(
            TMP_FontAsset body,
            TMP_FontAsset title,
            MonsterActiveFocusPresenter prefab = null,
            Material dimMaterial = null)
        {
            ownerFont = body;
            skillFont = title != null ? title : body;
            if (prefab != null) presenterPrefab = prefab;
            if (dimMaterial != null) dimMaterialTemplate = dimMaterial;
            cached = this;
        }

        public void EditorConfigurePresets(
            MonsterActiveFocusPreset legendary,
            MonsterActiveFocusPreset mythic)
        {
            legendaryPreset = legendary;
            mythicPreset = mythic;
            cached = this;
        }

        public void EditorConfigureBackgrounds(Sprite legendary, Sprite mythic)
        {
            legendaryCutInBackground = legendary;
            mythicCutInBackground = mythic;
            cached = this;
        }

        public void EditorConfigureOptionalPresentation(
            Material edgeFadeMaterial,
            GameObject feedbackPrefab)
        {
            cutInEdgeFadeMaterialTemplate = edgeFadeMaterial;
            focusFeedbackPrefab = feedbackPrefab;
            cached = this;
        }
#endif
    }
}
