using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.GiantSpellbook
{
    /*
     * MainBattle의 ContentFlow가 보유한 현재 편성 Snapshot을 거대마도서 전용 StartData로 감싸는 어댑터다.
     * ContentDefinition_GiantSpellbook.asset이 이 Factory를 참조하므로, 메인 입장 버튼을 누르면 자동 호출된다.
     * DEV Scene도 같은 Factory를 사용하지만 그쪽에서는 저장 데이터 대신 SeedBattlePartySnapshotFactory의 예시 편성을 넣는다.
     * Factory와 ContentDefinition 관계가 어렵다면 Notion `08_디자인패턴_현재시드와최종구조_이해하기`를 참고한다.
     */
    [CreateAssetMenu(
        menuName = "ProjectMT/Content/Giant Spellbook/Start Data Factory",
        fileName = "GiantSpellbookStartDataFactory")]
    public sealed class GiantSpellbookStartDataFactory : ContentStartDataFactory // 메인 편성을 던전 시작값으로 연결
    {
        public override IContentStartData Create(BattlePartySnapshot party)
        {
            // party가 없으면 잘못된 입장이므로 null을 반환하고 ContentFlow가 실행을 중단하게 한다.
            return party == null ? null : new GiantSpellbookStartData(party);
        }
    }
}
