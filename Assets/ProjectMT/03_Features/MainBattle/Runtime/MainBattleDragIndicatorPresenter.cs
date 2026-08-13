using System.Collections;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattleDragIndicatorPresenter : MonoBehaviour // 선택 유닛과 이동 목적지 표시
    {
        private const int RingSegmentCount = 64;
        private const float GroundOffset = 0.04f;
        private const float ReleasePulseSeconds = 0.16f;

        private static readonly Color SelectionColor = new Color(0.30f, 1f, 0.84f, 0.98f);
        private static readonly Color ValidColor = new Color(0.18f, 0.95f, 0.68f, 0.98f);
        private static readonly Color InvalidColor = new Color(1f, 0.22f, 0.18f, 0.98f);

        private Camera worldCamera;
        private UnitActor selectedUnit;
        private Renderer[] selectedRenderers;
        private GameObject visualRoot;
        private LineRenderer selectionMarker;
        private LineRenderer destinationRing;
        private Material lineMaterial;
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
            if (visualRoot != null)
            {
                return;
            }

            visualRoot = new GameObject("MainBattleDragIndicators");
            visualRoot.transform.SetParent(transform, false);
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
            lineMaterial = new Material(shader)
            {
                name = "Runtime_MainBattleDragIndicator",
                color = Color.white,
                hideFlags = HideFlags.DontSave
            };

            selectionMarker = CreateLineRenderer("SelectedMonsterMarker", 0.055f);
            selectionMarker.loop = true;
            selectionMarker.useWorldSpace = false;
            selectionMarker.positionCount = 4;
            selectionMarker.SetPositions(new[]
            {
                new Vector3(0f, 0.32f, 0f),
                new Vector3(0.22f, 0f, 0f),
                new Vector3(0f, -0.32f, 0f),
                new Vector3(-0.22f, 0f, 0f)
            });
            SetLineColor(selectionMarker, SelectionColor);

            destinationRing = CreateLineRenderer("DragDestinationRing", 0.052f);
            destinationRing.loop = true;
            destinationRing.useWorldSpace = false;
            destinationRing.positionCount = RingSegmentCount;
            for (var index = 0; index < RingSegmentCount; index++)
            {
                var angle = index * Mathf.PI * 2f / RingSegmentCount;
                destinationRing.SetPosition(index, new Vector3(
                    Mathf.Cos(angle) * MainBattleFormationRules.UnitRadius,
                    0f,
                    Mathf.Sin(angle) * MainBattleFormationRules.UnitRadius));
            }

            selectionMarker.enabled = false;
            destinationRing.enabled = false;
        }

        private LineRenderer CreateLineRenderer(string objectName, float width)
        {
            var lineObject = new GameObject(objectName, typeof(LineRenderer));
            lineObject.transform.SetParent(visualRoot.transform, false);
            var line = lineObject.GetComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.startWidth = width;
            line.endWidth = width;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.textureMode = LineTextureMode.Stretch;
            return line;
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
            if (lineMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(lineMaterial);
                }
                else
                {
                    DestroyImmediate(lineMaterial);
                }
            }
        }
    }
}
