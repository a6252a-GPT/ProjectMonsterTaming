using System;
using System.Collections.Generic;
using ProjectMT.Shared.Audio;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public static class HexCastleTurretAttackAssetUtility // 사각 원본 수치·연출 참조를 Hex 독립 데이터로 복제한다
    {
        public const string CatalogPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data/Turrets/CRHex_TurretAttackCatalog.asset";

        private const string SourceRoot =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/Data/Turrets/";
        private const string OutputFolder =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data/Turrets";

        [MenuItem("JC Tool/Castle Raid Hex/Turrets/Rebuild Independent Attack Catalog")]
        public static void RebuildCatalog()
        {
            EnsureOutputFolder();
            var profiles = new List<HexCastleTurretAttackProfile>(7);
            foreach (var weaponKind in new[]
                     {
                         HexCastleTurretWeaponKind.Cannon,
                         HexCastleTurretWeaponKind.Ballista,
                         HexCastleTurretWeaponKind.Fireball
                     })
            {
                for (var level = 1;
                     level <= HexCastleTurretAttackCatalog.ResolveSupportedMaximumLevel(weaponKind);
                     level++)
                {
                    profiles.Add(RebuildProfile(weaponKind, level));
                }
            }

            var catalog = AssetDatabase.LoadAssetAtPath<HexCastleTurretAttackCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<HexCastleTurretAttackCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.EditorConfigure(profiles.ToArray());
            if (!catalog.HasCompletePresentation)
            {
                throw new InvalidOperationException(
                    "Hex 포탑 3종의 Projectile·VFX·SFX 연출 계약이 완성되지 않았습니다.");
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceUpdate);
            Debug.Log(
                "[Hex Turret] 사각 포탑의 공격 수치·Projectile·Muzzle/Impact VFX·SFX 참조를 " +
                "Hex 독립 Catalog를 대포 2·발리스타 2·화염구 3레벨로 갱신했습니다. " +
                "발리스타는 비관통 단일 발사입니다.");
        }

        public static HexCastleTurretAttackCatalog LoadOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HexCastleTurretAttackCatalog>(CatalogPath);
            if (catalog == null || !catalog.IsComplete || !catalog.HasCompletePresentation)
            {
                RebuildCatalog();
                catalog = AssetDatabase.LoadAssetAtPath<HexCastleTurretAttackCatalog>(CatalogPath);
            }

            return catalog != null && catalog.IsComplete && catalog.HasCompletePresentation
                ? catalog
                : throw new InvalidOperationException("Hex 포탑 공격·연출 Catalog를 완성하지 못했습니다.");
        }

        private static HexCastleTurretAttackProfile RebuildProfile(
            HexCastleTurretWeaponKind weaponKind,
            int level)
        {
            var family = weaponKind.ToString();
            var sourcePath = $"{SourceRoot}CR_TurretAttack_{family}_Lv{level}.asset";
            var source = AssetDatabase.LoadMainAssetAtPath(sourcePath);
            if (source == null)
            {
                throw new InvalidOperationException($"기존 포탑 공격 Profile이 없습니다: {sourcePath}");
            }

            var serialized = new SerializedObject(source);
            var data = serialized.FindProperty("data");
            if (data == null)
            {
                throw new InvalidOperationException($"기존 포탑 공격 Profile의 data를 읽지 못했습니다: {sourcePath}");
            }

            var outputPath = $"{OutputFolder}/CRHex_TurretAttack_{family}_Lv{level}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<HexCastleTurretAttackProfile>(outputPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<HexCastleTurretAttackProfile>();
                AssetDatabase.CreateAsset(profile, outputPath);
            }

            var profileData = new HexCastleTurretAttackProfileData
            {
                weaponKind = weaponKind,
                level = ReadInt(data, "level"),
                impactType = (HexCastleTurretImpactType)ReadInt(data, "impactType"),
                targetPriority = (HexCastleTurretTargetPriority)ReadInt(data, "targetPriority"),
                sourceSearchRange = ReadFloat(data, "searchRange"),
                baseDamage = ReadFloat(data, "baseDamage"),
                cooldown = ReadFloat(data, "cooldown"),
                projectileCount = ReadInt(data, "projectileCount"),
                fireSequentially = ReadBool(data, "fireSequentially"),
                projectileVolleySize = ReadInt(data, "projectileVolleySize"),
                projectileFireDelay = ReadFloat(data, "projectileFireDelay"),
                spreadAngle = ReadFloat(data, "spreadAngle"),
                projectileSpeed = ReadFloat(data, "projectileSpeed"),
                projectileHitRadius = ReadFloat(data, "projectileHitRadius"),
                projectileLifetime = ReadFloat(data, "projectileLifetime"),
                pierceCount = ReadInt(data, "pierceCount"),
                piercingDamageRatio = ReadFloat(data, "piercingDamageRatio"),
                explosionRadius = ReadFloat(data, "explosionRadius"),
                projectileScale = ReadFloat(data, "projectileScale"),
                targetAimHeight = ReadFloat(data, "targetAimHeight"),
                headTurnSpeed = ReadFloat(data, "headTurnSpeed"),
                fireAngleTolerance = ReadFloat(data, "fireAngleTolerance"),
                loadedProjectileReloadRatio = ReadFloat(data, "loadedProjectileReloadRatio"),
                recoilDistance = ReadFloat(data, "recoilDistance"),
                recoilTiltAngle = ReadFloat(data, "recoilTiltAngle"),
                recoilKickDuration = ReadFloat(data, "recoilKickDuration"),
                recoilReturnDuration = ReadFloat(data, "recoilReturnDuration"),
                recoilSettleDistanceRatio = ReadFloat(data, "recoilSettleDistanceRatio"),
                recoilSettleTiltRatio = ReadFloat(data, "recoilSettleTiltRatio"),
                recoilSettleDuration = ReadFloat(data, "recoilSettleDuration"),
                projectilePrefab = ReadObject<GameObject>(data, "projectilePrefab"),
                impactVfxPrefab = ReadObject<GameObject>(data, "impactVfxPrefab"),
                impactVfxLifetime = ReadFloat(data, "impactVfxLifetime"),
                impactVfxScale = ReadFloat(data, "impactVfxScale"),
                fireSfx = ReadObject<SfxCue>(data, "fireSfx"),
                hitSfx = ReadObject<SfxCue>(data, "hitSfx"),
                explosionSfx = ReadObject<SfxCue>(data, "explosionSfx")
            };
            if (weaponKind == HexCastleTurretWeaponKind.Ballista)
            {
                profileData.impactType = HexCastleTurretImpactType.Direct;
                profileData.projectileCount = 1;
                profileData.fireSequentially = false;
                profileData.projectileVolleySize = 1;
                profileData.projectileFireDelay = 0f;
                profileData.spreadAngle = 0f;
                profileData.pierceCount = 1;
                profileData.piercingDamageRatio = 0f;
            }

            profile.EditorConfigure(profileData);
            if (!profile.IsValid || profile.WeaponKind != weaponKind || profile.Level != level)
            {
                throw new InvalidOperationException($"Hex 포탑 Profile 복제 결과가 잘못됐습니다: {outputPath}");
            }

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void EnsureOutputFolder()
        {
            const string parent =
                "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data";
            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder(parent, "Turrets");
            }
        }

        private static SerializedProperty Find(SerializedProperty data, string name)
        {
            return data.FindPropertyRelative(name) ??
                   throw new InvalidOperationException($"기존 포탑 Profile 필드가 없습니다: data.{name}");
        }

        private static int ReadInt(SerializedProperty data, string name) => Find(data, name).intValue;
        private static float ReadFloat(SerializedProperty data, string name) => Find(data, name).floatValue;
        private static bool ReadBool(SerializedProperty data, string name) => Find(data, name).boolValue;

        private static T ReadObject<T>(SerializedProperty data, string name) where T : UnityEngine.Object
        {
            return Find(data, name).objectReferenceValue as T;
        }
    }
}
