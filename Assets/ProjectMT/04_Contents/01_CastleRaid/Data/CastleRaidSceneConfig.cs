using ProjectMT.Contents.Framework;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    [CreateAssetMenu(menuName = "ProjectMT/Castle Raid/Scene Config", fileName = "CastleRaidSceneConfig")]
    public sealed class CastleRaidSceneConfig : ScriptableObject // CastleRaid 씬 연결 설정
    {
        [SerializeField] private ContentId contentId; // 씬이 받을 콘텐츠 ID
        [SerializeField] private CastleStageDefinition stageDefinition; // 향후 Stage 등록 자리

        public ContentId ContentId => contentId;
        public CastleStageDefinition StageDefinition => stageDefinition;

#if UNITY_EDITOR
        public void EditorConfigure(ContentId id, CastleStageDefinition definition)
        {
            contentId = id;
            stageDefinition = definition;
        }
#endif
    }
}
