using System;
using System.Collections.Generic;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Pooling;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleTurretCombatWorld : MonoBehaviour // Hex 전용 포탑 전투 경계
    {
        private readonly List<HexCastleAssaultUnit> assaultUnits = new List<HexCastleAssaultUnit>();
        private readonly List<HexCastleCellRuntime> blockingCells = new List<HexCastleCellRuntime>();

        [SerializeField] private ScenePoolScope poolScope;
        [SerializeField] private SfxPool sfxPool;
        [SerializeField, Min(0.1f)] private float cellSize = 1f;
        [SerializeField, Min(0.01f)] private float assaultCollisionRadius = 0.38f;
        [SerializeField, Min(0f)] private float assaultHitHeight = 0.35f;
        [SerializeField] private bool isRunning;

        public ScenePoolScope PoolScope => poolScope;
        public SfxPool SfxPool => sfxPool;
        public float CellSize => cellSize;
        public float AssaultCollisionRadius => assaultCollisionRadius;
        public float AssaultHitHeight => assaultHitHeight;
        public bool IsRunning => isRunning;
        public int RegisteredAssaultUnitCount => assaultUnits.Count;
        public int RegisteredCellCount => blockingCells.Count;

        private void OnEnable()
        {
            RebuildRegistry(transform);
        }

        public void Configure(
            ScenePoolScope targetPool,
            SfxPool targetSfxPool,
            float targetCellSize,
            bool startsRunning = true,
            float targetCollisionRadius = 0.38f,
            float targetHitHeight = 0.35f)
        {
            poolScope = targetPool;
            sfxPool = targetSfxPool;
            cellSize = Mathf.Max(0.1f, targetCellSize);
            assaultCollisionRadius = Mathf.Max(0.01f, targetCollisionRadius);
            assaultHitHeight = Mathf.Max(0f, targetHitHeight);
            isRunning = startsRunning;
        }

        public void SetRunning(bool value)
        {
            isRunning = value;
        }

        public void RebuildRegistry(Transform generatedRoot)
        {
            assaultUnits.Clear();
            blockingCells.Clear();
            if (generatedRoot == null)
            {
                return;
            }

            var units = generatedRoot.GetComponentsInChildren<HexCastleAssaultUnit>(true);
            for (var index = 0; index < units.Length; index++)
            {
                RegisterAssaultUnit(units[index]);
            }

            var cells = generatedRoot.GetComponentsInChildren<HexCastleCellRuntime>(true);
            for (var index = 0; index < cells.Length; index++)
            {
                RegisterCell(cells[index]);
            }
        }

        public void RegisterAssaultUnit(HexCastleAssaultUnit unit)
        {
            if (unit != null && !assaultUnits.Contains(unit))
            {
                assaultUnits.Add(unit);
            }
        }

        public void UnregisterAssaultUnit(HexCastleAssaultUnit unit)
        {
            if (unit != null)
            {
                assaultUnits.Remove(unit);
            }
        }

        public void RegisterCell(HexCastleCellRuntime cell)
        {
            if (cell != null && !blockingCells.Contains(cell))
            {
                blockingCells.Add(cell);
            }
        }

        public void UnregisterCell(HexCastleCellRuntime cell)
        {
            if (cell != null)
            {
                blockingCells.Remove(cell);
            }
        }

        public void ClearRegistry()
        {
            assaultUnits.Clear();
            blockingCells.Clear();
        }

        public HexCastleAssaultUnit FindNearestAssaultUnit(
            HexCoordinates sourceCoordinates,
            int rangeCells)
        {
            if (!isRunning || rangeCells <= 0)
            {
                return null;
            }

            PruneMissingReferences();
            HexCastleAssaultUnit result = null;
            var resultDistance = float.PositiveInfinity;
            for (var index = 0; index < assaultUnits.Count; index++)
            {
                var candidate = assaultUnits[index];
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                var localPosition = transform.InverseTransformPoint(candidate.transform.position);
                var coordinates = HexCoordinates.FromWorld(localPosition, cellSize);
                if (sourceCoordinates.DistanceTo(coordinates) > rangeCells)
                {
                    continue;
                }

                var offset = candidate.transform.position - transform.TransformPoint(
                    sourceCoordinates.ToWorld(cellSize));
                offset.y = 0f;
                var distance = offset.sqrMagnitude;
                if (result != null &&
                    (distance > resultDistance ||
                     Mathf.Approximately(distance, resultDistance) &&
                     candidate.GetInstanceID() >= result.GetInstanceID()))
                {
                    continue;
                }

                result = candidate;
                resultDistance = distance;
            }

            return result;
        }

        public HexCastleAssaultUnit FindTarget(
            HexCastleCellRuntime sourceCell,
            Vector3 origin,
            int rangeCells,
            HexCastleTurretTargetPriority priority,
            float projectileRadius,
            bool canAttackAcrossWalls)
        {
            if (!isRunning || sourceCell == null || rangeCells <= 0)
            {
                return null;
            }

            PruneMissingReferences();
            HexCastleAssaultUnit result = null;
            var resultTier = int.MaxValue;
            var resultDistance = 0f;
            for (var index = 0; index < assaultUnits.Count; index++)
            {
                var candidate = assaultUnits[index];
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                var localTargetPosition = transform.InverseTransformPoint(candidate.transform.position);
                var targetCoordinates = HexCoordinates.FromWorld(localTargetPosition, cellSize);
                if (sourceCell.Coordinates.DistanceTo(targetCoordinates) > rangeCells)
                {
                    continue;
                }

                var targetPoint = ResolveHitPoint(candidate);
                if (!canAttackAcrossWalls &&
                    IsLineBlocked(origin, targetPoint, projectileRadius, sourceCell))
                {
                    continue;
                }

                var flatOffset = candidate.transform.position - origin;
                flatOffset.y = 0f;
                var distance = flatOffset.sqrMagnitude;
                var tier = ResolveTargetTier(candidate.name);
                if (result != null && !IsCandidateBetter(
                        priority,
                        tier,
                        distance,
                        candidate.GetInstanceID(),
                        resultTier,
                        resultDistance,
                        result.GetInstanceID()))
                {
                    continue;
                }

                result = candidate;
                resultTier = tier;
                resultDistance = distance;
            }

            return result;
        }

        public bool IsLineBlocked(
            Vector3 origin,
            Vector3 targetPoint,
            float clearanceRadius,
            HexCastleCellRuntime ignoredCell = null)
        {
            PruneMissingReferences();
            for (var index = 0; index < blockingCells.Count; index++)
            {
                var cell = blockingCells[index];
                if (cell == null || cell == ignoredCell || !cell.IsBlocked ||
                    cell.FootprintCollider == null || !cell.FootprintCollider.enabled)
                {
                    continue;
                }

                if (IntersectsPlanarBounds(
                        origin,
                        targetPoint,
                        cell.FootprintCollider.bounds,
                        Mathf.Max(0f, clearanceRadius)))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryFindFirstAssaultHit(
            Vector3 from,
            Vector3 to,
            float projectileRadius,
            ISet<int> excludedIds,
            out HexCastleAssaultUnit target,
            out Vector3 hitPoint)
        {
            target = null;
            hitPoint = to;
            if (!isRunning)
            {
                return false;
            }

            PruneMissingReferences();
            var segment = to - from;
            var segmentLengthSquared = segment.sqrMagnitude;
            var bestRatio = float.PositiveInfinity;
            for (var index = 0; index < assaultUnits.Count; index++)
            {
                var unit = assaultUnits[index];
                if (unit == null || !unit.IsAlive ||
                    excludedIds != null && excludedIds.Contains(unit.GetInstanceID()))
                {
                    continue;
                }

                var center = ResolveHitPoint(unit);
                var ratio = segmentLengthSquared <= 0.000001f
                    ? 0f
                    : Mathf.Clamp01(Vector3.Dot(center - from, segment) / segmentLengthSquared);
                var closest = from + segment * ratio;
                var combinedRadius = Mathf.Max(0.01f, projectileRadius) + assaultCollisionRadius;
                if ((center - closest).sqrMagnitude > combinedRadius * combinedRadius || ratio >= bestRatio)
                {
                    continue;
                }

                target = unit;
                hitPoint = closest;
                bestRatio = ratio;
            }

            return target != null;
        }

        public bool ApplyDamage(
            HexCastleAssaultUnit target,
            float damage,
            Vector3 hitPoint,
            HexCastleTurretRuntime sourceTurret = null)
        {
            if (!isRunning || target == null || !target.IsAlive || damage <= 0f)
            {
                return false;
            }

            return target.ApplyDamage(
                damage,
                hitPoint,
                null,
                sourceTurret == null ? null : sourceTurret.Structure); // 피격 포탑만 짧게 위협으로 기억한다
        }

        public int ApplyAreaDamage(
            Vector3 center,
            float radius,
            float damage,
            HexCastleTurretRuntime sourceTurret = null)
        {
            if (!isRunning || radius <= 0f || damage <= 0f)
            {
                return 0;
            }

            PruneMissingReferences();
            var count = 0;
            for (var index = assaultUnits.Count - 1; index >= 0; index--)
            {
                var unit = assaultUnits[index];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                var unitPosition = unit.transform.position;
                var flatCenter = center;
                unitPosition.y = 0f;
                flatCenter.y = 0f;
                var distance = Vector3.Distance(unitPosition, flatCenter);
                if (distance > radius)
                {
                    continue;
                }

                var resolvedDamage = HexCastleTurretDamageMath.ResolveExplosionDamage(
                    damage,
                    radius,
                    distance);
                if (ApplyDamage(unit, resolvedDamage, ResolveHitPoint(unit), sourceTurret))
                {
                    sourceTurret?.ReportHit(resolvedDamage);
                    count++;
                }
            }

            return count;
        }

        public GameObject RentObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return poolScope == null ? null : poolScope.Rent(prefab, position, rotation);
        }

        public void ReturnObject(GameObject instance)
        {
            poolScope?.Return(instance);
        }

        public bool PlayCue(SfxCue cue, Vector3 position)
        {
            return cue != null && sfxPool != null && sfxPool.Play(cue, position);
        }

        public Vector3 ResolveHitPoint(HexCastleAssaultUnit unit)
        {
            return unit == null ? Vector3.zero : unit.transform.position + Vector3.up * assaultHitHeight;
        }

        private void PruneMissingReferences()
        {
            for (var index = assaultUnits.Count - 1; index >= 0; index--)
            {
                if (assaultUnits[index] == null)
                {
                    assaultUnits.RemoveAt(index);
                }
            }

            for (var index = blockingCells.Count - 1; index >= 0; index--)
            {
                if (blockingCells[index] == null)
                {
                    blockingCells.RemoveAt(index);
                }
            }
        }

        private static bool IsCandidateBetter(
            HexCastleTurretTargetPriority priority,
            int leftTier,
            float leftDistance,
            int leftId,
            int rightTier,
            float rightDistance,
            int rightId)
        {
            if (priority == HexCastleTurretTargetPriority.Nearest)
            {
                return leftDistance < rightDistance ||
                       Mathf.Approximately(leftDistance, rightDistance) && leftId < rightId;
            }

            return leftTier < rightTier ||
                   leftTier == rightTier && (leftDistance > rightDistance ||
                                             Mathf.Approximately(leftDistance, rightDistance) && leftId < rightId);
        }

        private static int ResolveTargetTier(string unitName)
        {
            if (string.IsNullOrWhiteSpace(unitName))
            {
                return 2;
            }

            if (unitName.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 0;
            }

            return unitName.IndexOf("elite", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 2;
        }

        private static bool IntersectsPlanarBounds(
            Vector3 origin,
            Vector3 target,
            Bounds bounds,
            float clearanceRadius)
        {
            var minimum = bounds.min - new Vector3(clearanceRadius, 0f, clearanceRadius);
            var maximum = bounds.max + new Vector3(clearanceRadius, 0f, clearanceRadius);
            var delta = target - origin;
            var minimumRatio = 0f;
            var maximumRatio = 1f;
            return ClipAxis(origin.x, delta.x, minimum.x, maximum.x, ref minimumRatio, ref maximumRatio) &&
                   ClipAxis(origin.z, delta.z, minimum.z, maximum.z, ref minimumRatio, ref maximumRatio);
        }

        private static bool ClipAxis(
            float origin,
            float delta,
            float minimum,
            float maximum,
            ref float minimumRatio,
            ref float maximumRatio)
        {
            if (Mathf.Abs(delta) <= 0.000001f)
            {
                return origin >= minimum && origin <= maximum;
            }

            var first = (minimum - origin) / delta;
            var second = (maximum - origin) / delta;
            if (first > second)
            {
                var swap = first;
                first = second;
                second = swap;
            }

            minimumRatio = Mathf.Max(minimumRatio, first);
            maximumRatio = Mathf.Min(maximumRatio, second);
            return minimumRatio <= maximumRatio;
        }
    }
}
