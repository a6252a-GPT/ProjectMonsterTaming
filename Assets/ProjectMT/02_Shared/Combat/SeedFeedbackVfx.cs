using ProjectMT.Shared.Pooling;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    [DisallowMultipleComponent]
    public sealed class SeedFeedbackVfx : MonoBehaviour // 시드용 확장 펄스 VFX
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Renderer targetRenderer; // 색상을 바꿀 렌더러

        private MaterialPropertyBlock block; // Material 복제 없는 색상 변경
        private ScenePoolScope owner; // 종료 뒤 반환할 풀
        private float duration; // 전체 재생 시간
        private float elapsed; // 누적 재생 시간
        private float size; // 최종 크기

        public void Play(ScenePoolScope pool, Color color, float playDuration, float targetSize)
        {
            if (block == null)
            {
                block = new MaterialPropertyBlock(); // 첫 재생 때 한 번 생성
            }

            owner = pool;
            duration = Mathf.Max(0.05f, playDuration);
            elapsed = 0f;
            size = Mathf.Max(0.05f, targetSize);
            transform.localScale = Vector3.one * 0.05f;
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            if (targetRenderer != null)
            {
                block.Clear();
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                targetRenderer.SetPropertyBlock(block); // 공유 Material은 유지
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            var ratio = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.one * Mathf.Lerp(0.05f, size, 1f - Mathf.Pow(1f - ratio, 2f));
            if (ratio >= 1f)
            {
                var pool = owner;
                owner = null;
                pool?.Return(gameObject); // 재생 완료 후 재사용
            }
        }
    }
}
