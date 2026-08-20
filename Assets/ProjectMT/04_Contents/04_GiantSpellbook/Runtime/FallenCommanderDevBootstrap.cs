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

            //���߿� �ⱸ
            debugExit = new DebugContentExit();
            //���� �̺�Ʈ ����
            debugExit.Exited += HandleExit;

            var startData = new FallenCommanderStartData();

            var runInfo = new ContentRunInfo(
                new ContentId("fallen_commander"),
                "dev_seed",
                ContentRunMode.SeedTest);

            //Controller �ʱ�ȭ�� ���ؽ�Ʈ ����
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