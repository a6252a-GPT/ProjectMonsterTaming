using System;
using System.Collections;
using System.Collections.Generic;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Pooling;
using ProjectMT.Shared.Unit;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProjectMT.Contents.CastleRaid
{
    [DisallowMultipleComponent]
    public sealed class CastleRaidController : MonoBehaviour, IContentController // 군단의 역습 배치·전투·결과 총괄
    {
        private const float InnerPathVerificationTimeoutSeconds = 2f; // 성벽 파괴 뒤 경로 확인 제한
        private const float InnerPathVerificationIntervalSeconds = 0.1f; // 경로 재검사 간격
        private const int RequiredConsecutivePathChecks = 5; // 열린 경로 연속 확인 횟수
        private const float CornerCoordinateTolerance = 0.05f; // 모서리 좌표 판정 오차
        private const float DeadUnitPoolReturnPaddingSeconds = 0.05f; // 사망 동작 종료 뒤 풀 반환 여유
        private const float BreachOutsideProbeDistance = 0.9f; // 성벽 바깥 NavMesh 탐색 거리
        private const float BreachInsideProbeDistance = 1.75f; // 성벽 안쪽 NavMesh 탐색 거리
        private const float BreachProbeRadius = 0.8f; // 끊긴 양쪽 NavMesh 표면 탐색 반경
        private const float BreachLinkWidth = 0.8f; // 파괴 타일 한 칸 통과 폭

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
        [SerializeField, Min(0.1f)] private float defenderRange = 8f; // 수비대 공격 거리

        private readonly List<CastleAssaultUnit> activeUnits = new List<CastleAssaultUnit>(); // 현재 출전 유닛
        private readonly List<GameObject> breachLinkObjects = new List<GameObject>(); // 파괴 성벽 런타임 연결
        private readonly List<Vector3> breachEntryPoints = new List<Vector3>(); // 파괴 지점 바로 안쪽 진입점
        private readonly HashSet<int> linkedWallIds = new HashSet<int>(); // 같은 성벽 중복 연결 차단
        private NavMeshPath innerPathProbe; // 진입 경로 검사 재사용 버퍼
        private ContentContext context; // 결과 반환 통로
        private CastleRaidStartData startData; // 이번 판 시작 정보
        private UnityAction[] unitButtonActions; // 해제용 버튼 콜백
        private bool[] deployedUnits; // 유닛별 배치 여부
        private int deployedCount; // 누적 배치 수
        private int selectedUnitIndex = -1; // 배치 대기 유닛 번호
        private float defenderAttackCooldown; // 다음 수비대 공격까지 시간
        private bool innerPathOpen; // 본성 진입 가능 여부
        private bool verifyingInnerPath; // 경로 확인 중복 방지
        private bool unitPathRefreshQueued; // 같은 프레임 파괴 경로 갱신 합치기
        private bool generationInProgress; // 중복 재생성 입력 차단

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
            deployedCount = 0;
            selectedUnitIndex = -1;
            deployedUnits = new bool[Mathf.Min(startData.DeploymentLimit, startData.Party.Units.Length)]; // 실제 배치 가능 인원만 추적
            innerPathProbe = new NavMeshPath(); // Unity 씬 인스턴스 생성이 끝난 뒤 네이티브 경로 버퍼 준비
            defenderAttackCooldown = defenderAttackInterval;
            innerPathOpen = false;
            verifyingInnerPath = false;
            unitPathRefreshQueued = false;
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target == null)
                {
                    continue;
                }

                target.Initialize(); // 씬에 고정된 목표물 재사용 초기화
                target.Damaged += HandleTargetDamaged;
                target.Destroyed += HandleTargetDestroyed;
            }

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
                targets[i].Shutdown();
            }

            for (var i = 0; i < activeUnits.Count; i++)
            {
                activeUnits[i]?.Shutdown();
            }

            activeUnits.Clear();
            poolScope?.ReturnAll();
            ClearBreachLinks();
            context = null;
            startData = null;
            deployedUnits = null;
            selectedUnitIndex = -1;
            verifyingInnerPath = false;
            unitPathRefreshQueued = false;
            IsRunning = false;
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

                unit.Tick(Time.deltaTime);
            }

            defenderAttackCooldown -= Time.deltaTime;
            if (defenderAttackCooldown <= 0f)
            {
                defenderAttackCooldown = defenderAttackInterval;
                AttackWithDefenders();
            }
        }

        public CastleTarget FindPriorityTarget(CastleAssaultUnit attacker)
        {
            var mustBreachWall = !innerPathOpen;
            var currentTargetMatchesPhase = attacker != null && attacker.Target != null &&
                                            (mustBreachWall
                                                ? attacker.Target.TargetKind == CastleTargetKind.Wall
                                                : attacker.Target.TargetKind != CastleTargetKind.Wall);
            if (currentTargetMatchesPhase && attacker.Target.IsAlive &&
                attacker.CanReachTarget(attacker.Target))
            {
                return attacker.Target; // 잡은 목표는 파괴할 때까지 유지해 대규모 성의 경로 재계산을 줄인다
            }

            CastleTarget bestStructure = null;
            CastleTarget bestWall = null;
            var bestPriority = int.MaxValue;
            var bestStructureDistance = float.PositiveInfinity;
            var bestWallDistance = float.PositiveInfinity;
            for (var i = 0; i < targets.Length; i++)
            {
                var candidate = targets[i];
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                if (mustBreachWall && candidate.TargetKind != CastleTargetKind.Wall)
                {
                    continue; // 성 밖에서는 건물·수비대 경로를 계산하지 않고 벽만 고른다
                }

                if (attacker != null && !attacker.CanReachTarget(candidate)) // 현재 NavMesh에서 닿는 목표만 선택
                {
                    continue;
                }

                var distance = attacker == null
                    ? 0f
                    : (candidate.transform.position - attacker.transform.position).sqrMagnitude;
                if (candidate.TargetKind == CastleTargetKind.Wall)
                {
                    if (distance < bestWallDistance)
                    {
                        bestWall = candidate;
                        bestWallDistance = distance;
                    }

                    continue;
                }

                var priority = GetPriority(candidate.TargetKind);
                if (priority < bestPriority || priority == bestPriority && distance < bestStructureDistance)
                {
                    bestStructure = candidate;
                    bestPriority = priority;
                    bestStructureDistance = distance;
                }
            }

            return mustBreachWall ? bestWall : bestStructure ?? bestWall;
        }

        public void Attack(CastleAssaultUnit attacker, CastleTarget target, float damage)
        {
            if (!IsRunning || attacker == null || target == null || !target.IsAlive)
            {
                return;
            }

            target.Health.ApplyDamage(new DamageRequest(null, Mathf.Max(0f, damage), target.transform.position));
        }

        public bool SelectUnit(int unitIndex)
        {
            if (!IsRunning || deployedUnits == null || unitIndex < 0 || unitIndex >= deployedUnits.Length ||
                deployedUnits[unitIndex])
            {
                return false;
            }

            selectedUnitIndex = unitIndex;
            SetStatus($"{ResolveUnitLabel(unitIndex)} 선택 · 초록색 외곽을 터치하세요");
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
            if (startData == null || deployedUnits == null || selectedUnitIndex < 0 ||
                selectedUnitIndex >= deployedUnits.Length || deployedUnits[selectedUnitIndex])
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
            deployedUnits[deployedIndex] = true;
            deployedCount++;
            selectedUnitIndex = -1;
            SetStatus("가장 가까운 성벽을 공격합니다");
            UpdateHud();
            return true;
        }

        private void HandleUnitDied(CastleAssaultUnit unit)
        {
            unit.Damaged -= HandleUnitDamaged;
            unit.Died -= HandleUnitDied;
            StartCoroutine(ReturnDeadUnitAfterFeedback(unit)); // 사망 연출이 끝난 뒤 풀 반환
            if (AllDeployedUnitsDead() && deployedCount >= startData.DeploymentLimit) // 추가 배치도 불가능할 때만 패배
            {
                SetStatus("습격 실패");
                IsRunning = false;
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

            if (target.BlocksNavigation)
            {
                QueueUnitPathRefresh(); // 성벽·건물 제거 뒤 생존 유닛 경로 갱신
            }

            if (target.TargetKind == CastleTargetKind.Wall)
            {
                TryCreateBreachLink(target); // 베이크 NavMesh의 외곽·내부 섬을 실제 보행 링크로 연결
                if (!innerPathOpen && !verifyingInnerPath)
                {
                    verifyingInnerPath = true;
                    StartCoroutine(VerifyInnerPath()); // 성벽 제거 뒤 실제 NavMesh 통로 확인
                }

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

        private void QueueUnitPathRefresh()
        {
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
                yield break;
            }

            for (var i = 0; i < activeUnits.Count; i++)
            {
                var unit = activeUnits[i];
                if (unit != null && unit.IsAlive)
                {
                    unit.RefreshNavigationPath();
                }
            }
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
                if (HasNonCornerDestroyedWall() && ValidatePathToInnerEntry()) // 모서리 꼼수와 일시 경로를 함께 차단
                {
                    consecutiveValidChecks++;
                    if (consecutiveValidChecks >= RequiredConsecutivePathChecks)
                    {
                        innerPathOpen = true; // 연속 확인에 성공해야 진입 허용
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

            if (!HasNonCornerDestroyedWall())
            {
                SetStatus("모서리만으로는 진입할 수 없습니다 · 인접 성벽도 파괴하세요");
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

                if ((defender.transform.position - victim.transform.position).sqrMagnitude <= defenderRange * defenderRange) // 제곱 거리로 범위 판정
                {
                    victim.ApplyDefenderDamage(defenderDamage, victim.transform.position);
                }
            }
        }

        private CastleAssaultUnit FindNearestAliveUnit()
        {
            CastleAssaultUnit nearest = null;
            var nearestDistance = float.PositiveInfinity;
            for (var i = 0; i < activeUnits.Count; i++)
            {
                var unit = activeUnits[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                var distance = unit.transform.position.sqrMagnitude;
                if (distance < nearestDistance)
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
            context.Exit.Cancel(); // 보상 없이 콘텐츠 종료
        }

        private void UpdateHud()
        {
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

                var available = deployedUnits != null && i < deployedUnits.Length;
                var deployed = available && deployedUnits[i];
                button.interactable = IsRunning && available && !deployed;
                if (button.targetGraphic != null)
                {
                    button.targetGraphic.color = deployed
                        ? new Color(0.18f, 0.2f, 0.22f, 0.9f)
                        : i == selectedUnitIndex
                            ? new Color(1f, 0.58f, 0.15f, 1f)
                            : new Color(0.12f, 0.3f, 0.36f, 0.96f);
                }

                if (unitButtonLabels != null && i < unitButtonLabels.Length && unitButtonLabels[i] != null)
                {
                    unitButtonLabels[i].text = deployed ? "배치 완료" : ResolveUnitLabel(i);
                }
            }
        }

        private bool TryCreateBreachLink(CastleTarget wall)
        {
            if (wall == null || innerEntry == null || linkedWallIds.Contains(wall.GetInstanceID()))
            {
                return false;
            }

            var inward = innerEntry.position - wall.transform.position;
            inward.y = 0f;
            if (inward.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            inward.Normalize();
            var outsideProbe = wall.transform.position - inward * BreachOutsideProbeDistance;
            var insideProbe = wall.transform.position + inward * BreachInsideProbeDistance;
            if (!NavMesh.SamplePosition(outsideProbe, out var outside, BreachProbeRadius, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(insideProbe, out var inside, BreachProbeRadius, NavMesh.AllAreas))
            {
                Debug.LogWarning($"Castle Raid breach link endpoints were not found. Wall={wall.name}", wall);
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
            return true;
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
