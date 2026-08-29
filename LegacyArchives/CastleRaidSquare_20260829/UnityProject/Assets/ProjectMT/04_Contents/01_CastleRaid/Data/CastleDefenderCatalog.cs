using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    [CreateAssetMenu(
        menuName = "ProjectMT/Castle Raid/Defender Catalog",
        fileName = "CastleRaidDefenderCatalog")]
    public sealed class CastleDefenderCatalog : ScriptableObject // 군단의 역습이 재사용할 정식 적 프리팹 목록
    {
        public const string DefaultResourcesPath = "CastleRaidDefenderCatalog";

        [SerializeField] private GameObject[] defenderPrefabs = Array.Empty<GameObject>();

        public IReadOnlyList<GameObject> DefenderPrefabs => defenderPrefabs;
        public bool IsComplete => CountConfiguredPrefabs() > 0;

        public GameObject Resolve(int seed, int defenseLayerCount)
        {
            _ = defenseLayerCount; // 난이도별 후보 제한은 다음 고도화 지점으로 남긴다
            var configuredCount = CountConfiguredPrefabs();
            if (configuredCount == 0)
            {
                return null;
            }

            var selectedIndex = PositiveModulo(seed, configuredCount);
            for (var index = 0; index < defenderPrefabs.Length; index++)
            {
                var prefab = defenderPrefabs[index];
                if (prefab == null)
                {
                    continue;
                }

                if (selectedIndex-- == 0)
                {
                    return prefab;
                }
            }

            return null;
        }

        private int CountConfiguredPrefabs()
        {
            var count = 0;
            for (var index = 0; index < defenderPrefabs.Length; index++)
            {
                if (defenderPrefabs[index] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

#if UNITY_EDITOR
        public void EditorConfigure(GameObject[] prefabs)
        {
            if (prefabs == null || prefabs.Length == 0)
            {
                throw new ArgumentException("수비대 프리팹이 하나 이상 필요합니다.", nameof(prefabs));
            }

            defenderPrefabs = (GameObject[])prefabs.Clone();
        }
#endif
    }
}
