using System;
using System.Collections.Generic;
using ProjectMT.Core.SceneFlow;
using UnityEngine;

namespace ProjectMT.Contents.Framework
{
    [Serializable]
    public sealed class ContentSceneVariant // 콘텐츠 변형과 전용 씬 연결
    {
        [SerializeField] private ContentVariantId variantId;
        [SerializeField] private SceneId sceneId;

        public ContentSceneVariant(ContentVariantId variantId, SceneId sceneId)
        {
            this.variantId = variantId;
            this.sceneId = sceneId;
        }

        public ContentVariantId VariantId => variantId;
        public SceneId SceneId => sceneId;
    }

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
        [SerializeField] private List<ContentSceneVariant> sceneVariants = new List<ContentSceneVariant>(); // 같은 보상 규칙의 공간 변형

        public ContentId ContentId => contentId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? contentId.Value : displayName.Trim();
        public ContentOpenMode OpenMode => openMode;
        public SceneId SceneId => sceneId;
        public ContentStartDataFactory StartDataFactory => startDataFactory;
        public ContentResultAdapter ResultAdapter => resultAdapter;
        public string DungeonKeyItemId => dungeonKeyItemId?.Trim() ?? string.Empty;
        public bool SupportsSweep => supportsSweep && !string.IsNullOrEmpty(DungeonKeyItemId);

        public bool TryResolveSceneId(ContentVariantId variantId, out SceneId resolvedSceneId)
        {
            if (!variantId.IsValid)
            {
                resolvedSceneId = sceneId;
                return resolvedSceneId.IsValid;
            }

            for (var i = 0; i < sceneVariants.Count; i++)
            {
                var variant = sceneVariants[i];
                if (variant != null && variant.VariantId == variantId && variant.SceneId.IsValid)
                {
                    resolvedSceneId = variant.SceneId;
                    return true;
                }
            }

            resolvedSceneId = default;
            return false;
        }

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

        public void EditorSetSceneVariants(IEnumerable<ContentSceneVariant> variants)
        {
            sceneVariants = variants == null
                ? new List<ContentSceneVariant>()
                : new List<ContentSceneVariant>(variants);
        }
#endif
    }
}
