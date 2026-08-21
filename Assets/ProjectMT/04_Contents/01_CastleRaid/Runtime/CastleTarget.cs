using System;
using System.Collections;
using System.Collections.Generic;
using ProjectMT.Contents.CastleRaid.Generation;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.CastleRaid
{
    public enum CastleTargetKind // 성 공격 목표 종류
    {
        Wall,
        Defender,
        Building,
        MainCastle
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(HealthComponent))]
    public sealed class CastleTarget : MonoBehaviour // 파괴 가능한 성 목표
    {
        [SerializeField] private CastleTargetKind targetKind; // 성벽·수비대·건물·본성
        [SerializeField, Min(1f)] private float maxHealth = 50f; // 목표 최대 체력
        [SerializeField] private HealthComponent health; // 공용 체력 부품
        [SerializeField] private AttackSlotProvider attackSlots; // 주변 공격 자리
        [SerializeField] private NavMeshObstacle linkedObstacle; // 생존 중 길 차단
        [SerializeField] private UnitVisualFeedback visualFeedback; // 피격·사망 펄스
        [SerializeField] private Renderer[] targetRenderers; // 사망 뒤 숨길 외형
        [SerializeField] private Collider[] targetColliders; // 사망 즉시 끌 충돌체
        [SerializeField] private bool hasGenerationMetadata; // 절차 생성 구역·방어선 정보 보유 여부
        [SerializeField] private string placementId = string.Empty;
        [SerializeField] private string districtId = string.Empty;
        [SerializeField] private CastleWallBand wallBand;
        [SerializeField, Min(0)] private int wallDefenseLayer;
        [SerializeField] private string wallLineId = string.Empty;
        [SerializeField] private CastleWallNeighborMask wallNeighborMask;
        [SerializeField] private string[] ownerDistrictIds = Array.Empty<string>();

        private Coroutine hidePresentationRoutine; // 사망 펄스 종료 대기

        public CastleTargetKind TargetKind => targetKind;
        public HealthComponent Health => health;
        public AttackSlotProvider AttackSlots => attackSlots;
        public bool IsAlive => health != null && health.IsAlive;
        public bool BlocksNavigation => linkedObstacle != null; // 파괴 뒤 경로 갱신 필요 여부
        public bool HasGenerationMetadata => hasGenerationMetadata;
        public string PlacementId => placementId;
        public string DistrictId => districtId;
        public CastleWallBand WallBand => wallBand;
        public int WallDefenseLayer => wallDefenseLayer;
        public string WallLineId => wallLineId;
        public CastleWallNeighborMask WallNeighborMask => wallNeighborMask;
        public IReadOnlyList<string> OwnerDistrictIds => ownerDistrictIds;

        public event Action<CastleTarget> Destroyed;
        public event Action<CastleTarget, DamageReport> Damaged;

        private void Awake()
        {
            ResolveReferences();
        }

        public void Initialize()
        {
            ResolveReferences();
            StopHidePresentationRoutine();
            SetPresentationEnabled(true);
            if (linkedObstacle != null)
            {
                linkedObstacle.enabled = true;
                linkedObstacle.carving = true; // 살아 있는 목표가 NavMesh 차단
            }

            attackSlots?.ReleaseAll();
            health.Damaged -= HandleDamaged;
            health.Died -= HandleDied;
            health.Initialize(maxHealth);
            HideHealthBar(); // 같은 고정 Stage 재시작 시 이전 표시 제거
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
        }

        public void Shutdown()
        {
            StopHidePresentationRoutine();
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Died -= HandleDied;
            }

            attackSlots?.ReleaseAll();
            HideHealthBar();
            Destroyed = null;
            Damaged = null;
        }

        private void HandleDamaged(DamageReport report)
        {
            visualFeedback?.PlayHit();
            CastleRaidOverheadHealthBar.ShowDamage(transform, health, false); // 적 구조물·수비대는 빨간색
            Damaged?.Invoke(this, report);
        }

        private void HandleDied(DamageReport report)
        {
            HideHealthBar();
            visualFeedback?.PlayDeath();
            attackSlots?.ReleaseAll();
            if (linkedObstacle != null)
            {
                linkedObstacle.carving = false;
                linkedObstacle.enabled = false; // 파괴 즉시 통로 개방
            }

            SetCollidersEnabled(false); // 죽은 목표 재선택 방지
            if (visualFeedback != null && isActiveAndEnabled)
            {
                hidePresentationRoutine = StartCoroutine(HideRenderersAfterDeath());
            }
            else
            {
                SetRenderersEnabled(false);
            }

            Destroyed?.Invoke(this);
        }

        private void HideHealthBar()
        {
            if (TryGetComponent<CastleRaidOverheadHealthBar>(out var healthBar))
            {
                healthBar.HideImmediately();
            }
        }

        private IEnumerator HideRenderersAfterDeath()
        {
            yield return new WaitForSeconds(UnitVisualFeedback.DeathPulseDurationSeconds); // 펄스를 끝까지 표시
            SetRenderersEnabled(false);
            hidePresentationRoutine = null;
        }

        private void StopHidePresentationRoutine()
        {
            if (hidePresentationRoutine == null)
            {
                return;
            }

            StopCoroutine(hidePresentationRoutine);
            hidePresentationRoutine = null;
        }

        private void ResolveReferences()
        {
            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }

            if (attackSlots == null)
            {
                attackSlots = GetComponent<AttackSlotProvider>();
            }

            if (visualFeedback == null)
            {
                visualFeedback = GetComponent<UnitVisualFeedback>();
            }

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (targetColliders == null || targetColliders.Length == 0)
            {
                targetColliders = GetComponentsInChildren<Collider>(true);
            }
        }

        private void SetPresentationEnabled(bool enabled)
        {
            SetRenderersEnabled(enabled);
            SetCollidersEnabled(enabled);
        }

        private void SetRenderersEnabled(bool enabled)
        {
            foreach (var targetRenderer in targetRenderers)
            {
                if (targetRenderer != null)
                {
                    targetRenderer.enabled = enabled;
                }
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            foreach (var targetCollider in targetColliders)
            {
                if (targetCollider != null)
                {
                    targetCollider.enabled = enabled;
                }
            }
        }

        public void Configure(
            CastleTargetKind kind,
            float healthValue,
            AttackSlotProvider slots,
            NavMeshObstacle obstacle)
        {
            targetKind = kind;
            maxHealth = Mathf.Max(1f, healthValue);
            attackSlots = slots;
            linkedObstacle = obstacle;
            ClearGenerationMetadata();
            ResolveReferences();
        }

        public void ConfigureGenerationMetadata(CastlePlacementData placement)
        {
            if (placement == null)
            {
                ClearGenerationMetadata();
                return;
            }

            hasGenerationMetadata = true;
            placementId = placement.PlacementId ?? string.Empty;
            districtId = placement.DistrictId ?? string.Empty;
            wallBand = placement.WallBand;
            wallDefenseLayer = Mathf.Max(0, placement.WallDefenseLayer);
            wallLineId = placement.WallLineId ?? string.Empty;
            wallNeighborMask = placement.WallNeighborMask;
            var sourceOwners = placement.OwnerDistrictIds;
            ownerDistrictIds = sourceOwners == null || sourceOwners.Count == 0
                ? string.IsNullOrWhiteSpace(districtId) ? Array.Empty<string>() : new[] { districtId }
                : CopyOwners(sourceOwners);
        }

        private void ClearGenerationMetadata()
        {
            hasGenerationMetadata = false;
            placementId = string.Empty;
            districtId = string.Empty;
            wallBand = CastleWallBand.None;
            wallDefenseLayer = 0;
            wallLineId = string.Empty;
            wallNeighborMask = CastleWallNeighborMask.None;
            ownerDistrictIds = Array.Empty<string>();
        }

        private static string[] CopyOwners(IReadOnlyList<string> sourceOwners)
        {
            var copiedOwners = new List<string>(sourceOwners.Count);
            for (var index = 0; index < sourceOwners.Count; index++)
            {
                var owner = sourceOwners[index];
                if (!string.IsNullOrWhiteSpace(owner) && !copiedOwners.Contains(owner))
                {
                    copiedOwners.Add(owner);
                }
            }

            return copiedOwners.ToArray();
        }

        public bool BlocksTurretLine(Vector3 from, Vector3 to, float clearanceRadius)
        {
            if (!IsAlive || targetKind != CastleTargetKind.Wall || !TryGetTurretBlockerBounds(out var bounds))
            {
                return false;
            }

            return CastleTurretLineOfFireMath.IntersectsPlanarBounds(from, to, bounds, clearanceRadius);
        }

        public bool TryGetTurretBlockerBounds(out Bounds bounds)
        {
            bounds = default;
            if (targetKind != CastleTargetKind.Wall)
            {
                return false;
            }

            ResolveReferences();
            var hasBounds = false;
            for (var index = 0; index < targetColliders.Length; index++)
            {
                var targetCollider = targetColliders[index];
                if (targetCollider == null || !targetCollider.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = targetCollider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(targetCollider.bounds);
                }
            }

            return hasBounds;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            CastleTargetKind kind,
            float healthValue,
            AttackSlotProvider slots,
            NavMeshObstacle obstacle)
        {
            Configure(kind, healthValue, slots, obstacle);
        }
#endif
    }

    public static class CastleTurretLineOfFireMath
    {
        public static bool IntersectsPlanarBounds(
            Vector3 from,
            Vector3 to,
            Bounds bounds,
            float clearanceRadius)
        {
            var expansion = Mathf.Max(0f, clearanceRadius);
            var minimum = new Vector2(bounds.min.x - expansion, bounds.min.z - expansion);
            var maximum = new Vector2(bounds.max.x + expansion, bounds.max.z + expansion);
            var start = new Vector2(from.x, from.z);
            var delta = new Vector2(to.x - from.x, to.z - from.z);
            var minimumTime = 0f;
            var maximumTime = 1f;
            return ClipAxis(start.x, delta.x, minimum.x, maximum.x, ref minimumTime, ref maximumTime) &&
                   ClipAxis(start.y, delta.y, minimum.y, maximum.y, ref minimumTime, ref maximumTime);
        }

        private static bool ClipAxis(
            float origin,
            float direction,
            float minimum,
            float maximum,
            ref float minimumTime,
            ref float maximumTime)
        {
            if (Mathf.Abs(direction) <= 0.00001f)
            {
                return origin >= minimum && origin <= maximum;
            }

            var first = (minimum - origin) / direction;
            var second = (maximum - origin) / direction;
            if (first > second)
            {
                (first, second) = (second, first);
            }

            minimumTime = Mathf.Max(minimumTime, first);
            maximumTime = Mathf.Min(maximumTime, second);
            return minimumTime <= maximumTime;
        }
    }

    public static class CastleBreachLinkMath
    {
        public static Vector3 ResolveInwardDirection(
            Vector3 wallPosition,
            Vector3 palacePosition,
            CastleWallNeighborMask neighborMask)
        {
            var towardPalace = palacePosition - wallPosition;
            towardPalace.y = 0f;
            if (towardPalace.sqrMagnitude <= 0.001f)
            {
                return Vector3.zero;
            }

            var hasHorizontalNeighbors = (neighborMask & (CastleWallNeighborMask.East | CastleWallNeighborMask.West)) != 0;
            var hasVerticalNeighbors = (neighborMask & (CastleWallNeighborMask.North | CastleWallNeighborMask.South)) != 0;
            if (hasHorizontalNeighbors && !hasVerticalNeighbors && Mathf.Abs(towardPalace.z) > 0.001f)
            {
                return new Vector3(0f, 0f, Mathf.Sign(towardPalace.z));
            }

            if (hasVerticalNeighbors && !hasHorizontalNeighbors && Mathf.Abs(towardPalace.x) > 0.001f)
            {
                return new Vector3(Mathf.Sign(towardPalace.x), 0f, 0f);
            }

            return Mathf.Abs(towardPalace.x) >= Mathf.Abs(towardPalace.z)
                ? new Vector3(Mathf.Sign(towardPalace.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(towardPalace.z));
        }

        public static Vector3 ResolveInwardDirectionFromAttackApproach(
            Vector3 wallPosition,
            Vector3 palacePosition,
            Vector3 attackPosition,
            CastleWallNeighborMask neighborMask)
        {
            var fallback = ResolveInwardDirection(wallPosition, palacePosition, neighborMask);
            var outsideDirection = attackPosition - wallPosition;
            outsideDirection.y = 0f;
            if (outsideDirection.sqrMagnitude <= 0.25f)
            {
                return fallback;
            }

            var attackInward = Mathf.Abs(outsideDirection.x) >= Mathf.Abs(outsideDirection.z)
                ? new Vector3(-Mathf.Sign(outsideDirection.x), 0f, 0f)
                : new Vector3(0f, 0f, -Mathf.Sign(outsideDirection.z));
            var towardPalace = palacePosition - wallPosition;
            towardPalace.y = 0f;
            return Vector3.Dot(attackInward, towardPalace) > 0.05f ? attackInward : fallback;
        }

        public static bool AreEndpointsOnOppositeSides(
            Vector3 wallPosition,
            Vector3 inward,
            Vector3 outside,
            Vector3 inside)
        {
            var outsideOffset = outside - wallPosition;
            var insideOffset = inside - wallPosition;
            outsideOffset.y = 0f;
            insideOffset.y = 0f;
            return Vector3.Dot(outsideOffset, inward) < -0.05f &&
                   Vector3.Dot(insideOffset, inward) > 0.05f;
        }

        public static Vector3 MoveAtConstantSpeed(
            Vector3 current,
            Vector3 destination,
            float speed,
            float deltaTime)
        {
            var maximumDistance = Mathf.Max(0f, speed) * Mathf.Max(0f, deltaTime);
            return Vector3.MoveTowards(current, destination, maximumDistance);
        }
    }
}
