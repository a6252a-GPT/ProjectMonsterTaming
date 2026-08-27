using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectMT.Shared.UI
{
    // 손을 대고 있는 동안만 콜백을 유지하는 범용 트리거. 길게 눌러야 보이는 미리보기/툴팁류 UI에 사용.
    //
    // IPointerExitHandler는 의도적으로 쓰지 않는다: 누르는 중 화면을 덮는 오버레이(Dimmed 등)를 켜면
    // 다음 프레임 레이캐스트가 그 오버레이에 맞아 "영역을 벗어났다"고 오판해 바로 닫혀버리기 때문.
    // OnPointerUp은 현재 포인터 아래에 뭐가 있는지와 무관하게 눌렀던 오브젝트에 그대로 전달된다.
    [DisallowMultipleComponent]
    public sealed class PointerHoldTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Action holdStart;
        private Action holdEnd;
        private bool isHolding;

        public void Configure(Action onHoldStart, Action onHoldEnd)
        {
            holdStart = onHoldStart;
            holdEnd = onHoldEnd;
        }

        private void OnDisable()
        {
            Release();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            isHolding = true;
            holdStart?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        private void Release()
        {
            if (!isHolding)
            {
                return;
            }

            isHolding = false;
            holdEnd?.Invoke();
        }

        // target에 아직 없으면 하나 붙여서 반환한다.
        public static PointerHoldTrigger EnsureOn(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            return target.GetComponent<PointerHoldTrigger>() ?? target.AddComponent<PointerHoldTrigger>();
        }
    }
}
