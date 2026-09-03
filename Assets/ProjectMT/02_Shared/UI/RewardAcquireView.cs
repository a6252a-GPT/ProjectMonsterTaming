using System;
using ProjectMT.Shared.Pooling;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Shared.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class RewardAcquireView : MonoBehaviour // 보상 한 줄의 HUD 방향 이동·페이드
    {
        [SerializeField] private Image itemIcon; // ItemDefinition 또는 장비 부위의 실제 아이콘
        [SerializeField] private TMP_Text labelText; // 이름과 수량 표시
        [SerializeField] private TMP_Text equipmentLevelText; // 장비만 아이콘 하단 중앙에 표시

        private RectTransform rectTransform; // UI 이동 대상
        private CanvasGroup canvasGroup; // 입력 없이 투명도만 제어
        private ScenePoolScope ownerPool; // 재생 종료 반환 풀
        private Action released; // Presenter 활성 수 복구
        private Vector2 startPosition; // 시작 로컬 좌표
        private Vector2 targetPosition; // HUD 도착 로컬 좌표
        private float startedAt; // unscaled 시작 시각
        private float duration; // 전체 연출 시간
        private bool playing; // 중복 반환 방지

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (!playing)
            {
                return;
            }

            var ratio = Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);
            var eased = 1f - Mathf.Pow(1f - ratio, 3f); // 초반 빠르고 도착 때 감속
            var arc = Mathf.Sin(ratio * Mathf.PI) * 72f;
            rectTransform.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, eased) + Vector2.up * arc;
            rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(ratio * Mathf.PI) * 0.14f);
            canvasGroup.alpha = ratio < 0.76f ? 1f : 1f - (ratio - 0.76f) / 0.24f;
            if (ratio >= 1f)
            {
                ReturnToPool();
            }
        }

        private void OnDisable()
        {
            playing = false;
            ownerPool = null;
            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.one;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            if (itemIcon != null)
            {
                itemIcon.sprite = null;
                itemIcon.gameObject.SetActive(false);
            }

            if (equipmentLevelText != null)
            {
                equipmentLevelText.text = string.Empty;
                equipmentLevelText.gameObject.SetActive(false);
            }

            var callback = released;
            released = null;
            callback?.Invoke();
        }

        public void Play(
            ScenePoolScope pool,
            Sprite icon,
            string label,
            Color color,
            Vector2 from,
            Vector2 to,
            float playDuration,
            Action onReleased)
        {
            Play(pool, icon, label, color, 0, from, to, playDuration, onReleased);
        }
        public void Play(
            ScenePoolScope pool,
            Sprite icon,
            string label,
            Color color,
            int equipmentLevel,
            Vector2 from,
            Vector2 to,
            float playDuration,
            Action onReleased)
        {
            ResolveReferences();
            ownerPool = pool;
            released = onReleased;
            startPosition = from;
            targetPosition = to;
            startedAt = Time.unscaledTime;
            duration = Mathf.Max(0.2f, playDuration);
            if (itemIcon != null)
            {
                itemIcon.sprite = icon;
                itemIcon.color = Color.white;
                itemIcon.preserveAspect = true;
                itemIcon.gameObject.SetActive(icon != null);
            }

            if (labelText != null)
            {
                var labelRect = labelText.rectTransform;
                var offsetMin = labelRect.offsetMin;
                offsetMin.x = icon != null ? 82f : 18f;
                labelRect.offsetMin = offsetMin;
            }

            labelText.text = label ?? string.Empty;
            labelText.color = color;
            if (equipmentLevelText != null)
            {
                equipmentLevelText.text = equipmentLevel > 0 ? $"Lv.{equipmentLevel}" : string.Empty;
                equipmentLevelText.gameObject.SetActive(equipmentLevel > 0);
            }
            rectTransform.anchoredPosition = startPosition;
            rectTransform.localScale = Vector3.one;
            canvasGroup.alpha = 1f;
            playing = true;
        }

        private void ReturnToPool()
        {
            if (!playing)
            {
                return;
            }

            playing = false;
            var pool = ownerPool;
            if (pool != null)
            {
                pool.Return(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void ResolveReferences()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (labelText == null)
            {
                labelText = GetComponentInChildren<TMP_Text>(true);
            }

            if (equipmentLevelText == null)
            {
                var texts = GetComponentsInChildren<TMP_Text>(true);
                for (var index = 0; index < texts.Length; index++)
                {
                    if (texts[index].name == "Text_Level")
                    {
                        equipmentLevelText = texts[index];
                        break;
                    }
                }
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(Image icon, TMP_Text text, TMP_Text level = null)
        {
            itemIcon = icon;
            labelText = text;
            equipmentLevelText = level;
            ResolveReferences();
        }
#endif
    }
}


