using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    public enum HexCastleAssaultPattern
    {
        GeneralAdvance = 0,
        ResourceRaider = 1,
        TurretHunter = 2,
        DefenderHunter = 3,
        WallBreaker = 4,
        ThreatSuppressor = 5,
        TacticalSupport = 6
    }

    public enum HexCastleAssaultSupportFocus
    {
        Adaptive = 0,
        AttackBuff = 1,
        DefenseBuff = 2,
        Recovery = 3
    }

    public enum HexCastleAssaultSupportAction
    {
        None = 0,
        AttackBuff = 1,
        DefenseBuff = 2,
        Heal = 3
    }

    public static class HexCastleAssaultAIPresentation // HUD 표기와 실제 정책 설명을 한곳에서 관리한다
    {
        public static string ResolveTag(HexCastleAssaultAIProfile profile)
        {
            if (profile == null)
            {
                return "일반 전진";
            }

            switch (profile.Pattern)
            {
                case HexCastleAssaultPattern.ResourceRaider:
                    return "자원 약탈";
                case HexCastleAssaultPattern.TurretHunter:
                    return "포탑 사냥";
                case HexCastleAssaultPattern.DefenderHunter:
                    return "수비대 사냥";
                case HexCastleAssaultPattern.WallBreaker:
                    return "성벽 파괴";
                case HexCastleAssaultPattern.ThreatSuppressor:
                    return "위협 억제";
                case HexCastleAssaultPattern.TacticalSupport:
                    return ResolveSupportTag(profile.SupportFocus);
                default:
                    return "일반 전진";
            }
        }

        public static string ResolveDescription(HexCastleAssaultAIProfile profile)
        {
            if (profile == null)
            {
                return "위협을 먼저 대응하면서 왕궁으로 전진하고, 진행 중 주변 건물을 확률적으로 공격합니다.";
            }

            switch (profile.Pattern)
            {
                case HexCastleAssaultPattern.ResourceRaider:
                    return "골드·장비·열쇠 건물을 우선 노립니다. 공격받으면 가까운 위협부터 처리한 뒤 원래 목표로 복귀합니다.";
                case HexCastleAssaultPattern.TurretHunter:
                    return "가까운 포탑을 우선 파괴합니다. 진입을 막는 성벽은 먼저 부수며, 닫힌 벽 안쪽 목표는 공격하지 않습니다.";
                case HexCastleAssaultPattern.DefenderHunter:
                    return "주변 수비대를 우선 추적해 제거합니다. 닫힌 성벽 안쪽 수비대는 벽이 열리기 전 공격하지 않습니다.";
                case HexCastleAssaultPattern.WallBreaker:
                    return "가까운 성벽 3개 중 가중 확률로 돌파 지점을 고르고, 다음 방어층으로 이어지는 성벽을 우선 파괴합니다.";
                case HexCastleAssaultPattern.ThreatSuppressor:
                    return "공격 중인 수비대·포탑 같은 위협을 넓은 범위에서 먼저 제거한 뒤 왕궁으로 전진합니다.";
                case HexCastleAssaultPattern.TacticalSupport:
                    return ResolveSupportDescription(profile.SupportFocus);
                default:
                    return "위협을 먼저 대응하면서 왕궁으로 전진하고, 진행 중 주변 건물을 확률적으로 공격합니다.";
            }
        }

        private static string ResolveSupportTag(HexCastleAssaultSupportFocus focus)
        {
            switch (focus)
            {
                case HexCastleAssaultSupportFocus.AttackBuff:
                    return "공격 지원";
                case HexCastleAssaultSupportFocus.DefenseBuff:
                    return "방어 지원";
                case HexCastleAssaultSupportFocus.Recovery:
                    return "회복 지원";
                default:
                    return "전술 지원";
            }
        }

        private static string ResolveSupportDescription(HexCastleAssaultSupportFocus focus)
        {
            switch (focus)
            {
                case HexCastleAssaultSupportFocus.AttackBuff:
                    return "주변 아군을 따라가며 필요한 대상에게 공격력 강화를 우선 제공합니다.";
                case HexCastleAssaultSupportFocus.DefenseBuff:
                    return "주변 아군을 따라가며 집중 공격받는 대상에게 피해 감소를 우선 제공합니다.";
                case HexCastleAssaultSupportFocus.Recovery:
                    return "주변 아군을 따라가며 체력이 낮은 대상을 우선 회복합니다.";
                default:
                    return "주변 아군을 따라가며 회복·공격 강화·피해 감소 중 가장 필요한 지원을 선택합니다.";
            }
        }
    }

    [Serializable]
    public sealed class HexCastleAssaultAIProfile // 사각 AI 성향을 Hex Runtime에 독립 복제한다
    {
        [SerializeField] private string monsterId = string.Empty;
        [SerializeField] private HexCastleAssaultPattern pattern = HexCastleAssaultPattern.GeneralAdvance;
        [SerializeField] private HexCastleAssaultSupportFocus supportFocus = HexCastleAssaultSupportFocus.Adaptive;
        [SerializeField, Min(1f)] private float supportRange = 5f;
        [SerializeField, Min(0.1f)] private float supportCooldown = 4f;
        [SerializeField, Min(0.1f)] private float supportDuration = 5f;
        [SerializeField, Range(0f, 1f)] private float healRatio = 0.24f;
        [SerializeField, Range(0f, 1f)] private float attackBuffRate = 0.2f;
        [SerializeField, Range(0.05f, 1f)] private float defenseDamageMultiplier = 0.75f;

        public string MonsterId => monsterId ?? string.Empty;
        public HexCastleAssaultPattern Pattern => pattern;
        public HexCastleAssaultSupportFocus SupportFocus => supportFocus;
        public float SupportRange => Mathf.Max(1f, supportRange);
        public float SupportCooldown => Mathf.Max(0.1f, supportCooldown);
        public float SupportDuration => Mathf.Max(0.1f, supportDuration);
        public float HealRatio => Mathf.Clamp01(healRatio);
        public float AttackBuffRate => Mathf.Clamp01(attackBuffRate);
        public float DefenseDamageMultiplier => Mathf.Clamp(defenseDamageMultiplier, 0.05f, 1f);

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            HexCastleAssaultPattern targetPattern,
            HexCastleAssaultSupportFocus targetFocus,
            float range,
            float cooldown,
            float duration,
            float healingRatio,
            float attackRate,
            float defenseMultiplier)
        {
            monsterId = id?.Trim() ?? string.Empty;
            pattern = targetPattern;
            supportFocus = targetFocus;
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
        fileName = "HexCastleAssaultAIProfileCatalog",
        menuName = "ProjectMT/Castle Raid Hex/Assault AI Profile Catalog")]
    public sealed class HexCastleAssaultAIProfileCatalog : ScriptableObject
    {
        public const string DefaultResourcesPath = "HexCastleAssaultAIProfileCatalog";

        private static readonly HexCastleAssaultAIProfile DefaultProfile =
            new HexCastleAssaultAIProfile();

        [SerializeField] private List<HexCastleAssaultAIProfile> entries =
            new List<HexCastleAssaultAIProfile>();

        public IReadOnlyList<HexCastleAssaultAIProfile> Entries => entries;

        public HexCastleAssaultAIProfile Resolve(string monsterId)
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
                    error = $"Hex 공격 AI Profile {index + 1}의 Monster ID가 비어 있습니다.";
                    return false;
                }

                if (!ids.Add(entry.MonsterId))
                {
                    error = $"Hex 공격 AI Profile ID가 중복됩니다. Monster={entry.MonsterId}";
                    return false;
                }
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorReplaceEntries(IEnumerable<HexCastleAssaultAIProfile> source)
        {
            entries = source == null
                ? new List<HexCastleAssaultAIProfile>()
                : new List<HexCastleAssaultAIProfile>(source);
        }
#endif
    }
}
