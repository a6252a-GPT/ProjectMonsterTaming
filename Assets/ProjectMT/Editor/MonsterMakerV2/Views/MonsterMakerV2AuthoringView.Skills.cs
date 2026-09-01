using System;
using System.Linq;
using ProjectMT.EditorTools.MonsterMaker;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    internal sealed partial class MonsterMakerV2AuthoringView
    {
        private enum SkillAuthoringTab
        {
            Passive,
            Active
        }

        private SkillAuthoringTab selectedSkillTab;

        private void BuildSkills()
        {
            var container = Section("skills");
            BuildSkillTabs(container);
            if (selectedSkillTab == SkillAuthoringTab.Passive)
            {
                BuildPassiveSkillTab(container);
                return;
            }

            BuildActiveSkillTab(container);
        }

        private void BuildSkillTabs(VisualElement container)
        {
            var row = new VisualElement { name = "skill-tab-row" };
            row.AddToClassList("skill-tab-row");
            AddSkillTabButton(row, "패시브", SkillAuthoringTab.Passive, "skill-tab-passive");
            AddSkillTabButton(row, "액티브", SkillAuthoringTab.Active, "skill-tab-active");
            container.Add(row);
        }

        private void AddSkillTabButton(
            VisualElement row,
            string label,
            SkillAuthoringTab tab,
            string name)
        {
            var button = new Button(() =>
            {
                if (selectedSkillTab == tab)
                {
                    return;
                }

                selectedSkillTab = tab;
                Rebuild();
            })
            {
                name = name,
                text = label
            };
            button.AddToClassList("skill-tab-button");
            if (selectedSkillTab == tab)
            {
                button.AddToClassList("skill-tab-button--active");
            }
            row.Add(button);
        }

        private void BuildPassiveSkillTab(VisualElement container)
        {
            AddProperty(container, "usePassiveSkill", "패시브 사용");
            if (!draft.UsePassiveSkill)
            {
                AddHelp(
                    container,
                    draft.RarityPassiveSkill == null
                        ? "패시브를 사용하지 않습니다. 사용을 켜면 저장된 패시브를 선택할 수 있습니다."
                        : "패시브 연결과 몬스터별 밸런스 값은 보존되며 전투에는 반영하지 않습니다.",
                    HelpBoxMessageType.Info);
                return;
            }

            var passiveSkill = serializedDraft.FindProperty("rarityPassiveSkill")?
                .objectReferenceValue as GenericMonsterPassiveSkill;
            AddSummary(
                container,
                passiveSkill == null
                    ? "패시브 · 선택되지 않음"
                    : $"패시브 · [{passiveSkill.SkillId}] {passiveSkill.DisplayName}",
                passiveSkill == null
                    ? "저장된 패시브를 선택하면 몬스터 전용 밸런스 항목이 열립니다."
                    : passiveSkill.Description);
            AddActionRow(
                container,
                (passiveSkill == null ? "패시브 선택" : "패시브 변경",
                    ShowPassivePresetMenu, "draft-action-button"));
            var passive = AddSubFoldout(container, "몬스터 전용 패시브 밸런스", false);
            BuildPassiveTuning(passive);
        }

        private void BuildActiveSkillTab(VisualElement container)
        {
            var rarity = (MonsterRarity)(
                serializedDraft.FindProperty("rarity")?.enumValueIndex ?? 0);
            var activeProperty = serializedDraft.FindProperty("rarityActiveSkill");
            var profileProperty = serializedDraft.FindProperty("activeAttackProfile");
            var effectProperty = serializedDraft.FindProperty("activeEffectProfile");
            var useActive = draft.UseActiveSkill;
            var activeUsageField = AddProperty(container, "useActiveSkill", "액티브 사용");

            if (rarity < MonsterRarity.Legendary)
            {
                activeUsageField?.SetEnabled(useActive);
                if (useActive)
                {
                    AddHelp(
                        container,
                        "일반·희귀·영웅 등급은 액티브를 사용할 수 없습니다. 액티브 사용을 끄세요.",
                        HelpBoxMessageType.Error);
                }
                else
                {
                    AddHelp(
                        container,
                        "액티브는 전설·신화 등급에서만 사용할 수 있습니다.",
                        HelpBoxMessageType.Info);
                }

                if (activeProperty?.objectReferenceValue != null ||
                    profileProperty?.objectReferenceValue != null ||
                    effectProperty?.objectReferenceValue != null)
                {
                    AddHelp(
                        container,
                        "과거 액티브 연결은 보관 중이며 현재 전투에는 반영하지 않습니다.",
                        HelpBoxMessageType.Warning);
                    AddActionRow(
                        container,
                        ("보관된 액티브 연결 제거", RemoveActiveAssignment, "danger-button"));
                }

                return;
            }

            if (!useActive)
            {
                AddHelp(
                    container,
                    profileProperty?.objectReferenceValue == null &&
                    effectProperty?.objectReferenceValue == null
                        ? "액티브를 사용하지 않습니다. 사용을 켜면 프리셋 선택과 조립소 기능이 열립니다."
                        : "액티브 프리셋·모션·VFX/SFX 값은 보존되며 전투에는 반영하지 않습니다.",
                    HelpBoxMessageType.Info);
                return;
            }

            AddHelp(
                container,
                "발동·기력 흐름은 동일합니다. 선택한 프리셋에 따라 공격형 또는 " +
                "지원·수호·디버프 효과형 제작 항목으로 자동 전환합니다.",
                HelpBoxMessageType.Info);
            var hasActiveAssignment = profileProperty?.objectReferenceValue != null ||
                                      effectProperty?.objectReferenceValue != null;
            if (hasActiveAssignment)
            {
                AddActionRow(
                    container,
                    ("액티브 스킬 변경", ShowActivePresetMenu, "draft-action-button"),
                    ("액티브 연결 제거", RemoveActiveAssignment, "danger-button"));
            }
            else
            {
                AddActionRow(
                    container,
                    ("액티브 스킬 선택", ShowActivePresetMenu, "draft-action-button"));
            }

            if (effectProperty?.objectReferenceValue != null)
            {
                BuildEffectActive(container, effectProperty, rarity);
            }
            else
            {
                BuildAttackActive(container, profileProperty, rarity);
            }

        }
        private void BuildAttackActive(
            VisualElement activeArea,
            SerializedProperty profileProperty,
            MonsterRarity rarity)
        {
            AddActionRow(
                activeArea,
                ("공격형 조립소 열기", () => openActiveWorkshop(false), "draft-action-button"));

            var profile = profileProperty?.objectReferenceValue as MonsterActiveAttackProfile;
            if (profile == null)
            {
                AddHelp(
                    activeArea,
                    "저장된 공격형 프리셋을 선택하거나 조립소에서 새 프리셋을 만드세요.",
                    HelpBoxMessageType.Warning);
                AddProperty(activeArea, "rarityActiveSkill", "기존 액티브 에셋");
                return;
            }

            AddSummary(
                activeArea,
                $"현재 액티브 · [{profile.ProfileId}] {profile.DisplayName}",
                BuildAttackActiveSummary(profile));
            AddProperty(activeArea, "activeSkillName", "몬스터 고유 스킬 이름");
            AddProperty(activeArea, "activeEnergyMaximum", "최대 기력");
            AddHelp(
                activeArea,
                $"공용 획득 · 초당 {MonsterActiveEnergyConfig.SharedEnergyPerSecond:0.#} / " +
                $"기본공격당 {MonsterActiveEnergyConfig.SharedEnergyPerBasicAttack:0.#} · " +
                "몬스터별 밸런스는 최대 기력으로 조정합니다.",
                HelpBoxMessageType.Info);
            BuildActiveRuntimeSync(activeArea);

            AddHelp(
                activeArea,
                "피해·범위·타이밍·Step 전체 속도는 공격형 프리셋이 단독 소유합니다. " +
                "다른 수치가 필요하면 조립소에서 프리셋을 복제해 새 원본으로 만드세요.",
                HelpBoxMessageType.Info);

            BuildActiveMotions(activeArea, profile);
            BuildActivePresentations(activeArea, profile);

            var generated = AddProperty(activeArea, "rarityActiveSkill", "생성된 액티브 에셋");
            generated?.SetEnabled(false);
            AddHelp(
                activeArea,
                rarity == MonsterRarity.Mythic
                    ? "신화 전용 에셋으로 생성됩니다."
                    : "전설 공격 액티브로 생성되며 신화 전용 실행기와 구분됩니다.",
                HelpBoxMessageType.Info);
        }

        private void BuildEffectActive(
            VisualElement activeArea,
            SerializedProperty profileProperty,
            MonsterRarity rarity)
        {
            AddActionRow(
                activeArea,
                ("효과형 조립소 열기", () => openActiveWorkshop(true), "draft-action-button"));

            var profile = profileProperty?.objectReferenceValue as MonsterEffectActiveProfile;
            if (profile == null)
            {
                AddHelp(
                    activeArea,
                    "지원·수호·디버프 중 원하는 효과형 프리셋을 선택하거나 조립소에서 만드세요.",
                    HelpBoxMessageType.Warning);
                AddProperty(activeArea, "rarityActiveSkill", "기존 액티브 에셋");
                return;
            }

            AddSummary(
                activeArea,
                $"현재 액티브 · [{GetEffectRoleLabel(profile.Role)}] " +
                $"[{profile.ProfileId}] {profile.DisplayName}",
                BuildEffectActiveSummary(profile));
            AddProperty(activeArea, "activeSkillName", "몬스터 고유 스킬 이름");
            AddProperty(activeArea, "activeEnergyMaximum", "최대 기력");
            AddHelp(
                activeArea,
                $"공용 획득 · 초당 {MonsterActiveEnergyConfig.SharedEnergyPerSecond:0.#} / " +
                $"기본공격당 {MonsterActiveEnergyConfig.SharedEnergyPerBasicAttack:0.#} · " +
                "몬스터별 밸런스는 최대 기력으로 조정합니다.",
                HelpBoxMessageType.Info);

            BuildEffectActiveMotions(activeArea, profile);
            BuildEffectActivePresentations(activeArea, profile);

            var generated = AddProperty(activeArea, "rarityActiveSkill", "생성된 액티브 에셋");
            generated?.SetEnabled(false);
            AddHelp(
                activeArea,
                rarity == MonsterRarity.Mythic
                    ? "신화 전용 효과형 액티브 에셋으로 생성됩니다."
                    : "전설 효과형 액티브 에셋으로 생성됩니다.",
                HelpBoxMessageType.Info);
        }

        private void BuildActiveRuntimeSync(VisualElement container)
        {
            if (draft?.ActiveAttackProfile == null || string.IsNullOrWhiteSpace(draft.MonsterId))
            {
                return;
            }

            if (sourceDraft?.ActiveAttackProfile == null)
            {
                AddHelp(
                    container,
                    "현재 액티브는 작업 사본에만 있습니다. 상단 전투 반영 시 게임 자산으로 생성됩니다.",
                    HelpBoxMessageType.Info);
                AddActionRow(
                    container,
                    ("저장하고 공격 액티브 게임 자산 갱신",
                        syncActiveRuntime, "draft-action-button"));
                return;
            }

            var paths = MonsterMakerAssetWriter.BuildPaths(sourceDraft.MonsterId);
            var active = AssetDatabase.LoadAssetAtPath<MonsterAttackActiveSkill>(
                MonsterMakerAssetWriter.BuildActivePath(sourceDraft.MonsterId));
            var motion = AssetDatabase.LoadAssetAtPath<MonsterMotionProfile>(paths[2]);
            var runtimeState = MonsterActiveAttackBindingProjection.EvaluateRuntimeSync(
                sourceDraft,
                active,
                motion,
                out var message);
            if (runtimeState == MonsterActiveAttackRuntimeSyncState.Synchronized)
            {
                AddHelp(
                    container,
                    "저장된 액티브 게임 자산 최신 · 작업 중 변경은 상단 전투 반영 시 함께 적용됩니다.",
                    HelpBoxMessageType.Info);
                return;
            }

            AddHelp(
                container,
                $"저장된 액티브 게임 자산 미반영 · {message}\n" +
                "상단 전투 반영으로 저장 원본과 게임 자산을 함께 갱신하세요.",
                HelpBoxMessageType.Warning);

            var preflight = MonsterMakerValidator.ValidateActiveAttack(draft);
            var errors = preflight.Issues
                .Where(issue => issue.Severity == MonsterMakerIssueSeverity.Error)
                .ToArray();
            if (errors.Length > 0)
            {
                var visibleErrors = errors
                    .Take(2)
                    .Select(issue => $"• {issue.Message}");
                var remaining = errors.Length > 2
                    ? $"\n• 그 외 {errors.Length - 2}개 · 버튼을 누르면 하단에 모두 표시합니다."
                    : string.Empty;
                AddHelp(
                    container,
                    $"게임 자산 갱신 전 설정 필요 · 오류 {errors.Length}개\n" +
                    string.Join("\n", visibleErrors) + remaining,
                    HelpBoxMessageType.Warning);
            }

            AddActionRow(
                container,
                ("저장하고 공격 액티브 게임 자산 갱신",
                    syncActiveRuntime, "draft-action-button"));
        }

        private static string BuildAttackActiveSummary(MonsterActiveAttackProfile profile)
        {
            var summary =
                $"공격 Step {profile.Steps.Count}개 · 예상 실행 {profile.EstimateDuration():0.##}초 · " +
                "수치는 프리셋 원본";
            return string.IsNullOrWhiteSpace(profile.Description)
                ? summary + "\n기획 메모 · 작성되지 않음"
                : summary + "\n기획 메모 · " + profile.Description;
        }

        private static string BuildEffectActiveSummary(MonsterEffectActiveProfile profile)
        {
            var summary = $"효과 묶음 {profile.Groups.Count}개 · 몬스터별 이름·기력·모션·연출 연결";
            return string.IsNullOrWhiteSpace(profile.Description)
                ? summary + "\n기획 메모 · 작성되지 않음"
                : summary + "\n기획 메모 · " + profile.Description;
        }

        private void BuildActiveMotions(
            VisualElement container,
            MonsterActiveAttackProfile profile)
        {
            var presentations = serializedDraft.FindProperty("activeAttackPresentations");
            var useCustom = serializedDraft.FindProperty("useCustomActiveStepMotions");
            var foldout = AddSubFoldout(
                container,
                useCustom?.boolValue == true
                    ? $"고급 · 액티브 스킬 모션 · 전용 {presentations?.arraySize ?? 0}개"
                    : "고급 · 액티브 스킬 모션 · 기본 공격 사용",
                false);
            AddProperty(foldout, "useCustomActiveStepMotions", "Step별 전용 공격 모션 사용");
            if (useCustom?.boolValue != true)
            {
                AddHelp(
                    foldout,
                    "기본 설정입니다. 모든 Step이 기본 공격 01의 모션·Clip 보정 속도·첫 판정 시점을 사용합니다. " +
                    "모션 전환 보간만 액티브 Step별 독립값이며, Step 전체 속도 배율은 선택한 공격형 프리셋 값이 추가로 적용됩니다.",
                    HelpBoxMessageType.Info);
            }

            if (presentations == null || presentations.arraySize != profile.Steps.Count)
            {
                AddHelp(
                    foldout,
                    "프로필 Step 자동 동기화가 완료되지 않았습니다. 액티브 프리셋을 다시 선택하세요.",
                    HelpBoxMessageType.Error);
                return;
            }

            if (presentations.arraySize > 1)
            {
                AddActionRow(
                    foldout,
                    (useCustom?.boolValue == true
                            ? "1번 모션 설정을 전체 Step에 적용"
                            : "1번 전환 시간을 전체 Step에 적용",
                        useCustom?.boolValue == true ? CopyFirstActiveMotion : CopyFirstActiveMotionFade,
                        "draft-action-button"));
            }

            for (var index = 0; index < presentations.arraySize; index++)
            {
                var step = profile.Steps[index];
                var item = presentations.GetArrayElementAtIndex(index);
                var card = AddSubFoldout(
                    foldout,
                    $"Step {index + 1:00} · {step.DisplayName}",
                    index == 0);
                if (useCustom?.boolValue == true)
                {
                    AddRelativeProperty(card, item.FindPropertyRelative("motionClip"), "공격 모션 Clip");
                    AddRelativeProperty(card, item.FindPropertyRelative("motionPlaybackSpeed"), "Clip 원본 보정 속도");
                }
                AddRelativeProperty(card, item.FindPropertyRelative("motionCrossFadeDuration"), "전환 시간(초)");
                if (useCustom?.boolValue == true)
                {
                    AddRelativeProperty(card, item.FindPropertyRelative("motionCommitNormalizedTime"), "판정 시작 시점(0~1)");
                }
            }
        }

        private void BuildActivePresentations(
            VisualElement container,
            MonsterActiveAttackProfile profile)
        {
            var presentations = serializedDraft.FindProperty("activeAttackPresentations");
            var foldout = AddSubFoldout(
                container,
                "몬스터 고유 공격형 액티브 VFX/SFX",
                true,
                "active-attack-vfx-root");
            AddHelp(
                foldout,
                "각 Step은 기본공격과 같은 계약 체결 카드를 사용하지만, 배정값과 전용 래퍼는 " +
                "액티브 전용으로 따로 저장됩니다.",
                HelpBoxMessageType.Info);
            if (presentations == null || presentations.arraySize != profile.Steps.Count)
            {
                AddHelp(
                    foldout,
                    "프로필 Step 자동 동기화가 완료되지 않았습니다. 액티브 프리셋을 다시 선택하세요.",
                    HelpBoxMessageType.Error);
                AddActionRow(
                    foldout,
                    ("액티브 연출 공간 다시 동기화", SyncActiveAttackVfxBindings,
                        "draft-action-button"));
                return;
            }

            for (var index = 0; index < presentations.arraySize; index++)
            {
                var source = profile.Steps[index];
                var presentation = presentations.GetArrayElementAtIndex(index);
                var bindings = presentation.FindPropertyRelative("attackBlockBindings");
                var rows = ResolveActiveVfxRows(source, bindings);
                var expected = source.AttackBlockVfxSlots.Count;
                var step = AddSubFoldout(
                    foldout,
                    $"Step {index + 1:00} · {source.DisplayName} · 공간 {expected}개",
                    index == 0,
                    $"active-attack-vfx-step-{source.StepId}");
                if (expected == 0)
                {
                    AddHelp(
                        step,
                        "이 Step의 공용 공격 블록 계약이 비어 있습니다. 액티브 조립소에서 공격 형태를 다시 선택해 자동 복구하세요.",
                        HelpBoxMessageType.Error);
                    continue;
                }
                if (rows.Count != expected)
                {
                    AddHelp(
                        step,
                        $"연출 연결 데이터가 부족합니다. 현재 {rows.Count}개 / 필요 {expected}개",
                        HelpBoxMessageType.Error);
                    AddActionRow(
                        step,
                        ("액티브 연출 공간 다시 동기화", SyncActiveAttackVfxBindings,
                            "draft-action-button"));
                }

                var vfxDecided = rows.Count(row =>
                    (MonsterBasicAttackVfxAssignmentState)row.Binding
                        .FindPropertyRelative("state").enumValueIndex !=
                    MonsterBasicAttackVfxAssignmentState.Undecided);
                var sfxDecided = rows.Count(row =>
                    (MonsterBasicAttackSfxAssignmentState)row.Binding
                        .FindPropertyRelative("sfxState").enumValueIndex !=
                    MonsterBasicAttackSfxAssignmentState.Undecided);
                var progress = new ProgressBar
                {
                    title = $"VFX 결정 {vfxDecided}/{expected} · SFX 결정 {sfxDecided}/{expected}",
                    value = expected > 0
                        ? (vfxDecided + sfxDecided) * 100f / (expected * 2f)
                        : 0f
                };
                progress.style.height = 20f;
                progress.style.marginBottom = 5f;
                step.Add(progress);

                for (var slotIndex = 0; slotIndex < rows.Count; slotIndex++)
                {
                    BuildBasicVfxCard(
                        step,
                        rows[slotIndex],
                        slotIndex,
                        "공격형 액티브",
                        $"active-attack-vfx-{source.StepId}-{rows[slotIndex].Slot.SlotId}");
                }
                var inactive = presentation.FindPropertyRelative("inactiveAttackBlockBindings");
                if (inactive != null && inactive.arraySize > 0)
                {
                    AddHelp(
                        step,
                        $"현재 공격 형태에서 사용하지 않는 이전 액티브 연결 {inactive.arraySize}개를 보관 중입니다. " +
                        "형태를 되돌리면 복원됩니다.",
                        HelpBoxMessageType.Info);
                }
            }
        }

        private void SyncActiveAttackVfxBindings()
        {
            ApplyObjectMutationAndRebuild(
                "Monster Maker V2 · 공격형 액티브 연출 공간 동기화",
                () => draft?.EditorSyncActiveAttackAuthoring());
        }

        private void BuildEffectActiveMotions(
            VisualElement container,
            MonsterEffectActiveProfile profile)
        {
            var presentations = serializedDraft.FindProperty("activeEffectPresentations");
            var useCustom = serializedDraft.FindProperty("useCustomActiveStepMotions");
            var foldout = AddSubFoldout(
                container,
                useCustom?.boolValue == true
                    ? $"고급 · 액티브 스킬 모션 · 전용 {presentations?.arraySize ?? 0}개"
                    : "고급 · 액티브 스킬 모션 · 기본 공격 사용",
                false);
            AddProperty(foldout, "useCustomActiveStepMotions", "묶음별 전용 공격 모션 사용");
            if (useCustom?.boolValue != true)
            {
                AddHelp(
                    foldout,
                    "기본 설정입니다. 모든 효과 묶음이 기본 공격 01의 모션과 판정 시점을 사용합니다.",
                    HelpBoxMessageType.Info);
                return;
            }

            if (presentations == null || presentations.arraySize != profile.Groups.Count)
            {
                AddHelp(
                    foldout,
                    "프로필 효과 묶음 자동 동기화가 완료되지 않았습니다. 액티브 프리셋을 다시 선택하세요.",
                    HelpBoxMessageType.Error);
                return;
            }

            if (presentations.arraySize > 1)
            {
                AddActionRow(
                    foldout,
                    ("1번 모션 설정을 전체 묶음에 적용", CopyFirstActiveMotion,
                        "draft-action-button"));
            }

            for (var index = 0; index < presentations.arraySize; index++)
            {
                var group = profile.Groups[index];
                var item = presentations.GetArrayElementAtIndex(index);
                var card = AddSubFoldout(
                    foldout,
                    $"#{index + 1:00} {group.DisplayName}",
                    index == 0);
                AddRelativeProperty(card, item.FindPropertyRelative("motionClip"), "공격 모션 Clip");
                AddRelativeProperty(card, item.FindPropertyRelative("motionPlaybackSpeed"), "재생 속도");
                AddRelativeProperty(card, item.FindPropertyRelative("motionCrossFadeDuration"), "전환 시간(초)");
                AddRelativeProperty(card, item.FindPropertyRelative("motionCommitNormalizedTime"), "판정 시작 시점(0~1)");
            }
        }

        private void BuildEffectActivePresentations(
            VisualElement container,
            MonsterEffectActiveProfile profile)
        {
            var presentations = serializedDraft.FindProperty("activeEffectPresentations");
            var foldout = AddSubFoldout(
                container,
                $"묶음별 VFX/SFX 연결 · {presentations?.arraySize ?? 0}개",
                false);
            AddHelp(
                foldout,
                "효과형 조립소에서 만든 공간 계약만 표시합니다. 실제 VFX/SFX는 몬스터별로 연결합니다.",
                HelpBoxMessageType.Info);
            if (presentations == null || presentations.arraySize != profile.Groups.Count)
            {
                AddHelp(
                    foldout,
                    "프로필 효과 묶음 자동 동기화가 완료되지 않았습니다. 액티브 프리셋을 다시 선택하세요.",
                    HelpBoxMessageType.Error);
                return;
            }

            for (var index = 0; index < presentations.arraySize; index++)
            {
                var source = profile.Groups[index];
                var presentation = presentations.GetArrayElementAtIndex(index);
                var slots = presentation.FindPropertyRelative("slots");
                var group = AddSubFoldout(
                    foldout,
                    $"#{index + 1:00} {source.DisplayName} · 공간 {source.PresentationSlots.Count}개",
                    index == 0);
                if (source.PresentationSlots.Count == 0)
                {
                    AddHelp(
                        group,
                        "이 묶음에는 VFX/SFX 공간 계약이 없습니다. 효과형 조립소에서 추가할 수 있습니다.",
                        HelpBoxMessageType.Info);
                    continue;
                }

                for (var slotIndex = 0;
                     slotIndex < source.PresentationSlots.Count && slotIndex < slots.arraySize;
                     slotIndex++)
                {
                    var contract = source.PresentationSlots[slotIndex];
                    var slot = slots.GetArrayElementAtIndex(slotIndex);
                    var slotCard = AddSubFoldout(
                        group,
                        $"{slotIndex + 1:00} · {contract.DisplayName}",
                        false);
                    AddHelp(
                        slotCard,
                        $"{contract.Timing} · {contract.Anchor}" +
                        (string.IsNullOrWhiteSpace(contract.Description)
                            ? string.Empty
                            : " · " + contract.Description),
                        HelpBoxMessageType.Info);
                    BuildFeedbackEditor(
                        slotCard,
                        slot.FindPropertyRelative("feedback"),
                        contract.DisplayName,
                        "원본 AudioClip",
                        "효과형 조립소의 공간 계약에 연결되는 몬스터 전용 연출입니다.",
                        ResolveActivePresentationAnchor(contract.Anchor),
                        true);
                }
            }
        }

        private static string GetEffectRoleLabel(MonsterEffectActiveRole role) => role switch
        {
            MonsterEffectActiveRole.Support => "지원",
            MonsterEffectActiveRole.Guard => "수호",
            MonsterEffectActiveRole.Debuff => "디버프",
            _ => role.ToString()
        };

        private static MonsterMakerPreviewAnchor ResolveActivePresentationAnchor(
            MonsterActivePresentationAnchor anchor)
        {
            return anchor switch
            {
                MonsterActivePresentationAnchor.AttackOrigin => MonsterMakerPreviewAnchor.AttackOrigin,
                MonsterActivePresentationAnchor.TargetPoint => MonsterMakerPreviewAnchor.HitCenter,
                _ => MonsterMakerPreviewAnchor.Root
            };
        }

        private void BuildPassiveTuning(VisualElement container)
        {
            var template = serializedDraft.FindProperty("rarityPassiveSkill")?
                .objectReferenceValue as GenericMonsterPassiveSkill;
            var tuning = serializedDraft.FindProperty("passiveTuning");
            if (template == null || tuning == null)
            {
                AddHelp(
                    container,
                    "패시브 종류를 선택하면 이 몬스터 전용 수치만 표시됩니다.",
                    HelpBoxMessageType.Info);
                return;
            }

            var initialized = tuning.FindPropertyRelative("initialized").boolValue;
            var kind = (GenericMonsterPassiveRuntimeKind)
                tuning.FindPropertyRelative("runtimeKind").enumValueIndex;
            if (!initialized || kind != template.RuntimeKind)
            {
                AddHelp(
                    container,
                    "선택한 패시브와 전용 수치가 맞지 않습니다. 템플릿 값으로 동기화해 주세요.",
                    HelpBoxMessageType.Error);
                AddActionRow(
                    container,
                    ("패시브 템플릿 값 동기화", RestorePassiveTemplate, "draft-action-button"));
                return;
            }

            AddSummary(
                container,
                $"{template.DisplayName} · {BuildPassiveLevelSummary(tuning, kind)}",
                ResolvePassiveRule(kind));
            AddHelp(
                container,
                "여기서 바꾼 수치는 현재 몬스터의 작업 사본에만 저장됩니다.",
                HelpBoxMessageType.Info);
            AddPercentField(container, tuning.FindPropertyRelative("primaryBase"), ResolvePrimaryLabel(kind));
            AddPercentField(container, tuning.FindPropertyRelative("primaryPerLevelStep"), "20레벨마다 증가");

            if (UsesPassiveSecondary(kind))
            {
                AddPercentField(container, tuning.FindPropertyRelative("secondaryBase"), ResolveSecondaryLabel(kind));
                AddPercentField(container, tuning.FindPropertyRelative("secondaryPerLevelStep"), "보조 효과 20레벨마다 증가");
            }
            if (UsesPassiveTriggerCount(kind))
            {
                AddRelativeProperty(container, tuning.FindPropertyRelative("triggerCount"), "몇 번째 공격마다");
            }
            if (kind == GenericMonsterPassiveRuntimeKind.SameTargetHaste)
            {
                AddRelativeProperty(container, tuning.FindPropertyRelative("maxStacks"), "최대 가속 중첩");
            }
            if (UsesPassiveDuration(kind))
            {
                AddRelativeProperty(
                    container,
                    tuning.FindPropertyRelative("duration"),
                    kind == GenericMonsterPassiveRuntimeKind.ImpactStrike
                        ? "일반 적 경직 시간 (초)"
                        : "효과 지속시간 (초)");
            }
            if (kind is GenericMonsterPassiveRuntimeKind.CrisisDefense or
                GenericMonsterPassiveRuntimeKind.KillHeal)
            {
                AddRelativeProperty(container, tuning.FindPropertyRelative("cooldown"), "다시 발동하기까지 (초)");
            }
            if (kind is GenericMonsterPassiveRuntimeKind.LowHealthHunter or
                GenericMonsterPassiveRuntimeKind.CrisisDefense)
            {
                AddPercentField(container, tuning.FindPropertyRelative("threshold"), "발동 체력 기준");
            }
            else if (kind == GenericMonsterPassiveRuntimeKind.LongRangeAim)
            {
                AddRelativeProperty(container, tuning.FindPropertyRelative("threshold"), "효과가 켜지는 거리 (m)");
            }
            if (kind == GenericMonsterPassiveRuntimeKind.FrontlineBond)
            {
                AddRelativeProperty(container, tuning.FindPropertyRelative("radius"), "아군을 확인할 거리 (m)");
            }

            AddActionRow(
                container,
                ("이 패시브의 기본값으로 되돌리기", RestorePassiveTemplate, "draft-action-button"));
        }

        private void AddPercentField(
            VisualElement container,
            SerializedProperty property,
            string label)
        {
            var path = property.propertyPath;
            var field = new FloatField(label + " (%)") { value = Mathf.Max(0f, property.floatValue) * 100f };
            field.AddToClassList("draft-property");
            field.RegisterValueChangedCallback(evt =>
            {
                var percent = float.IsFinite(evt.newValue) ? Mathf.Max(0f, evt.newValue) : 0f;
                field.SetValueWithoutNotify(percent);
                SetFloatProperty(path, percent / 100f, "Monster Maker V2 · 패시브 수치 입력");
            });
            container.Add(field);
        }

        private void RestorePassiveTemplate()
        {
            ApplyAndRebuild(
                "Monster Maker V2 · 패시브 기본값 복원",
                () =>
                {
                    var template = serializedDraft.FindProperty("rarityPassiveSkill")?
                        .objectReferenceValue as GenericMonsterPassiveSkill;
                    MonsterPassiveBalanceEditor.EnsureInitialized(
                        serializedDraft.FindProperty("passiveTuning"),
                        template,
                        true);
                });
        }

        private static string BuildPassiveLevelSummary(
            SerializedProperty tuning,
            GenericMonsterPassiveRuntimeKind kind)
        {
            var baseValue = tuning.FindPropertyRelative("primaryBase").floatValue;
            var step = tuning.FindPropertyRelative("primaryPerLevelStep").floatValue;
            var summary = $"Lv1 {baseValue * 100f:0.##}% → Lv200 {(baseValue + step * 10f) * 100f:0.##}%";
            if (UsesPassiveTriggerCount(kind))
            {
                summary += $" · {tuning.FindPropertyRelative("triggerCount").intValue}번째 공격마다";
            }
            return summary;
        }

        private static bool UsesPassiveSecondary(GenericMonsterPassiveRuntimeKind kind) =>
            kind is GenericMonsterPassiveRuntimeKind.ThreatMark or
                GenericMonsterPassiveRuntimeKind.EmergencyEntry;

        private static bool UsesPassiveTriggerCount(GenericMonsterPassiveRuntimeKind kind) =>
            kind is GenericMonsterPassiveRuntimeKind.RhythmPower or
                GenericMonsterPassiveRuntimeKind.ImpactStrike or
                GenericMonsterPassiveRuntimeKind.FractureMark or
                GenericMonsterPassiveRuntimeKind.HealingShot;

        private static bool UsesPassiveDuration(GenericMonsterPassiveRuntimeKind kind) =>
            kind is GenericMonsterPassiveRuntimeKind.SameTargetHaste or
                GenericMonsterPassiveRuntimeKind.ImpactStrike or
                GenericMonsterPassiveRuntimeKind.CrisisDefense or
                GenericMonsterPassiveRuntimeKind.FractureMark or
                GenericMonsterPassiveRuntimeKind.ThreatMark or
                GenericMonsterPassiveRuntimeKind.EmergencyEntry or
                GenericMonsterPassiveRuntimeKind.FirstWave;

        private static string ResolvePrimaryLabel(GenericMonsterPassiveRuntimeKind kind) => kind switch
        {
            GenericMonsterPassiveRuntimeKind.RhythmPower => "강화 공격 추가 피해",
            GenericMonsterPassiveRuntimeKind.SameTargetHaste => "중첩당 공격속도 증가",
            GenericMonsterPassiveRuntimeKind.ImpactStrike => "보스·구조물 추가 피해",
            GenericMonsterPassiveRuntimeKind.LowHealthHunter => "저체력 대상 추가 피해",
            GenericMonsterPassiveRuntimeKind.LongRangeAim => "장거리 추가 피해",
            GenericMonsterPassiveRuntimeKind.CrisisDefense => "받는 피해 감소",
            GenericMonsterPassiveRuntimeKind.FrontlineBond => "받는 피해 감소",
            GenericMonsterPassiveRuntimeKind.FractureMark => "대상이 더 받는 피해",
            GenericMonsterPassiveRuntimeKind.ThreatMark => "고위협 대상 추가 피해",
            GenericMonsterPassiveRuntimeKind.KillHeal => "최대 체력 회복",
            GenericMonsterPassiveRuntimeKind.CourageAura => "아군 공격력 증가",
            GenericMonsterPassiveRuntimeKind.HealingShot => "공격력 기반 회복량",
            GenericMonsterPassiveRuntimeKind.EmergencyEntry => "자신 최대 체력 보호막",
            GenericMonsterPassiveRuntimeKind.FirstWave => "공격력 증가",
            _ => "주 효과"
        };

        private static string ResolveSecondaryLabel(GenericMonsterPassiveRuntimeKind kind) => kind switch
        {
            GenericMonsterPassiveRuntimeKind.ThreatMark => "대상이 더 받는 피해",
            GenericMonsterPassiveRuntimeKind.EmergencyEntry => "아군 최대 체력 보호막",
            _ => "보조 효과"
        };

        private static string ResolvePassiveRule(GenericMonsterPassiveRuntimeKind kind) => kind switch
        {
            GenericMonsterPassiveRuntimeKind.ImpactStrike =>
                "일반 적은 잠깐 경직되고 보스·구조물은 추가 피해를 받습니다.",
            GenericMonsterPassiveRuntimeKind.EmergencyEntry =>
                "예비 교체 합류 때 자신과 체력이 가장 낮은 아군에게 적용됩니다.",
            GenericMonsterPassiveRuntimeKind.FirstWave => "전투 합류 즉시 공격력이 상승합니다.",
            GenericMonsterPassiveRuntimeKind.FrontlineBond =>
                "표시 거리 안에 자신을 제외한 아군이 2명 이상이면 발동합니다.",
            GenericMonsterPassiveRuntimeKind.ThreatMark =>
                "메인 전투는 원거리·보스, 군단의 역습은 수비대·포탑을 고위협으로 판단합니다.",
            _ => "메인 전투와 군단의 역습에서 같은 전용 수치가 사용됩니다."
        };

        private void CopyFirstActiveMotion()
        {
            ApplyAndRebuild(
                "Monster Maker V2 · 액티브 모션 전체 적용",
                () =>
                {
                    var presentations = serializedDraft.FindProperty(
                        draft.ActiveEffectProfile != null
                            ? "activeEffectPresentations"
                            : "activeAttackPresentations");
                    if (presentations == null || presentations.arraySize < 2)
                    {
                        return;
                    }

                    var source = presentations.GetArrayElementAtIndex(0);
                    for (var index = 1; index < presentations.arraySize; index++)
                    {
                        CopyActiveMotion(source, presentations.GetArrayElementAtIndex(index));
                    }
                });
        }

        private void CopyFirstActiveMotionFade()
        {
            ApplyAndRebuild(
                "Monster Maker V2 · 액티브 전환 시간 전체 적용",
                () =>
                {
                    var presentations = serializedDraft.FindProperty("activeAttackPresentations");
                    if (presentations == null || presentations.arraySize < 2)
                    {
                        return;
                    }

                    var fade = presentations.GetArrayElementAtIndex(0)
                        .FindPropertyRelative("motionCrossFadeDuration").floatValue;
                    for (var index = 1; index < presentations.arraySize; index++)
                    {
                        presentations.GetArrayElementAtIndex(index)
                            .FindPropertyRelative("motionCrossFadeDuration").floatValue = fade;
                    }
                });
        }

        private static void CopyActiveMotion(
            SerializedProperty source,
            SerializedProperty destination)
        {
            destination.FindPropertyRelative("motionConfigured").boolValue = true;
            destination.FindPropertyRelative("motionClip").objectReferenceValue =
                source.FindPropertyRelative("motionClip").objectReferenceValue;
            destination.FindPropertyRelative("motionPlaybackSpeed").floatValue =
                source.FindPropertyRelative("motionPlaybackSpeed").floatValue;
            destination.FindPropertyRelative("motionCrossFadeDuration").floatValue =
                source.FindPropertyRelative("motionCrossFadeDuration").floatValue;
            destination.FindPropertyRelative("motionCommitNormalizedTime").floatValue =
                source.FindPropertyRelative("motionCommitNormalizedTime").floatValue;
        }

        private void RemoveActiveAssignment()
        {
            ApplyAndRebuild(
                "Monster Maker V2 · 액티브 연결 제거",
                () =>
                {
                    serializedDraft.FindProperty("rarityActiveSkill").objectReferenceValue = null;
                    serializedDraft.FindProperty("activeAttackProfile").objectReferenceValue = null;
                    serializedDraft.FindProperty("activeEffectProfile").objectReferenceValue = null;
                    draft.EditorClearActiveProfiles();
                });
        }

        private void ShowPassivePresetMenu()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterSkillCatalog>(
                MonsterSkillCatalog.DefaultAssetPath);
            var profiles = catalog == null
                ? Array.Empty<GenericMonsterPassiveSkill>()
                : catalog.PassiveSkills
                    .OfType<GenericMonsterPassiveSkill>()
                    .OrderBy(profile => profile.DisplayName, StringComparer.CurrentCulture)
                    .ThenBy(profile => profile.SkillId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            var current = draft?.RarityPassiveSkill as GenericMonsterPassiveSkill;
            var menu = new GenericMenu();
            if (profiles.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("저장된 패시브 없음"));
            }

            foreach (var profile in profiles)
            {
                var captured = profile;
                var label = $"[{profile.SkillId}] {profile.DisplayName}" +
                            (profile.AuthoringEnabled ? string.Empty : " · 비활성");
                if (profile.AuthoringEnabled)
                {
                    menu.AddItem(
                        new GUIContent(label),
                        profile == current,
                        () => AssignPassivePreset(captured));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent(label), profile == current);
                }
            }

            menu.ShowAsContext();
        }

        private void AssignPassivePreset(GenericMonsterPassiveSkill profile)
        {
            if (profile == null || draft?.RarityPassiveSkill == profile)
            {
                return;
            }

            ApplyObjectMutationAndRebuild(
                "Monster Maker V2 · 패시브 선택",
                () => draft.EditorSetPassiveTemplate(profile, true));
        }

        private void ShowActivePresetMenu()
        {
            var attacks = AssetDatabase.FindAssets(
                    "t:MonsterActiveAttackProfile",
                    new[] { MonsterActiveAttackAuthoringService.ProfileRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterActiveAttackProfile>)
                .Where(profile => profile != null)
                .OrderBy(profile => profile.DisplayName, StringComparer.CurrentCulture)
                .ThenBy(profile => profile.ProfileId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var effects = AssetDatabase.FindAssets(
                    "t:MonsterEffectActiveProfile",
                    new[] { MonsterEffectActiveAuthoringService.ProfileRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterEffectActiveProfile>)
                .Where(profile => profile != null)
                .OrderBy(profile => profile.Role)
                .ThenBy(profile => profile.DisplayName, StringComparer.CurrentCulture)
                .ThenBy(profile => profile.ProfileId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var menu = new GenericMenu();
            if (attacks.Length == 0 && effects.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("저장된 액티브 스킬 없음"));
            }

            foreach (var profile in attacks)
            {
                var captured = profile;
                menu.AddItem(
                    new GUIContent($"[공격] [{profile.ProfileId}] {profile.DisplayName}"),
                    profile == draft?.ActiveAttackProfile,
                    () => AssignActiveAttackPreset(captured));
            }
            foreach (var profile in effects)
            {
                var captured = profile;
                menu.AddItem(
                    new GUIContent(
                        $"[{GetEffectRoleLabel(profile.Role)}] [{profile.ProfileId}] {profile.DisplayName}"),
                    profile == draft?.ActiveEffectProfile,
                    () => AssignActiveEffectPreset(captured));
            }

            menu.ShowAsContext();
        }

        private void AssignActiveAttackPreset(MonsterActiveAttackProfile profile)
        {
            if (profile == null || draft?.ActiveAttackProfile == profile)
            {
                return;
            }

            ApplyObjectMutationAndRebuild(
                "Monster Maker V2 · 공격형 액티브 선택",
                () => draft.EditorSetActiveAttackProfile(profile));
        }

        private void AssignActiveEffectPreset(MonsterEffectActiveProfile profile)
        {
            if (profile == null || draft?.ActiveEffectProfile == profile)
            {
                return;
            }

            ApplyObjectMutationAndRebuild(
                "Monster Maker V2 · 효과형 액티브 선택",
                () => draft.EditorSetActiveEffectProfile(profile));
        }

    }
}
