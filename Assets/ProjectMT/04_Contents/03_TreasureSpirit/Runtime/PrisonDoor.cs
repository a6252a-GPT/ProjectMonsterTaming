using System.Collections;
using UnityEngine;

namespace ProjectMT.Contents.GrowthDungeon
{
    public class PrisonDoor : MonoBehaviour
    {
        [Header("회전시킬 문 Transform")]
        [SerializeField] private Transform doorMeshTransform;

        [Header("문 회전 속도")]
        [SerializeField] private float openSpeed = 2.0f;

        private bool isOpened = false;

        private void OnTriggerEnter(Collider other)
        {
            if (isOpened) return;

            DungeonStarterController player = other.GetComponent<DungeonStarterController>();
            if (player != null)
            {
                if (player.HasKey)
                {
                    OpenDoor();
                }
                else
                {
                    Debug.Log("🔒 잠겨있습니다. 열쇠가 필요합니다.");
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
                // Assign이 안 되어 있다면 자기 자신을 회전
                transform.Rotate(0f, -90f, 0f);
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