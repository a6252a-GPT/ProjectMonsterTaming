using UnityEngine;

namespace ProjectMT.Contents.GrowthDungeon
{
    [DisallowMultipleComponent]
    public class DungeonStarterController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5.0f; // 초당 이동 속도

        private void Update()
        {
            // 1. 키보드 WASD / 방향키 입력 받기
            float x = Input.GetAxisRaw("Horizontal"); // A/D 또는 좌/우 화살표 (-1 ~ 1)
            float z = Input.GetAxisRaw("Vertical");   // W/S 또는 위/아래 화살표 (-1 ~ 1)

            Vector3 moveDir = new Vector3(x, 0f, z).normalized;

            if (moveDir.sqrMagnitude > 0.01f)
            {
                // 2. 이동 처리 (Translate)
                transform.Translate(moveDir * (moveSpeed * Time.deltaTime), Space.World);

                // 3. 이동 방향 바라보기 (회전)
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 15f * Time.deltaTime);
            }

        }

        // 열쇠 소지 여부
        public bool HasKey { get; set; } = false;
    }
}