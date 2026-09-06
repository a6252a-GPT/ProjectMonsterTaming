using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.CommanderSkill.Editor
{
    public static class CommanderSkillExpansionFinalAuthoring
    {
        private const string Root = "Assets/ProjectMT/03_Features/CommanderSkill";
        private const string Page = Root + "/Prefabs/PF_CommanderSkillPage.prefab";

        [MenuItem("Tools/ProjectMT/Commander Skill/Apply Approved Concept Polish")]
        public static void ApplyConceptPolish()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CommanderSkillCatalog>(
                Root + "/Resources/CommanderSkills/CommanderSkillCatalog.asset");
            if (catalog == null || catalog.Skills.Count != CommanderSkillApprovedConcept.SkillIds.Count)
                throw new InvalidOperationException("군단장 스킬 12종 Catalog를 먼저 준비해야 합니다.");

            var skills = catalog.Skills.Where(skill => skill != null).ToArray();
            var actualIds = skills.Select(skill => skill.SkillId).ToHashSet(StringComparer.Ordinal);
            if (!CommanderSkillApprovedConcept.SkillIds.All(actualIds.Contains))
                throw new InvalidOperationException("승인된 12종 중 Catalog에 없는 스킬이 있습니다.");

            var snapshots = skills.ToDictionary(skill => skill, EditorJsonUtility.ToJson);
            var backup = Path.GetFullPath(Path.Combine(Application.dataPath, "../..", "ProjectMT 개인파일/Backups",
                "CommanderSkillConceptPolish_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")));
            Directory.CreateDirectory(backup);
            AssetDatabase.ExportPackage(skills.Select(AssetDatabase.GetAssetPath).ToArray(),
                Path.Combine(backup, "BeforeConceptPolish.unitypackage"), ExportPackageOptions.Default);

            try
            {
                foreach (var skill in skills)
                {
                    using var serialized = new SerializedObject(skill);
                    serialized.FindProperty("description").stringValue =
                        CommanderSkillApprovedConcept.DescriptionFor(skill.SkillId);
                    serialized.FindProperty("castTime").floatValue =
                        CommanderSkillApprovedConcept.CastTimeFor(skill.SkillId);
                    serialized.FindProperty("pattern").FindPropertyRelative("firstBarrageHitAtTarget").boolValue =
                        CommanderSkillApprovedConcept.GuaranteesFirstBarrageHit(skill.SkillId);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    if (!skill.TryValidate(out var error))
                        throw new InvalidOperationException(skill.SkillId + ": " + error);
                    EditorUtility.SetDirty(skill);
                    AssetDatabase.SaveAssetIfDirty(skill);
                }
                if (!catalog.TryValidate(out var catalogError))
                    throw new InvalidOperationException(catalogError);
                Debug.Log("COMMANDER_SKILL_CONCEPT_POLISH_APPLIED backup=" + backup);
            }
            catch
            {
                foreach (var pair in snapshots)
                {
                    EditorJsonUtility.FromJsonOverwrite(pair.Value, pair.Key);
                    EditorUtility.SetDirty(pair.Key);
                    AssetDatabase.SaveAssetIfDirty(pair.Key);
                }
                throw;
            }
        }

        [MenuItem("Tools/ProjectMT/Commander Skill/Apply Approved Awakening Pull And Button")]
        public static void Apply()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CommanderSkillCatalog>(Root + "/Resources/CommanderSkills/CommanderSkillCatalog.asset");
            if (catalog == null || catalog.Skills.Count != 12 || catalog.Skills.Count(s => s.Category == CommanderSkillCategory.Attack) != 8)
                throw new InvalidOperationException("Attack8/Effect4 전환을 먼저 완료해야 합니다.");
            var originals = catalog.Skills.ToArray();
            var plans = new List<CommanderSkillDefinition>();
            var pulls = new List<CommanderPullEffectDefinition>();
            var addedPulls = new List<CommanderPullEffectDefinition>();
            var snapshots = originals.ToDictionary(s => s, EditorJsonUtility.ToJson);
            var stageData = new Dictionary<CommanderSkillDefinition, CommanderSkillAwakeningStage[]>();
            GameObject page = null;
            try
            {
                foreach (var original in originals)
                {
                    var copy = UnityEngine.Object.Instantiate(original);
                    plans.Add(copy);
                    var stages = original.AwakeningStages.Count == 0 ? CommanderSkillAwakeningAuthoring.CreateStages(copy) :
                        original.AwakeningStages.ToArray();
                    stageData.Add(original, stages);
                    copy.EditorConfigureAwakening(stages);
                    if ((copy.SkillId == "CS_RuptureMarch" || copy.SkillId == "CS_PhantomCharge") &&
                        !copy.Effects.OfType<CommanderPullEffectDefinition>().Any())
                    {
                        var gather = copy.SkillId == "CS_RuptureMarch";
                        var pull = ScriptableObject.CreateInstance<CommanderPullEffectDefinition>();
                        pulls.Add(pull);
                        pull.name = "__Effect_WeakPull";
                        pull.EditorConfigure(copy.SkillId + "_pull", gather ? CommanderSkillPullCenter.ImpactPosition : CommanderSkillPullCenter.CastOrigin,
                            gather ? 0.75f : 0.6f, 0.2f, gather ? 0.5f : 2f, gather ? 6 : 4);
                        AppendEffect(copy, pull);
                    }
                    if (!copy.TryValidate(out var error)) throw new InvalidOperationException(copy.SkillId + ": " + error);
                }
                page = PrefabUtility.LoadPrefabContents(Page);
                if (page.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component == null))
                    throw new InvalidOperationException("스킬 Page에 Missing Script가 있습니다.");
                var buttons = page.GetComponentsInChildren<Button>(true);
                var level = buttons.Single(button => button.name == "SkillLevelUpButton");
                var equip = buttons.Single(button => button.name == "SkillEquipButton");
                if (!buttons.Any(button => button.name == "SkillAwakenButton"))
                {
                    var awaken = UnityEngine.Object.Instantiate(level, level.transform.parent, false);
                    awaken.name = "SkillAwakenButton";
                    awaken.onClick = new Button.ButtonClickedEvent();
                    var row = new[] { level, awaken, equip };
                    for (var index = 0; index < row.Length; index++)
                    {
                        var rect = (RectTransform)row[index].transform;
                        rect.sizeDelta = new Vector2(140f, rect.sizeDelta.y);
                        rect.anchoredPosition = new Vector2((index - 1) * 148f, rect.anchoredPosition.y);
                        var label = row[index].GetComponentInChildren<TMP_Text>(true);
                        label.enableAutoSizing = true;
                        label.fontSizeMin = 11f;
                        label.fontSizeMax = 17f;
                        if (index == 1) label.text = "별각성";
                    }
                }
                var backup = Path.GetFullPath(Path.Combine(Application.dataPath, "../..", "ProjectMT 개인파일/Backups",
                    "CommanderSkillAwakening_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")));
                Directory.CreateDirectory(backup);
                AssetDatabase.ExportPackage(originals.Select(AssetDatabase.GetAssetPath).Concat(new[] { Page }).ToArray(),
                    Path.Combine(backup, "BeforeAwakeningAndPull.unitypackage"), ExportPackageOptions.Default);
                foreach (var original in originals)
                {
                    original.EditorConfigureAwakening(stageData[original]);
                    var pull = pulls.FirstOrDefault(p => p.EffectId == original.SkillId + "_pull");
                    if (pull != null)
                    {
                        AssetDatabase.AddObjectToAsset(pull, original);
                        addedPulls.Add(pull);
                        AppendEffect(original, pull);
                    }
                    EditorUtility.SetDirty(original);
                    AssetDatabase.SaveAssetIfDirty(original);
                }
                if (!catalog.TryValidate(out var catalogError)) throw new InvalidOperationException(catalogError);
                PrefabUtility.SaveAsPrefabAsset(page, Page, out var saved);
                if (!saved) throw new InvalidOperationException("각성 버튼 Prefab 저장 실패");
                Debug.Log("COMMANDER_AWAKENING_PULL_BUTTON_APPLIED backup=" + backup);
            }
            catch
            {
                foreach (var pair in snapshots)
                {
                    EditorJsonUtility.FromJsonOverwrite(pair.Value, pair.Key);
                    EditorUtility.SetDirty(pair.Key);
                    AssetDatabase.SaveAssetIfDirty(pair.Key);
                }
                foreach (var added in addedPulls) if (added != null) UnityEngine.Object.DestroyImmediate(added, true);
                throw;
            }
            finally
            {
                if (page != null) PrefabUtility.UnloadPrefabContents(page);
                foreach (var plan in plans) UnityEngine.Object.DestroyImmediate(plan);
                foreach (var pull in pulls) if (pull != null && !AssetDatabase.Contains(pull)) UnityEngine.Object.DestroyImmediate(pull);
            }
        }

        private static void AppendEffect(CommanderSkillDefinition skill, CommanderSkillEffectDefinition effect)
        {
            using var serialized = new SerializedObject(skill);
            var effects = serialized.FindProperty("effects");
            effects.arraySize++;
            effects.GetArrayElementAtIndex(effects.arraySize - 1).objectReferenceValue = effect;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    internal static class CommanderSkillApprovedConcept
    {
        public static readonly IReadOnlyList<string> SkillIds = new[]
        {
            "CS_TrackingBlade", "CS_DoomSpear", "CS_AbyssChain", "CS_PhantomCharge",
            "CS_ConquerorSigil", "CS_PhantomBarrage", "CS_DeathSentence", "CS_RuptureMarch",
            "CS_HeartOfBattlefield", "CS_MarchOfDead", "CS_WarGodBrand", "CS_ApocalypseWar"
        };

        public static bool GuaranteesFirstBarrageHit(string skillId) => skillId == "CS_PhantomBarrage";

        public static float CastTimeFor(string skillId) => skillId switch
        {
            "CS_DoomSpear" => 0.25f,
            "CS_DeathSentence" => 0.20f,
            "CS_ApocalypseWar" => 0.35f,
            _ => 0f
        };

        public static string DescriptionFor(string skillId) => skillId switch
        {
            "CS_TrackingBlade" => "가장 가까운 적을 추적하는 마력검 6자루를 발사합니다. 타격마다 Pursuit를 쌓고, 5회 피격되면 추가 단일 피해를 줍니다.",
            "CS_DoomSpear" => "가장 가까운 적에게 거대한 마법창을 떨어뜨려 반경 2.8m의 적 최대 8명에게 피해를 주고 Rupture를 새깁니다. Rupture는 4회 피격되면 주변에 추가 폭발을 일으킵니다.",
            "CS_AbyssChain" => "가장 가까운 적부터 사슬이 최대 4명에게 연쇄됩니다. 사슬에 묶인 적은 3초 동안 이동속도가 20%, 방어력이 15% 감소합니다.",
            "CS_PhantomCharge" => "적이 가장 밀집한 전열을 유령 군세가 3회 관통합니다. 첫 돌진에 맞은 적 최대 4명을 군단장 쪽으로 조금 끌어오며, Collapse가 3회 쌓이면 0.45초 동안 기절시킵니다.",
            "CS_ConquerorSigil" => "아군이 가장 밀집한 곳에 6초 동안 정복의 문장을 설치합니다. 범위 안 아군 최대 5명의 공격력을 10% 높이고 받는 피해를 10% 줄이는 효과를 매초 갱신합니다.",
            "CS_PhantomBarrage" => "적이 밀집한 지역에 유령탄 7발을 퍼붓습니다. 첫 포탄은 중심에 떨어지고 나머지는 반경 4m에 무작위로 떨어집니다. Agitation이 3스택 쌓이면 주변에 추가 폭발을 일으킵니다.",
            "CS_DeathSentence" => "가장 강한 적을 공격하고 5초 동안 사형 선고를 내립니다. 선고 중 받은 타격 수를 기록해 만료될 때 기록량에 비례한 추가 피해를 줍니다.",
            "CS_RuptureMarch" => "적이 밀집한 지역을 반경 5m로 5회 파열시킵니다. 첫 파열에 맞은 적 최대 6명을 중심으로 조금 모으고 Rupture를 새겨 연속 타격으로 주변 폭발을 유도합니다.",
            "CS_HeartOfBattlefield" => "체력이 가장 낮은 아군을 중심으로 반경 6.5m의 아군 최대 5명을 회복합니다. 최대 체력의 12%를 회복하고 5초 동안 최대 체력의 15%만큼 보호막을 부여합니다.",
            "CS_MarchOfDead" => "적이 밀집한 넓은 전열을 망자 군세가 4회 휩씁니다. 적 최대 20명에게 Ruin을 쌓고, 4스택에 도달하면 큰 광역 폭발을 일으킨 뒤 낙인을 소모합니다.",
            "CS_WarGodBrand" => "가장 강한 적에게 10초 동안 군신의 낙인을 새겨 받는 피해를 15% 늘립니다. 대상에게 다른 Mark가 발동할 때마다 2초 동안 공격력을 10% 낮춥니다.",
            "CS_ApocalypseWar" => "적이 밀집한 지역에 반경 10m의 종말 지대를 8초 동안 펼쳐 매초 적 최대 24명에게 피해를 줍니다. 동시에 Mark 필요 타격을 30% 줄이고 Mark 발동 피해와 쿨타임 회복을 강화합니다.",
            _ => throw new ArgumentOutOfRangeException(nameof(skillId), skillId, "승인된 군단장 스킬 ID가 아닙니다.")
        };
    }
}
