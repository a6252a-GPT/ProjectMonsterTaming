using UnityEngine;

namespace ProjectMT.Features.Expedition
{
    public enum EnemyAppearanceGroup // 원정대 외형 강도 그룹
    {
        Peasant,
        KnightTier1,
        KnightTier2,
        KnightTier3,
        MageTier1,
        MageTier2,
        MageTier3,
        FemalePeasant,
        UpperKnightLower,
        UpperKnightMid,
        UpperKnightHigh,
        UpperKnightFinal,
        Ninja
    }

    public enum EnemyHeadwearHairMode // 머리 장비와 머리카락 조합 규칙
    {
        RegularHair,
        HatHair,
        HideHair
    }

    [CreateAssetMenu(menuName = "ProjectMT/Expedition/Enemy Appearance Profile", fileName = "EnemyAppearanceProfile")]
    public sealed class EnemyAppearanceProfile : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private EnemyAppearanceGroup group;
        [SerializeField, Min(0.1f)] private float visualScale = 0.75f;
        [SerializeField] private RuntimeAnimatorController locomotionController;

        [Header("Body")]
        [SerializeField] private GameObject[] bodyPrefabs;
        [SerializeField] private GameObject[] fairHeadPrefabs;
        [SerializeField] private GameObject[] tanHeadPrefabs;
        [SerializeField] private GameObject[] facePrefabs;
        [SerializeField] private GameObject[] regularHairPrefabs;
        [SerializeField] private GameObject[] hatHairPrefabs;

        [Header("Equipment")]
        [SerializeField] private GameObject[] headwearPrefabs;
        [SerializeField] private GameObject[] rightHandPrefabs;
        [SerializeField] private GameObject[] leftHandPrefabs;
        [SerializeField] private GameObject[] backPrefabs;
        [SerializeField] private EnemyHeadwearHairMode hairModeWithHeadwear;
        [SerializeField, Range(0f, 1f)] private float headwearChance = 1f;
        [SerializeField, Range(0f, 1f)] private float rightHandChance = 1f;
        [SerializeField, Range(0f, 1f)] private float leftHandChance = 1f;
        [SerializeField, Min(0)] private int minimumBackAttachments;
        [SerializeField, Min(0)] private int maximumBackAttachments = 1;

        public EnemyAppearanceGroup Group => group;
        public float VisualScale => Mathf.Max(0.1f, visualScale);
        public RuntimeAnimatorController LocomotionController => locomotionController;
        public GameObject[] BodyPrefabs => bodyPrefabs ?? System.Array.Empty<GameObject>();
        public GameObject[] FairHeadPrefabs => fairHeadPrefabs ?? System.Array.Empty<GameObject>();
        public GameObject[] TanHeadPrefabs => tanHeadPrefabs ?? System.Array.Empty<GameObject>();
        public GameObject[] FacePrefabs => facePrefabs ?? System.Array.Empty<GameObject>();
        public GameObject[] RegularHairPrefabs => regularHairPrefabs ?? System.Array.Empty<GameObject>();
        public GameObject[] HatHairPrefabs => hatHairPrefabs ?? System.Array.Empty<GameObject>();
        public GameObject[] HeadwearPrefabs => headwearPrefabs ?? System.Array.Empty<GameObject>();
        public GameObject[] RightHandPrefabs => rightHandPrefabs ?? System.Array.Empty<GameObject>();
        public GameObject[] LeftHandPrefabs => leftHandPrefabs ?? System.Array.Empty<GameObject>();
        public GameObject[] BackPrefabs => backPrefabs ?? System.Array.Empty<GameObject>();
        public EnemyHeadwearHairMode HairModeWithHeadwear => hairModeWithHeadwear;
        public float HeadwearChance => Mathf.Clamp01(headwearChance);
        public float RightHandChance => Mathf.Clamp01(rightHandChance);
        public float LeftHandChance => Mathf.Clamp01(leftHandChance);
        public int MinimumBackAttachments => Mathf.Clamp(minimumBackAttachments, 0, BackPrefabs.Length);
        public int MaximumBackAttachments => Mathf.Clamp(maximumBackAttachments, MinimumBackAttachments, BackPrefabs.Length);
        public bool IsConfigured => BodyPrefabs.Length > 0 &&
                                    (FairHeadPrefabs.Length > 0 || TanHeadPrefabs.Length > 0) &&
                                    FacePrefabs.Length > 0;

        private void OnValidate()
        {
            visualScale = Mathf.Max(0.1f, visualScale);
            minimumBackAttachments = Mathf.Max(0, minimumBackAttachments);
            maximumBackAttachments = Mathf.Max(minimumBackAttachments, maximumBackAttachments);
        }
    }
}
