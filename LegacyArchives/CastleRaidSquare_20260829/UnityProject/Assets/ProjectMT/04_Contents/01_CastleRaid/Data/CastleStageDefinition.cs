using ProjectMT.Contents.CastleRaid.Generation;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    [CreateAssetMenu(menuName = "ProjectMT/Castle Raid/Stage Definition", fileName = "CastleStageDefinition")]
    public sealed class CastleStageDefinition : ScriptableObject // 향후 다중 성 등록용 정의
    {
        [SerializeField] private string stageId = "castle_seed"; // Stage 고정 식별자
        [SerializeField] private GameObject stagePrefab; // 현재는 연결 자리만 사용
        [SerializeField] private CastleStageLayout approvedLayout; // 승인된 절차 생성 배치 원본

        public string StageId => stageId;
        public GameObject StagePrefab => stagePrefab;
        public CastleStageLayout ApprovedLayout => approvedLayout;

#if UNITY_EDITOR
        public void EditorConfigure(string id, GameObject prefab)
        {
            stageId = id;
            stagePrefab = prefab;
        }

        public void EditorConfigure(string id, GameObject prefab, CastleStageLayout layout)
        {
            stageId = id;
            stagePrefab = prefab;
            approvedLayout = layout;
        }
#endif
    }
}
