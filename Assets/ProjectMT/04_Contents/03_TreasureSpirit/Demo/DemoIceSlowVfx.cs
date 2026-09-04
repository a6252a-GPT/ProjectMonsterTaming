using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    [DisallowMultipleComponent]
    internal sealed class DemoIceSlowVfx : MonoBehaviour
    {
        private const int CrystalCount = 3;
        private const float OrbitRadius = 0.3f;
        private const float OrbitSpeed = 140f;
        private const float CrystalSize = 0.2f;
        private static readonly Color CrystalTint = new Color(0.55f, 0.88f, 1f, 0.95f);

        private static Sprite crystalSprite;
        private static Material crystalMaterial;
        private static Mesh crystalQuad;

        private Transform host;
        private GameObject fxRoot;
        private Transform[] crystals;
        private float until;
        private float headHeight = 1.7f;
        private float spin;

        public static void Play(Transform body, float duration)
        {
            if (body == null || duration <= 0f)
            {
                return;
            }

            DemoIceSlowVfx view = body.GetComponent<DemoIceSlowVfx>();
            if (view == null)
            {
                view = body.gameObject.AddComponent<DemoIceSlowVfx>();
            }

            view.Restart(duration);
        }

        public static void Stop(Transform body)
        {
            if (body != null && body.TryGetComponent(out DemoIceSlowVfx view))
            {
                view.Hide();
            }
        }

        private void Restart(float duration)
        {
            host = transform;
            until = Mathf.Max(until, Time.time + duration);
            headHeight = ResolveHeadHeight(host);
            EnsureFx();
            fxRoot.SetActive(true);
            enabled = true;
        }

        private void Hide()
        {
            until = 0f;
            DestroyFx();
            if (enabled)
            {
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (host == null || Time.time >= until)
            {
                Hide();
                return;
            }

            spin += OrbitSpeed * Time.deltaTime;
            fxRoot.transform.position = host.position + Vector3.up * headHeight;
            Quaternion billboard = ResolveBillboard();
            for (int i = 0; i < crystals.Length; i++)
            {
                float yaw = spin + i * (360f / CrystalCount);
                Vector3 offset = Quaternion.Euler(0f, yaw, 0f) * new Vector3(OrbitRadius, 0f, 0f);
                offset.y = Mathf.Sin((spin + i * 90f) * Mathf.Deg2Rad) * 0.05f;
                crystals[i].localPosition = offset;
                crystals[i].rotation = billboard;
            }
        }

        private void OnDisable()
        {
            DestroyFx();
        }

        private void OnDestroy()
        {
            DestroyFx();
        }

        private void DestroyFx()
        {
            if (fxRoot == null)
            {
                return;
            }

            Destroy(fxRoot);
            fxRoot = null;
            crystals = null;
        }

        private void EnsureFx()
        {
            if (fxRoot != null)
            {
                return;
            }

            Sprite sprite = GetCrystalSprite();
            Material material = GetCrystalMaterial();
            fxRoot = new GameObject("IceSlowFx");
            crystals = new Transform[CrystalCount];
            for (int i = 0; i < CrystalCount; i++)
            {
                GameObject crystal = new GameObject("Crystal", typeof(MeshFilter), typeof(MeshRenderer));
                crystal.transform.SetParent(fxRoot.transform, false);
                crystal.transform.localScale = Vector3.one * CrystalSize;
                crystal.GetComponent<MeshFilter>().sharedMesh = GetCrystalQuad();
                MeshRenderer meshRenderer = crystal.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                crystals[i] = crystal.transform;
            }

            if (sprite != null)
            {
                material.mainTexture = sprite.texture;
            }
        }

        private static float ResolveHeadHeight(Transform body)
        {
            var agent = body.GetComponent<NavMeshAgent>();
            if (agent != null && agent.height > 0.2f)
            {
                return agent.height + 0.18f;
            }

            Renderer[] renderers = body.GetComponentsInChildren<Renderer>();
            float top = 0f;
            bool any = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer current = renderers[i];
                if (current == null || current is ParticleSystemRenderer)
                {
                    continue;
                }

                float height = current.bounds.max.y - body.position.y;
                if (!any || height > top)
                {
                    top = height;
                    any = true;
                }
            }

            return any ? top + 0.22f : 1.7f;
        }

        private static Quaternion ResolveBillboard()
        {
            Camera camera = Camera.main;
            return camera != null ? camera.transform.rotation : Quaternion.identity;
        }

        private static Mesh GetCrystalQuad()
        {
            if (crystalQuad != null)
            {
                return crystalQuad;
            }

            crystalQuad = new Mesh
            {
                name = "IceCrystalQuad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f)
                },
                triangles = new[] { 0, 2, 1, 2, 3, 1 }
            };
            crystalQuad.RecalculateBounds();
            return crystalQuad;
        }

        private static Material GetCrystalMaterial()
        {
            if (crystalMaterial != null)
            {
                return crystalMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            crystalMaterial = new Material(shader)
            {
                name = "IceSlowCrystal",
                color = CrystalTint
            };
            crystalMaterial.SetColor("_BaseColor", CrystalTint);
            crystalMaterial.SetColor("_Color", CrystalTint);
            crystalMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            crystalMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            crystalMaterial.SetInt("_ZWrite", 0);
            crystalMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            crystalMaterial.SetOverrideTag("RenderType", "Transparent");
            crystalMaterial.renderQueue = 3200;
            return crystalMaterial;
        }

        private static Sprite GetCrystalSprite()
        {
            if (crystalSprite != null)
            {
                return crystalSprite;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            float center = (size - 1) * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);
                    float flake = Mathf.Lerp(0.18f, 0.92f, Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 3f)), 0.7f));
                    float alpha = Mathf.Clamp01((flake - radius) * 10f);
                    float core = Mathf.Clamp01((0.16f - radius) * 14f);
                    alpha = Mathf.Max(alpha, core);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            crystalSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            crystalSprite.name = "IceCrystal";
            return crystalSprite;
        }
    }
}
