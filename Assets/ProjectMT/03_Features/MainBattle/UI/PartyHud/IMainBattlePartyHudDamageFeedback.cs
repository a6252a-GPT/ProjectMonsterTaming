namespace ProjectMT.Features.MainBattle
{
    public interface IMainBattlePartyHudDamageFeedback // 외부 피드백 패키지와 HUD 사이의 좁은 경계
    {
        bool IsConfigured { get; }
        void PlayDamageFeedback();
        void ResetDamageFeedback();
    }
}
