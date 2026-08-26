using System;

namespace ProjectMT.Shared.Reward
{
    // Bootstrap과 Features 어셈블리가 서로를 참조하지 않고도 같은 IRewardPresentationPlayer를 공유하기 위한 중계소.
    // ItemCatalogHub/QuestProgressServiceHub와 동일한 패턴: Bootstrap이 Bind, 다른 기능들은 Current/Ready로 조회한다.
    public static class RewardPresentationHub
    {
        public static IRewardPresentationPlayer Current { get; private set; }
        public static event Action<IRewardPresentationPlayer> Ready;

        public static void Bind(IRewardPresentationPlayer player)
        {
            Current = player;
            Ready?.Invoke(player);
        }
    }
}
