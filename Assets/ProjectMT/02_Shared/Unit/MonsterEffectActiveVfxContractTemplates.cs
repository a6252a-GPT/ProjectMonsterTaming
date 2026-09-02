#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Unit;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public enum MonsterEffectTargetPresentationMode
    {
        OneShot,
        DurationLifecycle
    }

    public enum MonsterEffectPresentationContractRole
    {
        CasterActivation,
        TargetApplied,
        TargetLoop,
        TargetExpired,
        Custom
    }

    public static class MonsterEffectActiveVfxContractTemplates
        // 효과형은 시전자 발동과 적용 대상의 1회/지속 수명주기만 제작자가 고른다.
    {
        public static MonsterEffectTargetPresentationMode ResolveTargetMode(
            MonsterEffectActiveGroup group)
        {
            if (group?.PresentationSlots == null) return MonsterEffectTargetPresentationMode.OneShot;
            return group.PresentationSlots.Any(slot =>
                    slot != null && ResolveRole(slot) is
                        MonsterEffectPresentationContractRole.TargetLoop or
                        MonsterEffectPresentationContractRole.TargetExpired)
                ? MonsterEffectTargetPresentationMode.DurationLifecycle
                : MonsterEffectTargetPresentationMode.OneShot;
        }

        public static MonsterActivePresentationSlot[] Build(
            MonsterEffectActiveGroup group,
            MonsterEffectTargetPresentationMode requestedMode)
        {
            if (group == null) return Array.Empty<MonsterActivePresentationSlot>();
            var mode = requestedMode == MonsterEffectTargetPresentationMode.DurationLifecycle &&
                       group.HasDurationPresentation
                ? requestedMode
                : MonsterEffectTargetPresentationMode.OneShot;
            var existing = group.PresentationSlots
                .Where(slot => slot != null)
                .ToList();
            var used = new HashSet<MonsterActivePresentationSlot>();
            var ids = new HashSet<string>(
                existing.Select(slot => slot.SlotId),
                StringComparer.OrdinalIgnoreCase);

            MonsterActivePresentationSlot Take(MonsterEffectPresentationContractRole role)
            {
                var value = existing.FirstOrDefault(slot =>
                    !used.Contains(slot) && ResolveRole(slot) == role);
                if (value != null) used.Add(value);
                return value;
            }

            var result = new List<MonsterActivePresentationSlot>
            {
                Configure(
                    Take(MonsterEffectPresentationContractRole.CasterActivation),
                    CreateId(ids, "cast_start"),
                    "시전자 발동",
                    MonsterActivePresentationEvent.MotionStart,
                    MonsterActivePresentationAnchor.CasterRoot,
                    MonsterActivePresentationMultiplicity.OncePerStep,
                    MonsterActivePresentationAttachment.World,
                    MonsterActivePresentationEndPolicy.ParticleDuration)
            };
            var applied = Take(MonsterEffectPresentationContractRole.TargetApplied);
            result.Add(Configure(
                applied,
                CreateId(ids, mode == MonsterEffectTargetPresentationMode.OneShot
                    ? "target_apply"
                    : "target_start"),
                mode == MonsterEffectTargetPresentationMode.OneShot
                    ? "대상 적용 · 1회"
                    : "대상 시작 · 1회",
                MonsterActivePresentationEvent.EffectApplied,
                MonsterActivePresentationAnchor.TargetRoot,
                MonsterActivePresentationMultiplicity.PerTargetHit,
                MonsterActivePresentationAttachment.World,
                MonsterActivePresentationEndPolicy.ParticleDuration));

            if (mode == MonsterEffectTargetPresentationMode.DurationLifecycle)
            {
                result.Add(Configure(
                    Take(MonsterEffectPresentationContractRole.TargetLoop),
                    CreateId(ids, "target_loop"),
                    "대상 지속",
                    MonsterActivePresentationEvent.EffectApplied,
                    MonsterActivePresentationAnchor.TargetRoot,
                    MonsterActivePresentationMultiplicity.ContinuousUntilEnd,
                    MonsterActivePresentationAttachment.FollowAnchor,
                    MonsterActivePresentationEndPolicy.Timed,
                    true,
                    group.PresentationDuration));
                result.Add(Configure(
                    Take(MonsterEffectPresentationContractRole.TargetExpired),
                    CreateId(ids, "target_end"),
                    "대상 끝 · 1회",
                    MonsterActivePresentationEvent.EffectExpired,
                    MonsterActivePresentationAnchor.TargetRoot,
                    MonsterActivePresentationMultiplicity.PerTargetHit,
                    MonsterActivePresentationAttachment.World,
                    MonsterActivePresentationEndPolicy.ParticleDuration));
            }

            foreach (var source in existing.Where(slot => !used.Contains(slot)))
            {
                var role = ResolveRole(source);
                if (mode == MonsterEffectTargetPresentationMode.OneShot && role is
                        MonsterEffectPresentationContractRole.TargetLoop or
                        MonsterEffectPresentationContractRole.TargetExpired)
                {
                    continue;
                }
                result.Add(role switch
                {
                    MonsterEffectPresentationContractRole.CasterActivation => Configure(
                        source, source.SlotId, "시전자 발동",
                        MonsterActivePresentationEvent.MotionStart,
                        MonsterActivePresentationAnchor.CasterRoot,
                        MonsterActivePresentationMultiplicity.OncePerStep,
                        MonsterActivePresentationAttachment.World,
                        MonsterActivePresentationEndPolicy.ParticleDuration),
                    MonsterEffectPresentationContractRole.TargetApplied => Configure(
                        source, source.SlotId,
                        mode == MonsterEffectTargetPresentationMode.OneShot
                            ? "대상 적용 · 1회"
                            : "대상 시작 · 1회",
                        MonsterActivePresentationEvent.EffectApplied,
                        MonsterActivePresentationAnchor.TargetRoot,
                        MonsterActivePresentationMultiplicity.PerTargetHit,
                        MonsterActivePresentationAttachment.World,
                        MonsterActivePresentationEndPolicy.ParticleDuration),
                    MonsterEffectPresentationContractRole.TargetLoop => Configure(
                        source, source.SlotId, "대상 지속",
                        MonsterActivePresentationEvent.EffectApplied,
                        MonsterActivePresentationAnchor.TargetRoot,
                        MonsterActivePresentationMultiplicity.ContinuousUntilEnd,
                        MonsterActivePresentationAttachment.FollowAnchor,
                        MonsterActivePresentationEndPolicy.Timed,
                        true,
                        group.PresentationDuration),
                    MonsterEffectPresentationContractRole.TargetExpired => Configure(
                        source, source.SlotId, "대상 끝 · 1회",
                        MonsterActivePresentationEvent.EffectExpired,
                        MonsterActivePresentationAnchor.TargetRoot,
                        MonsterActivePresentationMultiplicity.PerTargetHit,
                        MonsterActivePresentationAttachment.World,
                        MonsterActivePresentationEndPolicy.ParticleDuration),
                    _ => source.Clone()
                });
            }
            return result.ToArray();
        }

        public static MonsterActivePresentationSlot[] RefreshExisting(
            MonsterEffectActiveGroup group)
        {
            if (group == null) return Array.Empty<MonsterActivePresentationSlot>();
            var mode = ResolveTargetMode(group);
            if (!group.HasDurationPresentation)
                mode = MonsterEffectTargetPresentationMode.OneShot;
            var result = new List<MonsterActivePresentationSlot>();
            foreach (var source in group.PresentationSlots.Where(slot => slot != null))
            {
                var role = ResolveRole(source);
                if (!group.HasDurationPresentation && role is
                        MonsterEffectPresentationContractRole.TargetLoop or
                        MonsterEffectPresentationContractRole.TargetExpired)
                {
                    continue;
                }
                result.Add(role switch
                {
                    MonsterEffectPresentationContractRole.CasterActivation => Configure(
                        source, source.SlotId, "시전자 발동",
                        MonsterActivePresentationEvent.MotionStart,
                        MonsterActivePresentationAnchor.CasterRoot,
                        MonsterActivePresentationMultiplicity.OncePerStep,
                        MonsterActivePresentationAttachment.World,
                        MonsterActivePresentationEndPolicy.ParticleDuration),
                    MonsterEffectPresentationContractRole.TargetApplied => Configure(
                        source, source.SlotId,
                        mode == MonsterEffectTargetPresentationMode.OneShot
                            ? "대상 적용 · 1회"
                            : "대상 시작 · 1회",
                        MonsterActivePresentationEvent.EffectApplied,
                        MonsterActivePresentationAnchor.TargetRoot,
                        MonsterActivePresentationMultiplicity.PerTargetHit,
                        MonsterActivePresentationAttachment.World,
                        MonsterActivePresentationEndPolicy.ParticleDuration),
                    MonsterEffectPresentationContractRole.TargetLoop => Configure(
                        source, source.SlotId, "대상 지속",
                        MonsterActivePresentationEvent.EffectApplied,
                        MonsterActivePresentationAnchor.TargetRoot,
                        MonsterActivePresentationMultiplicity.ContinuousUntilEnd,
                        MonsterActivePresentationAttachment.FollowAnchor,
                        MonsterActivePresentationEndPolicy.Timed,
                        true,
                        group.PresentationDuration),
                    MonsterEffectPresentationContractRole.TargetExpired => Configure(
                        source, source.SlotId, "대상 끝 · 1회",
                        MonsterActivePresentationEvent.EffectExpired,
                        MonsterActivePresentationAnchor.TargetRoot,
                        MonsterActivePresentationMultiplicity.PerTargetHit,
                        MonsterActivePresentationAttachment.World,
                        MonsterActivePresentationEndPolicy.ParticleDuration),
                    _ => source.Clone()
                });
            }
            return result.ToArray();
        }

        public static bool HasRole(
            MonsterEffectActiveGroup group,
            MonsterEffectPresentationContractRole role)
        {
            return group?.PresentationSlots.Any(slot =>
                slot != null && ResolveRole(slot) == role) == true;
        }

        public static MonsterEffectPresentationContractRole ResolveRole(
            MonsterActivePresentationSlot slot)
        {
            if (slot == null) return MonsterEffectPresentationContractRole.Custom;
            var id = slot.SlotId.ToLowerInvariant();
            var title = slot.DisplayName;
            if (slot.Timing == MonsterActivePresentationEvent.EffectExpired ||
                id.Contains("end") || id.Contains("expire") || title.Contains("끝") || title.Contains("종료"))
            {
                return MonsterEffectPresentationContractRole.TargetExpired;
            }
            if (slot.Multiplicity == MonsterActivePresentationMultiplicity.ContinuousUntilEnd ||
                id.Contains("loop") || title.Contains("지속"))
            {
                return MonsterEffectPresentationContractRole.TargetLoop;
            }
            if (id.Contains("apply") || id.Contains("target") ||
                title.Contains("적용") || title.Contains("대상") ||
                (slot.Anchor is MonsterActivePresentationAnchor.TargetPoint or
                    MonsterActivePresentationAnchor.TargetRoot or
                    MonsterActivePresentationAnchor.HitPoint or
                    MonsterActivePresentationAnchor.AreaCenter &&
                 slot.Timing is MonsterActivePresentationEvent.Impact or
                     MonsterActivePresentationEvent.AreaResolved or
                     MonsterActivePresentationEvent.EffectApplied))
            {
                return MonsterEffectPresentationContractRole.TargetApplied;
            }
            if (slot.Anchor == MonsterActivePresentationAnchor.CasterRoot &&
                slot.Timing is MonsterActivePresentationEvent.MotionStart or
                    MonsterActivePresentationEvent.Launch)
            {
                return MonsterEffectPresentationContractRole.CasterActivation;
            }
            return MonsterEffectPresentationContractRole.Custom;
        }

        public static string RoleLabel(MonsterEffectPresentationContractRole role) => role switch
        {
            MonsterEffectPresentationContractRole.CasterActivation => "시전자 발동",
            MonsterEffectPresentationContractRole.TargetApplied => "적용 대상 · 1회",
            MonsterEffectPresentationContractRole.TargetLoop => "적용 대상 · 지속",
            MonsterEffectPresentationContractRole.TargetExpired => "적용 대상 · 끝",
            _ => "추가 공간"
        };

        public static string ContractDetails(MonsterActivePresentationSlot slot)
        {
            if (slot == null) return "계약이 비어 있습니다.";
            var role = ResolveRole(slot);
            return role switch
            {
                MonsterEffectPresentationContractRole.CasterActivation =>
                    "스킬 발동 순간 · 시전자 위치 · 1회 · 파티클 원래 수명",
                MonsterEffectPresentationContractRole.TargetApplied =>
                    "효과가 실제 적용되는 순간 · 적용된 각 유닛 위치 · 대상마다 1회",
                MonsterEffectPresentationContractRole.TargetLoop =>
                    $"효과가 실제 적용되는 순간부터 {slot.Duration:0.##}초 · 적용된 각 유닛 추적 · 종료 시 정리",
                MonsterEffectPresentationContractRole.TargetExpired =>
                    "효과 지속시간 종료 순간 · 살아 있는 적용 유닛 위치 · 대상마다 1회",
                _ => $"{slot.Timing} · {slot.Anchor} · {slot.Multiplicity} · {slot.Attachment} · {slot.EndPolicy}"
            };
        }

        private static MonsterActivePresentationSlot Configure(
            MonsterActivePresentationSlot source,
            string fallbackId,
            string title,
            MonsterActivePresentationEvent timing,
            MonsterActivePresentationAnchor anchor,
            MonsterActivePresentationMultiplicity multiplicity,
            MonsterActivePresentationAttachment attachment,
            MonsterActivePresentationEndPolicy endPolicy,
            bool useDuration = false,
            float duration = 1f)
        {
            var result = source?.Clone() ?? new MonsterActivePresentationSlot();
            result.EditorConfigure(
                source == null ? fallbackId : source.SlotId,
                title,
                timing,
                anchor,
                source?.Description ?? string.Empty,
                useDuration,
                Math.Max(0.05f, duration),
                multiplicity,
                attachment,
                endPolicy);
            return result;
        }

        private static string CreateId(HashSet<string> ids, string preferred)
        {
            if (ids.Add(preferred)) return preferred;
            for (var suffix = 2; suffix < 10000; suffix++)
            {
                var candidate = $"{preferred}_{suffix:00}";
                if (ids.Add(candidate)) return candidate;
            }
            return preferred + "_copy";
        }
    }
}
#endif
