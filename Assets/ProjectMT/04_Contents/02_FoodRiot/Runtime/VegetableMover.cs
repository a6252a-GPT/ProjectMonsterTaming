using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMT.Contents.FoodRiot
{
    [DisallowMultipleComponent]
    public sealed class VegetableMover : MonoBehaviour // 야채의 이동 및 반응(피격 도망, 점프) 담당
    {
        private Vector3 center; // 이동 구역 중심
        private Vector2 halfExtents; // 이동 구역 반쪽 크기
        private Vector3 direction; // 현재 이동 방향
        private float speed; // 초당 이동 거리
        private bool running; // 이동 활성 여부

        // --- 추가된 필드 ---
        [Header("점프 설정")]
        private float jumpTimer;
        private const float JumpInterval = 2.0f; // 2초 주기

        [Header("도망(피격) 설정")]
        private bool isFleeing;
        private float fleeTimer;
        private const float FleeDuration = 5.5f; // 피격 시 5.5초 동안 도망
        private const float FleeSpeedMultiplier = 1.8f; // 도망 시 속도 증폭 배율

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

            jumpTimer = 0f;
            isFleeing = false;
            fleeTimer = 0f;

            running = true;
        }

        private void Update()
        {
            if (!running)
            {
                return;
            }

            // --- [테스트용 코드] 키보드 Space 키나 마우스 우클릭 시 강제 피격(도망) 테스트 ---
            if (WasDebugHitPressed())
            {
                // 현재 야채 위치의 약간 앞쪽을 공격자 위치로 가정하고 도망 신호 전달
                OnDamaged(transform.position + transform.forward);
                Debug.Log("야채 피격! 도망 시작!");
            }
            // -------------------------------------------------------------------------

            // 1. 피격 후 도망 타이머 처리
            UpdateFleeState();

            // 2. 2초 주기 점프 처리
            UpdateJump();

            // 3. 이동 계산 (도망 상태일 때 속도 증가)
            float currentSpeed = isFleeing ? speed * FleeSpeedMultiplier : speed;
            var next = transform.position + direction * (currentSpeed * Time.deltaTime);

            // 경계 반사 및 Clamp
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

            // 회전 적용
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction, Vector3.up),
                    (isFleeing ? 12f : 6f) * Time.deltaTime); // 도망 시 빠르게 회전
            }
        }

        private static bool WasDebugHitPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                return true;
            }

            Mouse mouse = Mouse.current;
            return mouse != null && mouse.rightButton.wasPressedThisFrame;
        }

        /// <summary>
        /// 외부 피격 판정(군단장 공격 등) 시 호출되는 함수
        /// </summary>
        /// <param name="attackerPosition">공격자의 위치 (반대 방향으로 도망치기 위함)</param>
        public void OnDamaged(Vector3 attackerPosition)
        {
            if (!running) return;

            isFleeing = true;
            fleeTimer = FleeDuration;

            // 공격자 반대 방향으로 도망 방향 설정
            Vector3 fleeDir = (transform.position - attackerPosition);
            fleeDir.y = 0f; // XZ 평면 이동 고정

            if (fleeDir.sqrMagnitude > 0.01f)
            {
                direction = fleeDir.normalized;
            }
            else
            {
                direction = -transform.forward; // 거리가 너무 가까우면 뒤로 도망
            }
        }

        private void UpdateFleeState()
        {
            if (!isFleeing) return;

            fleeTimer -= Time.deltaTime;
            if (fleeTimer <= 0f)
            {
                isFleeing = false;
            }
        }

        private void UpdateJump()
        {
            jumpTimer += Time.deltaTime;
            if (jumpTimer >= JumpInterval)
            {
                jumpTimer = 0f;
                PerformJump();
            }
        }

        private void PerformJump()
        {
            // 물리 힘(AddForce) 대신 스크립트로 살짝 튀어올랐다 내려오는 연출
            StartCoroutine(JumpRoutine());
        }

        private System.Collections.IEnumerator JumpRoutine()
        {
            float duration = 0.4f; // 점프에 걸리는 총 시간 (0.4초)
            float jumpHeight = 1.0f; // 점프 높이
            float elapsed = 0f;
            Vector3 startPos = transform.position;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;

                // 포물선(Sin) 곡선으로 Y축 높이 계산
                float yOffset = Mathf.Sin(progress * Mathf.PI) * jumpHeight;

                // XZ 이동은 Update에서 처리하므로 Y 위치만 보정
                transform.position = new Vector3(transform.position.x, startPos.y + yOffset, transform.position.z);
                yield return null;
            }
        }

        private void OnDisable()
        {
            running = false;
        }
    }
}