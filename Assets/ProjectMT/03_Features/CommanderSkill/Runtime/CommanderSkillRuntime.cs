using System;
using System.Collections;
using System.Collections.Generic;
using ProjectMT.Shared.Audio;
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
        private const string CommanderSkillVoiceResourcePath =
            "Audio/CommanderVoice/SFX_CommanderSkillVoice";
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
        private int castingSlot = -1;
        private CommanderSkillDefinition castingDefinition;
        private float castingRemaining;
        private float castingDuration;
        private float castingMultiplier;
        private bool configured;
        private SfxCue commanderSkillVoice;

        public bool IsPaused => world == null || world.IsPaused || (isInputBlocked?.Invoke() ?? false);
        public bool IsConfigured => configured;
        public bool IsCasting => castingSlot >= 0 && castingDefinition != null;
        public int CastingSlot => IsCasting ? castingSlot : -1;
        public float CastingRemaining => IsCasting ? Mathf.Max(0f, castingRemaining) : 0f;
        public float CastingDuration => IsCasting ? Mathf.Max(0f, castingDuration) : 0f;

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
            commanderSkillVoice = Resources.Load<SfxCue>(CommanderSkillVoiceResourcePath);
            var gateway = world == null ? null : new CommanderSkillCombatGateway(world);
            combat = gateway;
            feedback = gateway;
            effectRunner = gateway == null
                ? null
                : new CommanderSkillEffectRunner(
                    new CommanderAreaDamageEffectHandler(gateway),
                    new CommanderUnitEffectHandler(gateway));
            executors.Clear();
            executors.Add(new CommanderAttackSkillExecutor());
            executors.Add(new CommanderEffectSkillExecutor());
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
            ClearPendingCast();
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
                IsCasting ||
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

            if (definition.CastTime > 0f &&
                combat.FindTarget(castOrigin.position, definition.Targeting) == null)
            {
                return false; // 대상 없는 빈 캐스팅을 시작하지 않음. 발동 때도 대상은 다시 검증한다.
            }

            var multiplier = GetEffectMultiplier(definition.SkillId, GetOwnedSkillLevel(definition.SkillId)) *
                             Mathf.Max(0f, externalDamageMultiplier?.Invoke() ?? 1f);
            if (definition.CastTime > 0f)
            {
                castingSlot = slotIndex;
                castingDefinition = definition;
                castingDuration = definition.CastTime;
                castingRemaining = definition.CastTime;
                castingMultiplier = multiplier;
                PlayCastingFeedback(definition, castOrigin.position, castOrigin.rotation);
                PlayCommanderSkillVoice();
                return true;
            }

            var activated = TryActivate(slotIndex, definition, executor, multiplier);
            if (activated)
            {
                PlayCommanderSkillVoice();
            }

            return activated;
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

        internal int ResolveImpact(
            CommanderSkillDefinition definition,
            CommanderSkillImpactContext impact,
            float effectMultiplier)
        {
            if (!configured || definition == null)
            {
                return 0;
            }

            var appliedCount = effectRunner?.Apply(definition, impact, effectMultiplier) ?? 0;
            PlayFeedback(
                definition.ImpactVfxPrefab,
                definition.ImpactVfxLifetime,
                definition.ImpactSfx,
                impact.Position,
                Quaternion.LookRotation(impact.Forward, Vector3.up),
                definition.ImpactVfxLocalOffset,
                definition.ImpactVfxLocalEuler,
                definition.ImpactVfxScale);
            return appliedCount;
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
                rotation,
                definition.CastVfxLocalOffset,
                definition.CastVfxLocalEuler,
                definition.CastVfxScale);
        }

        internal void PlayCastingFeedback(
            CommanderSkillDefinition definition,
            Vector3 position,
            Quaternion rotation)
        {
            if (definition == null)
            {
                return;
            }

            PlayFeedback(
                definition.CastingVfxPrefab,
                definition.CastingVfxLifetime,
                definition.CastingSfx,
                position,
                rotation,
                definition.CastingVfxLocalOffset,
                definition.CastingVfxLocalEuler,
                definition.CastingVfxScale);
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

            if (TickPendingCast(deltaTime))
            {
                return;
            }

            if (IsCasting)
            {
                return;
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

        private bool TryActivate(
            int slotIndex,
            CommanderSkillDefinition definition,
            ICommanderSkillExecutor executor,
            float multiplier)
        {
            var context = new CommanderSkillExecutionContext(
                this,
                combat,
                feedback,
                castOrigin,
                multiplier);
            if (executor == null || !executor.TryExecute(definition, context))
            {
                return false; // 대상·전달 실패 시 쿨타임과 피드백을 시작하지 않음
            }

            cooldownDuration[slotIndex] = definition.Cooldown;
            cooldownRemaining[slotIndex] = definition.Cooldown;
            return true;
        }

        private bool TickPendingCast(float deltaTime)
        {
            if (!IsCasting)
            {
                return false;
            }

            castingRemaining = Mathf.Max(0f, castingRemaining - Mathf.Max(0f, deltaTime));
            if (castingRemaining > 0f)
            {
                return false;
            }

            var slotIndex = castingSlot;
            var definition = castingDefinition;
            var multiplier = castingMultiplier;
            ClearPendingCast();

            if (slotIndex < 0 || slotIndex >= CommanderSkillSlotRules.SlotCount ||
                definition == null || catalog == null ||
                !catalog.TryGet(progressView.GetEquippedSkillId(slotIndex), out var current) ||
                current != definition)
            {
                return true;
            }

            TryActivate(slotIndex, definition, FindExecutor(definition), multiplier);
            return true;
        }

        private void ClearPendingCast()
        {
            castingSlot = -1;
            castingDefinition = null;
            castingRemaining = 0f;
            castingDuration = 0f;
            castingMultiplier = 0f;
        }

        private void PlayFeedback(
            GameObject vfxPrefab,
            float lifetime,
            ProjectMT.Shared.Audio.SfxCue sfx,
            Vector3 position,
            Quaternion rotation,
            Vector3 localOffset,
            Vector3 localEuler,
            float scale)
        {
            var resolvedPosition = position + rotation * localOffset;
            var resolvedRotation = rotation * Quaternion.Euler(localEuler);
            feedback?.PlaySfx(sfx, resolvedPosition);
            if (vfxPrefab == null || feedback == null)
            {
                return;
            }

            var instance = feedback.Rent(vfxPrefab, resolvedPosition, resolvedRotation);
            if (instance != null)
            {
                MonsterBasicAttackVfxPlayback.ApplyInstanceScale(
                    instance,
                    vfxPrefab.transform.localScale * Mathf.Max(0.01f, scale));
                StartCoroutine(ReturnFeedbackAfter(instance, lifetime));
            }
        }

        private void PlayCommanderSkillVoice()
        {
            if (castOrigin != null)
            {
                feedback?.PlaySfx(commanderSkillVoice, castOrigin.position);
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
