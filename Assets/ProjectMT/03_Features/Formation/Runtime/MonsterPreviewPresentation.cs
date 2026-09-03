using System;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ProjectMT.Features.Formation
{
    // UI 전용: 전투 Runtime 없이 정식 모델의 Idle만 재생한다.
    public sealed class MonsterPreviewPresentation : IDisposable
    {
        private PlayableGraph graph;
        private AnimationClipPlayable idlePlayable;
        private float idleLength;
        private float idleSpeed;
        private float idleTime;
        private GameObject model;
        private Vector3 authoredModelScale;

        public GameObject Root { get; private set; }
        public string MonsterId { get; private set; }
        public bool HasIdle => graph.IsValid();

        public static bool CanShow(MonsterDefinition definition) => definition != null &&
            (definition.RuntimeAssetSet?.VisualAdapterPrefab != null || definition.PreviewPrefab != null);

        public static MonsterPreviewPresentation Create(
            MonsterDefinition definition, Transform parent, int layer, float yaw, float phase = 0f)
        {
            if (!CanShow(definition) || parent == null) return null;
            var preview = new MonsterPreviewPresentation { MonsterId = definition.MonsterId };
            try
            {
                preview.Root = new GameObject($"Preview_{definition.MonsterId}");
                preview.Root.SetActive(false); // Instantiate/OnEnable 전에 전투·음향·물리를 차단
                preview.Root.transform.SetParent(parent, false);
                preview.Root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                var assets = definition.RuntimeAssetSet;
                var prefab = assets?.VisualAdapterPrefab != null ? assets.VisualAdapterPrefab : definition.PreviewPrefab;
                preview.model = UnityEngine.Object.Instantiate(prefab, preview.Root.transform, false);
                preview.authoredModelScale = preview.model.transform.localScale;
                preview.model.transform.localPosition = Vector3.zero;
                preview.model.transform.localRotation = Quaternion.identity;
                foreach (var child in preview.Root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
                foreach (var behaviour in preview.model.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
                foreach (var collider in preview.model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var body in preview.model.GetComponentsInChildren<Rigidbody>(true))
                {
                    body.detectCollisions = false;
                    body.isKinematic = true;
                }
                foreach (var particles in preview.model.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = particles.main;
                    main.playOnAwake = false;
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                foreach (var renderer in preview.model.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) renderer.enabled = false;
                    if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
                }
                var animatorPath = assets?.BodyProfile?.AnimatorPath;
                var animator = !string.IsNullOrEmpty(animatorPath)
                    ? preview.model.transform.Find(animatorPath)?.GetComponent<Animator>()
                    : preview.model.GetComponentInChildren<Animator>(true);
                var idle = assets?.MotionProfile?.Idle;
                if (animator != null && idle?.Clip != null)
                {
                    animator.runtimeAnimatorController = null; // 공격 전이/StateMachineBehaviour도 실행하지 않음
                    animator.applyRootMotion = false;
                    animator.fireEvents = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                    animator.enabled = true;
                    preview.idleLength = Mathf.Max(0.001f, idle.Clip.length);
                    preview.idleSpeed = idle.PlaybackSpeed;
                    preview.idleTime = Mathf.Repeat(phase, 1f) * preview.idleLength;
                    preview.graph = PlayableGraph.Create($"MonsterPreviewIdle_{definition.MonsterId}");
                    preview.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    preview.idlePlayable = AnimationClipPlayable.Create(preview.graph, idle.Clip);
                    preview.idlePlayable.SetApplyFootIK(false);
                    preview.idlePlayable.SetApplyPlayableIK(false);
                    var output = AnimationPlayableOutput.Create(preview.graph, "Idle", animator);
                    output.SetSourcePlayable(preview.idlePlayable);
                    preview.graph.Play();
                }
                preview.model.SetActive(true);
                preview.Root.SetActive(true);
                preview.Tick(0f); // T-pose가 아닌 실제 Idle 자세로 bounds 측정
                foreach (var feedback in preview.model.GetComponentsInChildren<UnitVisualFeedback>(true)) feedback.SetTint(definition.VisualTint);
                return preview;
            }
            catch { preview.Dispose(); throw; }
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!HasIdle || Root == null || !Root.activeInHierarchy) return;
            idleTime = Mathf.Repeat(idleTime + Mathf.Max(0f, unscaledDeltaTime) * idleSpeed, idleLength);
            idlePlayable.SetTime(idleTime);
            graph.Evaluate(0f);
        }

        // 실제 Mesh만 측정: 비활성 부품/VFX와 부풀려진 Skinned localBounds는 제외.
        public bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            if (Root == null) return false;
            var found = false;
            Mesh baked = null;
            try
            {
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    Bounds local;
                    if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                    {
                        baked ??= new Mesh { name = "MonsterPreviewBounds", hideFlags = HideFlags.HideAndDontSave };
                        skin.BakeMesh(baked, true); // TransformPoint가 scale을 적용하므로 Bake 단계의 scale은 보정
                        local = baked.bounds;
                    }
                    else if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
                        local = filter.sharedMesh.bounds;
                    else continue;
                    for (var corner = 0; corner < 8; corner++)
                    {
                        var point = renderer.transform.TransformPoint(local.center + Vector3.Scale(local.extents, Corner(corner)));
                        if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                        else bounds.Encapsulate(point);
                    }
                }
            }
            finally { DestroyOwned(baked); }
            return found && bounds.size.sqrMagnitude > 0.000001f;
        }

        public void UseAuthoredScale(Transform sizeReference)
        {
            if (model == null || Root == null || sizeReference == null) return;
            var referenceScale = sizeReference.lossyScale;
            var previewScale = Root.transform.lossyScale;
            model.transform.localScale = Vector3.Scale(authoredModelScale, new Vector3(
                Mathf.Abs(referenceScale.x) / Mathf.Max(0.000001f, Mathf.Abs(previewScale.x)),
                Mathf.Abs(referenceScale.y) / Mathf.Max(0.000001f, Mathf.Abs(previewScale.y)),
                Mathf.Abs(referenceScale.z) / Mathf.Max(0.000001f, Mathf.Abs(previewScale.z)))); // 구형 Anchor 배율만 상쇄, 모델별 VisualScale은 그대로 보존
            Tick(0f);
            if (TryGetBounds(out var bounds))
            {
                var anchor = Root.transform.position;
                model.transform.position += new Vector3(anchor.x - bounds.center.x, anchor.y - bounds.min.y, anchor.z - bounds.center.z);
            }
        }

        public void FitToSlot(float targetHeight, float maxWidth, float maxDepth, Transform sizeReference = null)
        {
            if (!TryGetBounds(out var bounds)) return;
            // 키를 먼저 맞추고 넓은 날개/긴 꼬리는 슬롯 겹침을 막는 한도만 적용.
            var parentScale = sizeReference != null ? sizeReference.lossyScale : Root.transform.lossyScale;
            var scale = Mathf.Min(targetHeight * Mathf.Abs(parentScale.y) / Mathf.Max(0.000001f, bounds.size.y),
                maxWidth * Mathf.Abs(parentScale.x) / Mathf.Max(0.000001f, bounds.size.x),
                maxDepth * Mathf.Abs(parentScale.z) / Mathf.Max(0.000001f, bounds.size.z));
            model.transform.localScale *= scale;
            Tick(0f); // 변경된 Transform으로 Skinned bone matrix를 먼저 갱신
            if (TryGetBounds(out bounds))
            {
                var anchor = Root.transform.position;
                model.transform.position += new Vector3(anchor.x - bounds.center.x, anchor.y - bounds.min.y, anchor.z - bounds.center.z);
            }
        }

        public void FitCamera(Camera camera)
        {
            if (camera == null || !TryGetBounds(out var bounds)) return;
            var rotation = Quaternion.Euler(8f, 0f, 0f);
            var inverse = Quaternion.Inverse(rotation);
            var verticalTan = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var horizontalTan = verticalTan * Mathf.Max(0.1f, camera.aspect);
            var spatialScale = Mathf.Max(0.0001f, Mathf.Abs(Root.transform.lossyScale.y));
            var distance = 0.5f * spatialScale;
            for (var corner = 0; corner < 8; corner++)
            {
                var point = inverse * Vector3.Scale(bounds.extents, Corner(corner));
                distance = Mathf.Max(distance, Mathf.Max(Mathf.Abs(point.x) / horizontalTan, Mathf.Abs(point.y) / verticalTan) * 1.18f - point.z);
            }
            camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * distance, rotation);
            camera.nearClipPlane = Mathf.Max(0.001f, 0.05f * spatialScale);
            camera.farClipPlane = Mathf.Max(20f * spatialScale, distance + bounds.size.magnitude * 2f);
        }

        private static Vector3 Corner(int index) => new Vector3(
            (index & 1) == 0 ? -1f : 1f, (index & 2) == 0 ? -1f : 1f, (index & 4) == 0 ? -1f : 1f);

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            if (Root != null) Root.SetActive(false);
            DestroyOwned(Root);
            Root = null;
            model = null;
        }

        internal static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
