using System;
using System.Collections.Generic;
using ProjectMT.Contents.Framework;
using ProjectMT.Features.Expedition;
using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattleHostedContentRunner : MonoBehaviour, IHostedContentRunner // 메인·Hosted 상호 배타 전환
    {
        [SerializeField] private GameObject mainGameplayRoot; // 메인전투 전체 묶음
        [SerializeField] private GrowthDungeonHost growthDungeonHost; // 성장 던전 실행 자리
        [SerializeField] private ExpeditionController expedition; // 현재 원정대 종료·재시작

        private readonly List<GameObject> stageMapRoots = new List<GameObject>();
        private readonly List<bool> stageMapActiveStates = new List<bool>();
        private GameObject globalDebugPanel;
        private bool globalDebugPanelWasActive;

        public bool IsOpen { get; private set; }

        public bool Open(ContentContext context)
        {
            if (IsOpen || context == null || mainGameplayRoot == null || growthDungeonHost == null || expedition == null)
            {
                return false;
            }

            expedition.StopWithoutResult(); // 보상 없이 현재 Run 종료
            mainGameplayRoot.SetActive(false); // 메인 플레이 영역 전체 비활성
            CacheAndHideStageMapRoots(); // Hosted 전용 배경과 겹치지 않게 숨긴다.
            CacheAndHideGlobalDebugPanel(); // DEV 전용 HUD와 겹치는 전역 디버그 버튼 숨김
            if (growthDungeonHost.Open(context))
            {
                IsOpen = true;
                return true;
            }

            RestoreGlobalDebugPanel(); // 열기 실패 시 전역 디버그 버튼 복구
            RestoreStageMapRoots(); // 열기 실패 시 원래 활성 상태 복구
            mainGameplayRoot.SetActive(true); // 열기 실패 시 메인 복구
            expedition.StartFromSavedMode(); // 저장된 모드로 새 Run
            return false;
        }

        public void Close()
        {
            CloseInternal(true);
        }

        public void CloseWithoutRestart()
        {
            CloseInternal(false);
        }

        private void CloseInternal(bool restartExpedition)
        {
            if (!IsOpen)
            {
                return;
            }

            growthDungeonHost.Close(); // Controller 종료 후 Prefab 비활성
            IsOpen = false;
            RestoreGlobalDebugPanel(); // Hosted 종료 뒤 전역 디버그 버튼 복구
            RestoreStageMapRoots(); // 메인 카메라가 켜지기 전에 배경 복구
            if (mainGameplayRoot != null)
            {
                mainGameplayRoot.SetActive(true);
            }

            if (restartExpedition)
            {
                expedition?.StartFromSavedMode();
            }
        }

        private void CacheAndHideStageMapRoots()
        {
            stageMapRoots.Clear();
            stageMapActiveStates.Clear();
            if (!gameObject.scene.IsValid())
            {
                return;
            }

            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root == null || !root.name.StartsWith("PF_StageMap_", StringComparison.Ordinal))
                {
                    continue;
                }

                stageMapRoots.Add(root);
                stageMapActiveStates.Add(root.activeSelf);
                root.SetActive(false);
            }
        }

        private void RestoreStageMapRoots()
        {
            var count = Mathf.Min(stageMapRoots.Count, stageMapActiveStates.Count);
            for (var index = 0; index < count; index++)
            {
                if (stageMapRoots[index] != null)
                {
                    stageMapRoots[index].SetActive(stageMapActiveStates[index]);
                }
            }

            stageMapRoots.Clear();
            stageMapActiveStates.Clear();
        }

        private void CacheAndHideGlobalDebugPanel()
        {
            globalDebugPanel = null;
            globalDebugPanelWasActive = false;
            var transforms = FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < transforms.Length; index++)
            {
                var candidate = transforms[index];
                if (candidate == null || candidate.name != "DebugPanel" ||
                    candidate.gameObject.scene.name != "DontDestroyOnLoad")
                {
                    continue;
                }

                globalDebugPanel = candidate.gameObject;
                globalDebugPanelWasActive = globalDebugPanel.activeSelf;
                globalDebugPanel.SetActive(false);
                return;
            }
        }

        private void RestoreGlobalDebugPanel()
        {
            if (globalDebugPanel != null)
            {
                globalDebugPanel.SetActive(globalDebugPanelWasActive);
            }

            globalDebugPanel = null;
            globalDebugPanelWasActive = false;
        }

#if UNITY_EDITOR
        public void EditorConfigure(GameObject gameplayRoot, GrowthDungeonHost host, ExpeditionController expeditionController)
        {
            mainGameplayRoot = gameplayRoot;
            growthDungeonHost = host;
            expedition = expeditionController;
        }
#endif
    }
}
