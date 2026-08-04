using System;
using ProjectMT.Shared.Pooling;
using TMPro;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class FloatingNumberView : MonoBehaviour // 월드 숫자 한 개의 곡선 이동·페이드
    {
        [SerializeField] private TMP_Text valueText; // 숫자 표시 TMP

        private ScenePoolScope ownerPool; // 종료 뒤 반환할 전투 풀
        private Action released; // Presenter 활성 수 복구
        private Camera facingCamera; // 쿼터뷰 카메라
        private Vector3 startPosition; // 재생 시작 위치
        private Vector3 cameraRight = Vector3.right; // 화면 기준 좌우 방향
        private Vector3 baseScale; // Prefab 기준 크기
        private Color baseColor; // 스타일 색상
        private float startedAt; // unscaled 시작 시각
        private float duration; // 전체 표시 시간
        private float riseDistance; // 위로 이동할 거리
        private float horizontalDistance; // 좌우 팬 이동 거리
        private float arcHeight; // 이동 곡선의 추가 높이
        private float initialTilt; // 시작 기울기
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
            var riseRatio = 1f - Mathf.Pow(1f - ratio, 3f); // 빠르게 뜨고 부드럽게 감속
            var fanRatio = Mathf.Sin(ratio * Mathf.PI * 0.5f); // 좌우로 완만하게 펼침
            var arcOffset = Mathf.Sin(ratio * Mathf.PI) * arcHeight;
            transform.position = startPosition
                + Vector3.up * (riseDistance * riseRatio + arcOffset)
                + cameraRight * (horizontalDistance * fanRatio);
            if (facingCamera != null)
            {
                var tilt = initialTilt * (1f - Mathf.SmoothStep(0f, 1f, ratio));
                transform.rotation = facingCamera.transform.rotation * Quaternion.Euler(0f, 0f, tilt); // 기울었다가 정면으로 안정
            }

            transform.localScale = baseScale * (scaleMultiplier * ResolvePopScale(ratio));
            var color = baseColor;
            var enterAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(ratio / 0.08f));
            var exitAlpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((ratio - 0.7f) / 0.3f));
            color.a *= Mathf.Min(enterAlpha, exitAlpha);
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
            cameraRight = Vector3.right;
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
            float sideDistance,
            float verticalArcHeight,
            float tiltAngle,
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
            cameraRight = facingCamera != null ? facingCamera.transform.right : Vector3.right;
            startPosition = transform.position;
            startedAt = Time.unscaledTime;
            duration = Mathf.Max(0.1f, playDuration);
            riseDistance = Mathf.Max(0f, verticalDistance);
            horizontalDistance = sideDistance;
            arcHeight = Mathf.Max(0f, verticalArcHeight);
            initialTilt = tiltAngle;
            scaleMultiplier = Mathf.Max(0.1f, sizeMultiplier);
            baseColor = color;
            valueText.text = value ?? string.Empty;
            var hiddenColor = baseColor;
            hiddenColor.a = 0f;
            valueText.color = hiddenColor;
            transform.localScale = baseScale * (scaleMultiplier * 0.72f);
            if (facingCamera != null)
            {
                transform.rotation = facingCamera.transform.rotation * Quaternion.Euler(0f, 0f, initialTilt);
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

        private static float ResolvePopScale(float ratio) // 짧게 튀어나온 뒤 안정
        {
            if (ratio < 0.16f)
            {
                return Mathf.Lerp(0.72f, 1.16f, Mathf.SmoothStep(0f, 1f, ratio / 0.16f));
            }

            if (ratio < 0.4f)
            {
                return Mathf.Lerp(1.16f, 1f, Mathf.SmoothStep(0f, 1f, (ratio - 0.16f) / 0.24f));
            }

            return Mathf.Lerp(1f, 0.94f, Mathf.SmoothStep(0f, 1f, (ratio - 0.4f) / 0.6f));
        }

#if UNITY_EDITOR
        public void EditorConfigure(TMP_Text text)
        {
            valueText = text;
        }
#endif
    }
}
