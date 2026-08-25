using System;
using System.Collections.Generic;
using ProjectMT.Features.Expedition;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    internal enum MonsterPreviewPositionAxis
    {
        X,
        Y,
        Z
    }

    internal enum MonsterMakerPreviewAnchor
    {
        Root,
        AttackOrigin,
        HitCenter,
        Socket
    }

    internal static class MonsterPreviewPositionHandleUtility // 두 Maker Preview가 같은 좌표 변환 규칙을 사용
    {
        public static bool TryWorldToGuiPoint(Camera camera, Rect rect, Vector3 worldPosition, out Vector2 guiPoint)
        {
            guiPoint = Vector2.zero;
            if (camera == null || rect.width <= 1f || rect.height <= 1f)
            {
                return false;
            }

            var viewport = camera.WorldToViewportPoint(worldPosition);
            if (viewport.z <= 0f)
            {
                return false;
            }

            guiPoint = new Vector2(
                rect.x + viewport.x * rect.width,
                rect.y + (1f - viewport.y) * rect.height);
            return true;
        }

        public static bool TryGuiPointToHorizontalPlane(
            Camera camera,
            Rect rect,
            Vector2 guiPoint,
            float planeHeight,
            out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (camera == null || rect.width <= 1f || rect.height <= 1f)
            {
                return false;
            }

            var viewport = new Vector3(
                Mathf.Clamp01((guiPoint.x - rect.x) / rect.width),
                Mathf.Clamp01(1f - (guiPoint.y - rect.y) / rect.height),
                0f);
            var ray = camera.ViewportPointToRay(viewport);
            var plane = new Plane(Vector3.up, new Vector3(0f, planeHeight, 0f));
            if (!plane.Raycast(ray, out var distance) || distance < 0f)
            {
                return false;
            }

            worldPosition = ray.GetPoint(distance);
            return true;
        }

        public static Vector3 ApplyHeightDrag(Vector3 startValue, float mouseDeltaY, float unitsPerPixel = 0.01f)
        {
            startValue.y -= mouseDeltaY * Mathf.Max(0.0001f, unitsPerPixel);
            return startValue;
        }

    }

    internal sealed class MonsterMakerPreviewStage : IDisposable // 수동 Clip·Marker와 Runtime 평가기를 함께 사용
    {
        private const float CameraInteractionRenderScale = 0.6f;
        private const float AnimationPlaybackRenderScale = 0.7f;
        private const string CombatTargetPrefabPath =
            "Assets/ProjectMT/03_Features/Expedition/Prefabs/PF_Enemy_Peasant.prefab";
        private const string FloatingNumberPrefabPath =
            "Assets/ProjectMT/02_Shared/Combat/Prefabs/PF_FloatingNumber.prefab";
        private const string HitVfxPrefabPath =
            "Assets/ProjectMT/02_Shared/Combat/Prefabs/PF_SeedHitVfx.prefab";
        private const float CombatTargetMaxHealth = 1000000f;
        private const float CombatTargetMinimumDistance = 1.6f;
        private const float RangedCombatTargetMinimumDistance = 3f;
        private const float CombatTargetVisualGap = 0.45f;
        private const int CombatTargetAppearanceSeed = 91073;

        private readonly PrefabPreviewStage stage = new PrefabPreviewStage();
        private readonly List<PreviewVfx> activeVfx = new List<PreviewVfx>();
        private readonly List<PreviewFloatingNumber> activeFloatingNumbers = new List<PreviewFloatingNumber>();
        private readonly List<PreviewHitVfx> activeHitVfx = new List<PreviewHitVfx>();
        private readonly List<PreviewProjectile> activeProjectiles = new List<PreviewProjectile>();
        private readonly List<PendingPreviewHit> pendingHits = new List<PendingPreviewHit>();
        private readonly List<MonsterAttackAreaIndicator> activeHitAreas = new List<MonsterAttackAreaIndicator>();
        private readonly PreviewCombatFeedbackPlayer combatFeedbackPlayer;
        private MonsterMakerDraft draft;
        private AnimationClip currentClip;
        private MonsterMakerAttackDraft currentAttack;
        private GameObject animationSampleRoot;
        private MonsterAttackMarker[] markerBuffer = Array.Empty<MonsterAttackMarker>();
        private MonsterMakerMarkerDraft[] markerDraftBuffer = Array.Empty<MonsterMakerMarkerDraft>();
        private GameObject dummyTarget;
        private UnitActor dummyTargetActor;
        private HealthComponent dummyTargetHealth;
        private UnitVisualFeedback dummyTargetVisualFeedback;
        private Animator[] dummyTargetAnimators = Array.Empty<Animator>();
        private PendingFloatingNumber pendingFloatingNumber;
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
        private float previewClock;
        private float lastAppliedDamage;
        private int previewHitCount;
        private uint floatingNumberSequence;
        private string combatStatus = "표준 적 준비 전";
        private Texture lastRenderedTexture;
        private Vector2Int lastRenderedSize;
        private bool renderDirty = true;
        private bool cameraInteractionActive;
        private bool targetFeedbackActive;

        public MonsterMakerPreviewStage()
        {
            combatFeedbackPlayer = new PreviewCombatFeedbackPlayer(this);
            stage.SetView(115f, 10f, 1.2f); // 공격자와 표준 적이 겹치지 않는 타격 확인용 기본 시점
        }

        public Texture Render(Rect rect)
        {
            return RenderInternal(rect, false);
        }

        public Texture RenderAfterInput(Rect rect, bool forceRender)
        {
            return RenderInternal(rect, forceRender);
        }

        private Texture RenderInternal(Rect rect, bool forceRender)
        {
            var renderScale = cameraInteractionActive
                ? CameraInteractionRenderScale
                : playing
                    ? AnimationPlaybackRenderScale
                    : 1f;
            var renderSize = new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(rect.width * renderScale)),
                Mathf.Max(1, Mathf.RoundToInt(rect.height * renderScale)));
            var renderRect = new Rect(0f, 0f, renderSize.x, renderSize.y);
            var eventType = Event.current?.type;
            var renderEvent = Event.current == null ||
                              eventType == EventType.Layout ||
                              eventType == EventType.Repaint;
            var sizeChanged = renderSize != lastRenderedSize;
            if (forceRender ||
                (renderEvent && (renderDirty || lastRenderedTexture == null || sizeChanged)))
            {
                lastRenderedTexture = stage.Render(renderRect);
                lastRenderedSize = renderSize;
                renderDirty = false;
            }

            return lastRenderedTexture;
        }
        public bool IsPlaying => playing;
        public bool RequiresContinuousTick => playing || targetFeedbackActive || pendingFloatingNumber.Active ||
                                               activeVfx.Count > 0 || activeFloatingNumbers.Count > 0 ||
                                               activeHitVfx.Count > 0 || activeProjectiles.Count > 0 ||
                                               pendingHits.Count > 0 || activeHitAreas.Count > 0;
        public float NormalizedTime => currentClip == null || currentClip.length <= 0f
            ? 0f
            : Mathf.Clamp01(playbackTime / currentClip.length);
        public string CurrentClipName => currentClip == null ? "선택 없음" : currentClip.name;
        public int EnvironmentIndex => stage.EnvironmentIndex;
        public bool HasCombatTarget => dummyTargetActor != null && dummyTargetHealth != null;
        public string CombatTargetLabel => HasCombatTarget ? "표준 적 · 농부" : "표준 적 없음";
        public float CombatTargetCurrentHealth => dummyTargetHealth?.CurrentHealth ?? 0f;
        public float CombatTargetMaximumHealth => dummyTargetHealth?.MaxHealth ?? 0f;
        public float CombatTargetDistance => dummyTarget == null || stage.PreviewRoot == null
            ? 0f
            : Vector3.Distance(stage.PreviewRoot.transform.position, dummyTarget.transform.position);
        public float LastAppliedDamage => lastAppliedDamage;
        public int PreviewHitCount => previewHitCount;
        public int ActiveFloatingNumberCount => activeFloatingNumbers.Count + (pendingFloatingNumber.Active ? 1 : 0);
        public int ActiveHitVfxCount => activeHitVfx.Count;
        public int ActiveMarkerVfxCount => activeVfx.Count;
        public int ActiveProjectileCount => activeProjectiles.Count;
        public int ActiveHitAreaCount => activeHitAreas.Count;
        public string CombatStatus => combatStatus;
        public Camera Camera => stage.Camera;

        public bool TryGetWorldPoint(
            MonsterMakerPreviewAnchor anchor,
            string socketPath,
            Vector3 localPosition,
            out Vector3 worldPosition)
        {
            var anchorTransform = ResolvePositionAnchor(anchor, socketPath);
            if (anchorTransform == null)
            {
                worldPosition = Vector3.zero;
                return false;
            }

            worldPosition = anchorTransform.TransformPoint(localPosition);
            return true;
        }

        public bool TryGetLocalPoint(
            MonsterMakerPreviewAnchor anchor,
            string socketPath,
            Vector3 worldPosition,
            out Vector3 localPosition)
        {
            var anchorTransform = ResolvePositionAnchor(anchor, socketPath);
            if (anchorTransform == null)
            {
                localPosition = Vector3.zero;
                return false;
            }

            localPosition = anchorTransform.InverseTransformPoint(worldPosition);
            return true;
        }

        public void ApplyDraftPositionOverrides()
        {
            if (draft == null || stage.PreviewRoot == null)
            {
                return;
            }

            var root = stage.PreviewRoot.transform;
            var visual = root.Find("Visual");
            if (visual != null)
            {
                visual.localPosition = draft.VisualLocalPosition + Vector3.up * draft.GroundOffset;
            }

            var attackOrigin = root.Find(draft.AttackOriginPath);
            if (attackOrigin != null)
            {
                attackOrigin.localPosition = draft.AttackOriginLocalPosition;
            }

            var hitCenter = root.Find(draft.HitCenterPath);
            if (hitCenter != null)
            {
                hitCenter.localPosition = draft.HitCenterLocalPosition;
            }

            renderDirty = true;
        }


        public void SetDraft(MonsterMakerDraft source)
        {
            lastRenderedTexture = null;
            lastRenderedSize = default;
            renderDirty = true;
            cameraInteractionActive = false;
            StopFeedback();
            DestroyCombatTarget();
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
            previewClock = 0f;
            lastAppliedDamage = 0f;
            previewHitCount = 0;
            floatingNumberSequence = 0u;
            combatStatus = "표준 적 준비 전";
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
            stage.RecalculateBounds(true);
            lastTickTime = EditorApplication.timeSinceStartup;
        }

        public void SetEnvironment(int index)
        {
            stage.SetEnvironment(index);
            renderDirty = true;
        }

        public void SetView(float yaw, float pitch, float distanceScale = 1f)
        {
            stage.SetView(yaw, pitch, distanceScale);
            renderDirty = true;
        }

        public bool ShowBasicAttackArea()
        {
            if (draft?.BasicAttackProfile == null || stage.PreviewRoot == null || !HasCombatTarget)
            {
                combatStatus = "기본공격 Profile 또는 표준 적이 없습니다";
                return false;
            }

            for (var index = activeHitAreas.Count - 1; index >= 0; index--)
            {
                var indicator = activeHitAreas[index];
                if (indicator != null)
                {
                    stage.RemoveAuxiliary(indicator.gameObject);
                }
            }
            activeHitAreas.Clear();

            ShowPreviewHitArea(draft.BasicAttackProfile, null);
            var visible = activeHitAreas.Count > 0;
            if (visible)
            {
                combatStatus = $"판정 표시 · [{draft.BasicAttackProfile.AttackId}] " +
                               $"{draft.BasicAttackProfile.Shape}";
                lastTickTime = EditorApplication.timeSinceStartup;
                renderDirty = true;
            }
            return visible;
        }

        public bool HandleInput(Rect rect, Event current)
        {
            if (current == null)
            {
                return false;
            }

            var insidePreview = rect.Contains(current.mousePosition);
            if (insidePreview && current.type == EventType.MouseDown && current.button == 1)
            {
                cameraInteractionActive = true;
            }

            var cameraChanged = insidePreview &&
                                ((current.type == EventType.MouseDrag && current.button == 1) ||
                                 current.type == EventType.ScrollWheel);
            var cameraInteractionEnded = cameraInteractionActive &&
                                         ((current.type == EventType.MouseUp && current.button == 1) ||
                                          current.type == EventType.MouseLeaveWindow);
            stage.HandleInput(rect, current);
            if (cameraInteractionEnded)
            {
                cameraInteractionActive = false;
            }

            if (cameraChanged || cameraInteractionEnded)
            {
                renderDirty = true;
            }

            return cameraChanged || cameraInteractionEnded;
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
                ResolveAttackStartFeedback(currentAttack),
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

            if (currentAttack != null)
            {
                StopFeedback();
                ResetCombatTarget();
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
            return Tick(deltaTime);
        }

        private bool Tick(float deltaTime)
        {
            deltaTime = Mathf.Clamp(deltaTime, 0f, 0.1f);
            previewClock += deltaTime;
            TickVfx(deltaTime);
            var animationChanged = playing && currentClip != null;
            if (animationChanged)
            {
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
            }

            var combatChanged = TickCombatPresentation(deltaTime);
            var changed = animationChanged || combatChanged || activeVfx.Count > 0;
            if (changed)
            {
                renderDirty = true;
            }
            return changed;
        }

        public void Dispose()
        {
            lastRenderedTexture = null;
            lastRenderedSize = default;
            renderDirty = true;
            cameraInteractionActive = false;
            StopFeedback();
            DestroyCombatTarget();
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
            if (attack != null)
            {
                ResetCombatTarget();
            }
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

            currentClip.SampleAnimation(
                animationSampleRoot != null ? animationSampleRoot : stage.PreviewRoot,
                playbackTime);

            if (previewMotionRoot != null)
            {
                previewMotionRoot.localPosition = previewBasePosition;
                previewMotionRoot.localRotation = previewBaseRotation;
                previewMotionRoot.localScale = previewBaseScale;
            }

            renderDirty = true;
        }

        private void HandleMarkerPassed(int index, MonsterAttackMarker marker)
        {
            if (draft == null || currentAttack == null || index < 0 || index >= markerDraftBuffer.Length)
            {
                return;
            }

            var markerDraft = markerDraftBuffer[index];
            var damage = Mathf.Max(0f, draft.AttackPower) * Mathf.Max(0f, marker.PowerRatio);
            var profile = draft.BasicAttackProfile;
            var socket = ResolvePreviewSocket(markerDraft.SocketOverride);
            var origin = socket == null ? stage.PreviewRoot.transform.position : socket.position;
            var targetPosition = ResolveCombatTargetHitPoint();
            var forward = targetPosition - origin;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.0001f
                ? stage.PreviewRoot.transform.forward
                : forward.normalized;
            var attackRotation = Quaternion.LookRotation(forward, Vector3.up);
            PlayProfileFeedbackAt(profile?.LaunchFeedback, origin, attackRotation);

            var markerFeedback = markerDraft.Feedback?.HasAny == true
                ? markerDraft.Feedback
                : null;
            var profileImpactFeedback = markerFeedback == null &&
                                        profile?.ImpactFeedback?.HasAnyFeedback == true
                ? profile.ImpactFeedback
                : null;
            var fallbackFeedback = markerFeedback ??
                                   (profileImpactFeedback == null ? draft.AttackMarkerFeedback : null);
            if (profile != null)
            {
                ShowPreviewHitArea(profile, markerDraft);
            }

            var usesProjectile = profile != null
                ? profile.UsesProjectileVisual
                : draft.CombatType == MonsterCombatType.Ranged &&
                  draft.RangedDeliveryMode == MonsterRangedDeliveryMode.Projectile;
            if (usesProjectile)
            {
                SpawnPreviewProjectile(
                    markerDraft,
                    damage,
                    fallbackFeedback,
                    profileImpactFeedback,
                    profile);
                return;
            }

            var hitCount = profile?.HitCount ?? 1;
            for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                var hitDamage = damage * (profile?.ResolveDamageRatio(hitIndex) ?? 1f);
                var playImpactFeedback = hitIndex == 0 || profile?.RepeatImpactFeedback != false;
                if (hitIndex == 0)
                {
                    if (ApplyPreviewDamage(hitDamage) && playImpactFeedback)
                    {
                        PlayResolvedImpactFeedback(
                            fallbackFeedback,
                            profileImpactFeedback,
                            ResolvePreviewImpactPosition(profile, origin),
                            attackRotation);
                    }
                    continue;
                }

                pendingHits.Add(new PendingPreviewHit(
                    previewClock + hitIndex * (profile?.RepeatHitInterval ?? 0.08f),
                    hitDamage,
                    fallbackFeedback,
                    profileImpactFeedback,
                    profile,
                    origin,
                    attackRotation,
                    playImpactFeedback));
            }
        }

        private void ShowPreviewHitArea(
            MonsterBasicAttackProfile profile,
            MonsterMakerMarkerDraft markerDraft)
        {
            if (profile == null || stage.PreviewRoot == null || !HasCombatTarget)
            {
                return;
            }

            var socket = ResolvePreviewSocket(markerDraft?.SocketOverride);
            var origin = socket == null ? stage.PreviewRoot.transform.position : socket.position;
            var target = ResolveCombatTargetHitPoint();
            var forward = target - origin;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = stage.PreviewRoot.transform.forward;
            }

            var indicator = MonsterAttackAreaIndicator.Create(
                null,
                profile,
                origin,
                forward,
                target,
                draft.AttackRange,
                new Color(0.1f, 0.9f, 1f, 0.78f),
                false);
            if (indicator == null)
            {
                return;
            }

            stage.AddAuxiliary(indicator.gameObject);
            activeHitAreas.Add(indicator);
        }

        private void PlayFeedback(MonsterMakerFeedbackDraft feedback, string socketPath)
        {
            if (feedback == null || stage.PreviewRoot == null)
            {
                return;
            }

            var socket = ResolvePreviewSocket(socketPath);
            var position = socket == null ? stage.PreviewRoot.transform.position : socket.position;
            var rotation = socket == null ? Quaternion.identity : socket.rotation;
            PlayFeedbackAt(feedback, position, rotation);
        }

        private void PlayFeedbackAt(
            MonsterMakerFeedbackDraft feedback,
            Vector3 position,
            Quaternion rotation)
        {
            if (feedback == null)
            {
                return;
            }

            PlaySound(feedback.Sound);
            if (feedback.Sound == null && feedback.Sfx != null && feedback.Sfx.TrySelectClip(out var clip))
            {
                SfxEditorAudioPreview.Play(clip, 0, false, feedback.Sfx.SelectVolume());
            }

            if (feedback.VfxPrefab == null || stage.PreviewRoot == null)
            {
                return;
            }

            var instance = UnityEngine.Object.Instantiate(feedback.VfxPrefab);
            instance.name = "[Monster Marker VFX] " + feedback.VfxPrefab.name;
            position += rotation * feedback.LocalPosition;
            rotation *= Quaternion.Euler(feedback.LocalEulerAngles);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = instance.transform.localScale *
                                            feedback.Scale * Mathf.Max(0.01f, draft.VfxScale);
            stage.AddAuxiliary(instance);
            activeVfx.Add(new PreviewVfx(instance, feedback.VfxLifetime));
        }

        private void PlayProfileFeedbackAt(
            MonsterFeedbackCue feedback,
            Vector3 position,
            Quaternion rotation)
        {
            if (feedback == null || !feedback.HasAnyFeedback)
            {
                return;
            }

            if (feedback.Sfx != null && feedback.Sfx.TrySelectClip(out var clip) && clip != null)
            {
                SfxEditorAudioPreview.Play(clip, 0, false, feedback.Sfx.SelectVolume());
            }
            if (feedback.VfxPrefab == null || stage.PreviewRoot == null)
            {
                return;
            }

            var instance = UnityEngine.Object.Instantiate(feedback.VfxPrefab);
            instance.name = "[Basic Attack Profile VFX] " + feedback.VfxPrefab.name;
            position += rotation * feedback.LocalPosition;
            rotation *= feedback.LocalRotation;
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = feedback.VfxPrefab.transform.localScale *
                                            feedback.Scale * Mathf.Max(0.01f, draft.VfxScale);
            stage.AddAuxiliary(instance);
            activeVfx.Add(new PreviewVfx(instance, feedback.VfxLifetime));
        }

        private void PlayResolvedImpactFeedback(
            MonsterMakerFeedbackDraft draftFeedback,
            MonsterFeedbackCue profileFeedback,
            Vector3 position,
            Quaternion rotation)
        {
            if (draftFeedback?.HasAny == true)
            {
                PlayFeedbackAt(draftFeedback, position, rotation);
                return;
            }

            PlayProfileFeedbackAt(profileFeedback, position, rotation);
        }

        private Vector3 ResolvePreviewImpactPosition(MonsterBasicAttackProfile profile, Vector3 origin)
        {
            return profile != null && profile.Shape == MonsterBasicAttackShape.Circle &&
                   profile.Center == MonsterBasicAttackCenter.Source
                ? origin + Vector3.up * 0.4f
                : ResolveCombatTargetHitPoint();
        }

        private static void PlaySound(AudioClip sound)
        {
            if (sound != null)
            {
                SfxEditorAudioPreview.Play(sound, 0, false, 1f);
            }
        }

        private MonsterMakerFeedbackDraft ResolveAttackStartFeedback(MonsterMakerAttackDraft attack)
        {
            return attack?.AttackStartFeedback?.HasAny == true
                ? attack.AttackStartFeedback
                : draft?.AttackStartFeedback;
        }

        private void SpawnPreviewProjectile(
            MonsterMakerMarkerDraft markerDraft,
            float damage,
            MonsterMakerFeedbackDraft impactFeedback,
            MonsterFeedbackCue profileImpactFeedback,
            MonsterBasicAttackProfile profile)
        {
            var projectilePresentation = profile?.ProjectileFeedback;
            var projectileVisual = projectilePresentation?.VfxPrefab != null
                ? projectilePresentation.VfxPrefab
                : draft?.ProjectilePrefab;
            if (projectileVisual == null)
            {
                projectileVisual = AssetDatabase.LoadAssetAtPath<GameObject>(
                    MonsterMakerAssetWriter.DefaultProjectilePrefabPath);
            }
            if (!HasCombatTarget || projectileVisual == null || damage <= 0f)
            {
                combatStatus = damage <= 0f ? "공격력 0 · 피해 없음" : "원거리 투사체 또는 표준 적 없음";
                return;
            }

            var socket = ResolvePreviewSocket(markerDraft?.SocketOverride);
            var origin = socket == null ? stage.PreviewRoot.transform.position : socket.position;
            var targetPosition = ResolveCombatTargetHitPoint();
            var direction = targetPosition - origin;
            direction.y = 0f;
            direction = direction.sqrMagnitude < 0.0001f ? Vector3.forward : direction.normalized;
            var projectileCount = profile?.ProjectileCount ?? 1;
            var spawned = false;
            for (var index = 0; index < projectileCount; index++)
            {
                var spreadRatio = projectileCount <= 1
                    ? 0f
                    : index / (float)(projectileCount - 1) - 0.5f;
                var shotDirection = Quaternion.Euler(
                    0f,
                    spreadRatio * (profile?.ProjectileSpreadAngle ?? 0f),
                    0f) * direction;
                var rotation = Quaternion.LookRotation(shotDirection, Vector3.up);
                var spawnPosition = origin;
                if (projectilePresentation != null)
                {
                    spawnPosition += rotation * projectilePresentation.LocalPosition;
                    rotation *= projectilePresentation.LocalRotation;
                }
                var instance = UnityEngine.Object.Instantiate(projectileVisual);
                instance.name = "[Monster Preview Projectile] " + projectileVisual.name;
                instance.transform.SetPositionAndRotation(spawnPosition, rotation);
                if (projectilePresentation != null)
                {
                    instance.transform.localScale = projectileVisual.transform.localScale *
                                                    projectilePresentation.Scale *
                                                    Mathf.Max(0.01f, draft.VfxScale);
                }
                var runtimeActor = instance.GetComponent<MonsterProjectileActor>();
                if (runtimeActor != null)
                {
                    runtimeActor.enabled = false; // Preview가 Editor delta로 같은 이동을 진행
                }

                var basicRuntimeActor = instance.GetComponent<MonsterBasicAttackProjectileActor>();
                if (basicRuntimeActor != null)
                {
                    basicRuntimeActor.enabled = false;
                }

                stage.AddAuxiliary(instance);
                activeProjectiles.Add(new PreviewProjectile(
                    instance,
                    damage,
                    Mathf.Max(0.01f, draft.ResolvedProjectileSpeed),
                    Mathf.Max(0.01f, draft.ResolvedProjectileLifetime),
                    impactFeedback,
                    profileImpactFeedback,
                    profile,
                    spawnPosition,
                    targetPosition,
                    shotDirection,
                    index == projectileCount / 2));
                spawned = true;
            }
            if (spawned)
            {
                if (projectilePresentation?.Sfx != null &&
                    projectilePresentation.Sfx.TrySelectClip(out var projectileClip) &&
                    projectileClip != null)
                {
                    SfxEditorAudioPreview.Play(
                        projectileClip,
                        0,
                        false,
                        projectilePresentation.Sfx.SelectVolume());
                }
                else if (profile?.LaunchFeedback?.Sfx == null)
                {
                    PlaySound(draft.ProjectileLaunchSound);
                }
            }
            combatStatus = "투사체 이동 중";
        }

        private bool ApplyPreviewDamage(float damage)
        {
            if (!HasCombatTarget)
            {
                combatStatus = "표준 적을 준비하지 못했습니다";
                return false;
            }

            if (damage <= 0f)
            {
                combatStatus = "공격력 0 · 피해 없음";
                return false;
            }

            if (!dummyTargetHealth.IsAlive)
            {
                ResetCombatTarget();
            }

            var hitPoint = ResolveCombatTargetHitPoint();
            if (!dummyTargetHealth.ApplyDamage(new DamageRequest(null, damage, hitPoint)))
            {
                combatStatus = "피해 적용 실패";
                return false;
            }

            return true;
        }

        private Vector3 ResolveCombatTargetHitPoint()
        {
            if (dummyTarget == null)
            {
                return Vector3.zero;
            }

            return TryResolveRenderBounds(dummyTarget, out var bounds)
                ? bounds.center + Vector3.up * 0.05f
                : dummyTarget.transform.position + Vector3.up * 0.8f;
        }

        private void HandlePreviewHit(UnitActor target, DamageReport report)
        {
            target?.VisualFeedback?.PlayHit();
            targetFeedbackActive = true;
            lastAppliedDamage = report.AppliedDamage;
            previewHitCount++;
            combatStatus = $"타격 {previewHitCount}회 · 피해 {Mathf.RoundToInt(report.AppliedDamage):N0}";
            QueueFloatingNumber(
                report.Request.HitPoint,
                report.AppliedDamage,
                FloatingNumberStyle.EnemyDamage,
                target == null ? 0 : target.GetInstanceID());
            SpawnPreviewHitVfx(report.Request.HitPoint);
        }

        private void HandlePreviewDeath(UnitActor target, DamageReport report)
        {
            target?.VisualFeedback?.PlayDeath();
            targetFeedbackActive = true;
            combatStatus = $"표준 적 처치 · 피해 {Mathf.RoundToInt(report.AppliedDamage):N0}";
        }

        private void QueueFloatingNumber(
            Vector3 position,
            float amount,
            FloatingNumberStyle style,
            int mergeKey)
        {
            if (amount <= 0f)
            {
                return;
            }

            if (mergeKey == 0)
            {
                mergeKey = dummyTarget == null ? 1 : dummyTarget.GetInstanceID();
            }

            if (pendingFloatingNumber.Active && pendingFloatingNumber.MergeKey == mergeKey)
            {
                pendingFloatingNumber.Amount += amount;
                pendingFloatingNumber.Position = position;
                pendingFloatingNumber.ReleaseAt = previewClock + FloatingNumberPresenter.DefaultMergeWindow;
                if (style == FloatingNumberStyle.Critical ||
                    pendingFloatingNumber.Style != FloatingNumberStyle.Critical)
                {
                    pendingFloatingNumber.Style = style;
                }

                return;
            }

            pendingFloatingNumber = new PendingFloatingNumber
            {
                Active = true,
                MergeKey = mergeKey,
                Position = position,
                Amount = amount,
                Style = style,
                ReleaseAt = previewClock + FloatingNumberPresenter.DefaultMergeWindow
            };
        }

        private void FlushFloatingNumber()
        {
            if (!pendingFloatingNumber.Active || pendingFloatingNumber.ReleaseAt > previewClock)
            {
                return;
            }

            var request = pendingFloatingNumber;
            pendingFloatingNumber = default;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloatingNumberPrefabPath);
            if (prefab == null)
            {
                combatStatus = "데미지 플로팅 Prefab을 찾지 못했습니다";
                return;
            }

            var instance = UnityEngine.Object.Instantiate(prefab);
            var view = instance.GetComponent<FloatingNumberView>();
            if (view == null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
                combatStatus = "데미지 플로팅 View를 찾지 못했습니다";
                return;
            }

            floatingNumberSequence = unchecked(floatingNumberSequence + 1u);
            var signedDrift = FloatingNumberPresenter.ResolveHorizontalDrift(
                request.MergeKey,
                floatingNumberSequence,
                FloatingNumberPresenter.DefaultHorizontalDrift);
            var side = signedDrift < 0f ? -1f : 1f;
            var cameraRight = stage.Camera == null ? Vector3.right : stage.Camera.transform.right;
            instance.name = "[Monster Preview Damage] " +
                            FloatingNumberPresenter.FormatValue(request.Amount, request.Style);
            instance.transform.position = request.Position
                                          + Vector3.up * FloatingNumberPresenter.DefaultHeightOffset
                                          + cameraRight * (signedDrift * 0.12f);
            instance.SetActive(true); // Runtime Pool.Rent와 동일하게 비활성 Prefab을 재생 상태로 전환
            view.Play(
                null,
                FloatingNumberPresenter.FormatValue(request.Amount, request.Style),
                FloatingNumberPresenter.ResolveColor(request.Style),
                FloatingNumberPresenter.DefaultDisplayDuration,
                FloatingNumberPresenter.DefaultRiseDistance,
                signedDrift,
                FloatingNumberPresenter.DefaultArcHeight,
                FloatingNumberPresenter.DefaultStartTilt * side,
                request.Style == FloatingNumberStyle.Critical ? 1.25f : 1f,
                stage.Camera,
                null);
            view.GetComponent<TMPro.TMP_Text>()?.ForceMeshUpdate(true, true); // 격리 Scene에 넣기 전에 Mesh를 먼저 생성
            stage.AddAuxiliary(instance);
            activeFloatingNumbers.Add(new PreviewFloatingNumber(instance, view));
        }

        private void SpawnPreviewHitVfx(Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HitVfxPrefabPath);
            if (prefab == null)
            {
                return;
            }

            var instance = UnityEngine.Object.Instantiate(prefab);
            var view = instance.GetComponent<SeedFeedbackVfx>();
            if (view == null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
                return;
            }

            instance.name = "[Monster Preview Hit VFX]";
            instance.transform.SetPositionAndRotation(position, Quaternion.identity);
            stage.AddAuxiliary(instance);
            view.Play(null, new Color(1f, 0.88f, 0.35f), 0.22f, 0.25f);
            activeHitVfx.Add(new PreviewHitVfx(instance, view));
        }

        private bool TickCombatPresentation(float deltaTime)
        {
            if (playing || targetFeedbackActive || pendingFloatingNumber.Active ||
                activeFloatingNumbers.Count > 0 || activeHitVfx.Count > 0 || activeProjectiles.Count > 0 ||
                pendingHits.Count > 0 || activeHitAreas.Count > 0)
            {
                for (var index = 0; index < dummyTargetAnimators.Length; index++)
                {
                    var animator = dummyTargetAnimators[index];
                    if (animator != null && animator.enabled && animator.gameObject.activeInHierarchy &&
                        animator.runtimeAnimatorController != null)
                    {
                        animator.Update(deltaTime);
                    }
                }
            }

            TickPendingHits();
            TickPreviewProjectiles(deltaTime);
            FlushFloatingNumber();
            var targetPulseActive = targetFeedbackActive &&
                                    (dummyTargetVisualFeedback?.Tick(deltaTime) ?? false);
            targetFeedbackActive = targetPulseActive;

            for (var index = activeFloatingNumbers.Count - 1; index >= 0; index--)
            {
                var item = activeFloatingNumbers[index];
                var isPlaying = item.Instance != null && item.View != null && item.View.Tick(deltaTime);
                if (!isPlaying)
                {
                    if (item.Instance != null)
                    {
                        stage.RemoveAuxiliary(item.Instance);
                    }

                    activeFloatingNumbers.RemoveAt(index);
                    continue;
                }

                ApplyPreviewTextOrientation(item.Instance);
                item.View.GetComponent<TMPro.TMP_Text>()?.ForceMeshUpdate(true, true); // Editor Preview에는 TMP UpdateManager가 없음
            }

            for (var index = activeHitVfx.Count - 1; index >= 0; index--)
            {
                var item = activeHitVfx[index];
                if (item.Instance == null || item.View == null || !item.View.Tick(deltaTime))
                {
                    if (item.Instance != null)
                    {
                        stage.RemoveAuxiliary(item.Instance);
                    }

                    activeHitVfx.RemoveAt(index);
                }
            }

            for (var index = activeHitAreas.Count - 1; index >= 0; index--)
            {
                var indicator = activeHitAreas[index];
                if (indicator != null && indicator.Tick(deltaTime))
                {
                    continue;
                }

                if (indicator != null)
                {
                    stage.RemoveAuxiliary(indicator.gameObject);
                }
                activeHitAreas.RemoveAt(index);
            }

            return targetPulseActive || pendingFloatingNumber.Active || activeFloatingNumbers.Count > 0 ||
                   activeHitVfx.Count > 0 || activeProjectiles.Count > 0 || pendingHits.Count > 0 ||
                   activeHitAreas.Count > 0;
        }

        private void TickPendingHits()
        {
            for (var index = pendingHits.Count - 1; index >= 0; index--)
            {
                var pending = pendingHits[index];
                if (previewClock < pending.ApplyAt)
                {
                    continue;
                }

                if (ApplyPreviewDamage(pending.Damage) && pending.PlayFeedback)
                {
                    PlayResolvedImpactFeedback(
                        pending.DraftFeedback,
                        pending.ProfileFeedback,
                        ResolvePreviewImpactPosition(pending.Profile, pending.Origin),
                        pending.Rotation);
                }
                pendingHits.RemoveAt(index);
            }
        }

        private static void ApplyPreviewTextOrientation(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            // PreviewRenderUtility의 TMP 양면 렌더 방향을 읽기 가능한 정면으로 맞춥니다.
            instance.transform.rotation *= Quaternion.Euler(0f, 180f, 0f);
            var scale = instance.transform.localScale;
            scale.x = -Mathf.Abs(scale.x);
            instance.transform.localScale = scale;
        }

        private void TickPreviewProjectiles(float deltaTime)
        {
            for (var index = activeProjectiles.Count - 1; index >= 0; index--)
            {
                var projectile = activeProjectiles[index];
                if (projectile.Instance == null || !HasCombatTarget)
                {
                    RemovePreviewProjectile(index, projectile);
                    continue;
                }

                projectile.Elapsed += deltaTime;
                if (projectile.Elapsed >= projectile.Lifetime)
                {
                    combatStatus = "투사체 수명 종료 · 피해 없음";
                    RemovePreviewProjectile(index, projectile);
                    continue;
                }

                var travel = projectile.Profile?.ProjectileTravel ??
                             MonsterBasicAttackProjectileTravel.Homing;
                var previous = projectile.Instance.transform.position;
                if (travel == MonsterBasicAttackProjectileTravel.Homing)
                {
                    projectile.TargetPosition = ResolveCombatTargetHitPoint();
                    MovePreviewProjectile(projectile, projectile.TargetPosition, deltaTime);
                    if ((projectile.Instance.transform.position - projectile.TargetPosition).sqrMagnitude <= 0.04f)
                    {
                        ApplyPreviewProjectileDamage(projectile, 0, projectile.TargetPosition);
                        RemovePreviewProjectile(index, projectile);
                        continue;
                    }
                }
                else if (travel == MonsterBasicAttackProjectileTravel.Returning)
                {
                    var destination = projectile.Returning
                        ? projectile.Origin
                        : projectile.TargetPosition;
                    MovePreviewProjectile(projectile, destination, deltaTime);
                    if (projectile.Returning && !projectile.ReturnDamageApplied &&
                        Vector3.Distance(projectile.Instance.transform.position, projectile.TargetPosition) >= 0.2f)
                    {
                        projectile.ReturnDamageApplied = true;
                        ApplyPreviewProjectileDamage(projectile, 1, projectile.TargetPosition);
                    }

                    if ((projectile.Instance.transform.position - destination).sqrMagnitude <= 0.04f)
                    {
                        if (!projectile.Returning)
                        {
                            ApplyPreviewProjectileDamage(projectile, 0, projectile.TargetPosition);
                            projectile.Returning = true;
                        }
                        else
                        {
                            RemovePreviewProjectile(index, projectile);
                            continue;
                        }
                    }
                }
                else
                {
                    var step = projectile.Speed * deltaTime;
                    projectile.Instance.transform.position += projectile.Direction * step;
                    projectile.Instance.transform.rotation = Quaternion.LookRotation(projectile.Direction, Vector3.up);
                    projectile.Traveled += step;
                    if (!projectile.DamageApplied && projectile.CanDamage &&
                        HasPassedPoint(previous, projectile.Instance.transform.position, projectile.TargetPosition))
                    {
                        projectile.DamageApplied = true;
                        ApplyPreviewProjectileDamage(projectile, 0, projectile.TargetPosition);
                    }

                    var maxDistance = projectile.Profile?.ResolveRange(draft.AttackRange) ??
                                      Vector3.Distance(projectile.Origin, projectile.TargetPosition);
                    if (projectile.Traveled >= maxDistance)
                    {
                        RemovePreviewProjectile(index, projectile);
                        continue;
                    }
                }

                var particles = projectile.Instance.GetComponentsInChildren<ParticleSystem>(true);
                for (var particleIndex = 0; particleIndex < particles.Length; particleIndex++)
                {
                    particles[particleIndex].Simulate(projectile.Elapsed, true, true);
                }
            }
        }

        private static void MovePreviewProjectile(
            PreviewProjectile projectile,
            Vector3 destination,
            float deltaTime)
        {
            var direction = destination - projectile.Instance.transform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                projectile.Instance.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
            projectile.Instance.transform.position = Vector3.MoveTowards(
                projectile.Instance.transform.position,
                destination,
                projectile.Speed * deltaTime);
        }

        private void ApplyPreviewProjectileDamage(
            PreviewProjectile projectile,
            int passIndex,
            Vector3 position)
        {
            if (!projectile.CanDamage)
            {
                return;
            }

            var ratio = projectile.Profile?.ResolveDamageRatio(passIndex) ?? 1f;
            if (ApplyPreviewDamage(projectile.Damage * ratio))
            {
                PlayResolvedImpactFeedback(
                    projectile.ImpactFeedback,
                    projectile.ProfileImpactFeedback,
                    position,
                    projectile.Instance == null ? Quaternion.identity : projectile.Instance.transform.rotation);
            }
        }

        private static bool HasPassedPoint(Vector3 previous, Vector3 current, Vector3 point)
        {
            return Vector3.Dot(point - previous, point - current) <= 0f ||
                   (current - point).sqrMagnitude <= 0.09f;
        }

        private void RemovePreviewProjectile(int index, PreviewProjectile projectile)
        {
            if (projectile.Instance != null)
            {
                stage.RemoveAuxiliary(projectile.Instance);
            }

            activeProjectiles.RemoveAt(index);
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

        private Transform ResolvePositionAnchor(MonsterMakerPreviewAnchor anchor, string socketPath)
        {
            if (stage.PreviewRoot == null)
            {
                return null;
            }

            var root = stage.PreviewRoot.transform;
            return anchor switch
            {
                MonsterMakerPreviewAnchor.AttackOrigin =>
                    root.Find(draft?.AttackOriginPath ?? string.Empty) ?? root,
                MonsterMakerPreviewAnchor.HitCenter =>
                    root.Find(draft?.HitCenterPath ?? string.Empty) ?? root,
                MonsterMakerPreviewAnchor.Socket => ResolvePreviewSocket(socketPath),
                _ => root
            };
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
            DestroyCombatTarget();

            if (draft?.VendorPrefab == null || stage.PreviewRoot == null)
            {
                combatStatus = "몬스터 모델을 지정하면 표준 적이 나타납니다";
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombatTargetPrefabPath);
            if (prefab == null)
            {
                combatStatus = "표준 적 Prefab을 찾지 못했습니다";
                return;
            }

            dummyTarget = UnityEngine.Object.Instantiate(prefab);
            dummyTarget.name = "[Monster Preview Target] PF_Enemy_Peasant";
            var previewForward = stage.PreviewRoot.transform.forward;
            dummyTarget.transform.SetPositionAndRotation(
                stage.PreviewRoot.transform.position,
                Quaternion.LookRotation(-previewForward, stage.PreviewRoot.transform.up));

            var appearance = dummyTarget.GetComponent<ModularEnemyAppearance>();
            var appearanceReady = appearance != null && appearance.PrepareForSpawn(new UnitSpawnRequest(
                "monster_maker_target",
                default,
                UnitTeam.Enemy,
                false,
                false,
                appearanceSeed: CombatTargetAppearanceSeed));
            if (!appearanceReady)
            {
                UnityEngine.Object.DestroyImmediate(dummyTarget);
                dummyTarget = null;
                combatStatus = "표준 적 외형 조립에 실패했습니다";
                return;
            }

            var attackerExtent = ResolveDirectionalExtent(
                stage.PreviewRoot,
                stage.PreviewRoot.transform.position,
                previewForward);
            var targetExtent = ResolveDirectionalExtent(
                dummyTarget,
                dummyTarget.transform.position,
                -previewForward);
            var minimumDistance = draft.CombatType == MonsterCombatType.Ranged
                ? RangedCombatTargetMinimumDistance
                : CombatTargetMinimumDistance;
            var targetDistance = Mathf.Max(
                minimumDistance,
                draft.AttackRange,
                attackerExtent + targetExtent + CombatTargetVisualGap);
            dummyTarget.transform.position = stage.PreviewRoot.transform.position + previewForward * targetDistance;

            dummyTargetHealth = dummyTarget.GetComponent<HealthComponent>();
            dummyTargetVisualFeedback = dummyTarget.GetComponent<UnitVisualFeedback>();
            dummyTargetActor = dummyTarget.GetComponent<UnitActor>();
            if (dummyTargetHealth == null || dummyTargetVisualFeedback == null || dummyTargetActor == null)
            {
                UnityEngine.Object.DestroyImmediate(dummyTarget);
                dummyTarget = null;
                dummyTargetHealth = null;
                dummyTargetVisualFeedback = null;
                dummyTargetActor = null;
                combatStatus = "표준 적 전투 부품이 불완전합니다";
                return;
            }

            dummyTargetVisualFeedback.RefreshRenderers();
            dummyTargetActor.EditorConfigureReferences(dummyTargetHealth, dummyTargetVisualFeedback);
            var targetStats = new UnitStatsSnapshot
            {
                maxHealth = CombatTargetMaxHealth,
                attackInterval = 1f
            };
            dummyTargetActor.Initialize(
                new UnitSpawnRequest(
                    "monster_maker_target",
                    targetStats,
                    UnitTeam.Enemy,
                    false,
                    false,
                    appearanceSeed: CombatTargetAppearanceSeed),
                null,
                combatFeedbackPlayer);
            dummyTargetAnimators = dummyTarget.GetComponentsInChildren<Animator>(true);
            stage.AddAuxiliary(dummyTarget);
            combatStatus = "공격 버튼을 눌러 실제 타격을 확인하세요";
        }

        private static float ResolveDirectionalExtent(
            GameObject root,
            Vector3 origin,
            Vector3 direction)
        {
            if (root == null || direction.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            var normalizedDirection = direction.normalized;
            var absoluteDirection = new Vector3(
                Mathf.Abs(normalizedDirection.x),
                Mathf.Abs(normalizedDirection.y),
                Mathf.Abs(normalizedDirection.z));
            var extent = 0f;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                var bounds = renderer.bounds;
                var centerProjection = Vector3.Dot(bounds.center - origin, normalizedDirection);
                var radiusProjection = Vector3.Dot(bounds.extents, absoluteDirection);
                extent = Mathf.Max(extent, centerProjection + radiusProjection);
            }

            return extent;
        }

        private static bool TryResolveRenderBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
            {
                return false;
            }

            var hasBounds = false;
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

            return hasBounds;
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
            ClearCombatPresentation();
        }

        private void ResetCombatTarget()
        {
            ClearCombatPresentation();
            lastAppliedDamage = 0f;
            previewHitCount = 0;
            if (!HasCombatTarget)
            {
                return;
            }

            dummyTargetHealth.Initialize(CombatTargetMaxHealth);
            dummyTargetVisualFeedback.RefreshRenderers();
            combatStatus = "공격 재생 중 · Marker 대기";
        }

        private void ClearCombatPresentation()
        {
            targetFeedbackActive = false;
            pendingFloatingNumber = default;
            pendingHits.Clear();
            for (var index = activeFloatingNumbers.Count - 1; index >= 0; index--)
            {
                if (activeFloatingNumbers[index].Instance != null)
                {
                    stage.RemoveAuxiliary(activeFloatingNumbers[index].Instance);
                }
            }

            activeFloatingNumbers.Clear();
            for (var index = activeHitVfx.Count - 1; index >= 0; index--)
            {
                if (activeHitVfx[index].Instance != null)
                {
                    stage.RemoveAuxiliary(activeHitVfx[index].Instance);
                }
            }

            activeHitVfx.Clear();
            for (var index = activeProjectiles.Count - 1; index >= 0; index--)
            {
                if (activeProjectiles[index].Instance != null)
                {
                    stage.RemoveAuxiliary(activeProjectiles[index].Instance);
                }
            }

            activeProjectiles.Clear();
            for (var index = activeHitAreas.Count - 1; index >= 0; index--)
            {
                var indicator = activeHitAreas[index];
                if (indicator != null)
                {
                    stage.RemoveAuxiliary(indicator.gameObject);
                }
            }

            activeHitAreas.Clear();
        }

        private void DestroyCombatTarget()
        {
            if (dummyTargetActor != null)
            {
                dummyTargetActor.Shutdown();
            }

            if (dummyTarget != null)
            {
                stage.RemoveAuxiliary(dummyTarget);
            }

            dummyTarget = null;
            dummyTargetActor = null;
            dummyTargetHealth = null;
            dummyTargetVisualFeedback = null;
            dummyTargetAnimators = Array.Empty<Animator>();
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

        private sealed class PreviewFloatingNumber
        {
            public PreviewFloatingNumber(GameObject instance, FloatingNumberView view)
            {
                Instance = instance;
                View = view;
            }

            public GameObject Instance { get; }
            public FloatingNumberView View { get; }
        }

        private sealed class PreviewHitVfx
        {
            public PreviewHitVfx(GameObject instance, SeedFeedbackVfx view)
            {
                Instance = instance;
                View = view;
            }

            public GameObject Instance { get; }
            public SeedFeedbackVfx View { get; }
        }

        private sealed class PreviewProjectile
        {
            public PreviewProjectile(
                GameObject instance,
                float damage,
                float speed,
                float lifetime,
                MonsterMakerFeedbackDraft impactFeedback,
                MonsterFeedbackCue profileImpactFeedback,
                MonsterBasicAttackProfile profile,
                Vector3 origin,
                Vector3 targetPosition,
                Vector3 direction,
                bool canDamage)
            {
                Instance = instance;
                Damage = damage;
                Speed = speed;
                Lifetime = lifetime;
                ImpactFeedback = impactFeedback;
                ProfileImpactFeedback = profileImpactFeedback;
                Profile = profile;
                Origin = origin;
                TargetPosition = targetPosition;
                Direction = direction.sqrMagnitude < 0.0001f ? Vector3.forward : direction.normalized;
                CanDamage = canDamage;
            }

            public GameObject Instance { get; }
            public float Damage { get; }
            public float Speed { get; }
            public float Lifetime { get; }
            public MonsterMakerFeedbackDraft ImpactFeedback { get; }
            public MonsterFeedbackCue ProfileImpactFeedback { get; }
            public MonsterBasicAttackProfile Profile { get; }
            public Vector3 Origin { get; }
            public Vector3 Direction { get; }
            public bool CanDamage { get; }
            public Vector3 TargetPosition { get; set; }
            public float Elapsed { get; set; }
            public float Traveled { get; set; }
            public bool Returning { get; set; }
            public bool DamageApplied { get; set; }
            public bool ReturnDamageApplied { get; set; }
        }

        private sealed class PendingPreviewHit
        {
            public PendingPreviewHit(
                float applyAt,
                float damage,
                MonsterMakerFeedbackDraft draftFeedback,
                MonsterFeedbackCue profileFeedback,
                MonsterBasicAttackProfile profile,
                Vector3 origin,
                Quaternion rotation,
                bool playFeedback)
            {
                ApplyAt = applyAt;
                Damage = damage;
                DraftFeedback = draftFeedback;
                ProfileFeedback = profileFeedback;
                Profile = profile;
                Origin = origin;
                Rotation = rotation;
                PlayFeedback = playFeedback;
            }

            public float ApplyAt { get; }
            public float Damage { get; }
            public MonsterMakerFeedbackDraft DraftFeedback { get; }
            public MonsterFeedbackCue ProfileFeedback { get; }
            public MonsterBasicAttackProfile Profile { get; }
            public Vector3 Origin { get; }
            public Quaternion Rotation { get; }
            public bool PlayFeedback { get; }
        }

        private struct PendingFloatingNumber
        {
            public bool Active;
            public int MergeKey;
            public Vector3 Position;
            public float Amount;
            public FloatingNumberStyle Style;
            public float ReleaseAt;
        }

        private sealed class PreviewCombatFeedbackPlayer : ICombatFeedbackPlayer
        {
            private readonly MonsterMakerPreviewStage owner;

            public PreviewCombatFeedbackPlayer(MonsterMakerPreviewStage owner)
            {
                this.owner = owner;
            }

            public void PlayHit(UnitActor target, DamageReport report)
            {
                owner.HandlePreviewHit(target, report);
            }

            public void PlayDeath(UnitActor target, DamageReport report)
            {
                owner.HandlePreviewDeath(target, report);
            }

            public void PlayClimax(Vector3 position, CombatClimaxStrength strength)
            {
            }

            public void PlayDamage(Vector3 position, float amount, FloatingNumberStyle style, int mergeKey)
            {
                owner.QueueFloatingNumber(position, amount, style, mergeKey);
            }
        }
    }
}
