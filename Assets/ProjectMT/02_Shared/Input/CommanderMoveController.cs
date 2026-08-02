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
        [SerializeField, Min(0f)] private float moveSpeed = 4f; // 초당 이동 거리
        [SerializeField] private Vector2 worldHalfExtents = new Vector2(6f, 4f); // 이동 가능 반경

        private Vector3 center; // 현재 콘텐츠 이동 중심
        private bool inputEnabled; // 플레이 중 입력 허용

        public MoveCommand CurrentCommand { get; private set; }

        private void Awake()
        {
            if (controlled == null)
            {
                controlled = transform;
            }

            center = controlled.position;
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
            var movement = new Vector3(CurrentCommand.Direction.x, 0f, CurrentCommand.Direction.y);
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
            if (enabled && controlled != null)
            {
                center = controlled.position; // 실행 시작점을 새 중심으로 사용
            }
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
        public void EditorConfigure(Transform target, SeedVirtualJoystick joystick, float speed, Vector2 halfExtents)
        {
            controlled = target;
            virtualJoystick = joystick;
            moveSpeed = speed;
            worldHalfExtents = halfExtents;
        }
#endif
    }
}
