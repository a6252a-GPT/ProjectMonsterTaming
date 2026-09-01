using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.EditorTools.MonsterMaker;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    public sealed partial class MonsterWorkshopV2Window
    {
        partial void BuildAttackAssembler()
        {
            attackSerialized.Update();
            var info = Section("1. 프리셋 정보", "스킬 이름과 최대 기력은 몬스터별 Maker V2에서 정합니다.");
            info.Add(BoundProperty(attackSerialized, attackSerialized.FindProperty("profileId"), "프리셋 ID"));
            info.Add(BoundProperty(attackSerialized, attackSerialized.FindProperty("displayName"), "표시 이름"));
            info.Add(BoundProperty(attackSerialized, attackSerialized.FindProperty("description"), "기획 메모"));
            assemblerScroll.Add(info);

            var steps = attackSerialized.FindProperty("steps");
            var stepSection = Section(
                $"2. 공격 Step · {steps.arraySize}개",
                "기본공격 한 번을 Step 하나로 이어 붙입니다. Step 번호와 표시 이름은 순서·공격 형태에서 자동 생성되며, 수치는 이 프리셋이 소유합니다. 다른 수치가 필요하면 프리셋을 복제하세요.");
            for (var index = 0; index < steps.arraySize; index++) BuildAttackStep(stepSection, steps, index);
            stepSection.Add(AddButton("+ 공격 Step 추가", () => ShowAddAttackStepMenu(steps)));
            assemblerScroll.Add(stepSection);
            BuildAttackFeel();
        }

        private void BuildAttackStep(VisualElement parent, SerializedProperty steps, int index)
        {
            var step = steps.GetArrayElementAtIndex(index);
            var pattern = (MonsterActiveAttackPattern)step.FindPropertyRelative("pattern").enumValueIndex;
            var card = new VisualElement(); card.AddToClassList("sub-card");
            card.Add(CardHeader($"Step {index + 1:00} · {ActivePatternLabel(pattern)}",
                SmallButton("▲", () => MoveAttackStep(index, -1), false, index > 0),
                SmallButton("▼", () => MoveAttackStep(index, 1), false, index < steps.arraySize - 1),
                SmallButton("기본공격에서 복사", () => ShowCopyBasicAttackMenu(index)),
                SmallButton("복제", () => DuplicateAttackStep(index)),
                SmallButton("삭제", () => DeleteAttackStep(index), true, steps.arraySize > 1)));

            if (index == 0)
            {
                card.Add(DelayControl(
                    attackSerialized,
                    step.FindPropertyRelative("delayAfterPrevious"),
                    "스킬 발동 후 딜레이"));
            }
            else
            {
                var startModeProperty = step.FindPropertyRelative("startMode");
                var startMode = (MonsterActiveStepStartMode)startModeProperty.enumValueIndex;
                card.Add(AttackEnumPopup(
                    startModeProperty,
                    "다음 Step 시작 기준",
                    EnumValues<MonsterActiveStepStartMode>(),
                    ActiveStepStartModeLabel,
                    ScheduleRebuild));
                card.Add(DelayControl(
                    attackSerialized,
                    step.FindPropertyRelative("delayAfterPrevious"),
                    startMode == MonsterActiveStepStartMode.AfterPreviousLaunch
                        ? "발사 후 체인 간격(초)"
                        : "이전 Step 종료 후 대기(초)"));
            }
            card.Add(BoundProperty(
                attackSerialized,
                step.FindPropertyRelative("playbackSpeed"),
                "Step 전체 속도 배율"));
            card.Add(AttackEnumPopup(step.FindPropertyRelative("targetPolicy"), "타깃 선택", EnumValues<MonsterActiveTargetPolicy>(), ActiveTargetLabel));
            card.Add(BoundProperty(
                attackSerialized,
                step.FindPropertyRelative("dashBeforeAttack"),
                "공격 전 돌진",
                () => ReconcileAttackStep(index, "돌진 사용")));
            if (step.FindPropertyRelative("dashBeforeAttack").boolValue)
            {
                card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("dashFrontDistance"), "돌진 거리(m)"));
                card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("dashDuration"), "도착 반동 시간(초)"));
            }

            card.Add(AttackEnumPopup(step.FindPropertyRelative("pattern"), "공격 형태", EnumValues<MonsterActiveAttackPattern>(), ActivePatternLabel,
                () => ReconcileAttackStep(index, "공격 형태")));
            var supported = EnumValues<MonsterActiveAttackProgression>().Where(value => MonsterActiveAttackStep.SupportsProgression(pattern, value)).ToList();
            card.Add(AttackEnumPopup(step.FindPropertyRelative("progression"), "판정 진행", supported, ActiveProgressionLabel));
            card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("progressionDuration"), "진행 시간(초)"));
            card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("damageMultiplier"), "공격력 배율"));
            var usesDamageRange = (MonsterActiveDamageMultiplierMode)step
                .FindPropertyRelative("damageMultiplierMode").enumValueIndex ==
                MonsterActiveDamageMultiplierMode.RandomRange;
            var damageRangeToggle = new Toggle("피해 배율 범위 사용") { value = usesDamageRange };
            damageRangeToggle.AddToClassList("editor-field");
            damageRangeToggle.RegisterValueChangedCallback(evt =>
                SetAttackDamageRange(index, evt.newValue));
            card.Add(damageRangeToggle);
            if (usesDamageRange)
            {
                card.Add(BoundProperty(
                    attackSerialized,
                    step.FindPropertyRelative("maximumDamageMultiplier"),
                    "최대 공격력 배율"));
            }
            card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("maxTargets"), "최대 타깃"));
            BuildAttackGeometry(card, step, pattern);
            BuildAttackHitSequence(card, step, index, pattern);
            card.Add(BoundProperty(
                attackSerialized,
                step.FindPropertyRelative("telegraphDelay"),
                "예고 후 판정(초)",
                () => ReconcileAttackStep(index, "예고 사용")));
            card.Add(BoundProperty(
                attackSerialized,
                step.FindPropertyRelative("hitAreaVisibleDuration"),
                "판정 표시 시간(초)"));
            card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("visualDuration"), "연출 유지(초)"));
            BuildAttackHitEffects(card, step, index);
            BuildAttackPresentationSlots(card, step, index);
            parent.Add(card);
        }

        private void BuildAttackGeometry(VisualElement card, SerializedProperty step, MonsterActiveAttackPattern pattern)
        {
            var projectile = pattern is MonsterActiveAttackPattern.PiercingProjectile or
                MonsterActiveAttackPattern.ExplosiveProjectile or
                MonsterActiveAttackPattern.StandardProjectile or
                MonsterActiveAttackPattern.ReturningProjectile or
                MonsterActiveAttackPattern.TravelingWave;
            if (pattern is not MonsterActiveAttackPattern.SingleTarget and
                not MonsterActiveAttackPattern.SelfCircle)
                card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("range"), "사거리(m)"));
            if (pattern is MonsterActiveAttackPattern.Line or MonsterActiveAttackPattern.PiercingBeam or
                MonsterActiveAttackPattern.PiercingProjectile or
                MonsterActiveAttackPattern.ReturningProjectile or
                MonsterActiveAttackPattern.TravelingWave)
                card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("width"), "공격 폭(m)"));
            if (pattern is MonsterActiveAttackPattern.SelfCircle or MonsterActiveAttackPattern.FrontCircle or
                MonsterActiveAttackPattern.TargetCircle ||
                pattern == MonsterActiveAttackPattern.InstantMagic && (MonsterActiveInstantMagicTarget)step.FindPropertyRelative("instantMagicTarget").enumValueIndex == MonsterActiveInstantMagicTarget.TargetArea)
                card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("radius"), "범위 반경(m)"));
            if (pattern == MonsterActiveAttackPattern.FrontCircle)
                card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("forwardOffset"), "전방 중심 거리(m)"));
            if (pattern is MonsterActiveAttackPattern.Cone or MonsterActiveAttackPattern.Breath)
                card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("angle"), "부채꼴 각도"));
            if (projectile)
            {
                if (pattern is MonsterActiveAttackPattern.StandardProjectile or
                    MonsterActiveAttackPattern.PiercingProjectile or
                    MonsterActiveAttackPattern.ExplosiveProjectile)
                {
                    card.Add(AttackEnumPopup(step.FindPropertyRelative("projectileFormation"), "투사체 형태", EnumValues<MonsterActiveProjectileFormation>(),
                        value => value == MonsterActiveProjectileFormation.Single ? "단일" : "부채꼴",
                        () => ReconcileAttackStep(ParseArrayIndex(step.propertyPath), "투사체 형태")));
                    if ((MonsterActiveProjectileFormation)step.FindPropertyRelative("projectileFormation").enumValueIndex == MonsterActiveProjectileFormation.Fan)
                    {
                        card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("projectileCount"), "투사체 개수"));
                        card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("projectileFanAngle"), "부채꼴 각도"));
                    }
                }
                if (pattern is MonsterActiveAttackPattern.StandardProjectile or
                    MonsterActiveAttackPattern.ExplosiveProjectile)
                {
                    var travelChoices = new List<MonsterBasicAttackProjectileTravel>
                    {
                        MonsterBasicAttackProjectileTravel.Homing,
                        MonsterBasicAttackProjectileTravel.Straight
                    };
                    card.Add(AttackEnumPopup(
                        step.FindPropertyRelative("projectileTravel"),
                        "투사체 경로",
                        travelChoices,
                        BasicTravelLabel,
                        () => ConfigureActiveProjectileTravel(ParseArrayIndex(step.propertyPath))));
                }
                card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("projectileSpeed"), "투사체 속도"));
                card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("projectileLifetime"), "최대 수명(초)"));
                card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("projectileCollisionRadius"), "충돌 반경(m)"));
                if (pattern == MonsterActiveAttackPattern.ExplosiveProjectile)
                    card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("explosionRadius"), "폭발 반경(m)"));
            }
            if (pattern == MonsterActiveAttackPattern.InstantMagic)
            {
                card.Add(AttackEnumPopup(step.FindPropertyRelative("instantMagicTarget"), "마법 타깃", EnumValues<MonsterActiveInstantMagicTarget>(),
                    value => value == MonsterActiveInstantMagicTarget.SingleTarget ? "단일 타깃" : "범위 타깃", () => ReconcileAttackStep(ParseArrayIndex(step.propertyPath), "마법 타깃")));
                card.Add(AttackEnumPopup(step.FindPropertyRelative("magicDirection"), "등장 방향", EnumValues<MonsterActiveMagicDirection>(),
                    value => value switch
                    {
                        MonsterActiveMagicDirection.GroundUp => "바닥에서 솟음",
                        MonsterActiveMagicDirection.SkyDown => "위에서 떨어짐",
                        _ => "정면에서 진행"
                    }, () => ReconcileAttackStep(ParseArrayIndex(step.propertyPath), "등장 방향")));
            }
        }

        private void ConfigureActiveProjectileTravel(int stepIndex)
        {
            if (stepIndex < 0) return;
            attackSerialized.Update();
            attackSerialized.FindProperty("steps").GetArrayElementAtIndex(stepIndex)
                .FindPropertyRelative("projectileTravelConfigured").boolValue = true;
            attackSerialized.ApplyModifiedProperties();
            ReconcileAttackStep(stepIndex, "투사체 경로");
        }

        private void BuildAttackHitSequence(
            VisualElement card,
            SerializedProperty step,
            int stepIndex,
            MonsterActiveAttackPattern pattern)
        {
            var ratios = step.FindPropertyRelative("damageRatios");
            if (pattern == MonsterActiveAttackPattern.ReturningProjectile)
            {
                card.Add(Help("왕복 투사체는 기본공격 공용 계약과 동일하게 전진·귀환 고정 2타를 사용합니다."));
                card.Add(BoundProperty(
                    attackSerialized,
                    step.FindPropertyRelative("repeatHitInterval"),
                    "왕복 타격 간격(초)"));
                card.Add(BoundProperty(
                    attackSerialized,
                    step.FindPropertyRelative("secondaryDamageRatio"),
                    "추가 대상 피해 배율"));
                card.Add(BoundProperty(
                    attackSerialized,
                    step.FindPropertyRelative("repeatImpactFeedback"),
                    "타격마다 명중 연출 재생"));
                return;
            }
            if (pattern == MonsterActiveAttackPattern.Breath)
            {
                card.Add(Help("브레스는 기본공격 공용 계약과 동일하게 2타 이상의 연속 판정을 항상 사용합니다."));
                var breathHitCount = new IntegerField("타격 횟수") { value = ratios.arraySize };
                breathHitCount.AddToClassList("editor-field");
                breathHitCount.RegisterValueChangedCallback(evt =>
                    SetAttackHitCount(
                        stepIndex,
                        Mathf.Clamp(evt.newValue, 2, MonsterBasicAttackProfile.MaximumHitCount)));
                card.Add(breathHitCount);
                card.Add(BoundProperty(
                    attackSerialized,
                    step.FindPropertyRelative("repeatHitInterval"),
                    "타격 간격(초)"));
                card.Add(BoundProperty(
                    attackSerialized,
                    step.FindPropertyRelative("secondaryDamageRatio"),
                    "추가 대상 피해 배율"));
                card.Add(BoundProperty(
                    attackSerialized,
                    step.FindPropertyRelative("repeatImpactFeedback"),
                    "타격마다 명중 연출 재생"));
                card.Add(Help("Step 공격력 배율을 타격 횟수로 균등 분배하며, 전체 합계는 항상 1로 유지됩니다."));
                return;
            }
            if (!MonsterActiveAttackStep.SupportsEditableMultiHit(pattern))
            {
                card.Add(Help("이 공격 형태는 기본공격 공용 계약과 동일하게 1회 판정을 사용합니다."));
                return;
            }

            var enabled = ratios.arraySize > 1;
            var toggle = new Toggle("연타 사용") { value = enabled };
            toggle.AddToClassList("editor-field");
            toggle.RegisterValueChangedCallback(evt =>
                SetAttackHitCount(stepIndex, evt.newValue ? 3 : 1));
            card.Add(toggle);
            if (!enabled) return;

            var count = new IntegerField("타격 횟수") { value = ratios.arraySize };
            count.AddToClassList("editor-field");
            count.RegisterValueChangedCallback(evt =>
                SetAttackHitCount(
                    stepIndex,
                    Mathf.Clamp(evt.newValue, 2, MonsterBasicAttackProfile.MaximumHitCount)));
            card.Add(count);
            card.Add(BoundProperty(
                attackSerialized,
                step.FindPropertyRelative("repeatHitInterval"),
                "타격 간격(초)"));
            card.Add(BoundProperty(
                attackSerialized,
                step.FindPropertyRelative("secondaryDamageRatio"),
                "추가 대상 피해 배율"));
            card.Add(BoundProperty(
                attackSerialized,
                step.FindPropertyRelative("repeatImpactFeedback"),
                "타격마다 명중 연출 재생"));
            card.Add(Help("Step 공격력 배율을 타격 횟수로 균등 분배하며, 전체 합계는 항상 1로 유지됩니다."));
        }

        private void SetAttackHitCount(int stepIndex, int count)
        {
            attackSerialized.Update();
            var step = attackSerialized.FindProperty("steps").GetArrayElementAtIndex(stepIndex);
            var ratios = step.FindPropertyRelative("damageRatios");
            count = Mathf.Clamp(count, 1, MonsterBasicAttackProfile.MaximumHitCount);
            ratios.arraySize = count;
            var ratio = 1f / count;
            for (var index = 0; index < count; index++)
            {
                ratios.GetArrayElementAtIndex(index).floatValue = ratio;
            }
            attackSerialized.ApplyModifiedProperties();
            ReconcileAttackStep(stepIndex, "연타 구성");
        }

        private void SetAttackDamageRange(int stepIndex, bool enabled)
        {
            attackSerialized.Update();
            var step = attackSerialized.FindProperty("steps").GetArrayElementAtIndex(stepIndex);
            step.FindPropertyRelative("damageMultiplierMode").enumValueIndex = enabled
                ? (int)MonsterActiveDamageMultiplierMode.RandomRange
                : (int)MonsterActiveDamageMultiplierMode.Fixed;
            var minimum = Mathf.Max(0f, step.FindPropertyRelative("damageMultiplier").floatValue);
            var maximum = step.FindPropertyRelative("maximumDamageMultiplier");
            maximum.floatValue = enabled ? Mathf.Max(minimum, maximum.floatValue) : minimum;
            attackSerialized.ApplyModifiedProperties();
            MarkCurrentDirty(rebuild: true);
        }

        private void BuildAttackHitEffects(VisualElement card, SerializedProperty step, int stepIndex)
        {
            var list = step.FindPropertyRelative("hitEffects");
            var section = Section($"추가 타격 효과 · {list.arraySize}개", "에어본·넉백·기절·출혈·화상·감속·끌어당기기는 이 Step의 기본 타격에 추가됩니다.");
            for (var index = 0; index < list.arraySize; index++)
            {
                var captured = index; var effect = list.GetArrayElementAtIndex(index);
                var shell = new VisualElement(); shell.AddToClassList("sub-card");
                shell.Add(CardHeader($"효과 {index + 1:00}", SmallButton("▲", () => MoveNestedArray(stepIndex, "hitEffects", captured, -1), false, index > 0),
                    SmallButton("▼", () => MoveNestedArray(stepIndex, "hitEffects", captured, 1), false, index < list.arraySize - 1), SmallButton("삭제", () => DeleteNestedArray(stepIndex, "hitEffects", captured), true)));
                shell.Add(AttackEnumPopup(effect.FindPropertyRelative("type"), "효과 종류", EnumValues<MonsterActiveHitEffectType>(), ActiveHitLabel,
                    ScheduleRebuild));
                var type = (MonsterActiveHitEffectType)effect.FindPropertyRelative("type").enumValueIndex;
                if (type is MonsterActiveHitEffectType.Knockback or MonsterActiveHitEffectType.Airborne or MonsterActiveHitEffectType.Bleed or MonsterActiveHitEffectType.Burn or MonsterActiveHitEffectType.Slow or MonsterActiveHitEffectType.Pull)
                    shell.Add(BoundProperty(attackSerialized, effect.FindPropertyRelative("magnitude"), ActiveMagnitudeLabel(type)));
                shell.Add(BoundProperty(attackSerialized, effect.FindPropertyRelative("duration"), "지속 시간(초)"));
                if (type is MonsterActiveHitEffectType.Bleed or MonsterActiveHitEffectType.Burn)
                {
                    shell.Add(BoundProperty(attackSerialized, effect.FindPropertyRelative("tickInterval"), "피해 간격(초)"));
                }
                section.Add(shell);
            }
            section.Add(AddButton("+ 타격 효과 추가", () => AddAttackHitEffect(stepIndex)));
            card.Add(section);
        }

        private void BuildAttackPresentationSlots(VisualElement card, SerializedProperty step, int stepIndex)
        {
            var list = step.FindPropertyRelative("attackBlockVfxSlots");
            var stepModel = attackWorking.Steps[stepIndex];
            var section = Section(
                $"VFX/SFX 공간 계약 · {list.arraySize}개",
                "기본공격과 같은 계약 생성기를 사용합니다. ID·이름·발생 규칙은 공격 형태에서 자동 결정되고, 제작자는 메모만 입력합니다.");
            for (var index = 0; index < list.arraySize; index++)
            {
                var captured = index;
                var slot = list.GetArrayElementAtIndex(index);
                var shell = new VisualElement(); shell.AddToClassList("sub-card");
                var model = stepModel.AttackBlockVfxSlots[index];
                shell.Add(CardHeader($"공간 {index + 1:00} · {model.DisplayName}",
                    SmallButton("▲", () => MoveAttackSlot(stepIndex, captured, -1),
                        false, index > 0),
                    SmallButton("▼", () => MoveAttackSlot(stepIndex, captured, 1),
                        false, index < list.arraySize - 1),
                    SmallButton("복제", () => DuplicateAttackSlot(stepIndex, captured),
                        false, !model.IsDeliveryVisual),
                    SmallButton("삭제", () => DeleteAttackSlot(stepIndex, captured), true)));
                shell.Add(ContractDetails(model));
                shell.Add(BoundProperty(
                    attackSerialized,
                    slot.FindPropertyRelative("productionMemo"),
                    "제작 메모"));
                section.Add(shell);
            }
            if (stepModel.InactiveAttackBlockVfxSlots.Count > 0)
            {
                section.Add(Help(
                    $"현재 공격 형태에서 쓰지 않는 이전 계약 {stepModel.InactiveAttackBlockVfxSlots.Count}개는 " +
                    "삭제하지 않고 이 Step 내부에 보관합니다."));
            }
            section.Add(AddButton(
                "+ VFX/SFX 공간 추가",
                () => ShowAddAttackSlotMenu(stepIndex)));
            card.Add(section);
        }

        private void BuildAttackFeel()
        {
            var section = Section("3. 액티브 공통 FEEL 타격감", "모든 Step의 실제 명중이 FEEL 프로필 하나를 공유합니다.");
            var feel = attackSerialized.FindProperty("impactFeel");
            var prefab = feel.FindPropertyRelative("prefab");
            var options = BasicAttackFeelPresetUtility.LoadFeelProfileOptions(prefab.objectReferenceValue as GameObject).ToList();
            var choices = options.Select(x => x.Label).ToList();
            var current = Mathf.Max(0, options.FindIndex(x => x.Profile == prefab.objectReferenceValue));
            var popup = new PopupField<string>("FEEL 프로필", choices, current); popup.AddToClassList("editor-field");
            popup.RegisterValueChangedCallback(evt =>
            {
                var index = choices.IndexOf(evt.newValue); attackSerialized.Update();
                attackSerialized.FindProperty("impactFeel").FindPropertyRelative("prefab").objectReferenceValue = index >= 0 ? options[index].Profile : null;
                attackSerialized.ApplyModifiedProperties(); attackDirty = true; RefreshState();
            });
            section.Add(popup); section.Add(AddButton("FEEL 연구소 열기", BasicAttackFeelPresetUtility.OpenFormalLab));
            assemblerScroll.Add(section);
        }

        private VisualElement DelayControl(SerializedObject serialized, SerializedProperty property, string label)
        {
            var shell = new VisualElement();
            var toggle = new Toggle("딜레이 사용") { value = property.floatValue > 0f }; toggle.AddToClassList("editor-field"); shell.Add(toggle);
            if (toggle.value) shell.Add(BoundProperty(serialized, property, label));
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (suppressUiCallbacks) return;
                serialized.Update(); var live = serialized.FindProperty(property.propertyPath); live.floatValue = evt.newValue ? Mathf.Max(0.1f, live.floatValue) : 0f;
                serialized.ApplyModifiedProperties(); MarkCurrentDirty(); ScheduleRebuild();
            });
            return shell;
        }

        private PopupField<T> AttackEnumPopup<T>(SerializedProperty property, string label, List<T> choices, Func<T, string> format, Action changed = null) where T : struct
        {
            if (choices == null || choices.Count == 0) throw new InvalidOperationException($"{label}에 표시할 수 있는 호환 옵션이 없습니다.");
            var current = (T)Enum.ToObject(typeof(T), property.enumValueIndex);
            if (!choices.Contains(current))
            {
                current = choices[0];
                property.enumValueIndex = Convert.ToInt32(current);
                attackSerialized.ApplyModifiedProperties();
                attackDirty = true;
            }
            var field = new PopupField<T>(label, choices, current, format, format); field.AddToClassList("editor-field");
            var path = property.propertyPath;
            field.RegisterValueChangedCallback(evt =>
            {
                if (suppressUiCallbacks) return;
                attackSerialized.Update(); attackSerialized.FindProperty(path).enumValueIndex = Convert.ToInt32(evt.newValue); attackSerialized.ApplyModifiedProperties();
                attackDirty = true; changed?.Invoke(); if (changed == null) RefreshState();
            });
            return field;
        }

        private void ShowAddAttackStepMenu(SerializedProperty steps)
        {
            var menu = new GenericMenu();
            foreach (var pattern in EnumValues<MonsterActiveAttackPattern>()) { var captured = pattern; menu.AddItem(new GUIContent(ActivePatternLabel(pattern)), false, () => AddAttackStep(captured)); }
            menu.ShowAsContext();
        }

        private void ShowCopyBasicAttackMenu(int stepIndex)
        {
            var profiles = AssetDatabase.FindAssets(
                    "t:MonsterBasicAttackProfile",
                    new[] { MonsterBasicAttackPresetUtility.ProfileRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>)
                .Where(profile => profile != null)
                .OrderBy(profile => profile.AttackId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var menu = new GenericMenu();
            foreach (var profile in profiles)
            {
                var captured = profile;
                menu.AddItem(
                    new GUIContent($"{profile.AttackId} · {profile.DisplayName}"),
                    false,
                    () => CopyBasicAttackIntoStep(stepIndex, captured));
            }
            if (profiles.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("복사할 기본공격 프리셋 없음"));
            }
            menu.ShowAsContext();
        }

        private void CopyBasicAttackIntoStep(int stepIndex, MonsterBasicAttackProfile source)
        {
            attackSerialized.ApplyModifiedProperties();
            if (source == null || stepIndex < 0 || stepIndex >= attackWorking.Steps.Count) return;
            Undo.RecordObject(attackWorking, "액티브 Step에 기본공격 블록 복사");
            var step = attackWorking.Steps[stepIndex];
            if (!step.EditorCopyAttackBlockFrom(source, out var error))
            {
                attackMessage = "오류: " + error;
                RefreshState();
                return;
            }
            step.EditorNormalizeIdentity(stepIndex);
            var reconciled = MonsterActiveAttackBlockContractTemplates.Reconcile(step, out var result);
            step.EditorSetAttackBlockVfxSlots(reconciled);
            attackSerialized.Update();
            attackDirty = true;
            attackMessage = $"[{source.DisplayName}] 공격 블록을 Step {stepIndex + 1:00}의 독립 데이터로 복사했습니다. " +
                            $"계약 유지 {result.Retained} · 추가 {result.Added} · 보관 {result.Archived}";
            RebuildCurrent();
        }

        private void AddAttackStep(MonsterActiveAttackPattern pattern)
        {
            attackSerialized.Update(); var list = attackSerialized.FindProperty("steps"); var index = list.arraySize; list.InsertArrayElementAtIndex(index);
            var step = list.GetArrayElementAtIndex(index); step.FindPropertyRelative("stepId").stringValue = $"step_{index + 1:00}";
            step.FindPropertyRelative("displayName").stringValue = ActivePatternLabel(pattern); step.FindPropertyRelative("pattern").enumValueIndex = (int)pattern;
            step.FindPropertyRelative("targetPolicy").enumValueIndex = (int)MonsterActiveTargetPolicy.SameTarget;
            step.FindPropertyRelative("dashBeforeAttack").boolValue = false;
            step.FindPropertyRelative("dashFrontDistance").floatValue = 1f;
            step.FindPropertyRelative("dashDuration").floatValue = 0.1f;
            step.FindPropertyRelative("progression").enumValueIndex = 0; step.FindPropertyRelative("delayAfterPrevious").floatValue = index == 0 ? 0f : 0.12f;
            step.FindPropertyRelative("playbackSpeed").floatValue = 1f;
            step.FindPropertyRelative("damageMultiplier").floatValue = 1f; step.FindPropertyRelative("maxTargets").intValue = 8;
            step.FindPropertyRelative("range").floatValue = 4f; step.FindPropertyRelative("width").floatValue = 1.2f; step.FindPropertyRelative("radius").floatValue = 1.8f;
            step.FindPropertyRelative("forwardOffset").floatValue = 1.5f; step.FindPropertyRelative("angle").floatValue = 70f;
            step.FindPropertyRelative("progressionDuration").floatValue = 0.25f; step.FindPropertyRelative("telegraphDelay").floatValue = 0.12f;
            step.FindPropertyRelative("visualDuration").floatValue = 0.8f;
            step.FindPropertyRelative("hitAreaVisibleDuration").floatValue = 0.42f;
            step.FindPropertyRelative("projectileFormation").enumValueIndex = (int)MonsterActiveProjectileFormation.Single;
            step.FindPropertyRelative("projectileCount").intValue = 1;
            step.FindPropertyRelative("projectileFanAngle").floatValue = 50f;
            step.FindPropertyRelative("projectileSpeed").floatValue = 10f;
            step.FindPropertyRelative("projectileCollisionRadius").floatValue = 0.25f;
            step.FindPropertyRelative("explosionRadius").floatValue = 1.8f;
            step.FindPropertyRelative("instantMagicTarget").enumValueIndex =
                (int)MonsterActiveInstantMagicTarget.SingleTarget;
            step.FindPropertyRelative("magicDirection").enumValueIndex =
                (int)MonsterActiveMagicDirection.GroundUp;
            step.FindPropertyRelative("damageRatios").arraySize = 1;
            step.FindPropertyRelative("damageRatios").GetArrayElementAtIndex(0).floatValue = 1f;
            step.FindPropertyRelative("secondaryDamageRatio").floatValue = 1f;
            step.FindPropertyRelative("repeatHitInterval").floatValue = 0.08f;
            step.FindPropertyRelative("projectileLifetime").floatValue = 3f;
            step.FindPropertyRelative("repeatImpactFeedback").boolValue = true;
            step.FindPropertyRelative("hitEffects").arraySize = 0;
            step.FindPropertyRelative("attackBlockVfxSlots").arraySize = 0;
            step.FindPropertyRelative("inactiveAttackBlockVfxSlots").arraySize = 0;
            step.FindPropertyRelative("presentationSlots").arraySize = 0;
            attackSerialized.ApplyModifiedProperties(); ReconcileAttackStep(index, "새 공격");
        }

        private void ReconcileAttackStep(int index, string reason)
        {
            attackSerialized.ApplyModifiedProperties();
            if (index < 0 || index >= attackWorking.Steps.Count) return;
            NormalizeAttackStepIdentities();
            var step = attackWorking.Steps[index];
            NormalizeAttackStepSequence(step);
            var reconciled = MonsterActiveAttackBlockContractTemplates.Reconcile(step, out var result);
            step.EditorSetAttackBlockVfxSlots(reconciled); attackSerialized.Update(); attackDirty = true;
            attackMessage = $"{reason} 기준 공간 정리 · 유지 {result.Retained} · 추가 {result.Added} · 제외 {result.Archived}";
            RebuildCurrent();
        }

        private static void NormalizeAttackStepSequence(MonsterActiveAttackStep step)
        {
            if (step == null) return;
            step.EditorNormalizeHitSequenceForPattern();
        }

        private void AddAttackHitEffect(int stepIndex)
        {
            attackSerialized.Update(); var list = attackSerialized.FindProperty("steps").GetArrayElementAtIndex(stepIndex).FindPropertyRelative("hitEffects");
            var index = list.arraySize; list.InsertArrayElementAtIndex(index); var effect = list.GetArrayElementAtIndex(index);
            effect.FindPropertyRelative("type").enumValueIndex = (int)MonsterActiveHitEffectType.Stun; effect.FindPropertyRelative("magnitude").floatValue = 0.25f;
            effect.FindPropertyRelative("duration").floatValue = 0.35f; effect.FindPropertyRelative("tickInterval").floatValue = 0.5f;
            attackSerialized.ApplyModifiedProperties(); attackDirty = true; RebuildCurrent();
        }

        private void ShowAddAttackSlotMenu(int stepIndex)
        {
            var roles = GetAvailableAttackSlotRoles(stepIndex);
            var menu = new GenericMenu();
            foreach (var role in roles)
            {
                var captured = role;
                menu.AddItem(
                    new GUIContent(BasicAttackWorkshopVfxRoles.GetLabel(role)),
                    false,
                    () => AddAttackSlotForRole(stepIndex, captured));
            }
            if (roles.Count == 0)
                menu.AddDisabledItem(new GUIContent("현재 공격 형태에 추가 가능한 공간 없음"));
            menu.ShowAsContext();
        }

        private List<BasicAttackWorkshopVfxRole> GetAvailableAttackSlotRoles(int stepIndex)
        {
            attackSerialized.ApplyModifiedProperties();
            if (stepIndex < 0 || stepIndex >= attackWorking.Steps.Count)
                return new List<BasicAttackWorkshopVfxRole>();
            var step = attackWorking.Steps[stepIndex];
            var compiled = ScriptableObject.CreateInstance<MonsterBasicAttackProfile>();
            compiled.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                step.EditorCompileAttackBlock(compiled);
                var hasDeliveryVisual = step.AttackBlockVfxSlots.Any(slot =>
                    slot != null && slot.IsDeliveryVisual);
                return BasicAttackWorkshopVfxRoles
                    .GetCompatibleValues(compiled, BasicAttackWorkshopVfxRole.Custom)
                    .Where(role => role != BasicAttackWorkshopVfxRole.Custom)
                    .Where(role => !hasDeliveryVisual ||
                                   role != BasicAttackWorkshopVfxRole.DeliveryVisual)
                    .ToList();
            }
            finally
            {
                DestroyImmediate(compiled);
            }
        }

        private static IEnumerable<string> StoredAttackSlotIds(MonsterActiveAttackStep step)
        {
            return step.AttackBlockVfxSlots
                .Where(slot => slot != null)
                .Select(slot => slot.SlotId)
                .Concat(step.InactiveAttackBlockVfxSlots
                    .Where(slot => slot != null)
                    .Select(slot => slot.SlotId));
        }

        private void AddAttackSlot(int stepIndex)
        {
            var role = GetAvailableAttackSlotRoles(stepIndex).FirstOrDefault();
            if (role != BasicAttackWorkshopVfxRole.Custom)
                AddAttackSlotForRole(stepIndex, role);
        }

        private void AddAttackSlotForRole(
            int stepIndex,
            BasicAttackWorkshopVfxRole role)
        {
            attackSerialized.ApplyModifiedProperties();
            if (role == BasicAttackWorkshopVfxRole.Custom ||
                stepIndex < 0 || stepIndex >= attackWorking.Steps.Count) return;
            Undo.RecordObject(attackWorking, "공격 액티브 VFX/SFX 공간 추가");
            var step = attackWorking.Steps[stepIndex];
            var activeIds = new HashSet<string>(
                step.AttackBlockVfxSlots
                    .Where(slot => slot != null)
                    .Select(slot => slot.SlotId),
                StringComparer.OrdinalIgnoreCase);
            var archived = step.InactiveAttackBlockVfxSlots
                .LastOrDefault(slot =>
                    slot != null &&
                    !activeIds.Contains(slot.SlotId) &&
                    BasicAttackWorkshopVfxRoles.Resolve(slot) == role);
            var slot = archived != null
                ? BasicAttackWorkshopVfxSlot.From(archived)
                : new BasicAttackWorkshopVfxSlot
                {
                    slotId = CreateAutomaticContractId(StoredAttackSlotIds(step), role)
                };
            slot.editorRole = role;
            BasicAttackWorkshopVfxRoles.Apply(slot, role);
            var next = step.AttackBlockVfxSlots
                .Where(candidate => candidate != null)
                .Select(candidate => candidate.EditorClone())
                .ToList();
            next.Add(slot.Compile());
            step.EditorSetAttackBlockVfxSlots(next);
            attackSerialized.Update();
            attackDirty = true;
            attackMessage = $"Step {stepIndex + 1:00}에 [{slot.displayName}] 공간을 추가했습니다.";
            RebuildCurrent();
        }

        private void DeleteAttackSlot(int stepIndex, int slotIndex)
        {
            attackSerialized.ApplyModifiedProperties();
            if (stepIndex < 0 || stepIndex >= attackWorking.Steps.Count) return;
            var step = attackWorking.Steps[stepIndex];
            if (slotIndex < 0 || slotIndex >= step.AttackBlockVfxSlots.Count) return;
            Undo.RecordObject(attackWorking, "공격 액티브 VFX/SFX 공간 삭제");
            var next = step.AttackBlockVfxSlots
                .Where(slot => slot != null)
                .Select(slot => slot.EditorClone())
                .ToList();
            next.RemoveAt(slotIndex);
            step.EditorSetAttackBlockVfxSlots(next);
            attackSerialized.Update();
            attackDirty = true;
            attackMessage = $"Step {stepIndex + 1:00}의 공간을 제외하고 보관했습니다.";
            RebuildCurrent();
        }

        private void DuplicateAttackSlot(int stepIndex, int slotIndex)
        {
            attackSerialized.ApplyModifiedProperties();
            if (stepIndex < 0 || stepIndex >= attackWorking.Steps.Count) return;
            var step = attackWorking.Steps[stepIndex];
            if (slotIndex < 0 || slotIndex >= step.AttackBlockVfxSlots.Count) return;
            var source = step.AttackBlockVfxSlots[slotIndex];
            if (source == null || source.IsDeliveryVisual) return;
            Undo.RecordObject(attackWorking, "공격 액티브 VFX/SFX 공간 복제");
            var next = step.AttackBlockVfxSlots
                .Where(slot => slot != null)
                .Select(slot => slot.EditorClone())
                .ToList();
            var copy = BasicAttackWorkshopVfxSlot.From(source);
            copy.slotId = CreateUniqueContractId(
                StoredAttackSlotIds(step),
                source.SlotId + "_copy");
            next.Insert(slotIndex + 1, copy.Compile());
            step.EditorSetAttackBlockVfxSlots(next);
            attackSerialized.Update();
            attackDirty = true;
            attackMessage = $"Step {stepIndex + 1:00}의 [{source.DisplayName}] 공간을 복제했습니다.";
            RebuildCurrent();
        }

        private void MoveAttackSlot(int stepIndex, int slotIndex, int direction)
        {
            attackSerialized.ApplyModifiedProperties();
            if (stepIndex < 0 || stepIndex >= attackWorking.Steps.Count) return;
            var step = attackWorking.Steps[stepIndex];
            if (slotIndex < 0 || slotIndex >= step.AttackBlockVfxSlots.Count) return;
            var target = Mathf.Clamp(slotIndex + direction, 0, step.AttackBlockVfxSlots.Count - 1);
            if (target == slotIndex) return;
            Undo.RecordObject(attackWorking, "공격 액티브 VFX/SFX 공간 순서 변경");
            var next = step.AttackBlockVfxSlots
                .Where(slot => slot != null)
                .Select(slot => slot.EditorClone())
                .ToList();
            (next[slotIndex], next[target]) = (next[target], next[slotIndex]);
            step.EditorSetAttackBlockVfxSlots(next);
            attackSerialized.Update();
            attackDirty = true;
            RebuildCurrent();
        }

        private void MoveAttackStep(int index, int direction)
        {
            attackSerialized.Update();
            var list = attackSerialized.FindProperty("steps");
            var target = Mathf.Clamp(index + direction, 0, list.arraySize - 1);
            if (target != index) list.MoveArrayElement(index, target);
            attackSerialized.ApplyModifiedProperties();
            NormalizeAttackStepIdentities();
            MarkCurrentDirty(rebuild: true);
        }

        private void DuplicateAttackStep(int index)
        {
            attackSerialized.Update();
            attackSerialized.FindProperty("steps").InsertArrayElementAtIndex(index);
            attackSerialized.ApplyModifiedProperties();
            NormalizeAttackStepIdentities();
            MarkCurrentDirty(rebuild: true);
        }

        private void DeleteAttackStep(int index)
        {
            attackSerialized.Update();
            var list = attackSerialized.FindProperty("steps");
            if (list.arraySize <= 1)
            {
                attackMessage = "오류: 공격 Step은 하나 이상 필요합니다.";
                RefreshState();
                return;
            }
            list.DeleteArrayElementAtIndex(index);
            attackSerialized.ApplyModifiedProperties();
            NormalizeAttackStepIdentities();
            MarkCurrentDirty(rebuild: true);
        }

        private void NormalizeAttackStepIdentities()
        {
            attackSerialized.Update();
            var steps = attackSerialized.FindProperty("steps");
            for (var index = 0; index < steps.arraySize; index++)
            {
                var step = steps.GetArrayElementAtIndex(index);
                var pattern = (MonsterActiveAttackPattern)step.FindPropertyRelative("pattern").enumValueIndex;
                step.FindPropertyRelative("stepId").stringValue =
                    MonsterActiveAttackStep.GetCanonicalStepId(index);
                step.FindPropertyRelative("displayName").stringValue =
                    MonsterActiveAttackStep.GetPatternDisplayName(pattern);
            }
            attackSerialized.ApplyModifiedProperties();
            attackDirty = true;
        }
        private void MoveNestedArray(int owner, string path, int index, int direction)
        { attackSerialized.Update(); var list = attackSerialized.FindProperty("steps").GetArrayElementAtIndex(owner).FindPropertyRelative(path); var target = Mathf.Clamp(index + direction, 0, list.arraySize - 1); if (target != index) list.MoveArrayElement(index, target); attackSerialized.ApplyModifiedProperties(); attackDirty = true; RebuildCurrent(); }
        private void DeleteNestedArray(int owner, string path, int index)
        { attackSerialized.Update(); attackSerialized.FindProperty("steps").GetArrayElementAtIndex(owner).FindPropertyRelative(path).DeleteArrayElementAtIndex(index); attackSerialized.ApplyModifiedProperties(); attackDirty = true; RebuildCurrent(); }
        private void DuplicateNestedArray(int owner, string path, int index)
        { attackSerialized.Update(); var list = attackSerialized.FindProperty("steps").GetArrayElementAtIndex(owner).FindPropertyRelative(path); list.InsertArrayElementAtIndex(index); var copy = list.GetArrayElementAtIndex(index + 1); copy.FindPropertyRelative("slotId").stringValue += "_copy"; attackSerialized.ApplyModifiedProperties(); attackDirty = true; RebuildCurrent(); }

        private static int ParseArrayIndex(string path) { var marker = path.LastIndexOf("Array.data[", StringComparison.Ordinal); if (marker < 0) return -1; marker += 11; var end = path.IndexOf(']', marker); return int.TryParse(path.Substring(marker, end - marker), out var value) ? value : -1; }
        private static string ActivePatternLabel(MonsterActiveAttackPattern value) =>
            MonsterActiveAttackStep.GetPatternDisplayName(value);
        private static string ActiveProgressionLabel(MonsterActiveAttackProgression value) => value switch { MonsterActiveAttackProgression.Instant => "한 번에", MonsterActiveAttackProgression.Forward => "앞으로 순차", MonsterActiveAttackProgression.LeftToRight => "왼쪽에서 오른쪽", MonsterActiveAttackProgression.RightToLeft => "오른쪽에서 왼쪽", _ => "바깥쪽으로 순차" };
        private static string ActiveTargetLabel(MonsterActiveTargetPolicy value) => value == MonsterActiveTargetPolicy.SameTarget ? "이전 Step과 같은 타깃" : "이전 Step과 다른 타깃";
        private static string ActiveStepStartModeLabel(MonsterActiveStepStartMode value) =>
            value == MonsterActiveStepStartMode.AfterPreviousLaunch
                ? "이전 Step 발사 후"
                : "이전 Step 완전 종료 후";
        private static string ActiveHitLabel(MonsterActiveHitEffectType value) => value switch { MonsterActiveHitEffectType.Knockback => "넉백", MonsterActiveHitEffectType.Airborne => "에어본", MonsterActiveHitEffectType.Stun => "기절", MonsterActiveHitEffectType.Bleed => "출혈", MonsterActiveHitEffectType.Burn => "화상", MonsterActiveHitEffectType.Slow => "감속", _ => "끌어당기기" };
        private static string ActiveMagnitudeLabel(MonsterActiveHitEffectType value) => value switch { MonsterActiveHitEffectType.Knockback => "밀어내는 거리(m)", MonsterActiveHitEffectType.Airborne => "띄우는 높이", MonsterActiveHitEffectType.Slow => "감속 비율(0~1)", MonsterActiveHitEffectType.Pull => "끌어당기는 거리(m)", _ => "효과 강도" };
        private static string ActiveEventLabel(MonsterActivePresentationEvent value) => value switch { MonsterActivePresentationEvent.Telegraph => "판정 예고", MonsterActivePresentationEvent.Launch => "공격 발사", MonsterActivePresentationEvent.Travel => "이동 중", MonsterActivePresentationEvent.Impact => "실제 명중", MonsterActivePresentationEvent.DashExit => "돌진 출발", MonsterActivePresentationEvent.DashEnter => "돌진 도착", MonsterActivePresentationEvent.MotionStart => "모션 시작", MonsterActivePresentationEvent.DeliverySpawn => "이동체 생성", MonsterActivePresentationEvent.AreaResolved => "범위 판정 완료", MonsterActivePresentationEvent.DeliveryEnd => "이동체 종료", _ => "Step 종료" };
        private static string ActiveAnchorLabel(MonsterActivePresentationAnchor value) => value switch { MonsterActivePresentationAnchor.CasterRoot => "시전자 중심", MonsterActivePresentationAnchor.AttackOrigin => "공격 시작점", MonsterActivePresentationAnchor.TargetPoint => "타깃 지점", MonsterActivePresentationAnchor.MarkerSocket => "Marker 소켓", MonsterActivePresentationAnchor.ProjectileRoot => "투사체 중심", MonsterActivePresentationAnchor.TargetRoot => "타깃 중심", MonsterActivePresentationAnchor.HitPoint => "실제 명중점", MonsterActivePresentationAnchor.AreaCenter => "범위 중심", _ => "이동 경로 시작점" };
        private static string ActiveMultiplicityLabel(MonsterActivePresentationMultiplicity value) => value switch { MonsterActivePresentationMultiplicity.OncePerStep => "Step당 한 번", MonsterActivePresentationMultiplicity.OncePerProjectile => "투사체마다", MonsterActivePresentationMultiplicity.PerTargetHit => "명중 대상마다", MonsterActivePresentationMultiplicity.PerDamageStage => "피해 단계마다", _ => "종료까지 지속" };
        private static string ActiveAttachmentLabel(MonsterActivePresentationAttachment value) => value switch { MonsterActivePresentationAttachment.World => "월드 고정", MonsterActivePresentationAttachment.FollowAnchor => "기준 위치 추적", _ => "이동체 외형" };
        private static string ActiveEndLabel(MonsterActivePresentationEndPolicy value) => value switch { MonsterActivePresentationEndPolicy.Timed => "설정 시간", MonsterActivePresentationEndPolicy.DeliveryEnd => "이동체 종료", MonsterActivePresentationEndPolicy.StepEnd => "Step 종료", MonsterActivePresentationEndPolicy.MotionEnd => "모션 종료", _ => "파티클 길이" };
    }
}
