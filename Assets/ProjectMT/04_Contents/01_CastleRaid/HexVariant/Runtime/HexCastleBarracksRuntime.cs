using System;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HexCastleCellRuntime))]
    public sealed class HexCastleBarracksRuntime : MonoBehaviour // 병영 Cell 생존 중에만 유닛을 보충한다
    {
        [SerializeField] private HexCastleCellRuntime structure;
        [SerializeField] private HexCastleGarrisonUnitRole unitRole;
        [SerializeField, Min(0.1f)] private float spawnInterval;
        [SerializeField, Min(0)] private int nearbySearchRadius;
        [SerializeField, Min(1)] private int nearbyMaximumCount;
        [SerializeField, Min(1)] private int unitsPerSpawn;
        [SerializeField] private bool isProducing;
        [SerializeField] private bool isRunning;

        private HexCastleGarrisonWorld world;
        private float remainingInterval;
        private bool hasProductionReservation;

        public HexCastleCellRuntime Structure => structure;
        public HexCastleGarrisonUnitRole UnitRole => unitRole;
        public bool IsRunning => isRunning && structure != null && structure.IsAlive;
        public bool IsProducing => IsRunning && isProducing;
        public float RemainingProductionSeconds => Mathf.Max(0f, remainingInterval);
        public int TotalSpawned { get; private set; }

        public void Configure(
            HexCastleCellRuntime linkedStructure,
            HexCastleGarrisonWorld targetWorld,
            HexCastleThemeOneTuning tuning)
        {
            structure = linkedStructure != null
                ? linkedStructure
                : throw new ArgumentNullException(nameof(linkedStructure));
            world = targetWorld != null
                ? targetWorld
                : throw new ArgumentNullException(nameof(targetWorld));
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            switch (structure.BuildingRole)
            {
                case HexCastleBuildingRole.KnightBarracks:
                    unitRole = HexCastleGarrisonUnitRole.Knight;
                    spawnInterval = tuning.KnightRefillInterval;
                    nearbySearchRadius = tuning.KnightSearchRadius;
                    nearbyMaximumCount = tuning.KnightMaximumNearbyCount;
                    unitsPerSpawn = tuning.KnightsPerRefill;
                    break;
                case HexCastleBuildingRole.FarmerBarracks:
                    unitRole = HexCastleGarrisonUnitRole.Farmer;
                    spawnInterval = tuning.FarmerSpawnInterval;
                    nearbySearchRadius = tuning.FarmerSearchRadius;
                    nearbyMaximumCount = tuning.FarmerMaximumNearbyCount;
                    unitsPerSpawn = tuning.FarmersPerSpawn;
                    break;
                default:
                    throw new ArgumentException(
                        $"병영 역할이 아닌 Cell은 소환 런타임을 가질 수 없습니다: {structure.BuildingRole}",
                        nameof(linkedStructure));
            }

            structure.Destroyed -= HandleStructureDestroyed;
            structure.Destroyed += HandleStructureDestroyed;
            remainingInterval = 0f;
            TotalSpawned = 0;
            isRunning = structure.IsAlive;
            isProducing = false;
            hasProductionReservation = false;
            TryBeginProduction();
        }

        public void Tick(float deltaTime)
        {
            if (!IsRunning || world == null)
            {
                return;
            }

            if (!isProducing)
            {
                TryBeginProduction();
                return;
            }

            remainingInterval -= Mathf.Max(0f, deltaTime);
            if (remainingInterval > 0f)
            {
                return;
            }

            ReleaseProductionReservation();
            isProducing = false;
            var currentCount = world.CountAlive(unitRole, structure.Coordinates, nearbySearchRadius);
            if (currentCount < nearbyMaximumCount)
            {
                var requestedCount = Mathf.Min(unitsPerSpawn, nearbyMaximumCount - currentCount);
                TotalSpawned += world.Spawn(unitRole, structure.Coordinates, requestedCount);
            }

            TryBeginProduction();
        }

        public void Shutdown()
        {
            if (structure != null)
            {
                structure.Destroyed -= HandleStructureDestroyed;
            }

            ReleaseProductionReservation();
            isProducing = false;
            isRunning = false;
            world = null;
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                Tick(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void HandleStructureDestroyed(HexCastleCellRuntime destroyedStructure)
        {
            ReleaseProductionReservation();
            isProducing = false;
            isRunning = false;
        }

        private void TryBeginProduction()
        {
            if (!IsRunning || isProducing || world == null ||
                !world.TryReserveProduction(
                    this,
                    unitRole,
                    structure.Coordinates,
                    nearbySearchRadius,
                    nearbyMaximumCount))
            {
                return;
            }

            hasProductionReservation = true;
            isProducing = true;
            remainingInterval = spawnInterval;
        }

        private void ReleaseProductionReservation()
        {
            if (!hasProductionReservation || world == null)
            {
                return;
            }

            world.ReleaseProductionReservation(this);
            hasProductionReservation = false;
        }
    }
}
