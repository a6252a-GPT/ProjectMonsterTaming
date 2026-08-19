using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    public sealed class DemoPrisonDoor : MonoBehaviour
    {
        [SerializeField] private float openSpeed = 2f;

        private Transform doorMeshTransform;
        private BakedDungeonLoader keyState;
        private DemoDungeonController dungeonController;
        private bool isOpened;

        public void Configure(
            Transform doorMesh,
            BakedDungeonLoader keySource,
            DemoDungeonController controller)
        {
            doorMeshTransform = doorMesh;
            keyState = keySource;
            dungeonController = controller;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isOpened || !DemoPlayerDetector.IsPlayer(other, null))
            {
                return;
            }

            if (keyState != null && keyState.HasKey)
            {
                OpenDoor();
            }
            else
            {
                Debug.Log("[DemoPrisonDoor] 열쇠가 필요합니다.");
            }
        }

        private void OpenDoor()
        {
            isOpened = true;
            Debug.Log("[DemoPrisonDoor] 감옥 문을 열었습니다.");

            if (doorMeshTransform != null)
            {
                StartCoroutine(DemoDoorRotation.RotateLocalY(doorMeshTransform, -90f, openSpeed));
            }

            dungeonController?.CompleteDungeon();
        }
    }
}
