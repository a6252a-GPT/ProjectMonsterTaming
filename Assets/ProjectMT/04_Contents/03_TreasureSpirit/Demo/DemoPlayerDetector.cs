using ProjectMT.Contents.TreasureSpirit;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class DemoPlayerDetector
    {
        public static bool IsPlayer(Collider other, Transform knownPlayer)
        {
            if (other == null)
            {
                return false;
            }

            if (knownPlayer != null &&
                (other.transform.IsChildOf(knownPlayer) || other.transform.root == knownPlayer))
            {
                return true;
            }

            return other.CompareTag("Player") ||
                   other.transform.root.CompareTag("Player") ||
                   other.GetComponentInParent<PlayerCharacterController>() != null ||
                   other.name.Contains("Commander") ||
                   other.GetComponent<DemoPlayerInteractSensor>() != null;
        }
    }

    public sealed class DemoPlayerInteractSensor : MonoBehaviour
    {
        public void Initialize(Transform player)
        {
        }

        private void OnTriggerEnter(Collider other)
        {
            DemoChestInteraction chest = other.GetComponent<DemoChestInteraction>();
            if (chest != null)
            {
                chest.TryInteractFromPlayer();
                return;
            }

            chest = other.GetComponentInParent<DemoChestInteraction>();
            chest?.TryInteractFromPlayer();
        }
    }
}
