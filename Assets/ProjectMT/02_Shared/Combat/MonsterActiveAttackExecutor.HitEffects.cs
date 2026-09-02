using System;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public sealed partial class MonsterActiveAttackExecutor
    {
        private void ApplyHit(UnitActor target, Vector3 effectCenter)
        {
            if (target == null || !target.IsAlive || !target.IsCombatReady) return;
            var amount = owner.EffectiveStats.damage * currentDamageMultiplier;
            var feel = skill.ImpactFeel;
            var feelIntensity = Mathf.Clamp(currentDamageMultiplier, 0.5f, 2f);
            var feelTarget = ResolveUnitFeelTarget(target);
            var feelOwnsTargetMotion = !feelPlayedForStep &&
                                       world.WillPlayBasicAttackFeelTargetMotion(
                                           feel,
                                           feelTarget,
                                           feelIntensity);
            var feedbackFlags = feelOwnsTargetMotion
                ? DamageFeedbackFlags.BasicAttackFeelTargetMotion
                : DamageFeedbackFlags.None;
            if (!world.ApplyMonsterSkillDamage(owner, target.Health, amount, feedbackFlags)) return;
            var presentation = skill.ResolvePresentation(currentStep.StepId);
            var impactRotation = Quaternion.LookRotation(ResolveForward(target), Vector3.up);
            var bodyScale = owner.RuntimeAssetSet?.BodyProfile?.VfxScale ?? 1f;
            PlayPresentationEvent(
                presentation,
                MonsterActivePresentationEvent.Impact,
                target,
                presentation?.Impact);
            if (!feelPlayedForStep && feel?.HasFeel == true)
            {
                world.PlayBasicAttackFeelAt(
                    feel,
                    target.transform.position,
                    impactRotation,
                    bodyScale,
                    feelTarget,
                    feelIntensity);
                feelPlayedForStep = true;
            }
            for (var index = 0; index < currentStep.HitEffects.Count; index++)
            {
                ApplyHitEffect(
                    target,
                    currentStep.HitEffects[index],
                    effectCenter,
                    owner,
                    owner.transform.position);
            }
        }

        private static GameObject ResolveUnitFeelTarget(UnitActor actor)
        {
            if (actor == null)
            {
                return null;
            }

            var visual = actor.transform.Find("Visual") ?? actor.transform.Find("VisualRoot");
            return visual != null ? visual.gameObject : actor.gameObject;
        }

        private void ApplyHitEffect(
            UnitActor target,
            MonsterActiveHitEffect effect,
            Vector3 effectCenter,
            UnitActor source,
            Vector3 sourcePosition)
        {
            if (target == null || effect == null)
            {
                return;
            }

            var applied = false;
            switch (effect.Type)
            {
                case MonsterActiveHitEffectType.Knockback:
                    applied = target.TryApplyActiveKnockback(target.transform.position - sourcePosition,
                        effect.Magnitude, effect.Duration, effect.SecondaryMagnitude);
                    break;
                case MonsterActiveHitEffectType.Airborne:
                    applied = target.TryApplyActiveAirborne(effect.Magnitude, effect.Duration);
                    break;
                case MonsterActiveHitEffectType.Stun:
                    applied = target.TryApplyActiveStun(effect.Duration);
                    break;
                case MonsterActiveHitEffectType.Bleed:
                    applied = target.IsAlive && source != null && effect.Magnitude > 0f && effect.Duration > 0f;
                    if (applied)
                    {
                        target.ApplyActiveBleed(source, effect.Magnitude, effect.Duration, effect.TickInterval);
                    }
                    break;
                case MonsterActiveHitEffectType.Burn:
                    applied = target.IsAlive && source != null && effect.Magnitude > 0f && effect.Duration > 0f;
                    if (applied)
                    {
                        target.ApplyActiveBurn(source, effect.Magnitude, effect.Duration, effect.TickInterval);
                    }
                    break;
                case MonsterActiveHitEffectType.Slow:
                    applied = target.IsAlive && effect.Magnitude > 0f && effect.Magnitude < 1f &&
                              effect.Duration > 0f;
                    if (applied)
                    {
                        target.ApplyActiveSlow(effect.Magnitude, effect.Duration);
                    }
                    break;
                case MonsterActiveHitEffectType.Pull:
                    applied = target.TryApplyActivePull(effectCenter, effect.Magnitude, effect.Duration);
                    break;
            }

            if (applied && TryResolveHitEffectStatusText(effect.Type, out var text))
            {
                world?.Feedback?.PlayStatusText(
                    target.transform.position,
                    text,
                    CombatStatusTextStyle.Impact,
                    target.GetInstanceID());
            }
        }

        private static bool TryResolveHitEffectStatusText(
            MonsterActiveHitEffectType type,
            out string text)
        {
            text = type switch
            {
                MonsterActiveHitEffectType.Knockback => "넉백!",
                MonsterActiveHitEffectType.Airborne => "에어본!",
                MonsterActiveHitEffectType.Stun => "기절!",
                MonsterActiveHitEffectType.Bleed => "출혈!",
                MonsterActiveHitEffectType.Burn => "화상!",
                MonsterActiveHitEffectType.Slow => "둔화!",
                MonsterActiveHitEffectType.Pull => "끌어당김!",
                _ => null
            };
            return !string.IsNullOrWhiteSpace(text);
        }
    }
}
