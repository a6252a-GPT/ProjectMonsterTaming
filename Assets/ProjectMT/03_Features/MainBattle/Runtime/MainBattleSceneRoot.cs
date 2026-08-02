using System;
using ProjectMT.Core.SceneFlow;
using ProjectMT.Contents.Framework;
using ProjectMT.Features.Expedition;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattleSceneRoot : MonoBehaviour, ISceneRoot // 메인전투 씬 조립·수명 관리
    {
        [SerializeField] private SceneId sceneId = new SceneId("main_battle"); // 메인 씬 식별자
        [SerializeField] private ContentId vegetableRiotContentId = new ContentId("vegetable_riot"); // Hosted 콘텐츠 ID
        [SerializeField] private ContentId castleRaidContentId = new ContentId("castle_raid"); // 별도 씬 콘텐츠 ID
        [SerializeField] private ExpeditionController expedition; // 원정대 진행 담당
        [SerializeField] private MainBattleHostedContentRunner hostedRunner; // 성장 던전 전환 담당
        [SerializeField] private Button vegetableRiotButton; // 야채 입장 버튼
        [SerializeField] private Button castleRaidButton; // 군단의 역습 입장 버튼
        [SerializeField] private TMP_Text statusText; // 현재 플레이 상태

        private MainSceneContext context; // 진행·콘텐츠 실행 권한
        private BattlePartySnapshot party; // 시드 부대 사진

        public SceneId SceneId => sceneId;
        public bool IsInitialized { get; private set; }

        public void Initialize(ISceneContext sceneContext)
        {
            if (IsInitialized)
            {
                return;
            }

            context = sceneContext as MainSceneContext;
            if (context == null)
            {
                throw new ArgumentException("MainSceneContext is required.", nameof(sceneContext));
            }

            if (expedition == null || hostedRunner == null)
            {
                throw new InvalidOperationException("MainBattle runtime references are missing.");
            }

            party = SeedBattlePartySnapshotFactory.Create(); // 현재 고정 두부 5기
            vegetableRiotButton?.onClick.AddListener(OpenVegetableRiot);
            castleRaidButton?.onClick.AddListener(OpenCastleRaid);
            expedition.Initialize(context.Progress, party);
            SetStatus("자동 전투");
            IsInitialized = true;
        }

        public void Shutdown()
        {
            vegetableRiotButton?.onClick.RemoveListener(OpenVegetableRiot);
            castleRaidButton?.onClick.RemoveListener(OpenCastleRaid);
            if (hostedRunner != null && hostedRunner.IsOpen)
            {
                hostedRunner.CloseWithoutRestart(); // 씬 종료 중 원정대 재시작 금지
            }

            expedition?.Shutdown();
            context = null;
            party = null;
            IsInitialized = false;
        }

        private void OpenVegetableRiot()
        {
            if (!TryOpenContent())
            {
                return;
            }

            if (context.ContentLauncher.StartHosted(vegetableRiotContentId, party, hostedRunner))
            {
                SetStatus("야채 대소동");
            }
        }

        private void OpenCastleRaid()
        {
            if (!TryOpenContent())
            {
                return;
            }

            expedition.StopWithoutResult(); // 별도 씬 이동 전 무정산 종료
            if (context.ContentLauncher.StartSeparate(castleRaidContentId, party))
            {
                SetStatus("군단의 역습");
            }
            else
            {
                expedition.StartFromSavedMode(); // 입장 실패 시 메인 복구
            }
        }

        private bool TryOpenContent()
        {
            if (CanOpenContent())
            {
                return true;
            }

            if (IsInitialized && expedition != null && expedition.IsSettling)
            {
                SetStatus("전투 결과 정산 중입니다. 잠시 후 다시 시도하세요.");
            }

            return false;
        }

        private bool CanOpenContent()
        {
            return IsInitialized && context != null && party != null &&
                   !context.ContentLauncher.IsRunning && !expedition.IsSettling; // 콘텐츠·정산 중 중복 입장 금지
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            ExpeditionController expeditionController,
            MainBattleHostedContentRunner runner,
            Button vegetableButton,
            Button castleButton,
            TMP_Text status)
        {
            expedition = expeditionController;
            hostedRunner = runner;
            vegetableRiotButton = vegetableButton;
            castleRaidButton = castleButton;
            statusText = status;
        }
#endif
    }
}
