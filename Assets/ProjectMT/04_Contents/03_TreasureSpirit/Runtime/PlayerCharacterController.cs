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

        [Header("회피")]
        [SerializeField] private float dodgeDistance = 2.5f;
        [SerializeField] private float dodgeDuration = 0.2f;
        [SerializeField] private float dodgeCooldown = 1.2f;
        [SerializeField] private float dodgeInvulnerableDuration = 0.3f;

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
        private float dodgeRemaining;
        private float dodgeInvulnerableUntil;
        private float nextDodgeTime;
        private Vector3 dodgeVelocity;

        public bool InputEnabled => inputEnabled;
        public int CurrentLives => currentLives;
        public int MaxLives => maxLives;
        public bool IsDodging => dodgeRemaining > 0f;
        public bool CanDodge => inputEnabled && !isDead && !IsDodging && Time.time >= nextDodgeTime;
        public float DodgeCooldownFill
        {
            get
            {
                if (Time.time >= nextDodgeTime)
                {
                    return 1f;
                }

                float wait = nextDodgeTime - Time.time;
                return 1f - Mathf.Clamp01(wait / Mathf.Max(0.01f, dodgeCooldown));
            }
        }
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
                StopDodge();
                UpdateAnimation(0f);
            }
            else
            {
                if (WasDodgePressed())
                {
                    TryDodge();
                }

                Vector2 inputDirection = ReadDirection();
                Vector3 movement = new Vector3(inputDirection.x, 0f, inputDirection.y);
                if (IsDodging)
                {
                    dodgeRemaining = Mathf.Max(0f, dodgeRemaining - Time.deltaTime);
                    moveDelta = dodgeVelocity * Time.deltaTime;
                    UpdateAnimation(1f);
                }
                else if (movement.sqrMagnitude > 0.001f)
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

            if (knockbackVelocity.sqrMagnitude > 0.0001f && !IsDodging)
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

        private bool WasDodgePressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.leftShiftKey.wasPressedThisFrame))
            {
                return true;
            }

            Gamepad gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        }

        public bool TryDodge()
        {
            if (!CanDodge)
            {
                return false;
            }

            Vector2 inputDirection = ReadDirection();
            Vector3 dodgeDirection = inputDirection.sqrMagnitude >= 0.001f
                ? new Vector3(inputDirection.x, 0f, inputDirection.y).normalized
                : GetFallbackDodgeDirection();

            Vector3 origin = transform.position;
            if (NavMesh.SamplePosition(origin, out NavMeshHit onMesh, 1.5f, NavMesh.AllAreas))
            {
                origin = onMesh.position;
            }

            Vector3 desired = origin + dodgeDirection * Mathf.Max(0.4f, dodgeDistance);
            Vector3 destination = ResolveDodgeDestination(origin, desired);
            Vector3 travel = destination - origin;
            travel.y = 0f;
            float duration = Mathf.Max(0.05f, dodgeDuration);
            dodgeVelocity = travel / duration;
            dodgeRemaining = duration;
            dodgeInvulnerableUntil = Time.time + Mathf.Max(duration, dodgeInvulnerableDuration);
            nextDodgeTime = Time.time + Mathf.Max(duration, dodgeCooldown);
            knockbackVelocity = Vector3.zero;

            if (travel.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(travel.normalized, Vector3.up);
            }

            return true;
        }

        private Vector3 GetFallbackDodgeDirection()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        }

        private static Vector3 ResolveDodgeDestination(Vector3 origin, Vector3 desired)
        {
            if (NavMesh.Raycast(origin, desired, out NavMeshHit blocked, NavMesh.AllAreas))
            {
                return blocked.position;
            }

            if (NavMesh.SamplePosition(desired, out NavMeshHit sampled, 0.85f, NavMesh.AllAreas))
            {
                return sampled.position;
            }

            return origin;
        }

        private void StopDodge()
        {
            dodgeRemaining = 0f;
            dodgeVelocity = Vector3.zero;
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
            StopDodge();
            dodgeInvulnerableUntil = 0f;
            nextDodgeTime = 0f;
            LivesChanged?.Invoke(currentLives, maxLives);
        }

        public void TakeDamage(float damage)
        {
            TakeDamage(damage, transform.position - transform.forward);
        }

        public void TakeDamage(float damage, Vector3 hitOrigin)
        {
            if (isDead || damage <= 0f || Time.time < dodgeInvulnerableUntil)
            {
                return;
            }

            currentLives = Mathf.Max(0, currentLives - 1);
            LivesChanged?.Invoke(currentLives, maxLives);
            PlayHitReaction(hitOrigin);
            if (currentLives > 0)
            {
                return;
            }

            isDead = true;
            SetInputEnabled(false);

            Demo.DemoDungeonController controller = FindFirstObjectByType<Demo.DemoDungeonController>();
            controller?.FailDungeon("함정에 당했습니다");
        }

        private void PlayHitReaction(Vector3 hitOrigin)
        {
            visualFeedback?.PlayHit();

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
            if (!enabled)
            {
                StopDodge();
                UpdateAnimation(0f);
            }
        }
    }
}