using System;
using System.Collections.Generic;
using ProjectMT.Shared.Audio;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public static class HexCastleTurretAttackAssetUtility // Hex 독립 포탑 프로필과 Catalog를 검증·재구성한다
    {
        public const string CatalogPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data/Turrets/CRHex_TurretAttackCatalog.asset";

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
                    profiles.Add(LoadProfile(weaponKind, level));
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
                "[Hex Turret] Hex 독립 공격 수치·Projectile·Muzzle/Impact VFX·SFX 참조를 " +
                "대포 2·발리스타 2·화염구 3레벨 Catalog로 재구성했습니다. " +
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

        private static HexCastleTurretAttackProfile LoadProfile(
            HexCastleTurretWeaponKind weaponKind,
            int level)
        {
            var family = weaponKind.ToString();
            var outputPath = $"{OutputFolder}/CRHex_TurretAttack_{family}_Lv{level}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<HexCastleTurretAttackProfile>(outputPath);
            if (profile == null)
            {
                throw new InvalidOperationException($"Hex 포탑 공격 Profile이 없습니다: {outputPath}");
            }

            if (!profile.IsValid || profile.WeaponKind != weaponKind || profile.Level != level)
            {
                throw new InvalidOperationException($"Hex 포탑 Profile 계약이 잘못됐습니다: {outputPath}");
            }

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

    }
}
