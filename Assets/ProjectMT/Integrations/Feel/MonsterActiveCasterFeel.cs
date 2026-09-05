using System.Collections.Generic;
using ProjectMT.Shared.Combat;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;

namespace ProjectMT.Integrations.Feel
{
    [Preserve, DisallowMultipleComponent]
    public sealed class MonsterActiveCasterFeel : MonoBehaviour // FEEL 시계로 시전자 윤곽과 발밑 파동을 재생한다
    {
        [Preserve] public float Progress;
        private Transform caster;
        private MMF_Player player;
        private FloatController clock;
        private Material rimMaterial;
        private Material ringMaterial;
        private LineRenderer ring;
        private MonsterActiveFocusStyle style;
        private Material flareMaterial;
        private readonly List<LineRenderer> flares = new List<LineRenderer>();
        private float radius;
        private float duration = 0.8f;
        private float strength;
        private bool stopped;
        private readonly List<Renderer> sources = new List<Renderer>();
        private readonly List<Renderer> overlays = new List<Renderer>();

        public static MonsterActiveCasterFeel Create(Transform target, float bodyRadius, Color color, bool mythic, MonsterActiveFocusStyle style = MonsterActiveFocusStyle.Flash)
        {
            var shader = Resources.Load<Shader>("MonsterActiveCasterAccent");
            if (target == null || shader == null)
            {
                return null;
            }
            var root = new GameObject("ActiveCasterFEEL");
            root.SetActive(false); // FloatController의 Awake 전에 바인딩한다
            var effect = root.AddComponent<MonsterActiveCasterFeel>();
            try
            {
                effect.caster = target;
                effect.style = style;
                if (style == MonsterActiveFocusStyle.ClassicDim)
                {
                    var config = MonsterActiveFocusPresentationConfig.Current;
                    effect.duration = config != null
                        ? config.ResolvePreset(mythic ? ProjectMT.Shared.Unit.MonsterRarity.Mythic :
                            ProjectMT.Shared.Unit.MonsterRarity.Legendary).MinimumVisibleDuration
                        : 2f;
                }
                root.layer = target.gameObject.layer;
                effect.radius = Mathf.Clamp(bodyRadius, 0.35f, 1.4f);
                effect.strength = mythic ? 1.3f : 1.15f;
                effect.rimMaterial = effect.CreateMaterial(shader, Color.Lerp(color, Color.white, 0.62f), 1f);
                effect.ringMaterial = effect.CreateMaterial(shader, Color.Lerp(color, Color.white, 0.48f), 0f);
                effect.BuildOverlays(target);
                effect.BuildRing();
                effect.BuildFlare(shader, color);
                effect.BuildPlayer();
                root.SetActive(true);
                effect.player.Initialization(true);
                effect.player.PlayFeedbacks(target.position, 1f);
                return effect;
            }
            catch
            {
                effect.StopImmediate();
                throw;
            }
        }

        private Material CreateMaterial(Shader shader, Color color, float rim)
        {
            var material = new Material(shader) { name = "ActiveCasterAccent_Runtime" };
            material.SetColor("_Color", color);
            material.SetFloat("_RimWeight", rim);
            material.SetFloat("_Intensity", 0f);
            return material;
        }

        private void BuildOverlays(Transform target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            foreach (var source in renderers)
            {
                if (!source.enabled || source is ParticleSystemRenderer || source is TrailRenderer ||
                    source is LineRenderer || source.name == "ActiveCasterRim")
                {
                    continue;
                }
                var skinned = source as SkinnedMeshRenderer;
                var mesh = skinned != null ? skinned.sharedMesh : source.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || (skinned == null && source is not MeshRenderer))
                {
                    continue;
                }
                var shell = new GameObject("ActiveCasterRim");
                shell.transform.SetParent(source.transform, false);
                shell.layer = source.gameObject.layer;
                Renderer overlay;
                if (skinned != null)
                {
                    var skin = shell.AddComponent<SkinnedMeshRenderer>();
                    skin.sharedMesh = mesh;
                    skin.bones = skinned.bones;
                    skin.rootBone = skinned.rootBone;
                    skin.localBounds = skinned.localBounds;
                    skin.quality = skinned.quality;
                    skin.updateWhenOffscreen = false;
                    overlay = skin;
                }
                else
                {
                    shell.AddComponent<MeshFilter>().sharedMesh = mesh;
                    overlay = shell.AddComponent<MeshRenderer>();
                }
                var materials = new Material[mesh.subMeshCount];
                for (var i = 0; i < materials.Length; i++) materials[i] = rimMaterial;
                overlay.sharedMaterials = materials;
                overlay.shadowCastingMode = ShadowCastingMode.Off;
                overlay.receiveShadows = false;
                overlay.lightProbeUsage = LightProbeUsage.Off;
                overlay.reflectionProbeUsage = ReflectionProbeUsage.Off;
                overlay.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                sources.Add(source);
                overlays.Add(overlay);
            }
        }

        private void BuildRing()
        {
            var ringObject = new GameObject("ActiveCasterWave");
            ringObject.layer = caster.gameObject.layer;
            ringObject.transform.SetParent(transform, false);
            ringObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring = ringObject.AddComponent<LineRenderer>();
            ring.sharedMaterial = ringMaterial;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.alignment = LineAlignment.TransformZ;
            ring.textureMode = LineTextureMode.Stretch;
            ring.positionCount = 64;
            ring.widthMultiplier = 0.045f;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.lightProbeUsage = LightProbeUsage.Off;
            ring.reflectionProbeUsage = ReflectionProbeUsage.Off;
            for (var i = 0; i < ring.positionCount; i++)
            {
                var angle = 2f * Mathf.PI * i / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
            }
        }



        private void BuildFlare(Shader shader, Color accent)
        {
            if (style != MonsterActiveFocusStyle.LightPillar && style != MonsterActiveFocusStyle.EnergyBurst) return;
            flareMaterial = CreateMaterial(shader, Color.Lerp(accent, Color.white, 0.7f), 0f);
            var count = style == MonsterActiveFocusStyle.LightPillar ? 3 : 10;
            for (var i = 0; i < count; i++)
            {
                var obj = new GameObject("ActiveCasterFlare");
                obj.transform.SetParent(transform, false);
                obj.layer = caster.gameObject.layer;
                var line = obj.AddComponent<LineRenderer>();
                line.sharedMaterial = flareMaterial;
                line.useWorldSpace = false;
                line.alignment = LineAlignment.View;
                line.positionCount = 2;
                line.textureMode = LineTextureMode.Stretch;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.lightProbeUsage = LightProbeUsage.Off;
                line.reflectionProbeUsage = ReflectionProbeUsage.Off;
                line.startWidth = style == MonsterActiveFocusStyle.LightPillar ? (i == 0 ? radius * 1.4f : 0.18f) : 0.24f;
                line.endWidth = 0.01f;
                flares.Add(line);
            }
        }

        private void UpdateFlare(float t)
        {
            if (flareMaterial == null) return;
            var flare = Mathf.SmoothStep(0f, 1f, t / 0.045f) *
                        (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.85f, t)));
            flareMaterial.SetFloat("_Intensity", 2.6f * flare);
            for (var i = 0; i < flares.Count; i++)
            {
                if (style == MonsterActiveFocusStyle.LightPillar)
                {
                    var offset = i == 0 ? 0f : (i == 1 ? -0.55f : 0.55f) * radius;
                    var height = Mathf.Lerp(0.6f, 4.2f, Mathf.Clamp01(t / 0.24f)) * (i == 0 ? 1f : 0.7f);
                    flares[i].SetPosition(0, new Vector3(offset, 0.08f, 0f));
                    flares[i].SetPosition(1, new Vector3(offset, height, 0f));
                }
                else
                {
                    var angle = i * 2f * Mathf.PI / flares.Count;
                    var direction = new Vector3(Mathf.Cos(angle), i % 2 == 0 ? 0.35f : 0.1f, Mathf.Sin(angle));
                    var expansion = Mathf.Clamp01((t - 0.08f) / 0.52f);
                    flares[i].SetPosition(0, Vector3.up * 0.45f + direction * radius * Mathf.Lerp(0.5f, 1.6f, expansion));
                    flares[i].SetPosition(1, Vector3.up * 0.45f + direction * radius * Mathf.Lerp(1.1f, 3.5f, expansion));
                }
            }
        }


        private void BuildPlayer()
        {
            clock = gameObject.AddComponent<FloatController>();
            clock.TargetObject = this;
            clock.PropertyName = nameof(Progress);
            clock.ControlMode = FloatController.ControlModes.OneTime;
            clock.UseUnscaledTime = true;
            clock.RevertToInitialValueAfterEnd = false;
            var timeline = new MMF_FloatController
            {
                Label = "시전자 발광 · 파동",
                Active = true,
                TargetFloatController = clock,
                ExtraTargetFloatControllers = new List<FloatController>(),
                Mode = MMF_FloatController.Modes.OneTime,
                OneTimeDuration = duration,
                OneTimeAmplitude = 1f,
                OneTimeRemapMin = 0f,
                OneTimeRemapMax = 1f,
                OneTimeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
                RevertToInitialValueAfterEnd = false,
                Timing = new MMFeedbackTiming { TimescaleMode = TimescaleModes.Unscaled, InterruptsOnStop = true }
            };
            player = gameObject.AddComponent<MMF_Player>();
            player.AutoPlayOnEnable = false;
            player.AutoPlayOnStart = false;
            player.AutoInitialization = false;
            player.InitializationMode = MMFeedbacks.InitializationModes.Script;
            player.ForceTimescaleMode = true;
            player.ForcedTimescaleMode = TimescaleModes.Unscaled;
            player.StopFeedbacksOnDisable = true;
            player.FeedbacksList = new List<MMF_Feedback> { timeline };
        }

        private void LateUpdate()
        {
            if (stopped) return;
            if (caster == null || !caster.gameObject.activeInHierarchy || Progress >= 1f)
            {
                StopImmediate();
                return;
            }
            transform.position = caster.position + Vector3.up * 0.04f;
            var t = Mathf.Clamp01(Progress);
            UpdateFlare(t);
            var envelope = Mathf.SmoothStep(0f, 1f, t / 0.06f) *
                           (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, t)));
            var burst = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.30f, t));
            rimMaterial.SetFloat("_Intensity", (1.65f + burst * 0.65f) * envelope * strength);
            rimMaterial.SetFloat("_BodyFill", 0.16f + burst * 0.18f);
            var wave = Mathf.Clamp01(t / 0.95f);
            ring.transform.localScale = Vector3.one * (radius * Mathf.Lerp(0.8f, 2.1f, wave));
            ring.widthMultiplier = Mathf.Lerp(0.23f, 0.13f, wave);
            ringMaterial.SetFloat("_Intensity", Mathf.Sin(wave * Mathf.PI) * 1.6f * strength);
            for (var i = 0; i < overlays.Count; i++)
            {
                var source = sources[i];
                var overlay = overlays[i];
                if (overlay == null) continue;
                overlay.enabled = source != null && source.enabled && source.gameObject.activeInHierarchy;
                if (source is SkinnedMeshRenderer skin && overlay is SkinnedMeshRenderer shell &&
                    skin.sharedMesh != null)
                {
                    for (var shape = 0; shape < skin.sharedMesh.blendShapeCount; shape++)
                    {
                        shell.SetBlendShapeWeight(shape, skin.GetBlendShapeWeight(shape));
                    }
                }
            }
        }

        public void StopImmediate()
        {
            if (stopped) return;
            stopped = true;
            if (player != null) player.StopFeedbacks();
            if (clock != null && clock.TargetAttribute != null) clock.Stop();
            ClearVisuals();
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            StopImmediate();
        }

        private void OnDestroy()
        {
            ClearVisuals();
        }

        private void ClearVisuals()
        {
            foreach (var overlay in overlays)
            {
                if (overlay == null) continue;
                overlay.gameObject.SetActive(false);
                Destroy(overlay.gameObject);
            }
            overlays.Clear();
            sources.Clear();
            if (ring != null) ring.enabled = false;
            if (flareMaterial != null) Destroy(flareMaterial);
            flareMaterial = null;
            foreach (var flare in flares) if (flare != null) flare.enabled = false;
            if (rimMaterial != null) Destroy(rimMaterial);
            if (ringMaterial != null) Destroy(ringMaterial);
            rimMaterial = null;
            ringMaterial = null;
        }
    }
}