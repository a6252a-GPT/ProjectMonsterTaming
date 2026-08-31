using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public static class MonsterBasicAttackPresetUtility // 15종 공용 Profile과 전체 Monster 매칭 원본
    {
        public const string ProfileRoot = "Assets/ProjectMT/02_Shared/Unit/Data/BasicAttacks";
        public const string CustomProfileRoot = ProfileRoot + "/Custom";
        private static readonly HashSet<string> BuiltInProfileIds = new HashSet<string>(
            new[]
            {
                "BA_M_01", "BA_M_02", "BA_M_03", "BA_M_04", "BA_M_05", "BA_M_06",
                "BA_R_01", "BA_R_02", "BA_R_03", "BA_R_04", "BA_R_05",
                "BA_S_01", "BA_S_02", "BA_S_03", "BA_S_04"
            },
            StringComparer.OrdinalIgnoreCase);
        private static Dictionary<int, int> draftUsageCounts;

        static MonsterBasicAttackPresetUtility()
        {
            EditorApplication.projectChanged += InvalidateUsageCache;
        }

        private static readonly IReadOnlyDictionary<string, string> MonsterProfileIds =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["aru_01"] = "BA_M_01",
                ["dubi_01"] = "BA_M_01",
                ["kir_01"] = "BA_M_01",
                ["piru_01"] = "BA_M_01",
                ["poi_poison_01"] = "BA_M_01",
                ["rage_01"] = "BA_M_01",
                ["rabi_queen_01"] = "BA_R_01",
                ["rabi_01"] = "BA_M_01",
                ["doomba_01"] = "BA_M_02",
                ["grimpy_01"] = "BA_M_01",
                ["hanjaemon_ice_01"] = "BA_R_03",
                ["kutan_01"] = "BA_M_01",
                ["chamchi_01"] = "BA_M_02",
                ["rako_01"] = "BA_M_01",
                ["wispy_01"] = "BA_R_01",
                ["berkan_01"] = "BA_M_02",
                ["krabi_01"] = "BA_R_01",
                ["lumi_01"] = "BA_M_05",
                ["phoenix_01"] = "BA_R_05",
                ["shakun_01"] = "BA_M_05",
                ["castley_01"] = "BA_M_04",
                ["werewolf_01"] = "BA_M_02",
                ["mukuk_01"] = "BA_S_04",
                ["never_ice_01"] = "BA_R_02",
                ["silpia_01"] = "BA_S_01",
                ["floria_01"] = "BA_S_03",
                ["fryar_01"] = "BA_M_02",
                ["angeonjun_01"] = "BA_S_02",
                ["kimhyeona_01"] = "BA_R_02",
                ["lucy_01"] = "BA_M_06",
                ["mingyu_mythic_01"] = "BA_M_03",
                ["oster_01"] = "BA_R_03",
                ["pc_bear_01"] = "BA_R_04",
                ["pipi_01"] = "BA_R_01",
                ["berry_01"] = "BA_R_01",
                ["pango_01"] = "BA_M_04",
                ["ruby_01"] = "BA_M_05",
                ["kain_01"] = "BA_M_06",
                ["argo_01"] = "BA_R_05",
                ["astell_01"] = "BA_S_01",
                ["candy_tree_01"] = "BA_R_03",
                ["ignis_01"] = "BA_M_02",
                ["pyron_01"] = "BA_R_03",
                ["nagaris_01"] = "BA_S_02"
            };

        public static IReadOnlyDictionary<string, string> Assignments => MonsterProfileIds;

        public static bool IsBuiltInProfile(MonsterBasicAttackProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            var id = profile.AttackId;
            return BuiltInProfileIds.Contains(id) &&
                   string.Equals(
                       AssetDatabase.GetAssetPath(profile),
                       $"{ProfileRoot}/{id}.asset",
                       StringComparison.OrdinalIgnoreCase);
        }

        public static int CountDraftUsages(MonsterBasicAttackProfile profile)
        {
            if (profile == null)
            {
                return 0;
            }

            if (draftUsageCounts == null)
            {
                draftUsageCounts = new Dictionary<int, int>();
                var drafts = AssetDatabase.FindAssets(
                        "t:MonsterMakerDraft",
                        new[] { MonsterMakerAssetWriter.DraftRoot })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<MonsterMakerDraft>);
                foreach (var draft in drafts)
                {
                    var recipe = draft?.BasicAttackProfile;
                    if (recipe == null)
                    {
                        continue;
                    }

                    var key = recipe.GetInstanceID();
                    draftUsageCounts.TryGetValue(key, out var count);
                    draftUsageCounts[key] = count + 1;
                }
            }

            return draftUsageCounts.TryGetValue(profile.GetInstanceID(), out var usageCount) ? usageCount : 0;
        }

        public static void InvalidateUsageCache()
        {
            draftUsageCounts = null;
        }

        public static bool TrySaveRecipe(MonsterBasicAttackProfile profile, out string error)
        {
            if (profile == null)
            {
                error = "저장할 기본공격 Recipe가 없습니다.";
                return false;
            }

            profile.EditorEnsureModularRecipe();
            if (!profile.TryValidate(out error))
            {
                return false;
            }

            var duplicate = AssetDatabase.FindAssets(
                    "t:MonsterBasicAttackProfile",
                    new[] { ProfileRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>)
                .FirstOrDefault(candidate => candidate != null && candidate != profile &&
                                             string.Equals(
                                                 candidate.AttackId,
                                                 profile.AttackId,
                                                 StringComparison.OrdinalIgnoreCase));
            if (duplicate != null)
            {
                error = $"Recipe ID가 중복됩니다: {profile.AttackId} / {AssetDatabase.GetAssetPath(duplicate)}";
                return false;
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            InvalidateUsageCache();
            error = null;
            return true;
        }

        [MenuItem("Tools/ProjectMT/Monster Maker/기본공격 초기 설정 · 기존 배정 유지")]
        public static void SetupAll()
        {
            SetupBuiltInProfiles();
            AssignRecommendationsToUnassigned();
        }

        [MenuItem("Tools/ProjectMT/Monster Maker/공식 기본공격 15종 생성·복구")]
        public static void SetupBuiltInProfiles()
        {
            EnsureFolder("Assets/ProjectMT/02_Shared/Unit/Data", "BasicAttacks");
            var profiles = CreateOrUpdateProfiles();
            if (profiles.Count != 15)
            {
                throw new InvalidOperationException($"기본공격 Profile은 정확히 15개여야 합니다. Current={profiles.Count}");
            }

            AssetDatabase.Refresh();
            Debug.Log("[Monster Maker] 공식 기본공격 15종 생성·복구 완료");
        }

        [MenuItem("Tools/ProjectMT/Monster Maker/기본공격 미지정 몬스터 추천 배정")]
        public static void AssignRecommendationsToUnassigned()
        {
            var validatedDrafts = ValidateSetupTargets();
            var profiles = BuiltInProfileIds
                .Select(LoadProfile)
                .Where(profile => profile != null)
                .ToDictionary(profile => profile.AttackId, StringComparer.OrdinalIgnoreCase);
            if (profiles.Count != BuiltInProfileIds.Count)
            {
                throw new InvalidOperationException(
                    "공식 기본공격 Profile 15종이 모두 필요합니다. 먼저 '공식 기본공격 15종 생성·복구'를 실행하세요.");
            }

            var drafts = new List<MonsterMakerDraft>(MonsterProfileIds.Count);
            foreach (var assignment in MonsterProfileIds)
            {
                if (!profiles.TryGetValue(assignment.Value, out var profile))
                {
                    throw new InvalidOperationException(
                        $"기본공격 Profile을 찾지 못했습니다. Monster={assignment.Key}, Profile={assignment.Value}");
                }

                var draft = validatedDrafts[assignment.Key];
                if (draft.BasicAttackProfile != null)
                {
                    continue;
                }

                Undo.RecordObject(draft, "미지정 기본공격 추천 배정");
                draft.EditorSetBasicAttackProfile(profile);
                draft.EditorPreserveLegacyProjectileTuning();
                EditorUtility.SetDirty(draft);
                AssetDatabase.SaveAssetIfDirty(draft);
                drafts.Add(draft);
            }
            try
            {
                for (var index = 0; index < drafts.Count; index++)
                {
                    EditorUtility.DisplayProgressBar(
                        "공용 기본공격 전체 매칭",
                        $"{drafts[index].MonsterId} Runtime 동기화",
                        index / (float)drafts.Count);
                    SyncRuntimeCombat(drafts[index]);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            Debug.Log($"[Monster Maker] 기본공격 미지정 몬스터 {drafts.Count}마리 추천 배정 완료 · 기존 배정 유지");
        }

        private static Dictionary<string, MonsterMakerDraft> ValidateSetupTargets()
        {
            var drafts = new Dictionary<string, MonsterMakerDraft>(StringComparer.OrdinalIgnoreCase);
            foreach (var assignment in MonsterProfileIds)
            {
                if (!BuiltInProfileIds.Contains(assignment.Value))
                {
                    throw new InvalidOperationException(
                        $"기본공격 매칭표에 등록되지 않은 Profile ID가 있습니다. Monster={assignment.Key}, Profile={assignment.Value}");
                }

                var draftPath = MonsterMakerAssetWriter.BuildDraftPath(assignment.Key);
                var draft = AssetDatabase.LoadAssetAtPath<MonsterMakerDraft>(draftPath);
                if (draft == null)
                {
                    throw new InvalidOperationException($"Monster Maker 제작 원본을 찾지 못했습니다: {draftPath}");
                }

                if (!string.Equals(draft.MonsterId, assignment.Key, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Monster Maker 제작 원본 ID가 경로와 다릅니다. Expected={assignment.Key}, Actual={draft.MonsterId}");
                }

                var combatPath = MonsterMakerAssetWriter.BuildPaths(assignment.Key)[3];
                if (AssetDatabase.LoadAssetAtPath<MonsterCombatProfile>(combatPath) == null)
                {
                    throw new InvalidOperationException($"Monster Combat Profile을 찾지 못했습니다: {combatPath}");
                }

                drafts.Add(assignment.Key, draft);
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(MonsterMakerAssetWriter.DefaultProjectilePrefabPath) == null)
            {
                throw new InvalidOperationException(
                    $"기본 투사체 Prefab을 찾지 못했습니다: {MonsterMakerAssetWriter.DefaultProjectilePrefabPath}");
            }

            return drafts;
        }

        private static void SyncRuntimeCombat(MonsterMakerDraft draft)
        {
            var profile = draft.BasicAttackProfile;
            if (profile == null)
            {
                throw new InvalidOperationException($"기본공격 Profile이 없습니다: {draft.MonsterId}");
            }

            var combatPath = MonsterMakerAssetWriter.BuildPaths(draft.MonsterId)[3];
            var combat = AssetDatabase.LoadAssetAtPath<MonsterCombatProfile>(combatPath);
            if (combat == null)
            {
                throw new InvalidOperationException($"Monster Combat Profile을 찾지 못했습니다: {combatPath}");
            }

            var desiredType = profile.CombatType == MonsterCombatType.Melee
                ? typeof(MeleeActionDefinition)
                : typeof(ProjectileActionDefinition);
            var action = AssetDatabase.LoadAllAssetsAtPath(combatPath)
                .OfType<MonsterActionDefinition>()
                .FirstOrDefault();
            if (action != null && action.GetType() != desiredType)
            {
                Undo.DestroyObjectImmediate(action);
                action = null;
            }

            if (action == null)
            {
                action = (MonsterActionDefinition)ScriptableObject.CreateInstance(desiredType);
                action.name = profile.CombatType + "_" + draft.MonsterId;
                AssetDatabase.AddObjectToAsset(action, combat);
            }

            if (action is MeleeActionDefinition melee)
            {
                var legacyMode = profile.Shape == MonsterBasicAttackShape.Single
                    ? MonsterMeleeAttackMode.Single
                    : MonsterMeleeAttackMode.Area;
                var legacyCenter = profile.Center == MonsterBasicAttackCenter.Source
                    ? MonsterMeleeAreaCenter.Source
                    : MonsterMeleeAreaCenter.PrimaryTarget;
                melee.EditorConfigure(legacyMode, profile.Radius, profile.MaxTargets, legacyCenter);
            }
            else if (action is ProjectileActionDefinition projectile)
            {
                var usesProjectile = profile.UsesProjectileVisual;
                var projectileVisual = usesProjectile ? draft.ProjectilePrefab : null;
                if (usesProjectile && projectileVisual == null)
                {
                    projectileVisual = AssetDatabase.LoadAssetAtPath<GameObject>(
                        MonsterMakerAssetWriter.DefaultProjectilePrefabPath);
                }

                var delivery = usesProjectile
                    ? MonsterRangedDeliveryMode.Projectile
                    : MonsterRangedDeliveryMode.Instant;
                projectile.EditorConfigure(
                    delivery,
                    profile.LegacyProjectileMode,
                    projectileVisual,
                    usesProjectile ? projectile.LaunchSfx : null,
                    draft.ResolvedProjectileSpeed,
                    draft.ResolvedProjectileLifetime,
                    draft.ResolvedProjectileHitRadius,
                    profile.MaxTargets,
                    profile.Radius,
                    profile.MaxTargets,
                    draft.ProjectileLaunchRecoilDistance,
                    draft.ProjectileLaunchRecoilDuration,
                    draft.OverrideProjectileTuning);
            }

            action.EditorSetBasicAttackProfile(profile);
            combat.EditorConfigure(profile.CombatType, action);
            EditorUtility.SetDirty(action);
            EditorUtility.SetDirty(combat);
            if (!action.TryValidate(out var actionError))
            {
                throw new InvalidOperationException(
                    $"기본공격 Runtime Action 검증 실패: {draft.MonsterId} / {actionError}");
            }
            if (!combat.TryValidate(out var combatError))
            {
                throw new InvalidOperationException(
                    $"기본공격 Combat Profile 검증 실패: {draft.MonsterId} / {combatError}");
            }

            AssetDatabase.SaveAssetIfDirty(draft);
            AssetDatabase.SaveAssetIfDirty(combat);
        }

        public static MonsterBasicAttackProfile LoadProfile(string attackId)
        {
            return string.IsNullOrWhiteSpace(attackId)
                ? null
                : AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>(
                    $"{ProfileRoot}/{attackId}.asset");
        }

        [MenuItem("Tools/ProjectMT/Monster Maker/기본공격 VFX 공간 계약만 초기화")]
        public static void SetupBuiltInVfxContractsOnly()
        {
            foreach (var attackId in BuiltInProfileIds)
            {
                var profile = LoadProfile(attackId);
                if (profile == null)
                {
                    throw new InvalidOperationException($"기본공격 Profile을 찾지 못했습니다: {attackId}");
                }
                Undo.RecordObject(profile, "기본공격 VFX 공간 계약 초기화");
                ApplyBuiltInVfxContract(profile);
                if (!profile.TryValidate(out var error))
                {
                    throw new InvalidOperationException(error);
                }
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
            }
            AssetDatabase.Refresh();
            Debug.Log("[Monster Maker] 공식 기본공격 15종 VFX 공간 계약 초기화 완료");
        }

        private static void ApplyBuiltInVfxContract(MonsterBasicAttackProfile profile)
        {
            profile.EditorSetVfxSlots(MonsterBasicAttackVfxContractTemplates.Build(profile));
        }

        private static MonsterBasicAttackVfxSlot[] BuildBuiltInVfxSlots(string attackId)
        {
            var motion = MonsterBasicAttackVfxAssignmentScope.MotionSpecific;
            var shared = MonsterBasicAttackVfxAssignmentScope.MonsterShared;
            return attackId switch
            {
                "BA_M_01" => new[]
                {
                    Vfx("swing_trail", "공격 궤적", "선택된 공격 모션의 휘두름 궤적", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion, MonsterBasicAttackVfxAttachment.FollowAnchor, MonsterBasicAttackVfxEndPolicy.MotionEnd),
                    Vfx("hit", "실제 명중", "피해가 적용된 대상 위치의 명중 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared)
                },
                "BA_M_02" => new[]
                {
                    Vfx("sweep_plane", "휩쓸기 면", "전방 부채꼴을 읽히게 하는 공격 면", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.SourceRoot, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion),
                    Vfx("target_hit", "대상별 명중", "실제로 피해를 받은 각 대상의 명중 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared)
                },
                "BA_M_03" => new[]
                {
                    Vfx("thrust_path", "찌르기 경로", "공격 원점에서 목표 방향으로 뻗는 직선 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.TrajectoryOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion),
                    Vfx("path_hit", "경로 명중", "직선 경로에서 피해를 받은 대상 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared)
                },
                "BA_M_04" => new[]
                {
                    Vfx("overhead_trail", "내려찍기 궤적", "선택된 모션의 내려찍기 궤적", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion, MonsterBasicAttackVfxAttachment.FollowAnchor, MonsterBasicAttackVfxEndPolicy.MotionEnd),
                    Vfx("ground_contact", "지면 접촉", "내려찍기가 닿은 중심점 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AreaCenter, MonsterBasicAttackVfxMultiplicity.OncePerExecution, shared),
                    Vfx("target_hit", "대상별 명중", "범위 안에서 실제 피해를 받은 각 대상의 명중 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared),
                    Vfx("area_wave", "범위 파동", "범위 판정이 해결된 뒤 펼쳐지는 원형 효과", MonsterBasicAttackVfxEvent.AreaResolved, MonsterBasicAttackVfxAnchor.AreaCenter, MonsterBasicAttackVfxMultiplicity.OncePerExecution, shared)
                },
                "BA_M_05" => new[]
                {
                    Vfx("dash_start", "돌진 시작", "공격 모션이 시작될 때의 예고 효과", MonsterBasicAttackVfxEvent.MotionStart, MonsterBasicAttackVfxAnchor.SourceRoot, MonsterBasicAttackVfxMultiplicity.OncePerMotion, motion),
                    Vfx("dash_trail", "돌진 잔상", "공격자를 따라가는 돌진 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.SourceRoot, MonsterBasicAttackVfxMultiplicity.ContinuousUntilEnd, motion, MonsterBasicAttackVfxAttachment.FollowAnchor, MonsterBasicAttackVfxEndPolicy.MotionEnd),
                    Vfx("hit", "실제 명중", "돌진 뒤 피해가 적용된 위치의 명중 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared)
                },
                "BA_M_06" => new[]
                {
                    Vfx("strike_trail", "연속 공격 궤적", "선택된 연속 공격 모션의 궤적", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion, MonsterBasicAttackVfxAttachment.FollowAnchor, MonsterBasicAttackVfxEndPolicy.MotionEnd),
                    Vfx("per_hit", "타격별 명중", "각 피해 단계의 명중 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerDamageStage, shared),
                    Vfx("final_hit", "마지막 타격", "마지막 피해 단계 뒤의 마무리 효과", MonsterBasicAttackVfxEvent.SequenceEnd, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.OncePerExecution, shared)
                },
                "BA_R_01" => ProjectileSlots("launch", "발사", "projectile", "투사체 본체", "hit", "실제 명중"),
                "BA_R_02" => new[]
                {
                    Vfx("launch", "발사", "관통 투사체가 생성되는 원점 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion),
                    Delivery("projectile", "관통 투사체 본체"),
                    Vfx("pierce_hit", "관통 명중", "경로 위에서 피해를 받은 대상별 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared),
                    Vfx("delivery_end", "비행 종료", "최대 거리 또는 수명으로 이동체가 끝나는 효과", MonsterBasicAttackVfxEvent.DeliveryEnd, MonsterBasicAttackVfxAnchor.ProjectileRoot, MonsterBasicAttackVfxMultiplicity.PerProjectile, shared)
                },
                "BA_R_03" => new[]
                {
                    Vfx("launch", "발사", "폭발 투사체가 생성되는 원점 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion),
                    Delivery("projectile", "폭발 투사체 본체"),
                    Vfx("contact", "접촉 명중", "투사체가 실제로 접촉한 위치 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared),
                    Vfx("area_explosion", "범위 폭발", "범위 피해 해결 뒤 중심점 폭발 효과", MonsterBasicAttackVfxEvent.AreaResolved, MonsterBasicAttackVfxAnchor.AreaCenter, MonsterBasicAttackVfxMultiplicity.OncePerExecution, shared)
                },
                "BA_R_04" => new[]
                {
                    Vfx("cast", "즉발 시전", "Marker 순간 공격 원점의 시전 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion),
                    Vfx("hit", "실제 명중", "즉시 피해가 적용된 대상 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared)
                },
                "BA_R_05" => new[]
                {
                    Vfx("multi_launch", "다중 발사", "부채꼴 탄막이 시작되는 원점 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion),
                    Delivery("projectile", "개별 투사체 본체"),
                    Vfx("hit", "개별 명중", "각 투사체가 피해를 적용한 대상 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared)
                },
                "BA_S_01" => new[]
                {
                    Vfx("launch", "왕복 발사", "왕복 투사체가 출발하는 원점 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion),
                    Delivery("projectile", "왕복 투사체 본체"),
                    Vfx("outbound_hit", "나가는 경로 명중", "전진 구간의 실제 명중 효과", MonsterBasicAttackVfxEvent.OutboundTargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared),
                    Vfx("turn", "회전 전환", "투사체가 복귀로 전환되는 지점 효과", MonsterBasicAttackVfxEvent.DeliveryTurn, MonsterBasicAttackVfxAnchor.ProjectileRoot, MonsterBasicAttackVfxMultiplicity.PerProjectile, shared),
                    Vfx("return_hit", "돌아오는 경로 명중", "복귀 구간의 실제 명중 효과", MonsterBasicAttackVfxEvent.ReturnTargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared)
                },
                "BA_S_02" => new[]
                {
                    Vfx("start", "브레스 시작", "브레스 모션 시작 예고 효과", MonsterBasicAttackVfxEvent.MotionStart, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerMotion, motion),
                    Vfx("body", "브레스 본체", "공격 원점을 따라 유지되는 브레스 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.ContinuousUntilEnd, motion, MonsterBasicAttackVfxAttachment.FollowAnchor, MonsterBasicAttackVfxEndPolicy.MotionEnd),
                    Vfx("repeated_hit", "반복 명중", "각 피해 단계에서 실제 명중한 위치 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerDamageStage, shared),
                    Vfx("end", "브레스 종료", "브레스 모션 종료 효과", MonsterBasicAttackVfxEvent.MotionEnd, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerMotion, motion)
                },
                "BA_S_03" => new[]
                {
                    Vfx("charge", "빔 충전", "빔 모션 시작의 충전 효과", MonsterBasicAttackVfxEvent.MotionStart, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerMotion, motion),
                    Vfx("beam_body", "빔 본체", "공격 원점에서 목표 방향으로 유지되는 빔 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.TrajectoryOrigin, MonsterBasicAttackVfxMultiplicity.ContinuousUntilEnd, motion, MonsterBasicAttackVfxAttachment.FollowAnchor, MonsterBasicAttackVfxEndPolicy.MotionEnd),
                    Vfx("contact_hit", "빔 접촉 명중", "직선 판정에서 피해를 받은 대상 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared),
                    Vfx("end", "빔 종료", "빔 모션 종료 효과", MonsterBasicAttackVfxEvent.MotionEnd, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerMotion, motion)
                },
                "BA_S_04" => new[]
                {
                    Vfx("start", "파동 시작", "이동 파동이 생성되는 원점 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion),
                    Delivery("wave_body", "이동 파동 본체"),
                    Vfx("path_hit", "경로 명중", "파동 경로에서 피해를 받은 대상 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared),
                    Vfx("disappear", "파동 소멸", "파동 이동체가 끝나는 위치 효과", MonsterBasicAttackVfxEvent.DeliveryEnd, MonsterBasicAttackVfxAnchor.ProjectileRoot, MonsterBasicAttackVfxMultiplicity.PerProjectile, shared)
                },
                _ => Array.Empty<MonsterBasicAttackVfxSlot>()
            };
        }

        private static MonsterBasicAttackVfxSlot[] ProjectileSlots(
            string launchId, string launchName, string deliveryId, string deliveryName,
            string hitId, string hitName)
        {
            return new[]
            {
                Vfx(launchId, launchName, "투사체가 생성되는 공격 원점 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, MonsterBasicAttackVfxAssignmentScope.MotionSpecific),
                Delivery(deliveryId, deliveryName),
                Vfx(hitId, hitName, "피해가 적용된 실제 명중 위치 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, MonsterBasicAttackVfxAssignmentScope.MonsterShared)
            };
        }

        private static MonsterBasicAttackVfxSlot Delivery(string id, string name)
        {
            return Vfx(id, name, "실제 이동 판정체의 몬스터 고유 외형",
                MonsterBasicAttackVfxEvent.DeliverySpawn, MonsterBasicAttackVfxAnchor.ProjectileRoot,
                MonsterBasicAttackVfxMultiplicity.PerProjectile,
                MonsterBasicAttackVfxAssignmentScope.MonsterShared,
                MonsterBasicAttackVfxAttachment.DeliveryVisual,
                MonsterBasicAttackVfxEndPolicy.DeliveryEnd);
        }

        private static MonsterBasicAttackVfxSlot Vfx(
            string id, string name, string guide, MonsterBasicAttackVfxEvent timing,
            MonsterBasicAttackVfxAnchor anchor, MonsterBasicAttackVfxMultiplicity repeat,
            MonsterBasicAttackVfxAssignmentScope scope,
            MonsterBasicAttackVfxAttachment attachment = MonsterBasicAttackVfxAttachment.World,
            MonsterBasicAttackVfxEndPolicy end = MonsterBasicAttackVfxEndPolicy.Timed,
            float lifetime = 1f)
        {
            var slot = new MonsterBasicAttackVfxSlot();
            slot.EditorConfigure(
                id,
                name,
                guide,
                timing,
                anchor,
                repeat,
                scope,
                attachment,
                end,
                lifetime);
            return slot;
        }

        private static Dictionary<string, MonsterBasicAttackProfile> CreateOrUpdateProfiles()
        {
            var profiles = new Dictionary<string, MonsterBasicAttackProfile>(StringComparer.OrdinalIgnoreCase);
            Configure(profiles, "BA_M_01", "단일 근접 타격",
                MonsterCombatType.Melee, MonsterBasicAttackDelivery.Contact, MonsterBasicAttackShape.Single,
                MonsterBasicAttackCenter.PrimaryTarget, MonsterBasicAttackProjectileTravel.None,
                1f, 0.35f, 60f, 0.4f, 1, 1, 0f, new[] { 1f }, 1f,
                designMemo: "주 대상 한 명을 즉시 타격하는 가장 단순한 근접 평타.");
            Configure(profiles, "BA_R_01", "단일 투사체",
                MonsterCombatType.Ranged, MonsterBasicAttackDelivery.Projectile, MonsterBasicAttackShape.Single,
                MonsterBasicAttackCenter.PrimaryTarget, MonsterBasicAttackProjectileTravel.Homing,
                1f, 0.25f, 30f, 0.3f, 1, 1, 0f, new[] { 1f }, 1f,
                designMemo: "주 대상을 따라가는 공용 유도 투사체 1발.");
            Configure(profiles, "BA_M_02", "전방 휩쓸기",
                MonsterCombatType.Melee, MonsterBasicAttackDelivery.Contact, MonsterBasicAttackShape.Fan,
                MonsterBasicAttackCenter.Source, MonsterBasicAttackProjectileTravel.None,
                1f, 0.35f, 100f, 1f, 3, 1, 0f, new[] { 1f }, 0.65f,
                designMemo: "전방 넓은 부채꼴을 한 번에 휩쓸어 최대 3명을 타격.");
            Configure(profiles, "BA_M_03", "직선 찌르기",
                MonsterCombatType.Melee, MonsterBasicAttackDelivery.Contact, MonsterBasicAttackShape.Line,
                MonsterBasicAttackCenter.Source, MonsterBasicAttackProjectileTravel.None,
                1.1f, 0.3f, 20f, 0.65f, 3, 1, 0f, new[] { 1f }, 0.75f,
                designMemo: "시전자 전방의 좁고 긴 직선을 찔러 일렬의 적을 타격.");
            Configure(profiles, "BA_M_04", "내려찍기 범위 타격",
                MonsterCombatType.Melee, MonsterBasicAttackDelivery.Contact, MonsterBasicAttackShape.Circle,
                MonsterBasicAttackCenter.PrimaryTarget, MonsterBasicAttackProjectileTravel.None,
                1f, 1.6f, 180f, 1f, 4, 1, 0f, new[] { 1f }, 0.65f,
                designMemo: "주 대상 지점을 내려찍어 주변 원형 범위까지 함께 타격.");
            Configure(profiles, "BA_R_02", "관통 투사체",
                MonsterCombatType.Ranged, MonsterBasicAttackDelivery.Projectile, MonsterBasicAttackShape.Line,
                MonsterBasicAttackCenter.Source, MonsterBasicAttackProjectileTravel.Straight,
                1.2f, 0.28f, 10f, 0.55f, 3, 1, 0f, new[] { 1f }, 0.8f,
                designMemo: "직선으로 날아가며 경로 위 최대 3명을 관통하는 투사체.");
            Configure(profiles, "BA_R_03", "폭발 투사체",
                MonsterCombatType.Ranged, MonsterBasicAttackDelivery.Projectile, MonsterBasicAttackShape.Circle,
                MonsterBasicAttackCenter.PrimaryTarget, MonsterBasicAttackProjectileTravel.Homing,
                1f, 1.55f, 180f, 1f, 4, 1, 0f, new[] { 1f }, 0.65f,
                designMemo: "주 대상을 유도한 뒤 충돌 지점에서 원형으로 폭발하는 투사체.");
            Configure(profiles, "BA_R_04", "즉발 원거리 타격",
                MonsterCombatType.Ranged, MonsterBasicAttackDelivery.Instant, MonsterBasicAttackShape.Single,
                MonsterBasicAttackCenter.PrimaryTarget, MonsterBasicAttackProjectileTravel.None,
                1f, 0.35f, 20f, 0.3f, 1, 1, 0f, new[] { 1f }, 1f,
                designMemo: "실제 투사체 이동 없이 Marker 시점에 원거리 주 대상을 즉시 타격.");
            Configure(profiles, "BA_M_05", "짧은 돌진 타격",
                MonsterCombatType.Melee, MonsterBasicAttackDelivery.Dash, MonsterBasicAttackShape.Single,
                MonsterBasicAttackCenter.PrimaryTarget, MonsterBasicAttackProjectileTravel.None,
                1f, 0.45f, 35f, 0.5f, 1, 1, 0f, new[] { 1f }, 1f,
                advanceDistance: 1.2f, advanceDuration: 0.11f,
                designMemo: "공격자가 실제 XZ로 짧게 접근한 뒤 주 대상을 단일 타격.");
            Configure(profiles, "BA_M_06", "단일 다단 타격",
                MonsterCombatType.Melee, MonsterBasicAttackDelivery.MultiHit, MonsterBasicAttackShape.Single,
                MonsterBasicAttackCenter.PrimaryTarget, MonsterBasicAttackProjectileTravel.None,
                1f, 0.4f, 30f, 0.4f, 1, 1, 0f, new[] { 0.3f, 0.3f, 0.4f }, 1f,
                hitInterval: 0.08f,
                designMemo: "하나의 평타 Marker에서 총 피해를 세 번으로 나눠 연속 전달.");
            Configure(profiles, "BA_R_05", "부채꼴 다중 투사체",
                MonsterCombatType.Ranged, MonsterBasicAttackDelivery.Projectile, MonsterBasicAttackShape.Fan,
                MonsterBasicAttackCenter.Source, MonsterBasicAttackProjectileTravel.Straight,
                1.1f, 0.24f, 35f, 0.35f, 3, 3, 28f, new[] { 1f }, 0.55f,
                stopAfterFirstTarget: true,
                designMemo: "직선 3발을 부채꼴로 동시에 발사해 서로 다른 전방 대상을 노림.");
            Configure(profiles, "BA_S_01", "왕복 투사체",
                MonsterCombatType.Ranged, MonsterBasicAttackDelivery.ReturningProjectile, MonsterBasicAttackShape.Line,
                MonsterBasicAttackCenter.Source, MonsterBasicAttackProjectileTravel.Returning,
                1.1f, 0.32f, 15f, 0.65f, 3, 1, 0f, new[] { 0.6f, 0.4f }, 0.7f,
                designMemo: "전진 60%와 복귀 40%로 같은 경로를 두 번 훑는 왕복 투사체.");
            Configure(profiles, "BA_S_02", "원뿔 브레스",
                MonsterCombatType.Ranged, MonsterBasicAttackDelivery.Breath, MonsterBasicAttackShape.Fan,
                MonsterBasicAttackCenter.Source, MonsterBasicAttackProjectileTravel.None,
                1f, 0.4f, 55f, 1f, 4, 1, 0f, new[] { 0.34f, 0.33f, 0.33f }, 0.6f,
                hitInterval: 0.07f,
                designMemo: "전방 부채꼴 브레스를 기본 0.8초 유지하며 총 피해를 세 단계로 나눠 전달.");
            profiles["BA_S_02"]?.EditorSetBreathDuration(0.8f);
            Configure(profiles, "BA_S_03", "직선 빔",
                MonsterCombatType.Ranged, MonsterBasicAttackDelivery.Beam, MonsterBasicAttackShape.Line,
                MonsterBasicAttackCenter.Source, MonsterBasicAttackProjectileTravel.None,
                1.1f, 0.28f, 10f, 0.5f, 4, 1, 0f, new[] { 1f }, 0.75f,
                designMemo: "Marker 시점에 전방 직선 전체를 즉시 관통 판정하는 빔.");
            Configure(profiles, "BA_S_04", "이동 파동",
                MonsterCombatType.Ranged, MonsterBasicAttackDelivery.TravelingWave, MonsterBasicAttackShape.Line,
                MonsterBasicAttackCenter.Source, MonsterBasicAttackProjectileTravel.Straight,
                1.3f, 0.6f, 20f, 1.2f, 4, 1, 0f, new[] { 1f }, 0.7f,
                projectileMoveSpeed: 8f,
                designMemo: "폭을 가진 판정 지대가 전방으로 이동하며 경로의 적을 통과 타격.");
            return profiles;
        }

        private static void Configure(
            IDictionary<string, MonsterBasicAttackProfile> destination,
            string attackId,
            string displayName,
            MonsterCombatType combatType,
            MonsterBasicAttackDelivery delivery,
            MonsterBasicAttackShape shape,
            MonsterBasicAttackCenter center,
            MonsterBasicAttackProjectileTravel travel,
            float rangeMultiplier,
            float radius,
            float angle,
            float lineWidth,
            int maxTargets,
            int projectileCount,
            float projectileSpread,
            float[] damageRatios,
            float secondaryDamageRatio,
            float hitInterval = 0.08f,
            float advanceDistance = 0f,
            float advanceDuration = 0.1f,
            bool stopAfterFirstTarget = false,
            float projectileMoveSpeed = 9f,
            float projectileLife = 3f,
            float projectileContactRadius = 0.25f,
            string designMemo = null)
        {
            var path = $"{ProfileRoot}/{attackId}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MonsterBasicAttackProfile>();
                profile.name = attackId;
                AssetDatabase.CreateAsset(profile, path);
            }

            profile.name = attackId;
            profile.EditorConfigure(
                attackId,
                displayName,
                combatType,
                delivery,
                shape,
                center,
                travel,
                rangeMultiplier,
                radius,
                angle,
                lineWidth,
                maxTargets,
                projectileCount,
                projectileSpread,
                damageRatios,
                secondaryDamageRatio,
                hitInterval,
                advanceDistance,
                advanceDuration,
                stopAfterFirstTarget,
                projectileMoveSpeed: projectileMoveSpeed,
                projectileLife: projectileLife,
                projectileContactRadius: projectileContactRadius);
            profile.EditorSetDesignMemo(designMemo);
            ApplyBuiltInVfxContract(profile);
            if (!profile.TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            destination.Add(attackId, profile);
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
