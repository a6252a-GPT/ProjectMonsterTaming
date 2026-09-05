using System.Collections;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattleDragIndicatorPresenter : MonoBehaviour // 선택 유닛과 이동 목적지 표시
    {
        private const float GroundOffset = 0.04f;
        private const float ReleasePulseSeconds = 0.16f;

        private static readonly Color ValidColor = new Color(0.18f, 0.95f, 0.68f, 0.98f);
        private static readonly Color InvalidColor = new Color(1f, 0.22f, 0.18f, 0.98f);

        private Camera worldCamera;
        private UnitActor selectedUnit;
        private Renderer[] selectedRenderers;
        private GameObject visualRoot;
        private LineRenderer selectionMarker;
        private LineRenderer destinationRing;
        [SerializeField] private MainBattleDragIndicatorView visualPrefab;
        private Coroutine releasePulse;

        public bool IsShowingSelection => selectionMarker != null && selectionMarker.enabled;
        public bool IsShowingDestination => destinationRing != null && destinationRing.enabled;

        public void Configure(Camera camera)
        {
            worldCamera = camera;
            EnsureVisuals();
            HideImmediate();
        }

        public void ShowPreview(UnitActor unit, Vector3 worldPosition, bool valid)
        {
            if (unit == null)
            {
                HideImmediate();
                return;
            }

            EnsureVisuals();
            StopReleasePulse();
            if (selectedUnit != unit)
            {
                selectedUnit = unit;
                selectedRenderers = unit.GetComponentsInChildren<Renderer>(false);
            }

            selectionMarker.enabled = true;
            destinationRing.enabled = true;
            destinationRing.transform.localScale = Vector3.one;
            SetDestinationPosition(worldPosition);
            SetLineColor(destinationRing, valid ? ValidColor : InvalidColor);
            UpdateSelectionMarker();
        }

        public void ShowRelease(UnitActor unit, Vector3 worldPosition, bool valid)
        {
            EnsureVisuals();
            selectedUnit = null;
            selectedRenderers = null;
            selectionMarker.enabled = false;
            destinationRing.enabled = true;
            destinationRing.transform.localScale = Vector3.one;
            SetDestinationPosition(worldPosition);
            SetLineColor(destinationRing, valid ? ValidColor : InvalidColor);
            StopReleasePulse();
            releasePulse = StartCoroutine(ReleasePulseRoutine(valid ? ValidColor : InvalidColor));
        }

        public void HideImmediate()
        {
            StopReleasePulse();
            selectedUnit = null;
            selectedRenderers = null;
            if (selectionMarker != null)
            {
                selectionMarker.enabled = false;
            }

            if (destinationRing != null)
            {
                destinationRing.enabled = false;
                destinationRing.transform.localScale = Vector3.one;
            }
        }

        public void Shutdown()
        {
            HideImmediate();
            worldCamera = null;
        }

        private void LateUpdate()
        {
            if (selectedUnit == null || !selectedUnit.IsAlive)
            {
                if (selectionMarker != null)
                {
                    selectionMarker.enabled = false;
                }

                selectedUnit = null;
                selectedRenderers = null;
                return;
            }

            UpdateSelectionMarker();
        }

        private void EnsureVisuals()
        {
            if (visualRoot != null) return;
            if (visualPrefab == null) throw new System.InvalidOperationException("The drag indicator prefab is required.");
            var view = Instantiate(visualPrefab, transform, false);
            visualRoot = view.gameObject;
            visualRoot.name = "MainBattleDragIndicators";
            selectionMarker = view.SelectionMarker;
            destinationRing = view.DestinationRing;
        }

        private void UpdateSelectionMarker()
        {
            if (selectionMarker == null || selectedUnit == null)
            {
                return;
            }

            var position = selectedUnit.transform.position + Vector3.up * 0.75f;
            if (selectedRenderers != null && selectedRenderers.Length > 0)
            {
                var hasBounds = false;
                var bounds = default(Bounds);
                for (var index = 0; index < selectedRenderers.Length; index++)
                {
                    var renderer = selectedRenderers[index];
                    if (renderer == null || !renderer.enabled)
                    {
                        continue;
                    }

                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }

                if (hasBounds)
                {
                    position = new Vector3(bounds.center.x, bounds.max.y + 0.34f, bounds.center.z);
                }
            }

            selectionMarker.transform.position = position;
            if (worldCamera != null)
            {
                selectionMarker.transform.rotation = Quaternion.LookRotation(
                    worldCamera.transform.forward,
                    worldCamera.transform.up);
            }
        }

        private void SetDestinationPosition(Vector3 worldPosition)
        {
            worldPosition.y += GroundOffset;
            destinationRing.transform.position = worldPosition;
            destinationRing.transform.rotation = Quaternion.identity;
        }

        private IEnumerator ReleasePulseRoutine(Color baseColor)
        {
            var elapsed = 0f;
            while (elapsed < ReleasePulseSeconds && destinationRing != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var ratio = Mathf.Clamp01(elapsed / ReleasePulseSeconds);
                destinationRing.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.24f, ratio);
                var color = baseColor;
                color.a = Mathf.Lerp(baseColor.a, 0f, ratio);
                SetLineColor(destinationRing, color);
                yield return null;
            }

            if (destinationRing != null)
            {
                destinationRing.enabled = false;
                destinationRing.transform.localScale = Vector3.one;
                SetLineColor(destinationRing, baseColor);
            }

            releasePulse = null;
        }

        private void StopReleasePulse()
        {
            if (releasePulse == null)
            {
                return;
            }

            StopCoroutine(releasePulse);
            releasePulse = null;
        }

        private static void SetLineColor(LineRenderer line, Color color)
        {
            if (line == null)
            {
                return;
            }

            line.startColor = color;
            line.endColor = color;
        }

        private void OnDisable()
        {
            HideImmediate();
        }

        private void OnDestroy()
        {
            StopReleasePulse();
        }
    }
}
