using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    [RequireComponent(typeof(Collider))]
    public sealed class FogAreaTrigger : MonoBehaviour
    {
        [SerializeField] private FogArea area;
        private Transform player;

        public void Initialize(FogArea fogArea, Transform playerTransform)
        {
            area = fogArea;
            player = playerTransform;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayerBody(other) || area == null)
            {
                return;
            }

            FogOfWarManager.Instance?.NotifyEntered(area);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayerBody(other) || area == null)
            {
                return;
            }

            FogOfWarManager.Instance?.NotifyExited(area);
        }

        private bool IsPlayerBody(Collider other)
        {
            if (other == null || other.isTrigger)
            {
                return false;
            }

            return DemoPlayerDetector.IsPlayer(other, player);
        }
    }
}
