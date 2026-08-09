using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Runtime Asset Set", fileName = "MR_Monster")]
    public sealed class MonsterRuntimeAssetSet : ScriptableObject // 정식 Monster 실행 자산 묶음
    {
        [SerializeField] private GameObject visualAdapterPrefab;
        [SerializeField] private RuntimeAnimatorController animatorController;
        [SerializeField] private MonsterBodyProfile bodyProfile;
        [SerializeField] private MonsterMotionProfile motionProfile;
        [SerializeField] private MonsterCombatProfile combatProfile;
        [SerializeField] private MonsterAscensionProfile ascensionProfile;
        [SerializeField] private MonsterFeedbackProfile feedbackProfile;

        public GameObject VisualAdapterPrefab => visualAdapterPrefab;
        public RuntimeAnimatorController AnimatorController => animatorController;
        public MonsterBodyProfile BodyProfile => bodyProfile;
        public MonsterMotionProfile MotionProfile => motionProfile;
        public MonsterCombatProfile CombatProfile => combatProfile;
        public MonsterAscensionProfile AscensionProfile => ascensionProfile;
        public MonsterFeedbackProfile FeedbackProfile => feedbackProfile;

        public bool TryValidate(out string error)
        {
            if (visualAdapterPrefab == null || animatorController == null || bodyProfile == null ||
                motionProfile == null || combatProfile == null || ascensionProfile == null ||
                feedbackProfile == null)
            {
                error = $"Monster Runtime Asset Set has a missing required reference. AssetSet={name}";
                return false;
            }

            if (visualAdapterPrefab.GetComponent<UnitActor>() == null ||
                visualAdapterPrefab.GetComponent<MonsterAnimationDriver>() == null)
            {
                error = $"Visual Adapter Root requires UnitActor and MonsterAnimationDriver. AssetSet={name}";
                return false;
            }

            var animatorRoot = string.IsNullOrWhiteSpace(bodyProfile.AnimatorPath)
                ? visualAdapterPrefab.transform
                : visualAdapterPrefab.transform.Find(bodyProfile.AnimatorPath);
            if (animatorRoot == null || animatorRoot.GetComponent<Animator>() == null)
            {
                error = $"Visual Adapter Animator path is invalid. AssetSet={name}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(bodyProfile.AttackOriginPath) ||
                visualAdapterPrefab.transform.Find(bodyProfile.AttackOriginPath) == null ||
                string.IsNullOrWhiteSpace(bodyProfile.HitCenterPath) ||
                visualAdapterPrefab.transform.Find(bodyProfile.HitCenterPath) == null)
            {
                error = $"Visual Adapter AttackOrigin or HitCenter path is invalid. AssetSet={name}";
                return false;
            }

            if (!bodyProfile.TryValidate(out error) ||
                !motionProfile.TryValidate(out error) ||
                !combatProfile.TryValidate(out error) ||
                !ascensionProfile.TryValidate(out error) ||
                !feedbackProfile.TryValidate(out error))
            {
                return false;
            }

            var attacks = motionProfile.Attacks;
            for (var attackIndex = 0; attackIndex < attacks.Length; attackIndex++)
            {
                var markers = attacks[attackIndex].Markers;
                for (var markerIndex = 0; markerIndex < markers.Length; markerIndex++)
                {
                    var path = markers[markerIndex].SocketOverride;
                    if (!string.IsNullOrWhiteSpace(path) && visualAdapterPrefab.transform.Find(path) == null)
                    {
                        error = $"Monster Attack marker socket path is invalid. " +
                                $"Motion={attacks[attackIndex].MotionId}, Marker={markerIndex}, Path={path}";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            GameObject adapterPrefab,
            RuntimeAnimatorController controller,
            MonsterBodyProfile body,
            MonsterMotionProfile motion,
            MonsterCombatProfile combat,
            MonsterAscensionProfile ascension,
            MonsterFeedbackProfile feedback)
        {
            visualAdapterPrefab = adapterPrefab;
            animatorController = controller;
            bodyProfile = body;
            motionProfile = motion;
            combatProfile = combat;
            ascensionProfile = ascension;
            feedbackProfile = feedback;
        }
#endif
    }
}
