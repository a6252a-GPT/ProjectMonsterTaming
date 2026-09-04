using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using ProjectMT.Shared.Input;
using ProjectMT.Shared.Unit;

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

        [Header("라이프")]
        [SerializeField] private int maxLives = 5;
        [SerializeField] private float hitKnockbackSpeed = 5.5f;
        [SerializeField] private float hitKnockbackDecay = 18f;
        [SerializeField] private float hitInvulnerableDuration = 0.45f;

        [Header("점프")]
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.25f;

        private CharacterController characterController;
        private UnitVisualFeedback visualFeedback;
        private MazeCameraFollow cameraFollow;
        private Vector3 knockbackVelocity;
        private int speedHash;
        private bool hasSpeedParameter;
        private bool inputEnabled = true;
        private float verticalVelocity;
        private int currentLives;
        private bool isDead;
        private float hitInvulnerableUntil;

        public bool InputEnabled => inputEnabled;
        public int CurrentLives => currentLives;
        public int MaxLives => maxLives;
        public bool IsJumping => characterController != null && !characterController.isGrounded;
        public bool CanJump => inputEnabled && !isDead && characterController != null && characterController.isGrounded;
        public float JumpReadyFill => CanJump ? 1f : 0f;
        public event Action<int, int> LivesChanged;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            CheckSpeedParameter();
            ResetLives();

            visualFeedback = GetComponent<UnitVisualFeedback>();
            if (visualFeedback == null)
            {
                visualFeedback = gameObject.AddComponent<UnitVisualFeedback>();
            }

            visualFeedback.RefreshRenderers();
        }

        private void OnEnable()
        {
            Demo.DemoCombatRoster.RegisterAlly(transform);
        }

        private void OnDisable()
        {
            Demo.DemoCombatRoster.UnregisterAlly(transform);
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
            if (characterController == null)
            {
                UpdateAnimation(0f);
                return;
            }

            Vector3 moveDelta = Vector3.zero;
            if (!inputEnabled || isDead)
            {
                UpdateAnimation(0f);
            }
            else
            {
                if (WasJumpPressed())
                {
                    TryJump();
                }

                Vector2 inputDirection = ReadDirection();
                Vector3 movement = new Vector3(inputDirection.x, 0f, inputDirection.y);
                if (movement.sqrMagnitude > 0.001f)
                {
                    moveDelta = movement.normalized * (moveSpeed * Time.deltaTime);

                    Quaternion targetRotation = Quaternion.LookRotation(moveDelta.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                    UpdateAnimation(1f);
                }
                else
                {
                    UpdateAnimation(0f);
                }
            }

            if (knockbackVelocity.sqrMagnitude > 0.0001f)
            {
                moveDelta += knockbackVelocity * Time.deltaTime;
                knockbackVelocity = Vector3.MoveTowards(
                    knockbackVelocity,
                    Vector3.zero,
                    hitKnockbackDecay * Time.deltaTime);
            }

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity += Physics.gravity.y * Time.deltaTime;
            }

            ClampHorizontalMoveToNavMesh(ref moveDelta);
            moveDelta.y = verticalVelocity * Time.deltaTime;
            characterController.Move(moveDelta);
        }

        private void ClampHorizontalMoveToNavMesh(ref Vector3 moveDelta)
        {
            Vector3 horizontal = new Vector3(moveDelta.x, 0f, moveDelta.z);
            if (horizontal.sqrMagnitude < 0.0000001f)
            {
                return;
            }

            Vector3 origin = transform.position;
            if (NavMesh.SamplePosition(origin, out NavMeshHit onMesh, 1.5f, NavMesh.AllAreas))
            {
                origin = onMesh.position;
            }

            Vector3 destination = origin + horizontal;
            if (NavMesh.SamplePosition(destination, out NavMeshHit sampled, 0.55f, NavMesh.AllAreas))
            {
                Vector3 allowed = sampled.position - origin;
                allowed.y = 0f;
                if (allowed.sqrMagnitude > 0.0000001f)
                {
                    moveDelta.x = allowed.x;
                    moveDelta.z = allowed.z;
                    return;
                }
            }

            if (NavMesh.Raycast(origin, destination, out NavMeshHit blocked, NavMesh.AllAreas))
            {
                Vector3 allowed = blocked.position - origin;
                allowed.y = 0f;
                moveDelta.x = allowed.x;
                moveDelta.z = allowed.z;
            }
            else
            {
                moveDelta.x = 0f;
                moveDelta.z = 0f;
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

        private static bool WasJumpPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                return true;
            }

            Gamepad gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        }

        public bool TryJump()
        {
            if (!CanJump)
            {
                return false;
            }

            verticalVelocity = Mathf.Sqrt(Mathf.Max(0.1f, jumpHeight) * -2f * Physics.gravity.y);
            knockbackVelocity = Vector3.zero;
            Demo.DemoDungeonAudio.PlayJump(transform.position);
            return true;
        }

        private void UpdateAnimation(float speedValue)
        {
            // 애니메이터에 해당 파라미터가 실제로 존재할 경우에만 안심하고 호출
            if (animator != null && hasSpeedParameter)
            {
                animator.SetFloat(speedHash, speedValue, 0.08f, Time.deltaTime);
            }
        }

        public void ResetLives()
        {
            isDead = false;
            currentLives = Mathf.Max(1, maxLives);
            verticalVelocity = 0f;
            hitInvulnerableUntil = 0f;
            LivesChanged?.Invoke(currentLives, maxLives);
        }

        public void TakeDamage(float damage)
        {
            TakeDamage(damage, transform.position - transform.forward);
        }

        public void TakeDamage(float damage, Vector3 hitOrigin)
        {
            if (isDead || damage <= 0f || Time.time < hitInvulnerableUntil)
            {
                return;
            }

            currentLives = Mathf.Max(0, currentLives - 1);
            LivesChanged?.Invoke(currentLives, maxLives);
            hitInvulnerableUntil = Time.time + Mathf.Max(0.1f, hitInvulnerableDuration);
            PlayHitReaction(hitOrigin);
            if (currentLives > 0)
            {
                return;
            }

            isDead = true;
            SetInputEnabled(false);
            Demo.DemoDungeonController.Active?.FailDungeon("함정에 당했습니다");
        }

        private void PlayHitReaction(Vector3 hitOrigin)
        {
            visualFeedback?.PlayHit();
            Demo.DemoDungeonAudio.PlayCommanderDamage(transform.position);

            Vector3 away = transform.position - hitOrigin;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = -transform.forward;
            }

            knockbackVelocity = away.normalized * hitKnockbackSpeed;

            if (cameraFollow == null)
            {
                Camera mainCamera = Camera.main;
                cameraFollow = mainCamera != null ? mainCamera.GetComponent<MazeCameraFollow>() : null;
            }

            cameraFollow?.PlayHitShake();
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled) UpdateAnimation(0f);
        }
    }
}
