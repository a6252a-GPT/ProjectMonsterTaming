using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit
{
    public class MazeCameraFollow : MonoBehaviour
    {
        [Header("추적 대상")]
        public Transform target; // 스타터(플레이어) Transform

        [Header("카메라 오프셋 설정")]
        [SerializeField] private Vector3 offset = new Vector3(0, 5f, -2f); // 높이 5m, 뒤로 2m
        [SerializeField] private float followSpeed = 10f;

        private void LateUpdate()
        {
            if (target == null) return;

            // 스타터 위치 + 오프셋 좌표로 부드럽게 이동
            Vector3 targetPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);

            // 항상 스타터를 바라보도록 회전 설정
            transform.LookAt(target.position);
        }
    }
}