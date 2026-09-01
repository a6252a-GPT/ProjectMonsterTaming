using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectMT.Contents.FallenCommander
{
    public sealed class FallenCommanderBossTransformationVisual
    {
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        private readonly List<RendererState> rendererStates = new();

        public FallenCommanderBossTransformationVisual(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                var sourceMaterials = renderer.sharedMaterials;
                var transformationMaterials = new Material[sourceMaterials.Length];
                for (var materialIndex = 0;
                     materialIndex < sourceMaterials.Length;
                     materialIndex++)
                {
                    var sourceMaterial = sourceMaterials[materialIndex];
                    if (sourceMaterial == null)
                    {
                        continue;
                    }

                    var transformationMaterial = new Material(sourceMaterial);
                    ConfigureTransparency(transformationMaterial);
                    transformationMaterials[materialIndex] = transformationMaterial;
                    rendererStates.Add(new RendererState(
                        renderer,
                        materialIndex,
                        sourceMaterials,
                        transformationMaterial,
                        sourceMaterial.HasProperty(BaseColorProperty),
                        sourceMaterial.HasProperty(ColorProperty),
                        sourceMaterial.HasProperty(BaseColorProperty)
                            ? sourceMaterial.GetColor(BaseColorProperty)
                            : Color.white,
                        sourceMaterial.HasProperty(ColorProperty)
                            ? sourceMaterial.GetColor(ColorProperty)
                            : Color.white));
                }

                renderer.sharedMaterials = transformationMaterials;
            }
        }

        public void SetVisibility(float visibility)
        {
            var clampedVisibility = Mathf.Clamp01(visibility);
            var brightness = Mathf.Lerp(0.08f, 1f, clampedVisibility);
            for (var index = 0; index < rendererStates.Count; index++)
            {
                rendererStates[index].Apply(clampedVisibility, brightness);
            }
        }

        public void Restore()
        {
            for (var index = 0; index < rendererStates.Count; index++)
            {
                rendererStates[index].Restore();
            }
        }

        private static void ConfigureTransparency(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            if (material.HasProperty("_SurfaceType"))
            {
                material.SetFloat("_SurfaceType", 1f);
            }

            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 3f);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private sealed class RendererState
        {
            private readonly Renderer renderer;
            private readonly int materialIndex;
            private readonly Material[] sourceMaterials;
            private readonly Material transformationMaterial;
            private readonly bool supportsBaseColor;
            private readonly bool supportsColor;
            private readonly Color baseColor;
            private readonly Color color;
            private readonly bool wasEnabled;

            public RendererState(
                Renderer renderer,
                int materialIndex,
                Material[] sourceMaterials,
                Material transformationMaterial,
                bool supportsBaseColor,
                bool supportsColor,
                Color baseColor,
                Color color)
            {
                this.renderer = renderer;
                this.materialIndex = materialIndex;
                this.sourceMaterials = sourceMaterials;
                this.transformationMaterial = transformationMaterial;
                this.supportsBaseColor = supportsBaseColor;
                this.supportsColor = supportsColor;
                this.baseColor = baseColor;
                this.color = color;
                wasEnabled = renderer.enabled;
            }

            public void Apply(float visibility, float brightness)
            {
                if (renderer == null)
                {
                    return;
                }

                renderer.enabled = wasEnabled && visibility > 0.01f;
                if (supportsBaseColor)
                {
                    transformationMaterial.SetColor(
                        BaseColorProperty,
                        ApplyVisibility(baseColor, visibility, brightness));
                }

                if (supportsColor)
                {
                    transformationMaterial.SetColor(
                        ColorProperty,
                        ApplyVisibility(color, visibility, brightness));
                }
            }

            public void Restore()
            {
                if (renderer == null)
                {
                    return;
                }

                renderer.enabled = wasEnabled;
                renderer.sharedMaterials = sourceMaterials;
                if (Application.isPlaying)
                {
                    Object.Destroy(transformationMaterial);
                }
                else
                {
                    Object.DestroyImmediate(transformationMaterial);
                }
            }

            private static Color ApplyVisibility(
                Color source,
                float visibility,
                float brightness)
            {
                return new Color(
                    source.r * brightness,
                    source.g * brightness,
                    source.b * brightness,
                    source.a * visibility);
            }
        }
    }
}
