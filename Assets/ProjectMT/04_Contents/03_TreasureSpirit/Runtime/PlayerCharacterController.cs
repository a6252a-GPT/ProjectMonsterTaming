using UnityEngine;
using UnityEngine.InputSystem;
using ProjectMT.Shared.Input;

namespace ProjectMT.Contents.TreasureSpirit
{
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class PlayerCharacterController : MonoBehaviour
    {
        [Header("이동 및 제어 설정")]
        [SerializeField] private SeedVirtualJoystick virtualJoystick;
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float rotationSpeed = 14f;

        [Header("애니메이션")]
        [SerializeField] private Animator animator;
        [SerializeField] private string speedParameter = "Speed";

        private CharacterController characterController;
        private int speedHash;
        private bool hasSpeedParameter; // 애니메이션 파라미터 안전성 체크 플래그
        private bool inputEnabled = true;

        public bool InputEnabled => inputEnabled;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            CheckSpeedParameter();
        }

        /// <summary>
        /// Animator Controller 내에 'Speed' 파라미터가 실제 존재하는지 사전 검사
        /// </summary>
        private void CheckSpeedParameter()
        {
            if (animator == null || string.IsNullOrEmpty(speedParameter))
            {
                hasSpeedParameter = false;
                return;
            }

            speedHash = Animator.StringToHash(speedParameter);
            hasSpeedParameter = false;

            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.nameHash == speedHash)
                {
                    hasSpeedParameter = true;
                    break;
                }
            }
        }

        private void Update()
        {
            if (!inputEnabled || characterController == null)
            {
                UpdateAnimation(0f);
                return;
            }

            // 1. 입력 감지
            Vector2 inputDirection = ReadDirection();
            Vector3 movement = new Vector3(inputDirection.x, 0f, inputDirection.y);

            if (movement.sqrMagnitude > 0.001f)
            {
                // 방향 정규화 및 이동 속도 계산
                Vector3 moveDelta = movement.normalized * (moveSpeed * Time.deltaTime);

                characterController.Move(moveDelta);

                // 4. 회전 처리
                if (moveDelta.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(moveDelta.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }

                UpdateAnimation(1f);
            }
            else
            {
                UpdateAnimation(0f);
            }
        }

        private Vector2 ReadDirection()
        {
            Vector2 direction = virtualJoystick != null ? virtualJoystick.Value : Vector2.zero;
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null)
            {
                float x = 0f;
                float y = 0f;

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;

                Vector2 keyboardDirection = new Vector2(x, y);

                // 더 입력값이 큰 쪽을 선택
                if (keyboardDirection.sqrMagnitude > direction.sqrMagnitude)
                {
                    direction = keyboardDirection;
                }
            }

            return Vector2.ClampMagnitude(direction, 1f);
        }

        private void UpdateAnimation(float speedValue)
        {
            // 애니메이터에 해당 파라미터가 실제로 존재할 경우에만 안심하고 호출
            if (animator != null && hasSpeedParameter)
            {
                animator.SetFloat(speedHash, speedValue, 0.08f, Time.deltaTime);
            }
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled)
            {
                UpdateAnimation(0f);
            }
        }
    }
}