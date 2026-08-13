using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Tools.StageMapSlicer
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class StageVegetationGpuInstancingEnabler : MonoBehaviour
    {
        [SerializeField] private bool applyOnEnable = true;
        [SerializeField] private bool enableTerrainDrawInstanced = true;
        [SerializeField] private Material[] targetMaterials = Array.Empty<Material>();

        public IReadOnlyList<Material> TargetMaterials => targetMaterials;
        public int TargetMaterialCount => targetMaterials?.Length ?? 0;
        public bool EnableTerrainDrawInstanced => enableTerrainDrawInstanced;

        private void OnEnable()
        {
            if (applyOnEnable)
            {
                ApplyNow();
            }
        }

        public void Configure(IEnumerable<Material> materials, bool terrainDrawInstanced)
        {
            HashSet<Material> uniqueMaterials = new HashSet<Material>();
            if (materials != null)
            {
                foreach (Material material in materials)
                {
                    if (material != null)
                    {
                        uniqueMaterials.Add(material);
                    }
                }
            }

            targetMaterials = new Material[uniqueMaterials.Count];
            uniqueMaterials.CopyTo(targetMaterials);
            enableTerrainDrawInstanced = terrainDrawInstanced;

            if (isActiveAndEnabled && applyOnEnable)
            {
                ApplyNow();
            }
        }

        public int ApplyNow()
        {
            HashSet<Material> targets = targetMaterials != null
                ? new HashSet<Material>(targetMaterials)
                : new HashSet<Material>();
            targets.Remove(null);

#if UNITY_EDITOR
            foreach (Material material in targets)
            {
                material.enableInstancing = false;
            }
            foreach (Terrain terrain in GetComponentsInChildren<Terrain>(true))
            {
                terrain.drawInstanced = false;
            }
#else
            foreach (Material material in targets)
            {
                material.enableInstancing = true;
            }
            if (enableTerrainDrawInstanced)
            {
                foreach (Terrain terrain in GetComponentsInChildren<Terrain>(true))
                {
                    terrain.drawInstanced = true;
                }
            }
#endif

            return CountTargetRenderers(targets);
        }

        private int CountTargetRenderers(HashSet<Material> targets)
        {
            int targetRendererCount = 0;
            foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    if (!targets.Contains(materials[materialIndex]))
                    {
                        continue;
                    }

                    targetRendererCount++;
                    break; // unity_* 인스턴스 배열은 Renderer가 자동 관리
                }
            }

            return targetRendererCount;
        }

    }
}
