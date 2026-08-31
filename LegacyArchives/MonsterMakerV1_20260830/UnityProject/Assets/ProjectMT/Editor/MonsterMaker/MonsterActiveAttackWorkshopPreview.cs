using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    internal sealed class MonsterActiveAttackWorkshopPreview : IDisposable // 기본공격 조립소형 독립 판정 Preview
    {
        private readonly List<GameObject> targets = new List<GameObject>();
        private readonly List<Vector3> targetPositions = new List<Vector3>();
        private readonly List<PreviewStep> timeline = new List<PreviewStep>();
        private readonly List<FeelPreviewInstance> feelInstances = new List<FeelPreviewInstance>();
        private PreviewRenderUtility utility;
        private GameObject root;
        private GameObject attacker;
        private GameObject delivery;
        private MonsterAttackAreaIndicator indicator;
        private Material groundMaterial;
        private Material sourceMaterial;
        private Material targetMaterial;
        private Material attackMaterial;
        private MonsterActiveAttackProfile profile;
        private double playbackStartedAt;
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
            var elapsed = (float)(EditorApplication.timeSinceStartup - playbackStartedAt);
            var activeIndex = -1;
            for (var index = 0; index < timeline.Count; index++)
            {
                var item = timeline[index];
                if (elapsed >= item.StartAt && elapsed <= item.EndAt)
                {
                    activeIndex = index;
                    break;
                }
            }

            if (activeIndex >= 0 && visibleStepIndex != activeIndex)
            {
                visibleStepIndex = activeIndex;
                ShowStep(timeline[activeIndex]);
            }

            for (var index = 0; index < timeline.Count; index++)
            {
                var item = timeline[index];
                if (!item.Impacted && elapsed >= item.ImpactAt)
                {
                    item.Impacted = true;
                    PulseVictims(item, elapsed);
                }
            }

            UpdateDelivery(elapsed, activeIndex);
            UpdateTargetPulses(elapsed);
            UpdateFeelInstances(elapsed);
            if (elapsed >= timeline[timeline.Count - 1].EndAt)
            {
                playing = false;
                delivery?.SetActive(false);
                status = $"재생 완료 · {timeline.Count} Step";
            }
            return true;
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
            playing = true;
            visibleStepIndex = -1;
            status = onlyStepIndex < 0
                ? $"전체 스킬 재생 · {profile.DisplayName}"
                : $"Step {onlyStepIndex + 1:00} 단독 재생";
        }

        private void BuildTimeline(int onlyStepIndex)
        {
            timeline.Clear();
            var time = 0f;
            var previousTargetPosition = targetPositions.Count > 0
                ? targetPositions[0]
                : new Vector3(0f, 0f, 3.2f);
            var first = onlyStepIndex < 0 ? 0 : Mathf.Clamp(onlyStepIndex, 0, profile.Steps.Count - 1);
            var last = onlyStepIndex < 0 ? profile.Steps.Count - 1 : first;
            for (var index = first; index <= last; index++)
            {
                var step = profile.Steps[index];
                var targetIndex = 0;
                var targetPosition = ResolveTargetPosition(
                    step.TargetPolicy,
                    previousTargetPosition,
                    index);
                previousTargetPosition = targetPosition;
                time += step.DelayAfterPrevious;
                var startAt = time;
                var launchAt = startAt + step.TelegraphDelay;
                var travel = step.IsProjectile
                    ? Vector3.Distance(Vector3.zero, targetPosition) / step.ProjectileSpeed
                    : 0f;
                var impactAt = launchAt + travel;
                var endAt = impactAt + Mathf.Max(0.3f, step.ProgressionDuration, step.VisualDuration * 0.5f);
                timeline.Add(new PreviewStep(
                    index,
                    step,
                    targetIndex,
                    targetPosition,
                    startAt,
                    launchAt,
                    impactAt,
                    endAt));
                time = endAt;
            }
        }

        private static Vector3 ResolveTargetPosition(
            MonsterActiveTargetPolicy policy,
            Vector3 previous,
            int stepIndex)
        {
            if (policy == MonsterActiveTargetPolicy.SameTarget || stepIndex == 0) return previous;
            var x = Mathf.Abs(previous.x) < 0.2f ? 1.2f : -Mathf.Sign(previous.x) * 1.2f;
            return new Vector3(x, previous.y, previous.z);
        }

        private void ShowStep(PreviewStep item)
        {
            if (item.TargetIndex < 0 || item.TargetIndex >= targetPositions.Count) return;
            var target = item.TargetPosition;
            targets[item.TargetIndex].transform.localPosition = target;
            var origin = attacker == null ? Vector3.zero : attacker.transform.localPosition;
            var forward = target - origin;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            if (item.Step.TeleportBeforeAttack && attacker != null)
            {
                attacker.transform.localPosition = target - forward * item.Step.TeleportFrontDistance;
                origin = attacker.transform.localPosition;
            }
            if (indicator != null) UnityEngine.Object.DestroyImmediate(indicator.gameObject);
            indicator = MonsterAttackAreaIndicator.CreateActive(
                root.transform,
                item.Step,
                origin,
                forward,
                target,
                new Color(0.1f, 1f, 0.85f, 1f),
                false);
            status = $"#{item.SourceIndex + 1:00} {item.Step.DisplayName} · {GetPatternLabel(item.Step.Pattern)} · " +
                     (item.Step.TargetPolicy == MonsterActiveTargetPolicy.SameTarget ? "같은 대상" : "다른 대상");
        }

        private void UpdateDelivery(float elapsed, int activeIndex)
        {
            if (delivery == null || activeIndex < 0 || activeIndex >= timeline.Count)
            {
                delivery?.SetActive(false);
                return;
            }
            var item = timeline[activeIndex];
            if (!item.Step.IsProjectile || elapsed < item.LaunchAt || elapsed > item.ImpactAt)
            {
                delivery.SetActive(false);
                return;
            }
            delivery.SetActive(true);
            var origin = attacker == null ? Vector3.zero : attacker.transform.localPosition;
            var ratio = Mathf.InverseLerp(item.LaunchAt, item.ImpactAt, elapsed);
            delivery.transform.localPosition = Vector3.Lerp(origin, item.TargetPosition, ratio) +
                                               Vector3.up * 0.25f;
            delivery.transform.localScale = item.Step.ProjectileFormation == MonsterActiveProjectileFormation.Fan
                ? new Vector3(0.65f, 0.18f, 0.35f)
                : Vector3.one * 0.22f;
        }

        private void PulseVictims(PreviewStep item, float elapsed)
        {
            var origin = attacker == null ? Vector3.zero : attacker.transform.localPosition;
            var target = item.TargetPosition;
            var forward = target - origin;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            var applied = 0;
            for (var index = 0; index < targetPositions.Count && applied < item.Step.MaxTargets; index++)
            {
                if (!IsInsideShape(item.Step, targetPositions[index], target, origin, forward)) continue;
                targetPulseTimes[index] = elapsed;
                applied++;
            }
            if (applied > 0) SpawnImpactFeel(item, target, forward, elapsed);
            status = $"#{item.SourceIndex + 1:00} 적중 · {applied}명 · 피해 {item.Step.DamageMultiplier:0.##}배";
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
            feelInstances.Add(new FeelPreviewInstance(instance, elapsed + feel.Lifetime));
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

        private static bool IsInsideShape(
            MonsterActiveAttackStep step,
            Vector3 point,
            Vector3 primary,
            Vector3 origin,
            Vector3 forward)
        {
            var delta = point - origin;
            var distance = delta.magnitude;
            switch (step.Pattern)
            {
                case MonsterActiveAttackPattern.Line:
                case MonsterActiveAttackPattern.PiercingBeam:
                    return IsInsideLine(delta, forward, step.Range, step.Width * 0.5f + 0.25f);
                case MonsterActiveAttackPattern.Cone:
                    return distance <= step.Range + 0.25f && Vector3.Angle(forward, delta) <= step.Angle * 0.5f;
                case MonsterActiveAttackPattern.SelfCircle:
                    return distance <= step.Radius + 0.25f;
                case MonsterActiveAttackPattern.FrontCircle:
                    return Vector3.Distance(point, origin + forward * step.ForwardOffset) <= step.Radius + 0.25f;
                case MonsterActiveAttackPattern.PiercingProjectile:
                    return step.ProjectileFormation == MonsterActiveProjectileFormation.Fan
                        ? distance <= step.Range + 0.25f &&
                          Vector3.Angle(forward, delta) <= step.ProjectileFanAngle * 0.5f
                        : IsInsideLine(delta, forward, step.Range, step.ProjectileCollisionRadius + 0.25f);
                case MonsterActiveAttackPattern.ExplosiveProjectile:
                    return Vector3.Distance(point, primary) <= step.ExplosionRadius + 0.25f ||
                           (step.ProjectileFormation == MonsterActiveProjectileFormation.Fan &&
                            distance <= step.Range + 0.25f &&
                            Vector3.Angle(forward, delta) <= step.ProjectileFanAngle * 0.5f);
                case MonsterActiveAttackPattern.InstantMagic:
                    return step.InstantMagicTarget == MonsterActiveInstantMagicTarget.SingleTarget
                        ? Vector3.Distance(point, primary) < 0.1f
                        : Vector3.Distance(point, primary) <= step.Radius + 0.25f;
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
            attacker = CreatePrimitive(PrimitiveType.Capsule, "Caster", new Vector3(0f, 0f, 0.15f),
                new Vector3(0.55f, 0.45f, 0.55f), sourceMaterial);
            var targetPosition = new Vector3(0f, 0f, 3.2f);
            targetPositions.Add(targetPosition);
            targets.Add(CreatePrimitive(PrimitiveType.Capsule, "Target", targetPosition,
                Vector3.one * 0.42f, targetMaterial));
            targetPulseTimes = new float[1];
            for (var index = 0; index < targetPulseTimes.Length; index++) targetPulseTimes[index] = -10f;
            delivery = CreatePrimitive(PrimitiveType.Sphere, "Delivery", Vector3.zero,
                Vector3.one * 0.22f, attackMaterial);
            delivery.SetActive(false);
            utility.AddSingleGO(root);
            ShowStaticStep(0);
        }

        private void ShowStaticStep(int index)
        {
            if (profile == null || profile.Steps.Count == 0) return;
            index = Mathf.Clamp(index, 0, profile.Steps.Count - 1);
            var step = profile.Steps[index];
            var target = targetPositions[0];
            var forward = target.normalized;
            indicator = MonsterAttackAreaIndicator.CreateActive(root.transform, step, Vector3.zero,
                forward, target, new Color(0.1f, 1f, 0.85f, 1f), false);
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
            if (attacker != null) attacker.transform.localPosition = new Vector3(0f, 0f, 0.15f);
            if (delivery != null) delivery.SetActive(false);
            for (var index = 0; index < targets.Count; index++) targets[index].transform.localScale = Vector3.one * 0.42f;
            for (var index = 0; index < targetPulseTimes.Length; index++) targetPulseTimes[index] = -10f;
            if (indicator != null) UnityEngine.Object.DestroyImmediate(indicator.gameObject);
            indicator = null;
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
            timeline.Clear();
            ClearFeelInstances();
            targets.Clear();
            targetPositions.Clear();
            indicator = null;
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            root = null;
            attacker = null;
            delivery = null;
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
                int targetIndex,
                Vector3 targetPosition,
                float startAt, float launchAt, float impactAt, float endAt)
            {
                SourceIndex = sourceIndex;
                Step = step;
                TargetIndex = targetIndex;
                TargetPosition = targetPosition;
                StartAt = startAt;
                LaunchAt = launchAt;
                ImpactAt = impactAt;
                EndAt = endAt;
            }

            public int SourceIndex { get; }
            public MonsterActiveAttackStep Step { get; }
            public int TargetIndex { get; }
            public Vector3 TargetPosition { get; }
            public float StartAt { get; }
            public float LaunchAt { get; }
            public float ImpactAt { get; }
            public float EndAt { get; }
            public bool Impacted { get; set; }
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
