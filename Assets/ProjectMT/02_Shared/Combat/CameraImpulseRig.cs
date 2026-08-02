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
            strength = Mathf.Max(strength, Mathf.Max(0f, amount)); // 더 강한 요청만 유지
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            strength = Mathf.MoveTowards(strength, 0f, recoverySpeed * Time.unscaledDeltaTime); // 일시정지와 무관하게 복귀
            var offset = Random.insideUnitSphere * strength;
            offset.z = 0f; // 화면 깊이 흔들림 제외
            target.localPosition = originLocalPosition + offset;
        }

        private void OnDisable()
        {
            if (target != null)
            {
                target.localPosition = originLocalPosition;
            }

            strength = 0f;
        }
    }
}
