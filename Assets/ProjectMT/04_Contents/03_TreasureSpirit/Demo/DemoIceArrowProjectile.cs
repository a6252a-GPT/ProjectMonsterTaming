using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    [DisallowMultipleComponent]
    internal sealed class DemoIceArrowProjectile : MonoBehaviour
    {
        private const int PoolCapacity = 8;

        private static readonly Stack<DemoIceArrowProjectile> pool = new Stack<DemoIceArrowProjectile>(PoolCapacity);
        private static readonly List<DemoIceArrowProjectile> live = new List<DemoIceArrowProjectile>(PoolCapacity);
        private static Material shaftMaterial;
        private static Material headMaterial;
        private static Transform poolRoot;

        private Vector3 launchDirection;
        private float travelSpeed;
        private float maxDistance;
        private float traveled;
        private float damage;
        private float slowSeconds;

        public static void Launch(
            Vector3 position,
            Vector3 direction,
            float speed,
            float travelDistance,
            float hitDamage,
            float slowDuration)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.forward;
            }

            direction.Normalize();
            DemoIceArrowProjectile projectile = Rent(position, Quaternion.LookRotation(direction, Vector3.up));
            projectile.travelSpeed = Mathf.Max(1f, speed);
            projectile.maxDistance = Mathf.Max(1f, travelDistance);
            projectile.damage = Mathf.Max(DemoIceCombat.ArrowDamage, hitDamage);
            projectile.slowSeconds = Mathf.Max(0.1f, slowDuration);
            projectile.launchDirection = direction;
            projectile.traveled = 0f;
            projectile.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction, Vector3.up));
        }

        public static void ClearPool()
        {
            live.Clear();
            pool.Clear();
            if (poolRoot != null)
            {
                Object.Destroy(poolRoot.gameObject);
                poolRoot = null;
            }
        }

        private void Update()
        {
            if (DemoDungeonController.IsGameplayPaused)
            {
                return;
            }

            float step = travelSpeed * Time.deltaTime;
            if (Physics.Raycast(
                    transform.position,
                    launchDirection,
                    out RaycastHit hit,
                    step + 0.1f,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                if (TryHitCollider(hit.collider))
                {
                    return;
                }

                if (hit.collider.GetComponentInParent<IDamageable>() == null)
                {
                    Recycle();
                    return;
                }
            }

            transform.position += launchDirection * step;
            traveled += step;
            IDamageable nearby = DemoCombatRoster.FindNearest(transform.position, 0.42f);
            if (nearby != null)
            {
                ApplyHit(nearby);
                return;
            }

            if (traveled >= maxDistance)
            {
                Recycle();
            }
        }

        private bool TryHitCollider(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            IDamageable damageable = collider.GetComponentInParent<IDamageable>();
            if (damageable == null || !DemoCombatRoster.IsEnemy(damageable) || !damageable.IsAlive)
            {
                return false;
            }

            ApplyHit(damageable);
            return true;
        }

        private void ApplyHit(IDamageable enemy)
        {
            float applied = enemy.ReceiveDamage(null, damage);
            if (enemy is IIceSlowable slowable)
            {
                slowable.ApplyMoveSlow(slowSeconds);
            }

            DemoIceHitVfx.Play(enemy, applied);
            Recycle();
        }

        private void Recycle()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            live.Remove(this);
            traveled = 0f;
            TrailRenderer trail = GetComponent<TrailRenderer>();
            trail?.Clear();
            gameObject.SetActive(false);
            transform.SetParent(PoolRoot, false);
            pool.Push(this);
        }

        private static DemoIceArrowProjectile Rent(Vector3 position, Quaternion rotation)
        {
            DemoIceArrowProjectile projectile = null;
            while (pool.Count > 0 && projectile == null)
            {
                projectile = pool.Pop();
            }

            if (projectile == null)
            {
                GameObject root = CreateVisual(position, rotation);
                root.transform.SetParent(PoolRoot, true);
                projectile = root.AddComponent<DemoIceArrowProjectile>();
            }
            else
            {
                projectile.gameObject.SetActive(true);
                projectile.transform.SetPositionAndRotation(position, rotation);
            }

            live.Add(projectile);
            return projectile;
        }

        private static Transform PoolRoot
        {
            get
            {
                if (poolRoot == null)
                {
                    GameObject root = new GameObject("DemoIceArrowPool");
                    Object.DontDestroyOnLoad(root);
                    poolRoot = root.transform;
                }

                return poolRoot;
            }
        }

        private static GameObject CreateVisual(Vector3 position, Quaternion rotation)
        {
            GameObject root = new GameObject("IceArrow");
            root.transform.SetPositionAndRotation(position, rotation);

            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Shaft";
            shaft.transform.SetParent(root.transform, false);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shaft.transform.localScale = new Vector3(0.07f, 0.28f, 0.07f);
            SetSharedColor(shaft, ref shaftMaterial, new Color(0.55f, 0.86f, 1f, 1f));
            DisableCollider(shaft);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 0f, 0.34f);
            head.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            head.transform.localScale = new Vector3(0.14f, 0.14f, 0.14f);
            SetSharedColor(head, ref headMaterial, new Color(0.82f, 0.95f, 1f, 1f));
            DisableCollider(head);

            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.widthMultiplier = 0.08f;
            trail.minVertexDistance = 0.05f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.7f, 0.92f, 1f), 0f),
                    new GradientColorKey(new Color(0.35f, 0.7f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.85f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            trail.colorGradient = gradient;
            Shader trailShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (trailShader != null)
            {
                trail.material = new Material(trailShader);
            }

            Light light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.86f, 1f);
            light.intensity = 2.4f;
            light.range = 3.2f;
            light.shadows = LightShadows.None;
            return root;
        }

        private static void DisableCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        private static void SetSharedColor(GameObject target, ref Material shared, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            if (shared == null)
            {
                shared = new Material(renderer.sharedMaterial);
                shared.color = color;
                if (shared.HasProperty("_BaseColor"))
                {
                    shared.SetColor("_BaseColor", color);
                }
            }

            renderer.sharedMaterial = shared;
        }
    }
}
