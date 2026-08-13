using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    [DisallowMultipleComponent]
    public sealed class CameraImpulseRig : MonoBehaviour // 가벼운 카메라 흔들림
    {
        [SerializeField] private Transform target; // 흔들 실제 Transform
        [SerializeField] private float recoverySpeed = 12f; // 원위치 복귀 속도

        private Vector3 originLocalPosition; // 시작 로컬 위치
        private float strength; // 남은 흔들림 세기
        private float phase;

        private void Awake()
        {
            if (target == null)
            {
                target = transform;
            }

            originLocalPosition = target.localPosition;
        }

        public void Impulse(float amount)
        {
            amount = Mathf.Max(0f, amount);
            if (amount <= strength)
            {
                return;
            }

            strength = amount; // 더 강한 요청만 유지
            phase = Mathf.Repeat(phase + 2.399963f, Mathf.PI * 2f); // 매 충격 방향은 바꾸되 한 충격 안에서는 부드럽게
        }

        public void RebaseOrigin()
        {
            if (target == null)
            {
                target = transform;
            }

            originLocalPosition = target.localPosition; // 카메라 구도 변경 뒤 새 기준점
            strength = 0f;
            phase = 0f;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var deltaTime = Time.unscaledDeltaTime;
            phase += deltaTime * 42f;
            strength *= Mathf.Exp(-Mathf.Max(1f, recoverySpeed) * deltaTime); // 짧고 매끈한 감쇠
            if (strength < 0.0005f)
            {
                strength = 0f;
                target.localPosition = originLocalPosition;
                return;
            }

            var offset = new Vector3(
                Mathf.Sin(phase),
                Mathf.Sin(phase * 1.37f + 1.1f) * 0.72f,
                0f) * strength; // 프레임 랜덤 지터 제거
            target.localPosition = originLocalPosition + offset;
        }

        private void OnDisable()
        {
            if (target != null)
            {
                target.localPosition = originLocalPosition;
            }

            strength = 0f;
            phase = 0f;
        }
    }
}
