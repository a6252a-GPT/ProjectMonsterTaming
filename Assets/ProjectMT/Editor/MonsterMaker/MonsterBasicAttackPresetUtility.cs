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

        private static readonly IReadOnlyDictionary<string, string> MonsterProfileIds =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["aru_01"] = "BA01",
                ["dubi_01"] = "BA01",
                ["kir_01"] = "BA01",
                ["piru_01"] = "BA01",
                ["poi_poison_01"] = "BA01",
                ["rage_01"] = "BA01",
                ["ru_01"] = "BA02",
                ["shell_01"] = "BA01",
                ["doomba_01"] = "BA03",
                ["grimpy_01"] = "BA01",
                ["hanjaemon_ice_01"] = "BA07",
                ["kutan_01"] = "BA01",
                ["nerea_01"] = "BA03",
                ["rako_01"] = "BA01",
                ["wispy_01"] = "BA02",
                ["berkan_01"] = "BA03",
                ["krabi_01"] = "BA02",
                ["lumi_01"] = "BA09",
                ["rubea_01"] = "BA11",
                ["shakun_01"] = "BA09",
                ["castley_01"] = "BA05",
                ["mingyu_legend_01"] = "BA03",
                ["mukuk_01"] = "BA15",
                ["never_ice_01"] = "BA06",
                ["silpia_01"] = "BA12",
                ["floria_01"] = "BA14",
                ["fryar_01"] = "BA03",
                ["grisu_fire_01"] = "BA13",
                ["kimhyeona_01"] = "BA06",
                ["lucy_01"] = "BA10",
                ["mingyu_mythic_01"] = "BA04",
                ["oster_01"] = "BA07",
                ["pc_bear_01"] = "BA08"
            };

        public static IReadOnlyDictionary<string, string> Assignments => MonsterProfileIds;

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
                EditorUtility.SetDirty(draft);
                drafts.Add(draft);
            }

            var unmappedDrafts = AssetDatabase.FindAssets("t:MonsterMakerDraft", new[] { MonsterMakerAssetWriter.DraftRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterMakerDraft>)
                .Where(candidate => candidate != null && !MonsterProfileIds.ContainsKey(candidate.MonsterId))
                .Select(candidate => candidate.MonsterId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            if (unmappedDrafts.Length > 0)
            {
                throw new InvalidOperationException(
                    "기본공격 미매칭 Draft가 있습니다: " + string.Join(", ", unmappedDrafts));
            }

            AssetDatabase.SaveAssets();
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

            AssetDatabase.SaveAssets();
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
                var mode = profile.Shape == MonsterBasicAttackShape.Circle
                    ? MonsterProjectileAttackMode.Area
                    : profile.ProjectileTravel == MonsterBasicAttackProjectileTravel.Straight
                        ? MonsterProjectileAttackMode.Piercing
                        : MonsterProjectileAttackMode.Single;
                var resolvedSpeed = draft.ProjectileSpeed > 0f
                    ? draft.ProjectileSpeed
                    : Mathf.Max(0.01f, projectile.Speed);
                var resolvedLifetime = draft.ProjectileLifetime > 0f
                    ? draft.ProjectileLifetime
                    : Mathf.Max(0.01f, projectile.Lifetime);
                projectile.EditorConfigure(
                    delivery,
                    mode,
                    projectileVisual,
                    usesProjectile ? projectile.LaunchSfx : null,
                    resolvedSpeed,
                    resolvedLifetime,
                    Mathf.Max(draft.ProjectileHitRadius, profile.Radius),
                    profile.MaxTargets,
                    profile.Radius,
                    profile.MaxTargets,
                    draft.ProjectileLaunchRecoilDistance,
                    draft.ProjectileLaunchRecoilDuration);
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
            Configure(profiles, "BA01", "단일 근접 타격",
                MonsterCombatType.Melee, MonsterBasicAttackDelivery.Contact, MonsterBasicAttackShape.Single,
                MonsterBasicAttackCenter.PrimaryTarget, MonsterBasicAttackProjectileTravel.None,
                1f, 0.35f, 60f, 0.4f, 1, 1, 0f, new[] { 1f }, 1f);
            Configure(profiles, "BA02", "단일 투사체",
                MonsterCombatType.Ranged, MonsterBasicAttackDelivery.Projectile, MonsterBasicAttackShape.Single,
                MonsterBasicAttackCenter.PrimaryTarget, MonsterBasicAttackProjectileTravel.Homing,
                1f, 0.25f, 30f, 0.3f, 1, 1, 0f, new[] { 1f }, 1f);
            Configure(profiles, "BA03", "전방 휩쓸기",
                MonsterCombatType.Melee, MonsterBasicAttackDelivery.Contact, MonsterBasicAttackShape.Fan,
                MonsterBasicAttackCenter.Source, MonsterBasicAttackProjectileTravel.None,
                1f, 0.35f, 100f, 1f, 3, 1, 0f, new[] { 1f }, 0.65f);
            Configure(profiles, "BA04", "직선 찌르기",
                MonsterCombatType.Melee, MonsterBasicAttackDelivery.Contact, MonsterBasicAttackShape.Line,
                MonsterBasicAttackCenter.Source, MonsterBasicAttackProjectileTravel.None,
                1.1f, 0.3f, 20f, 0.65f, 3, 1, 0f, new[] { 1f }, 0.75f);
            Configure(profiles, "BA05", "내려찍기 범위 타격",
                MonsterCombatType.Melee, MonsterBasicAttackDelivery.Contact, MonsterBasicAttackShape.Circle,
                MonsterBasicAttackCenter.PrimaryTarget, MonsterBasicAttackProjectileTravel.None,
                1f, 1.6f, 180f, 1f, 4, 1, 0f, new[] { 1f }, 0.65f);
            Configure(profiles, "BA06", "관통 투사체",
                MonsterCombatType.Ranged, MonsterBasicAttackDelivery.Projectile, MonsterBasicAttackShape.Line,
                MonsterBasicAttackCenter.Source, MonsterBasicAttackProjectileTravel.Straight,
                1.2f, 0.28f, 10f, 0.55f, 3, 1, 0f, new[] { 1f }, 0.8f);
            Configure(profiles, "BA07", "폭발 투사체",
                MonsterCombatType.Ranged, MonsterBasicAttackDelivery.Projectile, MonsterBasicAttackShape.Circle,
                MonsterBasicAttackCenter.PrimaryTarget, MonsterBasicAttackProjectileTravel.Homing,
                1f, 1.55f, 180f, 1f, 4, 1, 0f, new[] { 1f }, 0.65f);
            Configure(profiles, "BA08", "즉발 원거리 타격",
                MonsterCombatType.Ranged, MonsterBasicAttackDelivery.Instant, MonsterBasicAttackShape.Single,
                MonsterBasicAttackCenter.PrimaryTarget, MonsterBasicAttackProjectileTravel.None,
                1f, 0.35f, 20f, 0.3f, 1, 1, 0f, new[] { 1f }, 1f);
            Configure(profiles, "BA09", "짧은 돌진 타격",
                MonsterCombatType.Melee, MonsterBasicAttackDelivery.Dash, MonsterBasicAttackShape.Single,
                MonsterBasicAttackCenter.PrimaryTarget, MonsterBasicAttackProjectileTravel.None,
                1f, 0.45f, 35f, 0.5f, 1, 1, 0f, new[] { 1f }, 1f,
                advanceDistance: 1.2f, advanceDuration: 0.11f);
            Configure(profiles, "BA10", "단일 다단 타격",
                MonsterCombatType.Melee, MonsterBasicAttackDelivery.MultiHit, MonsterBasicAttackShape.Single,
                MonsterBasicAttackCenter.PrimaryTarget, MonsterBasicAttackProjectileTravel.None,
                1f, 0.4f, 30f, 0.4f, 1, 1, 0f, new[] { 0.3f, 0.3f, 0.4f }, 1f,
                hitInterval: 0.08f);
            Configure(profiles, "BA11", "부채꼴 다중 투사체",
                MonsterCombatType.Ranged, MonsterBasicAttackDelivery.Projectile, MonsterBasicAttackShape.Fan,
                MonsterBasicAttackCenter.Source, MonsterBasicAttackProjectileTravel.Straight,
                1.1f, 0.24f, 35f, 0.35f, 3, 3, 28f, new[] { 1f }, 0.55f,
                stopAfterFirstTarget: true);
            Configure(profiles, "BA12", "왕복 투사체",
                MonsterCombatType.Ranged, MonsterBasicAttackDelivery.ReturningProjectile, MonsterBasicAttackShape.Line,
                MonsterBasicAttackCenter.Source, MonsterBasicAttackProjectileTravel.Returning,
                1.1f, 0.32f, 15f, 0.65f, 3, 1, 0f, new[] { 0.6f, 0.4f }, 0.7f);
            Configure(profiles, "BA13", "원뿔 브레스",
                MonsterCombatType.Ranged, MonsterBasicAttackDelivery.Breath, MonsterBasicAttackShape.Fan,
                MonsterBasicAttackCenter.Source, MonsterBasicAttackProjectileTravel.None,
                1f, 0.4f, 55f, 1f, 4, 1, 0f, new[] { 0.34f, 0.33f, 0.33f }, 0.6f,
                hitInterval: 0.07f);
            Configure(profiles, "BA14", "직선 빔",
                MonsterCombatType.Ranged, MonsterBasicAttackDelivery.Beam, MonsterBasicAttackShape.Line,
                MonsterBasicAttackCenter.Source, MonsterBasicAttackProjectileTravel.None,
                1.1f, 0.28f, 10f, 0.5f, 4, 1, 0f, new[] { 1f }, 0.75f);
            Configure(profiles, "BA15", "이동 파동",
                MonsterCombatType.Ranged, MonsterBasicAttackDelivery.TravelingWave, MonsterBasicAttackShape.Line,
                MonsterBasicAttackCenter.Source, MonsterBasicAttackProjectileTravel.Straight,
                1.3f, 0.6f, 20f, 1.2f, 4, 1, 0f, new[] { 1f }, 0.7f);
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
            bool stopAfterFirstTarget = false)
        {
            var path = $"{ProfileRoot}/{attackId}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MonsterBasicAttackProfile>();
                profile.name = attackId;
                AssetDatabase.CreateAsset(profile, path);
            }

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
                stopAfterFirstTarget);
            if (!profile.TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            EditorUtility.SetDirty(profile);
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
