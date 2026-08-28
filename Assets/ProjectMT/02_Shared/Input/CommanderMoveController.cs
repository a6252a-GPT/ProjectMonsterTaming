using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMT.Shared.Input
{
    public readonly struct MoveCommand // 플랫폼 공통 이동 명령
    {
        public MoveCommand(Vector2 direction)
        {
            Direction = Vector2.ClampMagnitude(direction, 1f); // 대각선 속도 제한
        }

        public Vector2 Direction { get; }
    }

    [DisallowMultipleComponent]
    public sealed class CommanderMoveController : MonoBehaviour // 군단장 직접 이동 처리
    {
        [SerializeField] private Transform controlled; // 실제 이동 대상
        [SerializeField] private SeedVirtualJoystick virtualJoystick; // 모바일 입력
        [SerializeField] private Transform movementReference; // 화면 기준 이동 축
        [SerializeField, Min(0f)] private float moveSpeed = 4f; // 초당 이동 거리
        [SerializeField] private Vector2 worldHalfExtents = new Vector2(6f, 4f); // 이동 가능 반경
        [SerializeField, Min(0f)] private float evadeDistance = 2.4f; // 회피 이동 거리
        [SerializeField, Min(0.01f)] private float evadeDuration = 0.18f; // 회피 이동 시간

        private Vector3 center; // 현재 콘텐츠 이동 중심
        private bool inputEnabled; // 플레이 중 입력 허용
        private Coroutine evadeRoutine; // 진행 중인 회피 이동

        public MoveCommand CurrentCommand { get; private set; }
        public Vector3 InitialPosition { get; private set; } // 08.07 안건준 추가 - 처음 활성화된 위치(리스폰 기준점)
        public bool IsInputEnabled => inputEnabled && isActiveAndEnabled && controlled != null;
        public bool IsEvading { get; private set; }

        private void Awake()
        {
            if (controlled == null)
            {
                controlled = transform;
            }

            center = controlled.position;
            InitialPosition = controlled.position; // 08.07 안건준 추가 - 콘텐츠 재시작 시 되돌아갈 최초 위치 저장
        }

        private void Update()
        {
            if (!inputEnabled || controlled == null)
            {
                CurrentCommand = new MoveCommand(Vector2.zero);
                return;
            }

            var direction = ReadDirection();
            CurrentCommand = new MoveCommand(direction);
            if (IsEvading)
            {
                return; // 회피 중에는 일반 이동을 겹치지 않음
            }

            var movement = ResolveWorldMovement(CurrentCommand.Direction);
            var next = controlled.position + movement * (moveSpeed * Time.deltaTime);
            next.x = Mathf.Clamp(next.x, center.x - worldHalfExtents.x, center.x + worldHalfExtents.x);
            next.z = Mathf.Clamp(next.z, center.z - worldHalfExtents.y, center.z + worldHalfExtents.y);
            controlled.position = next; // 콘텐츠 경계 안으로 제한

            if (movement.sqrMagnitude > 0.001f)
            {
                controlled.rotation = Quaternion.Slerp(
                    controlled.rotation,
                    Quaternion.LookRotation(movement.normalized, Vector3.up),
                    14f * Time.deltaTime);
            }
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled)
            {
                StopEvade();
                CurrentCommand = new MoveCommand(Vector2.zero);
            }

            if (enabled && controlled != null)
            {
                center = controlled.position; // 실행 시작점을 새 중심으로 사용
            }
        }

        public bool TryEvade()
        {
            if (!IsInputEnabled || IsEvading)
            {
                return false;
            }

            var inputDirection = ReadDirection();
            if (inputDirection.sqrMagnitude < 0.001f)
            {
                inputDirection = CurrentCommand.Direction;
            }

            var evadeDirection = inputDirection.sqrMagnitude >= 0.001f
                ? ResolveWorldMovement(inputDirection).normalized
                : GetBackwardDirection();
            var start = controlled.position;
            var destination = ClampToBounds(start + evadeDirection * evadeDistance);
            if ((destination - start).sqrMagnitude < 0.0001f)
            {
                return false; // 경계에 완전히 막힌 방향은 실행하지 않음
            }

            evadeRoutine = StartCoroutine(Evade(start, destination));
            return true;
        }

        // 08.07 안건준 추가 - 이동 가능 범위를 (군단장 시작 위치가 아닌) 외부에서 직접 지정.
        // 콘텐츠마다 바닥 크기가 달라 시작 위치 기준 반경만으로는 맞지 않는 경우(예: 수호자의 탑) 사용.
        // 기본 동작(SetInputEnabled가 시작 위치를 중심으로 쓰는 것)은 그대로 유지되며, 이 메서드를 호출하는
        // 콘텐츠에만 영향을 준다.
        public void SetMovementBounds(Vector3 worldCenter, Vector2 halfExtents)
        {
            center = worldCenter;
            worldHalfExtents = halfExtents;
        }

        // 08.07 안건준 추가 - 콘텐츠를 나갔다가 다시 시작할 때 항상 처음 위치에서 시작하도록 되돌린다.
        // Hosted 콘텐츠(GrowthDungeonHost)는 인스턴스를 파괴하지 않고 재사용하므로, 그대로 두면
        // 이전에 이동했던 위치에서 이어서 시작해버린다. 아무도 호출하지 않으면 기존 동작에 영향이 없다.
        public void ResetToInitialPosition()
        {
            StopEvade();
            if (controlled != null)
            {
                controlled.position = InitialPosition;
            }
        }

        private IEnumerator Evade(Vector3 start, Vector3 destination)
        {
            IsEvading = true;
            var elapsed = 0f;
            while (elapsed < evadeDuration && inputEnabled && controlled != null)
            {
                elapsed += Time.deltaTime;
                var normalizedTime = Mathf.Clamp01(elapsed / evadeDuration);
                var easedTime = 1f - Mathf.Pow(1f - normalizedTime, 3f); // 빠르게 출발해 자연스럽게 감속
                controlled.position = Vector3.LerpUnclamped(start, destination, easedTime);
                yield return null;
            }

            if (inputEnabled && controlled != null)
            {
                controlled.position = destination;
            }

            IsEvading = false;
            evadeRoutine = null;
        }

        private Vector3 GetBackwardDirection()
        {
            var backward = -controlled.forward;
            backward.y = 0f;
            return backward.sqrMagnitude > 0.001f ? backward.normalized : Vector3.back;
        }

        private Vector3 ResolveWorldMovement(Vector2 direction)
        {
            if (movementReference == null)
            {
                return new Vector3(direction.x, 0f, direction.y);
            }

            var forward = movementReference.forward;
            var right = movementReference.right;
            forward.y = 0f;
            right.y = 0f;
            if (forward.sqrMagnitude < 0.001f || right.sqrMagnitude < 0.001f)
            {
                return new Vector3(direction.x, 0f, direction.y);
            }

            return Vector3.ClampMagnitude(
                right.normalized * direction.x + forward.normalized * direction.y,
                1f);
        }

        private Vector3 ClampToBounds(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, center.x - worldHalfExtents.x, center.x + worldHalfExtents.x);
            position.z = Mathf.Clamp(position.z, center.z - worldHalfExtents.y, center.z + worldHalfExtents.y);
            return position;
        }

        private void StopEvade()
        {
            if (evadeRoutine != null)
            {
                StopCoroutine(evadeRoutine);
                evadeRoutine = null;
            }

            IsEvading = false;
        }

        private void OnDisable()
        {
            StopEvade();
            CurrentCommand = new MoveCommand(Vector2.zero);
        }

        private Vector2 ReadDirection()
        {
            var direction = virtualJoystick == null ? Vector2.zero : virtualJoystick.Value;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                var x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                        (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                var y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) -
                        (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
                var keyboardDirection = new Vector2(x, y);
                if (keyboardDirection.sqrMagnitude > direction.sqrMagnitude)
                {
                    direction = keyboardDirection; // 더 강한 입력을 우선
                }
            }

            return Vector2.ClampMagnitude(direction, 1f);
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Transform target,
            SeedVirtualJoystick joystick,
            float speed,
            Vector2 halfExtents,
            Transform inputReference = null)
        {
            controlled = target;
            virtualJoystick = joystick;
            movementReference = inputReference;
            moveSpeed = speed;
            worldHalfExtents = halfExtents;
        }
#endif
    }
}
