using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectMT.Tools.FeelPreview
{
    /// <summary>기본공격 연구실에서 시각 FEEL 70종을 비교하는 데모 기반 카탈로그다.</summary>
    [DisallowMultipleComponent]
    public sealed partial class CombatFeelCatalogPreviewLab : MonoBehaviour
    {
        private enum Tab { Model, Impact, Screen }
        private enum LabMode { Library, Composer }
        private readonly struct Item
        {
            public Item(Tab tab, string typeName) { Tab = tab; TypeName = typeName; }
            public Tab Tab { get; }
            public string TypeName { get; }
        }

        private static readonly Item[] Items = BuildItems();
        [SerializeField] private GameObject target;
        private Tab selectedTab;
        private Item selected;
        private bool selectedValid;
        private Vector2 scroll;
        private Transform visual;
        private Transform originalParent;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Vector3 originalScale;
        private Renderer[] renderers = Array.Empty<Renderer>();
        private Animator targetAnimator;
        private float originalAnimatorSpeed = 1f;
        private Camera previewCamera;
        private Vector3 cameraPosition;
        private Quaternion cameraRotation;
        private float cameraFov;
        private float cameraNear;
        private float cameraFar;
        private bool cameraOrthographic;
        private float cameraOrthographicSize;
        private bool fog;
        private Color fogColor;
        private float fogDensity;
        private Coroutine playing;
        private readonly List<GameObject> transientObjects = new List<GameObject>();
        private GameObject volumeRoot;
        private VolumeProfile volumeProfile;
        private Color overlayColor;
        private float overlayAlpha;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle sectionStyle;
        private GUIStyle tabStyle;
        private GUIStyle activeTabStyle;
        private GUIStyle effectStyle;
        private GUIStyle activeEffectStyle;
        private GUIStyle infoStyle;
        private Texture2D panelTexture;
        private Texture2D tabTexture;
        private Texture2D activeTexture;
        private Texture2D effectTexture;
        private Texture2D infoTexture;
        private int playVersion;
        private bool panelExpanded = true;
        private LabMode labMode;
        private Vector2 panelPosition = new Vector2(float.NaN, float.NaN);
        private float panelLayoutWidth = float.NaN;
        private float panelViewportWidth = float.NaN;
        private Vector2 panelDragOffset;
        private bool draggingPanel;
        private readonly List<UnityEngine.Object> transientAssets = new List<UnityEngine.Object>();

        private static Item[] BuildItems()
        {
            var items = new List<Item>(70);
            Add(items, Tab.Model,
                "MMF_AnimationCrossfade", "MMF_Animation", "MMF_AnimatorPlayState", "MMF_AnimatorSpeed", "MMF_SpriteSheetAnimation",
                "MMF_Flicker", "MMF_Material", "MMF_MaterialSetProperty", "MMF_Blink", "MMF_ShaderController", "MMF_Sprite", "MMF_SpriteRenderer", "MMF_SpriteRendererAlpha", "MMF_TextureOffset", "MMF_TextureScale",
                "MMF_DestinationTransform", "MMF_LookAt", "MMF_Position", "MMF_PositionShake", "MMF_PositionSpring", "MMF_RotatePositionAround", "MMF_Rotation", "MMF_RotationShake", "MMF_RotationSpring", "MMF_Scale", "MMF_ScaleShake", "MMF_ScaleSpring", "MMF_SetParent", "MMF_SquashAndStretch", "MMF_SquashAndStretchSpring", "MMF_Wiggle");
            Add(items, Tab.Impact, "MMF_Light", "MMF_Light2D_URP", "MMF_ParticlesInstantiation", "MMF_Particles", "MMF_InstantiateObject", "MMF_LineRenderer", "MMF_TrailRenderer");
            Add(items, Tab.Screen,
                "MMF_Fog", "MMF_ShaderGlobal", "MMF_Skybox",
                "MMF_CameraShake", "MMF_CameraZoom", "MMF_CameraClippingPlanes", "MMF_Fade", "MMF_CameraFieldOfView", "MMF_Flash", "MMF_CameraOrthographicSize",
                "MMF_Bloom", "MMF_Bloom_URP", "MMF_ChannelMixer_URP", "MMF_ChromaticAberration", "MMF_ChromaticAberration_URP", "MMF_ColorAdjustments_URP", "MMF_ColorGrading", "MMF_DepthOfField", "MMF_DepthOfField_URP", "MMF_FilmGrain_URP", "MMF_GlobalPPVolumeAutoBlend", "MMF_GlobalPPVolumeAutoBlend_URP", "MMF_LensDistortion", "MMF_LensDistortion_URP", "MMF_MotionBlur_URP", "MMF_PaniniProjection_URP", "MMF_PPMovingFilter", "MMF_Vignette", "MMF_Vignette_URP", "MMF_WhiteBalance_URP", "MMF_FreezeFrame", "MMF_TimescaleModifier");
            return items.ToArray();
        }

        private static void Add(ICollection<Item> items, Tab tab, params string[] names)
        {
            foreach (var name in names) items.Add(new Item(tab, name));
        }

        private void Awake()
        {
            CacheReferences();
        }

        public void Configure(GameObject targetObject)
        {
            ResetPreview();
            target = targetObject;
            CacheReferences(true);
        }

        public void PreviewEffect(string typeName)
        {
            ResetAuthoringPreview();
            var item = Items.FirstOrDefault(candidate => candidate.TypeName == typeName);
            if (string.IsNullOrWhiteSpace(item.TypeName)) return;
            ResetPreview();
            CacheReferences();
            CaptureDemoState();
            selected = item;
            selectedValid = true;
            var version = ++playVersion;
            playing = StartCoroutine(PlayAndRestore(item, version));
        }

        private IEnumerator PlayAndRestore(Item item, int version)
        {
            yield return item.Tab switch
            {
                Tab.Model => PlayModelDemo(item.TypeName),
                Tab.Impact => PlayImpactDemo(item.TypeName),
                Tab.Screen => PlayScreenDemo(item.TypeName),
                _ => Empty()
            };
            if (version != playVersion) yield break;
            playing = null;
            RestoreRuntimeState();
        }

        public void ResetPreview()
        {
            playVersion++;
            if (playing != null) StopCoroutine(playing);
            playing = null;
            RestoreRuntimeState();
        }

        private void RestoreRuntimeState()
        {
            RestoreTarget();
            RestoreCamera();
            RenderSettings.fog = fog;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            overlayAlpha = 0f;
            Time.timeScale = 1f;
            RestoreDemoState();
            foreach (var item in transientObjects) if (item != null) { item.SetActive(false); Destroy(item); }
            transientObjects.Clear();
            if (volumeRoot != null) { volumeRoot.SetActive(false); Destroy(volumeRoot); }
            if (volumeProfile != null) Destroy(volumeProfile);
            volumeRoot = null;
            volumeProfile = null;
            foreach (var asset in transientAssets) if (asset != null) Destroy(asset);
            transientAssets.Clear();
        }

        private IEnumerator PlayModel(string typeName)
        {
            if (visual == null) yield break;
            if (typeName is "MMF_Flicker" or "MMF_Material" or "MMF_MaterialSetProperty" or "MMF_Blink" or "MMF_ShaderController")
            {
                yield return RendererPulse(new Color(1f, 0.34f, 0.14f));
                yield break;
            }
            if (typeName.Contains("Sprite") || typeName.Contains("Texture"))
            {
                yield return Marker(typeName);
                yield break;
            }
            if (typeName.StartsWith("MMF_Animation", StringComparison.Ordinal) || typeName.StartsWith("MMF_Animator", StringComparison.Ordinal))
            {
                var animator = visual.GetComponentInChildren<Animator>();
                var speed = animator != null ? animator.speed : 1f;
                if (animator != null && typeName == "MMF_AnimatorSpeed") animator.speed = 2.2f;
                yield return TransformPulse("MMF_RotationSpring");
                if (animator != null) animator.speed = speed;
                yield break;
            }
            yield return TransformPulse(typeName);
        }

        private IEnumerator TransformPulse(string typeName)
        {
            var pivot = CreateTransient("[Runtime] Catalog Pivot", visual.position + visual.forward * 0.5f);
            if (typeName == "MMF_SetParent") visual.SetParent(pivot.transform, true);
            for (var elapsed = 0f; elapsed < 0.58f; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / 0.58f);
                var pulse = Mathf.Sin(t * Mathf.PI);
                var spring = Mathf.Sin(t * Mathf.PI * 3.4f) * (1f - t);
                var jitter = Mathf.Sin(t * Mathf.PI * 12f) * (1f - t);
                if (typeName is "MMF_Position" or "MMF_DestinationTransform") visual.localPosition = originalPosition + new Vector3(0.28f * pulse, 0.04f * pulse, -0.14f * pulse);
                else if (typeName == "MMF_PositionShake") visual.localPosition = originalPosition + new Vector3(jitter * 0.075f, Mathf.Cos(elapsed * 70f) * 0.025f, 0f);
                else if (typeName == "MMF_PositionSpring") visual.localPosition = originalPosition + new Vector3(-spring * 0.26f, 0f, 0f);
                else if (typeName == "MMF_RotatePositionAround") visual.position = pivot.transform.position + new Vector3(Mathf.Cos(t * Mathf.PI * 1.3f), 0f, Mathf.Sin(t * Mathf.PI * 1.3f)) * 0.5f;
                else if (typeName == "MMF_LookAt") visual.localRotation = originalRotation * Quaternion.Euler(0f, 38f * pulse, 0f);
                else if (typeName == "MMF_Rotation") visual.localRotation = originalRotation * Quaternion.Euler(0f, 0f, 30f * pulse);
                else if (typeName == "MMF_RotationShake") visual.localRotation = originalRotation * Quaternion.Euler(0f, 0f, jitter * 8f);
                else if (typeName == "MMF_RotationSpring") visual.localRotation = originalRotation * Quaternion.Euler(0f, 0f, spring * 25f);
                else if (typeName == "MMF_Scale") visual.localScale = originalScale * (1f + 0.18f * pulse);
                else if (typeName == "MMF_ScaleShake") visual.localScale = originalScale * (1f + jitter * 0.07f);
                else if (typeName == "MMF_ScaleSpring") visual.localScale = originalScale * (1f + spring * 0.17f);
                else if (typeName == "MMF_SquashAndStretch") visual.localScale = Vector3.Scale(originalScale, new Vector3(1f + 0.16f * pulse, 1f - 0.23f * pulse, 1f + 0.16f * pulse));
                else if (typeName == "MMF_SquashAndStretchSpring") visual.localScale = Vector3.Scale(originalScale, new Vector3(1f - 0.12f * spring, 1f + 0.2f * spring, 1f - 0.12f * spring));
                else if (typeName == "MMF_Wiggle")
                {
                    visual.localPosition = originalPosition + new Vector3(jitter * 0.045f, 0f, 0f);
                    visual.localRotation = originalRotation * Quaternion.Euler(0f, jitter * 5f, jitter * 7f);
                    visual.localScale = originalScale * (1f + jitter * 0.05f);
                }
                else if (typeName == "MMF_SetParent") pivot.transform.position = (originalParent != null ? originalParent.position : originalPosition) + new Vector3(0f, 0.06f * pulse, -0.22f * pulse);
                yield return null;
            }
        }

        private IEnumerator PlayImpact(string typeName)
        {
            if (visual == null) yield break;
            if (typeName is "MMF_Light" or "MMF_Light2D_URP")
            {
                var root = CreateTransient("[Runtime] Catalog Light", HitPoint());
                if (typeName == "MMF_Light2D_URP")
                {
                    var halo = root.AddComponent<SpriteRenderer>();
                    halo.sprite = RadialSprite();
                    halo.color = new Color(0.4f, 0.86f, 1f, 0.8f);
                    yield return Scale(root.transform, 0.25f, 2f, 0.32f);
                    yield break;
                }
                var light = root.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.color = new Color(1f, 0.75f, 0.25f);
                light.range = 3.2f;
                for (var elapsed = 0f; elapsed < 0.18f; elapsed += Time.unscaledDeltaTime) { light.intensity = Mathf.Sin(elapsed / 0.18f * Mathf.PI) * 9f; yield return null; }
                yield break;
            }
            if (typeName is "MMF_ParticlesInstantiation" or "MMF_Particles")
            {
                CreateBurst(typeName == "MMF_ParticlesInstantiation" ? 34 : 22);
                yield return Wait(0.7f);
                yield break;
            }
            if (typeName == "MMF_InstantiateObject")
            {
                var root = CreateTransient("[Runtime] Catalog Object", HitPoint());
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.SetParent(root.transform, false);
                sphere.transform.localScale = Vector3.one * 0.24f;
                ApplyColor(sphere.GetComponent<Renderer>(), new Color(1f, 0.62f, 0.2f), 3f);
                yield return Scale(root.transform, 0.2f, 2.2f, 0.4f);
                yield break;
            }
            if (typeName == "MMF_LineRenderer") { yield return Line(); yield break; }
            yield return Trail();
        }

        private IEnumerator PlayScreen(string typeName)
        {
            if (typeName == "MMF_Fog")
            {
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.1f, 0.28f, 0.52f);
                RenderSettings.fogDensity = 0.055f;
                yield return Wait(0.55f);
                yield break;
            }
            if (typeName is "MMF_ShaderGlobal" or "MMF_Skybox")
            {
                yield return Overlay(new Color(0.25f, 0.68f, 1f), 0.2f, 0.45f);
                yield break;
            }
            if (typeName == "MMF_CameraShake") { yield return CameraShake(); yield break; }
            if (typeName == "MMF_CameraZoom") { yield return Fov(-9f, 0.35f); yield break; }
            if (typeName == "MMF_CameraClippingPlanes")
            {
                if (previewCamera != null) { previewCamera.nearClipPlane = 0.03f; previewCamera.farClipPlane = Mathf.Min(cameraFar, 35f); }
                yield return Wait(0.48f); yield break;
            }
            if (typeName == "MMF_Fade") { yield return Overlay(Color.black, 0.78f, 0.5f); yield break; }
            if (typeName == "MMF_CameraFieldOfView") { yield return Fov(7f, 0.26f); yield break; }
            if (typeName == "MMF_Flash") { yield return Overlay(new Color(1f, 0.93f, 0.7f), 0.5f, 0.2f); yield break; }
            if (typeName == "MMF_CameraOrthographicSize")
            {
                if (previewCamera != null) { previewCamera.orthographic = true; previewCamera.orthographicSize = 7.2f; }
                yield return Wait(0.54f); yield break;
            }
            if (typeName == "MMF_FreezeFrame") { Time.timeScale = 0f; yield return Wait(0.1f); yield break; }
            if (typeName == "MMF_TimescaleModifier") { Time.timeScale = 0.22f; yield return Wait(0.58f); yield break; }
            if (!CreatePostProcess(typeName)) yield return Overlay(new Color(0.35f, 0.7f, 1f), 0.18f, 0.35f);
            else yield return Wait(0.65f);
        }

        private IEnumerator RendererPulse(Color color)
        {
            for (var elapsed = 0f; elapsed < 0.45f; elapsed += Time.unscaledDeltaTime)
            {
                var pulse = Mathf.Sin(elapsed / 0.45f * Mathf.PI * 4f) * 0.5f + 0.5f;
                foreach (var renderer in renderers) ApplyColor(renderer, Color.Lerp(Color.white, color, pulse), pulse * 3.5f);
                yield return null;
            }
        }

        private IEnumerator Marker(string typeName)
        {
            var root = CreateTransient("[Runtime] Catalog Marker", HitPoint());
            var sprite = root.AddComponent<SpriteRenderer>();
            sprite.sprite = RadialSprite();
            sprite.color = new Color(0.4f, 0.9f, 1f, 0.9f);
            for (var elapsed = 0f; elapsed < 0.5f; elapsed += Time.unscaledDeltaTime)
            {
                var t = elapsed / 0.5f;
                root.transform.Rotate(0f, 0f, (typeName is "MMF_TextureOffset" or "MMF_SpriteSheetAnimation" ? -420f : 110f) * Time.unscaledDeltaTime);
                root.transform.localScale = Vector3.one * Mathf.Lerp(0.22f, typeName == "MMF_TextureScale" ? 2f : 1.55f, t);
                sprite.color = new Color(0.4f, 0.9f, 1f, typeName == "MMF_SpriteRendererAlpha" ? 1f - t : 0.9f - t * 0.4f);
                yield return null;
            }
        }

        private IEnumerator Line()
        {
            var root = CreateTransient("[Runtime] Catalog Line", HitPoint());
            var line = root.AddComponent<LineRenderer>();
            line.material = PreviewMaterial();
            line.positionCount = 2;
            line.widthMultiplier = 0.065f;
            line.SetPosition(0, HitPoint() - visual.right * 0.8f);
            line.SetPosition(1, HitPoint() + visual.right * 0.8f);
            for (var elapsed = 0f; elapsed < 0.35f; elapsed += Time.unscaledDeltaTime)
            {
                var alpha = Mathf.Sin(elapsed / 0.35f * Mathf.PI);
                line.startColor = new Color(1f, 0.85f, 0.35f, alpha);
                line.endColor = new Color(1f, 0.28f, 0.1f, alpha);
                yield return null;
            }
        }

        private IEnumerator Trail()
        {
            var root = CreateTransient("[Runtime] Catalog Trail", HitPoint() - visual.right * 0.85f);
            var trail = root.AddComponent<TrailRenderer>();
            trail.material = PreviewMaterial();
            trail.time = 0.32f;
            trail.startWidth = 0.18f;
            trail.endWidth = 0.01f;
            trail.startColor = new Color(0.42f, 0.88f, 1f, 0.9f);
            trail.endColor = new Color(0.2f, 0.45f, 1f, 0f);
            var start = root.transform.position;
            var end = HitPoint() + visual.right * 0.85f;
            for (var elapsed = 0f; elapsed < 0.42f; elapsed += Time.unscaledDeltaTime) { root.transform.position = Vector3.Lerp(start, end, elapsed / 0.42f); yield return null; }
        }

        private void CreateBurst(int count)
        {
            var root = CreateTransient("[Runtime] Catalog Particle", HitPoint());
            var particle = root.AddComponent<ParticleSystem>();
            var main = particle.main;
            main.duration = 0.4f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.68f, 0.22f), new Color(1f, 0.95f, 0.72f));
            var emission = particle.emission;
            emission.rateOverTime = 0;
            particle.Emit(count);
        }

        private IEnumerator CameraShake()
        {
            if (previewCamera == null) yield break;
            for (var elapsed = 0f; elapsed < 0.28f; elapsed += Time.unscaledDeltaTime)
            {
                var damp = 1f - elapsed / 0.28f;
                previewCamera.transform.position = cameraPosition + UnityEngine.Random.insideUnitSphere * (0.085f * damp);
                previewCamera.transform.rotation = cameraRotation * Quaternion.Euler(UnityEngine.Random.Range(-1.3f, 1.3f) * damp, UnityEngine.Random.Range(-1.3f, 1.3f) * damp, 0f);
                yield return null;
            }
        }

        private IEnumerator Fov(float amount, float duration)
        {
            if (previewCamera == null) yield break;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime) { previewCamera.fieldOfView = cameraFov + amount * Mathf.Sin(elapsed / duration * Mathf.PI); yield return null; }
        }

        private IEnumerator Overlay(Color color, float alpha, float duration)
        {
            overlayColor = color;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime) { overlayAlpha = alpha * Mathf.Sin(elapsed / duration * Mathf.PI); yield return null; }
            overlayAlpha = 0f;
        }

        private bool CreatePostProcess(string typeName)
        {
            var componentType = FindType(VolumeType(typeName));
            if (componentType == null) return false;
            volumeRoot = CreateTransient("[Runtime] Catalog Volume", Vector3.zero);
            var volume = volumeRoot.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 999f;
            volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = volumeProfile;
            var add = typeof(VolumeProfile).GetMethod("Add", new[] { typeof(Type), typeof(bool) });
            var component = add?.Invoke(volumeProfile, new object[] { componentType, true }) as VolumeComponent;
            if (component == null) return false;
            if (typeName.Contains("Bloom")) { Set(component, "intensity", 7f); Set(component, "threshold", 0.55f); }
            else if (typeName.Contains("ChannelMixer")) { Set(component, "redOutGreenIn", 45f); Set(component, "blueOutRedIn", 30f); }
            else if (typeName.Contains("Chromatic")) Set(component, "intensity", 0.72f);
            else if (typeName.Contains("ColorAdjust") || typeName.Contains("ColorGrading")) { Set(component, "postExposure", 0.65f); Set(component, "contrast", 44f); Set(component, "saturation", -24f); }
            else if (typeName.Contains("Depth")) { Set(component, "focusDistance", 4.8f); Set(component, "aperture", 0.3f); }
            else if (typeName.Contains("FilmGrain")) Set(component, "intensity", 0.75f);
            else if (typeName.Contains("Lens")) Set(component, "intensity", -0.5f);
            else if (typeName.Contains("MotionBlur")) Set(component, "intensity", 0.75f);
            else if (typeName.Contains("Panini")) { Set(component, "distance", 0.78f); Set(component, "cropToFit", 0.8f); }
            else if (typeName.Contains("WhiteBalance")) { Set(component, "temperature", 64f); Set(component, "tint", -22f); }
            else Set(component, "intensity", 0.53f);
            return true;
        }

        private static string VolumeType(string name)
        {
            if (name.Contains("Bloom")) return "UnityEngine.Rendering.Universal.Bloom";
            if (name.Contains("ChannelMixer")) return "UnityEngine.Rendering.Universal.ChannelMixer";
            if (name.Contains("Chromatic")) return "UnityEngine.Rendering.Universal.ChromaticAberration";
            if (name.Contains("ColorAdjust") || name.Contains("ColorGrading")) return "UnityEngine.Rendering.Universal.ColorAdjustments";
            if (name.Contains("Depth")) return "UnityEngine.Rendering.Universal.DepthOfField";
            if (name.Contains("FilmGrain")) return "UnityEngine.Rendering.Universal.FilmGrain";
            if (name.Contains("Lens")) return "UnityEngine.Rendering.Universal.LensDistortion";
            if (name.Contains("MotionBlur")) return "UnityEngine.Rendering.Universal.MotionBlur";
            if (name.Contains("Panini")) return "UnityEngine.Rendering.Universal.PaniniProjection";
            if (name.Contains("WhiteBalance")) return "UnityEngine.Rendering.Universal.WhiteBalance";
            return "UnityEngine.Rendering.Universal.Vignette";
        }

        private static Type FindType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            return null;
        }

        private static void Set(VolumeComponent component, string name, object value)
        {
            var parameter = component.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(component);
            if (parameter == null) return;
            var type = parameter.GetType();
            type.GetProperty("overrideState")?.SetValue(parameter, true);
            var property = type.GetProperty("value");
            if (property?.PropertyType == typeof(float)) property.SetValue(parameter, Convert.ToSingle(value));
            else if (property?.PropertyType == typeof(int)) property.SetValue(parameter, Convert.ToInt32(value));
        }

        private void CacheReferences(bool force = false)
        {
            if (force || visual == null)
            {
                var candidate = target != null ? target.transform.Find("Visual") ?? target.transform : null;
                if (candidate != null)
                {
                    visual = candidate;
                    originalParent = visual.parent;
                    originalPosition = visual.localPosition;
                    originalRotation = visual.localRotation;
                    originalScale = visual.localScale;
                    renderers = visual.GetComponentsInChildren<Renderer>(true);
                    targetAnimator = visual.GetComponentInChildren<Animator>(true);
                    originalAnimatorSpeed = targetAnimator != null ? targetAnimator.speed : 1f;
                }
            }
            previewCamera ??= Camera.main ?? FindFirstObjectByType<Camera>();
            if (previewCamera != null)
            {
                cameraPosition = previewCamera.transform.position;
                cameraRotation = previewCamera.transform.rotation;
                cameraFov = previewCamera.fieldOfView;
                cameraNear = previewCamera.nearClipPlane;
                cameraFar = previewCamera.farClipPlane;
                cameraOrthographic = previewCamera.orthographic;
                cameraOrthographicSize = previewCamera.orthographicSize;
            }
            fog = RenderSettings.fog;
            fogColor = RenderSettings.fogColor;
            fogDensity = RenderSettings.fogDensity;
        }

        private void RestoreTarget()
        {
            if (visual == null) return;
            if (visual.parent != originalParent) visual.SetParent(originalParent, false);
            visual.localPosition = originalPosition;
            visual.localRotation = originalRotation;
            visual.localScale = originalScale;
            if (targetAnimator != null) targetAnimator.speed = originalAnimatorSpeed;
        }

        private void RestoreCamera()
        {
            if (previewCamera == null) return;
            previewCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            previewCamera.fieldOfView = cameraFov;
            previewCamera.nearClipPlane = cameraNear;
            previewCamera.farClipPlane = cameraFar;
            previewCamera.orthographic = cameraOrthographic;
            previewCamera.orthographicSize = cameraOrthographicSize;
        }

        private GameObject CreateTransient(string name, Vector3 position)
        {
            var item = new GameObject(name);
            item.transform.position = position;
            transientObjects.Add(item);
            return item;
        }

        private Vector3 HitPoint() => visual.position + Vector3.up * 0.9f + visual.forward * 0.28f;

        private static IEnumerator Scale(Transform target, float from, float to, float duration)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime) { target.localScale = Vector3.one * Mathf.Lerp(from, to, elapsed / duration); yield return null; }
        }

        private static IEnumerator Wait(float duration)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime) yield return null;
        }

        private static IEnumerator Empty() { yield break; }

        private static void ApplyColor(Renderer renderer, Color color, float emission)
        {
            if (renderer == null || renderer.sharedMaterial == null) return;
            var block = new MaterialPropertyBlock();
            if (renderer.sharedMaterial.HasProperty("_BaseColor")) block.SetColor("_BaseColor", color);
            if (renderer.sharedMaterial.HasProperty("_Color")) block.SetColor("_Color", color);
            if (renderer.sharedMaterial.HasProperty("_EmissionColor")) block.SetColor("_EmissionColor", color * emission);
            renderer.SetPropertyBlock(block);
        }

        private Material PreviewMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            transientAssets.Add(material);
            return material;
        }

        private Sprite RadialSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var point = new Vector2(x / (float)(size - 1), y / (float)(size - 1)) * 2f - Vector2.one;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - point.magnitude)));
            }
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, size);
            transientAssets.Add(sprite);
            transientAssets.Add(texture);
            return sprite;
        }

        private void OnGUI()
        {
            EnsureStyles();
            // Game View의 OnGUI 좌표는 Screen 크기와 같은 렌더 좌표계다.
            // Windows 125% 배율은 Editor 창에만 적용되므로 여기서 다시 나누면 이중 보정된다.
            var viewportWidth = (float)Screen.width;
            var viewportHeight = (float)Screen.height;
            if (overlayAlpha > 0.001f)
            {
                var old = GUI.color;
                GUI.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, overlayAlpha);
                GUI.DrawTexture(new Rect(0f, 0f, viewportWidth, viewportHeight), Texture2D.whiteTexture);
                GUI.color = old;
            }
            DrawDemoOverlay();
            var authoringMode = labMode != LabMode.Library;
            var compact = viewportWidth < (authoringMode ? 1760f : 1540f);
            var width = authoringMode
                ? Mathf.Min(920f, viewportWidth - 48f)
                : compact ? Mathf.Min(720f, viewportWidth - 48f) : 512f;
            const float panelMargin = 24f;
            var height = Mathf.Min(860f, viewportHeight - panelMargin * 2f);
            UpdatePanelLayout(viewportWidth, viewportHeight, width, height, panelMargin);
            if (panelExpanded)
                HandlePanelDrag(new Rect(panelPosition.x + 16f, panelPosition.y + 8f, Mathf.Max(80f, width - 238f), 34f), viewportWidth, viewportHeight, width, height);
            var x = panelPosition.x;
            var y = panelPosition.y;
            if (!panelExpanded)
            {
                const float collapsedWidth = 236f;
                var collapsedX = GetCollapsedPanelX(viewportWidth, width, collapsedWidth);
                if (GUI.Button(new Rect(collapsedX, y, collapsedWidth, 42f), "시각 타격감 열기  ▼", activeTabStyle)) panelExpanded = true;
                return;
            }
            GUI.Box(new Rect(x, y, width, height), GUIContent.none, panelStyle);
            GUILayout.BeginArea(new Rect(x + 20f, y + 18f, width - 40f, height - 34f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("PROJECTMT  /  VISUAL IMPACT LAB", sectionStyle);
            GUILayout.Label("상단을 드래그해서 이동", textStyle, GUILayout.Width(126f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("우측 상단", tabStyle, GUILayout.Width(72f), GUILayout.Height(26f)))
                panelPosition = new Vector2(viewportWidth - width - panelMargin, panelMargin);
            if (GUILayout.Button("접기  ▲", tabStyle, GUILayout.Width(78f), GUILayout.Height(26f)))
            {
                panelExpanded = false;
                ResetPreview();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(labMode switch
            {
                LabMode.Composer => "FEEL 프로필 만들기",
                _ => "시각 타격감 라이브러리"
            }, titleStyle);
            GUILayout.Label(labMode == LabMode.Library
                ? "버튼을 누르면 단독 재생되고 끝나는 즉시 자동 원상복구됩니다."
                : "효과를 조립하고 주요값과 타격점을 조절한 뒤 하나의 프로필로 저장합니다.", textStyle);
            GUILayout.Space(8f);
            DrawLabModeTabs();
            if (labMode != LabMode.Library)
            {
                DrawAuthoringModePanel(width, height);
                GUILayout.EndArea();
                return;
            }
            GUILayout.BeginHorizontal();
            DrawTab(Tab.Model, "모델 반응 31"); DrawTab(Tab.Impact, "타격점 7"); DrawTab(Tab.Screen, "화면 반응 32");
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            GUILayout.Label(TabDescription(selectedTab), textStyle);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Max(150f, height - 545f)));
            var visible = Items.Where(item => item.Tab == selectedTab).ToArray();
            var columns = compact ? 1 : 2;
            var effectWidth = compact ? width - 48f : (width - 48f) * 0.5f;
            foreach (var group in visible.GroupBy(item => GroupName(item.TypeName)))
            {
                GUILayout.Space(7f);
                GUILayout.Label(group.Key, sectionStyle);
                var groupItems = group.ToArray();
                for (var index = 0; index < groupItems.Length; index += columns)
                {
                    GUILayout.BeginHorizontal();
                    DrawEffect(groupItems[index], effectWidth);
                    if (!compact && index + 1 < groupItems.Length) DrawEffect(groupItems[index + 1], effectWidth);
                    else if (!compact) GUILayout.Space(effectWidth);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.Space(7f);
            GUILayout.BeginVertical(infoStyle);
            GUILayout.Label(selectedValid ? DisplayName(selected.TypeName) : "효과를 선택하세요", titleStyle);
            GUILayout.Label(selectedValid ? selected.TypeName : "모델 31 · 타격점 7 · 화면 32", textStyle);
            GUILayout.Label(selectedValid ? DemoExplanation(selected.TypeName) : "Audio · Haptics · 전투 UI는 제외했습니다.", textStyle);
            DrawSelectedControls();
            GUILayout.Label(DemoStatusText(), sectionStyle);
            GUILayout.EndVertical();
            GUILayout.BeginHorizontal();
            GUI.enabled = selectedValid;
            if (GUILayout.Button("조절값으로 재생", activeTabStyle, GUILayout.Height(36f))) PreviewEffect(selected.TypeName);
            GUI.enabled = true;
            if (GUILayout.Button("즉시 정지 · 원상복구", tabStyle, GUILayout.Height(36f))) ResetPreview();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void HandlePanelDrag(Rect handle, float viewportWidth, float viewportHeight, float width, float height)
        {
            var current = Event.current;
            if (current == null) return;
            if (current.type == EventType.MouseDown && current.button == 0 && handle.Contains(current.mousePosition))
            {
                draggingPanel = true;
                panelDragOffset = current.mousePosition - panelPosition;
                current.Use();
                return;
            }
            if (current.type == EventType.MouseDrag && draggingPanel)
            {
                panelPosition = current.mousePosition - panelDragOffset;
                ClampPanelPosition(viewportWidth, viewportHeight, width, height);
                current.Use();
                return;
            }
            if (current.type == EventType.MouseUp && draggingPanel)
            {
                draggingPanel = false;
                ClampPanelPosition(viewportWidth, viewportHeight, width, height);
                current.Use();
            }
        }

        private void ClampPanelPosition(float viewportWidth, float viewportHeight, float width, float height)
        {
            panelPosition.x = Mathf.Clamp(panelPosition.x, 8f, Mathf.Max(8f, viewportWidth - width - 8f));
            panelPosition.y = Mathf.Clamp(panelPosition.y, 8f, Mathf.Max(8f, viewportHeight - height - 8f));
        }

        private void UpdatePanelLayout(float viewportWidth, float viewportHeight, float width, float height, float panelMargin)
        {
            if (float.IsNaN(panelPosition.x) || float.IsNaN(panelPosition.y))
            {
                panelPosition = new Vector2(viewportWidth - width - panelMargin, panelMargin);
            }
            else if (!float.IsNaN(panelLayoutWidth) && !float.IsNaN(panelViewportWidth))
            {
                var previousRightGap = panelViewportWidth - (panelPosition.x + panelLayoutWidth);
                if (previousRightGap <= panelMargin + 1f)
                    panelPosition.x = viewportWidth - width - panelMargin;
            }

            panelLayoutWidth = width;
            panelViewportWidth = viewportWidth;
            ClampPanelPosition(viewportWidth, viewportHeight, width, height);
        }

        private float GetCollapsedPanelX(float viewportWidth, float expandedWidth, float collapsedWidth)
        {
            var expandedRight = panelPosition.x + expandedWidth;
            return Mathf.Clamp(expandedRight - collapsedWidth, 8f, Mathf.Max(8f, viewportWidth - collapsedWidth - 8f));
        }

        private void DrawTab(Tab tab, string label)
        {
            if (GUILayout.Button(label, selectedTab == tab ? activeTabStyle : tabStyle, GUILayout.Height(40f)))
            {
                ResetPreview();
                selectedTab = tab;
                scroll = Vector2.zero;
            }
        }

        private void DrawEffect(Item item, float width)
        {
            var label = $"{DisplayName(item.TypeName)}\n<size=9>{item.TypeName}</size>";
            if (GUILayout.Button(label, selectedValid && selected.TypeName == item.TypeName ? activeEffectStyle : effectStyle, GUILayout.Width(width), GUILayout.Height(52f))) PreviewEffect(item.TypeName);
        }

        private static string TabDescription(Tab tab) => tab switch
        {
            Tab.Model => "피격 대상의 모션·표면·위치·회전·크기 변화를 비교합니다.",
            Tab.Impact => "실제 접촉 위치에 생기는 빛·파티클·선·잔상을 비교합니다.",
            Tab.Screen => "카메라·후처리·환경·시간 효과를 화면 전체에서 비교합니다.",
            _ => string.Empty
        };

        private static string GroupName(string typeName)
        {
            if (typeName.StartsWith("MMF_Animation", StringComparison.Ordinal) || typeName.StartsWith("MMF_Animator", StringComparison.Ordinal) || typeName == "MMF_SpriteSheetAnimation") return "애니메이션";
            if (typeName is "MMF_Flicker" or "MMF_Material" or "MMF_MaterialSetProperty" or "MMF_Blink" or "MMF_ShaderController" or "MMF_Sprite" or "MMF_SpriteRenderer" or "MMF_SpriteRendererAlpha" or "MMF_TextureOffset" or "MMF_TextureScale") return "표면 · 발광";
            if (typeName is "MMF_Light" or "MMF_Light2D_URP") return "빛";
            if (typeName.Contains("Particle") || typeName == "MMF_InstantiateObject") return "파티클 · 생성";
            if (typeName is "MMF_LineRenderer" or "MMF_TrailRenderer") return "선 · 잔상";
            if (typeName is "MMF_Fog" or "MMF_ShaderGlobal" or "MMF_Skybox") return "환경 · 전역";
            if (typeName.StartsWith("MMF_Camera", StringComparison.Ordinal) || typeName is "MMF_Fade" or "MMF_Flash") return "카메라";
            if (typeName is "MMF_FreezeFrame" or "MMF_TimescaleModifier") return "시간";
            if (typeName.Contains("Bloom") || typeName.Contains("Chromatic") || typeName.Contains("Color") || typeName.Contains("Depth") || typeName.Contains("Film") || typeName.Contains("Volume") || typeName.Contains("Lens") || typeName.Contains("Motion") || typeName.Contains("Panini") || typeName.Contains("PP") || typeName.Contains("Vignette") || typeName.Contains("WhiteBalance") || typeName.Contains("ChannelMixer")) return "후처리";
            return "위치 · 회전 · 크기";
        }

        private static string DisplayName(string typeName) => typeName switch
        {
            "MMF_AnimationCrossfade" => "모션 부드럽게 전환", "MMF_Animation" => "애니메이션 값 변경", "MMF_AnimatorPlayState" => "지정 모션 즉시 재생", "MMF_AnimatorSpeed" => "모션 속도 변화", "MMF_SpriteSheetAnimation" => "프레임 애니메이션",
            "MMF_Flicker" => "모델 색상 깜빡임", "MMF_Material" => "피격 재질 교체", "MMF_MaterialSetProperty" => "재질 값 순간 변화", "MMF_Blink" => "모델 렌더러 점멸", "MMF_ShaderController" => "셰이더 효과 재생", "MMF_Sprite" => "스프라이트 교체", "MMF_SpriteRenderer" => "스프라이트 색·반전", "MMF_SpriteRendererAlpha" => "스프라이트 투명도", "MMF_TextureOffset" => "텍스처 흐르기", "MMF_TextureScale" => "텍스처 크기 변화",
            "MMF_DestinationTransform" => "목표 위치로 이동", "MMF_LookAt" => "타격 방향 바라보기", "MMF_Position" => "위치 밀림", "MMF_PositionShake" => "위치 흔들림", "MMF_PositionSpring" => "위치 탄성 반동", "MMF_RotatePositionAround" => "중심 주위 회전", "MMF_Rotation" => "회전 반동", "MMF_RotationShake" => "회전 흔들림", "MMF_RotationSpring" => "회전 탄성", "MMF_Scale" => "크기 변화", "MMF_ScaleShake" => "크기 떨림", "MMF_ScaleSpring" => "크기 탄성", "MMF_SetParent" => "기준축 전환", "MMF_SquashAndStretch" => "눌림 · 늘어남", "MMF_SquashAndStretchSpring" => "탄성 눌림 · 늘어남", "MMF_Wiggle" => "복합 흔들림",
            "MMF_Light" => "3D 타격광", "MMF_Light2D_URP" => "2D 타격광", "MMF_ParticlesInstantiation" => "파티클 생성 폭발", "MMF_Particles" => "기존 파티클 재생", "MMF_InstantiateObject" => "타격 오브젝트 생성", "MMF_LineRenderer" => "베기 선광", "MMF_TrailRenderer" => "타격 잔상",
            "MMF_Fog" => "안개 변화", "MMF_ShaderGlobal" => "전역 셰이더 변화", "MMF_Skybox" => "하늘 · 환경 교체", "MMF_CameraShake" => "카메라 흔들림", "MMF_CameraZoom" => "카메라 줌", "MMF_CameraClippingPlanes" => "클리핑 거리 변화", "MMF_Fade" => "화면 페이드", "MMF_CameraFieldOfView" => "시야각 펄스", "MMF_Flash" => "화면 플래시", "MMF_CameraOrthographicSize" => "직교 시야 변화",
            "MMF_Bloom" => "블룸 발광 (Legacy)", "MMF_Bloom_URP" => "블룸 발광 (URP)", "MMF_ChannelMixer_URP" => "색 채널 혼합", "MMF_ChromaticAberration" => "색수차 (Legacy)", "MMF_ChromaticAberration_URP" => "색수차 (URP)", "MMF_ColorAdjustments_URP" => "노출 · 채도 · 대비", "MMF_ColorGrading" => "색보정 (Legacy)", "MMF_DepthOfField" => "초점 심도 (Legacy)", "MMF_DepthOfField_URP" => "초점 심도 (URP)", "MMF_FilmGrain_URP" => "필름 입자", "MMF_GlobalPPVolumeAutoBlend" => "전역 후처리 전환", "MMF_GlobalPPVolumeAutoBlend_URP" => "전역 후처리 전환 (URP)", "MMF_LensDistortion" => "렌즈 왜곡 (Legacy)", "MMF_LensDistortion_URP" => "렌즈 왜곡 (URP)", "MMF_MotionBlur_URP" => "모션 블러", "MMF_PaniniProjection_URP" => "파니니 투영", "MMF_PPMovingFilter" => "이동 후처리 필터", "MMF_Vignette" => "비네트 (Legacy)", "MMF_Vignette_URP" => "비네트 (URP)", "MMF_WhiteBalance_URP" => "화이트 밸런스", "MMF_FreezeFrame" => "순간 정지", "MMF_TimescaleModifier" => "슬로 모션",
            _ => typeName
        };

        private static string Detail(string typeName)
        {
            if (typeName.Contains("Spring")) return "Spring 복원 계열의 탄성 차이를 확인합니다.";
            if (typeName.Contains("Shake")) return "짧은 흔들림 계열의 리듬을 확인합니다.";
            if (typeName.Contains("Bloom") || typeName.Contains("Material") || typeName.Contains("Light")) return "밝기·발광·표면 반응을 확인합니다.";
            if (typeName.Contains("Particle") || typeName.Contains("Renderer") || typeName.Contains("Object")) return "타격점 형태와 지속시간을 확인합니다.";
            if (typeName.Contains("Camera") || typeName.Contains("PP") || typeName.Contains("URP") || typeName.Contains("Vignette")) return "화면 전역 효과입니다. 연구실에서만 비교합니다.";
            return "선택한 FEEL 효과의 시각적 성격을 연구실 조건으로 표시합니다.";
        }

        private void EnsureStyles()
        {
            if (panelStyle != null) return;
            panelTexture = Texture(new Color(0.025f, 0.035f, 0.05f, 0.97f));
            tabTexture = Texture(new Color(0.08f, 0.11f, 0.15f, 0.98f));
            activeTexture = Texture(new Color(0.08f, 0.68f, 0.64f, 0.98f));
            effectTexture = Texture(new Color(0.055f, 0.075f, 0.105f, 0.98f));
            infoTexture = Texture(new Color(0.045f, 0.065f, 0.09f, 0.98f));
            panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture } };
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, wordWrap = true };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.7f, 0.77f, 0.84f) }, wordWrap = true };
            sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.2f, 0.86f, 0.8f) }, wordWrap = true };
            tabStyle = ButtonStyle(tabTexture, new Color(0.8f, 0.85f, 0.9f), 11);
            activeTabStyle = ButtonStyle(activeTexture, Color.white, 11);
            effectStyle = ButtonStyle(effectTexture, new Color(0.78f, 0.84f, 0.9f), 10);
            activeEffectStyle = ButtonStyle(activeTexture, Color.white, 10);
            infoStyle = new GUIStyle(GUI.skin.box) { normal = { background = infoTexture }, padding = new RectOffset(10, 10, 8, 8) };
            effectStyle.richText = true;
            activeEffectStyle.richText = true;
        }

        private static GUIStyle ButtonStyle(Texture2D texture, Color color, int size) => new GUIStyle(GUI.skin.button)
        {
            normal = { background = texture, textColor = color }, hover = { background = texture, textColor = Color.white }, active = { background = texture, textColor = Color.white }, fontSize = size, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true, margin = new RectOffset(2, 2, 2, 2)
        };

        private static Texture2D Texture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color); texture.Apply(); return texture;
        }

        private void OnDisable() { ResetPreview(); ResetAuthoringPreview(); }
        private void OnDestroy()
        {
            DestroyAuthoringSession();
            DestroyDemoAssets(); Destroy(panelTexture); Destroy(tabTexture); Destroy(activeTexture); Destroy(effectTexture); Destroy(infoTexture);
        }
    }
}
