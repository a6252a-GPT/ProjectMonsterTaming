using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleAssaultUnit : MonoBehaviour // Hex Cell 전략과 실행을 분리한 공격 유닛
    {
        private const float TargetAwarenessInterval = 0.45f;
        private const float ThreatMemorySeconds = 3.5f;

        private IReadOnlyList<HexCoordinates> legacyPath;
        private IReadOnlyList<HexCoordinates> movementPath = Array.Empty<HexCoordinates>();
        private IReadOnlyDictionary<HexCoordinates, HexCastleCellRuntime> cellTargets;
        private HexCastleAssaultWorld assaultWorld;
        private HexCastleAssaultTarget currentTarget;
        private HexCastleAssaultTarget committedTarget;
        private HexCastleAssaultTarget recentThreat;
        private HexCastleAssaultIntentKind currentIntent;
        private HexCastleAssaultIntentKind committedIntent;
        private HexCastleAssaultSupportAction currentSupportAction;
        private HexCastleAssaultAIProfile aiProfile;
        private float cellSize;
        private Vector3 worldOrigin;
        private float moveSpeed;
        private float attackDamage;
        private float attackInterval;
        private float attackRange;
        private float nextAttackTime;
        private int pathIndex;
        private bool active;
        private float maximumHealth;
        private float currentHealth;
        private Renderer unitRenderer;
        private Vector3 baseScale;
        private float groundOffset = 0.42f;
        private MonsterAnimationDriver animationDriver;
        private MonsterRuntimeAssetSet runtimeAssetSet;
        private UnitVisualFeedback visualFeedback;
        private bool usesFormalVisual;
        private bool attackActionRunning;
        private int nextActionSequenceId;
        private HexCoordinates pendingLegacyAttackCoordinates;
        private HexCastleAssaultTarget pendingTarget;
        private Vector3 pendingAttackPosition;
        private bool strategicDecisionRequested;
        private float nextAwarenessAt;
        private int decisionTopologyVersion;
        private float recentThreatRemaining;
        private float recentDamagePerSecond;
        private float attackBuffRemaining;
        private float attackDamageMultiplier = 1f;
        private float defenseBuffRemaining;
        private float incomingDamageMultiplier = 1f;
        private float supportCooldownRemaining;
        private float trapMovementLockRemaining;
        private float trapSlowRemaining;
        private float trapMoveSpeedMultiplier = 1f;
        private bool dynamicRuntime;
        private string unitId = string.Empty;
        private readonly HashSet<int> evaluatedOpportunityLayers = new HashSet<int>();
        private readonly Dictionary<int, int> specialistTargetCounts = new Dictionary<int, int>();
        private readonly HexMonsterPassiveRuntime passiveRuntime = new HexMonsterPassiveRuntime();

        public bool ReachedPalace { get; private set; }
        public HexCoordinates CurrentCoordinates { get; private set; }
        public int DestroyedTargets { get; private set; }
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maximumHealth;
        public float HealthRatio => maximumHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maximumHealth);
        public bool IsAlive => currentHealth > 0f;
        public bool UsesFormalVisual => usesFormalVisual;
        public bool HasFormalAnimation => usesFormalVisual && animationDriver != null && animationDriver.IsReady;
        public float DeathPresentationDuration { get; private set; } = 0.38f;
        public float MoveSpeed => moveSpeed;
        public float TrapMovementLockRemaining => trapMovementLockRemaining;
        public float TrapSlowRemaining => trapSlowRemaining;
        public float CurrentMoveSpeedMultiplier => trapSlowRemaining > 0f
            ? trapMoveSpeedMultiplier
            : 1f;
        public float EstimatedDamagePerSecond => Mathf.Max(0.1f, attackDamage * attackDamageMultiplier) /
                                                  passiveRuntime.ResolveAttackInterval(attackInterval);
        public float BaseAttackDamage => attackDamage;
        public HexMonsterPassiveRuntime PassiveRuntime => passiveRuntime;
        public float RecentDamagePerSecond => recentDamagePerSecond;
        public bool HasAttackBuff => attackBuffRemaining > 0f;
        public bool HasDefenseBuff => defenseBuffRemaining > 0f;
        public bool CanPerformSupportAction => supportCooldownRemaining <= 0f;
        public bool HasCombatTarget => currentTarget.IsValid &&
                                       currentTarget.Kind != HexCastleAssaultTargetKind.Ally;
        public int AttackRangeCells => Mathf.Max(
            1,
            Mathf.CeilToInt(attackRange / Mathf.Max(0.1f, cellSize * 1.7320508f)));
        public int SupportRangeCells => aiProfile == null
            ? 1
            : Mathf.Max(
                1,
                Mathf.CeilToInt(aiProfile.SupportRange / Mathf.Max(0.1f, cellSize * 1.7320508f)));
        public int ExpectedDefenseLayer { get; private set; }
        public int RouteId { get; private set; }
        public int RouteSector { get; private set; }
        public HexCastleAssaultAIProfile AIProfile => aiProfile;
        public HexCastleAssaultTarget CurrentTarget => currentTarget;
        public HexCastleAssaultTarget CommittedTarget => committedTarget.IsValid ? committedTarget : default;
        public HexCastleAssaultIntentKind CurrentIntent => currentIntent;
        public HexCastleAssaultIntentKind CommittedIntent => committedIntent;
        public HexCastleAssaultSupportAction CurrentSupportAction => currentSupportAction;
        public bool HasSelectedInitialWall { get; private set; }
        public HexCastleAssaultTarget RecentThreat => recentThreatRemaining > 0f && recentThreat.IsValid
            ? recentThreat
            : default;
        public bool NeedsStrategicDecision => dynamicRuntime && active && IsAlive && !attackActionRunning &&
                                              (strategicDecisionRequested || !currentTarget.IsValid ||
                                               Time.time >= nextAwarenessAt ||
                                               assaultWorld != null &&
                                               decisionTopologyVersion != assaultWorld.TopologyVersion);

        public event Action<HexCastleAssaultUnit, DamageReport> Damaged;
        public event Action<HexCastleAssaultUnit> Died;
        public event Action<HexCastleAssaultUnit, HexCoordinates> EnteredCell;

        public void ConfigureForRoute(
            HexRouteResult route,
            float targetCellSize,
            float targetMoveSpeed,
            float targetAttackDamage,
            float targetAttackInterval,
            float targetHealth = 320f)
        {
            ConfigureLegacy(
                route,
                null,
                targetCellSize,
                Vector3.zero,
                targetMoveSpeed,
                targetAttackDamage,
                targetAttackInterval,
                targetHealth,
                0.42f,
                null,
                targetCellSize * 0.82f);
        }

        public void ConfigureForCells(
            HexRouteResult route,
            IReadOnlyDictionary<HexCoordinates, HexCastleCellRuntime> runtimeCells,
            float targetCellSize,
            Vector3 targetWorldOrigin,
            float targetMoveSpeed,
            float targetAttackDamage,
            float targetAttackInterval,
            float targetHealth = 320f)
        {
            ConfigureLegacy(
                route,
                runtimeCells,
                targetCellSize,
                targetWorldOrigin,
                targetMoveSpeed,
                targetAttackDamage,
                targetAttackInterval,
                targetHealth,
                0.42f,
                null,
                targetCellSize * 0.82f);
        }

        public void ConfigureForPartyUnit(
            HexRouteResult route,
            IReadOnlyDictionary<HexCoordinates, HexCastleCellRuntime> runtimeCells,
            float targetCellSize,
            Vector3 targetWorldOrigin,
            BattleUnitSnapshot unit)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            var stats = unit.Stats;
            ConfigureLegacy(
                route,
                runtimeCells,
                targetCellSize,
                targetWorldOrigin,
                Mathf.Max(0.1f, stats.moveSpeed),
                Mathf.Max(1f, stats.damage),
                Mathf.Max(0.05f, stats.attackInterval),
                Mathf.Max(1f, stats.maxHealth),
                0.02f,
                unit,
                Mathf.Max(0.35f, stats.attackRange));
        }

        public void ConfigureForPartyUnit(
            HexCastleAssaultWorld world,
            HexCoordinates start,
            IReadOnlyDictionary<HexCoordinates, HexCastleCellRuntime> runtimeCells,
            float targetCellSize,
            Vector3 targetWorldOrigin,
            BattleUnitSnapshot unit)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
            if (runtimeCells == null)
            {
                throw new ArgumentNullException(nameof(runtimeCells));
            }
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            ShutdownRuntime();
            assaultWorld = world;
            cellTargets = runtimeCells;
            cellSize = Mathf.Max(0.1f, targetCellSize);
            worldOrigin = targetWorldOrigin;
            unitId = unit.UnitId ?? string.Empty;
            var stats = unit.Stats;
            ConfigureCommon(
                Mathf.Max(0.1f, stats.moveSpeed),
                Mathf.Max(1f, stats.damage),
                Mathf.Max(0.05f, stats.attackInterval),
                Mathf.Max(1f, stats.maxHealth),
                0.02f,
                unit,
                Mathf.Max(0.35f, stats.attackRange));
            dynamicRuntime = true;
            legacyPath = null;
            movementPath = new[] { start };
            pathIndex = 0;
            CurrentCoordinates = start;
            ExpectedDefenseLayer = assaultWorld.DefenseLayerCount;
            aiProfile = assaultWorld.RegisterUnit(this, unitId);
            transform.position = ResolvePosition(start);
            passiveRuntime.Initialize(
                this,
                assaultWorld,
                unit.PassiveSkill,
                unit.Level,
                UnitEntryReason.CastleManualDeployment);
            strategicDecisionRequested = true;
            nextAwarenessAt = Time.time + ResolveDecisionSpread();
            decisionTopologyVersion = 0;
            active = true;
        }

        public void RefreshStrategicDecision()
        {
            if (!NeedsStrategicDecision || assaultWorld == null)
            {
                return;
            }

            strategicDecisionRequested = false;
            nextAwarenessAt = Time.time + TargetAwarenessInterval + ResolveDecisionSpread();
            if (!assaultWorld.TryResolveDecision(this, out var decision))
            {
                if (!currentTarget.IsValid)
                {
                    movementPath = new[] { CurrentCoordinates };
                    pathIndex = 0;
                    animationDriver?.PlayIdle();
                }
                return;
            }

            var startsNewCommitment = IsCommitmentIntent(decision.Intent) &&
                                      (!committedTarget.IsValid ||
                                       committedTarget.InstanceId != decision.Target.InstanceId ||
                                       committedIntent != decision.Intent);
            currentTarget = decision.Target;
            currentIntent = decision.Intent;
            currentSupportAction = decision.SupportAction;
            if (startsNewCommitment)
            {
                committedTarget = decision.Target;
                committedIntent = decision.Intent;
                if (decision.Intent == HexCastleAssaultIntentKind.InitialBreach)
                {
                    HasSelectedInitialWall = true;
                }
                else if (decision.Intent == HexCastleAssaultIntentKind.Specialist)
                {
                    specialistTargetCounts.TryGetValue(ExpectedDefenseLayer, out var count);
                    specialistTargetCounts[ExpectedDefenseLayer] = count + 1;
                }
            }
            movementPath = decision.MovementPath;
            pathIndex = 0;
            RouteId = decision.RouteId;
            RouteSector = decision.SectorId;
            decisionTopologyVersion = decision.TopologyVersion;
            if (movementPath.Count > 0 && movementPath[0] != CurrentCoordinates)
            {
                movementPath = new[] { CurrentCoordinates }.Concat(movementPath).ToArray();
            }
        }

        public void RequestStrategicDecision(bool immediate)
        {
            strategicDecisionRequested = true;
            if (immediate)
            {
                nextAwarenessAt = 0f;
            }
        }

        public bool HasEvaluatedOpportunity(int defenseLayer)
        {
            return evaluatedOpportunityLayers.Contains(defenseLayer);
        }

        public void MarkOpportunityEvaluated(int defenseLayer)
        {
            evaluatedOpportunityLayers.Add(defenseLayer);
        }

        public bool CanSelectSpecialistTarget(int defenseLayer, int maximumCount)
        {
            specialistTargetCounts.TryGetValue(defenseLayer, out var count);
            return count < Mathf.Max(0, maximumCount);
        }

        public bool ApplyDamage(float amount)
        {
            return ApplyDamage(amount, transform.position + Vector3.up * groundOffset);
        }

        public bool ApplyDamage(float amount, Vector3 hitPoint)
        {
            return ApplyDamage(amount, hitPoint, null, null);
        }

        public bool ApplyDamage(
            float amount,
            Vector3 hitPoint,
            HexCastleGarrisonUnit sourceDefender,
            HexCastleCellRuntime sourceStructure)
        {
            if (!IsAlive || amount <= 0f)
            {
                return false;
            }

            if (sourceDefender != null && sourceDefender.IsAlive)
            {
                recentThreat = new HexCastleAssaultTarget(sourceDefender);
                recentThreatRemaining = ThreatMemorySeconds;
                assaultWorld?.ReportThreat(this, recentThreat);
                RequestStrategicDecision(true);
            }
            else if (sourceStructure != null && sourceStructure.IsAlive)
            {
                recentThreat = new HexCastleAssaultTarget(sourceStructure, false);
                recentThreatRemaining = ThreatMemorySeconds;
                assaultWorld?.ReportThreat(this, recentThreat);
                RequestStrategicDecision(true);
            }

            var requested = passiveRuntime.ResolveIncomingDamage(
                amount * incomingDamageMultiplier,
                out var shieldAbsorbed);
            if (requested <= 0f && shieldAbsorbed > 0f)
            {
                visualFeedback?.PlayHit();
                return true;
            }
            var appliedDamage = Mathf.Min(currentHealth, requested);
            currentHealth = Mathf.Max(0f, currentHealth - appliedDamage);
            recentDamagePerSecond += appliedDamage / 2.5f;
            var killed = currentHealth <= 0f;
            var report = new DamageReport(
                new DamageRequest(null, amount, hitPoint),
                appliedDamage,
                currentHealth,
                killed);
            visualFeedback?.PlayHit();
            HexCastleOverheadHealthBar.ShowDamage(
                transform,
                currentHealth,
                maximumHealth,
                true);
            if (!usesFormalVisual)
            {
                transform.localScale = baseScale * Mathf.Lerp(0.72f, 1f, currentHealth / maximumHealth);
            }
            Damaged?.Invoke(this, report);
            if (!killed)
            {
                return true;
            }

            active = false;
            attackActionRunning = false;
            HideHealthBar();
            assaultWorld?.UnregisterUnit(this);
            if (usesFormalVisual)
            {
                DeathPresentationDuration = animationDriver?.PlayDeath() ?? 0.38f;
            }
            else if (unitRenderer != null)
            {
                unitRenderer.enabled = false;
            }

            Died?.Invoke(this);
            return true;
        }

        public void ApplyTrapMovementLock(float duration)
        {
            if (IsAlive)
            {
                trapMovementLockRemaining = Mathf.Max(trapMovementLockRemaining, Mathf.Max(0f, duration));
            }
        }

        public void ApplyTrapSlow(float movementSpeedMultiplier, float duration)
        {
            if (!IsAlive || duration <= 0f)
            {
                return;
            }

            trapMoveSpeedMultiplier = Mathf.Min(
                trapSlowRemaining > 0f ? trapMoveSpeedMultiplier : 1f,
                Mathf.Clamp(movementSpeedMultiplier, 0.1f, 1f));
            trapSlowRemaining = Mathf.Max(trapSlowRemaining, duration);
        }

        public void ApplySupport(
            HexCastleAssaultSupportAction action,
            HexCastleAssaultAIProfile sourceProfile)
        {
            if (!IsAlive || sourceProfile == null)
            {
                return;
            }

            switch (action)
            {
                case HexCastleAssaultSupportAction.Heal:
                    currentHealth = Mathf.Min(
                        maximumHealth,
                        currentHealth + maximumHealth * sourceProfile.HealRatio);
                    break;
                case HexCastleAssaultSupportAction.AttackBuff:
                    attackDamageMultiplier = Mathf.Max(
                        attackDamageMultiplier,
                        1f + sourceProfile.AttackBuffRate);
                    attackBuffRemaining = Mathf.Max(attackBuffRemaining, sourceProfile.SupportDuration);
                    break;
                case HexCastleAssaultSupportAction.DefenseBuff:
                    incomingDamageMultiplier = Mathf.Min(
                        incomingDamageMultiplier,
                        sourceProfile.DefenseDamageMultiplier);
                    defenseBuffRemaining = Mathf.Max(defenseBuffRemaining, sourceProfile.SupportDuration);
                    break;
            }
        }

        public float HealPassive(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return 0f;
            }

            var before = currentHealth;
            currentHealth = Mathf.Min(maximumHealth, currentHealth + amount);
            return currentHealth - before;
        }

        public void ShutdownRuntime()
        {
            passiveRuntime.Shutdown();
            HideHealthBar();
            assaultWorld?.UnregisterUnit(this);
            assaultWorld = null;
            currentTarget = default;
            committedTarget = default;
            recentThreat = default;
            currentIntent = HexCastleAssaultIntentKind.None;
            committedIntent = HexCastleAssaultIntentKind.None;
            currentSupportAction = HexCastleAssaultSupportAction.None;
            movementPath = Array.Empty<HexCoordinates>();
            legacyPath = null;
            cellTargets = null;
            aiProfile = null;
            runtimeAssetSet = null;
            evaluatedOpportunityLayers.Clear();
            specialistTargetCounts.Clear();
            HasSelectedInitialWall = false;
            dynamicRuntime = false;
            strategicDecisionRequested = false;
            active = false;
            trapMovementLockRemaining = 0f;
            trapSlowRemaining = 0f;
            trapMoveSpeedMultiplier = 1f;
            EnteredCell = null;
        }

        private void HideHealthBar()
        {
            if (TryGetComponent<HexCastleOverheadHealthBar>(out var healthBar))
            {
                healthBar.HideImmediately();
            }
        }

        private void ConfigureLegacy(
            HexRouteResult route,
            IReadOnlyDictionary<HexCoordinates, HexCastleCellRuntime> runtimeCells,
            float targetCellSize,
            Vector3 targetWorldOrigin,
            float targetMoveSpeed,
            float targetAttackDamage,
            float targetAttackInterval,
            float targetHealth,
            float targetGroundOffset,
            BattleUnitSnapshot unit,
            float targetAttackRange)
        {
            if (route == null || !route.IsComplete)
            {
                throw new ArgumentException("완전한 육각 돌파 경로가 필요합니다.", nameof(route));
            }

            ShutdownRuntime();
            legacyPath = route.Path;
            movementPath = Array.Empty<HexCoordinates>();
            cellTargets = runtimeCells;
            cellSize = Mathf.Max(0.1f, targetCellSize);
            worldOrigin = targetWorldOrigin;
            ConfigureCommon(
                targetMoveSpeed,
                targetAttackDamage,
                targetAttackInterval,
                targetHealth,
                targetGroundOffset,
                unit,
                targetAttackRange);
            dynamicRuntime = false;
            pathIndex = 0;
            CurrentCoordinates = legacyPath[0];
            transform.position = ResolvePosition(CurrentCoordinates);
            active = true;
        }

        private void ConfigureCommon(
            float targetMoveSpeed,
            float targetAttackDamage,
            float targetAttackInterval,
            float targetHealth,
            float targetGroundOffset,
            BattleUnitSnapshot unit,
            float targetAttackRange)
        {
            moveSpeed = Mathf.Max(0.1f, targetMoveSpeed);
            attackDamage = Mathf.Max(1f, targetAttackDamage);
            attackInterval = Mathf.Max(0.05f, targetAttackInterval);
            attackRange = Mathf.Max(0.35f, targetAttackRange);
            maximumHealth = Mathf.Max(1f, targetHealth);
            currentHealth = maximumHealth;
            unitRenderer = GetComponentInChildren<Renderer>();
            baseScale = transform.localScale;
            groundOffset = Mathf.Max(0f, targetGroundOffset);
            usesFormalVisual = unit != null;
            runtimeAssetSet = unit?.RuntimeAssetSet;
            attackActionRunning = false;
            nextActionSequenceId = 0;
            nextAttackTime = Time.time + UnityEngine.Random.Range(0f, attackInterval * 0.35f);
            DeathPresentationDuration = 0.38f;
            ReachedPalace = false;
            DestroyedTargets = 0;
            RouteId = 0;
            RouteSector = 0;
            ExpectedDefenseLayer = 0;
            recentThreatRemaining = 0f;
            recentDamagePerSecond = 0f;
            attackBuffRemaining = 0f;
            attackDamageMultiplier = 1f;
            defenseBuffRemaining = 0f;
            incomingDamageMultiplier = 1f;
            supportCooldownRemaining = 0f;
            trapMovementLockRemaining = 0f;
            trapSlowRemaining = 0f;
            trapMoveSpeedMultiplier = 1f;
            visualFeedback = GetComponent<UnitVisualFeedback>();
            animationDriver = GetComponent<MonsterAnimationDriver>();
            if (!usesFormalVisual)
            {
                return;
            }

            var actor = GetComponent<UnitActor>();
            if (actor != null)
            {
                actor.enabled = false;
            }

            visualFeedback?.SetTint(unit.VisualTint);
            if (animationDriver != null && !animationDriver.Initialize(unit.RuntimeAssetSet))
            {
                animationDriver = null;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || !active || !IsAlive)
            {
                return;
            }

            TickRuntimeEffects(Time.deltaTime);
            passiveRuntime.Tick(Time.deltaTime);
            if (dynamicRuntime)
            {
                TickDynamicRuntime(Time.deltaTime);
            }
            else
            {
                TickLegacyRuntime(Time.deltaTime);
            }
        }

        private void TickDynamicRuntime(float deltaTime)
        {
            if (attackActionRunning)
            {
                TickAttackAction(deltaTime);
                return;
            }

            if (movementPath != null && pathIndex < movementPath.Count - 1)
            {
                var nextCoordinates = movementPath[pathIndex + 1];
                if (!CanAssaultTraverse(nextCoordinates))
                {
                    RequestStrategicDecision(true);
                    animationDriver?.PlayIdle();
                    return;
                }

                var destination = ResolvePosition(nextCoordinates);
                MoveTowards(destination, deltaTime);
                if (Vector3.SqrMagnitude(transform.position - destination) > 0.015f)
                {
                    return;
                }

                pathIndex++;
                CurrentCoordinates = nextCoordinates;
                EnteredCell?.Invoke(this, nextCoordinates);
                UpdateDefenseProgress(nextCoordinates);
                return;
            }

            if (!currentTarget.IsValid)
            {
                RequestStrategicDecision(true);
                animationDriver?.PlayIdle();
                return;
            }

            if (currentTarget.Kind == HexCastleAssaultTargetKind.Ally)
            {
                TickSupportTarget();
                return;
            }

            if (!CanAttackTarget(currentTarget))
            {
                RequestStrategicDecision(true);
                animationDriver?.PlayIdle();
                return;
            }

            animationDriver?.PlayIdle();
            FaceTowards(ResolveTargetPosition(currentTarget), deltaTime);
            if (Time.time >= nextAttackTime)
            {
                StartDynamicAttack(currentTarget);
            }
        }

        private void TickLegacyRuntime(float deltaTime)
        {
            if (legacyPath == null || pathIndex >= legacyPath.Count - 1)
            {
                return;
            }

            var nextCoordinates = legacyPath[pathIndex + 1];
            if (attackActionRunning)
            {
                TickAttackAction(deltaTime);
                return;
            }

            if (!CanAssaultTraverse(nextCoordinates))
            {
                if (!HasAliveTarget(nextCoordinates))
                {
                    return;
                }

                var targetPosition = ResolvePosition(nextCoordinates);
                if (PlanarDistance(transform.position, targetPosition) > attackRange)
                {
                    MoveTowards(targetPosition, deltaTime);
                    return;
                }

                animationDriver?.PlayIdle();
                if (Time.time >= nextAttackTime)
                {
                    StartLegacyAttack(nextCoordinates, targetPosition);
                }
                return;
            }

            var destination = ResolvePosition(nextCoordinates);
            MoveTowards(destination, deltaTime);
            if (Vector3.SqrMagnitude(transform.position - destination) > 0.015f)
            {
                return;
            }

            pathIndex++;
            CurrentCoordinates = nextCoordinates;
            EnteredCell?.Invoke(this, nextCoordinates);
            if (nextCoordinates == new HexCoordinates(0, 0))
            {
                ReachedPalace = true;
                active = false;
                animationDriver?.PlayIdle(true);
            }
        }

        private void TickSupportTarget()
        {
            var ally = currentTarget.Ally;
            if (ally == null || !ally.IsAlive)
            {
                currentTarget = default;
                RequestStrategicDecision(true);
                return;
            }

            if (CurrentCoordinates.DistanceTo(ally.CurrentCoordinates) > SupportRangeCells)
            {
                RequestStrategicDecision(true);
                animationDriver?.PlayIdle();
                return;
            }

            FaceTowards(ally.transform.position, Time.deltaTime);
            if (currentSupportAction == HexCastleAssaultSupportAction.None ||
                supportCooldownRemaining > 0f || aiProfile == null)
            {
                currentTarget = default;
                currentSupportAction = HexCastleAssaultSupportAction.None;
                RequestStrategicDecision(true); // 지원할 일이 없으면 일반 진격으로 즉시 복귀한다
                animationDriver?.PlayIdle();
                return;
            }

            ally.ApplySupport(currentSupportAction, aiProfile);
            assaultWorld?.CommitSupportDecision(
                this,
                ally,
                currentSupportAction,
                aiProfile.SupportCooldown);
            supportCooldownRemaining = aiProfile.SupportCooldown;
            currentTarget = default;
            currentSupportAction = HexCastleAssaultSupportAction.None;
            RequestStrategicDecision(true); // 쿨다운 동안 아군 옆에서 멈추지 않는다
            animationDriver?.PlayIdle(true);
        }

        private void UpdateDefenseProgress(HexCoordinates coordinates)
        {
            if (ExpectedDefenseLayer <= 0 || cellTargets == null ||
                !cellTargets.TryGetValue(coordinates, out var cell) || cell == null ||
                cell.WallRole == HexCastleWallRole.Partition ||
                cell.DefenseLayer != ExpectedDefenseLayer)
            {
                return;
            }

            ExpectedDefenseLayer = Mathf.Max(0, ExpectedDefenseLayer - 1);
            RequestStrategicDecision(true);
        }

        private bool CanAttackTarget(HexCastleAssaultTarget target)
        {
            if (!target.IsValid)
            {
                return false;
            }

            if (target.Kind == HexCastleAssaultTargetKind.Palace)
            {
                return CurrentCoordinates.DistanceTo(target.Coordinates) <=
                       HexCastleFoundationGenerator.PalaceFootprintRadius + 1;
            }

            if (target.Kind == HexCastleAssaultTargetKind.Ally)
            {
                return CurrentCoordinates.DistanceTo(target.Coordinates) <= SupportRangeCells;
            }

            return CurrentCoordinates.DistanceTo(target.Coordinates) <= AttackRangeCells &&
                   (assaultWorld == null || assaultWorld.IsAttackLaneOpen(CurrentCoordinates, target));
        }

        private void MoveTowards(Vector3 destination, float deltaTime)
        {
            if (trapMovementLockRemaining > 0f)
            {
                animationDriver?.PlayIdle();
                return;
            }

            FaceTowards(destination, deltaTime);
            animationDriver?.PlayMove();
            transform.position = Vector3.MoveTowards(
                transform.position,
                destination,
                moveSpeed * CurrentMoveSpeedMultiplier * deltaTime);
        }

        private void FaceTowards(Vector3 destination, float deltaTime)
        {
            var direction = destination - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction.normalized, Vector3.up),
                deltaTime * 12f);
        }

        private Vector3 ResolvePosition(HexCoordinates coordinates)
        {
            return worldOrigin + coordinates.ToWorld(cellSize) + Vector3.up * groundOffset;
        }

        private Vector3 ResolveTargetPosition(HexCastleAssaultTarget target)
        {
            if (target.Defender != null)
            {
                return target.Defender.transform.position;
            }

            if (target.Ally != null)
            {
                return target.Ally.transform.position;
            }

            return target.Structure != null ? target.Structure.transform.position : transform.position;
        }

        private void StartDynamicAttack(HexCastleAssaultTarget target)
        {
            nextAttackTime = Time.time + passiveRuntime.ResolveAttackInterval(attackInterval);
            pendingTarget = target;
            pendingAttackPosition = ResolveTargetPosition(target);
            BeginAttackAction();
        }

        private void StartLegacyAttack(HexCoordinates coordinates, Vector3 hitPoint)
        {
            nextAttackTime = Time.time + passiveRuntime.ResolveAttackInterval(attackInterval);
            pendingLegacyAttackCoordinates = coordinates;
            pendingAttackPosition = hitPoint;
            pendingTarget = default;
            BeginAttackAction();
        }

        private void BeginAttackAction()
        {
            if (usesFormalVisual && animationDriver != null && animationDriver.IsReady)
            {
                attackActionRunning = true;
                var basicAttackProfile = runtimeAssetSet?.CombatProfile?.Action?.BasicAttackProfile;
                var breathDuration = basicAttackProfile != null && basicAttackProfile.UsesBreathDurationContract
                    ? basicAttackProfile.BreathDuration
                    : 0f;
                if (animationDriver.TryBeginAttack(
                        passiveRuntime.ResolveAttackInterval(attackInterval),
                        ++nextActionSequenceId,
                        HandleAttackMarker,
                        breathDuration))
                {
                    return;
                }

                attackActionRunning = false;
            }

            ApplyPendingAttack(1f);
        }

        private void TickAttackAction(float deltaTime)
        {
            FaceTowards(
                pendingTarget.Kind == HexCastleAssaultTargetKind.None
                    ? ResolvePosition(pendingLegacyAttackCoordinates)
                    : ResolveTargetPosition(pendingTarget),
                deltaTime);
            if (animationDriver == null || animationDriver.TickAttack(deltaTime, HandleAttackMarker))
            {
                attackActionRunning = false;
                animationDriver?.PlayIdle(true);
            }
        }

        private void HandleAttackMarker(int markerIndex, MonsterAttackMarker marker)
        {
            if (attackActionRunning)
            {
                ApplyPendingAttack(marker == null ? 1f : Mathf.Max(0f, marker.PowerRatio));
            }
        }

        private ProjectMT.Shared.Audio.SfxPool attackAudioPool;
        private ProjectMT.Shared.Audio.SfxCue attackSfx;

        public void ConfigureAttackAudio(ProjectMT.Shared.Audio.SfxPool pool, ProjectMT.Shared.Audio.SfxCue cue)
        {
            attackAudioPool = pool;
            attackSfx = cue;
        }

        private void ApplyPendingAttack(float powerRatio)
        {
            var damage = attackDamage * attackDamageMultiplier * Mathf.Max(0f, powerRatio);
            if (pendingTarget.Kind != HexCastleAssaultTargetKind.None)
            {
                if (pendingTarget.Kind == HexCastleAssaultTargetKind.Ally)
                {
                    return;
                }

                if (!CanAttackTarget(pendingTarget))
                {
                    currentTarget = default;
                    RequestStrategicDecision(true);
                    return;
                }

                damage = passiveRuntime.ResolveOutgoingDamage(damage, pendingTarget);
                damage *= assaultWorld?.ResolvePassiveDamageMultiplier(pendingTarget) ?? 1f;
                var wasAlive = pendingTarget.IsAlive;
                if (wasAlive && damage > 0f) attackAudioPool?.Play(attackSfx, transform.position); // 실제 공격 Marker에서만 재생
                if (pendingTarget.Structure != null)
                {
                    pendingTarget.Structure.ApplyDamage(damage, pendingAttackPosition);
                }
                else
                {
                    pendingTarget.Defender?.ApplyDamage(damage, pendingAttackPosition);
                }

                var destroyed = wasAlive && !pendingTarget.IsAlive;
                passiveRuntime.NotifyBasicAttackHit(pendingTarget, destroyed);
                if (destroyed)
                {
                    DestroyedTargets++;
                    if (committedTarget.InstanceId == pendingTarget.InstanceId)
                    {
                        committedTarget = default;
                        committedIntent = HexCastleAssaultIntentKind.None;
                    }
                    currentTarget = default;
                    RequestStrategicDecision(true);
                }
                return;
            }

            var legacyWasAlive = HasAliveTarget(pendingLegacyAttackCoordinates);
            if (legacyWasAlive && damage > 0f) attackAudioPool?.Play(attackSfx, transform.position);
            ApplyLegacyTargetDamage(pendingLegacyAttackCoordinates, pendingAttackPosition, damage);
            if (legacyWasAlive && !HasAliveTarget(pendingLegacyAttackCoordinates))
            {
                DestroyedTargets++;
            }
        }

        private bool HasAliveTarget(HexCoordinates coordinates)
        {
            return cellTargets != null &&
                   cellTargets.TryGetValue(coordinates, out var cellTarget) &&
                   cellTarget != null && cellTarget.IsAlive;
        }

        private bool CanAssaultTraverse(HexCoordinates coordinates)
        {
            return cellTargets == null ||
                   !cellTargets.TryGetValue(coordinates, out var cellTarget) ||
                   cellTarget == null || cellTarget.CanTraverse(HexCastleTraversalFaction.Assault);
        }

        private void ApplyLegacyTargetDamage(
            HexCoordinates coordinates,
            Vector3 hitPoint,
            float damage)
        {
            if (cellTargets != null &&
                cellTargets.TryGetValue(coordinates, out var cellTarget) &&
                cellTarget != null)
            {
                cellTarget.ApplyDamage(damage, hitPoint);
            }
        }

        private void TickRuntimeEffects(float deltaTime)
        {
            recentDamagePerSecond *= Mathf.Exp(-deltaTime / 2.5f);
            recentThreatRemaining = Mathf.Max(0f, recentThreatRemaining - deltaTime);
            supportCooldownRemaining = Mathf.Max(0f, supportCooldownRemaining - deltaTime);
            trapMovementLockRemaining = Mathf.Max(0f, trapMovementLockRemaining - deltaTime);
            trapSlowRemaining = Mathf.Max(0f, trapSlowRemaining - deltaTime);
            if (trapSlowRemaining <= 0f)
            {
                trapMoveSpeedMultiplier = 1f;
            }
            if (recentThreatRemaining <= 0f || !recentThreat.IsValid)
            {
                recentThreat = default;
            }

            if (attackBuffRemaining > 0f)
            {
                attackBuffRemaining = Mathf.Max(0f, attackBuffRemaining - deltaTime);
                if (attackBuffRemaining <= 0f)
                {
                    attackDamageMultiplier = 1f;
                }
            }

            if (defenseBuffRemaining > 0f)
            {
                defenseBuffRemaining = Mathf.Max(0f, defenseBuffRemaining - deltaTime);
                if (defenseBuffRemaining <= 0f)
                {
                    incomingDamageMultiplier = 1f;
                }
            }
        }

        private float ResolveDecisionSpread()
        {
            return Mathf.Abs(GetInstanceID() % 9) / 8f * 0.08f;
        }

        private static bool IsCommitmentIntent(HexCastleAssaultIntentKind intent)
        {
            return intent == HexCastleAssaultIntentKind.InitialBreach ||
                   intent == HexCastleAssaultIntentKind.Progress ||
                   intent == HexCastleAssaultIntentKind.Opportunity ||
                   intent == HexCastleAssaultIntentKind.Specialist ||
                   intent == HexCastleAssaultIntentKind.Palace;
        }

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }
    }
}
