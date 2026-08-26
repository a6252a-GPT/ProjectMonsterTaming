using System;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    public enum MainBattleMonsterRole // 1차 편성의 화면상 전투 역할
    {
        Vanguard,
        Guardian,
        Finisher,
        Marksman,
        BacklineHunter
    }

    [Serializable]
    public sealed class MainBattleAIProfile
    {
        [SerializeField] private string monsterId;
        [SerializeField] private MainBattleMonsterRole role;
        [SerializeField] private UnitTargetPriority targetPriority;
        [SerializeField, Range(0.2f, 1f)] private float preferredRangeRatio = 1f;
        [SerializeField, Range(0f, 0.95f)] private float retreatRangeRatio;
        [SerializeField, Range(0.08f, 1f)] private float retargetInterval = 0.2f;

        public string MonsterId => monsterId?.Trim() ?? string.Empty;
        public MainBattleMonsterRole Role => role;
        public UnitTargetPriority TargetPriority => targetPriority;
        public float PreferredRangeRatio => Mathf.Clamp(preferredRangeRatio, 0.2f, 1f);
        public float RetreatRangeRatio => Mathf.Clamp(retreatRangeRatio, 0f, PreferredRangeRatio - 0.05f);
        public float RetargetInterval => Mathf.Clamp(retargetInterval, 0.08f, 1f);

        public UnitCombatBehavior CreateBehavior()
        {
            return new UnitCombatBehavior(
                TargetPriority,
                PreferredRangeRatio,
                RetreatRangeRatio,
                RetargetInterval,
                ResolveTargetLoadPenalty(Role));
        }

        private static float ResolveTargetLoadPenalty(MainBattleMonsterRole monsterRole)
        {
            return monsterRole switch
            {
                MainBattleMonsterRole.Guardian => 1.2f,
                MainBattleMonsterRole.Vanguard => 0.9f,
                MainBattleMonsterRole.Marksman => 0.8f,
                MainBattleMonsterRole.BacklineHunter => 0.45f,
                _ => 0.25f
            };
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(MonsterId))
            {
                error = "MainBattle AI Profile has no monster ID.";
                return false;
            }

            if (RetreatRangeRatio >= PreferredRangeRatio)
            {
                error = $"MainBattle AI retreat range must be shorter than preferred range. Monster={MonsterId}";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public static MainBattleAIProfile EditorCreate(
            string id,
            MainBattleMonsterRole monsterRole,
            UnitTargetPriority priority,
            float preferredRange,
            float retreatRange,
            float retargetSeconds)
        {
            return new MainBattleAIProfile
            {
                monsterId = id?.Trim(),
                role = monsterRole,
                targetPriority = priority,
                preferredRangeRatio = Mathf.Clamp(preferredRange, 0.2f, 1f),
                retreatRangeRatio = Mathf.Clamp(retreatRange, 0f, 0.95f),
                retargetInterval = Mathf.Clamp(retargetSeconds, 0.08f, 1f)
            };
        }
#endif
    }

    [CreateAssetMenu(
        menuName = "ProjectMT/MainBattle/AI Profile Catalog",
        fileName = "MainBattleAIProfileCatalog")]
    public sealed class MainBattleAIProfileCatalog : ScriptableObject
    {
        public const string ResourceName = "MainBattleAIProfileCatalog";

        [SerializeField] private MainBattleAIProfile[] profiles = Array.Empty<MainBattleAIProfile>();

        public IReadOnlyList<MainBattleAIProfile> Profiles => profiles ?? Array.Empty<MainBattleAIProfile>();

        public bool TryResolve(string monsterId, out MainBattleAIProfile profile)
        {
            var normalizedId = monsterId?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedId) && profiles != null)
            {
                for (var index = 0; index < profiles.Length; index++)
                {
                    var candidate = profiles[index];
                    if (candidate != null && string.Equals(
                            candidate.MonsterId,
                            normalizedId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        profile = candidate;
                        return true;
                    }
                }
            }

            profile = null;
            return false;
        }

        public bool TryValidate(out string error)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (profiles != null)
            {
                for (var index = 0; index < profiles.Length; index++)
                {
                    var profile = profiles[index];
                    if (profile == null)
                    {
                        error = $"MainBattle AI Profile is null. Index={index}";
                        return false;
                    }

                    if (!profile.TryValidate(out error))
                    {
                        return false;
                    }

                    if (!ids.Add(profile.MonsterId))
                    {
                        error = $"MainBattle AI Profile ID is duplicated: {profile.MonsterId}";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        public static MainBattleAIProfileCatalog LoadDefault()
        {
            return Resources.Load<MainBattleAIProfileCatalog>(ResourceName);
        }

#if UNITY_EDITOR
        public void EditorConfigure(params MainBattleAIProfile[] values)
        {
            profiles = values ?? Array.Empty<MainBattleAIProfile>();
        }

        public void EditorUpsert(
            string monsterId,
            MainBattleMonsterRole role,
            UnitTargetPriority priority,
            float preferredRange,
            float retreatRange,
            float retargetSeconds)
        {
            var updated = MainBattleAIProfile.EditorCreate(
                monsterId,
                role,
                priority,
                preferredRange,
                retreatRange,
                retargetSeconds);
            var values = new List<MainBattleAIProfile>(profiles ?? Array.Empty<MainBattleAIProfile>());
            for (var index = 0; index < values.Count; index++)
            {
                if (!string.Equals(values[index]?.MonsterId, monsterId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                values[index] = updated;
                profiles = values.ToArray();
                return;
            }

            values.Add(updated);
            profiles = values.ToArray();
        }
#endif
    }
}
