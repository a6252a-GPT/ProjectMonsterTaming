using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    public sealed class FallenCommanderResolveVfxPool
    {
        private sealed class Entry
        {
            public GameObject Instance;
            public Vector3 PrefabScale;
        }

        private readonly List<Entry> entries = new();
        private GameObject activePrefab;
        private int activeCount;

        public GameObject Play(
            FallenCommanderAttackEffectData effects,
            Vector3 position,
            Vector3 direction,
            Transform parent,
            Transform boss,
            Transform commander,
            Vector3 areaScale)
        {
            var prefab = effects?.ResolveVfxPrefab;
            if (prefab == null)
            {
                return null;
            }

            EnsurePrefab(prefab);
            var entry = Acquire(prefab, parent);
            var context = new FallenCommanderEffectPlacementContext(
                position,
                direction,
                boss == null ? (Vector3?)null : boss.position,
                commander == null ? (Vector3?)null : commander.position,
                null);
            var placement = FallenCommanderEffectPlacementResolver.Resolve(
                effects,
                FallenCommanderEffectStage.Resolve,
                context);
            var transform = entry.Instance.transform;
            transform.SetParent(parent, true);
            transform.SetPositionAndRotation(placement.Position, placement.Rotation);
            transform.localScale = Vector3.Scale(
                entry.PrefabScale,
                Vector3.Scale(placement.Scale, ResolveScale(areaScale)));
            entry.Instance.SetActive(true);
            RestartParticles(entry.Instance);
            return entry.Instance;
        }

        public void ReleaseAll()
        {
            for (var index = 0; index < activeCount; index++)
            {
                if (entries[index].Instance != null)
                {
                    entries[index].Instance.SetActive(false);
                }
            }

            activeCount = 0;
        }

        public void Clear()
        {
            for (var index = 0; index < entries.Count; index++)
            {
                if (entries[index].Instance != null)
                {
                    Object.Destroy(entries[index].Instance);
                }
            }

            entries.Clear();
            activeCount = 0;
            activePrefab = null;
        }

        private Entry Acquire(GameObject prefab, Transform parent)
        {
            Entry entry;
            if (activeCount < entries.Count)
            {
                entry = entries[activeCount];
                if (entry.Instance == null)
                {
                    entry.Instance = Object.Instantiate(prefab, parent);
                    entry.PrefabScale = entry.Instance.transform.localScale;
                }
            }
            else
            {
                var instance = Object.Instantiate(prefab, parent);
                entry = new Entry
                {
                    Instance = instance,
                    PrefabScale = instance.transform.localScale
                };
                entries.Add(entry);
            }

            activeCount++;
            return entry;
        }

        private void EnsurePrefab(GameObject prefab)
        {
            if (activePrefab == prefab)
            {
                return;
            }

            Clear();
            activePrefab = prefab;
        }

        private static void RestartParticles(GameObject root)
        {
            var particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var index = 0; index < particles.Length; index++)
            {
                particles[index].Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
                particles[index].Play(true);
            }
        }

        private static Vector3 ResolveScale(Vector3 scale)
        {
            return scale == Vector3.zero ? Vector3.one : scale;
        }
    }
}
