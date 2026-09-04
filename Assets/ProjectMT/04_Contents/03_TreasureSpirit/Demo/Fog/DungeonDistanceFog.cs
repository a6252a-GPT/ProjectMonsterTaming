using UnityEngine;
using UnityEngine.Rendering;
using ProjectMT.Contents.TreasureSpirit;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    public sealed class DungeonDistanceFog : MonoBehaviour
    {
        public const float GroundRadius = 2f;
        public const float JumpRadius = 3f;
        public const float FadeDistance = 1.4f;
        private const float MaxAlpha = 0.58f;
        private const int MaxLights = 16;

        private static readonly int PlayerPosId = Shader.PropertyToID("_PlayerPos");
        private static readonly int ClearRadiusId = Shader.PropertyToID("_ClearRadius");
        private static readonly int FadeDistanceId = Shader.PropertyToID("_FadeDistance");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int LightCountId = Shader.PropertyToID("_LightCount");
        private static readonly int LightsId = Shader.PropertyToID("_Lights");

        private Transform player;
        private PlayerCharacterController playerMove;
        private Transform mapRoot;
        private Material material;
        private Light[] cachedLights = System.Array.Empty<Light>();
        private readonly Vector4[] lights = new Vector4[MaxLights];
        private float nextLightRefresh;

        public void Initialize(Transform dungeonRoot)
        {
            mapRoot = dungeonRoot;
            BindPlayer(null);
            CacheLights();
            EnsurePlane();
            RefreshLights();
            ApplyPlayer();
        }

        public void SetPlayer(Transform playerTransform)
        {
            BindPlayer(playerTransform);
            ApplyPlayer();
        }

        public static float ResolveClearRadius(Transform playerTransform)
        {
            PlayerCharacterController move = FindMove(playerTransform);
            return move != null && move.IsJumping ? JumpRadius : GroundRadius;
        }

        private void BindPlayer(Transform playerTransform)
        {
            player = playerTransform;
            playerMove = FindMove(playerTransform);
        }

        private static PlayerCharacterController FindMove(Transform playerTransform)
        {
            if (playerTransform == null)
            {
                return null;
            }

            PlayerCharacterController move = playerTransform.GetComponent<PlayerCharacterController>();
            return move != null ? move : playerTransform.GetComponentInParent<PlayerCharacterController>();
        }

        private void LateUpdate()
        {
            ApplyPlayer();
            if (Time.unscaledTime >= nextLightRefresh)
            {
                RefreshLights();
            }
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        private void EnsurePlane()
        {
            if (!TryGetMapBounds(out Bounds bounds))
            {
                bounds = new Bounds(transform.position, new Vector3(80f, 4f, 80f));
            }

            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plane.name = "DistanceFogPlane";
            plane.transform.SetParent(transform, false);
            Object.Destroy(plane.GetComponent<Collider>());

            plane.transform.position = new Vector3(
                bounds.center.x,
                bounds.max.y + 2.4f,
                bounds.center.z);
            plane.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            plane.transform.localScale = new Vector3(
                Mathf.Max(20f, bounds.size.x + 8f),
                Mathf.Max(20f, bounds.size.z + 8f),
                1f);

            Shader shader = Shader.Find("ProjectMT/TreasureSpirit/DistanceFog");
            material = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
            material.SetColor(BaseColorId, new Color(0.035f, 0.03f, 0.025f, MaxAlpha));
            material.SetColor("_Color", new Color(0.035f, 0.03f, 0.025f, MaxAlpha));
            material.SetFloat(ClearRadiusId, GroundRadius);
            material.SetFloat(FadeDistanceId, FadeDistance);
            material.renderQueue = 2450;

            MeshRenderer renderer = plane.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void ApplyPlayer()
        {
            if (material == null)
            {
                return;
            }

            if (player == null)
            {
                material.SetVector(PlayerPosId, new Vector3(0f, -9999f, 0f));
                material.SetFloat(ClearRadiusId, 0f);
                return;
            }

            material.SetVector(PlayerPosId, player.position);
            float radius = playerMove != null && playerMove.IsJumping ? JumpRadius : GroundRadius;
            material.SetFloat(ClearRadiusId, radius);
        }

        private void RefreshLights()
        {
            nextLightRefresh = Time.unscaledTime + 0.5f;
            if (material == null)
            {
                return;
            }

            Light[] found = cachedLights;
            int count = 0;
            for (int i = 0; i < found.Length && count < MaxLights; i++)
            {
                Light light = found[i];
                if (light == null || !light.enabled || light.type == LightType.Directional)
                {
                    continue;
                }

                float hole = Mathf.Clamp(light.range * 0.55f, 3.2f, 7.5f);
                Vector3 position = light.transform.position;
                lights[count] = new Vector4(position.x, position.y, position.z, hole);
                count++;
            }

            for (int i = count; i < MaxLights; i++)
            {
                lights[i] = Vector4.zero;
            }

            material.SetInt(LightCountId, count);
            material.SetVectorArray(LightsId, lights);
        }

        private void CacheLights()
        {
            cachedLights = mapRoot != null
                ? mapRoot.GetComponentsInChildren<Light>(true)
                : System.Array.Empty<Light>();
        }

        private bool TryGetMapBounds(out Bounds bounds)
        {
            if (DemoFloorBounds.TryGetBounds(mapRoot, out bounds))
            {
                return true;
            }

            Renderer[] renderers = mapRoot != null ? mapRoot.GetComponentsInChildren<Renderer>(true) : null;
            if (renderers == null || renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            return true;
        }
    }
}
