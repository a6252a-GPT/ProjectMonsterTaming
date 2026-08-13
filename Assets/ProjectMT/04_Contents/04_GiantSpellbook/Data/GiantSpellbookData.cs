using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;

namespace ProjectMT.Contents.GiantSpellbook
{
    public sealed class GiantSpellbookStartData : IContentStartData // 팀원 구현에 넘길 최소 시작값
    {
        public GiantSpellbookStartData(BattlePartySnapshot party)
        {
            Party = party;
        }

        public BattlePartySnapshot Party { get; }
    }

    // 거대 마도서 한 판이 성공했을 때 공용 시스템으로 전달하는 결과 데이터
    public sealed class GiantSpellbookResult : IContentResultData
    {
        //breakCount가 없으니 빈 형태로 시작
    }
}
