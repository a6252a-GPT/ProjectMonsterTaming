using NUnit.Framework;
using ProjectMT.Features.WorldDrops;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.Items;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.CastleRaidHex.Editor.Tests
{
    public sealed class HexCastleBattleHudContractTests
    {
        private const string HudPrefabPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Prefabs/PF_CastleRaidHexHUD.prefab";
        private const string ItemCatalogPath =
            "Assets/ProjectMT/02_Shared/Items/Data/ItemCatalog.asset";
        private const string DropCatalogPath =
            "Assets/ProjectMT/03_Features/WorldDrops/Data/WorldItemDropVisualCatalog.asset";
        private const string EquipmentBalancePath =
            "Assets/ProjectMT/02_Shared/Equipment/Data/EquipmentBalanceConfig.asset";
        private const string EquipmentDropCatalogPath =
            "Assets/ProjectMT/03_Features/WorldDrops/Data/EquipmentDropChestVisualCatalog.asset";

        [Test]
        public void ProductionHud_HasThreeMinuteClockAndClearStyleFailureStage()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var view = prefab.GetComponent<HexCastleBattleHudView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.HasRuntimeBindings, Is.True);
            Assert.That(view.ItemCatalog, Is.Not.Null);
            Assert.That(view.ItemDropVisualCatalog, Is.Not.Null);
            Assert.That(view.EquipmentBalanceConfig, Is.Not.Null);
            Assert.That(view.EquipmentDropVisualCatalog, Is.Not.Null);

            var timer = Find(prefab.transform, "TimerText")?.GetComponent<TMP_Text>();
            var overlay = Find(prefab.transform, "FailureOverlay");
            var stage = Find(prefab.transform, "FailureResultStage_920x900");
            var retry = Find(prefab.transform, "FreeRetryButton")?.GetComponent<Button>();
            var leave = Find(prefab.transform, "LeaveButton")?.GetComponent<Button>();
            Assert.That(timer, Is.Not.Null);
            Assert.That(timer.text, Is.EqualTo("03:00"));
            Assert.That(overlay, Is.Not.Null);
            Assert.That(stage, Is.Not.Null);
            Assert.That(Find(prefab.transform, "FailureDialog_Standard"), Is.Null);
            Assert.That(retry, Is.Not.Null);
            Assert.That(leave, Is.Not.Null);
            Assert.That(retry.GetComponent<RectTransform>().anchoredPosition.x, Is.EqualTo(-166f).Within(0.1f));
            Assert.That(leave.GetComponent<RectTransform>().anchoredPosition.x, Is.EqualTo(166f).Within(0.1f));
            Assert.That(Find(retry.transform, "Label")?.GetComponent<TMP_Text>()?.text,
                Is.EqualTo("무료 재도전"));
            Assert.That(Find(leave.transform, "Label")?.GetComponent<TMP_Text>()?.text,
                Is.EqualTo("나가기"));
            Assert.That(Find(stage, "ResultKicker")?.GetComponent<TMP_Text>()?.text,
                Is.EqualTo("군단의 역습 · 전투 결과"));
            Assert.That(Find(stage, "TitleText")?.GetComponent<TMP_Text>()?.text,
                Is.EqualTo("공략 실패"));
            Assert.That(Find(stage, "FailureDetailText")?.GetComponent<TMP_Text>(), Is.Not.Null);
            Assert.That(Find(stage, "VictoryIllustration")?.gameObject.activeSelf, Is.False);
            Assert.That(Find(stage, "RewardPackage")?.gameObject.activeSelf, Is.False);
            var firstStar = Find(stage, "Star_1")?.GetComponent<Image>();
            var secondStar = Find(stage, "Star_2")?.GetComponent<Image>();
            var thirdStar = Find(stage, "Star_3")?.GetComponent<Image>();
            Assert.That(firstStar?.sprite, Is.Not.Null);
            Assert.That(secondStar?.sprite, Is.SameAs(firstStar.sprite));
            Assert.That(thirdStar?.sprite, Is.SameAs(firstStar.sprite));
            Assert.That(overlay.gameObject.activeSelf, Is.False);
        }

        [TestCase(ItemIds.Gold)]
        [TestCase(ItemIds.FoodRiotKey)]
        [TestCase(ItemIds.TreasureSpiritKey)]
        [TestCase(ItemIds.FallenCommanderKey)]
        [TestCase(ItemIds.GuardiansTowerKey)]
        public void EveryLootBuildingReward_HasInventoryIconAndWorldDropVisual(string itemId)
        {
            var itemCatalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            var dropCatalog = AssetDatabase.LoadAssetAtPath<WorldItemDropVisualCatalog>(DropCatalogPath);
            Assert.That(itemCatalog, Is.Not.Null);
            Assert.That(dropCatalog, Is.Not.Null);
            Assert.That(itemCatalog.TryGet(itemId, out var definition), Is.True, itemId);
            Assert.That(definition.Icon, Is.Not.Null, $"{itemId} 인벤토리 아이콘");
            Assert.That(dropCatalog.TryResolve(itemId, out var visual), Is.True, itemId);
            Assert.That(visual.ModelPrefab, Is.Not.Null, $"{itemId} 월드 드랍 모델");
        }

        [Test]
        public void EquipmentLoot_UsesValidBalanceAndEveryGradeChestVisual()
        {
            var balance = AssetDatabase.LoadAssetAtPath<EquipmentBalanceConfig>(EquipmentBalancePath);
            var dropCatalog = AssetDatabase.LoadAssetAtPath<EquipmentDropChestVisualCatalog>(
                EquipmentDropCatalogPath);
            Assert.That(balance, Is.Not.Null);
            Assert.That(dropCatalog, Is.Not.Null);
            Assert.That(balance.TryValidate(out var balanceError), Is.True, balanceError);
            Assert.That(dropCatalog.TryValidate(out var dropError), Is.True, dropError);
            foreach (EquipmentGrade grade in System.Enum.GetValues(typeof(EquipmentGrade)))
            {
                Assert.That(dropCatalog.TryResolve(grade, out var visual), Is.True, grade.ToString());
                Assert.That(visual.ModelPrefab, Is.Not.Null, grade.ToString());
            }
        }

        private static Transform Find(Transform root, string objectName)
        {
            foreach (var value in root.GetComponentsInChildren<Transform>(true))
            {
                if (value.name == objectName)
                {
                    return value;
                }
            }

            return null;
        }
    }
}
