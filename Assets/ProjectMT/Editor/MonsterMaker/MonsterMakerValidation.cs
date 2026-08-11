using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public enum MonsterMakerIssueSeverity
    {
        Warning,
        Error
    }

    public readonly struct MonsterMakerIssue
    {
        public MonsterMakerIssue(MonsterMakerIssueSeverity severity, string code, string message, UnityEngine.Object context)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Context = context;
        }

        public MonsterMakerIssueSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public UnityEngine.Object Context { get; }
    }

    public sealed class MonsterMakerValidationReport
    {
        private readonly List<MonsterMakerIssue> issues = new List<MonsterMakerIssue>();

        public IReadOnlyList<MonsterMakerIssue> Issues => issues;
        public bool HasErrors => issues.Any(issue => issue.Severity == MonsterMakerIssueSeverity.Error);

        internal void Add(MonsterMakerIssueSeverity severity, string code, string message, UnityEngine.Object context)
        {
            issues.Add(new MonsterMakerIssue(severity, code, message, context));
        }
    }

    public static class MonsterMakerValidator // 생성 전에 Draft만 읽고 Catalog는 바꾸지 않는 검사기
    {
        public static MonsterMakerValidationReport Validate(MonsterMakerDraft draft)
        {
            var report = new MonsterMakerValidationReport();
            if (draft == null)
            {
                report.Add(MonsterMakerIssueSeverity.Error, "MAKER-DRAFT", "Monster Maker Draft가 없습니다.", null);
                return report;
            }

            ValidateIdentity(draft, report);
            ValidateBody(draft, report);
            ValidateStats(draft, report);
            ValidateMotions(draft, report);
            ValidateCombat(draft, report);
            ValidateAscension(draft, report);
            ValidateFeedback(draft, report);
            return report;
        }

        public static string ResolveAnimatorPath(MonsterMakerDraft draft)
        {
            if (draft?.VendorPrefab == null || draft.AnimatorSource == null)
            {
                return string.Empty;
            }

            var vendorPath = AssetDatabase.GetAssetPath(draft.VendorPrefab);
            var animatorPath = AssetDatabase.GetAssetPath(draft.AnimatorSource);
            if (string.IsNullOrWhiteSpace(vendorPath) || !string.Equals(vendorPath, animatorPath, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return AnimationUtility.CalculateTransformPath(
                draft.AnimatorSource.transform,
                draft.VendorPrefab.transform);
        }

        private static void ValidateIdentity(MonsterMakerDraft draft, MonsterMakerValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(draft.MonsterId))
            {
                report.Add(MonsterMakerIssueSeverity.Error, "MAKER-ID", "Monster ID를 입력하세요.", draft);
            }
            else if (!UsesSafeId(draft.MonsterId))
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-ID-CHAR",
                    "Monster ID는 영문·숫자·밑줄·하이픈만 사용할 수 있습니다.",
                    draft);
            }

            else
            {
                ValidateCatalogIdentityOwnership(draft, report);
            }

            if (string.IsNullOrWhiteSpace(draft.DisplayName))
            {
                report.Add(MonsterMakerIssueSeverity.Error, "MAKER-NAME", "표시 이름을 입력하세요.", draft);
            }

            if (draft.Portrait == null)
            {
                report.Add(MonsterMakerIssueSeverity.Error, "MAKER-PORTRAIT", "카드 초상화를 지정하세요.", draft);
            }
        }

        private static void ValidateBody(MonsterMakerDraft draft, MonsterMakerValidationReport report)
        {
            if (draft.VendorPrefab == null || PrefabUtility.GetPrefabAssetType(draft.VendorPrefab) == PrefabAssetType.NotAPrefab)
            {
                report.Add(MonsterMakerIssueSeverity.Error, "MAKER-PREFAB", "Project에 저장된 Vendor Prefab을 지정하세요.", draft);
                return;
            }

            if (draft.AnimatorSource == null)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-ANIMATOR",
                    "사용할 Animator를 제작자가 직접 지정해야 합니다.",
                    draft.VendorPrefab);
            }
            else
            {
                var vendorPath = AssetDatabase.GetAssetPath(draft.VendorPrefab);
                var animatorAssetPath = AssetDatabase.GetAssetPath(draft.AnimatorSource);
                if (!string.Equals(vendorPath, animatorAssetPath, StringComparison.Ordinal))
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-ANIMATOR-OWNER",
                        "선택한 Animator가 Vendor Prefab에 속하지 않습니다.",
                        draft.AnimatorSource);
                }
            }

            if (draft.VisualScale.x <= 0f || draft.VisualScale.y <= 0f || draft.VisualScale.z <= 0f ||
                draft.BodyRadius <= 0f || draft.BodyHeight <= 0f || draft.SelectionRadius <= 0f ||
                draft.HpBarHeight < 0f || draft.PreviewScale <= 0f || draft.VfxScale <= 0f)
            {
                report.Add(MonsterMakerIssueSeverity.Error, "MAKER-BODY", "크기·판정 수치는 양수여야 합니다.", draft);
            }

            if (string.IsNullOrWhiteSpace(draft.AttackOriginPath) || string.IsNullOrWhiteSpace(draft.HitCenterPath) ||
                string.Equals(draft.AttackOriginPath, draft.HitCenterPath, StringComparison.Ordinal))
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-SOCKET",
                    "AttackOrigin과 HitCenter는 서로 다른 유효 경로여야 합니다.",
                    draft);
            }
        }

        private static void ValidateStats(MonsterMakerDraft draft, MonsterMakerValidationReport report)
        {
            if (draft.MaxHealth <= 0f || draft.AttackPower < 0f || draft.Defense < 0f ||
                draft.AttackSpeed <= 0f || draft.MoveSpeed < 0f || draft.AttackRange <= 0f)
            {
                report.Add(MonsterMakerIssueSeverity.Error, "MAKER-STATS", "기본 능력치가 유효하지 않습니다.", draft);
            }
        }

        private static void ValidateMotions(MonsterMakerDraft draft, MonsterMakerValidationReport report)
        {
            ValidateRequiredClip(draft.IdleClip, "Idle", true, report, draft);
            ValidateRequiredClip(draft.MoveClip, "Move", true, report, draft);
            ValidateRequiredClip(draft.DeathClip, "Death", false, report, draft);
            if (draft.Attacks.Count == 0)
            {
                report.Add(MonsterMakerIssueSeverity.Error, "MAKER-ATTACK-NONE", "Attack Clip을 하나 이상 지정하세요.", draft);
                return;
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var attackIndex = 0; attackIndex < draft.Attacks.Count; attackIndex++)
            {
                var attack = draft.Attacks[attackIndex];
                if (attack == null || attack.Clip == null)
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-ATTACK",
                        $"Attack {attackIndex + 1}의 Clip을 지정하세요.",
                        draft);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(attack.MotionId) ||
                    !UsesSafeId(attack.MotionId) ||
                    !ids.Add(attack.MotionId))
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-ATTACK-ID",
                        $"자동 Attack Motion ID가 비었거나 중복되었습니다. 해당 공격을 삭제 후 다시 추가하세요: {attack.MotionId}",
                        attack.Clip);
                }

                ValidateClipRig(attack.Clip, "Attack", report);
                if (attack.Clip.isLooping)
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Warning,
                        "MAKER-ATTACK-LOOP",
                        $"Attack Clip이 Loop Import 상태입니다: {attack.MotionId}",
                        attack.Clip);
                }

                if (attack.Markers.Count == 0)
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-MARKER-NONE",
                        $"Attack {attack.MotionId}에 수동 Marker가 없습니다.",
                        attack.Clip);
                    continue;
                }

                var ratioSum = 0f;
                var previousTime = -1f;
                for (var markerIndex = 0; markerIndex < attack.Markers.Count; markerIndex++)
                {
                    var marker = attack.Markers[markerIndex];
                    if (marker == null || marker.NormalizedTime < 0f || marker.NormalizedTime > 1f ||
                        marker.PowerRatio < 0f || marker.NormalizedTime < previousTime)
                    {
                        report.Add(
                            MonsterMakerIssueSeverity.Error,
                            "MAKER-MARKER",
                            $"Attack {attack.MotionId} Marker는 0~1 오름차순이어야 합니다.",
                            attack.Clip);
                        break;
                    }

                    previousTime = marker.NormalizedTime;
                    ratioSum += marker.PowerRatio;
                    if (!CanResolveGeneratedSocket(draft, marker.SocketOverride))
                    {
                        report.Add(
                            MonsterMakerIssueSeverity.Error,
                            "MAKER-SOCKET-OVERRIDE",
                            $"Attack {attack.MotionId} Marker의 Socket 경로를 생성될 Adapter에서 찾을 수 없습니다: " +
                            marker.SocketOverride,
                            draft.VendorPrefab);
                    }
                }

                if (Mathf.Abs(ratioSum - 1f) > 0.001f)
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-MARKER-RATIO",
                        $"Attack {attack.MotionId} Marker 피해 비율 합은 1이어야 합니다. 현재 {ratioSum:0.###}",
                        attack.Clip);
                }
            }
        }

        private static void ValidateCombat(MonsterMakerDraft draft, MonsterMakerValidationReport report)
        {
            switch (draft.CombatType)
            {
                case MonsterCombatType.Melee:
                    if (draft.MeleeMode == MonsterMeleeAttackMode.Area &&
                        (draft.MeleeAreaRadius <= 0f || draft.MeleeMaxTargets < 1))
                    {
                        report.Add(MonsterMakerIssueSeverity.Error, "MAKER-MELEE", "근거리 범위 설정이 유효하지 않습니다.", draft);
                    }

                    break;
                case MonsterCombatType.Ranged:
                    if (draft.RangedDeliveryMode == MonsterRangedDeliveryMode.Projectile)
                    {
                        var projectileVisual = draft.ProjectilePrefab;
                        if (projectileVisual == null)
                        {
                            projectileVisual = AssetDatabase.LoadAssetAtPath<GameObject>(
                                MonsterMakerAssetWriter.DefaultProjectilePrefabPath);
                        }
                        if (projectileVisual == null ||
                            draft.ProjectileSpeed <= 0f || draft.ProjectileLifetime <= 0f)
                        {
                            report.Add(
                                MonsterMakerIssueSeverity.Error,
                                "MAKER-PROJECTILE",
                                "투사체형 원거리는 투사체 VFX 또는 공용 임시 구슬과 양수 속도·수명이 필요합니다.",
                                draft.ProjectilePrefab);
                        }

                        if (draft.ProjectileMode == MonsterProjectileAttackMode.Piercing &&
                            (draft.ProjectileHitRadius <= 0f || draft.ProjectileMaxPiercingTargets < 1))
                        {
                            report.Add(
                                MonsterMakerIssueSeverity.Error,
                                "MAKER-PROJECTILE-PIERCING",
                                "관통 투사체는 양수 피격 반경과 1 이상의 최대 관통 대상 수가 필요합니다.",
                                draft);
                        }
                    }
                    else if (draft.ProjectileMode == MonsterProjectileAttackMode.Piercing)
                    {
                        report.Add(
                            MonsterMakerIssueSeverity.Error,
                            "MAKER-INSTANT-PIERCING",
                            "즉발형 관통·빔은 아직 지원하지 않습니다. 단일 또는 범위를 선택하세요.",
                            draft);
                    }

                    if (draft.ProjectileMode == MonsterProjectileAttackMode.Area &&
                        (draft.ProjectileImpactRadius <= 0f || draft.ProjectileMaxImpactTargets < 1))
                    {
                        report.Add(
                            MonsterMakerIssueSeverity.Error,
                            "MAKER-PROJECTILE-AREA",
                            "범위형 원거리는 양수 범위 반경과 1 이상의 최대 피격 대상 수가 필요합니다.",
                            draft);
                    }

                    break;
                case MonsterCombatType.Special:
                    if (string.IsNullOrWhiteSpace(draft.SpecialEffectId) || draft.SpecialRadius <= 0f ||
                        draft.SpecialMaxTargets < 1 || draft.SpecialDuration <= 0f || draft.SpecialModifier.IsEmpty)
                    {
                        report.Add(
                            MonsterMakerIssueSeverity.Error,
                            "MAKER-SPECIAL",
                            "특수 Area Buff의 Effect ID·범위·대상 수·지속 시간·능력치 효과가 필요합니다.",
                            draft);
                    }

                    break;
            }
        }

        private static void ValidateAscension(MonsterMakerDraft draft, MonsterMakerValidationReport report)
        {
            if (!draft.AscensionConfigured)
            {
                return;
            }

            if (draft.Ascension1.IsEmpty || draft.Ascension3.IsEmpty || draft.Ascension5.IsEmpty ||
                draft.Ascension1.HasNegativeRate || draft.Ascension3.HasNegativeRate || draft.Ascension5.HasNegativeRate)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-ASCENSION-STAT",
                    "돌파 1·3·5는 음수가 아닌 능력치 보정이 각각 필요합니다.",
                    draft);
            }

            ValidateAbility(draft.Ascension2, 2, report, draft);
            ValidateAbility(draft.Ascension4, 4, report, draft);
        }

        private static void ValidateFeedback(MonsterMakerDraft draft, MonsterMakerValidationReport report)
        {
            ValidateFeedbackCue(draft.SpawnFeedback, "생성", report, draft);
            ValidateFeedbackCue(draft.AttackStartFeedback, "공격 시작", report, draft);
            ValidateFeedbackCue(draft.AttackMarkerFeedback, "공격 타격", report, draft);
            ValidateFeedbackCue(draft.HitFeedback, "피격", report, draft);
            ValidateFeedbackCue(draft.DeathFeedback, "사망", report, draft);
            ValidateFeedbackCue(draft.SpecialFeedback, "특수", report, draft);

            for (var attackIndex = 0; attackIndex < draft.Attacks.Count; attackIndex++)
            {
                var attack = draft.Attacks[attackIndex];
                if (attack == null)
                {
                    continue;
                }

                ValidateFeedbackCue(
                    attack.AttackStartFeedback,
                    $"{attack.MotionId} 공격 동작",
                    report,
                    draft);
                for (var markerIndex = 0; markerIndex < attack.Markers.Count; markerIndex++)
                {
                    ValidateFeedbackCue(
                        attack.Markers[markerIndex]?.Feedback,
                        $"{attack.MotionId} 타격 {markerIndex + 1}",
                        report,
                        draft);
                }
            }
        }

        private static void ValidateFeedbackCue(
            MonsterMakerFeedbackDraft feedback,
            string role,
            MonsterMakerValidationReport report,
            UnityEngine.Object context)
        {
            if (feedback?.Sfx != null && !feedback.Sfx.HasPlayableClip)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-SFX-EMPTY",
                    $"{role}의 기존 SFX Cue에 재생 가능한 AudioClip이 없습니다: {feedback.Sfx.name}",
                    context);
            }
        }

        private static bool CanResolveGeneratedSocket(MonsterMakerDraft draft, string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                string.Equals(path, draft.AttackOriginPath, StringComparison.Ordinal) ||
                string.Equals(path, draft.HitCenterPath, StringComparison.Ordinal) ||
                string.Equals(path, "Visual", StringComparison.Ordinal))
            {
                return true;
            }

            const string visualPrefix = "Visual/";
            return draft.VendorPrefab != null && path.StartsWith(visualPrefix, StringComparison.Ordinal) &&
                   draft.VendorPrefab.transform.Find(path.Substring(visualPrefix.Length)) != null;
        }

        private static void ValidateRequiredClip(
            AnimationClip clip,
            string role,
            bool shouldLoop,
            MonsterMakerValidationReport report,
            UnityEngine.Object context)
        {
            if (clip == null)
            {
                report.Add(MonsterMakerIssueSeverity.Error, $"MAKER-{role.ToUpperInvariant()}", $"{role} Clip을 직접 지정하세요.", context);
                return;
            }

            ValidateClipRig(clip, role, report);
            if (clip.isLooping != shouldLoop)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Warning,
                    $"MAKER-{role.ToUpperInvariant()}-LOOP",
                    shouldLoop
                        ? $"{role} Clip이 Loop Import 상태가 아닙니다. 도구는 자동 변경하지 않습니다."
                        : $"{role} Clip이 Loop Import 상태입니다. 도구는 자동 변경하지 않습니다.",
                    clip);
            }
        }

        private static void ValidateClipRig(
            AnimationClip clip,
            string role,
            MonsterMakerValidationReport report)
        {
            if (clip == null)
            {
                return;
            }

            if (clip.legacy)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-LEGACY",
                    $"{role} Clip은 Legacy Animation입니다. 수동 Adapter 작업이 필요합니다.",
                    clip);
            }
        }

        private static void ValidateAbility(
            MonsterMakerAbilityDraft ability,
            int milestone,
            MonsterMakerValidationReport report,
            UnityEngine.Object context)
        {
            if (ability == null || string.IsNullOrWhiteSpace(ability.AbilityId) || !UsesSafeId(ability.AbilityId))
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-ASCENSION-ABILITY",
                    $"돌파 {milestone} Ability ID를 입력하세요.",
                    context);
                return;
            }

            if (ability.Mode == MonsterAbilityMode.AutoActive && string.IsNullOrWhiteSpace(ability.TriggerPolicyId))
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-ABILITY-TRIGGER",
                    $"돌파 {milestone} Active는 명시적인 Trigger Policy ID가 필요합니다.",
                    context);
            }
        }

        private static void ValidateCatalogIdentityOwnership(
            MonsterMakerDraft draft,
            MonsterMakerValidationReport report)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(MonsterMakerAssetWriter.MonsterCatalogPath);
            if (catalog == null || !catalog.TryGet(draft.MonsterId, out var registered))
            {
                return;
            }

            var expectedDraftPath = MonsterMakerAssetWriter.BuildDraftPath(draft.MonsterId);
            var expectedDefinitionPath = MonsterMakerAssetWriter.BuildPaths(draft.MonsterId)[0];
            var ownsRegisteredMonster = EditorUtility.IsPersistent(draft) &&
                                        string.Equals(
                                            AssetDatabase.GetAssetPath(draft),
                                            expectedDraftPath,
                                            StringComparison.OrdinalIgnoreCase) &&
                                        string.Equals(
                                            AssetDatabase.GetAssetPath(registered),
                                            expectedDefinitionPath,
                                            StringComparison.OrdinalIgnoreCase);
            if (!ownsRegisteredMonster)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-ID-CATALOG",
                    "게임 Catalog에 이미 같은 ID의 다른 Monster가 있습니다. 기존 항목을 열거나 새 ID를 사용하세요.",
                    registered);
            }
        }

        internal static bool UsesSafeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var isAsciiLetter = character >= 'A' && character <= 'Z' || character >= 'a' && character <= 'z';
                var isAsciiDigit = character >= '0' && character <= '9';
                if (!isAsciiLetter && !isAsciiDigit && character != '_' && character != '-')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
