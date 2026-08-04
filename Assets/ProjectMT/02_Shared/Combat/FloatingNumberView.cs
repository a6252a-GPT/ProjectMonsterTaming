using System;
using ProjectMT.Shared.Pooling;
using TMPro;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class FloatingNumberView : MonoBehaviour // 월드 숫자 한 개의 이동·페이드
    {
        [SerializeField] private TMP_Text valueText; // 숫자 표시 TMP

        private ScenePoolScope ownerPool; // 종료 뒤 반환할 전투 풀
        private Action released; // Presenter 활성 수 복구
        private Camera facingCamera; // 쿼터뷰 카메라
        private Vector3 startPosition; // 재생 시작 위치
        private Vector3 baseScale; // Prefab 기준 크기
        private Color baseColor; // 스타일 색상
        private float startedAt; // unscaled 시작 시각
        private float duration; // 전체 표시 시간
        private float riseDistance; // 위로 이동할 거리
        private float scaleMultiplier; // 치명타 등 크기 강조
        private bool playing; // 풀 복귀 중복 방지

        private void Awake()
        {
            if (valueText == null)
            {
                valueText = GetComponent<TMP_Text>();
            }

            baseScale = transform.localScale;
        }

        private void Update()
        {
            if (!playing)
            {
                return;
            }

            var ratio = Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);
            var eased = 1f - (1f - ratio) * (1f - ratio); // 가볍게 감속하는 상승
            transform.position = startPosition + Vector3.up * (riseDistance * eased);
            if (facingCamera != null)
            {
                transform.rotation = facingCamera.transform.rotation; // 월드 TMP를 카메라 정면으로 유지
            }

            var emphasis = 1f + Mathf.Sin(ratio * Mathf.PI) * 0.18f;
            transform.localScale = baseScale * (scaleMultiplier * emphasis);
            var color = baseColor;
            color.a *= ratio < 0.68f ? 1f : 1f - (ratio - 0.68f) / 0.32f;
            valueText.color = color;
            if (ratio >= 1f)
            {
                ReturnToPool();
            }
        }

        private void OnDisable()
        {
            playing = false;
            ownerPool = null;
            facingCamera = null;
            transform.localScale = baseScale;
            var callback = released;
            released = null;
            callback?.Invoke();
        }

        public void Play(
            ScenePoolScope pool,
            string value,
            Color color,
            float playDuration,
            float verticalDistance,
            float sizeMultiplier,
            Camera camera,
            Action onReleased)
        {
            if (valueText == null)
            {
                valueText = GetComponent<TMP_Text>();
            }

            ownerPool = pool;
            released = onReleased;
            facingCamera = camera;
            startPosition = transform.position;
            startedAt = Time.unscaledTime;
            duration = Mathf.Max(0.1f, playDuration);
            riseDistance = Mathf.Max(0f, verticalDistance);
            scaleMultiplier = Mathf.Max(0.1f, sizeMultiplier);
            baseColor = color;
            valueText.text = value ?? string.Empty;
            valueText.color = baseColor;
            transform.localScale = baseScale * scaleMultiplier;
            if (facingCamera != null)
            {
                transform.rotation = facingCamera.transform.rotation;
            }

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

#if UNITY_EDITOR
        public void EditorConfigure(TMP_Text text)
        {
            valueText = text;
        }
#endif
    }
}
