using System;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleTurretVisual : MonoBehaviour // 재사용 헤드의 조준 소켓 계약
    {
        [SerializeField] private HexCastleTurretWeaponKind weaponKind;
        [SerializeField, Range(1, 3)] private int level = 1;
        [SerializeField] private Transform headRoot;
        [SerializeField] private Transform yawPivot;
        [SerializeField] private Transform pitchPivot;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Transform muzzleVfx;
        [SerializeField] private GameObject[] loadedProjectiles = Array.Empty<GameObject>();

        public HexCastleTurretWeaponKind WeaponKind => weaponKind;
        public int Level => level;
        public Transform HeadRoot => headRoot;
        public Transform YawPivot => yawPivot;
        public Transform PitchPivot => pitchPivot;
        public Transform Muzzle => muzzle;
        public int LoadedProjectileCount => loadedProjectiles?.Length ?? 0;
        public bool IsComplete => headRoot != null && yawPivot != null && pitchPivot != null && muzzle != null;

        public void Configure(
            HexCastleTurretWeaponKind targetWeaponKind,
            int targetLevel,
            Transform instantiatedHead)
        {
            if (targetWeaponKind == HexCastleTurretWeaponKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(targetWeaponKind));
            }

            weaponKind = targetWeaponKind;
            level = Mathf.Clamp(targetLevel, 1, 3);
            headRoot = instantiatedHead;
            yawPivot = headRoot?.Find("Joint_BodyMount/YawPivot");
            pitchPivot = yawPivot?.Find("PitchPivot");
            muzzle = pitchPivot?.Find("Muzzle");
            muzzleVfx = muzzle?.Find("VFX_Muzzle");
            var loadedRoot = pitchPivot?.Find("LoadedProjectiles");
            if (loadedRoot == null || loadedRoot.childCount == 0)
            {
                loadedProjectiles = Array.Empty<GameObject>();
            }
            else
            {
                loadedProjectiles = new GameObject[loadedRoot.childCount];
                for (var index = 0; index < loadedRoot.childCount; index++)
                {
                    loadedProjectiles[index] = loadedRoot.GetChild(index).gameObject;
                }
            }

            if (!IsComplete)
            {
                throw new InvalidOperationException(
                    "육각 포탑 헤드의 BodyMount/Yaw/Pitch/Muzzle 조립 계약이 완성되지 않았습니다.");
            }
        }

        public void SetLoadedProjectileVisible(int projectileIndex, bool visible)
        {
            if (loadedProjectiles == null || loadedProjectiles.Length == 0)
            {
                return;
            }

            var loaded = loadedProjectiles[Mathf.Abs(projectileIndex) % loadedProjectiles.Length];
            if (loaded != null)
            {
                loaded.SetActive(visible);
            }
        }

        public void SetAllLoadedProjectilesVisible(bool visible)
        {
            if (loadedProjectiles == null)
            {
                return;
            }

            for (var index = 0; index < loadedProjectiles.Length; index++)
            {
                if (loadedProjectiles[index] != null)
                {
                    loadedProjectiles[index].SetActive(visible);
                }
            }
        }

        public bool PlayMuzzleVfx()
        {
            if (muzzleVfx == null)
            {
                return false;
            }

            muzzleVfx.gameObject.SetActive(true);
            var particles = muzzleVfx.GetComponentsInChildren<ParticleSystem>(true);
            for (var index = 0; index < particles.Length; index++)
            {
                particles[index].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles[index].Play(true);
            }

            return particles.Length > 0;
        }
    }
}
