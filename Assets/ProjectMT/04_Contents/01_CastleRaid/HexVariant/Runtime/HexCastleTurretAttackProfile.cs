using System;
using ProjectMT.Shared.Audio;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    public enum HexCastleTurretImpactType
    {
        Direct,
        Pierce,
        ExplosionArea
    }

    public enum HexCastleTurretTargetPriority
    {
        Nearest,
        BossEliteThenFarthest
    }

    [Serializable]
    public struct HexCastleTurretAttackProfileData
    {
        [Header("Identity")]
        public HexCastleTurretWeaponKind weaponKind;
        [Range(1, 3)] public int level;
        public HexCastleTurretImpactType impactType;
        public HexCastleTurretTargetPriority targetPriority;

        [Header("Attack")]
        [Min(0.1f)] public float sourceSearchRange;
        [Min(0f)] public float baseDamage;
        [Min(0.05f)] public float cooldown;
        [Min(1)] public int projectileCount;
        public bool fireSequentially;
        [Min(1)] public int projectileVolleySize;
        [Min(0f)] public float projectileFireDelay;
        [Min(0f)] public float spreadAngle;

        [Header("Projectile")]
        [Min(0.1f)] public float projectileSpeed;
        [Min(0.01f)] public float projectileHitRadius;
        [Min(0.1f)] public float projectileLifetime;
        [Min(1)] public int pierceCount;
        [Range(0f, 1f)] public float piercingDamageRatio;
        [Min(0f)] public float explosionRadius;
        [Min(0.01f)] public float projectileScale;
        [Min(0f)] public float targetAimHeight;

        [Header("Aim And Presentation")]
        [Min(1f)] public float headTurnSpeed;
        [Range(0.1f, 45f)] public float fireAngleTolerance;
        [Range(0f, 1f)] public float loadedProjectileReloadRatio;
        [Min(0f)] public float recoilDistance;
        [Min(0f)] public float recoilTiltAngle;
        [Min(0.01f)] public float recoilKickDuration;
        [Min(0.01f)] public float recoilReturnDuration;
        [Range(0f, 1f)] public float recoilSettleDistanceRatio;
        [Range(0f, 1f)] public float recoilSettleTiltRatio;
        [Min(0.01f)] public float recoilSettleDuration;

        [Header("Assets")]
        public GameObject projectilePrefab;
        public GameObject impactVfxPrefab;
        [Min(0.05f)] public float impactVfxLifetime;
        [Min(0.01f)] public float impactVfxScale;
        public SfxCue fireSfx;
        public SfxCue hitSfx;
        public SfxCue explosionSfx;

        public bool IsValid => weaponKind != HexCastleTurretWeaponKind.None &&
                               level >= 1 && level <= 3 && sourceSearchRange > 0f &&
                               cooldown > 0f && projectileCount > 0 && projectileSpeed > 0f &&
                               projectileHitRadius > 0f && projectileLifetime > 0f && projectilePrefab != null;

        public bool HasCompletePresentation
        {
            get
            {
                if (!IsValid || fireSfx == null || !fireSfx.HasPlayableClip)
                {
                    return false;
                }

                switch (weaponKind)
                {
                    case HexCastleTurretWeaponKind.Cannon:
                        return impactVfxPrefab != null;
                    case HexCastleTurretWeaponKind.Ballista:
                        return hitSfx != null && hitSfx.HasPlayableClip;
                    case HexCastleTurretWeaponKind.Fireball:
                        return impactVfxPrefab != null &&
                               explosionSfx != null && explosionSfx.HasPlayableClip;
                    default:
                        return false;
                }
            }
        }
    }

    [CreateAssetMenu(
        menuName = "ProjectMT/Castle Raid Hex/Turret Attack Profile",
        fileName = "HexCastleTurretAttackProfile")]
    public sealed class HexCastleTurretAttackProfile : ScriptableObject
    {
        [SerializeField] private HexCastleTurretAttackProfileData data;

        public HexCastleTurretAttackProfileData Data => data;
        public HexCastleTurretWeaponKind WeaponKind => data.weaponKind;
        public int Level => data.level;
        public bool IsValid => data.IsValid;
        public bool HasCompletePresentation => data.HasCompletePresentation;

#if UNITY_EDITOR
        public void EditorConfigure(HexCastleTurretAttackProfileData value)
        {
            value.level = Mathf.Clamp(value.level, 1, 3);
            if (value.weaponKind == HexCastleTurretWeaponKind.Ballista)
            {
                value.impactType = HexCastleTurretImpactType.Direct;
                value.projectileCount = 1;
                value.fireSequentially = false;
                value.projectileVolleySize = 1;
                value.projectileFireDelay = 0f;
                value.spreadAngle = 0f;
                value.pierceCount = 1;
                value.piercingDamageRatio = 0f;
            }

            value.sourceSearchRange = Mathf.Max(0.1f, value.sourceSearchRange);
            value.baseDamage = Mathf.Max(0f, value.baseDamage);
            value.cooldown = Mathf.Max(0.05f, value.cooldown);
            value.projectileCount = Mathf.Max(1, value.projectileCount);
            value.projectileVolleySize = Mathf.Clamp(value.projectileVolleySize, 1, value.projectileCount);
            value.projectileFireDelay = Mathf.Max(0f, value.projectileFireDelay);
            value.spreadAngle = Mathf.Max(0f, value.spreadAngle);
            value.projectileSpeed = Mathf.Max(0.1f, value.projectileSpeed);
            value.projectileHitRadius = Mathf.Max(0.01f, value.projectileHitRadius);
            value.projectileLifetime = Mathf.Max(0.1f, value.projectileLifetime);
            value.pierceCount = Mathf.Max(1, value.pierceCount);
            value.piercingDamageRatio = Mathf.Clamp01(value.piercingDamageRatio);
            value.explosionRadius = Mathf.Max(0f, value.explosionRadius);
            value.projectileScale = Mathf.Max(0.01f, value.projectileScale);
            value.targetAimHeight = Mathf.Max(0f, value.targetAimHeight);
            value.headTurnSpeed = Mathf.Max(1f, value.headTurnSpeed);
            value.fireAngleTolerance = Mathf.Clamp(value.fireAngleTolerance, 0.1f, 45f);
            value.loadedProjectileReloadRatio = Mathf.Clamp01(value.loadedProjectileReloadRatio);
            value.recoilDistance = Mathf.Max(0f, value.recoilDistance);
            value.recoilTiltAngle = Mathf.Max(0f, value.recoilTiltAngle);
            value.recoilKickDuration = Mathf.Max(0.01f, value.recoilKickDuration);
            value.recoilReturnDuration = Mathf.Max(0.01f, value.recoilReturnDuration);
            value.recoilSettleDistanceRatio = Mathf.Clamp01(value.recoilSettleDistanceRatio);
            value.recoilSettleTiltRatio = Mathf.Clamp01(value.recoilSettleTiltRatio);
            value.recoilSettleDuration = Mathf.Max(0.01f, value.recoilSettleDuration);
            value.impactVfxLifetime = Mathf.Max(0.05f, value.impactVfxLifetime);
            value.impactVfxScale = Mathf.Max(0.01f, value.impactVfxScale);
            data = value;
        }
#endif
    }

    public static class HexCastleTurretDamageMath
    {
        public static float ResolveExplosionDamage(float baseDamage, float radius, float distance)
        {
            if (baseDamage <= 0f || radius <= 0f || distance > radius)
            {
                return 0f;
            }

            return baseDamage * Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(distance / radius));
        }
    }
}
