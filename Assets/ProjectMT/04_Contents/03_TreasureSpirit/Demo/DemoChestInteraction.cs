using System;
using UnityEngine;
using UnityEngine.AI;
using ProjectMT.Contents.TreasureSpirit;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    public sealed class DemoChestInteraction : MonoBehaviour
    {
        public enum ChestKind
        {
            Key,
            Mimic
        }

        [SerializeField] private float interactRadius = 1.75f;

        private ChestKind chestKind;
        private Transform playerTransform;
        private BakedDungeonLoader keyState;
        private GameObject mimicPrefab;
        private bool interacted;

        public void SetupKeyChest(Transform player, BakedDungeonLoader loader)
        {
            chestKind = ChestKind.Key;
            playerTransform = player;
            keyState = loader;
            DemoChestInteractionSetup.Ensure(gameObject);
        }

        public void SetupMimicChest(Transform player, GameObject mimic)
        {
            chestKind = ChestKind.Mimic;
            playerTransform = player;
            mimicPrefab = mimic;
            DemoChestInteractionSetup.Ensure(gameObject);
        }

        public void TryInteractFromPlayer()
        {
            if (!CanInteract())
            {
                return;
            }

            Interact();
        }

        private void Update()
        {
            if (!CanInteract() || playerTransform == null)
            {
                return;
            }

            Vector3 interactionPoint = DemoChestInteractionSetup.GetInteractionPoint(transform);
            float distance = Vector3.Distance(interactionPoint, playerTransform.position);
            if (distance <= interactRadius)
            {
                Interact();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!CanInteract() || !DemoPlayerDetector.IsPlayer(other, playerTransform))
            {
                return;
            }

            Interact();
        }

        private bool CanInteract()
        {
            return !interacted;
        }

        private void Interact()
        {
            interacted = true;

            switch (chestKind)
            {
                case ChestKind.Key:
                    keyState?.GrantKey();
                    Debug.Log("[DemoChestInteraction] 열쇠 상자를 열었습니다.");
                    Destroy(gameObject);
                    break;

                case ChestKind.Mimic:
                    SpawnMimic();
                    break;
            }
        }

        private void SpawnMimic()
        {
            if (mimicPrefab == null)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 spawnPosition = transform.position;
            Quaternion spawnRotation = transform.rotation;

            if (playerTransform != null)
            {
                Vector3 lookDirection = playerTransform.position - spawnPosition;
                lookDirection.y = 0f;
                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    spawnRotation = Quaternion.LookRotation(lookDirection);
                }
            }

            Transform parent = transform.parent;
            Destroy(gameObject);

            GameObject mimicObject = Instantiate(mimicPrefab, spawnPosition, spawnRotation, parent);
            mimicObject.name = mimicObject.name.Replace("(Clone)", "_Runtime");

            NavMeshAgent agent = mimicObject.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.autoTraverseOffMeshLink = false;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                agent.Warp(spawnPosition);
            }

            DemoMimicAI mimicAi = mimicObject.GetComponent<DemoMimicAI>();
            if (mimicAi == null)
            {
                mimicAi = mimicObject.AddComponent<DemoMimicAI>();
            }

            mimicAi.Initialize(playerTransform);

            Debug.Log("[DemoChestInteraction] 상자가 미믹으로 변신했습니다.");
        }
    }

    internal static class DemoChestInteractionSetup
    {
        public static void Ensure(GameObject chestObject)
        {
            Rigidbody rigidbody = chestObject.GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                rigidbody = chestObject.AddComponent<Rigidbody>();
            }

            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            Collider collider = chestObject.GetComponent<Collider>();
            if (collider == null)
            {
                collider = chestObject.GetComponentInChildren<Collider>();
            }

            if (collider == null)
            {
                BoxCollider boxCollider = chestObject.AddComponent<BoxCollider>();
                boxCollider.isTrigger = true;
                boxCollider.size = new Vector3(1.8f, 1.8f, 1.8f);
                boxCollider.center = new Vector3(0f, 0.9f, 0f);
            }
            else
            {
                collider.isTrigger = true;
            }
        }

        public static Vector3 GetInteractionPoint(Transform chestTransform)
        {
            Collider collider = chestTransform.GetComponent<Collider>();
            if (collider == null)
            {
                collider = chestTransform.GetComponentInChildren<Collider>();
            }

            return collider != null ? collider.bounds.center : chestTransform.position;
        }
    }
}
