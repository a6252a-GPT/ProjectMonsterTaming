using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectMT.Tools.FeelPreview
{
    /// <summary>공식 FEEL 데모의 역할과 주요값을 연구실 조건에 맞춰 재현한다.</summary>
    public sealed partial class CombatFeelCatalogPreviewLab
    {
        private enum DemoOverlay
        {
            None, ShaderScan, SkyGradient, BloomHalo, ChannelSplit, Chromatic,
            ColorGrade, Focus, Grain, VolumeBlend, Lens, MotionStreaks,
            Panini, MovingFilter, Vignette, WhiteBalance, Clipping, Freeze, Timescale
        }

        private readonly struct SliderSpec
        {
            public SliderSpec(string label, float min, float max, float defaultValue, int decimals = 2)
            {
                Label = label;
                Min = min;
                Max = max;
                DefaultValue = defaultValue;
                Decimals = decimals;
            }

            public string Label { get; }
            public float Min { get; }
            public float Max { get; }
            public float DefaultValue { get; }
            public int Decimals { get; }
            public string Format(float value) => Decimals == 0 ? Mathf.RoundToInt(value).ToString() : value.ToString(Decimals == 1 ? "0.0" : "0.00");
        }

        private readonly Dictionary<string, float[]> demoSettings = new Dictionary<string, float[]>();
        private readonly Dictionary<Renderer, bool> demoRendererEnabled = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Renderer, MaterialPropertyBlock> demoRendererBlocks = new Dictionary<Renderer, MaterialPropertyBlock>();
        private bool demoStateCaptured;
        private int demoAnimatorStateHash;
        private float demoAnimatorNormalizedTime;
        private Material demoSkybox;
        private AmbientMode demoAmbientMode;
        private Color demoAmbientSky;
        private Color demoAmbientEquator;
        private Color demoAmbientGround;
        private float demoAmbientIntensity;
        private FogMode demoFogMode;
        private float demoFogStart;
        private float demoFogEnd;
        private DemoOverlay demoOverlay;
        private float demoOverlayStrength;
        private float demoOverlayPhase;
        private Color demoOverlayColor = Color.white;
        private bool tuningDirty;
        private Texture2D demoRadialTexture;
        private Texture2D demoNoiseTexture;
        private Texture2D demoGridTexture;

        private static SliderSpec S(string label, float min, float max, float value, int decimals = 2) => new SliderSpec(label, min, max, value, decimals);

        private static SliderSpec[] SpecsFor(string typeName) => typeName switch
        {
            "MMF_AnimationCrossfade" => new[] { S("전환 시간", 0.05f, 0.8f, 0.18f), S("포즈 각도", 8f, 65f, 34f, 0), S("전체 시간", 0.35f, 1.6f, 0.85f) },
            "MMF_Animation" => new[] { S("동작 높이", 0.1f, 1.1f, 0.48f), S("과장도", 0.5f, 1.8f, 1f), S("전체 시간", 0.35f, 1.4f, 0.72f) },
            "MMF_AnimatorPlayState" => new[] { S("시작 시점", 0f, 1f, 0.5f), S("재생 속도", 0.35f, 2.5f, 1.25f), S("재생 시간", 0.3f, 1.4f, 0.7f) },
            "MMF_AnimatorSpeed" => new[] { S("속도 배율", 0.1f, 3f, 0.3f), S("변화 시간", 0.25f, 1.8f, 0.9f) },
            "MMF_SpriteSheetAnimation" => new[] { S("초당 프레임", 4f, 30f, 12f, 0), S("표시 크기", 0.45f, 2.2f, 1.15f), S("재생 시간", 0.25f, 1.5f, 0.7f) },
            "MMF_Flicker" => new[] { S("깜빡임 간격", 0.025f, 0.18f, 0.055f), S("발광 강도", 0.5f, 8f, 4f, 1), S("전체 시간", 0.15f, 1f, 0.45f) },
            "MMF_Material" => new[] { S("교체 혼합", 0.05f, 0.6f, 0.2f), S("금속성", 0f, 1f, 0.85f), S("발광", 0f, 6f, 1.8f, 1) },
            "MMF_MaterialSetProperty" => new[] { S("프로퍼티 변화량", 0.1f, 1f, 0.75f), S("반복 횟수", 1f, 8f, 3f, 0), S("전체 시간", 0.2f, 1.4f, 0.65f) },
            "MMF_Blink" => new[] { S("점멸 간격", 0.03f, 0.25f, 0.075f), S("점멸 횟수", 1f, 8f, 3f, 0) },
            "MMF_ShaderController" => new[] { S("효과 세기", 0.2f, 2f, 1f), S("스캔 폭", 0.08f, 0.7f, 0.25f), S("전체 시간", 0.3f, 1.5f, 0.8f) },
            "MMF_Sprite" => new[] { S("스프라이트 크기", 0.4f, 2.2f, 1.15f), S("교체 유지시간", 0.15f, 1.2f, 0.55f) },
            "MMF_SpriteRenderer" => new[] { S("색 변화량", 0.1f, 1f, 0.8f), S("반전 횟수", 1f, 6f, 2f, 0), S("전체 시간", 0.2f, 1.2f, 0.6f) },
            "MMF_SpriteRendererAlpha" => new[] { S("최저 투명도", 0f, 0.9f, 0.05f), S("전체 시간", 0.2f, 1.4f, 0.7f) },
            "MMF_TextureOffset" => new[] { S("흐름 속도", 0.2f, 5f, 1.8f), S("타일 수", 1f, 8f, 3f, 0), S("전체 시간", 0.25f, 1.5f, 0.8f) },
            "MMF_TextureScale" => new[] { S("확대 배율", 0.25f, 5f, 2.5f), S("전체 시간", 0.25f, 1.5f, 0.8f) },

            "MMF_DestinationTransform" => new[] { S("목표 거리", 0.15f, 1.5f, 0.65f), S("회전량", 0f, 90f, 25f, 0), S("이동 시간", 0.2f, 1.3f, 0.65f) },
            "MMF_LookAt" => new[] { S("대상 각도", 10f, 100f, 50f, 0), S("전환 시간", 0.15f, 1.2f, 0.55f) },
            "MMF_Position" => new[] { S("밀림 거리", 0.05f, 0.8f, 0.3f), S("이동 시간", 0.12f, 1f, 0.45f) },
            "MMF_PositionShake" => new[] { S("흔들림 범위", 0.01f, 0.22f, 0.075f), S("흔들림 속도", 8f, 55f, 28f, 0), S("지속시간", 0.15f, 1f, 0.45f) },
            "MMF_PositionSpring" => new[] { S("반동 거리", 0.05f, 0.75f, 0.3f), S("진동 빈도", 2f, 12f, 6f, 1), S("감쇠", 0.1f, 1.2f, 0.45f) },
            "MMF_RotatePositionAround" => new[] { S("회전 반경", 0.15f, 1.2f, 0.55f), S("회전 각도", 30f, 360f, 180f, 0), S("지속시간", 0.2f, 1.5f, 0.75f) },
            "MMF_Rotation" => new[] { S("회전 각도", 5f, 100f, 32f, 0), S("지속시간", 0.12f, 1.1f, 0.45f) },
            "MMF_RotationShake" => new[] { S("회전 범위", 1f, 30f, 9f, 0), S("흔들림 속도", 8f, 60f, 32f, 0), S("지속시간", 0.15f, 1f, 0.45f) },
            "MMF_RotationSpring" => new[] { S("회전 충격", 5f, 80f, 30f, 0), S("진동 빈도", 2f, 12f, 6.5f, 1), S("감쇠", 0.1f, 1.2f, 0.4f) },
            "MMF_Scale" => new[] { S("크기 변화", 0.05f, 0.65f, 0.22f), S("지속시간", 0.12f, 1f, 0.45f) },
            "MMF_ScaleShake" => new[] { S("크기 떨림", 0.01f, 0.25f, 0.075f), S("떨림 속도", 8f, 60f, 30f, 0), S("지속시간", 0.15f, 1f, 0.45f) },
            "MMF_ScaleSpring" => new[] { S("크기 충격", 0.05f, 0.6f, 0.24f), S("진동 빈도", 2f, 12f, 6f, 1), S("감쇠", 0.1f, 1.2f, 0.42f) },
            "MMF_SetParent" => new[] { S("부모축 회전", 10f, 120f, 55f, 0), S("부모축 이동", 0.05f, 0.8f, 0.28f), S("지속시간", 0.2f, 1.4f, 0.7f) },
            "MMF_SquashAndStretch" => new[] { S("눌림 강도", 0.05f, 0.65f, 0.3f), S("지속시간", 0.12f, 1f, 0.45f) },
            "MMF_SquashAndStretchSpring" => new[] { S("탄성 강도", 0.05f, 0.7f, 0.3f), S("진동 빈도", 2f, 12f, 6f, 1), S("감쇠", 0.1f, 1.2f, 0.38f) },
            "MMF_Wiggle" => new[] { S("복합 강도", 0.05f, 0.7f, 0.3f), S("속도", 4f, 30f, 13f, 0), S("지속시간", 0.2f, 1.4f, 0.7f) },

            "MMF_Light" => new[] { S("광량", 1f, 15f, 9f, 1), S("범위", 0.5f, 6f, 3.2f, 1), S("지속시간", 0.08f, 0.8f, 0.24f) },
            "MMF_Light2D_URP" => new[] { S("광량", 0.2f, 2f, 1f), S("반경", 0.3f, 3.5f, 1.8f), S("지속시간", 0.1f, 0.9f, 0.35f) },
            "MMF_ParticlesInstantiation" => new[] { S("파티클 수", 8f, 70f, 34f, 0), S("분출 속도", 0.5f, 5f, 2.7f), S("크기", 0.03f, 0.3f, 0.12f) },
            "MMF_Particles" => new[] { S("방출량", 5f, 80f, 28f, 0), S("속도", 0.2f, 4f, 1.6f), S("지속시간", 0.2f, 1.5f, 0.75f) },
            "MMF_InstantiateObject" => new[] { S("생성 크기", 0.08f, 0.8f, 0.3f), S("회전 속도", 0f, 720f, 260f, 0), S("유지시간", 0.15f, 1.3f, 0.65f) },
            "MMF_LineRenderer" => new[] { S("선 길이", 0.3f, 3f, 1.6f), S("선 두께", 0.01f, 0.25f, 0.065f), S("지속시간", 0.1f, 1f, 0.4f) },
            "MMF_TrailRenderer" => new[] { S("잔상 폭", 0.02f, 0.5f, 0.18f), S("이동 속도", 0.5f, 6f, 3.5f), S("잔상 시간", 0.08f, 0.8f, 0.32f) },

            "MMF_Fog" => new[] { S("안개 밀도", 0.005f, 0.12f, 0.055f, 3), S("전환 시간", 0.2f, 1.8f, 0.8f) },
            "MMF_ShaderGlobal" => new[] { S("전역값", 0f, 2f, 1f), S("스캔 속도", 0.2f, 3f, 1.2f), S("지속시간", 0.2f, 1.5f, 0.75f) },
            "MMF_Skybox" => new[] { S("노출", 0.2f, 2.5f, 1.25f), S("색 변화", 0f, 1f, 0.75f), S("유지시간", 0.2f, 1.5f, 0.75f) },
            "MMF_CameraShake" => new[] { S("흔들림 세기", 0.01f, 0.3f, 0.085f), S("주파수", 8f, 55f, 28f, 0), S("지속시간", 0.08f, 0.8f, 0.28f) },
            "MMF_CameraZoom" => new[] { S("줌 변화량", 2f, 25f, 10f, 0), S("진입 시간", 0.03f, 0.5f, 0.12f), S("유지시간", 0.05f, 0.8f, 0.2f) },
            "MMF_CameraClippingPlanes" => new[] { S("근거리 컷", 0.01f, 3f, 0.8f), S("원거리 컷", 10f, 150f, 38f, 0), S("전환 시간", 0.2f, 1.3f, 0.65f) },
            "MMF_Fade" => new[] { S("최대 불투명도", 0.1f, 1f, 0.78f), S("페이드 시간", 0.15f, 1.5f, 0.65f) },
            "MMF_CameraFieldOfView" => new[] { S("시야각 변화", 2f, 30f, 12f, 0), S("펄스 시간", 0.15f, 1.2f, 0.55f) },
            "MMF_Flash" => new[] { S("플래시 밝기", 0.1f, 1f, 0.65f), S("플래시 시간", 0.04f, 0.6f, 0.18f) },
            "MMF_CameraOrthographicSize" => new[] { S("직교 크기", 2f, 14f, 7.2f), S("전환 시간", 0.2f, 1.3f, 0.65f) },

            "MMF_Bloom" or "MMF_Bloom_URP" => new[] { S("발광 강도", 0.2f, 12f, 6.5f, 1), S("임계값", 0f, 2f, 0.55f), S("지속시간", 0.15f, 1.5f, 0.7f) },
            "MMF_ChannelMixer_URP" => new[] { S("채널 혼합", 0f, 100f, 55f, 0), S("지속시간", 0.2f, 1.5f, 0.75f) },
            "MMF_ChromaticAberration" or "MMF_ChromaticAberration_URP" => new[] { S("색수차 강도", 0f, 1f, 0.72f), S("지속시간", 0.1f, 1.2f, 0.55f) },
            "MMF_ColorAdjustments_URP" or "MMF_ColorGrading" => new[] { S("대비", -100f, 100f, 44f, 0), S("채도", -100f, 100f, -28f, 0), S("노출", -2f, 2f, 0.65f) },
            "MMF_DepthOfField" or "MMF_DepthOfField_URP" => new[] { S("초점 거리", 0.1f, 20f, 4.8f, 1), S("흐림 반경", 0.1f, 1.5f, 0.85f), S("지속시간", 0.2f, 1.5f, 0.8f) },
            "MMF_FilmGrain_URP" => new[] { S("입자 강도", 0f, 1f, 0.72f), S("응답성", 0f, 1f, 0.8f), S("지속시간", 0.2f, 1.5f, 0.8f) },
            "MMF_GlobalPPVolumeAutoBlend" or "MMF_GlobalPPVolumeAutoBlend_URP" => new[] { S("최종 가중치", 0f, 1f, 1f), S("블렌드 시간", 0.2f, 1.8f, 0.9f) },
            "MMF_LensDistortion" or "MMF_LensDistortion_URP" => new[] { S("왜곡 강도", -1f, 1f, -0.5f), S("중심 배율", 0f, 1f, 0.55f), S("지속시간", 0.15f, 1.3f, 0.65f) },
            "MMF_MotionBlur_URP" => new[] { S("블러 강도", 0f, 1f, 0.75f), S("카메라 이동", 0.02f, 0.5f, 0.16f), S("지속시간", 0.15f, 1.2f, 0.6f) },
            "MMF_PaniniProjection_URP" => new[] { S("투영 거리", 0f, 1f, 0.78f), S("화면 맞춤", 0f, 1f, 0.8f), S("지속시간", 0.2f, 1.4f, 0.75f) },
            "MMF_PPMovingFilter" => new[] { S("필터 크기", 0.15f, 1.2f, 0.55f), S("이동 속도", 0.2f, 2.5f, 1f), S("지속시간", 0.3f, 1.8f, 1f) },
            "MMF_Vignette" or "MMF_Vignette_URP" => new[] { S("비네트 강도", 0f, 1f, 0.62f), S("부드러움", 0f, 1f, 0.35f), S("지속시간", 0.15f, 1.3f, 0.65f) },
            "MMF_WhiteBalance_URP" => new[] { S("색온도", -100f, 100f, 64f, 0), S("색조", -100f, 100f, -22f, 0), S("지속시간", 0.2f, 1.4f, 0.75f) },
            "MMF_FreezeFrame" => new[] { S("정지 시간", 0.03f, 0.5f, 0.12f) },
            "MMF_TimescaleModifier" => new[] { S("시간 배율", 0.05f, 1f, 0.22f), S("유지시간", 0.15f, 1.5f, 0.7f) },
            _ => new[] { S("효과 세기", 0f, 1f, 0.75f), S("지속시간", 0.15f, 1.2f, 0.6f) }
        };

        private float[] SettingsFor(string typeName)
        {
            if (demoSettings.TryGetValue(typeName, out var values)) return values;
            var specs = SpecsFor(typeName);
            values = new float[specs.Length];
            for (var index = 0; index < specs.Length; index++) values[index] = specs[index].DefaultValue;
            demoSettings[typeName] = values;
            return values;
        }

        private float V(string typeName, int index)
        {
            var values = SettingsFor(typeName);
            return index >= 0 && index < values.Length ? values[index] : 0f;
        }

        private void CaptureDemoState()
        {
            demoRendererEnabled.Clear();
            demoRendererBlocks.Clear();
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                demoRendererEnabled[renderer] = renderer.enabled;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                demoRendererBlocks[renderer] = block;
            }

            if (targetAnimator != null && targetAnimator.runtimeAnimatorController != null && targetAnimator.isActiveAndEnabled)
            {
                var state = targetAnimator.GetCurrentAnimatorStateInfo(0);
                demoAnimatorStateHash = state.fullPathHash;
                demoAnimatorNormalizedTime = state.normalizedTime;
            }

            demoSkybox = RenderSettings.skybox;
            demoAmbientMode = RenderSettings.ambientMode;
            demoAmbientSky = RenderSettings.ambientSkyColor;
            demoAmbientEquator = RenderSettings.ambientEquatorColor;
            demoAmbientGround = RenderSettings.ambientGroundColor;
            demoAmbientIntensity = RenderSettings.ambientIntensity;
            demoFogMode = RenderSettings.fogMode;
            demoFogStart = RenderSettings.fogStartDistance;
            demoFogEnd = RenderSettings.fogEndDistance;
            demoStateCaptured = true;
            tuningDirty = false;
        }

        private void RestoreDemoState()
        {
            demoOverlay = DemoOverlay.None;
            demoOverlayStrength = 0f;
            demoOverlayPhase = 0f;
            Shader.SetGlobalFloat("_ProjectMTFeelLabPulse", 0f);
            if (!demoStateCaptured) return;

            foreach (var pair in demoRendererEnabled) if (pair.Key != null) pair.Key.enabled = pair.Value;
            foreach (var pair in demoRendererBlocks) if (pair.Key != null) pair.Key.SetPropertyBlock(pair.Value);
            if (targetAnimator != null)
            {
                targetAnimator.speed = originalAnimatorSpeed;
                if (demoAnimatorStateHash != 0 && targetAnimator.runtimeAnimatorController != null && targetAnimator.isActiveAndEnabled)
                {
                    targetAnimator.Play(demoAnimatorStateHash, 0, Mathf.Repeat(demoAnimatorNormalizedTime, 1f));
                    targetAnimator.Update(0f);
                }
            }

            RenderSettings.skybox = demoSkybox;
            RenderSettings.ambientMode = demoAmbientMode;
            RenderSettings.ambientSkyColor = demoAmbientSky;
            RenderSettings.ambientEquatorColor = demoAmbientEquator;
            RenderSettings.ambientGroundColor = demoAmbientGround;
            RenderSettings.ambientIntensity = demoAmbientIntensity;
            RenderSettings.fogMode = demoFogMode;
            RenderSettings.fogStartDistance = demoFogStart;
            RenderSettings.fogEndDistance = demoFogEnd;
        }

        private IEnumerator PlayModelDemo(string typeName)
        {
            if (visual == null) yield break;
            switch (typeName)
            {
                case "MMF_AnimationCrossfade": yield return DemoCrossfade(typeName); yield break;
                case "MMF_Animation": yield return DemoAnimationParameter(typeName); yield break;
                case "MMF_AnimatorPlayState": yield return DemoAnimatorPlayState(typeName); yield break;
                case "MMF_AnimatorSpeed": yield return DemoAnimatorSpeed(typeName); yield break;
                case "MMF_SpriteSheetAnimation": yield return DemoSpriteSheet(typeName); yield break;
                case "MMF_Flicker": yield return DemoFlicker(typeName); yield break;
                case "MMF_Material": yield return DemoMaterialSwap(typeName); yield break;
                case "MMF_MaterialSetProperty": yield return DemoMaterialProperty(typeName); yield break;
                case "MMF_Blink": yield return DemoBlink(typeName); yield break;
                case "MMF_ShaderController": yield return DemoShaderController(typeName); yield break;
                case "MMF_Sprite": yield return DemoSpriteSwap(typeName); yield break;
                case "MMF_SpriteRenderer": yield return DemoSpriteRenderer(typeName); yield break;
                case "MMF_SpriteRendererAlpha": yield return DemoSpriteAlpha(typeName); yield break;
                case "MMF_TextureOffset": yield return DemoTexture(typeName, true); yield break;
                case "MMF_TextureScale": yield return DemoTexture(typeName, false); yield break;
                default: yield return DemoTransform(typeName); yield break;
            }
        }

        private IEnumerator DemoCrossfade(string typeName)
        {
            var transition = V(typeName, 0);
            var angle = V(typeName, 1);
            var duration = Mathf.Max(V(typeName, 2), transition * 2f);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var blendIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / transition));
                var blendOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - duration + transition) / transition));
                var blend = Mathf.Min(blendIn, blendOut);
                visual.localRotation = originalRotation * Quaternion.Euler(angle * 0.18f * blend, -angle * blend, angle * 0.35f * blend);
                visual.localPosition = originalPosition + new Vector3(0f, -0.12f * blend, 0.08f * blend);
                visual.localScale = Vector3.Scale(originalScale, new Vector3(1f + 0.08f * blend, 1f - 0.12f * blend, 1f + 0.08f * blend));
                yield return null;
            }
        }

        private IEnumerator DemoAnimationParameter(string typeName)
        {
            var height = V(typeName, 0);
            var exaggeration = V(typeName, 1);
            var duration = V(typeName, 2);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var anticipation = t < 0.18f ? -Mathf.Sin(t / 0.18f * Mathf.PI) * 0.08f : 0f;
                var jump = Mathf.Sin(Mathf.Clamp01((t - 0.12f) / 0.76f) * Mathf.PI) * height;
                var landing = Mathf.Exp(-Mathf.Pow((t - 0.92f) * 18f, 2f));
                visual.localPosition = originalPosition + Vector3.up * (jump + anticipation);
                visual.localRotation = originalRotation * Quaternion.Euler(-18f * Mathf.Sin(t * Mathf.PI) * exaggeration, 0f, 0f);
                visual.localScale = Vector3.Scale(originalScale, new Vector3(1f + landing * 0.16f, 1f - landing * 0.22f, 1f + landing * 0.16f));
                yield return null;
            }
        }

        private IEnumerator DemoAnimatorPlayState(string typeName)
        {
            var start = V(typeName, 0);
            var speed = V(typeName, 1);
            var duration = V(typeName, 2);
            if (targetAnimator != null && demoAnimatorStateHash != 0)
            {
                targetAnimator.Play(demoAnimatorStateHash, 0, start);
                targetAnimator.speed = speed;
            }
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var frame = Mathf.Repeat(start + elapsed / duration * speed, 1f);
                var stepped = Mathf.Floor(frame * 4f) / 4f;
                visual.localRotation = originalRotation * Quaternion.Euler(-8f + stepped * 32f, stepped > 0.5f ? 28f : -18f, (stepped - 0.5f) * 22f);
                visual.localPosition = originalPosition + Vector3.up * (Mathf.Sin(stepped * Mathf.PI) * 0.12f);
                yield return null;
            }
        }

        private IEnumerator DemoAnimatorSpeed(string typeName)
        {
            var speed = V(typeName, 0);
            var duration = V(typeName, 1);
            if (targetAnimator != null) targetAnimator.speed = speed;
            var marker = CreateTransient("[Runtime] Animator Speed Dial", HitPoint());
            var hand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            transientObjects.Add(hand);
            hand.name = "Speed Hand";
            hand.transform.SetParent(marker.transform, false);
            hand.transform.localPosition = Vector3.up * 0.28f;
            hand.transform.localScale = new Vector3(0.035f, 0.55f, 0.035f);
            ApplyColor(hand.GetComponent<Renderer>(), new Color(0.2f, 0.95f, 0.85f), 4f);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                marker.transform.rotation = Quaternion.LookRotation(previewCamera != null ? previewCamera.transform.forward : Vector3.forward) * Quaternion.Euler(0f, 0f, -elapsed * speed * 420f);
                yield return null;
            }
        }

        private IEnumerator DemoSpriteSheet(string typeName)
        {
            var frameRate = Mathf.RoundToInt(V(typeName, 0));
            var size = V(typeName, 1);
            var duration = V(typeName, 2);
            var root = CreateTransient("[Runtime] Sprite Sheet", HitPoint());
            FaceCamera(root.transform);
            var spriteRenderer = root.AddComponent<SpriteRenderer>();
            var frames = new Sprite[6];
            for (var index = 0; index < frames.Length; index++) frames[index] = ShapeSprite(3 + index, 0.25f + index * 0.08f);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var frame = Mathf.FloorToInt(elapsed * frameRate) % frames.Length;
                spriteRenderer.sprite = frames[frame];
                spriteRenderer.color = Color.Lerp(new Color(1f, 0.28f, 0.08f), new Color(1f, 0.95f, 0.45f), frame / 5f);
                root.transform.localScale = Vector3.one * size * (0.75f + frame * 0.07f);
                yield return null;
            }
        }

        private IEnumerator DemoFlicker(string typeName)
        {
            var period = V(typeName, 0);
            var emission = V(typeName, 1);
            var duration = V(typeName, 2);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var on = Mathf.FloorToInt(elapsed / period) % 2 == 0;
                SetModelSurface(on ? new Color(1f, 0.08f, 0.02f) : Color.white, on ? emission : 0f, 0.1f, 0.25f);
                yield return null;
            }
        }

        private IEnumerator DemoMaterialSwap(string typeName)
        {
            var transition = V(typeName, 0);
            var metallic = V(typeName, 1);
            var emission = V(typeName, 2);
            var duration = transition * 2f + 0.35f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var blend = Mathf.Min(Mathf.Clamp01(elapsed / transition), Mathf.Clamp01((duration - elapsed) / transition));
                SetModelSurface(Color.Lerp(Color.white, new Color(1f, 0.62f, 0.08f), blend), emission * blend, metallic * blend, Mathf.Lerp(0.2f, 0.92f, blend));
                yield return null;
            }
        }

        private IEnumerator DemoMaterialProperty(string typeName)
        {
            var amount = V(typeName, 0);
            var repeats = V(typeName, 1);
            var duration = V(typeName, 2);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = elapsed / duration;
                var property = (Mathf.Sin(t * Mathf.PI * 2f * repeats - Mathf.PI * 0.5f) * 0.5f + 0.5f) * amount;
                SetModelSurface(Color.Lerp(Color.white, new Color(0.18f, 0.75f, 1f), property), property * 5f, property, property);
                yield return null;
            }
        }

        private IEnumerator DemoBlink(string typeName)
        {
            var interval = V(typeName, 0);
            var count = Mathf.RoundToInt(V(typeName, 1));
            var duration = interval * count * 2f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var visible = Mathf.FloorToInt(elapsed / interval) % 2 != 0;
                foreach (var renderer in renderers) if (renderer != null) renderer.enabled = visible;
                yield return null;
            }
        }

        private IEnumerator DemoShaderController(string typeName)
        {
            var amplitude = V(typeName, 0);
            var width = V(typeName, 1);
            var duration = V(typeName, 2);
            var scan = CreateTransient("[Runtime] Shader Scan", HitPoint());
            FaceCamera(scan.transform);
            var sprite = scan.AddComponent<SpriteRenderer>();
            sprite.sprite = RadialSpriteDemo();
            sprite.color = new Color(0.08f, 1f, 0.75f, 0.75f);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = elapsed / duration;
                scan.transform.position = HitPoint() + Vector3.up * Mathf.Lerp(-0.9f, 0.9f, t);
                scan.transform.localScale = new Vector3(1.8f, width, 1f);
                SetModelSurface(Color.Lerp(Color.white, new Color(0.05f, 1f, 0.7f), Mathf.Sin(t * Mathf.PI)), amplitude * 4f * Mathf.Sin(t * Mathf.PI), 0.2f, 0.7f);
                yield return null;
            }
        }

        private IEnumerator DemoSpriteSwap(string typeName)
        {
            var size = V(typeName, 0);
            var duration = V(typeName, 1);
            var root = CreateTransient("[Runtime] Sprite Swap", HitPoint());
            FaceCamera(root.transform);
            var sprite = root.AddComponent<SpriteRenderer>();
            var before = ShapeSprite(4, 0.62f);
            var after = ShapeSprite(8, 0.24f);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var changed = elapsed > duration * 0.35f;
                sprite.sprite = changed ? after : before;
                sprite.color = changed ? new Color(1f, 0.3f, 0.08f) : new Color(0.25f, 0.78f, 1f);
                root.transform.localScale = Vector3.one * size;
                yield return null;
            }
        }

        private IEnumerator DemoSpriteRenderer(string typeName)
        {
            var amount = V(typeName, 0);
            var flips = V(typeName, 1);
            var duration = V(typeName, 2);
            var root = CreateTransient("[Runtime] Sprite Renderer", HitPoint());
            FaceCamera(root.transform);
            var sprite = root.AddComponent<SpriteRenderer>();
            sprite.sprite = ShapeSprite(5, 0.4f);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = elapsed / duration;
                sprite.flipX = Mathf.FloorToInt(t * flips * 2f) % 2 == 0;
                sprite.color = Color.Lerp(new Color(0.2f, 0.75f, 1f), new Color(1f, 0.15f, 0.65f), Mathf.PingPong(t * amount * 2f, 1f));
                root.transform.localScale = new Vector3(sprite.flipX ? 1.3f : -1.3f, 1.3f, 1f);
                yield return null;
            }
        }

        private IEnumerator DemoSpriteAlpha(string typeName)
        {
            var minimum = V(typeName, 0);
            var duration = V(typeName, 1);
            var root = CreateTransient("[Runtime] Sprite Alpha", HitPoint());
            FaceCamera(root.transform);
            var sprite = root.AddComponent<SpriteRenderer>();
            sprite.sprite = ShapeSprite(6, 0.32f);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var alpha = Mathf.Lerp(1f, minimum, Mathf.Sin(elapsed / duration * Mathf.PI));
                sprite.color = new Color(0.3f, 0.9f, 1f, alpha);
                root.transform.localScale = Vector3.one * 1.4f;
                yield return null;
            }
        }

        private IEnumerator DemoTexture(string typeName, bool offset)
        {
            var amount = V(typeName, 0);
            var duration = offset ? V(typeName, 2) : V(typeName, 1);
            var root = GameObject.CreatePrimitive(PrimitiveType.Quad);
            transientObjects.Add(root);
            root.name = offset ? "[Runtime] Texture Offset" : "[Runtime] Texture Scale";
            root.transform.position = HitPoint();
            root.transform.localScale = Vector3.one * 1.35f;
            FaceCamera(root.transform);
            var material = PreviewMaterial();
            material.mainTexture = GridTextureDemo();
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", GridTextureDemo());
            root.GetComponent<Renderer>().sharedMaterial = material;
            var tiling = offset ? Mathf.Max(1f, V(typeName, 1)) : 1f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = elapsed / duration;
                material.mainTextureScale = offset ? Vector2.one * tiling : Vector2.one * Mathf.Lerp(1f, amount, Mathf.Sin(t * Mathf.PI));
                material.mainTextureOffset = offset ? new Vector2(t * amount, -t * amount * 0.45f) : Vector2.one * (0.5f - 0.5f / Mathf.Max(0.01f, material.mainTextureScale.x));
                yield return null;
            }
        }

        private IEnumerator DemoTransform(string typeName)
        {
            var duration = typeName switch
            {
                "MMF_DestinationTransform" => V(typeName, 2), "MMF_LookAt" => V(typeName, 1), "MMF_Position" => V(typeName, 1),
                "MMF_PositionShake" => V(typeName, 2), "MMF_RotatePositionAround" => V(typeName, 2), "MMF_Rotation" => V(typeName, 1),
                "MMF_RotationShake" => V(typeName, 2), "MMF_Scale" => V(typeName, 1), "MMF_ScaleShake" => V(typeName, 2),
                "MMF_SetParent" => V(typeName, 2), "MMF_SquashAndStretch" => V(typeName, 1), "MMF_Wiggle" => V(typeName, 2), _ => 0.8f
            };
            if (typeName.Contains("Spring")) duration = 1.05f;
            var worldStart = visual.position;
            var rotationStart = visual.rotation;
            GameObject marker = null;
            Transform pivot = null;
            if (typeName is "MMF_DestinationTransform" or "MMF_LookAt")
            {
                marker = CreateTransient("[Runtime] Transform Destination", HitPoint() + visual.right * V(typeName, 0));
                var sprite = marker.AddComponent<SpriteRenderer>();
                sprite.sprite = ShapeSprite(4, 0.55f);
                sprite.color = new Color(0.15f, 1f, 0.7f, 0.9f);
                marker.transform.localScale = Vector3.one * 0.45f;
                FaceCamera(marker.transform);
            }
            if (typeName is "MMF_RotatePositionAround" or "MMF_SetParent")
            {
                var pivotObject = CreateTransient("[Runtime] Parent Pivot", visual.position + (typeName == "MMF_RotatePositionAround" ? -visual.right * V(typeName, 0) : Vector3.zero));
                pivot = pivotObject.transform;
                if (typeName == "MMF_SetParent") visual.SetParent(pivot, true);
            }

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var pulse = Mathf.Sin(t * Mathf.PI);
                if (typeName == "MMF_DestinationTransform")
                {
                    var destination = marker.transform.position;
                    visual.position = Vector3.Lerp(worldStart, destination, Mathf.SmoothStep(0f, 1f, pulse));
                    visual.rotation = rotationStart * Quaternion.Euler(0f, V(typeName, 1) * pulse, 0f);
                    visual.localScale = originalScale * (1f + 0.12f * pulse);
                }
                else if (typeName == "MMF_LookAt") visual.localRotation = originalRotation * Quaternion.Euler(0f, V(typeName, 0) * pulse, 0f);
                else if (typeName == "MMF_Position") visual.localPosition = originalPosition + new Vector3(-V(typeName, 0) * pulse, 0.04f * pulse, 0f);
                else if (typeName == "MMF_PositionShake")
                {
                    var amount = V(typeName, 0) * (1f - t);
                    var phase = elapsed * V(typeName, 1);
                    visual.localPosition = originalPosition + new Vector3(Mathf.Sin(phase) * amount, Mathf.Cos(phase * 1.37f) * amount * 0.45f, Mathf.Sin(phase * 0.73f) * amount * 0.25f);
                }
                else if (typeName == "MMF_PositionSpring") visual.localPosition = originalPosition + visual.InverseTransformDirection(-visual.right) * (V(typeName, 0) * Spring(t, V(typeName, 1), V(typeName, 2)));
                else if (typeName == "MMF_RotatePositionAround")
                {
                    var angle = V(typeName, 1) * pulse * Mathf.Deg2Rad;
                    var radius = V(typeName, 0);
                    visual.position = pivot.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    visual.rotation = Quaternion.LookRotation((pivot.position - visual.position).normalized, Vector3.up);
                }
                else if (typeName == "MMF_Rotation") visual.localRotation = originalRotation * Quaternion.Euler(0f, 0f, -V(typeName, 0) * pulse);
                else if (typeName == "MMF_RotationShake") visual.localRotation = originalRotation * Quaternion.Euler(0f, Mathf.Sin(elapsed * V(typeName, 1) * 0.77f) * V(typeName, 0) * (1f - t), Mathf.Sin(elapsed * V(typeName, 1)) * V(typeName, 0) * (1f - t));
                else if (typeName == "MMF_RotationSpring") visual.localRotation = originalRotation * Quaternion.Euler(0f, 0f, V(typeName, 0) * Spring(t, V(typeName, 1), V(typeName, 2)));
                else if (typeName == "MMF_Scale") visual.localScale = originalScale * (1f + V(typeName, 0) * pulse);
                else if (typeName == "MMF_ScaleShake") visual.localScale = originalScale * (1f + Mathf.Sin(elapsed * V(typeName, 1)) * V(typeName, 0) * (1f - t));
                else if (typeName == "MMF_ScaleSpring") visual.localScale = originalScale * (1f + V(typeName, 0) * Spring(t, V(typeName, 1), V(typeName, 2)));
                else if (typeName == "MMF_SetParent")
                {
                    pivot.position = worldStart + Vector3.up * (V(typeName, 1) * pulse);
                    pivot.rotation = Quaternion.Euler(0f, V(typeName, 0) * pulse, 0f);
                }
                else if (typeName == "MMF_SquashAndStretch")
                {
                    var amount = V(typeName, 0) * pulse;
                    visual.localScale = Vector3.Scale(originalScale, new Vector3(1f + amount * 0.65f, 1f - amount, 1f + amount * 0.65f));
                }
                else if (typeName == "MMF_SquashAndStretchSpring")
                {
                    var amount = V(typeName, 0) * Spring(t, V(typeName, 1), V(typeName, 2));
                    visual.localScale = Vector3.Scale(originalScale, new Vector3(1f - amount * 0.55f, 1f + amount, 1f - amount * 0.55f));
                }
                else if (typeName == "MMF_Wiggle")
                {
                    var amount = V(typeName, 0) * (1f - t);
                    var phase = elapsed * V(typeName, 1);
                    visual.localPosition = originalPosition + new Vector3(Mathf.Sin(phase) * amount * 0.18f, Mathf.Cos(phase * 0.7f) * amount * 0.12f, 0f);
                    visual.localRotation = originalRotation * Quaternion.Euler(Mathf.Cos(phase * 1.3f) * amount * 18f, Mathf.Sin(phase * 0.8f) * amount * 22f, Mathf.Sin(phase) * amount * 28f);
                    visual.localScale = originalScale * (1f + Mathf.Sin(phase * 1.7f) * amount * 0.18f);
                }
                yield return null;
            }
        }

        private IEnumerator PlayImpactDemo(string typeName)
        {
            if (visual == null) yield break;
            if (typeName == "MMF_Light") { yield return DemoLight(typeName, false); yield break; }
            if (typeName == "MMF_Light2D_URP") { yield return DemoLight(typeName, true); yield break; }
            if (typeName == "MMF_ParticlesInstantiation") { yield return DemoParticles(typeName, true); yield break; }
            if (typeName == "MMF_Particles") { yield return DemoParticles(typeName, false); yield break; }
            if (typeName == "MMF_InstantiateObject") { yield return DemoObject(typeName); yield break; }
            if (typeName == "MMF_LineRenderer") { yield return DemoLine(typeName); yield break; }
            yield return DemoTrail(typeName);
        }

        private IEnumerator DemoLight(string typeName, bool twoDimensional)
        {
            var strength = V(typeName, 0);
            var range = V(typeName, 1);
            var duration = V(typeName, 2);
            var root = CreateTransient(twoDimensional ? "[Runtime] 2D Hit Light" : "[Runtime] 3D Hit Light", HitPoint());
            SpriteRenderer halo = null;
            Light light = null;
            if (twoDimensional)
            {
                FaceCamera(root.transform);
                halo = root.AddComponent<SpriteRenderer>();
                halo.sprite = RadialSpriteDemo();
            }
            else
            {
                light = root.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.color = new Color(1f, 0.68f, 0.18f);
                light.range = range;
            }
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var pulse = Mathf.Sin(elapsed / duration * Mathf.PI);
                if (light != null) light.intensity = strength * pulse;
                if (halo != null)
                {
                    halo.color = new Color(0.25f, 0.82f, 1f, pulse * 0.85f);
                    root.transform.localScale = Vector3.one * range * pulse;
                }
                yield return null;
            }
        }

        private IEnumerator DemoParticles(string typeName, bool instantiate)
        {
            var count = Mathf.RoundToInt(V(typeName, 0));
            var speed = V(typeName, 1);
            var sizeOrDuration = V(typeName, 2);
            var root = CreateTransient(instantiate ? "[Runtime] Instantiated Burst" : "[Runtime] Existing Particle Emitter", HitPoint());
            var particle = root.AddComponent<ParticleSystem>();
            var main = particle.main;
            main.loop = false;
            main.startLifetime = instantiate ? new ParticleSystem.MinMaxCurve(0.25f, 0.5f) : new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = instantiate ? new ParticleSystem.MinMaxCurve(speed * 0.6f, speed) : new ParticleSystem.MinMaxCurve(speed * 0.25f, speed * 0.55f);
            main.startSize = instantiate ? new ParticleSystem.MinMaxCurve(sizeOrDuration * 0.5f, sizeOrDuration) : new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.startColor = instantiate ? new ParticleSystem.MinMaxGradient(new Color(1f, 0.25f, 0.05f), new Color(1f, 0.95f, 0.4f)) : new ParticleSystem.MinMaxGradient(new Color(0.15f, 0.75f, 1f), new Color(0.55f, 1f, 0.8f));
            var shape = particle.shape;
            shape.shapeType = instantiate ? ParticleSystemShapeType.Sphere : ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            var emission = particle.emission;
            emission.rateOverTime = 0f;
            if (instantiate)
            {
                particle.Emit(count);
                yield return Wait(0.8f);
            }
            else
            {
                var duration = sizeOrDuration;
                var emitted = 0f;
                for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    emitted += count * Time.unscaledDeltaTime;
                    var batch = Mathf.FloorToInt(emitted);
                    if (batch > 0) { particle.Emit(batch); emitted -= batch; }
                    root.transform.rotation = Quaternion.Euler(-25f, elapsed * 90f, 0f);
                    yield return null;
                }
            }
        }

        private IEnumerator DemoObject(string typeName)
        {
            var size = V(typeName, 0);
            var speed = V(typeName, 1);
            var duration = V(typeName, 2);
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            transientObjects.Add(root);
            root.name = "[Runtime] Instantiated Hit Object";
            root.transform.position = HitPoint();
            ApplyColor(root.GetComponent<Renderer>(), new Color(1f, 0.35f, 0.08f), 4f);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = elapsed / duration;
                root.transform.localScale = Vector3.one * size * Mathf.Sin(t * Mathf.PI);
                root.transform.Rotate(new Vector3(1f, 1.4f, 0.7f), speed * Time.unscaledDeltaTime, Space.World);
                yield return null;
            }
        }

        private IEnumerator DemoLine(string typeName)
        {
            var length = V(typeName, 0);
            var width = V(typeName, 1);
            var duration = V(typeName, 2);
            var root = CreateTransient("[Runtime] Slash Line", HitPoint());
            var line = root.AddComponent<LineRenderer>();
            line.material = PreviewMaterial();
            line.positionCount = 3;
            line.useWorldSpace = true;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = elapsed / duration;
                var reveal = Mathf.Sin(t * Mathf.PI);
                var start = HitPoint() - visual.right * length * 0.5f - Vector3.up * length * 0.18f;
                var end = HitPoint() + visual.right * length * 0.5f + Vector3.up * length * 0.18f;
                line.SetPosition(0, start);
                line.SetPosition(1, Vector3.Lerp(start, end, Mathf.Clamp01(t * 2.2f)) + Vector3.up * 0.12f);
                line.SetPosition(2, Vector3.Lerp(start, end, Mathf.Clamp01(t * 2.2f)));
                line.widthMultiplier = width * reveal;
                line.startColor = new Color(1f, 0.9f, 0.4f, reveal);
                line.endColor = new Color(1f, 0.15f, 0.03f, 0f);
                yield return null;
            }
        }

        private IEnumerator DemoTrail(string typeName)
        {
            var width = V(typeName, 0);
            var speed = V(typeName, 1);
            var trailTime = V(typeName, 2);
            var root = CreateTransient("[Runtime] Curved Hit Trail", HitPoint() - visual.right * 0.9f);
            var trail = root.AddComponent<TrailRenderer>();
            trail.material = PreviewMaterial();
            trail.time = trailTime;
            trail.startWidth = width;
            trail.endWidth = 0f;
            trail.startColor = new Color(0.3f, 0.9f, 1f, 1f);
            trail.endColor = new Color(0.1f, 0.35f, 1f, 0f);
            var duration = Mathf.Clamp(2f / speed, 0.25f, 1.1f);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = elapsed / duration;
                root.transform.position = HitPoint() + visual.right * Mathf.Lerp(-1f, 1f, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.65f;
                yield return null;
            }
            yield return Wait(trailTime);
        }

        private IEnumerator PlayScreenDemo(string typeName)
        {
            if (typeName == "MMF_Fog") { yield return DemoFog(typeName); yield break; }
            if (typeName == "MMF_ShaderGlobal") { yield return DemoShaderGlobal(typeName); yield break; }
            if (typeName == "MMF_Skybox") { yield return DemoSkybox(typeName); yield break; }
            if (typeName == "MMF_CameraShake") { yield return DemoCameraShake(typeName); yield break; }
            if (typeName == "MMF_CameraZoom") { yield return DemoCameraZoom(typeName); yield break; }
            if (typeName == "MMF_CameraClippingPlanes") { yield return DemoClipping(typeName); yield break; }
            if (typeName == "MMF_Fade") { yield return Overlay(Color.black, V(typeName, 0), V(typeName, 1)); yield break; }
            if (typeName == "MMF_CameraFieldOfView") { yield return DemoFov(typeName); yield break; }
            if (typeName == "MMF_Flash") { yield return Overlay(new Color(1f, 0.94f, 0.7f), V(typeName, 0), V(typeName, 1)); yield break; }
            if (typeName == "MMF_CameraOrthographicSize") { yield return DemoOrthographic(typeName); yield break; }
            if (typeName == "MMF_FreezeFrame") { yield return DemoFreeze(typeName); yield break; }
            if (typeName == "MMF_TimescaleModifier") { yield return DemoTimescale(typeName); yield break; }
            yield return DemoPostProcess(typeName);
        }

        private IEnumerator DemoFog(string typeName)
        {
            var density = V(typeName, 0);
            var duration = V(typeName, 1);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var pulse = Mathf.Sin(elapsed / duration * Mathf.PI);
                RenderSettings.fogDensity = Mathf.Lerp(fogDensity, density, pulse);
                RenderSettings.fogColor = Color.Lerp(fogColor, new Color(0.08f, 0.22f, 0.42f), pulse);
                yield return null;
            }
        }

        private IEnumerator DemoShaderGlobal(string typeName)
        {
            var value = V(typeName, 0);
            var speed = V(typeName, 1);
            var duration = V(typeName, 2);
            demoOverlay = DemoOverlay.ShaderScan;
            demoOverlayColor = new Color(0.1f, 1f, 0.78f);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                demoOverlayPhase = Mathf.Repeat(elapsed * speed, 1f);
                demoOverlayStrength = value * Mathf.Sin(elapsed / duration * Mathf.PI);
                Shader.SetGlobalFloat("_ProjectMTFeelLabPulse", demoOverlayStrength);
                yield return null;
            }
            Shader.SetGlobalFloat("_ProjectMTFeelLabPulse", 0f);
        }

        private IEnumerator DemoSkybox(string typeName)
        {
            var exposure = V(typeName, 0);
            var colorAmount = V(typeName, 1);
            var duration = V(typeName, 2);
            var shader = Shader.Find("Skybox/Procedural");
            if (shader != null)
            {
                var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                transientAssets.Add(material);
                if (material.HasProperty("_Exposure")) material.SetFloat("_Exposure", exposure);
                if (material.HasProperty("_SkyTint")) material.SetColor("_SkyTint", Color.Lerp(Color.gray, new Color(0.18f, 0.48f, 1f), colorAmount));
                if (material.HasProperty("_GroundColor")) material.SetColor("_GroundColor", Color.Lerp(Color.gray, new Color(0.65f, 0.08f, 0.45f), colorAmount));
                RenderSettings.skybox = material;
            }
            demoOverlay = DemoOverlay.SkyGradient;
            demoOverlayColor = new Color(0.2f, 0.55f, 1f);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var pulse = Mathf.Sin(elapsed / duration * Mathf.PI);
                demoOverlayStrength = pulse * colorAmount;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = Color.Lerp(demoAmbientSky, new Color(0.1f, 0.35f, 1f), pulse * colorAmount);
                RenderSettings.ambientGroundColor = Color.Lerp(demoAmbientGround, new Color(0.55f, 0.04f, 0.35f), pulse * colorAmount);
                RenderSettings.ambientIntensity = Mathf.Lerp(demoAmbientIntensity, exposure, pulse);
                yield return null;
            }
        }

        private IEnumerator DemoCameraShake(string typeName)
        {
            if (previewCamera == null) yield break;
            var strength = V(typeName, 0);
            var frequency = V(typeName, 1);
            var duration = V(typeName, 2);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var damping = 1f - elapsed / duration;
                var phase = elapsed * frequency;
                previewCamera.transform.position = cameraPosition + previewCamera.transform.right * Mathf.Sin(phase) * strength * damping + previewCamera.transform.up * Mathf.Cos(phase * 1.37f) * strength * 0.55f * damping;
                previewCamera.transform.rotation = cameraRotation * Quaternion.Euler(Mathf.Cos(phase * 0.8f) * strength * 10f * damping, Mathf.Sin(phase * 0.65f) * strength * 8f * damping, Mathf.Sin(phase) * strength * 12f * damping);
                yield return null;
            }
        }

        private IEnumerator DemoCameraZoom(string typeName)
        {
            if (previewCamera == null) yield break;
            var amount = V(typeName, 0);
            var transition = V(typeName, 1);
            var hold = V(typeName, 2);
            var duration = transition * 2f + hold;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var zoomIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / transition));
                var zoomOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - transition - hold) / transition));
                previewCamera.fieldOfView = cameraFov - amount * Mathf.Min(zoomIn, zoomOut);
                yield return null;
            }
        }

        private IEnumerator DemoClipping(string typeName)
        {
            if (previewCamera == null) yield break;
            var near = V(typeName, 0);
            var far = V(typeName, 1);
            var duration = V(typeName, 2);
            demoOverlay = DemoOverlay.Clipping;
            demoOverlayColor = new Color(1f, 0.32f, 0.08f);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var pulse = Mathf.Sin(elapsed / duration * Mathf.PI);
                previewCamera.nearClipPlane = Mathf.Lerp(cameraNear, near, pulse);
                previewCamera.farClipPlane = Mathf.Lerp(cameraFar, far, pulse);
                demoOverlayStrength = pulse;
                demoOverlayPhase = previewCamera.nearClipPlane / Mathf.Max(0.01f, near);
                yield return null;
            }
        }

        private IEnumerator DemoFov(string typeName)
        {
            if (previewCamera == null) yield break;
            var amount = V(typeName, 0);
            var duration = V(typeName, 1);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = elapsed / duration;
                previewCamera.fieldOfView = cameraFov + Mathf.Sin(t * Mathf.PI * 2f) * amount * (1f - t * 0.35f);
                yield return null;
            }
        }

        private IEnumerator DemoOrthographic(string typeName)
        {
            if (previewCamera == null) yield break;
            var size = V(typeName, 0);
            var duration = V(typeName, 1);
            previewCamera.orthographic = true;
            var start = cameraOrthographic ? cameraOrthographicSize : Mathf.Max(2f, cameraFov * 0.09f);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                previewCamera.orthographicSize = Mathf.Lerp(start, size, Mathf.Sin(elapsed / duration * Mathf.PI));
                yield return null;
            }
        }

        private IEnumerator DemoFreeze(string typeName)
        {
            demoOverlay = DemoOverlay.Freeze;
            demoOverlayStrength = 1f;
            Time.timeScale = 0f;
            yield return Wait(V(typeName, 0));
        }

        private IEnumerator DemoTimescale(string typeName)
        {
            var scale = V(typeName, 0);
            var duration = V(typeName, 1);
            demoOverlay = DemoOverlay.Timescale;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var blend = Mathf.Clamp01(elapsed / Mathf.Min(0.18f, duration * 0.3f));
                Time.timeScale = Mathf.Lerp(1f, scale, blend);
                demoOverlayStrength = 1f - scale;
                demoOverlayPhase = Time.timeScale;
                yield return null;
            }
        }

        private IEnumerator DemoPostProcess(string typeName)
        {
            var specs = SpecsFor(typeName);
            var values = SettingsFor(typeName);
            var duration = values[values.Length - 1];
            if (typeName is "MMF_ColorAdjustments_URP" or "MMF_ColorGrading") duration = 0.85f;
            var components = new List<VolumeComponent>();
            DemoOverlay pattern;

            if (typeName is "MMF_Bloom" or "MMF_Bloom_URP") { components.Add(AddVolumeComponent("UnityEngine.Rendering.Universal.Bloom")); pattern = DemoOverlay.BloomHalo; }
            else if (typeName == "MMF_ChannelMixer_URP") { components.Add(AddVolumeComponent("UnityEngine.Rendering.Universal.ChannelMixer")); pattern = DemoOverlay.ChannelSplit; }
            else if (typeName is "MMF_ChromaticAberration" or "MMF_ChromaticAberration_URP") { components.Add(AddVolumeComponent("UnityEngine.Rendering.Universal.ChromaticAberration")); pattern = DemoOverlay.Chromatic; }
            else if (typeName is "MMF_ColorAdjustments_URP" or "MMF_ColorGrading") { components.Add(AddVolumeComponent("UnityEngine.Rendering.Universal.ColorAdjustments")); pattern = DemoOverlay.ColorGrade; }
            else if (typeName is "MMF_DepthOfField" or "MMF_DepthOfField_URP") { components.Add(AddVolumeComponent("UnityEngine.Rendering.Universal.DepthOfField")); pattern = DemoOverlay.Focus; }
            else if (typeName == "MMF_FilmGrain_URP") { components.Add(AddVolumeComponent("UnityEngine.Rendering.Universal.FilmGrain")); pattern = DemoOverlay.Grain; }
            else if (typeName is "MMF_GlobalPPVolumeAutoBlend" or "MMF_GlobalPPVolumeAutoBlend_URP")
            {
                components.Add(AddVolumeComponent("UnityEngine.Rendering.Universal.Bloom"));
                components.Add(AddVolumeComponent("UnityEngine.Rendering.Universal.Vignette"));
                components.Add(AddVolumeComponent("UnityEngine.Rendering.Universal.ColorAdjustments"));
                pattern = DemoOverlay.VolumeBlend;
            }
            else if (typeName is "MMF_LensDistortion" or "MMF_LensDistortion_URP") { components.Add(AddVolumeComponent("UnityEngine.Rendering.Universal.LensDistortion")); pattern = DemoOverlay.Lens; }
            else if (typeName == "MMF_MotionBlur_URP") { components.Add(AddVolumeComponent("UnityEngine.Rendering.Universal.MotionBlur")); pattern = DemoOverlay.MotionStreaks; }
            else if (typeName == "MMF_PaniniProjection_URP") { components.Add(AddVolumeComponent("UnityEngine.Rendering.Universal.PaniniProjection")); pattern = DemoOverlay.Panini; }
            else if (typeName == "MMF_PPMovingFilter") { components.Add(AddVolumeComponent("UnityEngine.Rendering.Universal.Vignette")); pattern = DemoOverlay.MovingFilter; }
            else if (typeName is "MMF_Vignette" or "MMF_Vignette_URP") { components.Add(AddVolumeComponent("UnityEngine.Rendering.Universal.Vignette")); pattern = DemoOverlay.Vignette; }
            else { components.Add(AddVolumeComponent("UnityEngine.Rendering.Universal.WhiteBalance")); pattern = DemoOverlay.WhiteBalance; }

            demoOverlay = pattern;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = elapsed / duration;
                var pulse = Mathf.Sin(t * Mathf.PI);
                demoOverlayStrength = pulse;
                demoOverlayPhase = t;
                ApplyPostProcess(typeName, components, values, pulse, t);
                yield return null;
            }
        }

        private VolumeComponent AddVolumeComponent(string fullName)
        {
            if (volumeRoot == null)
            {
                volumeRoot = CreateTransient("[Runtime] Demo Post Process Volume", Vector3.zero);
                var volume = volumeRoot.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 9999f;
                volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                volume.sharedProfile = volumeProfile;
            }
            var type = FindType(fullName);
            if (type == null || volumeProfile == null) return null;
            var add = typeof(VolumeProfile).GetMethod("Add", new[] { typeof(Type), typeof(bool) });
            return add?.Invoke(volumeProfile, new object[] { type, true }) as VolumeComponent;
        }

        private void ApplyPostProcess(string typeName, IList<VolumeComponent> components, float[] values, float pulse, float t)
        {
            if (components.Count == 0 || components[0] == null) return;
            var component = components[0];
            if (typeName is "MMF_Bloom" or "MMF_Bloom_URP") { SetVolume(component, "intensity", values[0] * pulse); SetVolume(component, "threshold", values[1]); }
            else if (typeName == "MMF_ChannelMixer_URP")
            {
                SetVolume(component, "redOutGreenIn", values[0] * pulse);
                SetVolume(component, "greenOutBlueIn", -values[0] * 0.65f * pulse);
                SetVolume(component, "blueOutRedIn", values[0] * 0.8f * pulse);
            }
            else if (typeName is "MMF_ChromaticAberration" or "MMF_ChromaticAberration_URP") SetVolume(component, "intensity", values[0] * pulse);
            else if (typeName is "MMF_ColorAdjustments_URP" or "MMF_ColorGrading")
            {
                SetVolume(component, "contrast", values[0] * pulse);
                SetVolume(component, "saturation", values[1] * pulse);
                SetVolume(component, "postExposure", values[2] * pulse);
                SetVolume(component, "hueShift", 28f * pulse);
            }
            else if (typeName is "MMF_DepthOfField" or "MMF_DepthOfField_URP")
            {
                SetVolume(component, "mode", 1);
                SetVolume(component, "gaussianStart", Mathf.Max(0.01f, values[0] * 0.35f));
                SetVolume(component, "gaussianEnd", values[0]);
                SetVolume(component, "gaussianMaxRadius", values[1] * pulse);
                SetVolume(component, "highQualitySampling", true);
            }
            else if (typeName == "MMF_FilmGrain_URP") { SetVolume(component, "intensity", values[0] * pulse); SetVolume(component, "response", values[1]); }
            else if (typeName is "MMF_GlobalPPVolumeAutoBlend" or "MMF_GlobalPPVolumeAutoBlend_URP")
            {
                var weight = values[0] * pulse;
                SetVolume(components[0], "intensity", 5f * weight);
                SetVolume(components[1], "intensity", 0.48f * weight);
                SetVolume(components[1], "smoothness", 0.38f);
                SetVolume(components[2], "contrast", 30f * weight);
                SetVolume(components[2], "saturation", -25f * weight);
            }
            else if (typeName is "MMF_LensDistortion" or "MMF_LensDistortion_URP") { SetVolume(component, "intensity", values[0] * pulse); SetVolume(component, "scale", Mathf.Lerp(1f, values[1], pulse)); }
            else if (typeName == "MMF_MotionBlur_URP")
            {
                SetVolume(component, "intensity", values[0] * pulse);
                SetVolume(component, "clamp", 0.18f);
                if (previewCamera != null) previewCamera.transform.position = cameraPosition + previewCamera.transform.right * (Mathf.Sin(t * Mathf.PI * 4f) * values[1] * pulse);
            }
            else if (typeName == "MMF_PaniniProjection_URP") { SetVolume(component, "distance", values[0] * pulse); SetVolume(component, "cropToFit", values[1]); }
            else if (typeName == "MMF_PPMovingFilter")
            {
                SetVolume(component, "intensity", 0.62f * pulse);
                SetVolume(component, "smoothness", 0.45f);
                SetVolume(component, "center", new Vector2(Mathf.Lerp(-0.2f, 1.2f, Mathf.Repeat(t * values[1], 1f)), 0.5f));
            }
            else if (typeName is "MMF_Vignette" or "MMF_Vignette_URP") { SetVolume(component, "intensity", values[0] * pulse); SetVolume(component, "smoothness", values[1]); SetVolume(component, "color", new Color(0.25f, 0f, 0.05f)); }
            else { SetVolume(component, "temperature", values[0] * pulse); SetVolume(component, "tint", values[1] * pulse); }
        }

        private static void SetVolume(VolumeComponent component, string fieldName, object value)
        {
            if (component == null) return;
            var field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            var parameter = field?.GetValue(component);
            if (parameter == null) return;
            var parameterType = parameter.GetType();
            parameterType.GetProperty("overrideState")?.SetValue(parameter, true);
            parameterType.GetField("overrideState")?.SetValue(parameter, true);
            var property = parameterType.GetProperty("value");
            if (property == null || !property.CanWrite) return;
            var targetType = property.PropertyType;
            try
            {
                if (targetType.IsEnum) property.SetValue(parameter, Enum.ToObject(targetType, Convert.ToInt32(value)));
                else if (targetType == typeof(float)) property.SetValue(parameter, Convert.ToSingle(value));
                else if (targetType == typeof(int)) property.SetValue(parameter, Convert.ToInt32(value));
                else if (targetType == typeof(bool)) property.SetValue(parameter, Convert.ToBoolean(value));
                else if (targetType == typeof(Color) && value is Color color) property.SetValue(parameter, color);
                else if (targetType == typeof(Vector2) && value is Vector2 vector) property.SetValue(parameter, vector);
            }
            catch (ArgumentException) { }
        }

        private void DrawSelectedControls()
        {
            if (!selectedValid) return;
            var specs = SpecsFor(selected.TypeName);
            var values = SettingsFor(selected.TypeName);
            GUILayout.Space(5f);
            GUILayout.Label("주요 조절값", sectionStyle);
            for (var index = 0; index < specs.Length; index++)
            {
                var spec = specs[index];
                GUILayout.BeginHorizontal();
                GUILayout.Label(spec.Label, textStyle, GUILayout.Width(96f));
                var next = GUILayout.HorizontalSlider(values[index], spec.Min, spec.Max, GUILayout.MinWidth(120f));
                GUILayout.Label(spec.Format(next), textStyle, GUILayout.Width(42f));
                GUILayout.EndHorizontal();
                if (!Mathf.Approximately(next, values[index]))
                {
                    values[index] = next;
                    tuningDirty = true;
                }
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(tuningDirty ? "값 변경됨 · 아래 재생 버튼으로 적용" : "공식 데모 역할 기준 추천값", textStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("기본값", tabStyle, GUILayout.Width(62f), GUILayout.Height(24f)))
            {
                for (var index = 0; index < specs.Length; index++) values[index] = specs[index].DefaultValue;
                tuningDirty = true;
            }
            GUILayout.EndHorizontal();
        }

        private string DemoStatusText()
        {
            if (playing != null) return "● 재생 중 · 종료 후 자동 복구";
            return tuningDirty ? "● 조절값 변경됨 · 재생 버튼으로 적용" : "○ 대기 · 이전 효과 복구 완료";
        }

        private void DrawDemoOverlay()
        {
            if (demoOverlay == DemoOverlay.None || demoOverlayStrength <= 0.001f) return;
            var old = GUI.color;
            var strength = Mathf.Clamp01(demoOverlayStrength);
            switch (demoOverlay)
            {
                case DemoOverlay.ShaderScan:
                    GUI.color = new Color(demoOverlayColor.r, demoOverlayColor.g, demoOverlayColor.b, 0.12f * strength);
                    GUI.DrawTexture(new Rect(demoOverlayPhase * Screen.width - 60f, 0f, 120f, Screen.height), Texture2D.whiteTexture);
                    for (var y = 0f; y < Screen.height; y += 18f) GUI.DrawTexture(new Rect(0f, y + demoOverlayPhase * 18f, Screen.width, 1f), Texture2D.whiteTexture);
                    break;
                case DemoOverlay.SkyGradient:
                    for (var index = 0; index < 8; index++)
                    {
                        GUI.color = new Color(index < 4 ? 0.1f : 0.7f, 0.25f, index < 4 ? 1f : 0.45f, strength * 0.035f * (8 - index));
                        GUI.DrawTexture(new Rect(0f, index * Screen.height / 8f, Screen.width, Screen.height / 8f + 1f), Texture2D.whiteTexture);
                    }
                    break;
                case DemoOverlay.BloomHalo:
                    GUI.color = new Color(1f, 0.8f, 0.3f, 0.35f * strength);
                    var hit = HitPointOnScreen();
                    GUI.DrawTexture(new Rect(hit.x - 150f * strength, hit.y - 150f * strength, 300f * strength, 300f * strength), RadialTextureDemo());
                    break;
                case DemoOverlay.ChannelSplit:
                    DrawFrame(new Color(1f, 0f, 0f, 0.28f * strength), 6f, -8f);
                    DrawFrame(new Color(0f, 1f, 0.2f, 0.22f * strength), 6f, 0f);
                    DrawFrame(new Color(0f, 0.4f, 1f, 0.28f * strength), 6f, 8f);
                    break;
                case DemoOverlay.Chromatic:
                    DrawFrame(new Color(1f, 0f, 0f, 0.3f * strength), 3f, -12f * strength);
                    DrawFrame(new Color(0f, 1f, 0.3f, 0.2f * strength), 3f, 0f);
                    DrawFrame(new Color(0f, 0.35f, 1f, 0.3f * strength), 3f, 12f * strength);
                    break;
                case DemoOverlay.ColorGrade:
                    GUI.color = new Color(1f, 0.12f, 0.42f, 0.055f * strength);
                    GUI.DrawTexture(new Rect(0f, 0f, Screen.width * 0.5f, Screen.height), Texture2D.whiteTexture);
                    GUI.color = new Color(0.05f, 0.65f, 1f, 0.055f * strength);
                    GUI.DrawTexture(new Rect(Screen.width * 0.5f, 0f, Screen.width * 0.5f, Screen.height), Texture2D.whiteTexture);
                    break;
                case DemoOverlay.Focus:
                    GUI.color = new Color(0f, 0f, 0f, 0.22f * strength);
                    GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height * 0.22f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(0f, Screen.height * 0.78f, Screen.width, Screen.height * 0.22f), Texture2D.whiteTexture);
                    DrawFrame(new Color(0.3f, 1f, 0.8f, 0.6f * strength), 2f, Screen.width * 0.32f);
                    break;
                case DemoOverlay.Grain:
                    GUI.color = new Color(1f, 1f, 1f, 0.22f * strength);
                    GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), NoiseTextureDemo(), ScaleMode.StretchToFill);
                    break;
                case DemoOverlay.VolumeBlend:
                    GUI.color = new Color(0.1f, 0.55f, 1f, 0.08f * strength);
                    GUI.DrawTexture(new Rect(0f, 0f, Screen.width * demoOverlayPhase, Screen.height), Texture2D.whiteTexture);
                    GUI.color = new Color(1f, 0.2f, 0.45f, 0.08f * strength);
                    GUI.DrawTexture(new Rect(Screen.width * demoOverlayPhase, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                    GUI.color = new Color(1f, 1f, 1f, 0.8f * strength);
                    GUI.DrawTexture(new Rect(Screen.width * demoOverlayPhase - 1f, 0f, 2f, Screen.height), Texture2D.whiteTexture);
                    break;
                case DemoOverlay.Lens:
                    GUI.color = new Color(0.3f, 0.85f, 1f, 0.22f * strength);
                    var lensSize = Mathf.Lerp(180f, Screen.height * 1.3f, strength);
                    GUI.DrawTexture(new Rect(Screen.width * 0.5f - lensSize * 0.5f, Screen.height * 0.5f - lensSize * 0.5f, lensSize, lensSize), RadialTextureDemo());
                    break;
                case DemoOverlay.MotionStreaks:
                    GUI.color = new Color(0.55f, 0.9f, 1f, 0.22f * strength);
                    for (var index = 0; index < 18; index++)
                    {
                        var y = (index + 0.5f) / 18f * Screen.height;
                        var x = Mathf.Repeat(index * 97f + demoOverlayPhase * Screen.width * 2f, Screen.width);
                        GUI.DrawTexture(new Rect(x, y, 120f * strength, 2f), Texture2D.whiteTexture);
                    }
                    break;
                case DemoOverlay.Panini:
                    DrawFrame(new Color(0.15f, 0.85f, 1f, 0.42f * strength), Mathf.Lerp(2f, 14f, strength), Mathf.Lerp(0f, 70f, strength));
                    break;
                case DemoOverlay.MovingFilter:
                    GUI.color = new Color(0.2f, 0.9f, 1f, 0.26f * strength);
                    var filterSize = Screen.height * 0.55f;
                    var filterX = Mathf.Lerp(-filterSize, Screen.width + filterSize, demoOverlayPhase);
                    GUI.DrawTexture(new Rect(filterX - filterSize * 0.5f, Screen.height * 0.5f - filterSize * 0.5f, filterSize, filterSize), RadialTextureDemo());
                    break;
                case DemoOverlay.Vignette:
                    GUI.color = new Color(0.18f, 0f, 0.04f, 0.28f * strength);
                    GUI.DrawTexture(new Rect(0f, 0f, Screen.width, 42f + 110f * strength), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(0f, Screen.height - 42f - 110f * strength, Screen.width, 42f + 110f * strength), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(0f, 0f, 42f + 110f * strength, Screen.height), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(Screen.width - 42f - 110f * strength, 0f, 42f + 110f * strength, Screen.height), Texture2D.whiteTexture);
                    break;
                case DemoOverlay.WhiteBalance:
                    GUI.color = new Color(1f, 0.45f, 0.08f, 0.07f * strength);
                    GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                    break;
                case DemoOverlay.Clipping:
                    DrawFrame(new Color(1f, 0.22f, 0.04f, 0.65f * strength), 5f, 20f + 20f * strength);
                    break;
                case DemoOverlay.Freeze:
                    DrawCenterLabel("FREEZE FRAME", new Color(0.8f, 0.95f, 1f, 0.95f));
                    break;
                case DemoOverlay.Timescale:
                    DrawCenterLabel($"TIME  ×{demoOverlayPhase:0.00}", new Color(0.3f, 0.95f, 1f, 0.9f));
                    break;
            }
            GUI.color = old;
        }

        private void DrawFrame(Color color, float thickness, float inset)
        {
            var old = GUI.color;
            GUI.color = color;
            var width = Mathf.Max(1f, Screen.width - inset * 2f);
            var height = Mathf.Max(1f, Screen.height - inset * 2f);
            GUI.DrawTexture(new Rect(inset, inset, width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(inset, inset + height - thickness, width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(inset, inset, thickness, height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(inset + width - thickness, inset, thickness, height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void DrawCenterLabel(string label, Color color)
        {
            var style = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 28, normal = { textColor = color } };
            GUI.Label(new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.16f, 360f, 48f), label, style);
        }

        private void SetModelSurface(Color color, float emission, float metallic, float smoothness)
        {
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMaterial == null) continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                if (renderer.sharedMaterial.HasProperty("_BaseColor")) block.SetColor("_BaseColor", color);
                if (renderer.sharedMaterial.HasProperty("_Color")) block.SetColor("_Color", color);
                if (renderer.sharedMaterial.HasProperty("_EmissionColor")) block.SetColor("_EmissionColor", color * emission);
                if (renderer.sharedMaterial.HasProperty("_Metallic")) block.SetFloat("_Metallic", metallic);
                if (renderer.sharedMaterial.HasProperty("_Smoothness")) block.SetFloat("_Smoothness", smoothness);
                if (renderer.sharedMaterial.HasProperty("_Cutoff")) block.SetFloat("_Cutoff", Mathf.Lerp(0f, 0.55f, metallic));
                renderer.SetPropertyBlock(block);
            }
        }

        private static float Spring(float normalizedTime, float frequency, float damping)
        {
            return Mathf.Exp(-damping * normalizedTime * 6f) * Mathf.Sin(normalizedTime * frequency * Mathf.PI * 2f);
        }

        private void FaceCamera(Transform item)
        {
            if (item == null || previewCamera == null) return;
            item.rotation = Quaternion.LookRotation(previewCamera.transform.position - item.position, previewCamera.transform.up);
        }

        private Vector2 HitPointOnScreen()
        {
            if (previewCamera == null) return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var point = previewCamera.WorldToScreenPoint(HitPoint());
            return new Vector2(point.x, Screen.height - point.y);
        }

        private Sprite ShapeSprite(int points, float innerRadius)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                var angle = Mathf.Atan2(p.y, p.x);
                var wave = Mathf.Cos(angle * points) * 0.5f + 0.5f;
                var radius = Mathf.Lerp(innerRadius, 0.88f, wave);
                var alpha = Mathf.Clamp01((radius - p.magnitude) * 14f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, size);
            transientAssets.Add(texture);
            transientAssets.Add(sprite);
            return sprite;
        }

        private Sprite RadialSpriteDemo()
        {
            var texture = RadialTextureDemo();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), Vector2.one * 0.5f, texture.width);
            transientAssets.Add(sprite);
            return sprite;
        }

        private Texture2D RadialTextureDemo()
        {
            if (demoRadialTexture != null) return demoRadialTexture;
            const int size = 64;
            demoRadialTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var p = new Vector2(x / (float)(size - 1), y / (float)(size - 1)) * 2f - Vector2.one;
                var alpha = Mathf.Pow(Mathf.Clamp01(1f - p.magnitude), 1.8f);
                demoRadialTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            demoRadialTexture.Apply();
            return demoRadialTexture;
        }

        private Texture2D NoiseTextureDemo()
        {
            if (demoNoiseTexture != null) return demoNoiseTexture;
            const int size = 96;
            demoNoiseTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var random = new System.Random(1701);
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var value = (float)random.NextDouble();
                demoNoiseTexture.SetPixel(x, y, new Color(value, value, value, value));
            }
            demoNoiseTexture.Apply();
            return demoNoiseTexture;
        }

        private Texture2D GridTextureDemo()
        {
            if (demoGridTexture != null) return demoGridTexture;
            const int size = 64;
            demoGridTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var checker = ((x / 8) + (y / 8)) % 2 == 0;
                var line = x % 16 < 2 || y % 16 < 2;
                demoGridTexture.SetPixel(x, y, line ? new Color(0.1f, 1f, 0.8f) : checker ? new Color(0.04f, 0.16f, 0.22f) : new Color(0.6f, 0.08f, 0.35f));
            }
            demoGridTexture.Apply();
            return demoGridTexture;
        }

        private void DestroyDemoAssets()
        {
            if (demoRadialTexture != null) Destroy(demoRadialTexture);
            if (demoNoiseTexture != null) Destroy(demoNoiseTexture);
            if (demoGridTexture != null) Destroy(demoGridTexture);
            demoRadialTexture = null;
            demoNoiseTexture = null;
            demoGridTexture = null;
        }
    }
}
