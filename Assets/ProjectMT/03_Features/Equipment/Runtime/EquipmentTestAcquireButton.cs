using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Equipment
{
    // 08.09 안건준 추가 - 테스트용 "장비 획득" 버튼(GetEquipmentButton).
    //
    // 실제로는 원정대 10·15·20... 스테이지를 클리어해야 장비 6개를 얻지만,
    // 아직 그 연결 작업 전이라 이 버튼을 누르면 "장비 드랍 대상 스테이지를 클리어했다"고
    // 가정하고 동일한 드랍 로직(EquipmentDropRoller)으로 장비 6개를 즉시 지급한다.
    //
    // 요청사항: 이 테스트 획득분은 저장하지 않고 현재 플레이 세션에서만 유지한다.
    // EquipmentInventoryRuntime이 static(세션 한정) 저장소이므로, 플레이를 재시작하면
    // 별도 처리 없이 자동으로 비워진다.
    [DisallowMultipleComponent]
    public sealed class EquipmentTestAcquireButton : MonoBehaviour
    {
        [SerializeField] private Button acquireButton;
        [SerializeField] private EquipmentCatalog catalog;

        private void Awake()
        {
            var button = ResolveButton();
            button?.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            var button = ResolveButton();
            button?.onClick.RemoveListener(HandleClicked);
        }

        // 이 스크립트는 GetEquipmentButton 오브젝트에 직접 붙으므로, 인스펙터에 연결이 안 돼 있으면
        // 같은 오브젝트의 Button 컴포넌트를 그대로 사용한다.
        private Button ResolveButton()
        {
            if (acquireButton == null)
            {
                acquireButton = GetComponent<Button>();
            }

            return acquireButton;
        }

        private EquipmentCatalog ResolveCatalog()
        {
            if (catalog != null)
            {
                return catalog;
            }

#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("t:EquipmentCatalog");
            if (guids.Length > 0)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<EquipmentCatalog>(path);
            }
#endif
            return catalog;
        }

        private void HandleClicked()
        {
            var equipmentCatalog = ResolveCatalog();
            if (equipmentCatalog == null)
            {
                Debug.LogWarning("EquipmentTestAcquireButton: EquipmentCatalog 참조가 없습니다.", this);
                return;
            }

            var drops = EquipmentDropRoller.RollDrop(equipmentCatalog);
            foreach (var definition in drops)
            {
                EquipmentInventoryRuntime.AddEquipment(definition, 1);
            }

            Debug.Log($"[테스트] 장비 6개 획득: {string.Join(", ", drops.Select(d => d.DisplayName))}");
        }
    }
}
