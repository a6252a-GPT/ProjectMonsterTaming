using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Features.Expedition;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.ExpeditionBalance
{
    internal readonly struct ExpeditionEnemyBalanceValues
    {
        public ExpeditionEnemyBalanceValues(
            EnemyAppearanceGroup group,
            float healthMultiplier,
            float damageMultiplier,
            float defenseMultiplier,
            float attacksPerSecond,
            float moveSpeed,
            float attackRange)
        {
            Group = group;
            HealthMultiplier = healthMultiplier;
            DamageMultiplier = damageMultiplier;
            DefenseMultiplier = defenseMultiplier;
            AttacksPerSecond = attacksPerSecond;
            MoveSpeed = moveSpeed;
            AttackRange = attackRange;
        }

        public EnemyAppearanceGroup Group { get; }
        public float HealthMultiplier { get; }
        public float DamageMultiplier { get; }
        public float DefenseMultiplier { get; }
        public float AttacksPerSecond { get; }
        public float MoveSpeed { get; }
        public float AttackRange { get; }

        public ExpeditionEnemyTypeBalance ToRuntime() => new ExpeditionEnemyTypeBalance(
            Group,
            HealthMultiplier,
            DamageMultiplier,
            DefenseMultiplier,
            1f / Mathf.Max(0.01f, AttacksPerSecond),
            MoveSpeed,
            AttackRange);
    }

    internal static class ExpeditionEnemyBalanceAssetWriter
    {
        internal const string ProfilePath =
            "Assets/ProjectMT/03_Features/Expedition/Data/ExpeditionSeedProfile_Seed.asset";
        internal const string AppearanceSetPath =
            "Assets/ProjectMT/03_Features/Expedition/Data/EnemyStageAppearanceSet_Seed.asset";

        public static ExpeditionSeedProfile LoadProfile() =>
            AssetDatabase.LoadAssetAtPath<ExpeditionSeedProfile>(ProfilePath);

        public static EnemyStageAppearanceSet LoadAppearanceSet() =>
            AssetDatabase.LoadAssetAtPath<EnemyStageAppearanceSet>(AppearanceSetPath);

        public static string CaptureSourceJson(ExpeditionSeedProfile profile) =>
            profile == null ? string.Empty : EditorJsonUtility.ToJson(profile);

        public static bool TryApply(
            IReadOnlyList<ExpeditionEnemyBalanceValues> rows,
            string expectedSourceJson,
            out string error)
        {
            error = string.Empty;
            var profile = LoadProfile();
            if (profile == null)
            {
                error = "운영 ExpeditionSeedProfile_Seed.asset을 찾을 수 없습니다.";
                return false;
            }

            if (!string.Equals(CaptureSourceJson(profile), expectedSourceJson, StringComparison.Ordinal))
            {
                error = "표를 연 뒤 원본이 외부에서 변경됐습니다. 새로고침 후 다시 수정하세요.";
                return false;
            }

            if (!Validate(rows, out error)) return false;
            var snapshot = CaptureSourceJson(profile);
            try
            {
                Undo.RecordObject(profile, "Apply expedition enemy balance table");
                profile.EditorConfigureEnemyTypeBalances(rows.Select(row => row.ToRuntime()).ToArray());
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                return true;
            }
            catch (Exception exception)
            {
                EditorJsonUtility.FromJsonOverwrite(snapshot, profile);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                error = $"적 밸런스 원본 반영 중 복구했습니다: {exception.Message}";
                return false;
            }
        }

        private static bool Validate(IReadOnlyList<ExpeditionEnemyBalanceValues> rows, out string error)
        {
            error = string.Empty;
            var groups = (EnemyAppearanceGroup[])Enum.GetValues(typeof(EnemyAppearanceGroup));
            if (rows == null || rows.Count != groups.Length)
            {
                error = $"적 종류는 {groups.Length}행이어야 합니다.";
                return false;
            }

            if (rows.Select(row => row.Group).Distinct().Count() != groups.Length)
            {
                error = "적 종류가 중복됐거나 빠졌습니다.";
                return false;
            }

            foreach (var row in rows)
            {
                if (!IsFinite(row.HealthMultiplier) || row.HealthMultiplier <= 0f ||
                    !IsFinite(row.DamageMultiplier) || row.DamageMultiplier <= 0f ||
                    !IsFinite(row.DefenseMultiplier) || row.DefenseMultiplier < 0f ||
                    !IsFinite(row.AttacksPerSecond) || row.AttacksPerSecond <= 0f ||
                    !IsFinite(row.MoveSpeed) || row.MoveSpeed <= 0f ||
                    !IsFinite(row.AttackRange) || row.AttackRange < 0.2f)
                {
                    error = $"{row.Group} 행에 0 이하이거나 유효하지 않은 값이 있습니다.";
                    return false;
                }
            }

            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
