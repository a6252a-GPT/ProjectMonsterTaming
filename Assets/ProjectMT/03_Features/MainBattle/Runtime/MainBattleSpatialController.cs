using System;
using System.Collections.Generic;
using ProjectMT.Features.Expedition;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattleSpatialController : MonoBehaviour // 메인전투 간격·군단장 추종
    {
        [Header("Soft Separation")]
        [SerializeField, Min(0f)] private float playerPairDistance = 0.82f;
        [SerializeField, Min(0f)] private float enemyPairDistance = 0.75f;
        [SerializeField, Min(0f)] private float opposingPairDistance = 0.62f;
        [SerializeField, Min(0f)] private float maxPairSeparationSpeed = 0.9f;
        [SerializeField, Min(0f)] private float maxUnitCorrectionSpeed = 0.45f;
        [SerializeField, Min(0f)] private float edgePadding = 0.35f;
        [SerializeField, Min(0.001f)] private float maxSolverDeltaTime = 0.05f;

        [Header("Commander Follow")]
        [SerializeField, Min(0f)] private float commanderRearOffset = 1.8f;
        [SerializeField, Min(0f)] private float commanderFollowDeadZone = 0.3f;
        [SerializeField, Min(0f)] private float commanderFollowSpeed = 2.4f;
        [SerializeField, Min(0f)] private float commanderCatchUpDistance = 2.8f;
        [SerializeField, Min(0f)] private float commanderCatchUpSpeed = 3.5f;

        [Header("Commander Facing")]
        [SerializeField, Min(0f)] private float commanderMoveTurnSpeed = 720f;
        [SerializeField, Min(0f)] private float commanderEnemyTurnSpeed = 360f;
        [SerializeField, Min(0f)] private float commanderEnemySettleDistance = 0.14f;
        [SerializeField, Min(0f)] private float commanderEnemySettleSpeed = 0.85f;

        private readonly List<UnitActor> units = new List<UnitActor>(32);
        private Vector3[] positions = new Vector3[32];
        private Vector3[] corrections = new Vector3[32];

        private ExpeditionController expedition;
        private Collider ground;
        private Transform commander;
        private MainBattleCommanderFootIkLock commanderFootIk;
        private Vector3 commanderStartPosition;
        private Quaternion commanderStartRotation;
        private Vector3 battleForward;
        private bool commanderFootIkOriginalEnabled;
        private bool commanderWasFollowing;
        private float commanderSettleDistanceRemaining;
        private int observedRunSequence;
        private bool configured;

        public void Configure(
            ExpeditionController expeditionController,
            Collider groundCollider,
            Transform commanderRoot,
            Transform enemySpawnAnchor)
        {
            Shutdown();
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

            commanderStartPosition = commander.position;
            commanderStartRotation = commander.rotation;
            battleForward = enemySpawnAnchor.position - commanderStartPosition;
            battleForward.y = 0f;
            if (battleForward.sqrMagnitude < 0.0001f)
            {
                battleForward = new Vector3(1f, 0f, 1f);
            }

            battleForward.Normalize();
            commanderFootIk = commander.GetComponentInChildren<MainBattleCommanderFootIkLock>(true);
            commanderFootIkOriginalEnabled = commanderFootIk != null && commanderFootIk.enabled;
            observedRunSequence = expedition.RunSequence;
            configured = true;
            enabled = true;
        }

        public void Shutdown()
        {
            RestoreCommanderFootIk();
            units.Clear();
            expedition = null;
            ground = null;
            commander = null;
            commanderFootIk = null;
            commanderWasFollowing = false;
            commanderSettleDistanceRemaining = 0f;
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
                positions[index] = units[index].transform.position;
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

                    var desiredDistance = GetDesiredDistance(left.Team, right.Team);
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
            UnitActor rearmostPlayer = null;
            var rearmostProgress = float.PositiveInfinity;
            for (var index = 0; index < units.Count; index++)
            {
                var unit = units[index];
                if (unit == null || !unit.IsAlive || unit.Team != UnitTeam.Player)
                {
                    continue;
                }

                var progress = Vector3.Dot(unit.transform.position - commanderStartPosition, battleForward);
                if (progress < rearmostProgress)
                {
                    rearmostProgress = progress;
                    rearmostPlayer = unit;
                }
            }

            if (rearmostPlayer == null)
            {
                FaceCommanderTowardEnemies(deltaTime, false);
                SetCommanderMoving(false);
                return;
            }

            var target = rearmostPlayer.transform.position - battleForward * commanderRearOffset;
            target.y = commander.position.y;
            target = ClampToGround(target);
            var distance = PlanarDistance(commander.position, target);
            if (distance <= commanderFollowDeadZone)
            {
                if (commanderWasFollowing)
                {
                    commanderWasFollowing = false;
                    commanderSettleDistanceRemaining = Mathf.Min(
                        commanderEnemySettleDistance,
                        commanderFollowDeadZone * 0.8f);
                }

                var settling = FaceCommanderTowardEnemies(deltaTime, true);
                SetCommanderMoving(settling);
                return;
            }

            var speed = distance >= commanderCatchUpDistance
                ? commanderCatchUpSpeed
                : commanderFollowSpeed;
            var nextPosition = Vector3.MoveTowards(commander.position, target, speed * deltaTime);
            var moveDirection = nextPosition - commander.position;
            moveDirection.y = 0f;
            var moved = moveDirection.sqrMagnitude > 0.00000001f;
            commander.position = nextPosition;
            if (moved)
            {
                RotateCommanderTowards(moveDirection, commanderMoveTurnSpeed, deltaTime);
                commanderWasFollowing = true;
                commanderSettleDistanceRemaining = 0f;
            }

            SetCommanderMoving(moved);
        }

        private bool FaceCommanderTowardEnemies(float deltaTime, bool allowSettleMove)
        {
            var direction = ResolveEnemyFacingDirection();
            RotateCommanderTowards(direction, commanderEnemyTurnSpeed, deltaTime);
            if (!allowSettleMove || commanderSettleDistanceRemaining <= 0f || commanderEnemySettleSpeed <= 0f)
            {
                return false;
            }

            var requestedDistance = Mathf.Min(
                commanderSettleDistanceRemaining,
                commanderEnemySettleSpeed * deltaTime);
            var previousPosition = commander.position;
            var nextPosition = ClampToGround(previousPosition + direction * requestedDistance);
            nextPosition.y = previousPosition.y;
            commander.position = nextPosition;
            var movedDistance = PlanarDistance(previousPosition, nextPosition);
            commanderSettleDistanceRemaining = Mathf.Max(0f, commanderSettleDistanceRemaining - movedDistance);
            if (movedDistance <= 0.0001f)
            {
                commanderSettleDistanceRemaining = 0f;
                return false;
            }

            return true;
        }

        private Vector3 ResolveEnemyFacingDirection()
        {
            var enemyCenter = Vector3.zero;
            var enemyCount = 0;
            for (var index = 0; index < units.Count; index++)
            {
                var unit = units[index];
                if (unit == null || !unit.IsAlive || unit.Team != UnitTeam.Enemy)
                {
                    continue;
                }

                enemyCenter += unit.transform.position;
                enemyCount++;
            }

            var direction = enemyCount > 0
                ? enemyCenter / enemyCount - commander.position
                : battleForward;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : battleForward;
        }

        private void RotateCommanderTowards(Vector3 direction, float turnSpeed, float deltaTime)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f || turnSpeed <= 0f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            commander.rotation = Quaternion.RotateTowards(
                commander.rotation,
                targetRotation,
                turnSpeed * deltaTime);
        }

        private void ResetCommander()
        {
            if (commander != null)
            {
                commander.position = commanderStartPosition;
                commander.rotation = commanderStartRotation;
            }

            commanderWasFollowing = false;
            commanderSettleDistanceRemaining = 0f;
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

        private float GetDesiredDistance(UnitTeam leftTeam, UnitTeam rightTeam)
        {
            if (leftTeam != rightTeam)
            {
                return opposingPairDistance;
            }

            return leftTeam == UnitTeam.Player ? playerPairDistance : enemyPairDistance;
        }

        private static bool CanSeparate(UnitActor unit)
        {
            return unit != null && unit.IsAlive && !unit.IsManuallyHeld;
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
