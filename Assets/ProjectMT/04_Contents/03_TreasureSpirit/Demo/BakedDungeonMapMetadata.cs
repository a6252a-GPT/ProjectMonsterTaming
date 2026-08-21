using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    [DisallowMultipleComponent]
    public sealed class BakedDungeonMapMetadata : MonoBehaviour
    {
        [SerializeField] private Transform playerSpawnPoint;

        public Transform PlayerSpawnPoint => playerSpawnPoint;

        public void ResolvePlayerSpawn(Transform mapRoot)
        {
            if (playerSpawnPoint != null || mapRoot == null)
            {
                return;
            }

            playerSpawnPoint = DemoMapUtil.FindStartPoint(mapRoot)
                ?? DemoMapUtil.FindDeepChild(mapRoot, "PlayerSpawn")
                ?? DemoMapUtil.FindDeepChild(mapRoot, "StartPoint");
        }
    }
}
