using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MoreMountains.Feedbacks;
using ProjectMT.Integrations.Feel;
using ProjectMT.Shared.Unit;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectMT.Tools.FeelPreview
{
    public sealed partial class CombatFeelCatalogPreviewLab
    {
        private void DrawLabModeTabs()
        {
            GUILayout.BeginHorizontal();
            DrawLabModeButton(LabMode.Library, "효과 보기");
            DrawLabModeButton(LabMode.Composer, "프로필 만들기");
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        private void DrawLabModeButton(LabMode mode, string label)
        {
            if (!GUILayout.Button(label, labMode == mode ? activeTabStyle : tabStyle, GUILayout.Height(34f)) || labMode == mode)
            {
                return;
            }

            ResetPreview();
            ResetAuthoringPreview();
            labMode = mode;
        }

        private void DrawAuthoringModePanel(float width, float height)
        {
#if UNITY_EDITOR
            DrawComposerPanel(width, height);
#else
            GUILayout.BeginVertical(infoStyle);
            GUILayout.Label("Editor 전용 기능입니다.", titleStyle);
            GUILayout.Label("FEEL Prefab 저장과 Profile 조회는 Unity Editor에서만 사용할 수 있습니다.", textStyle);
            GUILayout.EndVertical();
#endif
        }

        private void ResetAuthoringPreview()
        {
#if UNITY_EDITOR
            authoringPlayVersion++;
            if (authoringStopRoutine != null)
            {
                StopCoroutine(authoringStopRoutine);
                authoringStopRoutine = null;
            }
            if (activeAuthoringPreview != null)
            {
                var runtime = activeAuthoringPreview.GetComponent<BasicAttackFeelRuntimeAdapter>();
                runtime?.ResetBasicAttackFeel();
                activeAuthoringPreview.SetActive(false);
                DestroyAuthoringObject(activeAuthoringPreview);
                activeAuthoringPreview = null;
            }
            RestoreRuntimeState();
#endif
        }

        private void DestroyAuthoringSession()
        {
#if UNITY_EDITOR
            ResetAuthoringPreview();
            DisposeWorkingRoot();
#endif
        }

#if UNITY_EDITOR
        private const string FeelRoot = "Assets/ProjectMT/05_Art/FeelPresets/BasicAttack";
        private const string ProductionRoot = FeelRoot + "/Production";
        private const string UserProfileRoot = FeelRoot + "/Profiles";
        private const string GlobalPrefix = "[Global]";
        private const string PrefabTargetToken = "[PrefabTarget]";

        private GameObject workingFeelRoot;
        private MMF_Player workingPlayer;
        private MMF_ReferenceHolder workingReference;
        private BasicAttackFeelRuntimeAdapter workingAdapter;
        private GameObject loadedSourcePrefab;
        private GameObject activeAuthoringPreview;
        private Coroutine authoringStopRoutine;
        private int authoringPlayVersion;
        private int selectedLayerIndex = 1;
        private bool authoringDirty;
        private bool authoringAssetsDirty = true;
        private string authoringPresetName = "새 타격감";
        private string currentProfilePath;
        private string authoringNotice = "새 프로필을 만들거나 기존 프로필을 불러오세요.";
        private float cueLifetime = 0.85f;
        private float cueScale = 1f;
        private float authoringIntensity = 1f;
        private Vector3 cuePosition;
        private Vector3 cueEuler;
        private Tab authoringCatalogTab;
        private Vector2 authoringStackScroll;
        private Vector2 authoringInspectorScroll;
        private Vector2 authoringCatalogScroll;
        private GameObject[] cachedFeelProfiles = Array.Empty<GameObject>();
        private static readonly Dictionary<string, Type> AuthoringFeedbackTypeCache = new Dictionary<string, Type>(StringComparer.Ordinal);

        public int AuthoringExecutableLayerCount => workingPlayer?.FeedbacksList?.Count(feedback => feedback != null && !(feedback is MMF_ReferenceHolder)) ?? 0;
        public int AuthoringRuntimeLayerCount => workingPlayer?.FeedbacksList?.Count(IsRuntimePlayableFeedback) ?? 0;
        public bool AuthoringDirty => authoringDirty;
        public string AuthoringCurrentSourcePath => loadedSourcePrefab != null ? AssetDatabase.GetAssetPath(loadedSourcePrefab) : string.Empty;

        private void DrawComposerPanel(float width, float height)
        {
            EnsureWorkingSession();
            DrawComposerHeader();
            DrawCuePlacement();

            var availableHeight = Mathf.Max(300f, height - 338f);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width((width - 54f) * 0.47f));
            GUILayout.Label($"효과 스택  ·  전체 {AuthoringExecutableLayerCount}개  ·  실전 {AuthoringRuntimeLayerCount}개", sectionStyle);
            DrawWorkingStack(availableHeight);
            GUILayout.EndVertical();

            GUILayout.Space(8f);
            GUILayout.BeginVertical(GUILayout.Width((width - 54f) * 0.53f));
            DrawSelectedLayerInspector(availableHeight * 0.58f);
            GUILayout.Space(5f);
            DrawEffectCatalog(availableHeight * 0.40f);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Space(7f);
            DrawComposerFooter();
        }

        private void DrawComposerHeader()
        {
            EnsureAuthoringAssets();
            GUILayout.BeginVertical(infoStyle);
            GUILayout.BeginHorizontal();
            var source = loadedSourcePrefab != null ? ProfileDisplayName(loadedSourcePrefab) : "새 프로필";
            GUILayout.Label($"{source}{(authoringDirty ? "  ● 수정됨" : string.Empty)}", sectionStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("새로 만들기", tabStyle, GUILayout.Width(94f), GUILayout.Height(27f)) && ConfirmDiscardChanges())
                CreateBlankWorkingSession();
            if (GUILayout.Button("새로고침", tabStyle, GUILayout.Width(82f), GUILayout.Height(27f))) RefreshAuthoringAssets();
            GUILayout.EndHorizontal();

            DrawProfileSelector();
            GUILayout.Label("연구소는 FEEL 프로필만 만듭니다. 기본공격 연결은 Monster Maker에서 선택합니다.", textStyle);
            var editingUserProfile = IsUserProfileAsset(currentProfilePath);
            GUILayout.BeginHorizontal();
            GUILayout.Label(editingUserProfile ? "복사본 이름" : "프로필 이름", textStyle, GUILayout.Width(76f));
            var nextName = GUILayout.TextField(authoringPresetName, GUILayout.Height(25f));
            if (nextName != authoringPresetName) authoringPresetName = nextName;
            GUI.enabled = AuthoringRuntimeLayerCount > 0;
            if (editingUserProfile)
            {
                if (GUILayout.Button("현재 저장", activeTabStyle, GUILayout.Width(88f), GUILayout.Height(27f))) SaveWorkingProfile(false, true);
                if (GUILayout.Button("복사본 저장", tabStyle, GUILayout.Width(100f), GUILayout.Height(27f))) SaveWorkingProfile(true, false);
            }
            else if (GUILayout.Button("내 프로필 저장", activeTabStyle, GUILayout.Width(112f), GUILayout.Height(27f)))
            {
                SaveWorkingProfile(false, false);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (editingUserProfile)
                GUILayout.Label("현재 저장은 연결된 프로필을 갱신하고, 복사본 저장은 입력한 이름으로 새 프로필을 만듭니다.", textStyle);
            else if (loadedSourcePrefab != null && !IsUserProfileAsset(AssetDatabase.GetAssetPath(loadedSourcePrefab)))
                GUILayout.Label("기본 제공·참고 프로필은 원본을 보호하며, 저장하면 내 프로필로 복사됩니다.", textStyle);
            else
                GUILayout.Label("효과를 조립한 뒤 이름을 정하고 내 프로필로 저장하세요.", textStyle);
            GUILayout.EndVertical();
        }

        private void DrawProfileSelector()
        {
            var options = new string[cachedFeelProfiles.Length + 1];
            options[0] = "새 프로필 (저장 전)";
            for (var index = 0; index < cachedFeelProfiles.Length; index++)
                options[index + 1] = ProfileOptionLabel(cachedFeelProfiles[index]);

            var currentIndex = loadedSourcePrefab == null
                ? 0
                : Mathf.Max(0, Array.IndexOf(cachedFeelProfiles, loadedSourcePrefab) + 1);
            var nextIndex = EditorGUILayout.Popup("프로필 열기", currentIndex, options);
            if (nextIndex == currentIndex || nextIndex < 0 || nextIndex > cachedFeelProfiles.Length)
                return;
            if (!ConfirmDiscardChanges())
                return;
            if (nextIndex == 0)
            {
                CreateBlankWorkingSession();
                return;
            }
            LoadWorkingPreset(cachedFeelProfiles[nextIndex - 1]);
        }

        private void DrawCuePlacement()
        {
            GUILayout.Space(6f);
            GUILayout.BeginVertical(infoStyle);
            GUILayout.Label("프로필 타격점", sectionStyle);
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal();
            cueLifetime = Mathf.Max(0.05f, EditorGUILayout.FloatField("수명", cueLifetime));
            cueScale = Mathf.Max(0.01f, EditorGUILayout.FloatField("배율", cueScale));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            cuePosition = EditorGUILayout.Vector3Field("타격점 위치", cuePosition);
            cueEuler = EditorGUILayout.Vector3Field("타격점 회전", cueEuler);
            GUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) authoringDirty = true;
            GUILayout.EndVertical();
        }

        private void DrawWorkingStack(float height)
        {
            authoringStackScroll = GUILayout.BeginScrollView(authoringStackScroll, GUILayout.Height(height));
            if (workingPlayer?.FeedbacksList == null)
            {
                GUILayout.Label("편집 중인 프로필이 없습니다.", textStyle);
                GUILayout.EndScrollView();
                return;
            }

            for (var index = 0; index < workingPlayer.FeedbacksList.Count; index++)
            {
                var feedback = workingPlayer.FeedbacksList[index];
                if (feedback == null) continue;
                GUILayout.BeginVertical(index == selectedLayerIndex ? infoStyle : GUI.skin.box);
                GUILayout.BeginHorizontal();
                if (feedback is MMF_ReferenceHolder)
                {
                    GUILayout.Toggle(true, GUIContent.none, GUILayout.Width(18f));
                    GUILayout.Label("SYSTEM · Runtime Visual Target", sectionStyle);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("고정", textStyle, GUILayout.Width(34f));
                }
                else
                {
                    var nextActive = GUILayout.Toggle(feedback.Active, GUIContent.none, GUILayout.Width(18f));
                    if (nextActive != feedback.Active)
                    {
                        feedback.Active = nextActive;
                        MarkAuthoringDirty();
                    }
                    var timing = feedback.Timing;
                    var intensity = timing.UseIntensityInterval
                        ? $"  [{timing.IntensityIntervalMin:0.00}~{timing.IntensityIntervalMax:0.00}]"
                        : "  [전체]";
                    if (GUILayout.Button($"{index:00}  {DisplayName(feedback.GetType().Name)}\n<size=9>{feedback.GetType().Name}  ·  {timing.InitialDelay:0.000}s{intensity}</size>",
                            index == selectedLayerIndex ? activeEffectStyle : effectStyle,
                            GUILayout.Height(47f)))
                    {
                        selectedLayerIndex = index;
                    }
                    GUI.enabled = index > 1;
                    if (GUILayout.Button("▲", tabStyle, GUILayout.Width(28f), GUILayout.Height(24f))) { MoveLayer(index, index - 1); GUILayout.EndHorizontal(); GUILayout.EndVertical(); break; }
                    GUI.enabled = index < workingPlayer.FeedbacksList.Count - 1;
                    if (GUILayout.Button("▼", tabStyle, GUILayout.Width(28f), GUILayout.Height(24f))) { MoveLayer(index, index + 1); GUILayout.EndHorizontal(); GUILayout.EndVertical(); break; }
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }

        private void DrawSelectedLayerInspector(float height)
        {
            GUILayout.Label("선택 효과 주요값", sectionStyle);
            authoringInspectorScroll = GUILayout.BeginScrollView(authoringInspectorScroll, GUILayout.Height(height));
            var feedback = SelectedFeedback();
            if (feedback == null || feedback is MMF_ReferenceHolder)
            {
                GUILayout.BeginVertical(infoStyle);
                GUILayout.Label("효과 카드를 선택하세요.", textStyle);
                GUILayout.EndVertical();
                GUILayout.EndScrollView();
                return;
            }

            GUILayout.BeginVertical(infoStyle);
            GUILayout.Label(DisplayName(feedback.GetType().Name), titleStyle);
            GUILayout.Label(DemoExplanation(feedback.GetType().Name), textStyle);
            EditorGUI.BeginChangeCheck();
            feedback.Label = EditorGUILayout.TextField("레이어 이름", feedback.Label ?? string.Empty);
            feedback.Timing.InitialDelay = Mathf.Max(0f, EditorGUILayout.FloatField("시작 지연", feedback.Timing.InitialDelay));
            var duration = Mathf.Max(0.01f, feedback.FeedbackDuration);
            var nextDuration = Mathf.Max(0.01f, EditorGUILayout.FloatField("효과 시간", duration));
            if (!Mathf.Approximately(nextDuration, duration)) feedback.SetFeedbackDuration(nextDuration);
            DrawIntensityBand(feedback);
            DrawMajorFeedbackFields(feedback);
            DrawBoundAnchor(feedback);
            if (EditorGUI.EndChangeCheck()) MarkAuthoringDirty();
            GUILayout.Label(TargetStatus(feedback), textStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("복제", tabStyle, GUILayout.Height(28f))) DuplicateSelectedLayer();
            if (GUILayout.Button("선택값 초기화", tabStyle, GUILayout.Height(28f))) ResetSelectedLayerToShowcase();
            if (GUILayout.Button("삭제", tabStyle, GUILayout.Height(28f))) RemoveSelectedLayer();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private void DrawIntensityBand(MMF_Feedback feedback)
        {
            GUILayout.Label("강도 조건", sectionStyle);
            GUILayout.BeginHorizontal();
            DrawIntensityButton(feedback, "전체", false, 0f, 10f);
            DrawIntensityButton(feedback, "Light", true, 0f, 0.8f);
            DrawIntensityButton(feedback, "Standard", true, 0.8f, 1.25f);
            DrawIntensityButton(feedback, "Heavy", true, 1.25f, 10f);
            GUILayout.EndHorizontal();
            if (feedback.Timing.UseIntensityInterval)
            {
                GUILayout.BeginHorizontal();
                feedback.Timing.IntensityIntervalMin = EditorGUILayout.FloatField("최소", feedback.Timing.IntensityIntervalMin);
                feedback.Timing.IntensityIntervalMax = EditorGUILayout.FloatField("최대", feedback.Timing.IntensityIntervalMax);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawIntensityButton(MMF_Feedback feedback, string label, bool use, float min, float max)
        {
            var active = feedback.Timing.UseIntensityInterval == use && (!use ||
                Mathf.Approximately(feedback.Timing.IntensityIntervalMin, min) && Mathf.Approximately(feedback.Timing.IntensityIntervalMax, max));
            if (!GUILayout.Button(label, active ? activeTabStyle : tabStyle, GUILayout.Height(25f))) return;
            feedback.Timing.UseIntensityInterval = use;
            feedback.Timing.IntensityIntervalMin = min;
            feedback.Timing.IntensityIntervalMax = max;
            MarkAuthoringDirty();
        }

        private void DrawEffectCatalog(float height)
        {
            GUILayout.Label($"효과 추가 · 실제 설치 {Items.Count(item => ResolveFeedbackType(item.TypeName) != null)}/{Items.Length}", sectionStyle);
            GUILayout.BeginHorizontal();
            DrawAuthoringCatalogTab(Tab.Model, "모델 31");
            DrawAuthoringCatalogTab(Tab.Impact, "타격점 7");
            DrawAuthoringCatalogTab(Tab.Screen, "화면 32");
            GUILayout.EndHorizontal();
            authoringCatalogScroll = GUILayout.BeginScrollView(authoringCatalogScroll, GUILayout.Height(height));
            var candidates = Items.Where(item => item.Tab == authoringCatalogTab).ToArray();
            for (var index = 0; index < candidates.Length; index += 2)
            {
                GUILayout.BeginHorizontal();
                DrawAddEffectButton(candidates[index]);
                if (index + 1 < candidates.Length) DrawAddEffectButton(candidates[index + 1]);
                else GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        private void DrawAuthoringCatalogTab(Tab tab, string label)
        {
            if (GUILayout.Button(label, authoringCatalogTab == tab ? activeTabStyle : tabStyle, GUILayout.Height(26f)))
            {
                authoringCatalogTab = tab;
                authoringCatalogScroll = Vector2.zero;
            }
        }

        private void DrawAddEffectButton(Item item)
        {
            var resolved = ResolveFeedbackType(item.TypeName) != null;
            var support = resolved ? string.Empty : "\n<size=8>현재 패키지 미지원</size>";
            GUI.enabled = resolved;
            if (GUILayout.Button($"＋ {DisplayName(item.TypeName)}\n<size=9>{item.TypeName}</size>{support}", effectStyle, GUILayout.Height(resolved ? 43f : 52f)))
            {
                AddAuthoringEffect(item.TypeName);
            }
            GUI.enabled = true;
        }

        private void DrawComposerFooter()
        {
            GUILayout.BeginVertical(infoStyle);
            GUILayout.Label(authoringNotice, textStyle);
            GUILayout.Label(ValidateWorkingSession(), sectionStyle);
            GUILayout.BeginHorizontal();
            DrawPreviewWeight("Light", 0.62f);
            DrawPreviewWeight("Standard", 1f);
            DrawPreviewWeight("Heavy", 1.45f);
            GUI.enabled = workingPlayer != null && AuthoringExecutableLayerCount > 0;
            if (GUILayout.Button("전체 조합 재생", activeTabStyle, GUILayout.Height(34f))) PlayWorkingComposition();
            GUI.enabled = true;
            if (GUILayout.Button("정지 · 원상복구", tabStyle, GUILayout.Height(34f))) ResetAuthoringPreview();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawPreviewWeight(string label, float intensity)
        {
            if (GUILayout.Button(label, Mathf.Approximately(authoringIntensity, intensity) ? activeTabStyle : tabStyle, GUILayout.Height(34f)))
            {
                authoringIntensity = intensity;
                PlayWorkingComposition();
            }
        }

        private void EnsureWorkingSession()
        {
            if (workingFeelRoot == null || workingPlayer == null) CreateBlankWorkingSession();
        }

        private void CreateBlankWorkingSession()
        {
            ResetAuthoringPreview();
            DisposeWorkingRoot();
            workingFeelRoot = new GameObject("BAFeel_NewProfile") { hideFlags = HideFlags.HideAndDontSave };
            workingFeelRoot.SetActive(false);
            workingPlayer = workingFeelRoot.AddComponent<MMF_Player>();
            ConfigurePlayer(workingPlayer);
            workingReference = new MMF_ReferenceHolder { Label = "Runtime Visual Target", ForceReferenceOnAll = true };
            workingPlayer.FeedbacksList = new List<MMF_Feedback> { workingReference };
            workingAdapter = workingFeelRoot.AddComponent<BasicAttackFeelRuntimeAdapter>();
            workingAdapter.EditorConfigure(workingPlayer, workingReference);
            loadedSourcePrefab = null;
            currentProfilePath = string.Empty;
            authoringPresetName = "새 타격감";
            cueLifetime = 0.85f;
            cuePosition = Vector3.zero;
            cueEuler = Vector3.zero;
            cueScale = 1f;
            selectedLayerIndex = 0;
            authoringDirty = false;
            authoringNotice = "새 FEEL 프로필을 만들었습니다. 효과를 추가하고 저장하세요.";
        }

        private void LoadWorkingPreset(GameObject prefab)
        {
            if (prefab == null) return;
            ResetAuthoringPreview();
            DisposeWorkingRoot();
            workingFeelRoot = Instantiate(prefab);
            workingFeelRoot.name = prefab.name;
            workingFeelRoot.hideFlags = HideFlags.HideAndDontSave;
            workingFeelRoot.SetActive(false);
            workingPlayer = workingFeelRoot.GetComponent<MMF_Player>();
            workingAdapter = workingFeelRoot.GetComponent<BasicAttackFeelRuntimeAdapter>();
            if (workingPlayer == null)
            {
                authoringNotice = $"{prefab.name}: MMF_Player가 없습니다.";
                CreateBlankWorkingSession();
                return;
            }
            ConfigurePlayer(workingPlayer);
            workingPlayer.FeedbacksList ??= new List<MMF_Feedback>();
            workingReference = workingPlayer.FeedbacksList.OfType<MMF_ReferenceHolder>().FirstOrDefault();
            if (workingReference == null)
            {
                workingReference = new MMF_ReferenceHolder { Label = "Runtime Visual Target", ForceReferenceOnAll = true };
                workingPlayer.FeedbacksList.Insert(0, workingReference);
            }
            if (workingAdapter == null) workingAdapter = workingFeelRoot.AddComponent<BasicAttackFeelRuntimeAdapter>();
            workingAdapter.EditorConfigure(workingPlayer, workingReference);
            loadedSourcePrefab = prefab;
            var path = AssetDatabase.GetAssetPath(prefab);
            currentProfilePath = IsUserProfileAsset(path) ? path : string.Empty;
            var metadata = prefab.GetComponent<BasicAttackFeelProfileMetadata>();
            if (metadata != null)
            {
                cueLifetime = metadata.Lifetime;
                cuePosition = metadata.LocalPosition;
                cueEuler = metadata.LocalEulerAngles;
                cueScale = metadata.Scale;
            }
            else
            {
                cueLifetime = 0.85f;
                cuePosition = Vector3.zero;
                cueEuler = Vector3.zero;
                cueScale = 1f;
            }
            authoringPresetName = ProfileDisplayName(prefab);
            selectedLayerIndex = Mathf.Min(1, workingPlayer.FeedbacksList.Count - 1);
            authoringDirty = false;
            authoringNotice = $"{ProfileDisplayName(prefab)} 프로필을 불러왔습니다.";
        }

        private bool AddAuthoringEffect(string typeName)
        {
            EnsureWorkingSession();
            var type = ResolveFeedbackType(typeName);
            if (type == null)
            {
                authoringNotice = $"설치된 FEEL 타입을 찾지 못했습니다: {typeName}";
                return false;
            }
            try
            {
                var feedback = workingPlayer.AddFeedback(type, true);
                if (feedback == null)
                {
                    authoringNotice = $"효과 추가에 실패했습니다: {typeName}";
                    return false;
                }
                feedback.Active = true;
                feedback.Chance = 100f;
                feedback.Label = LayerLabel(typeName);
                feedback.Timing.InitialDelay = 0f;
                ApplyShowcaseDefaults(feedback);
                EnsurePrefabTarget(feedback);
                selectedLayerIndex = workingPlayer.FeedbacksList.IndexOf(feedback);
                MarkAuthoringDirty();
                authoringNotice = $"{DisplayName(typeName)}을 실제 MMF_Player 스택에 추가했습니다.";
                return true;
            }
            catch (Exception exception)
            {
                authoringNotice = $"{typeName} 추가 실패: {exception.Message}";
                return false;
            }
        }

        private void MoveLayer(int from, int to)
        {
            if (workingPlayer?.FeedbacksList == null || from <= 0 || to <= 0 || from >= workingPlayer.FeedbacksList.Count || to >= workingPlayer.FeedbacksList.Count) return;
            var feedback = workingPlayer.FeedbacksList[from];
            workingPlayer.FeedbacksList.RemoveAt(from);
            workingPlayer.FeedbacksList.Insert(to, feedback);
            selectedLayerIndex = to;
            MarkAuthoringDirty();
        }

        private void DuplicateSelectedLayer()
        {
            var feedback = SelectedFeedback();
            if (feedback == null || feedback is MMF_ReferenceHolder) return;
            var clone = (MMF_Feedback)Activator.CreateInstance(feedback.GetType());
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(feedback), clone);
            workingPlayer.AddFeedback(clone, false);
            selectedLayerIndex = workingPlayer.FeedbacksList.Count - 1;
            var copy = SelectedFeedback();
            if (copy != null) copy.Label = (feedback.Label ?? DisplayName(feedback.GetType().Name)) + " · 복제";
            MarkAuthoringDirty();
        }

        private void RemoveSelectedLayer()
        {
            if (workingPlayer?.FeedbacksList == null || selectedLayerIndex <= 0 || selectedLayerIndex >= workingPlayer.FeedbacksList.Count) return;
            workingPlayer.FeedbacksList.RemoveAt(selectedLayerIndex);
            selectedLayerIndex = Mathf.Clamp(selectedLayerIndex - 1, 0, workingPlayer.FeedbacksList.Count - 1);
            MarkAuthoringDirty();
        }

        private void ResetSelectedLayerToShowcase()
        {
            var feedback = SelectedFeedback();
            if (feedback == null || feedback is MMF_ReferenceHolder) return;
            feedback.OnAddFeedback();
            feedback.Active = true;
            feedback.Chance = 100f;
            feedback.Timing.InitialDelay = 0f;
            ApplyShowcaseDefaults(feedback);
            EnsurePrefabTarget(feedback);
            MarkAuthoringDirty();
        }

        private void ApplyShowcaseDefaults(MMF_Feedback feedback)
        {
            if (feedback == null) return;
            var typeName = feedback.GetType().Name;
            feedback.SetFeedbackDuration(Mathf.Clamp(feedback.FeedbackDuration <= 0.01f ? 0.65f : feedback.FeedbackDuration, 0.12f, 1.2f));
            SetFloatFields(feedback, "Frequency", 18f);
            SetFloatFields(feedback, "Damping", 0.72f);

            switch (typeName)
            {
                case "MMF_AnimatorSpeed":
                    SetField(feedback, "Duration", 0.42f);
                    SetField(feedback, "NewSpeedMin", 0.28f);
                    SetField(feedback, "NewSpeedMax", 0.42f);
                    break;
                case "MMF_Flicker":
                    SetField(feedback, "FlickerDuration", 0.32f);
                    SetField(feedback, "FlickerPeriod", 0.055f);
                    SetField(feedback, "FlickerColor", new Color(1f, 0.18f, 0.08f, 1f));
                    SetField(feedback, "PropertyName", "_BaseColor");
                    SetField(feedback, "UseMaterialPropertyBlocks", true);
                    break;
                case "MMF_Position":
                    SetField(feedback, "AnimatePositionDuration", 0.28f);
                    SetField(feedback, "RelativePosition", true);
                    SetField(feedback, "DeterminePositionsOnPlay", true);
                    SetField(feedback, "DestinationPosition", new Vector3(-0.16f, 0.025f, -0.08f));
                    break;
                case "MMF_PositionShake":
                    ConfigureShakeDefaults(feedback, 0.24f, 34f, 0.085f, new Vector3(1f, 0.22f, 0.65f));
                    break;
                case "MMF_Rotation":
                    SetField(feedback, "AnimateRotationDuration", 0.3f);
                    SetField(feedback, "DetermineRotationOnPlay", true);
                    SetField(feedback, "DestinationAngles", new Vector3(-3f, 10f, 7f));
                    break;
                case "MMF_RotationShake":
                    ConfigureShakeDefaults(feedback, 0.26f, 31f, 7.5f, new Vector3(0.35f, 1f, 0.7f));
                    break;
                case "MMF_Scale":
                    SetField(feedback, "AnimateScaleDuration", 0.28f);
                    SetField(feedback, "DetermineScaleOnPlay", true);
                    SetField(feedback, "DestinationScale", new Vector3(1.13f, 0.84f, 1.13f));
                    break;
                case "MMF_ScaleShake":
                    ConfigureShakeDefaults(feedback, 0.24f, 30f, 0.09f, Vector3.one);
                    break;
                case "MMF_SquashAndStretch":
                    SetField(feedback, "AnimateScaleDuration", 0.3f);
                    SetField(feedback, "DestinationScale", 0.2f);
                    SetField(feedback, "DetermineScaleOnPlay", true);
                    break;
                case "MMF_SquashAndStretchSpring":
                    SetField(feedback, "DeclaredDuration", 0.5f);
                    SetField(feedback, "BumpScaleMin", -0.12f);
                    SetField(feedback, "BumpScaleMax", 0.18f);
                    break;
                case "MMF_Wiggle":
                    SetField(feedback, "WigglePosition", true);
                    SetField(feedback, "WigglePositionDuration", 0.32f);
                    SetField(feedback, "WiggleRotation", true);
                    SetField(feedback, "WiggleRotationDuration", 0.32f);
                    break;
                case "MMF_Particles":
                    SetField(feedback, "EmitCount", 24);
                    SetField(feedback, "DeclaredDuration", 0.45f);
                    SetField(feedback, "StopSystemOnReset", true);
                    SetField(feedback, "StopSystemOnStopFeedback", true);
                    break;
                case "MMF_LineRenderer":
                    SetField(feedback, "Duration", 0.2f);
                    SetField(feedback, "ModifyWidth", true);
                    SetField(feedback, "ModifyColor", true);
                    break;
                case "MMF_TrailRenderer":
                    SetField(feedback, "Duration", 0.3f);
                    SetField(feedback, "ModifyWidth", true);
                    SetField(feedback, "ModifyColor", true);
                    SetField(feedback, "ModifyTime", true);
                    SetField(feedback, "NewTime", 0.28f);
                    break;
                case "MMF_Fog":
                    SetField(feedback, "Duration", 0.42f);
                    SetField(feedback, "ModifyFogDensity", true);
                    SetField(feedback, "DensityRemapZero", 0f);
                    SetField(feedback, "DensityRemapOne", 0.018f);
                    break;
                case "MMF_CameraShake":
                    ConfigureCameraShakeDefaults(feedback);
                    break;
                case "MMF_CameraZoom":
                    SetField(feedback, "RelativeFieldOfView", true);
                    SetField(feedback, "ZoomFieldOfView", -7f);
                    SetField(feedback, "ZoomTransitionDuration", 0.08f);
                    SetField(feedback, "ZoomDuration", 0.12f);
                    break;
                case "MMF_CameraFieldOfView":
                    SetField(feedback, "Duration", 0.18f);
                    SetField(feedback, "RelativeFieldOfView", true);
                    SetField(feedback, "RemapFieldOfViewZero", 0f);
                    SetField(feedback, "RemapFieldOfViewOne", 4.5f);
                    break;
                case "MMF_Flash":
                    SetField(feedback, "FlashDuration", 0.12f);
                    SetField(feedback, "FlashAlpha", 0.45f);
                    SetField(feedback, "FlashColor", new Color(1f, 0.86f, 0.62f, 1f));
                    break;
                case "MMF_FreezeFrame":
                    SetField(feedback, "FreezeFrameDuration", 0.075f);
                    break;
                case "MMF_TimescaleModifier":
                    SetField(feedback, "TimeScale", 0.22f);
                    SetField(feedback, "TimeScaleDuration", 0.16f);
                    SetField(feedback, "ResetTimescaleOnStop", true);
                    SetField(feedback, "UnfreezeTimescaleOnStop", true);
                    break;
            }

            if (feedback is MMF_PositionSpring)
            {
                SetField(feedback, "BumpPositionMin", new Vector3(-0.12f, -0.025f, -0.08f));
                SetField(feedback, "BumpPositionMax", new Vector3(0.12f, 0.045f, 0.08f));
                SetField(feedback, "DeclaredDuration", 0.55f);
            }
            else if (typeName.Contains("RotationSpring", StringComparison.Ordinal))
            {
                SetVectorFields(feedback, "Bump", new Vector3(-6f, -18f, -8f), new Vector3(6f, 18f, 8f));
                SetField(feedback, "DeclaredDuration", 0.55f);
            }
            else if (typeName.Contains("ScaleSpring", StringComparison.Ordinal))
            {
                SetVectorFields(feedback, "Bump", new Vector3(-0.12f, -0.08f, -0.12f), new Vector3(0.16f, 0.12f, 0.16f));
                SetField(feedback, "DeclaredDuration", 0.5f);
            }
            else if (feedback is MMF_Light light)
            {
                light.Duration = 0.18f;
                light.ModifyIntensity = true;
                light.ModifyRange = true;
                light.RemapIntensityZero = 0f;
                light.RemapIntensityOne = 3.4f;
                light.RemapRangeZero = 0.4f;
                light.RemapRangeOne = 2.4f;
                light.StartsOff = true;
                light.DisableOnStop = true;
            }
        }

        private static void ConfigureShakeDefaults(object feedback, float duration, float speed, float range, Vector3 direction)
        {
            SetField(feedback, "Duration", duration);
            SetField(feedback, "ShakeSpeed", speed);
            SetField(feedback, "ShakeRange", range);
            SetField(feedback, "ShakeMainDirection", direction);
            SetField(feedback, "ResetShakerValuesAfterShake", true);
            SetField(feedback, "ResetTargetValuesAfterShake", true);
        }

        private static void ConfigureCameraShakeDefaults(object feedback)
        {
            var field = feedback.GetType().GetField("CameraShakeProperties", BindingFlags.Instance | BindingFlags.Public);
            var properties = field?.GetValue(feedback);
            if (field == null || properties == null) return;
            SetField(properties, "Duration", 0.14f);
            SetField(properties, "Amplitude", 0.8f);
            SetField(properties, "Frequency", 28f);
            SetField(properties, "AmplitudeX", 0.65f);
            SetField(properties, "AmplitudeY", 0.5f);
            SetField(properties, "AmplitudeZ", 0.28f);
            field.SetValue(feedback, properties);
        }

        private void EnsurePrefabTarget(MMF_Feedback feedback)
        {
            if (feedback == null || workingFeelRoot == null) return;
            if (feedback is MMF_Light light && light.BoundLight == null)
            {
                var anchor = new GameObject($"Impact Light {workingPlayer.FeedbacksList.IndexOf(feedback):00}");
                anchor.transform.SetParent(workingFeelRoot.transform, false);
                anchor.transform.localPosition = Vector3.zero;
                var bound = anchor.AddComponent<Light>();
                bound.type = LightType.Point;
                bound.color = new Color(1f, 0.92f, 0.78f);
                bound.intensity = 0f;
                bound.range = 2.4f;
                bound.shadows = LightShadows.None;
                bound.enabled = false;
                light.BoundLight = bound;
                feedback.Label = $"{GlobalPrefix}{PrefabTargetToken} {DisplayName(feedback.GetType().Name)}";
            }
            else if (feedback.GetType().Name is "MMF_Particles" or "MMF_LineRenderer" or "MMF_TrailRenderer")
            {
                TryCreateRequiredComponent(feedback);
            }
            else if (Items.Any(item => item.Tab == Tab.Screen && item.TypeName == feedback.GetType().Name) &&
                     !(feedback.Label ?? string.Empty).StartsWith(GlobalPrefix, StringComparison.Ordinal))
            {
                feedback.Label = $"{GlobalPrefix} {DisplayName(feedback.GetType().Name)}";
            }
        }

        private void TryCreateRequiredComponent(MMF_Feedback feedback)
        {
            var fields = feedback.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
            foreach (var field in fields)
            {
                if (!typeof(Component).IsAssignableFrom(field.FieldType) || field.GetValue(feedback) != null) continue;
                Type componentType = null;
                if (field.FieldType == typeof(ParticleSystem)) componentType = typeof(ParticleSystem);
                else if (field.FieldType == typeof(LineRenderer)) componentType = typeof(LineRenderer);
                else if (field.FieldType == typeof(TrailRenderer)) componentType = typeof(TrailRenderer);
                if (componentType == null) continue;
                var anchor = new GameObject($"{feedback.GetType().Name} Anchor {workingPlayer.FeedbacksList.IndexOf(feedback):00}");
                anchor.transform.SetParent(workingFeelRoot.transform, false);
                anchor.transform.localPosition = Vector3.zero;
                var component = anchor.AddComponent(componentType);
                ConfigureImpactComponent(component);
                field.SetValue(feedback, component);
                feedback.Label = $"{PrefabTargetToken} {DisplayName(feedback.GetType().Name)}";
                break;
            }
        }

        private static void ConfigureImpactComponent(Component component)
        {
            if (component is ParticleSystem particle)
            {
                var main = particle.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 0.35f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.48f, 0.12f), new Color(1f, 0.95f, 0.66f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 64;
                var emission = particle.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)24) });
                var shape = particle.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.06f;
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

            if (component is LineRenderer line)
            {
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.SetPosition(0, new Vector3(-0.52f, 0f, 0f));
                line.SetPosition(1, new Vector3(0.52f, 0f, 0f));
                line.startWidth = 0.085f;
                line.endWidth = 0.015f;
                line.startColor = new Color(0.35f, 0.95f, 1f, 0.95f);
                line.endColor = new Color(0.35f, 0.95f, 1f, 0f);
                return;
            }

            if (component is TrailRenderer trail)
            {
                trail.time = 0.28f;
                trail.minVertexDistance = 0.02f;
                trail.startWidth = 0.09f;
                trail.endWidth = 0f;
                trail.startColor = new Color(1f, 0.82f, 0.35f, 0.95f);
                trail.endColor = new Color(1f, 0.35f, 0.08f, 0f);
                trail.emitting = false;
            }
        }

        private void DrawMajorFeedbackFields(MMF_Feedback feedback)
        {
            GUILayout.Label("주요 조절값", sectionStyle);
            foreach (var field in MajorFields(feedback.GetType())) DrawField(feedback, field);

            var nestedField = feedback.GetType().GetField("CameraShakeProperties", BindingFlags.Instance | BindingFlags.Public);
            var nested = nestedField?.GetValue(feedback);
            if (nested == null) return;
            GUILayout.Label("Camera Shake 핵심값", sectionStyle);
            foreach (var field in nested.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)
                         .Where(field => !field.IsStatic && IsEditableFieldType(field.FieldType))
                         .OrderByDescending(MajorFieldScore)
                         .Take(6))
            {
                DrawField(nested, field);
            }
            nestedField.SetValue(feedback, nested);
        }

        private static IEnumerable<FieldInfo> MajorFields(Type type)
        {
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => !field.IsStatic && IsEditableFieldType(field.FieldType) && MajorFieldScore(field) > 0)
                .OrderByDescending(MajorFieldScore)
                .ThenBy(field => field.MetadataToken)
                .Take(6);
        }

        private static int MajorFieldScore(FieldInfo field)
        {
            var name = field.Name;
            if (name is "Mode" or "Space") return 120;
            if (name.Contains("StateName", StringComparison.OrdinalIgnoreCase) || name.Contains("ParameterName", StringComparison.OrdinalIgnoreCase) || name.Contains("PropertyName", StringComparison.OrdinalIgnoreCase)) return 118;
            if (name.Contains("Duration", StringComparison.OrdinalIgnoreCase)) return 115;
            if (name.Contains("Intensity", StringComparison.OrdinalIgnoreCase) || name.Contains("Strength", StringComparison.OrdinalIgnoreCase) || name.Contains("Amplitude", StringComparison.OrdinalIgnoreCase)) return 110;
            if (name.Contains("Frequency", StringComparison.OrdinalIgnoreCase) || name.Contains("Damping", StringComparison.OrdinalIgnoreCase)) return 105;
            if (name.Contains("Position", StringComparison.OrdinalIgnoreCase) || name.Contains("Rotation", StringComparison.OrdinalIgnoreCase) || name.Contains("Scale", StringComparison.OrdinalIgnoreCase)) return 100;
            if (name.Contains("Range", StringComparison.OrdinalIgnoreCase) || name.Contains("Radius", StringComparison.OrdinalIgnoreCase) || name.Contains("Distance", StringComparison.OrdinalIgnoreCase)) return 95;
            if (name.Contains("Speed", StringComparison.OrdinalIgnoreCase) || name.Contains("Zoom", StringComparison.OrdinalIgnoreCase) || name.Contains("FieldOfView", StringComparison.OrdinalIgnoreCase) || name.Contains("Count", StringComparison.OrdinalIgnoreCase)) return 90;
            if (name.Contains("Color", StringComparison.OrdinalIgnoreCase) || name.Contains("Alpha", StringComparison.OrdinalIgnoreCase) || name.Contains("Width", StringComparison.OrdinalIgnoreCase) || name.Contains("Time", StringComparison.OrdinalIgnoreCase)) return 85;
            if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType) && (name.Contains("Target", StringComparison.OrdinalIgnoreCase) || name.Contains("Bound", StringComparison.OrdinalIgnoreCase) || name.Contains("Prefab", StringComparison.OrdinalIgnoreCase) || name.Contains("Renderer", StringComparison.OrdinalIgnoreCase))) return 80;
            return 0;
        }

        private static bool IsEditableFieldType(Type type)
        {
            return type == typeof(string) || type == typeof(float) || type == typeof(int) || type == typeof(bool) ||
                   type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4) || type == typeof(Color) ||
                   type == typeof(AnimationCurve) || type == typeof(Gradient) || type.IsEnum ||
                   typeof(UnityEngine.Object).IsAssignableFrom(type);
        }

        private void DrawField(object owner, FieldInfo field)
        {
            var value = field.GetValue(owner);
            object next = value;
            var label = PrettyFieldName(field.Name);
            if (field.FieldType == typeof(string)) next = EditorGUILayout.TextField(label, (string)value ?? string.Empty);
            else if (field.FieldType == typeof(float)) next = EditorGUILayout.FloatField(label, (float)value);
            else if (field.FieldType == typeof(int)) next = EditorGUILayout.IntField(label, (int)value);
            else if (field.FieldType == typeof(bool)) next = EditorGUILayout.Toggle(label, (bool)value);
            else if (field.FieldType == typeof(Vector2)) next = EditorGUILayout.Vector2Field(label, (Vector2)value);
            else if (field.FieldType == typeof(Vector3)) next = EditorGUILayout.Vector3Field(label, (Vector3)value);
            else if (field.FieldType == typeof(Vector4)) next = EditorGUILayout.Vector4Field(label, (Vector4)value);
            else if (field.FieldType == typeof(Color)) next = EditorGUILayout.ColorField(label, (Color)value);
            else if (field.FieldType == typeof(AnimationCurve)) next = EditorGUILayout.CurveField(label, (AnimationCurve)value);
            else if (field.FieldType == typeof(Gradient)) next = EditorGUILayout.GradientField(label, (Gradient)value);
            else if (field.FieldType.IsEnum) next = EditorGUILayout.EnumPopup(label, (Enum)value);
            else if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)) next = EditorGUILayout.ObjectField(label, (UnityEngine.Object)value, field.FieldType, true);
            if (!Equals(value, next)) field.SetValue(owner, next);
        }

        private void DrawBoundAnchor(MMF_Feedback feedback)
        {
            var anchor = BoundAnchor(feedback);
            if (anchor == null) return;
            GUILayout.Label("타격점 · 효과 Anchor", sectionStyle);
            anchor.localPosition = EditorGUILayout.Vector3Field("로컬 위치", anchor.localPosition);
            anchor.localEulerAngles = EditorGUILayout.Vector3Field("로컬 회전", anchor.localEulerAngles);
            anchor.localScale = EditorGUILayout.Vector3Field("로컬 크기", anchor.localScale);
            if (GUILayout.Button("Anchor 원점", tabStyle, GUILayout.Height(24f)))
            {
                anchor.localPosition = Vector3.zero;
                anchor.localRotation = Quaternion.identity;
                anchor.localScale = Vector3.one;
            }
        }

        private Transform BoundAnchor(MMF_Feedback feedback)
        {
            if (feedback == null || workingFeelRoot == null) return null;
            foreach (var field in feedback.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!(field.GetValue(feedback) is UnityEngine.Object value)) continue;
                var transform = value switch { Component component => component.transform, GameObject gameObject => gameObject.transform, _ => null };
                if (transform != null && transform != workingFeelRoot.transform && transform.IsChildOf(workingFeelRoot.transform)) return transform;
            }
            return null;
        }

        private string TargetStatus(MMF_Feedback feedback)
        {
            if (feedback == null) return string.Empty;
            if ((feedback.Label ?? string.Empty).Contains(PrefabTargetToken, StringComparison.Ordinal)) return "Target: Prefab 내부 Anchor · 좌표 저장 가능";
            if ((feedback.Label ?? string.Empty).StartsWith(GlobalPrefix, StringComparison.Ordinal)) return "Target: 카메라/화면 전역 · MainBattle 소유권 경고 적용";
            if (feedback.HasAutomatedTargetAcquisition) return "Target: 피격 대상 VisualRoot · Runtime Reference 자동 바인딩";
            return feedback.EvaluateRequiresSetup() ? "경고: 실제 재생에 필요한 Target/Asset 설정을 확인하세요." : "Target: 별도 참조 불필요";
        }

        private void PlayWorkingComposition()
        {
            EnsureWorkingSession();
            if (!Application.isPlaying || target == null || workingFeelRoot == null || AuthoringExecutableLayerCount == 0)
            {
                authoringNotice = "PlayMode와 피격 대상, 실행 효과가 필요합니다.";
                return;
            }
            ResetPreview();
            ResetAuthoringPreview();
            CacheReferences();
            CaptureDemoState();
            var position = HitPoint() + target.transform.rotation * cuePosition;
            var rotation = target.transform.rotation * Quaternion.Euler(cueEuler);
            activeAuthoringPreview = Instantiate(workingFeelRoot, position, rotation);
            activeAuthoringPreview.hideFlags = HideFlags.None;
            activeAuthoringPreview.name = workingFeelRoot.name + " [Lab Preview]";
            activeAuthoringPreview.transform.localScale = workingFeelRoot.transform.localScale * cueScale;
            activeAuthoringPreview.SetActive(true);
            var runtime = activeAuthoringPreview.GetComponent<BasicAttackFeelRuntimeAdapter>();
            runtime?.PlayBasicAttackFeel(
                position,
                target,
                authoringIntensity,
                BasicAttackFeelPlaybackOptions.None);
            var version = ++authoringPlayVersion;
            authoringStopRoutine = StartCoroutine(StopAuthoringAfter(cueLifetime, version));
            authoringNotice = $"실제 MMF 조합을 {authoringIntensity:0.00} 강도로 재생했습니다.";
        }

        private IEnumerator StopAuthoringAfter(float duration, int version)
        {
            yield return Wait(Mathf.Max(0.05f, duration));
            if (version != authoringPlayVersion) yield break;
            authoringStopRoutine = null;
            ResetAuthoringPreview();
        }

        private string SaveWorkingProfile(bool saveAs, bool confirmOverwrite, string explicitName = null)
        {
            EnsureWorkingSession();
            if (!TryValidateWorkingSession(out var validation))
            {
                authoringNotice = validation;
                return null;
            }
            EnsureFolder(UserProfileRoot);
            var fileName = SanitizePresetName(string.IsNullOrWhiteSpace(explicitName) ? authoringPresetName : explicitName);
            var requestedPath = $"{UserProfileRoot}/{fileName}.prefab";
            var canOverwriteCurrent = !saveAs && IsUserProfileAsset(currentProfilePath);
            var path = canOverwriteCurrent
                ? currentProfilePath
                : AssetDatabase.GenerateUniqueAssetPath(requestedPath);
            if (canOverwriteCurrent && confirmOverwrite &&
                !EditorUtility.DisplayDialog(
                    "FEEL 프로필 저장",
                    $"{ProfileDisplayName(loadedSourcePrefab)} 프로필의 효과와 값을 저장할까요?\n\n이 프로필을 사용하는 공격에도 변경 내용이 반영됩니다.",
                    "저장",
                    "취소"))
                return null;

            ResetAuthoringPreview();
            var saveRoot = Instantiate(workingFeelRoot);
            try
            {
                saveRoot.name = Path.GetFileNameWithoutExtension(path);
                saveRoot.hideFlags = HideFlags.None;
                saveRoot.SetActive(true);
                var player = saveRoot.GetComponent<MMF_Player>();
                var reference = player?.FeedbacksList?.OfType<MMF_ReferenceHolder>().FirstOrDefault();
                if (reference != null)
                {
                    reference.GameObjectReference = null;
                    reference.ForceReferenceOnAll = true;
                }
                var adapter = saveRoot.GetComponent<BasicAttackFeelRuntimeAdapter>();
                adapter?.EditorConfigure(player, reference);
                var metadata = saveRoot.GetComponent<BasicAttackFeelProfileMetadata>();
                if (metadata == null) metadata = saveRoot.AddComponent<BasicAttackFeelProfileMetadata>();
                metadata.EditorConfigure(cueLifetime, cuePosition, cueEuler, cueScale);
                var saved = PrefabUtility.SaveAsPrefabAsset(saveRoot, path);
                if (saved == null)
                {
                    authoringNotice = $"프로필 저장에 실패했습니다: {path}";
                    return null;
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                loadedSourcePrefab = saved;
                currentProfilePath = path;
                authoringPresetName = ProfileDisplayName(saved);
                authoringDirty = false;
                authoringAssetsDirty = true;
                EnsureAuthoringAssets();
                authoringNotice = $"FEEL 프로필 저장 완료 · {ProfileDisplayName(saved)} · {validation}";
                return path;
            }
            finally
            {
                DestroyAuthoringObject(saveRoot);
            }
        }

        private string ValidateWorkingSession()
        {
            TryValidateWorkingSession(out var validation);
            return validation;
        }

        private bool TryValidateWorkingSession(out string validation)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            if (workingPlayer == null) errors.Add("MMF_Player 없음");
            if (workingAdapter == null) errors.Add("RuntimeAdapter 없음");
            if (workingReference == null) errors.Add("Runtime Target 없음");
            if (workingPlayer?.FeedbacksList != null)
            {
                if (workingPlayer.FeedbacksList.Count == 0 || !(workingPlayer.FeedbacksList[0] is MMF_ReferenceHolder)) errors.Add("ReferenceHolder가 첫 계층이 아님");
                if (workingPlayer.FeedbacksList.Any(feedback => feedback == null)) errors.Add("빈 Feedback 존재");
                foreach (var feedback in workingPlayer.FeedbacksList.Where(feedback => feedback != null && !(feedback is MMF_ReferenceHolder)))
                {
                    if (feedback.Timing?.RepeatForever == true) warnings.Add("무한 반복");
                    if (feedback.Active && IsSharedCombatFeedback(feedback))
                        warnings.Add("MainBattle 공용 소유 효과");
                }
            }
            if (AuthoringExecutableLayerCount == 0) errors.Add("실행 효과 0개");
            else if (AuthoringRuntimeLayerCount == 0) errors.Add("실전 재생 효과 0개");
            if (cueLifetime <= 0f || cueScale <= 0f) errors.Add("Cue 수명/배율 오류");
            if (errors.Count > 0)
            {
                validation = $"저장 불가 · {string.Join(", ", errors.Distinct())}";
                return false;
            }
            validation = warnings.Count > 0
                ? $"검증 통과 · 경고 {string.Join(", ", warnings.Distinct())}"
                : $"검증 통과 · 실전 {AuthoringRuntimeLayerCount}개 · 원본 비변경";
            return true;
        }

        private static bool IsRuntimePlayableFeedback(MMF_Feedback feedback) =>
            feedback != null &&
            feedback is not MMF_ReferenceHolder &&
            feedback.Active &&
            !IsSharedCombatFeedback(feedback);

        private static bool IsSharedCombatFeedback(MMF_Feedback feedback) =>
            feedback is MMF_CameraShake or
            MMF_CameraFieldOfView or
            MMF_FreezeFrame or
            MMF_TimescaleModifier;

        private void EnsureAuthoringAssets()
        {
            if (!authoringAssetsDirty) return;
            var presetGuids = AssetDatabase.FindAssets("t:Prefab", new[] { FeelRoot });
            cachedFeelProfiles = presetGuids.Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(prefab => prefab != null && prefab.GetComponent<MMF_Player>() != null && prefab.GetComponent<BasicAttackFeelRuntimeAdapter>() != null)
                .OrderBy(prefab => AssetDatabase.GetAssetPath(prefab).StartsWith(ProductionRoot, StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(prefab => IsUserProfileAsset(AssetDatabase.GetAssetPath(prefab)) ? 0 : 1)
                .ThenBy(prefab => prefab.name, StringComparer.Ordinal)
                .ToArray();
            authoringAssetsDirty = false;
        }

        private void RefreshAuthoringAssets()
        {
            authoringAssetsDirty = true;
            EnsureAuthoringAssets();
            authoringNotice = $"저장된 FEEL 프로필 {cachedFeelProfiles.Length}개를 다시 읽었습니다.";
        }

        private MMF_Feedback SelectedFeedback()
        {
            return workingPlayer?.FeedbacksList != null && selectedLayerIndex >= 0 && selectedLayerIndex < workingPlayer.FeedbacksList.Count
                ? workingPlayer.FeedbacksList[selectedLayerIndex]
                : null;
        }

        private static Type ResolveFeedbackType(string typeName)
        {
            if (AuthoringFeedbackTypeCache.TryGetValue(typeName, out var cached)) return cached;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType($"MoreMountains.Feedbacks.{typeName}", false);
                if (type == null || !typeof(MMF_Feedback).IsAssignableFrom(type) || type.IsAbstract) continue;
                AuthoringFeedbackTypeCache[typeName] = type;
                return type;
            }
            AuthoringFeedbackTypeCache[typeName] = null;
            return null;
        }

        private string LayerLabel(string typeName)
        {
            var prefix = Items.Any(item => item.Tab == Tab.Screen && item.TypeName == typeName) ? GlobalPrefix + " " : string.Empty;
            return $"{prefix}{workingPlayer.FeedbacksList.Count:00} {DisplayName(typeName)}";
        }

        private static void ConfigurePlayer(MMF_Player player)
        {
            player.AutoPlayOnEnable = false;
            player.AutoPlayOnStart = false;
            player.AutoInitialization = false;
            player.InitializationMode = MMFeedbacks.InitializationModes.Script;
            player.StopFeedbacksOnDisable = true;
            player.RestoreInitialValuesOnDisable = true;
            player.FeedbacksList ??= new List<MMF_Feedback>();
        }

        private void MarkAuthoringDirty()
        {
            authoringDirty = true;
            if (workingPlayer != null) EditorUtility.SetDirty(workingPlayer);
        }

        private void DisposeWorkingRoot()
        {
            if (workingFeelRoot != null) DestroyAuthoringObject(workingFeelRoot);
            workingFeelRoot = null;
            workingPlayer = null;
            workingReference = null;
            workingAdapter = null;
        }

        private static void DestroyAuthoringObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private bool ConfirmDiscardChanges()
        {
            return !authoringDirty || EditorUtility.DisplayDialog(
                "저장하지 않은 변경",
                "현재 프로필의 저장하지 않은 변경을 버릴까요?",
                "버리기",
                "계속 편집");
        }

        private static bool IsUserProfileAsset(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   path.StartsWith(UserProfileRoot + "/", StringComparison.Ordinal);
        }

        private static string ProfileOptionLabel(GameObject profile)
        {
            if (profile == null) return "없음";
            var path = AssetDatabase.GetAssetPath(profile);
            if (path.StartsWith(ProductionRoot + "/", StringComparison.Ordinal))
                return $"기본 제공 · {ProfileDisplayName(profile)}";
            if (IsUserProfileAsset(path))
                return $"내 프로필 · {ProfileDisplayName(profile)}";
            return $"참고 프로필 · {ProfileDisplayName(profile)}";
        }

        private static string ProfileDisplayName(GameObject profile)
        {
            if (profile == null) return "새 타격감";
            return profile.name.StartsWith("BAFeel_", StringComparison.Ordinal)
                ? profile.name.Substring("BAFeel_".Length)
                : profile.name;
        }

        private static string SanitizePresetName(string value)
        {
            var name = string.IsNullOrWhiteSpace(value) ? "새_타격감" : value.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            if (!name.StartsWith("BAFeel_", StringComparison.Ordinal)) name = "BAFeel_" + name;
            return name;
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }

        private static void SetFloatFields(object owner, string contains, float value)
        {
            foreach (var field in owner.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)
                         .Where(field => field.FieldType == typeof(float) && field.Name.Contains(contains, StringComparison.OrdinalIgnoreCase)))
            {
                field.SetValue(owner, value);
            }
        }

        private static void SetVectorFields(object owner, string contains, Vector3 min, Vector3 max)
        {
            foreach (var field in owner.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)
                         .Where(field => field.FieldType == typeof(Vector3) && field.Name.Contains(contains, StringComparison.OrdinalIgnoreCase)))
            {
                field.SetValue(owner, field.Name.Contains("Min", StringComparison.OrdinalIgnoreCase) ? min : max);
            }
        }

        private static void SetField(object owner, string name, object value)
        {
            owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public)?.SetValue(owner, value);
        }

        private static string PrettyFieldName(string value)
        {
            return value
                .Replace("DeclaredDuration", "선언 시간")
                .Replace("Duration", "지속시간")
                .Replace("StateName", "상태 이름")
                .Replace("ParameterName", "파라미터 이름")
                .Replace("PropertyName", "프로퍼티 이름")
                .Replace("Intensity", "강도")
                .Replace("Frequency", "주파수")
                .Replace("Damping", "감쇠")
                .Replace("Position", "위치")
                .Replace("Rotation", "회전")
                .Replace("Scale", "크기")
                .Replace("Range", "범위")
                .Replace("Color", "색상")
                .Replace("Speed", "속도")
                .Replace("Target", "대상")
                .Replace("Bound", "연결 ");
        }

        public void AuthoringCreateBlankForDiagnostics() => CreateBlankWorkingSession();
        public bool AuthoringAddEffectForDiagnostics(string typeName) => AddAuthoringEffect(typeName);
        public string AuthoringSaveProfileForDiagnostics(string presetName) => SaveWorkingProfile(false, false, presetName);
        public void AuthoringLoadPresetForDiagnostics(GameObject prefab) => LoadWorkingPreset(prefab);
        public string AuthoringValidateForDiagnostics() => ValidateWorkingSession();
        public string[] AuthoringLayerTypesForDiagnostics() => workingPlayer?.FeedbacksList?
            .Where(feedback => feedback != null && !(feedback is MMF_ReferenceHolder))
            .Select(feedback => feedback.GetType().Name).ToArray() ?? Array.Empty<string>();
        public bool AuthoringDuplicateLayerForDiagnostics(int executableIndex)
        {
            if (!TrySelectExecutableLayer(executableIndex)) return false;
            var before = AuthoringExecutableLayerCount;
            DuplicateSelectedLayer();
            return AuthoringExecutableLayerCount == before + 1;
        }

        public bool AuthoringMoveLayerForDiagnostics(int fromExecutableIndex, int toExecutableIndex)
        {
            if (workingPlayer?.FeedbacksList == null || fromExecutableIndex < 0 || toExecutableIndex < 0 ||
                fromExecutableIndex >= AuthoringExecutableLayerCount || toExecutableIndex >= AuthoringExecutableLayerCount) return false;
            MoveLayer(fromExecutableIndex + 1, toExecutableIndex + 1);
            return selectedLayerIndex == toExecutableIndex + 1;
        }

        public bool AuthoringSetLayerActiveForDiagnostics(int executableIndex, bool active)
        {
            if (!TrySelectExecutableLayer(executableIndex)) return false;
            var feedback = SelectedFeedback();
            feedback.Active = active;
            MarkAuthoringDirty();
            return feedback.Active == active;
        }

        public bool AuthoringRemoveLayerForDiagnostics(int executableIndex)
        {
            if (!TrySelectExecutableLayer(executableIndex)) return false;
            var before = AuthoringExecutableLayerCount;
            RemoveSelectedLayer();
            return AuthoringExecutableLayerCount == before - 1;
        }

        public bool[] AuthoringLayerActiveForDiagnostics() => workingPlayer?.FeedbacksList?
            .Where(feedback => feedback != null && !(feedback is MMF_ReferenceHolder))
            .Select(feedback => feedback.Active).ToArray() ?? Array.Empty<bool>();

        private bool TrySelectExecutableLayer(int executableIndex)
        {
            if (workingPlayer?.FeedbacksList == null || executableIndex < 0 || executableIndex >= AuthoringExecutableLayerCount) return false;
            selectedLayerIndex = executableIndex + 1;
            return SelectedFeedback() is not MMF_ReferenceHolder;
        }
#endif
    }
}
