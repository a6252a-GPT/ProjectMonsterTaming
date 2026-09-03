using System;
using System.Linq;
using ProjectMT.EditorTools.MonsterMaker;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    internal readonly struct MonsterBalanceMetrics
    {
        public MonsterBalanceMetrics(string combatType, string basicTarget, string basicPattern,
            float basicDps, string activeName, string activeType, string activeTarget,
            float activeBurst, float activeDuration, float activeDps, bool hasActiveDamage)
        {
            CombatType = combatType;
            BasicTarget = basicTarget;
            BasicPattern = basicPattern;
            BasicDps = basicDps;
            ActiveName = activeName;
            ActiveType = activeType;
            ActiveTarget = activeTarget;
            ActiveBurst = activeBurst;
            ActiveDuration = activeDuration;
            ActiveDps = activeDps;
            HasActiveDamage = hasActiveDamage;
        }

        public string CombatType { get; }
        public string BasicTarget { get; }
        public string BasicPattern { get; }
        public float BasicDps { get; }
        public string ActiveName { get; }
        public string ActiveType { get; }
        public string ActiveTarget { get; }
        public float ActiveBurst { get; }
        public float ActiveDuration { get; }
        public float ActiveDps { get; }
        public bool HasActiveDamage { get; }
    }

    internal static class MonsterBalanceTableMetrics
    {
        public static MonsterBalanceMetrics Evaluate(MonsterMakerDraft draft, float attackPower, float attackSpeed)
        {
            if (draft == null)
            {
                return new MonsterBalanceMetrics("-", "-", "-", 0f, "-", "없음", "-", 0f, 0f, 0f, false);
            }

            var basicDps = Mathf.Max(0f, attackPower) * Mathf.Max(0f, attackSpeed);
            var activeName = draft.UseActiveSkill && !string.IsNullOrWhiteSpace(draft.ActiveSkillName)
                ? draft.ActiveSkillName : "-";
            if (draft.UseActiveSkill && draft.ActiveAttackProfile != null)
            {
                var profile = draft.ActiveAttackProfile;
                var multiplierBudget = 0f;
                for (var index = 0; index < profile.Steps.Count; index++)
                {
                    var step = profile.Steps[index];
                    if (step == null) continue;
                    multiplierBudget += step.DamageMultiplierMode == MonsterActiveDamageMultiplierMode.RandomRange
                        ? (step.DamageMultiplier + step.MaximumDamageMultiplier) * 0.5f
                        : step.DamageMultiplier;
                }

                var duration = Mathf.Max(0.05f, profile.EstimateDuration());
                var burst = Mathf.Max(0f, attackPower) * multiplierBudget;
                return new MonsterBalanceMetrics(
                    GetCombatTypeLabel(draft.CombatType), GetBasicTargetLabel(draft.BasicAttackProfile),
                    GetBasicPatternLabel(draft.BasicAttackProfile), basicDps, activeName, "공격",
                    GetActiveAttackTargetLabel(profile), burst, duration, burst / duration, true);
            }

            if (draft.UseActiveSkill && draft.ActiveEffectProfile != null)
            {
                var profile = draft.ActiveEffectProfile;
                return new MonsterBalanceMetrics(
                    GetCombatTypeLabel(draft.CombatType), GetBasicTargetLabel(draft.BasicAttackProfile),
                    GetBasicPatternLabel(draft.BasicAttackProfile), basicDps, activeName,
                    GetEffectRoleLabel(profile.Role), GetEffectTargetLabel(profile),
                    0f, profile.EstimateDuration(), 0f, false);
            }

            return new MonsterBalanceMetrics(
                GetCombatTypeLabel(draft.CombatType), GetBasicTargetLabel(draft.BasicAttackProfile),
                GetBasicPatternLabel(draft.BasicAttackProfile), basicDps,
                "-", "없음", "-", 0f, 0f, 0f, false);
        }

        public static string GetRarityLabel(MonsterRarity rarity) => rarity switch
        {
            MonsterRarity.Rare => "희귀",
            MonsterRarity.Epic => "영웅",
            MonsterRarity.Legendary => "전설",
            MonsterRarity.Mythic => "신화",
            _ => "일반"
        };

        public static string GetRarityClass(MonsterRarity rarity) => rarity switch
        {
            MonsterRarity.Rare => "balance-rarity--rare",
            MonsterRarity.Epic => "balance-rarity--epic",
            MonsterRarity.Legendary => "balance-rarity--legendary",
            MonsterRarity.Mythic => "balance-rarity--mythic",
            _ => "balance-rarity--common"
        };

        private static string GetCombatTypeLabel(MonsterCombatType type) => type switch
        {
            MonsterCombatType.Ranged => "원거리",
            MonsterCombatType.Special => "특수",
            _ => "근거리"
        };

        private static string GetBasicTargetLabel(MonsterBasicAttackProfile profile)
        {
            if (profile == null) return "미지정";
            if (profile.Shape == MonsterBasicAttackShape.Single && profile.MaxTargets <= 1 &&
                profile.ProjectileCount <= 1) return "단일 대상";
            var projectile = profile.ProjectileCount > 1 ? $" · {profile.ProjectileCount}발" : string.Empty;
            return $"최대 {profile.MaxTargets}명{projectile}";
        }

        private static string GetBasicPatternLabel(MonsterBasicAttackProfile profile)
        {
            if (profile == null) return "미지정";
            var shape = profile.Shape switch
            {
                MonsterBasicAttackShape.Fan => "부채꼴",
                MonsterBasicAttackShape.Line => "직선",
                MonsterBasicAttackShape.Circle => "원형",
                _ => "단일"
            };
            var delivery = profile.DeliveryModule switch
            {
                MonsterBasicAttackDeliveryModule.Projectile => "투사체",
                MonsterBasicAttackDeliveryModule.TravelingArea => "이동 범위",
                _ => "직접"
            };
            return $"{shape} · {delivery}";
        }

        private static string GetActiveAttackTargetLabel(MonsterActiveAttackProfile profile)
        {
            var steps = profile.Steps.Where(step => step != null).ToArray();
            if (steps.Length == 0) return "미지정";
            var maxTargets = steps.Max(step => step.MaxTargets);
            var allSingle = steps.All(step => IsSingleTargetPattern(step.Pattern) && step.MaxTargets <= 1);
            var target = allSingle ? "단일 대상" : $"범위 · 최대 {maxTargets}명";
            if (steps.Any(step => step.TargetPolicy == MonsterActiveTargetPolicy.DifferentTarget))
                target += " · 대상 전환";
            return steps.Length > 1 ? $"{target} · {steps.Length}단계" : target;
        }

        private static bool IsSingleTargetPattern(MonsterActiveAttackPattern pattern) =>
            pattern is MonsterActiveAttackPattern.SingleTarget or
                MonsterActiveAttackPattern.StandardProjectile or
                MonsterActiveAttackPattern.ReturningProjectile or
                MonsterActiveAttackPattern.InstantMagic;

        private static string GetEffectRoleLabel(MonsterEffectActiveRole role) => role switch
        {
            MonsterEffectActiveRole.Guard => "수호",
            MonsterEffectActiveRole.Debuff => "약화",
            _ => "지원"
        };

        private static string GetEffectTargetLabel(MonsterEffectActiveProfile profile)
        {
            var labels = profile.Groups.Where(group => group != null)
                .Select(group => GetSkillTargetLabel(group.Target)).Distinct().ToArray();
            if (labels.Length == 0) return "미지정";
            return labels.Length == 1 ? labels[0] : $"{string.Join("/", labels)} · {profile.Groups.Count}묶음";
        }

        private static string GetSkillTargetLabel(MonsterSkillTargetType target) => target switch
        {
            MonsterSkillTargetType.Self => "자신",
            MonsterSkillTargetType.CurrentTarget => "현재 대상",
            MonsterSkillTargetType.LowestHealthAlly => "최저 체력 아군",
            MonsterSkillTargetType.HighestAttackAlly => "최고 공격 아군",
            MonsterSkillTargetType.NearbyAllies => "주변 아군",
            MonsterSkillTargetType.AllAllies => "아군 전체",
            MonsterSkillTargetType.TargetAreaEnemies => "대상 주변 적",
            MonsterSkillTargetType.DensestEnemyPosition => "적 밀집 지역",
            MonsterSkillTargetType.HighestAttackEnemy => "최고 공격 적",
            MonsterSkillTargetType.LowestHealthEnemy => "최저 체력 적",
            MonsterSkillTargetType.HighestHealthEnemy => "최고 체력 적",
            MonsterSkillTargetType.FarthestEnemy => "가장 먼 적",
            MonsterSkillTargetType.RangedEnemyFirst => "원거리 적 우선",
            _ => "적 1명"
        };
    }
}
