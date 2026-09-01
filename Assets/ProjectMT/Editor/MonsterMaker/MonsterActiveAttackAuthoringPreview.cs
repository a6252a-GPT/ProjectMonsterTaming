using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    internal sealed class MonsterActiveAttackAuthoringPreview : IDisposable // 기본공격 조립소형 독립 판정 Preview
    {
        private static readonly Vector3 DefaultAttackerPosition = new Vector3(0f, 0f, 0.15f);
        private const float PreviewDashStopDistance = 0.5f;
        private const float MaximumSimulationStep = 1f / 60f;
        private readonly List<GameObject> targets = new List<GameObject>();
        private readonly List<Vector3> targetPositions = new List<Vector3>();
        private readonly List<PreviewStep> timeline = new List<PreviewStep>();
        private readonly List<FeelPreviewInstance> feelInstances = new List<FeelPreviewInstance>();
        private readonly List<MonsterBasicAttackProfile> attackBlocks = new List<MonsterBasicAttackProfile>();
        private PreviewRenderUtility utility;
        private GameObject root;
        private GameObject attacker;
        private GameObject delivery;
        private readonly List<GameObject> deliveries = new List<GameObject>();
        private MonsterAttackAreaIndicator indicator;
        private readonly List<MonsterAttackAreaIndicator> indicators =
            new List<MonsterAttackAreaIndicator>();
        private Material groundMaterial;
        private Material sourceMaterial;
        private Material targetMaterial;
        private Material attackMaterial;
        private MonsterActiveAttackProfile profile;
        private double playbackStartedAt;
        private float playbackElapsed;
        private bool playing;
        private int visibleStepIndex = -1;
        private string status = "프로필을 선택하면 판정 미리보기가 준비됩니다.";
        private float[] targetPulseTimes = Array.Empty<float>();

        public bool IsPlaying => playing;
        public string Status => status;
        internal int SceneHandle => utility?.camera != null ? utility.camera.gameObject.scene.handle : 0;

        public void SetProfile(MonsterActiveAttackProfile value)
        {
            if (profile == value && root != null) return;
            profile = value;
            Rebuild();
        }

        public void Refresh()
        {
            Rebuild();
        }

        public void PlayAll()
        {
            BeginPlayback(-1);
        }

        public void PlayStep(int stepIndex)
        {
            BeginPlayback(stepIndex);
        }

        public bool Tick()
        {
            if (!playing || timeline.Count == 0) return false;
            var targetElapsed = Mathf.Max(
                playbackElapsed,
                (float)(EditorApplication.timeSinceStartup - playbackStartedAt));
            var endAt = timeline.Max(item => item.EndAt);
            var simulationEnd = Mathf.Min(targetElapsed, endAt);
            if (simulationEnd <= playbackElapsed + 0.000001f)
            {
                AdvancePlayback(simulationEnd);
            }
            else
            {
                while (playbackElapsed + 0.000001f < simulationEnd)
                {
                    playbackElapsed = Mathf.Min(
                        simulationEnd,
                        playbackElapsed + MaximumSimulationStep);
                    AdvancePlayback(playbackElapsed);
                }
            }

            if (targetElapsed >= endAt)
            {
                playing = false;
                ResetPlaybackObjects();
                ShowStaticStep(0);
                status = $"재생 완료 · {timeline.Count} Step";
            }
            return true;
        }

        private void AdvancePlayback(float elapsed)
        {
            var activeIndex = -1;
            for (var index = 0; index < timeline.Count; index++)
            {
                var item = timeline[index];
                if (elapsed >= item.LaunchAt)
                {
                    activeIndex = index;
                }
            }

            if (activeIndex >= 0 && visibleStepIndex != activeIndex)
            {
                visibleStepIndex = activeIndex;
                ShowStep(timeline[activeIndex]);
            }

            UpdateDash(elapsed, activeIndex);

            for (var index = 0; index < timeline.Count; index++)
            {
                var item = timeline[index];
                while (item.NextHitIndex < item.HitTimes.Length &&
                       elapsed >= item.HitTimes[item.NextHitIndex])
                {
                    var hitIndex = item.NextHitIndex++;
                    PulseVictims(item, elapsed, hitIndex);
                }
            }

            UpdateDelivery(elapsed, activeIndex);
            UpdateTargetPulses(elapsed);
            UpdateFeelInstances(elapsed);
        }

        public void Render(Rect rect, bool topDown)
        {
            if (utility != null && !MonsterWorkshopPreviewSceneRecovery.HasRenderingMask(utility))
            {
                Rebuild();
            }
            if (utility == null || root == null || Event.current.type != EventType.Repaint ||
                rect.width <= 1f || rect.height <= 1f) return;
            ConfigureCamera(rect, topDown);
            utility.BeginPreview(rect, GUIStyle.none);
            utility.Render(true);
            var texture = utility.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, false);
        }

        private void BeginPlayback(int onlyStepIndex)
        {
            if (profile == null || profile.Steps.Count == 0 || root == null) return;
            BuildTimeline(onlyStepIndex);
            if (timeline.Count == 0) return;
            ResetPlaybackObjects();
            playbackStartedAt = EditorApplication.timeSinceStartup;
            playbackElapsed = 0f;
            playing = true;
            visibleStepIndex = -1;
            status = onlyStepIndex < 0
                ? $"전체 스킬 재생 · {profile.DisplayName}"
                : $"Step {onlyStepIndex + 1:00} 단독 재생";
        }

        private void BuildTimeline(int onlyStepIndex)
        {
            timeline.Clear();
            var previousLaunchAt = 0f;
            var previousEndAt = 0f;
            var previousTargetIndex = 0;
            var projectedAttackerPosition = DefaultAttackerPosition;
            var first = onlyStepIndex < 0 ? 0 : Mathf.Clamp(onlyStepIndex, 0, profile.Steps.Count - 1);
            var last = onlyStepIndex < 0 ? profile.Steps.Count - 1 : first;
            for (var index = first; index <= last; index++)
            {
                var step = profile.Steps[index];
                if (step == null || index < 0 || index >= attackBlocks.Count) continue;
                var attackBlock = attackBlocks[index];
                var targetIndex = ResolveTargetIndex(
                    step.TargetPolicy,
                    previousTargetIndex,
                    index > first);
                previousTargetIndex = targetIndex;
                var targetPosition = targetPositions.Count > 0
                    ? targetPositions[Mathf.Clamp(targetIndex, 0, targetPositions.Count - 1)]
                    : new Vector3(0f, 0f, 3.2f);
                var startOrigin = projectedAttackerPosition;
                var forward = targetPosition - projectedAttackerPosition;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
                forward.Normalize();
                var speed = step.PlaybackSpeed;
                var chainsFromLaunch = index > first &&
                                       step.StartMode == MonsterActiveStepStartMode.AfterPreviousLaunch;
                var startAt = chainsFromLaunch
                    ? previousLaunchAt
                    : previousEndAt + step.DelayAfterPrevious / speed;
                var launchAt = chainsFromLaunch
                    ? previousLaunchAt +
                      Mathf.Max(step.DelayAfterPrevious, step.TelegraphDelay) / speed
                    : startAt + step.TelegraphDelay / speed;
                if (attackBlock.MovementModule == MonsterBasicAttackMovementModule.Dash)
                {
                    projectedAttackerPosition = MonsterBasicAttackProfile.ResolveDashDestination(
                        projectedAttackerPosition,
                        targetPosition,
                        step.DashDistance,
                        PreviewDashStopDistance);
                }
                var targetDistance = Vector3.Distance(projectedAttackerPosition, targetPosition);
                var projectileSpeed = attackBlock.ProjectileSpeed * speed;
                var usesTargetEndpoint =
                    attackBlock.ProjectileTravel is MonsterBasicAttackProjectileTravel.Homing or
                        MonsterBasicAttackProjectileTravel.Returning ||
                    attackBlock.CollisionModule == MonsterBasicAttackCollisionModule.StopOnFirstTarget;
                var impactDistance = attackBlock.CollisionModule ==
                                     MonsterBasicAttackCollisionModule.AreaImpact && !usesTargetEndpoint
                    ? attackBlock.ResolveRange(1f)
                    : targetDistance;
                var travel = attackBlock.UsesProjectileVisual
                    ? impactDistance / Mathf.Max(0.01f, projectileSpeed)
                    : 0f;
                var deliveryDistance = attackBlock.UsesProjectileVisual && !usesTargetEndpoint
                    ? attackBlock.ResolveRange(1f)
                    : targetDistance;
                var outboundTravel = deliveryDistance / Mathf.Max(0.01f, projectileSpeed);
                var deliveryEndAt = launchAt +
                                    (attackBlock.ProjectileTravel == MonsterBasicAttackProjectileTravel.Returning
                                        ? outboundTravel * 2f
                                        : outboundTravel);
                var hitTimes = new float[attackBlock.HitCount];
                for (var hitIndex = 0; hitIndex < hitTimes.Length; hitIndex++)
                {
                    hitTimes[hitIndex] = attackBlock.SequenceModule ==
                                         MonsterBasicAttackSequenceModule.ReturnPasses
                        ? hitIndex == 0
                            ? launchAt + travel
                            : deliveryEndAt
                        : launchAt + travel +
                          hitIndex * attackBlock.ResolveRepeatHitInterval(attackBlock.BreathDuration) / speed;
                }
                var endAt = Mathf.Max(
                    launchAt + attackBlock.ResolveActivityDuration(speed),
                    deliveryEndAt);
                timeline.Add(new PreviewStep(
                    index,
                    step,
                    attackBlock,
                    targetIndex,
                    targetPosition,
                    startOrigin,
                    projectedAttackerPosition,
                    startAt,
                    launchAt,
                    hitTimes,
                    deliveryEndAt,
                    endAt));
                previousLaunchAt = launchAt;
                previousEndAt = Mathf.Max(previousEndAt, endAt);
            }
        }

        private int ResolveTargetIndex(
            MonsterActiveTargetPolicy policy,
            int previous,
            bool hasPreviousStep)
        {
            if (!hasPreviousStep || policy == MonsterActiveTargetPolicy.SameTarget || targets.Count <= 1)
                return Mathf.Clamp(previous, 0, Mathf.Max(0, targets.Count - 1));
            return (Mathf.Clamp(previous, 0, targets.Count - 1) + 1) % targets.Count;
        }

        private void ShowStep(PreviewStep item)
        {
            if (item.TargetIndex < 0 || item.TargetIndex >= targetPositions.Count) return;
            var target = item.TargetPosition;
            var origin = item.StartOrigin;
            var forward = target - origin;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            if (attacker != null)
            {
                attacker.transform.localPosition = origin;
                attacker.transform.localRotation = Quaternion.LookRotation(forward, Vector3.up);
            }
            ShowIndicators(item.AttackBlock, origin, forward, target);
            status = $"Step {item.SourceIndex + 1:00} · {item.Step.DisplayName} · " +
                     (item.Step.TargetPolicy == MonsterActiveTargetPolicy.SameTarget ? "같은 대상" : "다른 대상");
        }

        private void UpdateDash(float elapsed, int activeIndex)
        {
            if (attacker == null || activeIndex < 0 || activeIndex >= timeline.Count) return;
            var item = timeline[activeIndex];
            if (item.DashApplied ||
                item.AttackBlock.MovementModule != MonsterBasicAttackMovementModule.Dash ||
                elapsed < item.LaunchAt)
            {
                return;
            }

            item.DashApplied = true;
            var forward = item.TargetPosition - item.AttackOrigin;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            attacker.transform.localPosition = item.AttackOrigin;
            attacker.transform.localRotation = Quaternion.LookRotation(forward, Vector3.up);
            ShowIndicators(item.AttackBlock, item.AttackOrigin, forward, item.TargetPosition);
        }

        private void UpdateDelivery(float elapsed, int activeIndex)
        {
            if (deliveries.Count == 0 || activeIndex < 0 || activeIndex >= timeline.Count)
            {
                SetDeliveriesActive(0);
                return;
            }
            var item = timeline[activeIndex];
            if (!item.AttackBlock.UsesProjectileVisual ||
                elapsed < item.LaunchAt || elapsed > item.DeliveryEndAt)
            {
                SetDeliveriesActive(0);
                return;
            }
            var ratio = Mathf.InverseLerp(item.LaunchAt, item.DeliveryEndAt, elapsed);
            if (item.AttackBlock.ProjectileTravel == MonsterBasicAttackProjectileTravel.Returning)
            {
                ratio = ratio <= 0.5f ? ratio * 2f : (1f - ratio) * 2f;
            }
            var projectileCount = Mathf.Clamp(item.AttackBlock.ProjectileCount, 1, deliveries.Count);
            SetDeliveriesActive(projectileCount);
            var forward = item.TargetPosition - item.AttackOrigin;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            for (var index = 0; index < projectileCount; index++)
            {
                var projectile = deliveries[index];
                var end = ResolveProjectileEndpoint(
                    item.AttackBlock,
                    item.AttackOrigin,
                    item.TargetPosition,
                    forward,
                    index);
                var direction = end - item.AttackOrigin;
                direction.y = 0f;
                projectile.transform.localPosition = Vector3.Lerp(item.AttackOrigin, end, ratio) +
                                                     Vector3.up * 0.25f;
                projectile.transform.localRotation = Quaternion.LookRotation(
                    direction.sqrMagnitude < 0.0001f ? forward : direction.normalized,
                    Vector3.up);
                projectile.transform.localScale = Vector3.one * 0.22f;
            }
        }

        private void SetDeliveriesActive(int activeCount)
        {
            for (var index = 0; index < deliveries.Count; index++)
            {
                if (deliveries[index] != null)
                    deliveries[index].SetActive(index < activeCount);
            }
        }

        private void ShowIndicators(
            MonsterBasicAttackProfile attackBlock,
            Vector3 origin,
            Vector3 forward,
            Vector3 target)
        {
            ClearIndicators();
            if (attackBlock == null || root == null) return;
            var count = attackBlock.UsesProjectileVisual &&
                        attackBlock.CollisionModule == MonsterBasicAttackCollisionModule.AreaImpact
                ? attackBlock.ProjectileCount
                : 1;
            for (var index = 0; index < count; index++)
            {
                var indicatorTarget = count > 1 ||
                                      attackBlock.CollisionModule ==
                                      MonsterBasicAttackCollisionModule.AreaImpact
                    ? ResolveProjectileEndpoint(attackBlock, origin, target, forward, index)
                    : target;
                var created = MonsterAttackAreaIndicator.Create(
                    root.transform,
                    attackBlock,
                    origin,
                    forward,
                    indicatorTarget,
                    1f,
                    new Color(0.1f, 1f, 0.85f, 1f),
                    false);
                if (created == null) continue;
                indicators.Add(created);
                indicator ??= created;
            }
        }

        private void ClearIndicators()
        {
            for (var index = indicators.Count - 1; index >= 0; index--)
            {
                if (indicators[index] != null)
                    UnityEngine.Object.DestroyImmediate(indicators[index].gameObject);
            }
            indicators.Clear();
            indicator = null;
        }

        private static Vector3 ResolveProjectileEndpoint(
            MonsterBasicAttackProfile attackBlock,
            Vector3 origin,
            Vector3 target,
            Vector3 forward,
            int projectileIndex)
        {
            if (attackBlock == null) return target;
            var direction = attackBlock.ResolveProjectileDirection(forward, projectileIndex);
            var usesTargetEndpoint =
                attackBlock.ProjectileTravel is MonsterBasicAttackProjectileTravel.Homing or
                    MonsterBasicAttackProjectileTravel.Returning ||
                attackBlock.CollisionModule == MonsterBasicAttackCollisionModule.StopOnFirstTarget;
            return usesTargetEndpoint
                ? target
                : origin + direction * attackBlock.ResolveRange(1f);
        }

        private void PulseVictims(PreviewStep item, float elapsed, int hitIndex)
        {
            var origin = item.AttackOrigin;
            var target = item.TargetPosition;
            var forward = target - origin;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            var applied = 0;
            if (item.AttackBlock.CollisionModule == MonsterBasicAttackCollisionModule.AreaImpact &&
                item.AttackBlock.UsesProjectileVisual)
            {
                for (var projectileIndex = 0;
                     projectileIndex < item.AttackBlock.ProjectileCount;
                     projectileIndex++)
                {
                    var center = ResolveProjectileEndpoint(
                        item.AttackBlock,
                        origin,
                        target,
                        forward,
                        projectileIndex);
                    var victimIndices = Enumerable.Range(0, targetPositions.Count)
                        .Where(index => Vector3.Distance(targetPositions[index], center) <=
                                        item.AttackBlock.Radius)
                        .OrderBy(index => (targetPositions[index] - center).sqrMagnitude)
                        .Take(item.AttackBlock.MaxTargets)
                        .ToList();
                    victimIndices.Remove(item.TargetIndex);
                    victimIndices.Insert(0, item.TargetIndex);
                    if (victimIndices.Count > item.AttackBlock.MaxTargets)
                        victimIndices.RemoveAt(victimIndices.Count - 1);
                    for (var index = 0; index < victimIndices.Count; index++)
                    {
                        targetPulseTimes[victimIndices[index]] = elapsed;
                        applied++;
                    }
                    if (victimIndices.Count > 0) SpawnImpactFeel(item, center, forward, elapsed);
                }
            }
            else
            {
                for (var index = 0; index < targetPositions.Count && applied < item.Step.MaxTargets; index++)
                {
                    if (!IsInsideAttackBlock(
                            item.AttackBlock,
                            targetPositions[index],
                            target,
                            origin,
                            forward)) continue;
                    targetPulseTimes[index] = elapsed;
                    applied++;
                }
                if (applied > 0) SpawnImpactFeel(item, target, forward, elapsed);
            }
            var ratio = item.AttackBlock.ResolveDamageRatio(hitIndex) * item.Step.DamageMultiplier;
            status = $"Step {item.SourceIndex + 1:00} · {hitIndex + 1}/{item.AttackBlock.HitCount}타 " +
                     $"· 적중 {applied}명 · 피해 {ratio:0.##}배";
        }

        private void SpawnImpactFeel(PreviewStep item, Vector3 position, Vector3 forward, float elapsed)
        {
            var feel = profile?.ImpactFeel;
            if (feel?.HasFeel != true || root == null) return;
            var rotation = Quaternion.LookRotation(forward, Vector3.up);
            var instance = UnityEngine.Object.Instantiate(feel.Prefab);
            instance.name = "[Active Workshop FEEL] " + feel.Prefab.name;
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.SetParent(root.transform, false);
            instance.transform.localPosition = position + rotation * feel.LocalPosition;
            instance.transform.localRotation = rotation * feel.LocalRotation;
            instance.transform.localScale = feel.Prefab.transform.localScale * feel.Scale;
            var runtime = instance.GetComponentsInChildren<MonoBehaviour>(true)
                .OfType<IBasicAttackFeelRuntime>()
                .FirstOrDefault();
            runtime?.PlayBasicAttackFeel(
                instance.transform.position,
                targets[item.TargetIndex],
                Mathf.Clamp(item.Step.DamageMultiplier, 0.5f, 2f),
                BasicAttackFeelPlaybackOptions.None);
            feelInstances.Add(new FeelPreviewInstance(
                instance,
                elapsed + feel.Lifetime / item.Step.PlaybackSpeed));
        }

        private void UpdateFeelInstances(float elapsed)
        {
            for (var index = feelInstances.Count - 1; index >= 0; index--)
            {
                var item = feelInstances[index];
                if (item.Instance != null && elapsed < item.DestroyAt) continue;
                if (item.Instance != null) UnityEngine.Object.DestroyImmediate(item.Instance);
                feelInstances.RemoveAt(index);
            }
        }

        private static bool IsInsideAttackBlock(
            MonsterBasicAttackProfile attackBlock,
            Vector3 point,
            Vector3 primary,
            Vector3 origin,
            Vector3 forward)
        {
            if (attackBlock == null) return false;
            var delta = point - origin;
            var distance = delta.magnitude;
            var range = attackBlock.ResolveRange(1f);
            switch (attackBlock.Shape)
            {
                case MonsterBasicAttackShape.Line:
                    return IsInsideLine(
                        delta,
                        forward,
                        range,
                        attackBlock.LineWidth * 0.5f + 0.25f);
                case MonsterBasicAttackShape.Fan:
                    return distance <= range + 0.25f &&
                           (distance <= 0.001f ||
                            Vector3.Angle(forward, delta) <= attackBlock.Angle * 0.5f);
                case MonsterBasicAttackShape.Circle:
                    var center = attackBlock.Center switch
                    {
                        MonsterBasicAttackCenter.Source => origin,
                        MonsterBasicAttackCenter.Forward => origin + forward * attackBlock.ForwardOffset,
                        _ => primary
                    };
                    return Vector3.Distance(point, center) <= attackBlock.Radius + 0.25f;
                case MonsterBasicAttackShape.Single:
                    return Vector3.Distance(point, primary) < 0.1f;
                default:
                    return false;
            }
        }

        private static bool IsInsideLine(Vector3 delta, Vector3 forward, float length, float halfWidth)
        {
            var along = Vector3.Dot(delta, forward);
            return along >= 0f && along <= length && (delta - forward * along).magnitude <= halfWidth;
        }

        private void UpdateTargetPulses(float elapsed)
        {
            for (var index = 0; index < targets.Count; index++)
            {
                var age = elapsed - targetPulseTimes[index];
                var pulse = age >= 0f && age <= 0.32f ? Mathf.Sin(age / 0.32f * Mathf.PI) : 0f;
                targets[index].transform.localScale = Vector3.one * Mathf.Lerp(0.42f, 0.58f, pulse);
            }
        }

        private void Rebuild()
        {
            ClearContents();
            EnsureUtility();
            if (profile == null)
            {
                status = "프로필을 선택하면 판정 미리보기가 준비됩니다.";
                return;
            }
            root = new GameObject("[Active Attack Workshop Preview]") { hideFlags = HideFlags.HideAndDontSave };
            groundMaterial = CreateMaterial(new Color(0.11f, 0.13f, 0.16f));
            sourceMaterial = CreateMaterial(new Color(0.15f, 0.8f, 0.7f));
            targetMaterial = CreateMaterial(new Color(0.95f, 0.35f, 0.3f));
            attackMaterial = CreateMaterial(new Color(1f, 0.8f, 0.15f));
            CreatePrimitive(PrimitiveType.Cube, "Ground", new Vector3(0f, -0.08f, 2f),
                new Vector3(9f, 0.1f, 9f), groundMaterial);
            attacker = CreatePrimitive(PrimitiveType.Capsule, "Caster", DefaultAttackerPosition,
                new Vector3(0.55f, 0.45f, 0.55f), sourceMaterial);
            for (var index = 0; index < profile.Steps.Count; index++)
            {
                var block = ScriptableObject.CreateInstance<MonsterBasicAttackProfile>();
                block.name = $"__ActiveAuthoringPreview_{profile.Steps[index].StepId}";
                block.hideFlags = HideFlags.HideAndDontSave;
                profile.Steps[index].EditorCompileAttackBlock(block);
                attackBlocks.Add(block);
            }
            var targetCount = ResolveTargetCount();
            var primaryDistance = ResolvePrimaryTargetDistance();
            for (var index = 0; index < targetCount; index++)
            {
                var lateral = index == 0 ? 0f : (index % 2 == 1 ? 1f : -1f) * ((index + 1) / 2) * 1.2f;
                var targetPosition = new Vector3(
                    lateral,
                    0f,
                    primaryDistance + (index == 0 ? 0f : 0.22f));
                targetPositions.Add(targetPosition);
                targets.Add(CreatePrimitive(
                    PrimitiveType.Capsule,
                    $"Target {index + 1:00}",
                    targetPosition,
                    Vector3.one * 0.42f,
                    targetMaterial));
            }
            targetPulseTimes = new float[targetCount];
            for (var index = 0; index < targetPulseTimes.Length; index++) targetPulseTimes[index] = -10f;
            var maximumDeliveries = Mathf.Max(1, attackBlocks
                .Where(block => block != null && block.UsesProjectileVisual)
                .Select(block => block.ProjectileCount)
                .DefaultIfEmpty(1)
                .Max());
            for (var index = 0; index < maximumDeliveries; index++)
            {
                var projectile = CreatePrimitive(
                    PrimitiveType.Sphere,
                    $"Delivery {index + 1:00}",
                    Vector3.zero,
                    Vector3.one * 0.22f,
                    attackMaterial);
                projectile.SetActive(false);
                deliveries.Add(projectile);
            }
            delivery = deliveries[0];
            utility.AddSingleGO(root);
            ShowStaticStep(0);
        }

        private void ShowStaticStep(int index)
        {
            if (profile == null || profile.Steps.Count == 0) return;
            index = Mathf.Clamp(index, 0, profile.Steps.Count - 1);
            var step = profile.Steps[index];
            var attackBlock = index < attackBlocks.Count ? attackBlocks[index] : null;
            var target = targetPositions[0];
            var forward = target - DefaultAttackerPosition;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            ShowIndicators(attackBlock, DefaultAttackerPosition, forward, target);
            status = $"대기 · #{index + 1:00} {step.DisplayName} · {GetPatternLabel(step.Pattern)}";
        }

        private void EnsureUtility()
        {
            MonsterWorkshopPreviewSceneRecovery.RecoverOrphanedScenesIfNeeded();
            if (utility != null && !MonsterWorkshopPreviewSceneRecovery.HasRenderingMask(utility))
            {
                MonsterWorkshopPreviewSceneRecovery.UnregisterOwner(utility);
                utility.Cleanup();
                utility = null;
            }
            if (utility != null) return;
            utility = new PreviewRenderUtility();
            utility.camera.clearFlags = CameraClearFlags.SolidColor;
            utility.camera.backgroundColor = new Color(0.055f, 0.065f, 0.08f, 1f);
            utility.camera.nearClipPlane = 0.05f;
            utility.camera.farClipPlane = 30f;
            utility.lights[0].intensity = 1.25f;
            utility.lights[0].transform.rotation = Quaternion.Euler(45f, 35f, 0f);
            utility.ambientColor = new Color(0.35f, 0.35f, 0.4f);
            MonsterWorkshopPreviewSceneRecovery.RegisterOwner(utility);
        }

        private void ConfigureCamera(Rect rect, bool topDown)
        {
            if (topDown)
            {
                utility.camera.orthographic = true;
                utility.camera.orthographicSize = 4.1f;
                utility.camera.transform.position = new Vector3(0f, 10f, 2.2f);
                utility.camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            }
            else
            {
                utility.camera.orthographic = false;
                utility.camera.fieldOfView = 34f;
                utility.camera.transform.position = new Vector3(5.4f, 6.6f, -7.2f);
                utility.camera.transform.LookAt(new Vector3(0f, 0f, 2.2f));
            }
            utility.camera.aspect = rect.width / Mathf.Max(1f, rect.height);
        }

        private GameObject CreatePrimitive(
            PrimitiveType type,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.hideFlags = HideFlags.HideAndDontSave;
            item.transform.SetParent(root.transform, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            var collider = item.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        private void ResetPlaybackObjects()
        {
            ClearFeelInstances();
            if (attacker != null)
            {
                attacker.transform.localPosition = DefaultAttackerPosition;
                attacker.transform.localRotation = Quaternion.identity;
            }
            SetDeliveriesActive(0);
            for (var index = 0; index < targets.Count; index++) targets[index].transform.localScale = Vector3.one * 0.42f;
            for (var index = 0; index < targetPulseTimes.Length; index++) targetPulseTimes[index] = -10f;
            ClearIndicators();
        }

        private int ResolveTargetCount()
        {
            if (profile?.Steps == null || profile.Steps.Count == 0) return 1;
            var count = profile.Steps.Max(step => step?.MaxTargets ?? 1);
            if (profile.Steps.Any(step => step?.TargetPolicy == MonsterActiveTargetPolicy.DifferentTarget))
                count = Mathf.Max(2, count);
            return Mathf.Clamp(count, 1, 3);
        }

        private float ResolvePrimaryTargetDistance()
        {
            var step = profile?.Steps.FirstOrDefault(candidate => candidate != null);
            if (step == null) return 3.2f;
            var reach = step.Pattern switch
            {
                MonsterActiveAttackPattern.SelfCircle => step.Radius * 0.65f,
                MonsterActiveAttackPattern.FrontCircle => step.ForwardOffset,
                _ => step.Range * 0.7f
            };
            return Mathf.Max(0.8f, reach);
        }

        public void Dispose()
        {
            ClearContents();
            if (utility != null)
            {
                MonsterWorkshopPreviewSceneRecovery.UnregisterOwner(utility);
                utility.Cleanup();
                utility = null;
            }
        }

        private void ClearContents()
        {
            playing = false;
            playbackElapsed = 0f;
            timeline.Clear();
            ClearFeelInstances();
            for (var index = attackBlocks.Count - 1; index >= 0; index--)
            {
                if (attackBlocks[index] != null)
                    UnityEngine.Object.DestroyImmediate(attackBlocks[index]);
            }
            attackBlocks.Clear();
            targets.Clear();
            targetPositions.Clear();
            ClearIndicators();
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            root = null;
            attacker = null;
            delivery = null;
            deliveries.Clear();
            indicators.Clear();
            DestroyMaterial(ref groundMaterial);
            DestroyMaterial(ref sourceMaterial);
            DestroyMaterial(ref targetMaterial);
            DestroyMaterial(ref attackMaterial);
        }

        private void ClearFeelInstances()
        {
            for (var index = feelInstances.Count - 1; index >= 0; index--)
            {
                if (feelInstances[index].Instance != null)
                    UnityEngine.Object.DestroyImmediate(feelInstances[index].Instance);
            }
            feelInstances.Clear();
        }

        private static string GetPatternLabel(MonsterActiveAttackPattern pattern) => pattern switch
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
            _ => pattern.ToString()
        };

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return shader == null ? null : new Material(shader)
            {
                color = color,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material == null) return;
            UnityEngine.Object.DestroyImmediate(material);
            material = null;
        }

        private sealed class PreviewStep
        {
            public PreviewStep(
                int sourceIndex,
                MonsterActiveAttackStep step,
                MonsterBasicAttackProfile attackBlock,
                int targetIndex,
                Vector3 targetPosition,
                Vector3 startOrigin,
                Vector3 attackOrigin,
                float startAt,
                float launchAt,
                float[] hitTimes,
                float deliveryEndAt,
                float endAt)
            {
                SourceIndex = sourceIndex;
                Step = step;
                AttackBlock = attackBlock;
                TargetIndex = targetIndex;
                TargetPosition = targetPosition;
                StartOrigin = startOrigin;
                AttackOrigin = attackOrigin;
                StartAt = startAt;
                LaunchAt = launchAt;
                HitTimes = hitTimes ?? Array.Empty<float>();
                DeliveryEndAt = deliveryEndAt;
                EndAt = endAt;
            }

            public int SourceIndex { get; }
            public MonsterActiveAttackStep Step { get; }
            public MonsterBasicAttackProfile AttackBlock { get; }
            public int TargetIndex { get; }
            public Vector3 TargetPosition { get; }
            public Vector3 StartOrigin { get; }
            public Vector3 AttackOrigin { get; }
            public float StartAt { get; }
            public float LaunchAt { get; }
            public float[] HitTimes { get; }
            public float DeliveryEndAt { get; }
            public float EndAt { get; }
            public int NextHitIndex { get; set; }
            public bool DashApplied { get; set; }
        }

        private sealed class FeelPreviewInstance
        {
            public FeelPreviewInstance(GameObject instance, float destroyAt)
            {
                Instance = instance;
                DestroyAt = destroyAt;
            }

            public GameObject Instance { get; }
            public float DestroyAt { get; }
        }
    }
}
