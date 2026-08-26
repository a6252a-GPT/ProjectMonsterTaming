using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [CreateAssetMenu(
        fileName = "HexCastleGarrisonCatalog",
        menuName = "ProjectMT/Castle Raid Hex/Garrison Catalog")]
    public sealed class HexCastleGarrisonCatalog : ScriptableObject // Hex 병영이 쓸 정식 인간 유닛 외형 목록
    {
        public const string DefaultResourcesPath = "HexCastleGarrisonCatalog";

        [SerializeField] private GameObject[] knightPrefabs = Array.Empty<GameObject>();
        [SerializeField] private GameObject farmerPrefab;

        public IReadOnlyList<GameObject> KnightPrefabs => knightPrefabs;
        public GameObject FarmerPrefab => farmerPrefab;
        public bool IsComplete => farmerPrefab != null &&
                                  knightPrefabs != null &&
                                  knightPrefabs.Any(value => value != null);

        public GameObject ResolveKnight(int seed, int spawnSequence)
        {
            if (knightPrefabs == null || knightPrefabs.Length == 0)
            {
                return null;
            }

            var configured = knightPrefabs.Where(value => value != null).ToArray();
            if (configured.Length == 0)
            {
                return null;
            }

            unchecked
            {
                var score = seed * 397 ^ spawnSequence;
                var index = score % configured.Length;
                if (index < 0)
                {
                    index += configured.Length;
                }

                return configured[index];
            }
        }

        public GameObject ResolveFarmer()
        {
            return farmerPrefab;
        }

#if UNITY_EDITOR
        public void EditorConfigure(GameObject[] knights, GameObject farmer)
        {
            if (knights == null || knights.Length == 0 || knights.All(value => value == null))
            {
                throw new ArgumentException("기사 프리팹이 하나 이상 필요합니다.", nameof(knights));
            }

            knightPrefabs = knights.Where(value => value != null).Distinct().ToArray();
            farmerPrefab = farmer != null
                ? farmer
                : throw new ArgumentNullException(nameof(farmer));
        }
#endif
    }
}
