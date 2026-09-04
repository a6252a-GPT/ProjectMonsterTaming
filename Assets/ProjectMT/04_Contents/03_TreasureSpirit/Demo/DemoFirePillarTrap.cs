using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    /// <summary>
    /// Ceiling_SquareLarge 아래 바닥에서 +Y로 Super Magic FX Fire Spray를 분사합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoFirePillarTrap : MonoBehaviour
    {
        private const string VfxPrefabGuid = "62655a68ce8728d478371a3dc863f4b2";
        private const string VfxInstanceName = "Fire Spray";
        private const float SprayThicknessX = 1.1f;
        private const float MinSprayLengthZ = 1.5f;
        private const float VfxSpacingZ = 0.75f;
        private const int MaxVfxCount = 16;

        [SerializeField] private float damage = 22f;
        [SerializeField] private float hitCooldown = 0.35f;
        [SerializeField] private float idleDuration = 1.55f;
        [SerializeField] private float warningDuration = 0.45f;
        [SerializeField] private float activeDuration = 3f;

        private static GameObject cachedVfxPrefab;
        private static Material warningMaterial;

        private Transform warningDisc;
        private ParticleSystem[] vfxParticles = System.Array.Empty<ParticleSystem>();
        private Light[] vfxLights = System.Array.Empty<Light>();
        private static readonly RaycastHit[] floorHits = new RaycastHit[16];
        private float sprayLengthZ = MinSprayLengthZ;
        private float floorY;
        private float ceilingY;
        private float elapsed;
        private int phase;
        private readonly Dictionary<int, float> nextHitTime = new Dictionary<int, float>();
        private readonly Collider[] overlapHits = new Collider[64];

        public static void Spawn(Transform parent, Transform ceiling, int staggerIndex)
        {
            if (ceiling == null)
            {
                return;
            }

            EnsureWarningMaterial();
            Vector3 center = GetCeilingCenter(ceiling);
            float sprayLengthZ = MeasureCeilingLengthZ(ceiling);
            float ceilingY = center.y;
            float floorY = FindFloorY(center, ceiling);
            GameObject root = new GameObject("FireSprayTrap");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(center.x, floorY, center.z);

            DemoFirePillarTrap trap = root.AddComponent<DemoFirePillarTrap>();
            trap.ceilingY = ceilingY;
            trap.floorY = floorY;
            trap.sprayLengthZ = sprayLengthZ;
            trap.elapsed = staggerIndex * 0.35f;
            trap.BuildVisuals();
        }

        private void BuildVisuals()
        {
            float height = Mathf.Max(1.6f, ceilingY - floorY);
            warningDisc = CreateQuad(
                "WarningEmbers",
                warningMaterial,
                new Vector3(SprayThicknessX * 1.15f, 0.04f, sprayLengthZ * 0.98f),
                new Vector3(0f, 0.04f, 0f));
            warningDisc.gameObject.SetActive(false);

            SpawnVfxInstances(height);
            SetSprayActive(false);
        }

        private void SpawnVfxInstances(float height)
        {
            GameObject prefab = LoadVfxPrefab();
            if (prefab == null)
            {
                Debug.LogWarning("[DemoFirePillarTrap] Fire Spray 프리팹을 찾지 못했습니다.");
                return;
            }

            int count = Mathf.Clamp(Mathf.CeilToInt(sprayLengthZ / VfxSpacingZ), 1, MaxVfxCount);
            float spacing = sprayLengthZ / count;
            float heightScale = Mathf.Clamp(height / 5.5f, 0.4f, 1.15f);
            // -90 X 이후 local Y가 월드 Z. 슬롯 너비만큼 늘려 가운데가 비지 않게 한다.
            float zScale = Mathf.Max(heightScale, spacing / 0.85f);
            Quaternion upward = Quaternion.Euler(-90f, 0f, 0f);

            for (int i = 0; i < count; i++)
            {
                float z = -sprayLengthZ * 0.5f + spacing * (i + 0.5f);
                GameObject instance = Object.Instantiate(prefab, transform);
                instance.name = VfxInstanceName;
                instance.transform.localPosition = new Vector3(0f, 0.08f, z);
                instance.transform.localRotation = upward;
                instance.transform.localScale = new Vector3(heightScale, zScale, heightScale);
                DisableColliders(instance);
                DemoUrpParticleRemapper.Remap(instance);
                ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                for (int s = 0; s < systems.Length; s++)
                {
                    ParticleSystem.MainModule main = systems[s].main;
                    main.playOnAwake = false;
                    systems[s].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            List<ParticleSystem> roots = new List<ParticleSystem>();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null || child.name != VfxInstanceName)
                {
                    continue;
                }

                ParticleSystem rootParticles = child.GetComponent<ParticleSystem>();
                if (rootParticles != null)
                {
                    roots.Add(rootParticles);
                }
            }

            vfxParticles = roots.ToArray();
            vfxLights = GetComponentsInChildren<Light>(true);
            for (int i = 0; i < vfxLights.Length; i++)
            {
                DemoDungeonAtmosphere.TuneLocalFireLight(vfxLights[i]);
            }

            AudioSource[] prefabAudio = GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < prefabAudio.Length; i++)
            {
                if (prefabAudio[i] != null)
                {
                    prefabAudio[i].enabled = false;
                }
            }
        }

        private void Update()
        {
            if (DemoDungeonController.IsGameplayPaused)
            {
                return;
            }

            elapsed += Time.deltaTime;
            if (phase == 0)
            {
                if (elapsed >= idleDuration)
                {
                    elapsed = 0f;
                    phase = 1;
                    if (warningDisc != null)
                    {
                        warningDisc.gameObject.SetActive(true);
                    }
                }

                return;
            }

            if (phase == 1)
            {
                PulseWarning();
                if (elapsed >= warningDuration)
                {
                    elapsed = 0f;
                    phase = 2;
                    if (warningDisc != null)
                    {
                        warningDisc.gameObject.SetActive(false);
                    }

                    SetSprayActive(true);
                    DemoDungeonAudio.PlayFireIgnite(transform.position);
                    DemoDungeonAudio.SetFireLoop(transform, true);
                }

                return;
            }

            DetectHits();
            if (elapsed >= activeDuration)
            {
                elapsed = 0f;
                phase = 0;
                SetSprayActive(false);
                DemoDungeonAudio.SetFireLoop(transform, false);
            }
        }

        private void OnDisable()
        {
            DemoDungeonAudio.SetFireLoop(transform, false);
        }

        private void PulseWarning()
        {
            if (warningDisc == null)
            {
                return;
            }

            float pulse = 0.88f + 0.12f * Mathf.Sin(elapsed * 18f);
            warningDisc.localScale = new Vector3(
                SprayThicknessX * 1.15f * pulse,
                0.04f,
                sprayLengthZ * 0.98f * pulse);
        }

        private void SetSprayActive(bool active)
        {
            for (int i = 0; i < vfxParticles.Length; i++)
            {
                ParticleSystem particles = vfxParticles[i];
                if (particles == null)
                {
                    continue;
                }

                if (active)
                {
                    particles.Play(true);
                }
                else
                {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            for (int i = 0; i < vfxLights.Length; i++)
            {
                if (vfxLights[i] != null)
                {
                    vfxLights[i].enabled = active;
                }
            }
        }

        private void DetectHits()
        {
            float range = Mathf.Max(SprayThicknessX, sprayLengthZ) * 0.5f + 1.2f;
            if (DemoCombatRoster.FindNearestAlly(transform.position, range, false) == null)
            {
                return;
            }

            float height = Mathf.Max(0.5f, ceilingY - floorY);
            Vector3 center = transform.position + Vector3.up * (height * 0.5f);
            Vector3 half = new Vector3(SprayThicknessX * 0.5f + 0.2f, height * 0.5f, sprayLengthZ * 0.5f + 0.2f);
            int hitCount = Physics.OverlapBoxNonAlloc(
                center,
                half,
                overlapHits,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                TryHit(overlapHits[i]);
            }
        }

        private void TryHit(Collider other)
        {
            if (other == null || !DemoCombatTargetUtil.TryResolveAlly(other, out Transform body))
            {
                return;
            }

            TryHitBody(body);
        }

        private void TryHitBody(Transform body)
        {
            if (body == null)
            {
                return;
            }

            Vector3 delta = body.position - transform.position;
            if (Mathf.Abs(delta.x) > SprayThicknessX * 0.5f + 0.35f ||
                Mathf.Abs(delta.z) > sprayLengthZ * 0.5f + 0.35f)
            {
                return;
            }

            if (body.position.y < floorY - 0.6f || body.position.y > ceilingY + 0.4f)
            {
                return;
            }

            if (!DemoCombatTargetUtil.TryConsumeHit(nextHitTime, body.GetInstanceID(), hitCooldown))
            {
                return;
            }

            DemoCombatTargetUtil.DamageAlly(body, damage, transform.position);
        }

        private Transform CreateQuad(string objectName, Material material, Vector3 worldScale, Vector3 localPosition)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = objectName;
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = worldScale;
            Collider collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return visual.transform;
        }

        private static void DisableColliders(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        private static GameObject LoadVfxPrefab()
        {
            if (cachedVfxPrefab != null)
            {
                return cachedVfxPrefab;
            }

#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(VfxPrefabGuid);
            if (!string.IsNullOrEmpty(path))
            {
                cachedVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
#endif
            return cachedVfxPrefab;
        }

        private static float FindFloorY(Vector3 from, Transform ceiling)
        {
            Vector3 origin = from + Vector3.down * 0.08f;
            int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, floorHits, 8f, ~0, QueryTriggerInteraction.Ignore);
            float best = from.y - 3.4f;
            bool found = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = floorHits[i];
                if (hit.collider == null || hit.normal.y < 0.45f)
                {
                    continue;
                }

                Transform hitTransform = hit.collider.transform;
                if (hitTransform == ceiling || hitTransform.IsChildOf(ceiling))
                {
                    continue;
                }

                if (!found || hit.point.y > best)
                {
                    best = hit.point.y;
                    found = true;
                }
            }

            return found ? best : from.y - 3.4f;
        }

        private static Vector3 GetCeilingCenter(Transform ceiling)
        {
            Renderer renderer = ceiling.GetComponent<Renderer>();
            if (renderer == null)
            {
                renderer = ceiling.GetComponentInChildren<Renderer>();
            }

            return renderer != null ? renderer.bounds.center : ceiling.position;
        }

        private static float MeasureCeilingLengthZ(Transform ceiling)
        {
            Renderer renderer = ceiling.GetComponent<Renderer>();
            if (renderer == null)
            {
                renderer = ceiling.GetComponentInChildren<Renderer>();
            }

            if (renderer == null)
            {
                return MinSprayLengthZ;
            }

            return Mathf.Max(MinSprayLengthZ, renderer.bounds.size.z);
        }

        private static void EnsureWarningMaterial()
        {
            if (warningMaterial != null)
            {
                return;
            }

            warningMaterial = CreateLitMaterial(
                "FireSpray_Warning",
                new Color(1f, 0.28f, 0.05f),
                new Color(2.2f, 0.3f, 0.04f));
        }

        private static Material CreateLitMaterial(string materialName, Color baseColor, Color emission)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Material material = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
            material.name = materialName;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", baseColor);
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }

            return material;
        }
    }
}
