using System;
using System.Collections;
using System.Collections.Generic;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    [DisallowMultipleComponent]
    public sealed class CommanderSkillRuntime : MonoBehaviour // 쿨타임·자동사용·투사체 수명 관리
    {
        private const float AutoScanInterval = 0.1f;
        private readonly float[] cooldownRemaining = new float[CommanderSkillSlotRules.SlotCount];
        private readonly float[] cooldownDuration = new float[CommanderSkillSlotRules.SlotCount];

        private IGameProgressService progress;
        private CommanderSkillCatalog catalog;
        private CombatWorld world;
        private ICommanderSkillCombatGateway combat;
        private ICommanderSkillFeedbackGateway feedback;
        private CommanderSkillEffectRunner effectRunner;
        private readonly List<ICommanderSkillExecutor> executors = new List<ICommanderSkillExecutor>(3);
        private Transform castOrigin;
        private Func<bool> isInputBlocked;
        private Func<float> externalDamageMultiplier;
        private CommanderSkillProgressView progressView;
        private float autoScanRemaining;
        private bool configured;

        public bool IsPaused => world == null || world.IsPaused || (isInputBlocked?.Invoke() ?? false);
        public bool IsConfigured => configured;

        public void Configure(
            IGameProgressService progressService,
            CommanderSkillCatalog skillCatalog,
            CombatWorld combatWorld,
            Transform origin,
            Func<bool> inputBlocked = null,
            Func<float> damageMultiplier = null)
        {
            Shutdown();
            var catalogError = skillCatalog == null ? "Catalog asset is missing." : string.Empty;
            if (skillCatalog == null || !skillCatalog.TryValidate(out catalogError))
            {
                Debug.LogError($"Commander skill catalog is invalid: {catalogError}", skillCatalog);
                return;
            }

            progress = progressService;
            catalog = skillCatalog;
            world = combatWorld;
            castOrigin = origin;
            isInputBlocked = inputBlocked;
            externalDamageMultiplier = damageMultiplier;
            var gateway = world == null ? null : new CommanderSkillCombatGateway(world);
            combat = gateway;
            feedback = gateway;
            effectRunner = gateway == null
                ? null
                : new CommanderSkillEffectRunner(new CommanderAreaDamageEffectHandler(gateway));
            executors.Clear();
            executors.Add(new CommanderAttackSkillExecutor());
            configured = progress != null && catalog != null && world != null && castOrigin != null;
            if (progress != null)
            {
                progress.Changed += RefreshProgress;
            }

            RefreshProgress();
        }

        public void Shutdown()
        {
            if (progress != null)
            {
                progress.Changed -= RefreshProgress;
            }

            StopAllCoroutines();
            progress = null;
            catalog = null;
            world = null;
            combat = null;
            feedback = null;
            effectRunner = null;
            executors.Clear();
            castOrigin = null;
            isInputBlocked = null;
            externalDamageMultiplier = null;
            configured = false;
            autoScanRemaining = 0f;
            for (var index = 0; index < cooldownRemaining.Length; index++)
            {
                cooldownRemaining[index] = 0f;
                cooldownDuration[index] = 0f;
            }
        }

        public bool TryCastSlot(int slotIndex)
        {
            if (!configured || IsPaused || combat == null || !combat.IsReady ||
                slotIndex < 0 || slotIndex >= CommanderSkillSlotRules.SlotCount ||
                !progressView.IsSlotUnlocked(slotIndex) || cooldownRemaining[slotIndex] > 0f ||
                !catalog.TryGet(progressView.GetEquippedSkillId(slotIndex), out var definition))
            {
                return false;
            }

            var executor = FindExecutor(definition);
            if (executor == null)
            {
                return false;
            }

            var multiplier = GetEffectMultiplier(definition.SkillId, GetOwnedSkillLevel(definition.SkillId)) *
                             Mathf.Max(0f, externalDamageMultiplier?.Invoke() ?? 1f);
            var context = new CommanderSkillExecutionContext(
                this,
                combat,
                feedback,
                castOrigin,
                multiplier);
            if (!executor.TryExecute(definition, context))
            {
                return false; // 대상·전달 실패 시 쿨타임과 피드백을 시작하지 않음
            }

            cooldownDuration[slotIndex] = definition.Cooldown;
            cooldownRemaining[slotIndex] = definition.Cooldown;
            return true;
        }

        public float GetCooldownRemaining(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < cooldownRemaining.Length
                ? Mathf.Max(0f, cooldownRemaining[slotIndex])
                : 0f;
        }

        public float GetCooldownDuration(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < cooldownDuration.Length
                ? Mathf.Max(0f, cooldownDuration[slotIndex])
                : 0f;
        }

        internal void ResolveImpact(
            CommanderSkillDefinition definition,
            Vector3 position,
            float damageMultiplier)
        {
            if (!configured || definition == null)
            {
                return;
            }

            effectRunner?.Apply(definition, position, damageMultiplier);
            PlayFeedback(
                definition.ImpactVfxPrefab,
                definition.ImpactVfxLifetime,
                definition.ImpactSfx,
                position,
                Quaternion.identity);
        }

        internal void ReturnProjectile(GameObject projectile)
        {
            feedback?.Return(projectile);
        }

        internal void PlayCastFeedback(
            CommanderSkillDefinition definition,
            Vector3 position,
            Quaternion rotation)
        {
            if (definition == null)
            {
                return;
            }

            PlayFeedback(
                definition.CastVfxPrefab,
                definition.CastVfxLifetime,
                definition.CastSfx,
                position,
                rotation);
        }

        private void Update()
        {
            if (!configured || IsPaused)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            for (var index = 0; index < cooldownRemaining.Length; index++)
            {
                cooldownRemaining[index] = Mathf.Max(0f, cooldownRemaining[index] - deltaTime);
            }

            if (!progressView.AutoUseEnabled)
            {
                return;
            }

            autoScanRemaining -= deltaTime;
            if (autoScanRemaining > 0f)
            {
                return;
            }

            autoScanRemaining = AutoScanInterval;
            CommanderSkillPriority.TryUseFirstReadySlot(
                progressView,
                cooldownRemaining,
                catalog,
                TryCastSlot);
        }

        private void RefreshProgress()
        {
            progressView = progress?.View.CommanderSkills ?? default;
        }

        private int GetOwnedSkillLevel(string skillId)
        {
            var owned = progressView.OwnedSkills;
            for (var index = 0; index < owned.Count; index++)
            {
                if (owned[index].SkillId == skillId)
                {
                    return owned[index].Level;
                }
            }

            return 1;
        }

        private float GetEffectMultiplier(string skillId, int level)
        {
            return catalog != null && catalog.BalanceConfig.TryGetRule(skillId, out var rule)
                ? rule.GetDamageMultiplier(level)
                : 1f;
        }

        private ICommanderSkillExecutor FindExecutor(CommanderSkillDefinition definition)
        {
            for (var index = 0; index < executors.Count; index++)
            {
                if (executors[index].Supports(definition))
                {
                    return executors[index];
                }
            }

            return null;
        }

        private void PlayFeedback(
            GameObject vfxPrefab,
            float lifetime,
            ProjectMT.Shared.Audio.SfxCue sfx,
            Vector3 position,
            Quaternion rotation)
        {
            feedback?.PlaySfx(sfx, position);
            if (vfxPrefab == null || feedback == null)
            {
                return;
            }

            var instance = feedback.Rent(vfxPrefab, position, rotation);
            if (instance != null)
            {
                StartCoroutine(ReturnFeedbackAfter(instance, lifetime));
            }
        }

        private IEnumerator ReturnFeedbackAfter(GameObject instance, float delay)
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, delay));
            feedback?.Return(instance);
        }

        private void OnDestroy()
        {
            Shutdown();
        }
    }
}
