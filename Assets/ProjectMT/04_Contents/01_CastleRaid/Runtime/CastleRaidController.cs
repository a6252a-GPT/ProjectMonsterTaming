using System;
using System.Collections;
using System.Collections.Generic;
using ProjectMT.Contents.CastleRaid.Generation;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Pooling;
using ProjectMT.Shared.Unit;
using ProjectMT.Shared.Audio;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace ProjectMT.Contents.CastleRaid
{
    [DisallowMultipleComponent]
    public sealed class CastleRaidController : MonoBehaviour, IContentController // 군단의 역습 배치·전투·결과 총괄
    {
        private const float InnerPathVerificationTimeoutSeconds = 2f; // 성벽 파괴 뒤 경로 확인 제한
        private const float InnerPathVerificationIntervalSeconds = 0.1f; // 경로 재검사 간격
        private const int RequiredConsecutivePathChecks = 5; // 열린 경로 연속 확인 횟수
        private const float CornerCoordinateTolerance = 0.05f; // 고정 Stage 모서리 좌표 판정 오차
        private const float DeadUnitPoolReturnPaddingSeconds = 0.05f; // 사망 동작 종료 뒤 풀 반환 여유
        private const float BreachEndpointPadding = 0.12f; // 파괴 타일 양쪽 면을 조금만 벗어난다
        private const float BreachMinimumProbeDistance = 1.05f; // Carving 영역 바깥에서 양쪽 NavMesh를 찾는다
        private const float BreachProbeRadius = 0.35f; // 인접 성벽으로 스냅되지 않는 탐색 반경
        private const float BreachLinkWidth = 0.05f; // 한 칸 틈 중앙만 사용해 옆 성벽을 넘지 않는다
        private const float BreachMaximumLinkDistance = 2.4f;
        private const int BreachLinkRetryLimit = 20;
        private const int ReachabilityCandidateLimit = 8;
        private const int TurretCandidateLimit = 32;
        private const float WallSpatialCellSize = 1f;
        private const int WallSpatialTraversalLimit = 256;
        private const float BreachCrossingLateralTolerance = 0.9f;

        private sealed class WallBlockerRecord
        {
            public CastleTarget Target;
            public Bounds Bounds;
            public bool Alive;
            public int QueryStamp;
        }

        private sealed class BreachRouteRecord
        {
            public CastleTarget Wall;
            public Vector3 WallPosition;
            public Vector3 OutsidePoint;
            public Vector3 InsidePoint;
            public Vector3 Inward;
            public int DefenseLayer;
        }

        private sealed class AssaultRouteState
        {
            public readonly HashSet<string> KnownDistrictIds = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> CurrentDistrictIds = new HashSet<string>(StringComparer.Ordinal);
            public Vector3 PreviousPosition;
            public bool HasPreviousPosition;
            public int EnteredDefenseLayer = -1;
        }

        private enum TargetCandidateScope
        {
            Any,
            OuterWall,
            OpenedDistrict,
            OpenedInwardWall
        }

        [Header("Runtime")]
        [SerializeField] private ScenePoolScope poolScope; // 공격 유닛 재사용 풀
        [SerializeField] private CombatFeedbackPlayer combatFeedback; // 타격·파괴 연출
        [SerializeField] private Camera deploymentCamera; // 터치 좌표 변환 카메라
        [SerializeField] private CastleDeploymentZone deploymentZone; // 외곽 배치 가능 구역
        [SerializeField] private Transform innerEntry; // 성 내부 진입 목표점
        [SerializeField] private CastleTarget[] targets; // 성벽·수비대·본성 목록

        [Header("HUD")]
        [SerializeField] private TMP_Text deploymentText; // 현재 배치 수 표시
        [SerializeField] private TMP_Text statusText; // 진행 안내 문구
        [SerializeField] private Button[] unitButtons; // 출전 유닛 선택 버튼
        [SerializeField] private TMP_Text[] unitButtonLabels; // 유닛 버튼 글자
        [SerializeField] private Button exitButton; // 콘텐츠 나가기 버튼

        [Header("Runtime Generation")]
        [SerializeField] private CastleRuntimeStageGenerator runtimeStageGenerator; // 입장·재도전 성 생성기
        [SerializeField] private TMP_Text castleInfoText; // 현재 테마·방어선·Seed
        [SerializeField] private Button doubleWallButton; // 2중벽 새 성
        [SerializeField] private Button tripleWallButton; // 3중벽 새 성
        [SerializeField] private Button quadrupleWallButton; // 4중벽 새 성
        [SerializeField] private Button regenerateCastleButton; // 같은 방어선의 다른 성

        [Header("Seed Balance")]
        [SerializeField, Min(0.1f)] private float defenderAttackInterval = 1.15f; // 수비대 공격 주기
        [SerializeField, Min(0f)] private float defenderDamage = 7f; // 수비대 1회 피해
        [FormerlySerializedAs("defenderRange")]
        [SerializeField, Min(0.1f)] private float defenderDetectionRange = 8f; // 침입자 탐지·추격 거리
        [SerializeField, Min(0.35f)] private float defenderAttackRange = 1.25f; // 기본 근접 공격 거리
        [SerializeField, Min(0.1f)] private float defenderMoveSpeed = 2.4f; // 수비대 이동 속도
        [SerializeField, Min(0f)] private float defenderPatrolRadius = 2.5f; // 평상시 주둔지 순찰 반경

        [Header("Assault AI")]
        [SerializeField, Min(1f)] private float assaultLocalTargetRadius = 8f; // 현재 격실 주변 목표 검색 반경
        [SerializeField, Range(0f, 1f)] private float assaultPalaceContinuationWeight = 0.35f; // 왕궁 방향 진행 비용 반영률

        private readonly List<CastleAssaultUnit> activeUnits = new List<CastleAssaultUnit>(); // 현재 출전 유닛
        private readonly List<GameObject> breachLinkObjects = new List<GameObject>(); // 파괴 성벽 런타임 연결
        private readonly List<Vector3> breachEntryPoints = new List<Vector3>(); // 파괴 지점 바로 안쪽 진입점
        private readonly HashSet<int> linkedWallIds = new HashSet<int>(); // 같은 성벽 중복 연결 차단
        private readonly List<CastleTarget> aliveWalls = new List<CastleTarget>();
        private readonly List<CastleTarget> aliveDefenders = new List<CastleTarget>();
        private readonly List<CastleTarget> aliveBuildings = new List<CastleTarget>();
        private readonly List<CastleTarget> breachFrontierWalls = new List<CastleTarget>();
        private readonly HashSet<string> openedDistrictIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly CastleTarget[] nearestTargetBuffer = new CastleTarget[ReachabilityCandidateLimit];
        private readonly float[] nearestDistanceBuffer = new float[ReachabilityCandidateLimit];
        private readonly CastleAssaultUnit[] turretCandidateBuffer = new CastleAssaultUnit[TurretCandidateLimit];
        private readonly float[] turretDistanceBuffer = new float[TurretCandidateLimit];
        private readonly int[] turretTierBuffer = new int[TurretCandidateLimit];
        private readonly List<WallBlockerRecord> wallBlockers = new List<WallBlockerRecord>();
        private readonly Dictionary<CastleTarget, WallBlockerRecord> wallBlockersByTarget =
            new Dictionary<CastleTarget, WallBlockerRecord>();
        private readonly Dictionary<Vector2Int, List<WallBlockerRecord>> wallBlockersByCell =
            new Dictionary<Vector2Int, List<WallBlockerRecord>>();
        private readonly List<BreachRouteRecord> breachRoutes = new List<BreachRouteRecord>();
        private readonly Dictionary<CastleAssaultUnit, AssaultRouteState> assaultRouteStates =
            new Dictionary<CastleAssaultUnit, AssaultRouteState>();
        private readonly Dictionary<string, float> supportClaims = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly List<string> expiredSupportClaimKeys = new List<string>();
        private CastleRaidAIProfileCatalog aiProfileCatalog;
        private NavMeshPath innerPathProbe; // 진입 경로 검사 재사용 버퍼
        private ContentContext context; // 결과 반환 통로
        private CastleRaidStartData startData; // 이번 판 시작 정보
        private UnityAction[] unitButtonActions; // 해제용 버튼 콜백
        private int[] remainingDeployments; // 편성 슬롯별 남은 소환 수
        private int deployedCount; // 누적 배치 수
        private int selectedUnitIndex = -1; // 배치 대기 유닛 번호
        private float defenderAttackCooldown; // 다음 수비대 공격까지 시간
        private bool innerPathOpen; // 본성 진입 가능 여부
        private bool verifyingInnerPath; // 경로 확인 중복 방지
        private bool unitPathRefreshQueued; // 같은 프레임 파괴 경로 갱신 합치기
        private bool unitPathRetargetQueued; // 돌파 단계 변경이면 목표도 다시 고른다
        private bool generationInProgress; // 중복 재생성 입력 차단
        private CastleTarget mainCastle;
        private bool hasGenerationTargetMetadata;
        private bool hasDestroyedOuterWall;
        private bool routeEstablished;
        private int openedDefenseLayer = -1;
        private bool wallBlockerIndexReady;
        private int wallBlockerQueryStamp;

        public bool IsRunning { get; private set; }
        public int DeployedCount => deployedCount;
        public int SelectedUnitIndex => selectedUnitIndex;
        public bool InnerPathOpen => innerPathOpen;

        public void ConfigureRuntimeStage(
            CastleDeploymentZone zone,
            Transform pathProbe,
            CastleTarget[] castleTargets)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("진행 중인 Castle Raid의 Stage는 교체할 수 없습니다.");
            }

            deploymentZone = zone != null ? zone : throw new ArgumentNullException(nameof(zone));
            innerEntry = pathProbe != null ? pathProbe : throw new ArgumentNullException(nameof(pathProbe));
            targets = castleTargets != null && castleTargets.Length > 0
                ? castleTargets
                : throw new ArgumentException("생성 Stage에는 하나 이상의 목표가 필요합니다.", nameof(castleTargets));
        }

        public bool TryResolveInnerEntry(Vector3 fromPosition, out Vector3 position)
        {
            if (!innerPathOpen || breachEntryPoints.Count == 0)
            {
                position = default;
                return false;
            }

            var nearestIndex = 0;
            var nearestDistance = (breachEntryPoints[0] - fromPosition).sqrMagnitude;
            for (var i = 1; i < breachEntryPoints.Count; i++)
            {
                var distance = (breachEntryPoints[i] - fromPosition).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestIndex = i;
                    nearestDistance = distance;
                }
            }

            position = breachEntryPoints[nearestIndex];
            return true;
        }

        public bool TryResolveInnerEntry(CastleAssaultUnit attacker, out Vector3 position)
        {
            if (attacker == null || breachRoutes.Count == 0)
            {
                return TryResolveInnerEntry(attacker == null ? Vector3.zero : attacker.transform.position, out position);
            }

            var routeState = GetOrCreateAssaultRouteState(attacker);
            var expectedLayer = CastleAssaultRouteMath.ResolveNextInternalLayer(
                routeState.EnteredDefenseLayer,
                ResolveDefenseLayerCount());
            var nearestDistance = float.PositiveInfinity;
            position = default;
            for (var index = 0; index < breachRoutes.Count; index++)
            {
                var route = breachRoutes[index];
                if (route == null || route.DefenseLayer != expectedLayer ||
                    !attacker.TryMeasurePathToPosition(route.OutsidePoint, out var pathDistance) ||
                    pathDistance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = pathDistance;
                position = route.InsidePoint;
            }

            return !float.IsPositiveInfinity(nearestDistance);
        }

        public void Initialize(ContentContext contentContext)
        {
            Shutdown(); // 재초기화 전 이전 판 정리
            context = contentContext ?? throw new ArgumentNullException(nameof(contentContext));
            startData = contentContext.StartData as CastleRaidStartData;
            if (startData == null || startData.Party == null)
            {
                throw new ArgumentException("CastleRaidStartData is required.", nameof(contentContext));
            }

            runtimeStageGenerator?.EnsureGeneratedStage(); // 입장마다 검수된 랜덤 성을 전투 참조에 먼저 연결

            if (poolScope == null || deploymentCamera == null || deploymentZone == null ||
                targets == null || targets.Length == 0 || unitButtons == null || unitButtons.Length == 0)
            {
                throw new InvalidOperationException("Castle Raid runtime references are missing.");
            }

            poolScope.ReturnAll();
            activeUnits.Clear();
            aiProfileCatalog = Resources.Load<CastleRaidAIProfileCatalog>(CastleRaidAIProfileCatalog.DefaultResourcesPath);
            deployedCount = 0;
            selectedUnitIndex = -1;
            remainingDeployments = new int[startData.UnitSlotCount];
            for (var index = 0; index < remainingDeployments.Length; index++)
            {
                remainingDeployments[index] = startData.SummonsPerSlot;
            }
            innerPathProbe = new NavMeshPath(); // Unity 씬 인스턴스 생성이 끝난 뒤 네이티브 경로 버퍼 준비
            defenderAttackCooldown = defenderAttackInterval;
            innerPathOpen = false;
            verifyingInnerPath = false;
            unitPathRefreshQueued = false;
            unitPathRetargetQueued = false;
            hasDestroyedOuterWall = false;
            routeEstablished = false;
            openedDefenseLayer = -1;
            openedDistrictIds.Clear();
            breachFrontierWalls.Clear();
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target == null)
                {
                    continue;
                }

                target.Initialize(); // 씬에 고정된 목표물 재사용 초기화
                target.GetComponent<CastleDefenderUnit>()?.InitializeRuntime();
                target.Damaged += HandleTargetDamaged;
                target.Destroyed += HandleTargetDestroyed;
            }
            RebuildTargetCaches();

            BindUnitButtons();
            BindGenerationButtons();
            exitButton?.onClick.AddListener(Cancel);
            IsRunning = true;
            SetStatus("몬스터를 선택한 뒤 초록색 외곽을 터치하세요");
            UpdateHud();
            UpdateGenerationHud();
        }

        public void Shutdown()
        {
            StopAllCoroutines(); // 경로 확인·풀 반환 대기 중단
            UnbindUnitButtons();
            UnbindGenerationButtons();
            exitButton?.onClick.RemoveListener(Cancel);
            for (var i = 0; i < targets?.Length; i++)
            {
                if (targets[i] == null)
                {
                    continue;
                }

                targets[i].Damaged -= HandleTargetDamaged;
                targets[i].Destroyed -= HandleTargetDestroyed;
                targets[i].GetComponent<CastleDefenderUnit>()?.ShutdownRuntime();
                targets[i].Shutdown();
            }

            for (var i = 0; i < activeUnits.Count; i++)
            {
                activeUnits[i]?.Shutdown();
            }

            activeUnits.Clear();
            poolScope?.ReturnAll();
            ClearBreachLinks();
            ClearTargetCaches();
            context = null;
            startData = null;
            remainingDeployments = null;
            selectedUnitIndex = -1;
            verifyingInnerPath = false;
            unitPathRefreshQueued = false;
            unitPathRetargetQueued = false;
            IsRunning = false;
            UpdateDeploymentZoneVisual();
        }

        private void RebuildTargetCaches()
        {
            ClearTargetCaches();
            if (targets == null)
            {
                wallBlockerIndexReady = true;
                return;
            }

            for (var index = 0; index < targets.Length; index++)
            {
                var target = targets[index];
                if (target == null || !target.IsAlive)
                {
                    continue;
                }

                hasGenerationTargetMetadata |= target.HasGenerationMetadata;

                switch (target.TargetKind)
                {
                    case CastleTargetKind.Wall:
                        aliveWalls.Add(target);
                        AddWallBlocker(target);
                        break;
                    case CastleTargetKind.Defender:
                        aliveDefenders.Add(target);
                        break;
                    case CastleTargetKind.Building:
                        aliveBuildings.Add(target);
                        break;
                    case CastleTargetKind.MainCastle:
                        mainCastle = target;
                        break;
                }
            }

            wallBlockerIndexReady = true;
        }

        private void AddWallBlocker(CastleTarget target)
        {
            if (target == null || !target.TryGetTurretBlockerBounds(out var bounds))
            {
                return;
            }

            var blocker = new WallBlockerRecord
            {
                Target = target,
                Bounds = bounds,
                Alive = true
            };
            wallBlockers.Add(blocker);
            wallBlockersByTarget[target] = blocker;

            var minimumX = Mathf.FloorToInt(bounds.min.x / WallSpatialCellSize);
            var minimumZ = Mathf.FloorToInt(bounds.min.z / WallSpatialCellSize);
            var maximumX = Mathf.FloorToInt((bounds.max.x - 0.0001f) / WallSpatialCellSize);
            var maximumZ = Mathf.FloorToInt((bounds.max.z - 0.0001f) / WallSpatialCellSize);
            for (var z = minimumZ; z <= maximumZ; z++)
            {
                for (var x = minimumX; x <= maximumX; x++)
                {
                    var key = new Vector2Int(x, z);
                    if (!wallBlockersByCell.TryGetValue(key, out var cellBlockers))
                    {
                        cellBlockers = new List<WallBlockerRecord>(2);
                        wallBlockersByCell.Add(key, cellBlockers);
                    }

                    cellBlockers.Add(blocker);
                }
            }
        }

        private void RemoveCachedTarget(CastleTarget target)
        {
            switch (target.TargetKind)
            {
                case CastleTargetKind.Wall:
                    aliveWalls.Remove(target);
                    breachFrontierWalls.Remove(target);
                    if (wallBlockersByTarget.TryGetValue(target, out var blocker))
                    {
                        blocker.Alive = false;
                    }
                    break;
                case CastleTargetKind.Defender:
                    aliveDefenders.Remove(target);
                    break;
                case CastleTargetKind.Building:
                    aliveBuildings.Remove(target);
                    break;
                case CastleTargetKind.MainCastle:
                    if (mainCastle == target)
                    {
                        mainCastle = null;
                    }
                    break;
            }
        }

        private void ClearTargetCaches()
        {
            aliveWalls.Clear();
            aliveDefenders.Clear();
            aliveBuildings.Clear();
            mainCastle = null;
            hasGenerationTargetMetadata = false;
            wallBlockers.Clear();
            wallBlockersByTarget.Clear();
            wallBlockersByCell.Clear();
            wallBlockerQueryStamp = 0;
            wallBlockerIndexReady = false;
        }

        private void BindGenerationButtons()
        {
            doubleWallButton?.onClick.AddListener(GenerateDoubleWallCastle);
            tripleWallButton?.onClick.AddListener(GenerateTripleWallCastle);
            quadrupleWallButton?.onClick.AddListener(GenerateQuadrupleWallCastle);
            regenerateCastleButton?.onClick.AddListener(GenerateAnotherCastle);
        }

        private void UnbindGenerationButtons()
        {
            doubleWallButton?.onClick.RemoveListener(GenerateDoubleWallCastle);
            tripleWallButton?.onClick.RemoveListener(GenerateTripleWallCastle);
            quadrupleWallButton?.onClick.RemoveListener(GenerateQuadrupleWallCastle);
            regenerateCastleButton?.onClick.RemoveListener(GenerateAnotherCastle);
        }

        private void GenerateDoubleWallCastle()
        {
            RestartWithRandomCastle(2);
        }

        private void GenerateTripleWallCastle()
        {
            RestartWithRandomCastle(3);
        }

        private void GenerateQuadrupleWallCastle()
        {
            RestartWithRandomCastle(4);
        }

        private void GenerateAnotherCastle()
        {
            var defenseLayers = runtimeStageGenerator == null ||
                                runtimeStageGenerator.CurrentDefenseLayerCount < 2
                ? 2
                : runtimeStageGenerator.CurrentDefenseLayerCount;
            RestartWithRandomCastle(defenseLayers);
        }

        private void RestartWithRandomCastle(int defenseLayerCount)
        {
            if (!IsRunning || generationInProgress || runtimeStageGenerator == null || context == null)
            {
                return;
            }

            var restartContext = context;
            generationInProgress = true;
            UpdateGenerationHud();
            SetStatus($"{defenseLayerCount}중벽 성을 찾는 중입니다...");
            try
            {
                Shutdown(); // 현재 출전 유닛과 목표 이벤트를 정리한 뒤 Stage를 교체한다
                runtimeStageGenerator.GenerateRandomStage(defenseLayerCount);
                Initialize(restartContext);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                try
                {
                    Initialize(restartContext); // 새 후보가 실패하면 직전 성으로 즉시 복귀한다
                    SetStatus("새 성 생성에 실패해 이전 성으로 돌아왔습니다");
                }
                catch (Exception restoreException)
                {
                    Debug.LogException(restoreException, this);
                    SetStatus("성을 준비하지 못했습니다. 콘텐츠에서 나갔다가 다시 시도해 주세요");
                }
            }
            finally
            {
                generationInProgress = false;
                UpdateGenerationHud();
            }
        }

        private void UpdateGenerationHud()
        {
            if (castleInfoText != null)
            {
                castleInfoText.text = runtimeStageGenerator == null
                    ? "랜덤 성 생성기 미연결"
                    : runtimeStageGenerator.CurrentSummary;
            }

            var canGenerate = IsRunning && !generationInProgress;
            SetGenerationButtonState(doubleWallButton, canGenerate);
            SetGenerationButtonState(tripleWallButton, canGenerate);
            SetGenerationButtonState(quadrupleWallButton, canGenerate);
            SetGenerationButtonState(regenerateCastleButton, canGenerate);
        }

        private static void SetGenerationButtonState(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            for (var i = activeUnits.Count - 1; i >= 0; i--)
            {
                var unit = activeUnits[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                UpdateAssaultRouteProgress(unit);
                unit.Tick(Time.deltaTime);
            }

            for (var i = aliveDefenders.Count - 1; i >= 0; i--)
            {
                var defender = aliveDefenders[i];
                defender?.GetComponent<CastleDefenderUnit>()?.Tick(Time.deltaTime);
            }

            defenderAttackCooldown -= Time.deltaTime;
            if (defenderAttackCooldown <= 0f)
            {
                defenderAttackCooldown = defenderAttackInterval;
                AttackWithDefenders();
            }
        }

        public CastleTarget FindPriorityTarget(CastleAssaultUnit attacker, bool preferCurrentTarget = true)
        {
            if (!hasGenerationTargetMetadata)
            {
                return FindLegacyPriorityTarget(attacker, preferCurrentTarget);
            }

            if (attacker == null)
            {
                return mainCastle != null && mainCastle.IsAlive ? mainCastle : null;
            }

            var routeState = GetOrCreateAssaultRouteState(attacker);
            UpdateAssaultRouteProgress(attacker);
            var profile = attacker.AiProfile;
            var pattern = profile == null ? CastleRaidAiPattern.BalancedAdvance : profile.Pattern;
            var current = attacker.Target;
            var totalLayers = ResolveDefenseLayerCount();
            var nextLayer = CastleAssaultRouteMath.ResolveNextInternalLayer(
                routeState.EnteredDefenseLayer,
                totalLayers);

            if (routeState.EnteredDefenseLayer < 0)
            {
                if (preferCurrentTarget && IsValidOutsideTarget(attacker, current))
                {
                    return current;
                }

                var newBreach = FindBestWallForLayer(attacker, 0, null, out var newBreachScore);
                var openedRoute = FindOpenedRouteTarget(attacker, out var openedRouteScore);
                if (pattern == CastleRaidAiPattern.WallBreaker)
                {
                    newBreachScore *= 0.72f;
                }

                return openedRoute == null || newBreach != null && newBreachScore < openedRouteScore
                    ? newBreach
                    : openedRoute;
            }

            if (preferCurrentTarget && IsCurrentPolicyTargetAllowed(
                    attacker,
                    current,
                    routeState.CurrentDistrictIds,
                    nextLayer,
                    pattern))
            {
                return current;
            }

            if (pattern == CastleRaidAiPattern.PalaceRush)
            {
                var palace = FindReachableMainCastle(attacker);
                if (palace != null)
                {
                    return palace;
                }
            }

            if (nextLayer < 0)
            {
                var palace = FindReachableMainCastle(attacker);
                if (palace != null)
                {
                    return palace; // 최종 방어선을 넘은 뒤에는 모든 기본 정책이 왕궁을 최우선한다
                }
            }

            CastleTarget localTarget = null;
            if (pattern != CastleRaidAiPattern.WallBreaker && pattern != CastleRaidAiPattern.PalaceRush)
            {
                localTarget = FindBestLocalTarget(
                    attacker,
                    routeState.CurrentDistrictIds,
                    assaultLocalTargetRadius,
                    pattern,
                    out _);
                if (localTarget != null)
                {
                    return localTarget;
                }
            }

            if (nextLayer >= 0)
            {
                var wall = FindBestWallForLayer(attacker, nextLayer, routeState.CurrentDistrictIds, out _);
                if (wall != null)
                {
                    return wall;
                }

                wall = FindBestWallForLayer(attacker, nextLayer, null, out _);
                if (wall != null)
                {
                    return wall;
                }
            }

            if (pattern != CastleRaidAiPattern.WallBreaker && pattern != CastleRaidAiPattern.PalaceRush)
            {
                localTarget = FindBestLocalTarget(
                    attacker,
                    routeState.CurrentDistrictIds,
                    float.PositiveInfinity,
                    pattern,
                    out _);
            }

            return localTarget ?? FindReachableMainCastle(attacker) ??
                   FindEmergencyReachableTarget(attacker, routeState.EnteredDefenseLayer);
        }

        public CastleRaidAIProfile ResolveAIProfile(string monsterId)
        {
            return aiProfileCatalog == null ? null : aiProfileCatalog.Resolve(monsterId);
        }

        public bool TrySelectSupportDecision(
            CastleAssaultUnit source,
            out CastleRaidSupportDecision decision)
        {
            decision = default;
            var profile = source?.AiProfile;
            if (!IsRunning || source == null || !source.IsAlive || profile == null ||
                profile.Pattern != CastleRaidAiPattern.TacticalSupport)
            {
                return false;
            }

            ClearExpiredSupportClaims();
            var bestScore = 0.35f;
            var maximumTravelDistance = Mathf.Max(profile.SupportRange, assaultLocalTargetRadius);
            for (var index = 0; index < activeUnits.Count; index++)
            {
                var candidate = activeUnits[index];
                if (candidate == null || !candidate.IsAlive ||
                    !source.TryMeasurePathToPosition(candidate.transform.position, out var pathDistance) ||
                    pathDistance > maximumTravelDistance)
                {
                    continue;
                }

                var healClaimed = IsSupportClaimed(candidate, CastleRaidSupportAction.Heal, source);
                var healScore = CastleRaidSupportUtility.ScoreHeal(
                    candidate.HealthRatio,
                    candidate.RecentDamagePerSecond,
                    candidate.MaxHealth,
                    candidate.EstimatedTimeToLive,
                    profile.SupportFocus,
                    healClaimed);
                if (healScore > bestScore)
                {
                    bestScore = healScore;
                    decision = new CastleRaidSupportDecision(
                        CastleRaidSupportAction.Heal,
                        candidate,
                        profile,
                        healScore);
                }

                var defenseClaimed = IsSupportClaimed(candidate, CastleRaidSupportAction.DefenseBuff, source);
                var defenseScore = CastleRaidSupportUtility.ScoreDefenseBuff(
                    candidate.HealthRatio,
                    candidate.RecentDamagePerSecond,
                    candidate.MaxHealth,
                    candidate.HasDefenseBuff,
                    profile.SupportFocus,
                    defenseClaimed);
                if (defenseScore > bestScore)
                {
                    bestScore = defenseScore;
                    decision = new CastleRaidSupportDecision(
                        CastleRaidSupportAction.DefenseBuff,
                        candidate,
                        profile,
                        defenseScore);
                }

                if (candidate == source)
                {
                    continue; // 공격 강화는 다른 공격수에게 집중한다
                }

                var attackClaimed = IsSupportClaimed(candidate, CastleRaidSupportAction.AttackBuff, source);
                var attackScore = CastleRaidSupportUtility.ScoreAttackBuff(
                    candidate.EstimatedDamagePerSecond,
                    candidate.HasCombatTarget,
                    candidate.HasAttackBuff,
                    profile.SupportFocus,
                    attackClaimed);
                if (attackScore > bestScore)
                {
                    bestScore = attackScore;
                    decision = new CastleRaidSupportDecision(
                        CastleRaidSupportAction.AttackBuff,
                        candidate,
                        profile,
                        attackScore);
                }
            }

            if (!decision.IsValid)
            {
                return false;
            }

            supportClaims[BuildSupportClaimKey(decision.Target, decision.Action)] = Time.time + 0.9f;
            return true;
        }

        public void CommitSupportDecision(CastleAssaultUnit source, CastleRaidSupportDecision decision)
        {
            if (source == null || !decision.IsValid)
            {
                return;
            }

            supportClaims[BuildSupportClaimKey(decision.Target, decision.Action)] =
                Time.time + Mathf.Min(1.5f, Mathf.Max(0.5f, decision.Profile.SupportCooldown * 0.35f));
        }

        public void PlaySupportFeedback(CastleAssaultUnit target, CastleRaidSupportAction action, float amount)
        {
            if (target == null || action != CastleRaidSupportAction.Heal || amount <= 0f)
            {
                return;
            }

            combatFeedback?.PlayDamage(
                target.transform.position,
                amount,
                FloatingNumberStyle.Heal,
                target.GetInstanceID());
        }

        private bool IsValidOutsideTarget(CastleAssaultUnit attacker, CastleTarget candidate)
        {
            if (candidate == null || !candidate.IsAlive ||
                !attacker.TryMeasurePathToTarget(candidate, out _))
            {
                return false;
            }

            return candidate.TargetKind == CastleTargetKind.Wall && candidate.WallDefenseLayer == 0 ||
                   TargetBelongsToDistricts(candidate, openedDistrictIds);
        }

        private bool IsCurrentPolicyTargetAllowed(
            CastleAssaultUnit attacker,
            CastleTarget candidate,
            IReadOnlyCollection<string> districtIds,
            int nextLayer,
            CastleRaidAiPattern pattern)
        {
            if (candidate == null || !candidate.IsAlive ||
                !attacker.TryMeasurePathToTarget(candidate, out _))
            {
                return false;
            }

            if (candidate.TargetKind == CastleTargetKind.MainCastle)
            {
                return nextLayer < 0 || pattern == CastleRaidAiPattern.PalaceRush;
            }

            if (candidate.TargetKind == CastleTargetKind.Wall)
            {
                return nextLayer >= 0 && candidate.WallDefenseLayer == nextLayer &&
                       TargetBelongsToDistricts(candidate, districtIds);
            }

            return pattern != CastleRaidAiPattern.WallBreaker && pattern != CastleRaidAiPattern.PalaceRush &&
                   IsLocalTarget(candidate, districtIds);
        }

        private CastleTarget FindBestLocalTarget(
            CastleAssaultUnit attacker,
            IReadOnlyCollection<string> districtIds,
            float maximumDistance,
            CastleRaidAiPattern pattern,
            out float bestScore)
        {
            CastleTarget best = null;
            var resolvedBestScore = float.PositiveInfinity;
            ScoreLocalCandidates(aliveDefenders, false);
            ScoreLocalCandidates(aliveBuildings, true);
            bestScore = resolvedBestScore;
            return best;

            void ScoreLocalCandidates(IReadOnlyList<CastleTarget> candidates, bool building)
            {
                for (var index = 0; index < candidates.Count; index++)
                {
                    var candidate = candidates[index];
                    if (!IsLocalTarget(candidate, districtIds) ||
                        !TryScoreTarget(attacker, candidate, false, true, out var score, out var pathDistance) ||
                        pathDistance > maximumDistance)
                    {
                        continue;
                    }

                    var isTurret = building && candidate.TryGetComponent<CastleTurretRuntime>(out _);
                    switch (pattern)
                    {
                        case CastleRaidAiPattern.BuildingPriority when building && !isTurret:
                            score *= 0.52f;
                            break;
                        case CastleRaidAiPattern.DefenseFacilityPriority when isTurret:
                            score *= 0.45f;
                            break;
                        case CastleRaidAiPattern.DefenderPriority when !building:
                            score *= 0.5f;
                            break;
                        case CastleRaidAiPattern.BuildingPriority:
                        case CastleRaidAiPattern.DefenseFacilityPriority:
                        case CastleRaidAiPattern.DefenderPriority:
                            score *= 1.2f;
                            break;
                    }

                    if (score < resolvedBestScore)
                    {
                        best = candidate;
                        resolvedBestScore = score;
                    }
                }
            }
        }

        private CastleTarget FindBestWallForLayer(
            CastleAssaultUnit attacker,
            int defenseLayer,
            IReadOnlyCollection<string> districtIds,
            out float bestScore)
        {
            CastleTarget best = null;
            bestScore = float.PositiveInfinity;
            for (var index = 0; index < aliveWalls.Count; index++)
            {
                var wall = aliveWalls[index];
                if (wall == null || wall.WallDefenseLayer != defenseLayer ||
                    districtIds != null && districtIds.Count > 0 && !TargetBelongsToDistricts(wall, districtIds) ||
                    !TryScoreTarget(attacker, wall, true, true, out var score, out _))
                {
                    continue;
                }

                if (score < bestScore)
                {
                    best = wall;
                    bestScore = score;
                }
            }

            return best;
        }

        private CastleTarget FindOpenedRouteTarget(CastleAssaultUnit attacker, out float bestScore)
        {
            bestScore = float.PositiveInfinity;
            if (breachRoutes.Count == 0)
            {
                return null;
            }

            var target = FindBestLocalTarget(
                attacker,
                openedDistrictIds,
                float.PositiveInfinity,
                CastleRaidAiPattern.BalancedAdvance,
                out bestScore);
            if (target != null)
            {
                return target;
            }

            var nextLayer = FindNextAliveWallLayer(0);
            if (nextLayer >= 0)
            {
                target = FindBestWallForLayer(attacker, nextLayer, openedDistrictIds, out bestScore);
            }

            if (target != null)
            {
                return target;
            }

            target = FindReachableMainCastle(attacker);
            if (target != null)
            {
                TryScoreTarget(attacker, target, false, false, out bestScore, out _);
            }

            return target;
        }

        private CastleTarget FindEmergencyReachableTarget(CastleAssaultUnit attacker, int enteredLayer)
        {
            var layer = FindNextAliveWallLayer(enteredLayer);
            if (layer >= 0)
            {
                var wall = FindBestWallForLayer(attacker, layer, null, out _);
                if (wall != null)
                {
                    return wall;
                }
            }

            return FindNearestReachableTarget(attacker, aliveDefenders) ??
                   FindNearestReachableTarget(attacker, aliveBuildings) ??
                   FindNearestReachableTarget(attacker, aliveWalls) ??
                   FindReachableMainCastle(attacker);
        }

        private bool TryScoreTarget(
            CastleAssaultUnit attacker,
            CastleTarget candidate,
            bool includeDestructionTime,
            bool includePalaceContinuation,
            out float score,
            out float pathDistance)
        {
            score = float.PositiveInfinity;
            pathDistance = float.PositiveInfinity;
            if (attacker == null || candidate == null || !candidate.IsAlive ||
                !attacker.TryMeasurePathToTarget(candidate, out pathDistance))
            {
                return false;
            }

            var continuation = includePalaceContinuation && innerEntry != null
                ? PlanarDistance(candidate.transform.position, innerEntry.position) * assaultPalaceContinuationWeight
                : 0f;
            var healthValue = includeDestructionTime && candidate.Health != null
                ? candidate.Health.CurrentHealth
                : 0f;
            score = CastleAssaultRouteMath.EstimateRouteSeconds(
                pathDistance,
                attacker.MoveSpeed,
                healthValue,
                attacker.EstimatedDamagePerSecond,
                continuation);
            return true;
        }

        private int FindNextAliveWallLayer(int enteredDefenseLayer)
        {
            var best = int.MaxValue;
            for (var index = 0; index < aliveWalls.Count; index++)
            {
                var wall = aliveWalls[index];
                if (wall != null && wall.IsAlive && wall.WallDefenseLayer > enteredDefenseLayer)
                {
                    best = Mathf.Min(best, wall.WallDefenseLayer);
                }
            }

            return best == int.MaxValue ? -1 : best;
        }

        private int ResolveDefenseLayerCount()
        {
            var maximumLayer = -1;
            var targetCount = targets == null ? 0 : targets.Length;
            for (var index = 0; index < targetCount; index++)
            {
                var target = targets[index];
                if (target != null && target.HasGenerationMetadata && target.TargetKind == CastleTargetKind.Wall)
                {
                    maximumLayer = Mathf.Max(maximumLayer, target.WallDefenseLayer);
                }
            }

            return Mathf.Max(1, maximumLayer + 1);
        }

        private AssaultRouteState GetOrCreateAssaultRouteState(CastleAssaultUnit attacker)
        {
            if (!assaultRouteStates.TryGetValue(attacker, out var state))
            {
                state = new AssaultRouteState();
                assaultRouteStates.Add(attacker, state);
            }

            return state;
        }

        private void UpdateAssaultRouteProgress(CastleAssaultUnit attacker)
        {
            if (attacker == null)
            {
                return;
            }

            var state = GetOrCreateAssaultRouteState(attacker);
            var currentPosition = attacker.transform.position;
            if (!state.HasPreviousPosition)
            {
                state.PreviousPosition = currentPosition;
                state.HasPreviousPosition = true;
                return;
            }

            for (var index = 0; index < breachRoutes.Count; index++)
            {
                var route = breachRoutes[index];
                if (route == null || route.DefenseLayer <= state.EnteredDefenseLayer ||
                    !CastleAssaultRouteMath.HasCrossedInward(
                        state.PreviousPosition,
                        currentPosition,
                        route.WallPosition,
                        route.Inward,
                        BreachCrossingLateralTolerance))
                {
                    continue;
                }

                state.EnteredDefenseLayer = route.DefenseLayer;
                state.CurrentDistrictIds.Clear();
                AddRouteDistricts(route.Wall, state.KnownDistrictIds, state.CurrentDistrictIds);
                attacker.RequestNavigationRefresh(true, 0f);
            }

            state.PreviousPosition = currentPosition;
        }

        private static void AddRouteDistricts(
            CastleTarget wall,
            ISet<string> knownDistricts,
            ISet<string> currentDistricts)
        {
            if (wall == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(wall.DistrictId))
            {
                knownDistricts.Add(wall.DistrictId);
                currentDistricts.Add(wall.DistrictId);
            }

            var owners = wall.OwnerDistrictIds;
            for (var index = 0; index < owners.Count; index++)
            {
                var districtId = owners[index];
                if (!string.IsNullOrWhiteSpace(districtId))
                {
                    knownDistricts.Add(districtId);
                    currentDistricts.Add(districtId);
                }
            }
        }

        private static bool IsLocalTarget(CastleTarget candidate, IReadOnlyCollection<string> districtIds)
        {
            return candidate != null && candidate.IsAlive &&
                   candidate.TargetKind != CastleTargetKind.Wall &&
                   candidate.TargetKind != CastleTargetKind.MainCastle &&
                   TargetBelongsToDistricts(candidate, districtIds);
        }

        private static bool TargetBelongsToDistricts(
            CastleTarget target,
            IReadOnlyCollection<string> districtIds)
        {
            if (target == null || districtIds == null || districtIds.Count == 0)
            {
                return districtIds == null || districtIds.Count == 0;
            }

            if (!string.IsNullOrWhiteSpace(target.DistrictId) && CollectionContains(districtIds, target.DistrictId))
            {
                return true;
            }

            var owners = target.OwnerDistrictIds;
            for (var index = 0; index < owners.Count; index++)
            {
                if (CollectionContains(districtIds, owners[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CollectionContains(IReadOnlyCollection<string> values, string value)
        {
            foreach (var candidate in values)
            {
                if (string.Equals(candidate, value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSupportClaimed(
            CastleAssaultUnit target,
            CastleRaidSupportAction action,
            CastleAssaultUnit source)
        {
            return supportClaims.TryGetValue(BuildSupportClaimKey(target, action), out var expiresAt) &&
                   expiresAt > Time.time && target != source;
        }

        private static string BuildSupportClaimKey(CastleAssaultUnit target, CastleRaidSupportAction action)
        {
            return $"{target.GetInstanceID()}:{(int)action}";
        }

        private void ClearExpiredSupportClaims()
        {
            if (supportClaims.Count == 0)
            {
                return;
            }

            expiredSupportClaimKeys.Clear();
            foreach (var pair in supportClaims)
            {
                if (pair.Value <= Time.time)
                {
                    expiredSupportClaimKeys.Add(pair.Key);
                }
            }

            for (var index = 0; index < expiredSupportClaimKeys.Count; index++)
            {
                supportClaims.Remove(expiredSupportClaimKeys[index]);
            }
            expiredSupportClaimKeys.Clear();
        }

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }

        public void ConfigureGeneratedDefender(
            CastleDefenderUnit defender,
            CastleTarget target,
            int movementSeed)
        {
            if (defender == null)
            {
                throw new ArgumentNullException(nameof(defender));
            }

            defender.Configure(
                this,
                target,
                movementSeed,
                defenderMoveSpeed,
                defenderDetectionRange,
                defenderAttackRange,
                defenderDamage,
                defenderAttackInterval,
                defenderPatrolRadius);
        }

        private CastleTarget FindReachableMainCastle(CastleAssaultUnit attacker)
        {
            return mainCastle != null && mainCastle.IsAlive &&
                   (attacker == null || attacker.CanReachTarget(mainCastle))
                ? mainCastle
                : null;
        }

        private CastleTarget FindLegacyPriorityTarget(CastleAssaultUnit attacker, bool preferCurrentTarget)
        {
            var mustBreachWall = !innerPathOpen;
            var currentTargetMatchesPhase = attacker != null && attacker.Target != null &&
                                            (mustBreachWall
                                                ? attacker.Target.TargetKind == CastleTargetKind.Wall
                                                : attacker.Target.TargetKind != CastleTargetKind.Wall);
            if (preferCurrentTarget && currentTargetMatchesPhase && attacker.Target.IsAlive &&
                attacker.CanReachTarget(attacker.Target))
            {
                return attacker.Target;
            }

            if (mustBreachWall)
            {
                return FindNearestReachableTarget(attacker, aliveWalls);
            }

            var target = FindNearestReachableTarget(attacker, aliveDefenders);
            if (target != null)
            {
                return target;
            }

            target = FindNearestReachableTarget(attacker, aliveBuildings);
            if (target != null)
            {
                return target;
            }

            if (mainCastle != null && mainCastle.IsAlive && (attacker == null || attacker.CanReachTarget(mainCastle)))
            {
                return mainCastle;
            }

            return FindNearestReachableTarget(attacker, aliveWalls); // 내부 목표가 막히면 남은 성벽으로 복귀
        }

        private bool IsCurrentGeneratedTargetAllowed(CastleAssaultUnit attacker)
        {
            if (attacker == null || attacker.Target == null || !attacker.Target.IsAlive)
            {
                return false;
            }

            var current = attacker.Target;
            if (!innerPathOpen)
            {
                return breachFrontierWalls.Contains(current) || !routeEstablished &&
                       current.TargetKind == CastleTargetKind.Wall &&
                       current.WallBand == CastleWallBand.OuterPerimeter;
            }

            if (current.TargetKind == CastleTargetKind.Wall)
            {
                return breachFrontierWalls.Contains(current) ||
                       current.WallBand != CastleWallBand.OuterPerimeter &&
                       current.WallDefenseLayer > openedDefenseLayer &&
                       IsTargetInOpenedDistrict(current);
            }

            return IsTargetInOpenedDistrict(current);
        }

        private CastleTarget FindNextInwardWall(CastleAssaultUnit attacker)
        {
            var nextLayer = int.MaxValue;
            for (var index = 0; index < aliveWalls.Count; index++)
            {
                var wall = aliveWalls[index];
                if (!IsCandidateAllowed(wall, TargetCandidateScope.OpenedInwardWall, -1) ||
                    wall.WallDefenseLayer <= openedDefenseLayer)
                {
                    continue;
                }

                nextLayer = Mathf.Min(nextLayer, wall.WallDefenseLayer);
            }

            return nextLayer == int.MaxValue
                ? null
                : FindNearestReachableTarget(
                    attacker,
                    aliveWalls,
                    TargetCandidateScope.OpenedInwardWall,
                    nextLayer,
                    true);
        }

        private CastleTarget FindNearestReachableTarget(
            CastleAssaultUnit attacker,
            IReadOnlyList<CastleTarget> candidates,
            TargetCandidateScope scope = TargetCandidateScope.Any,
            int requiredWallLayer = -1,
            bool preferPalaceDistance = false)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            if (attacker == null)
            {
                for (var index = 0; index < candidates.Count; index++)
                {
                    if (IsCandidateAllowed(candidates[index], scope, requiredWallLayer))
                    {
                        return candidates[index];
                    }
                }

                return null;
            }

            var bufferedCount = 0;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (!IsCandidateAllowed(candidate, scope, requiredWallLayer))
                {
                    continue;
                }

                var distanceOrigin = preferPalaceDistance && innerEntry != null
                    ? innerEntry.position
                    : attacker.transform.position;
                var distance = (candidate.transform.position - distanceOrigin).sqrMagnitude;
                var insertIndex = bufferedCount;
                while (insertIndex > 0 && nearestDistanceBuffer[insertIndex - 1] > distance)
                {
                    insertIndex--;
                }

                if (insertIndex >= ReachabilityCandidateLimit)
                {
                    continue;
                }

                var moveEnd = Mathf.Min(bufferedCount, ReachabilityCandidateLimit - 1);
                for (var moveIndex = moveEnd; moveIndex > insertIndex; moveIndex--)
                {
                    nearestTargetBuffer[moveIndex] = nearestTargetBuffer[moveIndex - 1];
                    nearestDistanceBuffer[moveIndex] = nearestDistanceBuffer[moveIndex - 1];
                }

                nearestTargetBuffer[insertIndex] = candidate;
                nearestDistanceBuffer[insertIndex] = distance;
                bufferedCount = Mathf.Min(bufferedCount + 1, ReachabilityCandidateLimit);
            }

            for (var index = 0; index < bufferedCount; index++)
            {
                var candidate = nearestTargetBuffer[index];
                if (candidate != null && candidate.IsAlive && attacker.CanReachTarget(candidate))
                {
                    ClearNearestTargetBuffer(bufferedCount);
                    return candidate;
                }
            }

            CastleTarget fallback = null;
            var fallbackDistance = float.PositiveInfinity;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (!IsCandidateAllowed(candidate, scope, requiredWallLayer) ||
                    IsBufferedTarget(candidate, bufferedCount))
                {
                    continue;
                }

                var distanceOrigin = preferPalaceDistance && innerEntry != null
                    ? innerEntry.position
                    : attacker.transform.position;
                var distance = (candidate.transform.position - distanceOrigin).sqrMagnitude;
                if (distance >= fallbackDistance || !attacker.CanReachTarget(candidate))
                {
                    continue;
                }

                fallback = candidate;
                fallbackDistance = distance;
            }

            ClearNearestTargetBuffer(bufferedCount);
            return fallback;
        }

        private bool IsCandidateAllowed(
            CastleTarget candidate,
            TargetCandidateScope scope,
            int requiredWallLayer)
        {
            if (candidate == null || !candidate.IsAlive)
            {
                return false;
            }

            switch (scope)
            {
                case TargetCandidateScope.OuterWall:
                    return candidate.TargetKind == CastleTargetKind.Wall &&
                           candidate.WallBand == CastleWallBand.OuterPerimeter;
                case TargetCandidateScope.OpenedDistrict:
                    return IsTargetInOpenedDistrict(candidate);
                case TargetCandidateScope.OpenedInwardWall:
                    return candidate.TargetKind == CastleTargetKind.Wall &&
                           candidate.WallBand != CastleWallBand.OuterPerimeter &&
                           (requiredWallLayer < 0 || candidate.WallDefenseLayer == requiredWallLayer) &&
                           IsTargetInOpenedDistrict(candidate);
                default:
                    return true;
            }
        }

        private bool IsTargetInOpenedDistrict(CastleTarget target)
        {
            if (target == null || !target.HasGenerationMetadata)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(target.DistrictId) && openedDistrictIds.Contains(target.DistrictId))
            {
                return true;
            }

            var owners = target.OwnerDistrictIds;
            for (var index = 0; index < owners.Count; index++)
            {
                if (openedDistrictIds.Contains(owners[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsBufferedTarget(CastleTarget candidate, int count)
        {
            for (var index = 0; index < count; index++)
            {
                if (nearestTargetBuffer[index] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearNearestTargetBuffer(int count)
        {
            for (var index = 0; index < count; index++)
            {
                nearestTargetBuffer[index] = null;
                nearestDistanceBuffer[index] = 0f;
            }
        }

        public void Attack(CastleAssaultUnit attacker, CastleTarget target, float damage)
        {
            if (!IsRunning || attacker == null || target == null || !target.IsAlive)
            {
                return;
            }

            target.Health.ApplyDamage(new DamageRequest(null, Mathf.Max(0f, damage), target.transform.position));
        }

        public CastleAssaultUnit FindTurretTarget(
            Vector3 origin,
            float range,
            CastleTurretTargetPriority priority,
            float projectileRadius = 0f)
        {
            if (!IsRunning)
            {
                return null;
            }

            var rangeSquared = Mathf.Max(0f, range) * Mathf.Max(0f, range);
            var candidateCount = 0;
            for (var index = 0; index < activeUnits.Count; index++)
            {
                var unit = activeUnits[index];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                var offset = unit.transform.position - origin;
                offset.y = 0f;
                var distance = offset.sqrMagnitude;
                if (distance > rangeSquared)
                {
                    continue;
                }

                var tier = ResolveTurretTargetTier(unit.UnitId);
                var insertIndex = candidateCount;
                while (insertIndex > 0 && IsTurretCandidateBetter(
                           priority,
                           tier,
                           distance,
                           turretTierBuffer[insertIndex - 1],
                           turretDistanceBuffer[insertIndex - 1]))
                {
                    insertIndex--;
                }

                if (insertIndex >= TurretCandidateLimit)
                {
                    continue;
                }

                var moveEnd = Mathf.Min(candidateCount, TurretCandidateLimit - 1);
                for (var moveIndex = moveEnd; moveIndex > insertIndex; moveIndex--)
                {
                    turretCandidateBuffer[moveIndex] = turretCandidateBuffer[moveIndex - 1];
                    turretDistanceBuffer[moveIndex] = turretDistanceBuffer[moveIndex - 1];
                    turretTierBuffer[moveIndex] = turretTierBuffer[moveIndex - 1];
                }

                turretCandidateBuffer[insertIndex] = unit;
                turretDistanceBuffer[insertIndex] = distance;
                turretTierBuffer[insertIndex] = tier;
                candidateCount = Mathf.Min(candidateCount + 1, TurretCandidateLimit);
            }

            CastleAssaultUnit result = null;
            for (var index = 0; index < candidateCount; index++)
            {
                var candidate = turretCandidateBuffer[index];
                if (candidate != null && candidate.IsAlive &&
                    !IsTurretLineBlocked(origin, candidate.TurretHitPoint, projectileRadius))
                {
                    result = candidate;
                    break;
                }
            }

            ClearTurretCandidateBuffer(candidateCount);
            return result;
        }

        public bool IsTurretLineBlocked(Vector3 origin, Vector3 targetPoint, float clearanceRadius = 0f)
        {
            if (!wallBlockerIndexReady)
            {
                return IsTurretLineBlockedFallback(origin, targetPoint, clearanceRadius);
            }

            var queryStamp = NextWallBlockerQueryStamp();
            var startX = Mathf.FloorToInt(origin.x / WallSpatialCellSize);
            var startZ = Mathf.FloorToInt(origin.z / WallSpatialCellSize);
            var endX = Mathf.FloorToInt(targetPoint.x / WallSpatialCellSize);
            var endZ = Mathf.FloorToInt(targetPoint.z / WallSpatialCellSize);
            var deltaX = targetPoint.x - origin.x;
            var deltaZ = targetPoint.z - origin.z;
            var stepX = deltaX > 0f ? 1 : deltaX < 0f ? -1 : 0;
            var stepZ = deltaZ > 0f ? 1 : deltaZ < 0f ? -1 : 0;
            var nextBoundaryX = stepX > 0 ? (startX + 1) * WallSpatialCellSize : startX * WallSpatialCellSize;
            var nextBoundaryZ = stepZ > 0 ? (startZ + 1) * WallSpatialCellSize : startZ * WallSpatialCellSize;
            var maximumX = stepX == 0 ? float.PositiveInfinity : (nextBoundaryX - origin.x) / deltaX;
            var maximumZ = stepZ == 0 ? float.PositiveInfinity : (nextBoundaryZ - origin.z) / deltaZ;
            var deltaTimeX = stepX == 0 ? float.PositiveInfinity : WallSpatialCellSize / Mathf.Abs(deltaX);
            var deltaTimeZ = stepZ == 0 ? float.PositiveInfinity : WallSpatialCellSize / Mathf.Abs(deltaZ);
            var neighborRadius = Mathf.CeilToInt(Mathf.Max(0f, clearanceRadius) / WallSpatialCellSize);
            var cellX = startX;
            var cellZ = startZ;
            for (var step = 0; step < WallSpatialTraversalLimit; step++)
            {
                if (DoesWallCellBlockLine(
                        cellX,
                        cellZ,
                        neighborRadius,
                        queryStamp,
                        origin,
                        targetPoint,
                        clearanceRadius))
                {
                    return true;
                }

                if (cellX == endX && cellZ == endZ)
                {
                    break;
                }

                if (maximumX < maximumZ)
                {
                    cellX += stepX;
                    maximumX += deltaTimeX;
                }
                else if (maximumZ < maximumX)
                {
                    cellZ += stepZ;
                    maximumZ += deltaTimeZ;
                }
                else
                {
                    cellX += stepX;
                    cellZ += stepZ;
                    maximumX += deltaTimeX;
                    maximumZ += deltaTimeZ;
                }
            }

            return false;
        }

        private static bool IsTurretCandidateBetter(
            CastleTurretTargetPriority priority,
            int leftTier,
            float leftDistance,
            int rightTier,
            float rightDistance)
        {
            if (priority == CastleTurretTargetPriority.Nearest)
            {
                return leftDistance < rightDistance;
            }

            return leftTier < rightTier || leftTier == rightTier && leftDistance > rightDistance;
        }

        private void ClearTurretCandidateBuffer(int count)
        {
            for (var index = 0; index < count; index++)
            {
                turretCandidateBuffer[index] = null;
                turretDistanceBuffer[index] = 0f;
                turretTierBuffer[index] = 0;
            }
        }

        private bool IsTurretLineBlockedFallback(Vector3 origin, Vector3 targetPoint, float clearanceRadius)
        {
            if (targets == null)
            {
                return false;
            }

            for (var index = 0; index < targets.Length; index++)
            {
                var target = targets[index];
                if (target != null && target.BlocksTurretLine(origin, targetPoint, clearanceRadius))
                {
                    return true;
                }
            }

            return false;
        }

        private int NextWallBlockerQueryStamp()
        {
            if (wallBlockerQueryStamp < int.MaxValue)
            {
                return ++wallBlockerQueryStamp;
            }

            wallBlockerQueryStamp = 1;
            for (var index = 0; index < wallBlockers.Count; index++)
            {
                wallBlockers[index].QueryStamp = 0;
            }

            return wallBlockerQueryStamp;
        }

        private bool DoesWallCellBlockLine(
            int cellX,
            int cellZ,
            int neighborRadius,
            int queryStamp,
            Vector3 origin,
            Vector3 targetPoint,
            float clearanceRadius)
        {
            for (var offsetZ = -neighborRadius; offsetZ <= neighborRadius; offsetZ++)
            {
                for (var offsetX = -neighborRadius; offsetX <= neighborRadius; offsetX++)
                {
                    var key = new Vector2Int(cellX + offsetX, cellZ + offsetZ);
                    if (!wallBlockersByCell.TryGetValue(key, out var blockers))
                    {
                        continue;
                    }

                    for (var index = 0; index < blockers.Count; index++)
                    {
                        var blocker = blockers[index];
                        if (!blocker.Alive || blocker.QueryStamp == queryStamp)
                        {
                            continue;
                        }

                        blocker.QueryStamp = queryStamp;
                        if (CastleTurretLineOfFireMath.IntersectsPlanarBounds(
                                origin,
                                targetPoint,
                                blocker.Bounds,
                                clearanceRadius))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public bool TryFindFirstTurretHit(
            Vector3 from,
            Vector3 to,
            float projectileRadius,
            ISet<int> excludedIds,
            out CastleAssaultUnit target,
            out Vector3 hitPoint)
        {
            target = null;
            hitPoint = to;
            var segment = to - from;
            var segmentLengthSquared = segment.sqrMagnitude;
            var bestRatio = float.PositiveInfinity;
            for (var index = 0; index < activeUnits.Count; index++)
            {
                var unit = activeUnits[index];
                if (unit == null || !unit.IsAlive || excludedIds != null && excludedIds.Contains(unit.GetInstanceID()))
                {
                    continue;
                }

                var ratio = segmentLengthSquared <= 0.000001f
                    ? 0f
                    : Mathf.Clamp01(Vector3.Dot(unit.TurretHitPoint - from, segment) / segmentLengthSquared);
                var closest = from + segment * ratio;
                var combinedRadius = Mathf.Max(0.01f, projectileRadius) + unit.TurretCollisionRadius;
                if ((unit.TurretHitPoint - closest).sqrMagnitude > combinedRadius * combinedRadius || ratio >= bestRatio)
                {
                    continue;
                }

                target = unit;
                hitPoint = closest;
                bestRatio = ratio;
            }

            return target != null;
        }

        public bool ApplyTurretDamage(CastleAssaultUnit target, float damage, Vector3 hitPoint)
        {
            if (!IsRunning || target == null || !target.IsAlive || damage <= 0f)
            {
                return false;
            }

            target.ApplyDefenderDamage(damage, hitPoint);
            return true;
        }

        public int ApplyTurretAreaDamage(
            Vector3 center,
            float radius,
            float damage,
            CastleTurretRuntime sourceTurret = null)
        {
            if (!IsRunning || radius <= 0f || damage <= 0f)
            {
                return 0;
            }

            var count = 0;
            for (var index = activeUnits.Count - 1; index >= 0; index--)
            {
                var unit = activeUnits[index];
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

                var resolvedDamage = CastleTurretDamageMath.ResolveExplosionDamage(damage, radius, distance);
                if (ApplyTurretDamage(unit, resolvedDamage, unit.TurretHitPoint))
                {
                    sourceTurret?.ReportHit(resolvedDamage);
                    count++;
                }
            }

            return count;
        }

        public GameObject RentTurretObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return poolScope == null ? null : poolScope.Rent(prefab, position, rotation);
        }

        public void ReturnTurretObject(GameObject instance)
        {
            poolScope?.Return(instance);
        }

        public bool PlayTurretCue(SfxCue cue, Vector3 position)
        {
            return cue != null && combatFeedback != null && combatFeedback.PlayMonsterCue(cue, position);
        }

        private static int ResolveTurretTargetTier(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return 2;
            }

            if (unitId.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 0;
            }

            return unitId.IndexOf("elite", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 2;
        }

        public bool SelectUnit(int unitIndex)
        {
            if (!IsRunning || remainingDeployments == null || unitIndex < 0 ||
                unitIndex >= remainingDeployments.Length || remainingDeployments[unitIndex] <= 0)
            {
                return false;
            }

            selectedUnitIndex = unitIndex;
            SetStatus($"{ResolveUnitLabel(unitIndex)} 선택 · {remainingDeployments[unitIndex]}마리 배치 가능");
            UpdateHud();
            return true;
        }

        public bool TryDeployAtScreenPosition(Vector2 screenPosition)
        {
            if (!IsRunning || selectedUnitIndex < 0)
            {
                SetStatus("먼저 몬스터를 선택하세요");
                return false;
            }

            if (!deploymentZone.TryResolveSpawnPoint(deploymentCamera, screenPosition, out var spawnPoint))
            {
                SetStatus("초록색 외곽에만 배치할 수 있습니다");
                return false;
            }

            return DeploySelectedUnit(spawnPoint);
        }

        public bool TryDeployAtWorldPosition(Vector3 worldPosition)
        {
            if (!IsRunning || selectedUnitIndex < 0 || !deploymentZone.ContainsWorldPosition(worldPosition) ||
                !NavMesh.SamplePosition(worldPosition, out var hit, 1f, NavMesh.AllAreas) ||
                !deploymentZone.ContainsWorldPosition(hit.position))
            {
                return false;
            }

            return DeploySelectedUnit(hit.position);
        }

        private bool DeploySelectedUnit(Vector3 spawnPosition)
        {
            if (startData == null || remainingDeployments == null || selectedUnitIndex < 0 ||
                selectedUnitIndex >= remainingDeployments.Length || remainingDeployments[selectedUnitIndex] <= 0)
            {
                return false;
            }

            var direction = innerEntry == null ? Vector3.forward : innerEntry.position - spawnPosition; // 성 안쪽을 바라보게 배치
            direction.y = 0f;
            var rotation = direction.sqrMagnitude <= 0.001f
                ? Quaternion.identity
                : Quaternion.LookRotation(direction.normalized, Vector3.up);
            var snapshot = startData.Party.Units[selectedUnitIndex];
            var assaultPrefab = snapshot?.RuntimeAssetSet?.VisualAdapterPrefab;
            if (assaultPrefab == null)
            {
                Debug.LogError($"Castle Raid requires a formal Monster visual adapter. Unit={snapshot?.UnitId}");
                SetStatus("몬스터 실행 자산을 확인해주세요");
                return false;
            }

            var instance = poolScope.Rent(assaultPrefab, spawnPosition, rotation); // 편성 몬스터별 정식 Adapter 대여
            var unit = instance == null ? null : instance.GetComponent<CastleAssaultUnit>();
            if (instance != null && unit == null)
            {
                unit = instance.AddComponent<CastleAssaultUnit>(); // Adapter를 CastleRaid NavMesh 실행기로 조립
            }

            if (unit == null)
            {
                Debug.LogError("Castle Raid could not create a CastleAssaultUnit.");
                if (instance != null)
                {
                    poolScope.Return(instance);
                }

                return false;
            }

            var agent = instance.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh)
            {
                agent.Warp(spawnPosition); // NavMeshAgent 위치도 즉시 동기화
            }

            var deployedIndex = selectedUnitIndex; // 선택 해제 전에 번호 보관
            unit.Initialize(snapshot, this);
            unit.Damaged += HandleUnitDamaged;
            unit.Died += HandleUnitDied;
            activeUnits.Add(unit);
            remainingDeployments[deployedIndex]--;
            deployedCount++;
            selectedUnitIndex = remainingDeployments[deployedIndex] > 0 ? deployedIndex : -1;
            SetStatus(remainingDeployments[deployedIndex] > 0
                ? $"{ResolveUnitLabel(deployedIndex)} {remainingDeployments[deployedIndex]}마리 남음 · 계속 배치할 수 있습니다"
                : $"{ResolveUnitLabel(deployedIndex)} 3마리 배치 완료");
            UpdateHud();
            return true;
        }

        private void HandleUnitDied(CastleAssaultUnit unit)
        {
            unit.Damaged -= HandleUnitDamaged;
            unit.Died -= HandleUnitDied;
            StartCoroutine(ReturnDeadUnitAfterFeedback(unit)); // 사망 연출이 끝난 뒤 풀 반환
            if (AllDeployedUnitsDead() && !HasRemainingDeployments()) // 추가 배치도 불가능할 때만 패배
            {
                SetStatus("습격 실패");
                IsRunning = false;
                UpdateDeploymentZoneVisual();
                context.Exit.Fail(new CastleRaidResult(false));
            }
        }

        private void HandleTargetDamaged(CastleTarget target, DamageReport report)
        {
            if (target != null)
            {
                combatFeedback?.PlayDamage(
                    report.Request.HitPoint,
                    report.AppliedDamage,
                    FloatingNumberStyle.EnemyDamage,
                    target.GetInstanceID());
            }
        }

        private void HandleUnitDamaged(CastleAssaultUnit unit, DamageReport report)
        {
            if (unit != null)
            {
                combatFeedback?.PlayDamage(
                    report.Request.HitPoint,
                    report.AppliedDamage,
                    FloatingNumberStyle.PlayerDamage,
                    unit.GetInstanceID());
            }
        }

        private IEnumerator ReturnDeadUnitAfterFeedback(CastleAssaultUnit unit)
        {
            yield return new WaitForSeconds(unit.DeathPresentationDuration + DeadUnitPoolReturnPaddingSeconds);
            activeUnits.Remove(unit);
            assaultRouteStates.Remove(unit);
            if (unit == null)
            {
                yield break;
            }

            unit.Shutdown();
            poolScope?.Return(unit.gameObject);
        }

        private void HandleTargetDestroyed(CastleTarget target)
        {
            if (!IsRunning || target == null)
            {
                return;
            }

            RemoveCachedTarget(target);
            if (target.BlocksNavigation)
            {
                QueueUnitPathRefresh(target.TargetKind == CastleTargetKind.Wall); // 돌파 때는 목표 단계도 재평가
            }

            if (target.TargetKind == CastleTargetKind.Wall)
            {
                RegisterDestroyedWall(target);
                StartCoroutine(CreateBreachLinkAfterCarving(target)); // Carving 제거가 반영된 뒤 양쪽 NavMesh를 찾는다
                return;
            }

            if (target.TargetKind == CastleTargetKind.MainCastle)
            {
                combatFeedback?.PlayClimax(target.transform.position, CombatClimaxStrength.Strong);
                SetStatus(string.Empty); // 최종 결과는 AppRoot 공통창에서 표시
                IsRunning = false;
                UpdateHud();
                var result = new CastleRaidResult(true); // 본성 파괴만 승리 처리
                context?.Exit.Complete(result); // 저장 성공 뒤 AppRoot 공통 결과창에서 표시
            }
        }

        private IEnumerator CreateBreachLinkAfterCarving(CastleTarget wall)
        {
            var created = false;
            for (var attempt = 0; attempt < BreachLinkRetryLimit; attempt++)
            {
                yield return new WaitForSeconds(0.1f);
                if (!IsRunning || wall == null)
                {
                    yield break;
                }

                if (TryCreateBreachLink(wall, out var retryable))
                {
                    RegisterOpenedRoute(wall);
                    created = true;
                    break;
                }

                if (!retryable)
                {
                    break;
                }
            }

            if (created) // 살아 있는 다음 성벽을 넘지 않는 짧은 연결만 허용
            {
                openedDefenseLayer = Mathf.Max(openedDefenseLayer, wall.WallDefenseLayer);
            }

            RequestUnitNavigationRefreshes(true);
            if (!innerPathOpen && !verifyingInnerPath)
            {
                verifyingInnerPath = true;
                StartCoroutine(VerifyInnerPath()); // 성벽 제거 뒤 실제 NavMesh 통로 확인
            }
        }

        private void RegisterDestroyedWall(CastleTarget wall)
        {
            if (wall == null || !wall.HasGenerationMetadata)
            {
                return;
            }

            if (wall.WallBand == CastleWallBand.OuterPerimeter)
            {
                hasDestroyedOuterWall = true;
            }
        }

        private void RegisterOpenedRoute(CastleTarget wall)
        {
            if (wall == null || !wall.HasGenerationMetadata)
            {
                return;
            }

            if (!routeEstablished)
            {
                routeEstablished = true;
                openedDistrictIds.Clear();
                breachFrontierWalls.Clear(); // 첫 실제 통로를 이번 진격의 기준으로 고정
            }

            AddOpenedDistrict(wall.DistrictId);
            var owners = wall.OwnerDistrictIds;
            for (var index = 0; index < owners.Count; index++)
            {
                AddOpenedDistrict(owners[index]);
            }
        }

        private void AddOpenedDistrict(string districtIdValue)
        {
            if (!string.IsNullOrWhiteSpace(districtIdValue))
            {
                openedDistrictIds.Add(districtIdValue);
            }
        }

        private void QueueUnitPathRefresh(bool retarget)
        {
            unitPathRetargetQueued |= retarget;
            if (unitPathRefreshQueued)
            {
                return;
            }

            unitPathRefreshQueued = true;
            StartCoroutine(RefreshUnitPathsAfterObstacleChange());
        }

        private IEnumerator RefreshUnitPathsAfterObstacleChange()
        {
            yield return null; // NavMeshObstacle carving 제거 반영 프레임 대기
            unitPathRefreshQueued = false;
            if (!IsRunning)
            {
                unitPathRetargetQueued = false;
                yield break;
            }

            var retarget = unitPathRetargetQueued;
            unitPathRetargetQueued = false;
            RequestUnitNavigationRefreshes(retarget);
        }

        private IEnumerator VerifyInnerPath()
        {
            SetStatus("성벽 파괴 · 진입 경로 확인 중");
            var elapsed = 0f;
            var consecutiveValidChecks = 0;
            while (IsRunning && elapsed < InnerPathVerificationTimeoutSeconds)
            {
                yield return new WaitForSeconds(InnerPathVerificationIntervalSeconds);
                elapsed += InnerPathVerificationIntervalSeconds;
                if (HasEntryBreachCandidate() && ValidatePathToInnerEntry()) // 외곽 돌파와 실제 경로를 함께 확인
                {
                    consecutiveValidChecks++;
                    if (consecutiveValidChecks >= RequiredConsecutivePathChecks)
                    {
                        innerPathOpen = true; // 연속 확인에 성공해야 진입 허용
                        RequestUnitNavigationRefreshes(true); // 내부 우선순위를 짧게 분산해 다시 고른다
                        break;
                    }
                }
                else
                {
                    consecutiveValidChecks = 0;
                }
            }

            verifyingInnerPath = false;
            if (!IsRunning)
            {
                yield break;
            }

            if (innerPathOpen)
            {
                SetStatus("성 내부로 진격합니다");
                yield break;
            }

            if (!HasEntryBreachCandidate())
            {
                SetStatus(hasGenerationTargetMetadata
                    ? "진입로 앞 성벽을 더 파괴하세요"
                    : "모서리만으로는 진입할 수 없습니다 · 인접 성벽도 파괴하세요");
                yield break;
            }

            if (HasAliveWallTarget())
            {
                SetStatus("진입로가 부족합니다 · 다른 성벽을 공격하세요");
                yield break;
            }

            Debug.LogError("Castle Raid inner NavMesh path remained blocked after all wall targets were destroyed.");
            SetStatus("진입 경로가 막혀 있습니다");
        }

        private void RequestUnitNavigationRefreshes(bool retarget)
        {
            for (var index = 0; index < activeUnits.Count; index++)
            {
                var unit = activeUnits[index];
                if (unit != null && unit.IsAlive)
                {
                    unit.RequestNavigationRefresh(retarget, index % 9 * 0.01f);
                }
            }
        }

        private bool ValidatePathToInnerEntry()
        {
            if (breachEntryPoints.Count == 0)
            {
                return false;
            }

            if (innerPathProbe == null)
            {
                innerPathProbe = new NavMeshPath();
            }

            var aliveUnitCount = 0;
            for (var i = 0; i < activeUnits.Count; i++)
            {
                var unit = activeUnits[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                aliveUnitCount++;
                var agent = unit.GetComponent<NavMeshAgent>();
                if (agent == null || !agent.isOnNavMesh)
                {
                    return false;
                }

                var canReachBreach = false;
                for (var pointIndex = 0; pointIndex < breachEntryPoints.Count; pointIndex++)
                {
                    if (NavMesh.CalculatePath(
                            agent.nextPosition,
                            breachEntryPoints[pointIndex],
                            NavMesh.AllAreas,
                            innerPathProbe) &&
                        innerPathProbe.status == NavMeshPathStatus.PathComplete)
                    {
                        canReachBreach = true;
                        break;
                    }
                }

                if (!canReachBreach)
                {
                    return false;
                }
            }

            return aliveUnitCount > 0; // 검사할 생존 유닛이 있어야 성공
        }

        private bool HasEntryBreachCandidate()
        {
            return hasGenerationTargetMetadata
                ? hasDestroyedOuterWall && breachEntryPoints.Count > 0
                : HasNonCornerDestroyedWall();
        }

        private bool HasNonCornerDestroyedWall()
        {
            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var minZ = float.PositiveInfinity;
            var maxZ = float.NegativeInfinity;
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target == null || target.TargetKind != CastleTargetKind.Wall)
                {
                    continue;
                }

                var position = target.transform.position;
                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
                minZ = Mathf.Min(minZ, position.z);
                maxZ = Mathf.Max(maxZ, position.z);
            }

            if (float.IsInfinity(minX))
            {
                return false;
            }

            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target == null || target.IsAlive || target.TargetKind != CastleTargetKind.Wall)
                {
                    continue;
                }

                var position = target.transform.position;
                var onHorizontalEdge = Mathf.Abs(position.z - minZ) <= CornerCoordinateTolerance ||
                                       Mathf.Abs(position.z - maxZ) <= CornerCoordinateTolerance;
                var onVerticalEdge = Mathf.Abs(position.x - minX) <= CornerCoordinateTolerance ||
                                     Mathf.Abs(position.x - maxX) <= CornerCoordinateTolerance;
                if (!onHorizontalEdge || !onVerticalEdge) // 두 변에 동시에 닿으면 모서리 성벽
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasAliveWallTarget()
        {
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target != null && target.IsAlive && target.TargetKind == CastleTargetKind.Wall)
                {
                    return true;
                }
            }

            return false;
        }

        private void AttackWithDefenders()
        {
            var victim = FindNearestAliveUnit();
            if (victim == null)
            {
                return;
            }

            for (var i = 0; i < targets.Length; i++)
            {
                var defender = targets[i];
                if (defender == null || !defender.IsAlive || defender.TargetKind != CastleTargetKind.Defender)
                {
                    continue;
                }

                if (defender.TryGetComponent<CastleDefenderUnit>(out _))
                {
                    continue; // 절차 생성 수비대는 이동 AI가 개별 공격 주기를 관리한다
                }

                if ((defender.transform.position - victim.transform.position).sqrMagnitude <=
                    defenderDetectionRange * defenderDetectionRange) // 고정 Stage 수비대 호환
                {
                    victim.ApplyDefenderDamage(defenderDamage, victim.transform.position);
                }
            }
        }

        private CastleAssaultUnit FindNearestAliveUnit()
        {
            return FindNearestAliveUnit(Vector3.zero, float.PositiveInfinity);
        }

        public CastleAssaultUnit FindNearestAliveUnit(Vector3 origin, float maximumDistance)
        {
            CastleAssaultUnit nearest = null;
            var nearestDistance = maximumDistance >= float.MaxValue
                ? float.PositiveInfinity
                : Mathf.Max(0f, maximumDistance) * Mathf.Max(0f, maximumDistance);
            for (var i = 0; i < activeUnits.Count; i++)
            {
                var unit = activeUnits[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                var offset = unit.transform.position - origin;
                offset.y = 0f;
                var distance = offset.sqrMagnitude;
                if (distance <= nearestDistance)
                {
                    nearest = unit;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private bool AllDeployedUnitsDead()
        {
            for (var i = 0; i < activeUnits.Count; i++)
            {
                if (activeUnits[i] != null && activeUnits[i].IsAlive)
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasRemainingDeployments()
        {
            if (remainingDeployments == null)
            {
                return false;
            }

            for (var index = 0; index < remainingDeployments.Length; index++)
            {
                if (remainingDeployments[index] > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void BindUnitButtons()
        {
            UnbindUnitButtons();
            unitButtonActions = new UnityAction[unitButtons.Length];
            for (var i = 0; i < unitButtons.Length; i++)
            {
                if (unitButtons[i] == null)
                {
                    continue;
                }

                var unitIndex = i; // 버튼마다 반복문 번호를 따로 보관
                unitButtonActions[i] = () => SelectUnit(unitIndex);
                unitButtons[i].onClick.AddListener(unitButtonActions[i]);
            }
        }

        private void UnbindUnitButtons()
        {
            if (unitButtons == null || unitButtonActions == null)
            {
                unitButtonActions = null;
                return;
            }

            for (var i = 0; i < unitButtons.Length && i < unitButtonActions.Length; i++)
            {
                if (unitButtons[i] != null && unitButtonActions[i] != null)
                {
                    unitButtons[i].onClick.RemoveListener(unitButtonActions[i]);
                }
            }

            unitButtonActions = null;
        }

        private void Cancel()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            UpdateDeploymentZoneVisual();
            context.Exit.Cancel(); // 보상 없이 콘텐츠 종료
        }

        private void UpdateHud()
        {
            UpdateDeploymentZoneVisual();
            if (deploymentText != null)
            {
                var limit = startData == null ? 0 : startData.DeploymentLimit;
                deploymentText.text = $"배치 {deployedCount}/{limit}";
            }

            var buttonCount = unitButtons == null ? 0 : unitButtons.Length;
            for (var i = 0; i < buttonCount; i++)
            {
                var button = unitButtons[i];
                if (button == null)
                {
                    continue;
                }

                var available = remainingDeployments != null && i < remainingDeployments.Length;
                var remaining = available ? remainingDeployments[i] : 0;
                var exhausted = available && remaining <= 0;
                button.interactable = IsRunning && available && !exhausted;
                if (button.targetGraphic != null)
                {
                    button.targetGraphic.color = !available || exhausted
                        ? new Color(0.18f, 0.2f, 0.22f, 0.9f)
                        : i == selectedUnitIndex
                            ? new Color(1f, 0.58f, 0.15f, 1f)
                            : new Color(0.12f, 0.3f, 0.36f, 0.96f);
                }

                if (unitButtonLabels != null && i < unitButtonLabels.Length && unitButtonLabels[i] != null)
                {
                    unitButtonLabels[i].text = !available
                        ? $"슬롯 {i + 1}\n비어 있음"
                        : exhausted
                            ? $"{ResolveUnitLabel(i)}\n소진"
                            : $"{ResolveUnitLabel(i)}\n×{remaining}";
                }
            }
        }

        private void UpdateDeploymentZoneVisual()
        {
            deploymentZone?.SetVisualVisible(IsRunning && HasRemainingDeployments());
        }

        private bool TryCreateBreachLink(CastleTarget wall, out bool retryable)
        {
            retryable = false;
            if (wall == null || innerEntry == null)
            {
                return false;
            }

            if (routeEstablished && wall.HasGenerationMetadata &&
                wall.WallBand == CastleWallBand.OuterPerimeter && !IsTargetInOpenedDistrict(wall))
            {
                return false; // 첫 진격로와 무관한 외곽 통로는 추가로 열지 않는다
            }

            if (linkedWallIds.Contains(wall.GetInstanceID()))
            {
                return true;
            }

            var inward = CastleBreachLinkMath.ResolveInwardDirection(
                wall.transform.position,
                innerEntry.position,
                wall.WallNeighborMask);
            if (inward.sqrMagnitude <= 0.5f)
            {
                return false;
            }

            var wallHalfExtent = 0.46f;
            if (wallBlockersByTarget.TryGetValue(wall, out var destroyedBlocker))
            {
                wallHalfExtent = Mathf.Abs(inward.x) > 0.5f
                    ? destroyedBlocker.Bounds.extents.x
                    : destroyedBlocker.Bounds.extents.z;
            }

            var endpointDistance = Mathf.Max(BreachMinimumProbeDistance, wallHalfExtent + BreachEndpointPadding);
            var outsideProbe = wall.transform.position - inward * endpointDistance;
            var insideProbe = wall.transform.position + inward * endpointDistance;
            var clearanceRadius = ResolveBreachClearanceRadius() + BreachLinkWidth * 0.5f;
            if (TryFindAliveWallBlockingBreach(
                    wall.transform.position,
                    inward,
                    outsideProbe,
                    insideProbe,
                    clearanceRadius,
                    out var plannedBlockingWall))
            {
                AddBreachFrontierWall(plannedBlockingWall); // NavMesh 탐색 전에 바로 안쪽 벽부터 고정
                return false;
            }

            if (!NavMesh.SamplePosition(outsideProbe, out var outside, BreachProbeRadius, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(insideProbe, out var inside, BreachProbeRadius, NavMesh.AllAreas))
            {
                retryable = true; // Carving 복구 지연은 같은 파괴 지점에서 다시 확인한다
                return false;
            }

            if (!CastleBreachLinkMath.AreEndpointsOnOppositeSides(
                    wall.transform.position,
                    inward,
                    outside.position,
                    inside.position) ||
                Vector3.Distance(outside.position, inside.position) > BreachMaximumLinkDistance)
            {
                Debug.LogWarning($"Castle Raid rejected an unsafe breach span. Wall={wall.name}", wall);
                return false;
            }

            if (TryFindAliveWallBlockingBreach(
                    wall.transform.position,
                    inward,
                    outside.position,
                    inside.position,
                    clearanceRadius,
                    out var blockingWall))
            {
                AddBreachFrontierWall(blockingWall);
                return false;
            }

            var linkRoot = new GameObject($"BreachLink_{wall.name}");
            linkRoot.SetActive(false);
            linkRoot.transform.SetParent(transform, false);
            var link = linkRoot.AddComponent<NavMeshLink>();
            link.agentTypeID = 0;
            link.area = 0;
            link.startPoint = linkRoot.transform.InverseTransformPoint(outside.position);
            link.endPoint = linkRoot.transform.InverseTransformPoint(inside.position);
            link.width = BreachLinkWidth;
            link.bidirectional = true;
            link.costModifier = -1f;
            link.autoUpdate = false;
            linkRoot.SetActive(true);
            link.UpdateLink();
            breachLinkObjects.Add(linkRoot);
            breachEntryPoints.Add(inside.position);
            linkedWallIds.Add(wall.GetInstanceID());
            breachRoutes.Add(new BreachRouteRecord
            {
                Wall = wall,
                WallPosition = wall.transform.position,
                OutsidePoint = outside.position,
                InsidePoint = inside.position,
                Inward = inward,
                DefenseLayer = Mathf.Max(0, wall.WallDefenseLayer)
            });
            return true;
        }

        private float ResolveBreachClearanceRadius()
        {
            var settings = NavMesh.GetSettingsByID(0);
            return settings.agentRadius > 0f ? settings.agentRadius : 0.5f;
        }

        private bool TryFindAliveWallBlockingBreach(
            Vector3 breachedWallPosition,
            Vector3 inward,
            Vector3 outside,
            Vector3 inside,
            float clearanceRadius,
            out CastleTarget blockingWall)
        {
            blockingWall = null;
            var blocked = false;
            var nearestForwardDistance = float.PositiveInfinity;
            for (var index = 0; index < wallBlockers.Count; index++)
            {
                var blocker = wallBlockers[index];
                if (!blocker.Alive || blocker.Target == null ||
                    !CastleTurretLineOfFireMath.IntersectsPlanarBounds(
                        outside,
                        inside,
                        blocker.Bounds,
                        clearanceRadius))
                {
                    continue;
                }

                blocked = true;
                var relative = blocker.Bounds.center - breachedWallPosition;
                relative.y = 0f;
                var forwardDistance = Vector3.Dot(relative, inward);
                if (forwardDistance > 0.05f && forwardDistance < nearestForwardDistance)
                {
                    blockingWall = blocker.Target;
                    nearestForwardDistance = forwardDistance;
                }
            }

            return blocked;
        }

        private void AddBreachFrontierWall(CastleTarget wall)
        {
            if (wall != null && wall.IsAlive && !breachFrontierWalls.Contains(wall))
            {
                breachFrontierWalls.Add(wall); // 현재 틈 바로 안쪽의 성벽을 다음 목표로 고정
            }
        }

        private void ClearBreachLinks()
        {
            for (var index = breachLinkObjects.Count - 1; index >= 0; index--)
            {
                if (breachLinkObjects[index] != null)
                {
                    Destroy(breachLinkObjects[index]);
                }
            }

            breachLinkObjects.Clear();
            breachEntryPoints.Clear();
            linkedWallIds.Clear();
            breachRoutes.Clear();
            assaultRouteStates.Clear();
            supportClaims.Clear();
            expiredSupportClaimKeys.Clear();
            breachFrontierWalls.Clear();
            openedDistrictIds.Clear();
            hasDestroyedOuterWall = false;
            routeEstablished = false;
            openedDefenseLayer = -1;
        }

        private string ResolveUnitLabel(int unitIndex)
        {
            var units = startData?.Party?.Units;
            if (units == null || unitIndex < 0 || unitIndex >= units.Length || units[unitIndex] == null)
            {
                return $"부대 {unitIndex + 1}";
            }

            return string.IsNullOrWhiteSpace(units[unitIndex].DisplayName)
                ? $"부대 {unitIndex + 1}"
                : units[unitIndex].DisplayName;
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        private static int GetPriority(CastleTargetKind kind)
        {
            switch (kind)
            {
                case CastleTargetKind.Wall:
                    return 0;
                case CastleTargetKind.Defender:
                    return 1;
                case CastleTargetKind.Building:
                    return 2;
                case CastleTargetKind.MainCastle:
                    return 3;
                default:
                    return int.MaxValue;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            ScenePoolScope pool,
            CombatFeedbackPlayer feedback,
            Camera worldCamera,
            CastleDeploymentZone zone,
            Transform pathProbe,
            CastleTarget[] castleTargets,
            TMP_Text deployment,
            TMP_Text status,
            Button[] rosterButtons,
            TMP_Text[] rosterLabels,
            Button exit)
        {
            poolScope = pool;
            combatFeedback = feedback;
            deploymentCamera = worldCamera;
            deploymentZone = zone;
            innerEntry = pathProbe;
            targets = castleTargets;
            deploymentText = deployment;
            statusText = status;
            unitButtons = rosterButtons;
            unitButtonLabels = rosterLabels;
            exitButton = exit;
        }

        public void EditorConfigureRuntimeGeneration(
            CastleRuntimeStageGenerator stageGenerator,
            TMP_Text castleInfo,
            Button doubleWall,
            Button tripleWall,
            Button quadrupleWall,
            Button regenerate)
        {
            runtimeStageGenerator = stageGenerator;
            castleInfoText = castleInfo;
            doubleWallButton = doubleWall;
            tripleWallButton = tripleWall;
            quadrupleWallButton = quadrupleWall;
            regenerateCastleButton = regenerate;
        }
#endif
    }
}
