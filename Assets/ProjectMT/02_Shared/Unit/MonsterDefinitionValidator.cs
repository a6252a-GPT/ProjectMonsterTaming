using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum MonsterValidationSeverity
    {
        Warning,
        Error
    }

    public readonly struct MonsterValidationIssue
    {
        public MonsterValidationIssue(
            MonsterValidationSeverity severity,
            string code,
            string message,
            UnityEngine.Object context)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Context = context;
        }

        public MonsterValidationSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public UnityEngine.Object Context { get; }
    }

    public sealed class MonsterValidationReport
    {
        private readonly List<MonsterValidationIssue> issues = new List<MonsterValidationIssue>();

        public IReadOnlyList<MonsterValidationIssue> Issues => issues;
        public bool HasErrors
        {
            get
            {
                for (var index = 0; index < issues.Count; index++)
                {
                    if (issues[index].Severity == MonsterValidationSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        internal void Add(
            MonsterValidationSeverity severity,
            string code,
            string message,
            UnityEngine.Object context)
        {
            issues.Add(new MonsterValidationIssue(severity, code, message, context));
        }
    }

    public static class MonsterDefinitionValidator // Maker와 Runtime이 공유하는 구조 검사
    {
        public static MonsterValidationReport Validate(
            MonsterDefinition definition,
            bool requireFormalRuntime)
        {
            var report = new MonsterValidationReport();
            if (definition == null)
            {
                report.Add(MonsterValidationSeverity.Error, "MON-NULL", "Monster Definition is missing.", null);
                return report;
            }

            if (string.IsNullOrWhiteSpace(definition.MonsterId))
            {
                report.Add(MonsterValidationSeverity.Error, "MON-ID-EMPTY", "Monster ID is blank.", definition);
            }
            else if (!UsesAllowedIdCharacters(definition.MonsterId))
            {
                report.Add(
                    MonsterValidationSeverity.Error,
                    "MON-ID-CHAR",
                    "Monster ID may use letters, numbers, underscore and hyphen only.",
                    definition);
            }

            if (!definition.HasExplicitDisplayName)
            {
                report.Add(MonsterValidationSeverity.Error, "MON-NAME", "Monster display name is blank.", definition);
            }

            if (definition.MaxHealth <= 0f || definition.AttackPower < 0f || definition.Defense < 0f ||
                definition.AttackSpeed <= 0f || definition.MoveSpeed < 0f || definition.AttackRange <= 0f)
            {
                report.Add(MonsterValidationSeverity.Error, "MON-STATS", "Monster base stats are invalid.", definition);
            }

            if (definition.Portrait == null)
            {
                report.Add(MonsterValidationSeverity.Error, "MON-PORTRAIT", "Monster portrait is missing.", definition);
            }

            if (definition.PreviewPrefab == null)
            {
                report.Add(MonsterValidationSeverity.Error, "MON-PREVIEW", "Monster preview prefab is missing.", definition);
            }

            if (definition.RuntimeAssetSet == null)
            {
                if (requireFormalRuntime)
                {
                    report.Add(
                        MonsterValidationSeverity.Error,
                        "MON-RUNTIME",
                        "Formal Monster requires a Runtime Asset Set.",
                        definition);
                }

                return report;
            }

            if (string.IsNullOrWhiteSpace(definition.RuntimeAssetKey))
            {
                report.Add(
                    MonsterValidationSeverity.Error,
                    "MON-RUNTIME-KEY",
                    "Formal Monster requires a Runtime Asset Key.",
                    definition);
            }

            if (!definition.RuntimeAssetSet.TryValidate(out var runtimeError))
            {
                report.Add(
                    MonsterValidationSeverity.Error,
                    "MON-RUNTIME-INVALID",
                    runtimeError,
                    definition.RuntimeAssetSet);
                return report;
            }

            AddMotionWarnings(definition.RuntimeAssetSet.MotionProfile, report);
            return report;
        }

        private static bool UsesAllowedIdCharacters(string value)
        {
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var isAsciiLetter = character >= 'A' && character <= 'Z' || character >= 'a' && character <= 'z';
                var isAsciiDigit = character >= '0' && character <= '9';
                if (!isAsciiLetter && !isAsciiDigit && character != '_' && character != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private static void AddMotionWarnings(
            MonsterMotionProfile motionProfile,
            MonsterValidationReport report)
        {
            if (motionProfile.Idle.Clip != null && !motionProfile.Idle.Clip.isLooping)
            {
                report.Add(
                    MonsterValidationSeverity.Warning,
                    "MON-IDLE-LOOP",
                    "Idle clip is not imported as looping. The tool will not change it automatically.",
                    motionProfile.Idle.Clip);
            }

            if (motionProfile.Move.Clip != null && !motionProfile.Move.Clip.isLooping)
            {
                report.Add(
                    MonsterValidationSeverity.Warning,
                    "MON-MOVE-LOOP",
                    "Move clip is not imported as looping. The tool will not change it automatically.",
                    motionProfile.Move.Clip);
            }

            if (motionProfile.Death.Clip != null && motionProfile.Death.Clip.isLooping)
            {
                report.Add(
                    MonsterValidationSeverity.Warning,
                    "MON-DEATH-LOOP",
                    "Death clip is imported as looping. The tool will not change it automatically.",
                    motionProfile.Death.Clip);
            }

            var attacks = motionProfile.Attacks;
            var totalWeight = 0f;
            for (var index = 0; index < attacks.Length; index++)
            {
                totalWeight += attacks[index].Weight;
                if (attacks[index].Clip != null && attacks[index].Clip.isLooping)
                {
                    report.Add(
                        MonsterValidationSeverity.Warning,
                        "MON-ATTACK-LOOP",
                        $"Attack clip is imported as looping. Motion={attacks[index].MotionId}",
                        attacks[index].Clip);
                }
            }

            if (totalWeight <= 0f)
            {
                report.Add(
                    MonsterValidationSeverity.Warning,
                    "MON-ATTACK-WEIGHT",
                    "All Attack weights are zero. Runtime will use an even fallback selection.",
                    motionProfile);
            }
        }

    }
}
