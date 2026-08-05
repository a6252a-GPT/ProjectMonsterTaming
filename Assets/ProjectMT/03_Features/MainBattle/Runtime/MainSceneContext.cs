using System;
using ProjectMT.Core.SceneFlow;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Reward;
using ProjectMT.Shared.Unit;

namespace ProjectMT.Features.MainBattle
{
    public sealed class MainSceneContext : ISceneContext // 메인전투에 허용된 권한 봉투
    {
        public MainSceneContext(
            IGameProgressService progress,
            IContentLauncher contentLauncher,
            MonsterCatalog monsterCatalog,
            Func<BattlePartySnapshot> partyFactory,
            IRewardPresentationPlayer rewardPresentation)
        {
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));
            ContentLauncher = contentLauncher ?? throw new ArgumentNullException(nameof(contentLauncher));
            MonsterCatalog = monsterCatalog ?? throw new ArgumentNullException(nameof(monsterCatalog));
            this.partyFactory = partyFactory ?? throw new ArgumentNullException(nameof(partyFactory));
            RewardPresentation = rewardPresentation;
            Party = this.partyFactory();
        }

        private readonly Func<BattlePartySnapshot> partyFactory;

        public IGameProgressService Progress { get; } // 진행 조회·변경 권한
        public IContentLauncher ContentLauncher { get; } // 콘텐츠 입장 권한
        public MonsterCatalog MonsterCatalog { get; } // 편성 화면 조회용 등록부
        public IRewardPresentationPlayer RewardPresentation { get; } // 저장 확정 보상 표현
        public BattlePartySnapshot Party { get; private set; } // 현재 저장에서 만든 전투 부대

        public BattlePartySnapshot RefreshParty()
        {
            Party = partyFactory(); // 저장 확정 뒤 다음 전투용 사진만 교체
            return Party;
        }
    }
}
