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
        partial void BuildBasicAssembler()
        {
            var recipe = basicSession.Recipe;
            var info = Section("1. 프리셋 정보", "ID는 저장 자산의 참조 보호를 위해 처음 저장한 뒤 고정됩니다.");
            info.Add(BasicText("프리셋 ID", () => recipe.attackId, value => recipe.attackId = value));
            info.Add(BasicText("표시 이름", () => recipe.displayName, value => recipe.displayName = value));
            info.Add(BasicText("기획 메모", () => recipe.designMemo, value => recipe.designMemo = value, true));
            assemblerScroll.Add(info);

            var form = Section("2. 공격 방식", "선택한 방식에 필요한 값만 표시합니다. 방식 변경 시 VFX/SFX 공간 계약도 함께 정리됩니다.");
            form.Add(BasicPopup("큰 분류", EnumValues<BasicAttackWorkshopFamily>(), () => recipe.family,
                value => recipe.family = value, BasicFamilyLabel, true));
            if (recipe.family == BasicAttackWorkshopFamily.Melee) BuildBasicMelee(form, recipe);
            else if (recipe.family == BasicAttackWorkshopFamily.Ranged) BuildBasicRanged(form, recipe);
            else BuildBasicSpecial(form, recipe);
            assemblerScroll.Add(form);

            var common = Section("3. 공통 판정 수치");
            common.Add(BasicFloat("사거리 배율", () => recipe.rangeMultiplier, value => recipe.rangeMultiplier = value));
            common.Add(BasicInt("최대 대상", () => recipe.maxTargets, value => recipe.maxTargets = value));
            common.Add(BasicFloat("판정 표시 시간(초)", () => recipe.hitAreaVisibleDuration, value => recipe.hitAreaVisibleDuration = value));
            assemblerScroll.Add(common);

            BuildBasicVfxContracts(recipe);
            BuildBasicFeel(recipe);
        }

        private void BuildBasicMelee(VisualElement form, BasicAttackWorkshopRecipe recipe)
        {
            form.Add(BasicPopup("공격 형태", EnumValues<BasicAttackWorkshopMeleePattern>(), () => recipe.meleePattern,
                value => recipe.meleePattern = value, BasicMeleeLabel, true));
            if (recipe.meleePattern == BasicAttackWorkshopMeleePattern.Fan)
                form.Add(BasicFloat("부채꼴 각도", () => recipe.angle, value => recipe.angle = value));
            if (recipe.meleePattern == BasicAttackWorkshopMeleePattern.Line)
                form.Add(BasicFloat("직선 폭(m)", () => recipe.lineWidth, value => recipe.lineWidth = value));
            if (recipe.meleePattern == BasicAttackWorkshopMeleePattern.Circle)
            {
                form.Add(BasicFloat("원형 반경(m)", () => recipe.radius, value => recipe.radius = value));
                form.Add(BasicPopup("원형 중심", EnumValues<MonsterBasicAttackCenter>(), () => recipe.circleCenter,
                    value => recipe.circleCenter = value, value => value == MonsterBasicAttackCenter.Source ? "내 주변" : "대상 중심", true));
            }
            form.Add(BasicToggle("돌진 사용", () => recipe.dash, value => recipe.dash = value, true, true));
            if (recipe.dash)
            {
                form.Add(BasicFloat("돌진 거리(m)", () => recipe.dashDistance, value => recipe.dashDistance = value));
                form.Add(BasicFloat("돌진 시간(초)", () => recipe.dashDuration, value => recipe.dashDuration = value));
            }
            else
            {
                form.Add(BasicToggle("연타 사용", () => recipe.multiHit, value => recipe.multiHit = value, true, true));
                if (recipe.multiHit) BuildBasicMultiHit(form, recipe);
            }
        }

        private void BuildBasicRanged(VisualElement form, BasicAttackWorkshopRecipe recipe)
        {
            form.Add(BasicPopup("공격 형태", EnumValues<BasicAttackWorkshopRangedPattern>(), () => recipe.rangedPattern,
                value => recipe.rangedPattern = value, value => value == BasicAttackWorkshopRangedPattern.Projectile ? "투사체" : "즉발", true));
            if (recipe.rangedPattern == BasicAttackWorkshopRangedPattern.Instant)
            {
                form.Add(BasicToggle("연타 사용", () => recipe.multiHit, value => recipe.multiHit = value, true, true));
                if (recipe.multiHit) BuildBasicMultiHit(form, recipe);
                return;
            }
            form.Add(BasicPopup("투사체 경로", EnumValues<MonsterBasicAttackProjectileTravel>(), () => recipe.projectilePath,
                value => recipe.projectilePath = value, BasicTravelLabel, true));
            form.Add(BasicPopup("명중 방식", EnumValues<BasicAttackWorkshopProjectileImpact>(), () => recipe.projectileImpact,
                value => recipe.projectileImpact = value, BasicImpactLabel, true));
            if (recipe.projectileImpact != BasicAttackWorkshopProjectileImpact.Pierce)
                form.Add(BasicPopup("발사 형태", EnumValues<BasicAttackWorkshopVolley>(), () => recipe.volley,
                    value => recipe.volley = value, value => value == BasicAttackWorkshopVolley.Single ? "단일" : "부채꼴", true));
            if (recipe.volley == BasicAttackWorkshopVolley.Spread && recipe.projectileImpact != BasicAttackWorkshopProjectileImpact.Pierce)
            {
                form.Add(BasicInt("투사체 개수", () => recipe.projectileCount, value => recipe.projectileCount = value));
                form.Add(BasicFloat("부채꼴 각도", () => recipe.projectileSpreadAngle, value => recipe.projectileSpreadAngle = value));
            }
            if (recipe.projectileImpact == BasicAttackWorkshopProjectileImpact.Explosion)
                form.Add(BasicFloat("폭발 반경(m)", () => recipe.radius, value => recipe.radius = value));
            BuildBasicProjectileNumbers(form, recipe);
        }

        private void BuildBasicSpecial(VisualElement form, BasicAttackWorkshopRecipe recipe)
        {
            form.Add(BasicPopup("공격 형태", EnumValues<BasicAttackWorkshopSpecialPattern>(), () => recipe.specialPattern,
                value => recipe.specialPattern = value, BasicSpecialLabel, true));
            if (recipe.specialPattern == BasicAttackWorkshopSpecialPattern.Breath)
            {
                form.Add(BasicFloat("부채꼴 각도", () => recipe.angle, value => recipe.angle = value));
                form.Add(BasicInt("피해 단계", () => recipe.hitCount, value => recipe.hitCount = value));
                form.Add(BasicFloat("브레스 지속(초)", () => recipe.breathDuration, value => recipe.breathDuration = value));
            }
            else if (recipe.specialPattern == BasicAttackWorkshopSpecialPattern.Beam)
                form.Add(BasicFloat("빔 폭(m)", () => recipe.lineWidth, value => recipe.lineWidth = value));
            else
                BuildBasicProjectileNumbers(form, recipe);
            if (recipe.specialPattern == BasicAttackWorkshopSpecialPattern.ReturningProjectile)
                form.Add(BasicFloat("복귀 피해 배율", () => recipe.secondaryDamageRatio, value => recipe.secondaryDamageRatio = value));
        }

        private void BuildBasicMultiHit(VisualElement form, BasicAttackWorkshopRecipe recipe)
        {
            form.Add(BasicInt("타격 횟수", () => recipe.hitCount, value => recipe.hitCount = value));
            form.Add(BasicFloat("타격 간격(초)", () => recipe.repeatHitInterval, value => recipe.repeatHitInterval = value));
        }

        private void BuildBasicProjectileNumbers(VisualElement form, BasicAttackWorkshopRecipe recipe)
        {
            form.Add(BasicFloat("이동 속도", () => recipe.projectileSpeed, value => recipe.projectileSpeed = value));
            form.Add(BasicFloat("최대 수명(초)", () => recipe.projectileLifetime, value => recipe.projectileLifetime = value));
            form.Add(BasicFloat("충돌 반경(m)", () => recipe.projectileCollisionRadius, value => recipe.projectileCollisionRadius = value));
        }

        private void BuildBasicVfxContracts(BasicAttackWorkshopRecipe recipe)
        {
            var section = Section($"4. VFX/SFX 공간 계약 · {recipe.vfxSlots.Count}개", "실제 자산이 아니라 발생 시점·위치·수명만 정의합니다. 몬스터 고유 VFX/SFX는 Maker V2에서 연결합니다.");
            for (var index = 0; index < recipe.vfxSlots.Count; index++)
            {
                var captured = index;
                var slot = recipe.vfxSlots[index];
                var card = new VisualElement(); card.AddToClassList("sub-card");
                card.Add(CardHeader($"공간 {index + 1:00} · {slot.displayName}",
                    SmallButton("▲", () => MoveBasicSlot(captured, -1), false, index > 0), SmallButton("▼", () => MoveBasicSlot(captured, 1), false, index < recipe.vfxSlots.Count - 1),
                    SmallButton("복제", () => DuplicateBasicSlot(captured)), SmallButton("삭제", () => DeleteBasicSlot(captured), true)));
                var roleChoices = BasicAttackWorkshopVfxRoles
                    .GetCompatibleValues(basicSession.WorkingProfile, slot.editorRole)
                    .ToList();
                card.Add(BasicPopup("공간 역할", roleChoices, () => slot.editorRole,
                    value =>
                    {
                        slot.editorRole = value;
                        if (value == BasicAttackWorkshopVfxRole.Custom) slot.showAdvanced = true;
                        else BasicAttackWorkshopVfxRoles.Apply(slot, value);
                    }, BasicAttackWorkshopVfxRoles.GetLabel, false));
                card.Add(Help(BasicAttackWorkshopVfxRoles.GetGuide(slot.editorRole)));
                card.Add(BasicText("공간 ID", () => slot.slotId, value => slot.slotId = value));
                card.Add(BasicText("표시 이름", () => slot.displayName, value => slot.displayName = value));
                card.Add(BasicText("제작 메모", () => slot.description, value => slot.description = value, true));
                card.Add(BasicToggle("고급 계약 보기", () => slot.showAdvanced, value => slot.showAdvanced = value, true, false));
                if (slot.showAdvanced)
                {
                    card.Add(BasicPopup("발생 시점", EnumValues<MonsterBasicAttackVfxEvent>(), () => slot.eventType,
                        value => { slot.eventType = value; slot.editorRole = BasicAttackWorkshopVfxRoles.Resolve(slot); }, MonsterBasicAttackVfxEditorLabels.Get, false));
                    card.Add(BasicPopup("기준 위치", EnumValues<MonsterBasicAttackVfxAnchor>(), () => slot.anchor,
                        value => { slot.anchor = value; slot.editorRole = BasicAttackWorkshopVfxRoles.Resolve(slot); }, MonsterBasicAttackVfxEditorLabels.Get, false));
                    card.Add(BasicPopup("재생 횟수", EnumValues<MonsterBasicAttackVfxMultiplicity>(), () => slot.multiplicity,
                        value => { slot.multiplicity = value; slot.editorRole = BasicAttackWorkshopVfxRoles.Resolve(slot); }, MonsterBasicAttackVfxEditorLabels.Get, false));
                    card.Add(BasicPopup("몬스터 적용", EnumValues<MonsterBasicAttackVfxAssignmentScope>(), () => slot.assignmentScope,
                        value => slot.assignmentScope = value, MonsterBasicAttackVfxEditorLabels.Get, false));
                    card.Add(BasicPopup("부착 방식", EnumValues<MonsterBasicAttackVfxAttachment>(), () => slot.attachment,
                        value => { slot.attachment = value; slot.editorRole = BasicAttackWorkshopVfxRoles.Resolve(slot); }, MonsterBasicAttackVfxEditorLabels.Get, false));
                    card.Add(BasicPopup("종료 규칙", EnumValues<MonsterBasicAttackVfxEndPolicy>(), () => slot.endPolicy,
                        value => { slot.endPolicy = value; slot.editorRole = BasicAttackWorkshopVfxRoles.Resolve(slot); }, MonsterBasicAttackVfxEditorLabels.Get, false));
                    if (slot.endPolicy is MonsterBasicAttackVfxEndPolicy.Timed or
                        MonsterBasicAttackVfxEndPolicy.ParticleDuration)
                        card.Add(BasicFloat("기본 수명(초)", () => slot.defaultLifetime, value => slot.defaultLifetime = value));
                }
                section.Add(card);
            }
            var contractError = ResolveBasicVfxContractError(recipe);
            if (!string.IsNullOrWhiteSpace(contractError))
                section.Add(Help("계약 확인 필요 · " + contractError, true));
            section.Add(AddButton("+ VFX/SFX 공간 추가", AddBasicSlot));
            assemblerScroll.Add(section);
        }

        private string ResolveBasicVfxContractError(BasicAttackWorkshopRecipe recipe)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deliveryVisualCount = 0;
            for (var index = 0; index < recipe.vfxSlots.Count; index++)
            {
                var slot = recipe.vfxSlots[index];
                if (slot == null) return $"공간 {index + 1:00}이 비어 있습니다.";
                if (string.IsNullOrWhiteSpace(slot.slotId))
                    return $"공간 {index + 1:00}의 공간 ID를 입력하세요.";
                if (!ids.Add(slot.slotId.Trim()))
                    return $"공간 ID [{slot.slotId}]가 중복되었습니다.";

                var compiled = slot.Compile();
                if (!compiled.TryValidate(out _))
                    return $"공간 {index + 1:00}의 필수 값이나 수명 설정이 올바르지 않습니다.";
                if (!MonsterBasicAttackVfxCompatibility.TryValidateSlot(
                        basicSession.WorkingProfile, compiled, out _))
                    return $"공간 {index + 1:00}의 발생 시점·위치·반복 조합을 현재 공격 방식에서 사용할 수 없습니다.";
                if (compiled.IsDeliveryVisual) deliveryVisualCount++;
            }
            return deliveryVisualCount > 1 ? "이동체 외형 공간은 하나만 사용할 수 있습니다." : string.Empty;
        }

        private void BuildBasicFeel(BasicAttackWorkshopRecipe recipe)
        {
            var section = Section("5. 공통 FEEL 타격감", "모든 실제 명중에 공유할 FEEL 프로필 하나만 연결합니다.");
            var options = BasicAttackFeelPresetUtility.LoadFeelProfileOptions(recipe.impactFeelPrefab).ToList();
            var choices = options.Select(x => x.Label).ToList();
            var current = Mathf.Max(0, options.FindIndex(x => x.Profile == recipe.impactFeelPrefab));
            var popup = new PopupField<string>("FEEL 프로필", choices, current); popup.AddToClassList("editor-field");
            popup.RegisterValueChangedCallback(evt =>
            {
                var index = choices.IndexOf(evt.newValue); recipe.impactFeelPrefab = index >= 0 ? options[index].Profile : null;
                basicSession.NotifyChanged(false); RefreshState();
            });
            section.Add(popup);
            section.Add(AddButton("FEEL 연구소 열기", BasicAttackFeelPresetUtility.OpenFormalLab));
            assemblerScroll.Add(section);
        }

        private void AddBasicSlot()
        {
            var slot = new BasicAttackWorkshopVfxSlot { slotId = $"vfx_{basicSession.Recipe.vfxSlots.Count + 1:00}", displayName = "새 VFX 공간" };
            basicSession.Recipe.vfxSlots.Add(slot); basicSession.NotifyChanged(false); RebuildCurrent();
        }
        private void DeleteBasicSlot(int index) { basicSession.Recipe.vfxSlots.RemoveAt(index); basicSession.NotifyChanged(false); RebuildCurrent(); }
        private void DuplicateBasicSlot(int index)
        {
            var source = basicSession.Recipe.vfxSlots[index];
            var copy = BasicAttackWorkshopVfxSlot.From(source.Compile()); copy.slotId += "_copy";
            basicSession.Recipe.vfxSlots.Insert(index + 1, copy); basicSession.NotifyChanged(false); RebuildCurrent();
        }
        private void MoveBasicSlot(int index, int direction)
        {
            var target = Mathf.Clamp(index + direction, 0, basicSession.Recipe.vfxSlots.Count - 1); if (target == index) return;
            (basicSession.Recipe.vfxSlots[index], basicSession.Recipe.vfxSlots[target]) = (basicSession.Recipe.vfxSlots[target], basicSession.Recipe.vfxSlots[index]);
            basicSession.NotifyChanged(false); RebuildCurrent();
        }

        private TextField BasicText(string label, Func<string> get, Action<string> set, bool multiline = false)
        {
            var field = new TextField(label) { value = get(), multiline = multiline }; field.AddToClassList("editor-field");
            if (multiline) field.style.minHeight = 50f;
            field.RegisterValueChangedCallback(evt => { set(evt.newValue); basicSession.NotifyChanged(false); RefreshState(); }); return field;
        }
        private FloatField BasicFloat(string label, Func<float> get, Action<float> set)
        {
            var field = new FloatField(label) { value = get() }; field.AddToClassList("editor-field");
            field.RegisterValueChangedCallback(evt => { set(evt.newValue); basicSession.NotifyChanged(false); RefreshState(); }); return field;
        }
        private IntegerField BasicInt(string label, Func<int> get, Action<int> set)
        {
            var field = new IntegerField(label) { value = get() }; field.AddToClassList("editor-field");
            field.RegisterValueChangedCallback(evt => { set(evt.newValue); basicSession.NotifyChanged(false); RefreshState(); }); return field;
        }
        private Toggle BasicToggle(string label, Func<bool> get, Action<bool> set, bool rebuild, bool reconcile = false)
        {
            var field = new Toggle(label) { value = get() }; field.AddToClassList("editor-field");
            field.RegisterValueChangedCallback(evt => { set(evt.newValue); basicSession.NotifyChanged(reconcile); if (rebuild) ScheduleRebuild(); else RefreshState(); }); return field;
        }
        private PopupField<T> BasicPopup<T>(string label, List<T> values, Func<T> get, Action<T> set, Func<T, string> format, bool reconcile) where T : struct
        {
            var field = new PopupField<T>(label, values, get(), format, format); field.AddToClassList("editor-field");
            field.RegisterValueChangedCallback(evt => { set(evt.newValue); basicSession.NotifyChanged(reconcile); ScheduleRebuild(); }); return field;
        }
        private static List<T> EnumValues<T>() where T : struct => Enum.GetValues(typeof(T)).Cast<T>().ToList();

        private static string BasicFamilyLabel(BasicAttackWorkshopFamily value) => value switch { BasicAttackWorkshopFamily.Melee => "근접", BasicAttackWorkshopFamily.Ranged => "원거리", _ => "특수" };
        private static string BasicMeleeLabel(BasicAttackWorkshopMeleePattern value) => value switch { BasicAttackWorkshopMeleePattern.Single => "단일", BasicAttackWorkshopMeleePattern.Fan => "부채꼴", BasicAttackWorkshopMeleePattern.Line => "일자", _ => "원형" };
        private static string BasicSpecialLabel(BasicAttackWorkshopSpecialPattern value) => value switch { BasicAttackWorkshopSpecialPattern.ReturningProjectile => "왕복 투사체", BasicAttackWorkshopSpecialPattern.Breath => "브레스", BasicAttackWorkshopSpecialPattern.Beam => "관통 빔", _ => "이동 파동" };
        private static string BasicImpactLabel(BasicAttackWorkshopProjectileImpact value) => value switch { BasicAttackWorkshopProjectileImpact.StopOnFirstTarget => "첫 대상에서 종료", BasicAttackWorkshopProjectileImpact.Pierce => "관통", _ => "폭발" };
        private static string BasicTravelLabel(MonsterBasicAttackProjectileTravel value) => value switch { MonsterBasicAttackProjectileTravel.Straight => "직선", MonsterBasicAttackProjectileTravel.Homing => "유도", MonsterBasicAttackProjectileTravel.Returning => "왕복", _ => "없음" };
    }
}
