using System;
using System.Collections;
using System.Collections.Generic;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Pooling;
using ProjectMT.Shared.UI;
using ProjectMT.Shared.Unit;
using TMPro;
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
        private const float DeadUnitPoolReturnDelaySeconds = UnitVisualFeedback.DeathPulseDurationSeconds + 0.05f; // 사망 연출 뒤 반환

        [Header("Runtime")]
        [SerializeField] private ScenePoolScope poolScope; // 공격 유닛 재사용 풀
        [SerializeField] private CombatFeedbackPlayer combatFeedback; // 타격·파괴 연출
        [SerializeField] private GameObject assaultUnitPrefab; // 출전 유닛 원본
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
        [SerializeField] private ContentClearOverlay clearOverlay; // 승리 결과 화면

        [Header("Seed Balance")]
        [SerializeField, Min(0.1f)] private float defenderAttackInterval = 1.15f; // 수비대 공격 주기
        [SerializeField, Min(0f)] private float defenderDamage = 7f; // 수비대 1회 피해
        [SerializeField, Min(0.1f)] private float defenderRange = 8f; // 수비대 공격 거리

        private readonly List<CastleAssaultUnit> activeUnits = new List<CastleAssaultUnit>(); // 현재 출전 유닛
        private ContentContext context; // 결과 반환 통로
        private CastleRaidStartData startData; // 이번 판 시작 정보
        private UnityAction[] unitButtonActions; // 해제용 버튼 콜백
        private bool[] deployedUnits; // 유닛별 배치 여부
        private int deployedCount; // 누적 배치 수
        private int selectedUnitIndex = -1; // 배치 대기 유닛 번호
        private float defenderAttackCooldown; // 다음 수비대 공격까지 시간
        private bool innerPathOpen; // 본성 진입 가능 여부
        private bool verifyingInnerPath; // 경로 확인 중복 방지

        public bool IsRunning { get; private set; }
        public int DeployedCount => deployedCount;
        public int SelectedUnitIndex => selectedUnitIndex;
        public bool InnerPathOpen => innerPathOpen;

        public void Initialize(ContentContext contentContext)
        {
            Shutdown(); // 재초기화 전 이전 판 정리
            context = contentContext ?? throw new ArgumentNullException(nameof(contentContext));
            startData = contentContext.StartData as CastleRaidStartData;
            if (startData == null || startData.Party == null)
            {
                throw new ArgumentException("CastleRaidStartData is required.", nameof(contentContext));
            }

            if (poolScope == null || assaultUnitPrefab == null || deploymentCamera == null || deploymentZone == null ||
                targets == null || targets.Length == 0 || unitButtons == null || unitButtons.Length == 0)
            {
                throw new InvalidOperationException("Castle Raid runtime references are missing.");
            }

            poolScope.ReturnAll();
            clearOverlay?.Hide();
            activeUnits.Clear();
            deployedCount = 0;
            selectedUnitIndex = -1;
            deployedUnits = new bool[Mathf.Min(startData.DeploymentLimit, startData.Party.Units.Length)]; // 실제 배치 가능 인원만 추적
            defenderAttackCooldown = defenderAttackInterval;
            innerPathOpen = false;
            verifyingInnerPath = false;
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target == null)
                {
                    continue;
                }

                target.Initialize(); // 씬에 고정된 목표물 재사용 초기화
                target.Destroyed += HandleTargetDestroyed;
            }

            BindUnitButtons();
            exitButton?.onClick.AddListener(Cancel);
            IsRunning = true;
            SetStatus("두부를 선택한 뒤 초록색 외곽을 터치하세요");
            UpdateHud();
        }

        public void Shutdown()
        {
            StopAllCoroutines(); // 경로 확인·풀 반환 대기 중단
            clearOverlay?.Hide();
            UnbindUnitButtons();
            exitButton?.onClick.RemoveListener(Cancel);
            for (var i = 0; i < targets?.Length; i++)
            {
                if (targets[i] == null)
                {
                    continue;
                }

                targets[i].Destroyed -= HandleTargetDestroyed;
                targets[i].Shutdown();
            }

            for (var i = 0; i < activeUnits.Count; i++)
            {
                activeUnits[i]?.Shutdown();
            }

            activeUnits.Clear();
            poolScope?.ReturnAll();
            context = null;
            startData = null;
            deployedUnits = null;
            selectedUnitIndex = -1;
            verifyingInnerPath = false;
            IsRunning = false;
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
            CastleTarget best = null;
            var bestPriority = int.MaxValue;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < targets.Length; i++)
            {
                var candidate = targets[i];
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                var priority = GetPriority(candidate.TargetKind);
                if (!innerPathOpen && candidate.TargetKind != CastleTargetKind.Wall) // 진입 전에는 성벽만 공격
                {
                    continue;
                }

                if (innerPathOpen && candidate.TargetKind == CastleTargetKind.Wall) // 진입 뒤에는 내부 목표만 공격
                {
                    continue;
                }

                var distance = attacker == null
                    ? 0f
                    : (candidate.transform.position - attacker.transform.position).sqrMagnitude;
                if (priority < bestPriority || priority == bestPriority && distance < bestDistance) // 같은 종류면 가까운 목표 우선
                {
                    best = candidate;
                    bestPriority = priority;
                    bestDistance = distance;
                }
            }

            return best;
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
            SetStatus($"두부 {unitIndex + 1} 선택 · 초록색 외곽을 터치하세요");
            UpdateHud();
            return true;
        }

        public bool TryDeployAtScreenPosition(Vector2 screenPosition)
        {
            if (!IsRunning || selectedUnitIndex < 0)
            {
                SetStatus("먼저 두부를 선택하세요");
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
            var instance = poolScope.Rent(assaultUnitPrefab, spawnPosition, rotation); // 생성 대신 풀에서 대여
            var unit = instance == null ? null : instance.GetComponent<CastleAssaultUnit>();
            if (unit == null)
            {
                Debug.LogError("Castle assault prefab has no CastleAssaultUnit.");
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
            unit.Initialize(startData.Party.Units[deployedIndex], this);
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
            unit.Died -= HandleUnitDied;
            StartCoroutine(ReturnDeadUnitAfterFeedback(unit)); // 사망 연출이 끝난 뒤 풀 반환
            if (AllDeployedUnitsDead() && deployedCount >= startData.DeploymentLimit) // 추가 배치도 불가능할 때만 패배
            {
                SetStatus("습격 실패");
                IsRunning = false;
                context.Exit.Fail(new CastleRaidResult(false));
            }
        }

        private IEnumerator ReturnDeadUnitAfterFeedback(CastleAssaultUnit unit)
        {
            yield return new WaitForSeconds(DeadUnitPoolReturnDelaySeconds);
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

            if (target.TargetKind == CastleTargetKind.Wall)
            {
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
                SetStatus(clearOverlay == null ? "성 파괴 완료" : string.Empty);
                IsRunning = false;
                UpdateHud();
                var result = new CastleRaidResult(true); // 본성 파괴만 승리 처리
                if (clearOverlay != null &&
                    clearOverlay.TryShow("성을 파괴했습니다", "보상 연동 예정", () => CompleteClear(result)))
                {
                    return;
                }

                CompleteClear(result);
            }
        }

        private void CompleteClear(CastleRaidResult result)
        {
            context?.Exit.Complete(result);
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
            if (innerEntry == null ||
                !NavMesh.SamplePosition(innerEntry.position, out var end, 2f, NavMesh.AllAreas))
            {
                return false;
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

                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(agent.nextPosition, end.position, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
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
                    unitButtonLabels[i].text = deployed ? "배치 완료" : $"두부 {i + 1}";
                }
            }
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
            GameObject assaultPrefab,
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
            assaultUnitPrefab = assaultPrefab;
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
#endif
    }
}
