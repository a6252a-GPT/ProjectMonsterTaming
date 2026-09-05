using System.Collections.Generic;
namespace ProjectMT.Shared.Audio
{
    public readonly struct SfxEventDefinition
    {
        public readonly string Id, Category, Name, Description;
        public SfxEventDefinition(string id, string category, string name, string description)
        { Id = id; Category = category; Name = name; Description = description; }
    }
    public static partial class SfxEvents
    {
        public const string Equip = "EVENT-Equip";
        public const string Unequip = "EVENT-Unequip";
        public const string Upgrade = "EVENT-Upgrade";
        public const string Dismantle = "EVENT-Dismantle";
        public const string EquipmentLock = "EVENT-EquipmentLock";
        public const string MonsterLevel = "EVENT-MonsterLevel";
        public const string MonsterAscend = "EVENT-MonsterAscend";
        public const string CommanderLevel = "EVENT-CommanderLevel";
        public const string LegionUpgrade = "EVENT-LegionUpgrade";
        public const string Potential = "EVENT-Potential";
        public const string PotentialLock = "EVENT-PotentialLock";
        public const string SkillEquip = "EVENT-SkillEquip";
        public const string SkillLevel = "EVENT-SkillLevel";
        public const string SkillSummon = "EVENT-SkillSummon";
        public const string Formation = "EVENT-Formation";
        public const string Placement = "EVENT-Placement";
        public const string Attendance = "EVENT-Attendance";
        public const string MailClaim = "EVENT-MailClaim";
        public const string QuestClaim = "EVENT-QuestClaim";
        public const string CollectionClaim = "EVENT-CollectionClaim";
        public const string OfflineClaim = "EVENT-OfflineClaim";
        public const string ItemUse = "EVENT-ItemUse";
        public const string ItemDiscard = "EVENT-ItemDiscard";
        public const string DropCollect = "EVENT-DropCollect";
        public const string QuestComplete = "EVENT-QuestComplete";
        public const string Rejected = "EVENT-Rejected";
        public const string Toggle = "EVENT-Toggle";
        public const string Slider = "EVENT-Slider";
        public const string Dropdown = "EVENT-Dropdown";
        public const string DropSpawn = "EVENT-DropSpawn";
        public const string ContentStart = "EVENT-ContentStart";
        public const string SaveError = "EVENT-SaveError";
        public const string GachaCharge = "EVENT-GachaCharge";
        public const string GachaEnter = "EVENT-GachaEnter";
        public const string GachaFlip = "EVENT-GachaFlip";
        public const string GachaReveal = "EVENT-GachaReveal";
        public const string GachaLegendary = "EVENT-GachaLegendary";
        public const string GachaMythic = "EVENT-GachaMythic";
        public static readonly IReadOnlyList<SfxEventDefinition> Definitions = new SfxEventDefinition[]
        {
            new SfxEventDefinition(Button, "UI", "버튼 누르기", "사용 가능한 버튼을 누를 때"),
            new SfxEventDefinition(Open, "UI", "팝업 열기", "기존 팝업 열기 애니메이션이 시작될 때"),
            new SfxEventDefinition(Close, "UI", "팝업 닫기", "기존 팝업 닫기 호출 시"),
            new SfxEventDefinition(Hit, "전투", "공용 피격", "Maker 피격음이 없는 유닛의 피격 시"),
            new SfxEventDefinition(Death, "전투", "공용 사망", "Maker 사망음이 없는 유닛의 사망 시"),
            new SfxEventDefinition(Weak, "전투", "증원 강조", "원정대 증원 강조 연출 시"),
            new SfxEventDefinition(Reward, "보상", "보상 알림", "공용 획득 알림 표시 시 · 개별 수령음과 별도"),
            new SfxEventDefinition(BattleStart, "전투", "첫 웨이브", "원정대 첫 웨이브 행군 시작 시"),
            new SfxEventDefinition(Wave, "전투", "다음 웨이브", "원정대 두 번째 이후 웨이브 행군 시작 시"),
            new SfxEventDefinition(Boss, "전투", "보스 등장", "원정대 보스 생성 성공 시"),
            new SfxEventDefinition(Victory, "전투", "승리", "원정대·공용 콘텐츠 승리 확정 시"),
            new SfxEventDefinition(Defeat, "전투", "패배", "원정대·공용 콘텐츠 패배 확정 시"),
            new SfxEventDefinition(Equip, "장비", "장비 장착", "장비 장착·자동 장착 저장 성공"),
            new SfxEventDefinition(Unequip, "장비", "장비 해제", "장비 해제 저장 성공"),
            new SfxEventDefinition(Upgrade, "장비", "장비 슬롯 강화", "슬롯 강화 저장 성공"),
            new SfxEventDefinition(Dismantle, "장비", "장비 분해", "선택·일괄 분해 저장 성공"),
            new SfxEventDefinition(EquipmentLock, "장비", "장비 잠금 변경", "잠금·해제 저장 성공"),
            new SfxEventDefinition(MonsterLevel, "성장", "몬스터 레벨업", "몬스터 레벨업 저장 성공"),
            new SfxEventDefinition(MonsterAscend, "성장", "몬스터 돌파", "몬스터 돌파 저장 성공"),
            new SfxEventDefinition(CommanderLevel, "성장", "군단장 레벨업", "군단장 레벨업 저장 성공"),
            new SfxEventDefinition(LegionUpgrade, "성장", "군단 능력 강화", "군단 능력 강화 저장 성공"),
            new SfxEventDefinition(Potential, "성장", "잠재능력 변경", "잠재능력 배정·종류·수치 변경 저장 성공"),
            new SfxEventDefinition(PotentialLock, "성장", "잠재능력 잠금 변경", "잠재능력 잠금·해제 저장 성공"),
            new SfxEventDefinition(SkillEquip, "성장", "군단장 스킬 장착", "군단장 스킬 슬롯 변경 저장 성공"),
            new SfxEventDefinition(SkillLevel, "성장", "군단장 스킬 성장", "군단장 스킬 레벨업 저장 성공"),
            new SfxEventDefinition(SkillSummon, "뽑기", "군단장 스킬 소환 확정", "스킬 소환 결과 저장 성공"),
            new SfxEventDefinition(Formation, "편성", "몬스터 편성 변경", "몬스터 편성·제외 저장 성공"),
            new SfxEventDefinition(Placement, "편성", "전장 배치 적용", "전장 위치 배치 저장 성공"),
            new SfxEventDefinition(Attendance, "보상", "출석 수령", "출석 보상 수령 저장 성공"),
            new SfxEventDefinition(MailClaim, "보상", "우편 수령", "첨부·일괄 수령 저장 성공 · 한 요청에 한 번"),
            new SfxEventDefinition(QuestClaim, "보상", "퀘스트 수령", "단일·일괄·반복 퀘스트 수령 저장 성공"),
            new SfxEventDefinition(CollectionClaim, "보상", "도감 보상 수령", "몬스터 도감 보상 수령 저장 성공"),
            new SfxEventDefinition(OfflineClaim, "보상", "오프라인 보상 확인", "누적 오프라인 보상 확인 저장 성공"),
            new SfxEventDefinition(ItemUse, "아이템", "아이템 사용", "아이템 사용 저장 성공"),
            new SfxEventDefinition(ItemDiscard, "아이템", "아이템 버리기", "아이템 버리기 저장 성공"),
            new SfxEventDefinition(DropCollect, "아이템", "장비 획득 확정", "월드 드롭 등 장비 획득 묶음 저장 성공"),
            new SfxEventDefinition(QuestComplete, "보상", "퀘스트 목표 달성", "퀘스트 진행도가 목표에 처음 도달한 저장 성공"),
            new SfxEventDefinition(Rejected, "UI", "사용 불가", "비활성 버튼 누르기 또는 지원하는 저장 행동 거절 시"),
            new SfxEventDefinition(Toggle, "UI", "토글 변경", "사용자 포인터·키보드 조작으로 토글 값 변경 시"),
            new SfxEventDefinition(Slider, "UI", "슬라이더 조절", "사용자가 슬라이더를 조절해 값이 바뀔 때 · 반복 제한 적용"),
            new SfxEventDefinition(Dropdown, "UI", "선택 목록 변경", "사용자 선택 목록 값 변경 시"),
            new SfxEventDefinition(DropSpawn, "아이템", "월드 장비 출현", "장비 드롭 생성 성공 시 · 화면 효과음"),
            new SfxEventDefinition(ContentStart, "전투", "콘텐츠 시작", "성장 콘텐츠 열기 성공 또는 별도 콘텐츠 씬 준비 시"),
            new SfxEventDefinition(SaveError, "UI", "저장 실패", "진행 데이터 파일 저장 중 예외 발생 시"),
            new SfxEventDefinition(GachaCharge, "뽑기", "소환 준비", "몬스터 뽑기 시작 연출 시"),
            new SfxEventDefinition(GachaEnter, "뽑기", "카드 등장", "몬스터 뽑기 각 카드 등장 시"),
            new SfxEventDefinition(GachaFlip, "뽑기", "카드 회전", "몬스터 뽑기 각 카드 회전 시"),
            new SfxEventDefinition(GachaReveal, "뽑기", "일반 등급 공개", "몬스터 뽑기 전설 미만 카드 앞면 공개 시"),
            new SfxEventDefinition(GachaLegendary, "뽑기", "전설 공개", "몬스터 뽑기 전설 카드 앞면 공개 시"),
            new SfxEventDefinition(GachaMythic, "뽑기", "신화 공개", "몬스터 뽑기 신화 카드 앞면 공개 시"),
        };
    }
}
