using System;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    public enum CastleTurretFamily // 포탑 헤드 교체와 후속 공격 연결에 쓰는 종류 계약
    {
        Cannon,
        Ballista,
        Fireball
    }

    [DisallowMultipleComponent]
    public sealed class CastleTurretVisual : MonoBehaviour
    {
        [SerializeField] private CastleTurretFamily family;
        [SerializeField, Range(1, 3)] private int level = 1;
        [SerializeField] private Transform headRoot;
        [SerializeField] private Transform yawPivot;
        [SerializeField] private Transform pitchPivot;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Transform muzzleVfx;
        [SerializeField] private GameObject[] loadedProjectiles = Array.Empty<GameObject>();

        public CastleTurretFamily Family => family;
        public int Level => level;
        public Transform HeadRoot => headRoot;
        public Transform YawPivot => yawPivot;
        public Transform PitchPivot => pitchPivot;
        public Transform Muzzle => muzzle;
        public int LoadedProjectileCount => loadedProjectiles?.Length ?? 0;

        public void Configure(CastleTurretFamily turretFamily, int turretLevel, Transform instantiatedHead)
        {
            family = turretFamily;
            level = Mathf.Clamp(turretLevel, 1, 3);
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

            if (headRoot == null || yawPivot == null || pitchPivot == null || muzzle == null)
            {
                throw new InvalidOperationException("포탑 헤드의 BodyMount/Yaw/Pitch/Muzzle 조립 계약이 완성되지 않았습니다.");
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

        public void PlayMuzzleVfx()
        {
            if (muzzleVfx == null)
            {
                return;
            }

            muzzleVfx.gameObject.SetActive(true);
            var particles = muzzleVfx.GetComponentsInChildren<ParticleSystem>(true);
            for (var index = 0; index < particles.Length; index++)
            {
                particles[index].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles[index].Play(true);
            }
        }
    }

    public static class CastleTurretVisualSelector
    {
        public static CastleTurretFamily ResolveFamily(int seed, string placementId)
        {
            unchecked
            {
                const uint offsetBasis = 2166136261u;
                const uint prime = 16777619u;
                var hash = (offsetBasis ^ (uint)seed) * prime;
                var id = placementId ?? string.Empty;
                for (var index = 0; index < id.Length; index++)
                {
                    hash = (hash ^ id[index]) * prime;
                }

                return (CastleTurretFamily)(hash % 3u);
            }
        }

        public static int ResolveLevel(int defenseLayerCount, int defenseRing)
        {
            return Mathf.Clamp(defenseLayerCount - Mathf.Max(0, defenseRing), 1, 3);
        }
    }
}
