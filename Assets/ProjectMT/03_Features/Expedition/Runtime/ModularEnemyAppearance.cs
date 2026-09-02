using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.Expedition
{
    [DisallowMultipleComponent]
    public sealed class ModularEnemyAppearance : MonoBehaviour, IUnitSpawnPreparation, IUnitCombatAnimation // Vendor 파츠 조립·레거시 전투 동작
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private const string IdleState = "Idle";
        private const string MoveState = "Move";
        private const string StunState = "Stun";
        private static readonly string[] RightHandSocketNames = { "+ R Hand", "+R Hand" };
        private static readonly string[] UpperKnightEquipmentPalettes = { "Blue", "Green", "Purple", "Red" };

        [SerializeField] private EnemyAppearanceProfile profile;
        [SerializeField] private Transform visualRoot;

        private readonly List<GameObject> attachments = new List<GameObject>();
        private GameObject bodyInstance;
        private Animator animator;
        private UnitActor actor;
        private System.Random animationRandom;
        private float transientAnimationRemaining;
        private bool deathPlaying;
        private Vector3 lastPosition;

        public EnemyAppearanceProfile Profile => profile;
        public string CurrentBodyName => bodyInstance == null ? string.Empty : bodyInstance.name;
        public int CurrentAttachmentCount => attachments.Count(item => item != null);
        public string CurrentAnimationState { get; private set; } = string.Empty;

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
            animationRandom = new System.Random(seed ^ 0x4D54414E);
            actor = GetComponent<UnitActor>();
            var bodyPrefab = Pick(profile.BodyPrefabs, random);
            if (bodyPrefab == null)
            {
                return false;
            }

            bodyInstance = InstantiatePart(bodyPrefab, visualRoot);
            bodyInstance.transform.localScale = Vector3.one * profile.VisualScale * request.VisualScaleMultiplier;
            var skinToken = FirstToken(bodyPrefab.name);
            var paletteToken = ResolveEquipmentPalette(profile.Group, SecondToken(bodyPrefab.name), random);
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
                if (animator.gameObject.activeInHierarchy)
                {
                    animator.Update(0f); // 비활성 Stage 조립 중에는 강제 평가하지 않는다
                }

                PlayState(IdleState, 0f);
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
            lastPosition = currentPosition;

            if (deathPlaying)
            {
                animator.SetFloat(SpeedId, 0f);
                return;
            }

            if (actor != null && actor.IsActiveStunned)
            {
                if (!string.Equals(CurrentAnimationState, StunState, StringComparison.Ordinal))
                {
                    PlayState(StunState, actor.IsActiveStunned ? transientAnimationRemaining : 0f);
                }

                transientAnimationRemaining = Mathf.Max(0f, transientAnimationRemaining - Time.deltaTime);
                animator.SetFloat(SpeedId, 0f);
                return;
            }

            transientAnimationRemaining = Mathf.Max(0f, transientAnimationRemaining - Time.deltaTime);
            if (transientAnimationRemaining > 0f)
            {
                animator.SetFloat(SpeedId, 0f);
                return;
            }

            animator.SetFloat(SpeedId, speed, 0.08f, Time.deltaTime);
            var locomotionState = speed > 0.05f ? MoveState : IdleState;
            if (!string.Equals(CurrentAnimationState, locomotionState, StringComparison.Ordinal))
            {
                PlayState(locomotionState, 0f);
            }
        }

        public void PlayAttack()
        {
            if (deathPlaying)
            {
                return;
            }

            if (IsMage(profile.Group))
            {
                PlayState("Attack_Mage", 2.53f);
                return;
            }

            var attackIndex = profile.Group == EnemyAppearanceGroup.Ninja
                ? 6
                : Next(1, 7);
            PlayState($"Attack_{attackIndex:00}", ResolveMeleeAttackDuration(attackIndex));
        }

        public void PlayHit()
        {
            if (deathPlaying)
            {
                return;
            }

            var hitIndex = Next(1, 3);
            var duration = IsMage(profile.Group)
                ? (hitIndex == 1 ? 0.43f : 0.47f)
                : (hitIndex == 1 ? 0.67f : 0.87f);
            PlayState($"Damage_{hitIndex:00}", duration);
        }

        public void PlayStun(float duration)
        {
            if (deathPlaying)
            {
                return;
            }

            PlayState(StunState, Mathf.Max(0.01f, duration));
        }

        public float PlayDeath()
        {
            if (IsMage(profile.Group))
            {
                deathPlaying = true;
                PlayState("Death_01", 2.53f);
                return 2.53f;
            }

            var deathIndex = Next(1, 5);
            var duration = deathIndex switch
            {
                1 => 3.17f,
                2 => 3.17f,
                3 => 3.33f,
                _ => 2.83f
            };
            deathPlaying = true;
            PlayState($"Death_{deathIndex:00}", duration);
            return duration;
        }

        private void PlayState(string stateName, float duration)
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            var stateHash = Animator.StringToHash($"Base Layer.{stateName}");
            if (!animator.HasState(0, stateHash))
            {
                Debug.LogWarning($"Enemy animator state is missing: {stateName}", this);
                return;
            }

            animator.CrossFadeInFixedTime(stateHash, 0.06f, 0, 0f);
            CurrentAnimationState = stateName;
            transientAnimationRemaining = duration;
        }

        private int Next(int minimumInclusive, int maximumExclusive)
        {
            animationRandom ??= new System.Random(StableHash(gameObject.name));
            return animationRandom.Next(minimumInclusive, maximumExclusive);
        }

        private static bool IsMage(EnemyAppearanceGroup group)
        {
            return group == EnemyAppearanceGroup.MageTier1 ||
                   group == EnemyAppearanceGroup.MageTier2 ||
                   group == EnemyAppearanceGroup.MageTier3;
        }

        private static float ResolveMeleeAttackDuration(int attackIndex)
        {
            return attackIndex switch
            {
                1 => 1.33f,
                2 => 0.73f,
                3 => 0.77f,
                4 => 1.10f,
                5 => 1.27f,
                _ => 1.13f
            };
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
            actor = null;
            animationRandom = null;
            transientAnimationRemaining = 0f;
            deathPlaying = false;
            CurrentAnimationState = string.Empty;
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

        private static string ResolveEquipmentPalette(
            EnemyAppearanceGroup group,
            string bodyPalette,
            System.Random random)
        {
            if (group != EnemyAppearanceGroup.UpperKnightLower &&
                group != EnemyAppearanceGroup.UpperKnightMid &&
                group != EnemyAppearanceGroup.UpperKnightHigh &&
                group != EnemyAppearanceGroup.UpperKnightFinal)
            {
                return bodyPalette;
            }

            return bodyPalette switch
            {
                "Aqua" => "Blue",
                "Nature" => "Green",
                "Dark" => "Purple",
                "Fire" => "Red",
                _ => UpperKnightEquipmentPalettes[random.Next(UpperKnightEquipmentPalettes.Length)]
            };
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
