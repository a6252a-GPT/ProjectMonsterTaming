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
            DisableBlockingColliders();
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
            return new Vector3(2.4f, 2.6f, 2.4f);
        }

        private Vector3 GetTriggerCenter()
        {
            return new Vector3(0f, 1.2f, 0f);
        }

        private void OpenDoor()
        {
            isOpen = true;
            DemoDungeonAudio.PlayDoor(doorPivot != null ? doorPivot.position : transform.position);
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

    internal static class DemoDoorRotation
    {
        public static IEnumerator RotateLocalY(Transform pivot, float openAngle, float openSpeed)
        {
            if (pivot == null)
            {
                yield break;
            }

            Quaternion startRotation = pivot.localRotation;
            Quaternion targetRotation = startRotation * Quaternion.Euler(0f, openAngle, 0f);

            float progress = 0f;
            while (progress < 1f)
            {
                progress += Time.deltaTime * openSpeed;
                pivot.localRotation = Quaternion.Slerp(startRotation, targetRotation, progress);
                yield return null;
            }

            pivot.localRotation = targetRotation;
        }
    }
}
