using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 비인간 캐릭터 프리팹의 Animator 모션을 GIF 미리보기용 PNG 프레임으로 렌더링한다.
/// GIF 인코딩은 외부 Pillow 스크립트가 담당한다.
/// </summary>
public static class MonsterMotionGifExporter
{
    private const string SourceRoot = "Assets/ThirdParty/01_비인간캐릭터";
    private const string OutputRoot = SourceRoot + "/몬스터모션";
    private const string FrameRoot = "MonsterMotionGifFrames";
    private const int FrameSize = 160;
    private const int FramesPerSecond = 8;
    private const float MaxGifDuration = 2f;
    private const float CameraFieldOfView = 35f;
    private const float CameraPadding = 1.35f;

    [MenuItem("JC Tool/Animation/Export Monster Motion GIF Frames")]
    public static void ExportAll()
    {
        if (!Directory.Exists(ProjectPath(FrameRoot)))
            Directory.CreateDirectory(ProjectPath(FrameRoot));

        Directory.CreateDirectory(ProjectPath(OutputRoot));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        List<MonsterClipEntry> entries = CollectEntries();
        Debug.Log($"[MonsterMotionGifExporter] 대상 모션 {entries.Count}개를 찾았습니다.");
        ExportEntries(entries);
    }

    public static void ExportSample()
    {
        if (!Directory.Exists(ProjectPath(FrameRoot)))
            Directory.CreateDirectory(ProjectPath(FrameRoot));

        Directory.CreateDirectory(ProjectPath(OutputRoot));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        List<MonsterClipEntry> entries = CollectEntries().Take(1).ToList();
        Debug.Log($"[MonsterMotionGifExporter] 샘플 모션 {entries.Count}개를 렌더링합니다.");
        ExportEntries(entries);
    }

    private static void ExportEntries(List<MonsterClipEntry> entries)
    {

        HashSet<string> usedOutputNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int exportedClipCount = 0;
        int skippedClipCount = 0;

        try
        {
            AnimationMode.StartAnimationMode();

            for (int index = 0; index < entries.Count; index++)
            {
                MonsterClipEntry entry = entries[index];
                string outputName = ResolveUniqueOutputName(entry.MonsterName, entry.MotionName, usedOutputNames);
                string frameDirectory = ProjectPath(Path.Combine(FrameRoot, outputName));

                if (Directory.Exists(frameDirectory))
                    Directory.Delete(frameDirectory, true);
                Directory.CreateDirectory(frameDirectory);

                bool exported = ExportClipFrames(entry, frameDirectory);
                if (exported)
                    exportedClipCount++;
                else
                    skippedClipCount++;

                if ((index + 1) % 25 == 0 || index + 1 == entries.Count)
                    Debug.Log($"[MonsterMotionGifExporter] 진행 {index + 1}/{entries.Count} | 성공 {exportedClipCount} | 건너뜀 {skippedClipCount}");
            }
        }
        finally
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"[MonsterMotionGifExporter] PNG 프레임 생성 완료: 성공 {exportedClipCount}개, 건너뜀 {skippedClipCount}개");
    }

    private static List<MonsterClipEntry> CollectEntries()
    {
        List<MonsterClipEntry> entries = new List<MonsterClipEntry>();
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { SourceRoot });

        foreach (string prefabGuid in prefabGuids.OrderBy(guid => guid, StringComparer.Ordinal))
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                continue;

            Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
            HashSet<int> usedClipIds = new HashSet<int>();

            foreach (Animator animator in animators)
            {
                RuntimeAnimatorController controller = animator.runtimeAnimatorController;
                if (controller == null)
                    continue;

                foreach (AnimationClip clip in controller.animationClips)
                {
                    if (clip == null || clip.length <= 0f || !usedClipIds.Add(clip.GetInstanceID()))
                        continue;

                    string motionName = ResolveMotionName(clip.name);
                    if (string.IsNullOrWhiteSpace(motionName))
                        continue;

                    entries.Add(new MonsterClipEntry(prefab, clip, prefab.name, motionName, prefabPath));
                }
            }
        }

        return entries
            .OrderBy(entry => entry.MonsterName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.MotionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.PrefabPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ExportClipFrames(MonsterClipEntry entry, string frameDirectory)
    {
        GameObject instance = null;
        PreviewRenderUtility preview = null;
        Texture2D captureTexture = null;
        List<Material> previewMaterials = null;

        try
        {
            instance = UnityEngine.Object.Instantiate(entry.Prefab);
            if (instance == null)
                return false;

            instance.name = "[MonsterMotionGifExporter] " + entry.Prefab.name;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            SetHideFlagsRecursive(instance, HideFlags.HideAndDontSave);
            previewMaterials = ApplyPreviewMaterials(instance);

            preview = CreatePreview(instance);
            captureTexture = new Texture2D(FrameSize, FrameSize, TextureFormat.RGBA32, false, false);

            int frameCount = Mathf.Max(2, Mathf.CeilToInt(Mathf.Min(entry.Clip.length, MaxGifDuration) * FramesPerSecond));
            float duration = Mathf.Min(entry.Clip.length, MaxGifDuration);
            List<float> sampleTimes = BuildSampleTimes(entry.Clip, frameCount, duration);
            Bounds frameBounds = CalculateAnimationBounds(instance, entry.Clip, sampleTimes);
            ConfigureCamera(preview.camera, frameBounds);

            for (int frameIndex = 0; frameIndex < sampleTimes.Count; frameIndex++)
            {
                AnimationMode.SampleAnimationClip(instance, entry.Clip, sampleTimes[frameIndex]);
                CaptureFrame(preview, captureTexture, Path.Combine(frameDirectory, $"frame_{frameIndex:000}.png"));
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MonsterMotionGifExporter] 실패: {entry.PrefabPath} / {entry.Clip.name}\n{exception}");
            return false;
        }
        finally
        {
            if (captureTexture != null)
                UnityEngine.Object.DestroyImmediate(captureTexture);
            if (preview != null)
                preview.Cleanup();
            if (previewMaterials != null)
            {
                foreach (Material material in previewMaterials)
                    UnityEngine.Object.DestroyImmediate(material);
            }
            if (instance != null)
                UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static List<Material> ApplyPreviewMaterials(GameObject instance)
    {
        List<Material> createdMaterials = new List<Material>();
        Shader fallbackShader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
        if (fallbackShader == null)
            return createdMaterials;

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] sourceMaterials = renderer.sharedMaterials;
            Material[] fallbackMaterials = new Material[sourceMaterials.Length];

            for (int index = 0; index < sourceMaterials.Length; index++)
            {
                Material sourceMaterial = sourceMaterials[index];
                Material fallbackMaterial = new Material(fallbackShader)
                {
                    name = "[MonsterMotionGifExporter] Preview Material",
                    hideFlags = HideFlags.HideAndDontSave
                };

                if (sourceMaterial != null)
                {
                    Texture sourceTexture = sourceMaterial.mainTexture;
                    if (sourceTexture != null && fallbackMaterial.HasProperty("_MainTex"))
                        fallbackMaterial.SetTexture("_MainTex", sourceTexture);

                    if (fallbackMaterial.HasProperty("_Color"))
                        fallbackMaterial.SetColor("_Color", Color.white);
                }

                fallbackMaterials[index] = fallbackMaterial;
                createdMaterials.Add(fallbackMaterial);
            }

            renderer.sharedMaterials = fallbackMaterials;
        }

        return createdMaterials;
    }

    private static PreviewRenderUtility CreatePreview(GameObject instance)
    {
        PreviewRenderUtility preview = new PreviewRenderUtility();
        preview.camera.cameraType = CameraType.Preview;
        preview.camera.clearFlags = CameraClearFlags.SolidColor;
        preview.camera.backgroundColor = new Color(0.055f, 0.065f, 0.085f, 1f);
        preview.camera.nearClipPlane = 0.01f;
        preview.camera.farClipPlane = 5000f;
        preview.camera.fieldOfView = CameraFieldOfView;
        preview.camera.cullingMask = ~0;
        preview.ambientColor = new Color(0.48f, 0.48f, 0.48f, 1f);

        if (preview.lights.Length > 0 && preview.lights[0] != null)
        {
            preview.lights[0].type = LightType.Directional;
            preview.lights[0].intensity = 2.2f;
            preview.lights[0].color = new Color(1f, 0.94f, 0.86f, 1f);
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, -35f, 0f);
        }

        if (preview.lights.Length > 1 && preview.lights[1] != null)
        {
            preview.lights[1].type = LightType.Directional;
            preview.lights[1].intensity = 1.25f;
            preview.lights[1].color = new Color(0.62f, 0.74f, 1f, 1f);
            preview.lights[1].transform.rotation = Quaternion.Euler(325f, 140f, 0f);
        }

        preview.AddSingleGO(instance);
        return preview;
    }

    private static List<float> BuildSampleTimes(AnimationClip clip, int frameCount, float duration)
    {
        List<float> sampleTimes = new List<float>(frameCount);
        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            float normalizedTime = frameCount <= 1 ? 0f : frameIndex / (float)(frameCount - 1);
            sampleTimes.Add(Mathf.Min(clip.length, normalizedTime * duration));
        }

        return sampleTimes;
    }

    private static Bounds CalculateAnimationBounds(GameObject instance, AnimationClip clip, List<float> sampleTimes)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(instance.transform.position, Vector3.one);
        bool hasBounds = false;

        foreach (float sampleTime in sampleTimes)
        {
            AnimationMode.SampleAnimationClip(instance, clip, sampleTime);
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

        if (!hasBounds)
            bounds = new Bounds(instance.transform.position, Vector3.one);

        return bounds;
    }

    private static void ConfigureCamera(Camera camera, Bounds bounds)
    {
        Vector3 target = bounds.center;
        Vector3 direction = new Vector3(4f, 2.4f, -6f).normalized;
        float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
        float distance = radius * CameraPadding / Mathf.Tan(CameraFieldOfView * 0.5f * Mathf.Deg2Rad);

        camera.transform.position = target + direction * distance;
        camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);
    }

    private static void CaptureFrame(PreviewRenderUtility preview, Texture2D captureTexture, string outputPath)
    {
        Rect previewRect = new Rect(0f, 0f, FrameSize, FrameSize);
        preview.BeginPreview(previewRect, GUIStyle.none);
        preview.Render(true);
        Texture previewTexture = preview.EndPreview();
        RenderTexture renderTexture = previewTexture as RenderTexture;
        if (renderTexture == null)
            throw new InvalidOperationException("PreviewRenderUtility가 RenderTexture를 반환하지 않았습니다.");

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = renderTexture;
        captureTexture.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0);
        captureTexture.Apply(false, false);
        RenderTexture.active = previousActive;

        File.WriteAllBytes(outputPath, captureTexture.EncodeToPNG());
    }

    private static string ResolveUniqueOutputName(string monsterName, string motionName, HashSet<string> usedNames)
    {
        string baseName = SanitizeFilePart(monsterName) + "_" + SanitizeFilePart(motionName);
        string candidate = baseName;
        int suffix = 2;

        while (!usedNames.Add(candidate))
            candidate = baseName + "_" + suffix++;

        return candidate;
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

    private static string SanitizeFilePart(string value)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string sanitized = new string(value.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray());
        return string.Join(" ", sanitized.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    private static string ProjectPath(string relativePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void SetHideFlagsRecursive(GameObject target, HideFlags hideFlags)
    {
        target.hideFlags = hideFlags;
        foreach (Transform child in target.transform)
            SetHideFlagsRecursive(child.gameObject, hideFlags);
    }

    private sealed class MonsterClipEntry
    {
        public MonsterClipEntry(GameObject prefab, AnimationClip clip, string monsterName, string motionName, string prefabPath)
        {
            Prefab = prefab;
            Clip = clip;
            MonsterName = monsterName;
            MotionName = motionName;
            PrefabPath = prefabPath;
        }

        public GameObject Prefab { get; }
        public AnimationClip Clip { get; }
        public string MonsterName { get; }
        public string MotionName { get; }
        public string PrefabPath { get; }
    }
}
