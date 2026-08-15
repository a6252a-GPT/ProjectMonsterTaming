using System;
using System.Collections.Generic;
using ProjectMT.Shared.Commander;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Stats;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class GrowthCalculator : MonoBehaviour // 능력치 6종 강화 계산·UI 반영
    {
        private sealed class GrowthRow
        {
            public Button Button;
            public TMP_Text LevelText;
            public TMP_Text ValueText;
            public TMP_Text CostText;
        }

        [Header("체력")]
        [SerializeField] private Button healthButton; // 체력 강화 버튼
        [SerializeField] private TMP_Text healthLevelText; // 체력 레벨 표시 텍스트

        [Header("공격력")]
        [SerializeField] private Button attackButton; // 공격력 강화 버튼
        [SerializeField] private TMP_Text attackLevelText; // 공격력 레벨 표시 텍스트

        [Header("방어력")]
        [SerializeField] private Button defenseButton; // 방어력 강화 버튼
        [SerializeField] private TMP_Text defenseLevelText; // 방어력 레벨 표시 텍스트

        [Header("공격 속도")]
        [SerializeField] private Button attackSpeedButton; // 공격 속도 강화 버튼
        [SerializeField] private TMP_Text attackSpeedLevelText; // 공격 속도 레벨 표시 텍스트

        [Header("이동 속도")]
        [SerializeField] private Button moveSpeedButton; // 이동 속도 강화 버튼
        [SerializeField] private TMP_Text moveSpeedLevelText; // 이동 속도 레벨 표시 텍스트

        [Header("사거리")]
        [SerializeField] private Button attackRangeButton; // 사거리 강화 버튼
        [SerializeField] private TMP_Text attackRangeLevelText; // 사거리 레벨 표시 텍스트

        private readonly Dictionary<CommanderLegionStat, GrowthRow> rows = new();
        private IGameProgressService progress;
        private CommanderGrowthConfig config;
        private Action savedCallback;
        private bool savePending;

        public LegionStatBonus CurrentBonus { get; private set; }
        public event Action<LegionStatBonus> BonusChanged;

        private void Awake()
        {
            CacheRow(CommanderLegionStat.MaxHealth, "GrowthRow_Health", healthButton, healthLevelText);
            CacheRow(CommanderLegionStat.AttackPower, "GrowthRow_Attack", attackButton, attackLevelText);
            CacheRow(CommanderLegionStat.Defense, "GrowthRow_Defense", defenseButton, defenseLevelText);
            CacheRow(CommanderLegionStat.AttackSpeed, "GrowthRow_AttackSpeed", attackSpeedButton, attackSpeedLevelText);
            CacheRow(CommanderLegionStat.MoveSpeed, "GrowthRow_MoveSpeed", moveSpeedButton, moveSpeedLevelText);
            CacheRow(CommanderLegionStat.AttackRange, "GrowthRow_AttackRange", attackRangeButton, attackRangeLevelText);

            EnsureFullRectHitArea(healthButton);
            EnsureFullRectHitArea(attackButton);
            EnsureFullRectHitArea(defenseButton);
            EnsureFullRectHitArea(attackSpeedButton);
            EnsureFullRectHitArea(moveSpeedButton);
            EnsureFullRectHitArea(attackRangeButton);

            healthButton?.onClick.AddListener(UpgradeHealth);
            attackButton?.onClick.AddListener(UpgradeAttack);
            defenseButton?.onClick.AddListener(UpgradeDefense);
            attackSpeedButton?.onClick.AddListener(UpgradeAttackSpeed);
            moveSpeedButton?.onClick.AddListener(UpgradeMoveSpeed);
            attackRangeButton?.onClick.AddListener(UpgradeAttackRange);
        }

        private void OnEnable() => Refresh();

        private void OnDestroy()
        {
            if (progress != null)
            {
                progress.Changed -= Refresh;
            }

            healthButton?.onClick.RemoveListener(UpgradeHealth);
            attackButton?.onClick.RemoveListener(UpgradeAttack);
            defenseButton?.onClick.RemoveListener(UpgradeDefense);
            attackSpeedButton?.onClick.RemoveListener(UpgradeAttackSpeed);
            moveSpeedButton?.onClick.RemoveListener(UpgradeMoveSpeed);
            attackRangeButton?.onClick.RemoveListener(UpgradeAttackRange);
        }

        public void Configure(
            IGameProgressService progressService,
            CommanderGrowthConfig growthConfig,
            Action onSaved = null)
        {
            if (progress != null)
            {
                progress.Changed -= Refresh;
            }

            progress = progressService;
            config = growthConfig;
            savedCallback = onSaved;
            if (progress != null)
            {
                progress.Changed += Refresh;
            }

            Refresh();
        }

        private async void Upgrade(CommanderLegionStat stat)
        {
            if (savePending || progress == null || !progress.IsLoaded || config == null)
            {
                return;
            }

            var level = progress.View.CommanderLegionGrowth.GetLevel(stat);
            if (level >= config.GetLegionGrowthMaxLevel(stat))
            {
                return;
            }

            savePending = true;
            Refresh();
            try
            {
                if (await progress.TryApplyAndSaveAsync(GameProgressChange.UpgradeCommanderLegionStat(stat, level)))
                {
                    savedCallback?.Invoke(); // 현재 전투는 유지하고 다음 전투 Snapshot만 갱신
                }
            }
            finally
            {
                savePending = false;
                Refresh();
            }
        }

        private void UpgradeHealth() => Upgrade(CommanderLegionStat.MaxHealth);
        private void UpgradeAttack() => Upgrade(CommanderLegionStat.AttackPower);
        private void UpgradeDefense() => Upgrade(CommanderLegionStat.Defense);
        private void UpgradeAttackSpeed() => Upgrade(CommanderLegionStat.AttackSpeed);
        private void UpgradeMoveSpeed() => Upgrade(CommanderLegionStat.MoveSpeed);
        private void UpgradeAttackRange() => Upgrade(CommanderLegionStat.AttackRange);

        private void Refresh()
        {
            if (progress == null || !progress.IsLoaded || config == null)
            {
                return;
            }

            var progressView = progress.View;
            var growth = progressView.CommanderLegionGrowth;
            foreach (var pair in rows)
            {
                var stat = pair.Key;
                var row = pair.Value;
                var level = growth.GetLevel(stat);
                var maxLevel = config.GetLegionGrowthMaxLevel(stat);
                var maxed = level >= maxLevel;
                SetText(row.LevelText, $"LV. {level}");
                SetText(row.ValueText, $"+{config.GetLegionGrowthRate(stat, level) * 100f:0}%");
                SetText(row.CostText, maxed ? "MAX" : FormatCost(stat, level));
                if (row.Button != null)
                {
                    row.Button.interactable = !savePending && !maxed && CanAfford(progressView, stat, level);
                }
            }

            CurrentBonus = new LegionStatBonus(
                config.GetLegionGrowthRate(CommanderLegionStat.MaxHealth, growth.HealthLevel),
                config.GetLegionGrowthRate(CommanderLegionStat.AttackPower, growth.AttackLevel),
                config.GetLegionGrowthRate(CommanderLegionStat.Defense, growth.DefenseLevel),
                config.GetLegionGrowthRate(CommanderLegionStat.AttackSpeed, growth.AttackSpeedLevel),
                config.GetLegionGrowthRate(CommanderLegionStat.MoveSpeed, growth.MoveSpeedLevel),
                config.GetLegionGrowthRate(CommanderLegionStat.AttackRange, growth.AttackRangeLevel));
            BonusChanged?.Invoke(CurrentBonus);
        }

        private bool CanAfford(GameProgressView view, CommanderLegionStat stat, int level)
        {
            return config.UsesGoldForLegionGrowth(stat)
                ? view.Gold >= config.GetLegionGrowthGoldCost(stat, level)
                : view.CommanderLegionGrowth.UnspentTrainingPoints >=
                  config.GetLegionGrowthTrainingPointCost(stat, level);
        }

        private string FormatCost(CommanderLegionStat stat, int level)
        {
            return config.UsesGoldForLegionGrowth(stat)
                ? $"{config.GetLegionGrowthGoldCost(stat, level):N0}"
                : $"{config.GetLegionGrowthTrainingPointCost(stat, level):N0} P";
        }

        private void CacheRow(
            CommanderLegionStat stat,
            string rowName,
            Button button,
            TMP_Text levelText)
        {
            var rowTransform = FindDeep(transform, rowName);
            rows[stat] = new GrowthRow
            {
                Button = button,
                LevelText = levelText,
                ValueText = rowTransform?.Find("Text_Value")?.GetComponent<TMP_Text>(),
                CostText = rowTransform?.Find("ButtonArea/Group/Text_Value")?.GetComponent<TMP_Text>()
            };
        }

        private static Transform FindDeep(Transform root, string targetName)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < all.Length; index++)
            {
                if (all[index].name == targetName)
                {
                    return all[index];
                }
            }

            return null;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static void EnsureFullRectHitArea(Button button)
        {
            if (button == null)
            {
                return;
            }

            var hitArea = button.GetComponent<Graphic>();
            if (hitArea == null)
            {
                var image = button.gameObject.AddComponent<Image>();
                image.color = Color.clear;
                hitArea = image;
            }

            hitArea.raycastTarget = true;
            if (button.targetGraphic == null)
            {
                button.targetGraphic = hitArea;
            }
        }
    }
}
