using System.Linq;
using ProjectMT.Shared.Equipment;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Equipment
{
    // 08.10 안건준 수정 - 테스트용 "장비 획득" 버튼(GetEquipmentButton).
    //
    // 실제로는 원정대 10·15·20... 스테이지를 클리어해야 장비 6개를 얻지만,
    // 아직 그 연결 작업 전이라 이 버튼을 누르면 "장비 드랍 대상 스테이지를 클리어했다"고
    // 가정하고 동일한 드랍 로직(EquipmentDropRoller)으로 장비 6개를 즉시 지급한다.
    //
    // 08.10 안건준 수정 - 이제 장비 보유가 GameProgressData(저장 파일)에 실제로 저장되므로,
    // 이 버튼으로 얻은 장비도 재시작 후에도 남는다. "저장 데이터 초기화" 디버그 기능을 쓰면
    // 다른 진행 데이터와 함께 초기화된다.
    [DisallowMultipleComponent]
    public sealed class EquipmentTestAcquireButton : MonoBehaviour
    {
        [SerializeField] private Button acquireButton;

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

        private async void HandleClicked()
        {
            var drops = EquipmentDropRoller.RollDrop();
            var acquired = await EquipmentInventoryRuntime.TryAcquireDropAsync(drops);
            if (!acquired)
            {
                Debug.LogWarning("EquipmentTestAcquireButton: 장비 획득 저장에 실패했습니다(진행 데이터 미로딩 등).", this);
                return;
            }

            Debug.Log($"[테스트] 장비 {drops.Count}개 획득: " +
                      string.Join(", ", drops.Select(d => $"{EquipmentGradeInfo.GetDisplayName(d.Grade)} {EquipmentPartInfo.GetDisplayName(d.Part)}")));
        }
    }
}
