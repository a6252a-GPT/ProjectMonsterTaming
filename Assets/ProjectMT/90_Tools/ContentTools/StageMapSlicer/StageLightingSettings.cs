using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectMT.Tools.StageMapSlicer
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class StageLightingSettings : MonoBehaviour
    {
        [SerializeField] private bool applyOnEnable = true;
        [SerializeField] private bool applySunReference;
        [SerializeField] private Light sun;
        [SerializeField] private Material skybox;

        [Header("Ambient")]
        [SerializeField] private AmbientMode ambientMode;
        [SerializeField] private Color ambientLight;
        [SerializeField] private Color ambientSkyColor;
        [SerializeField] private Color ambientEquatorColor;
        [SerializeField] private Color ambientGroundColor;
        [SerializeField] private float ambientIntensity;
        [SerializeField] private Color subtractiveShadowColor;

        [Header("Reflection")]
        [SerializeField] private DefaultReflectionMode defaultReflectionMode;
        [SerializeField] private int defaultReflectionResolution;
        [SerializeField] private Texture customReflectionTexture;
        [SerializeField] private float reflectionIntensity;
        [SerializeField] private int reflectionBounces;

        [Header("Fog")]
        [SerializeField] private bool fog;
        [SerializeField] private Color fogColor;
        [SerializeField] private FogMode fogMode;
        [SerializeField] private float fogDensity;
        [SerializeField] private float fogStartDistance;
        [SerializeField] private float fogEndDistance;

        [Header("Other")]
        [SerializeField] private float haloStrength;
        [SerializeField] private float flareStrength;
        [SerializeField] private float flareFadeSpeed;

        private void OnEnable()
        {
            if (Application.isPlaying && applyOnEnable)
            {
                Apply();
            }
        }

        public void CaptureCurrent(Light stageSun)
        {
            applySunReference = RenderSettings.sun == null || stageSun != null;
            sun = stageSun;
            skybox = RenderSettings.skybox;

            ambientMode = RenderSettings.ambientMode;
            ambientLight = RenderSettings.ambientLight;
            ambientSkyColor = RenderSettings.ambientSkyColor;
            ambientEquatorColor = RenderSettings.ambientEquatorColor;
            ambientGroundColor = RenderSettings.ambientGroundColor;
            ambientIntensity = RenderSettings.ambientIntensity;
            subtractiveShadowColor = RenderSettings.subtractiveShadowColor;

            defaultReflectionMode = RenderSettings.defaultReflectionMode;
            defaultReflectionResolution = RenderSettings.defaultReflectionResolution;
            customReflectionTexture = RenderSettings.customReflectionTexture;
            reflectionIntensity = RenderSettings.reflectionIntensity;
            reflectionBounces = RenderSettings.reflectionBounces;

            fog = RenderSettings.fog;
            fogColor = RenderSettings.fogColor;
            fogMode = RenderSettings.fogMode;
            fogDensity = RenderSettings.fogDensity;
            fogStartDistance = RenderSettings.fogStartDistance;
            fogEndDistance = RenderSettings.fogEndDistance;

            haloStrength = RenderSettings.haloStrength;
            flareStrength = RenderSettings.flareStrength;
            flareFadeSpeed = RenderSettings.flareFadeSpeed;
        }

        [ContextMenu("저장된 환경 설정 적용")]
        public void Apply()
        {
            if (applySunReference)
            {
                RenderSettings.sun = sun;
            }

            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientLight = ambientLight;
            RenderSettings.ambientSkyColor = ambientSkyColor;
            RenderSettings.ambientEquatorColor = ambientEquatorColor;
            RenderSettings.ambientGroundColor = ambientGroundColor;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.subtractiveShadowColor = subtractiveShadowColor;

            RenderSettings.defaultReflectionMode = defaultReflectionMode;
            RenderSettings.defaultReflectionResolution = defaultReflectionResolution;
            RenderSettings.customReflectionTexture = customReflectionTexture;
            RenderSettings.reflectionIntensity = reflectionIntensity;
            RenderSettings.reflectionBounces = reflectionBounces;

            RenderSettings.fog = fog;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogStartDistance = fogStartDistance;
            RenderSettings.fogEndDistance = fogEndDistance;

            RenderSettings.haloStrength = haloStrength;
            RenderSettings.flareStrength = flareStrength;
            RenderSettings.flareFadeSpeed = flareFadeSpeed;
            DynamicGI.UpdateEnvironment();
        }
    }
}
