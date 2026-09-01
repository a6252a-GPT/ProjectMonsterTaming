using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class DemoDungeonAtmosphere
    {
        private static readonly Color VoidColor = new Color(0.07f, 0.06f, 0.05f, 1f);
        private static readonly Color FogColor = new Color(0.28f, 0.25f, 0.21f, 1f);
        private static readonly Color AmbientColor = new Color(0.62f, 0.58f, 0.50f, 1f);
        private static readonly Color TorchColor = new Color(1f, 0.84f, 0.62f, 1f);

        private const float FogDensity = 0.002f;
        private const float TorchIntensity = 9.5f;
        private const float TorchRange = 13f;

        private static bool captured;
        private static bool previousFog;
        private static Color previousFogColor;
        private static FogMode previousFogMode;
        private static float previousFogDensity;
        private static Color previousAmbientSky;
        private static Color previousAmbientEquator;
        private static Color previousAmbientGround;
        private static float previousAmbientIntensity;
        private static AmbientMode previousAmbientMode;
        private static Material previousSkybox;
        private static float previousReflectionIntensity;

        public static void Apply(GameObject mapRoot)
        {
            CaptureIfNeeded();
            ApplyWorld();
            ApplyCamera();
            DimDirectionalLights();
            TuneLocalLights(mapRoot);
        }

        public static void TuneLocalFireLight(Light light)
        {
            if (light == null || light.type == LightType.Directional)
            {
                return;
            }

            light.color = TorchColor;
            light.intensity = Mathf.Max(light.intensity, TorchIntensity);
            light.range = Mathf.Clamp(light.range <= 0.01f ? TorchRange : light.range, 8f, TorchRange);
            light.shadows = LightShadows.None;
        }

        public static void Restore()
        {
            if (!captured)
            {
                return;
            }

            RenderSettings.fog = previousFog;
            RenderSettings.fogColor = previousFogColor;
            RenderSettings.fogMode = previousFogMode;
            RenderSettings.fogDensity = previousFogDensity;
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientSkyColor = previousAmbientSky;
            RenderSettings.ambientEquatorColor = previousAmbientEquator;
            RenderSettings.ambientGroundColor = previousAmbientGround;
            RenderSettings.ambientIntensity = previousAmbientIntensity;
            RenderSettings.skybox = previousSkybox;
            RenderSettings.reflectionIntensity = previousReflectionIntensity;
            captured = false;
        }

        private static void CaptureIfNeeded()
        {
            if (captured)
            {
                return;
            }

            previousFog = RenderSettings.fog;
            previousFogColor = RenderSettings.fogColor;
            previousFogMode = RenderSettings.fogMode;
            previousFogDensity = RenderSettings.fogDensity;
            previousAmbientMode = RenderSettings.ambientMode;
            previousAmbientSky = RenderSettings.ambientSkyColor;
            previousAmbientEquator = RenderSettings.ambientEquatorColor;
            previousAmbientGround = RenderSettings.ambientGroundColor;
            previousAmbientIntensity = RenderSettings.ambientIntensity;
            previousSkybox = RenderSettings.skybox;
            previousReflectionIntensity = RenderSettings.reflectionIntensity;
            captured = true;
        }

        private static void ApplyWorld()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientSkyColor = AmbientColor;
            RenderSettings.ambientEquatorColor = AmbientColor;
            RenderSettings.ambientGroundColor = AmbientColor;
            RenderSettings.ambientIntensity = 1.25f;
            RenderSettings.reflectionIntensity = 0.4f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = FogDensity;
        }

        private static void ApplyCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = VoidColor;
        }

        private static void DimDirectionalLights()
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light != null && light.type == LightType.Directional)
                {
                    light.enabled = true;
                    light.intensity = Mathf.Max(light.intensity, 0.45f);
                    light.color = new Color(1f, 0.93f, 0.84f);
                }
            }
        }

        private static void TuneLocalLights(GameObject mapRoot)
        {
            if (mapRoot == null)
            {
                return;
            }

            Light[] lights = mapRoot.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light == null)
                {
                    continue;
                }

                if (light.type == LightType.Directional)
                {
                    light.enabled = true;
                    light.intensity = Mathf.Max(light.intensity, 0.45f);
                    light.color = new Color(1f, 0.93f, 0.84f);
                    continue;
                }

                TuneLocalFireLight(light);
            }
        }
    }
}
