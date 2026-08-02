using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.VegetableRiot
{
    [DisallowMultipleComponent]
    public sealed class VegetableRiotDevBootstrap : MonoBehaviour // 개발 씬 단독 실행 진입점
    {
        [SerializeField] private VegetableRiotController controller; // 실행할 콘텐츠 본체
        [SerializeField] private VegetableRiotStartDataFactory startDataFactory; // 테스트 시작값 생성기

        private DebugContentExit debugExit; // 결과를 로그로 받는 개발용 출구

        private void Start()
        {
            if (controller == null || startDataFactory == null)
            {
                Debug.LogError("Vegetable Riot DEV references are missing.");
                return;
            }

            debugExit = new DebugContentExit();
            debugExit.Exited += HandleExit;
            var startData = startDataFactory.Create(SeedBattlePartySnapshotFactory.Create()); // 시드 파티로 바로 시작
            var context = new ContentContext(
                new ContentRunInfo(new ContentId("vegetable_riot"), "dev_seed", ContentRunMode.SeedTest),
                startData,
                debugExit);
            controller.Initialize(context);
        }

        private void HandleExit(ContentOutcome outcome, IContentResultData result)
        {
            controller.Shutdown();
            Debug.Log($"Vegetable Riot DEV finished. Outcome={outcome}, Result={result?.GetType().Name ?? "None"}");
        }

        private void OnDestroy()
        {
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
        public void EditorConfigure(VegetableRiotController targetController, VegetableRiotStartDataFactory factory)
        {
            controller = targetController;
            startDataFactory = factory;
        }
#endif
    }
}
