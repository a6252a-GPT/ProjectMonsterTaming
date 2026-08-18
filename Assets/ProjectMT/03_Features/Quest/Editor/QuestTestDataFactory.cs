using ProjectMT.Features.Quest;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.Reward;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.Quest
{
    // 우편함·퀘스트 UI가 준비되기 전, QuestDebugController로 바로 확인할 수 있는 샘플 퀘스트 2종을 만든다.
    // 정식 기획 데이터가 아니라 배관(카탈로그 연동·저장·보상 수령)이 실제로 동작하는지 확인하기 위한 점검용이다.
    public static class QuestTestDataFactory
    {
        public const string DataRoot = "Assets/ProjectMT/03_Features/Quest/TestData";
        public const string RewardPath = DataRoot + "/RD_QuestTestReward.asset";
        public const string CatalogPath = DataRoot + "/QuestCatalog_Test.asset";
        public const string FirstQuestPath = DataRoot + "/QD_001_ExpeditionClear.asset";
        public const string SecondQuestPath = DataRoot + "/QD_002_MonsterSummon.asset";

        [MenuItem("JC Tool/Quest/Create Test Data")]
        private static void CreateTestDataFromMenu()
        {
            var catalog = CreateOrUpdate();
            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);
            Debug.Log($"퀘스트 테스트 데이터를 갱신했습니다: {CatalogPath}", catalog);
        }

        public static QuestCatalog CreateOrUpdate()
        {
            EnsureFolder(DataRoot);

            // 골드·경험치만 지급(아이템 카탈로그 등록 여부와 무관하게 항상 안전하게 지급 검증 가능).
            var reward = GetOrCreate<RewardDefinition>(RewardPath);
            reward.EditorConfigure(500L, 100L, null);
            EditorUtility.SetDirty(reward);

            // 기획서 3장 예시 흐름: 원정대 클리어 → 완료 → 보상 수령 → 몬스터 뽑기 해금 → 다음 퀘스트 시작.
            var first = GetOrCreate<QuestDefinition>(FirstQuestPath);
            first.EditorConfigure(
                new QuestId("quest_001_expedition_clear"),
                "첫 원정대 클리어",
                "원정대를 1회 클리어하세요.",
                QuestType.Main,
                QuestConditionType.ExpeditionClear,
                1L,
                default,
                reward,
                new[] { QuestUnlockTarget.MonsterSummon });
            EditorUtility.SetDirty(first);

            var second = GetOrCreate<QuestDefinition>(SecondQuestPath);
            second.EditorConfigure(
                new QuestId("quest_002_monster_summon"),
                "첫 몬스터 뽑기",
                "몬스터를 1회 뽑으세요.",
                QuestType.Main,
                QuestConditionType.MonsterSummon,
                1L,
                first.QuestId,
                reward,
                new[] { QuestUnlockTarget.Formation });
            EditorUtility.SetDirty(second);

            var catalog = GetOrCreate<QuestCatalog>(CatalogPath);
            catalog.EditorSetDefinitions(new[] { first, second });
            EditorUtility.SetDirty(catalog);

            if (!catalog.TryValidate(out var error))
            {
                Debug.LogError($"퀘스트 테스트 데이터 검증 실패: {error}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return catalog;
        }

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string assetFolder)
        {
            var parts = assetFolder.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }
    }
}
