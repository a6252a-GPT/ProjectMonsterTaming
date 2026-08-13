using System.Collections;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit
{
    public class PrisonDoor : MonoBehaviour
    {
        [Header("회전시킬 문 Transform")]
        [SerializeField] private Transform doorMeshTransform;

        [Header("문 회전 속도")]
        [SerializeField] private float openSpeed = 2.0f;

        [Header("던전 컨트롤러")]
        [SerializeField] private DungeonController dungeonController;

        private bool isOpened = false;
        private MazeGenerator mazeGenerator;

        private void Start()
        {
            mazeGenerator = FindFirstObjectByType<MazeGenerator>();

            // Hierarchy 상에서 던전 컨트롤러 자동 탐색
            if (dungeonController == null)
            {
                dungeonController = FindFirstObjectByType<DungeonController>();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"트리거 진입: {other.name}, tag={other.tag}"); // 임시로 최상단에 추가

            if (isOpened) return;

            // 플레이어(군단장) 인식 (태그, 컴포넌트, 루트 태그 포함)
            bool isPlayer = other.CompareTag("Player") ||
                            other.transform.root.CompareTag("Player") ||
                            other.GetComponentInParent<PlayerCharacterController>() != null ||
                            other.name.Contains("Commander");

            if (isPlayer)
            {
                // MazeGenerator의 HasKey(열쇠 소지 여부) 체크
                if (mazeGenerator != null && mazeGenerator.HasKey)
                {
                    OpenDoor();
                }
                else
                {
                    Debug.Log("🔒 잠겨있습니다. 열쇠가 필요합니다!");
                }
            }
        }

        private void OpenDoor()
        {
            isOpened = true;
            Debug.Log("🔓 열쇠로 문을 열었습니다!");

            if (doorMeshTransform != null)
            {
                StartCoroutine(RotateDoorRoutine());
            }
            else
            {
                transform.Rotate(0f, -90f, 0f);
            }

            // 문이 열린 시점에 던전 컨트롤러에 클리어 알림 전달
            if (dungeonController != null)
            {
                dungeonController.CompleteDungeon();
            }
            else
            {
                Debug.LogWarning("⚠️ DungeonController를 찾을 수 없습니다.");
            }
        }

        private IEnumerator RotateDoorRoutine()
        {
            Quaternion startRotation = doorMeshTransform.localRotation;
            Quaternion targetRotation = startRotation * Quaternion.Euler(0f, -90f, 0f);

            float progress = 0f;
            while (progress < 1f)
            {
                progress += Time.deltaTime * openSpeed;
                doorMeshTransform.localRotation = Quaternion.Slerp(startRotation, targetRotation, progress);
                yield return null;
            }

            doorMeshTransform.localRotation = targetRotation;
        }
    }
}