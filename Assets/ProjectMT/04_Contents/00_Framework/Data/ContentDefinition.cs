using ProjectMT.Core.SceneFlow;
using UnityEngine;

namespace ProjectMT.Contents.Framework
{
    [CreateAssetMenu(menuName = "ProjectMT/Content/Definition", fileName = "ContentDefinition")]
    public sealed class ContentDefinition : ScriptableObject // 콘텐츠 가벼운 신분증
    {
        [SerializeField] private ContentId contentId; // 콘텐츠 고정 식별자
        [SerializeField] private string displayName; // 공통 결과·선택 UI 이름
        [SerializeField] private ContentOpenMode openMode; // Hosted·별도 씬 구분
        [SerializeField] private SceneId sceneId; // 별도 씬일 때만 사용
        [SerializeField] private ContentStartDataFactory startDataFactory; // 타입 시작값 생성기
        [SerializeField] private ContentResultAdapter resultAdapter; // 타입 결과 번역기
        [SerializeField] private string dungeonKeyItemId; // 성장 던전 파밍·소탕 비용
        [SerializeField] private bool supportsSweep; // Runtime 없이 동일 규칙 정산 가능

        public ContentId ContentId => contentId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? contentId.Value : displayName.Trim();
        public ContentOpenMode OpenMode => openMode;
        public SceneId SceneId => sceneId;
        public ContentStartDataFactory StartDataFactory => startDataFactory;
        public ContentResultAdapter ResultAdapter => resultAdapter;
        public string DungeonKeyItemId => dungeonKeyItemId?.Trim() ?? string.Empty;
        public bool SupportsSweep => supportsSweep && !string.IsNullOrEmpty(DungeonKeyItemId);

#if UNITY_EDITOR
        public void EditorConfigure(
            ContentId id,
            ContentOpenMode mode,
            SceneId targetSceneId,
            ContentStartDataFactory factory,
            ContentResultAdapter adapter)
        {
            contentId = id;
            openMode = mode;
            sceneId = targetSceneId;
            startDataFactory = factory;
            resultAdapter = adapter;
        }

        public void EditorConfigurePresentationAndCost(string title, string keyItemId, bool sweepEnabled)
        {
            displayName = title?.Trim();
            dungeonKeyItemId = keyItemId?.Trim();
            supportsSweep = sweepEnabled;
        }
#endif
    }
}
