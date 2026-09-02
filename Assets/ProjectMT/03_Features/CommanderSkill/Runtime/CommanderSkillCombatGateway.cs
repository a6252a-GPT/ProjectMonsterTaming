using System.Collections.Generic;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    public readonly struct CommanderSkillDamageRequest // 기능 피해 규칙을 공용 DamageRequest와 분리
    {
        public CommanderSkillDamageRequest(
            string skillId,
            CommanderSkillDamageKind damageKind,
            Vector3 center,
            float damage,
            float radius,
            int maxTargets)
            : this(
                skillId,
                damageKind,
                MonsterBasicAttackShape.Circle,
                MonsterBasicAttackCenter.PrimaryTarget,
                center,
                null,
                center,
                Vector3.forward,
                radius,
                radius,
                0f,
                90f,
                2f,
                maxTargets,
                damage)
        {
        }

        public CommanderSkillDamageRequest(
            string skillId,
            CommanderSkillDamageKind damageKind,
            MonsterBasicAttackShape shape,
            MonsterBasicAttackCenter centerMode,
            Vector3 castOrigin,
            UnitActor primaryTarget,
            Vector3 impactPosition,
            Vector3 forward,
            float range,
            float radius,
            float forwardOffset,
            float angle,
            float lineWidth,
            int maxTargets,
            float damage)
        {
            SkillId = skillId ?? string.Empty;
            DamageKind = damageKind;
            Shape = shape;
            CenterMode = centerMode;
            CastOrigin = castOrigin;
            PrimaryTarget = primaryTarget;
            ImpactPosition = impactPosition;
            Forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Range = Mathf.Max(0.1f, range);
            Radius = Mathf.Max(0.1f, radius);
            ForwardOffset = Mathf.Max(0f, forwardOffset);
            Angle = Mathf.Clamp(angle, 5f, 180f);
            LineWidth = Mathf.Max(0.05f, lineWidth);
            MaxTargets = Mathf.Max(1, maxTargets);
            Damage = Mathf.Max(0f, damage);
        }

        public string SkillId { get; }
        public CommanderSkillDamageKind DamageKind { get; }
        public MonsterBasicAttackShape Shape { get; }
        public MonsterBasicAttackCenter CenterMode { get; }
        public Vector3 CastOrigin { get; }
        public UnitActor PrimaryTarget { get; }
        public Vector3 ImpactPosition { get; }
        public Vector3 Forward { get; }
        public float Range { get; }
        public float Damage { get; }
        public float Radius { get; }
        public float ForwardOffset { get; }
        public float Angle { get; }
        public float LineWidth { get; }
        public int MaxTargets { get; }
    }

    public readonly struct CommanderSkillUnitEffectRequest // 효과형 액티브 값을 UnitActor 공용 API로 전달
    {
        public CommanderSkillUnitEffectRequest(
            string skillId,
            CommanderUnitEffectDefinition effect,
            CommanderSkillTargetTeam targetTeam,
            UnitActor primaryTarget,
            Vector3 center,
            float multiplier)
        {
            SkillId = skillId ?? string.Empty;
            Effect = effect;
            TargetTeam = targetTeam;
            PrimaryTarget = primaryTarget;
            Center = center;
            Multiplier = Mathf.Max(0f, multiplier);
        }

        public string SkillId { get; }
        public CommanderUnitEffectDefinition Effect { get; }
        public CommanderSkillTargetTeam TargetTeam { get; }
        public UnitActor PrimaryTarget { get; }
        public Vector3 Center { get; }
        public float Multiplier { get; }
    }

    public interface ICommanderSkillCombatGateway // 타기팅·피해 적용 경계
    {
        bool IsReady { get; }
        UnitActor FindTarget(Vector3 origin, CommanderSkillTargetingDefinition targeting);
        int ApplyAreaDamage(CommanderSkillDamageRequest request);
        int ApplyUnitEffect(CommanderSkillUnitEffectRequest request);
    }

    public interface ICommanderSkillFeedbackGateway // Pool·VFX·SFX 경계
    {
        GameObject Rent(GameObject prefab, Vector3 position, Quaternion rotation);
        void Return(GameObject instance);
        void PlaySfx(ProjectMT.Shared.Audio.SfxCue cue, Vector3 position);
    }

    public sealed class CommanderSkillCombatGateway : ICommanderSkillCombatGateway, ICommanderSkillFeedbackGateway
    {
        private readonly CombatWorld world;
        private readonly List<UnitActor> targets = new List<UnitActor>(64);

        public CommanderSkillCombatGateway(CombatWorld combatWorld)
        {
            world = combatWorld;
        }

        public bool IsReady => world != null && !world.IsPaused;

        public UnitActor FindTarget(Vector3 origin, CommanderSkillTargetingDefinition targeting)
        {
            if (world == null || targeting == null)
            {
                return null;
            }

            var team = targeting.TargetTeam == CommanderSkillTargetTeam.Ally
                ? UnitTeam.Player
                : UnitTeam.Enemy;
            var collectCount = targeting.Selection == CommanderSkillTargetSelection.Nearest ? 1 : 64;
            world.CollectUnits(team, origin, targeting.Range, collectCount, targets);
            if (targets.Count == 0 || targeting.Selection == CommanderSkillTargetSelection.Nearest)
            {
                return targets.Count > 0 ? targets[0] : null;
            }

            UnitActor selected = null;
            var lowestRatio = float.MaxValue;
            for (var index = 0; index < targets.Count; index++)
            {
                var candidate = targets[index];
                if (candidate?.Health == null || !candidate.IsAlive)
                {
                    continue;
                }

                var ratio = candidate.Health.MaxHealth > 0f
                    ? candidate.Health.CurrentHealth / candidate.Health.MaxHealth
                    : 1f;
                if (ratio < lowestRatio)
                {
                    selected = candidate;
                    lowestRatio = ratio;
                }
            }

            return selected;
        }

        public GameObject Rent(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return world?.RentMonsterObject(prefab, position, rotation);
        }

        public void Return(GameObject instance)
        {
            world?.ReturnMonsterObject(instance);
        }

        public int ApplyAreaDamage(CommanderSkillDamageRequest request)
        {
            if (world == null || request.Damage <= 0f)
            {
                return 0;
            }

            ResolveDamageTargets(request);
            var hitCount = 0;
            for (var index = 0; index < targets.Count; index++)
            {
                var target = targets[index];
                if (target?.Health != null && target.Health.ApplyDamage(
                        new DamageRequest(
                            null,
                            request.Damage,
                            target.transform.position + Vector3.up * 0.35f)))
                {
                    hitCount++;
                }
            }

            return hitCount;
        }

        public int ApplyUnitEffect(CommanderSkillUnitEffectRequest request)
        {
            if (world == null || request.Effect == null)
            {
                return 0;
            }

            var team = request.TargetTeam == CommanderSkillTargetTeam.Ally
                ? UnitTeam.Player
                : UnitTeam.Enemy;
            targets.Clear();
            if (request.Effect.Scope == CommanderSkillEffectScope.PrimaryTarget)
            {
                if (request.PrimaryTarget != null && request.PrimaryTarget.IsAlive &&
                    request.PrimaryTarget.Team == team)
                {
                    targets.Add(request.PrimaryTarget);
                }
            }
            else
            {
                world.CollectUnits(
                    team,
                    request.Center,
                    request.Effect.Radius,
                    request.Effect.MaxTargets,
                    targets);
            }

            var appliedCount = 0;
            for (var index = 0; index < targets.Count; index++)
            {
                if (ApplyUnitEffectToTarget(request, targets[index]))
                {
                    appliedCount++;
                }
            }
            return appliedCount;
        }

        private void ResolveDamageTargets(CommanderSkillDamageRequest request)
        {
            switch (request.Shape)
            {
                case MonsterBasicAttackShape.Single:
                    targets.Clear();
                    if (request.PrimaryTarget != null && request.PrimaryTarget.IsAlive &&
                        request.PrimaryTarget.Team == UnitTeam.Enemy)
                    {
                        targets.Add(request.PrimaryTarget);
                    }
                    break;
                case MonsterBasicAttackShape.Fan:
                    world.CollectUnitsInFan(
                        UnitTeam.Enemy,
                        request.CastOrigin,
                        request.Forward,
                        request.Range,
                        request.Angle,
                        request.MaxTargets,
                        targets);
                    break;
                case MonsterBasicAttackShape.Line:
                    world.CollectUnitsInLine(
                        UnitTeam.Enemy,
                        request.CastOrigin,
                        request.Forward,
                        request.Range,
                        request.LineWidth,
                        request.MaxTargets,
                        targets);
                    break;
                default:
                    var center = request.CenterMode switch
                    {
                        MonsterBasicAttackCenter.Source => request.CastOrigin,
                        MonsterBasicAttackCenter.Forward =>
                            request.CastOrigin + request.Forward * request.ForwardOffset,
                        _ => request.ImpactPosition
                    };
                    world.CollectUnits(
                        UnitTeam.Enemy,
                        center,
                        request.Radius,
                        request.MaxTargets,
                        targets);
                    break;
            }
        }

        private bool ApplyUnitEffectToTarget(CommanderSkillUnitEffectRequest request, UnitActor target)
        {
            if (target == null || !target.IsAlive || target.Health == null)
            {
                return false;
            }

            var effect = request.Effect;
            var amount = ResolveEffectAmount(effect, target) * request.Multiplier;
            var runtimeId = $"commander_{request.SkillId}_{effect.EffectId}";
            switch (effect.EffectType)
            {
                case CommanderSkillUnitEffectType.Heal:
                    var before = target.Health.CurrentHealth;
                    target.Health.Heal(amount);
                    var healed = target.Health.CurrentHealth - before;
                    if (healed > 0f)
                    {
                        world.Feedback?.PlayFloatingNumber(
                            target.transform.position,
                            healed,
                            FloatingNumberStyle.Heal,
                            target.GetInstanceID());
                    }
                    return healed > 0f;
                case CommanderSkillUnitEffectType.Shield:
                    target.SkillRuntime.GrantShield(amount, effect.Duration);
                    return true;
                case CommanderSkillUnitEffectType.AttackBuff:
                    ApplyStatEffect(target, runtimeId, effect,
                        new MonsterStatModifier(0f, amount, 0f, 0f, 0f, 0f));
                    return true;
                case CommanderSkillUnitEffectType.DefenseBuff:
                    ApplyStatEffect(target, runtimeId, effect,
                        new MonsterStatModifier(0f, 0f, amount, 0f, 0f, 0f));
                    return true;
                case CommanderSkillUnitEffectType.AttackSpeedBuff:
                    ApplyStatEffect(target, runtimeId, effect,
                        new MonsterStatModifier(0f, 0f, 0f, amount, 0f, 0f));
                    return true;
                case CommanderSkillUnitEffectType.AttackDebuff:
                    ApplyStatEffect(target, runtimeId, effect,
                        new MonsterStatModifier(0f, -Mathf.Min(amount, 0.95f), 0f, 0f, 0f, 0f));
                    return true;
                case CommanderSkillUnitEffectType.DefenseDebuff:
                    ApplyStatEffect(target, runtimeId, effect,
                        new MonsterStatModifier(0f, 0f, -Mathf.Min(amount, 0.95f), 0f, 0f, 0f));
                    return true;
                case CommanderSkillUnitEffectType.AttackSpeedDebuff:
                    ApplyStatEffect(target, runtimeId, effect,
                        new MonsterStatModifier(0f, 0f, 0f, -Mathf.Min(amount, 0.95f), 0f, 0f));
                    return true;
                case CommanderSkillUnitEffectType.MoveSpeedDebuff:
                    ApplyStatEffect(target, runtimeId, effect,
                        new MonsterStatModifier(0f, 0f, 0f, 0f, -Mathf.Min(amount, 0.95f), 0f));
                    return true;
                case CommanderSkillUnitEffectType.DamageReduction:
                    target.SkillRuntime.ApplyDamageReduction(Mathf.Clamp(amount, 0f, 0.95f), effect.Duration);
                    return true;
                case CommanderSkillUnitEffectType.DamageReflect:
                    target.SkillRuntime.ApplyDamageReflect(Mathf.Clamp01(amount), effect.Duration);
                    return true;
                case CommanderSkillUnitEffectType.Cleanse:
                    return target.TryCleanseOneDebuff();
                case CommanderSkillUnitEffectType.EnergyGain:
                    target.SkillRuntime.GrantActiveEnergy(amount);
                    return true;
                case CommanderSkillUnitEffectType.Slow:
                    target.ApplyActiveSlow(Mathf.Clamp(amount, 0f, 0.95f), effect.Duration);
                    return true;
                case CommanderSkillUnitEffectType.Stun:
                    return target.TryApplyActiveStun(effect.Duration);
                case CommanderSkillUnitEffectType.Mark:
                    target.SkillRuntime.ApplyExposure(Mathf.Clamp(amount, 0f, 0.95f), effect.Duration);
                    return true;
                case CommanderSkillUnitEffectType.EnergyDrain:
                    target.SkillRuntime.DrainActiveEnergy(amount);
                    return true;
                default:
                    return false;
            }
        }

        private static float ResolveEffectAmount(CommanderUnitEffectDefinition effect, UnitActor target)
        {
            return effect.ValueSource switch
            {
                CommanderSkillEffectValueSource.TargetMaxHealthRatio =>
                    target.Health.MaxHealth * effect.Magnitude,
                CommanderSkillEffectValueSource.TargetMissingHealthRatio =>
                    Mathf.Max(0f, target.Health.MaxHealth - target.Health.CurrentHealth) * effect.Magnitude,
                CommanderSkillEffectValueSource.TargetEnergyCapacityRatio =>
                    target.SkillRuntime.EnergyCapacity * effect.Magnitude,
                _ => effect.Magnitude
            };
        }

        private static void ApplyStatEffect(
            UnitActor target,
            string runtimeId,
            CommanderUnitEffectDefinition effect,
            MonsterStatModifier modifier)
        {
            target.ApplyMonsterBuff(runtimeId, modifier, effect.Duration, effect.StackPolicy);
        }

        public void PlaySfx(ProjectMT.Shared.Audio.SfxCue cue, Vector3 position)
        {
            world?.PlayMonsterSfx(cue, position);
        }
    }
}
