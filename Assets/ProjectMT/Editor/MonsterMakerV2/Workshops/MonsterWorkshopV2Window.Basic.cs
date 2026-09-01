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
                    value => recipe.circleCenter = value, BasicCenterLabel, true));
            }
            form.Add(BasicToggle("돌진 사용", () => recipe.dash, value => recipe.dash = value, true, true));
            if (recipe.dash)
            {
                form.Add(BasicFloat("돌진 거리(m)", () => recipe.dashDistance, value => recipe.dashDistance = value));
                form.Add(BasicFloat("도착 반동 시간(초)", () => recipe.dashDuration, value => recipe.dashDuration = value));
                form.Add(Help("돌진은 판정 시점에 즉시 위치를 옮깁니다. 이 시간은 도착 뒤 모델 반동만 조절합니다."));
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
            var section = Section(
                $"4. VFX/SFX 공간 계약 · {recipe.vfxSlots.Count}개",
                "공격 방식에서 ID·이름·발생 규칙을 자동 생성합니다. 제작자는 메모만 기록하고, 실제 자산은 Maker V2에서 연결합니다.");
            for (var index = 0; index < recipe.vfxSlots.Count; index++)
            {
                var captured = index;
                var slot = recipe.vfxSlots[index];
                var card = new VisualElement(); card.AddToClassList("sub-card");
                card.Add(CardHeader($"공간 {index + 1:00} · {slot.displayName}",
                    SmallButton("▲", () => MoveBasicSlot(captured, -1), false, index > 0),
                    SmallButton("▼", () => MoveBasicSlot(captured, 1), false,
                        index < recipe.vfxSlots.Count - 1),
                    SmallButton("복제", () => DuplicateBasicSlot(captured), false,
                        !slot.Compile().IsDeliveryVisual),
                    SmallButton("삭제", () => DeleteBasicSlot(captured), true)));
                card.Add(ContractDetails(slot));
                card.Add(BasicText(
                    "제작 메모",
                    () => slot.productionMemo,
                    value => slot.productionMemo = value,
                    true));
                section.Add(card);
            }
            var contractError = ResolveBasicVfxContractError(recipe);
            if (!string.IsNullOrWhiteSpace(contractError))
                section.Add(Help("계약 확인 필요 · " + contractError, true));
            if (basicSession.WorkingProfile.InactiveVfxSlots.Count > 0)
            {
                section.Add(Help(
                    $"현재 공격 방식에서 쓰지 않는 이전 계약 {basicSession.WorkingProfile.InactiveVfxSlots.Count}개는 " +
                    "삭제하지 않고 프리셋 내부에 보관합니다."));
            }
            section.Add(AddButton("+ VFX/SFX 공간 추가", ShowAddBasicSlotMenu));
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

        private void ShowAddBasicSlotMenu()
        {
            var roles = GetAvailableBasicSlotRoles();
            var menu = new GenericMenu();
            foreach (var role in roles)
            {
                var captured = role;
                menu.AddItem(
                    new GUIContent(BasicAttackWorkshopVfxRoles.GetLabel(role)),
                    false,
                    () => AddBasicSlotForRole(captured));
            }
            if (roles.Count == 0)
                menu.AddDisabledItem(new GUIContent("현재 공격 방식에 추가 가능한 공간 없음"));
            menu.ShowAsContext();
        }

        private List<BasicAttackWorkshopVfxRole> GetAvailableBasicSlotRoles()
        {
            var hasDeliveryVisual = basicSession.Recipe.vfxSlots.Any(slot =>
                slot != null && slot.Compile().IsDeliveryVisual);
            return BasicAttackWorkshopVfxRoles
                .GetCompatibleValues(
                    basicSession.WorkingProfile,
                    BasicAttackWorkshopVfxRole.Custom)
                .Where(role => role != BasicAttackWorkshopVfxRole.Custom)
                .Where(role => !hasDeliveryVisual || role != BasicAttackWorkshopVfxRole.DeliveryVisual)
                .ToList();
        }

        private IEnumerable<string> StoredBasicSlotIds()
        {
            return basicSession.Recipe.vfxSlots
                .Where(slot => slot != null)
                .Select(slot => slot.slotId)
                .Concat(basicSession.WorkingProfile.InactiveVfxSlots
                    .Where(slot => slot != null)
                    .Select(slot => slot.SlotId));
        }

        private void AddBasicSlot()
        {
            var role = GetAvailableBasicSlotRoles().FirstOrDefault();
            if (role != BasicAttackWorkshopVfxRole.Custom) AddBasicSlotForRole(role);
        }

        private void AddBasicSlotForRole(BasicAttackWorkshopVfxRole role)
        {
            if (role == BasicAttackWorkshopVfxRole.Custom) return;
            var activeIds = new HashSet<string>(
                basicSession.Recipe.vfxSlots
                    .Where(slot => slot != null)
                    .Select(slot => slot.slotId),
                StringComparer.OrdinalIgnoreCase);
            var archived = basicSession.WorkingProfile.InactiveVfxSlots
                .LastOrDefault(slot =>
                    slot != null &&
                    !activeIds.Contains(slot.SlotId) &&
                    BasicAttackWorkshopVfxRoles.Resolve(slot) == role);
            var slot = archived != null
                ? BasicAttackWorkshopVfxSlot.From(archived)
                : new BasicAttackWorkshopVfxSlot
                {
                    slotId = CreateAutomaticContractId(StoredBasicSlotIds(), role)
                };
            slot.editorRole = role;
            BasicAttackWorkshopVfxRoles.Apply(slot, role);
            basicSession.Recipe.vfxSlots.Add(slot);
            basicSession.NotifyChanged(false);
            RebuildCurrent();
        }

        private void DeleteBasicSlot(int index)
        {
            if (index < 0 || index >= basicSession.Recipe.vfxSlots.Count) return;
            basicSession.Recipe.vfxSlots.RemoveAt(index);
            basicSession.NotifyChanged(false);
            RebuildCurrent();
        }

        private void DuplicateBasicSlot(int index)
        {
            if (index < 0 || index >= basicSession.Recipe.vfxSlots.Count) return;
            var source = basicSession.Recipe.vfxSlots[index];
            if (source == null || source.Compile().IsDeliveryVisual) return;
            var copy = BasicAttackWorkshopVfxSlot.From(source.Compile());
            copy.slotId = CreateUniqueContractId(StoredBasicSlotIds(), source.slotId + "_copy");
            basicSession.Recipe.vfxSlots.Insert(index + 1, copy);
            basicSession.NotifyChanged(false);
            RebuildCurrent();
        }

        private void MoveBasicSlot(int index, int direction)
        {
            if (index < 0 || index >= basicSession.Recipe.vfxSlots.Count) return;
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
        private static string BasicCenterLabel(MonsterBasicAttackCenter value) => value switch
        {
            MonsterBasicAttackCenter.Source => "내 주변 원형",
            MonsterBasicAttackCenter.Forward => "내 앞 원형",
            _ => "대상 중심 원형"
        };
        private static string BasicSpecialLabel(BasicAttackWorkshopSpecialPattern value) => value switch { BasicAttackWorkshopSpecialPattern.ReturningProjectile => "왕복 투사체", BasicAttackWorkshopSpecialPattern.Breath => "브레스", BasicAttackWorkshopSpecialPattern.Beam => "관통 빔", _ => "이동 파동" };
        private static string BasicImpactLabel(BasicAttackWorkshopProjectileImpact value) => value switch { BasicAttackWorkshopProjectileImpact.StopOnFirstTarget => "첫 대상에서 종료", BasicAttackWorkshopProjectileImpact.Pierce => "관통", _ => "폭발" };
        private static string BasicTravelLabel(MonsterBasicAttackProjectileTravel value) => value switch { MonsterBasicAttackProjectileTravel.Straight => "직선", MonsterBasicAttackProjectileTravel.Homing => "유도", MonsterBasicAttackProjectileTravel.Returning => "왕복", _ => "없음" };
    }
}
