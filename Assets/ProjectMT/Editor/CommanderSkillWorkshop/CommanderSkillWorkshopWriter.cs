using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Features.CommanderSkill;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.CommanderSkillWorkshop
{
    internal readonly struct CommanderSkillWorkshopWriteResult
    {
        public CommanderSkillWorkshopWriteResult(
            bool success,
            string message,
            CommanderSkillDefinition asset = null)
        {
            Success = success;
            Message = message ?? string.Empty;
            Asset = asset;
        }

        public bool Success { get; }
        public string Message { get; }
        public CommanderSkillDefinition Asset { get; }
    }

    internal static class CommanderSkillWorkshopWriter // Draft→전용 SubAsset→Catalog 원자 저장 경계
    {
        internal const string SkillRoot =
            "Assets/ProjectMT/03_Features/CommanderSkill/Resources/CommanderSkills/Custom";
        internal const string CatalogPath =
            "Assets/ProjectMT/03_Features/CommanderSkill/Resources/CommanderSkills/CommanderSkillCatalog.asset";
        internal const string BalancePath =
            "Assets/ProjectMT/03_Features/CommanderSkill/Resources/CommanderSkills/Rules/CommanderSkillBalanceConfig.asset";
        internal const string SummonPath =
            "Assets/ProjectMT/03_Features/CommanderSkill/Resources/CommanderSkills/Rules/CommanderSkillSummonConfig.asset";

        private const string TargetingName = "__Targeting";
        private const string DamageName = "__Damage";
        private const string EffectPrefix = "__Effect_";
        private const string GeneratedSfxPrefix = "__CommanderSkillSfx_";
        private const string CastingSfxName = GeneratedSfxPrefix + "Casting";
        private const string ActivationSfxName = GeneratedSfxPrefix + "Activation";
        private const string ImpactSfxName = GeneratedSfxPrefix + "Impact";
        private const string MarkSfxPrefix = GeneratedSfxPrefix + "Mark_";
        private static readonly Vector2 DefaultSfxVolume = new Vector2(0.9f, 1f);
        private static readonly Vector2 DefaultSfxPitch = new Vector2(0.98f, 1.02f);

        public static CommanderSkillWorkshopWriteResult SaveNew(CommanderSkillWorkshopDraft draft)
        {
            draft?.NormalizeCatalogOptions();
            var validation = CommanderSkillWorkshopValidator.Validate(draft);
            if (!validation.IsValid)
            {
                return Failed(validation);
            }

            EnsureFolder(SkillRoot);
            var fileName = draft.SkillId.StartsWith("CS_", StringComparison.Ordinal)
                ? draft.SkillId
                : $"CS_{draft.SkillId}";
            var path = $"{SkillRoot}/{fileName}.asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                return new CommanderSkillWorkshopWriteResult(
                    false,
                    $"오류: 같은 ID의 제작물이 이미 있습니다. {path}");
            }

            CommanderSkillDefinition definition = null;
            try
            {
                definition = CreateDefinition(draft.Category);
                definition.name = $"CS_{draft.SkillId}";
                AssetDatabase.CreateAsset(definition, path);
                ConfigureDefinition(draft, definition, path);
                SynchronizeRegistration(draft, definition, false);
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                return new CommanderSkillWorkshopWriteResult(
                    true,
                    $"저장 완료: {path}",
                    definition);
            }
            catch (Exception exception)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
                Debug.LogException(exception);
                return new CommanderSkillWorkshopWriteResult(false, $"오류: 저장 실패 - {exception.Message}");
            }
        }

        public static CommanderSkillWorkshopWriteResult Update(
            CommanderSkillWorkshopDraft draft,
            CommanderSkillDefinition loaded)
        {
            draft?.NormalizeCatalogOptions();
            var validation = CommanderSkillWorkshopValidator.Validate(draft);
            if (!validation.IsValid)
            {
                return Failed(validation);
            }
            if (loaded == null)
            {
                return new CommanderSkillWorkshopWriteResult(false, "오류: 갱신할 현재 자산이 없습니다.");
            }
            if (!string.Equals(loaded.SkillId, draft.SkillId, StringComparison.Ordinal))
            {
                return new CommanderSkillWorkshopWriteResult(
                    false,
                    "오류: 현재 저장에서는 스킬 ID를 바꿀 수 없습니다. 새 이름으로 저장을 사용하세요.");
            }
            if (!MatchesType(loaded, draft.Category))
            {
                return new CommanderSkillWorkshopWriteResult(
                    false,
                    "오류: 공격형과 효과형은 자산 타입이 다릅니다. 종류를 바꿨다면 새 이름으로 저장하세요.");
            }

            var path = AssetDatabase.GetAssetPath(loaded);
            if (string.IsNullOrWhiteSpace(path))
            {
                return new CommanderSkillWorkshopWriteResult(false, "오류: 현재 자산 경로를 찾을 수 없습니다.");
            }

            var snapshots = CaptureSnapshots(path);
            try
            {
                ConfigureDefinition(draft, loaded, path);
                SynchronizeRegistration(draft, loaded, true);
                EditorUtility.SetDirty(loaded);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                return new CommanderSkillWorkshopWriteResult(true, $"현재 자산 갱신 완료: {path}", loaded);
            }
            catch (Exception exception)
            {
                RestoreSnapshots(path, snapshots);
                Debug.LogException(exception);
                return new CommanderSkillWorkshopWriteResult(false, $"오류: 갱신 실패 - {exception.Message}");
            }
        }

        private static void ConfigureDefinition(
            CommanderSkillWorkshopDraft draft,
            CommanderSkillDefinition definition,
            string path)
        {
            var keep = new HashSet<UnityEngine.Object> { definition };
            var targeting = GetOrCreateSubAsset<CommanderSkillTargetingDefinition>(
                path,
                TargetingName,
                definition,
                keep);
            targeting.EditorConfigure(draft.TargetTeam, draft.TargetSelection, draft.TargetRange);
            EditorUtility.SetDirty(targeting);
            var castingSfx = ResolveSfx(
                path,
                CastingSfxName,
                definition,
                keep,
                draft.CastingSound,
                draft.CastingSfxSource);
            var activationSfx = ResolveSfx(
                path,
                ActivationSfxName,
                definition,
                keep,
                draft.CastSound,
                draft.CastSfxSource);
            var impactSfx = ResolveSfx(
                path,
                ImpactSfxName,
                definition,
                keep,
                draft.ImpactSound,
                draft.ImpactSfxSource);

            if (definition is CommanderAttackSkillDefinition attack)
            {
                var damage = GetOrCreateSubAsset<CommanderAreaDamageEffectDefinition>(
                    path,
                    DamageName,
                    definition,
                    keep);
                damage.EditorConfigure(
                    $"{draft.SkillId}_damage",
                    draft.DamageKind,
                    draft.BaseDamage,
                    draft.PerHitMultiplier,
                    draft.Shape,
                    draft.Center,
                    draft.Radius,
                    draft.ForwardOffset,
                    draft.Angle,
                    draft.LineWidth,
                    draft.MaxTargets);
                EditorUtility.SetDirty(damage);
                var attackEffects = new CommanderSkillEffectDefinition[draft.Effects.Count + 1];
                attackEffects[0] = damage;
                for (var index = 0; index < draft.Effects.Count; index++)
                {
                    attackEffects[index + 1] = BuildDraftEffect(draft, draft.Effects[index], index, path, definition, keep);
                }
                attack.EditorConfigure(
                    draft.SkillId,
                    draft.DisplayName,
                    draft.Description,
                    draft.Icon,
                    draft.CastTime,
                    draft.Cooldown,
                    targeting,
                    attackEffects,
                    draft.DeliveryModule,
                    draft.ProjectilePrefab,
                    draft.ProjectileSpeed,
                    draft.Trajectory,
                    draft.ArcHeight,
                    draft.CastVfxPrefab,
                    draft.CastVfxLifetime,
                    activationSfx,
                    draft.ImpactVfxPrefab,
                    draft.ImpactVfxLifetime,
                    impactSfx);
            }
            else if (definition is CommanderEffectSkillDefinition effectSkill)
            {
                var effects = new CommanderSkillEffectDefinition[draft.Effects.Count];
                for (var index = 0; index < draft.Effects.Count; index++)
                {
                    effects[index] = BuildDraftEffect(draft, draft.Effects[index], index, path, definition, keep);
                }
                effectSkill.EditorConfigure(
                    draft.SkillId,
                    draft.DisplayName,
                    draft.Description,
                    draft.Icon,
                    draft.Category,
                    draft.CastTime,
                    draft.Cooldown,
                    targeting,
                    effects,
                    draft.CastVfxPrefab,
                    draft.CastVfxLifetime,
                    activationSfx,
                    draft.ImpactVfxPrefab,
                    draft.ImpactVfxLifetime,
                    impactSfx);
            }

            EditorUtility.SetDirty(definition);
            var pattern = new CommanderSkillPatternConfig();
            pattern.EditorConfigure(draft.PatternType, draft.RepeatCount, draft.RepeatInterval,
                draft.PatternDuration, draft.TickInterval, draft.RandomRadius,
                draft.ChainCount, draft.ChainRadius);
            definition.EditorConfigureV2(draft.Rarity, pattern);
            definition.EditorConfigureFeedbackTransforms(
                draft.CastVfxLocalOffset,
                draft.CastVfxLocalEuler,
                draft.CastVfxScale,
                draft.ImpactVfxLocalOffset,
                draft.ImpactVfxLocalEuler,
                draft.ImpactVfxScale);
            definition.EditorConfigureCastingFeedback(
                draft.CastingVfxPrefab,
                draft.CastingVfxLifetime,
                castingSfx,
                draft.CastingVfxLocalOffset,
                draft.CastingVfxLocalEuler,
                draft.CastingVfxScale);
            definition.EditorConfigurePersistentFeedback(draft.PersistentVfxPrefab,
                draft.PersistentVfxLocalOffset, draft.PersistentVfxLocalEuler,
                draft.PersistentVfxScale, draft.PersistentVfxAnchor);
            if (!definition.TryValidate(out var error))
            {
                throw new InvalidOperationException($"런타임 정의 검증 실패: {error}");
            }

            var localAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (var index = 0; index < localAssets.Length; index++)
            {
                var local = localAssets[index];
                if (local == null || keep.Contains(local) || !IsWorkshopOwnedSubAsset(local))
                {
                    continue;
                }
                UnityEngine.Object.DestroyImmediate(local, true);
            }
        }

        private static CommanderSkillEffectDefinition BuildDraftEffect(CommanderSkillWorkshopDraft draft,
            CommanderSkillWorkshopEffectDraft source, int index, string path,
            CommanderSkillDefinition owner, HashSet<UnityEngine.Object> keep)
        {
            if (source.Kind == CommanderSkillWorkshopEffectKind.CommanderMark)
            {
                if (source.SharedMarkDefinition != null) return source.SharedMarkDefinition;
                var triggerEffects = new List<CommanderSkillEffectDefinition>();
                for (var triggerIndex = 0; triggerIndex < source.TriggerEffects.Count; triggerIndex++)
                {
                    triggerEffects.Add(BuildTriggerEffect(draft, source.TriggerEffects[triggerIndex], index,
                        triggerIndex, path, owner, keep));
                }
                if (triggerEffects.Count == 0 && source.TriggerDamage > 0f)
                {
                    var legacyTrigger = GetOrCreateSubAsset<CommanderAreaDamageEffectDefinition>(path,
                        $"__MarkTrigger_{index + 1:00}_01", owner, keep);
                    legacyTrigger.EditorConfigure($"{source.EffectId}_trigger_damage", draft.DamageKind,
                        source.TriggerDamage, source.TriggerPerHitMultiplier, MonsterBasicAttackShape.Circle,
                        MonsterBasicAttackCenter.PrimaryTarget, source.Radius, 0f, 90f, 1f, source.MaxTargets);
                    EditorUtility.SetDirty(legacyTrigger);
                    triggerEffects.Add(legacyTrigger);
                }
                var mark = GetOrCreateSubAsset<CommanderMarkEffectDefinition>(path,
                    $"{EffectPrefix}{index + 1:00}", owner, keep);
                mark.EditorConfigure(source.EffectId, source.MarkId, source.Duration, source.Scope,
                    source.Radius, source.MaxTargets, source.MarkTrigger, source.RequiredHits,
                    source.RequiredStacks, source.MarkMaxStacks, source.ConsumeOnTrigger,
                    source.RefreshDurationOnApply, source.TriggerCooldown,
                    triggerEffects.ToArray());
                mark.EditorConfigureRecording(source.RecordHitCount);
                mark.EditorConfigureDamageOriginFilter(source.CountBasicAttack, source.CountMonsterSkill,
                    source.CountCommanderSkill, source.CountCommanderMarkTrigger);
                mark.EditorConfigureFeedback(
                    BuildMarkFeedback(source.OnApply, "OnApply", index, path, owner, keep),
                    BuildMarkFeedback(source.Loop, "Loop", index, path, owner, keep),
                    BuildMarkFeedback(source.OnStack, "OnStack", index, path, owner, keep),
                    BuildMarkFeedback(source.OnTrigger, "OnTrigger", index, path, owner, keep),
                    BuildMarkFeedback(source.OnRemove, "OnRemove", index, path, owner, keep));
                EditorUtility.SetDirty(mark);
                return mark;
            }
            if (source.Kind == CommanderSkillWorkshopEffectKind.AreaDamage)
            {
                var damage = GetOrCreateSubAsset<CommanderAreaDamageEffectDefinition>(path,
                    $"{EffectPrefix}{index + 1:00}", owner, keep);
                ConfigureDamageEffect(damage, source);
                return damage;
            }
            if (source.Kind == CommanderSkillWorkshopEffectKind.RecordedHitDamage)
            {
                var recorded = GetOrCreateSubAsset<CommanderRecordedHitDamageEffectDefinition>(path,
                    $"{EffectPrefix}{index + 1:00}", owner, keep);
                recorded.EditorConfigure(source.EffectId, source.RecordedBaseMultiplier,
                    source.RecordedMultiplierPerHit, source.MaximumRecordedHits);
                EditorUtility.SetDirty(recorded);
                return recorded;
            }
            if (source.Kind == CommanderSkillWorkshopEffectKind.GlobalModifier)
            {
                var modifier = GetOrCreateSubAsset<CommanderGlobalModifierEffectDefinition>(path,
                    $"{EffectPrefix}{index + 1:00}", owner, keep);
                modifier.EditorConfigure(source.EffectId, source.Duration, source.MarkRequiredHitsMultiplier,
                    source.MarkTriggerDamageMultiplier, source.CooldownRecoveryMultiplier);
                EditorUtility.SetDirty(modifier);
                return modifier;
            }
            var unit = GetOrCreateSubAsset<CommanderUnitEffectDefinition>(path,
                $"{EffectPrefix}{index + 1:00}", owner, keep);
            unit.EditorConfigure(source.EffectId, source.EffectType, source.ValueSource, source.Magnitude,
                source.Duration, source.Scope, source.Radius, source.MaxTargets, source.StackPolicy);
            EditorUtility.SetDirty(unit);
            return unit;
        }

        private static CommanderSkillEffectDefinition BuildTriggerEffect(CommanderSkillWorkshopDraft draft,
            CommanderSkillWorkshopEffectDraft source, int markIndex, int triggerIndex, string path,
            CommanderSkillDefinition owner, HashSet<UnityEngine.Object> keep)
        {
            var objectName = $"__MarkTrigger_{markIndex + 1:00}_{triggerIndex + 1:00}";
            if (source.Kind == CommanderSkillWorkshopEffectKind.AreaDamage)
            {
                var damage = GetOrCreateSubAsset<CommanderAreaDamageEffectDefinition>(path, objectName, owner, keep);
                ConfigureDamageEffect(damage, source);
                return damage;
            }
            if (source.Kind == CommanderSkillWorkshopEffectKind.RecordedHitDamage)
            {
                var recorded = GetOrCreateSubAsset<CommanderRecordedHitDamageEffectDefinition>(path,
                    objectName, owner, keep);
                recorded.EditorConfigure(source.EffectId, source.RecordedBaseMultiplier,
                    source.RecordedMultiplierPerHit, source.MaximumRecordedHits);
                EditorUtility.SetDirty(recorded);
                return recorded;
            }
            if (source.Kind != CommanderSkillWorkshopEffectKind.UnitEffect)
                throw new InvalidOperationException($"Mark Trigger는 {source.Kind} 효과를 지원하지 않습니다.");
            var unit = GetOrCreateSubAsset<CommanderUnitEffectDefinition>(path, objectName, owner, keep);
            unit.EditorConfigure(source.EffectId, source.EffectType, source.ValueSource, source.Magnitude,
                source.Duration, source.Scope, source.Radius, source.MaxTargets, source.StackPolicy);
            EditorUtility.SetDirty(unit);
            return unit;
        }

        private static void ConfigureDamageEffect(CommanderAreaDamageEffectDefinition damage,
            CommanderSkillWorkshopEffectDraft source)
        {
            damage.EditorConfigure(source.EffectId, source.DamageKind, source.BaseDamage,
                source.PerHitMultiplier, source.DamageShape, source.DamageCenter, source.Radius,
                source.ForwardOffset, source.Angle, source.LineWidth, source.MaxTargets);
            EditorUtility.SetDirty(damage);
        }

        private static CommanderMarkFeedbackSlot BuildMarkFeedback(CommanderMarkFeedbackDraft source,
            string slotName, int effectIndex, string path, CommanderSkillDefinition owner,
            HashSet<UnityEngine.Object> keep)
        {
            source ??= new CommanderMarkFeedbackDraft();
            var cue = ResolveSfx(path, $"{MarkSfxPrefix}{effectIndex + 1:00}_{slotName}", owner, keep,
                source.Sound, source.SfxSource);
            var slot = new CommanderMarkFeedbackSlot();
            slot.EditorConfigure(source.VfxPrefab, source.Lifetime, source.LocalOffset, source.LocalEuler,
                source.Scale, cue, source.Anchor);
            return slot;
        }

        private static T GetOrCreateSubAsset<T>(
            string path,
            string objectName,
            CommanderSkillDefinition owner,
            HashSet<UnityEngine.Object> keep)
            where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<T>()
                .FirstOrDefault(candidate => candidate.name == objectName);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<T>();
                existing.name = objectName;
                existing.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(existing, owner);
            }
            keep.Add(existing);
            return existing;
        }

        private static bool IsWorkshopOwnedSubAsset(UnityEngine.Object asset)
        {
            return asset != null &&
                   (asset.name == TargetingName || asset.name == DamageName ||
                    asset.name.StartsWith(EffectPrefix, StringComparison.Ordinal) ||
                    asset.name.StartsWith("__MarkTrigger_", StringComparison.Ordinal) ||
                    asset.name.StartsWith(GeneratedSfxPrefix, StringComparison.Ordinal));
        }

        private static SfxCue ResolveSfx(
            string path,
            string objectName,
            CommanderSkillDefinition owner,
            HashSet<UnityEngine.Object> keep,
            AudioClip sound,
            SfxCue sourceCue)
        {
            if (sound == null)
            {
                return null;
            }

            if (sourceCue != null && sourceCue.PrimaryClip == sound)
            {
                if (string.Equals(
                        AssetDatabase.GetAssetPath(sourceCue),
                        path,
                        StringComparison.OrdinalIgnoreCase) &&
                    sourceCue.name.StartsWith(GeneratedSfxPrefix, StringComparison.Ordinal))
                {
                    keep.Add(sourceCue);
                }
                return sourceCue;
            }

            var cue = GetOrCreateSubAsset<SfxCue>(path, objectName, owner, keep);
            cue.EditorConfigure(
                new[] { sound },
                DefaultSfxVolume,
                DefaultSfxPitch,
                1f,
                0.05f,
                SfxPriority.Normal);
            EditorUtility.SetDirty(cue);
            return cue;
        }

        private static CommanderSkillDefinition CreateDefinition(CommanderSkillCategory category)
        {
            return category == CommanderSkillCategory.Attack
                ? ScriptableObject.CreateInstance<CommanderAttackSkillDefinition>()
                : ScriptableObject.CreateInstance<CommanderEffectSkillDefinition>();
        }

        private static bool MatchesType(CommanderSkillDefinition definition, CommanderSkillCategory category)
        {
            return category == CommanderSkillCategory.Attack
                ? definition is CommanderAttackSkillDefinition
                : definition is CommanderEffectSkillDefinition;
        }

        private static void SynchronizeRegistration(
            CommanderSkillWorkshopDraft draft,
            CommanderSkillDefinition definition,
            bool removeWhenDisabled)
        {
            if (!draft.RegisterInCatalog && !removeWhenDisabled)
            {
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<CommanderSkillCatalog>(CatalogPath);
            var balance = AssetDatabase.LoadAssetAtPath<CommanderSkillBalanceConfig>(BalancePath);
            var summon = AssetDatabase.LoadAssetAtPath<CommanderSkillSummonConfig>(SummonPath);
            if (catalog == null || balance == null || summon == null)
            {
                throw new InvalidOperationException(
                    "군단장 Skill Catalog, Balance Config 또는 Summon Config를 찾을 수 없습니다.");
            }

            ApplyRegistrationChange(draft, definition, catalog, balance, summon);
        }

        private static void ApplyRegistrationChange(
            CommanderSkillWorkshopDraft draft,
            CommanderSkillDefinition definition,
            CommanderSkillCatalog catalog,
            CommanderSkillBalanceConfig balance,
            CommanderSkillSummonConfig summon)
        {
            if (draft.RegisterInCatalog && draft.IncludeInSummonPool &&
                draft.MinimumSummonLevel > summon.Levels.Count)
            {
                throw new InvalidOperationException(
                    $"소환 해금 단계가 현재 최대 단계({summon.Levels.Count})를 넘었습니다.");
            }

            if (draft.RegisterInCatalog)
            {
                var duplicate = catalog.Skills.FirstOrDefault(candidate =>
                    candidate != null && candidate != definition &&
                    string.Equals(candidate.SkillId, draft.SkillId, StringComparison.Ordinal));
                if (duplicate != null)
                {
                    throw new InvalidOperationException($"Catalog에 같은 ID가 이미 있습니다: {draft.SkillId}");
                }
            }

            var catalogJson = EditorJsonUtility.ToJson(catalog);
            var balanceJson = EditorJsonUtility.ToJson(balance);
            var summonJson = EditorJsonUtility.ToJson(summon);
            try
            {
                var definitions = catalog.Skills
                    .Where(candidate => candidate != null && candidate != definition)
                    .ToList();
                if (draft.RegisterInCatalog)
                {
                    definitions.Add(definition);
                }

                var rules = balance.SkillRules
                    .Where(rule => rule != null &&
                                   !string.Equals(rule.SkillId, draft.SkillId, StringComparison.Ordinal))
                    .ToList();
                if (draft.RegisterInCatalog)
                {
                    var curve = AnimationCurve.Linear(
                        1f,
                        1f,
                        draft.MaxLevel,
                        draft.MaxLevelEffectMultiplier);
                    curve.preWrapMode = WrapMode.ClampForever;
                    curve.postWrapMode = WrapMode.ClampForever;
                    var newRule = new CommanderSkillGrowthRule();
                    newRule.EditorConfigure(
                        draft.SkillId,
                        draft.MaxLevel,
                        draft.RequiredDuplicateCount,
                        curve,
                        draft.BaseGoldCost,
                        draft.GoldCostGrowthMultiplier);
                    rules.Add(newRule);
                }

                balance.EditorConfigure(rules.ToArray());
                summon.EditorConfigure(
                    summon.TicketItemId,
                    BuildSummonLevels(summon, draft),
                    CloneSummonOffers(summon),
                    summon.DiamondCostPerMissingTicket);
                catalog.EditorConfigure(balance, summon, definitions.ToArray());
                EditorUtility.SetDirty(balance);
                EditorUtility.SetDirty(summon);
                EditorUtility.SetDirty(catalog);
                if (!catalog.TryValidate(out var error))
                {
                    throw new InvalidOperationException($"Catalog 검증 실패: {error}");
                }
            }
            catch
            {
                EditorJsonUtility.FromJsonOverwrite(balanceJson, balance);
                EditorJsonUtility.FromJsonOverwrite(summonJson, summon);
                EditorJsonUtility.FromJsonOverwrite(catalogJson, catalog);
                EditorUtility.SetDirty(balance);
                EditorUtility.SetDirty(summon);
                EditorUtility.SetDirty(catalog);
                throw;
            }
        }

        private static CommanderSkillSummonLevelRule[] BuildSummonLevels(
            CommanderSkillSummonConfig summon,
            CommanderSkillWorkshopDraft draft)
        {
            var levels = new CommanderSkillSummonLevelRule[summon.Levels.Count];
            for (var levelIndex = 0; levelIndex < summon.Levels.Count; levelIndex++)
            {
                var sourceLevel = summon.Levels[levelIndex];
                if (sourceLevel == null)
                {
                    throw new InvalidOperationException($"소환 {levelIndex + 1}단계 설정이 비어 있습니다.");
                }

                var entries = new List<CommanderSkillSummonPoolEntry>();
                var sourcePool = sourceLevel.Pool;
                for (var entryIndex = 0; entryIndex < sourcePool.Count; entryIndex++)
                {
                    var source = sourcePool[entryIndex];
                    if (source == null ||
                        string.Equals(source.SkillId, draft.SkillId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var clone = new CommanderSkillSummonPoolEntry();
                    clone.EditorConfigure(source.SkillId, source.Weight);
                    entries.Add(clone);
                }

                if (draft.RegisterInCatalog && draft.IncludeInSummonPool &&
                    levelIndex + 1 >= draft.MinimumSummonLevel)
                {
                    var entry = new CommanderSkillSummonPoolEntry();
                    entry.EditorConfigure(draft.SkillId, draft.SummonWeight);
                    entries.Add(entry);
                }

                var level = new CommanderSkillSummonLevelRule();
                level.EditorConfigure(sourceLevel.RequiredAccumulatedCount, entries.ToArray());
                levels[levelIndex] = level;
            }
            return levels;
        }

        private static CommanderSkillSummonOffer[] CloneSummonOffers(CommanderSkillSummonConfig summon)
        {
            var offers = new CommanderSkillSummonOffer[summon.Offers.Count];
            for (var index = 0; index < summon.Offers.Count; index++)
            {
                var source = summon.Offers[index];
                if (source == null)
                {
                    throw new InvalidOperationException($"소환 상품 {index + 1} 설정이 비어 있습니다.");
                }

                var clone = new CommanderSkillSummonOffer();
                clone.EditorConfigure(source.DrawCount, source.TicketCost);
                offers[index] = clone;
            }
            return offers;
        }

        private static List<ObjectSnapshot> CaptureSnapshots(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .Where(asset => asset != null)
                .Select(asset => new ObjectSnapshot(asset, EditorJsonUtility.ToJson(asset)))
                .ToList();
        }

        private static void RestoreSnapshots(string path, IReadOnlyList<ObjectSnapshot> snapshots)
        {
            var originalObjects = new HashSet<UnityEngine.Object>(snapshots.Select(snapshot => snapshot.Target));
            var current = AssetDatabase.LoadAllAssetsAtPath(path);
            for (var index = 0; index < current.Length; index++)
            {
                if (current[index] != null && !originalObjects.Contains(current[index]) &&
                    IsWorkshopOwnedSubAsset(current[index]))
                {
                    UnityEngine.Object.DestroyImmediate(current[index], true);
                }
            }
            for (var index = 0; index < snapshots.Count; index++)
            {
                var snapshot = snapshots[index];
                if (snapshot.Target == null)
                {
                    continue;
                }
                EditorJsonUtility.FromJsonOverwrite(snapshot.Json, snapshot.Target);
                EditorUtility.SetDirty(snapshot.Target);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static CommanderSkillWorkshopWriteResult Failed(CommanderSkillWorkshopValidation validation)
        {
            return new CommanderSkillWorkshopWriteResult(
                false,
                "오류: " + string.Join(" / ", validation.Errors));
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }
                current = next;
            }
        }

        private readonly struct ObjectSnapshot
        {
            public ObjectSnapshot(UnityEngine.Object target, string json)
            {
                Target = target;
                Json = json;
            }

            public UnityEngine.Object Target { get; }
            public string Json { get; }
        }
    }
}
