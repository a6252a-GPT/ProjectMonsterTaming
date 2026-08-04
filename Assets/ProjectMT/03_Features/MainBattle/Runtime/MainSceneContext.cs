using ProjectMT.Core.SceneFlow;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;

namespace ProjectMT.Features.MainBattle
{
    public sealed class MainSceneContext : ISceneContext // 메인전투에 허용된 권한 봉투
    {
        public MainSceneContext(
            IGameProgressService progress,
            IContentLauncher contentLauncher,
            BattlePartySnapshot party)
        {
            Progress = progress;
            ContentLauncher = contentLauncher;
            Party = party;
        }

        public IGameProgressService Progress { get; } // 진행 조회·변경 권한
        public IContentLauncher ContentLauncher { get; } // 콘텐츠 입장 권한
        public BattlePartySnapshot Party { get; } // 현재 저장에서 만든 전투 부대
    }
}
