using UnityEngine;

namespace ProjectMT.Contents.FoodRiot
{
    [DisallowMultipleComponent]
    public sealed class VegetableMover : MonoBehaviour // 야채의 단순 왕복 이동 담당
    {
        private Vector3 center; // 이동 구역 중심
        private Vector2 halfExtents; // 이동 구역 반쪽 크기
        private Vector3 direction; // 현재 이동 방향
        private float speed; // 초당 이동 거리
        private bool running; // 이동 활성 여부

        public void Initialize(Vector3 areaCenter, Vector2 areaHalfExtents, float moveSpeed)
        {
            center = areaCenter;
            halfExtents = areaHalfExtents;
            speed = Mathf.Max(0.2f, moveSpeed);
            direction = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
            if (direction.sqrMagnitude < 0.1f)
            {
                direction = Vector3.forward; // 무방향 난수 예외 처리
            }

            running = true;
        }

        private void Update()
        {
            if (!running)
            {
                return;
            }

            var next = transform.position + direction * (speed * Time.deltaTime);
            if (next.x < center.x - halfExtents.x || next.x > center.x + halfExtents.x)
            {
                direction.x *= -1f; // 좌우 경계에서 반사
                next.x = Mathf.Clamp(next.x, center.x - halfExtents.x, center.x + halfExtents.x);
            }

            if (next.z < center.z - halfExtents.y || next.z > center.z + halfExtents.y)
            {
                direction.z *= -1f; // 위아래 경계에서 반사
                next.z = Mathf.Clamp(next.z, center.z - halfExtents.y, center.z + halfExtents.y);
            }

            transform.position = next;
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction, Vector3.up),
                    6f * Time.deltaTime);
            }
        }

        private void OnDisable()
        {
            running = false;
        }
    }
}
