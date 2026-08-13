using System;
using ProjectMT.Core.SceneFlow;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using ProjectMT.Shared.Stats;
using ProjectMT.Shared.Unit;

namespace ProjectMT.Features.MainBattle
{
    public sealed class MainSceneContext : ISceneContext // 메인전투에 허용된 권한 봉투
    {
        public MainSceneContext(
            IGameProgressService progress,
            IContentLauncher contentLauncher,
            MonsterCatalog monsterCatalog,
            Func<BattlePartySnapshot> partyFactory,
            IRewardPresentationPlayer rewardPresentation)
            : this(
                progress,
                contentLauncher,
                monsterCatalog,
                null,
                null,
                null,
                partyFactory,
                rewardPresentation,
                null)
        {
        }

        public MainSceneContext(
            IGameProgressService progress,
            IContentLauncher contentLauncher,
            MonsterCatalog monsterCatalog,
            ItemCatalog itemCatalog,
            CommanderGrowthConfig commanderGrowthConfig,
            EquipmentBalanceConfig equipmentBalanceConfig,
            Func<BattlePartySnapshot> partyFactory,
            IRewardPresentationPlayer rewardPresentation,
            IGrowthDungeonSweepService growthDungeonSweep = null)
        {
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));
            ContentLauncher = contentLauncher ?? throw new ArgumentNullException(nameof(contentLauncher));
            MonsterCatalog = monsterCatalog ?? throw new ArgumentNullException(nameof(monsterCatalog));
            ItemCatalog = itemCatalog;
            CommanderGrowthConfig = commanderGrowthConfig;
            EquipmentBalanceConfig = equipmentBalanceConfig;
            this.partyFactory = partyFactory ?? throw new ArgumentNullException(nameof(partyFactory));
            RewardPresentation = rewardPresentation;
            GrowthDungeonSweep = growthDungeonSweep;
            Party = this.partyFactory();
        }

        private readonly Func<BattlePartySnapshot> partyFactory;

        public IGameProgressService Progress { get; } // 진행 조회·변경 권한
        public IContentLauncher ContentLauncher { get; } // 콘텐츠 입장 권한
        public MonsterCatalog MonsterCatalog { get; } // 편성 화면 조회용 등록부
        public ItemCatalog ItemCatalog { get; } // 일반 인벤토리 조회용 등록부
        public CommanderGrowthConfig CommanderGrowthConfig { get; } // 군단장 성장 표시용 설정
        public EquipmentBalanceConfig EquipmentBalanceConfig { get; } // 장비창 계산용 설정
        public IRewardPresentationPlayer RewardPresentation { get; } // 저장 확정 보상 표현
        public IGrowthDungeonSweepService GrowthDungeonSweep { get; } // 성장 던전 1회 소탕
        public BattlePartySnapshot Party { get; private set; } // 현재 저장에서 만든 전투 부대

        public BattlePartySnapshot RefreshParty()
        {
            Party = partyFactory(); // 저장 확정 뒤 다음 전투용 사진만 교체
            return Party;
        }
    }
}
