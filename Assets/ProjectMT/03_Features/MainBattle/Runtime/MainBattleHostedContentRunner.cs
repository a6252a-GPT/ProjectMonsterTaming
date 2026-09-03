using System;
using System.Collections.Generic;
using ProjectMT.Contents.Framework;
using ProjectMT.Core;
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
            MagicaClothActivation.SetActive(mainGameplayRoot, false); // 메인 플레이 영역 전체 비활성
            CacheAndHideStageMapRoots(); // Hosted 전용 배경과 겹치지 않게 숨긴다.
            CacheAndHideGlobalDebugPanel(); // DEV 전용 HUD와 겹치는 전역 디버그 버튼 숨김
            if (growthDungeonHost.Open(context))
            {
                IsOpen = true;
                return true;
            }

            RestoreGlobalDebugPanel(); // 열기 실패 시 전역 디버그 버튼 복구
            RestoreStageMapRoots(); // 열기 실패 시 원래 활성 상태 복구
            MagicaClothActivation.SetActive(mainGameplayRoot, true); // 열기 실패 시 메인 복구
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

            IsOpen = false;
            if (!PlaySession.CanMutateWorld)
            {
                return;
            }

            growthDungeonHost.Close(); // Controller 종료 후 Prefab 비활성
            RestoreGlobalDebugPanel(); // Hosted 종료 뒤 전역 디버그 버튼 복구
            RestoreStageMapRoots(); // 메인 카메라가 켜지기 전에 배경 복구
            if (mainGameplayRoot != null)
            {
                MagicaClothActivation.SetActive(mainGameplayRoot, true);
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
                SetStageMapRootActive(root, false);
            }
        }

        private void RestoreStageMapRoots()
        {
            var count = Mathf.Min(stageMapRoots.Count, stageMapActiveStates.Count);
            for (var index = 0; index < count; index++)
            {
                var root = stageMapRoots[index];
                if (root != null)
                {
                    SetStageMapRootActive(root, stageMapActiveStates[index]);
                }
            }

            stageMapRoots.Clear();
            stageMapActiveStates.Clear();
        }

        // Terrain이 꺼진 채 다시 켜질 때 Unity가 MeshCollider를 붙이며 경고를 낸다.
        // 콜라이더를 먼저 끄고 루트만 전환한 뒤, 컴포넌트 활성 상태를 원래대로 되돌린다.
        private static void SetStageMapRootActive(GameObject root, bool active)
        {
            var terrains = root.GetComponentsInChildren<Terrain>(true);
            var terrainColliders = new TerrainCollider[terrains.Length];
            for (var index = 0; index < terrains.Length; index++)
            {
                var terrain = terrains[index];
                if (terrain == null)
                {
                    continue;
                }

                var meshCollider = terrain.GetComponent<MeshCollider>();
                if (meshCollider != null)
                {
                    meshCollider.enabled = false;
                    UnityEngine.Object.Destroy(meshCollider);
                }

                var terrainCollider = terrain.GetComponent<TerrainCollider>();
                if (terrainCollider == null)
                {
                    continue;
                }

                terrainColliders[index] = terrainCollider;
                terrainCollider.enabled = false;
            }

            root.SetActive(active);

            for (var index = 0; index < terrainColliders.Length; index++)
            {
                var terrainCollider = terrainColliders[index];
                if (terrainCollider != null)
                {
                    terrainCollider.enabled = true;
                }
            }
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
