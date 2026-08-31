using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public sealed partial class MonsterEffectActiveWorkshopWindow : EditorWindow // 통합 액티브 조립소의 효과형 화면
    {
        private const float LibraryWidth = 285f;
        private const float AssemblerWidth = 480f;
        private const float ContentWidth = AssemblerWidth - 30f;
        private const float PreviewMinimumWidth = 300f;
        private readonly List<MonsterEffectActiveProfile> profiles = new List<MonsterEffectActiveProfile>();
        private readonly Dictionary<MonsterEffectActiveProfile, int> usages =
            new Dictionary<MonsterEffectActiveProfile, int>();
        private MonsterEffectActiveProfile profile;
        private MonsterEffectActiveProfile loadedProfile;
        private MonsterMakerDraft originDraft;
        private SerializedObject serializedProfile;
        private Vector2 libraryScroll;
        private Vector2 assemblerScroll;
        private string search = string.Empty;
        private bool dirty;
        private string message = string.Empty;
        private MessageType messageType = MessageType.Info;
        private bool previewPlaying;
        private bool previewAllGroups;
        private double previewStartedAt;
        private int selectedPreviewGroup;
        private Rect lastAssemblerContentRect;
        private Rect lastAssemblerViewportRect;
        private Rect lastGroupHeaderRightmostRect;
        private Rect lastSaveRightmostRect;
        private Rect lastPreviewColumnRect;
        private Rect lastPreviewToolbarRightmostRect;

        public static event Action PresetAssigned;

        [MenuItem("JC Tool/Monster/Legacy/효과형 액티브 조립소 V1")]
        private static void OpenLegacyMenu() => OpenFor(null, null);

        private static readonly MonsterSkillTargetType[] AllyTargets =
        {
            MonsterSkillTargetType.Self,
            MonsterSkillTargetType.LowestHealthAlly,
            MonsterSkillTargetType.HighestAttackAlly,
            MonsterSkillTargetType.NearbyAllies,
            MonsterSkillTargetType.AllAllies
        };

        private static readonly MonsterSkillTargetType[] EnemyTargets =
        {
            MonsterSkillTargetType.CurrentTarget,
            MonsterSkillTargetType.NearestEnemy,
            MonsterSkillTargetType.FarthestEnemy,
            MonsterSkillTargetType.LowestHealthEnemy,
            MonsterSkillTargetType.HighestAttackEnemy,
            MonsterSkillTargetType.RangedEnemyFirst,
            MonsterSkillTargetType.TargetAreaEnemies
        };

        private static readonly MonsterSkillTargetType[] GuardTargets =
            AllyTargets.Concat(new[]
            {
                MonsterSkillTargetType.CurrentTarget,
                MonsterSkillTargetType.NearestEnemy,
                MonsterSkillTargetType.TargetAreaEnemies
            }).ToArray();
        private static readonly MonsterSkillEffectType[] SupportEffects =
        {
            MonsterSkillEffectType.Heal,
            MonsterSkillEffectType.AttackBuff,
            MonsterSkillEffectType.AttackSpeedBuff,
            MonsterSkillEffectType.EnergyGain
        };

        private static readonly MonsterSkillEffectType[] GuardEffects =
        {
            MonsterSkillEffectType.Shield,
            MonsterSkillEffectType.DefenseBuff,
            MonsterSkillEffectType.DamageReduction,
            MonsterSkillEffectType.Taunt
        };

        private static readonly MonsterSkillEffectType[] DebuffEffects =
        {
            MonsterSkillEffectType.AttackDebuff,
            MonsterSkillEffectType.DefenseDebuff,
            MonsterSkillEffectType.AttackSpeedDebuff,
            MonsterSkillEffectType.MoveSpeedDebuff,
            MonsterSkillEffectType.Mark,
            MonsterSkillEffectType.Slow,
            MonsterSkillEffectType.Stun,
            MonsterSkillEffectType.Pull,
            MonsterSkillEffectType.EnergyDrain
        };

        public static void OpenFor(MonsterEffectActiveProfile target, MonsterMakerDraft draft = null)
        {
            foreach (var stale in Resources.FindObjectsOfTypeAll<MonsterEffectActiveWorkshopWindow>())
            {
                stale.Close();
            }
            var window = CreateInstance<MonsterEffectActiveWorkshopWindow>();
            window.titleContent = new GUIContent("액티브 스킬 조립소");
            window.minSize = new Vector2(1100f, 700f);
            var main = EditorGUIUtility.GetMainWindowPosition();
            var width = Mathf.Clamp(main.width - 120f, 1100f, 1380f);
            var height = Mathf.Clamp(main.height - 120f, 700f, 900f);
            window.position = new Rect(
                main.x + (main.width - width) * 0.5f,
                main.y + (main.height - height) * 0.5f,
                width,
                height);
            window.originDraft = draft;
            if (target == null) window.StartBlank();
            else window.LoadProfile(target);
            window.ShowUtility();
            window.Focus();
        }

        public override void SaveChanges()
        {
            if (loadedProfile == null) SaveAsNew();
            else UpdateLoaded();
            if (!dirty) base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            SetDirty(false);
            base.DiscardChanges();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("액티브 스킬 조립소");
            minSize = new Vector2(1100f, 700f);
            RefreshProfiles();
            if (profile == null) StartBlank();
            EditorApplication.projectChanged += RefreshProfiles;
            EditorApplication.update += TickPreview;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshProfiles;
            EditorApplication.update -= TickPreview;
            DisposeWorkingCopy();
        }

        private void OnGUI()
        {
            MonsterWorkshopVisualTheme.DrawHeader(
                "액티브 스킬 조립소",
                "공격형과 효과형이 같은 기력·모션·발동 연출 흐름을 사용합니다");
            DrawModeToolbar();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLibrary();
                DrawAssembler();
                DrawPreview();
            }
        }

        private void DrawModeToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button("공격형", GUILayout.Height(30f)))
                {
                    var draft = originDraft;
                    var attack = draft?.ActiveAttackProfile;
                    EditorApplication.delayCall += () =>
                    {
                        MonsterActiveAttackWorkshopWindow.OpenFor(attack, draft);
                        Close();
                    };
                    GUIUtility.ExitGUI();
                }
                var previous = GUI.backgroundColor;
                GUI.backgroundColor = Color.Lerp(Color.white, MonsterWorkshopVisualTheme.PrimaryColor, 0.55f);
                GUILayout.Button("효과형 · 지원 / 수호 / 디버프", GUILayout.Height(30f));
                GUI.backgroundColor = previous;
            }
        }

        private void DrawLibrary()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(LibraryWidth)))
            {
                GUILayout.Label("저장된 프리셋", EditorStyles.boldLabel);
                if (MonsterWorkshopVisualTheme.DrawTintedButton(
                        new GUIContent("+ 빈 효과형 액티브 조립"),
                        MonsterWorkshopVisualTheme.PrimaryColor,
                        28f))
                {
                    StartBlank();
                }

                if (originDraft != null)
                {
                    var assigned = originDraft.ActiveEffectProfile;
                    GUILayout.Label(
                        assigned == null
                            ? $"현재 {originDraft.MonsterId} · 효과형 미배정"
                            : $"현재 {originDraft.MonsterId} · [{assigned.ProfileId}]",
                        EditorStyles.miniLabel);
                    using (new EditorGUI.DisabledScope(assigned == null))
                    {
                        if (GUILayout.Button(
                                assigned == null ? "현재 배정 프리셋 없음" : "현재 배정 프리셋 불러오기",
                                GUILayout.Height(24f)))
                        {
                            LoadProfile(assigned);
                        }
                    }
                }

                search = EditorGUILayout.TextField("검색", search);
                libraryScroll = MonsterWorkshopVisualTheme.BeginVerticalScrollView(libraryScroll);
                GUILayout.Space(4f);
                GUILayout.Label($"프리셋 {profiles.Count(MatchesSearch)}종", EditorStyles.miniBoldLabel);
                foreach (var candidate in profiles)
                {
                    if (!MatchesSearch(candidate)) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var usage = usages.TryGetValue(candidate, out var count) ? count : 0;
                        if (MonsterWorkshopVisualTheme.DrawPresetButton(
                                new GUIContent(
                                    $"[{RoleBadge(candidate.Role)}] [{candidate.ProfileId}] {candidate.DisplayName}",
                                    $"현재 {usage}마리가 사용 · {candidate.Description}"),
                                candidate == loadedProfile))
                        {
                            LoadProfile(candidate);
                        }
                        GUILayout.Label(usage.ToString(), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(24f));
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawAssembler()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(AssemblerWidth)))
            {
                GUILayout.Label("효과형 액티브 조립", EditorStyles.boldLabel);
                if (loadedProfile == null)
                {
                    GUILayout.Label(
                        "빈 작업 사본 · 저장 전에는 기존 프리셋에 영향을 주지 않습니다.",
                        EditorStyles.wordWrappedMiniLabel);
                }
                else
                {
                    var assetPath = AssetDatabase.GetAssetPath(loadedProfile);
                    GUILayout.Label(
                        new GUIContent($"직접 수정 중 · {loadedProfile.name}", assetPath),
                        EditorStyles.miniLabel);
                }
                if (loadedProfile != null)
                {
                    GUILayout.Label(
                        "프리셋 ID만 잠깁니다. 이름·역할·효과 묶음은 바로 편집한 뒤 아래 업데이트로 저장합니다.",
                        EditorStyles.wordWrappedMiniLabel);
                    if (GUILayout.Button("다른 프리셋으로 복제", EditorStyles.miniButton, GUILayout.Height(22f)))
                    {
                        loadedProfile = null;
                        serializedProfile.FindProperty("profileId").stringValue = profile.ProfileId + "_copy";
                        serializedProfile.ApplyModifiedProperties();
                        OnChanged();
                    }
                }

                serializedProfile.UpdateIfRequiredOrScript();
                assemblerScroll = MonsterWorkshopVisualTheme.BeginVerticalScrollView(assemblerScroll);
                using (var contentScope = new EditorGUILayout.VerticalScope(GUILayout.Width(ContentWidth)))
                {
                    DrawMetadata();
                    GUILayout.Space(7f);
                    DrawGroups();
                    GUILayout.Space(7f);
                    DrawValidation();
                    if (Event.current.type == EventType.Repaint)
                    {
                        lastAssemblerContentRect = contentScope.rect;
                    }
                }
                EditorGUILayout.EndScrollView();
                if (Event.current.type == EventType.Repaint)
                {
                    lastAssemblerViewportRect = GUILayoutUtility.GetLastRect();
                }
                if (serializedProfile.ApplyModifiedProperties()) OnChanged();
                GUILayout.Space(7f);
                DrawSaveControls();
            }
        }

        private void DrawMetadata()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("프로필 정보", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(loadedProfile != null))
                {
                    EditorGUILayout.PropertyField(
                        serializedProfile.FindProperty("profileId"),
                        new GUIContent("프리셋 ID"));
                }
                EditorGUILayout.PropertyField(
                    serializedProfile.FindProperty("displayName"),
                    new GUIContent("표시 이름"));
                EditorGUILayout.PropertyField(
                    serializedProfile.FindProperty("description"),
                    new GUIContent("기획 메모"));
                GUILayout.Space(4f);
                GUILayout.Label("주 역할", EditorStyles.miniBoldLabel);
                var role = (MonsterEffectActiveRole)serializedProfile.FindProperty("role").enumValueIndex;
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawRoleButton(MonsterEffectActiveRole.Support, "지원", role);
                    DrawRoleButton(MonsterEffectActiveRole.Guard, "수호", role);
                    DrawRoleButton(MonsterEffectActiveRole.Debuff, "디버프", role);
                }
                GUILayout.Label(RoleDescription(role), EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawRoleButton(
            MonsterEffectActiveRole value,
            string label,
            MonsterEffectActiveRole current)
        {
            var previous = GUI.backgroundColor;
            if (value == current) GUI.backgroundColor = Color.Lerp(Color.white, RoleColor(value), 0.62f);
            if (GUILayout.Button(label, GUILayout.Height(27f)) && value != current)
            {
                serializedProfile.FindProperty("role").enumValueIndex = (int)value;
                NormalizeForRole(value);
                serializedProfile.ApplyModifiedProperties();
                OnChanged();
            }
            GUI.backgroundColor = previous;
        }
    }
}
