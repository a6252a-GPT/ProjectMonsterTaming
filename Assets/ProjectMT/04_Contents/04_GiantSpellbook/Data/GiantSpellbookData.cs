using ProjectMT.Contents.Framework;

namespace ProjectMT.Contents.GiantSpellbook
{
    public sealed class GiantSpellbookStartData : IContentStartData // 군단장 단독 보스전에 필요한 시작 표식
    {
    }

    // 거대 마도서 한 판이 성공했을 때 공용 시스템으로 전달하는 결과 데이터
    public sealed class GiantSpellbookResult : IContentResultData
    {
        // breakCount가 없으니 빈 형태로 시작
    }
}
