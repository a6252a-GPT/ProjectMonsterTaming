using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// VFX Preview와 Monster Maker가 공유하는 격리형 Prefab Preview 기반
internal sealed class PrefabPreviewStage : IDisposable
{
    private static readonly PreviewEnvironment[] Environments =
    {
        new PreviewEnvironment(
            "스튜디오",
            new Color(0.235f, 0.255f, 0.29f, 1f),
            new Color(0.56f, 0.59f, 0.66f, 1f),
            new Color(1f, 0.96f, 0.9f, 1f),
            new Color(0.58f, 0.7f, 1f, 1f),
            2.2f,
            1.35f),
        new PreviewEnvironment(
            "다크",
            new Color(0.025f, 0.03f, 0.045f, 1f),
            new Color(0.24f, 0.27f, 0.34f, 1f),
            new Color(1f, 0.9f, 0.78f, 1f),
            new Color(0.36f, 0.55f, 1f, 1f),
            1.9f,
            1.05f),
        new PreviewEnvironment(
            "라이트",
            new Color(0.72f, 0.74f, 0.77f, 1f),
            new Color(0.78f, 0.8f, 0.84f, 1f),
            new Color(1f, 0.98f, 0.94f, 1f),
            new Color(0.72f, 0.82f, 1f, 1f),
            1.65f,
            0.8f),
        new PreviewEnvironment(
            "게임",
            new Color(0.065f, 0.085f, 0.125f, 1f),
            new Color(0.38f, 0.43f, 0.54f, 1f),
            new Color(1f, 0.84f, 0.62f, 1f),
            new Color(0.32f, 0.58f, 1f, 1f),
            2.6f,
            1.55f)
    };

    private PreviewRenderUtility utility;
    private GameObject previewRoot;
    private GameObject floor;
    private Material floorMaterial;
    private readonly List<GameObject> auxiliaries = new List<GameObject>();
    private Vector3 frameCenter = Vector3.up * 0.5f;
    private float frameRadius = 1f;
    private float cameraYaw = 180f;
    private float cameraPitch = 12f;
    private float cameraDistanceScale = 1f;
    private float framingScale = 1f;
    private int environmentIndex;

    public GameObject PreviewRoot => previewRoot;
    public Camera Camera => utility?.camera;
    public int EnvironmentIndex => environmentIndex;
    public static int EnvironmentCount => Environments.Length;

    public static string GetEnvironmentLabel(int index)
    {
        return Environments[Mathf.Clamp(index, 0, Environments.Length - 1)].Label;
    }

    public GameObject SetPrefab(GameObject prefab, Action<GameObject> configure = null)
    {
        EnsureUtility();
        ClearPrefab();
        if (prefab == null)
        {
            return null;
        }

        previewRoot = UnityEngine.Object.Instantiate(prefab);
        previewRoot.name = "[JC Preview] " + prefab.name;
        SetHideFlagsRecursive(previewRoot, HideFlags.HideAndDontSave);
        configure?.Invoke(previewRoot);
        utility.AddSingleGO(previewRoot);
        RecalculateBounds();
        RebuildFloor();
        return previewRoot;
    }

    public void ClearPrefab()
    {
        ClearAuxiliaries();
        if (previewRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(previewRoot);
            previewRoot = null;
        }

        ClearFloor();
    }

    public void AddAuxiliary(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        EnsureUtility();
        SetHideFlagsRecursive(instance, HideFlags.HideAndDontSave);
        utility.AddSingleGO(instance);
        auxiliaries.Add(instance);
    }

    public void RemoveAuxiliary(GameObject instance)
    {
        if (instance == null || !auxiliaries.Remove(instance))
        {
            return;
        }

        UnityEngine.Object.DestroyImmediate(instance);
    }

        public void SetEnvironment(int index)
        {
            environmentIndex = Mathf.Clamp(index, 0, Environments.Length - 1);
            ApplyEnvironment();
            if (previewRoot != null)
            {
                RebuildFloor();
            }
        }

    public void SetView(float yaw, float pitch, float distanceScale = 1f)
    {
        cameraYaw = yaw;
        cameraPitch = Mathf.Clamp(pitch, -80f, 80f);
        cameraDistanceScale = Mathf.Clamp(distanceScale, 0.15f, 8f);
    }

    public void SetFramingScale(float scale)
    {
        framingScale = Mathf.Clamp(scale, 0.01f, 100f);
    }

    public void RecalculateBounds(bool includeAuxiliaries = false)
    {
        if (previewRoot == null)
        {
            frameCenter = Vector3.up * 0.5f;
            frameRadius = 1f;
            return;
        }

        var hasBounds = false;
        var bounds = new Bounds(previewRoot.transform.position, Vector3.one);
        EncapsulateRenderers(previewRoot, ref hasBounds, ref bounds);
        if (includeAuxiliaries)
        {
            for (var index = 0; index < auxiliaries.Count; index++)
            {
                EncapsulateRenderers(auxiliaries[index], ref hasBounds, ref bounds);
            }
        }

        frameCenter = hasBounds ? bounds.center : previewRoot.transform.position + Vector3.up * 0.5f;
        frameRadius = hasBounds ? Mathf.Max(0.35f, bounds.extents.magnitude) : 1f;
        if (floor != null)
        {
            RebuildFloor();
        }
    }

    private static void EncapsulateRenderers(GameObject root, ref bool hasBounds, ref Bounds bounds)
    {
        if (root == null)
        {
            return;
        }

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (var index = 0; index < renderers.Length; index++)
        {
            var renderer = renderers[index];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

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

    public Texture Render(Rect rect)
    {
        EnsureUtility();
        if (previewRoot == null || rect.width < 2f || rect.height < 2f)
        {
            return null;
        }

        UpdateCamera(rect);
        utility.BeginPreview(rect, GUIStyle.none);
        utility.Render(true);
        return utility.EndPreview();
    }

    public void HandleInput(Rect rect, Event current)
    {
        if (current == null || !rect.Contains(current.mousePosition))
        {
            return;
        }

        if (current.type == EventType.MouseDrag && current.button == 1)
        {
            cameraYaw -= current.delta.x * 0.35f;
            cameraPitch = Mathf.Clamp(cameraPitch + current.delta.y * 0.35f, -80f, 80f);
            current.Use();
        }
        else if (current.type == EventType.ScrollWheel)
        {
            cameraDistanceScale = Mathf.Clamp(
                cameraDistanceScale * (1f + current.delta.y * 0.08f),
                0.15f,
                8f);
            current.Use();
        }
    }

    public void Dispose()
    {
        ClearPrefab();
        if (utility != null)
        {
            utility.Cleanup();
            utility = null;
        }
    }

    public static void ConfigureUniversalCamera(Camera camera)
    {
        if (camera == null)
        {
            return;
        }

        camera.cameraType = CameraType.Preview;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 5000f;
        camera.fieldOfView = 45f;
        camera.cullingMask = ~0;
        var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
        {
            cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }

        cameraData.renderPostProcessing = false;
    }

    public static void ConfigureLight(Light light, float intensity, Quaternion rotation, Color color)
    {
        if (light == null)
        {
            return;
        }

        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.transform.rotation = rotation;
    }

    public static Material CreateFloorMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                     Shader.Find("Standard") ??
                     Shader.Find("Unlit/Color") ??
                     Shader.Find("Sprites/Default") ??
                     Shader.Find("Hidden/Internal-Colored");
        if (shader == null)
        {
            return null;
        }

        var material = new Material(shader)
        {
            name = "[JC Preview Floor Material]",
            hideFlags = HideFlags.HideAndDontSave
        };
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        return material;
    }

    private void EnsureUtility()
    {
        if (utility != null)
        {
            return;
        }

        utility = new PreviewRenderUtility();
        ConfigureUniversalCamera(utility.camera);
        ApplyEnvironment();
    }

    private void ApplyEnvironment()
    {
        if (utility == null)
        {
            return;
        }

        var environment = Environments[Mathf.Clamp(environmentIndex, 0, Environments.Length - 1)];
        utility.camera.backgroundColor = environment.Background;
        utility.ambientColor = environment.Ambient;
        if (utility.lights.Length > 0)
        {
            ConfigureLight(
                utility.lights[0],
                environment.KeyIntensity,
                Quaternion.Euler(42f, -32f, 0f),
                environment.KeyColor);
        }

        if (utility.lights.Length > 1)
        {
            ConfigureLight(
                utility.lights[1],
                environment.FillIntensity,
                Quaternion.Euler(325f, 138f, 0f),
                environment.FillColor);
        }
    }

    private void UpdateCamera(Rect rect)
    {
        var yawRotation = Quaternion.AngleAxis(cameraYaw, Vector3.up);
        var right = yawRotation * Vector3.right;
        var direction = Quaternion.AngleAxis(cameraPitch, right) * (yawRotation * Vector3.forward);
        var aspect = Mathf.Max(0.2f, rect.width / Mathf.Max(1f, rect.height));
        var verticalHalfAngle = Mathf.Max(1f, utility.camera.fieldOfView * 0.5f) * Mathf.Deg2Rad;
        var horizontalHalfAngle = Mathf.Atan(Mathf.Tan(verticalHalfAngle) * aspect);
        var limitingAngle = Mathf.Min(verticalHalfAngle, horizontalHalfAngle);
        var distance = frameRadius / Mathf.Max(0.05f, Mathf.Sin(limitingAngle));
        distance *= 1.25f * cameraDistanceScale / framingScale;
        utility.camera.transform.position = frameCenter - direction.normalized * distance;
        utility.camera.transform.rotation = Quaternion.LookRotation(
            frameCenter - utility.camera.transform.position,
            Vector3.up);
    }

    private void RebuildFloor()
    {
        ClearFloor();
        if (utility == null || previewRoot == null)
        {
            return;
        }

        floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "[JC Preview Floor]";
        floor.hideFlags = HideFlags.HideAndDontSave;
        floor.transform.position = new Vector3(frameCenter.x, 0f, frameCenter.z);
        var size = Mathf.Max(4f, frameRadius * 4f);
        floor.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
        var collider = floor.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }

        var environment = Environments[Mathf.Clamp(environmentIndex, 0, Environments.Length - 1)];
        floorMaterial = CreateFloorMaterial(environment.Ambient * 0.62f);
        var renderer = floor.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = floorMaterial;
        }

        utility.AddSingleGO(floor);
    }

    private void ClearFloor()
    {
        if (floor != null)
        {
            UnityEngine.Object.DestroyImmediate(floor);
            floor = null;
        }

        if (floorMaterial != null)
        {
            UnityEngine.Object.DestroyImmediate(floorMaterial);
            floorMaterial = null;
        }
    }

    private void ClearAuxiliaries()
    {
        for (var index = auxiliaries.Count - 1; index >= 0; index--)
        {
            if (auxiliaries[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(auxiliaries[index]);
            }
        }

        auxiliaries.Clear();
    }

    private static void SetHideFlagsRecursive(GameObject root, HideFlags flags)
    {
        if (root == null)
        {
            return;
        }

        var transforms = root.GetComponentsInChildren<Transform>(true);
        for (var index = 0; index < transforms.Length; index++)
        {
            transforms[index].gameObject.hideFlags = flags;
        }
    }

    private readonly struct PreviewEnvironment
    {
        public PreviewEnvironment(
            string label,
            Color background,
            Color ambient,
            Color keyColor,
            Color fillColor,
            float keyIntensity,
            float fillIntensity)
        {
            Label = label;
            Background = background;
            Ambient = ambient;
            KeyColor = keyColor;
            FillColor = fillColor;
            KeyIntensity = keyIntensity;
            FillIntensity = fillIntensity;
        }

        public string Label { get; }
        public Color Background { get; }
        public Color Ambient { get; }
        public Color KeyColor { get; }
        public Color FillColor { get; }
        public float KeyIntensity { get; }
        public float FillIntensity { get; }
    }
}
