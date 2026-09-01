using System;
using System.Collections.Generic;
using ProjectMT.Contents.CastleRaidHex;
using ProjectMT.EditorTools.MonsterMaker;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    internal sealed partial class MonsterMakerV2AuthoringView // V1 작업 흐름을 재현하는 V2 전용 제작 뷰
    {
        private static readonly string[] RarityLabels =
            { "일반", "희귀", "영웅", "전설", "신화" };
        private static readonly string[] ImpactStrengthLabels =
            { "보통 충격", "가벼운 충격", "강한 충격" };
        private static readonly string[] ReactionWeightLabels =
            { "보통 체급", "가벼운 체급", "무거운 체급" };
        private static readonly string[] MainBattleRoleLabels =
            { "선봉", "수호", "마무리", "사수", "후열 추적" };
        private static readonly string[] TargetPriorityLabels =
            { "가장 가까운 적", "체력이 낮은 적", "원거리 적 우선" };
        private static readonly string[] CastleRaidPatternLabels =
            { "일반 진격형", "자원 약탈형", "포탑 사냥형", "수비대 사냥형", "성벽 파괴형", "위협 억제형", "전술 지원형" };
        private static readonly string[] CastleRaidSupportLabels =
            { "상황 적응", "공격 강화", "방어 강화", "회복 집중" };
        private static readonly string[] SkillAugmentOperationLabels =
        {
            "효과량 증가율", "지속 시간 추가(초)", "내부 쿨다운 감소율",
            "필요 발동 횟수 감소", "최대 대상 수 증가", "반복 횟수 증가"
        };

        private readonly VisualElement root;
        private readonly VisualElement bindingRoot;
        private readonly Action openBasicWorkshop;
        private readonly Action<bool> openActiveWorkshop;
        private readonly Action showBasicAttackArea;
        private readonly Action syncActiveRuntime; // 자동 Step 동기화 뒤 검증·저장·게임 자산 갱신
        private readonly Action<string, string, MonsterMakerPreviewPositionValueMode, MonsterMakerPreviewAnchor>
            openPositionAdjust;
        private readonly Action<MonsterBasicAttackVfxSlot, string> openVfxAdjust;
        private readonly Action<string, string, MonsterMakerPreviewAnchor> openFeedbackVfxAdjust;
        private readonly Dictionary<string, PropertyField> fields =
            new Dictionary<string, PropertyField>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> foldoutStates =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        private SerializedObject serializedDraft;
        private MonsterMakerDraft draft;
        private MonsterMakerDraft sourceDraft;
        private Action changed;
        private bool lockMonsterId;
        private bool building;
        private string boundDraftStateKey;

        public MonsterMakerV2AuthoringView(
            VisualElement root,
            Action openBasicWorkshop,
            Action<bool> openActiveWorkshop,
            Action showBasicAttackArea,
            Action syncActiveRuntime,
            Action<string, string, MonsterMakerPreviewPositionValueMode, MonsterMakerPreviewAnchor>
                openPositionAdjust,
            Action<MonsterBasicAttackVfxSlot, string> openVfxAdjust,
            Action<string, string, MonsterMakerPreviewAnchor> openFeedbackVfxAdjust)
        {
            this.root = root;
            bindingRoot = root.Q<VisualElement>("draft-scroll");
            this.openBasicWorkshop = openBasicWorkshop;
            this.openActiveWorkshop = openActiveWorkshop;
            this.showBasicAttackArea = showBasicAttackArea;
            this.syncActiveRuntime = syncActiveRuntime;
            this.openPositionAdjust = openPositionAdjust;
            this.openVfxAdjust = openVfxAdjust;
            this.openFeedbackVfxAdjust = openFeedbackVfxAdjust;
            bindingRoot.RegisterCallback<SerializedPropertyChangeEvent>(OnPropertyChanged);
        }

        public void Bind(
            SerializedObject serializedObject,
            MonsterMakerDraft workingDraft,
            MonsterMakerDraft persistentDraft,
            bool shouldLockMonsterId,
            Action onChanged)
        {
            serializedDraft = serializedObject;
            draft = workingDraft;
            sourceDraft = persistentDraft;
            draft?.EditorEnsureSplitSkillUsage();
            var nextStateKey = persistentDraft != null
                ? AssetDatabase.GetAssetPath(persistentDraft)
                : "new-draft";
            if (!string.Equals(boundDraftStateKey, nextStateKey, StringComparison.Ordinal))
            {
                boundDraftStateKey = nextStateKey;
                foldoutStates.Clear();
                selectedSkillTab = draft != null &&
                                   (draft.HasActiveProfile || draft.UseActiveSkill)
                    ? SkillAuthoringTab.Active
                    : SkillAuthoringTab.Passive;
            }
            lockMonsterId = shouldLockMonsterId;
            changed = onChanged;
            Rebuild();
        }

        public void Unbind()
        {
            bindingRoot.Unbind();
            serializedDraft = null;
            draft = null;
            sourceDraft = null;
            changed = null;
            fields.Clear();
        }

        private void Rebuild()
        {
            if (serializedDraft == null)
            {
                return;
            }

            building = true;
            bindingRoot.Unbind();
            fields.Clear();
            ClearSection("identity");
            ClearSection("model");
            ClearSection("stats");
            ClearSection("impact");
            ClearSection("mainbattle");
            ClearSection("skills");
            ClearSection("combat");
            ClearSection("motions");
            ClearSection("castle");
            ClearSection("ascension");

            BuildIdentity();
            BuildModel();
            BuildStats();
            BuildImpact();
            BuildMainBattle();
            BuildSkills();
            BuildCombat();
            BuildMotions();
            BuildCastle();
            BuildAscension();

            bindingRoot.Bind(serializedDraft);
            AlignPropertyFieldColumns();
            bindingRoot.schedule.Execute(AlignPropertyFieldColumns);
            building = false;
        }

        private void AlignPropertyFieldColumns()
        {
            foreach (var field in fields.Values)
            {
                if (field.ClassListContains("draft-property--stacked"))
                {
                    continue;
                }

                var label = field.Q<Label>(className: "unity-base-field__label");
                if (label == null)
                {
                    continue;
                }

                label.style.minWidth = 142f;
                label.style.width = 142f;
                label.style.maxWidth = 142f;
                label.style.flexBasis = 142f;
                label.style.flexGrow = 0f;
                label.style.flexShrink = 0f;
            }
        }

        private void BuildIdentity()
        {
            var container = Section("identity");
            var id = AddProperty(container, "monsterId", "몬스터 ID");
            id?.SetEnabled(!lockMonsterId);
            if (lockMonsterId)
            {
                AddHelp(
                    container,
                    "저장된 제작 원본의 ID는 파일 소유권 보호를 위해 고정됩니다.",
                    HelpBoxMessageType.Info);
            }

            AddProperty(container, "displayName", "표시 이름");
            AddEnumProperty(container, "rarity", "등급", RarityLabels, true);
            AddProperty(container, "portrait", "카드 초상화");
            var productionMemo = AddProperty(container, "productionMemo", "제작 메모");
            productionMemo?.AddToClassList("draft-property--stacked");
        }

        private void BuildModel()
        {
            var container = Section("model");
            AddProperty(container, "vendorPrefab", "3D 모델 프리팹");
            AddProperty(container, "animatorSource", "모델 애니메이터");

            var advanced = AddSubFoldout(container, "모델 상세 보정 · 필요할 때만", false);
            AddProperty(advanced, "visualScale", "모델 크기");
            AddPropertyWithAction(
                advanced,
                "visualLocalPosition",
                "모델 위치",
                "Preview에서 모델 위치 직접 조절",
                () => openPositionAdjust?.Invoke(
                    "visualLocalPosition",
                    "모델 위치",
                    MonsterMakerPreviewPositionValueMode.VisualLocal,
                    MonsterMakerPreviewAnchor.Root));
            AddProperty(advanced, "groundOffset", "바닥 높이 보정");
            AddProperty(advanced, "facingYawOffset", "정면 회전 보정");
            AddPropertyWithAction(
                advanced,
                "attackOriginLocalPosition",
                "공격 기준점 위치",
                "Preview에서 총구/공격 기준점 조절",
                () => openPositionAdjust?.Invoke(
                    "attackOriginLocalPosition",
                    "총구/공격 기준점",
                    MonsterMakerPreviewPositionValueMode.RootLocal,
                    MonsterMakerPreviewAnchor.Root));
            AddPropertyWithAction(
                advanced,
                "hitCenterLocalPosition",
                "피격 기준점 위치",
                "Preview에서 피격 중심 조절",
                () => openPositionAdjust?.Invoke(
                    "hitCenterLocalPosition",
                    "피격 중심",
                    MonsterMakerPreviewPositionValueMode.RootLocal,
                    MonsterMakerPreviewAnchor.Root));
            AddHelp(
                advanced,
                "크기·위치·바닥·정면·공격/피격 기준점은 Vendor 원본을 바꾸지 않고 " +
                "몬스터 전용 생성 자산에만 반영됩니다.",
                HelpBoxMessageType.Info);
        }

        private void BuildStats()
        {
            var container = Section("stats");
            AddProperty(container, "maxHealth", "체력");
            AddProperty(container, "attackPower", "공격력");
            AddProperty(container, "defense", "방어력");
            AddProperty(container, "attackSpeed", "공격 속도");
            AddProperty(container, "moveSpeed", "이동 속도");
            AddProperty(container, "attackRange", "기준 공격 거리");
            AddHelp(
                container,
                "여기는 몬스터의 전투 수치입니다. 공격 모양·연타·투사체·판정 배율은 " +
                "7번 기본공격 프리셋에서 정합니다.",
                HelpBoxMessageType.Info);
        }

        private void BuildImpact()
        {
            var container = Section("impact");
            AddEnumProperty(
                container,
                "impactStrength",
                "타격 강도",
                ImpactStrengthLabels,
                false);
            AddEnumProperty(
                container,
                "reactionWeight",
                "피격 체급",
                ReactionWeightLabels,
                false);
            AddHelp(
                container,
                "타격 강도는 공격 방식이 아니라 맞은 적의 넉백·에어본·경직 세기입니다. " +
                "피격 체급은 이 몬스터가 맞았을 때 얼마나 튕기는지 정합니다.",
                HelpBoxMessageType.Info);
        }

        private void BuildMainBattle()
        {
            var container = Section("mainbattle");
            AddEnumProperty(
                container,
                "mainBattleRole",
                "전투 역할",
                MainBattleRoleLabels,
                true);
            AddEnumProperty(
                container,
                "mainBattleTargetPriority",
                "대상 우선순위",
                TargetPriorityLabels,
                false);
            AddProperty(container, "mainBattlePreferredRangeRatio", "희망 거리 비율");
            AddProperty(container, "mainBattleRetreatRangeRatio", "후퇴 시작 비율");
            AddProperty(container, "mainBattleRetargetInterval", "대상 재탐색 간격");
            var role = (MainBattleMonsterRole)(
                serializedDraft.FindProperty("mainBattleRole")?.enumValueIndex ?? 0);
            AddHelp(container, ResolveMainBattleRoleHelp(role), HelpBoxMessageType.Info);
        }

        private void BuildMotions()
        {
            var container = Section("motions");
            AddProperty(container, "idleClip", "대기 애니메이션");
            AddProperty(container, "idleSpeed", "대기 재생 속도");
            AddProperty(container, "moveClip", "이동 애니메이션");
            AddProperty(container, "movePlaybackSpeed", "이동 재생 속도");
            AddProperty(container, "deathClip", "사망 애니메이션");
            AddProperty(container, "deathSpeed", "사망 재생 속도");

            BuildFeedbackEditor(
                container,
                serializedDraft.FindProperty("deathFeedback"),
                "사망 애니메이션 시작",
                "사망 사운드",
                "사망 애니메이션 시작 시 재생됩니다. AudioClip만 지정해도 전투 반영 때 " +
                "SFX Cue를 자동 생성합니다.",
                MonsterMakerPreviewAnchor.HitCenter,
                false);
        }

        private void BuildCastle()
        {
            var container = Section("castle");
            AddEnumProperty(
                container,
                "castleRaidAiPattern",
                "행동 패턴",
                CastleRaidPatternLabels,
                true);
            AddHelp(
                container,
                "군단의 역습에서만 사용하는 목표 선택 규칙입니다. 메인 전투 AI에는 영향을 주지 않습니다.",
                HelpBoxMessageType.Info);
            var pattern = (HexCastleAssaultPattern)(
                serializedDraft.FindProperty("castleRaidAiPattern")?.enumValueIndex ?? 0);
            if (pattern != HexCastleAssaultPattern.TacticalSupport)
            {
                return;
            }

            var support = AddSubFoldout(container, "전술 지원 세부 설정", true);
            AddEnumProperty(
                support,
                "castleRaidSupportFocus",
                "지원 성향",
                CastleRaidSupportLabels,
                false);
            AddProperty(support, "castleRaidSupportRange", "지원 범위");
            AddProperty(support, "castleRaidSupportCooldown", "지원 재사용 시간");
            AddProperty(support, "castleRaidSupportDuration", "강화 지속 시간");
            AddProperty(support, "castleRaidHealRatio", "최대 체력 회복 비율");
            AddProperty(support, "castleRaidAttackBuffRate", "공격력 증가 비율");
            AddProperty(support, "castleRaidDefenseDamageMultiplier", "받는 피해 배율");
        }

        private void BuildAscension()
        {
            var container = Section("ascension");
            AddProperty(container, "ascensionConfigured", "돌파 옵션 사용");
            var configured =
                serializedDraft.FindProperty("ascensionConfigured")?.boolValue ?? false;
            if (!configured)
            {
                AddHelp(
                    container,
                    "미설정 상태로도 전투 편입할 수 있으며 돌파 능력치와 스킬은 적용되지 않습니다.",
                    HelpBoxMessageType.Info);
                return;
            }

            BuildStatModifier(container, "ascension1", "1돌파 능력치");
            var usesPassive = draft?.UsePassiveSkill == true;
            var usesActive = draft?.UseActiveSkill == true &&
                             draft.Rarity >= MonsterRarity.Legendary;
            if (usesPassive)
            {
                BuildSkillAugment(container, "ascension2", "2돌파 · 패시브 강화", false);
            }
            else
            {
                AddHelp(
                    container,
                    "패시브를 사용하면 2돌파 스킬 강화 항목이 열립니다. 기존 값은 보존됩니다.",
                    HelpBoxMessageType.Info);
            }

            BuildStatModifier(container, "ascension3", "3돌파 능력치");
            if (usesActive || usesPassive)
            {
                BuildSkillAugment(
                    container,
                    "ascension4",
                    usesActive ? "4돌파 · 액티브 강화" : "4돌파 · 패시브 추가 강화",
                    usesActive);
            }
            BuildStatModifier(container, "ascension5", "5돌파 능력치");
        }

        private void BuildStatModifier(
            VisualElement container,
            string propertyName,
            string label)
        {
            var modifier = serializedDraft.FindProperty(propertyName);
            var foldout = AddSubFoldout(container, label, false);
            AddRelativeProperty(foldout, modifier?.FindPropertyRelative("healthRate"), "체력 증가율");
            AddRelativeProperty(foldout, modifier?.FindPropertyRelative("attackRate"), "공격력 증가율");
            AddRelativeProperty(foldout, modifier?.FindPropertyRelative("defenseRate"), "방어력 증가율");
            AddRelativeProperty(foldout, modifier?.FindPropertyRelative("attackSpeedRate"), "공격 속도 증가율");
            AddRelativeProperty(foldout, modifier?.FindPropertyRelative("moveSpeedRate"), "이동 속도 증가율");
            AddRelativeProperty(foldout, modifier?.FindPropertyRelative("attackRangeRate"), "공격 사거리 증가율");
        }

        private void BuildSkillAugment(
            VisualElement container,
            string propertyName,
            string label,
            bool targetsActive)
        {
            var ability = serializedDraft.FindProperty(propertyName);
            var foldout = AddSubFoldout(container, label, false);
            AddRelativeProperty(foldout, ability?.FindPropertyRelative("abilityId"), "스킬 ID");
            AddRelativeProperty(foldout, ability?.FindPropertyRelative("displayName"), "강화 이름");
            AddHelp(
                foldout,
                targetsActive ? "대상 · 현재 선택한 액티브" : "대상 · 현재 선택한 패시브",
                HelpBoxMessageType.Info);

            var operation = ability?.FindPropertyRelative("augmentOperation");
            if (operation == null)
            {
                AddHelp(foldout, "강화 방식을 찾을 수 없습니다.", HelpBoxMessageType.Error);
                return;
            }

            var choices = new List<string>(SkillAugmentOperationLabels);
            var currentIndex = Mathf.Clamp(operation.enumValueIndex, 0, choices.Count - 1);
            var popup = new PopupField<string>("강화 방식", choices, currentIndex);
            popup.AddToClassList("draft-property");
            popup.RegisterValueChangedCallback(evt =>
            {
                var next = choices.IndexOf(evt.newValue);
                if (next >= 0)
                {
                    ApplyAndRebuild(
                        $"Monster Maker V2 · {label} 방식 변경",
                        () => operation.enumValueIndex = next);
                }
            });
            foldout.Add(popup);

            var enumValue = (MonsterSkillAugmentOperation)currentIndex;
            switch (enumValue)
            {
                case MonsterSkillAugmentOperation.MagnitudeMultiplier:
                    AddRelativeProperty(
                        foldout,
                        ability.FindPropertyRelative("augmentScalarValue"),
                        "효과량 증가율");
                    break;
                case MonsterSkillAugmentOperation.DurationBonusSeconds:
                    AddRelativeProperty(
                        foldout,
                        ability.FindPropertyRelative("augmentScalarValue"),
                        "추가 지속 시간(초)");
                    break;
                case MonsterSkillAugmentOperation.CooldownReductionRate:
                    AddRelativeProperty(
                        foldout,
                        ability.FindPropertyRelative("augmentScalarValue"),
                        "쿨다운 감소율");
                    break;
                default:
                    AddRelativeProperty(
                        foldout,
                        ability.FindPropertyRelative("augmentIntegerValue"),
                        "증감 횟수");
                    break;
            }
        }

        private void OnPropertyChanged(SerializedPropertyChangeEvent evt)
        {
            if (building || serializedDraft == null)
            {
                return;
            }

            var path = evt.changedProperty?.propertyPath ?? string.Empty;
            serializedDraft.ApplyModifiedProperties();
            ReconcileDependentAuthoring(path);
            changed?.Invoke();
            if (RequiresRebuild(path))
            {
                EditorApplication.delayCall -= Rebuild;
                EditorApplication.delayCall += Rebuild;
            }
        }

        private void ReconcileDependentAuthoring(string propertyPath)
        {
            if (draft == null)
            {
                return;
            }

            if (propertyPath == "rarityPassiveSkill")
            {
                if (draft.RarityPassiveSkill == null ||
                    draft.RarityPassiveSkill is GenericMonsterPassiveSkill)
                {
                    draft.EditorSetPassiveTemplate(
                        draft.RarityPassiveSkill as GenericMonsterPassiveSkill);
                }
            }
            else if (propertyPath == "usePassiveSkill" ||
                     propertyPath == "useActiveSkill")
            {
                draft.EditorCommitSplitSkillUsage();
            }
            else if (propertyPath == "activeAttackProfile")
            {
                draft.EditorSetActiveAttackProfile(draft.ActiveAttackProfile);
            }
            else if (propertyPath == "activeEffectProfile")
            {
                draft.EditorSetActiveEffectProfile(draft.ActiveEffectProfile);
            }
            else if (propertyPath == "basicAttackProfile")
            {
                draft.EditorSetBasicAttackProfile(draft.BasicAttackProfile);
            }

            serializedDraft.UpdateIfRequiredOrScript();
        }

        private static bool RequiresRebuild(string propertyPath)
        {
            return propertyPath == "rarity" ||
                   propertyPath == "usePassiveSkill" ||
                   propertyPath == "useActiveSkill" ||
                   propertyPath == "activeAttackProfile" ||
                   propertyPath == "activeEffectProfile" ||
                   propertyPath == "useCustomActiveStepMotions" ||
                   propertyPath == "basicAttackProfile" ||
                   propertyPath == "castleRaidAiPattern" ||
                   propertyPath == "ascensionConfigured" ||
                   propertyPath == "mainBattleRole" ||
                   propertyPath.EndsWith(".vfxPrefab", StringComparison.Ordinal) ||
                   propertyPath.EndsWith(".sound", StringComparison.Ordinal);
        }

        private void ApplyAndRebuild(string undoName, Action mutation)
        {
            if (draft == null || serializedDraft == null)
            {
                return;
            }

            serializedDraft.ApplyModifiedProperties();
            Undo.RegisterCompleteObjectUndo(draft, undoName);
            serializedDraft.UpdateIfRequiredOrScript();
            mutation?.Invoke();
            serializedDraft.ApplyModifiedPropertiesWithoutUndo();
            changed?.Invoke();
            Rebuild();
        }

        private void ApplyObjectMutationAndRebuild(string undoName, Action mutation)
        {
            if (draft == null || serializedDraft == null)
            {
                return;
            }

            serializedDraft.ApplyModifiedProperties();
            Undo.RegisterCompleteObjectUndo(draft, undoName);
            mutation?.Invoke();
            EditorUtility.SetDirty(draft);
            serializedDraft.UpdateIfRequiredOrScript();
            changed?.Invoke();
            Rebuild();
        }

        private void SetFloatProperty(string propertyPath, float value, string undoName)
        {
            if (draft == null || serializedDraft == null)
            {
                return;
            }

            serializedDraft.ApplyModifiedProperties();
            Undo.RecordObject(draft, undoName);
            serializedDraft.UpdateIfRequiredOrScript();
            var property = serializedDraft.FindProperty(propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.Float)
            {
                return;
            }

            property.floatValue = value;
            serializedDraft.ApplyModifiedPropertiesWithoutUndo();
            changed?.Invoke();
        }

        private VisualElement Section(string key)
        {
            return root.Q<VisualElement>("content-" + key);
        }

        private void ClearSection(string key)
        {
            Section(key)?.Clear();
        }

        private PropertyField AddProperty(
            VisualElement container,
            string propertyName,
            string label)
        {
            return AddRelativeProperty(
                container,
                serializedDraft?.FindProperty(propertyName),
                label,
                "field-" + propertyName);
        }

        private PopupField<string> AddEnumProperty(
            VisualElement container,
            string propertyName,
            string label,
            IReadOnlyList<string> labels,
            bool rebuildOnChange)
        {
            var property = serializedDraft?.FindProperty(propertyName);
            if (container == null || property == null ||
                property.propertyType != SerializedPropertyType.Enum ||
                labels == null || labels.Count == 0)
            {
                AddHelp(
                    container,
                    $"선택 항목을 찾을 수 없습니다: {label}",
                    HelpBoxMessageType.Error);
                return null;
            }

            var choices = new List<string>(labels);
            var currentIndex = Mathf.Clamp(property.enumValueIndex, 0, choices.Count - 1);
            var popup = new PopupField<string>(label, choices, currentIndex)
            {
                name = "field-" + propertyName
            };
            popup.AddToClassList("draft-property");
            popup.RegisterValueChangedCallback(evt =>
            {
                var nextIndex = choices.IndexOf(evt.newValue);
                if (nextIndex < 0)
                {
                    return;
                }

                if (rebuildOnChange)
                {
                    ApplyAndRebuild(
                        $"Monster Maker V2 · {label} 변경",
                        () => property.enumValueIndex = nextIndex);
                }
                else
                {
                    SetEnumProperty(
                        propertyName,
                        nextIndex,
                        $"Monster Maker V2 · {label} 변경");
                }
            });
            container.Add(popup);
            return popup;
        }

        private void SetEnumProperty(string propertyPath, int value, string undoName)
        {
            if (draft == null || serializedDraft == null)
            {
                return;
            }

            serializedDraft.ApplyModifiedProperties();
            Undo.RecordObject(draft, undoName);
            serializedDraft.UpdateIfRequiredOrScript();
            var property = serializedDraft.FindProperty(propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
            {
                return;
            }

            property.enumValueIndex = value;
            serializedDraft.ApplyModifiedPropertiesWithoutUndo();
            changed?.Invoke();
        }

        private PropertyField AddRelativeProperty(
            VisualElement container,
            SerializedProperty property,
            string label,
            string name = null)
        {
            if (container == null)
            {
                return null;
            }

            if (property == null)
            {
                AddHelp(
                    container,
                    $"직렬화 속성을 찾을 수 없습니다: {label}",
                    HelpBoxMessageType.Error);
                return null;
            }

            var field = new PropertyField(property.Copy(), label)
            {
                name = name ?? "field-" + property.propertyPath.Replace('.', '-')
            };
            field.AddToClassList("draft-property");
            container.Add(field);
            fields[property.propertyPath] = field;
            return field;
        }

        private void AddPropertyWithAction(
            VisualElement container,
            string propertyName,
            string label,
            string buttonLabel,
            Action action)
        {
            AddProperty(container, propertyName, label);
            var row = new VisualElement();
            row.AddToClassList("draft-action-row");
            var button = new Button(action) { text = buttonLabel };
            button.AddToClassList("draft-action-button");
            row.Add(button);
            container.Add(row);
        }

        private Foldout AddSubFoldout(
            VisualElement container,
            string title,
            bool expanded,
            string stateKey = null)
        {
            var key = string.IsNullOrWhiteSpace(stateKey)
                ? "foldout:" + title
                : stateKey;
            var value = foldoutStates.TryGetValue(key, out var stored)
                ? stored
                : expanded;
            var foldout = new Foldout { text = title, value = value };
            foldout.RegisterValueChangedCallback(evt => foldoutStates[key] = evt.newValue);
            foldout.AddToClassList("draft-subfoldout");
            container.Add(foldout);
            return foldout;
        }

        private static VisualElement AddActionRow(
            VisualElement container,
            params (string Label, Action Action, string ClassName)[] actions)
        {
            var row = new VisualElement();
            row.AddToClassList("draft-action-row");
            foreach (var action in actions)
            {
                var button = new Button(action.Action) { text = action.Label };
                button.AddToClassList(
                    string.IsNullOrWhiteSpace(action.ClassName)
                        ? "draft-action-button"
                        : action.ClassName);
                row.Add(button);
            }
            container.Add(row);
            return row;
        }

        private static HelpBox AddHelp(
            VisualElement container,
            string text,
            HelpBoxMessageType type)
        {
            var help = new HelpBox(text, type);
            help.AddToClassList("draft-help");
            if (type == HelpBoxMessageType.Info) help.AddToClassList("draft-help--optional");
            container.Add(help);
            return help;
        }

        private static VisualElement AddSummary(
            VisualElement container,
            string title,
            string body)
        {
            var card = new VisualElement();
            card.AddToClassList("summary-card");
            var heading = new Label(title);
            heading.AddToClassList("summary-title");
            var content = new Label(body);
            content.AddToClassList("summary-body");
            card.Add(heading);
            card.Add(content);
            container.Add(card);
            return card;
        }

        private static string ResolveMainBattleRoleHelp(MainBattleMonsterRole role)
        {
            return role switch
            {
                MainBattleMonsterRole.Guardian =>
                    "수호: 전열을 지키며 대상이 한 곳에 몰리지 않게 분산합니다.",
                MainBattleMonsterRole.Finisher =>
                    "마무리: 체력이 낮은 적을 우선해 전투 수를 빠르게 줄입니다.",
                MainBattleMonsterRole.Marksman =>
                    "사수: 원거리 희망 거리를 유지하며 적이 너무 가까우면 후퇴합니다.",
                MainBattleMonsterRole.BacklineHunter =>
                    "후열 추적: 원거리 적을 우선 선택하고 안전거리를 유지합니다.",
                _ => "선봉: 가까운 적에게 빠르게 접근해 전투선을 먼저 형성합니다."
            };
        }
    }
}
