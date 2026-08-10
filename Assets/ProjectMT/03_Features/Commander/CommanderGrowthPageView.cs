using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Commander
{
    [DisallowMultipleComponent]
    public sealed class CommanderGrowthPageView : MonoBehaviour
    {
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            closeButton?.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            closeButton?.onClick.RemoveListener(Close);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
