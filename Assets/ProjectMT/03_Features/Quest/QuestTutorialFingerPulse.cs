using UnityEngine;

namespace ProjectMT.Features.Quest
{
    // 튜토리얼 손가락의 위치·클릭 판정은 건드리지 않고 크기만 부드럽게 반복해 시선을 유도한다.
    [DisallowMultipleComponent]
    public sealed class QuestTutorialFingerPulse : MonoBehaviour
    {
        private const float MinimumScale = 0.9f;
        private const float MaximumScale = 1.08f;
        private const float CycleSeconds = 0.85f;

        private Vector3 baseScale;
        private bool initialized;

        public static QuestTutorialFingerPulse Ensure(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            var pulse = target.GetComponent<QuestTutorialFingerPulse>();
            return pulse != null ? pulse : target.AddComponent<QuestTutorialFingerPulse>();
        }

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

            var phase = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / CycleSeconds);
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
