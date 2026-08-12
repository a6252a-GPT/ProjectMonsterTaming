using System;
using UnityEngine;

namespace ProjectMT.Tools.StageMapSlicer
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class StageVegetationDistanceCuller : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float cullDistance = 28f;
        [SerializeField, Min(0.05f)] private float updateInterval = 0.2f;
        [SerializeField] private Transform[] cells = Array.Empty<Transform>();

        private Camera targetCamera;
        private float nextUpdateTime;

        public int CellCount => cells?.Length ?? 0;

        public void Configure(float distance, Transform[] targets)
        {
            cullDistance = Mathf.Max(1f, distance);
            cells = targets ?? Array.Empty<Transform>();
        }

        private void OnEnable()
        {
            SetAllCellsActive(true);
            targetCamera = null;
            nextUpdateTime = 0f;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextUpdateTime)
            {
                return;
            }

            nextUpdateTime = Time.unscaledTime + Mathf.Max(0.05f, updateInterval);
            if (!TryResolveCamera(out Camera camera))
            {
                SetAllCellsActive(true);
                return;
            }

            Vector3 cameraPosition = camera.transform.position;
            float cullDistanceSquared = cullDistance * cullDistance;
            foreach (Transform cell in cells)
            {
                if (cell == null)
                {
                    continue;
                }

                Vector3 offset = cell.position - cameraPosition;
                offset.y = 0f;
                bool visible = offset.sqrMagnitude <= cullDistanceSquared;
                if (cell.gameObject.activeSelf != visible)
                {
                    cell.gameObject.SetActive(visible);
                }
            }
        }

        private void OnDisable()
        {
            SetAllCellsActive(true);
        }

        private bool TryResolveCamera(out Camera camera)
        {
            if (targetCamera == null || !targetCamera.isActiveAndEnabled)
            {
                targetCamera = Camera.main;
            }

            camera = targetCamera;
            return camera != null;
        }

        private void SetAllCellsActive(bool active)
        {
            foreach (Transform cell in cells)
            {
                if (cell != null && cell.gameObject.activeSelf != active)
                {
                    cell.gameObject.SetActive(active);
                }
            }
        }
    }
}
