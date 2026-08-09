using System;
using System.Collections.Generic;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    internal sealed class MonsterMakerPreviewStage : IDisposable // 수동 Clip·Marker와 Runtime 평가기를 함께 사용
    {
        private readonly PrefabPreviewStage stage = new PrefabPreviewStage();
        private readonly List<PreviewVfx> activeVfx = new List<PreviewVfx>();
        private MonsterMakerDraft draft;
        private AnimationClip currentClip;
        private MonsterMakerAttackDraft currentAttack;
        private GameObject animationSampleRoot;
        private MonsterAttackMarker[] markerBuffer = Array.Empty<MonsterAttackMarker>();
        private MonsterMakerMarkerDraft[] markerDraftBuffer = Array.Empty<MonsterMakerMarkerDraft>();
        private GameObject dummyTarget;
        private Material dummyMaterial;
        private Transform previewMotionRoot;
        private Vector3 previewBasePosition;
        private Quaternion previewBaseRotation = Quaternion.identity;
        private Vector3 previewBaseScale = Vector3.one;
        private double lastTickTime;
        private float playbackTime;
        private float playbackSpeed = 1f;
        private float previousNormalizedTime = -0.0001f;
        private int nextMarkerIndex;
        private int lastRandomAttackIndex = -1;
        private bool loop;
        private bool playing;

        public MonsterMakerPreviewStage()
        {
            stage.SetView(145f, 10f);
        }

        public Texture Render(Rect rect) => stage.Render(rect);
        public bool IsPlaying => playing;
        public float NormalizedTime => currentClip == null || currentClip.length <= 0f
            ? 0f
            : Mathf.Clamp01(playbackTime / currentClip.length);
        public string CurrentClipName => currentClip == null ? "선택 없음" : currentClip.name;
        public int EnvironmentIndex => stage.EnvironmentIndex;

        public void SetDraft(MonsterMakerDraft source)
        {
            StopFeedback();
            draft = source;
            currentClip = null;
            currentAttack = null;
            animationSampleRoot = null;
            previewMotionRoot = null;
            playing = false;
            playbackTime = 0f;
            markerBuffer = Array.Empty<MonsterAttackMarker>();
            markerDraftBuffer = Array.Empty<MonsterMakerMarkerDraft>();
            lastRandomAttackIndex = -1;
            stage.SetFramingScale(source?.PreviewScale ?? 1f);
            var template = CreatePreviewAdapterTemplate(source);
            try
            {
                stage.SetPrefab(template, instance =>
                {
                    var visual = instance.transform.Find("Visual");
                    var animatorPath = MonsterMakerValidator.ResolveAnimatorPath(source);
                    var animatorRoot = visual == null
                        ? null
                        : string.IsNullOrWhiteSpace(animatorPath)
                            ? visual
                            : visual.Find(animatorPath);
                    var animator = animatorRoot == null ? null : animatorRoot.GetComponent<Animator>();
                    if (animator != null)
                    {
                        animator.applyRootMotion = false;
                        animator.enabled = false;
                        animationSampleRoot = animator.gameObject;
                    }

                    previewMotionRoot = visual;
                    previewBasePosition = visual == null ? Vector3.zero : visual.localPosition;
                    previewBaseRotation = visual == null ? Quaternion.identity : visual.localRotation;
                    previewBaseScale = visual == null ? Vector3.one : visual.localScale;
                });
            }
            finally
            {
                if (template != null)
                {
                    UnityEngine.Object.DestroyImmediate(template);
                }
            }

            RebuildDummyTarget();
            stage.RecalculateBounds();
            lastTickTime = EditorApplication.timeSinceStartup;
        }

        public void SetEnvironment(int index)
        {
            stage.SetEnvironment(index);
        }

        public void SetView(float yaw, float pitch, float distanceScale = 1f)
        {
            stage.SetView(yaw, pitch, distanceScale);
        }

        public void HandleInput(Rect rect, Event current)
        {
            stage.HandleInput(rect, current);
        }

        public void PlayIdle()
        {
            BeginClip(draft?.IdleClip, draft?.IdleSpeed ?? 1f, true, null, null, null);
        }

        public void PlayMove()
        {
            BeginClip(draft?.MoveClip, draft?.MovePlaybackSpeed ?? 1f, true, null, null, null);
        }

        public void PlayAttack(int index)
        {
            if (draft == null || index < 0 || index >= draft.Attacks.Count)
            {
                return;
            }

            var attack = draft.Attacks[index];
            if (attack?.Clip == null)
            {
                return;
            }

            currentAttack = attack;

            var markers = new List<MonsterAttackMarker>();
            var markerDrafts = new List<MonsterMakerMarkerDraft>();
            for (var markerIndex = 0; markerIndex < currentAttack.Markers.Count; markerIndex++)
            {
                var source = currentAttack.Markers[markerIndex];
                if (source == null)
                {
                    continue;
                }

                var marker = new MonsterAttackMarker();
                marker.EditorConfigure(source.NormalizedTime, source.PowerRatio, null, source.SocketOverride);
                markers.Add(marker);
                markerDrafts.Add(source);
            }

            markerBuffer = markers.ToArray();
            markerDraftBuffer = markerDrafts.ToArray();

            BeginClip(
                currentAttack.Clip,
                MonsterAnimationDriver.ResolveAttackPlaybackSpeed(
                    currentAttack.Clip,
                    currentAttack.PlaybackSpeed,
                    1f / Mathf.Max(0.01f, draft.AttackSpeed)),
                false,
                currentAttack,
                draft.AttackStartFeedback,
                draft.AttackOriginPath);
        }

        public void PlayRandomAttack()
        {
            if (draft == null || draft.Attacks.Count == 0)
            {
                return;
            }

            var eligibleIndices = new List<int>();
            var totalWeight = 0f;
            for (var index = 0; index < draft.Attacks.Count; index++)
            {
                var attack = draft.Attacks[index];
                if (attack == null || attack.Clip == null ||
                    (index == lastRandomAttackIndex && attack.PreventImmediateRepeat && draft.Attacks.Count > 1))
                {
                    continue;
                }

                eligibleIndices.Add(index);
                totalWeight += attack.Weight;
            }

            if (eligibleIndices.Count == 0)
            {
                if (lastRandomAttackIndex >= 0 && lastRandomAttackIndex < draft.Attacks.Count &&
                    draft.Attacks[lastRandomAttackIndex]?.Clip != null)
                {
                    PlayAttack(lastRandomAttackIndex);
                    return;
                }

                for (var index = 0; index < draft.Attacks.Count; index++)
                {
                    if (draft.Attacks[index]?.Clip != null)
                    {
                        lastRandomAttackIndex = index;
                        PlayAttack(index);
                        return;
                    }
                }

                return;
            }

            if (totalWeight <= 0f)
            {
                var selected = eligibleIndices[UnityEngine.Random.Range(0, eligibleIndices.Count)];
                lastRandomAttackIndex = selected;
                PlayAttack(selected);
                return;
            }

            var choice = UnityEngine.Random.value * totalWeight;
            var fallbackIndex = eligibleIndices[eligibleIndices.Count - 1];
            for (var index = 0; index < draft.Attacks.Count; index++)
            {
                var attack = draft.Attacks[index];
                if (attack == null || attack.Clip == null ||
                    (index == lastRandomAttackIndex && attack.PreventImmediateRepeat && draft.Attacks.Count > 1))
                {
                    continue;
                }

                choice -= attack.Weight;
                if (choice > 0f)
                {
                    continue;
                }

                lastRandomAttackIndex = index;
                PlayAttack(index);
                return;
            }

            lastRandomAttackIndex = fallbackIndex;
            PlayAttack(fallbackIndex);
        }

        public void PlayDeath()
        {
            BeginClip(
                draft?.DeathClip,
                draft?.DeathSpeed ?? 1f,
                false,
                null,
                draft?.DeathFeedback,
                draft?.HitCenterPath);
        }

        public void TogglePause()
        {
            if (currentClip == null)
            {
                return;
            }

            playing = !playing;
            lastTickTime = EditorApplication.timeSinceStartup;
        }

        public void Restart()
        {
            if (currentClip == null)
            {
                return;
            }

            playbackTime = 0f;
            previousNormalizedTime = -0.0001f;
            nextMarkerIndex = 0;
            playing = true;
            SampleCurrentPose();
            lastTickTime = EditorApplication.timeSinceStartup;
        }

        public void Scrub(float normalizedTime)
        {
            if (currentClip == null)
            {
                return;
            }

            playing = false;
            playbackTime = Mathf.Clamp01(normalizedTime) * currentClip.length;
            previousNormalizedTime = NormalizedTime;
            nextMarkerIndex = 0;
            while (nextMarkerIndex < markerBuffer.Length &&
                   markerBuffer[nextMarkerIndex].NormalizedTime <= previousNormalizedTime)
            {
                nextMarkerIndex++;
            }

            SampleCurrentPose();
        }

        public bool Tick()
        {
            var now = EditorApplication.timeSinceStartup;
            var deltaTime = Mathf.Clamp((float)(now - lastTickTime), 0f, 0.1f);
            lastTickTime = now;
            TickVfx(deltaTime);
            if (!playing || currentClip == null)
            {
                return activeVfx.Count > 0;
            }

            playbackTime += deltaTime * playbackSpeed;
            var finished = playbackTime >= currentClip.length;
            if (finished && loop)
            {
                playbackTime %= Mathf.Max(0.01f, currentClip.length);
                previousNormalizedTime = -0.0001f;
                nextMarkerIndex = 0;
            }
            else if (finished)
            {
                playbackTime = currentClip.length;
                playing = false;
            }

            var normalizedTime = NormalizedTime;
            MonsterAttackMarkerEvaluator.EvaluatePassed(
                markerBuffer,
                previousNormalizedTime,
                normalizedTime,
                ref nextMarkerIndex,
                HandleMarkerPassed);
            previousNormalizedTime = normalizedTime;
            SampleCurrentPose();
            return true;
        }

        public void Dispose()
        {
            StopFeedback();
            DestroyDummyMaterial();
            stage.Dispose();
        }

        private void BeginClip(
            AnimationClip clip,
            float speed,
            bool shouldLoop,
            MonsterMakerAttackDraft attack,
            MonsterMakerFeedbackDraft startFeedback,
            string startSocketPath)
        {
            if (draft == null || stage.PreviewRoot == null || clip == null)
            {
                return;
            }

            StopFeedback();
            currentClip = clip;
            currentAttack = attack;
            playbackSpeed = Mathf.Max(0.01f, speed);
            loop = shouldLoop;
            playbackTime = 0f;
            previousNormalizedTime = -0.0001f;
            nextMarkerIndex = 0;
            if (attack == null)
            {
                markerBuffer = Array.Empty<MonsterAttackMarker>();
                markerDraftBuffer = Array.Empty<MonsterMakerMarkerDraft>();
            }

            playing = true;
            SampleCurrentPose();
            PlayFeedback(startFeedback, startSocketPath);
            lastTickTime = EditorApplication.timeSinceStartup;
        }

        private void SampleCurrentPose()
        {
            if (stage.PreviewRoot == null || currentClip == null)
            {
                return;
            }

            currentClip.SampleAnimation(animationSampleRoot != null ? animationSampleRoot : stage.PreviewRoot, playbackTime);
            if (previewMotionRoot != null)
            {
                previewMotionRoot.localPosition = previewBasePosition;
                previewMotionRoot.localRotation = previewBaseRotation;
                previewMotionRoot.localScale = previewBaseScale;
            }
        }

        private void HandleMarkerPassed(int index, MonsterAttackMarker marker)
        {
            if (draft == null || currentAttack == null || index < 0 || index >= markerDraftBuffer.Length)
            {
                return;
            }

            var markerDraft = markerDraftBuffer[index];
            var feedback = markerDraft.Feedback?.HasAny == true
                ? markerDraft.Feedback
                : draft.AttackMarkerFeedback;
            PlayFeedback(feedback, markerDraft.SocketOverride);
        }

        private void PlayFeedback(MonsterMakerFeedbackDraft feedback, string socketPath)
        {
            if (feedback == null)
            {
                return;
            }

            if (feedback.Sfx != null && feedback.Sfx.TrySelectClip(out var clip))
            {
                SfxEditorAudioPreview.Play(clip, 0, false, feedback.Sfx.SelectVolume());
            }

            if (feedback.VfxPrefab == null || stage.PreviewRoot == null)
            {
                return;
            }

            var instance = UnityEngine.Object.Instantiate(feedback.VfxPrefab);
            instance.name = "[Monster Marker VFX] " + feedback.VfxPrefab.name;
            var socket = ResolvePreviewSocket(socketPath);
            var position = socket.TransformPoint(feedback.LocalPosition);
            var rotation = socket.rotation * Quaternion.Euler(feedback.LocalEulerAngles);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = instance.transform.localScale *
                                            feedback.Scale * Mathf.Max(0.01f, draft.VfxScale);
            stage.AddAuxiliary(instance);
            activeVfx.Add(new PreviewVfx(instance, feedback.VfxLifetime));
        }

        private Transform ResolvePreviewSocket(string path)
        {
            if (stage.PreviewRoot == null)
            {
                return null;
            }

            var resolvedPath = string.IsNullOrWhiteSpace(path) ? draft?.AttackOriginPath : path;
            if (!string.IsNullOrWhiteSpace(resolvedPath))
            {
                var explicitSocket = stage.PreviewRoot.transform.Find(resolvedPath);
                if (explicitSocket != null)
                {
                    return explicitSocket;
                }
            }

            var fallbackPath = draft?.AttackOriginPath;
            return !string.IsNullOrWhiteSpace(fallbackPath)
                ? stage.PreviewRoot.transform.Find(fallbackPath) ?? stage.PreviewRoot.transform
                : stage.PreviewRoot.transform;
        }

        private static GameObject CreatePreviewAdapterTemplate(MonsterMakerDraft source)
        {
            if (source?.VendorPrefab == null)
            {
                return null;
            }

            var root = new GameObject("[Monster Preview Adapter Template]");
            try
            {
                var visual = UnityEngine.Object.Instantiate(source.VendorPrefab, root.transform);
                visual.name = "Visual";
                visual.transform.localPosition = source.VisualLocalPosition + Vector3.up * source.GroundOffset;
                visual.transform.localRotation = Quaternion.Euler(0f, source.FacingYawOffset, 0f);
                visual.transform.localScale = source.VisualScale;

                var attackOrigin = EnsurePreviewTransformPath(root.transform, source.AttackOriginPath);
                attackOrigin.localPosition = source.AttackOriginLocalPosition;
                var hitCenter = EnsurePreviewTransformPath(root.transform, source.HitCenterPath);
                hitCenter.localPosition = source.HitCenterLocalPosition;
                return root;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        private static Transform EnsurePreviewTransformPath(Transform root, string path)
        {
            var current = root;
            var parts = (path ?? string.Empty).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < parts.Length; index++)
            {
                var child = current.Find(parts[index]);
                if (child == null)
                {
                    child = new GameObject(parts[index]).transform;
                    child.SetParent(current, false);
                }

                current = child;
            }

            return current;
        }

        private void TickVfx(float deltaTime)
        {
            for (var index = activeVfx.Count - 1; index >= 0; index--)
            {
                var vfx = activeVfx[index];
                if (vfx.Instance == null)
                {
                    activeVfx.RemoveAt(index);
                    continue;
                }

                vfx.Elapsed += Mathf.Max(0f, deltaTime);
                var particles = vfx.Instance.GetComponentsInChildren<ParticleSystem>(true);
                for (var particleIndex = 0; particleIndex < particles.Length; particleIndex++)
                {
                    particles[particleIndex].Simulate(vfx.Elapsed, true, true);
                }

                if (vfx.Elapsed < vfx.Lifetime)
                {
                    continue;
                }

                stage.RemoveAuxiliary(vfx.Instance);
                activeVfx.RemoveAt(index);
            }
        }

        private void RebuildDummyTarget()
        {
            if (dummyTarget != null)
            {
                stage.RemoveAuxiliary(dummyTarget);
                dummyTarget = null;
            }

            DestroyDummyMaterial();

            if (draft?.VendorPrefab == null || stage.PreviewRoot == null)
            {
                return;
            }

            dummyTarget = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummyTarget.name = "[Monster Preview Target]";
            var collider = dummyTarget.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            dummyTarget.transform.position = stage.PreviewRoot.transform.TransformPoint(
                new Vector3(0.62f, Mathf.Max(0.2f, draft.HitCenterLocalPosition.y), draft.AttackRange));
            dummyTarget.transform.localScale = new Vector3(0.2f, 0.42f, 0.2f);
            var renderer = dummyTarget.GetComponent<Renderer>();
            if (renderer != null)
            {
                dummyMaterial = PrefabPreviewStage.CreateFloorMaterial(new Color(0.72f, 0.18f, 0.13f, 1f));
                renderer.sharedMaterial = dummyMaterial;
            }

            stage.AddAuxiliary(dummyTarget);
        }

        private void StopFeedback()
        {
            SfxEditorAudioPreview.StopAll();
            for (var index = activeVfx.Count - 1; index >= 0; index--)
            {
                if (activeVfx[index].Instance != null)
                {
                    stage.RemoveAuxiliary(activeVfx[index].Instance);
                }
            }

            activeVfx.Clear();
        }

        private void DestroyDummyMaterial()
        {
            if (dummyMaterial == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(dummyMaterial);
            dummyMaterial = null;
        }

        private sealed class PreviewVfx
        {
            public PreviewVfx(GameObject instance, float lifetime)
            {
                Instance = instance;
                Lifetime = Mathf.Max(0.01f, lifetime);
            }

            public GameObject Instance { get; }
            public float Lifetime { get; }
            public float Elapsed { get; set; }
        }
    }
}
