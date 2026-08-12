using System;
using System.Collections;
using System.Collections.Generic;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Input;
using ProjectMT.Shared.UI;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.GuardianTrial
{
    // 08.06 안건준 추가 - 수호자의 탑 진행·결과 총괄
    // 식량 대소동(FoodRiotController)과 코드/프리팹/데이터를 완전히 분리한 전용 콘텐츠 컨트롤러.
    // 차이점: 적은 시작할 때 한 번만 스폰(재보충 없음), 제한 시간 1분, 방어 건물 4개 관리, 상단 처치/남은 게이지 표시.
    // 08.07 안건준 수정 - (1)재입장 시 항상 처음 위치에서 시작 (2)군단장이 건물 근처에 오면 아군이 건물을
    // 우선 공격 (3)건물별 버프·파괴 알림 (4)난이도에 따른 적 체력·공격력·방어력·건물 체력 스케일링(레벨당 +10%,
    // 적 "수"는 더 이상 난이도로 늘지 않음) (5)적을 여러 구역에 나눠서 스폰 (6)적 프리팹을 여러 종류 중
    // 랜덤 선택할 수 있는 구조 추가.
    [DisallowMultipleComponent]
    public sealed class GuardiansTowerController : MonoBehaviour, IContentController
    {
        // 08.07 안건준 수정 - 난이도 1당 적 수를 늘리던 방식은 폐지. 이제 시작 적 수는 난이도와 무관하게 고정이고,
        // 5초마다 오는 증원도 이 상한(100마리) 안에서만 스폰된다(상한 도달 시 증원 정지).
        private const int MaxEnemyCount = 100;
        // 08.07 안건준 수정 - 난이도 1당 "적 체력/공격력/방어력"과 "건물 체력"이 전부 +10%씩 오르도록 통일.
        private const float DifficultyStatGrowthPerLevel = 0.10f;
        private const float StructureAggroHoldSeconds = 0.35f; // 군단장이 범위 밖으로 나가면 이 시간 안에 자연히 원래 대상으로 복귀
        private const float NotificationDisplaySeconds = 2f; // 08.07 안건준 추가 - 알림 문구 1개당 표시 시간(큐 처리 간격)
        // 08.07 안건준 수정 - 4번 건물(Spawn) 버프의 "살아있는 동안 초당 소환" 방식은 폐지했다.
        // 대신 아래 ReinforcementInterval/ReinforcementEnemyCount로 "건물 버프와 무관하게" 일정 주기마다
        // 적을 소환하는 증원을 상시 적용한다. 이 증원 스폰에는 상한(MaxEnemyCount)이 적용되지 않는다.
        private const float ReinforcementInterval = 5f; // 08.07 안건준 추가 - 증원 주기(초)
        private const int ReinforcementEnemyCount = 2; // 08.07 안건준 추가 - 주기마다 추가로 소환할 적 수
        // 08.07 안건준 추가 - 4번 건물(AttackBoost)이 파괴되는 순간 아군 전체에게 1회 적용하는 공격력 배율.
        private const float AllyAttackBuffMultiplier = 2f;

        [Header("Runtime")]
        [SerializeField] private CombatWorld combatWorld; // 유닛 생성·정리 공간
        [SerializeField] private GameObject followerPrefab; // 아군 추종자 원본
        [SerializeField] private GameObject enemyPrefab; // 침입하는 적 원본 (수호자의 탑 전용, 기본값/하위 호환용)
        // 08.07 안건준 추가 - 적 종류가 여러 개면 이 목록에서 매 스폰마다 랜덤으로 하나를 고른다.
        // 비워두면 기존처럼 enemyPrefab 하나만 사용한다(현재 프로젝트에는 적 몬스터 변형이 없어 비워둔 상태).
        [SerializeField] private GameObject[] enemyPrefabVariants = new GameObject[0];
        [SerializeField] private GameObject commanderRoot; // 직접 조작 군단장
        [SerializeField] private CommanderMoveController commanderMove; // 군단장 이동 입력
        [SerializeField] private Transform enemyAreaCenter; // 적 스폰 구역 중심(보통 맵 정중앙, 군단장 시작 위치와 동일)
        // 08.07 안건준 수정 - 적이 군단장 바로 옆(구역 내부)에도 스폰되어 너무 가깝다는 문제가 있어,
        // 사각형 내부를 채우는 대신 이 반쪽 크기로 정해지는 사각형 "테두리"를 따라서만 스폰하도록 변경.
        // 값을 키우면 더 바깥쪽(화면 속 파란 네모 위치)에서 스폰되어 다가온다.
        // 08.07 안건준 수정 - 시작 시 적 스폰 테두리를 더 바깥으로 벌려 군단장과의 거리를 확보한다.
        // x=좌우 반쪽 크기, y=앞뒤 반쪽 크기. 값을 키울수록 빨간 네모처럼 더 넓은 테두리에서 스폰된다.
        [SerializeField] private Vector2 enemyAreaHalfExtents = new Vector2(12f, 9f); // 스폰 테두리 반쪽 크기 (기존 8,6 → 12,9)
        [SerializeField] private GuardiansTowerStructure[] structures = new GuardiansTowerStructure[0]; // 네 모서리 방어 건물

        [Header("Follower Tuning")]
        // 08.07 안건준 추가 - 이동 범위가 넓어져 추종 몬스터가 늦게 따라오는 문제 보정 (수호자의 탑 전용 값)
        [SerializeField, Min(0f)] private float followerMinMoveSpeed = 5.5f; // 군단장 이동속도(4)보다 항상 빠르게 하여 즉시 따라오게 함
        [SerializeField, Min(0f)] private float followerDetectionRange = 6.5f; // 대형에서 벗어나 적을 탐색하는 거리
        [SerializeField, Min(0f)] private float followerLeashRange = 3f; // 전투 중이라도 이 거리 이상 벌어지면 즉시 대형으로 복귀 (기존 8 → 축소)
        // 08.07 안건준 추가 - 군단장이 방어 건물에 이 거리 안으로 들어오면 아군이 적 대신 건물을 우선 공격한다.
        // 08.07 안건준 수정 - 군단장이 건물 근처에 올 때 아군이 건물을 우선 공격하는 반경.
        // 값이 작으면 건물에 거의 붙어야만 발동되고, 키우면 사진의 파란 원처럼 멀리서도 발동된다.
        [SerializeField, Min(0f)] private float structureAggroRadius = 12f;
        // 08.07 안건준 추가 - 군단장·아군이 건물(기둥)과 겹쳐 보이지 않도록 하는 충돌 반경.
        [SerializeField, Min(0f)] private float structureObstacleRadius = 1f; // 건물 쪽 반경(대략 기둥 두께)
        [SerializeField, Min(0f)] private float unitCollisionRadius = 0.4f; // 군단장/아군 쪽 반경

        [Header("HUD")]
        [SerializeField] private TMP_Text timerText; // 남은 시간 표시
        [SerializeField] private TMP_Text resultText; // 조작 안내·결과 문구 + 08.07 추가: 버프 해제 알림도 여기에 표시
        [SerializeField] private TMP_Text enemyGaugeText; // "잡은 수량 X / 남은 수량 Y"
        [SerializeField] private Image enemyGaugeFillImage; // Type=Filled 상단 게이지
        [SerializeField] private Button exitButton; // 콘텐츠 나가기 버튼
        [SerializeField] private ContentClearOverlay clearOverlay; // 종료 결과 화면

        private ContentContext context; // 결과 반환 통로
        private GuardiansTowerStartData startData; // 이번 판 시작 정보
        private float timeRemaining; // 남은 제한 시간
        private int killCount; // 이번 판 처치 수
        private int enemyTotalCount; // 시작 시 스폰한 총 적 수 (게이지 분모, 판 중 고정)
        private int enemyAliveCount; // 현재 생존 적 수
        private readonly List<UnitActor> followers = new List<UnitActor>(); // 08.07 안건준 추가 - 건물 어그로 강제 지정 대상
        private GuardiansTowerStructureBuffs structureBuffs; // 08.07 안건준 추가 - 건물별 버프 담당
        private Coroutine notificationClearRoutine; // 08.07 안건준 추가 - 알림 문구 자동 소거
        // 08.07 안건준 수정 - 버프 해제 알림과 회복 알림이 동시에 발생해도 겹쳐서 잘리지 않도록 순서대로 큐에 쌓아 표시한다.
        private readonly Queue<string> notificationQueue = new Queue<string>();
        private float reinforcementTimer; // 08.07 안건준 추가 - 버프와 무관한 상시 증원 주기 타이머
        private float enemyStatMultiplier = 1f; // 08.07 안건준 추가 - 난이도에 따른 적 체력/공격력/방어력 배율(스폰마다 적용)

        public bool IsRunning { get; private set; }

        public void Initialize(ContentContext contentContext)
        {
            Shutdown(); // 재초기화 전 이전 판 정리
            context = contentContext ?? throw new ArgumentNullException(nameof(contentContext));
            startData = contentContext.StartData as GuardiansTowerStartData;
            if (startData == null || startData.Party == null)
            {
                throw new ArgumentException("GuardiansTowerStartData is required.", nameof(contentContext));
            }

            if (combatWorld == null || followerPrefab == null || enemyPrefab == null || commanderRoot == null)
            {
                throw new InvalidOperationException("Guardians Tower runtime references are missing.");
            }

            combatWorld.Clear(); // 이전 판의 남은 유닛 제거
            clearOverlay?.Hide();
            commanderRoot.SetActive(true);
            commanderMove?.ResetToInitialPosition(); // 08.07 안건준 추가 - 나갔다가 다시 들어오면 항상 처음(중앙) 위치에서 시작
            commanderMove?.SetInputEnabled(true);
            exitButton?.onClick.AddListener(Cancel);
            timeRemaining = startData.DurationSeconds;
            killCount = 0;
            reinforcementTimer = 0f; // 08.07 안건준 추가 - 재입장마다 증원 타이머 초기화
            followers.Clear();

            // 08.07 안건준 수정 - 난이도 스케일링: 클리어할 때마다 1씩 오르는 난이도 값을 읽어
            // 적 체력·공격력·방어력, 건물 체력을 전부 레벨당 +10% 적용한다. (적 "수"는 더 이상 난이도로 늘지 않음)
            var difficultyLevel = Mathf.Max(0, context.Progress?.View.GuardiansTowerDifficultyLevel ?? 0);
            var difficultyMultiplier = 1f + DifficultyStatGrowthPerLevel * difficultyLevel;
            enemyStatMultiplier = difficultyMultiplier; // SpawnEnemy에서 매 스폰마다 사용
            var structureHealthMultiplier = difficultyMultiplier;
            enemyTotalCount = Mathf.Clamp(startData.EnemyCount, 1, MaxEnemyCount); // 08.07 안건준 수정 - 난이도로 늘어나지 않고 시작 수만 사용(상한만 유지)
            enemyAliveCount = 0;
            IsRunning = true;
            if (resultText != null)
            {
                resultText.text = "이동 키나 조이스틱으로 움직이세요";
            }

            SpawnFollowers();
            InitializeStructures(structureHealthMultiplier); // 방어 건물 체력을 (기준값 x 난이도 배율)로 초기화
            // 08.07 안건준 수정 - 4번 건물 파괴 시 아군 공격력 버프 적용 콜백(ApplyAllyAttackBuff)을 함께 전달
            structureBuffs = new GuardiansTowerStructureBuffs(structures, ShowCenterNotification, ApplyAllyAttackBuff);
            structureBuffs.Reset();
            for (var i = 0; i < enemyTotalCount; i++)
            {
                SpawnEnemy(i); // 시작할 때 한 번만 스폰 (재보충 없음)
            }

            UpdateHud();
        }

        public void Shutdown()
        {
            clearOverlay?.Hide();
            exitButton?.onClick.RemoveListener(Cancel);
            commanderMove?.SetInputEnabled(false);
            ShutdownStructures();
            structureBuffs?.Shutdown();
            structureBuffs = null;
            followers.Clear();
            if (notificationClearRoutine != null)
            {
                StopCoroutine(notificationClearRoutine);
                notificationClearRoutine = null;
            }

            notificationQueue.Clear(); // 08.07 안건준 추가 - 재시작 시 이전 판 알림이 남아있지 않도록 정리

            combatWorld?.Clear();
            if (commanderRoot != null)
            {
                commanderRoot.SetActive(false);
            }

            context = null;
            startData = null;
            IsRunning = false;
        }

        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
            UpdateHud();
            UpdateStructureAggro(); // 08.07 안건준 추가 - 군단장이 건물 근처에 오면 아군이 건물을 우선 공격
            structureBuffs?.Tick(Time.deltaTime); // 08.07 안건준 추가 - 방어력 환불·체력회복 버프 처리
            UpdateReinforcements(Time.deltaTime); // 08.07 안건준 수정 - 버프와 무관하게 5초마다 적 2마리 추가 소환
            if (timeRemaining <= 0f)
            {
                // 08.07 안건준 추가 - 시간이 다 됐는데 적이 남아있으면 클리어가 아니라 실패로 처리한다.
                if (enemyAliveCount > 0)
                {
                    Fail("시간 초과");
                }
                else
                {
                    Complete();
                }
            }
        }

        // 08.07 안건준 추가 - 군단장·아군이 건물(기둥)을 파고들지 않도록 매 프레임 뒤로 밀어낸다.
        // 08.07 안건준 수정 - 건물을 공격 중인 아군은 밀어내지 않는다(밀어내기 반경이 공격 사거리보다
        // 넓으면 계속 접근->밀림이 반복돼 공격을 못 하고 부들부들 떠는 것처럼 보이는 문제가 있었음).
        private void LateUpdate()
        {
            if (!IsRunning || structures == null || structures.Length == 0)
            {
                return;
            }

            if (commanderRoot != null)
            {
                PushOutOfStructures(commanderRoot.transform, null); // 군단장은 건물을 공격하지 않으므로 항상 밀어냄
            }

            for (var i = 0; i < followers.Count; i++)
            {
                var follower = followers[i];
                if (follower != null && follower.IsAlive)
                {
                    PushOutOfStructures(follower.transform, follower);
                }
            }
        }

        private void PushOutOfStructures(Transform unit, UnitActor actor)
        {
            for (var i = 0; i < structures.Length; i++)
            {
                var structure = structures[i];
                if (structure == null || !structure.IsAlive)
                {
                    continue; // 부서진 건물은 더 이상 막지 않는다
                }

                if (actor != null && actor.IsForcedTargeting(structure.Health))
                {
                    continue; // 08.07 안건준 추가 - 지금 이 건물을 공격 중이면 사거리 안까지 접근을 허용
                }

                var minDistance = ResolveObstacleRadius(structure) + unitCollisionRadius;
                var structurePosition = structure.transform.position;
                var offset = unit.position - structurePosition;
                offset.y = 0f;
                var distance = offset.magnitude;
                if (distance >= minDistance)
                {
                    continue;
                }

                var pushDirection = distance > 0.0001f ? offset / distance : Vector3.forward;
                var corrected = structurePosition + pushDirection * minDistance;
                corrected.y = unit.position.y;
                unit.position = corrected;
            }
        }

        // 08.07 안건준 추가 - 건물의 실제 렌더링 크기(Renderer.bounds)를 기준으로 충돌 반경을 구한다.
        // 기둥을 나중에 더 크게/작게 조절해도 손으로 값을 다시 맞출 필요가 없다. 렌더러가 없으면
        // 인스펙터에 지정한 기본값(structureObstacleRadius)을 그대로 쓴다.
        private float ResolveObstacleRadius(GuardiansTowerStructure structure)
        {
            if (structure.TryGetComponent<Renderer>(out var structureRenderer))
            {
                var extents = structureRenderer.bounds.extents;
                return Mathf.Max(extents.x, extents.z);
            }

            return structureObstacleRadius;
        }

        private void SpawnFollowers()
        {
            var offsets = new[] // 군단장 뒤쪽 추종 대형
            {
                new Vector3(-1.2f, 0f, -0.9f),
                new Vector3(0f, 0f, -1.2f),
                new Vector3(1.2f, 0f, -0.9f),
                new Vector3(-0.7f, 0f, -2f),
                new Vector3(0.7f, 0f, -2f)
            };
            var partyUnits = startData.Party.Units;
            for (var i = 0; i < partyUnits.Length && i < offsets.Length; i++)
            {
                var spawnPosition = commanderRoot.transform.position + offsets[i];
                var stats = partyUnits[i].Stats;
                stats.moveSpeed = Mathf.Max(stats.moveSpeed, followerMinMoveSpeed); // 군단장보다 느려서 뒤처지는 현상 방지
                var request = new UnitSpawnRequest(
                    partyUnits[i].UnitId,
                    stats,
                    UnitTeam.Player,
                    visualTint: partyUnits[i].VisualTint,
                    runtimeAssetSet: partyUnits[i].RuntimeAssetSet);
                var actor = combatWorld.SpawnUnit(followerPrefab, request, spawnPosition, Quaternion.identity);
                if (actor == null)
                {
                    continue;
                }

                actor.SetFollowAnchor(commanderRoot.transform, offsets[i], followerDetectionRange, followerLeashRange);
                actor.Died += HandleFollowerDied; // 08.07 안건준 추가 - 전멸 여부 판정용
                followers.Add(actor); // 08.07 안건준 추가 - 건물 어그로 강제 지정 대상 목록
            }
        }

        // 08.07 안건준 추가 - 아군 몬스터가 죽을 때마다 목록에서 제거하고, 전멸했다면 실패 처리한다.
        private void HandleFollowerDied(UnitActor actor)
        {
            followers.Remove(actor);
            if (IsRunning && followers.Count == 0)
            {
                Fail("전멸");
            }
        }

        // 08.07 안건준 추가 - 군단장이 살아있는 방어 건물 중 하나와 structureAggroRadius 이내로 가까워지면,
        // 모든 추종자의 공격을 (원래 적 대신) 그 건물로 강제 지정한다. 범위를 벗어나면 잠시 후 자연히
        // 원래 자동 전투(FindNearestOpponent)로 복귀한다.
        private void UpdateStructureAggro()
        {
            if (structures == null || structures.Length == 0 || followers.Count == 0 || commanderRoot == null)
            {
                return;
            }

            GuardiansTowerStructure nearestStructure = null;
            var nearestDistanceSquared = structureAggroRadius * structureAggroRadius;
            var commanderPosition = commanderRoot.transform.position;
            for (var i = 0; i < structures.Length; i++)
            {
                var structure = structures[i];
                if (structure == null || !structure.IsAlive)
                {
                    continue;
                }

                var offset = structure.transform.position - commanderPosition;
                offset.y = 0f;
                var distanceSquared = offset.sqrMagnitude;
                if (distanceSquared <= nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearestStructure = structure;
                }
            }

            if (nearestStructure == null)
            {
                return; // 근처에 건물이 없으면 강제 지정하지 않는다 (기존 강제 지정은 시간이 지나면 자연히 만료됨)
            }

            for (var i = 0; i < followers.Count; i++)
            {
                var follower = followers[i];
                if (follower != null && follower.IsAlive)
                {
                    follower.ForceTarget(nearestStructure.Health, StructureAggroHoldSeconds); // 매 프레임 갱신해 계속 유지
                }
            }
        }

        private void InitializeStructures(float healthMultiplier)
        {
            if (structures == null)
            {
                return;
            }

            for (var i = 0; i < structures.Length; i++)
            {
                structures[i]?.Initialize(healthMultiplier); // 08.07 안건준 수정 - 난이도 배율 반영
            }
        }

        private void ShutdownStructures()
        {
            if (structures == null)
            {
                return;
            }

            for (var i = 0; i < structures.Length; i++)
            {
                structures[i]?.Shutdown();
            }
        }

        // 08.07 안건준 수정 - 건물 버프와 무관하게 ReinforcementInterval마다 ReinforcementEnemyCount마리씩
        // 상시 증원한다(이전의 "4번 건물이 살아있는 동안만" 방식은 폐지). 시간 제한이 있는 동안 계속 진행되지만,
        // 08.07 안건준 수정 - 이제는 초기 스폰 수와 합쳐서 MaxEnemyCount(100마리) 상한을 넘지 않는다.
        // 상한에 도달하면 타이머는 계속 흐르되 더 이상 스폰하지 않는다(나중에 처치로 총수가 줄어드는 게 아니라
        // "누적 스폰 수" 기준 상한이라 이후에도 다시 늘지 않음).
        private void UpdateReinforcements(float deltaTime)
        {
            if (enemyTotalCount >= MaxEnemyCount)
            {
                return; // 상한에 도달하면 증원 자체를 멈춘다
            }

            reinforcementTimer += deltaTime;
            while (reinforcementTimer >= ReinforcementInterval)
            {
                reinforcementTimer -= ReinforcementInterval;
                var remaining = MaxEnemyCount - enemyTotalCount; // 상한까지 남은 여유만큼만 스폰
                if (remaining <= 0)
                {
                    break;
                }

                SpawnReinforcements(Mathf.Min(ReinforcementEnemyCount, remaining));
            }
        }

        // 08.07 안건준 추가 - 4번 건물(AttackBoost)이 파괴되는 순간 GuardiansTowerStructureBuffs가 호출하는 콜백.
        // 현재 생존 중인 아군 전체에게 공격력 배율을 즉시 적용한다(이번 판 동안 계속 유지되는 1회성 영구 버프).
        private void ApplyAllyAttackBuff()
        {
            for (var i = 0; i < followers.Count; i++)
            {
                followers[i]?.SetDamageMultiplier(AllyAttackBuffMultiplier);
            }
        }

        // 08.07 안건준 수정 - 증원으로 적을 추가 소환한다. enemyTotalCount를 그대로 늘려서 상단 게이지
        // 분모(총 마리 수)에도 반영되며, 호출하는 쪽(UpdateReinforcements)에서 이미 MaxEnemyCount 상한을
        // 넘지 않도록 count를 제한해서 넘겨준다.
        private void SpawnReinforcements(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var sequence = enemyTotalCount;
                enemyTotalCount++;
                SpawnEnemy(sequence);
            }

            UpdateHud();
        }

        private void SpawnEnemy(int sequence)
        {
            var position = GetSpawnPosition(sequence); // 08.07 안건준 수정 - 스폰 구역 테두리를 따라 분산 스폰
            // 08.07 안건준 수정 - 체력/공격력/방어력에 난이도 배율(enemyStatMultiplier, 레벨당 +10%)을 적용한다.
            // (참고: 방어력은 현재 0이라 배율을 곱해도 0이며, 이 프로젝트의 데미지 계산은 defense 수치를
            // 아직 사용하지 않는다. 방어력을 실제로 전투에 반영하려면 별도의 피해 감소 공식이 필요하다.)
            var stats = new UnitStatsSnapshot // 침입 적: 직접 이동·공격하여 건물·아군을 위협
            {
                maxHealth = 6f * enemyStatMultiplier,
                damage = 4f * enemyStatMultiplier,
                defense = 0f * enemyStatMultiplier,
                moveSpeed = 2.2f,
                attackRange = 0.6f,
                attackInterval = 1f,
                projectileSpeed = 0f,
                ranged = false
            };
            var prefab = PickEnemyPrefab(); // 08.07 안건준 추가 - 여러 종류 중 랜덤 선택(현재는 목록이 비어 있어 기존 프리팹 그대로 사용)
            var request = new UnitSpawnRequest($"guardians_tower_enemy_{sequence}", stats, UnitTeam.Enemy);
            var actor = combatWorld.SpawnUnit(prefab, request, position, Quaternion.identity);
            if (actor == null)
            {
                return;
            }

            enemyAliveCount++;
            actor.Died += HandleEnemyDied; // 요청: 재보충 없이 처치 수만 집계
            structureBuffs?.RegisterEnemy(actor.Health); // 08.07 안건준 추가 - 스폰 시점의 건물 버프(체력)를 즉시 반영
        }

        // 08.07 안건준 추가 - 적 프리팹이 여러 종류 등록되어 있으면 그중 하나를 무작위로 고른다.
        // 지금은 프로젝트에 적 몬스터 변형이 없어 목록이 비어 있고, 항상 기존 enemyPrefab을 사용한다.
        // 나중에 몬스터가 추가되면 enemyPrefabVariants 배열에 등록하기만 하면 자동으로 랜덤 스폰에 포함된다.
        private GameObject PickEnemyPrefab()
        {
            if (enemyPrefabVariants == null || enemyPrefabVariants.Length == 0)
            {
                return enemyPrefab;
            }

            var index = UnityEngine.Random.Range(0, enemyPrefabVariants.Length);
            var picked = enemyPrefabVariants[index];
            return picked != null ? picked : enemyPrefab;
        }

        // 08.07 안건준 수정 - 스폰 구역 "내부"를 격자로 채우면 군단장이 있는 중앙 부근에도 적이
        // 스폰되어 버려서 너무 가깝다는 문제가 있었다. 이제는 enemyAreaHalfExtents로 정해지는
        // 사각형의 "테두리(둘레)"를 따라서만 적을 고르게 분산 배치하고, 거기서부터 중앙으로 다가오게 한다.
        // 적 수가 많아져도(초기 스폰은 최대 MaxEnemyCount마리, 이후 증원은 무제한) 테두리 전체에 고르게
        // 퍼지며, 겹치지 않도록 약간 무작위로 흩뜨린다.
        private Vector3 GetSpawnPosition(int sequence)
        {
            var center = enemyAreaCenter == null ? transform.position : enemyAreaCenter.position;
            var halfExtentX = Mathf.Max(0.5f, enemyAreaHalfExtents.x);
            var halfExtentZ = Mathf.Max(0.5f, enemyAreaHalfExtents.y);
            var count = Mathf.Max(1, enemyTotalCount);

            var perimeter = 2f * (2f * halfExtentX + 2f * halfExtentZ);
            var spacing = perimeter / count;
            var jitter = UnityEngine.Random.Range(spacing * -0.3f, spacing * 0.3f); // 완전히 일정한 간격이 되지 않도록 살짝 흩뜨림
            var distance = Mathf.Repeat(spacing * sequence + jitter, perimeter);

            var bottomEdgeLength = 2f * halfExtentX;
            var rightEdgeLength = 2f * halfExtentZ;
            var topEdgeLength = 2f * halfExtentX;

            Vector3 offset;
            if (distance < bottomEdgeLength) // 아래쪽 변: 왼쪽 -> 오른쪽
            {
                offset = new Vector3(-halfExtentX + distance, 0f, -halfExtentZ);
            }
            else if (distance < bottomEdgeLength + rightEdgeLength) // 오른쪽 변: 아래 -> 위
            {
                offset = new Vector3(halfExtentX, 0f, -halfExtentZ + (distance - bottomEdgeLength));
            }
            else if (distance < bottomEdgeLength + rightEdgeLength + topEdgeLength) // 위쪽 변: 오른쪽 -> 왼쪽
            {
                offset = new Vector3(
                    halfExtentX - (distance - bottomEdgeLength - rightEdgeLength),
                    0f,
                    halfExtentZ);
            }
            else // 왼쪽 변: 위 -> 아래
            {
                offset = new Vector3(
                    -halfExtentX,
                    0f,
                    halfExtentZ - (distance - bottomEdgeLength - rightEdgeLength - topEdgeLength));
            }

            return center + offset;
        }

        private void HandleEnemyDied(UnitActor actor)
        {
            if (!IsRunning)
            {
                return;
            }

            killCount++;
            enemyAliveCount = Mathf.Max(0, enemyAliveCount - 1);
            UpdateHud();
            if (enemyAliveCount <= 0)
            {
                Complete(); // 남은 적을 모두 잡으면 제한 시간 전에도 즉시 종료
            }
        }

        private void Complete()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            commanderMove?.SetInputEnabled(false);
            combatWorld.Clear();
            if (resultText != null)
            {
                resultText.text = clearOverlay == null ? $"완료 · 처치 {killCount}" : string.Empty;
            }

            var result = new GuardiansTowerResult(killCount, cleared: true); // 08.07 안건준 수정 - 성공 클리어는 난이도 상승 대상
            context?.Exit.Complete(result); // 저장 성공 뒤 AppRoot 공통창에서 표시
        }

        // 08.07 안건준 추가 - 아군 전멸 또는 시간 초과(적 잔존) 시 호출되는 실패 처리.
        // 클리어(Complete)와 달리 제목이 "실패"로 표시되지만, 그때까지 처치한 수만큼 보상은 그대로 지급한다.
        private void Fail(string reason)
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            commanderMove?.SetInputEnabled(false);
            combatWorld.Clear();
            if (resultText != null)
            {
                resultText.text = clearOverlay == null ? $"실패 · {reason} · 처치 {killCount}" : string.Empty;
            }

            var result = new GuardiansTowerResult(killCount, cleared: false); // 08.07 안건준 수정 - 실패는 난이도를 올리지 않음
            context?.Exit.Fail(result); // 실패는 보상·열쇠 변경 없이 공통 결과만 표시
        }

        private void Cancel()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            commanderMove?.SetInputEnabled(false);
            combatWorld.Clear();
            context.Exit.Cancel(); // 보상 없이 콘텐츠 종료
        }

        // 08.07 안건준 추가 - 화면 중앙 문구(resultText)에 잠깐 알림을 띄우고 일정 시간 뒤 지운다.
        // 건물 파괴로 버프가 해제될 때 호출된다(예: "적의 방어력이 약해졌습니다.").
        // 08.07 안건준 수정 - 여러 알림(버프 해제 + 회복 등)이 거의 동시에 발생해도 서로 덮어써서
        // 못 보고 지나치는 일이 없도록, 즉시 표시하지 않고 큐에 쌓았다가 순서대로 하나씩 보여준다.
        private void ShowCenterNotification(string message)
        {
            if (resultText == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            notificationQueue.Enqueue(message);
            if (notificationClearRoutine == null)
            {
                notificationClearRoutine = StartCoroutine(ProcessNotificationQueue());
            }
        }

        private IEnumerator ProcessNotificationQueue()
        {
            while (notificationQueue.Count > 0)
            {
                var message = notificationQueue.Dequeue();
                if (resultText != null)
                {
                    resultText.text = message;
                }

                yield return new WaitForSeconds(NotificationDisplaySeconds);
            }

            if (resultText != null && IsRunning)
            {
                resultText.text = string.Empty;
            }

            notificationClearRoutine = null;
        }

        private void UpdateHud()
        {
            if (timerText != null)
            {
                timerText.text = $"남은 시간 {Mathf.CeilToInt(timeRemaining)}초";
            }

            if (enemyGaugeText != null)
            {
                enemyGaugeText.text = $"잡은 수량 {killCount} / 남은 수량 {enemyAliveCount}";
            }

            if (enemyGaugeFillImage != null)
            {
                enemyGaugeFillImage.fillAmount = enemyTotalCount <= 0 ? 0f : (float)enemyAliveCount / enemyTotalCount; // 남은 비율만큼 게이지 감소
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            CombatWorld world,
            GameObject follower,
            GameObject enemy,
            GameObject commander,
            CommanderMoveController moveController,
            Transform areaCenter,
            GuardiansTowerStructure[] structureList,
            TMP_Text timer,
            TMP_Text result,
            TMP_Text gaugeText,
            Image gaugeFill,
            Button exit,
            ContentClearOverlay overlay)
        {
            combatWorld = world;
            followerPrefab = follower;
            enemyPrefab = enemy;
            commanderRoot = commander;
            commanderMove = moveController;
            enemyAreaCenter = areaCenter;
            structures = structureList ?? new GuardiansTowerStructure[0];
            timerText = timer;
            resultText = result;
            enemyGaugeText = gaugeText;
            enemyGaugeFillImage = gaugeFill;
            exitButton = exit;
            clearOverlay = overlay;
        }
#endif
    }
}
