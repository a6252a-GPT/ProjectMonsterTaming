using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectMT.Features.Equipment
{
    // 08.10 안건준 추가 - 인벤토리 페이지를 이전/다음 버튼뿐 아니라 "위/아래로 드래그(스와이프)" 또는
    // "마우스 휠 스크롤"로도 넘길 수 있도록, 유니티 기본 Scroll View(ScrollRect)의 입력을 페이지 전환
    // 이벤트로 바꿔주는 핸들러.
    //
    // 08.10 안건준 수정 - 드래그(IDragHandler)만 처리하고 있었는데, 에디터/PC 테스트에서는 마우스 휠
    // 스크롤(IScrollHandler)로 시도하는 경우가 많고 이 둘은 유니티에서 서로 다른 이벤트라 휠은 전혀
    // 반응하지 않았다. 휠 스크롤도 같이 처리하도록 추가.
    //
    // 콘텐츠를 실제로 스크롤해서 여러 페이지를 이어붙여 보여주는 방식이 아니라, 기존 버튼 방식과 동일하게
    // "한 번에 20개씩" 페이지 단위로 슬롯 내용을 다시 채우는 구조를 그대로 유지한다. 그래서 드래그/휠 입력이
    // 일정 기준을 넘으면 페이지를 1칸 넘기고, 스크롤 위치 자체는 다시 원위치로 되돌린다(콘텐츠가 밀린 채로
    // 남아있지 않도록).
    [RequireComponent(typeof(RectTransform))]
    public sealed class EquipmentInventorySwipeHandler :
        MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        private const float SwipeThresholdPixels = 60f;
        private const float ScrollWheelCooldownSeconds = 0.25f; // 휠 한 번 굴릴 때 여러 페이지가 한꺼번에 넘어가지 않도록

        // +1 = 다음 페이지(위로 드래그 / 휠을 아래로), -1 = 이전 페이지(아래로 드래그 / 휠을 위로)
        public event System.Action<int> PageDeltaRequested;

        private ScrollRect scrollRect;
        private Vector2 dragStartPosition;
        private bool consumedThisDrag;
        private float lastScrollWheelTime = float.NegativeInfinity;

        private void Awake()
        {
            scrollRect = GetComponent<ScrollRect>();
        }

        // 마우스 휠 스크롤 처리. scrollDelta.y 는 휠을 위로 올리면 양수, 아래로 내리면 음수로 들어온다
        // (일반적인 목록 스크롤 관례상 "아래로 내리면 다음 내용"이 되도록 부호를 맞춘다).
        public void OnScroll(PointerEventData eventData)
        {
            var deltaY = eventData.scrollDelta.y;
            if (Mathf.Approximately(deltaY, 0f))
            {
                return;
            }

            if (Time.unscaledTime - lastScrollWheelTime < ScrollWheelCooldownSeconds)
            {
                return;
            }

            lastScrollWheelTime = Time.unscaledTime;
            PageDeltaRequested?.Invoke(deltaY < 0f ? 1 : -1);
            ResetScrollPosition();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragStartPosition = eventData.position;
            consumedThisDrag = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (consumedThisDrag)
            {
                return;
            }

            // 위로 드래그(화면 기준 y가 줄어듦)하면 양수가 되도록 계산 -> "다음 페이지"로 취급한다.
            var deltaY = dragStartPosition.y - eventData.position.y;
            if (Mathf.Abs(deltaY) < SwipeThresholdPixels)
            {
                return;
            }

            consumedThisDrag = true;
            PageDeltaRequested?.Invoke(deltaY > 0f ? 1 : -1);
            ResetScrollPosition(); // 페이지가 바뀌는 즉시 되돌려서 콘텐츠가 밀린 채로 남아있지 않게 한다.
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ResetScrollPosition();
        }

        // 실제 페이지 전환은 EquipmentPageController가 슬롯 내용을 다시 채우는 방식으로 처리하므로,
        // 스크롤뷰 자체는 항상 맨 위(1f)로 되돌려서 다음 드래그도 같은 기준으로 판정되게 한다.
        private void ResetScrollPosition()
        {
            if (scrollRect == null)
            {
                return;
            }

            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;
            scrollRect.horizontalNormalizedPosition = 0f;
        }
    }
}
