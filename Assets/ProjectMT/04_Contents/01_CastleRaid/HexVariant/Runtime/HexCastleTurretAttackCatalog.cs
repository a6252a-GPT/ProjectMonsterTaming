using System;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [CreateAssetMenu(
        menuName = "ProjectMT/Castle Raid Hex/Turret Attack Catalog",
        fileName = "HexCastleTurretAttackCatalog")]
    public sealed class HexCastleTurretAttackCatalog : ScriptableObject
    {
        [SerializeField] private HexCastleTurretAttackProfile[] profiles = new HexCastleTurretAttackProfile[7];

        public bool IsComplete
        {
            get
            {
                if (profiles == null || profiles.Length != 7)
                {
                    return false;
                }

                for (var weaponIndex = (int)HexCastleTurretWeaponKind.Cannon;
                     weaponIndex <= (int)HexCastleTurretWeaponKind.Fireball;
                     weaponIndex++)
                {
                    var weapon = (HexCastleTurretWeaponKind)weaponIndex;
                    for (var level = 1; level <= ResolveSupportedMaximumLevel(weapon); level++)
                    {
                        if (Resolve(weapon, level) == null)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        public bool HasCompletePresentation
        {
            get
            {
                if (!IsComplete)
                {
                    return false;
                }

                for (var weaponIndex = (int)HexCastleTurretWeaponKind.Cannon;
                     weaponIndex <= (int)HexCastleTurretWeaponKind.Fireball;
                     weaponIndex++)
                {
                    var weapon = (HexCastleTurretWeaponKind)weaponIndex;
                    for (var level = 1; level <= ResolveSupportedMaximumLevel(weapon); level++)
                    {
                        if (!Resolve(weapon, level).HasCompletePresentation)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        public HexCastleTurretAttackProfile Resolve(HexCastleTurretWeaponKind weaponKind, int level)
        {
            if (weaponKind == HexCastleTurretWeaponKind.None || profiles == null)
            {
                return null;
            }

            if (level < 1 || level > ResolveSupportedMaximumLevel(weaponKind))
            {
                return null;
            }

            for (var index = 0; index < profiles.Length; index++)
            {
                var profile = profiles[index];
                if (profile != null && profile.IsValid &&
                    profile.WeaponKind == weaponKind && profile.Level == level)
                {
                    return profile;
                }
            }

            return null;
        }

        public static int ResolveSupportedMaximumLevel(HexCastleTurretWeaponKind weaponKind)
        {
            return weaponKind == HexCastleTurretWeaponKind.Fireball
                ? 3
                : weaponKind == HexCastleTurretWeaponKind.Cannon ||
                  weaponKind == HexCastleTurretWeaponKind.Ballista
                    ? 2
                    : 0;
        }

#if UNITY_EDITOR
        public void EditorConfigure(HexCastleTurretAttackProfile[] source)
        {
            if (source == null || source.Length != 7)
            {
                throw new ArgumentException("육각 포탑 공격 프로필은 대포 2·발리스타 2·화염구 3레벨, 일곱 개가 필요합니다.", nameof(source));
            }

            profiles = new HexCastleTurretAttackProfile[source.Length];
            Array.Copy(source, profiles, source.Length);
            if (!IsComplete)
            {
                throw new ArgumentException("육각 포탑 공격 프로필 종류·레벨 계약이 완성되지 않았습니다.", nameof(source));
            }
        }
#endif
    }
}
