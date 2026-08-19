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
            "2f86b722492c5ba4abdf70ebc7f3620e",
            "7d7dec4cc6fbe52458bc059e87b907b7",
            "cbe773124cd013242aabb6a1f74c70e0",
            "5e796f7386e39aa4689bb40c2ee9da3b",
            "156ab314b017f57469b01f2613ef49ea",
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
