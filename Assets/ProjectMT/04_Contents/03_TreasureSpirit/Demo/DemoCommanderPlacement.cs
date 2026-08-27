using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class DemoCommanderPlacement
    {
        private const float CommanderColliderHeight = 1.4f;

        public static void PlaceOnSurface(Transform commander, Vector3 surfacePoint, float feetHeightOffset)
        {
            if (commander == null)
            {
                return;
            }

            CharacterController controller = commander.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
                ApplyControllerCenter(controller);
            }

            Vector3 position = surfacePoint;
            position.y += feetHeightOffset;
            commander.SetPositionAndRotation(position, Quaternion.identity);

            if (controller != null)
            {
                controller.enabled = true;
            }

            EnsureInteractSensor(commander);
        }

        private static void EnsureInteractSensor(Transform commander)
        {
            Transform existing = commander.Find("InteractSensor");
            GameObject sensorObject = existing != null ? existing.gameObject : new GameObject("InteractSensor");
            sensorObject.transform.SetParent(commander, false);
            sensorObject.transform.localPosition = new Vector3(0f, 0.7f, 0f);

            SphereCollider sensorCollider = sensorObject.GetComponent<SphereCollider>();
            if (sensorCollider == null)
            {
                sensorCollider = sensorObject.AddComponent<SphereCollider>();
            }

            sensorCollider.isTrigger = true;
            sensorCollider.radius = 0.4f;

            Rigidbody sensorBody = sensorObject.GetComponent<Rigidbody>();
            if (sensorBody == null)
            {
                sensorBody = sensorObject.AddComponent<Rigidbody>();
            }

            sensorBody.isKinematic = true;
            sensorBody.useGravity = false;

            DemoPlayerInteractSensor sensor = sensorObject.GetComponent<DemoPlayerInteractSensor>();
            if (sensor == null)
            {
                sensor = sensorObject.AddComponent<DemoPlayerInteractSensor>();
            }

            sensor.Initialize(commander);
        }

        public static void ApplyControllerCenter(CharacterController controller)
        {
            if (controller == null)
            {
                return;
            }

            controller.height = CommanderColliderHeight;
            float halfHeight = controller.height * 0.5f;
            controller.center = new Vector3(0f, halfHeight, 0f);
            controller.stepOffset = Mathf.Min(controller.stepOffset, halfHeight);
        }
    }
}
