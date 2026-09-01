using System.Collections;
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
                DemoDungeonAudio.PlayLockFail(transform.position);
            }
        }

        private void OpenDoor()
        {
            isOpened = true;
            DemoDungeonAudio.PlayPrisonDoor(transform.position);

            if (doorMeshTransform != null)
            {
                StartCoroutine(DemoDoorRotation.RotateLocalY(doorMeshTransform, -90f, openSpeed));
            }

            dungeonController?.CompleteDungeon();
        }
    }
}
