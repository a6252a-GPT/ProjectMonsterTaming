using ProjectMT.Contents.Framework;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    [DisallowMultipleComponent]
    public sealed class FallenCommanderDevBootstrap : MonoBehaviour
    {
        [SerializeField] private FallenCommanderController controller;

        private DebugContentExit debugExit;

        private void Start()
        {
            if (controller == null)
            {
                Debug.LogError(
                    "Fallen Commander DEV controller is missing.",
                    this);

                return;
            }

            //개발용 출구
            debugExit = new DebugContentExit();
            //종료 이벤트 구독
            debugExit.Exited += HandleExit;

            var startData = new FallenCommanderStartData();

            var runInfo = new ContentRunInfo(
                new ContentId("fallen_commander"),
                "dev_seed",
                ContentRunMode.SeedTest);

            //Controller 초기화용 컨텍스트 생성
            var context = new ContentContext(
                runInfo,
                startData,
                debugExit);

            controller.Initialize(context);
        }

        private void HandleExit(
            ContentOutcome outcome,
            IContentResultData result)
        {
            controller.Shutdown();

            Debug.Log(
                $"Fallen Commander DEV finished. Outcome={outcome}",
                this);
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
    }
}