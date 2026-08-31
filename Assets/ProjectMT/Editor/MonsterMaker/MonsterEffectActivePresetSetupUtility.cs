using System;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public static class MonsterEffectActivePresetSetupUtility // 조립소 검증용 역할별 완성 예시
    {
        [MenuItem("JC Tool/Monster/유틸리티/효과형 예시 프리셋 3종 생성")]
        public static void CreateProductionExamples()
        {
            var profiles = new[]
            {
                BuildBattleHymn(),
                BuildGuardianSanctuary(),
                BuildAbyssalCurse()
            };
            MonsterEffectActiveProfile first = null;
            foreach (var working in profiles)
            {
                var existing = FindById(working.ProfileId);
                MonsterEffectActiveProfile saved;
                if (existing == null)
                {
                    if (!MonsterEffectActiveAuthoringService.TryCreate(
                            working,
                            out saved,
                            out _,
                            out var createError))
                    {
                        throw new InvalidOperationException(createError);
                    }
                }
                else
                {
                    if (!MonsterEffectActiveAuthoringService.TryUpdate(working, existing, out var updateError))
                    {
                        throw new InvalidOperationException(updateError);
                    }
                    saved = existing;
                }
                first ??= saved;
                UnityEngine.Object.DestroyImmediate(working);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = first;
            if (first != null) EditorGUIUtility.PingObject(first);
            Debug.Log("[Monster Effect Active] 지원·수호·디버프 예시 프리셋 3종 생성/갱신 완료");
        }

        private static MonsterEffectActiveProfile BuildBattleHymn()
        {
            var group = Group(
                "group_01",
                "전군 고양",
                0f,
                MonsterSkillTargetType.AllAllies,
                true,
                0f,
                32,
                new[]
                {
                    Effect("attack_up", MonsterSkillEffectType.AttackBuff,
                        MonsterSkillValueSource.Flat, 0.25f, 6f),
                    Effect("haste", MonsterSkillEffectType.AttackSpeedBuff,
                        MonsterSkillValueSource.Flat, 0.2f, 6f),
                    Effect("energy_restore", MonsterSkillEffectType.EnergyGain,
                        MonsterSkillValueSource.TargetEnergyCapacityRatio, 0.15f)
                },
                new[]
                {
                    Slot("cast_start", "시전자 고양", MonsterActivePresentationEvent.MotionStart,
                        MonsterActivePresentationAnchor.CasterRoot),
                    Slot("apply", "아군 전체 적용", MonsterActivePresentationEvent.AreaResolved,
                        MonsterActivePresentationAnchor.AreaCenter),
                    Slot("target_loop", "강화 지속", MonsterActivePresentationEvent.AreaResolved,
                        MonsterActivePresentationAnchor.TargetRoot, true, 6f)
                });
            return Profile(
                "battle_hymn",
                "전장의 찬가",
                "모든 아군의 공격력과 공격속도를 높이고 최대 기력의 15%를 회복합니다.",
                MonsterEffectActiveRole.Support,
                group);
        }

        private static MonsterEffectActiveProfile BuildGuardianSanctuary()
        {
            var protect = Group(
                "group_01",
                "수호 장막",
                0f,
                MonsterSkillTargetType.AllAllies,
                true,
                0f,
                32,
                new[]
                {
                    Effect("party_shield", MonsterSkillEffectType.Shield,
                        MonsterSkillValueSource.AttackPowerRatio, 2f, 5f),
                    Effect("defense_up", MonsterSkillEffectType.DefenseBuff,
                        MonsterSkillValueSource.Flat, 0.25f, 5f)
                },
                new[]
                {
                    Slot("cast_start", "수호 발동", MonsterActivePresentationEvent.MotionStart,
                        MonsterActivePresentationAnchor.CasterRoot),
                    Slot("barrier_apply", "보호막 적용", MonsterActivePresentationEvent.AreaResolved,
                        MonsterActivePresentationAnchor.AreaCenter),
                    Slot("barrier_loop", "보호막 지속", MonsterActivePresentationEvent.AreaResolved,
                        MonsterActivePresentationAnchor.TargetRoot, true, 5f)
                });
            var taunt = Group(
                "group_02",
                "수호자 도발",
                0.12f,
                MonsterSkillTargetType.TargetAreaEnemies,
                true,
                4.5f,
                16,
                new[]
                {
                    Effect("guardian_taunt", MonsterSkillEffectType.Taunt,
                        MonsterSkillValueSource.Flat, 0f, 3f)
                },
                new[]
                {
                    Slot("taunt_apply", "도발 적용", MonsterActivePresentationEvent.AreaResolved,
                        MonsterActivePresentationAnchor.CasterRoot)
                });
            return Profile(
                "guardian_sanctuary",
                "수호 성역",
                "모든 아군에게 보호막과 방어력을 제공한 뒤 주변 적을 3초 동안 도발합니다.",
                MonsterEffectActiveRole.Guard,
                protect,
                taunt);
        }

        private static MonsterEffectActiveProfile BuildAbyssalCurse()
        {
            var group = Group(
                "group_01",
                "심연 낙인",
                0f,
                MonsterSkillTargetType.TargetAreaEnemies,
                true,
                5f,
                12,
                new[]
                {
                    Effect("attack_down", MonsterSkillEffectType.AttackDebuff,
                        MonsterSkillValueSource.Flat, 0.2f, 6f),
                    Effect("defense_down", MonsterSkillEffectType.DefenseDebuff,
                        MonsterSkillValueSource.Flat, 0.2f, 6f),
                    Effect("exposure", MonsterSkillEffectType.Mark,
                        MonsterSkillValueSource.Flat, 0.15f, 6f),
                    Effect("energy_drain", MonsterSkillEffectType.EnergyDrain,
                        MonsterSkillValueSource.TargetEnergyCapacityRatio, 0.1f)
                },
                new[]
                {
                    Slot("curse_apply", "저주 적용", MonsterActivePresentationEvent.AreaResolved,
                        MonsterActivePresentationAnchor.AreaCenter),
                    Slot("curse_loop", "낙인 지속", MonsterActivePresentationEvent.AreaResolved,
                        MonsterActivePresentationAnchor.TargetRoot, true, 6f)
                });
            return Profile(
                "abyssal_curse",
                "심연의 저주",
                "대상 주변 적의 공격력·방어력을 낮추고 받는 피해를 늘리며 최대 기력의 10%를 감소시킵니다.",
                MonsterEffectActiveRole.Debuff,
                group);
        }

        private static MonsterEffectActiveProfile Profile(
            string id,
            string title,
            string description,
            MonsterEffectActiveRole role,
            params MonsterEffectActiveGroup[] groups)
        {
            var profile = ScriptableObject.CreateInstance<MonsterEffectActiveProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            profile.EditorConfigure(id, title, description, role, groups);
            if (!profile.TryValidate(out var error)) throw new InvalidOperationException(error);
            return profile;
        }

        private static MonsterEffectActiveGroup Group(
            string id,
            string title,
            float delay,
            MonsterSkillTargetType target,
            bool includeCaster,
            float radius,
            int maxTargets,
            MonsterSkillEffect[] effects,
            MonsterActivePresentationSlot[] slots)
        {
            var group = new MonsterEffectActiveGroup();
            group.EditorConfigure(id, title, delay, target, includeCaster, radius, maxTargets, effects, slots);
            return group;
        }

        private static MonsterSkillEffect Effect(
            string id,
            MonsterSkillEffectType type,
            MonsterSkillValueSource source,
            float amount,
            float duration = 0f,
            float interval = 0f)
        {
            var effect = new MonsterSkillEffect();
            effect.EditorConfigure(
                id,
                type,
                source,
                amount,
                duration,
                0f,
                1,
                MonsterSkillStackPolicy.StrongestWins,
                1,
                0f,
                MonsterSkillMagnitudeMode.Fixed,
                amount,
                interval);
            return effect;
        }

        private static MonsterActivePresentationSlot Slot(
            string id,
            string title,
            MonsterActivePresentationEvent timing,
            MonsterActivePresentationAnchor anchor,
            bool useDuration = false,
            float duration = 1f)
        {
            var slot = new MonsterActivePresentationSlot();
            slot.EditorConfigure(
                id,
                title,
                timing,
                anchor,
                useDuration ? "효과 지속시간 동안 재생하는 Loop VFX/SFX 공간" : "효과 적용 순간의 VFX/SFX 공간",
                useDuration,
                duration,
                useDuration
                    ? MonsterActivePresentationMultiplicity.ContinuousUntilEnd
                    : MonsterActivePresentationMultiplicity.OncePerStep,
                useDuration
                    ? MonsterActivePresentationAttachment.FollowAnchor
                    : MonsterActivePresentationAttachment.World,
                useDuration
                    ? MonsterActivePresentationEndPolicy.Timed
                    : MonsterActivePresentationEndPolicy.ParticleDuration);
            return slot;
        }

        private static MonsterEffectActiveProfile FindById(string profileId)
        {
            if (!AssetDatabase.IsValidFolder(MonsterEffectActiveAuthoringService.ProfileRoot)) return null;
            return AssetDatabase.FindAssets(
                    "t:MonsterEffectActiveProfile",
                    new[] { MonsterEffectActiveAuthoringService.ProfileRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterEffectActiveProfile>)
                .FirstOrDefault(profile => profile != null && string.Equals(
                    profile.ProfileId,
                    profileId,
                    StringComparison.OrdinalIgnoreCase));
        }
    }
}
