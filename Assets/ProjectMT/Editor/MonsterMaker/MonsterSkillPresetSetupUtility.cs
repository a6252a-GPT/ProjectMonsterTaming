using System;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public static class MonsterSkillPresetSetupUtility
    {
        private const string Root = "Assets/ProjectMT/02_Shared/Unit/Data/Skills";
        private const string PassiveRoot = Root + "/Passive";
        private const string ActiveRoot = Root + "/Active";

        private static readonly HashSet<string> P0AuthoringSkillIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "passive_nth_hit_power",
            "passive_nth_hit_splash",
            "passive_low_hp_hunter",
            "passive_ranged_hunter",
            "passive_entry_shield",
            "passive_heavy_body",
            "passive_last_stand",
            "passive_nth_hit_heal",
            "passive_team_haste_rhythm",
            "passive_armor_shred",
            "active_cone_strike",
            "active_spin_attack",
            "active_execute_strike",
            "active_multihit_single",
            "active_piercing_projectile",
            "active_explosive_projectile",
            "active_rear_snipe",
            "active_taunt_shield",
            "active_group_shield",
            "active_defense_stance",
            "active_single_heal",
            "active_life_wave",
            "active_team_haste",
            "active_courage_song",
            "active_attack_mark"
        };

        [MenuItem("JC Tool/Monster/Rebuild Generic Skill Presets")]
        public static void RebuildFromMenu()
        {
            var catalog = CreateOrUpdateDefaults();
            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);
            Debug.Log(
                $"Monster Skill Preset 갱신 완료: " +
                $"Passive={catalog.PassiveSkills.Count} (P0={CountEnabled(catalog.PassiveSkills)}), " +
                $"Active={catalog.ActiveSkills.Count} (P0={CountEnabled(catalog.ActiveSkills)})");
        }

        public static MonsterSkillCatalog CreateOrUpdateDefaults()
        {
            EnsureFolder("Assets/ProjectMT/02_Shared/Unit", "Data");
            EnsureFolder("Assets/ProjectMT/02_Shared/Unit/Data", "Skills");
            EnsureFolder(Root, "Passive");
            EnsureFolder(Root, "Active");

            var passives = new List<MonsterPassiveSkill>
            {
                Passive(
                    "passive_nth_hit_power", "박자 강화", "N번째 기본 공격을 강화합니다.",
                    Recipe(MonsterSkillTriggerType.BasicAttackNthHit, 3, MonsterSkillTargetType.CurrentTarget,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("nth_hit_damage", MonsterSkillEffectType.Damage, 1.35f))),
                Passive(
                    "passive_nth_hit_splash", "폭발 타격", "N번째 기본 공격이 대상 주변까지 타격합니다.",
                    Recipe(MonsterSkillTriggerType.BasicAttackNthHit, 4, MonsterSkillTargetType.TargetAreaEnemies,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.TargetCircle,
                        Effect("nth_hit_splash", MonsterSkillEffectType.Damage, 0.65f, radius: 1.2f, maxTargets: 3))),
                Passive(
                    "passive_same_target_haste", "가속 연타", "같은 대상을 연속 적중하면 공격 속도가 증가합니다.",
                    Recipe(MonsterSkillTriggerType.BasicAttackHit, 1, MonsterSkillTargetType.Self,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("same_target_haste", MonsterSkillEffectType.AttackSpeedBuff, 0.05f, duration: 2f,
                            policy: MonsterSkillStackPolicy.Stack),
                        Condition(MonsterSkillConditionType.SameTargetContinuous))),
                Passive(
                    "passive_first_hit", "선제타", "새 표적에 가하는 첫 공격을 강화합니다.",
                    Recipe(MonsterSkillTriggerType.TargetChanged, 1, MonsterSkillTargetType.CurrentTarget,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("first_hit_damage", MonsterSkillEffectType.Damage, 1.3f))),
                Passive(
                    "passive_low_hp_hunter", "피 냄새", "체력이 낮은 적에게 추가 피해를 줍니다.",
                    Recipe(MonsterSkillTriggerType.BasicAttackHit, 1, MonsterSkillTargetType.CurrentTarget,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("low_hp_bonus", MonsterSkillEffectType.Damage, 0.3f),
                        Condition(MonsterSkillConditionType.TargetHealthBelow, 0.4f))),
                Passive(
                    "passive_ranged_hunter", "후열 사냥", "원거리 적에게 추가 피해와 표식을 부여합니다.",
                    Recipe(MonsterSkillTriggerType.BasicAttackHit, 1, MonsterSkillTargetType.CurrentTarget,
                        MonsterSkillDeliveryType.Mark, MonsterSkillShapeType.Single,
                        new[]
                        {
                            Effect("ranged_hunter_damage", MonsterSkillEffectType.Damage, 0.2f),
                            Effect("ranged_hunter_mark", MonsterSkillEffectType.Mark, 1f, duration: 5f)
                        },
                        Condition(MonsterSkillConditionType.TargetIsRanged))),
                Passive(
                    "passive_kill_energy", "포식 충전", "적을 처치하면 에너지를 회복합니다.",
                    Recipe(MonsterSkillTriggerType.Kill, 1, MonsterSkillTargetType.Self,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("kill_energy", MonsterSkillEffectType.EnergyGain, 150f, MonsterSkillValueSource.Flat))),
                Passive(
                    "passive_kill_heal", "흡수 본능", "적을 처치하면 자신의 체력을 회복합니다.",
                    Recipe(MonsterSkillTriggerType.Kill, 1, MonsterSkillTargetType.Self,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("kill_heal", MonsterSkillEffectType.Heal, 0.08f, MonsterSkillValueSource.MaxHealthRatio))),
                Passive(
                    "passive_entry_shield", "합류 보호막", "전투 합류 시 최대 체력 비례 보호막을 얻습니다.",
                    Recipe(MonsterSkillTriggerType.CombatJoin, 1, MonsterSkillTargetType.Self,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("entry_shield", MonsterSkillEffectType.Shield, 0.18f,
                            MonsterSkillValueSource.MaxHealthRatio, 6f))),
                Passive(
                    "passive_crisis_defense", "위기 방어", "체력이 낮아지면 잠시 방어력이 증가합니다.",
                    Recipe(MonsterSkillTriggerType.HealthThresholdEntered, 1, MonsterSkillTargetType.Self,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("crisis_defense", MonsterSkillEffectType.DefenseBuff, 0.25f, duration: 4f),
                        Condition(MonsterSkillConditionType.SelfHealthBelow, 0.4f))),
                Passive(
                    "passive_hit_counter", "피격 반격", "일정 횟수 피해를 받으면 공격자에게 반격합니다.",
                    Recipe(MonsterSkillTriggerType.DamagedNthTime, 4, MonsterSkillTargetType.Attacker,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("hit_counter_damage", MonsterSkillEffectType.Damage, 0.8f))),
                Passive(
                    "passive_shield_break_pulse", "보호막 파동", "보호막이 피해로 파괴되면 주변 적을 경직시킵니다.",
                    Recipe(MonsterSkillTriggerType.ShieldBroken, 1, MonsterSkillTargetType.TargetAreaEnemies,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.SelfCircle,
                        Effect("shield_break_stagger", MonsterSkillEffectType.Stagger, 0.18f,
                            MonsterSkillValueSource.Flat, 0.18f, 1.4f, 3))),
                Passive(
                    "passive_heavy_body", "묵직한 몸", "전투 중 넉백 저항을 얻습니다.",
                    Recipe(MonsterSkillTriggerType.CombatStart, 1, MonsterSkillTargetType.Self,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("heavy_body_resist", MonsterSkillEffectType.KnockbackResistance, 0.35f))),
                Passive(
                    "passive_nth_hit_heal", "치유 탄환", "N번째 기본 공격 적중 시 최저 체력 아군을 회복합니다.",
                    Recipe(MonsterSkillTriggerType.BasicAttackNthHit, 4, MonsterSkillTargetType.LowestHealthAlly,
                        MonsterSkillDeliveryType.Projectile, MonsterSkillShapeType.Single,
                        Effect("nth_hit_heal", MonsterSkillEffectType.Heal, 0.65f))),
                Passive(
                    "passive_team_haste_rhythm", "박자 공유", "N번째 기본 공격마다 아군 전체 공격 속도를 높입니다.",
                    Recipe(MonsterSkillTriggerType.BasicAttackNthHit, 4, MonsterSkillTargetType.AllAllies,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.SelfCircle,
                        Effect("team_haste_rhythm", MonsterSkillEffectType.AttackSpeedBuff, 0.08f, duration: 2f,
                            radius: 100f, maxTargets: 5))),
                Passive(
                    "passive_ally_crisis_shield", "위기 지원", "아군이 처음 위기에 빠지면 보호막을 보냅니다.",
                    Recipe(MonsterSkillTriggerType.AllyHealthThresholdEntered, 1,
                        MonsterSkillTargetType.LowestHealthAlly, MonsterSkillDeliveryType.Projectile,
                        MonsterSkillShapeType.Single,
                        Effect("ally_crisis_shield", MonsterSkillEffectType.Shield, 0.1f,
                            MonsterSkillValueSource.TargetMaxHealthRatio, 4f),
                        Condition(MonsterSkillConditionType.OncePerBattle))),
                Passive(
                    "passive_long_range_aim", "장거리 조준", "먼 적을 공격할 때 추가 피해를 줍니다.",
                    Recipe(MonsterSkillTriggerType.BasicAttackHit, 1, MonsterSkillTargetType.CurrentTarget,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("long_range_bonus", MonsterSkillEffectType.Damage, 0.22f),
                        Condition(MonsterSkillConditionType.DistanceAtLeast, 4f))),
                Passive(
                    "passive_close_pressure", "근접 압박", "가까운 적에게 추가 피해와 짧은 경직을 줍니다.",
                    Recipe(MonsterSkillTriggerType.BasicAttackHit, 1, MonsterSkillTargetType.CurrentTarget,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        new[]
                        {
                            Effect("close_pressure_damage", MonsterSkillEffectType.Damage, 0.18f),
                            Effect("close_pressure_stagger", MonsterSkillEffectType.Stagger, 0.08f,
                                MonsterSkillValueSource.Flat, duration: 0.08f)
                        },
                        Condition(MonsterSkillConditionType.DistanceAtMost, 1.5f))),
                Passive(
                    "passive_weakpoint_stack", "약점 누적", "같은 대상을 계속 공격하면 방어 약화를 누적합니다.",
                    Recipe(MonsterSkillTriggerType.BasicAttackHit, 1, MonsterSkillTargetType.CurrentTarget,
                        MonsterSkillDeliveryType.Mark, MonsterSkillShapeType.Single,
                        Effect("weakpoint_defense_down", MonsterSkillEffectType.DefenseDebuff, 0.04f,
                            duration: 5f, policy: MonsterSkillStackPolicy.Stack),
                        Condition(MonsterSkillConditionType.SameTargetContinuous))),
                Passive(
                    "passive_elite_hunter", "거대 사냥꾼", "정예·보스에게 추가 피해를 줍니다.",
                    Recipe(MonsterSkillTriggerType.BasicAttackHit, 1, MonsterSkillTargetType.CurrentTarget,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("elite_hunter_damage", MonsterSkillEffectType.Damage, 0.25f),
                        Condition(MonsterSkillConditionType.TargetIsBoss))),
                Passive(
                    "passive_outnumbered_guard", "다수 상대", "주변 적이 많을 때 방어력이 증가합니다.",
                    Recipe(MonsterSkillTriggerType.Damaged, 1, MonsterSkillTargetType.Self,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("outnumbered_defense", MonsterSkillEffectType.DefenseBuff, 0.15f, duration: 2f),
                        Condition(MonsterSkillConditionType.NearbyEnemyCountAtLeast, count: 3))),
                Passive(
                    "passive_last_stand", "최후의 버팀", "전투당 한 번 위기 체력에서 보호막을 얻습니다.",
                    Recipe(MonsterSkillTriggerType.HealthThresholdEntered, 1, MonsterSkillTargetType.Self,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("last_stand_shield", MonsterSkillEffectType.Shield, 0.2f,
                            MonsterSkillValueSource.MaxHealthRatio, 4f),
                        Condition(MonsterSkillConditionType.SelfHealthBelow, 0.2f),
                        Condition(MonsterSkillConditionType.OncePerBattle))),
                Passive(
                    "passive_thorn_shell", "가시 껍질", "피해를 받으면 일부를 공격자에게 되돌립니다.",
                    TimedRecipe(MonsterSkillTriggerType.Damaged, 1, 0.5f, MonsterSkillTargetType.Attacker,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("thorn_reflect", MonsterSkillEffectType.DamageReflect, 0.15f,
                            MonsterSkillValueSource.ReceivedDamageRatio))),
                Passive(
                    "passive_breathing_room", "숨 고르기", "일정 시간 피해받지 않으면 체력을 회복합니다.",
                    TimedRecipe(MonsterSkillTriggerType.NoDamageForDuration, 1, 4f, MonsterSkillTargetType.Self,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("breathing_heal", MonsterSkillEffectType.Heal, 0.06f,
                            MonsterSkillValueSource.MaxHealthRatio))),
                Passive(
                    "passive_courage_aura", "용기 오라", "전투 시작 시 아군 전체의 공격력을 높입니다.",
                    Recipe(MonsterSkillTriggerType.CombatStart, 1, MonsterSkillTargetType.AllAllies,
                        MonsterSkillDeliveryType.Aura, MonsterSkillShapeType.SelfCircle,
                        Effect("courage_aura", MonsterSkillEffectType.AttackBuff, 0.06f,
                            duration: 999f, radius: 100f, maxTargets: 5))),
                Passive(
                    "passive_guard_aura", "수호 오라", "전투 시작 시 아군 전체의 방어력을 높입니다.",
                    Recipe(MonsterSkillTriggerType.CombatStart, 1, MonsterSkillTargetType.AllAllies,
                        MonsterSkillDeliveryType.Aura, MonsterSkillShapeType.SelfCircle,
                        Effect("guard_aura", MonsterSkillEffectType.DefenseBuff, 0.06f,
                            duration: 999f, radius: 100f, maxTargets: 5))),
                Passive(
                    "passive_cleanse_leaf", "정화의 잎", "아군이 위기에 빠지면 약화 효과 하나를 제거합니다.",
                    TimedRecipe(MonsterSkillTriggerType.AllyHealthThresholdEntered, 1, 8f,
                        MonsterSkillTargetType.LowestHealthAlly, MonsterSkillDeliveryType.Projectile,
                        MonsterSkillShapeType.Single,
                        Effect("cleanse_leaf", MonsterSkillEffectType.Cleanse, 1f, MonsterSkillValueSource.Flat),
                        Condition(MonsterSkillConditionType.TargetHealthBelow, 0.35f))),
                Passive(
                    "passive_skill_response", "스킬 호응", "아군이 액티브를 쓰면 자신이 잠시 강화됩니다.",
                    TimedRecipe(MonsterSkillTriggerType.AllyActiveUsed, 1, 2f, MonsterSkillTargetType.Self,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("skill_response_attack", MonsterSkillEffectType.AttackBuff, 0.12f, duration: 3f))),
                Passive(
                    "passive_formation_bond", "진형 결속", "가까운 아군이 둘 이상이면 방어력이 증가합니다.",
                    TimedRecipe(MonsterSkillTriggerType.Interval, 1, 1f, MonsterSkillTargetType.Self,
                        MonsterSkillDeliveryType.Aura, MonsterSkillShapeType.SelfCircle,
                        Effect("formation_defense", MonsterSkillEffectType.DefenseBuff, 0.12f, duration: 1.2f),
                        Condition(MonsterSkillConditionType.NearbyAllyCountAtLeast, count: 2))),
                Passive(
                    "passive_armor_shred", "갑옷 파쇄", "N번째 기본 공격이 대상의 방어력을 낮춥니다.",
                    Recipe(MonsterSkillTriggerType.BasicAttackNthHit, 4, MonsterSkillTargetType.CurrentTarget,
                        MonsterSkillDeliveryType.Mark, MonsterSkillShapeType.Single,
                        Effect("armor_shred", MonsterSkillEffectType.DefenseDebuff, 0.1f, duration: 4f))),
                Passive(
                    "passive_first_wave", "첫 파도", "웨이브 시작 직후 잠시 공격력이 증가합니다.",
                    Recipe(MonsterSkillTriggerType.WaveStart, 1, MonsterSkillTargetType.Self,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("first_wave_attack", MonsterSkillEffectType.AttackBuff, 0.15f, duration: 5f),
                        Condition(MonsterSkillConditionType.OncePerWave))),
                Passive(
                    "passive_death_echo", "생명의 잔향", "사망할 때 최저 체력 아군을 회복합니다.",
                    Recipe(MonsterSkillTriggerType.Death, 1, MonsterSkillTargetType.LowestHealthAlly,
                        MonsterSkillDeliveryType.Projectile, MonsterSkillShapeType.Single,
                        Effect("death_echo_heal", MonsterSkillEffectType.Heal, 0.9f))),
                Passive(
                    "passive_death_burst", "죽음의 폭발", "사망할 때 주변 적에게 피해를 줍니다.",
                    Recipe(MonsterSkillTriggerType.Death, 1, MonsterSkillTargetType.TargetAreaEnemies,
                        MonsterSkillDeliveryType.Radial, MonsterSkillShapeType.SelfCircle,
                        Effect("death_burst_damage", MonsterSkillEffectType.Damage, 1.2f,
                            radius: 1.5f, maxTargets: 3)))
            };

            var actives = new List<MonsterActiveSkill>
            {
                Active(
                    "active_dash_line", "직선 돌진", "대상에게 돌진하며 경로의 적을 타격합니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.CurrentTarget,
                        MonsterSkillDeliveryType.Dash, MonsterSkillShapeType.Capsule,
                        Effect("dash_line_damage", MonsterSkillEffectType.Damage, 1.8f, radius: 0.8f, maxTargets: 2))),
                Active(
                    "active_leap_impact", "도약 강습", "적 밀집 지역으로 도약해 범위 피해와 경직을 줍니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.DensestEnemyPosition,
                        MonsterSkillDeliveryType.Leap, MonsterSkillShapeType.TargetCircle,
                        new[]
                        {
                            Effect("leap_damage", MonsterSkillEffectType.Damage, 1.6f, radius: 1.5f, maxTargets: 3),
                            Effect("leap_stagger", MonsterSkillEffectType.Stagger, 0.2f,
                                MonsterSkillValueSource.Flat, 0.2f, 1.5f, 3)
                        })),
                Active(
                    "active_cone_strike", "전방 강타", "전방 부채꼴의 적을 강하게 타격합니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.CurrentTarget,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.ForwardCone,
                        Effect("cone_damage", MonsterSkillEffectType.Damage, 1.7f, radius: 1.8f, maxTargets: 3))),
                Active(
                    "active_spin_attack", "회전 공격", "자신 주변 적을 한 번씩 타격합니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.TargetAreaEnemies,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.SelfCircle,
                        Effect("spin_damage", MonsterSkillEffectType.Damage, 1.5f, radius: 1.6f, maxTargets: 4))),
                Active(
                    "active_execute_strike", "처형 일격", "최저 체력 적에게 잃은 체력 비례 추가 피해를 줍니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.LowestHealthEnemy,
                        MonsterSkillDeliveryType.Dash, MonsterSkillShapeType.Single,
                        Effect("execute_damage", MonsterSkillEffectType.Damage, 2.2f,
                            MonsterSkillValueSource.TargetMissingHealthRatio))),
                Active(
                    "active_multihit_single", "집중 연속타", "현재 대상 하나를 빠르게 여러 번 타격합니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.CurrentTarget,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        Effect("multihit_damage", MonsterSkillEffectType.Damage, 0.7f, repeats: 3))),
                Active(
                    "active_piercing_projectile", "관통 탄환", "후열까지 관통하는 직선 투사체를 발사합니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.RangedEnemyFirst,
                        MonsterSkillDeliveryType.PiercingProjectile, MonsterSkillShapeType.Line,
                        Effect("piercing_damage", MonsterSkillEffectType.Damage, 1.6f, radius: 0.7f, maxTargets: 3))),
                Active(
                    "active_explosive_projectile", "폭발 구체", "착탄 지점에 범위 피해를 주는 투사체를 발사합니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.DensestEnemyPosition,
                        MonsterSkillDeliveryType.Projectile, MonsterSkillShapeType.TargetCircle,
                        Effect("explosive_damage", MonsterSkillEffectType.Damage, 1.55f, radius: 1.5f, maxTargets: 4))),
                Active(
                    "active_traveling_wave", "이동 파도", "전방으로 이동하는 파도가 적을 관통하고 감속시킵니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.RangedEnemyFirst,
                        MonsterSkillDeliveryType.TravelingWave, MonsterSkillShapeType.Capsule,
                        new[]
                        {
                            Effect("wave_damage", MonsterSkillEffectType.Damage, 1.5f, radius: 0.8f, maxTargets: 3),
                            Effect("wave_slow", MonsterSkillEffectType.Slow, 0.2f, duration: 2.5f,
                                radius: 0.8f, maxTargets: 3)
                        })),
                Active(
                    "active_taunt_shield", "도발 보호막", "주변 적을 유도하고 자신에게 보호막을 부여합니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.Self,
                        MonsterSkillDeliveryType.Aura, MonsterSkillShapeType.SelfCircle,
                        new[]
                        {
                            Effect("taunt", MonsterSkillEffectType.Taunt, 1f,
                                MonsterSkillValueSource.Flat, 2f, 2f, 4),
                            Effect("taunt_shield", MonsterSkillEffectType.Shield, 0.25f,
                                MonsterSkillValueSource.MaxHealthRatio, 5f, 2f, 1)
                        })),
                Active(
                    "active_single_heal", "긴급 회복", "최저 체력 아군에게 강한 회복 투사체를 보냅니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.LowestHealthAlly,
                        MonsterSkillDeliveryType.Projectile, MonsterSkillShapeType.Single,
                        Effect("single_heal", MonsterSkillEffectType.Heal, 2.2f))),
                Active(
                    "active_team_haste", "신속의 노래", "아군 전체의 공격 속도와 이동 속도를 높입니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.AllAllies,
                        MonsterSkillDeliveryType.Aura, MonsterSkillShapeType.SelfCircle,
                        new[]
                        {
                            Effect("team_attack_haste", MonsterSkillEffectType.AttackSpeedBuff, 0.18f, duration: 4f,
                                radius: 100f, maxTargets: 5),
                            Effect("team_move_haste", MonsterSkillEffectType.MoveSpeedBuff, 0.12f, duration: 4f,
                                radius: 100f, maxTargets: 5)
                        })),
                Active(
                    "active_chain_shot", "도탄 사격", "투사체가 적 사이를 연쇄하며 타격합니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.CurrentTarget,
                        MonsterSkillDeliveryType.Projectile, MonsterSkillShapeType.Chain,
                        Effect("chain_shot_damage", MonsterSkillEffectType.Damage, 0.65f,
                            radius: 4f, maxTargets: 3, repeats: 3))),
                Active(
                    "active_spread_barrage", "부채꼴 연사", "전방 여러 적에게 넓게 투사체를 연사합니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.CurrentTarget,
                        MonsterSkillDeliveryType.Projectile, MonsterSkillShapeType.ForwardCone,
                        Effect("spread_barrage_damage", MonsterSkillEffectType.Damage, 0.35f,
                            radius: 2.2f, maxTargets: 4, repeats: 3))),
                Active(
                    "active_rear_snipe", "후열 저격", "원거리 적을 우선해 강한 단일 사격을 가합니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.RangedEnemyFirst,
                        MonsterSkillDeliveryType.Projectile, MonsterSkillShapeType.Single,
                        Effect("rear_snipe_damage", MonsterSkillEffectType.Damage, 2.35f))),
                Active(
                    "active_delayed_mark", "지연 폭파 표식", "표식을 남긴 뒤 잠시 후 폭발시킵니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.HighestAttackEnemy,
                        MonsterSkillDeliveryType.Mark, MonsterSkillShapeType.TargetCircle,
                        new[]
                        {
                            Effect("delayed_mark", MonsterSkillEffectType.Mark, 1f,
                                MonsterSkillValueSource.Flat, duration: 2f),
                            Effect("delayed_mark_burst", MonsterSkillEffectType.Damage, 1.8f,
                                radius: 1.2f, maxTargets: 3, delay: 2f)
                        })),
                Active(
                    "active_sky_rain", "하늘 낙하", "밀집 지역에 여러 차례 투사체를 떨어뜨립니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.DensestEnemyPosition,
                        MonsterSkillDeliveryType.GroundDrop, MonsterSkillShapeType.TargetCircle,
                        Effect("sky_rain_damage", MonsterSkillEffectType.Damage, 0.34f,
                            radius: 1.7f, maxTargets: 4, repeats: 5))),
                Active(
                    "active_shockwave", "충격파", "주변 적에게 피해를 주고 짧게 밀어냅니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.TargetAreaEnemies,
                        MonsterSkillDeliveryType.Radial, MonsterSkillShapeType.SelfCircle,
                        new[]
                        {
                            Effect("shockwave_damage", MonsterSkillEffectType.Damage, 1.1f,
                                radius: 1.8f, maxTargets: 4),
                            Effect("shockwave_knockback", MonsterSkillEffectType.Knockback, 0.35f,
                                MonsterSkillValueSource.Flat, radius: 1.8f, maxTargets: 4)
                        })),
                Active(
                    "active_cold_zone", "한기 장판", "지속 피해와 감속을 주는 지대를 만듭니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.DensestEnemyPosition,
                        MonsterSkillDeliveryType.Zone, MonsterSkillShapeType.Zone,
                        new[]
                        {
                            Effect("cold_zone_damage", MonsterSkillEffectType.Damage, 0.3f,
                                duration: 3f, radius: 1.7f, maxTargets: 4, repeats: 3),
                            Effect("cold_zone_slow", MonsterSkillEffectType.Slow, 0.25f,
                                duration: 3f, radius: 1.7f, maxTargets: 4)
                        })),
                Active(
                    "active_life_wave", "생명 파동", "아군 전체를 즉시 회복합니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.AllAllies,
                        MonsterSkillDeliveryType.Aura, MonsterSkillShapeType.SelfCircle,
                        Effect("life_wave_heal", MonsterSkillEffectType.Heal, 0.8f,
                            radius: 100f, maxTargets: 5))),
                Active(
                    "active_group_shield", "수호막 전개", "아군 전체에게 보호막을 부여합니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.AllAllies,
                        MonsterSkillDeliveryType.Aura, MonsterSkillShapeType.SelfCircle,
                        Effect("group_shield", MonsterSkillEffectType.Shield, 0.12f,
                            MonsterSkillValueSource.TargetMaxHealthRatio, 4f, 100f, 5))),
                Active(
                    "active_courage_song", "용기의 노래", "아군 전체의 공격력을 잠시 높입니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.AllAllies,
                        MonsterSkillDeliveryType.Aura, MonsterSkillShapeType.SelfCircle,
                        Effect("courage_song", MonsterSkillEffectType.AttackBuff, 0.18f,
                            duration: 4f, radius: 100f, maxTargets: 5))),
                Active(
                    "active_energy_wave", "에너지 물결", "아군 전체의 에너지를 회복합니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.AllAllies,
                        MonsterSkillDeliveryType.Aura, MonsterSkillShapeType.SelfCircle,
                        Effect("energy_wave", MonsterSkillEffectType.EnergyGain, 120f,
                            MonsterSkillValueSource.Flat, radius: 100f, maxTargets: 5))),
                Active(
                    "active_attack_mark", "공격 표식", "위험한 적을 표식해 아군의 집중 공격을 돕습니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.HighestAttackEnemy,
                        MonsterSkillDeliveryType.Mark, MonsterSkillShapeType.Single,
                        new[]
                        {
                            Effect("attack_mark", MonsterSkillEffectType.Mark, 1f,
                                MonsterSkillValueSource.Flat, duration: 5f),
                            Effect("attack_mark_defense_down", MonsterSkillEffectType.DefenseDebuff, 0.15f,
                                duration: 5f)
                        })),
                Active(
                    "active_defense_stance", "철벽 자세", "자신의 피해 감소와 넉백 저항을 크게 높입니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.Self,
                        MonsterSkillDeliveryType.Instant, MonsterSkillShapeType.Single,
                        new[]
                        {
                            Effect("stance_reduction", MonsterSkillEffectType.DamageReduction, 0.45f, duration: 3f),
                            Effect("stance_resist", MonsterSkillEffectType.KnockbackResistance, 1f, duration: 3f)
                        })),
                Active(
                    "active_radial_barrage", "방사형 포화", "사방으로 투사체를 뿌려 주변 적을 타격합니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.TargetAreaEnemies,
                        MonsterSkillDeliveryType.Radial, MonsterSkillShapeType.SelfCircle,
                        Effect("radial_barrage_damage", MonsterSkillEffectType.Damage, 0.45f,
                            radius: 2f, maxTargets: 5, repeats: 3))),
                Active(
                    "active_returning_blade", "왕복 투사체", "투사체가 전진하고 되돌아오며 두 번 타격합니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.RangedEnemyFirst,
                        MonsterSkillDeliveryType.ReturningProjectile, MonsterSkillShapeType.Line,
                        Effect("returning_blade_damage", MonsterSkillEffectType.Damage, 0.85f,
                            radius: 0.7f, maxTargets: 3, repeats: 2))),
                Active(
                    "active_binding_zone", "속박 지대", "범위 안 적의 이동을 잠시 묶습니다.",
                    Recipe(MonsterSkillTriggerType.EnergyMax, 1, MonsterSkillTargetType.DensestEnemyPosition,
                        MonsterSkillDeliveryType.Zone, MonsterSkillShapeType.Zone,
                        new[]
                        {
                            Effect("binding_zone_damage", MonsterSkillEffectType.Damage, 0.8f,
                                radius: 1.6f, maxTargets: 4),
                            Effect("binding_zone_root", MonsterSkillEffectType.Root, 1f,
                                MonsterSkillValueSource.Flat, 1.2f, 1.6f, 4)
                        }))
            };

            var catalog = GetOrCreate<MonsterSkillCatalog>(MonsterSkillCatalog.DefaultAssetPath);
            catalog.EditorConfigure(passives.ToArray(), actives.ToArray());
            EditorUtility.SetDirty(catalog);
            if (!catalog.TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return catalog;
        }

        private static GenericMonsterPassiveSkill Passive(
            string id,
            string title,
            string description,
            MonsterSkillRecipe recipe)
        {
            var asset = GetOrCreate<GenericMonsterPassiveSkill>($"{PassiveRoot}/MP_{id}.asset");
            asset.EditorConfigure(id, title, description, MonsterSkillPresentationTier.Subtle, recipe);
            asset.EditorSetAuthoringEnabled(P0AuthoringSkillIds.Contains(id));
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static GenericMonsterActiveSkill Active(
            string id,
            string title,
            string description,
            MonsterSkillRecipe recipe)
        {
            var asset = GetOrCreate<GenericMonsterActiveSkill>($"{ActiveRoot}/MS_{id}.asset");
            asset.EditorConfigure(id, title, description, MonsterSkillPresentationTier.Heroic, recipe);
            asset.EditorSetAuthoringEnabled(P0AuthoringSkillIds.Contains(id));
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static int CountEnabled<TSkill>(IReadOnlyList<TSkill> skills)
            where TSkill : MonsterSkillDefinitionBase
        {
            var count = 0;
            for (var index = 0; index < skills.Count; index++)
            {
                if (skills[index] != null && skills[index].AuthoringEnabled)
                {
                    count++;
                }
            }

            return count;
        }

        private static MonsterSkillRecipe Recipe(
            MonsterSkillTriggerType trigger,
            int triggerCount,
            MonsterSkillTargetType target,
            MonsterSkillDeliveryType delivery,
            MonsterSkillShapeType shape,
            MonsterSkillEffect effect,
            params MonsterSkillCondition[] conditions)
        {
            return Recipe(trigger, triggerCount, target, delivery, shape, new[] { effect }, conditions);
        }

        private static MonsterSkillRecipe TimedRecipe(
            MonsterSkillTriggerType trigger,
            int triggerCount,
            float internalCooldown,
            MonsterSkillTargetType target,
            MonsterSkillDeliveryType delivery,
            MonsterSkillShapeType shape,
            MonsterSkillEffect effect,
            params MonsterSkillCondition[] conditions)
        {
            var recipe = new MonsterSkillRecipe();
            recipe.EditorConfigure(
                trigger,
                triggerCount,
                internalCooldown,
                target,
                delivery,
                shape,
                conditions,
                new[] { effect });
            return recipe;
        }

        private static MonsterSkillRecipe Recipe(
            MonsterSkillTriggerType trigger,
            int triggerCount,
            MonsterSkillTargetType target,
            MonsterSkillDeliveryType delivery,
            MonsterSkillShapeType shape,
            MonsterSkillEffect[] effects,
            params MonsterSkillCondition[] conditions)
        {
            var recipe = new MonsterSkillRecipe();
            recipe.EditorConfigure(trigger, triggerCount, 0f, target, delivery, shape, conditions, effects);
            return recipe;
        }

        private static MonsterSkillCondition Condition(
            MonsterSkillConditionType type,
            float value = 0f,
            int count = 1,
            string referenceId = null)
        {
            var condition = new MonsterSkillCondition();
            condition.EditorConfigure(type, value, count, referenceId);
            return condition;
        }

        private static MonsterSkillEffect Effect(
            string id,
            MonsterSkillEffectType type,
            float magnitude,
            MonsterSkillValueSource source = MonsterSkillValueSource.AttackPowerRatio,
            float duration = 0f,
            float radius = 0f,
            int maxTargets = 1,
            MonsterSkillStackPolicy policy = MonsterSkillStackPolicy.RefreshDuration,
            int repeats = 1,
            float delay = 0f)
        {
            var effect = new MonsterSkillEffect();
            effect.EditorConfigure(id, type, source, magnitude, duration, radius, maxTargets, policy, repeats, delay);
            return effect;
        }

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
