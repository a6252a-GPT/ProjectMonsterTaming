using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    internal sealed class FallenCommanderTimeoutBossPositionLock : MonoBehaviour
    {
        private Vector3 lockedPosition;

        public void SetPosition(Vector3 position)
        {
            lockedPosition = position;
            transform.position = lockedPosition;
        }

        private void LateUpdate()
        {
            transform.position = lockedPosition;
        }
    }
}
