using UnityEngine;

namespace ProjectMT.Features.Quest
{
    public static class QuestPanelRuntimeFactory // 편집용 전시 패널을 실제 HUD 팝업으로 복제
    {
        public static DailyMissionPanelView Create(DailyMissionPanelView source, Transform popupParent)
        {
            if (source == null || popupParent == null)
            {
                return source;
            }

            if (source.transform.IsChildOf(popupParent))
            {
                source.Close();
                StretchToParent(source.transform as RectTransform);
                return source;
            }

            source.Close(); // 전시 슬롯은 플레이 화면에서 직접 열지 않는다.
            var instance = Object.Instantiate(source.gameObject, popupParent, false);
            instance.name = source.gameObject.name.Replace("_REFERENCE", "_Runtime");
            StretchToParent(instance.transform as RectTransform);
            instance.SetActive(false);
            return instance.GetComponent<DailyMissionPanelView>();
        }

        private static void StretchToParent(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }
    }
}
