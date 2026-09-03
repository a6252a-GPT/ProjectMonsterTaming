#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class BakedDungeonPrefabRegistry
    {
        internal static readonly string[] DefaultPrefabGuids =
        {
            "cbe773124cd013242aabb6a1f74c70e0", // Easy1
            "5e796f7386e39aa4689bb40c2ee9da3b", // Easy2
            "156ab314b017f57469b01f2613ef49ea", // Easy3
            "2f86b722492c5ba4abdf70ebc7f3620e", // Normal1
            "7d7dec4cc6fbe52458bc059e87b907b7", // Normal2
            "b577592ed6167cd4f80e6f93c18e0e8a", // Normal3
            "1001b59f03163d64c8ae170bdf0ab9df", // Hard1
        };

        internal static GameObject[] LoadDefaultPrefabs()
        {
#if UNITY_EDITOR
            GameObject[] prefabs = new GameObject[DefaultPrefabGuids.Length];

            for (int i = 0; i < DefaultPrefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(DefaultPrefabGuids[i]);
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return prefabs;
#else
            return null;
#endif
        }
    }
}
