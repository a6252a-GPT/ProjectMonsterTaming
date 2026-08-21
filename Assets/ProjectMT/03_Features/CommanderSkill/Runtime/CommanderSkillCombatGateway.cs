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
        {
            SkillId = skillId ?? string.Empty;
            DamageKind = damageKind;
            Center = center;
            Damage = Mathf.Max(0f, damage);
            Radius = Mathf.Max(0.1f, radius);
            MaxTargets = Mathf.Max(1, maxTargets);
        }

        public string SkillId { get; }
        public CommanderSkillDamageKind DamageKind { get; }
        public Vector3 Center { get; }
        public float Damage { get; }
        public float Radius { get; }
        public int MaxTargets { get; }
    }

    public interface ICommanderSkillCombatGateway // 타기팅·피해 적용 경계
    {
        bool IsReady { get; }
        UnitActor FindTarget(Vector3 origin, CommanderSkillTargetingDefinition targeting);
        int ApplyAreaDamage(CommanderSkillDamageRequest request);
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

            world.CollectUnits(
                UnitTeam.Enemy,
                request.Center,
                request.Radius,
                request.MaxTargets,
                targets);
            var hitCount = 0;
            for (var index = 0; index < targets.Count; index++)
            {
                var target = targets[index];
                if (target?.Health != null && target.Health.ApplyDamage(
                        new DamageRequest(
                            null,
                            request.Damage,
                            request.Center + Vector3.up * 0.35f)))
                {
                    hitCount++;
                }
            }

            return hitCount;
        }

        public void PlaySfx(ProjectMT.Shared.Audio.SfxCue cue, Vector3 position)
        {
            world?.PlayMonsterSfx(cue, position);
        }
    }
}
