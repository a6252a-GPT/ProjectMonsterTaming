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
                ["ru_01"] = "BA_R_01",
                ["shell_01"] = "BA_M_01",
                ["doomba_01"] = "BA_M_02",
                ["grimpy_01"] = "BA_M_01",
                ["hanjaemon_ice_01"] = "BA_R_03",
                ["kutan_01"] = "BA_M_01",
                ["nerea_01"] = "BA_M_02",
                ["rako_01"] = "BA_M_01",
                ["wispy_01"] = "BA_R_01",
                ["berkan_01"] = "BA_M_02",
                ["krabi_01"] = "BA_R_01",
                ["lumi_01"] = "BA_M_05",
                ["rubea_01"] = "BA_R_05",
                ["shakun_01"] = "BA_M_05",
                ["castley_01"] = "BA_M_04",
                ["mingyu_legend_01"] = "BA_M_02",
                ["mukuk_01"] = "BA_S_04",
                ["never_ice_01"] = "BA_R_02",
                ["silpia_01"] = "BA_S_01",
                ["floria_01"] = "BA_S_03",
                ["fryar_01"] = "BA_M_02",
                ["grisu_fire_01"] = "BA_S_02",
                ["kimhyeona_01"] = "BA_R_02",
                ["lucy_01"] = "BA_M_06",
                ["mingyu_mythic_01"] = "BA_M_03",
                ["oster_01"] = "BA_R_03",
                ["pc_bear_01"] = "BA_R_04"
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

        [MenuItem("Tools/ProjectMT/Monster Maker/기본공격 15종 생성 및 전체 매칭")]
        public static void SetupAll()
        {
            EnsureFolder("Assets/ProjectMT/02_Shared/Unit/Data", "BasicAttacks");
            var profiles = CreateOrUpdateProfiles();
            if (profiles.Count != 15)
            {
                throw new InvalidOperationException($"기본공격 Profile은 정확히 15개여야 합니다. Current={profiles.Count}");
            }

            var drafts = new List<MonsterMakerDraft>(MonsterProfileIds.Count);
            foreach (var assignment in MonsterProfileIds)
            {
                if (!profiles.TryGetValue(assignment.Value, out var profile))
                {
                    throw new InvalidOperationException(
                        $"기본공격 Profile을 찾지 못했습니다. Monster={assignment.Key}, Profile={assignment.Value}");
                }

                var draftPath = MonsterMakerAssetWriter.BuildDraftPath(assignment.Key);
                var draft = AssetDatabase.LoadAssetAtPath<MonsterMakerDraft>(draftPath);
                if (draft == null)
                {
                    throw new InvalidOperationException($"Monster Maker Draft를 찾지 못했습니다: {draftPath}");
                }

                Undo.RecordObject(draft, "기본공격 전체 매칭");
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
            Debug.Log($"[Monster Maker] 기본공격 15종 생성 및 {drafts.Count}마리 전체 매칭 완료");
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
                designMemo: "전방 부채꼴을 짧게 유지하며 총 피해를 세 번으로 나눠 전달.");
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
