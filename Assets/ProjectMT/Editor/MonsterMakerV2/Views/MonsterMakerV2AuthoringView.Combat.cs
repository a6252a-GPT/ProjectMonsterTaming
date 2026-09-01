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
    internal sealed partial class MonsterMakerV2AuthoringView
    {
        private static readonly string[] AssignmentLabels =
        {
            "미결정", "사용 안 함", "배정"
        };

        private void BuildCombat()
        {
            var container = Section("combat");
            var profile = serializedDraft.FindProperty("basicAttackProfile")?
                .objectReferenceValue as MonsterBasicAttackProfile;
            AddActionRow(
                container,
                (profile == null ? "기본공격 선택" : "기본공격 변경",
                    ShowBasicAttackPresetMenu, "draft-action-button"),
                ("기본공격 조립소 열기", openBasicWorkshop, "draft-action-button"));

            if (profile == null)
            {
                AddHelp(
                    container,
                    "저장된 기본공격을 선택하거나 조립소에서 새 프리셋을 만들어야 합니다.",
                    HelpBoxMessageType.Warning);
            }
            else
            {
                AddSummary(
                    container,
                    $"현재 기본공격 · [{profile.AttackId}] {profile.DisplayName}",
                    BuildBasicAttackSummary(profile) +
                    (string.IsNullOrWhiteSpace(profile.DesignMemo)
                        ? string.Empty
                        : "\n기획 의도 · " + profile.DesignMemo));
                AddActionRow(
                    container,
                    ("현재 판정범위 표시", showBasicAttackArea, "draft-action-button"));
            }

            BuildBasicAttackVfx(container, profile);
            BuildAttackMotions(container, profile);
            AddHelp(
                container,
                "조립소는 공격 방식과 연출 공간을 정의합니다. 이 Monster의 VFX/SFX는 공간 카드에서 " +
                "배정하고, 공격 동작에서는 Clip과 Marker 시점만 정합니다.",
                HelpBoxMessageType.Info);
        }

        private void ShowBasicAttackPresetMenu()
        {
            var profiles = AssetDatabase.FindAssets(
                    "t:MonsterBasicAttackProfile",
                    new[] { MonsterBasicAttackPresetUtility.ProfileRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>)
                .Where(candidate => candidate != null)
                .OrderBy(candidate => candidate.AttackId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var menu = new GenericMenu();
            if (profiles.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("저장된 기본공격 없음"));
            }

            foreach (var candidate in profiles)
            {
                var captured = candidate;
                menu.AddItem(
                    new GUIContent($"[{candidate.AttackId}] {candidate.DisplayName}"),
                    candidate == draft?.BasicAttackProfile,
                    () => AssignBasicAttackPreset(captured));
            }

            menu.ShowAsContext();
        }

        private void AssignBasicAttackPreset(MonsterBasicAttackProfile profile)
        {
            if (profile == null || draft?.BasicAttackProfile == profile)
            {
                return;
            }

            ApplyObjectMutationAndRebuild(
                "Monster Maker V2 · 기본공격 선택",
                () =>
                {
                    draft.EditorSetBasicAttackProfile(profile);
                    draft.EditorAdoptBasicAttackProfileTuning();
                    MonsterBasicAttackPresetUtility.InvalidateUsageCache();
                });
        }

        private void BuildBasicAttackVfx(
            VisualElement container,
            MonsterBasicAttackProfile profile)
        {
            var foldout = AddSubFoldout(container, "몬스터 고유 기본공격 VFX/SFX", true);
            if (profile == null)
            {
                AddHelp(
                    foldout,
                    "기본공격을 선택하면 해당 공격이 요구하는 연출 공간이 자동으로 표시됩니다.",
                    HelpBoxMessageType.Info);
                return;
            }

            if (profile.VfxSlots.Count == 0)
            {
                AddHelp(
                    foldout,
                    "이 기본공격에는 연출 공간 계약이 없습니다. 기본공격 조립소에서 먼저 정의하세요.",
                    HelpBoxMessageType.Warning);
                return;
            }

            var bindings = serializedDraft.FindProperty("basicAttackVfxBindings");
            var rows = ResolveBasicVfxRows(profile, bindings);
            var expected = ResolveExpectedBasicVfxCount(profile);
            if (rows.Count < expected)
            {
                AddHelp(
                    foldout,
                    $"연출 연결 데이터가 부족합니다. 현재 {rows.Count}개 / 필요 {expected}개",
                    HelpBoxMessageType.Error);
                AddActionRow(
                    foldout,
                    ("프로필 연출 공간 다시 동기화", SyncBasicVfxBindings, "draft-action-button"));
            }

            var vfxDecided = 0;
            var sfxDecided = 0;
            foreach (var row in rows)
            {
                var vfxState = (MonsterBasicAttackVfxAssignmentState)
                    row.Binding.FindPropertyRelative("state").enumValueIndex;
                var sfxState = (MonsterBasicAttackSfxAssignmentState)
                    row.Binding.FindPropertyRelative("sfxState").enumValueIndex;
                if (vfxState != MonsterBasicAttackVfxAssignmentState.Undecided)
                {
                    vfxDecided++;
                }
                if (sfxState != MonsterBasicAttackSfxAssignmentState.Undecided)
                {
                    sfxDecided++;
                }
            }

            var progress = new ProgressBar
            {
                title = $"VFX 결정 {vfxDecided}/{expected} · SFX 결정 {sfxDecided}/{expected}",
                value = expected > 0
                    ? (vfxDecided + sfxDecided) * 100f / (expected * 2f)
                    : 0f
            };
            progress.style.height = 20f;
            progress.style.marginBottom = 5f;
            foldout.Add(progress);
            BuildBasicRuntimeSyncStatus(foldout);

            for (var index = 0; index < rows.Count; index++)
            {
                BuildBasicVfxCard(foldout, rows[index], index);
            }
            BuildInactiveBasicAttackBindings(foldout);

            AddHelp(
                foldout,
                "VFX 보정과 원본 AudioClip은 제작 원본에 보존됩니다. 전투 반영 때 SFX Cue는 " +
                "역할별로 자동 생성됩니다.",
                HelpBoxMessageType.Info);
        }

        private void BuildBasicRuntimeSyncStatus(VisualElement container)
        {
            if (draft == null || string.IsNullOrWhiteSpace(draft.MonsterId))
            {
                return;
            }

            if (sourceDraft == null)
            {
                AddHelp(
                    container,
                    "새 제작 원본 · 상단 전투 반영 때 기본공격 게임 자산이 생성됩니다.",
                    HelpBoxMessageType.Info);
                return;
            }

            var paths = MonsterMakerAssetWriter.BuildPaths(sourceDraft.MonsterId);
            var combat = AssetDatabase.LoadAssetAtPath<MonsterCombatProfile>(paths[3]);
            var feedback = AssetDatabase.LoadAssetAtPath<MonsterFeedbackProfile>(paths[5]);
            var runtimeState = MonsterBasicAttackBindingProjection.EvaluateRuntimeSync(
                sourceDraft,
                combat,
                feedback,
                out var message);
            AddHelp(
                container,
                runtimeState == MonsterBasicAttackRuntimeSyncState.Synchronized
                    ? "저장된 게임 자산 최신 · 작업 중 변경은 상단 전투 반영 시 함께 적용됩니다."
                    : $"저장된 게임 자산 미반영 · {message}\n상단 전투 반영으로 저장 원본과 게임 자산을 함께 갱신하세요.",
                runtimeState == MonsterBasicAttackRuntimeSyncState.Synchronized
                    ? HelpBoxMessageType.Info
                    : HelpBoxMessageType.Warning);
        }

        private void BuildInactiveBasicAttackBindings(VisualElement container)
        {
            var inactive = MonsterBasicAttackBindingProjection.BuildInactiveBindings(draft);
            if (inactive.Count == 0)
            {
                return;
            }

            var foldout = AddSubFoldout(
                container,
                $"고급 · 이전 프리셋·모션 연결 {inactive.Count}개 보관 중",
                false);
            AddHelp(
                foldout,
                "현재 기본공격에서는 사용하지 않으며, 이전 프리셋이나 모션으로 돌아갈 때 복원됩니다. " +
                "미리보기와 게임 자산에는 출력되지 않습니다.",
                HelpBoxMessageType.Info);
            foreach (var binding in inactive)
            {
                if (binding == null)
                {
                    continue;
                }

                var motion = string.IsNullOrWhiteSpace(binding.MotionId)
                    ? "공통"
                    : binding.MotionId;
                var label = new Label(
                    $"[{binding.AttackId}] {binding.SlotId} · {motion}");
                label.AddToClassList("summary-body");
                foldout.Add(label);
            }
        }

        private void BuildBasicVfxCard(
            VisualElement container,
            BasicVfxRow row,
            int index,
            string contextLabel = "기본공격",
            string foldoutKey = null)
        {
            var motion = string.IsNullOrWhiteSpace(row.MotionId)
                ? "공용"
                : row.MotionId;
            var card = AddSubFoldout(
                container,
                $"{index + 1:00} · {row.Slot.DisplayName} · {motion}",
                index == 0,
                foldoutKey);
            AddHelp(
                card,
                $"{ResolveVfxEventLabel(row.Slot.EventType)} · " +
                $"{ResolveVfxAnchorLabel(row.Slot.Anchor)} · " +
                $"{ResolveVfxMultiplicityLabel(row.Slot.Multiplicity)}" +
                (string.IsNullOrWhiteSpace(row.Slot.Description)
                    ? string.Empty
                    : "\n" + row.Slot.Description),
                HelpBoxMessageType.Info);

            var vfxState = row.Binding.FindPropertyRelative("state");
            AddAssignmentPopup(
                card,
                "VFX 결정",
                vfxState,
                $"Monster Maker V2 · {contextLabel} VFX 결정 변경");
            if ((MonsterBasicAttackVfxAssignmentState)vfxState.enumValueIndex ==
                MonsterBasicAttackVfxAssignmentState.Assigned)
            {
                AddRelativeProperty(
                    card,
                    row.Binding.FindPropertyRelative("prefab"),
                    "VFX 프리팹");
                AddTimingGauge(card, row.Slot, row.Binding);
                AddHelp(
                    card,
                    "유지 시간·시작점·속도·위치·회전·크기는 아래 VFX 보정 창에서 한 번에 조절합니다.",
                    HelpBoxMessageType.Info);
                var bindingPath = row.Binding.propertyPath;
                var assignedPrefab = row.Binding.FindPropertyRelative("prefab")
                    .objectReferenceValue as GameObject;
                var isWrapper = MonsterBasicAttackVfxPrefabUtility.IsMonsterWrapper(
                    assignedPrefab,
                    draft?.MonsterId,
                    row.WrapperOwner);
                AddActionRow(
                    card,
                    ("VFX 보정 · 재생",
                        () => openVfxAdjust?.Invoke(row.Slot, bindingPath),
                        "draft-action-button"),
                    (isWrapper ? "전용 Prefab 편집" : "전용 래퍼 만들기",
                        () => CreateOrEditBasicVfxWrapper(
                            row.Slot,
                            bindingPath,
                            row.MotionId,
                            row.WrapperOwner),
                        "draft-action-button"));
            }

            var sfxState = row.Binding.FindPropertyRelative("sfxState");
            AddAssignmentPopup(
                card,
                "SFX 결정",
                sfxState,
                $"Monster Maker V2 · {contextLabel} SFX 결정 변경");
            if ((MonsterBasicAttackSfxAssignmentState)sfxState.enumValueIndex ==
                MonsterBasicAttackSfxAssignmentState.Assigned)
            {
                AddRelativeProperty(
                    card,
                    row.Binding.FindPropertyRelative("sound"),
                    "원본 AudioClip");
                AddRelativeProperty(
                    card,
                    row.Binding.FindPropertyRelative("soundVolume"),
                    "SFX 볼륨");
                var bindingPath = row.Binding.propertyPath;
                AddActionRow(
                    card,
                    ("SFX 미리듣기",
                        () => PreviewBasicAttackSound(bindingPath),
                        "draft-action-button"),
                    ("SFX 정지",
                        SfxEditorAudioPreview.StopAll,
                        "draft-action-button"));
                var generated = AddRelativeProperty(
                    card,
                    row.Binding.FindPropertyRelative("sfx"),
                    "생성된 SFX Cue");
                generated?.SetEnabled(false);
            }
        }

        private void PreviewBasicAttackSound(string bindingPath)
        {
            serializedDraft.UpdateIfRequiredOrScript();
            var binding = serializedDraft.FindProperty(bindingPath);
            var clip = binding?.FindPropertyRelative("sound").objectReferenceValue as AudioClip;
            var volume = binding?.FindPropertyRelative("soundVolume").floatValue ?? 1f;
            if (clip != null)
            {
                SfxEditorAudioPreview.Play(clip, 0, false, volume);
            }
        }

        private void AddAssignmentPopup(
            VisualElement container,
            string label,
            SerializedProperty property,
            string undoName)
        {
            var current = ToAssignmentIndex(property.enumValueIndex);
            var popup = new PopupField<string>(
                label,
                new List<string>(AssignmentLabels),
                current);
            popup.RegisterValueChangedCallback(evt =>
            {
                var next = Array.IndexOf(AssignmentLabels, evt.newValue);
                ApplyAndRebuild(undoName, () => property.enumValueIndex = FromAssignmentIndex(next));
            });
            popup.AddToClassList("draft-property");
            container.Add(popup);
        }

        private void AddTimingGauge(
            VisualElement container,
            MonsterBasicAttackVfxSlot slot,
            SerializedProperty binding)
        {
            var property = binding.FindPropertyRelative("eventTimingOffset");
            if (!slot.AllowsMonsterTimingOffset)
            {
                property.floatValue = 0f;
                AddHelp(
                    container,
                    "이 연출은 전달체 수명과 결합되어 몬스터별 타이밍 보정을 사용하지 않습니다.",
                    HelpBoxMessageType.Info);
                return;
            }

            var magnitude = Mathf.Abs(property.floatValue);
            var range = Mathf.Max(2f, Mathf.Ceil(magnitude / 0.5f) * 0.5f);
            var row = new VisualElement();
            row.AddToClassList("draft-action-row");
            var path = property.propertyPath;
            var minimum = slot.AllowsTimingLead ? -range : 0f;
            var maximum = range;
            var slider = new Slider(
                slot.AllowsTimingLead ? "발생 타이밍 · 먼저 ↔ 늦게" : "발생 타이밍 · 정시 ↔ 늦게",
                minimum,
                maximum) { value = Mathf.Clamp(property.floatValue, minimum, maximum) };
            slider.style.flexGrow = 1f;
            var input = new FloatField { value = property.floatValue };
            input.style.flexGrow = 0f;
            input.style.width = 68f;
            var syncing = false;
            slider.RegisterValueChangedCallback(evt =>
            {
                if (syncing) return;
                syncing = true;
                input.SetValueWithoutNotify(evt.newValue);
                SetFloatProperty(path, slot.ClampTimingOffset(evt.newValue), "Monster Maker V2 · VFX 타이밍 조절");
                syncing = false;
            });
            input.RegisterValueChangedCallback(evt =>
            {
                if (syncing) return;
                var next = float.IsFinite(evt.newValue) ? slot.ClampTimingOffset(evt.newValue) : 0f;
                syncing = true;
                input.SetValueWithoutNotify(next);
                slider.SetValueWithoutNotify(Mathf.Clamp(next, minimum, maximum));
                SetFloatProperty(path, next, "Monster Maker V2 · VFX 타이밍 입력");
                syncing = false;
            });
            var reset = new Button(() =>
                ApplyAndRebuild(
                    "Monster Maker V2 · 연출 타이밍 초기화",
                    () => serializedDraft.FindProperty(property.propertyPath).floatValue = 0f))
            {
                text = "0초"
            };
            reset.AddToClassList("draft-action-button");
            reset.style.flexGrow = 0f;
            reset.style.width = 46f;
            row.Add(slider);
            row.Add(input);
            row.Add(reset);
            container.Add(row);
            AddHelp(
                container,
                "게이지는 현재 값에 맞춰 자동 확장되고 숫자 직접 입력은 제한하지 않습니다.",
                HelpBoxMessageType.Info);
        }

        private void CreateOrEditBasicVfxWrapper(
            MonsterBasicAttackVfxSlot slot,
            string bindingPath,
            string motionId,
            MonsterAttackVfxWrapperOwner owner)
        {
            serializedDraft.ApplyModifiedProperties();
            var binding = serializedDraft.FindProperty(bindingPath);
            var source = binding?.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
            if (source == null || draft == null)
            {
                EditorUtility.DisplayDialog("전용 VFX", "먼저 Project에 저장된 VFX Prefab을 지정하세요.", "확인");
                return;
            }

            if (MonsterBasicAttackVfxPrefabUtility.IsMonsterWrapper(
                    source,
                    draft.MonsterId,
                    owner))
            {
                AssetDatabase.OpenAsset(source);
                EditorGUIUtility.PingObject(source);
                return;
            }

            var attackId = binding.FindPropertyRelative("attackId").stringValue;
            if (!MonsterBasicAttackVfxPrefabUtility.TryCreateWrapper(
                    draft.MonsterId,
                    owner,
                    attackId,
                    slot.SlotId,
                    motionId,
                    source,
                    binding.FindPropertyRelative("localPosition").vector3Value,
                    binding.FindPropertyRelative("localEulerAngles").vector3Value,
                    binding.FindPropertyRelative("scale").floatValue,
                    out var wrapper,
                    out var error))
            {
                EditorUtility.DisplayDialog(
                    "전용 VFX 래퍼 생성 실패",
                    string.IsNullOrWhiteSpace(error) ? "알 수 없는 오류입니다." : error,
                    "확인");
                return;
            }

            ApplyAndRebuild(
                "Monster Maker V2 · 전용 VFX 래퍼 연결",
                () =>
                {
                    var refreshed = serializedDraft.FindProperty(bindingPath);
                    refreshed.FindPropertyRelative("prefab").objectReferenceValue = wrapper;
                    refreshed.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
                    refreshed.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
                    refreshed.FindPropertyRelative("scale").floatValue = 1f;
                });
            EditorGUIUtility.PingObject(wrapper);
        }

        private void BuildAttackMotions(
            VisualElement container,
            MonsterBasicAttackProfile profile)
        {
            var foldout = AddSubFoldout(container, "공격 Motion · 실제 발생 시점", true);
            var attacks = serializedDraft.FindProperty("attacks");
            if (attacks == null)
            {
                AddHelp(foldout, "공격 Motion 데이터를 찾을 수 없습니다.", HelpBoxMessageType.Error);
                return;
            }

            for (var index = 0; index < attacks.arraySize; index++)
            {
                var attack = attacks.GetArrayElementAtIndex(index);
                var clip = attack.FindPropertyRelative("clip").objectReferenceValue;
                var card = AddSubFoldout(
                    foldout,
                    $"공격 {index + 1:00} · {(clip == null ? "미지정" : clip.name)}",
                    index == 0);
                AddRelativeProperty(card, attack.FindPropertyRelative("clip"), "공격 애니메이션");
                AddRelativeProperty(card, attack.FindPropertyRelative("playbackSpeed"), "재생 속도");
                AddRelativeProperty(card, attack.FindPropertyRelative("crossFadeDuration"), "전환 시간");
                if (attacks.arraySize > 1)
                {
                    AddRelativeProperty(card, attack.FindPropertyRelative("weight"), "무작위 선택 비중");
                    AddRelativeProperty(
                        card,
                        attack.FindPropertyRelative("preventImmediateRepeat"),
                        "같은 동작 연속 방지");
                }

                BuildAttackMarker(card, attack, profile);
                if (profile != null &&
                    profile.PresentationKind == MonsterBasicAttackPresentationKind.Breath)
                {
                    var breath = AddSubFoldout(card, "브레스 지속시간 예외", false);
                    AddRelativeProperty(
                        breath,
                        attack.FindPropertyRelative("overrideBreathDuration"),
                        "몬스터별 지속시간 사용");
                    if (attack.FindPropertyRelative("overrideBreathDuration").boolValue)
                    {
                        AddRelativeProperty(
                            breath,
                            attack.FindPropertyRelative("breathDuration"),
                            "지속시간");
                        AddHelp(
                            breath,
                            $"프리셋 기본값 {profile.BreathDuration:0.###}초",
                            HelpBoxMessageType.Info);
                    }
                }

                if (attacks.arraySize > 1)
                {
                    var capturedIndex = index;
                    AddActionRow(
                        card,
                        ($"공격 {index + 1:00} 삭제",
                            () => RemoveAttack(capturedIndex),
                            "danger-button"));
                }
            }

            AddActionRow(
                foldout,
                ("공격 애니메이션 추가", AddAttack, "draft-action-button"));
        }

        private void BuildAttackMarker(
            VisualElement container,
            SerializedProperty attack,
            MonsterBasicAttackProfile profile)
        {
            var markers = attack.FindPropertyRelative("markers");
            var timing = profile != null && profile.UsesProjectileVisual ? "발사" : "타격";
            var markerCard = AddSubFoldout(container, $"피해 실행 시점 · {timing}", true);
            if (markers.arraySize != 1)
            {
                AddHelp(
                    markerCard,
                    "기본공격 모션마다 피해를 실행하는 시점이 정확히 1개 필요합니다.",
                    HelpBoxMessageType.Error);
                var path = markers.propertyPath;
                AddActionRow(
                    markerCard,
                    ("실행 시점 1개로 정리",
                        () => NormalizeMarker(path),
                        "draft-action-button"));
                return;
            }

            var marker = markers.GetArrayElementAtIndex(0);
            AddRelativeProperty(
                markerCard,
                marker.FindPropertyRelative("normalizedTime"),
                $"{timing} 시점 · 동작 진행률 0~1");
            var power = marker.FindPropertyRelative("powerRatio");
            power.floatValue = 1f;
            var socket = marker.FindPropertyRelative("socketOverride");
            if (!string.IsNullOrWhiteSpace(socket.stringValue))
            {
                var advanced = AddSubFoldout(markerCard, "고급 위치 예외", false);
                AddRelativeProperty(advanced, socket, "부착 위치 경로");
            }
        }

        private static string ResolveVfxEventLabel(MonsterBasicAttackVfxEvent value)
        {
            return value switch
            {
                MonsterBasicAttackVfxEvent.MotionStart => "공격 모션 시작",
                MonsterBasicAttackVfxEvent.RecipeExecute => "피해 실행 시점",
                MonsterBasicAttackVfxEvent.DeliverySpawn => "투사체 생성",
                MonsterBasicAttackVfxEvent.TargetDamaged => "대상 피해",
                MonsterBasicAttackVfxEvent.OutboundTargetDamaged => "왕복 공격의 전진 피해",
                MonsterBasicAttackVfxEvent.ReturnTargetDamaged => "왕복 공격의 귀환 피해",
                MonsterBasicAttackVfxEvent.AreaResolved => "범위 판정 완료",
                MonsterBasicAttackVfxEvent.SequenceEnd => "연속 공격 종료",
                MonsterBasicAttackVfxEvent.DeliveryTurn => "투사체 방향 전환",
                MonsterBasicAttackVfxEvent.DeliveryEnd => "투사체 종료",
                MonsterBasicAttackVfxEvent.MotionEnd => "공격 모션 종료",
                MonsterBasicAttackVfxEvent.DashExit => "돌진 출발",
                MonsterBasicAttackVfxEvent.DashEnter => "돌진 도착",
                _ => "지정 시점"
            };
        }

        private static string ResolveVfxAnchorLabel(MonsterBasicAttackVfxAnchor value)
        {
            return value switch
            {
                MonsterBasicAttackVfxAnchor.SourceRoot => "몬스터 중심",
                MonsterBasicAttackVfxAnchor.AttackOrigin => "공격 기준점",
                MonsterBasicAttackVfxAnchor.MarkerSocket => "공격 시점 부착 위치",
                MonsterBasicAttackVfxAnchor.ProjectileRoot => "투사체 중심",
                MonsterBasicAttackVfxAnchor.TargetRoot => "대상 중심",
                MonsterBasicAttackVfxAnchor.HitPoint => "피격 지점",
                MonsterBasicAttackVfxAnchor.AreaCenter => "범위 중심",
                MonsterBasicAttackVfxAnchor.TrajectoryOrigin => "궤적 시작점",
                _ => "기준 위치"
            };
        }

        private static string ResolveVfxMultiplicityLabel(
            MonsterBasicAttackVfxMultiplicity value)
        {
            return value switch
            {
                MonsterBasicAttackVfxMultiplicity.OncePerMotion => "모션마다 1회",
                MonsterBasicAttackVfxMultiplicity.OncePerExecution => "공격 실행마다 1회",
                MonsterBasicAttackVfxMultiplicity.PerProjectile => "투사체마다",
                MonsterBasicAttackVfxMultiplicity.PerTargetHit => "피격 대상마다",
                MonsterBasicAttackVfxMultiplicity.PerDamageStage => "피해 단계마다",
                MonsterBasicAttackVfxMultiplicity.ContinuousUntilEnd => "종료까지 유지",
                _ => "필요할 때 재생"
            };
        }

        private void AddAttack()
        {
            ApplyAndRebuild(
                "Monster Maker V2 · 공격 Motion 추가",
                () =>
                {
                    var attacks = serializedDraft.FindProperty("attacks");
                    var motionId = BuildNextAttackMotionId(attacks);
                    var index = attacks.arraySize;
                    attacks.InsertArrayElementAtIndex(index);
                    var attack = attacks.GetArrayElementAtIndex(index);
                    attack.FindPropertyRelative("motionId").stringValue = motionId;
                    attack.FindPropertyRelative("clip").objectReferenceValue = null;
                    attack.FindPropertyRelative("playbackSpeed").floatValue = 1f;
                    attack.FindPropertyRelative("crossFadeDuration").floatValue = 0.06f;
                    attack.FindPropertyRelative("weight").floatValue = 1f;
                    attack.FindPropertyRelative("preventImmediateRepeat").boolValue = false;
                    attack.FindPropertyRelative("overrideBreathDuration").boolValue = false;
                    attack.FindPropertyRelative("breathDuration").floatValue = 0.8f;
                    var markers = attack.FindPropertyRelative("markers");
                    markers.arraySize = 1;
                    ResetMarker(markers.GetArrayElementAtIndex(0));
                });
        }

        private void RemoveAttack(int index)
        {
            ApplyAndRebuild(
                "Monster Maker V2 · 공격 Motion 삭제",
                () => serializedDraft.FindProperty("attacks").DeleteArrayElementAtIndex(index));
        }

        private void NormalizeMarker(string propertyPath)
        {
            ApplyAndRebuild(
                "Monster Maker V2 · 실행 Marker 정리",
                () =>
                {
                    var markers = serializedDraft.FindProperty(propertyPath);
                    markers.arraySize = 1;
                    ResetMarker(markers.GetArrayElementAtIndex(0));
                });
        }

        private void SyncBasicVfxBindings()
        {
            ApplyAndRebuild(
                "Monster Maker V2 · 기본공격 연출 공간 동기화",
                () =>
                {
                    var profile = serializedDraft.FindProperty("basicAttackProfile")
                        .objectReferenceValue as MonsterBasicAttackProfile;
                    var bindings = serializedDraft.FindProperty("basicAttackVfxBindings");
                    if (profile == null || bindings == null)
                    {
                        return;
                    }

                    foreach (var slot in profile.VfxSlots)
                    {
                        foreach (var motionId in ResolveBasicVfxMotionIds(slot))
                        {
                            FindOrCreateBasicVfxBinding(bindings, profile, slot, motionId);
                        }
                    }
                });
        }

        private List<BasicVfxRow> ResolveBasicVfxRows(
            MonsterBasicAttackProfile profile,
            SerializedProperty bindings)
        {
            var rows = new List<BasicVfxRow>();
            if (profile == null || bindings == null)
            {
                return rows;
            }

            foreach (var slot in profile.VfxSlots)
            {
                foreach (var motionId in ResolveBasicVfxMotionIds(slot))
                {
                    var binding = FindBasicVfxBinding(
                        bindings,
                        profile.AttackId,
                        slot.SlotId,
                        motionId);
                    if (binding != null)
                    {
                        rows.Add(new BasicVfxRow(
                            slot,
                            binding,
                            motionId,
                            MonsterAttackVfxWrapperOwner.BasicAttack));
                    }
                }
            }
            return rows;
        }

        private List<BasicVfxRow> ResolveActiveVfxRows(
            MonsterActiveAttackStep step,
            SerializedProperty bindings)
        {
            var rows = new List<BasicVfxRow>();
            if (step == null || bindings == null) return rows;
            var attackId = "active_" + step.StepId;
            foreach (var slot in step.AttackBlockVfxSlots)
            {
                if (slot == null) continue;
                var motionId = slot.AssignmentScope == MonsterBasicAttackVfxAssignmentScope.MotionSpecific
                    ? step.StepId
                    : string.Empty;
                var binding = FindBasicVfxBinding(
                    bindings,
                    attackId,
                    slot.SlotId,
                    motionId);
                if (binding != null)
                {
                    rows.Add(new BasicVfxRow(
                        slot,
                        binding,
                        motionId,
                        MonsterAttackVfxWrapperOwner.ActiveAttack));
                }
            }
            return rows;
        }

        private int ResolveExpectedBasicVfxCount(MonsterBasicAttackProfile profile)
        {
            var count = 0;
            foreach (var slot in profile.VfxSlots)
            {
                count += ResolveBasicVfxMotionIds(slot).Count;
            }
            return count;
        }

        private List<string> ResolveBasicVfxMotionIds(MonsterBasicAttackVfxSlot slot)
        {
            var result = new List<string>();
            if (slot.AssignmentScope == MonsterBasicAttackVfxAssignmentScope.MonsterShared)
            {
                result.Add(string.Empty);
                return result;
            }

            var attacks = serializedDraft.FindProperty("attacks");
            for (var index = 0; attacks != null && index < attacks.arraySize; index++)
            {
                var motionId = attacks.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("motionId").stringValue?.Trim();
                if (!string.IsNullOrWhiteSpace(motionId) && !result.Contains(motionId))
                {
                    result.Add(motionId);
                }
            }
            if (result.Count == 0)
            {
                result.Add("attack01");
            }
            return result;
        }

        private static SerializedProperty FindBasicVfxBinding(
            SerializedProperty bindings,
            string attackId,
            string slotId,
            string motionId)
        {
            for (var index = 0; index < bindings.arraySize; index++)
            {
                var candidate = bindings.GetArrayElementAtIndex(index);
                if (string.Equals(
                        candidate.FindPropertyRelative("attackId").stringValue,
                        attackId,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        candidate.FindPropertyRelative("slotId").stringValue,
                        slotId,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        candidate.FindPropertyRelative("motionId").stringValue,
                        motionId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static SerializedProperty FindOrCreateBasicVfxBinding(
            SerializedProperty bindings,
            MonsterBasicAttackProfile profile,
            MonsterBasicAttackVfxSlot slot,
            string motionId)
        {
            var existing = FindBasicVfxBinding(
                bindings,
                profile.AttackId,
                slot.SlotId,
                motionId);
            if (existing != null)
            {
                return existing;
            }

            var index = bindings.arraySize;
            bindings.InsertArrayElementAtIndex(index);
            var created = bindings.GetArrayElementAtIndex(index);
            created.FindPropertyRelative("attackId").stringValue = profile.AttackId;
            created.FindPropertyRelative("slotId").stringValue = slot.SlotId;
            created.FindPropertyRelative("motionId").stringValue = motionId;
            created.FindPropertyRelative("state").enumValueIndex =
                (int)MonsterBasicAttackVfxAssignmentState.Undecided;
            created.FindPropertyRelative("prefab").objectReferenceValue = null;
            created.FindPropertyRelative("sfxState").enumValueIndex =
                (int)MonsterBasicAttackSfxAssignmentState.Undecided;
            created.FindPropertyRelative("sound").objectReferenceValue = null;
            created.FindPropertyRelative("soundVolume").floatValue = 1f;
            created.FindPropertyRelative("sfx").objectReferenceValue = null;
            created.FindPropertyRelative("lifetime").floatValue = slot.DefaultLifetime;
            created.FindPropertyRelative("playbackOffset").floatValue = 0f;
            created.FindPropertyRelative("playbackSpeed").floatValue = 1f;
            created.FindPropertyRelative("eventTimingOffset").floatValue = 0f;
            created.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
            created.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
            created.FindPropertyRelative("scale").floatValue = 1f;
            return created;
        }

        private static string BuildNextAttackMotionId(SerializedProperty attacks)
        {
            for (var number = 1; ; number++)
            {
                var candidate = $"attack{number:00}";
                var alreadyUsed = false;
                for (var index = 0; index < attacks.arraySize; index++)
                {
                    var existing = attacks.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("motionId").stringValue;
                    if (string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyUsed = true;
                        break;
                    }
                }
                if (!alreadyUsed)
                {
                    return candidate;
                }
            }
        }

        private static void ResetMarker(SerializedProperty marker)
        {
            marker.FindPropertyRelative("normalizedTime").floatValue = 0.5f;
            marker.FindPropertyRelative("powerRatio").floatValue = 1f;
            marker.FindPropertyRelative("socketOverride").stringValue = string.Empty;
        }

        private static int ToAssignmentIndex(int enumValue)
        {
            return enumValue switch
            {
                (int)MonsterBasicAttackVfxAssignmentState.Disabled => 1,
                (int)MonsterBasicAttackVfxAssignmentState.Assigned => 2,
                _ => 0
            };
        }

        private static int FromAssignmentIndex(int index)
        {
            return index switch
            {
                1 => (int)MonsterBasicAttackVfxAssignmentState.Disabled,
                2 => (int)MonsterBasicAttackVfxAssignmentState.Assigned,
                _ => (int)MonsterBasicAttackVfxAssignmentState.Undecided
            };
        }

        private static string BuildBasicAttackSummary(MonsterBasicAttackProfile profile)
        {
            var family = profile.AttackId.StartsWith("BA_S_", StringComparison.OrdinalIgnoreCase)
                ? "특수"
                : profile.CombatType == MonsterCombatType.Melee ? "근거리" : "원거리";
            var delivery = profile.PresentationKind switch
            {
                MonsterBasicAttackPresentationKind.Returning => "왕복 투사체",
                MonsterBasicAttackPresentationKind.Breath => "브레스",
                MonsterBasicAttackPresentationKind.Beam => "빔",
                MonsterBasicAttackPresentationKind.Wave => "진행 파동",
                MonsterBasicAttackPresentationKind.Instant => "즉발",
                _ when profile.UsesProjectileVisual => "투사체",
                _ => "직접 타격"
            };
            var shape = profile.Shape switch
            {
                MonsterBasicAttackShape.Fan => "부채꼴",
                MonsterBasicAttackShape.Line => "직선",
                MonsterBasicAttackShape.Circle => "원형",
                _ => "단일"
            };
            var hit = profile.HitCount > 1 ? $"{profile.HitCount}타" : "단타";
            var movement = profile.MovementModule == MonsterBasicAttackMovementModule.Dash
                ? " · 실제 돌진"
                : string.Empty;
            return $"{family} · {delivery} · {shape} · {hit}{movement} · 최대 {profile.MaxTargets}명";
        }

        private readonly struct BasicVfxRow
        {
            public BasicVfxRow(
                MonsterBasicAttackVfxSlot slot,
                SerializedProperty binding,
                string motionId,
                MonsterAttackVfxWrapperOwner wrapperOwner)
            {
                Slot = slot;
                Binding = binding;
                MotionId = motionId;
                WrapperOwner = wrapperOwner;
            }

            public MonsterBasicAttackVfxSlot Slot { get; }
            public SerializedProperty Binding { get; }
            public string MotionId { get; }
            public MonsterAttackVfxWrapperOwner WrapperOwner { get; }
        }
    }
}
