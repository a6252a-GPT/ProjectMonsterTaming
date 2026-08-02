namespace ProjectMT.Core.SceneFlow
{
    public interface ISceneContext // 씬별 권한 봉투 표식
    {
    }

    public interface ISceneRoot // 정식 씬 시작·종료 계약
    {
        SceneId SceneId { get; }
        bool IsInitialized { get; }
        void Initialize(ISceneContext context);
        void Shutdown();
    }

    public interface ISceneNavigator // 콘텐츠가 쓰는 좁은 씬 이동 계약
    {
        bool IsTransitioning { get; }
        void Load(SceneId sceneId);
    }
}
