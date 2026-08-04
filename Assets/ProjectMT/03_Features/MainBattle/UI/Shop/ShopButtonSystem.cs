using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class ShopButtonSystem : MonoBehaviour // 상점 패널 열기/닫기
    {
        [SerializeField] private Button gachaShopButton; // HUD의 "뽑기 상점" 버튼 (열기)
        [SerializeField] private Button closeButton; // ShopPanel 안의 "닫기" 버튼
        [SerializeField] private GameObject shopPanel; // 활성화/비활성화할 상점 패널 (ShopPanel)

        public bool IsShopOpen => shopPanel != null && shopPanel.activeSelf; // 외부에서 상점 열림 상태 확인용

        private void Awake()
        {
            if (shopPanel == null)
            {
                Debug.LogError("ShopButtonSystem: shopPanel reference is missing.", this); // 필수 참조 누락 경고
                return;
            }

            shopPanel.SetActive(false); // 시작 시 항상 비활성화
            gachaShopButton?.onClick.AddListener(OpenShop); // 뽑기 상점 버튼 → 패널 열기
            closeButton?.onClick.AddListener(CloseShop); // 닫기 버튼 → 패널 닫기
        }

        private void OnDestroy()
        {
            gachaShopButton?.onClick.RemoveListener(OpenShop); // 이벤트 해제로 누수 방지
            closeButton?.onClick.RemoveListener(CloseShop);
        }

        private void OpenShop()
        {
            if (shopPanel == null)
            {
                return;
            }

            shopPanel.SetActive(true); // 상점 패널 표시
        }

        private void CloseShop()
        {
            if (shopPanel == null)
            {
                return;
            }

            shopPanel.SetActive(false); // 상점 패널 숨김
        }

#if UNITY_EDITOR
        public void EditorConfigure(Button openButton, Button close, GameObject panel) // 에디터에서 참조 자동 연결용
        {
            gachaShopButton = openButton;
            closeButton = close;
            shopPanel = panel;
        }
#endif
    }
}
