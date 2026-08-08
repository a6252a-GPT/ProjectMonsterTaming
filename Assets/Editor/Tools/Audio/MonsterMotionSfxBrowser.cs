using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 몬스터 애니메이션과 SFX 후보를 한 화면에서 비교하는 Editor 전용 도구다.
/// 씬이나 기존 프리팹을 수정하지 않고 임시 미리보기 인스턴스만 사용한다.
/// </summary>
public sealed class MonsterMotionSfxBrowser : EditorWindow
{
    private const string WindowTitle = "몬스터 모션 + SFX 브라우저";
    private const string MenuPath = "JC Tool/Animation/몬스터 모션 + SFX 브라우저";
    private const string MonsterRoot = "Assets/ProjectMT/05_Art/Characters/01_비인간캐릭터";
    private const string MappingAssetPath = "Assets/Editor/Tools/Audio/MonsterMotionSfxMappings.asset";
    private const float PreviewFieldOfView = 35f;
    private const float PreviewCameraPadding = 1.35f;
    private const double PreviewFrameInterval = 1.0 / 60.0;
    private const float ListWidth = 260f;
    private static readonly Vector2 MinimumWindowSize = new Vector2(900f, 720f);

    [SerializeField] private string monsterSearch = string.Empty;
    [SerializeField] private string motionSearch = string.Empty;
    [SerializeField] private string selectedMonsterName = string.Empty;
    [SerializeField] private string selectedMotionKey = string.Empty;
    [SerializeField] private AudioClip selectedAudioClip;
    [SerializeField] private float sfxDelaySeconds;
    [SerializeField] private float previewVolume = 1f;
    [SerializeField] private bool loopMotion;
    [SerializeField] private float previewRotationY;

    private readonly List<MotionEntry> motionEntries = new List<MotionEntry>();
    private readonly List<string> monsterNames = new List<string>();
    private readonly Dictionary<string, MonsterEntry> monsterEntries =
        new Dictionary<string, MonsterEntry>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MonsterMotionSet> motionCache =
        new Dictionary<string, MonsterMotionSet>(StringComparer.OrdinalIgnoreCase);

    private Vector2 monsterScroll;
    private Vector2 motionScroll;
    private GameObject previewRoot;
    private GameObject previewInstance;
    private PreviewRenderUtility previewUtility;
    private List<Material> previewMaterials;
    private Animator previewAnimator;
    private MotionEntry selectedMotion;
    private bool dataLoaded;
    private bool isPlaying;
    private bool isPaused;
    private bool animationModeStartedByTool;
    private bool sfxStarted;
    private double playbackStartedAt;
    private double lastPreviewUpdateAt;
    private float nextSfxTriggerSeconds;
    private float elapsedSeconds;
    private string statusMessage = "모션과 SFX를 선택하세요.";
    private MessageType statusType = MessageType.Info;
    private string previewBindingMessage = string.Empty;
    private MessageType previewBindingMessageType = MessageType.Info;

    [MenuItem(MenuPath)]
    private static void Open()
    {
        MonsterMotionSfxBrowser window = GetWindow<MonsterMotionSfxBrowser>(WindowTitle);
        window.minSize = MinimumWindowSize;
        window.Show();
    }

    private void OnEnable()
    {
        minSize = MinimumWindowSize;
        dataLoaded = false;
        bool hasMonsters = RefreshMonsterIndex();
        dataLoaded = true;

        if (hasMonsters)
        {
            EnsureSelectedMonster();
            LoadSelectedMonsterMotions();
            RestoreSelectedMotion();
        }

        TryUseProjectSelection(false);
        EditorApplication.update -= TickPlayback;
        EditorApplication.update += TickPlayback;
    }

    private void OnDisable()
    {
        EditorApplication.update -= TickPlayback;
        StopPlayback(false);
        CleanupPreview();
    }

    private void OnSelectionChange()
    {
        TryUseProjectSelection(true);
    }

    private void OnGUI()
    {
        DrawHeader();
        DrawSourceControls();

        if (!dataLoaded)
        {
            EditorGUILayout.HelpBox("데이터를 불러오는 중입니다.", MessageType.Info);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        DrawMonsterAndMotionLists();
        DrawPreviewPanel();
        EditorGUILayout.EndHorizontal();

        DrawStatusBar();
        HandleKeyboardShortcuts();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(WindowTitle, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "모션을 재생하면서 SFX 후보를 바꿔 듣고, 마음에 드는 조합만 별도 파일에 저장합니다.",
            EditorStyles.miniLabel);
        EditorGUILayout.Space(4f);
    }

    private void DrawSourceControls()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("몬스터 폴더", GUILayout.Width(80f));
        EditorGUILayout.SelectableLabel(MonsterRoot, EditorStyles.textField, GUILayout.Height(18f));
        if (GUILayout.Button("모션 새로고침", GUILayout.Width(110f)))
            RefreshData();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("선택 SFX", GUILayout.Width(80f));
        AudioClip nextAudioClip = (AudioClip)EditorGUILayout.ObjectField(
            selectedAudioClip,
            typeof(AudioClip),
            false);
        if (nextAudioClip != selectedAudioClip)
            SelectAudioClip(nextAudioClip, true);

        using (new EditorGUI.DisabledScope(selectedAudioClip == null))
        {
            if (GUILayout.Button("위치 표시", GUILayout.Width(76f)))
                EditorGUIUtility.PingObject(selectedAudioClip);
            if (GUILayout.Button("비우기", GUILayout.Width(60f)))
                SelectAudioClip(null, false);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(
            "Project 창에서 AudioClip을 클릭하면 자동으로 적용하고 모션과 함께 다시 재생합니다.",
            EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6f);
    }

    private void DrawMonsterAndMotionLists()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(ListWidth));
        DrawSectionTitle("몬스터", monsterNames.Count.ToString("N0"));
        monsterSearch = EditorGUILayout.DelayedTextField("검색", monsterSearch);

        monsterScroll = EditorGUILayout.BeginScrollView(monsterScroll, GUILayout.Height(210f));
        foreach (string monsterName in monsterNames)
        {
            if (!Matches(monsterName, monsterSearch))
                continue;

            bool selected = string.Equals(selectedMonsterName, monsterName, StringComparison.Ordinal);
            if (DrawListButton(monsterName, selected))
            {
                StopPlayback(false);
                CleanupPreview();
                selectedMonsterName = monsterName;
                selectedMotionKey = string.Empty;
                selectedMotion = null;
                previewRotationY = 0f;
                LoadSelectedMonsterMotions();
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8f);
        DrawSectionTitle("모션", GetMotionCountForSelectedMonster().ToString("N0"));
        motionSearch = EditorGUILayout.DelayedTextField("검색", motionSearch);
        motionScroll = EditorGUILayout.BeginScrollView(motionScroll);
        foreach (MotionEntry entry in GetFilteredMotionEntries())
        {
            bool selected = entry.Key == selectedMotionKey;
            if (DrawListButton(entry.MotionName, selected))
                SelectMotion(entry);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawPreviewPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        DrawSectionTitle("모션 미리보기", selectedMotion == null ? "선택 안 됨" : selectedMotion.MotionName);

        Rect previewRect = GUILayoutUtility.GetRect(10f, 390f, GUILayout.ExpandWidth(true));
        if (previewUtility == null || previewInstance == null)
        {
            EditorGUI.DrawRect(previewRect, new Color(0.07f, 0.08f, 0.1f, 1f));
            GUI.Label(previewRect, "왼쪽에서 모션을 선택하세요.", CenteredLabelStyle());
        }
        else if (Event.current.type == EventType.Repaint)
        {
            previewUtility.BeginPreview(previewRect, GUIStyle.none);
            previewUtility.Render(true);
            Texture previewTexture = previewUtility.EndPreview();
            if (previewTexture != null)
                GUI.DrawTexture(previewRect, previewTexture, ScaleMode.ScaleToFit, true);
        }

        DrawPreviewControls();
        EditorGUILayout.EndVertical();
    }

    private void DrawPreviewControls()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(selectedMotion == null))
        {
            if (GUILayout.Button(isPlaying && !isPaused ? "일시정지" : "재생", GUILayout.Width(76f)))
                TogglePlayPause();
            if (GUILayout.Button("처음부터", GUILayout.Width(76f)))
                StartPlayback();
            if (GUILayout.Button("정지", GUILayout.Width(60f)))
                StopPlayback();
            bool nextLoopMotion = GUILayout.Toggle(loopMotion, "모션 반복", GUILayout.Width(84f));
            if (nextLoopMotion != loopMotion)
            {
                loopMotion = nextLoopMotion;
                if (isPlaying || isPaused)
                    StartPlayback();
            }
        }
        EditorGUILayout.EndHorizontal();

        float motionLength = selectedMotion == null ? 1f : Mathf.Max(selectedMotion.Clip.length, 0.01f);
        float nextTime = EditorGUILayout.Slider("모션 시간", elapsedSeconds, 0f, motionLength);
        if (!isPlaying && !Mathf.Approximately(nextTime, elapsedSeconds))
        {
            elapsedSeconds = nextTime;
            EvaluateMotion(elapsedSeconds);
        }

        sfxDelaySeconds = EditorGUILayout.Slider("SFX 지연", sfxDelaySeconds, 0f, motionLength);
        EditorGUI.BeginChangeCheck();
        previewVolume = EditorGUILayout.Slider("SFX 볼륨", previewVolume, 0f, 1f);
        if (EditorGUI.EndChangeCheck() && sfxStarted)
            SfxEditorAudioPreview.SetVolume(previewVolume);
        previewRotationY = EditorGUILayout.Slider("정면 회전", previewRotationY, -180f, 180f);
        if (previewRoot != null)
            previewRoot.transform.localRotation = Quaternion.Euler(0f, previewRotationY, 0f);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            selectedAudioClip == null ? "SFX를 선택하세요." : selectedAudioClip.name,
            EditorStyles.miniLabel);
        using (new EditorGUI.DisabledScope(selectedMotion == null || selectedAudioClip == null))
        {
            if (GUILayout.Button("현재 조합 저장", GUILayout.Width(120f)))
                SaveCurrentMapping();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        if (selectedMotion != null)
        {
            EditorGUILayout.LabelField(
                $"{selectedMotion.MonsterName} / {selectedMotion.MotionName} / {selectedMotion.Clip.length:0.00}s",
                EditorStyles.miniLabel);
        }
    }

    private void DrawStatusBar()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(statusMessage, statusType);
    }

    private void RefreshData()
    {
        StopPlayback(false);
        CleanupPreview();
        dataLoaded = false;

        if (RefreshMonsterIndex())
        {
            EnsureSelectedMonster();
            LoadSelectedMonsterMotions();
            RestoreSelectedMotion();
        }

        dataLoaded = true;
        Repaint();
    }

    private bool RefreshMonsterIndex()
    {
        motionEntries.Clear();
        monsterNames.Clear();
        monsterEntries.Clear();
        motionCache.Clear();

        if (!AssetDatabase.IsValidFolder(MonsterRoot))
        {
            SetStatus($"몬스터 폴더를 찾을 수 없습니다: {MonsterRoot}", MessageType.Warning);
            return false;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { MonsterRoot });
        foreach (string guid in prefabGuids
                     .OrderBy(AssetDatabase.GUIDToAssetPath, StringComparer.OrdinalIgnoreCase))
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            string monsterName = Path.GetFileNameWithoutExtension(prefabPath);
            if (string.IsNullOrWhiteSpace(monsterName) || monsterEntries.ContainsKey(monsterName))
                continue;

            MonsterEntry entry = new MonsterEntry(monsterName, prefabPath);
            monsterEntries.Add(monsterName, entry);
            monsterNames.Add(monsterName);
        }

        monsterNames.Sort(StringComparer.OrdinalIgnoreCase);
        SetStatus($"몬스터 {monsterNames.Count:N0}개를 불러왔습니다. 몬스터를 선택하면 해당 모션만 읽습니다.", MessageType.Info);
        return monsterNames.Count > 0;
    }

    private void EnsureSelectedMonster()
    {
        if (!string.IsNullOrEmpty(selectedMonsterName) && monsterEntries.ContainsKey(selectedMonsterName))
            return;

        selectedMonsterName = monsterNames.FirstOrDefault() ?? string.Empty;
        selectedMotionKey = string.Empty;
        selectedMotion = null;
    }

    private void LoadSelectedMonsterMotions()
    {
        motionEntries.Clear();
        if (!monsterEntries.TryGetValue(selectedMonsterName, out MonsterEntry monsterEntry))
            return;

        if (!motionCache.TryGetValue(monsterEntry.PrefabPath, out MonsterMotionSet motionSet))
        {
            motionSet = CollectMotionSet(monsterEntry);
            motionCache.Add(monsterEntry.PrefabPath, motionSet);
        }

        motionEntries.AddRange(motionSet.Motions);
        if (motionEntries.Count == 0)
        {
            SetStatus($"{selectedMonsterName}: 재생 가능한 모션을 찾지 못했습니다.", MessageType.Warning);
            return;
        }

        string fallbackText = motionSet.UsedAvatarFolderFallback
            ? " Controller가 없는 Animator는 Avatar 폴더의 클립을 사용했습니다."
            : string.Empty;
        string missingText = motionSet.AnimatorWithoutClipsCount > 0
            ? $" 클립이 없는 Animator {motionSet.AnimatorWithoutClipsCount}개는 제외했습니다."
            : string.Empty;
        SetStatus(
            $"{selectedMonsterName}: 모션 {motionEntries.Count:N0}개를 불러왔습니다.{fallbackText}{missingText}",
            motionSet.AnimatorWithoutClipsCount > 0 ? MessageType.Warning : MessageType.Info);
    }

    private void RestoreSelectedMotion()
    {
        if (string.IsNullOrEmpty(selectedMotionKey))
            return;

        selectedMotion = motionEntries.FirstOrDefault(entry => entry.Key == selectedMotionKey);
        if (selectedMotion == null)
        {
            selectedMotionKey = string.Empty;
            return;
        }

        RebuildPreviewInstance();
    }

    private static MonsterMotionSet CollectMotionSet(MonsterEntry monsterEntry)
    {
        List<MotionEntry> motions = new List<MotionEntry>();
        GameObject projectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(monsterEntry.PrefabPath);
        if (projectPrefab == null)
            return new MonsterMotionSet(motions, false, 0);

        GameObject animationPrefab = ResolveAnimationPrefab(projectPrefab);
        bool usedAvatarFolderFallback = false;
        int animatorWithoutClipsCount = 0;
        HashSet<string> usedMotionKeys = new HashSet<string>(StringComparer.Ordinal);
        Animator[] animators = animationPrefab.GetComponentsInChildren<Animator>(true)
            .OrderBy(animator => AnimationUtility.CalculateTransformPath(animator.transform, animationPrefab.transform),
                StringComparer.Ordinal)
            .ToArray();

        foreach (Animator animator in animators)
        {
            string animatorPath = AnimationUtility.CalculateTransformPath(animator.transform, animationPrefab.transform);
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            IEnumerable<AnimationClip> clips;

            if (controller != null && controller.animationClips.Length > 0)
            {
                clips = controller.animationClips;
            }
            else
            {
                clips = FindAvatarFolderAnimationClips(animator);
                usedAvatarFolderFallback = true;
            }

            int addedClipCount = 0;
            foreach (AnimationClip clip in clips.Where(clip => clip != null).Distinct())
            {
                if (clip.length <= 0f || clip.legacy || clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    continue;

                string motionName = ResolveMotionName(clip.name);
                if (string.IsNullOrWhiteSpace(motionName))
                    continue;

                string motionKey = BuildMotionKey(monsterEntry.PrefabPath, animatorPath, clip);
                if (!usedMotionKeys.Add(motionKey))
                    continue;

                motions.Add(new MotionEntry(
                    projectPrefab,
                    animationPrefab,
                    clip,
                    monsterEntry.Name,
                    motionName,
                    monsterEntry.PrefabPath,
                    animatorPath,
                    motionKey));
                addedClipCount++;
            }

            if (addedClipCount == 0)
                animatorWithoutClipsCount++;
        }

        motions.Sort((left, right) =>
        {
            return string.Compare(left.MotionName, right.MotionName, StringComparison.OrdinalIgnoreCase);
        });

        return new MonsterMotionSet(motions, usedAvatarFolderFallback, animatorWithoutClipsCount);
    }

    private static GameObject ResolveAnimationPrefab(GameObject projectPrefab)
    {
        string projectPrefabPath = AssetDatabase.GetAssetPath(projectPrefab);
        foreach (Transform child in projectPrefab.GetComponentsInChildren<Transform>(true))
        {
            GameObject sourceObject = PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject);
            if (sourceObject == null || sourceObject == projectPrefab)
                continue;

            GameObject sourceRoot = sourceObject.transform.root.gameObject;
            string sourcePrefabPath = AssetDatabase.GetAssetPath(sourceRoot);
            if (string.IsNullOrEmpty(sourcePrefabPath) || sourcePrefabPath == projectPrefabPath)
                continue;

            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
            if (sourcePrefab != null && sourcePrefab.GetComponentsInChildren<Animator>(true).Length > 0)
                return sourcePrefab;
        }

        GameObject prefabContents = null;
        try
        {
            prefabContents = PrefabUtility.LoadPrefabContents(projectPrefabPath);
            foreach (Transform child in prefabContents.GetComponentsInChildren<Transform>(true))
            {
                GameObject nestedInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(child.gameObject);
                if (nestedInstanceRoot == null || nestedInstanceRoot == prefabContents)
                    continue;

                GameObject sourceObject = PrefabUtility.GetCorrespondingObjectFromOriginalSource(nestedInstanceRoot);
                if (sourceObject == null)
                    continue;

                GameObject sourceRoot = sourceObject.transform.root.gameObject;
                string sourcePrefabPath = AssetDatabase.GetAssetPath(sourceRoot);
                if (string.IsNullOrEmpty(sourcePrefabPath) || sourcePrefabPath == projectPrefabPath)
                    continue;

                GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
                if (sourcePrefab != null && sourcePrefab.GetComponentsInChildren<Animator>(true).Length > 0)
                    return sourcePrefab;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MonsterMotionSfxBrowser] 중첩 프리팹 원본 탐색을 건너뛰었습니다: {projectPrefabPath}\n{exception.Message}");
        }
        finally
        {
            if (prefabContents != null)
                PrefabUtility.UnloadPrefabContents(prefabContents);
        }

        return projectPrefab;
    }

    private static IEnumerable<AnimationClip> FindAvatarFolderAnimationClips(Animator animator)
    {
        string avatarPath = AssetDatabase.GetAssetPath(animator.avatar);
        if (string.IsNullOrEmpty(avatarPath))
        {
            GameObject originalSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(animator.gameObject);
            avatarPath = AssetDatabase.GetAssetPath(originalSource);
        }

        string folderPath = Path.GetDirectoryName(avatarPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            return Array.Empty<AnimationClip>();

        List<AnimationClip> clips = new List<AnimationClip>();
        HashSet<int> usedClipIds = new HashSet<int>();
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folderPath }))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>())
            {
                if (usedClipIds.Add(clip.GetInstanceID()))
                    clips.Add(clip);
            }
        }

        return clips;
    }

    private void SelectMotion(MotionEntry entry)
    {
        StopPlayback(false);
        CleanupPreview();
        selectedMotion = entry;
        selectedMotionKey = entry.Key;
        elapsedSeconds = 0f;
        sfxStarted = false;
        RebuildPreviewInstance();
        if (previewInstance != null)
        {
            SetStatus(
                string.IsNullOrEmpty(previewBindingMessage)
                    ? $"선택됨: {entry.MonsterName} / {entry.MotionName}"
                    : $"선택됨: {entry.MonsterName} / {entry.MotionName} | {previewBindingMessage}",
                previewBindingMessageType);
        }
    }

    private void RebuildPreviewInstance()
    {
        CleanupPreview();
        if (selectedMotion == null || selectedMotion.ProjectPrefab == null)
            return;

        try
        {
            previewRoot = new GameObject("[MonsterMotionSfxBrowser] Preview Root");
            previewInstance = Instantiate(selectedMotion.PreviewPrefab);
            previewInstance.name = "[MonsterMotionSfxBrowser] " + selectedMotion.MonsterName;
            previewInstance.transform.SetParent(previewRoot.transform, false);
            SetHideFlagsRecursive(previewRoot, HideFlags.HideAndDontSave);

            previewAnimator = FindPreviewAnimator(previewInstance, selectedMotion.AnimatorPath);
            if (previewAnimator == null)
                throw new InvalidOperationException($"Animator를 찾을 수 없습니다: {selectedMotion.AnimatorPath}");

            PreparePreviewAnimators(previewInstance);
            AnalyzePreviewBindings(previewInstance, selectedMotion.Clip);
            previewMaterials = ApplyPreviewMaterials(previewInstance);
            previewUtility = CreatePreview(previewRoot);
            previewRoot.transform.localRotation = Quaternion.Euler(0f, previewRotationY, 0f);
            if (!EvaluateMotion(0f))
            {
                CleanupPreview();
                return;
            }
            ConfigureCamera(previewUtility.camera, CalculateAnimationBounds(previewInstance, selectedMotion.Clip));
            if (!EvaluateMotion(0f))
                CleanupPreview();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SetStatus(
                $"미리보기를 만들지 못했습니다: {selectedMotion.MonsterName} / {selectedMotion.MotionName}",
                MessageType.Error);
            CleanupPreview();
        }
    }

    private void StartPlayback()
    {
        if (selectedMotion == null)
            return;

        if (previewInstance == null)
            RebuildPreviewInstance();
        if (previewInstance == null)
            return;

        StopPlayback(false);
        elapsedSeconds = 0f;
        sfxStarted = false;
        nextSfxTriggerSeconds = Mathf.Clamp(sfxDelaySeconds, 0f, selectedMotion.Clip.length);
        playbackStartedAt = EditorApplication.timeSinceStartup;
        lastPreviewUpdateAt = 0d;
        isPlaying = true;
        isPaused = false;
        EvaluateMotion(0f);
        TryPlayScheduledSfx(0f, Mathf.Max(selectedMotion.Clip.length, 0.01f));
        Repaint();
    }

    private void TogglePlayPause()
    {
        if (!isPlaying)
        {
            StartPlayback();
            return;
        }

        if (isPaused)
        {
            playbackStartedAt = EditorApplication.timeSinceStartup - elapsedSeconds;
            isPaused = false;
            if (sfxStarted)
                SfxEditorAudioPreview.Resume();
        }
        else
        {
            elapsedSeconds = GetPlaybackElapsed();
            isPaused = true;
            if (sfxStarted)
                SfxEditorAudioPreview.Pause();
        }
        Repaint();
    }

    private void StopPlayback(bool resetMotion = true)
    {
        isPlaying = false;
        isPaused = false;
        sfxStarted = false;
        elapsedSeconds = 0f;
        nextSfxTriggerSeconds = 0f;
        SfxEditorAudioPreview.StopAll();

        if (resetMotion && selectedMotion != null && previewInstance != null)
            EvaluateMotion(0f);
        Repaint();
    }

    private void TickPlayback()
    {
        if (!isPlaying || isPaused || selectedMotion == null)
            return;

        double now = EditorApplication.timeSinceStartup;
        if (now - lastPreviewUpdateAt < PreviewFrameInterval)
            return;
        lastPreviewUpdateAt = now;

        elapsedSeconds = GetPlaybackElapsed();
        float motionLength = Mathf.Max(selectedMotion.Clip.length, 0.01f);
        float sampleTime = loopMotion ? Mathf.Repeat(elapsedSeconds, motionLength) : Mathf.Min(elapsedSeconds, motionLength);
        EvaluateMotion(sampleTime);
        TryPlayScheduledSfx(elapsedSeconds, motionLength);

        if (!loopMotion && elapsedSeconds >= motionLength)
            isPlaying = false;

        Repaint();
    }

    private void TryPlayScheduledSfx(float playbackSeconds, float motionLength)
    {
        if (selectedAudioClip == null || playbackSeconds < nextSfxTriggerSeconds)
            return;

        if (!StartSelectedSfx())
            return;
        sfxStarted = true;

        if (!loopMotion)
        {
            nextSfxTriggerSeconds = float.PositiveInfinity;
            return;
        }

        float delay = Mathf.Clamp(sfxDelaySeconds, 0f, motionLength);
        int nextLoopIndex = Mathf.FloorToInt(Mathf.Max(0f, playbackSeconds - delay) / motionLength) + 1;
        nextSfxTriggerSeconds = delay + nextLoopIndex * motionLength;
    }

    private bool StartSelectedSfx()
    {
        if (selectedAudioClip == null)
            return false;
        if (SfxEditorAudioPreview.Play(selectedAudioClip, 0, false, previewVolume))
            return true;

        SetStatus("Unity Editor의 AudioUtil 미리듣기 API를 사용할 수 없습니다.", MessageType.Warning);
        return false;
    }

    private void TryUseProjectSelection(bool replayCombined)
    {
        AudioClip clip = ResolveAudioClip(Selection.activeObject);
        if (clip == null || clip == selectedAudioClip)
            return;

        SelectAudioClip(clip, replayCombined);
    }

    private void SelectAudioClip(AudioClip clip, bool replayCombined)
    {
        selectedAudioClip = clip;
        if (selectedAudioClip == null)
        {
            SfxEditorAudioPreview.StopAll();
            sfxStarted = false;
            SetStatus("SFX 선택을 비웠습니다.", MessageType.Info);
            Repaint();
            return;
        }

        SetStatus($"Project 선택 SFX 적용: {selectedAudioClip.name}", MessageType.Info);
        if (replayCombined)
        {
            if (selectedMotion != null)
                StartPlayback();
            else
                sfxStarted = StartSelectedSfx();
        }
        Repaint();
    }

    private static AudioClip ResolveAudioClip(UnityEngine.Object asset)
    {
        if (asset is AudioClip clip)
            return clip;
        if (asset == null)
            return null;

        string assetPath = AssetDatabase.GetAssetPath(asset);
        return string.IsNullOrEmpty(assetPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
    }

    private bool EvaluateMotion(float time)
    {
        if (selectedMotion == null
            || previewInstance == null
            || selectedMotion.Clip == null)
            return false;

        try
        {
            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
                animationModeStartedByTool = true;
            }

            bool samplingStarted = false;
            try
            {
                AnimationMode.BeginSampling();
                samplingStarted = true;
                AnimationMode.SampleAnimationClip(
                    previewInstance,
                    selectedMotion.Clip,
                    Mathf.Clamp(time, 0f, selectedMotion.Clip.length));
            }
            finally
            {
                if (samplingStarted)
                    AnimationMode.EndSampling();
            }

            return true;
        }
        catch (Exception exception)
        {
            isPlaying = false;
            Debug.LogException(exception);
            SetStatus(
                $"모션을 재생하지 못했습니다: {selectedMotion.MonsterName} / {selectedMotion.MotionName}",
                MessageType.Error);
            return false;
        }
    }

    private float GetPlaybackElapsed()
    {
        return Mathf.Max(0f, (float)(EditorApplication.timeSinceStartup - playbackStartedAt));
    }

    private void SaveCurrentMapping()
    {
        if (selectedMotion == null || selectedAudioClip == null)
            return;

        MonsterMotionSfxMappingAsset asset = AssetDatabase.LoadAssetAtPath<MonsterMotionSfxMappingAsset>(MappingAssetPath);
        if (asset == null)
        {
            asset = CreateInstance<MonsterMotionSfxMappingAsset>();
            AssetDatabase.CreateAsset(asset, MappingAssetPath);
        }
        else
        {
            Undo.RecordObject(asset, "몬스터 모션 SFX 조합 저장");
        }

        if (asset.Mappings == null)
            asset.Mappings = new List<MonsterMotionSfxMapping>();

        string key = selectedMotion.Key;
        MonsterMotionSfxMapping mapping = asset.Mappings.FirstOrDefault(item => item.MotionKey == key);
        if (mapping == null)
        {
            mapping = new MonsterMotionSfxMapping();
            asset.Mappings.Add(mapping);
        }

        mapping.MotionKey = key;
        mapping.MonsterName = selectedMotion.MonsterName;
        mapping.MotionName = selectedMotion.MotionName;
        mapping.Motion = selectedMotion.Clip;
        mapping.MonsterPrefab = selectedMotion.ProjectPrefab;
        mapping.Sfx = selectedAudioClip;
        mapping.SfxDelaySeconds = sfxDelaySeconds;
        mapping.Volume = previewVolume;

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        SetStatus($"조합을 저장했습니다: {selectedMotion.MonsterName} / {selectedMotion.MotionName} + {selectedAudioClip.name}", MessageType.Info);
    }

    private List<MotionEntry> GetFilteredMotionEntries()
    {
        return motionEntries
            .Where(entry => string.Equals(entry.MonsterName, selectedMonsterName, StringComparison.OrdinalIgnoreCase))
            .Where(entry => Matches(entry.MotionName, motionSearch))
            .ToList();
    }

    private int GetMotionCountForSelectedMonster()
    {
        return motionEntries.Count(entry => string.Equals(entry.MonsterName, selectedMonsterName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool Matches(string value, string search)
    {
        return string.IsNullOrWhiteSpace(search)
               || value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool DrawListButton(string label, bool selected)
    {
        Color previousColor = GUI.backgroundColor;
        if (selected)
            GUI.backgroundColor = new Color(0.35f, 0.65f, 1f, 1f);
        bool clicked = GUILayout.Button(label, EditorStyles.miniButton, GUILayout.ExpandWidth(true));
        GUI.backgroundColor = previousColor;
        return clicked;
    }

    private static void DrawSectionTitle(string title, string detail)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField(detail, EditorStyles.miniLabel, GUILayout.Width(86f));
        EditorGUILayout.EndHorizontal();
    }

    private void HandleKeyboardShortcuts()
    {
        if (Event.current.type != EventType.KeyDown || Event.current.keyCode != KeyCode.Space)
            return;
        if (GUIUtility.keyboardControl != 0)
            return;
        TogglePlayPause();
        Event.current.Use();
    }

    private GUIStyle CenteredLabelStyle()
    {
        GUIStyle style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };
        return style;
    }

    private void SetStatus(string message, MessageType type)
    {
        statusMessage = message;
        statusType = type;
    }

    private void CleanupPreview()
    {
        if (animationModeStartedByTool && AnimationMode.InAnimationMode())
            AnimationMode.StopAnimationMode();
        animationModeStartedByTool = false;
        previewAnimator = null;

        if (previewUtility != null)
        {
            previewUtility.Cleanup();
            previewUtility = null;
        }

        if (previewMaterials != null)
        {
            foreach (Material material in previewMaterials)
            {
                if (material != null)
                    DestroyImmediate(material);
            }
            previewMaterials = null;
        }

        if (previewRoot != null)
        {
            DestroyImmediate(previewRoot);
            previewRoot = null;
        }

        previewInstance = null;
        previewBindingMessage = string.Empty;
        previewBindingMessageType = MessageType.Info;
    }

    private void AnalyzePreviewBindings(GameObject instance, AnimationClip clip)
    {
        previewBindingMessage = string.Empty;
        previewBindingMessageType = MessageType.Info;
        if (instance == null || clip == null)
            return;

        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        int transformBindingCount = 0;
        int matchedBindingCount = 0;
        int missingBindingCount = 0;
        int animatedBindingCount = 0;

        foreach (EditorCurveBinding binding in bindings)
        {
            if (binding.type != typeof(Transform))
                continue;

            transformBindingCount++;
            Transform target = string.IsNullOrEmpty(binding.path)
                ? instance.transform
                : instance.transform.Find(binding.path);
            if (target == null)
            {
                missingBindingCount++;
                continue;
            }

            matchedBindingCount++;
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (HasCurveVariation(curve))
                animatedBindingCount++;
        }

        if (transformBindingCount == 0)
        {
            previewBindingMessage = "Transform 커브가 없어 포즈가 고정될 수 있습니다.";
            previewBindingMessageType = MessageType.Warning;
        }
        else if (missingBindingCount > 0)
        {
            previewBindingMessage =
                $"Transform 바인딩 {missingBindingCount}개를 프리팹에서 찾지 못했습니다. " +
                $"일치 {matchedBindingCount}/{transformBindingCount}";
            previewBindingMessageType = MessageType.Warning;
        }
        else if (animatedBindingCount == 0)
        {
            previewBindingMessage = "Transform 커브는 있지만 값 변화가 없어 포즈가 고정될 수 있습니다.";
            previewBindingMessageType = MessageType.Warning;
        }
        else
        {
            previewBindingMessage =
                $"Transform 커브 {transformBindingCount}개 / 실제 변화 감지 {animatedBindingCount}개";
            previewBindingMessageType = MessageType.Info;
        }
    }

    private static bool HasCurveVariation(AnimationCurve curve)
    {
        if (curve == null || curve.length < 2)
            return false;

        float firstValue = curve.keys[0].value;
        for (int index = 1; index < curve.length; index++)
        {
            if (!Mathf.Approximately(firstValue, curve.keys[index].value))
                return true;
        }

        return false;
    }

    private static Animator FindPreviewAnimator(GameObject instance, string animatorPath)
    {
        Animator[] animators = instance.GetComponentsInChildren<Animator>(true);
        foreach (Animator animator in animators)
        {
            string path = AnimationUtility.CalculateTransformPath(animator.transform, instance.transform);
            if (string.Equals(path, animatorPath, StringComparison.Ordinal))
                return animator;
        }

        return animators.Length == 1 ? animators[0] : null;
    }

    private static PreviewRenderUtility CreatePreview(GameObject instance)
    {
        PreviewRenderUtility preview = new PreviewRenderUtility();
        preview.camera.cameraType = CameraType.Preview;
        preview.camera.clearFlags = CameraClearFlags.SolidColor;
        preview.camera.backgroundColor = new Color(0.055f, 0.065f, 0.085f, 1f);
        preview.camera.nearClipPlane = 0.01f;
        preview.camera.farClipPlane = 5000f;
        preview.camera.fieldOfView = PreviewFieldOfView;
        preview.ambientColor = new Color(0.48f, 0.48f, 0.48f, 1f);

        if (preview.lights.Length > 0 && preview.lights[0] != null)
        {
            preview.lights[0].type = LightType.Directional;
            preview.lights[0].intensity = 2.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, -35f, 0f);
        }

        if (preview.lights.Length > 1 && preview.lights[1] != null)
        {
            preview.lights[1].type = LightType.Directional;
            preview.lights[1].intensity = 1.25f;
            preview.lights[1].transform.rotation = Quaternion.Euler(325f, 140f, 0f);
        }

        preview.AddSingleGO(instance);
        return preview;
    }

    private Bounds CalculateAnimationBounds(GameObject instance, AnimationClip clip)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(instance.transform.position, Vector3.one);
        bool hasBounds = false;

        const int SampleCount = 10;
        for (int index = 0; index < SampleCount; index++)
        {
            float normalizedTime = index / (float)(SampleCount - 1);
            if (!EvaluateMotion(normalizedTime * clip.length))
                break;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled)
                    continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        return hasBounds ? bounds : new Bounds(instance.transform.position, Vector3.one);
    }

    private static void ConfigureCamera(Camera camera, Bounds bounds)
    {
        Vector3 target = bounds.center;
        // ThirdParty 미리보기 씬과 같은 +Z 전방 기준으로 카메라를 배치한다.
        Vector3 direction = new Vector3(0f, 0.12f, 1f).normalized;
        float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
        float distance = radius * PreviewCameraPadding / Mathf.Tan(PreviewFieldOfView * 0.5f * Mathf.Deg2Rad);
        camera.transform.position = target + direction * distance;
        camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);
    }

    private static List<Material> ApplyPreviewMaterials(GameObject instance)
    {
        List<Material> createdMaterials = new List<Material>();
        Shader litFallbackShader = Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard")
                                   ?? Shader.Find("Unlit/Color");
        Shader transparentFallbackShader = Shader.Find("Universal Render Pipeline/Unlit")
                                            ?? litFallbackShader;
        if (litFallbackShader == null)
            return createdMaterials;

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] sourceMaterials = renderer.sharedMaterials;
            Material[] previewMaterials = new Material[sourceMaterials.Length];
            for (int index = 0; index < sourceMaterials.Length; index++)
            {
                Material sourceMaterial = sourceMaterials[index];
                if (IsUrpCompatibleMaterial(sourceMaterial))
                {
                    previewMaterials[index] = sourceMaterial;
                    continue;
                }

                Shader fallbackShader = IsTransparentSourceMaterial(sourceMaterial)
                    ? transparentFallbackShader
                    : litFallbackShader;
                Material previewMaterial = CreateFallbackMaterial(sourceMaterial, fallbackShader);
                previewMaterial.name = "[MonsterMotionSfxBrowser] Preview Material";
                previewMaterial.hideFlags = HideFlags.HideAndDontSave;
                previewMaterials[index] = previewMaterial;
                createdMaterials.Add(previewMaterial);
            }
            renderer.sharedMaterials = previewMaterials;
        }

        return createdMaterials;
    }

    private static Material CreateFallbackMaterial(Material sourceMaterial, Shader fallbackShader)
    {
        Material fallbackMaterial = new Material(fallbackShader);
        if (sourceMaterial == null)
            return fallbackMaterial;

        Texture sourceTexture = sourceMaterial.mainTexture;
        if (sourceTexture != null)
        {
            if (fallbackMaterial.HasProperty("_BaseMap"))
                fallbackMaterial.SetTexture("_BaseMap", sourceTexture);
            if (fallbackMaterial.HasProperty("_MainTex"))
                fallbackMaterial.SetTexture("_MainTex", sourceTexture);
        }

        Color sourceColor = Color.white;
        if (sourceMaterial.HasProperty("_BaseColor"))
            sourceColor = sourceMaterial.GetColor("_BaseColor");
        else if (sourceMaterial.HasProperty("_Color"))
            sourceColor = sourceMaterial.GetColor("_Color");

        if (fallbackMaterial.HasProperty("_BaseColor"))
            fallbackMaterial.SetColor("_BaseColor", sourceColor);
        if (fallbackMaterial.HasProperty("_Color"))
            fallbackMaterial.SetColor("_Color", sourceColor);

        bool alphaClip = IsAlphaClipSourceMaterial(sourceMaterial);
        bool premultiplied = IsPremultipliedSourceMaterial(sourceMaterial);
        bool transparent = IsTransparentSourceMaterial(sourceMaterial);

        if (fallbackMaterial.HasProperty("_AlphaClip"))
            fallbackMaterial.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
        if (fallbackMaterial.HasProperty("_Cutoff") && sourceMaterial.HasProperty("_Cutoff"))
            fallbackMaterial.SetFloat("_Cutoff", sourceMaterial.GetFloat("_Cutoff"));

        if (alphaClip)
            fallbackMaterial.EnableKeyword("_ALPHATEST_ON");

        if (transparent)
        {
            if (fallbackMaterial.HasProperty("_Surface"))
                fallbackMaterial.SetFloat("_Surface", 1f);
            if (fallbackMaterial.HasProperty("_Blend"))
                fallbackMaterial.SetFloat("_Blend", premultiplied ? 1f : 0f);
            if (fallbackMaterial.HasProperty("_SrcBlend"))
                fallbackMaterial.SetInt("_SrcBlend", (int)(premultiplied ? BlendMode.One : BlendMode.SrcAlpha));
            if (fallbackMaterial.HasProperty("_DstBlend"))
                fallbackMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (fallbackMaterial.HasProperty("_ZWrite"))
                fallbackMaterial.SetInt("_ZWrite", 0);

            if (premultiplied)
                fallbackMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            else
                fallbackMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            fallbackMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            fallbackMaterial.SetOverrideTag("RenderType", "Transparent");
            fallbackMaterial.renderQueue = (int)RenderQueue.Transparent;
        }

        return fallbackMaterial;
    }

    private static bool IsTransparentSourceMaterial(Material material)
    {
        if (material == null)
            return false;

        float mode = material.HasProperty("_Mode") ? material.GetFloat("_Mode") : -1f;
        string renderType = material.GetTag("RenderType", false, string.Empty);
        return material.renderQueue >= (int)RenderQueue.Transparent
               || string.Equals(renderType, "Transparent", StringComparison.OrdinalIgnoreCase)
               || material.IsKeywordEnabled("_ALPHABLEND_ON")
               || material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON")
               || Mathf.Approximately(mode, 2f)
               || Mathf.Approximately(mode, 3f);
    }

    private static bool IsAlphaClipSourceMaterial(Material material)
    {
        if (material == null)
            return false;

        float mode = material.HasProperty("_Mode") ? material.GetFloat("_Mode") : -1f;
        return material.IsKeywordEnabled("_ALPHATEST_ON") || Mathf.Approximately(mode, 1f);
    }

    private static bool IsPremultipliedSourceMaterial(Material material)
    {
        if (material == null)
            return false;

        float mode = material.HasProperty("_Mode") ? material.GetFloat("_Mode") : -1f;
        return material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON") || Mathf.Approximately(mode, 3f);
    }

    private static bool IsUrpCompatibleMaterial(Material material)
    {
        if (material == null || material.shader == null || !material.shader.isSupported)
            return false;

        string shaderName = material.shader.name;
        return shaderName.StartsWith("Toon/", StringComparison.OrdinalIgnoreCase)
               || shaderName.IndexOf("Universal Render Pipeline", StringComparison.OrdinalIgnoreCase) >= 0
               || shaderName.IndexOf("Shader Graphs", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void SetHideFlagsRecursive(GameObject target, HideFlags hideFlags)
    {
        target.hideFlags = hideFlags;
        foreach (Transform child in target.transform)
            SetHideFlagsRecursive(child.gameObject, hideFlags);
    }

    private static void PreparePreviewAnimators(GameObject instance)
    {
        foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
        {
            // Controller는 유지하되 자동 진행만 막고, AnimationMode가 포즈를 샘플링하게 한다.
            animator.enabled = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.speed = 0f;
        }
    }

    private static string ResolveMotionName(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return string.Empty;

        int separatorIndex = clipName.LastIndexOf('@');
        string motionName = separatorIndex >= 0 && separatorIndex < clipName.Length - 1
            ? clipName.Substring(separatorIndex + 1)
            : clipName;
        return motionName.Trim();
    }

    private static string BuildMotionKey(string prefabPath, string animatorPath, AnimationClip clip)
    {
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long localId))
            return prefabPath + "|" + animatorPath + "|" + guid + "|" + localId;

        return prefabPath + "|" + animatorPath + "|" + AssetDatabase.GetAssetPath(clip) + "|" + clip.name;
    }

    private sealed class MonsterEntry
    {
        public MonsterEntry(string name, string prefabPath)
        {
            Name = name;
            PrefabPath = prefabPath;
        }

        public string Name { get; }
        public string PrefabPath { get; }
    }

    private sealed class MonsterMotionSet
    {
        public MonsterMotionSet(
            List<MotionEntry> motions,
            bool usedAvatarFolderFallback,
            int animatorWithoutClipsCount)
        {
            Motions = motions;
            UsedAvatarFolderFallback = usedAvatarFolderFallback;
            AnimatorWithoutClipsCount = animatorWithoutClipsCount;
        }

        public List<MotionEntry> Motions { get; }
        public bool UsedAvatarFolderFallback { get; }
        public int AnimatorWithoutClipsCount { get; }
    }

    [Serializable]
    private sealed class MotionEntry
    {
        public MotionEntry(
            GameObject projectPrefab,
            GameObject previewPrefab,
            AnimationClip clip,
            string monsterName,
            string motionName,
            string prefabPath,
            string animatorPath,
            string key)
        {
            ProjectPrefab = projectPrefab;
            PreviewPrefab = previewPrefab;
            Clip = clip;
            MonsterName = monsterName;
            MotionName = motionName;
            PrefabPath = prefabPath;
            AnimatorPath = animatorPath;
            Key = key;
        }

        public GameObject ProjectPrefab { get; }
        public GameObject PreviewPrefab { get; }
        public AnimationClip Clip { get; }
        public string MonsterName { get; }
        public string MotionName { get; }
        public string PrefabPath { get; }
        public string AnimatorPath { get; }
        public string Key { get; }
    }
}
