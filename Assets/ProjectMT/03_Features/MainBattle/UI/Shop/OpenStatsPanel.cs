using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class OpenStatsPanel : MonoBehaviour // GrowthButton 클릭으로 성장 관련 패널 2개 토글
    {
        [SerializeField] private Button growthButton; // 클릭 시 패널 열고 닫는 버튼 (캐릭터 성장)
        [SerializeField] private GameObject upgradePanel; // 능력치 강화 패널 (UpgradePanel)
        [SerializeField] private GameObject characterStatsPanel; // 현재 능력치 패널 (CharacterStatsPanel)

        public bool IsPanelOpen =>
            (upgradePanel != null && upgradePanel.activeSelf) ||
            (characterStatsPanel != null && characterStatsPanel.activeSelf); // 둘 중 하나라도 열려 있으면 true

        private void Awake()
        {
            if (growthButton == null)
            {
                growthButton = GetComponent<Button>(); // Inspector에 안 넣었으면 같은 오브젝트 Button으로 대체
            }

            if (upgradePanel == null || characterStatsPanel == null)
            {
                Debug.LogError("OpenStatsPanel: upgradePanel 또는 characterStatsPanel 참조가 비어 있습니다.", this);
                return;
            }

            SetPanelsActive(false); // 시작 시 두 패널 모두 비활성화
            growthButton?.onClick.AddListener(ToggleStatsPanel); // 클릭 이벤트 등록
        }

        private void OnDestroy()
        {
            growthButton?.onClick.RemoveListener(ToggleStatsPanel); // 오브젝트 파괴 시 이벤트 해제로 누수 방지
        }

        private void ToggleStatsPanel()
        {
            if (upgradePanel == null || characterStatsPanel == null)
            {
                return;
            }

            SetPanelsActive(!IsPanelOpen); // 닫혀 있으면 둘 다 열고, 열려 있으면 둘 다 닫기
        }

        private void SetPanelsActive(bool active)
        {
            upgradePanel.SetActive(active); // UpgradePanel 활성/비활성
            characterStatsPanel.SetActive(active); // CharacterStatsPanel 활성/비활성
        }

#if UNITY_EDITOR
        public void EditorConfigure(Button button, GameObject upgrade, GameObject characterStats) // 에디터 참조 연결용
        {
            growthButton = button;
            upgradePanel = upgrade;
            characterStatsPanel = characterStats;
        }
#endif
    }
}
