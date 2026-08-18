using System.Collections.Generic;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Quest;
using UnityEngine;

namespace ProjectMT.Features.Quest
{
    // 우편함·퀘스트 UI가 아직 없는 동안 카탈로그·진행도 연동이 의도대로 동작하는지
    // 콘솔에서 바로 확인하기 위한 임시 점검용 컴포넌트.
    // 씬에 붙이고 Catalog만 연결하면, 저장 서비스가 로드될 때 스스로 QuestRuntime에 붙는다.
    [DisallowMultipleComponent]
    public sealed class QuestDebugController : MonoBehaviour
    {
        [SerializeField] private QuestCatalog catalog;
        [SerializeField] private long progressStepPerCall = 1L;

        private void OnEnable()
        {
            QuestProgressServiceHub.Ready += Configure;
            if (QuestProgressServiceHub.Current != null)
            {
                Configure(QuestProgressServiceHub.Current);
            }
        }

        private void OnDisable()
        {
            QuestProgressServiceHub.Ready -= Configure;
        }

        public void Configure(IGameProgressService progressService)
        {
            QuestRuntime.Configure(progressService, catalog);
        }

        [ContextMenu("Quest/Log All")]
        private void LogAll()
        {
            if (!TryGetDefinitions(out var definitions))
            {
                return;
            }

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition != null)
                {
                    QuestRuntime.LogQuestSnapshot(definition, QuestRuntime.GetProgress(definition.QuestId));
                }
            }
        }

        [ContextMenu("Quest/Advance All By Step")]
        private async void AdvanceAllByStep()
        {
            if (!TryGetDefinitions(out var definitions))
            {
                return;
            }

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                var applied = await QuestRuntime.TryAdvanceProgressAsync(definition.QuestId, progressStepPerCall);
                if (!applied)
                {
                    continue;
                }

                var progress = QuestRuntime.GetProgress(definition.QuestId);
                Debug.Log(
                    $"[Quest] {definition.DisplayName} 진행도 {progress.CurrentProgress} / {definition.TargetValue} " +
                    $"(완료: {progress.Completed})");
            }
        }

        [ContextMenu("Quest/Claim All Completed Rewards")]
        private async void ClaimAllCompletedRewards()
        {
            if (!TryGetDefinitions(out var definitions))
            {
                return;
            }

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition != null)
                {
                    await QuestRuntime.TryClaimRewardAsync(definition.QuestId);
                }
            }
        }

        private bool TryGetDefinitions(out IReadOnlyList<QuestDefinition> definitions)
        {
            if (catalog == null)
            {
                Debug.LogWarning("QuestDebugController: QuestCatalog가 연결되어 있지 않습니다.", this);
                definitions = null;
                return false;
            }

            if (!QuestRuntime.IsReady)
            {
                Debug.LogWarning(
                    "QuestDebugController: 진행 데이터가 아직 연결되지 않았습니다(씬 초기화가 끝난 뒤 시도하세요).", this);
            }

            definitions = catalog.Definitions;
            return true;
        }
    }
}
