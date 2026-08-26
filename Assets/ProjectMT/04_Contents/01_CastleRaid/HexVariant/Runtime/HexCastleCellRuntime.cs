using System;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleCellRuntime : MonoBehaviour // 한 좌표의 판정·체력·길막을 소유한다
    {
        [SerializeField] private int q;
        [SerializeField] private int r;
        [SerializeField] private HexCastleCellKind kind;
        [SerializeField] private int defenseLayer;
        [SerializeField] private HexCastleWallRole wallRole;
        [SerializeField] private int regionId;
        [SerializeField] private int wallConnectionMask = -1;
        [SerializeField] private HexCastleBuildingRole buildingRole;
        [SerializeField] private HexCastlePlacementDensity placementDensity;
        [SerializeField] private int buildingGrade;
        [SerializeField] private HexCastleTurretWeaponKind turretWeaponKind;
        [SerializeField] private int turretRangeCells;
        [SerializeField] private bool turretCanAttackAcrossWalls;
        [SerializeField] private HexCastleGateRole gateRole;
        [SerializeField] private int gatePassageMask;
        [SerializeField] private HexCastleLootKind lootKind;
        [SerializeField] private bool initialBlocked;
        [SerializeField, Min(0f)] private float maxHealth;
        [SerializeField] private HealthComponent health;
        [SerializeField] private Collider footprintCollider;
        [SerializeField] private NavMeshObstacle navigationObstacle;
        [SerializeField] private Transform tileVisualRoot;
        [SerializeField] private Transform contentVisualRoot;
        [SerializeField] private Transform destroyedVisualRoot;

        private bool initialized;
        private bool isBlocked;
        private bool isDestroyed;

        public HexCoordinates Coordinates => new HexCoordinates(q, r);
        public HexCastleCellKind Kind => kind;
        public int DefenseLayer => defenseLayer;
        public HexCastleWallRole WallRole => wallRole;
        public int RegionId => regionId;
        public int WallConnectionMask => wallConnectionMask;
        public HexCastleBuildingRole BuildingRole => buildingRole;
        public HexCastlePlacementDensity PlacementDensity => placementDensity;
        public int BuildingGrade => buildingGrade;
        public HexCastleTurretWeaponKind TurretWeaponKind => turretWeaponKind;
        public int TurretRangeCells => turretRangeCells;
        public bool TurretCanAttackAcrossWalls => turretCanAttackAcrossWalls;
        public HexCastleGateRole GateRole => gateRole;
        public int GatePassageMask => gatePassageMask;
        public bool AllowsDefenderTraversal => gateRole == HexCastleGateRole.OpenDefenderPassage;
        public HexCastleLootKind LootKind => lootKind;
        public bool InitialBlocked => initialBlocked;
        public float MaxHealth => maxHealth;
        public float CurrentHealth => health == null ? 0f : health.CurrentHealth;
        public bool IsDamageable => initialBlocked && maxHealth > 0f;
        public bool IsAlive => IsDamageable && health != null && health.IsAlive;
        public bool IsBlocked => isBlocked;
        public bool IsDestroyed => isDestroyed;
        public HealthComponent Health => health;
        public Collider FootprintCollider => footprintCollider;
        public NavMeshObstacle NavigationObstacle => navigationObstacle;
        public Transform TileVisualRoot => tileVisualRoot;
        public Transform ContentVisualRoot => contentVisualRoot;
        public Transform DestroyedVisualRoot => destroyedVisualRoot;

        public event Action<HexCastleCellRuntime, DamageReport> Damaged;
        public event Action<HexCastleCellRuntime> Destroyed;
        public event Action<HexCastleCellRuntime, bool> BlockingChanged;

        public void Configure(
            HexCastleCell cell,
            HealthComponent targetHealth,
            Collider targetCollider,
            NavMeshObstacle targetObstacle,
            Transform targetTileVisualRoot,
            Transform targetContentVisualRoot,
            Transform targetDestroyedVisualRoot = null)
        {
            if (cell == null)
            {
                throw new ArgumentNullException(nameof(cell));
            }

            UnbindHealth();
            q = cell.Coordinates.Q;
            r = cell.Coordinates.R;
            kind = cell.Kind;
            defenseLayer = cell.DefenseLayer;
            wallRole = cell.WallRole;
            regionId = cell.RegionId;
            wallConnectionMask = cell.WallConnectionMask;
            buildingRole = cell.BuildingRole;
            placementDensity = cell.PlacementDensity;
            buildingGrade = cell.BuildingGrade;
            turretWeaponKind = cell.TurretWeaponKind;
            turretRangeCells = cell.TurretRangeCells;
            turretCanAttackAcrossWalls = cell.TurretCanAttackAcrossWalls;
            gateRole = cell.GateRole;
            gatePassageMask = cell.GatePassageMask;
            lootKind = cell.LootKind;
            initialBlocked = cell.InitialBlocked;
            maxHealth = Mathf.Max(0f, cell.MaxHealth);
            health = targetHealth;
            footprintCollider = targetCollider;
            navigationObstacle = targetObstacle;
            tileVisualRoot = targetTileVisualRoot;
            contentVisualRoot = targetContentVisualRoot;
            destroyedVisualRoot = targetDestroyedVisualRoot;
            ValidateOwnership();
            InitializeState();
        }

        public void InitializeState()
        {
            UnbindHealth();
            HideHealthBar();
            isDestroyed = false;
            isBlocked = initialBlocked;
            if (IsDamageable)
            {
                health.Initialize(maxHealth);
                BindHealth();
            }

            SetBlockingEnabled(isBlocked);
            SetVisualState(false);
            initialized = true;
        }

        public bool ApplyDamage(float amount, Vector3 hitPoint)
        {
            if (!initialized || !IsAlive || amount <= 0f)
            {
                return false;
            }

            return health.ApplyDamage(new DamageRequest(null, amount, hitPoint));
        }

        public bool CanTraverse(HexCastleTraversalFaction faction)
        {
            return !isBlocked ||
                   faction == HexCastleTraversalFaction.Defender && AllowsDefenderTraversal;
        }

        public bool CanEnterFrom(int direction, HexCastleTraversalFaction faction)
        {
            if (!isBlocked)
            {
                return true;
            }

            return faction == HexCastleTraversalFaction.Defender &&
                   AllowsDefenderTraversal &&
                   direction >= 0 && direction < HexCoordinates.Directions.Length &&
                   (gatePassageMask & 1 << direction) != 0;
        }

        public bool CanTraverseBetween(
            int entryDirection,
            int exitDirection,
            HexCastleTraversalFaction faction)
        {
            if (!isBlocked)
            {
                return true;
            }

            return entryDirection != exitDirection &&
                   CanEnterFrom(entryDirection, faction) &&
                   CanEnterFrom(exitDirection, faction);
        }

        public void Shutdown()
        {
            UnbindHealth();
            HideHealthBar();
            initialized = false;
            Damaged = null;
            Destroyed = null;
            BlockingChanged = null;
        }

        private void OnDisable()
        {
            UnbindHealth();
        }

        private void OnEnable()
        {
            if (initialized && IsDamageable)
            {
                BindHealth();
            }
        }

        private void HandleDamaged(DamageReport report)
        {
            HexCastleOverheadHealthBar.ShowDamage(transform, health);
            Damaged?.Invoke(this, report);
        }

        private void HandleDied(DamageReport report)
        {
            if (isDestroyed)
            {
                return;
            }

            isDestroyed = true;
            HideHealthBar();
            SetBlocked(false);
            SetVisualState(true);
            Destroyed?.Invoke(this);
        }

        private void SetBlocked(bool value)
        {
            if (isBlocked == value)
            {
                SetBlockingEnabled(value);
                return;
            }

            isBlocked = value;
            SetBlockingEnabled(value);
            BlockingChanged?.Invoke(this, value);
        }

        private void SetBlockingEnabled(bool value)
        {
            if (footprintCollider != null)
            {
                footprintCollider.enabled = value;
            }

            if (navigationObstacle != null)
            {
                navigationObstacle.carving = value;
                navigationObstacle.enabled = value;
            }
        }

        private void SetVisualState(bool destroyed)
        {
            if (tileVisualRoot != null)
            {
                tileVisualRoot.gameObject.SetActive(true);
            }

            if (contentVisualRoot != null)
            {
                contentVisualRoot.gameObject.SetActive(!destroyed);
            }

            if (destroyedVisualRoot != null)
            {
                destroyedVisualRoot.gameObject.SetActive(destroyed);
            }
        }

        private void ValidateOwnership()
        {
            if (tileVisualRoot == null || tileVisualRoot == transform || !tileVisualRoot.IsChildOf(transform))
            {
                throw new InvalidOperationException($"Cell {Coordinates}의 TileVisualRoot가 Cell Root 자식이 아닙니다.");
            }

            ValidateVisualRoot(tileVisualRoot);
            ValidateVisualRoot(contentVisualRoot);
            ValidateVisualRoot(destroyedVisualRoot);

            if (!initialBlocked)
            {
                if (maxHealth > 0f || health != null || footprintCollider != null || navigationObstacle != null ||
                    GetComponents<HealthComponent>().Length != 0 || GetComponents<Collider>().Length != 0 ||
                    GetComponents<NavMeshObstacle>().Length != 0)
                {
                    throw new InvalidOperationException($"열린 Cell {Coordinates}에 체력·충돌·길막이 있습니다.");
                }

                return;
            }

            if (maxHealth <= 0f || health == null || footprintCollider == null || navigationObstacle == null)
            {
                throw new InvalidOperationException($"차단 Cell {Coordinates}의 체력·충돌·길막 구성이 불완전합니다.");
            }

            if (health.transform != transform || footprintCollider.transform != transform ||
                navigationObstacle.transform != transform)
            {
                throw new InvalidOperationException($"Cell {Coordinates}의 판정 Component는 같은 Cell Root에 있어야 합니다.");
            }

            if (GetComponents<HexCastleCellRuntime>().Length != 1 ||
                GetComponents<HealthComponent>().Length != 1 ||
                GetComponents<Collider>().Length != 1 ||
                GetComponents<NavMeshObstacle>().Length != 1)
            {
                throw new InvalidOperationException($"Cell {Coordinates}은 판정·체력·충돌·길막을 각각 하나만 소유해야 합니다.");
            }
        }

        private static void ValidateVisualRoot(Transform visualRoot)
        {
            if (visualRoot == null)
            {
                return;
            }

            if (visualRoot.GetComponentInChildren<HealthComponent>(true) != null ||
                visualRoot.GetComponentInChildren<Collider>(true) != null ||
                visualRoot.GetComponentInChildren<NavMeshObstacle>(true) != null ||
                visualRoot.GetComponentInChildren<HexCastleCellRuntime>(true) != null)
            {
                throw new InvalidOperationException($"Visual Root {visualRoot.name} 안에 게임플레이 Component가 있습니다.");
            }
        }

        private void UnbindHealth()
        {
            if (health == null)
            {
                return;
            }

            health.Damaged -= HandleDamaged;
            health.Died -= HandleDied;
        }

        private void BindHealth()
        {
            if (health == null)
            {
                return;
            }

            health.Damaged -= HandleDamaged;
            health.Died -= HandleDied;
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
        }

        private void HideHealthBar()
        {
            if (TryGetComponent<HexCastleOverheadHealthBar>(out var healthBar))
            {
                healthBar.HideImmediately();
            }
        }
    }
}
