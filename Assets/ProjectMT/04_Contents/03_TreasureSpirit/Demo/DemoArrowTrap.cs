using System.Collections.Generic;
using UnityEngine;
using ProjectMT.Contents.TreasureSpirit;

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
                Fire(GetBurstSpawnPosition());
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

        private void Fire(Vector3 spawnPosition)
        {
            GameObject arrowObject = DemoArrowProjectile.CreateVisual(spawnPosition, transform.rotation);
            DemoArrowProjectile projectile = arrowObject.AddComponent<DemoArrowProjectile>();
            projectile.Launch(fireDirection, arrowSpeed, maxDistance, damage);
        }
    }

    [DisallowMultipleComponent]
    public sealed class DemoArrowProjectile : MonoBehaviour
    {
        private Vector3 velocity;
        private float maxDistance;
        private float traveled;
        private float damage;
        private readonly Collider[] overlapHits = new Collider[12];
        private readonly Dictionary<int, float> nextHitTime = new Dictionary<int, float>();

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
            SetColor(shaft, new Color(0.42f, 0.28f, 0.14f));
            DisableCollider(shaft);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 0f, 0.26f);
            head.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            head.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
            SetColor(head, new Color(0.55f, 0.55f, 0.58f));
            DisableCollider(head);

            return root;
        }

        public void Launch(Vector3 direction, float speed, float travelDistance, float hitDamage)
        {
            velocity = direction.normalized * speed;
            maxDistance = travelDistance;
            damage = hitDamage;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        private void Update()
        {
            float step = velocity.magnitude * Time.deltaTime;
            Vector3 move = velocity * Time.deltaTime;
            if (Physics.Raycast(
                    transform.position,
                    velocity.normalized,
                    out RaycastHit hit,
                    step + 0.08f,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                if (!IsCharacter(hit.collider) && hit.normal.y < 0.55f)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            transform.position += move;
            traveled += step;
            DetectHits();

            if (traveled >= maxDistance)
            {
                Destroy(gameObject);
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
                TryHit(overlapHits[i]);
            }
        }

        private void TryHit(Collider other)
        {
            if (other == null)
            {
                return;
            }

            PlayerCharacterController player = other.GetComponentInParent<PlayerCharacterController>();
            FollowerAI follower = player == null ? other.GetComponentInParent<FollowerAI>() : null;
            Transform body = player != null ? player.transform : (follower != null ? follower.transform : null);
            if (body == null)
            {
                return;
            }

            Vector3 delta = body.position + Vector3.up * 0.7f - transform.position;
            if (delta.magnitude > 0.38f)
            {
                return;
            }

            int targetId = body.GetInstanceID();
            if (nextHitTime.TryGetValue(targetId, out float readyAt) && Time.time < readyAt)
            {
                return;
            }

            nextHitTime[targetId] = Time.time + 0.4f;
            if (player != null)
            {
                player.TakeDamage(damage, transform.position);
            }
            else
            {
                follower.TakeDamage(damage);
            }

            Destroy(gameObject);
        }

        private static bool IsCharacter(Collider other)
        {
            return other != null &&
                   (other.GetComponentInParent<PlayerCharacterController>() != null ||
                    other.GetComponentInParent<FollowerAI>() != null);
        }

        private static void DisableCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        private static void SetColor(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }
    }
}
