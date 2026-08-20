using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Quest
{
    // "MissionButton"에 붙여서 누르면 일일·주간 퀘스트 패널("Progression_Mission")을 연다.
    // Progression_Mission은 보통 MissionButton과 같은 부모(MissionUI) 아래의 형제 오브젝트라서
    // 우선 형제 관계로 찾고, 구조가 달라져도 동작하도록 이름 기반 전체 탐색을 예비 경로로 둔다.
    [DisallowMultipleComponent]
    public sealed class MissionButtonController : MonoBehaviour
    {
        private const string PanelObjectName = "Progression_Mission";

        [SerializeField] private GameObject missionPanel;

        private Button button;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        private void ResolveReferences()
        {
            if (missionPanel == null)
            {
                missionPanel = FindPanel();
            }

            if (button == null)
            {
                button = GetComponent<Button>();
            }

            // 다른 기능이 상위 오브젝트를 SetActive(false)/(true)로 껐다 켜면 리스너가 중복 등록되거나
            // 빠질 수 있어, 매번(Awake·OnEnable) Remove 후 Add로 다시 붙여 클릭이 항상 살아있게 한다.
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
                button.onClick.AddListener(HandleClicked);
            }
            else
            {
                Debug.LogWarning(
                    $"[Quest][UI] \"{name}\"에 Button 컴포넌트가 없어 미션 패널을 열 수 없습니다.", this);
            }
        }

        private void HandleClicked()
        {
            if (missionPanel == null)
            {
                missionPanel = FindPanel();
                if (missionPanel == null)
                {
                    Debug.LogWarning(
                        $"[Quest][UI] \"{PanelObjectName}\" 오브젝트를 찾지 못해 미션 패널을 열 수 없습니다.", this);
                    return;
                }
            }

            missionPanel.SetActive(true);
        }

        private GameObject FindPanel()
        {
            var root = transform.root != null ? transform.root : transform;

            // 패널이 중복 생성 등으로 이름이 바뀌어도(예: "Progression_Mission2") 항상 찾을 수 있도록,
            // 이름보다 먼저 실제 패널 기능을 담당하는 컴포넌트(DailyMissionPanelView)로 찾는다.
            var panelView = root.GetComponentInChildren<DailyMissionPanelView>(true);
            if (panelView != null)
            {
                return panelView.gameObject;
            }

            var sibling = transform.parent != null ? transform.parent.Find(PanelObjectName) : null;
            if (sibling != null)
            {
                return sibling.gameObject;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == PanelObjectName)
                {
                    return transforms[i].gameObject;
                }
            }

            return null;
        }

#if UNITY_EDITOR
        public void EditorConfigure(GameObject panel)
        {
            missionPanel = panel;
        }
#endif
    }
}
