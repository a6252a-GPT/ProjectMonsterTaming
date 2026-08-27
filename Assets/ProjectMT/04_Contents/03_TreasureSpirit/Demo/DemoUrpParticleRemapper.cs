using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    /// <summary>
    /// Built-in 파티클 셰이더를 URP Particles/Unlit으로 바꿔 마젠타를 막습니다.
    /// </summary>
    internal static class DemoUrpParticleRemapper
    {
        private static readonly Dictionary<int, Material> urpParticleLooks = new Dictionary<int, Material>();

        public static void Remap(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            ParticleSystemRenderer[] renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                ParticleSystemRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.sharedMaterial = GetUrpParticleMaterial(renderer.sharedMaterial);
                if (renderer.trailMaterial != null)
                {
                    renderer.trailMaterial = GetUrpParticleMaterial(renderer.trailMaterial);
                }

                Material[] shared = renderer.sharedMaterials;
                if (shared == null || shared.Length <= 1)
                {
                    continue;
                }

                bool changed = false;
                for (int m = 0; m < shared.Length; m++)
                {
                    Material remapped = GetUrpParticleMaterial(shared[m]);
                    if (remapped != shared[m])
                    {
                        shared[m] = remapped;
                        changed = true;
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = shared;
                }
            }
        }

        private static Material GetUrpParticleMaterial(Material source)
        {
            if (source != null && IsUrpShader(source.shader))
            {
                return source;
            }

            int key = source != null ? source.GetInstanceID() : 0;
            if (urpParticleLooks.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Material urp = CreateUrpParticleMaterial(source);
            urpParticleLooks[key] = urp;
            return urp;
        }

        private static bool IsUrpShader(Shader shader)
        {
            if (shader == null)
            {
                return false;
            }

            string shaderName = shader.name;
            return shaderName.IndexOf("Universal", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   shaderName.IndexOf("URP", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Material CreateUrpParticleMaterial(Material source)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material material = new Material(shader)
            {
                name = source != null ? source.name + "_URP" : "Particle_URP"
            };
            Texture texture = null;
            Color color = Color.white;
            if (source != null)
            {
                if (source.HasProperty("_BaseMap"))
                {
                    texture = source.GetTexture("_BaseMap");
                }

                if (texture == null && source.HasProperty("_MainTex"))
                {
                    texture = source.GetTexture("_MainTex");
                }

                if (source.HasProperty("_BaseColor"))
                {
                    color = source.GetColor("_BaseColor");
                }
                else if (source.HasProperty("_Color"))
                {
                    color = source.GetColor("_Color");
                }
                else if (source.HasProperty("_TintColor"))
                {
                    color = source.GetColor("_TintColor");
                }
            }

            if (texture == null)
            {
                texture = Texture2D.whiteTexture;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 2f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", 1f);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", 1f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.renderQueue = 3000;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return material;
        }
    }
}
