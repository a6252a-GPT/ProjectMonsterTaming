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

            // DEV 종료 경계를 준비한다.
            debugExit = new DebugContentExit();
            // 종료 이벤트는 같은 부트스트랩에서 정리한다.
            debugExit.Exited += HandleExit;

            StartDevBattle();
        }

        // DEV 테스트 버튼에서 현재 전투를 정리하고 새 전투를 시작한다.
        public void DebugRestartBattle()
        {
            if (controller == null || debugExit == null)
            {
                return;
            }

            StartDevBattle();
        }

        // 매 재시작마다 새로운 시작 Context를 만들어 Controller를 초기화한다.
        private void StartDevBattle()
        {
            if (controller == null || debugExit == null)
            {
                return;
            }

            var startData = new FallenCommanderStartData();

            var runInfo = new ContentRunInfo(
                new ContentId("fallen_commander"),
                "dev_seed",
                ContentRunMode.SeedTest);

            // Controller 초기화에 필요한 공통 Context를 만든다.
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
