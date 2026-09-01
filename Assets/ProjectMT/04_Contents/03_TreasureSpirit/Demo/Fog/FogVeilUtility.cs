using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class FogVeilUtility
    {
        public static readonly Color UnexploredColor = new Color(0.05f, 0.045f, 0.04f, 0.5f);
        public static readonly Color ExploredColor = new Color(0.07f, 0.06f, 0.05f, 0.38f);

        private static Material sharedMaterial;

        public static Material SharedMaterial
        {
            get
            {
                if (sharedMaterial == null)
                {
                    sharedMaterial = CreateMaterial();
                }

                return sharedMaterial;
            }
        }

        private static readonly MaterialPropertyBlock ColorBlock = new MaterialPropertyBlock();

        public static void ApplyColor(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.GetPropertyBlock(ColorBlock);
            ColorBlock.SetColor("_BaseColor", color);
            ColorBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(ColorBlock);
        }

        private static Material CreateMaterial()
        {
            Shader shader = Shader.Find("ProjectMT/TreasureSpirit/FogVeil");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material material = new Material(shader);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = 3100;
            material.SetColor("_BaseColor", UnexploredColor);
            material.SetColor("_Color", UnexploredColor);
            return material;
        }
    }
}
