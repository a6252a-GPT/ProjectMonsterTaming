using ProjectMT.Core.Config;
using ProjectMT.Core.SceneFlow;
using ProjectMT.Contents.Framework;
using ProjectMT.Features.OfflineReward;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Stats;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Bootstrap
{
    [CreateAssetMenu(menuName = "ProjectMT/Bootstrap/Project Config", fileName = "ProjectConfig")]
    public sealed class ProjectConfig : ScriptableObject // 앱 시작 참조 모음
    {
        [SerializeField] private SceneCatalog sceneCatalog; // 정식 씬 목록
        [SerializeField] private ContentCatalog contentCatalog; // 등록 콘텐츠 목록
        [SerializeField] private MonsterCatalog monsterCatalog; // 몬스터 Definition 목록
        [SerializeField] private ItemCatalog itemCatalog; // 일반 아이템 Definition 목록
        [SerializeField] private CombatStatConfig combatStatConfig; // 전투 능력치 상한·기본값
        [SerializeField] private CommanderGrowthConfig commanderGrowthConfig; // 군단장 레벨 성장 곡선
        [SerializeField] private EquipmentBalanceConfig equipmentBalanceConfig; // 장비 드랍·옵션 밸런스
        [SerializeField] private OfflineRewardConfig offlineRewardConfig; // 방치 시간·단계별 임시 보상률
        [SerializeField] private SceneId entrySceneId; // 최초 진입 씬
        [SerializeField] private SceneId mainBattleSceneId; // 기본 복귀 씬

        public SceneCatalog SceneCatalog => sceneCatalog;
        public ContentCatalog ContentCatalog => contentCatalog;
        public MonsterCatalog MonsterCatalog => monsterCatalog;
        public ItemCatalog ItemCatalog => itemCatalog;
        public CombatStatConfig CombatStatConfig => combatStatConfig;
        public CommanderGrowthConfig CommanderGrowthConfig => commanderGrowthConfig;
        public EquipmentBalanceConfig EquipmentBalanceConfig => equipmentBalanceConfig;
        public OfflineRewardConfig OfflineRewardConfig => offlineRewardConfig;
        public SceneId EntrySceneId => entrySceneId;
        public SceneId MainBattleSceneId => mainBattleSceneId;

#if UNITY_EDITOR
        public void EditorConfigure(
            SceneCatalog scenes,
            ContentCatalog contents,
            SceneId entryId,
            SceneId mainBattleId)
        {
            sceneCatalog = scenes;
            contentCatalog = contents;
            entrySceneId = entryId;
            mainBattleSceneId = mainBattleId;
        }

        public void EditorConfigureMonsterCatalog(MonsterCatalog catalog)
        {
            monsterCatalog = catalog;
        }

        public void EditorConfigureItemCatalog(ItemCatalog catalog)
        {
            itemCatalog = catalog;
        }

        public void EditorConfigureStatConfigs(
            CombatStatConfig combatStats,
            CommanderGrowthConfig commanderGrowth,
            EquipmentBalanceConfig equipmentBalance)
        {
            combatStatConfig = combatStats;
            commanderGrowthConfig = commanderGrowth;
            equipmentBalanceConfig = equipmentBalance;
        }

        public void EditorConfigureOfflineReward(OfflineRewardConfig config)
        {
            offlineRewardConfig = config;
        }
#endif
    }
}
