using UnityEngine;

namespace ProjectMT.Contents.Framework
{
    [CreateAssetMenu(menuName = "ProjectMT/Content/Growth Dungeon Config", fileName = "GrowthDungeonConfig")]
    public sealed class GrowthDungeonConfig : ScriptableObject // Hosted 던전 실행 연결표
    {
        [SerializeField] private ContentId contentId; // 연결할 콘텐츠 ID
        [SerializeField] private GameObject runtimePrefab; // 같은 원본 실행 Prefab

        public ContentId ContentId => contentId;
        public GameObject RuntimePrefab => runtimePrefab;

#if UNITY_EDITOR
        public void EditorConfigure(ContentId id, GameObject prefab)
        {
            contentId = id;
            runtimePrefab = prefab;
        }
#endif
    }
}
