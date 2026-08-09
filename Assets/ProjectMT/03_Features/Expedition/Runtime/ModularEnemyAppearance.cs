using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.Expedition
{
    [DisallowMultipleComponent]
    public sealed class ModularEnemyAppearance : MonoBehaviour, IUnitSpawnPreparation // Vendor 파츠를 ProjectMT 적으로 조립
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly string[] RightHandSocketNames = { "+ R Hand", "+R Hand" };

        [SerializeField] private EnemyAppearanceProfile profile;
        [SerializeField] private Transform visualRoot;

        private readonly List<GameObject> attachments = new List<GameObject>();
        private GameObject bodyInstance;
        private Animator animator;
        private Vector3 lastPosition;

        public EnemyAppearanceProfile Profile => profile;
        public string CurrentBodyName => bodyInstance == null ? string.Empty : bodyInstance.name;
        public int CurrentAttachmentCount => attachments.Count(item => item != null);

        public bool PrepareForSpawn(UnitSpawnRequest request)
        {
            if (profile == null || !profile.IsConfigured)
            {
                Debug.LogError("Enemy appearance profile is missing or incomplete.", this);
                return false;
            }

            if (visualRoot == null)
            {
                visualRoot = transform.Find("VisualRoot");
            }

            if (visualRoot == null)
            {
                Debug.LogError("Modular enemy has no VisualRoot.", this);
                return false;
            }

            ClearCurrentAppearance();
            var seed = request.AppearanceSeed == 0 ? StableHash(request.UnitId) : request.AppearanceSeed;
            var random = new System.Random(seed);
            var bodyPrefab = Pick(profile.BodyPrefabs, random);
            if (bodyPrefab == null)
            {
                return false;
            }

            bodyInstance = InstantiatePart(bodyPrefab, visualRoot);
            bodyInstance.transform.localScale = Vector3.one * profile.VisualScale;
            var skinToken = FirstToken(bodyPrefab.name);
            var paletteToken = SecondToken(bodyPrefab.name);
            var headSocket = FindDescendant(bodyInstance.transform, "+ Head");
            var rightHandSocket = FindDescendant(bodyInstance.transform, RightHandSocketNames);
            var leftHandSocket = FindDescendant(bodyInstance.transform, "+ L Hand");
            var backSocket = FindDescendant(bodyInstance.transform, "+ Back");
            if (headSocket == null || rightHandSocket == null || leftHandSocket == null || backSocket == null)
            {
                Debug.LogError($"Enemy body has incomplete sockets: {bodyPrefab.name}", this);
                ClearCurrentAppearance();
                return false;
            }

            var heads = string.Equals(skinToken, "Tan", StringComparison.OrdinalIgnoreCase)
                ? profile.TanHeadPrefabs
                : profile.FairHeadPrefabs;
            AddPart(Pick(heads, random), headSocket);

            var face = Pick(profile.FacePrefabs, random);
            AddPart(face, headSocket);
            var colorToken = LastToken(face == null ? string.Empty : face.name);

            var headwear = Roll(profile.HeadwearChance, profile.HeadwearPrefabs, random)
                ? PickForPalette(profile.HeadwearPrefabs, paletteToken, random)
                : null;
            AddPart(headwear, headSocket);

            var hairPool = profile.RegularHairPrefabs;
            if (headwear != null)
            {
                hairPool = profile.HairModeWithHeadwear switch
                {
                    EnemyHeadwearHairMode.HatHair => profile.HatHairPrefabs,
                    EnemyHeadwearHairMode.HideHair => Array.Empty<GameObject>(),
                    _ => profile.RegularHairPrefabs
                };
            }

            AddPart(PickForTrailingToken(hairPool, colorToken, random), headSocket);
            if (Roll(profile.RightHandChance, profile.RightHandPrefabs, random))
            {
                AddPart(PickForPalette(profile.RightHandPrefabs, paletteToken, random), rightHandSocket);
            }

            if (Roll(profile.LeftHandChance, profile.LeftHandPrefabs, random))
            {
                AddPart(PickForPalette(profile.LeftHandPrefabs, paletteToken, random), leftHandSocket);
            }

            AddBackParts(backSocket, paletteToken, random);
            animator = bodyInstance.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = profile.LocomotionController;
                animator.Rebind();
                animator.Update(0f);
            }

            GetComponent<UnitVisualFeedback>()?.RefreshRenderers();
            lastPosition = transform.position;
            return true;
        }

        private void Update()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            var currentPosition = transform.position;
            var movement = currentPosition - lastPosition;
            movement.y = 0f;
            var speed = Time.deltaTime <= 0f ? 0f : movement.magnitude / Time.deltaTime;
            animator.SetFloat(SpeedId, speed, 0.08f, Time.deltaTime);
            lastPosition = currentPosition;
        }

        private void AddBackParts(Transform socket, string paletteToken, System.Random random)
        {
            if (profile.BackPrefabs.Length == 0 || profile.MaximumBackAttachments == 0)
            {
                return;
            }

            var candidates = FilterForPalette(profile.BackPrefabs, paletteToken).ToList();
            var count = random.Next(profile.MinimumBackAttachments, profile.MaximumBackAttachments + 1);
            count = Mathf.Min(count, candidates.Count);
            for (var index = 0; index < count; index++)
            {
                var selectedIndex = random.Next(candidates.Count);
                AddPart(candidates[selectedIndex], socket);
                candidates.RemoveAt(selectedIndex);
            }
        }

        private void AddPart(GameObject prefab, Transform parent)
        {
            if (prefab == null || parent == null)
            {
                return;
            }

            attachments.Add(InstantiatePart(prefab, parent));
        }

        private static GameObject InstantiatePart(GameObject prefab, Transform parent)
        {
            var instance = Instantiate(prefab, parent, false);
            instance.name = prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private void ClearCurrentAppearance()
        {
            attachments.Clear();
            animator = null;
            for (var index = visualRoot == null ? -1 : visualRoot.childCount - 1; index >= 0; index--)
            {
                var child = visualRoot.GetChild(index).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }

            bodyInstance = null;
        }

        private static bool Roll(float chance, GameObject[] candidates, System.Random random)
        {
            return candidates != null && candidates.Length > 0 && random.NextDouble() <= Mathf.Clamp01(chance);
        }

        private static GameObject Pick(GameObject[] candidates, System.Random random)
        {
            if (candidates == null || candidates.Length == 0)
            {
                return null;
            }

            return candidates[random.Next(candidates.Length)];
        }

        private static GameObject PickForPalette(GameObject[] candidates, string paletteToken, System.Random random)
        {
            return Pick(FilterForPalette(candidates, paletteToken).ToArray(), random);
        }

        private static IEnumerable<GameObject> FilterForPalette(GameObject[] candidates, string paletteToken)
        {
            if (candidates == null || candidates.Length == 0)
            {
                return Array.Empty<GameObject>();
            }

            var matched = candidates.Where(item => item != null && PaletteMatches(item.name, paletteToken)).ToArray();
            return matched.Length > 0 ? matched : candidates.Where(item => item != null);
        }

        private static GameObject PickForTrailingToken(GameObject[] candidates, string token, System.Random random)
        {
            if (candidates == null || candidates.Length == 0)
            {
                return null;
            }

            var matched = candidates.Where(item => item != null &&
                                                   string.Equals(LastToken(item.name), token, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return Pick(matched.Length > 0 ? matched : candidates, random);
        }

        private static bool PaletteMatches(string name, string paletteToken)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(paletteToken))
            {
                return false;
            }

            if (name.IndexOf(paletteToken, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return string.Equals(paletteToken, "Nature", StringComparison.OrdinalIgnoreCase) &&
                   name.IndexOf("Green", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Transform FindDescendant(Transform root, params string[] names)
        {
            if (root == null)
            {
                return null;
            }

            for (var nameIndex = 0; nameIndex < names.Length; nameIndex++)
            {
                if (string.Equals(root.name, names[nameIndex], StringComparison.Ordinal))
                {
                    return root;
                }
            }

            for (var childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                var result = FindDescendant(root.GetChild(childIndex), names);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                var hash = (int)2166136261;
                foreach (var character in value ?? string.Empty)
                {
                    hash = (hash ^ character) * 16777619;
                }

                return hash == 0 ? 1 : hash;
            }
        }

        private static string FirstToken(string value)
        {
            return Tokens(value).FirstOrDefault() ?? string.Empty;
        }

        private static string SecondToken(string value)
        {
            return Tokens(value).Skip(1).FirstOrDefault() ?? string.Empty;
        }

        private static string LastToken(string value)
        {
            return Tokens(value).LastOrDefault() ?? string.Empty;
        }

        private static string[] Tokens(string value)
        {
            return (value ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
