using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectMT.Contents.CastleRaidHex;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public sealed class MonsterMakerWriteResult
    {
        public MonsterMakerWriteResult(
            MonsterDefinition definition,
            bool updatedExisting,
            IReadOnlyList<string> outputPaths,
            IReadOnlyDictionary<string, string> guidBefore,
            IReadOnlyDictionary<string, string> guidAfter,
            MonsterValidationReport validation)
        {
            Definition = definition;
            UpdatedExisting = updatedExisting;
            OutputPaths = outputPaths;
            GuidBefore = guidBefore;
            GuidAfter = guidAfter;
            Validation = validation;
        }

        public MonsterDefinition Definition { get; }
        public bool UpdatedExisting { get; }
        public IReadOnlyList<string> OutputPaths { get; }
        public IReadOnlyDictionary<string, string> GuidBefore { get; }
        public IReadOnlyDictionary<string, string> GuidAfter { get; }
        public MonsterValidationReport Validation { get; }
    }

    public static class MonsterMakerAssetWriter // 검증 통과 산출물을 만들고 마지막에 Catalog를 갱신
    {
        public const string MonsterCatalogPath = "Assets/ProjectMT/02_Shared/Unit/Data/MonsterCatalog.asset";
        public const string MonsterRarityCatalogPath = "Assets/ProjectMT/02_Shared/Unit/Data/MonsterRarityCatalog.asset";
        public const string DataRoot = "Assets/ProjectMT/02_Shared/Unit/Data/Monsters";
        public const string ArtRoot = "Assets/ProjectMT/05_Art/Monsters";
        public const string DraftRoot = "Assets/ProjectMT/Editor/MonsterMaker/Drafts";
        public const string CastleRaidAIProfileCatalogPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Resources/HexCastleAssaultAIProfileCatalog.asset";
        public const string MainBattleAIProfileCatalogPath =
            "Assets/ProjectMT/03_Features/MainBattle/Resources/MainBattleAIProfileCatalog.asset";
        public const string DefaultProjectilePrefabPath =
            "Assets/ProjectMT/02_Shared/Combat/Prefabs/PF_SeedProjectile.prefab";

        public static MonsterMakerWriteResult BuildAndRegister(
            MonsterMakerDraft draft,
            MonsterCatalog catalog = null,
            MonsterRarityCatalog rarityCatalog = null)
        {
            draft?.EditorSyncActiveAttackAuthoring();
            var preflight = MonsterMakerValidator.Validate(draft);
            if (preflight.HasErrors)
            {
                throw new InvalidOperationException(BuildIssueText(preflight.Issues));
            }

            catalog ??= AssetDatabase.LoadAssetAtPath<MonsterCatalog>(MonsterCatalogPath);
            rarityCatalog ??= AssetDatabase.LoadAssetAtPath<MonsterRarityCatalog>(MonsterRarityCatalogPath);
            if (catalog == null || rarityCatalog == null)
            {
                throw new InvalidOperationException("MonsterCatalog 또는 MonsterRarityCatalog을 찾을 수 없습니다.");
            }

            var paths = BuildPaths(draft.MonsterId);
            var generatesUniquePassive = draft.UsePassiveSkill &&
                                         draft.RarityPassiveSkill is GenericMonsterPassiveSkill;
            var generatesAttackActive = draft.UseActiveSkill &&
                                        draft.Rarity >= MonsterRarity.Legendary &&
                                        draft.HasActiveProfile;
            var passivePath = BuildPassivePath(draft.MonsterId);
            var extraPaths = new List<string>();
            if (generatesUniquePassive) extraPaths.Add(passivePath);
            if (generatesAttackActive) extraPaths.Add(BuildActivePath(draft.MonsterId));
            var outputPaths = paths.Concat(extraPaths).ToArray();
            var dataFolder = DataRoot + "/" + draft.MonsterId;
            var artFolder = ArtRoot + "/" + draft.MonsterId;
            var catalogPath = RequirePersistentAssetPath(catalog, "MonsterCatalog");
            var rarityCatalogPath = RequirePersistentAssetPath(rarityCatalog, "MonsterRarityCatalog");
            ValidateProductionDraftOwnership(draft, catalogPath, rarityCatalogPath);
            var writesProductionAiCatalog =
                string.Equals(catalogPath, MonsterCatalogPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(rarityCatalogPath, MonsterRarityCatalogPath, StringComparison.OrdinalIgnoreCase);
            var transactionPaths = outputPaths.Concat(new[] { catalogPath, rarityCatalogPath });
            if (writesProductionAiCatalog)
            {
                transactionPaths = transactionPaths.Concat(new[]
                {
                    CastleRaidAIProfileCatalogPath,
                    MainBattleAIProfileCatalogPath
                });
            }
            var transaction = MonsterMakerWriteTransaction.Capture(
                transactionPaths,
                new[] { dataFolder, artFolder });

            try
            {
                EnsureFolder("Assets/ProjectMT/02_Shared/Unit/Data", "Monsters");
                EnsureFolder(DataRoot, draft.MonsterId);
                EnsureFolder("Assets/ProjectMT/05_Art", "Monsters");
                EnsureFolder(ArtRoot, draft.MonsterId);
                if (writesProductionAiCatalog)
                {
                    EnsureFolder("Assets/ProjectMT/04_Contents/01_CastleRaid", "Resources");
                    EnsureFolder("Assets/ProjectMT/03_Features/MainBattle", "Resources");
                }

                var guidBefore = outputPaths.ToDictionary(path => path, AssetDatabase.AssetPathToGUID);
                var updatedExisting = !string.IsNullOrEmpty(guidBefore[paths[0]]);

                var uniquePassive = generatesUniquePassive
                    ? BuildOrUpdateMonsterPassiveAsset(draft)
                    : null;

                var body = GetOrCreateAsset<MonsterBodyProfile>(paths[1], "MB_" + draft.MonsterId);
                var motion = GetOrCreateAsset<MonsterMotionProfile>(paths[2], "MM_" + draft.MonsterId);
                var combat = GetOrCreateAsset<MonsterCombatProfile>(paths[3], "MC_" + draft.MonsterId);
                var ascension = GetOrCreateAsset<MonsterAscensionProfile>(paths[4], "MA_" + draft.MonsterId);
                var feedback = GetOrCreateAsset<MonsterFeedbackProfile>(paths[5], "MF_" + draft.MonsterId);
                var castleRaidAiCatalog = writesProductionAiCatalog
                    ? GetOrCreateAsset<HexCastleAssaultAIProfileCatalog>(
                        CastleRaidAIProfileCatalogPath,
                        "HexCastleAssaultAIProfileCatalog")
                    : null;
                var mainBattleAiCatalog = writesProductionAiCatalog
                    ? GetOrCreateAsset<MainBattleAIProfileCatalog>(
                        MainBattleAIProfileCatalogPath,
                        "MainBattleAIProfileCatalog")
                    : null;
                var generatedSfx = new MonsterMakerGeneratedSfxWriter(feedback, paths[5], draft.MonsterId);

            body.EditorConfigure(
                draft.VisualScale,
                draft.VisualLocalPosition,
                draft.GroundOffset,
                draft.FacingYawOffset,
                draft.BodyRadius,
                draft.BodyHeight,
                draft.SelectionRadius,
                draft.HpBarHeight,
                "Visual" + PrefixPath(MonsterMakerValidator.ResolveAnimatorPath(draft)),
                draft.AttackOriginPath,
                draft.HitCenterPath,
                draft.RigMode,
                draft.PreviewScale,
                draft.VfxScale);

            var idle = new MonsterMotionSlot();
            idle.EditorConfigure(draft.IdleClip, draft.IdleSpeed, 0.08f, true);
            var move = new MonsterMotionSlot();
            move.EditorConfigure(draft.MoveClip, draft.MovePlaybackSpeed, 0.08f, true);
            MonsterMotionSlot active = null;
            var activeStepMotions = Array.Empty<MonsterActiveStepMotion>();
            if (generatesAttackActive)
            {
                activeStepMotions = draft.CurrentActivePresentations
                    .Where(source => source != null)
                    .Select(source =>
                    {
                        draft.ResolveActiveStepMotion(
                            source,
                            out var clip,
                            out var playbackSpeed,
                            out var crossFadeDuration,
                            out var commitNormalizedTime);
                        var motion = new MonsterActiveStepMotion();
                        motion.EditorConfigure(
                            source.StepId,
                            clip,
                            playbackSpeed,
                            crossFadeDuration,
                            commitNormalizedTime);
                        return motion;
                    })
                    .ToArray();
                var firstMotion = activeStepMotions.FirstOrDefault();
                active = new MonsterMotionSlot();
                active.EditorConfigure(
                    firstMotion?.Clip,
                    firstMotion?.PlaybackSpeed ?? 1f,
                    firstMotion?.CrossFadeDuration ?? 0.08f,
                    false);
            }
            var death = new MonsterMotionSlot();
            death.EditorConfigure(
                draft.DeathClip,
                draft.DeathSpeed,
                0.08f,
                false,
                CreateFeedbackCue(draft.DeathFeedback, generatedSfx, "Death"));
            var attacks = new MonsterAttackMotion[draft.Attacks.Count];
            for (var attackIndex = 0; attackIndex < draft.Attacks.Count; attackIndex++)
            {
                var source = draft.Attacks[attackIndex];
                var markers = new MonsterAttackMarker[source.Markers.Count];
                for (var markerIndex = 0; markerIndex < source.Markers.Count; markerIndex++)
                {
                    var markerDraft = source.Markers[markerIndex];
                    var marker = new MonsterAttackMarker();
                    marker.EditorConfigure(
                        markerDraft.NormalizedTime,
                        markerDraft.PowerRatio,
                        null,
                        markerDraft.SocketOverride);
                    markers[markerIndex] = marker;
                }

                var attack = new MonsterAttackMotion();
                attack.EditorConfigure(
                    source.MotionId,
                    source.Clip,
                    source.PlaybackSpeed,
                    source.CrossFadeDuration,
                    source.Weight,
                    source.PreventImmediateRepeat,
                    markers,
                    null,
                    source.OverrideBreathDuration,
                    source.BreathDuration);
                attacks[attackIndex] = attack;
            }

            motion.EditorConfigure(idle, move, attacks, active, activeStepMotions, death);
            var action = ConfigureCombatAction(combat, paths[3], draft, generatedSfx);
            combat.EditorConfigure(
                draft.BasicAttackProfile != null ? draft.BasicAttackProfile.CombatType : draft.CombatType,
                action);
            combat.EditorSetImpact(draft.ImpactStrength, draft.ReactionWeight);

            MonsterAbilityDefinition ability2 = null;
            MonsterAbilityDefinition ability4 = null;
            if (draft.AscensionConfigured)
            {
                ability2 = GetOrCreateAbility(ascension, paths[4], "Ability_" + draft.MonsterId + "_A2");
                ability4 = GetOrCreateAbility(ascension, paths[4], "Ability_" + draft.MonsterId + "_A4");
                if (draft.UsePassiveSkill)
                {
                    ability2.EditorConfigureSkillAugment(
                        draft.Ascension2.AbilityId,
                        draft.Ascension2.DisplayName,
                        MonsterSkillAugmentTarget.Passive,
                        draft.Ascension2.AugmentOperation,
                        draft.Ascension2.AugmentScalarValue,
                        draft.Ascension2.AugmentIntegerValue);
                }
                else
                {
                    ability2.EditorConfigure(
                        draft.Ascension2.AbilityId,
                        draft.Ascension2.DisplayName,
                        draft.Ascension2.Mode,
                        draft.Ascension2.TriggerPolicyId);
                }

                if (draft.UseActiveSkill && draft.Rarity >= MonsterRarity.Legendary)
                {
                    ability4.EditorConfigureSkillAugment(
                        draft.Ascension4.AbilityId,
                        draft.Ascension4.DisplayName,
                        MonsterSkillAugmentTarget.Active,
                        draft.Ascension4.AugmentOperation,
                        draft.Ascension4.AugmentScalarValue,
                        draft.Ascension4.AugmentIntegerValue);
                }
                else if (draft.UsePassiveSkill)
                {
                    ability4.EditorConfigureSkillAugment(
                        draft.Ascension4.AbilityId,
                        draft.Ascension4.DisplayName,
                        MonsterSkillAugmentTarget.Passive,
                        draft.Ascension4.AugmentOperation,
                        draft.Ascension4.AugmentScalarValue,
                        draft.Ascension4.AugmentIntegerValue);
                }
                else
                {
                    ability4.EditorConfigure(
                        draft.Ascension4.AbilityId,
                        draft.Ascension4.DisplayName,
                        draft.Ascension4.Mode,
                        draft.Ascension4.TriggerPolicyId);
                }
            }

            ascension.EditorConfigure(
                draft.AscensionConfigured,
                draft.Ascension1,
                ability2,
                draft.Ascension3,
                ability4,
                draft.Ascension5);

            feedback.EditorConfigure(
                CreateFeedbackCue(draft.SpawnFeedback, generatedSfx, "Spawn"),
                null,
                null,
                CreateFeedbackCue(draft.HitFeedback, generatedSfx, "HitReceived"),
                CreateFeedbackCue(draft.DeathFeedback, generatedSfx, "Death"),
                CreateFeedbackCue(draft.SpecialFeedback, generatedSfx, "Special"));
            feedback.EditorSetBasicAttackVfxBindings(
                CompileBasicAttackPresentationBindings(draft, generatedSfx));
            var attackActive = generatesAttackActive
                ? BuildOrUpdateMonsterActiveAsset(draft, generatedSfx)
                : null;
            generatedSfx.RemoveUnused();

            var controller = ConfigureAnimatorController(paths[7], motion);
            var adapter = ConfigureVisualAdapter(paths[8], draft, controller);
            var runtime = GetOrCreateAsset<MonsterRuntimeAssetSet>(paths[6], "MR_" + draft.MonsterId);
            runtime.EditorConfigure(adapter, controller, body, motion, combat, ascension, feedback);

            var definition = GetOrCreateAsset<MonsterDefinition>(paths[0], "MD_" + draft.MonsterId);
            if (!string.IsNullOrWhiteSpace(definition.MonsterId) &&
                !string.Equals(definition.MonsterId, draft.MonsterId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("기존 Definition의 안정 ID와 제작 원본 ID가 다릅니다.");
            }

            definition.EditorConfigure(
                draft.MonsterId,
                draft.MaxHealth,
                draft.AttackPower,
                draft.Defense,
                draft.AttackSpeed,
                draft.MoveSpeed,
                draft.AttackRange,
                draft.CombatType == MonsterCombatType.Ranged);
            definition.EditorConfigurePresentation(draft.DisplayName, draft.Portrait, draft.VendorPrefab);
            definition.EditorConfigureVisualTint(Color.white);
            definition.EditorConfigureFormalRuntime("monster/" + draft.MonsterId, runtime);

            if (castleRaidAiCatalog != null)
            {
                castleRaidAiCatalog.EditorUpsert(
                    draft.MonsterId,
                    draft.CastleRaidAiPattern,
                    draft.CastleRaidSupportFocus,
                    draft.CastleRaidSupportRange,
                    draft.CastleRaidSupportCooldown,
                    draft.CastleRaidSupportDuration,
                    draft.CastleRaidHealRatio,
                    draft.CastleRaidAttackBuffRate,
                    draft.CastleRaidDefenseDamageMultiplier);
                if (!castleRaidAiCatalog.TryValidate(out var aiCatalogError))
                {
                    throw new InvalidOperationException(aiCatalogError);
                }
            }

            if (mainBattleAiCatalog != null)
            {
                mainBattleAiCatalog.EditorUpsert(
                    draft.MonsterId,
                    draft.MainBattleRole,
                    draft.MainBattleTargetPriority,
                    draft.MainBattlePreferredRangeRatio,
                    draft.MainBattleRetreatRangeRatio,
                    draft.MainBattleRetargetInterval);
                if (!mainBattleAiCatalog.TryValidate(out var mainBattleAiError))
                {
                    throw new InvalidOperationException(mainBattleAiError);
                }
            }

            MarkDirty(
                body,
                motion,
                combat,
                action,
                ascension,
                ability2,
                ability4,
                feedback,
                runtime,
                definition,
                castleRaidAiCatalog,
                mainBattleAiCatalog);
            MarkDirty(uniquePassive);
            MarkDirty(attackActive, draft);
            SaveAssetsIfDirty(
                body,
                motion,
                combat,
                action,
                ascension,
                ability2,
                ability4,
                feedback,
                runtime,
                controller,
                adapter,
                definition,
                castleRaidAiCatalog,
                mainBattleAiCatalog);
            SaveAssetsIfDirty(uniquePassive);
            SaveAssetsIfDirty(attackActive, draft);
            generatedSfx.SaveIfDirty();
            AssetDatabase.ImportAsset(paths[8], ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(paths[7], ImportAssetOptions.ForceUpdate);

            definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(paths[0]);
            var outputValidation = MonsterDefinitionValidator.Validate(definition, true);
            if (outputValidation.HasErrors)
            {
                throw new InvalidOperationException(BuildRuntimeIssueText(outputValidation.Issues));
            }

            RegisterLast(catalog, rarityCatalog, definition, draft, uniquePassive, attackActive);
            SaveAssetsIfDirty(catalog, rarityCatalog, castleRaidAiCatalog, mainBattleAiCatalog);
            AssetDatabase.Refresh();

            if (!catalog.TryGet(draft.MonsterId, out var registered) || registered != definition)
            {
                throw new InvalidOperationException("생성물 검증 뒤 MonsterCatalog 등록을 확인하지 못했습니다.");
            }

            if (!rarityCatalog.TryGetRarity(draft.MonsterId, out var rarity) || rarity != draft.Rarity)
            {
                throw new InvalidOperationException("생성물 검증 뒤 MonsterRarityCatalog 등록을 확인하지 못했습니다.");
            }

            var assignedAiProfile = castleRaidAiCatalog?.Resolve(draft.MonsterId);
            if (castleRaidAiCatalog != null && (assignedAiProfile == null ||
                assignedAiProfile.Pattern != draft.CastleRaidAiPattern))
            {
                throw new InvalidOperationException("생성물 검증 뒤 Hex Castle Raid AI Profile 등록을 확인하지 못했습니다.");
            }

            if (mainBattleAiCatalog != null &&
                (!mainBattleAiCatalog.TryResolve(draft.MonsterId, out var mainBattleProfile) ||
                 mainBattleProfile.Role != draft.MainBattleRole ||
                 mainBattleProfile.TargetPriority != draft.MainBattleTargetPriority))
            {
                throw new InvalidOperationException("생성물 검증 뒤 MainBattle AI Profile 등록을 확인하지 못했습니다.");
            }

            var guidAfter = outputPaths.ToDictionary(path => path, AssetDatabase.AssetPathToGUID);
            for (var index = 0; index < outputPaths.Length; index++)
            {
                var before = guidBefore[outputPaths[index]];
                if (!string.IsNullOrEmpty(before) && !string.Equals(before, guidAfter[outputPaths[index]], StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("기존 Asset GUID가 변경되었습니다: " + outputPaths[index]);
                }
            }

                var result = new MonsterMakerWriteResult(
                    definition,
                    updatedExisting,
                    outputPaths,
                    guidBefore,
                    guidAfter,
                    outputValidation);
                transaction.Commit();
                return result;
            }
            catch (Exception buildException)
            {
                try
                {
                    transaction.Rollback();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException(
                        "Monster Maker 생성과 원상복구가 모두 실패했습니다.",
                        buildException,
                        rollbackException);
                }

                throw;
            }
            finally
            {
                transaction.Dispose();
            }
        }

        public static void SynchronizeBasicAttackRuntime(MonsterMakerDraft draft)
        {
            if (draft == null || draft.BasicAttackProfile == null)
            {
                throw new InvalidOperationException("동기화할 Maker Draft와 기본공격 Profile이 필요합니다.");
            }
            if (!draft.BasicAttackProfile.TryValidate(out var profileError))
            {
                throw new InvalidOperationException(profileError);
            }

            var paths = BuildPaths(draft.MonsterId);
            var combat = AssetDatabase.LoadAssetAtPath<MonsterCombatProfile>(paths[3]);
            var feedback = AssetDatabase.LoadAssetAtPath<MonsterFeedbackProfile>(paths[5]);
            if (combat == null || feedback == null)
            {
                throw new InvalidOperationException(
                    $"정식 기본공격 Runtime 자산을 찾을 수 없습니다: {draft.MonsterId}");
            }

            var transaction = MonsterMakerWriteTransaction.Capture(
                new[] { paths[3], paths[5] },
                Array.Empty<string>());
            try
            {
                var generatedSfx = new MonsterMakerGeneratedSfxWriter(
                    feedback,
                    paths[5],
                    draft.MonsterId);
                var action = ConfigureCombatAction(combat, paths[3], draft, generatedSfx);
                combat.EditorConfigure(draft.BasicAttackProfile.CombatType, action);
                var bindings = CompileBasicAttackPresentationBindings(draft, generatedSfx);
                for (var index = 0; index < bindings.Count; index++)
                {
                    if (!bindings[index].TryValidate(out var bindingError))
                    {
                        throw new InvalidOperationException(bindingError);
                    }
                }
                feedback.EditorSetBasicAttackVfxBindings(bindings);
                MarkDirty(combat, action, feedback);
                SaveAssetsIfDirty(combat, action, feedback);
                generatedSfx.SaveIfDirty();

                var syncState = MonsterBasicAttackBindingProjection.EvaluateRuntimeSync(
                    draft,
                    combat,
                    feedback,
                    out var syncMessage);
                if (syncState != MonsterBasicAttackRuntimeSyncState.Synchronized)
                {
                    throw new InvalidOperationException(
                        $"기본공격 Runtime 동기화 검증 실패: {syncMessage}");
                }

                transaction.Commit();
            }
            catch (Exception syncException)
            {
                try
                {
                    transaction.Rollback();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException(
                        "기본공격 Runtime 동기화와 원상복구가 모두 실패했습니다.",
                        syncException,
                        rollbackException);
                }
                throw;
            }
            finally
            {
                transaction.Dispose();
            }
        }

        public static MonsterAttackActiveSkill SynchronizeActiveAttackRuntime(
            MonsterMakerDraft draft,
            MonsterCatalog catalog = null,
            MonsterRarityCatalog rarityCatalog = null)
        {
            draft?.EditorSyncActiveAttackAuthoring();
            var preflight = MonsterMakerValidator.ValidateActiveAttack(draft);
            if (preflight.HasErrors)
            {
                throw new InvalidOperationException(BuildIssueText(preflight.Issues));
            }
            if (!EditorUtility.IsPersistent(draft))
            {
                throw new InvalidOperationException("액티브만 반영하려면 먼저 Maker 제작 원본을 저장하세요.");
            }

            catalog ??= AssetDatabase.LoadAssetAtPath<MonsterCatalog>(MonsterCatalogPath);
            rarityCatalog ??= AssetDatabase.LoadAssetAtPath<MonsterRarityCatalog>(MonsterRarityCatalogPath);
            if (catalog == null || rarityCatalog == null)
            {
                throw new InvalidOperationException("MonsterCatalog 또는 MonsterRarityCatalog을 찾을 수 없습니다.");
            }
            var catalogPath = RequirePersistentAssetPath(catalog, "MonsterCatalog");
            var rarityCatalogPath = RequirePersistentAssetPath(rarityCatalog, "MonsterRarityCatalog");
            ValidateProductionDraftOwnership(draft, catalogPath, rarityCatalogPath);
            var paths = BuildPaths(draft.MonsterId);
            var definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(paths[0]);
            var motion = AssetDatabase.LoadAssetAtPath<MonsterMotionProfile>(paths[2]);
            var feedback = AssetDatabase.LoadAssetAtPath<MonsterFeedbackProfile>(paths[5]);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(paths[7]);
            if (definition == null || motion == null || feedback == null || controller == null || rarityCatalog == null)
            {
                throw new InvalidOperationException(
                    "액티브만 반영할 정식 Monster Runtime 기반 자산이 없습니다. 먼저 전체 정식 생성·수정을 한 번 실행하세요.");
            }
            if (!catalog.TryGet(draft.MonsterId, out var registeredDefinition) || registeredDefinition != definition)
            {
                throw new InvalidOperationException(
                    "선택한 MonsterCatalog의 몬스터와 현재 Runtime Definition이 일치하지 않습니다.");
            }

            var activePath = BuildActivePath(draft.MonsterId);
            var draftPath = RequirePersistentAssetPath(draft, "Monster Maker 제작 원본");
            var transaction = MonsterMakerWriteTransaction.Capture(
                new[] { activePath, paths[2], paths[5], paths[7], rarityCatalogPath, draftPath },
                Array.Empty<string>());
            try
            {
                var generatedSfx = new MonsterMakerGeneratedSfxWriter(feedback, paths[5], draft.MonsterId);
                ConfigureActiveMotionProjection(draft, motion);
                controller = ConfigureAnimatorController(paths[7], motion);
                var active = BuildOrUpdateMonsterActiveAsset(draft, generatedSfx) as MonsterAttackActiveSkill ??
                             throw new InvalidOperationException("공격형 액티브 Runtime 자산을 만들지 못했습니다.");
                UpdateRarityActiveReference(rarityCatalog, definition, active);
                MarkDirty(active, draft, motion, feedback, controller, rarityCatalog);
                SaveAssetsIfDirty(active, draft, motion, feedback, controller, rarityCatalog);
                generatedSfx.SaveIfDirty();
                AssetDatabase.ImportAsset(paths[7], ImportAssetOptions.ForceUpdate);

                var state = MonsterActiveAttackBindingProjection.EvaluateRuntimeSync(
                    draft,
                    active,
                    motion,
                    out var syncMessage);
                if (state != MonsterActiveAttackRuntimeSyncState.Synchronized)
                {
                    throw new InvalidOperationException($"액티브 Runtime 동기화 검증 실패: {syncMessage}");
                }
                transaction.Commit();
                return active;
            }
            catch (Exception syncException)
            {
                try
                {
                    transaction.Rollback();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException(
                        "액티브 Runtime 동기화와 원상복구가 모두 실패했습니다.",
                        syncException,
                        rollbackException);
                }
                throw;
            }
            finally
            {
                transaction.Dispose();
            }
        }

        public static MonsterEffectActiveSkill SynchronizeActiveEffectRuntime(
            MonsterMakerDraft draft,
            MonsterCatalog catalog = null,
            MonsterRarityCatalog rarityCatalog = null)
        {
            draft?.EditorSyncActiveEffectAuthoring();
            var preflight = MonsterMakerValidator.ValidateActiveEffect(draft);
            if (preflight.HasErrors)
            {
                throw new InvalidOperationException(BuildIssueText(preflight.Issues));
            }
            if (!EditorUtility.IsPersistent(draft))
            {
                throw new InvalidOperationException("액티브만 반영하려면 먼저 Maker 제작 원본을 저장하세요.");
            }

            catalog ??= AssetDatabase.LoadAssetAtPath<MonsterCatalog>(MonsterCatalogPath);
            rarityCatalog ??= AssetDatabase.LoadAssetAtPath<MonsterRarityCatalog>(MonsterRarityCatalogPath);
            if (catalog == null || rarityCatalog == null)
            {
                throw new InvalidOperationException("MonsterCatalog 또는 MonsterRarityCatalog을 찾을 수 없습니다.");
            }
            var catalogPath = RequirePersistentAssetPath(catalog, "MonsterCatalog");
            var rarityCatalogPath = RequirePersistentAssetPath(rarityCatalog, "MonsterRarityCatalog");
            ValidateProductionDraftOwnership(draft, catalogPath, rarityCatalogPath);
            var paths = BuildPaths(draft.MonsterId);
            var definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(paths[0]);
            var motion = AssetDatabase.LoadAssetAtPath<MonsterMotionProfile>(paths[2]);
            var feedback = AssetDatabase.LoadAssetAtPath<MonsterFeedbackProfile>(paths[5]);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(paths[7]);
            if (definition == null || motion == null || feedback == null || controller == null)
            {
                throw new InvalidOperationException(
                    "액티브만 반영할 정식 Monster Runtime 기반 자산이 없습니다. 먼저 전체 정식 생성·수정을 한 번 실행하세요.");
            }
            if (!catalog.TryGet(draft.MonsterId, out var registeredDefinition) || registeredDefinition != definition)
            {
                throw new InvalidOperationException(
                    "선택한 MonsterCatalog의 몬스터와 현재 Runtime Definition이 일치하지 않습니다.");
            }

            var activePath = BuildActivePath(draft.MonsterId);
            var draftPath = RequirePersistentAssetPath(draft, "Monster Maker 제작 원본");
            var transaction = MonsterMakerWriteTransaction.Capture(
                new[] { activePath, paths[2], paths[5], paths[7], rarityCatalogPath, draftPath },
                Array.Empty<string>());
            try
            {
                var generatedSfx = new MonsterMakerGeneratedSfxWriter(feedback, paths[5], draft.MonsterId);
                ConfigureActiveMotionProjection(draft, motion);
                controller = ConfigureAnimatorController(paths[7], motion);
                var active = BuildOrUpdateMonsterActiveAsset(draft, generatedSfx) as MonsterEffectActiveSkill ??
                             throw new InvalidOperationException("효과형 액티브 Runtime 자산을 만들지 못했습니다.");
                UpdateRarityActiveReference(rarityCatalog, definition, active);
                MarkDirty(active, draft, motion, feedback, controller, rarityCatalog);
                SaveAssetsIfDirty(active, draft, motion, feedback, controller, rarityCatalog);
                generatedSfx.SaveIfDirty();
                AssetDatabase.ImportAsset(paths[7], ImportAssetOptions.ForceUpdate);

                if (active.SourceProfile != draft.ActiveEffectProfile ||
                    !string.Equals(active.SkillId, $"active_{draft.MonsterId}", StringComparison.OrdinalIgnoreCase) ||
                    active.EnergyCost != draft.ActiveEnergyMaximum)
                {
                    throw new InvalidOperationException("효과형 액티브 Runtime 동기화 검증 실패");
                }
                transaction.Commit();
                return active;
            }
            catch (Exception syncException)
            {
                try
                {
                    transaction.Rollback();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException(
                        "효과형 액티브 Runtime 동기화와 원상복구가 모두 실패했습니다.",
                        syncException,
                        rollbackException);
                }
                throw;
            }
            finally
            {
                transaction.Dispose();
            }
        }

        public static IReadOnlyList<string> BuildPaths(string monsterId)
        {
            var data = DataRoot + "/" + monsterId;
            var art = ArtRoot + "/" + monsterId;
            return new[]
            {
                data + "/MD_" + monsterId + ".asset",
                data + "/MB_" + monsterId + ".asset",
                data + "/MM_" + monsterId + ".asset",
                data + "/MC_" + monsterId + ".asset",
                data + "/MA_" + monsterId + ".asset",
                data + "/MF_" + monsterId + ".asset",
                data + "/MR_" + monsterId + ".asset",
                art + "/AC_" + monsterId + ".controller",
                art + "/PF_" + monsterId + "_VisualAdapter.prefab"
            };
        }

        public static string BuildDraftPath(string monsterId)
        {
            return $"{DraftRoot}/Draft_{monsterId}.asset";
        }

        public static string BuildPassivePath(string monsterId)
        {
            return $"{DataRoot}/{monsterId}/MP_{monsterId}_Passive.asset";
        }

        public static string BuildActivePath(string monsterId)
        {
            return $"{DataRoot}/{monsterId}/MSA_{monsterId}_Active.asset";
        }

        public static GenericMonsterPassiveSkill BuildOrUpdateMonsterPassiveAsset(MonsterMakerDraft draft)
        {
            if (draft == null || !(draft.RarityPassiveSkill is GenericMonsterPassiveSkill template))
            {
                return null;
            }
            var tuningError = "몬스터 전용 패시브 수치가 없습니다.";
            if (draft.PassiveTuning == null || !draft.PassiveTuning.TryValidate(template, out tuningError))
            {
                throw new InvalidOperationException(tuningError);
            }

            EnsureFolder("Assets/ProjectMT/02_Shared/Unit/Data", "Monsters");
            EnsureFolder(DataRoot, draft.MonsterId);
            var path = BuildPassivePath(draft.MonsterId);
            var passive = GetOrCreateAsset<GenericMonsterPassiveSkill>(path, $"MP_{draft.MonsterId}_Passive");
            passive.EditorConfigure(
                $"{template.SkillId}_{draft.MonsterId}",
                template.DisplayName,
                template.Description,
                template.PresentationTier,
                template.Recipe,
                template.Icon);
            var tuning = draft.PassiveTuning;
            passive.EditorConfigureRuntime(
                tuning.RuntimeKind,
                tuning.PrimaryBase,
                tuning.PrimaryPerLevelStep,
                tuning.SecondaryBase,
                tuning.SecondaryPerLevelStep,
                tuning.TriggerCount,
                tuning.MaxStacks,
                tuning.Duration,
                tuning.Cooldown,
                tuning.Threshold,
                tuning.Radius,
                tuning.MaxTargets);
            passive.EditorSetAuthoringEnabled(template.AuthoringEnabled);
            if (!passive.TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }
            EditorUtility.SetDirty(passive);
            AssetDatabase.SaveAssetIfDirty(passive);
            return passive;
        }

        private static MonsterActiveSkill BuildOrUpdateMonsterActiveAsset(
            MonsterMakerDraft draft,
            MonsterMakerGeneratedSfxWriter generatedSfx)
        {
            var path = BuildActivePath(draft.MonsterId);
            if (draft.ActiveEffectProfile != null)
            {
                var active = GetOrCreateActiveSkillAsset<MonsterEffectActiveSkill>(
                    path,
                    $"MSE_{draft.MonsterId}_Active");
                active.EditorConfigure(
                    $"active_{draft.MonsterId}",
                    draft.ActiveSkillName,
                    draft.ActiveEffectProfile.Description,
                    draft.Portrait,
                    draft.ActiveEffectProfile,
                    CompileActiveEffectPresentationBindings(draft, generatedSfx),
                    draft.ActiveEnergyMaximum,
                    ResolveActiveCommitNormalizedTime(draft),
                    draft.Rarity == MonsterRarity.Mythic);
                if (!active.TryValidate(out var effectError))
                {
                    throw new InvalidOperationException(effectError);
                }
                draft.EditorSetResolvedActiveSkill(active);
                EditorUtility.SetDirty(active);
                EditorUtility.SetDirty(draft);
                return active;
            }

            var attack = GetOrCreateActiveSkillAsset<MonsterAttackActiveSkill>(
                path,
                $"MSA_{draft.MonsterId}_Active");
            var compiledAttackBlocks = CompileActiveAttackBlocks(draft, attack);
            attack.EditorConfigure(
                $"active_{draft.MonsterId}",
                draft.ActiveSkillName,
                draft.ActiveAttackProfile.Description,
                draft.Portrait,
                draft.ActiveAttackProfile,
                compiledAttackBlocks,
                null,
                CompileActiveAttackPresentationBindings(draft, generatedSfx),
                draft.ActiveEnergyMaximum,
                ResolveActiveCommitNormalizedTime(draft),
                draft.Rarity == MonsterRarity.Mythic);
            if (!attack.TryValidate(out var attackError))
            {
                throw new InvalidOperationException(attackError);
            }
            draft.EditorSetResolvedActiveSkill(attack);
            EditorUtility.SetDirty(attack);
            EditorUtility.SetDirty(draft);
            return attack;
        }

        private static MonsterBasicAttackProfile[] CompileActiveAttackBlocks(
            MonsterMakerDraft draft,
            MonsterAttackActiveSkill owner)
        {
            if (draft?.ActiveAttackProfile == null || owner == null)
            {
                return Array.Empty<MonsterBasicAttackProfile>();
            }

            var path = AssetDatabase.GetAssetPath(owner);
            var generated = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<MonsterBasicAttackProfile>()
                .Where(candidate => candidate != null &&
                                    candidate.name.StartsWith("__ActiveAttackBlock_", StringComparison.Ordinal))
                .ToArray();
            var existing = generated
                .GroupBy(candidate => candidate.name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var retained = new HashSet<MonsterBasicAttackProfile>();
            var result = new List<MonsterBasicAttackProfile>(draft.ActiveAttackProfile.Steps.Count);
            for (var index = 0; index < draft.ActiveAttackProfile.Steps.Count; index++)
            {
                var step = draft.ActiveAttackProfile.Steps[index];
                if (step == null) continue;
                var name = "__ActiveAttackBlock_" + step.StepId;
                if (!existing.TryGetValue(name, out var block))
                {
                    block = ScriptableObject.CreateInstance<MonsterBasicAttackProfile>();
                    block.name = name;
                    block.hideFlags = HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(block, owner);
                }
                step.EditorCompileAttackBlock(block);
                var projectileCarrier = block.UsesProjectileVisual
                    ? draft.ProjectilePrefab != null
                        ? draft.ProjectilePrefab
                        : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultProjectilePrefabPath)
                    : null;
                block.EditorSetProjectileCarrierPrefab(projectileCarrier);
                block.EditorSetFeelFeedback(null, null, draft.ActiveAttackProfile.ImpactFeel);
                block.name = name;
                block.hideFlags = HideFlags.HideInHierarchy;
                EditorUtility.SetDirty(block);
                retained.Add(block);
                result.Add(block);
            }
            foreach (var stale in generated)
            {
                if (retained.Contains(stale)) continue;
                UnityEngine.Object.DestroyImmediate(stale, true);
            }
            return result.ToArray();
        }
        private static float ResolveActiveCommitNormalizedTime(MonsterMakerDraft draft)
        {
            var presentations = draft.CurrentActivePresentations;
            var firstPresentation = presentations.Count > 0 ? presentations[0] : null;
            draft.ResolveActiveStepMotion(
                firstPresentation,
                out _,
                out _,
                out _,
                out var commitNormalizedTime);
            return commitNormalizedTime;
        }

        private static void ConfigureActiveMotionProjection(
            MonsterMakerDraft draft,
            MonsterMotionProfile motion)
        {
            var activeSteps = draft.CurrentActivePresentations
                .Where(source => source != null)
                .Select(source =>
                {
                    draft.ResolveActiveStepMotion(
                        source,
                        out var clip,
                        out var speed,
                        out var fade,
                        out var commit);
                    var result = new MonsterActiveStepMotion();
                    result.EditorConfigure(source.StepId, clip, speed, fade, commit);
                    return result;
                })
                .ToArray();
            var first = activeSteps.FirstOrDefault();
            var active = new MonsterMotionSlot();
            active.EditorConfigure(
                first?.Clip,
                first?.PlaybackSpeed ?? 1f,
                first?.CrossFadeDuration ?? 0.08f,
                false);
            motion.EditorConfigure(
                motion.Idle,
                motion.Move,
                motion.Attacks,
                active,
                activeSteps,
                motion.Death);
        }

        private static void ValidateProductionDraftOwnership(
            MonsterMakerDraft draft,
            string catalogPath,
            string rarityCatalogPath)
        {
            var usesProductionCatalog =
                string.Equals(catalogPath, MonsterCatalogPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rarityCatalogPath, MonsterRarityCatalogPath, StringComparison.OrdinalIgnoreCase);
            if (!usesProductionCatalog)
            {
                return;
            }

            if (draft == null || !EditorUtility.IsPersistent(draft))
            {
                throw new InvalidOperationException("정식 Catalog 편입은 먼저 저장한 Maker 제작 원본에서만 실행할 수 있습니다.");
            }

            var actualPath = AssetDatabase.GetAssetPath(draft).Replace('\\', '/');
            var expectedPath = BuildDraftPath(draft.MonsterId);
            if (!string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"제작 원본 ID 소유권이 일치하지 않습니다. Expected={expectedPath}, Actual={actualPath}");
            }
        }

        private static T GetOrCreateActiveSkillAsset<T>(string path, string assetName)
            where T : MonsterActiveSkill
        {
            var existing = AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>().FirstOrDefault();
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<T>();
                existing.name = assetName;
                var main = AssetDatabase.LoadMainAssetAtPath(path);
                if (main == null)
                {
                    AssetDatabase.CreateAsset(existing, path);
                }
                else
                {
                    existing.hideFlags = HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(existing, main);
                }
            }

            existing.name = assetName;
            existing.hideFlags = HideFlags.None;
            if (AssetDatabase.LoadMainAssetAtPath(path) != existing)
            {
                AssetDatabase.SetMainObject(existing, path);
            }

            foreach (var stale in AssetDatabase.LoadAllAssetsAtPath(path)
                         .OfType<MonsterActiveSkill>()
                         .Where(candidate => candidate != null && candidate != existing)
                         .ToArray())
            {
                UnityEngine.Object.DestroyImmediate(stale, true);
            }

            if (existing is MonsterEffectActiveSkill)
            {
                foreach (var staleBlock in AssetDatabase.LoadAllAssetsAtPath(path)
                             .OfType<MonsterBasicAttackProfile>()
                             .Where(candidate => candidate != null &&
                                                 candidate.name.StartsWith(
                                                     "__ActiveAttackBlock_",
                                                     StringComparison.Ordinal))
                             .ToArray())
                {
                    UnityEngine.Object.DestroyImmediate(staleBlock, true);
                }
            }

            EditorUtility.SetDirty(existing);
            return existing;
        }
        private static T GetOrCreateAsset<T>(string path, string assetName) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static MonsterActionDefinition ConfigureCombatAction(
            MonsterCombatProfile combat,
            string combatPath,
            MonsterMakerDraft draft,
            MonsterMakerGeneratedSfxWriter generatedSfx)
        {
            var resolvedCombatType = draft.BasicAttackProfile != null
                ? draft.BasicAttackProfile.CombatType
                : draft.CombatType;
            var desiredType = resolvedCombatType switch
            {
                MonsterCombatType.Melee => typeof(MeleeActionDefinition),
                MonsterCombatType.Ranged => typeof(ProjectileActionDefinition),
                MonsterCombatType.Special => typeof(SpecialActionDefinition),
                _ => throw new ArgumentOutOfRangeException()
            };
            var existing = AssetDatabase.LoadAllAssetsAtPath(combatPath)
                .OfType<MonsterActionDefinition>()
                .FirstOrDefault();
            if (existing != null && existing.GetType() != desiredType)
            {
                UnityEngine.Object.DestroyImmediate(existing, true);
                existing = null;
            }

            if (existing == null)
            {
                existing = (MonsterActionDefinition)ScriptableObject.CreateInstance(desiredType);
                existing.name = resolvedCombatType + "_" + draft.MonsterId;
                AssetDatabase.AddObjectToAsset(existing, combat);
            }

            switch (existing)
            {
                case MeleeActionDefinition melee:
                    melee.EditorConfigure(
                        draft.MeleeMode,
                        draft.MeleeAreaRadius,
                        draft.MeleeMaxTargets,
                        draft.MeleeAreaCenter);
                    break;
                case ProjectileActionDefinition projectile:
                    var usesProjectileVisual = draft.BasicAttackProfile != null
                        ? draft.BasicAttackProfile.UsesProjectileVisual
                        : draft.RangedDeliveryMode == MonsterRangedDeliveryMode.Projectile;
                    var resolvedDelivery = usesProjectileVisual
                        ? MonsterRangedDeliveryMode.Projectile
                        : MonsterRangedDeliveryMode.Instant;
                    var resolvedMode = draft.BasicAttackProfile == null
                        ? draft.ProjectileMode
                        : draft.BasicAttackProfile.LegacyProjectileMode;
                    var resolvedPiercingTargets = draft.BasicAttackProfile == null
                        ? draft.ProjectileMaxPiercingTargets
                        : draft.BasicAttackProfile.MaxTargets;
                    var resolvedImpactRadius = draft.BasicAttackProfile == null
                        ? draft.ProjectileImpactRadius
                        : draft.BasicAttackProfile.Radius;
                    var resolvedImpactTargets = draft.BasicAttackProfile == null
                        ? draft.ProjectileMaxImpactTargets
                        : draft.BasicAttackProfile.MaxTargets;
                    var projectileVisual = usesProjectileVisual
                        ? draft.ProjectilePrefab
                        : null;
                    if (usesProjectileVisual && projectileVisual == null)
                    {
                        projectileVisual = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultProjectilePrefabPath);
                    }
                    projectile.EditorConfigure(
                        resolvedDelivery,
                        resolvedMode,
                        projectileVisual,
                        null,
                        draft.ResolvedProjectileSpeed,
                        draft.ResolvedProjectileLifetime,
                        draft.ResolvedProjectileHitRadius,
                        resolvedPiercingTargets,
                        resolvedImpactRadius,
                        resolvedImpactTargets,
                        draft.ProjectileLaunchRecoilDistance,
                        draft.ProjectileLaunchRecoilDuration,
                        draft.OverrideProjectileTuning);
                    break;
                case SpecialActionDefinition special:
                    special.EditorConfigure(
                        draft.SpecialEffectId,
                        draft.SpecialTargetTeam,
                        draft.SpecialRadius,
                        draft.SpecialMaxTargets,
                        draft.SpecialDuration,
                        draft.SpecialStackPolicy,
                        draft.SpecialModifier);
                    break;
            }

            existing.EditorSetBasicAttackProfile(draft.BasicAttackProfile);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static MonsterAbilityDefinition GetOrCreateAbility(
            MonsterAscensionProfile profile,
            string profilePath,
            string objectName)
        {
            var ability = AssetDatabase.LoadAllAssetsAtPath(profilePath)
                .OfType<MonsterAbilityDefinition>()
                .FirstOrDefault(candidate => string.Equals(candidate.name, objectName, StringComparison.Ordinal));
            if (ability != null)
            {
                return ability;
            }

            ability = ScriptableObject.CreateInstance<MonsterAbilityDefinition>();
            ability.name = objectName;
            AssetDatabase.AddObjectToAsset(ability, profile);
            return ability;
        }

        private static AnimatorController ConfigureAnimatorController(string path, MonsterMotionProfile motion)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            var stateMachine = controller.layers[0].stateMachine;
            var expected = new Dictionary<string, AnimationClip>(StringComparer.Ordinal)
            {
                [MonsterMotionProfile.IdleStateName] = motion.Idle.Clip,
                [MonsterMotionProfile.MoveStateName] = motion.Move.Clip,
                [MonsterMotionProfile.DeathStateName] = motion.Death.Clip
            };
            if (motion.Active?.Clip != null)
            {
                expected[MonsterMotionProfile.ActiveStateName] = motion.Active.Clip;
            }
            for (var index = 0; index < motion.ActiveSteps.Count; index++)
            {
                var activeStep = motion.ActiveSteps[index];
                if (activeStep?.Clip != null)
                {
                    expected[activeStep.StateName] = activeStep.Clip;
                }
            }
            for (var index = 0; index < motion.Attacks.Length; index++)
            {
                expected[motion.Attacks[index].StateName] = motion.Attacks[index].Clip;
            }

            var states = stateMachine.states;
            for (var index = states.Length - 1; index >= 0; index--)
            {
                if (!expected.ContainsKey(states[index].state.name))
                {
                    stateMachine.RemoveState(states[index].state);
                }
            }

            AnimatorState idleState = null;
            foreach (var pair in expected)
            {
                var state = stateMachine.states
                    .Select(child => child.state)
                    .FirstOrDefault(candidate => string.Equals(candidate.name, pair.Key, StringComparison.Ordinal));
                state ??= stateMachine.AddState(pair.Key);
                state.motion = pair.Value;
                state.speed = 1f;
                if (pair.Key == MonsterMotionProfile.IdleStateName)
                {
                    idleState = state;
                }
            }

            stateMachine.defaultState = idleState;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static GameObject ConfigureVisualAdapter(
            string path,
            MonsterMakerDraft draft,
            RuntimeAnimatorController controller)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject root;
            Scene previewScene = default;
            var loadedContents = existing != null;
            if (loadedContents)
            {
                root = PrefabUtility.LoadPrefabContents(path);
            }
            else
            {
                previewScene = EditorSceneManager.NewPreviewScene();
                root = new GameObject("PF_" + draft.MonsterId + "_VisualAdapter");
                SceneManager.MoveGameObjectToScene(root, previewScene);
            }

            try
            {
                root.name = "PF_" + draft.MonsterId + "_VisualAdapter";
                EnsureComponent<HealthComponent>(root);
                EnsureComponent<UnitVisualFeedback>(root);
                EnsureComponent<UnitActor>(root);
                var driver = EnsureComponent<MonsterAnimationDriver>(root);

                var visual = root.transform.Find("Visual");
                var source = visual == null ? null : PrefabUtility.GetCorrespondingObjectFromSource(visual.gameObject);
                var sourcePath = source == null ? string.Empty : AssetDatabase.GetAssetPath(source);
                var desiredPath = AssetDatabase.GetAssetPath(draft.VendorPrefab);
                if (visual == null || !string.Equals(sourcePath, desiredPath, StringComparison.Ordinal))
                {
                    if (visual != null)
                    {
                        UnityEngine.Object.DestroyImmediate(visual.gameObject);
                    }

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(draft.VendorPrefab, root.scene);
                    if (instance == null)
                    {
                        throw new InvalidOperationException("Vendor Prefab Instance 생성에 실패했습니다.");
                    }

                    visual = instance.transform;
                    visual.name = "Visual";
                    visual.SetParent(root.transform, false);
                }

                visual.localPosition = draft.VisualLocalPosition + Vector3.up * draft.GroundOffset;
                visual.localRotation = Quaternion.Euler(0f, draft.FacingYawOffset, 0f);
                visual.localScale = draft.VisualScale;

                var relativeAnimatorPath = MonsterMakerValidator.ResolveAnimatorPath(draft);
                var animatorTransform = string.IsNullOrWhiteSpace(relativeAnimatorPath)
                    ? visual
                    : visual.Find(relativeAnimatorPath);
                var animator = animatorTransform == null ? null : animatorTransform.GetComponent<Animator>();
                if (animator == null)
                {
                    throw new InvalidOperationException("수동 지정 Animator 경로를 Adapter에서 찾지 못했습니다.");
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                var attackOrigin = EnsureTransformPath(root.transform, draft.AttackOriginPath);
                attackOrigin.localPosition = draft.AttackOriginLocalPosition;
                var hitCenter = EnsureTransformPath(root.transform, draft.HitCenterPath);
                hitCenter.localPosition = draft.HitCenterLocalPosition;
                driver.EditorConfigure(animator, root.transform, attackOrigin, hitCenter);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (saved == null)
                {
                    throw new InvalidOperationException("Visual Adapter Prefab 저장에 실패했습니다.");
                }
            }
            finally
            {
                if (loadedContents)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    EditorSceneManager.ClosePreviewScene(previewScene);
                }
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static MonsterFeedbackCue CreateFeedbackCue(
            MonsterMakerFeedbackDraft draft,
            MonsterMakerGeneratedSfxWriter generatedSfx,
            string roleId)
        {
            if (draft?.HasAny != true)
            {
                return null;
            }

            var cue = new MonsterFeedbackCue();
            cue.EditorConfigure(
                generatedSfx.Resolve(draft, roleId),
                draft.VfxPrefab,
                draft.VfxLifetime,
                draft.LocalPosition,
                draft.LocalEulerAngles,
                draft.Scale);
            return cue;
        }

        private static MonsterFeedbackCue CreateActiveFeedbackCue(
            MonsterMakerActivePresentationSlotDraft slot,
            MonsterMakerGeneratedSfxWriter generatedSfx,
            string roleId)
        {
            if (slot == null) return null;
            var draft = slot.Feedback;
            var sfx = slot.SfxState == MonsterBasicAttackSfxAssignmentState.Assigned
                ? generatedSfx.Resolve(draft, roleId)
                : null;
            var vfx = slot.VfxState == MonsterBasicAttackVfxAssignmentState.Assigned
                ? draft.VfxPrefab
                : null;
            if (sfx == null && vfx == null) return null;

            var cue = new MonsterFeedbackCue();
            cue.EditorConfigure(
                sfx,
                vfx,
                draft.VfxLifetime,
                draft.LocalPosition,
                draft.LocalEulerAngles,
                draft.Scale);
            return cue;
        }

        private static IReadOnlyList<MonsterBasicAttackVfxBinding> CompileBasicAttackPresentationBindings(
            MonsterMakerDraft draft,
            MonsterMakerGeneratedSfxWriter generatedSfx)
        {
            var result = new List<MonsterBasicAttackVfxBinding>();
            foreach (var binding in MonsterBasicAttackBindingProjection.BuildActiveBindings(draft))
            {
                if (binding == null)
                {
                    continue;
                }

                var motion = string.IsNullOrWhiteSpace(binding.MotionId)
                    ? "Shared"
                    : binding.MotionId;
                var roleId = $"BasicAttack_{binding.AttackId}_{binding.SlotId}_{motion}";
                var runtimeSfx = binding.SfxState == MonsterBasicAttackSfxAssignmentState.Assigned
                    ? generatedSfx.Resolve(binding, roleId)
                    : null;
                result.Add(binding.EditorCloneForRuntime(
                    runtimeSfx));
            }
            return result;
        }

        private static MonsterEffectActivePresentationBinding[] CompileActiveEffectPresentationBindings(
            MonsterMakerDraft draft,
            MonsterMakerGeneratedSfxWriter generatedSfx)
        {
            var result = new List<MonsterEffectActivePresentationBinding>();
            foreach (var source in draft.ActiveEffectPresentations)
            {
                if (source == null || string.IsNullOrWhiteSpace(source.StepId)) continue;
                var group = draft.ActiveEffectProfile?.Groups.FirstOrDefault(candidate =>
                    candidate != null && string.Equals(
                        candidate.GroupId,
                        source.StepId,
                        StringComparison.OrdinalIgnoreCase));
                var slots = new List<MonsterActiveAttackPresentationCueBinding>();
                if (group != null)
                {
                    for (var index = 0; index < group.PresentationSlots.Count; index++)
                    {
                        var contract = group.PresentationSlots[index];
                        if (contract == null) continue;
                        var slotDraft = source.ResolveSlot(contract.SlotId);
                        var binding = new MonsterActiveAttackPresentationCueBinding();
                        binding.EditorConfigure(
                            contract.SlotId,
                            contract.Timing,
                            contract.Anchor,
                            CreateActiveFeedbackCue(
                                slotDraft,
                                generatedSfx,
                                $"EffectActive_{source.StepId}_{contract.SlotId}"),
                            contract.UseDuration,
                            contract.Duration,
                            contract.Multiplicity,
                            contract.Attachment,
                            contract.EndPolicy);
                        slots.Add(binding);
                    }
                }
                var presentation = new MonsterEffectActivePresentationBinding();
                presentation.EditorConfigure(source.StepId, slots.ToArray());
                result.Add(presentation);
            }
            return result.ToArray();
        }
        private static MonsterActiveAttackPresentationBinding[] CompileActiveAttackPresentationBindings(
            MonsterMakerDraft draft,
            MonsterMakerGeneratedSfxWriter generatedSfx)
        {
            var result = new List<MonsterActiveAttackPresentationBinding>();
            foreach (var source in draft.ActiveAttackPresentations)
            {
                if (source == null || string.IsNullOrWhiteSpace(source.StepId))
                {
                    continue;
                }

                var basicBindings = new List<MonsterBasicAttackVfxBinding>();
                foreach (var sourceBinding in source.AttackBlockBindings)
                {
                    if (sourceBinding == null) continue;
                    var motion = string.IsNullOrWhiteSpace(sourceBinding.MotionId)
                        ? "Shared"
                        : sourceBinding.MotionId;
                    var runtimeSfx = sourceBinding.SfxState ==
                                     MonsterBasicAttackSfxAssignmentState.Assigned
                        ? generatedSfx.Resolve(
                            sourceBinding,
                            $"ActiveBasic_{source.StepId}_{sourceBinding.SlotId}_{motion}")
                        : null;
                    basicBindings.Add(sourceBinding.EditorCloneForRuntime(runtimeSfx));
                }
                var binding = new MonsterActiveAttackPresentationBinding();
                binding.EditorConfigure(
                    source.StepId,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    Array.Empty<MonsterActiveAttackPresentationCueBinding>(),
                    basicBindings.ToArray());
                result.Add(binding);
            }
            return result.ToArray();
        }

        private sealed class MonsterMakerGeneratedSfxWriter // AudioClip 입력을 역할별 Cue 서브에셋으로 관리
        {
            private const string GeneratedPrefix = "__MonsterMakerSfx_";
            private static readonly Vector2 DefaultVolume = new Vector2(0.9f, 1f);
            private static readonly Vector2 DefaultPitch = new Vector2(0.98f, 1.02f);
            private const float DefaultSpatialBlend = 1f;
            private const float DefaultDuplicateCooldown = 0.05f;

            private readonly MonsterFeedbackProfile owner;
            private readonly string assetPath;
            private readonly string monsterId;
            private readonly Dictionary<string, SfxCue> existing;
            private readonly HashSet<string> usedNames = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<SfxCue> usedCues = new HashSet<SfxCue>();

            public MonsterMakerGeneratedSfxWriter(
                MonsterFeedbackProfile feedbackProfile,
                string feedbackPath,
                string sourceMonsterId)
            {
                owner = feedbackProfile;
                assetPath = feedbackPath;
                monsterId = sourceMonsterId;
                existing = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                    .OfType<SfxCue>()
                    .Where(candidate => candidate.name.StartsWith(GeneratedPrefix, StringComparison.Ordinal))
                    .GroupBy(candidate => candidate.name, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            }

            public SfxCue Resolve(MonsterMakerFeedbackDraft draft, string roleId)
            {
                if (draft == null || draft.Sound == null)
                {
                    RegisterLegacyGeneratedCue(draft?.Sfx);
                    return draft?.Sfx;
                }

                return Resolve(draft.Sound, roleId);
            }

            public SfxCue Resolve(AudioClip sound, string roleId)
            {
                return Resolve(sound, roleId, null);
            }

            public SfxCue Resolve(MonsterBasicAttackVfxBinding binding, string roleId)
            {
                if (binding == null)
                {
                    return null;
                }
                if (binding.Sound == null)
                {
                    RegisterLegacyGeneratedCue(binding.Sfx);
                    return binding.Sfx;
                }
                return Resolve(binding.Sound, roleId, binding.SoundVolume);
            }

            public SfxCue Resolve(AudioClip sound, string roleId, float? volume)
            {
                if (sound == null)
                {
                    return null;
                }

                var objectName = BuildObjectName(roleId);
                usedNames.Add(objectName);
                if (!existing.TryGetValue(objectName, out var cue) || cue == null)
                {
                    cue = ScriptableObject.CreateInstance<SfxCue>();
                    cue.name = objectName;
                    cue.hideFlags = HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(cue, owner);
                    existing[objectName] = cue;
                }

                cue.EditorConfigure(
                    new[] { sound },
                    volume.HasValue
                        ? Vector2.one * Mathf.Clamp01(volume.Value)
                        : DefaultVolume,
                    DefaultPitch,
                    DefaultSpatialBlend,
                    DefaultDuplicateCooldown,
                    SfxPriority.Normal);
                EditorUtility.SetDirty(cue);
                EditorUtility.SetDirty(owner);
                usedCues.Add(cue);
                return cue;
            }

            public void RemoveUnused()
            {
                var generated = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                    .OfType<SfxCue>()
                    .Where(candidate => candidate.name.StartsWith(GeneratedPrefix, StringComparison.Ordinal))
                    .ToArray();
                for (var index = 0; index < generated.Length; index++)
                {
                    if (!usedNames.Contains(generated[index].name))
                    {
                        UnityEngine.Object.DestroyImmediate(generated[index], true);
                        EditorUtility.SetDirty(owner);
                    }
                }
            }

            public void SaveIfDirty()
            {
                foreach (var cue in usedCues)
                {
                    if (cue != null)
                    {
                        AssetDatabase.SaveAssetIfDirty(cue);
                    }
                }
            }

            private void RegisterLegacyGeneratedCue(SfxCue cue)
            {
                if (cue == null ||
                    !string.Equals(AssetDatabase.GetAssetPath(cue), assetPath, StringComparison.OrdinalIgnoreCase) ||
                    !cue.name.StartsWith(GeneratedPrefix, StringComparison.Ordinal))
                {
                    return;
                }

                usedNames.Add(cue.name);
                usedCues.Add(cue);
            }

            private string BuildObjectName(string roleId)
            {
                var rawName = $"{GeneratedPrefix}{monsterId}_{roleId}";
                return new string(rawName
                    .Select(character => char.IsLetterOrDigit(character) || character == '_' || character == '-'
                        ? character
                        : '_')
                    .ToArray());
            }
        }

        private static void RegisterLast(
            MonsterCatalog catalog,
            MonsterRarityCatalog rarityCatalog,
            MonsterDefinition definition,
            MonsterMakerDraft draft,
            MonsterPassiveSkill resolvedPassive,
            MonsterActiveSkill resolvedActive)
        {
            var previousDefinitions = catalog.Definitions.ToList();
            var matching = previousDefinitions
                .Where(candidate => candidate != null &&
                                    string.Equals(candidate.MonsterId, draft.MonsterId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matching.Length > 1 || matching.Length == 1 && matching[0] != definition)
            {
                throw new InvalidOperationException("MonsterCatalog에 같은 ID의 다른 Definition이 있습니다.");
            }

            if (matching.Length == 0)
            {
                previousDefinitions.Add(definition);
                catalog.EditorSetDefinitions(previousDefinitions);
            }

            if (!catalog.TryValidate(out var catalogError))
            {
                throw new InvalidOperationException(catalogError);
            }

            RegisterRarity(rarityCatalog, definition, draft, resolvedPassive, resolvedActive);
            if (!rarityCatalog.TryValidate(out var rarityCatalogError))
            {
                throw new InvalidOperationException(rarityCatalogError);
            }

            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(rarityCatalog);
        }

        private static void RegisterRarity(
            MonsterRarityCatalog rarityCatalog,
            MonsterDefinition definition,
            MonsterMakerDraft draft,
            MonsterPassiveSkill resolvedPassive,
            MonsterActiveSkill resolvedActive)
        {
            var serialized = new SerializedObject(rarityCatalog);
            var common = serialized.FindProperty("commonToEpicEntries");
            var legendary = serialized.FindProperty("legendaryMythicEntries");
            RemoveMonsterEntries(common, definition.MonsterId);
            RemoveMonsterEntries(legendary, definition.MonsterId);

            var isLegendary = draft.Rarity == MonsterRarity.Legendary || draft.Rarity == MonsterRarity.Mythic;
            var target = isLegendary ? legendary : common;
            target.InsertArrayElementAtIndex(target.arraySize);
            var entry = target.GetArrayElementAtIndex(target.arraySize - 1);
            entry.FindPropertyRelative("monster").objectReferenceValue = definition;
            entry.FindPropertyRelative("rarity").enumValueIndex = (int)draft.Rarity;
            entry.FindPropertyRelative("passiveSkill").objectReferenceValue =
                draft.UsePassiveSkill ? resolvedPassive ?? draft.RarityPassiveSkill : null;
            var activeSkill = entry.FindPropertyRelative("activeSkill");
            if (activeSkill != null)
            {
                activeSkill.objectReferenceValue = draft.UseActiveSkill ? resolvedActive : null;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpdateRarityActiveReference(
            MonsterRarityCatalog rarityCatalog,
            MonsterDefinition definition,
            MonsterActiveSkill active)
        {
            var serialized = new SerializedObject(rarityCatalog);
            var groups = new[]
            {
                serialized.FindProperty("commonToEpicEntries"),
                serialized.FindProperty("legendaryMythicEntries")
            };
            var updated = false;
            for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                var entries = groups[groupIndex];
                for (var index = 0; entries != null && index < entries.arraySize; index++)
                {
                    var entry = entries.GetArrayElementAtIndex(index);
                    var monster = entry.FindPropertyRelative("monster").objectReferenceValue as MonsterDefinition;
                    if (monster == null || !string.Equals(
                            monster.MonsterId,
                            definition.MonsterId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    entry.FindPropertyRelative("activeSkill").objectReferenceValue = active;
                    updated = true;
                }
            }
            if (!updated)
            {
                throw new InvalidOperationException("MonsterRarityCatalog에서 액티브를 연결할 몬스터 항목을 찾지 못했습니다.");
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (!rarityCatalog.TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }
            EditorUtility.SetDirty(rarityCatalog);
        }

        private static void RemoveMonsterEntries(SerializedProperty entries, string monsterId)
        {
            for (var index = entries.arraySize - 1; index >= 0; index--)
            {
                var monster = entries.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("monster")
                    .objectReferenceValue as MonsterDefinition;
                if (monster != null && string.Equals(monster.MonsterId, monsterId, StringComparison.OrdinalIgnoreCase))
                {
                    entries.DeleteArrayElementAtIndex(index);
                }
            }
        }

        private static T EnsureComponent<T>(GameObject root) where T : Component
        {
            var component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        private static Transform EnsureTransformPath(Transform root, string path)
        {
            var current = root;
            var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < parts.Length; index++)
            {
                var child = current.Find(parts[index]);
                if (child == null)
                {
                    child = new GameObject(parts[index]).transform;
                    child.SetParent(current, false);
                }

                current = child;
            }

            return current;
        }

        private static string PrefixPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : "/" + path;
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static string RequirePersistentAssetPath(UnityEngine.Object asset, string label)
        {
            var path = asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrWhiteSpace(path) || !AssetDatabase.Contains(asset))
            {
                throw new InvalidOperationException(label + "은 저장된 Project Asset이어야 합니다.");
            }

            return MonsterMakerWriteTransaction.NormalizeAssetPath(path);
        }

        private sealed class MonsterMakerWriteTransaction : IDisposable
        {
            private readonly string backupRoot;
            private readonly IReadOnlyList<FileSnapshot> files;
            private readonly IReadOnlyList<FolderSnapshot> folders;
            private bool committed;
            private bool rolledBack;
            private bool disposed;

            private MonsterMakerWriteTransaction(
                string backupRoot,
                IReadOnlyList<FileSnapshot> files,
                IReadOnlyList<FolderSnapshot> folders)
            {
                this.backupRoot = backupRoot;
                this.files = files;
                this.folders = folders;
            }

            public static MonsterMakerWriteTransaction Capture(
                IEnumerable<string> filePaths,
                IEnumerable<string> folderPaths)
            {
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var backupRoot = Path.Combine(
                    projectRoot,
                    "Library",
                    "ProjectMT",
                    "MonsterMakerTransactions",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(backupRoot);

                try
                {
                    var files = new List<FileSnapshot>();
                    var normalizedFiles = filePaths
                        .Select(NormalizeAssetPath)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    for (var index = 0; index < normalizedFiles.Length; index++)
                    {
                        var assetPath = normalizedFiles[index];
                        var fullPath = ToFullPath(projectRoot, assetPath);
                        var metaPath = fullPath + ".meta";
                        var fileBackup = Path.Combine(backupRoot, index + ".asset");
                        var metaBackup = Path.Combine(backupRoot, index + ".meta");
                        var fileExists = File.Exists(fullPath);
                        var metaExists = File.Exists(metaPath);
                        if (fileExists)
                        {
                            File.Copy(fullPath, fileBackup, true);
                        }

                        if (metaExists)
                        {
                            File.Copy(metaPath, metaBackup, true);
                        }

                        files.Add(new FileSnapshot(
                            assetPath,
                            fullPath,
                            metaPath,
                            fileBackup,
                            metaBackup,
                            fileExists,
                            metaExists));
                    }

                    var folders = folderPaths
                        .Select(NormalizeAssetPath)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Select(path => new FolderSnapshot(
                            path,
                            ToFullPath(projectRoot, path),
                            AssetDatabase.IsValidFolder(path)))
                        .ToArray();
                    return new MonsterMakerWriteTransaction(backupRoot, files, folders);
                }
                catch
                {
                    DeleteDirectoryIfPresent(backupRoot);
                    throw;
                }
            }

            public static string NormalizeAssetPath(string path)
            {
                var normalized = (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
                if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) ||
                    normalized.Contains("/../") ||
                    normalized.EndsWith("/..", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Monster Maker 트랜잭션은 Assets 하위의 정확한 경로만 처리합니다: " + path);
                }

                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                ToFullPath(projectRoot, normalized);
                return normalized;
            }

            public void Commit()
            {
                if (rolledBack)
                {
                    throw new InvalidOperationException("이미 원상복구된 Monster Maker 트랜잭션은 완료할 수 없습니다.");
                }

                committed = true;
            }

            public void Rollback()
            {
                if (committed || rolledBack)
                {
                    return;
                }

                for (var index = 0; index < folders.Count; index++)
                {
                    var folder = folders[index];
                    if (folder.Existed)
                    {
                        continue;
                    }

                    DeleteDirectoryIfPresent(folder.FullPath);
                    DeleteFileIfPresent(folder.FullPath + ".meta");
                }

                for (var index = 0; index < files.Count; index++)
                {
                    RestoreFile(files[index]);
                }

                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                for (var index = 0; index < files.Count; index++)
                {
                    if (files[index].FileExisted)
                    {
                        AssetDatabase.ImportAsset(
                            files[index].AssetPath,
                            ImportAssetOptions.ForceSynchronousImport |
                            ImportAssetOptions.ForceUpdate);
                    }
                }

                rolledBack = true;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                DeleteDirectoryIfPresent(backupRoot);
            }

            private static void RestoreFile(FileSnapshot snapshot)
            {
                if (snapshot.FileExisted)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(snapshot.FullPath));
                    File.Copy(snapshot.FileBackupPath, snapshot.FullPath, true);
                }
                else
                {
                    DeleteFileIfPresent(snapshot.FullPath);
                }

                if (snapshot.MetaExisted)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(snapshot.MetaPath));
                    File.Copy(snapshot.MetaBackupPath, snapshot.MetaPath, true);
                }
                else
                {
                    DeleteFileIfPresent(snapshot.MetaPath);
                }
            }

            private static string ToFullPath(string projectRoot, string assetPath)
            {
                var fullPath = Path.GetFullPath(Path.Combine(
                    projectRoot,
                    assetPath.Replace('/', Path.DirectorySeparatorChar)));
                var assetsRoot = Path.GetFullPath(Application.dataPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Monster Maker 트랜잭션 경로가 Assets 밖을 가리킵니다: " + assetPath);
                }

                return fullPath;
            }

            private static void DeleteDirectoryIfPresent(string path)
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }

            private static void DeleteFileIfPresent(string path)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            private readonly struct FileSnapshot
            {
                public FileSnapshot(
                    string assetPath,
                    string fullPath,
                    string metaPath,
                    string fileBackupPath,
                    string metaBackupPath,
                    bool fileExisted,
                    bool metaExisted)
                {
                    AssetPath = assetPath;
                    FullPath = fullPath;
                    MetaPath = metaPath;
                    FileBackupPath = fileBackupPath;
                    MetaBackupPath = metaBackupPath;
                    FileExisted = fileExisted;
                    MetaExisted = metaExisted;
                }

                public string AssetPath { get; }
                public string FullPath { get; }
                public string MetaPath { get; }
                public string FileBackupPath { get; }
                public string MetaBackupPath { get; }
                public bool FileExisted { get; }
                public bool MetaExisted { get; }
            }

            private readonly struct FolderSnapshot
            {
                public FolderSnapshot(string assetPath, string fullPath, bool existed)
                {
                    AssetPath = assetPath;
                    FullPath = fullPath;
                    Existed = existed;
                }

                public string AssetPath { get; }
                public string FullPath { get; }
                public bool Existed { get; }
            }
        }

        private static void MarkDirty(params UnityEngine.Object[] objects)
        {
            for (var index = 0; index < objects.Length; index++)
            {
                if (objects[index] != null)
                {
                    EditorUtility.SetDirty(objects[index]);
                }
            }
        }

        private static void SaveAssetsIfDirty(params UnityEngine.Object[] objects)
        {
            for (var index = 0; index < objects.Length; index++)
            {
                if (objects[index] != null)
                {
                    AssetDatabase.SaveAssetIfDirty(objects[index]);
                }
            }
        }

        private static string BuildIssueText(IReadOnlyList<MonsterMakerIssue> issues)
        {
            return string.Join(
                Environment.NewLine,
                issues.Where(issue => issue.Severity == MonsterMakerIssueSeverity.Error)
                    .Select(issue => issue.Code + ": " + issue.Message));
        }

        private static string BuildRuntimeIssueText(IReadOnlyList<MonsterValidationIssue> issues)
        {
            return string.Join(
                Environment.NewLine,
                issues.Where(issue => issue.Severity == MonsterValidationSeverity.Error)
                    .Select(issue => issue.Code + ": " + issue.Message));
        }
    }
}
