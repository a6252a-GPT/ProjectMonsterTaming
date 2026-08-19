using System.Collections;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    public sealed class DemoDoor : MonoBehaviour
    {
        [SerializeField] private float openSpeed = 2f;
        [SerializeField] private float openAngle = -90f;

        private Transform doorPivot;
        private bool isOpen;

        public void Configure(Transform pivot)
        {
            doorPivot = pivot != null ? pivot : transform;
            EnsureTriggerVolume();
        }

        public void TryOpen(Collider other)
        {
            if (isOpen || !DemoPlayerDetector.IsPlayer(other, null))
            {
                return;
            }

            OpenDoor();
        }

        private void EnsureTriggerVolume()
        {
            Transform existing = doorPivot.Find("DoorTrigger");
            GameObject triggerObject = existing != null ? existing.gameObject : new GameObject("DoorTrigger");
            triggerObject.transform.SetParent(doorPivot, false);
            triggerObject.transform.localPosition = Vector3.zero;
            triggerObject.transform.localRotation = Quaternion.identity;

            BoxCollider trigger = triggerObject.GetComponent<BoxCollider>();
            if (trigger == null)
            {
                trigger = triggerObject.AddComponent<BoxCollider>();
            }

            trigger.isTrigger = true;
            trigger.size = GetTriggerSize();
            trigger.center = GetTriggerCenter();

            DemoDoorTrigger relay = triggerObject.GetComponent<DemoDoorTrigger>();
            if (relay == null)
            {
                relay = triggerObject.AddComponent<DemoDoorTrigger>();
            }

            relay.Bind(this);
        }

        private Vector3 GetTriggerSize()
        {
            Renderer renderer = doorPivot.GetComponent<Renderer>();
            if (renderer == null)
            {
                return new Vector3(2.5f, 2.5f, 2.5f);
            }

            Vector3 localSize = doorPivot.InverseTransformVector(renderer.bounds.size);
            localSize.x = Mathf.Max(Mathf.Abs(localSize.x), 1.5f);
            localSize.y = Mathf.Max(Mathf.Abs(localSize.y), 2f);
            localSize.z = Mathf.Max(Mathf.Abs(localSize.z), 1.5f);
            return localSize;
        }

        private Vector3 GetTriggerCenter()
        {
            Renderer renderer = doorPivot.GetComponent<Renderer>();
            if (renderer == null)
            {
                return Vector3.zero;
            }

            return doorPivot.InverseTransformPoint(renderer.bounds.center);
        }

        private void OpenDoor()
        {
            isOpen = true;
            StartCoroutine(DemoDoorRotation.RotateLocalY(doorPivot, openAngle, openSpeed));
            DisableBlockingColliders();
        }

        private void DisableBlockingColliders()
        {
            Collider[] colliders = doorPivot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || collider.isTrigger)
                {
                    continue;
                }

                collider.enabled = false;
            }
        }
    }

    internal sealed class DemoDoorTrigger : MonoBehaviour
    {
        private DemoDoor door;

        public void Bind(DemoDoor boundDoor)
        {
            door = boundDoor;
        }

        private void OnTriggerEnter(Collider other)
        {
            door?.TryOpen(other);
        }
    }
}
