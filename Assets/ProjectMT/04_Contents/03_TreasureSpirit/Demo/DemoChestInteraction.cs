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

        [SerializeField] private float interactRadius = 0.55f;

        private ChestKind chestKind;
        private Transform playerTransform;
        private BakedDungeonLoader keyState;
        private GameObject mimicPrefab;
        private float difficultyMultiplier = 1f;
        private bool interacted;

        public void SetupKeyChest(Transform player, BakedDungeonLoader loader)
        {
            chestKind = ChestKind.Key;
            playerTransform = player;
            keyState = loader;
            DemoChestInteractionSetup.Ensure(gameObject);
        }

        public void SetupMimicChest(Transform player, GameObject mimic, float difficulty = 1f)
        {
            chestKind = ChestKind.Mimic;
            playerTransform = player;
            mimicPrefab = mimic;
            difficultyMultiplier = Mathf.Max(1f, difficulty);
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
            Vector3 toPlayer = playerTransform.position - interactionPoint;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude <= interactRadius * interactRadius)
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
            return !interacted && !DemoChestQuizOverlay.IsOpen;
        }

        private void Interact()
        {
            interacted = true;

            switch (chestKind)
            {
                case ChestKind.Key:
                    OpenKeyQuiz();
                    break;

                case ChestKind.Mimic:
                    SpawnMimic();
                    break;
            }
        }

        private void OpenKeyQuiz()
        {
            DemoDungeonAudio.PlayChestOpen(transform.position);
            DemoDungeonDifficulty difficulty = DemoDungeonDifficultyUtil.Resolve(
                keyState != null ? keyState.ActiveMapInstance : null);
            DemoChestQuizOverlay.Show(difficulty, playerTransform, OnKeyQuizSolved, OnKeyQuizClosed);
        }

        private void OnKeyQuizSolved()
        {
            keyState?.GrantKey();
            if (this != null)
            {
                Destroy(gameObject);
            }
        }

        private void OnKeyQuizClosed()
        {
            if (this != null)
            {
                interacted = false;
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
            DemoUrpParticleRemapper.Remap(mimicObject);

            NavMeshAgent agent = mimicObject.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.autoTraverseOffMeshLink = false;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                agent.Warp(spawnPosition);
            }

            DemoDungeonAudio.PlayChestOpen(spawnPosition);
            DemoDungeonAudio.PlayMimic(spawnPosition);

            DemoMimicAI mimicAi = mimicObject.GetComponent<DemoMimicAI>();
            if (mimicAi == null)
            {
                mimicAi = mimicObject.AddComponent<DemoMimicAI>();
            }

            mimicAi.Initialize(playerTransform, difficultyMultiplier);
        }
    }

    internal static class DemoChestInteractionSetup
    {
        private const float TriggerSizeXz = 0.62f;
        private const float TriggerHeight = 0.9f;

        public static void Ensure(GameObject chestObject)
        {
            Rigidbody rigidbody = chestObject.GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                rigidbody = chestObject.AddComponent<Rigidbody>();
            }

            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            Collider[] colliders = chestObject.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }

            BoxCollider boxCollider = chestObject.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = chestObject.AddComponent<BoxCollider>();
            }

            Vector3 lossyScale = chestObject.transform.lossyScale;
            float scaleX = Mathf.Max(0.01f, Mathf.Abs(lossyScale.x));
            float scaleY = Mathf.Max(0.01f, Mathf.Abs(lossyScale.y));
            float scaleZ = Mathf.Max(0.01f, Mathf.Abs(lossyScale.z));

            boxCollider.enabled = true;
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(TriggerSizeXz / scaleX, TriggerHeight / scaleY, TriggerSizeXz / scaleZ);
            boxCollider.center = new Vector3(0f, (TriggerHeight * 0.5f) / scaleY, 0f);
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
