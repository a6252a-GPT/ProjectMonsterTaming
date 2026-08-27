using ProjectMT.Contents.Framework;
using ProjectMT.Contents.TreasureSpirit.Demo;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit
{
    [DisallowMultipleComponent]
    public sealed class TreasureSpiritDevBootstrap : MonoBehaviour // DEV 씬 단독 실행 진입점
    {
        [SerializeField] private DemoDungeonController controller;
        [SerializeField] private TreasureSpiritStartDataFactory startDataFactory;

        private DebugContentExit debugExit;

        private void Start()
        {
            if (controller == null || startDataFactory == null)
            {
                Debug.LogError("Treasure Spirit DEV references are missing.");
                return;
            }

            debugExit = new DebugContentExit();
            debugExit.Exited += HandleExit;
            var startData = startDataFactory.Create(SeedBattlePartySnapshotFactory.Create());
            var context = new ContentContext(
                new ContentRunInfo(
                    new ContentId(GrowthDungeonProgressIds.TreasureSpirit),
                    "1",
                    ContentRunMode.SeedTest),
                startData,
                debugExit);
            controller.Initialize(context);
        }

        private void HandleExit(ContentOutcome outcome, IContentResultData result)
        {
            controller.Shutdown();
            Debug.Log($"Treasure Spirit DEV finished. Outcome={outcome}, Result={result?.GetType().Name ?? "None"}");
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
        public void EditorConfigure(
            DemoDungeonController targetController,
            TreasureSpiritStartDataFactory factory)
        {
            controller = targetController;
            startDataFactory = factory;
        }
#endif
    }
}
