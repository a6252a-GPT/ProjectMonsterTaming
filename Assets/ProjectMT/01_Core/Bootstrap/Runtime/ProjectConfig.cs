using ProjectMT.Core.Config;
using ProjectMT.Core.SceneFlow;
using ProjectMT.Contents.Framework;
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
        [SerializeField] private SceneId entrySceneId; // 최초 진입 씬
        [SerializeField] private SceneId mainBattleSceneId; // 기본 복귀 씬

        public SceneCatalog SceneCatalog => sceneCatalog;
        public ContentCatalog ContentCatalog => contentCatalog;
        public MonsterCatalog MonsterCatalog => monsterCatalog;
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
#endif
    }
}
