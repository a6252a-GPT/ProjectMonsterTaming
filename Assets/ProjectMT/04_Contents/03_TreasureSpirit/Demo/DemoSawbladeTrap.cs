using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    /// <summary>
    /// Traphome_Mid 안에서 톱날을 회전·왕복시키고, 군단장/팔로워에게 접촉 피해를 줍니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoSawbladeTrap : MonoBehaviour
    {
        [SerializeField] private Transform home;
        [SerializeField] private float damage = 25f;
        [SerializeField] private float hitCooldown = 0.7f;
        [SerializeField] private float moveSpeed = 2.2f;
        [SerializeField] private float spinDegreesPerSecond = 540f;
        [SerializeField] private float endPadding = 0.35f;

        private Vector3 startPoint;
        private Vector3 endPoint;
        private Vector3 localSpinAxis = Vector3.forward;
        private float travelLength;
        private float elapsed;
        private readonly Dictionary<int, float> nextHitTime = new Dictionary<int, float>();
        private readonly Collider[] overlapHits = new Collider[16];

        public void Initialize(Transform trapHome, bool alignDiscToTravel = false)
        {
            home = trapHome;
            EnsureHitVolume();
            CacheTravelPath();
            CacheLocalSpinAxis();
        }

        private void Awake()
        {
            EnsureHitVolume();
        }

        private void Start()
        {
            if (home != null)
            {
                CacheTravelPath();
            }
        }

        private void Update()
        {
            if (travelLength <= 0.001f)
            {
                return;
            }

            elapsed += Time.deltaTime * moveSpeed;
            float t = Mathf.PingPong(elapsed / travelLength, 1f);
            transform.position = Vector3.Lerp(startPoint, endPoint, t);
            transform.Rotate(localSpinAxis, spinDegreesPerSecond * Time.deltaTime, Space.Self);
            DetectHits();
        }

        private void DetectHits()
        {
            if (!TryGetBladeBounds(out Bounds bounds))
            {
                return;
            }

            int hitCount = Physics.OverlapBoxNonAlloc(
                bounds.center,
                bounds.extents,
                overlapHits,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                TryHit(overlapHits[i], bounds);
            }
        }

        private void TryHit(Collider other, Bounds bladeBounds)
        {
            if (other == null || other.transform == transform || other.transform.IsChildOf(transform))
            {
                return;
            }

            PlayerCharacterController player = other.GetComponentInParent<PlayerCharacterController>();
            FollowerAI follower = player == null ? other.GetComponentInParent<FollowerAI>() : null;
            Transform body = player != null ? player.transform : (follower != null ? follower.transform : null);
            if (body == null || !IsTouchingBlade(body.position, bladeBounds))
            {
                return;
            }

            int targetId = body.GetInstanceID();
            if (nextHitTime.TryGetValue(targetId, out float readyAt) && Time.time < readyAt)
            {
                return;
            }

            nextHitTime[targetId] = Time.time + hitCooldown;
            if (player != null)
            {
                player.TakeDamage(damage, transform.position);
                return;
            }

            follower.TakeDamage(damage);
        }

        private bool IsTouchingBlade(Vector3 bodyPosition, Bounds bladeBounds)
        {
            Vector3 extents = bladeBounds.extents;
            float radius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)) * 0.9f;
            float halfThickness = Mathf.Min(extents.x, Mathf.Min(extents.y, extents.z)) + 0.03f;
            Vector3 worldAxis = transform.TransformDirection(localSpinAxis).normalized;

            for (int sample = 0; sample < 3; sample++)
            {
                Vector3 point = bodyPosition + Vector3.up * (sample * 0.55f);
                Vector3 delta = point - bladeBounds.center;
                float alongAxis = Vector3.Dot(delta, worldAxis);
                Vector3 onPlane = delta - worldAxis * alongAxis;
                if (Mathf.Abs(alongAxis) <= halfThickness && onPlane.magnitude <= radius)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetBladeBounds(out Bounds bounds)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            bounds = new Bounds(transform.position, Vector3.zero);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private void CacheTravelPath()
        {
            if (home == null)
            {
                return;
            }

            Bounds bounds = GetHomeBounds(home);
            GetLocalXRange(home, bounds, out float minX, out float maxX);
            minX += endPadding;
            maxX -= endPadding;
            if (maxX - minX < 0.3f)
            {
                float centerX = (minX + maxX) * 0.5f;
                minX = centerX - 0.15f;
                maxX = centerX + 0.15f;
            }

            Vector3 localCenter = home.InverseTransformPoint(bounds.center);
            localCenter.y = home.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y + 0.32f, bounds.center.z)).y;
            startPoint = home.TransformPoint(new Vector3(minX, localCenter.y, localCenter.z));
            endPoint = home.TransformPoint(new Vector3(maxX, localCenter.y, localCenter.z));
            travelLength = Vector3.Distance(startPoint, endPoint);
            transform.position = startPoint;
        }

        private void CacheLocalSpinAxis()
        {
            Vector3 meshSize = Vector3.one;
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = GetComponentInChildren<MeshFilter>();
            }

            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                meshSize = meshFilter.sharedMesh.bounds.size;
            }
            else
            {
                meshSize = transform.localScale;
            }

            localSpinAxis = SmallestLocalAxis(meshSize);
        }

        private static Vector3 SmallestLocalAxis(Vector3 size)
        {
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            if (size.x <= size.y && size.x <= size.z)
            {
                return Vector3.right;
            }

            if (size.y <= size.z)
            {
                return Vector3.up;
            }

            return Vector3.forward;
        }

        private void EnsureHitVolume()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }

            Rigidbody rigidbody = GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                rigidbody = gameObject.AddComponent<Rigidbody>();
            }

            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private static void GetLocalXRange(Transform trapHome, Bounds worldBounds, out float minX, out float maxX)
        {
            Vector3 extents = worldBounds.extents;
            Vector3 center = worldBounds.center;
            minX = float.MaxValue;
            maxX = float.MinValue;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + new Vector3(extents.x * x, extents.y * y, extents.z * z);
                        float localX = trapHome.InverseTransformPoint(corner).x;
                        if (localX < minX)
                        {
                            minX = localX;
                        }

                        if (localX > maxX)
                        {
                            maxX = localX;
                        }
                    }
                }
            }
        }

        private static Bounds GetHomeBounds(Transform trapHome)
        {
            Renderer[] renderers = trapHome.GetComponentsInChildren<Renderer>(true);
            bool initialized = false;
            Bounds bounds = new Bounds(trapHome.position, Vector3.one);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || IsSawblade(renderer.transform))
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (initialized)
            {
                return bounds;
            }

            Collider homeCollider = trapHome.GetComponent<Collider>();
            return homeCollider != null
                ? homeCollider.bounds
                : new Bounds(trapHome.position, new Vector3(4f, 1f, 2f));
        }

        private static bool IsSawblade(Transform target)
        {
            Transform current = target;
            while (current != null)
            {
                if (current.name.IndexOf("saw", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }
    }
}
