using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly List<PendingFloatingText> pendingFloatingTexts = new List<PendingFloatingText>();
        private readonly Dictionary<int, float> nextFloatingTextReleaseAt = new Dictionary<int, float>();
        private readonly Dictionary<int, RecentFloatingText> recentFloatingTexts =
            new Dictionary<int, RecentFloatingText>();
        private readonly List<PreviewHitVfx> activeHitVfx = new List<PreviewHitVfx>();
        private readonly List<PreviewProjectile> activeProjectiles = new List<PreviewProjectile>();
        private readonly List<PendingPreviewHit> pendingHits = new List<PendingPreviewHit>();
        private readonly List<PendingContractVfx> pendingContractVfx = new List<PendingContractVfx>();
        private readonly List<PendingActivePreviewEvent> pendingActiveEvents =
            new List<PendingActivePreviewEvent>();
        private readonly List<MonsterBasicAttackProfile> activePreviewAttackBlocks =
            new List<MonsterBasicAttackProfile>();
        private readonly List<PendingEffectPreviewGroup> pendingEffectPreviewGroups =
            new List<PendingEffectPreviewGroup>();
        private readonly List<MonsterAttackAreaIndicator> activeHitAreas = new List<MonsterAttackAreaIndicator>();
        private readonly List<PreviewTarget> dummyTargets = new List<PreviewTarget>();
        private readonly List<Vector3> activePreviewTargetPositions = new List<Vector3>();
        private readonly List<Quaternion> activePreviewTargetRotations = new List<Quaternion>();
        private readonly HashSet<string> basicAttackVfxClaims = new HashSet<string>();
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
        private float attackPoseHoldDuration;
        private float remainingAttackPoseHold;
        private float previousNormalizedTime = -0.0001f;
        private int nextMarkerIndex;
        private int basicAttackVfxSequence;
        private int activeAttackVfxSequence;
        private int lastRandomAttackIndex = -1;
        private bool loop;
        private bool playing;
        private float previewClock;
        private float lastAppliedDamage;
        private int previewHitCount;
        private int floatingTextUniqueKey = int.MinValue;
        private uint floatingNumberSequence;
        private string combatStatus = "표준 적 준비 전";
        private Texture lastRenderedTexture;
        private Vector2Int lastRenderedSize;
        private bool renderDirty = true;
        private bool cameraInteractionActive;
        private bool targetFeedbackActive;
        private bool activeSkillPreviewRunning;
        private Vector3 activePreviewOrigin;
        private Quaternion activePreviewRotation = Quaternion.identity;
        private Transform[] activeStepBlendTransforms = Array.Empty<Transform>();
        private Vector3[] activeStepBlendPositions = Array.Empty<Vector3>();
        private Quaternion[] activeStepBlendRotations = Array.Empty<Quaternion>();
        private Vector3[] activeStepBlendScales = Array.Empty<Vector3>();
        private float activeStepBlendDuration;
        private float activeStepBlendElapsed;

        public bool CanPlayActiveSkill =>
            draft?.UseActiveSkill == true &&
            draft.HasActiveProfile &&
            draft.Attacks.Count > 0 &&
            draft.Attacks[0]?.Clip != null &&
            (!draft.UseCustomActiveStepMotions ||
             draft.CurrentActivePresentations.Count > 0 &&
             draft.CurrentActivePresentations.All(presentation =>
                 presentation != null && presentation.MotionClip != null));
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
            var renderSize = new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(rect.width)),
                Mathf.Max(1, Mathf.RoundToInt(rect.height)));
            var renderRect = new Rect(0f, 0f, renderSize.x, renderSize.y);
            var eventType = Event.current?.type;
            var renderEvent = Event.current == null ||
                              eventType == EventType.Layout ||
                              eventType == EventType.Repaint;
            var pixelsPerPoint = Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
            var expectedTextureSize = new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(renderSize.x * pixelsPerPoint)),
                Mathf.Max(1, Mathf.RoundToInt(renderSize.y * pixelsPerPoint)));
            var sizeChanged = renderSize != lastRenderedSize ||
                              lastRenderedTexture != null &&
                              (lastRenderedTexture.width != expectedTextureSize.x ||
                               lastRenderedTexture.height != expectedTextureSize.y);
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
        public bool RequiresContinuousTick => playing || activeSkillPreviewRunning ||
                                               targetFeedbackActive || pendingFloatingNumber.Active ||
                                               pendingFloatingTexts.Count > 0 ||
                                               activeVfx.Count > 0 || activeFloatingNumbers.Count > 0 ||
                                               activeHitVfx.Count > 0 || activeProjectiles.Count > 0 ||
                                               pendingHits.Count > 0 || pendingContractVfx.Count > 0 ||
                                               activeHitAreas.Count > 0 ||
                                               pendingEffectPreviewGroups.Count > 0;
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
        public int ActiveFloatingNumberCount => activeFloatingNumbers.Count +
                                                (pendingFloatingNumber.Active ? 1 : 0) +
                                                pendingFloatingTexts.Count;
        public int ActiveHitVfxCount => activeHitVfx.Count;
        public int ActiveMarkerVfxCount => activeVfx.Count;
        public int PreviewPlaceholderVfxCount => activeVfx.Count(candidate => candidate.IsPlaceholder) +
                                                 activeProjectiles.Count(candidate => candidate.IsPlaceholder);
        public int ActiveProjectileCount => activeProjectiles.Count;
        public bool HasPresentationVfx => activeVfx.Count > 0 || activeProjectiles.Count > 0;
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
            attackPoseHoldDuration = 0f;
            remainingAttackPoseHold = 0f;
            markerBuffer = Array.Empty<MonsterAttackMarker>();
            markerDraftBuffer = Array.Empty<MonsterMakerMarkerDraft>();
            lastRandomAttackIndex = -1;
            previewClock = 0f;
            lastAppliedDamage = 0f;
            previewHitCount = 0;
            floatingTextUniqueKey = int.MinValue;
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

        public void PlayActiveSkill()
        {
            if (!CanPlayActiveSkill || stage.PreviewRoot == null) return;
            StopFeedback();
            ResetCombatTarget();
            pendingActiveEvents.Clear();
            pendingEffectPreviewGroups.Clear();
            activeSkillPreviewRunning = true;
            activePreviewOrigin = stage.PreviewRoot.transform.position;
            activePreviewRotation = stage.PreviewRoot.transform.rotation;
            if (draft.ActiveEffectProfile != null)
            {
                var presentation = draft.ActiveEffectPresentations.FirstOrDefault();
                draft.ResolveActiveStepMotion(
                    presentation,
                    out var effectClip,
                    out var effectSpeed,
                    out _,
                    out _);
                BeginClip(effectClip, effectSpeed, false, null, null, null);
                // BeginClip은 이전 Preview 피드백을 비우면서 액티브 실행 플래그도 내린다.
                // 효과 묶음을 예약하기 전에 다시 올려야 공용 시간축이 실제로 전진한다.
                activeSkillPreviewRunning = true;
                var executeAt = previewClock;
                for (var groupIndex = 0; groupIndex < draft.ActiveEffectProfile.Groups.Count; groupIndex++)
                {
                    var group = draft.ActiveEffectProfile.Groups[groupIndex];
                    if (group == null) continue;
                    executeAt += group.DelayAfterPrevious;
                    var groupPresentation = draft.ActiveEffectPresentations.FirstOrDefault(candidate =>
                        candidate != null && string.Equals(
                            candidate.StepId,
                            group.GroupId,
                            StringComparison.OrdinalIgnoreCase));
                    pendingEffectPreviewGroups.Add(new PendingEffectPreviewGroup(
                        executeAt,
                        group,
                        groupPresentation,
                        EffectPreviewPhase.Activation));
                    pendingEffectPreviewGroups.Add(new PendingEffectPreviewGroup(
                        executeAt + group.PresentationLifecycleStartDelay,
                        group,
                        groupPresentation,
                        EffectPreviewPhase.Applied));
                    if (group.UsesDurationPresentationLifecycle)
                    {
                        pendingEffectPreviewGroups.Add(new PendingEffectPreviewGroup(
                            executeAt + group.PresentationLifecycleStartDelay +
                            group.PresentationLifecycleDuration,
                            group,
                            groupPresentation,
                            EffectPreviewPhase.Expired));
                    }
                }
                pendingEffectPreviewGroups.Sort((left, right) => left.ExecuteAt.CompareTo(right.ExecuteAt));
                combatStatus =
                    $"효과형 액티브 준비 · [{GetEffectRoleLabel(draft.ActiveEffectProfile.Role)}] {draft.ActiveSkillName}";
                lastTickTime = EditorApplication.timeSinceStartup;
                return;
            }
            PositionCombatTargetsForActivePreview();
            var eventTime = previewClock;
            var previousLaunchTime = previewClock;
            var previousCompleteTime = previewClock;
            var previewRoot = stage.PreviewRoot.transform;
            var projectedRootPosition = activePreviewOrigin;
            var projectedRootRotation = activePreviewRotation;
            var attackOrigin = ResolvePreviewSocket(draft.AttackOriginPath) ?? previewRoot;
            var attackOriginLocalPosition = previewRoot.InverseTransformPoint(attackOrigin.position);
            var sequenceBase = unchecked(++activeAttackVfxSequence * 1000);
            UnitActor previousStepTarget = null;
            for (var stepIndex = 0; stepIndex < draft.ActiveAttackProfile.Steps.Count; stepIndex++)
            {
                var source = draft.ActiveAttackProfile.Steps[stepIndex];
                var step = source.Clone();
                var presentation = ResolveActivePresentation(step.StepId);
                var attackBlockProfile = ScriptableObject.CreateInstance<MonsterBasicAttackProfile>();
                attackBlockProfile.name = $"__ActivePreview_{step.StepId}";
                attackBlockProfile.hideFlags = HideFlags.HideAndDontSave;
                step.EditorCompileAttackBlock(attackBlockProfile);
                attackBlockProfile.EditorSetProjectileCarrierPrefab(
                    attackBlockProfile.UsesProjectileVisual
                        ? draft.ProjectilePrefab != null
                            ? draft.ProjectilePrefab
                            : AssetDatabase.LoadAssetAtPath<GameObject>(
                                MonsterMakerAssetWriter.DefaultProjectilePrefabPath)
                        : null);
                activePreviewAttackBlocks.Add(attackBlockProfile);
                var attackBlock = new ActivePreviewAttackBlock(
                    attackBlockProfile,
                    presentation,
                    step.StepId,
                    step.PlaybackSpeed,
                    sequenceBase + stepIndex + 1);
                draft.ResolveActiveStepMotion(
                    presentation,
                    out var motionClip,
                    out var motionPlaybackSpeed,
                    out _,
                    out var motionCommitNormalizedTime);
                var motionDuration = motionClip == null
                    ? 0f
                    : motionClip.length /
                      Mathf.Max(0.01f, motionPlaybackSpeed * step.PlaybackSpeed);
                var motionCommitDelay = motionDuration * motionCommitNormalizedTime;
                var stepTarget = ResolveActivePreviewStepTarget(step, previousStepTarget);
                if (stepTarget != null) previousStepTarget = stepTarget;
                var targetPoint = ResolveCombatTargetHitPoint(stepTarget);
                var projectedForward = targetPoint - projectedRootPosition;
                projectedForward.y = 0f;
                if (projectedForward.sqrMagnitude < 0.0001f)
                    projectedForward = projectedRootRotation * Vector3.forward;
                projectedForward = projectedForward.sqrMagnitude < 0.0001f
                    ? Vector3.forward
                    : projectedForward.normalized;
                projectedRootRotation = Quaternion.LookRotation(projectedForward, Vector3.up);
                var chainsFromLaunch = stepIndex > 0 &&
                                       step.StartMode == MonsterActiveStepStartMode.AfterPreviousLaunch;
                eventTime = chainsFromLaunch
                    ? previousLaunchTime
                    : previousCompleteTime + step.DelayAfterPrevious / step.PlaybackSpeed;
                pendingActiveEvents.Add(new PendingActivePreviewEvent(
                    eventTime, stepIndex, ActivePreviewEventType.Motion, step, presentation,
                    stepTarget, attackBlock));
                pendingActiveEvents.Add(new PendingActivePreviewEvent(
                    eventTime, stepIndex, ActivePreviewEventType.Telegraph, step, presentation,
                    stepTarget, attackBlock));
                var requiredBeforeLaunch = Mathf.Max(
                    step.TelegraphDelay / step.PlaybackSpeed,
                    motionCommitDelay);
                var launchTime = chainsFromLaunch
                    ? previousLaunchTime + Mathf.Max(
                        step.DelayAfterPrevious / step.PlaybackSpeed,
                        requiredBeforeLaunch)
                    : eventTime + requiredBeforeLaunch;
                var recipeLeadAttackOrigin = Matrix4x4.TRS(
                        projectedRootPosition,
                        projectedRootRotation,
                        previewRoot.lossyScale)
                    .MultiplyPoint3x4(attackOriginLocalPosition);
                var recipeLeadRotation = projectedRootRotation;
                if (attackBlockProfile.MovementModule == MonsterBasicAttackMovementModule.Dash)
                {
                    pendingActiveEvents.Add(new PendingActivePreviewEvent(
                        launchTime, stepIndex, ActivePreviewEventType.Dash, step, presentation,
                        stepTarget, attackBlock));
                    if (stepTarget != null)
                    {
                        projectedRootPosition = ResolveActivePreviewDashDestination(
                            projectedRootPosition,
                            stepTarget,
                            step.DashDistance);
                    }
                    projectedForward = targetPoint - projectedRootPosition;
                    projectedForward.y = 0f;
                    if (projectedForward.sqrMagnitude < 0.0001f)
                        projectedForward = projectedRootRotation * Vector3.forward;
                    projectedForward = projectedForward.sqrMagnitude < 0.0001f
                        ? Vector3.forward
                        : projectedForward.normalized;
                    projectedRootRotation = Quaternion.LookRotation(projectedForward, Vector3.up);
                }
                pendingActiveEvents.Add(new PendingActivePreviewEvent(
                    launchTime, stepIndex, ActivePreviewEventType.Launch, step, presentation,
                    stepTarget, attackBlock));
                var projectedAttackOrigin = Matrix4x4.TRS(
                        projectedRootPosition,
                        projectedRootRotation,
                        previewRoot.lossyScale)
                    .MultiplyPoint3x4(attackOriginLocalPosition);
                var targetDistance = stepTarget == null
                    ? 0f
                    : Vector3.Distance(projectedAttackOrigin, targetPoint);
                var projectileSpeed = attackBlockProfile.ProjectileSpeed * step.PlaybackSpeed;
                var usesTargetEndpoint =
                    attackBlockProfile.ProjectileTravel is MonsterBasicAttackProjectileTravel.Homing or
                        MonsterBasicAttackProjectileTravel.Returning ||
                    attackBlockProfile.CollisionModule == MonsterBasicAttackCollisionModule.StopOnFirstTarget;
                var impactDistance = attackBlockProfile.CollisionModule ==
                                     MonsterBasicAttackCollisionModule.AreaImpact && !usesTargetEndpoint
                    ? attackBlockProfile.ResolveRange(1f)
                    : targetDistance;
                var targetTravelTime = attackBlockProfile.UsesProjectileVisual && stepTarget != null
                    ? impactDistance / Mathf.Max(0.01f, projectileSpeed)
                    : 0f;
                var deliveryDistance = attackBlockProfile.UsesProjectileVisual
                    ? usesTargetEndpoint
                        ? targetDistance
                        : attackBlockProfile.ResolveRange(1f)
                    : 0f;
                var outboundTravelTime = deliveryDistance / Mathf.Max(0.01f, projectileSpeed);
                var deliveryTravelTime = attackBlockProfile.ProjectileTravel ==
                                         MonsterBasicAttackProjectileTravel.Returning
                    ? outboundTravelTime * 2f
                    : outboundTravelTime;
                var progressiveVictims = !attackBlockProfile.UsesProjectileVisual &&
                                         attackBlockProfile.SequenceModule ==
                                         MonsterBasicAttackSequenceModule.Single &&
                                         attackBlockProfile.Progression !=
                                         MonsterBasicAttackProgression.Simultaneous
                    ? ResolveActivePreviewVictims(
                        step,
                        stepTarget,
                        projectedRootPosition,
                        projectedForward)
                    : null;
                if (progressiveVictims != null && progressiveVictims.Count > 0)
                {
                    OrderActivePreviewVictimsForProgression(
                        attackBlockProfile,
                        progressiveVictims,
                        stepTarget,
                        projectedRootPosition,
                        projectedForward);
                    var minimum = ResolveActivePreviewProgressionAxis(
                        attackBlockProfile,
                        progressiveVictims[0],
                        stepTarget,
                        projectedRootPosition,
                        projectedForward);
                    var maximum = ResolveActivePreviewProgressionAxis(
                        attackBlockProfile,
                        progressiveVictims[progressiveVictims.Count - 1],
                        stepTarget,
                        projectedRootPosition,
                        projectedForward);
                    for (var victimIndex = 0; victimIndex < progressiveVictims.Count; victimIndex++)
                    {
                        var victim = progressiveVictims[victimIndex];
                        var axis = ResolveActivePreviewProgressionAxis(
                            attackBlockProfile,
                            victim,
                            stepTarget,
                            projectedRootPosition,
                            projectedForward);
                        var ratio = maximum > minimum
                            ? Mathf.InverseLerp(minimum, maximum, axis)
                            : 0f;
                        var hitTime = launchTime + targetTravelTime +
                                      ratio * attackBlockProfile.ProgressionDuration /
                                      Mathf.Max(0.05f, step.PlaybackSpeed);
                        pendingActiveEvents.Add(new PendingActivePreviewEvent(
                            hitTime,
                            stepIndex,
                            ActivePreviewEventType.Impact,
                            step,
                            presentation,
                            stepTarget,
                            attackBlock,
                            0,
                            victim,
                            victimIndex,
                            progressiveVictims.Count));
                    }
                }
                else
                {
                    var hitCount = attackBlockProfile.HitCount;
                    for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
                    {
                        var hitTime = ResolveActivePreviewHitTime(
                            attackBlockProfile,
                            hitIndex,
                            launchTime,
                            targetTravelTime,
                            deliveryTravelTime,
                            step.PlaybackSpeed);
                        pendingActiveEvents.Add(new PendingActivePreviewEvent(
                            hitTime, stepIndex, ActivePreviewEventType.Impact, step, presentation,
                            stepTarget, attackBlock, hitIndex));
                    }
                }
                if (attackBlockProfile.UsesProjectileVisual)
                {
                    pendingActiveEvents.Add(new PendingActivePreviewEvent(
                        launchTime + deliveryTravelTime,
                        stepIndex,
                        ActivePreviewEventType.DeliveryEnd,
                        step,
                        presentation,
                        stepTarget,
                        attackBlock));
                }
                ScheduleActiveRecipeLeadContractVfx(
                    attackBlock,
                    eventTime,
                    launchTime,
                    recipeLeadAttackOrigin,
                    targetPoint,
                    recipeLeadRotation,
                    stepTarget);
                var completeTime = Mathf.Max(
                    eventTime + motionDuration,
                    launchTime + attackBlockProfile.ResolveActivityDuration(step.PlaybackSpeed),
                    launchTime + deliveryTravelTime);
                pendingActiveEvents.Add(new PendingActivePreviewEvent(
                    completeTime, stepIndex, ActivePreviewEventType.Complete, step, presentation,
                    stepTarget, attackBlock));
                previousLaunchTime = launchTime;
                previousCompleteTime = Mathf.Max(previousCompleteTime, completeTime);
                eventTime = previousCompleteTime;
            }
            pendingActiveEvents.Sort(PendingActivePreviewEvent.Compare);
            combatStatus = $"액티브 준비 · {draft.ActiveSkillName}";
            lastTickTime = EditorApplication.timeSinceStartup;
        }

        private static string GetEffectRoleLabel(MonsterEffectActiveRole role) => role switch
        {
            MonsterEffectActiveRole.Support => "지원",
            MonsterEffectActiveRole.Guard => "수호",
            MonsterEffectActiveRole.Debuff => "디버프",
            _ => role.ToString()
        };
        private MonsterMakerActiveStepPresentationDraft ResolveActivePresentation(string stepId)
        {
            for (var index = 0; index < draft.ActiveAttackPresentations.Count; index++)
            {
                var candidate = draft.ActiveAttackPresentations[index];
                if (candidate != null && string.Equals(candidate.StepId, stepId,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
            return null;
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
                null,
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
            remainingAttackPoseHold = attackPoseHoldDuration;
            previousNormalizedTime = -0.0001f;
            nextMarkerIndex = 0;
            playing = true;
            SampleCurrentPose();
            if (currentAttack != null)
            {
                BeginBasicAttackContractPreview(draft?.AttackOriginPath);
            }
            lastTickTime = EditorApplication.timeSinceStartup;
        }

        private static float ResolveActivePreviewHitTime(
            MonsterBasicAttackProfile profile,
            int hitIndex,
            float launchTime,
            float targetTravelTime,
            float deliveryTravelTime,
            float stepPlaybackSpeed)
        {
            if (profile?.SequenceModule == MonsterBasicAttackSequenceModule.ReturnPasses)
            {
                // 기본공격 Preview와 동일하게 나가는 타격은 편도 도착, 돌아오는 타격은
                // 왕복 이동 완료 시점에 둬 두 계약이 한 프레임에 겹치지 않게 한다.
                return launchTime + (hitIndex <= 0 ? targetTravelTime : deliveryTravelTime);
            }
            return launchTime + targetTravelTime +
                   Mathf.Max(0, hitIndex) * profile.ResolveRepeatHitInterval(profile.BreathDuration) /
                   Mathf.Max(0.05f, stepPlaybackSpeed);
        }

        private static void OrderActivePreviewVictimsForProgression(
            MonsterBasicAttackProfile profile,
            List<UnitActor> victims,
            UnitActor primary,
            Vector3 origin,
            Vector3 forward)
        {
            victims.Sort((left, right) => ResolveActivePreviewProgressionAxis(
                    profile, left, primary, origin, forward)
                .CompareTo(ResolveActivePreviewProgressionAxis(
                    profile, right, primary, origin, forward)));
        }

        private static float ResolveActivePreviewProgressionAxis(
            MonsterBasicAttackProfile profile,
            UnitActor actor,
            UnitActor primary,
            Vector3 origin,
            Vector3 forward)
        {
            if (profile == null || actor == null) return 0f;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            var center = profile.Center switch
            {
                MonsterBasicAttackCenter.Source => origin,
                MonsterBasicAttackCenter.Forward => origin + forward * profile.ForwardOffset,
                _ => primary == null ? origin : primary.transform.position
            };
            var offset = actor.transform.position - center;
            offset.y = 0f;
            var side = Vector3.Cross(Vector3.up, forward);
            return profile.Progression switch
            {
                MonsterBasicAttackProgression.Forward => Vector3.Dot(offset, forward),
                MonsterBasicAttackProgression.LeftToRight => Vector3.Dot(offset, side),
                MonsterBasicAttackProgression.RightToLeft => -Vector3.Dot(offset, side),
                MonsterBasicAttackProgression.Outward => offset.magnitude,
                _ => 0f
            };
        }

        private UnitActor ResolveActivePreviewStepTarget(
            MonsterActiveAttackStep step,
            UnitActor previous)
        {
            var previousReady = previous != null && previous.IsAlive && previous.IsCombatReady
                ? previous
                : null;
            var initial = dummyTargetActor != null && dummyTargetActor.IsAlive &&
                          dummyTargetActor.IsCombatReady
                ? dummyTargetActor
                : null;
            if (previousReady == null) return initial ?? dummyTargets
                .Select(candidate => candidate.Actor)
                .FirstOrDefault(candidate => candidate != null && candidate.IsAlive && candidate.IsCombatReady);
            if (step.TargetPolicy == MonsterActiveTargetPolicy.SameTarget) return previousReady;
            for (var index = 0; index < dummyTargets.Count; index++)
            {
                var candidate = dummyTargets[index].Actor;
                if (candidate != null && candidate != previousReady && candidate.IsAlive &&
                    candidate.IsCombatReady)
                {
                    return candidate;
                }
            }
            return previousReady ?? initial;
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
            TickPendingContractVfx();
            TickVfx(deltaTime);
            var animationChanged = playing && currentClip != null;
            if (animationChanged)
            {
                activeStepBlendElapsed += deltaTime;
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
                    var overflow = Mathf.Max(
                        0f,
                        (playbackTime - currentClip.length) / Mathf.Max(0.01f, playbackSpeed));
                    playbackTime = currentClip.length;
                    remainingAttackPoseHold = Mathf.Max(0f, remainingAttackPoseHold - overflow);
                    finished = remainingAttackPoseHold <= 0.0001f;
                    if (finished)
                    {
                        playing = false;
                        // 액티브 Step은 자체 공격 블록이 MotionEnd를 소유한다. 여기서 기본공격
                        // Profile을 호출하면 액티브 종료 시 기본공격 VFX가 섞여 나온다.
                        if (!activeSkillPreviewRunning)
                        {
                            PlayContractVfx(
                                MonsterBasicAttackVfxEvent.MotionEnd,
                                draft?.BasicAttackProfile,
                                stage.PreviewRoot.transform.position,
                                ResolveCombatTargetHitPoint(),
                                ResolveCombatTargetHitPoint(),
                                stage.PreviewRoot.transform.rotation);
                            ReleaseContractVfx(MonsterBasicAttackVfxEndPolicy.MotionEnd, null);
                        }
                    }
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

            var activeSkillChanged = TickActiveSkillPreview();
            var combatChanged = TickCombatPresentation(deltaTime);
            var changed = animationChanged || activeSkillChanged || combatChanged || activeVfx.Count > 0;
            if (changed)
            {
                renderDirty = true;
            }
            return changed;
        }

        private bool TickActiveSkillPreview()
        {
            if (!activeSkillPreviewRunning) return false;
            var changed = false;
            while (pendingEffectPreviewGroups.Count > 0)
            {
                var pending = pendingEffectPreviewGroups[0];
                if (pending.ExecuteAt > previewClock) break;
                ExecuteEffectPreviewGroup(pending);
                pendingEffectPreviewGroups.RemoveAt(0);
                changed = true;
            }
            while (pendingActiveEvents.Count > 0)
            {
                var pending = pendingActiveEvents[0];
                if (pending.ExecuteAt > previewClock) break;
                ExecuteActivePreviewEvent(pending);
                pendingActiveEvents.RemoveAt(0);
                changed = true;
            }
            if (pendingActiveEvents.Count == 0 && pendingEffectPreviewGroups.Count == 0 &&
                pendingContractVfx.Count == 0 && activeVfx.Count == 0 &&
                activeProjectiles.Count == 0 && pendingHits.Count == 0)
            {
                activeSkillPreviewRunning = false;
                if (stage.PreviewRoot != null)
                    stage.PreviewRoot.transform.SetPositionAndRotation(
                        activePreviewOrigin,
                        activePreviewRotation);
                RestoreCombatTargetsAfterActivePreview();
                combatStatus = draft?.ActiveEffectProfile == null
                    ? $"액티브 완료 · {previewHitCount}회 타격"
                    : $"효과형 액티브 완료 · {draft.ActiveEffectProfile.Groups.Count}개 묶음";
            }
            return changed;
        }

        private void ExecuteEffectPreviewGroup(PendingEffectPreviewGroup pending)
        {
            if (pending?.Group == null || stage.PreviewRoot == null) return;
            var targets = ResolveEffectPreviewTargets(pending.Group);
            if (targets.Count == 0) return;
            switch (pending.Phase)
            {
                case EffectPreviewPhase.Activation:
                    PlayEffectPresentationEvent(pending.Group, pending.Presentation,
                        MonsterActivePresentationEvent.MotionStart, targets);
                    PlayEffectPresentationEvent(pending.Group, pending.Presentation,
                        MonsterActivePresentationEvent.Launch, targets);
                    combatStatus = $"{pending.Group.DisplayName} 발동";
                    break;
                case EffectPreviewPhase.Applied:
                    // 기존 저장 계약도 실제 효과 적용 시점으로 함께 이동시킨다.
                    PlayEffectPresentationEvent(pending.Group, pending.Presentation,
                        MonsterActivePresentationEvent.Impact, targets);
                    PlayEffectPresentationEvent(pending.Group, pending.Presentation,
                        MonsterActivePresentationEvent.AreaResolved, targets);
                    PlayEffectPresentationEvent(pending.Group, pending.Presentation,
                        MonsterActivePresentationEvent.EffectApplied, targets);
                    PlayEffectPresentationEvent(pending.Group, pending.Presentation,
                        MonsterActivePresentationEvent.StepEnd, targets);
                    ReleaseActivePresentationVfx(MonsterActivePresentationEndPolicy.StepEnd);
                    ReleaseActivePresentationVfx(MonsterActivePresentationEndPolicy.MotionEnd);
                    combatStatus = $"{pending.Group.DisplayName} 적용 · {targets.Count}명";
                    break;
                case EffectPreviewPhase.Expired:
                    PlayEffectPresentationEvent(pending.Group, pending.Presentation,
                        MonsterActivePresentationEvent.EffectExpired, targets);
                    combatStatus = $"{pending.Group.DisplayName} 효과 종료";
                    break;
            }
        }

        private void PlayEffectPresentationEvent(
            MonsterEffectActiveGroup group,
            MonsterMakerActiveStepPresentationDraft presentation,
            MonsterActivePresentationEvent timing,
            IReadOnlyList<Transform> targets)
        {
            if (group == null || presentation == null || stage.PreviewRoot == null) return;
            for (var index = 0; index < group.PresentationSlots.Count; index++)
            {
                var contract = group.PresentationSlots[index];
                if (contract == null || contract.Timing != timing) continue;
                var slot = presentation.ResolveSlot(contract.SlotId);
                if (slot?.Feedback == null) continue;
                var occurrenceCount = contract.Multiplicity switch
                {
                    MonsterActivePresentationMultiplicity.PerTargetHit => targets?.Count ?? 0,
                    MonsterActivePresentationMultiplicity.ContinuousUntilEnd
                        when MonsterEffectActiveVfxCompatibility.IsTargetAnchor(contract.Anchor) =>
                        targets?.Count ?? 0,
                    _ => targets == null || targets.Count == 0 ? 0 : 1
                };
                for (var occurrence = 0; occurrence < occurrenceCount; occurrence++)
                {
                    var target = contract.Multiplicity == MonsterActivePresentationMultiplicity.OncePerStep
                        ? targets[0]
                        : targets[Mathf.Clamp(occurrence, 0, targets.Count - 1)];
                    ResolveEffectPresentationPose(
                        contract,
                        target,
                        targets,
                        out var position,
                        out var rotation,
                        out var parent);
                    PlayActiveSlotFeedback(slot, contract, position, rotation, parent, position, 0f, 1f);
                }
            }
        }

        private void ResolveEffectPresentationPose(
            MonsterActivePresentationSlot contract,
            Transform target,
            IReadOnlyList<Transform> targets,
            out Vector3 position,
            out Quaternion rotation,
            out Transform parent)
        {
            var root = stage.PreviewRoot.transform;
            var attackOrigin = ResolvePreviewSocket(draft.AttackOriginPath) ?? root;
            var targetTransform = target == null ? root : target;
            var targetActor = targetTransform.GetComponent<UnitActor>();
            var hitCenter = targetActor?.AnimationDriver?.HitCenter;
            var areaCenter = ResolveEffectPreviewAreaCenter(targets, root.position);
            parent = contract.Anchor switch
            {
                MonsterActivePresentationAnchor.CasterRoot => root,
                MonsterActivePresentationAnchor.AttackOrigin or
                    MonsterActivePresentationAnchor.MarkerSocket or
                    MonsterActivePresentationAnchor.TrajectoryOrigin => attackOrigin,
                MonsterActivePresentationAnchor.TargetPoint or
                    MonsterActivePresentationAnchor.TargetRoot => targetTransform,
                MonsterActivePresentationAnchor.HitPoint => hitCenter ?? targetTransform,
                _ => null
            };
            position = contract.Anchor switch
            {
                MonsterActivePresentationAnchor.CasterRoot => root.position,
                MonsterActivePresentationAnchor.AttackOrigin or
                    MonsterActivePresentationAnchor.MarkerSocket or
                    MonsterActivePresentationAnchor.TrajectoryOrigin => attackOrigin.position,
                MonsterActivePresentationAnchor.HitPoint => hitCenter?.position ?? targetTransform.position,
                MonsterActivePresentationAnchor.AreaCenter => areaCenter,
                _ => targetTransform.position
            };
            var forward = position - root.position;
            forward.y = 0f;
            rotation = Quaternion.LookRotation(
                forward.sqrMagnitude < 0.0001f ? root.forward : forward.normalized,
                Vector3.up);
        }

        private List<Transform> ResolveEffectPreviewTargets(MonsterEffectActiveGroup group)
        {
            var result = new List<Transform>(3);
            if (group == null || stage.PreviewRoot == null) return result;
            var root = stage.PreviewRoot.transform;
            var maximum = Mathf.Clamp(group.MaxTargets, 1, 3);
            if (group.Target == MonsterSkillTargetType.Self ||
                group.Target is MonsterSkillTargetType.LowestHealthAlly or
                    MonsterSkillTargetType.HighestAttackAlly)
            {
                result.Add(root);
                return result;
            }

            if (group.Target is MonsterSkillTargetType.NearbyAllies or MonsterSkillTargetType.AllAllies)
            {
                if (group.IncludeCaster) result.Add(root);
                for (var index = 0; index < dummyTargets.Count && result.Count < maximum; index++)
                {
                    var actor = dummyTargets[index].Actor;
                    if (actor != null && actor.IsAlive) result.Add(actor.transform);
                }
                return result;
            }

            for (var index = 0; index < dummyTargets.Count && result.Count < maximum; index++)
            {
                var actor = dummyTargets[index].Actor;
                if (actor != null && actor.IsAlive) result.Add(actor.transform);
                if (group.Target != MonsterSkillTargetType.TargetAreaEnemies) break;
            }
            return result;
        }

        private static Vector3 ResolveEffectPreviewAreaCenter(
            IReadOnlyList<Transform> targets,
            Vector3 fallback)
        {
            if (targets == null || targets.Count == 0) return fallback;
            var total = Vector3.zero;
            var count = 0;
            for (var index = 0; index < targets.Count; index++)
            {
                if (targets[index] == null) continue;
                total += targets[index].position;
                count++;
            }
            return count > 0 ? total / count : fallback;
        }

        private void ScheduleActiveRecipeLeadContractVfx(
            ActivePreviewAttackBlock attackBlock,
            float motionStartTime,
            float recipeTime,
            Vector3 origin,
            Vector3 targetPoint,
            Quaternion rotation,
            UnitActor targetActor)
        {
            var profile = attackBlock?.Profile;
            var bindings = attackBlock?.Presentation?.AttackBlockBindings;
            if (profile == null || bindings == null) return;
            var areaCenter = ResolveAttackBlockAreaCenter(
                profile,
                origin,
                targetPoint,
                rotation * Vector3.forward);
            for (var index = 0; index < profile.VfxSlots.Count; index++)
            {
                var slot = profile.VfxSlots[index];
                if (slot == null || slot.EventType != MonsterBasicAttackVfxEvent.RecipeExecute ||
                    slot.IsDeliveryVisual ||
                    !TryResolvePreviewPresentation(
                        bindings,
                        profile.AttackId,
                        slot,
                        attackBlock.MotionId,
                        out var binding) ||
                    binding.State != MonsterBasicAttackVfxAssignmentState.Assigned)
                {
                    continue;
                }

                var timingOffset = slot.ClampTimingOffset(binding.EventTimingOffset) /
                                   attackBlock.PlaybackSpeed;
                var claim = $"{attackBlock.SequenceId}|{slot.SlotId}|vfx|once";
                if (timingOffset >= 0f || !basicAttackVfxClaims.Add(claim)) continue;
                pendingContractVfx.Add(new PendingContractVfx(
                    Mathf.Max(motionStartTime, recipeTime + timingOffset),
                    slot,
                    binding,
                    origin,
                    targetPoint,
                    areaCenter,
                    rotation,
                    null,
                    draft.AttackOriginPath,
                    targetActor,
                    attackBlock.PlaybackSpeed,
                    attackBlock.SequenceId,
                    "공격 액티브"));
            }
        }

        private void ExecuteActivePreviewEvent(PendingActivePreviewEvent pending)
        {
            if (pending.Step == null || stage.PreviewRoot == null) return;
            if (pending.AttackBlock != null)
            {
                ExecuteActiveAttackBlockPreviewEvent(pending);
                return;
            }
            var root = stage.PreviewRoot.transform;
            var targetPoint = ResolveCombatTargetHitPoint(pending.Target);
            var forward = targetPoint - root.position;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = root.forward;
            forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
            switch (pending.Type)
            {
                case ActivePreviewEventType.Dash:
                    ShowPreviewHitArea(pending.Step, root.position, forward, targetPoint);
                    PlayActivePresentationEvent(
                        pending.Step,
                        pending.Presentation,
                        MonsterActivePresentationEvent.DashExit,
                        targetPoint,
                        forward,
                        pending.Target == null ? null : new[] { pending.Target });
                    if (pending.Target != null)
                    {
                        var destination = ResolveActivePreviewDashDestination(
                            root.position,
                            pending.Target,
                            pending.Step.DashDistance);
                        root.position = destination;
                        root.rotation = Quaternion.LookRotation(forward, Vector3.up);
                    }
                    PlayActivePresentationEvent(
                        pending.Step,
                        pending.Presentation,
                        MonsterActivePresentationEvent.DashEnter,
                        targetPoint,
                        forward,
                        pending.Target == null ? null : new[] { pending.Target });
                    combatStatus = $"{pending.Step.DisplayName} 돌진";
                    break;
                case ActivePreviewEventType.Motion:
                    root.rotation = Quaternion.LookRotation(forward, Vector3.up);
                    PlayActivePresentationEvent(
                        pending.Step,
                        pending.Presentation,
                        MonsterActivePresentationEvent.MotionStart,
                        targetPoint,
                        forward,
                        pending.Target == null ? null : new[] { pending.Target });
                    BeginActiveStepClip(pending.Presentation, pending.Step);
                    combatStatus = $"{pending.Step.DisplayName} 모션 재생";
                    break;
                case ActivePreviewEventType.Telegraph:
                    PlayActivePresentationEvent(
                        pending.Step,
                        pending.Presentation,
                        MonsterActivePresentationEvent.Telegraph,
                        targetPoint,
                        forward,
                        pending.Target == null ? null : new[] { pending.Target });
                    combatStatus = $"{pending.Step.DisplayName} 예고 · {GetActivePatternLabel(pending.Step.Pattern)}";
                    break;
                case ActivePreviewEventType.Launch:
                    if (!pending.Step.DashBeforeAttack)
                    {
                        ShowPreviewHitArea(pending.Step, root.position, forward, targetPoint);
                    }
                    PlayActivePresentationEvent(
                        pending.Step,
                        pending.Presentation,
                        MonsterActivePresentationEvent.Launch,
                        targetPoint,
                        forward,
                        pending.Target == null ? null : new[] { pending.Target });
                    PlayActivePresentationEvent(
                        pending.Step,
                        pending.Presentation,
                        MonsterActivePresentationEvent.DeliverySpawn,
                        targetPoint,
                        forward,
                        pending.Target == null ? null : new[] { pending.Target });
                    PlayActivePresentationEvent(
                        pending.Step,
                        pending.Presentation,
                        MonsterActivePresentationEvent.Travel,
                        targetPoint,
                        forward,
                        pending.Target == null ? null : new[] { pending.Target });
                    combatStatus = $"{pending.Step.DisplayName} 발동 · {GetActiveTargetPolicyLabel(pending.Step.TargetPolicy)}";
                    break;
                case ActivePreviewEventType.Impact:
                    var impactRotation = Quaternion.LookRotation(forward);
                    var victims = ResolveActivePreviewVictims(pending.Step, pending.Target, root.position, forward);
                    PlayActivePresentationEvent(
                        pending.Step,
                        pending.Presentation,
                        MonsterActivePresentationEvent.Impact,
                        targetPoint,
                        forward,
                        victims);
                    PlayFeelAt(
                        draft.ActiveAttackProfile?.ImpactFeel,
                        targetPoint,
                        impactRotation,
                        pending.Target == null ? null : pending.Target.gameObject);
                    // Monster Maker 재생은 최종 연출 확인용이다. 판정 외곽선은 조립소의
                    // 판정 Preview가 소유하며 실제 VFX 위에 자동으로 겹치지 않는다.
                    var appliedCount = 0;
                    for (var index = 0; index < victims.Count; index++)
                    {
                        if (ApplyPreviewDamage(victims[index], draft.AttackPower * pending.Step.DamageMultiplier))
                            appliedCount++;
                    }
                    combatStatus = $"{pending.Step.DisplayName} 적중 {appliedCount}명 · 피해 {pending.Step.DamageMultiplier:0.##}배" +
                                   (pending.Step.HitEffects.Count > 0
                                       ? $" · 타격효과 {pending.Step.HitEffects.Count}개"
                                       : string.Empty);
                    break;
                case ActivePreviewEventType.Complete:
                    var completedVictims = ResolveActivePreviewVictims(
                        pending.Step,
                        pending.Target,
                        root.position,
                        forward);
                    PlayActivePresentationEvent(
                        pending.Step,
                        pending.Presentation,
                        MonsterActivePresentationEvent.AreaResolved,
                        targetPoint,
                        forward,
                        completedVictims);
                    PlayActivePresentationEvent(
                        pending.Step,
                        pending.Presentation,
                        MonsterActivePresentationEvent.DeliveryEnd,
                        targetPoint,
                        forward,
                        completedVictims);
                    ReleaseActivePresentationVfx(MonsterActivePresentationEndPolicy.DeliveryEnd);
                    PlayActivePresentationEvent(
                        pending.Step,
                        pending.Presentation,
                        MonsterActivePresentationEvent.StepEnd,
                        targetPoint,
                        forward,
                        completedVictims);
                    ReleaseActivePresentationVfx(MonsterActivePresentationEndPolicy.StepEnd);
                    ReleaseActivePresentationVfx(MonsterActivePresentationEndPolicy.MotionEnd);
                    combatStatus = $"{pending.Step.DisplayName} Step 종료";
                    break;
            }
        }

        private void ExecuteActiveAttackBlockPreviewEvent(PendingActivePreviewEvent pending)
        {
            var attackBlock = pending.AttackBlock;
            var profile = attackBlock?.Profile;
            if (profile == null || stage.PreviewRoot == null) return;
            var root = stage.PreviewRoot.transform;
            var attackOrigin = ResolvePreviewSocket(draft.AttackOriginPath) ?? root;
            var origin = attackOrigin.position;
            var targetPoint = ResolveCombatTargetHitPoint(pending.Target);
            var forward = targetPoint - root.position;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = root.forward;
            forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
            var rotation = Quaternion.LookRotation(forward, Vector3.up);
            var areaCenter = ResolveAttackBlockAreaCenter(profile, origin, targetPoint, forward);

            switch (pending.Type)
            {
                case ActivePreviewEventType.Dash:
                    ShowPreviewHitArea(profile, origin, forward, targetPoint, 1f);
                    PlayActiveAttackBlockContract(
                        attackBlock,
                        MonsterBasicAttackVfxEvent.DashExit,
                        origin,
                        targetPoint,
                        areaCenter,
                        rotation,
                        targetActor: pending.Target);
                    if (pending.Target != null)
                    {
                        var destination = ResolveActivePreviewDashDestination(
                            root.position,
                            pending.Target,
                            pending.Step.DashDistance);
                        root.SetPositionAndRotation(destination, rotation);
                        attackOrigin = ResolvePreviewSocket(draft.AttackOriginPath) ?? root;
                        origin = attackOrigin.position;
                        areaCenter = ResolveAttackBlockAreaCenter(profile, origin, targetPoint, forward);
                    }
                    PlayActiveAttackBlockContract(
                        attackBlock,
                        MonsterBasicAttackVfxEvent.DashEnter,
                        origin,
                        targetPoint,
                        areaCenter,
                        rotation,
                        targetActor: pending.Target);
                    combatStatus = $"{pending.Step.DisplayName} 돌진";
                    break;

                case ActivePreviewEventType.Motion:
                    root.rotation = rotation;
                    PlayActiveAttackBlockContract(
                        attackBlock,
                        MonsterBasicAttackVfxEvent.MotionStart,
                        origin,
                        targetPoint,
                        areaCenter,
                        rotation,
                        targetActor: pending.Target);
                    BeginActiveStepClip(pending.Presentation, pending.Step);
                    combatStatus = $"{pending.Step.DisplayName} 모션 재생";
                    break;

                case ActivePreviewEventType.Telegraph:
                    PlayActiveAttackBlockContract(
                        attackBlock,
                        MonsterBasicAttackVfxEvent.Telegraph,
                        origin,
                        targetPoint,
                        areaCenter,
                        rotation,
                        targetActor: pending.Target);
                    combatStatus = $"{pending.Step.DisplayName} 예고 · {GetActivePatternLabel(pending.Step.Pattern)}";
                    break;

                case ActivePreviewEventType.Launch:
                    if (profile.MovementModule != MonsterBasicAttackMovementModule.Dash)
                    {
                        ShowPreviewHitArea(profile, origin, forward, targetPoint, 1f);
                    }
                    PlayActiveAttackBlockContract(
                        attackBlock,
                        MonsterBasicAttackVfxEvent.RecipeExecute,
                        origin,
                        targetPoint,
                        areaCenter,
                        rotation,
                        targetActor: pending.Target);
                    if (profile.UsesProjectileVisual)
                    {
                        SpawnActiveAttackBlockDeliveryVisuals(
                            attackBlock,
                            origin,
                            targetPoint,
                            forward,
                            pending.Target);
                    }
                    combatStatus = $"{pending.Step.DisplayName} 발동 · " +
                                   GetActiveTargetPolicyLabel(pending.Step.TargetPolicy);
                    break;

                case ActivePreviewEventType.Impact:
                    var appliedCount = 0;
                    var damageRatio = profile.ResolveDamageRatio(pending.DamageStage);
                    var usesProjectileAreas = profile.CollisionModule ==
                                              MonsterBasicAttackCollisionModule.AreaImpact &&
                                              attackBlock.Deliveries.Any(delivery => delivery != null);
                    if (usesProjectileAreas)
                    {
                        for (var deliveryIndex = 0;
                             deliveryIndex < attackBlock.Deliveries.Count;
                             deliveryIndex++)
                        {
                            var delivery = attackBlock.Deliveries[deliveryIndex];
                            if (delivery == null) continue;
                            var projectileCenter = delivery.transform.position;
                            var victims = ResolveActivePreviewAreaVictims(
                                profile,
                                pending.Target,
                                projectileCenter);
                            var deliveryApplied = false;
                            for (var victimIndex = 0; victimIndex < victims.Count; victimIndex++)
                            {
                                var victim = victims[victimIndex];
                                var targetRatio = victim == pending.Target ? 1f : profile.SecondaryDamageRatio;
                                var damage = draft.AttackPower * pending.Step.DamageMultiplier *
                                             damageRatio * targetRatio;
                                if (!ApplyPreviewDamage(
                                        victim,
                                        damage,
                                        profile.HitCount > 1
                                            ? DamageFeedbackFlags.SeparateFloatingNumber
                                            : DamageFeedbackFlags.None))
                                {
                                    continue;
                                }
                                appliedCount++;
                                deliveryApplied = true;
                                var hitPoint = ResolveCombatTargetHitPoint(victim);
                                PlayActiveAttackBlockContract(
                                    attackBlock,
                                    MonsterBasicAttackVfxEvent.TargetDamaged,
                                    origin,
                                    hitPoint,
                                    projectileCenter,
                                    delivery.transform.rotation,
                                    delivery,
                                    victim,
                                    pending.DamageStage);
                            }
                            if (!deliveryApplied) continue;
                            PlayActiveAttackBlockContract(
                                attackBlock,
                                MonsterBasicAttackVfxEvent.AreaResolved,
                                origin,
                                projectileCenter + Vector3.up * 0.4f,
                                projectileCenter,
                                delivery.transform.rotation,
                                delivery,
                                pending.Target,
                                pending.DamageStage);
                        }
                    }
                    else
                    {
                        var victims = pending.ProgressiveTarget == null
                            ? ResolveActivePreviewVictims(
                                pending.Step,
                                pending.Target,
                                root.position,
                                forward)
                            : new List<UnitActor> { pending.ProgressiveTarget };
                        for (var index = 0; index < victims.Count; index++)
                        {
                            var victim = victims[index];
                            var targetRatio = victim == pending.Target ? 1f : profile.SecondaryDamageRatio;
                            var damage = draft.AttackPower * pending.Step.DamageMultiplier *
                                         damageRatio * targetRatio;
                            if (!ApplyPreviewDamage(
                                    victim,
                                    damage,
                                    profile.HitCount > 1
                                        ? DamageFeedbackFlags.SeparateFloatingNumber
                                        : DamageFeedbackFlags.None))
                            {
                                continue;
                            }
                            appliedCount++;
                            var hitPoint = ResolveCombatTargetHitPoint(victim);
                            PlayActiveAttackBlockContract(
                                attackBlock,
                                MonsterBasicAttackVfxEvent.TargetDamaged,
                                origin,
                                hitPoint,
                                areaCenter,
                                rotation,
                                targetActor: victim,
                                damageStage: pending.DamageStage);
                            if (profile.SequenceModule == MonsterBasicAttackSequenceModule.ReturnPasses)
                            {
                                PlayActiveAttackBlockContract(
                                    attackBlock,
                                    pending.DamageStage == 0
                                        ? MonsterBasicAttackVfxEvent.OutboundTargetDamaged
                                        : MonsterBasicAttackVfxEvent.ReturnTargetDamaged,
                                    origin,
                                    hitPoint,
                                    areaCenter,
                                    rotation,
                                    targetActor: victim,
                                    damageStage: pending.DamageStage);
                            }
                        }
                        if (appliedCount > 0)
                        {
                            attackBlock.ProgressiveDamageApplied = true;
                        }
                        var damageStageComplete = !pending.IsProgressiveImpact ||
                                                  pending.IsFinalProgressiveImpact;
                        if (damageStageComplete && attackBlock.ProgressiveDamageApplied &&
                            profile.Shape == MonsterBasicAttackShape.Circle)
                        {
                            PlayActiveAttackBlockContract(
                                attackBlock,
                                MonsterBasicAttackVfxEvent.AreaResolved,
                                origin,
                                areaCenter + Vector3.up * 0.4f,
                                areaCenter,
                                rotation,
                                targetActor: pending.Target,
                                damageStage: pending.DamageStage);
                        }
                    }
                    if (profile.SequenceModule == MonsterBasicAttackSequenceModule.ReturnPasses &&
                        pending.DamageStage == 0)
                    {
                        for (var index = 0; index < attackBlock.Deliveries.Count; index++)
                        {
                            var delivery = attackBlock.Deliveries[index];
                            if (delivery == null) continue;
                            PlayActiveAttackBlockContract(
                                attackBlock,
                                MonsterBasicAttackVfxEvent.DeliveryTurn,
                                origin,
                                delivery.transform.position,
                                areaCenter,
                                delivery.transform.rotation,
                                delivery,
                                pending.Target,
                                pending.DamageStage);
                        }
                    }
                    if ((!pending.IsProgressiveImpact && appliedCount > 0 ||
                         pending.IsFinalProgressiveImpact && attackBlock.ProgressiveDamageApplied) &&
                        pending.DamageStage == profile.HitCount - 1)
                    {
                        PlayActiveAttackBlockContract(
                            attackBlock,
                            MonsterBasicAttackVfxEvent.SequenceEnd,
                            origin,
                            targetPoint,
                            areaCenter,
                            rotation,
                            targetActor: pending.Target,
                            damageStage: pending.DamageStage);
                    }
                    if (appliedCount > 0 &&
                        (pending.DamageStage == 0 || profile.RepeatImpactFeedback) &&
                        (!pending.IsProgressiveImpact || !attackBlock.ProgressiveImpactFeelPlayed))
                    {
                        PlayFeelAt(
                            draft.ActiveAttackProfile?.ImpactFeel,
                            targetPoint,
                            rotation,
                            pending.Target == null ? null : pending.Target.gameObject);
                        if (pending.IsProgressiveImpact)
                        {
                            attackBlock.ProgressiveImpactFeelPlayed = true;
                        }
                    }
                    combatStatus = $"{pending.Step.DisplayName} {pending.DamageStage + 1}/{profile.HitCount}타 " +
                                   $"· 적중 {appliedCount}명 · {damageRatio * pending.Step.DamageMultiplier:0.##}배" +
                                   (pending.Step.HitEffects.Count > 0
                                       ? $" · 타격효과 {pending.Step.HitEffects.Count}개"
                                       : string.Empty);
                    break;

                case ActivePreviewEventType.DeliveryEnd:
                    ReleaseActiveAttackBlockDeliveries(attackBlock, pending.Target);
                    combatStatus = $"{pending.Step.DisplayName} 이동체 종료";
                    break;

                case ActivePreviewEventType.Complete:
                    ReleaseActiveAttackBlockDeliveries(attackBlock, pending.Target);
                    PlayActiveAttackBlockContract(
                        attackBlock,
                        MonsterBasicAttackVfxEvent.MotionEnd,
                        origin,
                        targetPoint,
                        areaCenter,
                        rotation,
                        targetActor: pending.Target);
                    ReleaseContractVfx(
                        MonsterBasicAttackVfxEndPolicy.MotionEnd,
                        null,
                        attackBlock.SequenceId);
                    combatStatus = $"{pending.Step.DisplayName} Step 종료";
                    break;
            }
        }

        private bool PlayActiveAttackBlockContract(
            ActivePreviewAttackBlock attackBlock,
            MonsterBasicAttackVfxEvent eventType,
            Vector3 origin,
            Vector3 hitPoint,
            Vector3 areaCenter,
            Quaternion rotation,
            GameObject projectile = null,
            UnitActor targetActor = null,
            int damageStage = 0)
        {
            if (attackBlock?.Profile == null) return false;
            return PlayContractVfx(
                eventType,
                attackBlock.Profile,
                origin,
                hitPoint,
                areaCenter,
                rotation,
                projectile,
                draft.AttackOriginPath,
                damageStage,
                attackBlock.Presentation?.AttackBlockBindings,
                attackBlock.MotionId,
                attackBlock.PlaybackSpeed,
                attackBlock.SequenceId,
                "공격 액티브",
                targetActor);
        }

        private void SpawnActiveAttackBlockDeliveryVisuals(
            ActivePreviewAttackBlock attackBlock,
            Vector3 origin,
            Vector3 targetPoint,
            Vector3 forward,
            UnitActor targetActor)
        {
            var profile = attackBlock?.Profile;
            if (profile == null || stage.PreviewRoot == null) return;
            var bindings = attackBlock.Presentation?.AttackBlockBindings;
            var hasDeliveryVisual = TryResolvePreviewDeliveryVisual(
                profile,
                bindings,
                attackBlock.MotionId,
                out var deliverySlot,
                out var deliveryBinding);
            var projectileVisual = hasDeliveryVisual
                ? deliveryBinding.Prefab
                : profile.ProjectileCarrierPrefab;
            if (projectileVisual == null && !hasDeliveryVisual) return;
            var carrierOnly = profile.VfxSlots.Count > 0 && !hasDeliveryVisual;

            var count = profile.ProjectileCount;
            var speed = profile.ProjectileSpeed * attackBlock.PlaybackSpeed;
            for (var index = 0; index < count; index++)
            {
                var direction = profile.ResolveProjectileDirection(forward, index);
                var rotation = Quaternion.LookRotation(direction, Vector3.up);
                var spawnPosition = origin;
                if (hasDeliveryVisual)
                {
                    spawnPosition += rotation * deliveryBinding.LocalPosition;
                    rotation *= deliveryBinding.LocalRotation;
                }
                var deliveryEnd = profile.ProjectileTravel is MonsterBasicAttackProjectileTravel.Homing or
                                      MonsterBasicAttackProjectileTravel.Returning ||
                                  profile.CollisionModule == MonsterBasicAttackCollisionModule.StopOnFirstTarget
                    ? targetPoint
                    : origin + direction * profile.ResolveRange(1f);
                var outboundDuration = Vector3.Distance(spawnPosition, deliveryEnd) /
                                       Mathf.Max(0.01f, speed);
                var totalDuration = profile.ProjectileTravel == MonsterBasicAttackProjectileTravel.Returning
                    ? outboundDuration * 2f
                    : outboundDuration;
                var isPlaceholder = hasDeliveryVisual && deliveryBinding.Prefab == null;
                var instance = isPlaceholder
                    ? CreatePreviewVfxPlaceholder(
                        $"공격 액티브 · {deliverySlot.DisplayName}",
                        MonsterActivePresentationEvent.DeliverySpawn,
                        spawnPosition,
                        rotation,
                        deliveryBinding.Scale * Mathf.Max(0.01f, draft.VfxScale))
                    : UnityEngine.Object.Instantiate(projectileVisual);
                if (!isPlaceholder)
                {
                    instance.name = carrierOnly
                        ? "[공격 액티브 판정 이동체 · VFX 미배정] " + projectileVisual.name
                        : "[공격 액티브 이동체] " + projectileVisual.name;
                    instance.transform.SetPositionAndRotation(spawnPosition, rotation);
                    var scale = hasDeliveryVisual ? deliveryBinding.Scale : 1f;
                    MonsterBasicAttackVfxPlayback.ApplyInstanceScale(
                        instance,
                        projectileVisual.transform.localScale * scale * Mathf.Max(0.01f, draft.VfxScale));
                    MonsterBasicAttackVfxPlayback.ApplyBrightnessScale(
                        instance,
                        MonsterBasicAttackVfxPlayback.DefaultMainBattleBrightnessScale);
                    if (carrierOnly)
                    {
                        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                            renderer.enabled = false;
                        MonsterBasicAttackVfxPlayback.StopAndClear(instance);
                    }
                }
                var legacyProjectile = instance.GetComponent<MonsterProjectileActor>();
                if (legacyProjectile != null) legacyProjectile.enabled = false;
                var basicProjectile = instance.GetComponent<MonsterBasicAttackProjectileActor>();
                if (basicProjectile != null) basicProjectile.enabled = false;
                stage.AddAuxiliary(instance);
                if (!isPlaceholder && !carrierOnly)
                {
                    MonsterBasicAttackVfxPlayback.RestartAtOffset(
                        instance,
                        hasDeliveryVisual ? deliveryBinding.PlaybackOffset : 0f,
                        playbackSpeed: (hasDeliveryVisual ? deliveryBinding.PlaybackSpeed : 1f) *
                                       attackBlock.PlaybackSpeed);
                }
                activeVfx.Add(new PreviewVfx(
                    instance,
                    Mathf.Max(profile.ProjectileLifetime / attackBlock.PlaybackSpeed, totalDuration),
                    MonsterBasicAttackVfxEndPolicy.DeliveryEnd,
                    instance,
                    isPlaceholder,
                    hasDeliveryVisual ? deliveryBinding.PlaybackOffset : 0f,
                    (hasDeliveryVisual ? deliveryBinding.PlaybackSpeed : 1f) * attackBlock.PlaybackSpeed,
                    attackBlock.SequenceId,
                    true,
                    spawnPosition,
                    deliveryEnd,
                    totalDuration,
                    profile.ProjectileTravel == MonsterBasicAttackProjectileTravel.Returning));
                attackBlock.Deliveries.Add(instance);
                PlayActiveAttackBlockContract(
                    attackBlock,
                    MonsterBasicAttackVfxEvent.DeliverySpawn,
                    origin,
                    targetPoint,
                    targetPoint,
                    rotation,
                    instance,
                    targetActor);
            }
        }

        private void ReleaseActiveAttackBlockDeliveries(
            ActivePreviewAttackBlock attackBlock,
            UnitActor targetActor)
        {
            if (attackBlock?.Profile == null) return;
            for (var index = attackBlock.Deliveries.Count - 1; index >= 0; index--)
            {
                var delivery = attackBlock.Deliveries[index];
                if (delivery != null)
                {
                    var position = delivery.transform.position;
                    PlayActiveAttackBlockContract(
                        attackBlock,
                        MonsterBasicAttackVfxEvent.DeliveryEnd,
                        position,
                        position,
                        position,
                        delivery.transform.rotation,
                        delivery,
                        targetActor);
                    ReleaseContractVfx(
                        MonsterBasicAttackVfxEndPolicy.DeliveryEnd,
                        delivery,
                        attackBlock.SequenceId);
                }
                attackBlock.Deliveries.RemoveAt(index);
            }
        }

        private static Vector3 ResolveAttackBlockAreaCenter(
            MonsterBasicAttackProfile profile,
            Vector3 origin,
            Vector3 targetPoint,
            Vector3 forward)
        {
            if (profile == null) return targetPoint;
            return profile.Center switch
            {
                MonsterBasicAttackCenter.Source => origin,
                MonsterBasicAttackCenter.Forward => origin + forward * profile.ForwardOffset,
                _ => targetPoint
            };
        }

        private Vector3 ResolveActivePreviewDashDestination(
            Vector3 sourcePosition,
            UnitActor target,
            float maximumDistance)
        {
            if (target == null) return sourcePosition;
            var sourceRadius = Mathf.Max(0.05f, draft?.BodyRadius ?? 0.5f);
            return MonsterBasicAttackProfile.ResolveDashDestination(
                sourcePosition,
                target.transform.position,
                maximumDistance,
                sourceRadius + target.BodyRadius);
        }

        private List<UnitActor> ResolveActivePreviewAreaVictims(
            MonsterBasicAttackProfile profile,
            UnitActor primary,
            Vector3 center)
        {
            var result = dummyTargets
                .Select(target => target.Actor)
                .Where(actor => actor != null && actor.IsAlive)
                .Where(actor =>
                {
                    var offset = actor.transform.position - center;
                    offset.y = 0f;
                    return offset.sqrMagnitude <= profile.Radius * profile.Radius;
                })
                .OrderBy(actor =>
                {
                    var offset = actor.transform.position - center;
                    offset.y = 0f;
                    return offset.sqrMagnitude;
                })
                .Take(profile.MaxTargets)
                .ToList();
            if (primary == null) return result;
            result.Remove(primary);
            result.Insert(0, primary);
            if (result.Count > profile.MaxTargets)
            {
                result.RemoveAt(result.Count - 1);
            }
            return result;
        }

        private void PlayActivePresentationEvent(
            MonsterActiveAttackStep step,
            MonsterMakerActiveStepPresentationDraft presentation,
            MonsterActivePresentationEvent timing,
            Vector3 targetPoint,
            Vector3 forward,
            IReadOnlyList<UnitActor> targets = null)
        {
            if (step?.PresentationSlots.Count > 0)
            {
                for (var index = 0; index < step.PresentationSlots.Count; index++)
                {
                    var contract = step.PresentationSlots[index];
                    if (contract == null || contract.Timing != timing) continue;
                    var slot = presentation?.ResolveSlot(contract.SlotId);
                    if (slot?.Feedback == null) continue;
                    var occurrenceCount = contract.Multiplicity switch
                    {
                        MonsterActivePresentationMultiplicity.OncePerProjectile =>
                            Mathf.Max(1, step.ProjectileCount),
                        MonsterActivePresentationMultiplicity.PerTargetHit =>
                            targets?.Count ?? 0,
                        _ => 1
                    };
                    for (var occurrence = 0; occurrence < occurrenceCount; occurrence++)
                    {
                        var target = targets != null && targets.Count > 0
                            ? targets[Mathf.Min(occurrence, targets.Count - 1)]
                            : null;
                        var occurrencePoint = target == null
                            ? targetPoint
                            : ResolveCombatTargetHitPoint(target);
                        if (contract.Anchor == MonsterActivePresentationAnchor.AreaCenter)
                        {
                            occurrencePoint = targetPoint;
                        }
                        ResolveActivePresentationPose(
                            step,
                            contract,
                            timing,
                            target,
                            occurrencePoint,
                            forward,
                            occurrence,
                            out var position,
                            out var rotation,
                            out var parent,
                            out var deliveryEnd,
                            out var deliveryDuration);
                        PlayActiveSlotFeedback(
                            slot,
                            contract,
                            position,
                            rotation,
                            parent,
                            deliveryEnd,
                            deliveryDuration,
                            step.PlaybackSpeed);
                    }
                }
                return;
            }

            var legacy = timing switch
            {
                MonsterActivePresentationEvent.Telegraph => presentation?.Telegraph,
                MonsterActivePresentationEvent.Launch => presentation?.Launch,
                MonsterActivePresentationEvent.Travel => presentation?.Travel,
                MonsterActivePresentationEvent.Impact => presentation?.Impact,
                MonsterActivePresentationEvent.DashExit => presentation?.DashExit,
                MonsterActivePresentationEvent.DashEnter => presentation?.DashEnter,
                _ => null
            };
            if (timing == MonsterActivePresentationEvent.Telegraph ||
                timing == MonsterActivePresentationEvent.Impact)
            {
                PlayFeedbackAt(legacy, targetPoint, Quaternion.LookRotation(forward));
            }
            else
            {
                PlayFeedback(legacy, draft.AttackOriginPath);
            }
        }

        private void ResolveActivePresentationPose(
            MonsterActiveAttackStep step,
            MonsterActivePresentationSlot contract,
            MonsterActivePresentationEvent timing,
            UnitActor target,
            Vector3 targetPoint,
            Vector3 forward,
            int occurrence,
            out Vector3 position,
            out Quaternion rotation,
            out Transform parent,
            out Vector3 deliveryEnd,
            out float deliveryDuration)
        {
            var root = stage.PreviewRoot.transform;
            var attackOrigin = ResolvePreviewSocket(draft.AttackOriginPath) ?? root;
            var direction = ResolveActiveProjectileDirection(step, forward, occurrence);
            var targetTransform = target == null ? null : target.transform;
            var targetHitCenter = target?.AnimationDriver?.HitCenter;
            var areaCenter = ResolveActivePreviewAreaCenter(
                step,
                targetPoint,
                root.position,
                forward,
                occurrence);
            deliveryEnd = step.Pattern == MonsterActiveAttackPattern.ExplosiveProjectile &&
                          occurrence == 0 && target != null
                ? targetPoint
                : attackOrigin.position + direction * step.Range;
            deliveryDuration = step.IsProjectile
                ? Vector3.Distance(attackOrigin.position, deliveryEnd) /
                  Mathf.Max(0.1f, step.ProjectileSpeed * step.PlaybackSpeed)
                : 0f;
            parent = contract.Anchor switch
            {
                MonsterActivePresentationAnchor.CasterRoot => root,
                MonsterActivePresentationAnchor.AttackOrigin or
                    MonsterActivePresentationAnchor.MarkerSocket or
                    MonsterActivePresentationAnchor.TrajectoryOrigin => attackOrigin,
                MonsterActivePresentationAnchor.TargetPoint or
                    MonsterActivePresentationAnchor.TargetRoot => targetTransform,
                MonsterActivePresentationAnchor.HitPoint => targetHitCenter ?? targetTransform,
                _ => null
            };
            position = contract.Anchor switch
            {
                MonsterActivePresentationAnchor.CasterRoot => root.position,
                MonsterActivePresentationAnchor.AttackOrigin or
                    MonsterActivePresentationAnchor.MarkerSocket or
                    MonsterActivePresentationAnchor.TrajectoryOrigin => attackOrigin.position,
                MonsterActivePresentationAnchor.TargetPoint or
                    MonsterActivePresentationAnchor.TargetRoot =>
                    targetTransform == null ? targetPoint : targetTransform.position,
                MonsterActivePresentationAnchor.HitPoint => targetPoint,
                MonsterActivePresentationAnchor.AreaCenter => areaCenter,
                MonsterActivePresentationAnchor.ProjectileRoot =>
                    timing == MonsterActivePresentationEvent.DeliverySpawn ? attackOrigin.position : deliveryEnd,
                _ => root.position
            };
            rotation = step.Pattern == MonsterActiveAttackPattern.InstantMagic
                ? step.MagicDirection switch
                {
                    MonsterActiveMagicDirection.GroundUp =>
                        Quaternion.LookRotation(Vector3.up, forward),
                    MonsterActiveMagicDirection.SkyDown =>
                        Quaternion.LookRotation(Vector3.down, forward),
                    _ => Quaternion.LookRotation(forward, Vector3.up)
                }
                : Quaternion.LookRotation(
                    contract.Anchor == MonsterActivePresentationAnchor.ProjectileRoot ? direction : forward,
                    Vector3.up);
        }

        private static Vector3 ResolveActiveProjectileDirection(
            MonsterActiveAttackStep step,
            Vector3 forward,
            int occurrence)
        {
            var count = Mathf.Max(1, step.ProjectileCount);
            var spreadRatio = count <= 1
                ? 0f
                : Mathf.Clamp(occurrence, 0, count - 1) / (float)(count - 1) - 0.5f;
            return Quaternion.AngleAxis(
                spreadRatio * step.ProjectileFanAngle,
                Vector3.up) * forward;
        }

        private static Vector3 ResolveActivePreviewAreaCenter(
            MonsterActiveAttackStep step,
            Vector3 targetPoint,
            Vector3 origin,
            Vector3 forward,
            int occurrence)
        {
            return step.Pattern switch
            {
                MonsterActiveAttackPattern.SelfCircle => origin,
                MonsterActiveAttackPattern.FrontCircle => origin + forward * step.ForwardOffset,
                MonsterActiveAttackPattern.InstantMagic => targetPoint,
                MonsterActiveAttackPattern.ExplosiveProjectile when occurrence == 0 => targetPoint,
                MonsterActiveAttackPattern.ExplosiveProjectile =>
                    origin + ResolveActiveProjectileDirection(step, forward, occurrence) * step.Range,
                _ => origin
            };
        }

        private void PlayActiveSlotFeedback(
            MonsterMakerActivePresentationSlotDraft slot,
            MonsterActivePresentationSlot contract,
            Vector3 position,
            Quaternion rotation,
            Transform parent,
            Vector3 deliveryEnd,
            float deliveryDuration,
            float stepPlaybackSpeed)
        {
            var feedback = slot.Feedback;
            if (slot.SfxState == MonsterBasicAttackSfxAssignmentState.Assigned)
            {
                if (feedback.Sound != null)
                    SfxEditorAudioPreview.Play(feedback.Sound, 0, false, feedback.SoundVolume);
                if (feedback.Sound == null && feedback.Sfx != null && feedback.Sfx.TrySelectClip(out var clip))
                {
                    SfxEditorAudioPreview.Play(clip, 0, false, feedback.Sfx.SelectVolume());
                }
            }
            if (slot.VfxState != MonsterBasicAttackVfxAssignmentState.Assigned ||
                stage.PreviewRoot == null)
            {
                return;
            }

            position += rotation * feedback.LocalPosition;
            rotation *= Quaternion.Euler(feedback.LocalEulerAngles);
            var isPlaceholder = feedback.VfxPrefab == null;
            var instance = isPlaceholder
                ? CreatePreviewVfxPlaceholder(
                    $"액티브 · {contract.DisplayName}",
                    contract.Timing,
                    position,
                    rotation,
                    feedback.Scale * Mathf.Max(0.01f, draft.VfxScale))
                : UnityEngine.Object.Instantiate(feedback.VfxPrefab);
            if (!isPlaceholder)
            {
                instance.name = $"[Active Skill VFX] {contract.DisplayName}";
                instance.transform.SetPositionAndRotation(position, rotation);
                MonsterBasicAttackVfxPlayback.ApplyInstanceScale(
                    instance,
                    feedback.VfxPrefab.transform.localScale *
                    feedback.Scale * Mathf.Max(0.01f, draft.VfxScale));
                MonsterBasicAttackVfxPlayback.ApplyBrightnessScale(
                    instance,
                    MonsterBasicAttackVfxPlayback.DefaultMainBattleBrightnessScale);
            }
            stage.AddAuxiliary(instance);
            if (contract.Attachment == MonsterActivePresentationAttachment.FollowAnchor && parent != null)
            {
                instance.transform.SetParent(parent, true);
            }
            if (!isPlaceholder)
            {
                // Preview Scene 이동이 Play 상태를 초기화하므로 등록이 끝난 뒤 시작한다.
                MonsterBasicAttackVfxPlayback.RestartAtOffset(
                    instance,
                    0f,
                    playbackSpeed: stepPlaybackSpeed);
            }
            var lifetime = (contract.UseDuration ? contract.Duration : feedback.VfxLifetime) /
                           Mathf.Max(0.05f, stepPlaybackSpeed);
            activeVfx.Add(new PreviewVfx(
                instance,
                lifetime,
                contract.EndPolicy,
                contract.Attachment == MonsterActivePresentationAttachment.DeliveryVisual,
                position,
                deliveryEnd,
                deliveryDuration,
                isPlaceholder));
        }

        private void ReleaseActivePresentationVfx(MonsterActivePresentationEndPolicy policy)
        {
            for (var index = activeVfx.Count - 1; index >= 0; index--)
            {
                var vfx = activeVfx[index];
                if (vfx.ActiveEndPolicy != policy) continue;
                if (vfx.Instance != null) stage.RemoveAuxiliary(vfx.Instance);
                activeVfx.RemoveAt(index);
            }
        }

        private List<UnitActor> ResolveActivePreviewVictims(
            MonsterActiveAttackStep step,
            UnitActor primary,
            Vector3 origin,
            Vector3 forward)
        {
            var result = new List<UnitActor>();
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            for (var index = 0; index < dummyTargets.Count && result.Count < step.MaxTargets; index++)
            {
                var actor = dummyTargets[index].Actor;
                if (actor == null || !actor.IsAlive || !IsInsideActivePreviewShape(step, actor, primary, origin, forward))
                    continue;
                result.Add(actor); // 한 Step에서는 한 대상이 여러 탄·범위에 겹쳐도 한 번만 적중
            }
            if (primary != null && primary.IsAlive)
            {
                result.Remove(primary);
                result.Insert(0, primary);
                if (result.Count > step.MaxTargets)
                {
                    result.RemoveAt(result.Count - 1);
                }
            }
            return result;
        }

        private static bool IsInsideActivePreviewShape(
            MonsterActiveAttackStep step,
            UnitActor candidate,
            UnitActor primary,
            Vector3 origin,
            Vector3 forward)
        {
            var point = candidate.transform.position;
            point.y = origin.y;
            var delta = point - origin;
            var distance = delta.magnitude;
            var targetRadius = candidate.BodyRadius;
            switch (step.Pattern)
            {
                case MonsterActiveAttackPattern.Line:
                case MonsterActiveAttackPattern.PiercingBeam:
                case MonsterActiveAttackPattern.ReturningProjectile:
                case MonsterActiveAttackPattern.TravelingWave:
                    return IsInsideLine(delta, forward, step.Range, step.Width * 0.5f + targetRadius);
                case MonsterActiveAttackPattern.Cone:
                case MonsterActiveAttackPattern.Breath:
                    return distance <= step.Range + targetRadius &&
                           (distance <= 0.001f || Vector3.Angle(forward, delta) <= step.Angle * 0.5f);
                case MonsterActiveAttackPattern.SelfCircle:
                    return distance <= step.Radius + targetRadius;
                case MonsterActiveAttackPattern.FrontCircle:
                    return Vector3.Distance(point, origin + forward * step.ForwardOffset) <= step.Radius + targetRadius;
                case MonsterActiveAttackPattern.TargetCircle:
                    return primary != null && Vector3.Distance(point, primary.transform.position) <=
                           step.Radius + targetRadius;
                case MonsterActiveAttackPattern.PiercingProjectile:
                    return step.ProjectileFormation == MonsterActiveProjectileFormation.Fan
                        ? distance <= step.Range + targetRadius &&
                          (distance <= 0.001f || Vector3.Angle(forward, delta) <= step.ProjectileFanAngle * 0.5f)
                        : IsInsideLine(delta, forward, step.Range, step.ProjectileCollisionRadius + targetRadius);
                case MonsterActiveAttackPattern.ExplosiveProjectile:
                    if (primary != null && Vector3.Distance(point, primary.transform.position) <=
                        step.ExplosionRadius + targetRadius) return true;
                    return step.ProjectileFormation == MonsterActiveProjectileFormation.Fan &&
                           distance <= step.Range + targetRadius &&
                           (distance <= 0.001f || Vector3.Angle(forward, delta) <= step.ProjectileFanAngle * 0.5f);
                case MonsterActiveAttackPattern.StandardProjectile:
                    return step.ProjectileCount > 1
                        ? distance <= step.Range + targetRadius &&
                          (distance <= 0.001f ||
                           Vector3.Angle(forward, delta) <= step.ProjectileFanAngle * 0.5f)
                        : candidate == primary;
                case MonsterActiveAttackPattern.InstantMagic:
                    return step.InstantMagicTarget == MonsterActiveInstantMagicTarget.SingleTarget
                        ? candidate == primary
                        : primary != null && Vector3.Distance(point, primary.transform.position) <=
                          step.Radius + targetRadius;
                default:
                    return candidate == primary;
            }
        }

        private static bool IsInsideLine(Vector3 delta, Vector3 forward, float length, float halfWidth)
        {
            var along = Vector3.Dot(delta, forward);
            if (along < 0f || along > length) return false;
            return (delta - forward * along).magnitude <= halfWidth;
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
            attackPoseHoldDuration = 0f;
            if (attack != null && draft.BasicAttackProfile != null &&
                draft.BasicAttackProfile.UsesBreathDurationContract && markerBuffer.Length > 0)
            {
                var motionDuration = clip.length / playbackSpeed;
                var recipeStart = motionDuration * markerBuffer[0].NormalizedTime;
                var breathDuration = attack.ResolveBreathDuration(draft.BasicAttackProfile.BreathDuration);
                attackPoseHoldDuration = Mathf.Max(0f, recipeStart + breathDuration - motionDuration);
            }
            remainingAttackPoseHold = attackPoseHoldDuration;
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
            if (attack != null)
            {
                BeginBasicAttackContractPreview(startSocketPath);
            }
            lastTickTime = EditorApplication.timeSinceStartup;
        }

        private void BeginBasicAttackContractPreview(string startSocketPath)
        {
            if (currentAttack == null || draft?.BasicAttackProfile == null || stage.PreviewRoot == null)
            {
                return;
            }

            basicAttackVfxSequence++;
            basicAttackVfxClaims.Clear();
            PlayContractVfx(
                MonsterBasicAttackVfxEvent.MotionStart,
                draft.BasicAttackProfile,
                stage.PreviewRoot.transform.position,
                ResolveCombatTargetHitPoint(),
                ResolveCombatTargetHitPoint(),
                stage.PreviewRoot.transform.rotation,
                null,
                startSocketPath);
            ScheduleRecipeLeadContractVfx(draft.BasicAttackProfile);
            PlayContractVfx(
                MonsterBasicAttackVfxEvent.Telegraph,
                draft.BasicAttackProfile,
                stage.PreviewRoot.transform.position,
                ResolveCombatTargetHitPoint(),
                ResolveBasicAttackAreaCenter(draft.BasicAttackProfile, startSocketPath),
                stage.PreviewRoot.transform.rotation,
                null,
                startSocketPath);
        }

        private Vector3 ResolveBasicAttackAreaCenter(
            MonsterBasicAttackProfile profile,
            string startSocketPath)
        {
            if (stage.PreviewRoot == null || profile == null)
            {
                return ResolveCombatTargetHitPoint();
            }
            var originTransform = ResolvePreviewSocket(startSocketPath) ??
                                  ResolvePreviewSocket(draft?.AttackOriginPath) ??
                                  stage.PreviewRoot.transform;
            var origin = originTransform.position;
            var target = ResolveCombatTargetHitPoint();
            var forward = target - origin;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = originTransform.forward;
            }
            return profile.Center switch
            {
                MonsterBasicAttackCenter.Source => origin,
                MonsterBasicAttackCenter.Forward =>
                    origin + forward.normalized * profile.ForwardOffset,
                _ => target
            };
        }

        private void BeginActiveStepClip(
            MonsterMakerActiveStepPresentationDraft presentation,
            MonsterActiveAttackStep step)
        {
            if (stage.PreviewRoot == null || draft == null) return;
            draft.ResolveActiveStepMotion(
                presentation,
                out var motionClip,
                out var motionPlaybackSpeed,
                out var motionCrossFadeDuration,
                out _);
            if (motionClip == null) return;
            CaptureActiveStepBlendPose(motionCrossFadeDuration);
            currentClip = motionClip;
            currentAttack = null;
            playbackSpeed = motionPlaybackSpeed * Mathf.Max(0.05f, step?.PlaybackSpeed ?? 1f);
            loop = false;
            playbackTime = 0f;
            attackPoseHoldDuration = 0f;
            remainingAttackPoseHold = 0f;
            previousNormalizedTime = -0.0001f;
            nextMarkerIndex = 0;
            markerBuffer = Array.Empty<MonsterAttackMarker>();
            markerDraftBuffer = Array.Empty<MonsterMakerMarkerDraft>();
            playing = true;
            SampleCurrentPose();
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

            ApplyActiveStepBlendPose();

            renderDirty = true;
        }

        private void CaptureActiveStepBlendPose(float duration)
        {
            ClearActiveStepBlendPose();
            activeStepBlendDuration = Mathf.Max(0f, duration);
            if (activeStepBlendDuration <= 0f || stage.PreviewRoot == null)
            {
                return;
            }

            var root = animationSampleRoot != null ? animationSampleRoot.transform : stage.PreviewRoot.transform;
            activeStepBlendTransforms = root.GetComponentsInChildren<Transform>(true);
            activeStepBlendPositions = new Vector3[activeStepBlendTransforms.Length];
            activeStepBlendRotations = new Quaternion[activeStepBlendTransforms.Length];
            activeStepBlendScales = new Vector3[activeStepBlendTransforms.Length];
            for (var index = 0; index < activeStepBlendTransforms.Length; index++)
            {
                var current = activeStepBlendTransforms[index];
                activeStepBlendPositions[index] = current.localPosition;
                activeStepBlendRotations[index] = current.localRotation;
                activeStepBlendScales[index] = current.localScale;
            }
        }

        private void ApplyActiveStepBlendPose()
        {
            if (activeStepBlendDuration <= 0f || activeStepBlendTransforms.Length == 0)
            {
                return;
            }

            var ratio = Mathf.Clamp01(activeStepBlendElapsed / activeStepBlendDuration);
            for (var index = 0; index < activeStepBlendTransforms.Length; index++)
            {
                var current = activeStepBlendTransforms[index];
                if (current == null) continue;
                current.localPosition = Vector3.LerpUnclamped(
                    activeStepBlendPositions[index], current.localPosition, ratio);
                current.localRotation = Quaternion.SlerpUnclamped(
                    activeStepBlendRotations[index], current.localRotation, ratio);
                current.localScale = Vector3.LerpUnclamped(
                    activeStepBlendScales[index], current.localScale, ratio);
            }

            if (ratio >= 1f)
            {
                ClearActiveStepBlendPose();
            }
        }

        private void ClearActiveStepBlendPose()
        {
            activeStepBlendTransforms = Array.Empty<Transform>();
            activeStepBlendPositions = Array.Empty<Vector3>();
            activeStepBlendRotations = Array.Empty<Quaternion>();
            activeStepBlendScales = Array.Empty<Vector3>();
            activeStepBlendDuration = 0f;
            activeStepBlendElapsed = 0f;
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
            var usesPresentationContract = profile?.VfxSlots.Count > 0;
            if (usesPresentationContract)
            {
                PlayContractVfx(
                    MonsterBasicAttackVfxEvent.RecipeExecute,
                    profile,
                    origin,
                    targetPosition,
                    targetPosition,
                    attackRotation,
                    null,
                    markerDraft.SocketOverride);
            }
            else
            {
                PlayProfileFeedbackAt(profile?.LaunchFeedback, origin, attackRotation);
            }

            var profileImpactFeedback = !usesPresentationContract &&
                                        profile?.ImpactFeedback?.HasAnyFeedback == true
                ? profile.ImpactFeedback
                : null;
            MonsterMakerFeedbackDraft fallbackFeedback = null;
            ShowPreviewHitArea(profile, origin, forward, targetPosition, draft.AttackRange);

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
            var motionBreathDuration = profile != null && profile.UsesBreathDurationContract && currentAttack != null
                ? currentAttack.ResolveBreathDuration(profile.BreathDuration)
                : 0f;
            var repeatHitInterval = profile?.ResolveRepeatHitInterval(motionBreathDuration) ?? 0.08f;
            for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                var hitDamage = damage * (profile?.ResolveDamageRatio(hitIndex) ?? 1f);
                var playImpactFeedback = hitIndex == 0 || profile?.RepeatImpactFeedback != false;
                if (hitIndex == 0)
                {
                    var applied = ApplyPreviewDamage(
                        hitDamage,
                        ResolvePreviewDamageFeedbackFlags(profile));
                    if (applied)
                    {
                        var impactPosition = ResolvePreviewImpactPosition(profile, origin);
                        PlayContractVfx(
                            MonsterBasicAttackVfxEvent.TargetDamaged,
                            profile,
                            origin,
                            impactPosition,
                            targetPosition,
                            attackRotation,
                            null,
                            markerDraft.SocketOverride,
                            hitIndex);
                        if (profile?.Shape == MonsterBasicAttackShape.Circle)
                        {
                            PlayContractVfx(
                                MonsterBasicAttackVfxEvent.AreaResolved,
                                profile,
                                origin,
                                impactPosition,
                                profile.Center == MonsterBasicAttackCenter.Source ? origin : targetPosition,
                                attackRotation,
                                null,
                                markerDraft.SocketOverride,
                                hitIndex);
                        }
                        if (hitCount == 1)
                        {
                            PlayContractVfx(
                                MonsterBasicAttackVfxEvent.SequenceEnd,
                                profile,
                                origin,
                                impactPosition,
                                targetPosition,
                                attackRotation,
                                null,
                                markerDraft.SocketOverride,
                                hitIndex);
                        }
                    }
                    if (applied && playImpactFeedback)
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
                    previewClock + hitIndex * repeatHitInterval,
                    hitDamage,
                    fallbackFeedback,
                    profileImpactFeedback,
                    profile,
                    origin,
                    attackRotation,
                    playImpactFeedback,
                    hitIndex));
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

            ShowPreviewHitArea(profile, origin, forward, target, draft.AttackRange);
        }

        private void ShowPreviewHitArea(
            MonsterBasicAttackProfile profile,
            Vector3 origin,
            Vector3 forward,
            Vector3 target,
            float attackRange)
        {
            if (profile == null || stage.PreviewRoot == null)
            {
                return;
            }

            ClearPreviewHitAreas();
            var indicator = MonsterAttackAreaIndicator.Create(
                null,
                profile,
                origin,
                forward,
                target,
                attackRange,
                new Color(0.1f, 0.9f, 1f, 0.78f),
                false);
            if (indicator == null)
            {
                return;
            }

            stage.AddAuxiliary(indicator.gameObject);
            activeHitAreas.Add(indicator);
        }

        private void ShowPreviewHitArea(
            MonsterActiveAttackStep step,
            Vector3 origin,
            Vector3 forward,
            Vector3 target)
        {
            if (step == null || stage.PreviewRoot == null)
            {
                return;
            }

            ClearPreviewHitAreas();
            var indicator = MonsterAttackAreaIndicator.CreateActive(
                null,
                step,
                origin,
                forward,
                target,
                new Color(0.1f, 0.9f, 1f, 0.78f),
                false);
            if (indicator == null)
            {
                return;
            }

            stage.AddAuxiliary(indicator.gameObject);
            activeHitAreas.Add(indicator);
        }

        private void ClearPreviewHitAreas()
        {
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
            Quaternion rotation,
            float vfxLifetimeOverride = 0f)
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
            MonsterBasicAttackVfxPlayback.ApplyBrightnessScale(
                instance,
                MonsterBasicAttackVfxPlayback.DefaultMainBattleBrightnessScale);
            stage.AddAuxiliary(instance);
            MonsterBasicAttackVfxPlayback.RestartAtOffset(instance, 0f);
            activeVfx.Add(new PreviewVfx(
                instance,
                vfxLifetimeOverride > 0f ? vfxLifetimeOverride : feedback.VfxLifetime));
        }

        private void PlayFeelAt(
            BasicAttackFeelCue feel,
            Vector3 position,
            Quaternion rotation,
            GameObject target)
        {
            if (feel?.HasFeel != true || stage.PreviewRoot == null) return;
            var instance = UnityEngine.Object.Instantiate(feel.Prefab);
            instance.name = "[Active FEEL] " + feel.Prefab.name;
            position += rotation * feel.LocalPosition;
            rotation *= feel.LocalRotation;
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = feel.Prefab.transform.localScale *
                                            feel.Scale * Mathf.Max(0.01f, draft.VfxScale);
            MonsterBasicAttackVfxPlayback.ApplyBrightnessScale(
                instance,
                MonsterBasicAttackVfxPlayback.DefaultMainBattleBrightnessScale);
            stage.AddAuxiliary(instance);
            var runtime = instance.GetComponentsInChildren<MonoBehaviour>(true)
                .OfType<IBasicAttackFeelRuntime>()
                .FirstOrDefault();
            runtime?.PlayBasicAttackFeel(
                position,
                target,
                1f,
                BasicAttackFeelPlaybackOptions.None);
            activeVfx.Add(new PreviewVfx(instance, feel.Lifetime));
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
            MonsterBasicAttackVfxPlayback.ApplyBrightnessScale(
                instance,
                MonsterBasicAttackVfxPlayback.DefaultMainBattleBrightnessScale);
            stage.AddAuxiliary(instance);
            MonsterBasicAttackVfxPlayback.RestartAtOffset(instance, 0f);
            activeVfx.Add(new PreviewVfx(instance, feedback.VfxLifetime));
        }

        private bool PlayContractVfx(
            MonsterBasicAttackVfxEvent eventType,
            MonsterBasicAttackProfile profile,
            Vector3 origin,
            Vector3 hitPoint,
            Vector3 areaCenter,
            Quaternion rotation,
            GameObject projectile = null,
            string socketPath = null,
            int damageStage = 0,
            IReadOnlyList<MonsterBasicAttackVfxBinding> bindingsOverride = null,
            string motionIdOverride = null,
            float playbackSpeedMultiplier = 1f,
            int? sequenceIdOverride = null,
            string previewLabel = "기본공격",
            UnitActor targetActor = null)
        {
            if (profile == null || draft == null || stage.PreviewRoot == null)
            {
                return false;
            }

            var bindings = bindingsOverride ?? draft.BasicAttackVfxBindings;
            var motionId = motionIdOverride ?? currentAttack?.MotionId;
            var resolvedPlaybackSpeed = float.IsNaN(playbackSpeedMultiplier) ||
                                        float.IsInfinity(playbackSpeedMultiplier)
                ? 1f
                : Mathf.Max(0.05f, playbackSpeedMultiplier);
            var sequenceId = sequenceIdOverride ?? basicAttackVfxSequence;
            var played = false;
            foreach (var slot in profile.VfxSlots)
            {
                if (slot == null || slot.EventType != eventType ||
                    !TryResolvePreviewPresentation(
                        bindings,
                        profile.AttackId,
                        slot,
                        motionId,
                        out var binding))
                {
                    continue;
                }

                var claimSuffix = slot.Multiplicity switch
                {
                    MonsterBasicAttackVfxMultiplicity.PerProjectile =>
                        projectile == null ? "none" : projectile.GetInstanceID().ToString(),
                    MonsterBasicAttackVfxMultiplicity.PerTargetHit =>
                        $"{(targetActor == null ? dummyTargetActor?.GetInstanceID() ?? 0 : targetActor.GetInstanceID())}:{damageStage}",
                    MonsterBasicAttackVfxMultiplicity.PerDamageStage => damageStage.ToString(),
                    _ => "once"
                };

                var claimPrefix = $"{sequenceId}|{slot.SlotId}";
                if (binding.HasSound &&
                    basicAttackVfxClaims.Add($"{claimPrefix}|sfx|{claimSuffix}"))
                {
                    if (binding.Sound != null)
                    {
                        SfxEditorAudioPreview.Play(
                            binding.Sound,
                            0,
                            false,
                            binding.SoundVolume);
                        played = true;
                    }
                    else if (binding.Sfx != null &&
                             binding.Sfx.TrySelectClip(out var soundClip) &&
                             soundClip != null)
                    {
                        SfxEditorAudioPreview.Play(
                            soundClip,
                            0,
                            false,
                            binding.Sfx.SelectVolume());
                        played = true;
                    }
                }

                // 이동체 외형은 Preview Projectile이 직접 소유한다.
                if (slot.IsDeliveryVisual ||
                    binding.State != MonsterBasicAttackVfxAssignmentState.Assigned)
                {
                    continue;
                }

                var vfxClaim = $"{claimPrefix}|vfx|{claimSuffix}";
                var timingOffset = slot.ClampTimingOffset(binding.EventTimingOffset) /
                                   resolvedPlaybackSpeed;
                if (timingOffset < 0f &&
                    eventType == MonsterBasicAttackVfxEvent.RecipeExecute &&
                    basicAttackVfxClaims.Contains(vfxClaim))
                {
                    continue;
                }
                if (!basicAttackVfxClaims.Add(vfxClaim))
                {
                    continue;
                }
                if (timingOffset > 0f)
                {
                    pendingContractVfx.Add(new PendingContractVfx(
                        previewClock + timingOffset,
                        slot,
                        binding,
                        origin,
                        hitPoint,
                        areaCenter,
                        rotation,
                        projectile,
                        socketPath,
                        targetActor,
                        resolvedPlaybackSpeed,
                        sequenceId,
                        previewLabel));
                    played = true;
                    continue;
                }

                played |= PlayContractVfxInstance(
                    slot,
                    binding,
                    origin,
                    hitPoint,
                    areaCenter,
                    rotation,
                    projectile,
                    socketPath,
                    targetActor,
                    resolvedPlaybackSpeed,
                    sequenceId,
                    previewLabel);
            }
            return played;
        }

        private bool PlayContractVfxInstance(
            MonsterBasicAttackVfxSlot slot,
            MonsterBasicAttackVfxBinding binding,
            Vector3 origin,
            Vector3 hitPoint,
            Vector3 areaCenter,
            Quaternion rotation,
            GameObject projectile,
            string socketPath,
            UnitActor targetActor = null,
            float playbackSpeedMultiplier = 1f,
            int sequenceId = 0,
            string previewLabel = "기본공격")
        {
            if (slot == null || binding == null ||
                binding.State != MonsterBasicAttackVfxAssignmentState.Assigned ||
                stage.PreviewRoot == null)
            {
                return false;
            }

            ResolveContractAnchor(
                slot.Anchor,
                projectile,
                targetActor,
                socketPath,
                origin,
                hitPoint,
                areaCenter,
                rotation,
                out var anchor,
                out var position,
                out var resolvedRotation);
            position += resolvedRotation * binding.LocalPosition;
            resolvedRotation *= binding.LocalRotation;

            var isPlaceholder = binding.Prefab == null;
            var instance = isPlaceholder
                ? CreatePreviewVfxPlaceholder(
                    $"{previewLabel} · {slot.DisplayName}",
                    ToActivePresentationEvent(slot.EventType),
                    position,
                    resolvedRotation,
                    binding.Scale * Mathf.Max(0.01f, draft.VfxScale))
                : UnityEngine.Object.Instantiate(binding.Prefab);
            if (!isPlaceholder)
            {
                // 기본공격 Preview의 기존 진단·QA 식별자는 유지하고,
                // 공격 액티브처럼 명시된 다른 재생 소유자만 별도 이름을 사용한다.
                var instanceLabel = string.Equals(
                    previewLabel,
                    "기본공격",
                    StringComparison.Ordinal)
                    ? "Basic Attack"
                    : previewLabel;
                instance.name = $"[{instanceLabel} VFX] {slot.DisplayName}";
                instance.transform.SetPositionAndRotation(position, resolvedRotation);
                MonsterBasicAttackVfxPlayback.ApplyInstanceScale(
                    instance,
                    binding.Prefab.transform.localScale *
                    binding.Scale * Mathf.Max(0.01f, draft.VfxScale));
                MonsterBasicAttackVfxPlayback.ApplyBrightnessScale(
                    instance,
                    MonsterBasicAttackVfxPlayback.DefaultMainBattleBrightnessScale);
            }
            stage.AddAuxiliary(instance);
            if (slot.Attachment == MonsterBasicAttackVfxAttachment.FollowAnchor && anchor != null)
            {
                instance.transform.SetParent(anchor, true);
            }
            if (!isPlaceholder)
            {
                // AddSingleGO가 Preview Scene으로 옮긴 다음에 Vendor 파티클을 시작해야 한다.
                MonsterBasicAttackVfxPlayback.RestartAtOffset(
                    instance,
                    binding.PlaybackOffset,
                    playbackSpeed: binding.PlaybackSpeed * playbackSpeedMultiplier);
            }
            activeVfx.Add(new PreviewVfx(
                instance,
                binding.Lifetime / Mathf.Max(0.05f, playbackSpeedMultiplier),
                slot.EndPolicy,
                projectile,
                isPlaceholder,
                binding.PlaybackOffset,
                binding.PlaybackSpeed * playbackSpeedMultiplier,
                sequenceId));
            return true;
        }

        private static bool TryResolvePreviewPresentation(
            IReadOnlyList<MonsterBasicAttackVfxBinding> bindings,
            string attackId,
            MonsterBasicAttackVfxSlot slot,
            string motionId,
            out MonsterBasicAttackVfxBinding binding)
        {
            var hasPresentation = MonsterBasicAttackVfxResolver.TryResolvePresentation(
                bindings,
                attackId,
                slot,
                motionId,
                out binding);
            return hasPresentation ||
                   binding?.State == MonsterBasicAttackVfxAssignmentState.Assigned;
        }

        private void ScheduleRecipeLeadContractVfx(MonsterBasicAttackProfile profile)
        {
            if (profile == null || currentClip == null || markerBuffer.Length == 0 ||
                draft == null || stage.PreviewRoot == null)
            {
                return;
            }

            var markerIndex = Mathf.Clamp(nextMarkerIndex, 0, markerBuffer.Length - 1);
            var markerDelay =
                currentClip.length * markerBuffer[markerIndex].NormalizedTime /
                Mathf.Max(0.01f, playbackSpeed);
            var socketPath = markerIndex < markerDraftBuffer.Length
                ? markerDraftBuffer[markerIndex]?.SocketOverride
                : null;
            var originSocket = ResolvePreviewSocket(socketPath);
            var origin = originSocket == null
                ? stage.PreviewRoot.transform.position
                : originSocket.position;
            var targetPosition = ResolveCombatTargetHitPoint();
            var forward = targetPosition - origin;
            forward.y = 0f;
            var rotation = Quaternion.LookRotation(
                forward.sqrMagnitude < 0.0001f ? stage.PreviewRoot.transform.forward : forward.normalized,
                Vector3.up);

            foreach (var slot in profile.VfxSlots)
            {
                if (slot == null ||
                    slot.EventType != MonsterBasicAttackVfxEvent.RecipeExecute ||
                    slot.IsDeliveryVisual ||
                    !TryResolvePreviewPresentation(
                        draft.BasicAttackVfxBindings,
                        profile.AttackId,
                        slot,
                        currentAttack?.MotionId,
                        out var binding) ||
                    binding.State != MonsterBasicAttackVfxAssignmentState.Assigned)
                {
                    continue;
                }

                var timingOffset = slot.ClampTimingOffset(binding.EventTimingOffset);
                var claim = $"{basicAttackVfxSequence}|{slot.SlotId}|vfx|once";
                if (timingOffset >= 0f || !basicAttackVfxClaims.Add(claim))
                {
                    continue;
                }

                var executeAt = previewClock + Mathf.Max(0f, markerDelay + timingOffset);
                if (executeAt <= previewClock + 0.0001f)
                {
                    PlayContractVfxInstance(
                        slot,
                        binding,
                        origin,
                        targetPosition,
                        targetPosition,
                        rotation,
                        null,
                        socketPath);
                    continue;
                }

                pendingContractVfx.Add(new PendingContractVfx(
                    executeAt,
                    slot,
                    binding,
                    origin,
                    targetPosition,
                    targetPosition,
                    rotation,
                    null,
                    socketPath));
            }
        }

        private void TickPendingContractVfx()
        {
            for (var index = pendingContractVfx.Count - 1; index >= 0; index--)
            {
                var pending = pendingContractVfx[index];
                if (previewClock + 0.0001f < pending.ExecuteAt)
                {
                    continue;
                }

                pendingContractVfx.RemoveAt(index);
                PlayContractVfxInstance(
                    pending.Slot,
                    pending.Binding,
                    pending.Origin,
                    pending.HitPoint,
                    pending.AreaCenter,
                    pending.Rotation,
                    pending.Projectile,
                    pending.SocketPath,
                    pending.TargetActor,
                    pending.PlaybackSpeedMultiplier,
                    pending.SequenceId,
                    pending.PreviewLabel);
            }
        }

        private void ResolveContractAnchor(
            MonsterBasicAttackVfxAnchor anchorKind,
            GameObject projectile,
            UnitActor targetActor,
            string socketPath,
            Vector3 origin,
            Vector3 hitPoint,
            Vector3 areaCenter,
            Quaternion rotation,
            out Transform anchor,
            out Vector3 position,
            out Quaternion resolvedRotation)
        {
            anchor = null;
            position = origin;
            resolvedRotation = rotation;
            switch (anchorKind)
            {
                case MonsterBasicAttackVfxAnchor.SourceRoot:
                    anchor = stage.PreviewRoot.transform;
                    break;
                case MonsterBasicAttackVfxAnchor.AttackOrigin:
                    anchor = ResolvePreviewSocket(draft.AttackOriginPath);
                    break;
                case MonsterBasicAttackVfxAnchor.MarkerSocket:
                    anchor = ResolvePreviewSocket(socketPath);
                    break;
                case MonsterBasicAttackVfxAnchor.ProjectileRoot:
                    anchor = projectile?.transform;
                    break;
                case MonsterBasicAttackVfxAnchor.TargetRoot:
                    anchor = targetActor == null ? dummyTarget?.transform : targetActor.transform;
                    break;
                case MonsterBasicAttackVfxAnchor.HitPoint:
                    position = hitPoint;
                    return;
                case MonsterBasicAttackVfxAnchor.AreaCenter:
                    position = areaCenter;
                    return;
                case MonsterBasicAttackVfxAnchor.TrajectoryOrigin:
                    anchor = ResolvePreviewSocket(draft.AttackOriginPath) ?? stage.PreviewRoot.transform;
                    position = origin;
                    return;
            }
            if (anchor != null)
            {
                position = anchor.position;
                resolvedRotation = anchor.rotation;
            }
        }

        private void ReleaseContractVfx(
            MonsterBasicAttackVfxEndPolicy policy,
            GameObject projectile,
            int? sequenceId = null)
        {
            for (var index = pendingContractVfx.Count - 1; index >= 0; index--)
            {
                var pending = pendingContractVfx[index];
                if (pending.Slot.EndPolicy != policy ||
                    sequenceId.HasValue && pending.SequenceId != sequenceId.Value ||
                    policy == MonsterBasicAttackVfxEndPolicy.DeliveryEnd &&
                    pending.Projectile != projectile)
                {
                    continue;
                }
                pendingContractVfx.RemoveAt(index);
            }

            for (var index = activeVfx.Count - 1; index >= 0; index--)
            {
                var vfx = activeVfx[index];
                if (vfx.EndPolicy != policy ||
                    sequenceId.HasValue && vfx.SequenceId != sequenceId.Value ||
                    policy == MonsterBasicAttackVfxEndPolicy.DeliveryEnd &&
                    vfx.Delivery != projectile)
                {
                    continue;
                }
                if (vfx.Instance != null)
                {
                    stage.RemoveAuxiliary(vfx.Instance);
                }
                activeVfx.RemoveAt(index);
            }
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

        private void SpawnPreviewProjectile(
            MonsterMakerMarkerDraft markerDraft,
            float damage,
            MonsterMakerFeedbackDraft impactFeedback,
            MonsterFeedbackCue profileImpactFeedback,
            MonsterBasicAttackProfile profile)
        {
            var usesPresentationContract = profile?.VfxSlots.Count > 0;
            var projectilePresentation = usesPresentationContract ? null : profile?.ProjectileFeedback;
            var hasDeliveryVisual = TryResolvePreviewDeliveryVisual(
                profile,
                draft?.BasicAttackVfxBindings,
                currentAttack?.MotionId,
                out var deliverySlot,
                out var deliveryBinding);
            var projectileVisual = hasDeliveryVisual
                ? deliveryBinding.Prefab
                : projectilePresentation?.VfxPrefab != null
                    ? projectilePresentation.VfxPrefab
                    : draft?.ProjectilePrefab;
            if (projectileVisual == null)
            {
                projectileVisual = AssetDatabase.LoadAssetAtPath<GameObject>(
                    MonsterMakerAssetWriter.DefaultProjectilePrefabPath);
            }
            var carrierOnly = usesPresentationContract && !hasDeliveryVisual;
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
                var shotDirection = profile == null
                    ? direction
                    : profile.ResolveProjectileDirection(direction, index);
                var rotation = Quaternion.LookRotation(shotDirection, Vector3.up);
                var spawnPosition = origin;
                if (hasDeliveryVisual)
                {
                    spawnPosition += rotation * deliveryBinding.LocalPosition;
                    rotation *= deliveryBinding.LocalRotation;
                }
                else if (projectilePresentation != null)
                {
                    spawnPosition += rotation * projectilePresentation.LocalPosition;
                    rotation *= projectilePresentation.LocalRotation;
                }
                var isPlaceholder = hasDeliveryVisual && deliveryBinding.Prefab == null;
                var instance = isPlaceholder
                    ? CreatePreviewVfxPlaceholder(
                        $"기본공격 · {deliverySlot.DisplayName}",
                        MonsterActivePresentationEvent.DeliverySpawn,
                        spawnPosition,
                        rotation,
                        deliveryBinding.Scale * Mathf.Max(0.01f, draft.VfxScale))
                    : UnityEngine.Object.Instantiate(projectileVisual);
                if (!isPlaceholder)
                {
                    instance.name = carrierOnly
                        ? "[Monster Preview 판정 이동체 · VFX 미배정] " + projectileVisual.name
                        : "[Monster Preview Projectile] " + projectileVisual.name;
                    instance.transform.SetPositionAndRotation(spawnPosition, rotation);
                }
                if (!isPlaceholder)
                {
                    var presentationScale = hasDeliveryVisual
                        ? deliveryBinding.Scale
                        : projectilePresentation?.Scale ?? 1f;
                    MonsterBasicAttackVfxPlayback.ApplyInstanceScale(
                        instance,
                        projectileVisual.transform.localScale *
                        presentationScale *
                        Mathf.Max(0.01f, draft.VfxScale));
                    MonsterBasicAttackVfxPlayback.ApplyBrightnessScale(
                        instance,
                        MonsterBasicAttackVfxPlayback.DefaultMainBattleBrightnessScale);
                    if (carrierOnly)
                    {
                        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                            renderer.enabled = false;
                        MonsterBasicAttackVfxPlayback.StopAndClear(instance);
                    }
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
                if (!isPlaceholder && !carrierOnly)
                {
                    // Preview Scene 등록 뒤 재생해야 씬 이동 중 Play 상태가 사라지지 않는다.
                    MonsterBasicAttackVfxPlayback.RestartAtOffset(
                        instance,
                        hasDeliveryVisual ? deliveryBinding.PlaybackOffset : 0f,
                        playbackSpeed: hasDeliveryVisual ? deliveryBinding.PlaybackSpeed : 1f);
                }
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
                    index == projectileCount / 2,
                    isPlaceholder,
                    hasDeliveryVisual ? deliveryBinding.PlaybackOffset : 0f,
                    hasDeliveryVisual ? deliveryBinding.PlaybackSpeed : 1f));
                PlayContractVfx(
                    MonsterBasicAttackVfxEvent.DeliverySpawn,
                    profile,
                    origin,
                    targetPosition,
                    targetPosition,
                    rotation,
                    instance,
                    markerDraft?.SocketOverride);
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
            }
            combatStatus = "투사체 이동 중";
        }

        private static bool TryResolvePreviewDeliveryVisual(
            MonsterBasicAttackProfile profile,
            IReadOnlyList<MonsterBasicAttackVfxBinding> bindings,
            string motionId,
            out MonsterBasicAttackVfxSlot slot,
            out MonsterBasicAttackVfxBinding binding)
        {
            slot = null;
            binding = null;
            if (profile == null) return false;
            for (var index = 0; index < profile.VfxSlots.Count; index++)
            {
                var candidate = profile.VfxSlots[index];
                if (candidate == null || !candidate.IsDeliveryVisual ||
                    !TryResolvePreviewPresentation(bindings, profile.AttackId, candidate, motionId,
                        out var resolved) ||
                    resolved.State != MonsterBasicAttackVfxAssignmentState.Assigned)
                {
                    continue;
                }
                slot = candidate;
                binding = resolved;
                return true;
            }
            return false;
        }

        private bool ApplyPreviewDamage(
            float damage,
            DamageFeedbackFlags feedbackFlags = DamageFeedbackFlags.None)
        {
            return ApplyPreviewDamage(dummyTargetActor, damage, feedbackFlags);
        }

        private bool ApplyPreviewDamage(
            UnitActor target,
            float damage,
            DamageFeedbackFlags feedbackFlags = DamageFeedbackFlags.None)
        {
            if (target == null || target.Health == null)
            {
                combatStatus = "표준 적을 준비하지 못했습니다";
                return false;
            }

            if (damage <= 0f)
            {
                combatStatus = "공격력 0 · 피해 없음";
                return false;
            }

            if (!target.Health.IsAlive)
            {
                if (target == dummyTargetActor) ResetCombatTarget();
                if (!target.Health.IsAlive) return false;
            }

            var hitPoint = ResolveCombatTargetHitPoint(target);
            if (!target.Health.ApplyDamage(new DamageRequest(null, damage, hitPoint, false, feedbackFlags)))
            {
                combatStatus = "피해 적용 실패";
                return false;
            }

            return true;
        }

        private Vector3 ResolveCombatTargetHitPoint()
        {
            return ResolveCombatTargetHitPoint(dummyTargetActor);
        }

        private static Vector3 ResolveCombatTargetHitPoint(UnitActor target)
        {
            if (target == null)
            {
                return Vector3.zero;
            }

            return TryResolveRenderBounds(target.gameObject, out var bounds)
                ? bounds.center + Vector3.up * 0.05f
                : target.transform.position + Vector3.up * 0.8f;
        }

        private void HandlePreviewHit(UnitActor target, DamageReport report)
        {
            target?.VisualFeedback?.PlayHit();
            targetFeedbackActive = true;
            lastAppliedDamage = report.AppliedDamage;
            previewHitCount++;
            combatStatus = $"타격 {previewHitCount}회 · 피해 {Mathf.RoundToInt(report.AppliedDamage):N0}";
            var separateNumber =
                (report.Request.FeedbackFlags & DamageFeedbackFlags.SeparateFloatingNumber) != 0;
            QueueFloatingNumber(
                report.Request.HitPoint,
                report.AppliedDamage,
                FloatingNumberStyle.EnemyDamage,
                target == null ? 0 : target.GetInstanceID(),
                separateNumber);
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
            int mergeKey,
            bool separate = false)
        {
            if (amount <= 0f)
            {
                return;
            }

            if (mergeKey == 0)
            {
                mergeKey = dummyTarget == null ? 1 : dummyTarget.GetInstanceID();
            }

            if (separate)
            {
                SpawnFloatingNumber(new PendingFloatingNumber
                {
                    Active = true,
                    MergeKey = int.MinValue + previewHitCount,
                    Position = position,
                    Amount = amount,
                    Style = style,
                    ReleaseAt = previewClock
                });
                return;
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
            SpawnFloatingNumber(request);
        }

        private void QueueFloatingText(
            Vector3 position,
            string text,
            CombatStatusTextStyle style,
            int queueKey)
        {
            text = text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (queueKey == 0)
            {
                queueKey = floatingTextUniqueKey++;
                if (floatingTextUniqueKey >= 0)
                {
                    floatingTextUniqueKey = int.MinValue;
                }
            }

            if (recentFloatingTexts.TryGetValue(queueKey, out var recent) &&
                recent.Value == text &&
                previewClock - recent.QueuedAt < FloatingNumberPresenter.StatusDuplicateWindow)
            {
                return;
            }

            var releaseAt = nextFloatingTextReleaseAt.TryGetValue(queueKey, out var next)
                ? Mathf.Max(previewClock, next)
                : previewClock;
            if (releaseAt - previewClock > FloatingNumberPresenter.StatusMaximumQueueDelay)
            {
                return;
            }

            pendingFloatingTexts.Add(new PendingFloatingText
            {
                Position = position,
                Value = text,
                Style = style,
                ReleaseAt = releaseAt,
                QueuedAt = previewClock,
                QueueKey = queueKey
            });
            nextFloatingTextReleaseAt[queueKey] =
                releaseAt + FloatingNumberPresenter.StatusQueueInterval;
            recentFloatingTexts[queueKey] = new RecentFloatingText(text, previewClock);
        }

        private void FlushFloatingTexts()
        {
            for (var index = pendingFloatingTexts.Count - 1; index >= 0; index--)
            {
                var request = pendingFloatingTexts[index];
                if (request.ReleaseAt > previewClock)
                {
                    continue;
                }

                pendingFloatingTexts.RemoveAt(index);
                if (previewClock - request.QueuedAt <= FloatingNumberPresenter.StatusMaximumQueueDelay)
                {
                    SpawnFloatingText(request);
                }
            }
        }

        private void SpawnFloatingNumber(PendingFloatingNumber request)
        {
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

        private void SpawnFloatingText(PendingFloatingText request)
        {
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
                request.QueueKey,
                floatingNumberSequence,
                FloatingNumberPresenter.DefaultHorizontalDrift * 0.14f);
            instance.name = "[Monster Preview Status] " + request.Value;
            instance.transform.position = request.Position +
                                          Vector3.up * FloatingNumberPresenter.DefaultHeightOffset;
            instance.SetActive(true);
            view.Play(
                null,
                request.Value,
                FloatingNumberPresenter.ResolveStatusColor(request.Style),
                0.72f,
                0.58f,
                signedDrift,
                0.08f,
                0f,
                0.86f,
                stage.Camera,
                null);
            view.GetComponent<TMPro.TMP_Text>()?.ForceMeshUpdate(true, true);
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
                pendingFloatingTexts.Count > 0 ||
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
            FlushFloatingTexts();
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

            return targetPulseActive || pendingFloatingNumber.Active || pendingFloatingTexts.Count > 0 ||
                   activeFloatingNumbers.Count > 0 ||
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

                var applied = ApplyPreviewDamage(
                    pending.Damage,
                    ResolvePreviewDamageFeedbackFlags(pending.Profile));
                if (applied)
                {
                    var impactPosition = ResolvePreviewImpactPosition(pending.Profile, pending.Origin);
                    PlayContractVfx(
                        MonsterBasicAttackVfxEvent.TargetDamaged,
                        pending.Profile,
                        pending.Origin,
                        impactPosition,
                        ResolveCombatTargetHitPoint(),
                        pending.Rotation,
                        damageStage: pending.HitIndex);
                    if (pending.HitIndex == (pending.Profile?.HitCount ?? 1) - 1)
                    {
                        PlayContractVfx(
                            MonsterBasicAttackVfxEvent.SequenceEnd,
                            pending.Profile,
                            pending.Origin,
                            impactPosition,
                            ResolveCombatTargetHitPoint(),
                            pending.Rotation,
                            damageStage: pending.HitIndex);
                    }
                }
                if (applied && pending.PlayFeedback)
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
                            PlayContractVfx(
                                MonsterBasicAttackVfxEvent.DeliveryTurn,
                                projectile.Profile,
                                projectile.Origin,
                                projectile.TargetPosition,
                                projectile.TargetPosition,
                                projectile.Instance.transform.rotation,
                                projectile.Instance);
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

                // 투사체도 다른 계약 VFX와 같은 절대 시간으로 샘플링합니다. 일부 Vendor
                // 파티클은 작은 delta 누적 Simulate에서 첫 Emission이 유실될 수 있습니다.
                MonsterBasicAttackVfxPlayback.SimulateAtTime(
                    projectile.Instance,
                    projectile.Elapsed,
                    projectile.PlaybackOffset,
                    projectile.PlaybackSpeed);
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
            if (ApplyPreviewDamage(
                    projectile.Damage * ratio,
                    ResolvePreviewDamageFeedbackFlags(projectile.Profile)))
            {
                var rotation = projectile.Instance == null
                    ? Quaternion.identity
                    : projectile.Instance.transform.rotation;
                PlayContractVfx(
                    MonsterBasicAttackVfxEvent.TargetDamaged,
                    projectile.Profile,
                    projectile.Origin,
                    position,
                    position,
                    rotation,
                    projectile.Instance,
                    damageStage: passIndex);
                if (projectile.Profile?.SequenceModule == MonsterBasicAttackSequenceModule.ReturnPasses)
                {
                    PlayContractVfx(
                        passIndex == 0
                            ? MonsterBasicAttackVfxEvent.OutboundTargetDamaged
                            : MonsterBasicAttackVfxEvent.ReturnTargetDamaged,
                        projectile.Profile,
                        projectile.Origin,
                        position,
                        position,
                        rotation,
                        projectile.Instance,
                        damageStage: passIndex);
                }
                if (projectile.Profile?.CollisionModule == MonsterBasicAttackCollisionModule.AreaImpact)
                {
                    PlayContractVfx(
                        MonsterBasicAttackVfxEvent.AreaResolved,
                        projectile.Profile,
                        projectile.Origin,
                        position,
                        position,
                        rotation,
                        projectile.Instance,
                        damageStage: passIndex);
                }
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

        private static DamageFeedbackFlags ResolvePreviewDamageFeedbackFlags(
            MonsterBasicAttackProfile profile)
        {
            return (profile?.HitCount ?? 1) > 1
                ? DamageFeedbackFlags.SeparateFloatingNumber
                : DamageFeedbackFlags.None;
        }

        private void RemovePreviewProjectile(int index, PreviewProjectile projectile)
        {
            if (projectile.Instance != null)
            {
                PlayContractVfx(
                    MonsterBasicAttackVfxEvent.DeliveryEnd,
                    projectile.Profile,
                    projectile.Origin,
                    projectile.Instance.transform.position,
                    projectile.Instance.transform.position,
                    projectile.Instance.transform.rotation,
                    projectile.Instance);
                ReleaseContractVfx(
                    MonsterBasicAttackVfxEndPolicy.DeliveryEnd,
                    projectile.Instance);
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

        private static MonsterActivePresentationEvent ToActivePresentationEvent(
            MonsterBasicAttackVfxEvent eventType) => eventType switch
        {
            MonsterBasicAttackVfxEvent.MotionStart => MonsterActivePresentationEvent.MotionStart,
            MonsterBasicAttackVfxEvent.RecipeExecute => MonsterActivePresentationEvent.Launch,
            MonsterBasicAttackVfxEvent.DeliverySpawn => MonsterActivePresentationEvent.DeliverySpawn,
            MonsterBasicAttackVfxEvent.DeliveryTurn => MonsterActivePresentationEvent.Travel,
            MonsterBasicAttackVfxEvent.DeliveryEnd => MonsterActivePresentationEvent.DeliveryEnd,
            MonsterBasicAttackVfxEvent.AreaResolved => MonsterActivePresentationEvent.AreaResolved,
            MonsterBasicAttackVfxEvent.SequenceEnd or MonsterBasicAttackVfxEvent.MotionEnd =>
                MonsterActivePresentationEvent.StepEnd,
            _ => MonsterActivePresentationEvent.Impact
        };

        private static GameObject CreatePreviewVfxPlaceholder(
            string label,
            MonsterActivePresentationEvent eventType,
            Vector3 position,
            Quaternion rotation,
            float scale)
        {
            var root = new GameObject($"[Preview VFX Placeholder] {label}")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            root.transform.SetPositionAndRotation(position, rotation);
            root.transform.localScale = Vector3.one * Mathf.Max(0.05f, scale);
            var color = PreviewPlaceholderColor(eventType);
            AddPreviewPlaceholderPrimitive(root.transform, PrimitiveType.Sphere, Vector3.zero,
                new Vector3(0.34f, 0.34f, 0.34f), color);
            AddPreviewPlaceholderPrimitive(root.transform, PrimitiveType.Cube, Vector3.zero,
                new Vector3(0.8f, 0.07f, 0.07f), color);
            AddPreviewPlaceholderPrimitive(root.transform, PrimitiveType.Cube, Vector3.zero,
                new Vector3(0.07f, 0.8f, 0.07f), color);
            AddPreviewPlaceholderPrimitive(root.transform, PrimitiveType.Cube, Vector3.zero,
                new Vector3(0.07f, 0.07f, 0.8f), color);
            return root;
        }

        private static void AddPreviewPlaceholderPrimitive(
            Transform parent,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Color color)
        {
            var child = GameObject.CreatePrimitive(primitiveType);
            child.name = "Preview Timing Marker";
            child.hideFlags = HideFlags.HideAndDontSave;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = localScale;
            var collider = child.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            var renderer = child.GetComponent<Renderer>();
            if (renderer == null) return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            block.SetColor("_EmissionColor", color * 1.5f);
            renderer.SetPropertyBlock(block);
        }

        private static Color PreviewPlaceholderColor(MonsterActivePresentationEvent eventType) => eventType switch
        {
            MonsterActivePresentationEvent.Telegraph => new Color(0.2f, 0.9f, 1f, 1f),
            MonsterActivePresentationEvent.MotionStart => new Color(0.35f, 0.72f, 1f, 1f),
            MonsterActivePresentationEvent.Launch or MonsterActivePresentationEvent.DeliverySpawn =>
                new Color(1f, 0.78f, 0.18f, 1f),
            MonsterActivePresentationEvent.Travel => new Color(0.95f, 0.48f, 1f, 1f),
            MonsterActivePresentationEvent.Impact or MonsterActivePresentationEvent.AreaResolved =>
                new Color(1f, 0.3f, 0.2f, 1f),
            MonsterActivePresentationEvent.DashExit or MonsterActivePresentationEvent.DashEnter =>
                new Color(0.62f, 0.38f, 1f, 1f),
            _ => new Color(0.4f, 1f, 0.62f, 1f)
        };

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

                var elapsedDelta = Mathf.Max(0f, deltaTime);
                vfx.Elapsed += elapsedDelta;
                if (vfx.IsActiveDelivery)
                {
                    var ratio = vfx.DeliveryDuration <= 0f
                        ? 1f
                        : Mathf.Clamp01(vfx.Elapsed / vfx.DeliveryDuration);
                    if (vfx.DeliveryReturns)
                    {
                        ratio = ratio <= 0.5f ? ratio * 2f : (1f - ratio) * 2f;
                    }
                    vfx.Instance.transform.position = Vector3.Lerp(vfx.DeliveryStart, vfx.DeliveryEnd, ratio);
                }
                if (vfx.IsPlaceholder)
                {
                    var pulse = 1f + Mathf.Sin(vfx.Elapsed * 12f) * 0.12f;
                    vfx.Instance.transform.localScale = vfx.BaseScale * pulse;
                }
                // Vendor 자식·SubEmitter는 매 프레임 Clear 후 절대시간 재탐색하면
                // 준비 상태만 반복되어 Renderer Bounds가 0으로 남을 수 있다.
                // 생성 시 RestartAtOffset으로 한 번 맞추고 이후에는 연속 시간만 전진시킨다.
                MonsterBasicAttackVfxPlayback.Simulate(vfx.Instance, elapsedDelta);

                if (vfx.ActiveEndPolicy is MonsterActivePresentationEndPolicy.DeliveryEnd or
                    MonsterActivePresentationEndPolicy.StepEnd or
                    MonsterActivePresentationEndPolicy.MotionEnd)
                {
                    continue;
                }
                if (vfx.EndPolicy is MonsterBasicAttackVfxEndPolicy.MotionEnd or
                    MonsterBasicAttackVfxEndPolicy.DeliveryEnd)
                {
                    continue;
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

            var previewForward = stage.PreviewRoot.transform.forward;
            var attackerExtent = ResolveDirectionalExtent(
                stage.PreviewRoot,
                stage.PreviewRoot.transform.position,
                previewForward);
            var minimumDistance = draft.CombatType == MonsterCombatType.Ranged
                ? RangedCombatTargetMinimumDistance
                : CombatTargetMinimumDistance;
            var allAnimators = new List<Animator>();
            var targetCount = ResolvePreviewTargetCount();
            for (var index = 0; index < targetCount; index++)
            {
                var instance = UnityEngine.Object.Instantiate(prefab);
                instance.name = $"[Monster Preview Target {index + 1}] PF_Enemy_Peasant";
                instance.transform.SetPositionAndRotation(
                    stage.PreviewRoot.transform.position,
                    Quaternion.LookRotation(-previewForward, stage.PreviewRoot.transform.up));

                var appearance = instance.GetComponent<ModularEnemyAppearance>();
                var unitId = $"monster_maker_target_{index + 1}";
                var appearanceReady = appearance != null && appearance.PrepareForSpawn(new UnitSpawnRequest(
                    unitId,
                    default,
                    UnitTeam.Enemy,
                    false,
                    false,
                    appearanceSeed: CombatTargetAppearanceSeed + index));
                var health = instance.GetComponent<HealthComponent>();
                var visual = instance.GetComponent<UnitVisualFeedback>();
                var actor = instance.GetComponent<UnitActor>();
                if (!appearanceReady || health == null || visual == null || actor == null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                    continue;
                }

                var targetExtent = ResolveDirectionalExtent(instance, instance.transform.position, -previewForward);
                var targetDistance = Mathf.Max(
                    minimumDistance,
                    draft.AttackRange,
                    attackerExtent + targetExtent + CombatTargetVisualGap);
                var sideIndex = (index + 1) / 2;
                var sideSign = index == 0 ? 0f : index % 2 == 1 ? 1f : -1f;
                var lateralOffset = Vector3.Cross(Vector3.up, previewForward).normalized *
                                    (sideSign * sideIndex * 0.72f);
                var depthOffset = index == 0 ? 0f : sideIndex * 0.22f;
                instance.transform.position = stage.PreviewRoot.transform.position +
                                              previewForward * (targetDistance + depthOffset) +
                                              lateralOffset;

                visual.RefreshRenderers();
                actor.EditorConfigureReferences(health, visual);
                var targetStats = new UnitStatsSnapshot
                {
                    maxHealth = CombatTargetMaxHealth,
                    attackInterval = 1f
                };
                actor.Initialize(
                    new UnitSpawnRequest(
                        unitId,
                        targetStats,
                        UnitTeam.Enemy,
                        false,
                        false,
                        appearanceSeed: CombatTargetAppearanceSeed + index,
                        displayName: $"표준 적 {index + 1}"),
                    null,
                    combatFeedbackPlayer);
                var animators = instance.GetComponentsInChildren<Animator>(true);
                allAnimators.AddRange(animators);
                stage.AddAuxiliary(instance);
                dummyTargets.Add(new PreviewTarget(instance, actor, health, visual));
            }

            if (dummyTargets.Count == 0)
            {
                combatStatus = "표준 적 외형 또는 전투 부품 조립에 실패했습니다";
                return;
            }

            var primary = dummyTargets[0];
            dummyTarget = primary.Instance;
            dummyTargetActor = primary.Actor;
            dummyTargetHealth = primary.Health;
            dummyTargetVisualFeedback = primary.VisualFeedback;
            dummyTargetAnimators = allAnimators.ToArray();
            combatStatus = "공격 버튼을 눌러 실제 타격을 확인하세요";
        }

        private void PositionCombatTargetsForActivePreview()
        {
            activePreviewTargetPositions.Clear();
            activePreviewTargetRotations.Clear();
            var profile = draft?.ActiveAttackProfile;
            var root = stage.PreviewRoot?.transform;
            var firstStep = profile?.Steps.FirstOrDefault(step => step != null);
            if (root == null || firstStep == null || dummyTargets.Count == 0) return;

            var forward = root.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
            var reach = firstStep.Pattern switch
            {
                MonsterActiveAttackPattern.SelfCircle => firstStep.Radius * 0.65f,
                MonsterActiveAttackPattern.FrontCircle => firstStep.ForwardOffset,
                MonsterActiveAttackPattern.TargetCircle => firstStep.Range * 0.7f,
                _ => firstStep.Range * 0.7f
            };
            var primaryDistance = Mathf.Max(0.8f, reach);
            var side = Vector3.Cross(Vector3.up, forward).normalized;
            for (var index = 0; index < dummyTargets.Count; index++)
            {
                var target = dummyTargets[index].Instance;
                if (target == null) continue;
                activePreviewTargetPositions.Add(target.transform.position);
                activePreviewTargetRotations.Add(target.transform.rotation);
                var sideIndex = (index + 1) / 2;
                var sideSign = index == 0 ? 0f : index % 2 == 1 ? 1f : -1f;
                var lateral = side * (sideSign * sideIndex * 0.72f);
                var depth = index == 0 ? 0f : sideIndex * 0.22f;
                target.transform.SetPositionAndRotation(
                    root.position + forward * (primaryDistance + depth) + lateral,
                    Quaternion.LookRotation(-forward, Vector3.up));
            }
        }

        private void RestoreCombatTargetsAfterActivePreview()
        {
            if (activePreviewTargetPositions.Count == 0)
            {
                activePreviewTargetRotations.Clear();
                return;
            }
            var count = Mathf.Min(
                dummyTargets.Count,
                Mathf.Min(activePreviewTargetPositions.Count, activePreviewTargetRotations.Count));
            for (var index = 0; index < count; index++)
            {
                var target = dummyTargets[index].Instance;
                if (target == null) continue;
                target.transform.SetPositionAndRotation(
                    activePreviewTargetPositions[index],
                    activePreviewTargetRotations[index]);
            }
            activePreviewTargetPositions.Clear();
            activePreviewTargetRotations.Clear();
        }

        private int ResolvePreviewTargetCount()
        {
            if (draft?.UseActiveSkill != true) return 1;
            var attackProfile = draft.ActiveAttackProfile;
            if (attackProfile != null)
            {
                var count = attackProfile.Steps.Count == 0
                    ? 1
                    : attackProfile.Steps.Max(step => step?.MaxTargets ?? 1);
                if (attackProfile.Steps.Any(step =>
                    step?.TargetPolicy == MonsterActiveTargetPolicy.DifferentTarget))
                {
                    count = Mathf.Max(2, count);
                }
                return Mathf.Clamp(count, 1, 3);
            }

            var effectProfile = draft.ActiveEffectProfile;
            if (effectProfile == null || effectProfile.Groups.Count == 0) return 1;
            var effectCount = effectProfile.Groups.Max(group => group?.MaxTargets ?? 1);
            if (effectProfile.Groups.Any(group => group != null &&
                    (group.Target is MonsterSkillTargetType.NearbyAllies or
                        MonsterSkillTargetType.AllAllies or
                        MonsterSkillTargetType.TargetAreaEnemies)))
            {
                effectCount = Mathf.Max(2, effectCount);
            }
            return Mathf.Clamp(effectCount, 1, 3);
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
            ClearActiveStepBlendPose();
            if (activeSkillPreviewRunning && stage.PreviewRoot != null)
            {
                stage.PreviewRoot.transform.SetPositionAndRotation(
                    activePreviewOrigin,
                    activePreviewRotation);
            }
            RestoreCombatTargetsAfterActivePreview();
            activeSkillPreviewRunning = false;
            pendingActiveEvents.Clear();
            pendingEffectPreviewGroups.Clear();
            basicAttackVfxClaims.Clear();
            SfxEditorAudioPreview.StopAll();
            for (var index = activeVfx.Count - 1; index >= 0; index--)
            {
                if (activeVfx[index].Instance != null)
                {
                    stage.RemoveAuxiliary(activeVfx[index].Instance);
                }
            }

            activeVfx.Clear();
            pendingContractVfx.Clear();
            for (var index = activePreviewAttackBlocks.Count - 1; index >= 0; index--)
            {
                if (activePreviewAttackBlocks[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(activePreviewAttackBlocks[index]);
                }
            }
            activePreviewAttackBlocks.Clear();
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

            for (var index = 0; index < dummyTargets.Count; index++)
            {
                var target = dummyTargets[index];
                target.Health?.Initialize(CombatTargetMaxHealth);
                target.VisualFeedback?.RefreshRenderers();
            }
            combatStatus = "공격 재생 중 · Marker 대기";
        }

        private void ClearCombatPresentation()
        {
            targetFeedbackActive = false;
            pendingFloatingNumber = default;
            pendingFloatingTexts.Clear();
            nextFloatingTextReleaseAt.Clear();
            recentFloatingTexts.Clear();
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

        private static string GetActivePatternLabel(MonsterActiveAttackPattern pattern) => pattern switch
        {
            MonsterActiveAttackPattern.Line => "일자 피해",
            MonsterActiveAttackPattern.Cone => "부채꼴 피해",
            MonsterActiveAttackPattern.SelfCircle => "내 주변 원형",
            MonsterActiveAttackPattern.FrontCircle => "내 앞 원형",
            MonsterActiveAttackPattern.PiercingProjectile => "관통 투사체",
            MonsterActiveAttackPattern.ExplosiveProjectile => "폭발 투사체",
            MonsterActiveAttackPattern.PiercingBeam => "관통 빔",
            MonsterActiveAttackPattern.InstantMagic => "즉발 마법",
            MonsterActiveAttackPattern.SingleTarget => "단일 대상",
            MonsterActiveAttackPattern.StandardProjectile => "일반 투사체",
            MonsterActiveAttackPattern.ReturningProjectile => "왕복 투사체",
            MonsterActiveAttackPattern.Breath => "브레스",
            MonsterActiveAttackPattern.TravelingWave => "이동 파동",
            MonsterActiveAttackPattern.TargetCircle => "대상 중심 원형",
            _ => "공격"
        };

        private static string GetActiveTargetPolicyLabel(MonsterActiveTargetPolicy policy) => policy switch
        {
            MonsterActiveTargetPolicy.SameTarget => "앞 Step과 같은 대상",
            MonsterActiveTargetPolicy.DifferentTarget => "앞 Step과 다른 대상",
            _ => "대상 선택"
        };

        private enum ActivePreviewEventType
        {
            Motion,
            Telegraph,
            Dash,
            Launch,
            Impact,
            DeliveryEnd,
            Complete
        }

        private sealed class ActivePreviewAttackBlock
        {
            public ActivePreviewAttackBlock(
                MonsterBasicAttackProfile profile,
                MonsterMakerActiveStepPresentationDraft presentation,
                string motionId,
                float playbackSpeed,
                int sequenceId)
            {
                Profile = profile;
                Presentation = presentation;
                MotionId = motionId ?? string.Empty;
                PlaybackSpeed = Mathf.Max(0.05f, playbackSpeed);
                SequenceId = sequenceId;
            }

            public MonsterBasicAttackProfile Profile { get; }
            public MonsterMakerActiveStepPresentationDraft Presentation { get; }
            public string MotionId { get; }
            public float PlaybackSpeed { get; }
            public int SequenceId { get; }
            public List<GameObject> Deliveries { get; } = new List<GameObject>();
            public bool ProgressiveDamageApplied { get; set; }
            public bool ProgressiveImpactFeelPlayed { get; set; }
        }

        private sealed class PendingActivePreviewEvent
        {
            public PendingActivePreviewEvent(
                float executeAt,
                int stepIndex,
                ActivePreviewEventType type,
                MonsterActiveAttackStep step,
                MonsterMakerActiveStepPresentationDraft presentation,
                UnitActor target,
                ActivePreviewAttackBlock attackBlock = null,
                int damageStage = 0,
                UnitActor progressiveTarget = null,
                int progressiveIndex = 0,
                int progressiveCount = 0)
            {
                ExecuteAt = executeAt;
                StepIndex = stepIndex;
                Type = type;
                Step = step;
                Presentation = presentation;
                Target = target;
                AttackBlock = attackBlock;
                DamageStage = Mathf.Max(0, damageStage);
                ProgressiveTarget = progressiveTarget;
                ProgressiveIndex = Mathf.Max(0, progressiveIndex);
                ProgressiveCount = Mathf.Max(0, progressiveCount);
            }

            public float ExecuteAt { get; }
            public int StepIndex { get; }
            public ActivePreviewEventType Type { get; }
            public MonsterActiveAttackStep Step { get; }
            public MonsterMakerActiveStepPresentationDraft Presentation { get; }
            public UnitActor Target { get; }
            public ActivePreviewAttackBlock AttackBlock { get; }
            public int DamageStage { get; }
            public UnitActor ProgressiveTarget { get; }
            public int ProgressiveIndex { get; }
            public int ProgressiveCount { get; }
            public bool IsProgressiveImpact => ProgressiveTarget != null && ProgressiveCount > 0;
            public bool IsFinalProgressiveImpact => IsProgressiveImpact &&
                                                    ProgressiveIndex >= ProgressiveCount - 1;

            public static int Compare(PendingActivePreviewEvent left, PendingActivePreviewEvent right)
            {
                var time = left.ExecuteAt.CompareTo(right.ExecuteAt);
                if (time != 0) return time;
                var step = left.StepIndex.CompareTo(right.StepIndex);
                if (step != 0) return step;
                var type = left.Type.CompareTo(right.Type);
                if (type != 0) return type;
                var damageStage = left.DamageStage.CompareTo(right.DamageStage);
                return damageStage != 0
                    ? damageStage
                    : left.ProgressiveIndex.CompareTo(right.ProgressiveIndex);
            }
        }

        private void DestroyCombatTarget()
        {
            for (var index = dummyTargets.Count - 1; index >= 0; index--)
            {
                var target = dummyTargets[index];
                target.Actor?.Shutdown();
                if (target.Instance != null) stage.RemoveAuxiliary(target.Instance);
            }
            dummyTargets.Clear();

            dummyTarget = null;
            dummyTargetActor = null;
            dummyTargetHealth = null;
            dummyTargetVisualFeedback = null;
            dummyTargetAnimators = Array.Empty<Animator>();
        }

        private sealed class PreviewTarget
        {
            public PreviewTarget(
                GameObject instance,
                UnitActor actor,
                HealthComponent health,
                UnitVisualFeedback visualFeedback)
            {
                Instance = instance;
                Actor = actor;
                Health = health;
                VisualFeedback = visualFeedback;
            }

            public GameObject Instance { get; }
            public UnitActor Actor { get; }
            public HealthComponent Health { get; }
            public UnitVisualFeedback VisualFeedback { get; }
        }

        private sealed class PreviewVfx
        {
            public PreviewVfx(
                GameObject instance,
                float lifetime,
                MonsterBasicAttackVfxEndPolicy endPolicy = MonsterBasicAttackVfxEndPolicy.Timed,
                GameObject delivery = null,
                bool isPlaceholder = false,
                float playbackOffset = 0f,
                float playbackSpeed = 1f,
                int sequenceId = 0,
                bool isActiveDelivery = false,
                Vector3 deliveryStart = default,
                Vector3 deliveryEnd = default,
                float deliveryDuration = 0f,
                bool deliveryReturns = false)
            {
                Instance = instance;
                Lifetime = Mathf.Max(0.01f, lifetime);
                EndPolicy = endPolicy;
                Delivery = delivery;
                IsPlaceholder = isPlaceholder;
                PlaybackOffset = Mathf.Max(0f, playbackOffset);
                PlaybackSpeed = float.IsNaN(playbackSpeed) || float.IsInfinity(playbackSpeed)
                    ? 1f
                    : Mathf.Max(0.01f, playbackSpeed);
                SequenceId = sequenceId;
                IsActiveDelivery = isActiveDelivery;
                DeliveryStart = deliveryStart;
                DeliveryEnd = deliveryEnd;
                DeliveryDuration = Mathf.Max(0f, deliveryDuration);
                DeliveryReturns = deliveryReturns;
                BaseScale = instance == null ? Vector3.one : instance.transform.localScale;
            }

            public PreviewVfx(
                GameObject instance,
                float lifetime,
                MonsterActivePresentationEndPolicy endPolicy,
                bool isDelivery,
                Vector3 deliveryStart,
                Vector3 deliveryEnd,
                float deliveryDuration,
                bool isPlaceholder = false)
            {
                Instance = instance;
                Lifetime = Mathf.Max(0.01f, lifetime);
                ActiveEndPolicy = endPolicy;
                IsActiveDelivery = isDelivery;
                DeliveryStart = deliveryStart;
                DeliveryEnd = deliveryEnd;
                DeliveryDuration = Mathf.Max(0f, deliveryDuration);
                IsPlaceholder = isPlaceholder;
                BaseScale = instance == null ? Vector3.one : instance.transform.localScale;
            }

            public GameObject Instance { get; }
            public float Lifetime { get; }
            public MonsterBasicAttackVfxEndPolicy EndPolicy { get; }
            public MonsterActivePresentationEndPolicy? ActiveEndPolicy { get; }
            public GameObject Delivery { get; }
            public bool IsActiveDelivery { get; }
            public Vector3 DeliveryStart { get; }
            public Vector3 DeliveryEnd { get; }
            public float DeliveryDuration { get; }
            public bool DeliveryReturns { get; }
            public bool IsPlaceholder { get; }
            public float PlaybackOffset { get; }
            public float PlaybackSpeed { get; } = 1f;
            public int SequenceId { get; }
            public Vector3 BaseScale { get; }
            public float Elapsed { get; set; }
        }

        private sealed class PendingEffectPreviewGroup
        {
            public PendingEffectPreviewGroup(
                float executeAt,
                MonsterEffectActiveGroup group,
                MonsterMakerActiveStepPresentationDraft presentation,
                EffectPreviewPhase phase)
            {
                ExecuteAt = executeAt;
                Group = group;
                Presentation = presentation;
                Phase = phase;
            }

            public float ExecuteAt { get; }
            public MonsterEffectActiveGroup Group { get; }
            public MonsterMakerActiveStepPresentationDraft Presentation { get; }
            public EffectPreviewPhase Phase { get; }
        }

        private enum EffectPreviewPhase
        {
            Activation,
            Applied,
            Expired
        }

        private sealed class PendingContractVfx
        {
            public PendingContractVfx(
                float executeAt,
                MonsterBasicAttackVfxSlot slot,
                MonsterBasicAttackVfxBinding binding,
                Vector3 origin,
                Vector3 hitPoint,
                Vector3 areaCenter,
                Quaternion rotation,
                GameObject projectile,
                string socketPath,
                UnitActor targetActor = null,
                float playbackSpeedMultiplier = 1f,
                int sequenceId = 0,
                string previewLabel = "기본공격")
            {
                ExecuteAt = executeAt;
                Slot = slot;
                Binding = binding;
                Origin = origin;
                HitPoint = hitPoint;
                AreaCenter = areaCenter;
                Rotation = rotation;
                Projectile = projectile;
                SocketPath = socketPath;
                TargetActor = targetActor;
                PlaybackSpeedMultiplier = Mathf.Max(0.05f, playbackSpeedMultiplier);
                SequenceId = sequenceId;
                PreviewLabel = string.IsNullOrWhiteSpace(previewLabel) ? "기본공격" : previewLabel;
            }

            public float ExecuteAt { get; }
            public MonsterBasicAttackVfxSlot Slot { get; }
            public MonsterBasicAttackVfxBinding Binding { get; }
            public Vector3 Origin { get; }
            public Vector3 HitPoint { get; }
            public Vector3 AreaCenter { get; }
            public Quaternion Rotation { get; }
            public GameObject Projectile { get; }
            public string SocketPath { get; }
            public UnitActor TargetActor { get; }
            public float PlaybackSpeedMultiplier { get; }
            public int SequenceId { get; }
            public string PreviewLabel { get; }
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
                bool canDamage,
                bool isPlaceholder = false,
                float playbackOffset = 0f,
                float playbackSpeed = 1f)
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
                IsPlaceholder = isPlaceholder;
                PlaybackOffset = Mathf.Max(0f, playbackOffset);
                PlaybackSpeed = float.IsNaN(playbackSpeed) || float.IsInfinity(playbackSpeed)
                    ? 1f
                    : Mathf.Max(0.01f, playbackSpeed);
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
            public bool IsPlaceholder { get; }
            public float PlaybackOffset { get; }
            public float PlaybackSpeed { get; }
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
                bool playFeedback,
                int hitIndex)
            {
                ApplyAt = applyAt;
                Damage = damage;
                DraftFeedback = draftFeedback;
                ProfileFeedback = profileFeedback;
                Profile = profile;
                Origin = origin;
                Rotation = rotation;
                PlayFeedback = playFeedback;
                HitIndex = hitIndex;
            }

            public float ApplyAt { get; }
            public float Damage { get; }
            public MonsterMakerFeedbackDraft DraftFeedback { get; }
            public MonsterFeedbackCue ProfileFeedback { get; }
            public MonsterBasicAttackProfile Profile { get; }
            public Vector3 Origin { get; }
            public Quaternion Rotation { get; }
            public bool PlayFeedback { get; }
            public int HitIndex { get; }
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

        private struct PendingFloatingText
        {
            public Vector3 Position;
            public string Value;
            public CombatStatusTextStyle Style;
            public float ReleaseAt;
            public float QueuedAt;
            public int QueueKey;
        }

        private readonly struct RecentFloatingText
        {
            public RecentFloatingText(string value, float queuedAt)
            {
                Value = value;
                QueuedAt = queuedAt;
            }

            public string Value { get; }
            public float QueuedAt { get; }
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

            public void PlayDamage(
                Vector3 position,
                float amount,
                FloatingNumberStyle style,
                int mergeKey,
                DamageFeedbackFlags feedbackFlags = DamageFeedbackFlags.None)
            {
                owner.QueueFloatingNumber(
                    position,
                    amount,
                    style,
                    mergeKey,
                    (feedbackFlags & DamageFeedbackFlags.SeparateFloatingNumber) != 0);
            }

            public void PlayFloatingNumber(Vector3 position, float amount, FloatingNumberStyle style, int mergeKey)
            {
                owner.QueueFloatingNumber(position, amount, style, mergeKey);
            }

            public void PlayStatusText(
                Vector3 position,
                string text,
                CombatStatusTextStyle style,
                int queueKey)
            {
                owner.QueueFloatingText(position, text, style, queueKey);
            }
        }
    }
}
