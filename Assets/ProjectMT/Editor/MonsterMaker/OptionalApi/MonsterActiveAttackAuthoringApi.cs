using System;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker.OptionalApi
{
    [Serializable]
    public sealed class MonsterActiveAttackAuthoringRequest
    {
        public string profileId = "active_custom_01";
        public string displayName = "새 공격 액티브";
        public string description = string.Empty;
        public string impactFeelPrefabPath = string.Empty;
        public MonsterActiveAttackStepRequest[] steps = Array.Empty<MonsterActiveAttackStepRequest>();
    }

    [Serializable]
    public sealed class MonsterActiveAttackStepRequest
    {
        public string stepId = "step_01";
        public string displayName = "일자 피해";
        public float delayAfterPrevious;
        public string targetPolicy = "SameTarget";
        public bool teleportBeforeAttack;
        public float teleportFrontDistance = 1f;
        public string pattern = "Line";
        public string progression = "Instant";
        public float damageMultiplier = 1f;
        public int maxTargets = 8;
        public float range = 4f;
        public float width = 1.2f;
        public float radius = 1.8f;
        public float forwardOffset = 1.5f;
        public float angle = 70f;
        public float progressionDuration = 0.25f;
        public float telegraphDelay = 0.12f;
        public float visualDuration = 0.8f;
        public string projectileFormation = "Single";
        public int projectileCount = 1;
        public float projectileFanAngle = 50f;
        public float projectileSpeed = 10f;
        public float projectileCollisionRadius = 0.25f;
        public float explosionRadius = 1.8f;
        public string instantMagicTarget = "SingleTarget";
        public string magicDirection = "GroundUp";
        public MonsterActiveHitEffectRequest[] hitEffects;
        public MonsterActivePresentationSlotRequest[] presentationSlots;
    }

    [Serializable]
    public sealed class MonsterActiveHitEffectRequest
    {
        public string type = "Knockback";
        public float magnitude = 0.25f;
        public float duration = 0.35f;
        public float secondaryMagnitude;
        public float tickInterval = 0.5f;
    }

    [Serializable]
    public sealed class MonsterActivePresentationSlotRequest
    {
        public string slotId = "impact";
        public string displayName = "실제 타격";
        public string timing = "Impact";
        public string anchor = "TargetPoint";
        public string description = string.Empty;
        public bool useDuration;
        public float duration = 1f;
    }

    [Serializable]
    public sealed class MonsterActiveAttackAuthoringResult
    {
        public bool success;
        public string operation;
        public string profileId;
        public string assetPath;
        public string error;
    }

    [Serializable]
    internal sealed class MonsterActiveAttackAuthoringContract
    {
        public int schemaVersion = 1;
        public string scope = "한 번 호출할 때 공격 액티브 프로필 하나만 작성합니다.";
        public string[] operations;
        public string[] patterns;
        public string[] progressions;
        public string[] targetPolicies;
        public string[] projectileFormations;
        public string[] instantMagicTargets;
        public string[] magicDirections;
        public string[] hitEffectTypes;
        public string[] presentationEvents;
        public string[] presentationAnchors;
        public MonsterActiveAttackAuthoringRequest example;
    }

    public static class MonsterActiveAttackAuthoringApi // 폴더째 제거 가능한 AI용 단일 프리셋 어댑터
    {
        public static string GetContractJson()
        {
            var contract = new MonsterActiveAttackAuthoringContract
            {
                operations = new[] { "ValidateProfileJson", "CreateProfileFromJson", "UpdateProfileFromJson" },
                patterns = Enum.GetNames(typeof(MonsterActiveAttackPattern)),
                progressions = Enum.GetNames(typeof(MonsterActiveAttackProgression)),
                targetPolicies = Enum.GetNames(typeof(MonsterActiveTargetPolicy)),
                projectileFormations = Enum.GetNames(typeof(MonsterActiveProjectileFormation)),
                instantMagicTargets = Enum.GetNames(typeof(MonsterActiveInstantMagicTarget)),
                magicDirections = Enum.GetNames(typeof(MonsterActiveMagicDirection)),
                hitEffectTypes = Enum.GetNames(typeof(MonsterActiveHitEffectType)),
                presentationEvents = Enum.GetNames(typeof(MonsterActivePresentationEvent)),
                presentationAnchors = Enum.GetNames(typeof(MonsterActivePresentationAnchor)),
                example = CreateExampleRequest()
            };
            return JsonUtility.ToJson(contract, true);
        }

        public static string GetExampleJson() => JsonUtility.ToJson(CreateExampleRequest(), true);

        public static string ValidateProfileJson(string json)
        {
            if (!TryBuildProfile(json, out var profile, out var error))
            {
                return BuildResult(false, "validate", string.Empty, string.Empty, error);
            }
            try
            {
                var success = MonsterActiveAttackAuthoringService.TryValidate(profile, null, out error);
                return BuildResult(success, "validate", profile.ProfileId, string.Empty, error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        public static string CreateProfileFromJson(string json)
        {
            if (!TryBuildProfile(json, out var profile, out var error))
            {
                return BuildResult(false, "create", string.Empty, string.Empty, error);
            }
            try
            {
                var success = MonsterActiveAttackAuthoringService.TryCreate(
                    profile,
                    out var createdAsset,
                    out var assetPath,
                    out error);
                return BuildResult(success, "create", createdAsset != null ? createdAsset.ProfileId : profile.ProfileId,
                    assetPath, error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        public static string UpdateProfileFromJson(string assetPath, string json)
        {
            var target = string.IsNullOrWhiteSpace(assetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<MonsterActiveAttackProfile>(assetPath);
            if (target == null)
            {
                return BuildResult(false, "update", string.Empty, assetPath,
                    $"업데이트할 공격 액티브 프로필을 찾지 못했습니다: {assetPath}");
            }
            if (!TryBuildProfile(json, out var profile, out var error))
            {
                return BuildResult(false, "update", target.ProfileId, assetPath, error);
            }
            try
            {
                var success = MonsterActiveAttackAuthoringService.TryUpdate(profile, target, out error);
                return BuildResult(success, "update", target.ProfileId, assetPath, error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        private static bool TryBuildProfile(
            string json,
            out MonsterActiveAttackProfile profile,
            out string error)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "작성 요청 JSON이 비어 있습니다.";
                return false;
            }
            if (json.TrimStart().StartsWith("[", StringComparison.Ordinal))
            {
                error = "배열 요청은 지원하지 않습니다. 한 번에 프로필 하나만 작성하세요.";
                return false;
            }

            MonsterActiveAttackAuthoringRequest request;
            try
            {
                request = new MonsterActiveAttackAuthoringRequest();
                JsonUtility.FromJsonOverwrite(json, request);
            }
            catch (Exception exception)
            {
                error = $"작성 요청 JSON을 읽지 못했습니다: {exception.Message}";
                return false;
            }
            if (request.steps == null || request.steps.Length == 0)
            {
                error = "공격 액티브 Step을 하나 이상 입력하세요.";
                return false;
            }
            if (request.steps.Length > MonsterActiveAttackProfile.MaximumStepCount)
            {
                error = $"공격 액티브 Step은 최대 {MonsterActiveAttackProfile.MaximumStepCount}개입니다.";
                return false;
            }

            var steps = new List<MonsterActiveAttackStep>(request.steps.Length);
            for (var index = 0; index < request.steps.Length; index++)
            {
                if (!TryBuildStep(request.steps[index], index, out var step, out error)) return false;
                steps.Add(step);
            }

            profile = ScriptableObject.CreateInstance<MonsterActiveAttackProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            profile.EditorConfigure(request.profileId, request.displayName, request.description, steps);
            if (!string.IsNullOrWhiteSpace(request.impactFeelPrefabPath))
            {
                var feelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(request.impactFeelPrefabPath);
                if (feelPrefab == null)
                {
                    UnityEngine.Object.DestroyImmediate(profile);
                    profile = null;
                    error = $"FEEL Prefab을 찾지 못했습니다: {request.impactFeelPrefabPath}";
                    return false;
                }
                var feel = new BasicAttackFeelCue();
                feel.EditorConfigure(feelPrefab);
                profile.EditorSetImpactFeel(feel);
            }
            error = string.Empty;
            return true;
        }

        private static bool TryBuildStep(
            MonsterActiveAttackStepRequest request,
            int index,
            out MonsterActiveAttackStep step,
            out string error)
        {
            step = null;
            if (request == null)
            {
                error = $"Step {index + 1} 요청이 비어 있습니다.";
                return false;
            }
            if (!TryParseEnum(request.targetPolicy, $"Step {index + 1}.targetPolicy",
                    out MonsterActiveTargetPolicy targetPolicy, out error) ||
                !TryParseEnum(request.pattern, $"Step {index + 1}.pattern",
                    out MonsterActiveAttackPattern pattern, out error) ||
                !TryParseEnum(request.progression, $"Step {index + 1}.progression",
                    out MonsterActiveAttackProgression progression, out error))
            {
                return false;
            }
            if (!MonsterActiveAttackStep.SupportsProgression(pattern, progression))
            {
                error = $"Step {index + 1}의 {pattern} 형태는 {progression} 진행 방식을 지원하지 않습니다.";
                return false;
            }
            if (!TryBuildEffects(request.hitEffects, index, out var effects, out error)) return false;

            step = new MonsterActiveAttackStep();
            step.EditorConfigure(
                request.stepId,
                request.displayName,
                pattern,
                request.damageMultiplier,
                request.delayAfterPrevious,
                targetPolicy,
                progression,
                effects);
            step.EditorConfigureGeometry(
                PositiveOr(request.range, 4f),
                PositiveOr(request.width, 1.2f),
                PositiveOr(request.radius, 1.8f),
                Mathf.Max(0f, request.forwardOffset),
                PositiveOr(request.angle, 70f),
                request.maxTargets > 0 ? request.maxTargets : 8,
                Mathf.Max(0f, request.progressionDuration),
                Mathf.Max(0f, request.telegraphDelay),
                PositiveOr(request.visualDuration, 0.8f));
            step.EditorConfigureTeleport(request.teleportBeforeAttack, Mathf.Max(0f, request.teleportFrontDistance));

            if (pattern == MonsterActiveAttackPattern.PiercingProjectile ||
                pattern == MonsterActiveAttackPattern.ExplosiveProjectile)
            {
                if (!TryParseEnum(request.projectileFormation, $"Step {index + 1}.projectileFormation",
                        out MonsterActiveProjectileFormation formation, out error))
                {
                    return false;
                }
                var projectileCount = formation == MonsterActiveProjectileFormation.Single
                    ? 1
                    : request.projectileCount >= 2 ? request.projectileCount : 3;
                step.EditorConfigureProjectile(
                    formation,
                    projectileCount,
                    PositiveOr(request.projectileFanAngle, 50f),
                    PositiveOr(request.projectileSpeed, 10f),
                    PositiveOr(request.projectileCollisionRadius, 0.25f),
                    PositiveOr(request.explosionRadius, 1.8f));
            }
            if (pattern == MonsterActiveAttackPattern.InstantMagic)
            {
                if (!TryParseEnum(request.instantMagicTarget, $"Step {index + 1}.instantMagicTarget",
                        out MonsterActiveInstantMagicTarget magicTarget, out error) ||
                    !TryParseEnum(request.magicDirection, $"Step {index + 1}.magicDirection",
                        out MonsterActiveMagicDirection magicDirection, out error))
                {
                    return false;
                }
                step.EditorConfigureInstantMagic(magicTarget, magicDirection);
            }
            if (request.presentationSlots != null)
            {
                if (!TryBuildPresentationSlots(request.presentationSlots, index, out var slots, out error)) return false;
                step.EditorSetPresentationSlots(slots);
            }
            else
            {
                step.EditorSetPresentationSlots(MonsterActiveAttackVfxContractTemplates.Build(step));
            }
            error = string.Empty;
            return true;
        }

        private static bool TryBuildEffects(
            MonsterActiveHitEffectRequest[] requests,
            int stepIndex,
            out MonsterActiveHitEffect[] effects,
            out string error)
        {
            if (requests == null)
            {
                effects = Array.Empty<MonsterActiveHitEffect>();
                error = string.Empty;
                return true;
            }
            effects = new MonsterActiveHitEffect[requests.Length];
            for (var index = 0; index < requests.Length; index++)
            {
                var request = requests[index];
                if (request == null)
                {
                    error = $"Step {stepIndex + 1}.hitEffects[{index}] 요청이 비어 있습니다.";
                    return false;
                }
                if (!TryParseEnum(request.type, $"Step {stepIndex + 1}.hitEffects[{index}].type",
                        out MonsterActiveHitEffectType type, out error))
                {
                    return false;
                }
                var effect = new MonsterActiveHitEffect();
                effect.EditorConfigure(type, request.magnitude, request.duration,
                    request.secondaryMagnitude, PositiveOr(request.tickInterval, 0.5f));
                effects[index] = effect;
            }
            error = string.Empty;
            return true;
        }

        private static bool TryBuildPresentationSlots(
            MonsterActivePresentationSlotRequest[] requests,
            int stepIndex,
            out MonsterActivePresentationSlot[] slots,
            out string error)
        {
            slots = new MonsterActivePresentationSlot[requests.Length];
            for (var index = 0; index < requests.Length; index++)
            {
                var request = requests[index];
                if (request == null)
                {
                    error = $"Step {stepIndex + 1}.presentationSlots[{index}] 요청이 비어 있습니다.";
                    return false;
                }
                if (!TryParseEnum(request.timing, $"Step {stepIndex + 1}.presentationSlots[{index}].timing",
                        out MonsterActivePresentationEvent timing, out error) ||
                    !TryParseEnum(request.anchor, $"Step {stepIndex + 1}.presentationSlots[{index}].anchor",
                        out MonsterActivePresentationAnchor anchor, out error))
                {
                    return false;
                }
                var slot = new MonsterActivePresentationSlot();
                slot.EditorConfigure(
                    request.slotId,
                    request.displayName,
                    timing,
                    anchor,
                    request.description,
                    request.useDuration,
                    PositiveOr(request.duration, 1f));
                slots[index] = slot;
            }
            error = string.Empty;
            return true;
        }

        private static bool TryParseEnum<T>(string value, string field, out T result, out string error)
            where T : struct
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                Enum.TryParse(value.Trim(), true, out result) &&
                Enum.IsDefined(typeof(T), result))
            {
                error = string.Empty;
                return true;
            }
            result = default;
            error = $"{field} 값이 유효하지 않습니다: {value}. 허용값={string.Join(",", Enum.GetNames(typeof(T)))}";
            return false;
        }

        private static float PositiveOr(float value, float fallback) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f ? value : fallback;

        private static string BuildResult(
            bool success,
            string operation,
            string profileId,
            string assetPath,
            string error)
        {
            return JsonUtility.ToJson(new MonsterActiveAttackAuthoringResult
            {
                success = success,
                operation = operation,
                profileId = profileId ?? string.Empty,
                assetPath = assetPath ?? string.Empty,
                error = error ?? string.Empty
            }, true);
        }

        private static MonsterActiveAttackAuthoringRequest CreateExampleRequest()
        {
            return new MonsterActiveAttackAuthoringRequest
            {
                profileId = "active_example",
                displayName = "예시 액티브",
                description = "한 번에 프로필 하나를 작성하는 예시입니다.",
                steps = new[]
                {
                    new MonsterActiveAttackStepRequest
                    {
                        stepId = "step_01",
                        displayName = "전방 분쇄",
                        pattern = "Line",
                        progression = "Forward",
                        damageMultiplier = 1.4f,
                        hitEffects = new[]
                        {
                            new MonsterActiveHitEffectRequest
                            {
                                type = "Airborne",
                                magnitude = 0.6f,
                                duration = 0.45f
                            }
                        },
                        presentationSlots = new[]
                        {
                            new MonsterActivePresentationSlotRequest
                            {
                                slotId = "telegraph",
                                displayName = "판정 예고",
                                timing = "Telegraph",
                                anchor = "TargetPoint"
                            },
                            new MonsterActivePresentationSlotRequest
                            {
                                slotId = "launch",
                                displayName = "공격 발동",
                                timing = "Launch",
                                anchor = "AttackOrigin"
                            },
                            new MonsterActivePresentationSlotRequest()
                        }
                    }
                }
            };
        }
    }
}
