using System;
using System.Collections.Generic;
using ProjectMT.Core.SceneFlow;
using UnityEngine;

namespace ProjectMT.Core.Config
{
    public enum SceneKind // 정식 씬 역할 구분
    {
        Entry,
        MainBattle,
        SeparateContent
    }

    [Serializable]
    public sealed class SceneEntry // 씬 ID와 경로 한 묶음
    {
        [SerializeField] private SceneId sceneId; // 배포 후 유지할 식별자
        [SerializeField] private string scenePath; // Build Settings 경로
        [SerializeField] private SceneKind sceneKind; // 씬 용도

        public SceneEntry(SceneId sceneId, string scenePath, SceneKind sceneKind)
        {
            this.sceneId = sceneId;
            this.scenePath = scenePath;
            this.sceneKind = sceneKind;
        }

        public SceneId SceneId => sceneId;
        public string ScenePath => scenePath;
        public SceneKind SceneKind => sceneKind;
    }

    [CreateAssetMenu(menuName = "ProjectMT/Core/Scene Catalog", fileName = "SceneCatalog")]
    public sealed class SceneCatalog : ScriptableObject // 정식 씬 등록부
    {
        [SerializeField] private List<SceneEntry> entries = new List<SceneEntry>(); // 등록 씬 목록

        public IReadOnlyList<SceneEntry> Entries => entries;

        public bool TryGet(SceneId sceneId, out SceneEntry entry)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].SceneId == sceneId)
                {
                    entry = entries[i]; // ID가 같은 첫 항목 반환
                    return true;
                }
            }

            entry = null;
            return false;
        }

#if UNITY_EDITOR
        public void EditorSetEntries(IEnumerable<SceneEntry> values)
        {
            entries = values == null ? new List<SceneEntry>() : new List<SceneEntry>(values);
        }
#endif
    }
}
