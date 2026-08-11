using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Equipment
{
    // "UpgradeSlotButton" 클릭 시 EquipmentSlotUpgradePanelController(PF_EquipmentSlotUpgrade)를 여는 진입점.
    [DisallowMultipleComponent]
    public sealed class EquipmentSlotUpgradeEntryPoint : MonoBehaviour
    {
        private Button openButton;
        private EquipmentSlotUpgradePanelController panel;

        private void Awake()
        {
            // 씬 루트부터 찾아서 UpgradeSlotButton이 계층 어디에 있어도 연결한다.
            var buttonTransform = FindDeep(transform.root, "UpgradeSlotButton");
            if (buttonTransform == null)
            {
                Debug.LogWarning("EquipmentSlotUpgradeEntryPoint: UpgradeSlotButton을 찾지 못했습니다.", this);
                return;
            }

            openButton = buttonTransform.GetComponent<Button>();
            if (openButton == null)
            {
                openButton = buttonTransform.gameObject.AddComponent<Button>();
                openButton.transition = Selectable.Transition.None;
            }

            openButton.onClick.AddListener(HandleOpenButtonClicked);
        }

        private void OnDestroy()
        {
            openButton?.onClick.RemoveListener(HandleOpenButtonClicked);
        }

        // 패널 인스턴스를 주입받고 기본 비활성 상태로 강제한다.
        public void Configure(EquipmentSlotUpgradePanelController panelController)
        {
            panel = panelController;
            if (panel != null)
            {
                panel.gameObject.SetActive(false);
            }
        }

        private void HandleOpenButtonClicked()
        {
            if (panel == null)
            {
                Debug.LogWarning("EquipmentSlotUpgradeEntryPoint: 연결된 EquipmentSlotUpgradePanelController가 없습니다.", this);
                return;
            }

            panel.Open();
        }

        private static Transform FindDeep(Transform root, string childName)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].name == childName)
                {
                    return all[i];
                }
            }

            return null;
        }
    }
}
