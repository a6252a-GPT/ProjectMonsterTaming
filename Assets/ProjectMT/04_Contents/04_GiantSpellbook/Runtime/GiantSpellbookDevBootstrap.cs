using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.GiantSpellbook
{
    /*
     * DEV_03_GiantSpellbook Scene을 00_Entry나 실제 저장 데이터 없이 바로 실행하기 위한 개발 전용 진입점이다.
     * 정식 MainBattle에서는 AppRoot의 ContentFlow가 Context를 만들지만, DEV Scene에는 AppRoot가 없으므로
     * 이 컴포넌트가 시드 편성과 DebugContentExit을 조립해 같은 GiantSpellbookController.Initialize()를 호출한다.
     * 따라서 팀원은 DEV Scene에서 먼저 작업해도 정식 MainBattle과 동일한 Runtime Prefab·Controller를 수정하게 된다.
     * DEV와 Hosted 차이는 Notion `04_1단계_현재시드구조_이해하기`, 확장 순서는
     * `05_2단계_현재시드에서_최종구조로_가는방법`의 성장 던전 부분을 참고한다.
     */
    [DisallowMultipleComponent]
    public sealed class GiantSpellbookDevBootstrap : MonoBehaviour // DEV 씬 단독 실행 진입점
    {
        [SerializeField] private GiantSpellbookController controller;
        [SerializeField] private GiantSpellbookStartDataFactory startDataFactory;

        private DebugContentExit debugExit;

        private void Start()
        {
            if (controller == null || startDataFactory == null)
            {
                Debug.LogError("Giant Spellbook DEV references are missing.");
                return;
            }

            // DebugContentExit은 실제 저장이나 보상 처리를 하지 않고 종료 결과를 이벤트와 로그로만 돌려준다.
            debugExit = new DebugContentExit();
            debugExit.Exited += HandleExit;

            // 실제 보유 몬스터 대신 고정 예시 편성을 만들어 전투 연결을 즉시 확인한다.
            var startData = startDataFactory.Create(SeedBattlePartySnapshotFactory.Create());
            var context = new ContentContext(
                new ContentRunInfo(new ContentId("giant_spellbook"), "dev_seed", ContentRunMode.SeedTest),
                startData,
                debugExit);
            controller.Initialize(context);
        }

        private void HandleExit(ContentOutcome outcome, IContentResultData result)
        {
            controller.Shutdown();
            Debug.Log($"Giant Spellbook DEV finished. Outcome={outcome}");
        }

        private void OnDestroy()
        {
            // Scene을 닫거나 PlayMode를 종료할 때 이벤트와 풀링 유닛을 남기지 않도록 정리한다.
            if (debugExit != null)
            {
                debugExit.Exited -= HandleExit;
            }

            if (controller != null)
            {
                controller.Shutdown();
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(GiantSpellbookController targetController, GiantSpellbookStartDataFactory factory)
        {
            controller = targetController;
            startDataFactory = factory;
        }
#endif
    }
}
