using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    /// <summary>
    /// Sewer_Square에서 2초 연사 후 2초 휴식으로 화살을 발사합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoArrowTrap : MonoBehaviour
    {
        [SerializeField] private float arrowSpeed = 16f;
        [SerializeField] private float maxDistance = 36f;
        [SerializeField] private float damage = 20f;
        [SerializeField] private float burstDuration = 2f;
        [SerializeField] private float restDuration = 2f;
        [SerializeField] private int arrowsPerBurst = 28;
        [SerializeField] private float burstSpread = 0.55f;

        private Vector3 fireDirection = Vector3.forward;
        private float elapsed;
        private bool resting;
        private int firedInBurst;

        public void InitializeBurst(Vector3 worldDirection, float travelDistance)
        {
            fireDirection = worldDirection.sqrMagnitude > 0.001f
                ? worldDirection.normalized
                : transform.forward;
            fireDirection.y = 0f;
            if (fireDirection.sqrMagnitude < 0.001f)
            {
                fireDirection = Vector3.forward;
            }

            fireDirection.Normalize();
            maxDistance = Mathf.Max(16f, travelDistance);
            transform.rotation = Quaternion.LookRotation(fireDirection, Vector3.up);
            resting = false;
            elapsed = 0f;
            firedInBurst = 0;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (resting)
            {
                if (elapsed >= restDuration)
                {
                    resting = false;
                    elapsed = 0f;
                    firedInBurst = 0;
                }

                return;
            }

            float shotInterval = burstDuration / Mathf.Max(1, arrowsPerBurst);
            while (firedInBurst < arrowsPerBurst && elapsed >= firedInBurst * shotInterval)
            {
                Vector3 spawnPosition = GetBurstSpawnPosition();
                if ((firedInBurst % 2) == 0)
                {
                    DemoDungeonAudio.PlayArrow(spawnPosition);
                }

                DemoArrowProjectile.LaunchFromPool(spawnPosition, fireDirection, arrowSpeed, maxDistance, damage);
                firedInBurst++;
            }

            if (firedInBurst >= arrowsPerBurst && elapsed >= burstDuration)
            {
                resting = true;
                elapsed = 0f;
                firedInBurst = 0;
            }
        }

        private Vector3 GetBurstSpawnPosition()
        {
            Vector3 right = Vector3.Cross(Vector3.up, fireDirection);
            if (right.sqrMagnitude < 0.001f)
            {
                right = Vector3.right;
            }

            right.Normalize();
            return transform.position
                   + right * Random.Range(-burstSpread, burstSpread)
                   + Vector3.up * Random.Range(-0.12f, 0.18f);
        }
    }

    [DisallowMultipleComponent]
    public sealed class DemoArrowProjectile : MonoBehaviour
    {
        private const int PoolCapacity = 32;

        private static readonly Stack<DemoArrowProjectile> pool = new Stack<DemoArrowProjectile>(PoolCapacity);
        private static readonly List<DemoArrowProjectile> live = new List<DemoArrowProjectile>(PoolCapacity);
        private static Material shaftMaterial;
        private static Material headMaterial;
        private static Transform poolRoot;

        private Vector3 velocity;
        private Vector3 launchDirection;
        private float travelSpeed;
        private float maxDistance;
        private float traveled;
        private float damage;
        private readonly Collider[] overlapHits = new Collider[12];
        private readonly Dictionary<int, float> nextHitTime = new Dictionary<int, float>();

        public static void LaunchFromPool(Vector3 position, Vector3 direction, float speed, float travelDistance, float hitDamage)
        {
            DemoArrowProjectile projectile = Rent(position, Quaternion.LookRotation(direction, Vector3.up));
            projectile.Launch(direction, speed, travelDistance, hitDamage);
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

        private static DemoArrowProjectile Rent(Vector3 position, Quaternion rotation)
        {
            DemoArrowProjectile projectile = null;
            while (pool.Count > 0 && projectile == null)
            {
                projectile = pool.Pop();
            }

            if (projectile == null)
            {
                GameObject root = CreateVisual(position, rotation);
                root.transform.SetParent(PoolRoot, true);
                projectile = root.AddComponent<DemoArrowProjectile>();
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
                    GameObject root = new GameObject("DemoArrowPool");
                    Object.DontDestroyOnLoad(root);
                    poolRoot = root.transform;
                }

                return poolRoot;
            }
        }

        public static GameObject CreateVisual(Transform emitter)
        {
            return CreateVisual(emitter.position, emitter.rotation);
        }

        public static GameObject CreateVisual(Vector3 position, Quaternion rotation)
        {
            GameObject root = new GameObject("Arrow");
            root.transform.SetPositionAndRotation(position, rotation);

            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Shaft";
            shaft.transform.SetParent(root.transform, false);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shaft.transform.localScale = new Vector3(0.045f, 0.2f, 0.045f);
            SetSharedColor(shaft, ref shaftMaterial, new Color(0.42f, 0.28f, 0.14f));
            DisableCollider(shaft);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 0f, 0.26f);
            head.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            head.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
            SetSharedColor(head, ref headMaterial, new Color(0.55f, 0.55f, 0.58f));
            DisableCollider(head);

            return root;
        }

        public void Launch(Vector3 direction, float speed, float travelDistance, float hitDamage)
        {
            launchDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            travelSpeed = speed;
            velocity = launchDirection * travelSpeed;
            maxDistance = travelDistance;
            damage = hitDamage;
            traveled = 0f;
            nextHitTime.Clear();
            transform.rotation = Quaternion.LookRotation(launchDirection, Vector3.up);
        }

        private void Update()
        {
            float step = travelSpeed * Time.deltaTime;
            Vector3 move = velocity * Time.deltaTime;
            if (Physics.Raycast(
                    transform.position,
                    launchDirection,
                    out RaycastHit hit,
                    step + 0.08f,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                if (!DemoCombatTargetUtil.TryResolveAlly(hit.collider, out _) && hit.normal.y < 0.55f)
                {
                    Recycle();
                    return;
                }
            }

            transform.position += move;
            traveled += step;
            DetectHits();

            if (traveled >= maxDistance)
            {
                Recycle();
            }
        }

        private void DetectHits()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                0.16f,
                overlapHits,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                if (TryHit(overlapHits[i]))
                {
                    return;
                }
            }
        }

        private bool TryHit(Collider other)
        {
            if (other == null || !DemoCombatTargetUtil.TryResolveAlly(other, out Transform body))
            {
                return false;
            }

            Vector3 delta = body.position + Vector3.up * 0.7f - transform.position;
            if (delta.sqrMagnitude > 0.38f * 0.38f)
            {
                return false;
            }

            if (!DemoCombatTargetUtil.TryConsumeHit(nextHitTime, body.GetInstanceID(), 0.4f))
            {
                return false;
            }

            DemoCombatTargetUtil.DamageAlly(body, damage, transform.position);
            Recycle();
            return true;
        }

        private void Recycle()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            live.Remove(this);
            nextHitTime.Clear();
            traveled = 0f;
            gameObject.SetActive(false);
            transform.SetParent(PoolRoot, false);
            pool.Push(this);
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
