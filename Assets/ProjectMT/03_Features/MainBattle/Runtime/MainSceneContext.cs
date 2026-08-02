using ProjectMT.Core.SceneFlow;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.GameData;

namespace ProjectMT.Features.MainBattle
{
    public sealed class MainSceneContext : ISceneContext // 메인전투에 허용된 권한 봉투
    {
        public MainSceneContext(IGameProgressService progress, IContentLauncher contentLauncher)
        {
            Progress = progress;
            ContentLauncher = contentLauncher;
        }

        public IGameProgressService Progress { get; } // 진행 조회·변경 권한
        public IContentLauncher ContentLauncher { get; } // 콘텐츠 입장 권한
    }
}
