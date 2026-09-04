using System;
using System.Collections.Generic;
using ProjectMT.Features.CommanderSkill;
using ProjectMT.Shared.Unit;

namespace ProjectMT.EditorTools.CommanderSkillWorkshop
{
    internal sealed class CommanderSkillWorkshopValidation
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public bool IsValid => Errors.Count == 0;
    }

    internal static class CommanderSkillWorkshopValidator // 실제 스킬 자산 저장 전 제작값 검사
    {
        public static CommanderSkillWorkshopValidation Validate(CommanderSkillWorkshopDraft draft)
        {
            var result = new CommanderSkillWorkshopValidation();
            if (draft == null)
            {
                result.Errors.Add("편집 중인 스킬이 없습니다.");
                return result;
            }

            if (!UsesSafeId(draft.SkillId))
            {
                result.Errors.Add("스킬 ID는 영문 소문자·숫자·밑줄만 사용해야 합니다.");
            }
            else if (!draft.SkillId.StartsWith("commander_skill_", StringComparison.Ordinal))
            {
                result.Warnings.Add("운영 검색을 위해 ID 앞에 commander_skill_ 사용을 권장합니다.");
            }

            if (string.IsNullOrWhiteSpace(draft.DisplayName))
            {
                result.Errors.Add("표시 이름이 비어 있습니다.");
            }
            if (!IsFiniteNonNegative(draft.CastTime))
            {
                result.Errors.Add("캐스팅 시간은 0 이상의 유한한 값이어야 합니다.");
            }
            if (!IsFinitePositive(draft.Cooldown) || draft.Cooldown < 0.1f)
            {
                result.Errors.Add("쿨타임은 0.1초 이상이어야 합니다.");
            }
            if (!IsFinitePositive(draft.TargetRange) || draft.TargetRange < 1f)
            {
                result.Errors.Add("대상 탐색 거리는 1m 이상이어야 합니다.");
            }

            if (draft.Category == CommanderSkillCategory.Attack)
            {
                ValidateAttack(draft, result);
            }
            else
            {
                ValidateEffect(draft, result);
            }

            if (draft.CastTime > 0f &&
                draft.CastingVfxPrefab == null && draft.CastingSound == null)
            {
                result.Warnings.Add("캐스팅 시작 VFX/SFX가 비어 있습니다. 자산 선택 전이면 그대로 저장할 수 있습니다.");
            }
            if (draft.CastVfxPrefab == null && draft.CastSound == null)
            {
                result.Warnings.Add("발동·발사 VFX/SFX가 비어 있습니다. 자산 선택 전이면 그대로 저장할 수 있습니다.");
            }
            if (draft.ImpactVfxPrefab == null && draft.ImpactSound == null)
            {
                result.Warnings.Add("대상 적용 VFX/SFX가 비어 있습니다. 자산 선택 전이면 그대로 저장할 수 있습니다.");
            }
            if (!IsFiniteVector(draft.CastingVfxLocalOffset) ||
                !IsFiniteVector(draft.CastingVfxLocalEuler) ||
                !IsFiniteVector(draft.CastVfxLocalOffset) || !IsFiniteVector(draft.CastVfxLocalEuler) ||
                !IsFiniteVector(draft.ImpactVfxLocalOffset) || !IsFiniteVector(draft.ImpactVfxLocalEuler) ||
                !IsFinitePositive(draft.CastingVfxScale) || !IsFinitePositive(draft.CastVfxScale) ||
                !IsFinitePositive(draft.ImpactVfxScale))
            {
                result.Errors.Add("VFX 위치·회전·크기 값이 유효하지 않습니다.");
            }
            if (draft.Icon == null && draft.RegisterInCatalog)
            {
                result.Errors.Add("소환·보유·장착 화면에 사용할 스킬 아이콘이 필요합니다.");
            }
            else if (draft.Icon == null)
            {
                result.Warnings.Add("로컬 실험 자산의 스킬 아이콘이 비어 있습니다.");
            }

            if (draft.RegisterInCatalog)
            {
                if (draft.MaxLevel < 1 || draft.RequiredDuplicateCount < 1 ||
                    draft.BaseGoldCost < 1L ||
                    !IsFinitePositive(draft.GoldCostGrowthMultiplier) ||
                    draft.GoldCostGrowthMultiplier < 1f ||
                    !IsFinitePositive(draft.MaxLevelEffectMultiplier))
                {
                    result.Errors.Add("카탈로그 레벨·각성 예약값·골드 성장값은 모두 1 이상의 유효한 값이어야 합니다.");
                }
            }
            if (draft.IncludeInSummonPool && !draft.RegisterInCatalog)
            {
                result.Errors.Add("소환 풀 등록은 Catalog 등록과 함께 사용해야 합니다.");
            }
            if (draft.IncludeInSummonPool &&
                (draft.MinimumSummonLevel < 1 || draft.SummonWeight < 1))
            {
                result.Errors.Add("소환 해금 단계와 가중치는 1 이상이어야 합니다.");
            }
            return result;
        }

        private static void ValidateAttack(
            CommanderSkillWorkshopDraft draft,
            CommanderSkillWorkshopValidation result)
        {
            if (draft.TargetTeam != CommanderSkillTargetTeam.Enemy)
            {
                result.Errors.Add("공격형은 적 진영을 대상으로 해야 합니다.");
            }
            if (draft.DeliveryModule is not MonsterBasicAttackDeliveryModule.Direct and
                not MonsterBasicAttackDeliveryModule.Projectile)
            {
                result.Errors.Add("1차 군단장 공격 전달은 즉시 판정 또는 투사체만 지원합니다.");
            }
            if (!IsFiniteNonNegative(draft.BaseDamage) || draft.BaseDamage <= 0f)
            {
                result.Errors.Add("공격 피해는 0보다 커야 합니다.");
            }
            if (!Enum.IsDefined(typeof(MonsterBasicAttackShape), draft.Shape))
            {
                result.Errors.Add("공격 판정 모양이 유효하지 않습니다.");
            }
            if (draft.MaxTargets < 1)
            {
                result.Errors.Add("최대 대상 수는 1 이상이어야 합니다.");
            }
            if (draft.Shape == MonsterBasicAttackShape.Circle &&
                (!IsFinitePositive(draft.Radius) || draft.Radius < 0.1f))
            {
                result.Errors.Add("원형 반경은 0.1m 이상이어야 합니다.");
            }
            if (draft.Shape == MonsterBasicAttackShape.Fan &&
                (!IsFinitePositive(draft.Angle) || draft.Angle < 5f || draft.Angle > 180f))
            {
                result.Errors.Add("부채꼴 각도는 5~180도여야 합니다.");
            }
            if (draft.Shape == MonsterBasicAttackShape.Line &&
                (!IsFinitePositive(draft.LineWidth) || draft.LineWidth < 0.05f))
            {
                result.Errors.Add("직선 폭은 0.05m 이상이어야 합니다.");
            }

            if (draft.DeliveryModule == MonsterBasicAttackDeliveryModule.Projectile)
            {
                if (draft.ProjectilePrefab == null)
                {
                    result.Errors.Add("투사체 전달에는 Projectile Prefab이 필요합니다.");
                }
                else if (draft.ProjectilePrefab.GetComponent<CommanderSkillProjectile>() == null)
                {
                    result.Errors.Add("Projectile Prefab 루트에 CommanderSkillProjectile이 필요합니다.");
                }
                if (!IsFinitePositive(draft.ProjectileSpeed) || draft.ProjectileSpeed < 1f)
                {
                    result.Errors.Add("투사체 속도는 1m/s 이상이어야 합니다.");
                }
                if (draft.Shape is MonsterBasicAttackShape.Fan or MonsterBasicAttackShape.Line)
                {
                    result.Errors.Add("투사체 전달은 1차에서 단일 또는 원형 판정만 지원합니다.");
                }
            }
        }

        private static void ValidateEffect(
            CommanderSkillWorkshopDraft draft,
            CommanderSkillWorkshopValidation result)
        {
            if (draft.Category is not CommanderSkillCategory.Buff and not CommanderSkillCategory.Debuff)
            {
                result.Errors.Add("효과형은 버프형 또는 디버프형이어야 합니다.");
                return;
            }

            var expectedTeam = draft.Category == CommanderSkillCategory.Buff
                ? CommanderSkillTargetTeam.Ally
                : CommanderSkillTargetTeam.Enemy;
            if (draft.TargetTeam != expectedTeam)
            {
                result.Errors.Add("효과 분류와 대상 진영이 일치하지 않습니다.");
            }
            if (draft.Effects == null || draft.Effects.Count == 0 || draft.Effects.Count > 8)
            {
                result.Errors.Add("효과 카드는 1~8개여야 합니다.");
                return;
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < draft.Effects.Count; index++)
            {
                var effect = draft.Effects[index];
                if (effect == null)
                {
                    result.Errors.Add($"효과 {index + 1}이 비어 있습니다.");
                    continue;
                }
                if (!UsesSafeId(effect.EffectId) || !ids.Add(effect.EffectId))
                {
                    result.Errors.Add($"효과 {index + 1}의 ID가 비었거나 중복되었습니다.");
                }
                if (!CommanderUnitEffectDefinition.IsCompatible(draft.Category, effect.EffectType))
                {
                    result.Errors.Add($"효과 {index + 1}은 {draft.Category} 분류에서 사용할 수 없습니다.");
                }
                if (!CommanderUnitEffectDefinition.IsValueSourceCompatible(
                        effect.EffectType,
                        effect.ValueSource))
                {
                    result.Errors.Add($"효과 {index + 1}의 수치 기준이 효과 종류와 맞지 않습니다.");
                }
                if (!IsFiniteNonNegative(effect.Magnitude) ||
                    (effect.EffectType is not CommanderSkillUnitEffectType.Cleanse and
                     not CommanderSkillUnitEffectType.Stun && effect.Magnitude <= 0f))
                {
                    result.Errors.Add($"효과 {index + 1}의 수치가 유효하지 않습니다.");
                }
                else if (CommanderUnitEffectDefinition.UsesRatioMagnitude(
                             effect.EffectType,
                             effect.ValueSource) &&
                         effect.Magnitude > 1f)
                {
                    result.Errors.Add(
                        $"효과 {index + 1}의 비율 수치는 0~1이어야 합니다. 예: 0.2 = 20%.");
                }
                if (CommanderUnitEffectDefinition.RequiresDuration(effect.EffectType) &&
                    (!IsFinitePositive(effect.Duration) || effect.Duration <= 0f))
                {
                    result.Errors.Add($"효과 {index + 1}은 0초보다 긴 지속 시간이 필요합니다.");
                }
                if (effect.Scope == CommanderSkillEffectScope.Area &&
                    (!IsFinitePositive(effect.Radius) || effect.Radius < 0.1f || effect.MaxTargets < 1))
                {
                    result.Errors.Add($"효과 {index + 1}의 범위·최대 대상 수가 유효하지 않습니다.");
                }
            }
        }

        private static bool UsesSafeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
            {
                return false;
            }
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!(character is >= 'a' and <= 'z') &&
                    !(character is >= '0' and <= '9') && character != '_')
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFiniteVector(UnityEngine.Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
