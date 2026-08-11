using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;

namespace ProjectMT.Contents.GiantSpellbook
{
    /*
     * 콘텐츠가 시작될 때 필요한 읽기 전용 데이터 묶음이다.
     * 현재는 입장 순간의 BattlePartySnapshot만 전달한다. 팀원이 제한시간, 난이도, 선택 스테이지 같은
     * 시작 조건을 추가해야 한다면 이 클래스의 생성자와 읽기 전용 프로퍼티에 값을 더하면 된다.
     * 진행 중 저장 데이터를 직접 읽거나 수정하지 않고, 한 판 동안 사용할 값을 시작 시점에 고정하는 역할이다.
     * Snapshot 개념은 Notion `04_1단계_현재시드구조_이해하기`의 용어·데이터 설명을 먼저 참고한다.
     */
    public sealed class GiantSpellbookStartData : IContentStartData // 팀원 구현에 넘길 최소 시작값
    {
        public GiantSpellbookStartData(BattlePartySnapshot party)
        {
            Party = party;
        }

        public BattlePartySnapshot Party { get; }
    }
}
