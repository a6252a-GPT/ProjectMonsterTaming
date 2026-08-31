using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Contents.CastleRaidHex;
using ProjectMT.Features.MainBattle;
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
            return Validate(draft, draft);
        }

        internal static MonsterMakerValidationReport Validate(
            MonsterMakerDraft draft,
            MonsterMakerDraft catalogIdentityOwner)
        {
            var report = new MonsterMakerValidationReport();
            if (draft == null)
            {
                report.Add(MonsterMakerIssueSeverity.Error, "MAKER-DRAFT", "Monster Maker 제작 원본이 없습니다.", null);
                return report;
            }

            ValidateIdentity(draft, catalogIdentityOwner ?? draft, report);
            ValidateSkills(draft, report);
            ValidateBody(draft, report);
            ValidateStats(draft, report);
            ValidateMotions(draft, report);
            ValidateCombat(draft, report);
            ValidateBasicAttackVfx(draft, report);
            ValidateMainBattleAI(draft, report);
            ValidateCastleRaidAI(draft, report);
            ValidateAscension(draft, report);
            ValidateFeedback(draft, report);
            return report;
        }

        public static MonsterMakerValidationReport ValidateActiveAttack(MonsterMakerDraft draft)
        {
            var report = new MonsterMakerValidationReport();
            if (draft == null)
            {
                report.Add(MonsterMakerIssueSeverity.Error, "MAKER-DRAFT", "Monster Maker 제작 원본이 없습니다.", null);
                return report;
            }
            if (!draft.UseActiveSkill)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-ACTIVE-DISABLED",
                    "액티브 스킬을 반영하려면 액티브 사용을 켜세요.",
                    draft);
                return report;
            }
            if (draft.Rarity < MonsterRarity.Legendary)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-SKILL-ACTIVE-RARITY",
                    "공격 액티브는 전설·신화 몬스터만 사용할 수 있습니다.",
                    draft);
                return report;
            }
            if (draft.ActiveAttackProfile == null)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-SKILL-ACTIVE-PENDING",
                    "반영할 공격 액티브 프로필이 없습니다.",
                    draft);
                return report;
            }
            ValidateActiveAttackAuthoring(draft, report);
            return report;
        }

        private static void ValidateBasicAttackVfx(
            MonsterMakerDraft draft,
            MonsterMakerValidationReport report)
        {
            var profile = draft.BasicAttackProfile;
            if (profile == null)
            {
                return;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var binding in draft.BasicAttackVfxBindings)
            {
                if (binding == null)
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-BASIC-VFX-BINDING",
                        "기본공격 연출 배정 데이터가 비어 있습니다.",
                        draft);
                    continue;
                }
                if (!binding.TryValidate(out var bindingError))
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-BASIC-VFX-BINDING",
                        bindingError,
                        draft);
                    continue;
                }
                var key = $"{binding.AttackId}|{binding.SlotId}|{binding.MotionId}";
                if (!seen.Add(key))
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-BASIC-VFX-DUPLICATE",
                        $"기본공격 연출 배정 키가 중복됩니다: {key}",
                        draft);
                }
            }

            foreach (var slot in profile.VfxSlots)
            {
                if (slot == null)
                {
                    continue;
                }
                var motionIds = slot.AssignmentScope == MonsterBasicAttackVfxAssignmentScope.MotionSpecific
                    ? draft.Attacks.Where(attack => attack != null)
                        .Select(attack => attack.MotionId)
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                    : new[] { string.Empty };
                foreach (var motionId in motionIds)
                {
                    var binding = draft.BasicAttackVfxBindings.LastOrDefault(candidate =>
                        candidate != null &&
                        string.Equals(candidate.AttackId, profile.AttackId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(candidate.SlotId, slot.SlotId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(candidate.MotionId, motionId, StringComparison.OrdinalIgnoreCase));
                    if (binding == null || binding.State == MonsterBasicAttackVfxAssignmentState.Undecided)
                    {
                        report.Add(
                            MonsterMakerIssueSeverity.Warning,
                            "MAKER-BASIC-VFX-PENDING",
                            $"기본공격 VFX가 미결정입니다: {slot.DisplayName}" +
                            (string.IsNullOrWhiteSpace(motionId) ? string.Empty : $" / {motionId}"),
                            draft);
                    }
                    else if (binding.State == MonsterBasicAttackVfxAssignmentState.Assigned &&
                             binding.Prefab == null)
                    {
                        report.Add(
                            MonsterMakerIssueSeverity.Error,
                            "MAKER-BASIC-VFX-PREFAB",
                            $"배정 상태인 기본공격 VFX Prefab이 비어 있습니다: {slot.DisplayName}",
                            draft);
                    }

                    if (binding == null ||
                        binding.SfxState == MonsterBasicAttackSfxAssignmentState.Undecided)
                    {
                        report.Add(
                            MonsterMakerIssueSeverity.Warning,
                            "MAKER-BASIC-SFX-PENDING",
                            $"기본공격 SFX 사용 여부가 미결정입니다: {slot.DisplayName}" +
                            (string.IsNullOrWhiteSpace(motionId) ? string.Empty : $" / {motionId}"),
                            draft);
                    }
                    else if (binding.SfxState == MonsterBasicAttackSfxAssignmentState.Assigned &&
                             binding.Sound == null)
                    {
                        report.Add(
                            MonsterMakerIssueSeverity.Error,
                            "MAKER-BASIC-SFX-CLIP",
                            $"SFX 사용 상태이지만 AudioClip이 비어 있습니다: {slot.DisplayName}",
                            draft);
                    }
                }
            }

            var draftPath = AssetDatabase.GetAssetPath(draft);
            if (!string.IsNullOrWhiteSpace(draftPath) &&
                draftPath.StartsWith(MonsterMakerAssetWriter.DraftRoot + "/", StringComparison.Ordinal))
            {
                var paths = MonsterMakerAssetWriter.BuildPaths(draft.MonsterId);
                var combat = AssetDatabase.LoadAssetAtPath<MonsterCombatProfile>(paths[3]);
                var feedback = AssetDatabase.LoadAssetAtPath<MonsterFeedbackProfile>(paths[5]);
                var syncState = MonsterBasicAttackBindingProjection.EvaluateRuntimeSync(
                    draft,
                    combat,
                    feedback,
                    out var syncMessage);
                if (syncState != MonsterBasicAttackRuntimeSyncState.Synchronized)
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Warning,
                        "MAKER-BASIC-RUNTIME-SYNC",
                        $"기본공격 변경사항이 게임 자산과 다릅니다. {syncMessage}",
                        draft);
                }
            }
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

        private static void ValidateIdentity(
            MonsterMakerDraft draft,
            MonsterMakerDraft catalogIdentityOwner,
            MonsterMakerValidationReport report)
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
                ValidateCatalogIdentityOwnership(draft, catalogIdentityOwner, report);
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

        private static void ValidateCastleRaidAI(
            MonsterMakerDraft draft,
            MonsterMakerValidationReport report)
        {
            if (draft.CastleRaidAiPattern != HexCastleAssaultPattern.TacticalSupport)
            {
                return;
            }

            if (draft.CastleRaidSupportRange < 1f || draft.CastleRaidSupportCooldown <= 0f ||
                draft.CastleRaidSupportDuration <= 0f || draft.CastleRaidHealRatio < 0f ||
                draft.CastleRaidHealRatio > 1f || draft.CastleRaidAttackBuffRate < 0f ||
                draft.CastleRaidAttackBuffRate > 1f || draft.CastleRaidDefenseDamageMultiplier < 0.05f ||
                draft.CastleRaidDefenseDamageMultiplier > 1f)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-CASTLE-AI",
                    "군단의 역습 지원형 범위·재사용·지속·효과 비율이 유효하지 않습니다.",
                    draft);
            }
        }

        private static void ValidateSkills(MonsterMakerDraft draft, MonsterMakerValidationReport report)
        {
            if (draft.UsePassiveSkill)
            {
                var catalog = AssetDatabase.LoadAssetAtPath<MonsterSkillCatalog>(
                    MonsterSkillCatalog.DefaultAssetPath);
                var catalogError = string.Empty;
                if (catalog == null || !catalog.TryValidate(out catalogError))
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-SKILL-CATALOG",
                        "범용 Monster Skill Catalog이 없거나 유효하지 않습니다. " + catalogError,
                        catalog);
                }
                else if (draft.RarityPassiveSkill == null)
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-SKILL-PASSIVE-MISSING",
                        "패시브 사용이 켜져 있지만 패시브가 선택되지 않았습니다.",
                        draft);
                }
                else if (!draft.RarityPassiveSkill.TryValidate(out var passiveError))
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-SKILL-PASSIVE-INVALID",
                        passiveError,
                        draft.RarityPassiveSkill);
                }
                else if (!catalog.Contains(draft.RarityPassiveSkill))
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-SKILL-PASSIVE-UNREGISTERED",
                        "선택한 패시브가 Monster Skill Catalog에 등록되지 않았습니다.",
                        draft.RarityPassiveSkill);
                }
                else if (!draft.RarityPassiveSkill.AuthoringEnabled)
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-SKILL-PASSIVE-DISABLED",
                        "선택한 패시브는 현재 P0 고도화 대상이 아니어서 Monster Maker에서 비활성 상태입니다.",
                        draft.RarityPassiveSkill);
                }

                if (draft.RarityPassiveSkill is GenericMonsterPassiveSkill genericPassive)
                {
                    var tuningError = "몬스터 전용 패시브 수치가 없습니다.";
                    if (draft.PassiveTuning == null ||
                        !draft.PassiveTuning.TryValidate(genericPassive, out tuningError))
                    {
                        report.Add(
                            MonsterMakerIssueSeverity.Error,
                            "MAKER-SKILL-PASSIVE-TUNING",
                            tuningError,
                            draft);
                    }
                }
            }

            if (!draft.UseActiveSkill)
            {
                return;
            }

            var active = draft.RarityActiveSkill;
            if (draft.Rarity < MonsterRarity.Legendary)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-SKILL-ACTIVE-RARITY",
                    "액티브 사용은 전설·신화 몬스터만 켤 수 있습니다.",
                    active != null ? active :
                    draft.ActiveAttackProfile != null ? draft.ActiveAttackProfile :
                    draft.ActiveEffectProfile != null ? draft.ActiveEffectProfile : draft);
                return;
            }

            if (!draft.HasActiveProfile)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-SKILL-ACTIVE-PENDING",
                    "액티브 사용이 켜져 있지만 공격형 또는 효과형 액티브 프로필이 없습니다.",
                    draft);
                return;
            }
            if (draft.ActiveAttackProfile != null && draft.ActiveEffectProfile != null)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-SKILL-ACTIVE-DUPLICATE",
                    "공격형과 효과형 액티브를 동시에 선택할 수 없습니다.",
                    draft);
                return;
            }

            if (draft.ActiveEffectProfile != null) ValidateActiveEffectAuthoring(draft, report);
            else ValidateActiveAttackAuthoring(draft, report);
        }
        private static void ValidateActiveAttackAuthoring(
            MonsterMakerDraft draft,
            MonsterMakerValidationReport report)
        {
            var profile = draft.ActiveAttackProfile;
            if (!profile.TryValidate(out var profileError))
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-ACTIVE-PROFILE",
                    profileError,
                    profile);
            }

            if (string.IsNullOrWhiteSpace(draft.ActiveSkillName))
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-ACTIVE-NAME",
                    "이 몬스터가 사용할 고유 액티브 스킬 이름을 입력하세요.",
                    draft);
            }

            if (draft.ActiveEnergyMaximum < 1)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-ACTIVE-ENERGY",
                    "액티브 최대 기력은 1 이상이어야 합니다.",
                    draft);
            }

            var profileStepIds = new HashSet<string>(
                profile.Steps.Select(step => step.StepId),
                StringComparer.OrdinalIgnoreCase);
            var tuningIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tuning in draft.ActiveAttackStepTunings)
            {
                var tuningError = "액티브 Step 튜닝이 비어 있습니다.";
                if (tuning == null || !tuning.TryValidate(out tuningError))
                {
                    report.Add(MonsterMakerIssueSeverity.Error, "MAKER-ACTIVE-TUNING", tuningError, draft);
                    continue;
                }
                if (!profileStepIds.Contains(tuning.StepId) || !tuningIds.Add(tuning.StepId))
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-ACTIVE-TUNING-ID",
                        $"프로필과 일치하지 않거나 중복된 Step 튜닝입니다. Step={tuning.StepId}",
                        draft);
                }
            }
            if (tuningIds.Count != profile.Steps.Count)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-ACTIVE-TUNING-COUNT",
                    "프로필이 변경되었습니다. 액티브 섹션에서 Step을 다시 동기화하세요.",
                    draft);
            }

            var presentationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var presentation in draft.ActiveAttackPresentations)
            {
                if (presentation == null || !profileStepIds.Contains(presentation.StepId) ||
                    !presentationIds.Add(presentation.StepId))
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-ACTIVE-PRESENTATION-ID",
                        $"프로필과 일치하지 않거나 중복된 Step 연출 연결입니다. Step={presentation?.StepId}",
                        draft);
                    continue;
                }
                var sourceStep = profile.Steps.FirstOrDefault(step =>
                    step != null && string.Equals(
                        step.StepId,
                        presentation.StepId,
                        StringComparison.OrdinalIgnoreCase));
                if (sourceStep == null)
                {
                    continue;
                }
                if (draft.UseCustomActiveStepMotions && presentation.MotionClip == null)
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-ACTIVE-STEP-MOTION",
                        $"액티브 스텝 [{presentation.StepId}]의 공격 모션 Clip을 지정하세요.",
                        draft);
                }
                else if (draft.UseCustomActiveStepMotions)
                {
                    ValidateClipRig(presentation.MotionClip, $"Active/{presentation.StepId}", report);
                }
                if (draft.UseCustomActiveStepMotions &&
                    (!IsFinitePositive(presentation.MotionPlaybackSpeed) ||
                    !IsFiniteNonNegative(presentation.MotionCrossFadeDuration) ||
                    !IsFiniteInRange(presentation.MotionCommitNormalizedTime, 0f, 1f)))
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-ACTIVE-STEP-MOTION-TUNING",
                        $"액티브 스텝 [{presentation.StepId}]의 재생 속도·전환 시간·판정 시작 시점이 유효하지 않습니다.",
                        draft);
                }
                ValidateFeedbackCue(presentation.Telegraph, $"{presentation.StepId} 예고", report, draft);
                ValidateFeedbackCue(presentation.Launch, $"{presentation.StepId} 발동", report, draft);
                ValidateFeedbackCue(presentation.Travel, $"{presentation.StepId} 이동", report, draft);
                ValidateFeedbackCue(presentation.Impact, $"{presentation.StepId} 타격", report, draft);
                ValidateFeedbackCue(presentation.TeleportExit, $"{presentation.StepId} 순간이동 출발", report, draft);
                ValidateFeedbackCue(presentation.TeleportEnter, $"{presentation.StepId} 순간이동 도착", report, draft);
                var resolvedSlotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var slotIndex = 0; slotIndex < sourceStep.PresentationSlots.Count; slotIndex++)
                {
                    var contract = sourceStep.PresentationSlots[slotIndex];
                    if (contract == null) continue;
                    var slot = presentation.ResolveSlot(contract.SlotId);
                    if (slot == null || !resolvedSlotIds.Add(contract.SlotId))
                    {
                        report.Add(
                            MonsterMakerIssueSeverity.Error,
                            "MAKER-ACTIVE-SLOT-MISSING",
                            $"액티브 Step [{presentation.StepId}]의 공간 [{contract.DisplayName}] 연결이 없습니다.",
                            draft);
                        continue;
                    }
                    ValidateActivePresentationSlot(
                        presentation.StepId,
                        contract,
                        slot,
                        report,
                        draft);
                }
                if (presentation.Slots.Count != sourceStep.PresentationSlots.Count)
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-ACTIVE-SLOT-COUNT",
                        $"액티브 Step [{presentation.StepId}]의 현재 공간 수가 프로필 계약과 다릅니다.",
                        draft);
                }
            }
            if (presentationIds.Count != profile.Steps.Count)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-ACTIVE-PRESENTATION-COUNT",
                    "프로필이 변경되었습니다. 액티브 연출 연결을 다시 동기화하세요.",
                    draft);
            }

            if (draft.RarityActiveSkill != null && draft.RarityActiveSkill is not MonsterAttackActiveSkill)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Warning,
                    "MAKER-ACTIVE-LEGACY",
                    "기존 액티브 참조는 전투 반영 시 새 공격 액티브 에셋으로 교체됩니다.",
                    draft.RarityActiveSkill);
            }
        }

        private static void ValidateActiveEffectAuthoring(
            MonsterMakerDraft draft,
            MonsterMakerValidationReport report)
        {
            var profile = draft.ActiveEffectProfile;
            if (!profile.TryValidate(out var profileError))
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-EFFECT-ACTIVE-PROFILE",
                    profileError,
                    profile);
            }
            if (string.IsNullOrWhiteSpace(draft.ActiveSkillName))
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-ACTIVE-NAME",
                    "이 몬스터가 사용할 고유 액티브 스킬 이름을 입력하세요.",
                    draft);
            }
            if (draft.ActiveEnergyMaximum < 1)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-ACTIVE-ENERGY",
                    "액티브 최대 기력은 1 이상이어야 합니다.",
                    draft);
            }

            var groupIds = new HashSet<string>(
                profile.Groups.Select(group => group.GroupId),
                StringComparer.OrdinalIgnoreCase);
            var presentationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var presentation in draft.ActiveEffectPresentations)
            {
                if (presentation == null || !groupIds.Contains(presentation.StepId) ||
                    !presentationIds.Add(presentation.StepId))
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-EFFECT-ACTIVE-PRESENTATION-ID",
                        $"효과 묶음과 일치하지 않거나 중복된 연출 연결입니다: {presentation?.StepId}",
                        draft);
                    continue;
                }
                var group = profile.Groups.First(candidate =>
                    string.Equals(candidate.GroupId, presentation.StepId, StringComparison.OrdinalIgnoreCase));
                if (draft.UseCustomActiveStepMotions && presentation.MotionClip == null)
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-EFFECT-ACTIVE-MOTION",
                        $"전용 모션을 사용하지만 Clip이 비어 있습니다: {group.DisplayName}",
                        draft);
                }
                for (var slotIndex = 0; slotIndex < group.PresentationSlots.Count; slotIndex++)
                {
                    var contract = group.PresentationSlots[slotIndex];
                    var slot = presentation.ResolveSlot(contract.SlotId);
                    if (slot == null)
                    {
                        report.Add(
                            MonsterMakerIssueSeverity.Error,
                            "MAKER-EFFECT-ACTIVE-SLOT",
                            $"효과형 VFX/SFX 연결이 비어 있습니다: {group.GroupId}/{contract.SlotId}",
                            draft);
                        continue;
                    }
                    ValidateActivePresentationSlot(
                        group.GroupId,
                        contract,
                        slot,
                        report,
                        draft);
                }
            }
            if (presentationIds.Count != profile.Groups.Count)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-EFFECT-ACTIVE-PRESENTATION-COUNT",
                    "프로필이 변경되었습니다. 효과형 액티브 연출 연결을 다시 동기화하세요.",
                    draft);
            }
            if (draft.RarityActiveSkill != null && draft.RarityActiveSkill is not MonsterEffectActiveSkill)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Warning,
                    "MAKER-EFFECT-ACTIVE-LEGACY",
                    "기존 액티브 참조는 전투 반영 시 새 효과형 액티브 에셋으로 교체됩니다.",
                    draft.RarityActiveSkill);
            }
        }
        private static void ValidateActivePresentationSlot(
            string stepId,
            MonsterActivePresentationSlot contract,
            MonsterMakerActivePresentationSlotDraft slot,
            MonsterMakerValidationReport report,
            MonsterMakerDraft draft)
        {
            var label = $"{stepId} / {contract.DisplayName}";
            if (slot.VfxState == MonsterBasicAttackVfxAssignmentState.Undecided)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Warning,
                    "MAKER-ACTIVE-VFX-PENDING",
                    $"액티브 VFX 사용 여부가 미결정입니다: {label}",
                    draft);
            }
            else if (slot.VfxState == MonsterBasicAttackVfxAssignmentState.Assigned)
            {
                if (slot.Feedback.VfxPrefab == null)
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-ACTIVE-VFX-MISSING",
                        $"액티브 VFX 사용 상태이지만 Prefab이 비어 있습니다: {label}",
                        draft);
                }
                else if (!IsFinitePositive(slot.Feedback.VfxLifetime) ||
                         !IsFinitePositive(slot.Feedback.Scale))
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-ACTIVE-VFX-TUNING",
                        $"액티브 VFX 수명·크기 값이 유효하지 않습니다: {label}",
                        draft);
                }
            }

            if (slot.SfxState == MonsterBasicAttackSfxAssignmentState.Undecided)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Warning,
                    "MAKER-ACTIVE-SFX-PENDING",
                    $"액티브 SFX 사용 여부가 미결정입니다: {label}",
                    draft);
            }
            else if (slot.SfxState == MonsterBasicAttackSfxAssignmentState.Assigned &&
                     !slot.Feedback.HasSound)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-ACTIVE-SFX-MISSING",
                    $"액티브 SFX 사용 상태이지만 AudioClip이 비어 있습니다: {label}",
                    draft);
            }
        }

        private static void ValidateMainBattleAI(
            MonsterMakerDraft draft,
            MonsterMakerValidationReport report)
        {
            if (!Enum.IsDefined(typeof(MonsterImpactStrength), draft.ImpactStrength) ||
                !Enum.IsDefined(typeof(MonsterReactionWeight), draft.ReactionWeight) ||
                !Enum.IsDefined(typeof(MainBattleMonsterRole), draft.MainBattleRole) ||
                !Enum.IsDefined(typeof(UnitTargetPriority), draft.MainBattleTargetPriority))
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-MAIN-AI-ENUM",
                    "공격 무게·피격 체급 또는 MainBattle 역할 AI 분류가 유효하지 않습니다.",
                    draft);
                return;
            }

            if (draft.MainBattlePreferredRangeRatio < 0.2f ||
                draft.MainBattlePreferredRangeRatio > 1f ||
                draft.MainBattleRetreatRangeRatio < 0f ||
                draft.MainBattleRetreatRangeRatio >= draft.MainBattlePreferredRangeRatio ||
                draft.MainBattleRetargetInterval < 0.08f ||
                draft.MainBattleRetargetInterval > 1f)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-MAIN-AI-RANGE",
                    "MainBattle 후퇴 거리는 희망 거리보다 짧아야 하며 재탐색 값은 0.08~1초여야 합니다.",
                    draft);
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
                if (attack.Markers.Count != 1)
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-BASIC-ATTACK-MARKER",
                        $"Attack {attack.MotionId}은 기본공격 Recipe를 시작하는 Marker가 정확히 1개여야 합니다.",
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
            var basicAttack = draft.BasicAttackProfile;
            if (basicAttack == null)
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-BASIC-ATTACK",
                    "15종 공용 기본공격 프로필 중 하나를 선택해야 합니다.",
                    draft);
            }
            else
            {
                if (!basicAttack.TryValidate(out var basicAttackError))
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-BASIC-ATTACK",
                        basicAttackError,
                        basicAttack);
                }

                if (draft.CombatType != basicAttack.CombatType)
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-BASIC-ATTACK-TYPE",
                        $"제작 원본 공격 종류와 기본공격 프로필이 다릅니다. Source={draft.CombatType}, Profile={basicAttack.CombatType}",
                        draft);
                }

                if (basicAttack.CombatType == MonsterCombatType.Ranged)
                {
                    if (draft.ProjectileLaunchRecoilDistance < 0f || draft.ProjectileLaunchRecoilDuration <= 0f)
                    {
                        report.Add(
                            MonsterMakerIssueSeverity.Error,
                            "MAKER-PROJECTILE-RECOIL",
                            "원거리 발사 반동은 0 이상의 거리와 양수 시간이 필요합니다.",
                            draft);
                    }

                    if (basicAttack.UsesProjectileVisual)
                    {
                        var projectileVisual = draft.ProjectilePrefab;
                        if (projectileVisual == null)
                        {
                            projectileVisual = AssetDatabase.LoadAssetAtPath<GameObject>(
                                MonsterMakerAssetWriter.DefaultProjectilePrefabPath);
                        }
                        if (projectileVisual == null ||
                            draft.ResolvedProjectileSpeed <= 0f || draft.ResolvedProjectileLifetime <= 0f)
                        {
                            report.Add(
                                MonsterMakerIssueSeverity.Error,
                                "MAKER-PROJECTILE",
                                "투사체형 기본공격은 투사체 VFX 또는 공용 임시 구슬과 양수 속도·수명이 필요합니다.",
                                draft.ProjectilePrefab);
                        }

                        var requiredTravelDistance = basicAttack.ProjectileTravel switch
                        {
                            MonsterBasicAttackProjectileTravel.Homing => draft.AttackRange,
                            MonsterBasicAttackProjectileTravel.Returning => draft.AttackRange * 2f,
                            _ => basicAttack.ResolveRange(draft.AttackRange)
                        };
                        var travelCapacity = draft.ResolvedProjectileSpeed * draft.ResolvedProjectileLifetime;
                        if (travelCapacity + 0.001f < requiredTravelDistance)
                        {
                            report.Add(
                                MonsterMakerIssueSeverity.Error,
                                "MAKER-PROJECTILE-RANGE",
                                $"투사체 속도×수명({travelCapacity:0.##}m)이 필요한 최대 이동 거리({requiredTravelDistance:0.##}m)보다 짧습니다.",
                                draft);
                        }
                    }
                }

                return;
            }

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
                    if (draft.ProjectileLaunchRecoilDistance < 0f || draft.ProjectileLaunchRecoilDuration <= 0f)
                    {
                        report.Add(
                            MonsterMakerIssueSeverity.Error,
                            "MAKER-PROJECTILE-RECOIL",
                            "원거리 발사 반동은 0 이상의 거리와 양수 시간이 필요합니다.",
                            draft);
                    }

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

            if (draft.UsePassiveSkill)
            {
                ValidateAbility(draft.Ascension2, 2, MonsterSkillAugmentTarget.Passive, report, draft);
                if (draft.RarityPassiveSkill == null)
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-ASCENSION-PASSIVE-TARGET",
                        "2돌파 강화 대상인 패시브를 먼저 선택하세요.",
                        draft);
                }
            }
            else
            {
                ValidateLegacyAbility(draft.Ascension2, 2, report, draft);
            }

            if (draft.UseActiveSkill && draft.Rarity >= MonsterRarity.Legendary)
            {
                ValidateAbility(draft.Ascension4, 4, MonsterSkillAugmentTarget.Active, report, draft);
                if (!draft.HasActiveProfile)
                {
                    report.Add(
                        MonsterMakerIssueSeverity.Error,
                        "MAKER-ASCENSION-ACTIVE-TARGET",
                        "4돌파 강화 대상인 액티브를 먼저 선택하세요.",
                        draft);
                }
            }
            else if (draft.UsePassiveSkill)
            {
                ValidateAbility(draft.Ascension4, 4, MonsterSkillAugmentTarget.Passive, report, draft);
            }
            else
            {
                ValidateLegacyAbility(draft.Ascension4, 4, report, draft);
            }
        }

        private static void ValidateFeedback(MonsterMakerDraft draft, MonsterMakerValidationReport report)
        {
            ValidateFeedbackCue(draft.SpawnFeedback, "생성", report, draft);
            ValidateFeedbackCue(draft.HitFeedback, "피격", report, draft);
            ValidateFeedbackCue(draft.DeathFeedback, "사망", report, draft);
            ValidateFeedbackCue(draft.SpecialFeedback, "특수", report, draft);
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
            MonsterSkillAugmentTarget target,
            MonsterMakerValidationReport report,
            UnityEngine.Object context)
        {
            if (ability == null || string.IsNullOrWhiteSpace(ability.AbilityId) || !UsesSafeId(ability.AbilityId))
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-ASCENSION-ABILITY",
                    $"돌파 {milestone} 스킬 강화 ID를 입력하세요.",
                    context);
                return;
            }

            if (!Enum.IsDefined(typeof(MonsterSkillAugmentOperation), ability.AugmentOperation))
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-ASCENSION-AUGMENT",
                    $"돌파 {milestone}의 스킬 강화 방식이 유효하지 않습니다.",
                    context);
                return;
            }

            var usesScalar = ability.AugmentOperation == MonsterSkillAugmentOperation.MagnitudeMultiplier ||
                             ability.AugmentOperation == MonsterSkillAugmentOperation.DurationBonusSeconds ||
                             ability.AugmentOperation == MonsterSkillAugmentOperation.CooldownReductionRate;
            var scalarInvalid = ability.AugmentScalarValue <= 0f ||
                                (ability.AugmentOperation == MonsterSkillAugmentOperation.CooldownReductionRate &&
                                 ability.AugmentScalarValue >= 1f);
            if ((usesScalar && scalarInvalid) || (!usesScalar && ability.AugmentIntegerValue > 5))
            {
                report.Add(
                    MonsterMakerIssueSeverity.Error,
                    "MAKER-ASCENSION-AUGMENT-VALUE",
                    $"돌파 {milestone}의 {target} 강화값이 유효하지 않습니다.",
                    context);
            }
        }

        private static void ValidateLegacyAbility(
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
            MonsterMakerDraft catalogIdentityOwner,
            MonsterMakerValidationReport report)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(MonsterMakerAssetWriter.MonsterCatalogPath);
            if (catalog == null || !catalog.TryGet(draft.MonsterId, out var registered))
            {
                return;
            }

            var expectedDraftPath = MonsterMakerAssetWriter.BuildDraftPath(draft.MonsterId);
            var expectedDefinitionPath = MonsterMakerAssetWriter.BuildPaths(draft.MonsterId)[0];
            var ownsRegisteredMonster = EditorUtility.IsPersistent(catalogIdentityOwner) &&
                                        string.Equals(
                                            AssetDatabase.GetAssetPath(catalogIdentityOwner),
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

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private static bool IsFiniteInRange(float value, float minimum, float maximum)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
        }
    }
}
