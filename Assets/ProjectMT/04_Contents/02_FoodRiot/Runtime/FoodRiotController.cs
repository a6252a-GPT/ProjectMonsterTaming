using System;
using System.Collections;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Input;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.FoodRiot
{
    [DisallowMultipleComponent]
    public sealed class FoodRiotController : MonoBehaviour, IContentController // 식량 대소동 진행·결과 총괄
    {
        [Header("Runtime")]
        [SerializeField] private CombatWorld combatWorld; // 유닛 생성·정리 공간
        [SerializeField] private GameObject followerPrefab; // 아군 추종자 원본
        [SerializeField] private GameObject vegetablePrefab; // 도망가는 야채 원본
        [SerializeField] private GameObject commanderRoot; // 직접 조작 군단장
        [SerializeField] private CommanderMoveController commanderMove; // 군단장 이동 입력
        [SerializeField] private Transform vegetableAreaCenter; // 야채 활동 구역 중심
        [SerializeField] private Vector2 vegetableAreaHalfExtents = new Vector2(5.5f, 3.5f); // 활동 구역 반쪽 크기

        [Header("HUD")]
        [SerializeField] private TMP_Text timerText; // 남은 시간 표시
        [SerializeField] private TMP_Text killText; // 처치 수 표시
        [SerializeField] private TMP_Text resultText; // 조작 안내·결과 문구
        [SerializeField] private Button exitButton; // 콘텐츠 나가기 버튼

        private ContentContext context; // 결과 반환 통로
        private FoodRiotStartData startData; // 이번 판 시작 정보
        private float timeRemaining; // 남은 제한 시간
        private int killCount; // 이번 판 처치 수
        private int spawnSequence; // 야채 고유 번호

        public bool IsRunning { get; private set; }

        public void Initialize(ContentContext contentContext)
        {
            Shutdown(); // 재초기화 전 이전 판 정리
            context = contentContext ?? throw new ArgumentNullException(nameof(contentContext));
            startData = contentContext.StartData as FoodRiotStartData;
            if (startData == null || startData.Party == null)
            {
                throw new ArgumentException("FoodRiotStartData is required.", nameof(contentContext));
            }

            if (combatWorld == null || followerPrefab == null || vegetablePrefab == null || commanderRoot == null)
            {
                throw new InvalidOperationException("Food Riot runtime references are missing.");
            }

            combatWorld.Clear(); // 이전 판의 남은 유닛 제거
            commanderRoot.SetActive(true);
            commanderMove?.ResetToInitialPosition(); // 재입장도 최초 위치에서 시작
            commanderMove?.SetInputEnabled(true);
            exitButton?.onClick.AddListener(Cancel);
            timeRemaining = startData.DurationSeconds;
            killCount = 0;
            spawnSequence = 0;
            IsRunning = true;
            if (resultText != null)
            {
                resultText.text = "이동 키나 조이스틱으로 움직이세요";
            }

            SpawnFollowers();
            for (var i = 0; i < startData.ActiveVegetableCount; i++)
            {
                SpawnVegetable();
            }

            UpdateHud();
        }

        public void Shutdown()
        {
            StopAllCoroutines();
            exitButton?.onClick.RemoveListener(Cancel);
            commanderMove?.SetInputEnabled(false);
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
            if (timeRemaining <= 0f)
            {
                Complete();
            }
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
                var request = new UnitSpawnRequest(
                    partyUnits[i].UnitId,
                    partyUnits[i].Stats,
                    UnitTeam.Player,
                    visualTint: partyUnits[i].VisualTint,
                    runtimeAssetSet: partyUnits[i].RuntimeAssetSet);
                var actor = combatWorld.SpawnUnit(followerPrefab, request, spawnPosition, Quaternion.identity);
                actor?.SetFollowAnchor(commanderRoot.transform, offsets[i], 6.5f, 8f);
            }
        }

        private void SpawnVegetable()
        {
            if (!IsRunning)
            {
                return;
            }

            var center = vegetableAreaCenter == null ? transform.position : vegetableAreaCenter.position;
            var position = center + new Vector3(
                UnityEngine.Random.Range(-vegetableAreaHalfExtents.x, vegetableAreaHalfExtents.x),
                0f,
                UnityEngine.Random.Range(-vegetableAreaHalfExtents.y, vegetableAreaHalfExtents.y));
            var hitCount = 2 + spawnSequence % 3; // 야채마다 2~4회 타격 필요
            var stats = new UnitStatsSnapshot
            {
                maxHealth = hitCount,
                damage = 0f,
                moveSpeed = 0f,
                attackRange = 0.5f,
                attackInterval = 1f,
                projectileSpeed = 0f,
                ranged = false
            };
            var request = new UnitSpawnRequest(
                $"vegetable_{spawnSequence++}",
                stats,
                UnitTeam.Enemy,
                false,
                false,
                1f);
            var actor = combatWorld.SpawnUnit(vegetablePrefab, request, position, Quaternion.identity);
            if (actor == null)
            {
                return;
            }

            actor.Died += HandleVegetableDied; // 처치 시 같은 수만큼 재보충
            var mover = actor.GetComponent<VegetableMover>();
            mover?.Initialize(center, vegetableAreaHalfExtents, UnityEngine.Random.Range(0.7f, 1.35f));
        }

        private void HandleVegetableDied(UnitActor actor)
        {
            if (!IsRunning)
            {
                return;
            }

            killCount++;
            UpdateHud();
            StartCoroutine(RespawnVegetable()); // 짧은 공백 뒤 새 야채 투입
        }

        private IEnumerator RespawnVegetable()
        {
            yield return new WaitForSeconds(0.45f);
            SpawnVegetable();
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
                resultText.text = string.Empty; // 최종 결과는 AppRoot 공통창에서 표시
            }

            var result = new FoodRiotResult(killCount); // 최종 처치 수를 보상 계층에 전달
            context?.Exit.Complete(result); // 최종 결과는 저장 성공 뒤 AppRoot 공통창에서 표시
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

        private void UpdateHud()
        {
            if (timerText != null)
            {
                timerText.text = $"남은 시간 {Mathf.CeilToInt(timeRemaining)}초";
            }

            if (killText != null)
            {
                killText.text = $"처치 {killCount}";
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            CombatWorld world,
            GameObject follower,
            GameObject vegetable,
            GameObject commander,
            CommanderMoveController moveController,
            Transform areaCenter,
            TMP_Text timer,
            TMP_Text kills,
            TMP_Text result,
            Button exit)
        {
            combatWorld = world;
            followerPrefab = follower;
            vegetablePrefab = vegetable;
            commanderRoot = commander;
            commanderMove = moveController;
            vegetableAreaCenter = areaCenter;
            timerText = timer;
            killText = kills;
            resultText = result;
            exitButton = exit;
        }
#endif
    }
}
