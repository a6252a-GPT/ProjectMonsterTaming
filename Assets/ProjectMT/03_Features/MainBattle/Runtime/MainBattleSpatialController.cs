using System;
using System.Collections.Generic;
using ProjectMT.Features.Expedition;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattleSpatialController : MonoBehaviour // 메인전투 실시간 간격·군단장 자동전투 이동
    {
        [Header("Soft Separation")]
        [SerializeField, Min(0f)] private float playerPairDistance = 0.85f;
        [SerializeField, Min(0f)] private float enemyPairDistance = 0.8f;
        [SerializeField, Min(0f)] private float opposingPairDistance = 0.65f;
        [SerializeField, Min(0f)] private float maxPairSeparationSpeed = 0.65f;
        [SerializeField, Min(0f)] private float maxUnitCorrectionSpeed = 0.28f;
        [SerializeField, Min(0f)] private float edgePadding = 0.35f;
        [SerializeField, Min(0.001f)] private float maxSolverDeltaTime = 0.05f;

        [Header("Commander Auto Combat")]
        [SerializeField, Min(0f)] private float commanderPreferredCombatRange = 8f;
        [SerializeField, Min(0f)] private float commanderRetreatCombatRange = 6f;
        [SerializeField, Min(0f)] private float commanderMoveSpeed = 2.4f;

        [Header("Commander Facing")]
        [SerializeField, Min(0.01f)] private float commanderTurnSmoothTime = 0.28f;
        [SerializeField, Min(0f)] private float commanderMaxTurnSpeed = 210f;

        private readonly List<UnitActor> units = new List<UnitActor>(32);
        private Vector3[] positions = new Vector3[32];
        private Vector3[] corrections = new Vector3[32];

        private ExpeditionController expedition;
        private Collider ground;
        private Transform commander;
        private MainBattleCommanderFootIkLock commanderFootIk;
        private Vector3 commanderStartPosition;
        private Quaternion commanderStartRotation;
        private Quaternion commanderFacingOffset;
        private Vector3 battleForward;
        private bool commanderFootIkOriginalEnabled;
        private UnitActor commanderCombatTarget;
        private Func<bool> commanderActionLocked;
        private float commanderYawVelocity;
        private int observedRunSequence;
        private bool configured;

        public void Configure(
            ExpeditionController expeditionController,
            Collider groundCollider,
            Transform commanderRoot,
            Transform playerFormationAnchor,
            Transform enemySpawnAnchor,
            Func<bool> isCommanderActionLocked = null)
        {
            Shutdown();
            ApplyCombatTuning(CombatImpactTuning.ActiveConfig);
            expedition = expeditionController != null
                ? expeditionController
                : throw new ArgumentNullException(nameof(expeditionController));
            ground = groundCollider != null
                ? groundCollider
                : throw new ArgumentNullException(nameof(groundCollider));
            commander = commanderRoot != null
                ? commanderRoot
                : throw new ArgumentNullException(nameof(commanderRoot));
            if (enemySpawnAnchor == null)
            {
                throw new ArgumentNullException(nameof(enemySpawnAnchor));
            }

            if (playerFormationAnchor == null)
            {
                throw new ArgumentNullException(nameof(playerFormationAnchor));
            }

            commanderStartPosition = commander.position;
            commanderStartRotation = commander.rotation;
            commanderFacingOffset = commanderStartRotation; // 루트 회전은 모델의 로컬 정면 보정값
            battleForward = enemySpawnAnchor.position - playerFormationAnchor.position;
            battleForward.y = 0f;
            if (battleForward.sqrMagnitude < 0.0001f)
            {
                battleForward = new Vector3(1f, 0f, 1f);
            }

            battleForward.Normalize();
            commanderActionLocked = isCommanderActionLocked;
            commanderFootIk = commander.GetComponentInChildren<MainBattleCommanderFootIkLock>(true);
            commanderFootIkOriginalEnabled = commanderFootIk != null && commanderFootIk.enabled;
            observedRunSequence = expedition.RunSequence;
            configured = true;
            enabled = true;
        }

        private void ApplyCombatTuning(CombatTuningConfig tuning)
        {
            if (tuning == null)
            {
                return;
            }

            playerPairDistance = tuning.MainBattlePlayerPairDistance;
            enemyPairDistance = tuning.MainBattleEnemyPairDistance;
            opposingPairDistance = tuning.MainBattleOpposingPairDistance;
            maxPairSeparationSpeed = tuning.MainBattlePairSeparationSpeed;
            maxUnitCorrectionSpeed = tuning.MainBattleUnitCorrectionSpeed;
        }

        public void Shutdown()
        {
            RestoreCommanderFootIk();
            units.Clear();
            expedition = null;
            ground = null;
            commander = null;
            commanderFootIk = null;
            commanderFacingOffset = Quaternion.identity;
            commanderCombatTarget = null;
            commanderActionLocked = null;
            commanderYawVelocity = 0f;
            observedRunSequence = 0;
            configured = false;
            enabled = false;
        }

        public void ResetToStart()
        {
            ResetCommander();
        }

        private void LateUpdate()
        {
            if (!configured || expedition == null || commander == null || ground == null)
            {
                return;
            }

            if (observedRunSequence != expedition.RunSequence)
            {
                observedRunSequence = expedition.RunSequence;
                ResetCommander();
            }

            if (!expedition.IsRunning)
            {
                SetCommanderMoving(false);
                return;
            }

            TickSpatial(Mathf.Min(Time.deltaTime, maxSolverDeltaTime));
        }

        private void TickSpatial(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            expedition.CollectActiveUnits(units);
            EnsureBufferCapacity(units.Count);
            CachePositions();
            SolveSeparation(deltaTime);
            ApplyCorrections(deltaTime);
            UpdateCommander(deltaTime);
        }

        private void CachePositions()
        {
            for (var index = 0; index < units.Count; index++)
            {
                var unit = units[index];
                positions[index] = unit.transform.position;
                corrections[index] = Vector3.zero;
            }
        }

        private void SolveSeparation(float deltaTime)
        {
            for (var leftIndex = 0; leftIndex < units.Count - 1; leftIndex++)
            {
                var left = units[leftIndex];
                if (!CanSeparate(left))
                {
                    continue;
                }

                for (var rightIndex = leftIndex + 1; rightIndex < units.Count; rightIndex++)
                {
                    var right = units[rightIndex];
                    if (!CanSeparate(right))
                    {
                        continue;
                    }

                    var desiredDistance = GetDesiredDistance(left, right);
                    var pairMove = CalculatePairMove(
                        left,
                        right,
                        positions[leftIndex],
                        positions[rightIndex],
                        desiredDistance,
                        deltaTime);
                    corrections[leftIndex] += pairMove * 0.5f;
                    corrections[rightIndex] -= pairMove * 0.5f;
                }
            }
        }

        private Vector3 CalculatePairMove(
            UnitActor left,
            UnitActor right,
            Vector3 leftPosition,
            Vector3 rightPosition,
            float desiredDistance,
            float deltaTime)
        {
            if (desiredDistance <= 0f || maxPairSeparationSpeed <= 0f)
            {
                return Vector3.zero;
            }

            var delta = leftPosition - rightPosition;
            delta.y = 0f;
            var desiredDistanceSquared = desiredDistance * desiredDistance;
            if (delta.sqrMagnitude >= desiredDistanceSquared)
            {
                return Vector3.zero;
            }

            var distance = Mathf.Sqrt(delta.sqrMagnitude);
            var direction = distance > 0.001f
                ? delta / distance
                : GetStableFallbackDirection(left, right);
            var overlap = Mathf.Clamp01(1f - distance / desiredDistance);
            var easedOverlap = overlap * overlap * (3f - 2f * overlap);
            return direction * (maxPairSeparationSpeed * easedOverlap * deltaTime);
        }

        private void ApplyCorrections(float deltaTime)
        {
            var maxCorrection = Mathf.Max(0f, maxUnitCorrectionSpeed) * deltaTime;
            for (var index = 0; index < units.Count; index++)
            {
                var unit = units[index];
                if (!CanSeparate(unit))
                {
                    continue;
                }

                var correction = Vector3.ClampMagnitude(corrections[index], maxCorrection);
                var nextPosition = positions[index] + correction;
                nextPosition.y = positions[index].y;
                unit.transform.position = ClampToGround(nextPosition);
            }
        }

        private void UpdateCommander(float deltaTime)
        {
            commanderCombatTarget = ResolveCommanderCombatTarget();
            if (commanderCombatTarget == null || (commanderActionLocked?.Invoke() ?? false))
            {
                commanderYawVelocity = 0f;
                SetCommanderMoving(false);
                return;
            }

            var targetPosition = commanderCombatTarget.transform.position;
            targetPosition.y = commander.position.y;
            var distance = PlanarDistance(commander.position, targetPosition);
            var targetBodyRadius = commanderCombatTarget.BodyRadius;
            var preferredRange = Mathf.Max(
                commanderRetreatCombatRange + 0.25f,
                commanderPreferredCombatRange) + targetBodyRadius;
            var retreatRange = Mathf.Min(
                commanderRetreatCombatRange,
                preferredRange - targetBodyRadius - 0.25f) + targetBodyRadius;

            if (distance > preferredRange)
            {
                MoveCommander(targetPosition - commander.position, deltaTime);
                return;
            }

            if (distance < retreatRange)
            {
                MoveCommander(commander.position - targetPosition, deltaTime);
                return;
            }

            SmoothTurnCommander(targetPosition - commander.position, deltaTime);
            SetCommanderMoving(false);
        }

        private UnitActor ResolveCommanderCombatTarget()
        {
            if (IsValidCommanderTarget(commanderCombatTarget))
            {
                return commanderCombatTarget; // 몬스터와 같이 살아 있는 현재 타깃을 유지
            }

            UnitActor nearest = null;
            var nearestDistanceSquared = float.PositiveInfinity;
            for (var index = 0; index < units.Count; index++)
            {
                var unit = units[index];
                if (!IsValidCommanderTarget(unit))
                {
                    continue;
                }

                var delta = unit.transform.position - commander.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < nearestDistanceSquared)
                {
                    nearest = unit;
                    nearestDistanceSquared = delta.sqrMagnitude;
                }
            }

            return nearest;
        }

        private static bool IsValidCommanderTarget(UnitActor unit)
        {
            return unit != null && unit.IsAlive && unit.IsCombatReady && unit.Team == UnitTeam.Enemy;
        }

        private void MoveCommander(Vector3 direction, float deltaTime)
        {
            direction.y = 0f;
            if (commanderMoveSpeed <= 0f || direction.sqrMagnitude <= 0.0001f)
            {
                SetCommanderMoving(false);
                return;
            }

            var destination = commander.position + direction.normalized;
            var nextPosition = Vector3.MoveTowards(
                commander.position,
                destination,
                commanderMoveSpeed * deltaTime); // UnitActor와 같은 등속 접근
            nextPosition = ClampToGround(nextPosition);
            nextPosition.y = commander.position.y;
            var moveDirection = nextPosition - commander.position;
            moveDirection.y = 0f;
            var moved = moveDirection.sqrMagnitude > 0.00000001f;
            commander.position = nextPosition;
            if (moved)
            {
                SmoothTurnCommander(moveDirection, deltaTime);
            }

            SetCommanderMoving(moved);
        }

        private void SmoothTurnCommander(Vector3 direction, float deltaTime)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f || commanderMaxTurnSpeed <= 0f)
            {
                return;
            }

            var logicalRotation = commander.rotation * Quaternion.Inverse(commanderFacingOffset);
            var currentYaw = logicalRotation.eulerAngles.y;
            var targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            var nextYaw = Mathf.SmoothDampAngle(
                currentYaw,
                targetYaw,
                ref commanderYawVelocity,
                commanderTurnSmoothTime,
                commanderMaxTurnSpeed,
                deltaTime);
            commander.rotation = Quaternion.Euler(0f, nextYaw, 0f) * commanderFacingOffset;
        }

        private void ResetCommander()
        {
            if (commander != null)
            {
                commander.position = commanderStartPosition;
                commander.rotation = commanderStartRotation;
            }

            commanderCombatTarget = null;
            commanderYawVelocity = 0f;
            SetCommanderMoving(false);
        }

        private void SetCommanderMoving(bool moving)
        {
            if (commanderFootIk != null && commanderFootIkOriginalEnabled)
            {
                commanderFootIk.enabled = !moving;
            }
        }

        private void RestoreCommanderFootIk()
        {
            if (commanderFootIk != null)
            {
                commanderFootIk.enabled = commanderFootIkOriginalEnabled;
            }
        }

        private Vector3 ClampToGround(Vector3 position)
        {
            var bounds = ground.bounds;
            var minX = Mathf.Min(bounds.center.x, bounds.min.x + edgePadding);
            var maxX = Mathf.Max(bounds.center.x, bounds.max.x - edgePadding);
            var minZ = Mathf.Min(bounds.center.z, bounds.min.z + edgePadding);
            var maxZ = Mathf.Max(bounds.center.z, bounds.max.z - edgePadding);
            position.x = Mathf.Clamp(position.x, minX, maxX);
            position.z = Mathf.Clamp(position.z, minZ, maxZ);
            return position;
        }

        private float GetDesiredDistance(UnitActor left, UnitActor right)
        {
            if (left.Team != right.Team)
            {
                return Mathf.Max(opposingPairDistance, (left.BodyRadius + right.BodyRadius) * 0.9f);
            }

            var configuredDistance = left.Team == UnitTeam.Player ? playerPairDistance : enemyPairDistance;
            return Mathf.Max(configuredDistance, left.BodyRadius + right.BodyRadius + 0.06f);
        }

        private static bool CanSeparate(UnitActor unit)
        {
            return unit != null && unit.IsAlive && unit.IsCombatReady &&
                   !unit.IsManuallyHeld && !unit.IsInHitReaction; // 넉백·후경직을 간격 보정이 상쇄하지 않음
        }

        private static Vector3 GetStableFallbackDirection(UnitActor left, UnitActor right)
        {
            var leftId = left.GetInstanceID();
            var rightId = right.GetInstanceID();
            var lowId = Math.Min(leftId, rightId);
            var highId = Math.Max(leftId, rightId);
            var hash = unchecked((uint)(lowId * 73856093 ^ highId * 19349663));
            var angle = hash % 4096u * (Mathf.PI * 2f / 4096f);
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            return leftId == lowId ? direction : -direction;
        }

        private void EnsureBufferCapacity(int requiredCount)
        {
            if (positions.Length >= requiredCount)
            {
                return;
            }

            var capacity = positions.Length;
            while (capacity < requiredCount)
            {
                capacity *= 2;
            }

            Array.Resize(ref positions, capacity);
            Array.Resize(ref corrections, capacity);
        }

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }
    }
}
