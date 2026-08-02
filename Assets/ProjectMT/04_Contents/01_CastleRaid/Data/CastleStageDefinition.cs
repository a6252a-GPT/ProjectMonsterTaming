using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    [CreateAssetMenu(menuName = "ProjectMT/Castle Raid/Stage Definition", fileName = "CastleStageDefinition")]
    public sealed class CastleStageDefinition : ScriptableObject // 향후 다중 성 등록용 정의
    {
        [SerializeField] private string stageId = "castle_seed"; // Stage 고정 식별자
        [SerializeField] private GameObject stagePrefab; // 현재는 연결 자리만 사용

        public string StageId => stageId;
        public GameObject StagePrefab => stagePrefab;

#if UNITY_EDITOR
        public void EditorConfigure(string id, GameObject prefab)
        {
            stageId = id;
            stagePrefab = prefab;
        }
#endif
    }
}
