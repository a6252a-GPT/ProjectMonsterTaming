using UnityEngine;

namespace ProjectMT.Features.Quest
{
    // 원본 손가락은 세 번 움직인 뒤 제자리에 남아 안내한다.
    [DisallowMultipleComponent]
    public sealed class QuestTutorialFingerPulse : MonoBehaviour
    {
        private const float MinimumScale = 0.9f;
        private const float MaximumScale = 1.08f;
        private const float CycleSeconds = 0.85f;

        private Vector3 baseScale;
        private bool initialized;
        private float startedAt;

        private void OnEnable() { startedAt = Time.unscaledTime; }

        private void Awake()
        {
            Rebase();
        }

        private void Update()
        {
            if (!initialized)
            {
                Rebase();
            }

            var elapsed = Time.unscaledTime - startedAt;
            if (elapsed >= CycleSeconds * 3f) { transform.localScale = baseScale; return; }
            var phase = Mathf.Sin(elapsed * Mathf.PI * 2f / CycleSeconds);
            var factor = Mathf.Lerp(MinimumScale, MaximumScale, (phase + 1f) * 0.5f);
            transform.localScale = new Vector3(
                baseScale.x * factor,
                baseScale.y * factor,
                baseScale.z);
        }

        private void OnDisable()
        {
            if (initialized)
            {
                transform.localScale = baseScale;
            }
        }

        public void Rebase()
        {
            baseScale = transform.localScale;
            initialized = true;
        }
    }
}
