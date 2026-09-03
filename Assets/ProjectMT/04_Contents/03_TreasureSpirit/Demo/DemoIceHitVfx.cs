using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    [DisallowMultipleComponent]
    internal sealed class DemoIceHitVfx : MonoBehaviour
    {
        private const float Duration = 0.38f;
        private const int ShardCount = 7;
        private static readonly Color BurstTint = new Color(0.55f, 0.9f, 1f, 0.92f);
        private static readonly Color NumberTint = new Color(0.62f, 0.94f, 1f, 1f);

        private static Material burstMaterial;
        private static Material shardMaterial;
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

        private Transform[] shards;
        private Vector3[] shardVelocities;
        private Renderer burstRenderer;
        private Light burstLight;
        private float elapsed;

        public static void Play(IDamageable enemy, float appliedDamage)
        {
            if (enemy == null)
            {
                return;
            }

            Vector3 hitPoint = enemy.Position + Vector3.up * 0.95f;
            PlayBurst(hitPoint);
            PlayBodyFlash(enemy);
            PlayDamageNumber(enemy, enemy.Position, appliedDamage);
        }

        private static void PlayDamageNumber(IDamageable enemy, Vector3 hitPoint, float appliedDamage)
        {
            if (appliedDamage <= 0f)
            {
                return;
            }

            ICombatFeedbackPlayer feedback = DemoDungeonController.Active != null
                ? DemoDungeonController.Active.CombatWorld?.Feedback
                : null;
            if (feedback == null)
            {
                return;
            }

            int mergeKey = enemy is Component component ? component.GetInstanceID() : 0;
            feedback.PlayFloatingNumber(
                hitPoint,
                appliedDamage,
                FloatingNumberStyle.EnemyDamage,
                mergeKey);
        }

        private static void PlayBodyFlash(IDamageable enemy)
        {
            if (enemy is not Component component)
            {
                return;
            }

            UnitVisualFeedback visual = component.GetComponent<UnitVisualFeedback>();
            if (visual == null)
            {
                visual = component.gameObject.AddComponent<UnitVisualFeedback>();
            }

            visual.PlayHit();
        }

        private static void PlayBurst(Vector3 position)
        {
            GameObject root = new GameObject("IceHitBurst");
            root.transform.position = position;
            DemoIceHitVfx view = root.AddComponent<DemoIceHitVfx>();
            view.Build();
        }

        private void Build()
        {
            GameObject burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            burst.name = "Burst";
            burst.transform.SetParent(transform, false);
            burst.transform.localScale = Vector3.one * 0.18f;
            Collider burstCollider = burst.GetComponent<Collider>();
            if (burstCollider != null)
            {
                burstCollider.enabled = false;
            }

            burstRenderer = burst.GetComponent<Renderer>();
            burstRenderer.sharedMaterial = GetBurstMaterial();
            burstRenderer.shadowCastingMode = ShadowCastingMode.Off;
            burstRenderer.receiveShadows = false;

            burstLight = gameObject.AddComponent<Light>();
            burstLight.type = LightType.Point;
            burstLight.color = BurstTint;
            burstLight.intensity = 3.4f;
            burstLight.range = 3.6f;
            burstLight.shadows = LightShadows.None;

            shards = new Transform[ShardCount];
            shardVelocities = new Vector3[ShardCount];
            Material shardMat = GetShardMaterial();
            for (int i = 0; i < ShardCount; i++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "Shard";
                shard.transform.SetParent(transform, false);
                shard.transform.localScale = new Vector3(0.07f, 0.18f, 0.07f);
                shard.transform.localRotation = Random.rotation;
                Collider shardCollider = shard.GetComponent<Collider>();
                if (shardCollider != null)
                {
                    shardCollider.enabled = false;
                }

                Renderer shardRenderer = shard.GetComponent<Renderer>();
                shardRenderer.sharedMaterial = shardMat;
                shardRenderer.shadowCastingMode = ShadowCastingMode.Off;
                shardRenderer.receiveShadows = false;

                float yaw = i * (360f / ShardCount) + Random.Range(-18f, 18f);
                Vector3 direction = Quaternion.Euler(Random.Range(-28f, 42f), yaw, 0f) * Vector3.forward;
                shardVelocities[i] = direction * Random.Range(2.4f, 3.8f);
                shards[i] = shard.transform;
            }
        }

        private void Update()
        {
            if (DemoDungeonController.IsGameplayPaused)
            {
                return;
            }

            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / Duration);
            float grow = Mathf.Lerp(0.18f, 1.35f, 1f - Mathf.Pow(1f - ratio, 2f));
            if (burstRenderer != null)
            {
                burstRenderer.transform.localScale = Vector3.one * grow;
                Color color = BurstTint;
                color.a = Mathf.Lerp(0.75f, 0f, ratio);
                ApplyColor(burstRenderer, color);
            }

            if (burstLight != null)
            {
                burstLight.intensity = Mathf.Lerp(3.4f, 0f, ratio);
            }

            if (shards != null)
            {
                for (int i = 0; i < shards.Length; i++)
                {
                    Transform shard = shards[i];
                    if (shard == null)
                    {
                        continue;
                    }

                    shard.localPosition += shardVelocities[i] * Time.deltaTime;
                    shardVelocities[i] += Vector3.down * (6.5f * Time.deltaTime);
                    shard.localScale = Vector3.Lerp(new Vector3(0.07f, 0.18f, 0.07f), Vector3.zero, ratio);
                }
            }

            if (ratio >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private static void ApplyColor(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.GetPropertyBlock(PropertyBlock);
            PropertyBlock.SetColor("_BaseColor", color);
            PropertyBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(PropertyBlock);
        }

        private static Material GetBurstMaterial()
        {
            if (burstMaterial != null)
            {
                return burstMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            burstMaterial = new Material(shader)
            {
                name = "IceHitBurst",
                color = BurstTint
            };
            burstMaterial.SetColor("_BaseColor", BurstTint);
            burstMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            burstMaterial.SetInt("_DstBlend", (int)BlendMode.One);
            burstMaterial.SetInt("_ZWrite", 0);
            burstMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            burstMaterial.SetOverrideTag("RenderType", "Transparent");
            burstMaterial.renderQueue = 3100;
            return burstMaterial;
        }

        private static Material GetShardMaterial()
        {
            if (shardMaterial != null)
            {
                return shardMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            shardMaterial = new Material(shader)
            {
                name = "IceHitShard",
                color = NumberTint
            };
            shardMaterial.SetColor("_BaseColor", NumberTint);
            shardMaterial.SetColor("_Color", NumberTint);
            return shardMaterial;
        }
    }
}
