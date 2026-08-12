using System;
using System.Collections;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattleMonsterDragController : MonoBehaviour // 메인전투 아군 직접 재배치
    {
        private const int NoPointerId = int.MinValue;
        private const int MousePointerId = -1;

        [Header("Hold")]
        [SerializeField, Min(0.1f)] private float liftHeight = 1.15f;
        [SerializeField, Min(1f)] private float followSharpness = 18f;
        [SerializeField, Min(0f)] private float pickPaddingPixels = 18f;

        [Header("Motion")]
        [SerializeField, Min(0.1f)] private float wiggleSpeed = 18f;
        [SerializeField, Range(0f, 35f)] private float wiggleAngle = 14f;
        [SerializeField, Min(0.05f)] private float dropDurationSeconds = 0.16f;

        private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

        private Camera worldCamera;
        private Collider groundCollider;
        private Func<bool> canInteract;
        private Func<UnitActor, bool> canSelectUnit;
        private Func<UnitActor, Vector3, bool> canDropAt;
        private Action<UnitActor, Vector3, bool> dragPreviewChanged;
        private Action<UnitActor, Vector3, bool> dragReleased;
        private Action dragCancelled;
        private bool configured;

        private int activePointerId = NoPointerId;
        private bool activePointerIsTouch;

        private UnitActor activeUnit;
        private Vector3 originalPosition;
        private Vector3 lastGroundPosition;
        private Quaternion restingRotation;
        private bool currentGroundValid;
        private Coroutine dropRoutine;

        public UnitActor HeldUnit => dropRoutine == null ? activeUnit : null;
        public bool IsHolding => HeldUnit != null && HeldUnit.IsManuallyHeld;
        public bool IsInteracting => activeUnit != null || dropRoutine != null;

        public void Configure(Camera camera, Collider ground, Func<bool> interactionGate)
        {
            Configure(camera, ground, interactionGate, null, null, null);
        }

        public void Configure(
            Camera camera,
            Collider ground,
            Func<bool> interactionGate,
            Action<UnitActor, Vector3, bool> previewChanged,
            Action<UnitActor, Vector3, bool> released,
            Action cancelled)
        {
            CancelInteraction();
            worldCamera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            groundCollider = ground != null ? ground : throw new ArgumentNullException(nameof(ground));
            canInteract = interactionGate;
            canSelectUnit = null;
            canDropAt = null;
            dragPreviewChanged = previewChanged;
            dragReleased = released;
            dragCancelled = cancelled;
            configured = true;
            enabled = true;
        }

        public void ConfigurePlacement(
            Camera camera,
            Collider ground,
            Func<bool> interactionGate,
            Func<UnitActor, bool> selectionGate,
            Func<UnitActor, Vector3, bool> dropValidator,
            Action<UnitActor, Vector3, bool> previewChanged,
            Action<UnitActor, Vector3, bool> released)
        {
            Configure(camera, ground, interactionGate);
            canSelectUnit = selectionGate;
            canDropAt = dropValidator;
            dragPreviewChanged = previewChanged;
            dragReleased = released;
        }

        public void Shutdown()
        {
            CancelInteraction();
            configured = false;
            canInteract = null;
            canSelectUnit = null;
            canDropAt = null;
            dragPreviewChanged = null;
            dragReleased = null;
            dragCancelled = null;
            worldCamera = null;
            groundCollider = null;
            enabled = false;
        }

        private void Update()
        {
            if (!configured)
            {
                return;
            }

            if (canInteract != null && !canInteract())
            {
                CancelInteraction();
                return;
            }

            if (dropRoutine != null)
            {
                return; // 착지 중에는 다음 몬스터를 잡지 않음
            }

            if (activePointerId == NoPointerId)
            {
                TryBeginPointer();
                return;
            }

            if (!TryReadActivePointer(out var screenPosition, out var pressed, out var released))
            {
                ReleasePointer(false);
                return;
            }

            if (!pressed || released)
            {
                ReleasePointer(currentGroundValid);
                return;
            }

            UpdateHeldUnit(screenPosition);
        }

        private void TryBeginPointer()
        {
            if (!TryReadNewPointer(out var pointerId, out var isTouch, out var screenPosition) ||
                IsPointerOverUi(pointerId, screenPosition))
            {
                return;
            }

            var unit = FindPlayerUnit(screenPosition);
            if (unit == null)
            {
                return;
            }

            activePointerId = pointerId;
            activePointerIsTouch = isTouch;
            currentGroundValid = false;
            BeginHold(unit, screenPosition); // 누른 즉시 들어 올리고 이후 이동을 따라감
            UpdateHeldUnit(screenPosition);
        }

        private bool TryReadNewPointer(out int pointerId, out bool isTouch, out Vector2 position)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    var phase = touch.phase.ReadValue();
                    if (!touch.press.wasPressedThisFrame && phase != InputTouchPhase.Began)
                    {
                        continue;
                    }

                    pointerId = touch.touchId.ReadValue();
                    isTouch = true;
                    position = touch.position.ReadValue();
                    return true;
                }
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                pointerId = MousePointerId;
                isTouch = false;
                position = mouse.position.ReadValue();
                return true;
            }

            pointerId = NoPointerId;
            isTouch = false;
            position = default;
            return false;
        }

        private bool TryReadActivePointer(out Vector2 position, out bool pressed, out bool released)
        {
            if (!activePointerIsTouch)
            {
                var mouse = Mouse.current;
                if (mouse == null)
                {
                    position = default;
                    pressed = false;
                    released = true;
                    return false;
                }

                position = mouse.position.ReadValue();
                pressed = mouse.leftButton.isPressed;
                released = mouse.leftButton.wasReleasedThisFrame;
                return true;
            }

            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (touch.touchId.ReadValue() != activePointerId)
                    {
                        continue;
                    }

                    position = touch.position.ReadValue();
                    var phase = touch.phase.ReadValue();
                    pressed = touch.press.isPressed || phase == InputTouchPhase.Began ||
                              phase == InputTouchPhase.Moved || phase == InputTouchPhase.Stationary;
                    released = touch.press.wasReleasedThisFrame ||
                               phase == InputTouchPhase.Ended || phase == InputTouchPhase.Canceled;
                    return true;
                }
            }

            position = default;
            pressed = false;
            released = true;
            return false;
        }

        private UnitActor FindPlayerUnit(Vector2 screenPosition)
        {
            var units = FindObjectsByType<UnitActor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var nearestDistance = float.PositiveInfinity;
            UnitActor nearest = null;
            foreach (var unit in units)
            {
                if (unit == null || !unit.IsAlive || unit.IsManuallyHeld || unit.Team != UnitTeam.Player ||
                    (canSelectUnit != null && !canSelectUnit(unit)) ||
                    !TryGetScreenRect(unit, out var screenRect) || !screenRect.Contains(screenPosition))
                {
                    continue;
                }

                var distance = ((Vector2)screenRect.center - screenPosition).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = unit;
                }
            }

            return nearest;
        }

        private bool TryGetScreenRect(UnitActor unit, out Rect screenRect)
        {
            var renderers = unit.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0)
            {
                screenRect = default;
                return false;
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }
            }

            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var center = bounds.center;
            var extents = bounds.extents;
            for (var corner = 0; corner < 8; corner++)
            {
                var worldPoint = center + new Vector3(
                    (corner & 1) == 0 ? -extents.x : extents.x,
                    (corner & 2) == 0 ? -extents.y : extents.y,
                    (corner & 4) == 0 ? -extents.z : extents.z);
                var screenPoint = worldCamera.WorldToScreenPoint(worldPoint);
                if (screenPoint.z <= 0f)
                {
                    screenRect = default;
                    return false;
                }

                min = Vector2.Min(min, screenPoint);
                max = Vector2.Max(max, screenPoint);
            }

            var padding = Mathf.Max(pickPaddingPixels, Mathf.Min(Screen.width, Screen.height) * 0.012f);
            screenRect = Rect.MinMaxRect(min.x - padding, min.y - padding, max.x + padding, max.y + padding);
            return true;
        }

        private void BeginHold(UnitActor unit, Vector2 screenPosition)
        {
            if (unit == null || !unit.BeginManualReposition())
            {
                ResetPointerState();
                return;
            }

            activeUnit = unit;
            originalPosition = unit.transform.position;
            lastGroundPosition = originalPosition;
            restingRotation = unit.transform.rotation;
            activeUnit.Died += HandleActiveUnitDied;
            var projected = TryProjectGround(screenPosition, out var groundPosition);
            if (projected)
            {
                groundPosition.y = originalPosition.y;
                lastGroundPosition = groundPosition;
            }

            currentGroundValid = projected && (canDropAt == null || canDropAt(activeUnit, lastGroundPosition));
            dragPreviewChanged?.Invoke(activeUnit, lastGroundPosition, currentGroundValid);
        }

        private void UpdateHeldUnit(Vector2 screenPosition)
        {
            if (activeUnit == null || !activeUnit.IsAlive)
            {
                return;
            }

            var projected = TryProjectGround(screenPosition, out var groundPosition);
            if (projected)
            {
                groundPosition.y = originalPosition.y;
                lastGroundPosition = groundPosition;
            }

            currentGroundValid = projected && (canDropAt == null || canDropAt(activeUnit, lastGroundPosition));
            dragPreviewChanged?.Invoke(activeUnit, lastGroundPosition, currentGroundValid);

            var phase = Time.unscaledTime * wiggleSpeed;
            var bob = Mathf.Abs(Mathf.Sin(phase * 0.5f)) * 0.08f;
            var liftedPosition = lastGroundPosition + Vector3.up * (liftHeight + bob);
            var follow = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
            activeUnit.transform.position = Vector3.Lerp(activeUnit.transform.position, liftedPosition, follow);
            activeUnit.transform.rotation = restingRotation * Quaternion.Euler(
                Mathf.Sin(phase * 1.31f) * wiggleAngle,
                Mathf.Sin(phase * 0.73f) * wiggleAngle * 0.45f,
                Mathf.Cos(phase) * wiggleAngle); // 목덜미를 잡힌 듯한 가벼운 파닥임
        }

        private bool TryProjectGround(Vector2 screenPosition, out Vector3 groundPosition)
        {
            var ray = worldCamera.ScreenPointToRay(screenPosition);
            if (groundCollider.Raycast(ray, out var hit, 1000f))
            {
                groundPosition = hit.point;
                return true;
            }

            groundPosition = default;
            return false;
        }

        private void ReleasePointer(bool validGround)
        {
            if (activeUnit == null)
            {
                ResetPointerState();
                return;
            }

            var unit = activeUnit;
            var target = validGround ? lastGroundPosition : originalPosition;
            dragReleased?.Invoke(unit, lastGroundPosition, validGround);
            ResetPointerState();
            BeginDrop(target);
        }

        private void BeginDrop(Vector3 target)
        {
            if (activeUnit == null || dropRoutine != null)
            {
                return;
            }

            dropRoutine = StartCoroutine(DropRoutine(activeUnit, target));
        }

        private IEnumerator DropRoutine(UnitActor unit, Vector3 target)
        {
            var start = unit == null ? target : unit.transform.position;
            var startRotation = unit == null ? restingRotation : unit.transform.rotation;
            var duration = Mathf.Max(0.05f, dropDurationSeconds);
            var elapsed = 0f;
            while (elapsed < duration && unit != null && unit.gameObject.activeInHierarchy)
            {
                elapsed += Time.unscaledDeltaTime;
                var ratio = Mathf.Clamp01(elapsed / duration);
                var fall = ratio * ratio;
                var bounce = Mathf.Sin(ratio * Mathf.PI) * (1f - ratio) * 0.07f;
                unit.transform.position = Vector3.Lerp(start, target, fall) + Vector3.up * bounce;
                unit.transform.rotation = Quaternion.Slerp(startRotation, restingRotation, fall);
                yield return null;
            }

            if (unit != null && unit.gameObject.activeInHierarchy)
            {
                unit.transform.position = target;
                unit.transform.rotation = restingRotation;
            }

            FinishActiveUnit(unit);
        }

        private void HandleActiveUnitDied(UnitActor unit)
        {
            if (unit == null || unit != activeUnit)
            {
                return;
            }

            ResetPointerState();
            if (dropRoutine == null)
            {
                dragCancelled?.Invoke();
                BeginDrop(lastGroundPosition); // 잡힌 채 사망하면 땅으로 놓고 기존 사망 수명 유지
            }
        }

        private void FinishActiveUnit(UnitActor unit)
        {
            if (unit != null)
            {
                unit.Died -= HandleActiveUnitDied;
                unit.EndManualReposition();
            }

            if (unit == activeUnit)
            {
                activeUnit = null;
            }

            dropRoutine = null;
        }

        private bool IsPointerOverUi(int pointerId, Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            var eventData = new PointerEventData(eventSystem)
            {
                pointerId = pointerId,
                position = screenPosition
            };
            uiRaycastResults.Clear();
            eventSystem.RaycastAll(eventData, uiRaycastResults);
            return uiRaycastResults.Count > 0;
        }

        private void ResetPointerState()
        {
            activePointerId = NoPointerId;
            activePointerIsTouch = false;
            currentGroundValid = false;
        }

        private void CancelInteraction()
        {
            dragCancelled?.Invoke();
            ResetPointerState();
            if (dropRoutine != null)
            {
                StopCoroutine(dropRoutine);
                dropRoutine = null;
            }

            if (activeUnit != null)
            {
                activeUnit.Died -= HandleActiveUnitDied;
                if (activeUnit.IsAlive)
                {
                    activeUnit.transform.position = originalPosition;
                    activeUnit.transform.rotation = restingRotation;
                }

                activeUnit.EndManualReposition();
                activeUnit = null;
            }

        }

        public void CancelCurrentInteraction()
        {
            CancelInteraction();
        }

        private void OnDisable()
        {
            CancelInteraction();
        }

        private void OnDestroy()
        {
            CancelInteraction();
        }
    }
}
