using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    public enum CastleRaidAiPattern // 군단의 역습 전용 목표 정책
    {
        BalancedAdvance,
        BuildingPriority,
        DefenseFacilityPriority,
        DefenderPriority,
        WallBreaker,
        PalaceRush,
        TacticalSupport
    }

    public enum CastleRaidSupportFocus // 지원형의 성향
    {
        Adaptive,
        AttackBuff,
        DefenseBuff,
        Recovery
    }

    public enum CastleRaidSupportAction
    {
        None,
        AttackBuff,
        DefenseBuff,
        Heal
    }

    [Serializable]
    public sealed class CastleRaidAIProfile // 설정만 보존하고 실행 상태는 유닛이 소유
    {
        [SerializeField] private string monsterId = string.Empty;
        [SerializeField] private CastleRaidAiPattern pattern = CastleRaidAiPattern.BalancedAdvance;
        [SerializeField] private CastleRaidSupportFocus supportFocus = CastleRaidSupportFocus.Adaptive;
        [SerializeField, Min(1f)] private float supportRange = 5f;
        [SerializeField, Min(0.1f)] private float supportCooldown = 4f;
        [SerializeField, Min(0.1f)] private float supportDuration = 5f;
        [SerializeField, Range(0f, 1f)] private float healRatio = 0.24f;
        [SerializeField, Range(0f, 1f)] private float attackBuffRate = 0.2f;
        [SerializeField, Range(0.05f, 1f)] private float defenseDamageMultiplier = 0.75f;

        public CastleRaidAIProfile()
        {
        }

        public CastleRaidAIProfile(string id, CastleRaidAiPattern aiPattern)
        {
            monsterId = id ?? string.Empty;
            pattern = aiPattern;
        }

        public string MonsterId => monsterId ?? string.Empty;
        public CastleRaidAiPattern Pattern => pattern;
        public CastleRaidSupportFocus SupportFocus => supportFocus;
        public float SupportRange => Mathf.Max(1f, supportRange);
        public float SupportCooldown => Mathf.Max(0.1f, supportCooldown);
        public float SupportDuration => Mathf.Max(0.1f, supportDuration);
        public float HealRatio => Mathf.Clamp01(healRatio);
        public float AttackBuffRate => Mathf.Clamp01(attackBuffRate);
        public float DefenseDamageMultiplier => Mathf.Clamp(defenseDamageMultiplier, 0.05f, 1f);

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            CastleRaidAiPattern aiPattern,
            CastleRaidSupportFocus focus,
            float range,
            float cooldown,
            float duration,
            float healingRatio,
            float attackRate,
            float defenseMultiplier)
        {
            monsterId = id?.Trim() ?? string.Empty;
            pattern = aiPattern;
            supportFocus = focus;
            supportRange = Mathf.Max(1f, range);
            supportCooldown = Mathf.Max(0.1f, cooldown);
            supportDuration = Mathf.Max(0.1f, duration);
            healRatio = Mathf.Clamp01(healingRatio);
            attackBuffRate = Mathf.Clamp01(attackRate);
            defenseDamageMultiplier = Mathf.Clamp(defenseMultiplier, 0.05f, 1f);
        }
#endif
    }

    [CreateAssetMenu(
        fileName = "CastleRaidAIProfileCatalog",
        menuName = "ProjectMT/Castle Raid/AI Profile Catalog")]
    public sealed class CastleRaidAIProfileCatalog : ScriptableObject // Monster ID별 군단의 역습 전용 설정
    {
        public const string DefaultResourcesPath = "CastleRaidAIProfileCatalog";

        private static readonly CastleRaidAIProfile DefaultProfile =
            new CastleRaidAIProfile(string.Empty, CastleRaidAiPattern.BalancedAdvance);

        [SerializeField] private List<CastleRaidAIProfile> entries = new List<CastleRaidAIProfile>();

        public IReadOnlyList<CastleRaidAIProfile> Entries => entries;

        public CastleRaidAIProfile Resolve(string monsterId)
        {
            if (!string.IsNullOrWhiteSpace(monsterId))
            {
                for (var index = 0; index < entries.Count; index++)
                {
                    var entry = entries[index];
                    if (entry != null && string.Equals(
                            entry.MonsterId,
                            monsterId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return entry;
                    }
                }
            }

            return DefaultProfile;
        }

        public bool TryValidate(out string error)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.MonsterId))
                {
                    error = $"Castle Raid AI Profile {index + 1}의 Monster ID가 비어 있습니다.";
                    return false;
                }

                if (!ids.Add(entry.MonsterId))
                {
                    error = $"Castle Raid AI Profile ID가 중복되었습니다. Monster={entry.MonsterId}";
                    return false;
                }
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorUpsert(
            string monsterId,
            CastleRaidAiPattern pattern,
            CastleRaidSupportFocus focus,
            float range,
            float cooldown,
            float duration,
            float healRatio,
            float attackBuffRate,
            float defenseDamageMultiplier)
        {
            CastleRaidAIProfile entry = null;
            for (var index = 0; index < entries.Count; index++)
            {
                if (entries[index] != null && string.Equals(
                        entries[index].MonsterId,
                        monsterId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    entry = entries[index];
                    break;
                }
            }

            if (entry == null)
            {
                entry = new CastleRaidAIProfile();
                entries.Add(entry);
            }

            entry.EditorConfigure(
                monsterId,
                pattern,
                focus,
                range,
                cooldown,
                duration,
                healRatio,
                attackBuffRate,
                defenseDamageMultiplier);
        }
#endif
    }
}
