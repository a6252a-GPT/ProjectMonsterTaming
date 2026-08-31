using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProjectMT.EditorTools.MonsterMaker;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    // V1 EditorWindow와 무관하게 기본공격 판정·타이밍·VFX/SFX·FEEL을 재생하는 V2 Preview.
    internal sealed class MonsterBasicAttackWorkshopPreviewV2 : IDisposable
    {
        private const string StandardFeelTargetPath =
            "Assets/ProjectMT/03_Features/Expedition/Prefabs/PF_Enemy_Knight_T1.prefab";

        private readonly List<GameObject> projectiles = new List<GameObject>();
        private readonly List<ContractPreviewVfx> contractVfx = new List<ContractPreviewVfx>();
        private readonly List<PendingContractVfx> pendingContractVfx = new List<PendingContractVfx>();
        private readonly HashSet<string> contractClaims = new HashSet<string>();
        private PreviewRenderUtility utility;
        private MonsterBasicAttackProfile profile;
        private BasicAttackWorkshopRecipe recipe;
        private MonsterMakerDraft originDraft;
        private GameObject root, attacker, target, impactPulse, launchVfx, impactVfx, launchFeel, impactFeel;
        private Material groundMaterial, sourceMaterial, targetMaterial, attackMaterial;
        private Vector3 attackerStart, targetStart;
        private Vector3 targetBaseScale = Vector3.one * 0.45f;
        private string sourceSignature = string.Empty;
        private double playbackStartedAt;
        private bool playing, deliveryActivated, lastImpactHasFeedback;
        private float activationElapsed = -1f, lastImpactElapsed = -1f;
        private int nextImpactIndex;
        private int selectedMotionIndex;
        private MonsterImpactStrength impactStrength = MonsterImpactStrength.Standard;

        internal bool IsPlaying => playing;
        internal string Summary => BuildTimingSummary();

        internal void SetSource(
            MonsterBasicAttackProfile nextProfile,
            BasicAttackWorkshopRecipe nextRecipe,
            MonsterMakerDraft nextDraft)
        {
            var signature = nextProfile == null ? string.Empty : JsonUtility.ToJson(nextProfile);
            signature += $"|{BuildContractSignature(nextDraft)}";
            profile = nextProfile;
            recipe = nextRecipe;
            originDraft = nextDraft;
            selectedMotionIndex = Mathf.Clamp(selectedMotionIndex, 0,
                Mathf.Max(0, (originDraft?.Attacks?.Count ?? 0) - 1));
            if (string.Equals(sourceSignature, signature, StringComparison.Ordinal) && utility != null) return;
            sourceSignature = signature;
            Refresh();
        }

        internal void SetImpactStrength(MonsterImpactStrength strength) => impactStrength = strength;

        internal void SetMotionIndex(int index)
        {
            var next = Mathf.Clamp(index, 0, Mathf.Max(0, (originDraft?.Attacks?.Count ?? 0) - 1));
            if (selectedMotionIndex == next) return;
            selectedMotionIndex = next;
            Stop();
        }

        internal void Refresh()
        {
            ClearContents();
            if (profile == null) return;

            MonsterWorkshopPreviewSceneRecovery.RecoverOrphanedScenesIfNeeded();
            if (utility != null && !MonsterWorkshopPreviewSceneRecovery.HasRenderingMask(utility))
            {
                MonsterWorkshopPreviewSceneRecovery.UnregisterOwner(utility);
                utility.Cleanup();
                utility = null;
            }
            utility ??= CreateUtility();

            root = new GameObject("[Basic Attack Workshop V2 Preview]") { hideFlags = HideFlags.HideAndDontSave };
            groundMaterial = CreateMaterial(new Color(0.11f, 0.13f, 0.16f));
            sourceMaterial = CreateMaterial(new Color(0.15f, 0.8f, 0.7f));
            targetMaterial = CreateMaterial(new Color(0.95f, 0.35f, 0.3f));
            attackMaterial = CreateMaterial(new Color(1f, 0.8f, 0.15f));
            CreatePrimitive(PrimitiveType.Cube, "Ground", new Vector3(0f, -0.08f, 1.8f), new Vector3(8f, 0.1f, 8f), groundMaterial);
            attacker = CreatePrimitive(PrimitiveType.Capsule, "Attacker", new Vector3(0f, 0f, 0.15f), new Vector3(0.55f, 0.45f, 0.55f), sourceMaterial);

            var baseRange = ResolveBaseAttackRange();
            var resolvedRange = Mathf.Min(profile.ResolveRange(baseRange), 4.5f);
            var targetPoint = Vector3.forward * Mathf.Max(0.7f, resolvedRange);
            target = CreateTarget(targetPoint);
            attackerStart = attacker.transform.localPosition;
            targetStart = target.transform.localPosition;

            if (profile.UsesProjectileVisual)
            {
                for (var index = 0; index < profile.ProjectileCount; index++)
                {
                    var projectile = CreateProjectile(index, attackerStart);
                    projectile.SetActive(false);
                    projectiles.Add(projectile);
                }
            }

            launchVfx = CreateFeedback(profile.LaunchFeedback, "Launch VFX", attackerStart);
            impactVfx = CreateFeedback(profile.ImpactFeedback, "Impact VFX", targetPoint);
            launchFeel = CreateFeel(profile.LaunchFeel, "Launch FEEL", attackerStart);
            impactFeel = CreateFeel(profile.ImpactFeel, "Impact FEEL", targetPoint);
            impactPulse = CreatePrimitive(PrimitiveType.Sphere, "Impact / Explosion", targetPoint, Vector3.one * 0.01f, attackMaterial);
            impactPulse.SetActive(false);

            MonsterAttackAreaIndicator.Create(root.transform, profile, Vector3.zero, Vector3.forward,
                targetPoint, baseRange, new Color(0.1f, 1f, 0.85f, 1f), false);
            utility.AddSingleGO(root);
            ResetPlaybackObjects();
        }

        internal void Play()
        {
            if (profile == null) return;
            if (utility == null || attacker == null || target == null) Refresh();
            if (attacker == null || target == null) return;
            playbackStartedAt = EditorApplication.timeSinceStartup;
            playing = true;
            deliveryActivated = false;
            activationElapsed = -1f;
            lastImpactElapsed = -1f;
            nextImpactIndex = 0;
            ResetPlaybackObjects();
            QueueContractEvent(MonsterBasicAttackVfxEvent.MotionStart, 0f, 0);
            QueueContractEvent(
                MonsterBasicAttackVfxEvent.RecipeExecute,
                ResolveActivationTime(ResolveMotionDuration()),
                0);
            TickContractVfx(0f);
        }

        internal void Stop()
        {
            playing = false;
            deliveryActivated = false;
            activationElapsed = -1f;
            ResetPlaybackObjects();
        }

        internal void Render(Rect rect, bool topDown)
        {
            if (utility == null) Refresh();
            if (utility == null || rect.width <= 1f || rect.height <= 1f) return;
            if (!MonsterWorkshopPreviewSceneRecovery.HasRenderingMask(utility)) Refresh();
            Tick();
            ConfigureCamera(rect, topDown);
            if (Event.current.type != EventType.Repaint) return;
            utility.BeginPreview(rect, GUIStyle.none);
            utility.Render(true);
            GUI.DrawTexture(rect, utility.EndPreview(), ScaleMode.ScaleToFit, false);
        }

        private void Tick()
        {
            if (!playing || profile == null || attacker == null || target == null) return;
            var elapsed = (float)(EditorApplication.timeSinceStartup - playbackStartedAt);
            var motionDuration = ResolveMotionDuration();
            var activationTime = ResolveActivationTime(motionDuration);
            var impactTimes = ResolveImpactTimes(motionDuration);
            if (!deliveryActivated && elapsed >= activationTime) ActivateDelivery(elapsed);
            while (nextImpactIndex < impactTimes.Count && elapsed >= impactTimes[nextImpactIndex])
            {
                TriggerImpact(elapsed, nextImpactIndex);
                nextImpactIndex++;
            }

            var impactAge = lastImpactElapsed < 0f ? float.MaxValue : elapsed - lastImpactElapsed;
            var intensity = ResolveImpactIntensity();
            var targetPulse = impactAge < 0.22f ? 1f + Mathf.Sin(Mathf.Clamp01(impactAge / 0.22f) * Mathf.PI) * 0.13f * intensity : 1f;
            target.transform.localScale = targetBaseScale * targetPulse;

            if (profile.MovementModule == MonsterBasicAttackMovementModule.Dash && !deliveryActivated)
            {
                var ratio = activationTime <= 0f ? 1f : Mathf.Clamp01(elapsed / activationTime);
                var direction = (targetStart - attackerStart).normalized;
                attacker.transform.localPosition = attackerStart + direction * Mathf.Min(profile.DashDistance, 1.5f) * Mathf.SmoothStep(0f, 1f, ratio);
            }

            UpdateProjectiles(elapsed);
            TickContractVfx(elapsed);
            UpdateTimedObject(launchVfx, activationElapsed, elapsed, profile.LaunchFeedback?.VfxLifetime ?? 0.4f, true);
            UpdateTimedObject(launchFeel, activationElapsed, elapsed, profile.LaunchFeel?.Lifetime ?? 0.4f, false);
            var feedbackStartedAt = lastImpactHasFeedback ? lastImpactElapsed : -1f;
            UpdateTimedObject(impactVfx, feedbackStartedAt, elapsed, profile.ImpactFeedback?.VfxLifetime ?? 0.4f, true);
            UpdateTimedObject(impactFeel, feedbackStartedAt, elapsed, profile.ImpactFeel?.Lifetime ?? 0.4f, false);
            UpdateImpactPulse(impactAge);
            if (elapsed >= ResolvePlaybackDuration(motionDuration, impactTimes)) Stop();
        }

        private void ActivateDelivery(float elapsed)
        {
            deliveryActivated = true;
            activationElapsed = elapsed;
            PlaySfx(recipe?.launchSfx);
            if (profile.UsesProjectileVisual) PlaySfx(recipe?.projectileSfx);
            foreach (var projectile in projectiles) if (projectile != null) projectile.SetActive(true);
            if (launchVfx != null) launchVfx.SetActive(true);
            if (launchFeel != null)
            {
                launchFeel.SetActive(true);
                PlayFeel(launchFeel, attacker, 1f);
            }
        }

        private void TriggerImpact(float elapsed, int hitIndex)
        {
            lastImpactElapsed = elapsed;
            var impactEvent = profile.ProjectileTravel == MonsterBasicAttackProjectileTravel.Returning
                ? hitIndex == 0
                    ? MonsterBasicAttackVfxEvent.OutboundTargetDamaged
                    : MonsterBasicAttackVfxEvent.ReturnTargetDamaged
                : MonsterBasicAttackVfxEvent.TargetDamaged;
            QueueContractEvent(impactEvent, elapsed, hitIndex);
            var impactCount = ResolveImpactTimes(ResolveMotionDuration()).Count;
            if (hitIndex >= impactCount - 1)
            {
                if (profile.CollisionModule == MonsterBasicAttackCollisionModule.AreaImpact ||
                    profile.Shape == MonsterBasicAttackShape.Circle)
                {
                    QueueContractEvent(MonsterBasicAttackVfxEvent.AreaResolved, elapsed, hitIndex);
                }
                QueueContractEvent(MonsterBasicAttackVfxEvent.SequenceEnd, elapsed, hitIndex);
            }
            TickContractVfx(elapsed);
            lastImpactHasFeedback = hitIndex == 0 || profile.RepeatImpactFeedback;
            if (!lastImpactHasFeedback) return;
            PlaySfx(recipe?.impactSfx);
            if (impactVfx != null) impactVfx.SetActive(true);
            if (impactFeel != null)
            {
                impactFeel.SetActive(true);
                PlayFeel(impactFeel, target, ResolveImpactIntensity());
            }
        }

        private void UpdateProjectiles(float elapsed)
        {
            if (!deliveryActivated || projectiles.Count == 0) return;
            var travelDuration = ResolveTravelDuration();
            var travelAge = Mathf.Max(0f, elapsed - activationElapsed);
            var baseTravel = Mathf.Clamp01(travelAge / travelDuration);
            if (profile.ProjectileTravel == MonsterBasicAttackProjectileTravel.Returning)
                baseTravel = Mathf.PingPong(travelAge / travelDuration, 1f);

            for (var index = 0; index < projectiles.Count; index++)
            {
                var projectile = projectiles[index];
                if (projectile == null) continue;
                var ratio = projectiles.Count <= 1 ? 0f : index / (float)(projectiles.Count - 1) - 0.5f;
                var endOffset = Quaternion.Euler(0f, ratio * profile.ProjectileSpreadAngle, 0f) * (targetStart - attackerStart);
                projectile.transform.localPosition = Vector3.Lerp(attackerStart, attackerStart + endOffset, baseTravel);
                SimulateParticles(projectile, travelAge);
                var completed = profile.ProjectileTravel == MonsterBasicAttackProjectileTravel.Returning
                    ? travelAge >= travelDuration * 2f
                    : travelAge >= travelDuration;
                if (profile.ProjectileTravel == MonsterBasicAttackProjectileTravel.Returning &&
                    travelAge >= travelDuration && !completed)
                {
                    QueueContractEvent(
                        MonsterBasicAttackVfxEvent.DeliveryTurn,
                        elapsed,
                        0,
                        projectile);
                }
                if (completed && projectile.activeSelf)
                {
                    QueueContractEvent(
                        MonsterBasicAttackVfxEvent.DeliveryEnd,
                        elapsed,
                        0,
                        projectile);
                    projectile.SetActive(false);
                }
            }
        }

        private void UpdateImpactPulse(float impactAge)
        {
            if (impactPulse == null) return;
            var explosion = profile.CollisionModule == MonsterBasicAttackCollisionModule.AreaImpact ||
                            profile.Shape == MonsterBasicAttackShape.Circle;
            var progress = Mathf.Clamp01(impactAge / 0.28f);
            var active = explosion && impactAge >= 0f && progress < 1f;
            impactPulse.SetActive(active);
            if (active) impactPulse.transform.localScale = Vector3.one *
                Mathf.Lerp(0.05f, Mathf.Max(profile.Radius, 0.35f) * 2f, Mathf.SmoothStep(0f, 1f, progress));
        }

        private float ResolveMotionDuration()
        {
            var attack = ResolveSelectedMotion();
            return attack?.Clip == null ? 1.15f : Mathf.Max(0.05f, attack.Clip.length / attack.PlaybackSpeed);
        }

        private float ResolveMarker()
        {
            var attack = ResolveSelectedMotion();
            var marker = attack?.Markers?.Where(item => item != null)
                .OrderBy(item => item.NormalizedTime).FirstOrDefault();
            return marker == null ? 0.55f : Mathf.Clamp01(marker.NormalizedTime);
        }

        private MonsterMakerAttackDraft ResolveSelectedMotion()
        {
            var attacks = originDraft?.Attacks;
            if (attacks == null || attacks.Count == 0) return null;
            selectedMotionIndex = Mathf.Clamp(selectedMotionIndex, 0, attacks.Count - 1);
            return attacks[selectedMotionIndex];
        }

        private float ResolveActivationTime(float motionDuration) => ResolveMarker() * motionDuration;
        private float ResolveBaseAttackRange() => profile != null && profile.CombatType == MonsterCombatType.Melee ? 2f : 4f;
        private float ResolveTravelDuration() => profile == null || !profile.UsesProjectileVisual ? 0f :
            Mathf.Max(0.01f, Vector3.Distance(attackerStart, targetStart) / Mathf.Max(0.01f, profile.ProjectileSpeed));

        private List<float> ResolveImpactTimes(float motionDuration)
        {
            var result = new List<float>();
            if (profile == null) return result;
            var activation = ResolveActivationTime(motionDuration);
            if (!profile.UsesProjectileVisual)
            {
                for (var index = 0; index < profile.HitCount; index++) result.Add(activation + index * profile.RepeatHitInterval);
                return result;
            }
            var travel = ResolveTravelDuration();
            result.Add(activation + travel);
            if (profile.SequenceModule == MonsterBasicAttackSequenceModule.ReturnPasses)
                result.Add(activation + travel * 2f);
            return result;
        }

        private float ResolvePlaybackDuration(float motionDuration, IReadOnlyList<float> impacts)
        {
            var duration = Mathf.Max(0.05f, motionDuration);
            if (impacts.Count > 0) duration = Mathf.Max(duration, impacts[impacts.Count - 1] + 0.45f);
            if (profile.SequenceModule == MonsterBasicAttackSequenceModule.ReturnPasses)
                duration = Mathf.Max(duration, ResolveActivationTime(motionDuration) + ResolveTravelDuration() * 2f + 0.1f);
            return duration;
        }

        private string BuildTimingSummary()
        {
            if (profile == null) return "타이밍 계산 전";
            var motion = ResolveMotionDuration();
            var activation = ResolveActivationTime(motion);
            var impacts = ResolveImpactTimes(motion);
            var impactText = impacts.Count == 0 ? "피해 없음" : string.Join(" / ", impacts.Select((time, index) => $"피해 {index + 1}: {time:0.000}초"));
            return $"{(profile.UsesProjectileVisual ? "발사" : "판정")}: 동작 {ResolveMarker():0.000} ({activation:0.000}초) · {impactText}";
        }

        private static PreviewRenderUtility CreateUtility()
        {
            var result = new PreviewRenderUtility();
            result.camera.clearFlags = CameraClearFlags.SolidColor;
            result.camera.backgroundColor = new Color(0.055f, 0.065f, 0.08f, 1f);
            result.camera.nearClipPlane = 0.05f;
            result.camera.farClipPlane = 30f;
            result.lights[0].intensity = 1.25f;
            result.lights[0].transform.rotation = Quaternion.Euler(45f, 35f, 0f);
            result.ambientColor = new Color(0.35f, 0.35f, 0.4f);
            MonsterWorkshopPreviewSceneRecovery.RegisterOwner(result);
            return result;
        }

        private void ConfigureCamera(Rect rect, bool topDown)
        {
            if (topDown)
            {
                utility.camera.orthographic = true;
                utility.camera.orthographicSize = 3.4f;
                utility.camera.transform.position = new Vector3(0f, 10f, 2.1f);
                utility.camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            }
            else
            {
                utility.camera.orthographic = false;
                utility.camera.fieldOfView = 34f;
                utility.camera.transform.position = new Vector3(5.2f, 6.4f, -6.8f);
                utility.camera.transform.LookAt(new Vector3(0f, 0f, 2.1f));
            }
            utility.camera.aspect = rect.width / Mathf.Max(1f, rect.height);
        }

        private GameObject CreatePrimitive(PrimitiveType type, string objectName, Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = objectName;
            item.hideFlags = HideFlags.HideAndDontSave;
            item.transform.SetParent(root.transform, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            var collider = item.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            if (item.TryGetComponent<Renderer>(out var renderer)) renderer.sharedMaterial = material;
            return item;
        }

        private GameObject CreateTarget(Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StandardFeelTargetPath);
            if (prefab == null)
            {
                targetBaseScale = Vector3.one * 0.45f;
                return CreatePrimitive(PrimitiveType.Sphere, "Primary Target", position, targetBaseScale, targetMaterial);
            }
            var holder = new GameObject("Primary Target · FEEL Test Model") { hideFlags = HideFlags.HideAndDontSave };
            holder.transform.SetParent(root.transform, false);
            holder.transform.localPosition = position;
            var visual = UnityEngine.Object.Instantiate(prefab);
            SetHideFlags(visual);
            visual.transform.SetParent(holder.transform, false);
            foreach (var behaviour in visual.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { targetBaseScale = Vector3.one; return holder; }
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            var center = holder.transform.InverseTransformPoint(bounds.center);
            var bottom = holder.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            visual.transform.localPosition -= new Vector3(center.x, bottom.y, center.z);
            var largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            targetBaseScale = Vector3.one * (largest <= 0.0001f ? 1f : 1.1f / largest);
            holder.transform.localScale = targetBaseScale;
            return holder;
        }

        private GameObject CreateProjectile(int index, Vector3 position)
        {
            var holder = new GameObject($"Attack Delivery {index + 1:00}") { hideFlags = HideFlags.HideAndDontSave };
            holder.transform.SetParent(root.transform, false);
            holder.transform.localPosition = position;
            if (TryResolveDeliveryVisual(out var deliveryBinding) && deliveryBinding.Prefab != null)
                AddBindingChild(holder.transform, deliveryBinding);
            else if (profile.ProjectileFeedback?.VfxPrefab != null)
                AddFeedbackChild(holder.transform, profile.ProjectileFeedback);
            else
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "Fallback Projectile";
                marker.hideFlags = HideFlags.HideAndDontSave;
                marker.transform.SetParent(holder.transform, false);
                marker.transform.localScale = Vector3.one * 0.22f;
                var collider = marker.GetComponent<Collider>();
                if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
                marker.GetComponent<Renderer>().sharedMaterial = attackMaterial;
            }
            return holder;
        }

        private GameObject CreateFeedback(MonsterFeedbackCue cue, string objectName, Vector3 position)
        {
            if (cue?.VfxPrefab == null) return null;
            var holder = new GameObject(objectName) { hideFlags = HideFlags.HideAndDontSave };
            holder.transform.SetParent(root.transform, false);
            holder.transform.localPosition = position;
            AddFeedbackChild(holder.transform, cue);
            holder.SetActive(false);
            return holder;
        }

        private static void AddFeedbackChild(Transform parent, MonsterFeedbackCue cue)
        {
            var instance = UnityEngine.Object.Instantiate(cue.VfxPrefab);
            SetHideFlags(instance);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = cue.LocalPosition;
            instance.transform.localRotation = cue.LocalRotation;
            instance.transform.localScale = cue.VfxPrefab.transform.localScale * cue.Scale;
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        }

        private void AddBindingChild(Transform parent, MonsterBasicAttackVfxBinding binding)
        {
            var instance = UnityEngine.Object.Instantiate(binding.Prefab);
            SetHideFlags(instance);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = binding.LocalPosition;
            instance.transform.localRotation = binding.LocalRotation;
            MonsterBasicAttackVfxPlayback.ApplyInstanceScale(
                instance,
                binding.Prefab.transform.localScale *
                binding.Scale * Mathf.Max(0.01f, originDraft?.VfxScale ?? 1f));
            instance.SetActive(true);
            MonsterBasicAttackVfxPlayback.RestartAtOffset(
                instance,
                binding.PlaybackOffset,
                playbackSpeed: binding.PlaybackSpeed);
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;
        }

        private bool TryResolveDeliveryVisual(out MonsterBasicAttackVfxBinding binding)
        {
            binding = null;
            if (profile == null || originDraft == null) return false;
            return MonsterBasicAttackVfxResolver.TryResolveDeliveryVisual(
                profile,
                originDraft.BasicAttackVfxBindings,
                ResolveSelectedMotion()?.MotionId,
                out _,
                out binding);
        }

        private void QueueContractEvent(
            MonsterBasicAttackVfxEvent eventType,
            float eventTime,
            int damageStage,
            GameObject projectile = null)
        {
            if (profile == null || originDraft == null) return;
            foreach (var slot in profile.VfxSlots)
            {
                if (slot == null || slot.EventType != eventType ||
                    !TryResolveContractPresentation(slot, out var binding))
                    continue;

                var suffix = slot.Multiplicity switch
                {
                    MonsterBasicAttackVfxMultiplicity.PerProjectile =>
                        projectile == null ? "none" : projectile.GetInstanceID().ToString(),
                    MonsterBasicAttackVfxMultiplicity.PerTargetHit => $"target:{damageStage}",
                    MonsterBasicAttackVfxMultiplicity.PerDamageStage => damageStage.ToString(),
                    _ => "once"
                };
                var claimPrefix = $"{eventType}|{slot.SlotId}|{suffix}";
                if (binding.HasSound && contractClaims.Add(claimPrefix + "|sfx"))
                    PlayContractSfx(binding);

                if (slot.IsDeliveryVisual ||
                    binding.State != MonsterBasicAttackVfxAssignmentState.Assigned ||
                    !contractClaims.Add(claimPrefix + "|vfx"))
                    continue;

                pendingContractVfx.Add(new PendingContractVfx(
                    Mathf.Max(0f, eventTime + slot.ClampTimingOffset(binding.EventTimingOffset)),
                    slot,
                    binding,
                    damageStage,
                    projectile));
            }
        }

        private bool TryResolveContractPresentation(
            MonsterBasicAttackVfxSlot slot,
            out MonsterBasicAttackVfxBinding binding)
        {
            var resolved = MonsterBasicAttackVfxResolver.TryResolvePresentation(
                originDraft.BasicAttackVfxBindings,
                profile.AttackId,
                slot,
                ResolveSelectedMotion()?.MotionId,
                out binding);
            return resolved || binding?.State == MonsterBasicAttackVfxAssignmentState.Assigned;
        }

        private void TickContractVfx(float elapsed)
        {
            for (var index = pendingContractVfx.Count - 1; index >= 0; index--)
            {
                var pending = pendingContractVfx[index];
                if (elapsed + 0.0001f < pending.ExecuteAt) continue;
                pendingContractVfx.RemoveAt(index);
                SpawnContractVfx(pending, elapsed);
            }

            for (var index = contractVfx.Count - 1; index >= 0; index--)
            {
                var active = contractVfx[index];
                if (active.Instance == null)
                {
                    contractVfx.RemoveAt(index);
                    continue;
                }
                var age = Mathf.Max(0f, elapsed - active.StartedAt);
                if ((active.EndPolicy is MonsterBasicAttackVfxEndPolicy.Timed or
                    MonsterBasicAttackVfxEndPolicy.ParticleDuration) &&
                    age >= active.Lifetime)
                {
                    UnityEngine.Object.DestroyImmediate(active.Instance);
                    contractVfx.RemoveAt(index);
                    continue;
                }
                SimulateContractParticles(
                    active.Instance,
                    active.PlaybackOffset + age * active.PlaybackSpeed);
            }
        }

        private void SpawnContractVfx(PendingContractVfx pending, float elapsed)
        {
            ResolveContractPose(
                pending.Slot.Anchor,
                pending.Projectile,
                out var anchor,
                out var position,
                out var rotation);
            position += rotation * pending.Binding.LocalPosition;
            rotation *= pending.Binding.LocalRotation;

            var placeholder = pending.Binding.Prefab == null;
            GameObject instance;
            if (placeholder)
            {
                instance = CreatePrimitive(
                    PrimitiveType.Sphere,
                    $"[Preview VFX Placeholder] 기본공격 · {pending.Slot.DisplayName}",
                    position,
                    Vector3.one * 0.32f * pending.Binding.Scale,
                    attackMaterial);
            }
            else
            {
                instance = UnityEngine.Object.Instantiate(pending.Binding.Prefab);
                instance.name = $"[Basic Attack VFX] {pending.Slot.DisplayName}";
                SetHideFlags(instance);
                instance.transform.SetParent(root.transform, false);
                instance.transform.SetPositionAndRotation(position, rotation);
                MonsterBasicAttackVfxPlayback.ApplyInstanceScale(
                    instance,
                    pending.Binding.Prefab.transform.localScale *
                    pending.Binding.Scale * Mathf.Max(0.01f, originDraft.VfxScale));
                instance.SetActive(true);
                MonsterBasicAttackVfxPlayback.RestartAtOffset(
                    instance,
                    pending.Binding.PlaybackOffset,
                    playbackSpeed: pending.Binding.PlaybackSpeed);
            }
            if (pending.Slot.Attachment == MonsterBasicAttackVfxAttachment.FollowAnchor &&
                anchor != null)
                instance.transform.SetParent(anchor, true);
            contractVfx.Add(new ContractPreviewVfx(
                instance,
                elapsed,
                pending.Binding.Lifetime,
                pending.Binding.PlaybackOffset,
                pending.Binding.PlaybackSpeed,
                pending.Slot.EndPolicy));
        }

        private void ResolveContractPose(
            MonsterBasicAttackVfxAnchor anchorKind,
            GameObject projectile,
            out Transform anchor,
            out Vector3 position,
            out Quaternion rotation)
        {
            var forward = target.transform.position - attacker.transform.position;
            forward.y = 0f;
            rotation = Quaternion.LookRotation(
                forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized,
                Vector3.up);
            anchor = null;
            position = attacker.transform.position;
            switch (anchorKind)
            {
                case MonsterBasicAttackVfxAnchor.SourceRoot:
                case MonsterBasicAttackVfxAnchor.AttackOrigin:
                case MonsterBasicAttackVfxAnchor.MarkerSocket:
                    anchor = attacker.transform;
                    break;
                case MonsterBasicAttackVfxAnchor.ProjectileRoot:
                    anchor = projectile?.transform;
                    break;
                case MonsterBasicAttackVfxAnchor.TargetRoot:
                    anchor = target.transform;
                    break;
                case MonsterBasicAttackVfxAnchor.HitPoint:
                    position = target.transform.position + Vector3.up * 0.4f;
                    return;
                case MonsterBasicAttackVfxAnchor.AreaCenter:
                    position = profile.Shape == MonsterBasicAttackShape.Circle &&
                               profile.Center == MonsterBasicAttackCenter.Source
                        ? attacker.transform.position
                        : target.transform.position;
                    return;
                case MonsterBasicAttackVfxAnchor.TrajectoryOrigin:
                    anchor = attacker.transform;
                    position = attacker.transform.position;
                    return;
            }
            if (anchor != null)
            {
                position = anchor.position;
                rotation = anchor.rotation;
            }
        }

        private static void SimulateContractParticles(GameObject item, float time)
        {
            foreach (var particle in item.GetComponentsInChildren<ParticleSystem>(true))
                particle.Simulate(Mathf.Max(0f, time), false, true, true);
        }

        private static void PlayContractSfx(MonsterBasicAttackVfxBinding binding)
        {
            if (binding.Sound != null)
                SfxEditorAudioPreview.Play(binding.Sound, 0, false, binding.SoundVolume);
            else
                PlaySfx(binding.Sfx);
        }

        private static string BuildContractSignature(MonsterMakerDraft draft)
        {
            if (draft == null) return "standalone";
            var bindings = draft.BasicAttackVfxBindings.Select(binding => binding == null
                ? "null"
                : string.Join("|",
                    binding.AttackId,
                    binding.SlotId,
                    binding.MotionId,
                    binding.State,
                    binding.Prefab == null ? 0 : binding.Prefab.GetInstanceID(),
                    binding.Lifetime,
                    binding.PlaybackOffset,
                    binding.PlaybackSpeed,
                    binding.EventTimingOffset,
                    binding.LocalPosition,
                    binding.Scale));
            var motions = draft.Attacks.Select(attack => attack?.MotionId ?? string.Empty);
            return $"{draft.GetInstanceID()}::{string.Join(";", motions)}::{string.Join(";", bindings)}";
        }

        private GameObject CreateFeel(BasicAttackFeelCue cue, string objectName, Vector3 position)
        {
            if (cue?.Prefab == null) return null;
            var holder = new GameObject(objectName) { hideFlags = HideFlags.HideAndDontSave };
            holder.transform.SetParent(root.transform, false);
            holder.transform.localPosition = position;
            var instance = UnityEngine.Object.Instantiate(cue.Prefab);
            SetHideFlags(instance);
            instance.transform.SetParent(holder.transform, false);
            instance.transform.localPosition = cue.LocalPosition;
            instance.transform.localRotation = cue.LocalRotation;
            instance.transform.localScale = cue.Prefab.transform.localScale * cue.Scale;
            holder.SetActive(false);
            return holder;
        }

        private void ResetPlaybackObjects()
        {
            for (var index = contractVfx.Count - 1; index >= 0; index--)
                if (contractVfx[index].Instance != null)
                    UnityEngine.Object.DestroyImmediate(contractVfx[index].Instance);
            contractVfx.Clear();
            pendingContractVfx.Clear();
            contractClaims.Clear();
            if (attacker != null) { attacker.transform.localPosition = attackerStart; attacker.transform.localScale = new Vector3(0.55f, 0.45f, 0.55f); }
            if (target != null) { target.transform.localPosition = targetStart; target.transform.localScale = targetBaseScale; }
            foreach (var projectile in projectiles) if (projectile != null) { projectile.transform.localPosition = attackerStart; projectile.SetActive(false); }
            if (impactPulse != null) { impactPulse.transform.localPosition = targetStart; impactPulse.transform.localScale = Vector3.one * 0.01f; impactPulse.SetActive(false); }
            if (launchVfx != null) launchVfx.SetActive(false);
            if (impactVfx != null) impactVfx.SetActive(false);
            ResetFeel(launchFeel);
            ResetFeel(impactFeel);
            nextImpactIndex = 0;
            lastImpactElapsed = -1f;
            lastImpactHasFeedback = false;
        }

        private static void UpdateTimedObject(GameObject item, float startedAt, float elapsed, float lifetime, bool particles)
        {
            if (item == null) return;
            var age = startedAt < 0f ? float.MaxValue : elapsed - startedAt;
            var active = age >= 0f && age < Mathf.Max(0.05f, lifetime);
            item.SetActive(active);
            if (active && particles) SimulateParticles(item, age);
        }

        private float ResolveImpactIntensity() => impactStrength switch
        {
            MonsterImpactStrength.Light => 0.62f,
            MonsterImpactStrength.Heavy => 1.45f,
            _ => 1f
        };

        private static void PlayFeel(GameObject feelRoot, GameObject feelTarget, float intensity)
        {
            var runtime = feelRoot?.GetComponentsInChildren<MonoBehaviour>(true).OfType<IBasicAttackFeelRuntime>().FirstOrDefault();
            runtime?.PlayBasicAttackFeel(feelRoot.transform.position, feelTarget, intensity, BasicAttackFeelPlaybackOptions.None);
        }

        private static void ResetFeel(GameObject feelRoot)
        {
            if (feelRoot == null) return;
            feelRoot.GetComponentsInChildren<MonoBehaviour>(true).OfType<IBasicAttackFeelRuntime>().FirstOrDefault()?.ResetBasicAttackFeel();
            feelRoot.SetActive(false);
        }

        private static void SimulateParticles(GameObject item, float time)
        {
            if (item == null) return;
            foreach (var particle in item.GetComponentsInChildren<ParticleSystem>(true))
                particle.Simulate(Mathf.Max(0f, time), true, true, false);
        }

        private static void PlaySfx(SfxCue cue)
        {
            if (cue == null || !cue.TrySelectClip(out var clip) || clip == null) return;
            var audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            var method = audioUtil?.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name == "PlayPreviewClip");
            if (method == null) return;
            var count = method.GetParameters().Length;
            var arguments = count switch { 1 => new object[] { clip }, 2 => new object[] { clip, 0 }, _ => new object[] { clip, 0, false } };
            try { method.Invoke(null, arguments); } catch { /* Unity 버전별 AudioUtil 차이는 미리보기만 생략합니다. */ }
        }

        private static void SetHideFlags(GameObject item)
        {
            foreach (var child in item.GetComponentsInChildren<Transform>(true)) child.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return shader == null ? null : new Material(shader) { color = color, hideFlags = HideFlags.HideAndDontSave };
        }

        private void ClearContents()
        {
            playing = false;
            contractVfx.Clear();
            pendingContractVfx.Clear();
            contractClaims.Clear();
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            root = attacker = target = impactPulse = launchVfx = impactVfx = launchFeel = impactFeel = null;
            projectiles.Clear();
            DestroyMaterial(ref groundMaterial);
            DestroyMaterial(ref sourceMaterial);
            DestroyMaterial(ref targetMaterial);
            DestroyMaterial(ref attackMaterial);
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
            material = null;
        }

        private readonly struct PendingContractVfx
        {
            public PendingContractVfx(
                float executeAt,
                MonsterBasicAttackVfxSlot slot,
                MonsterBasicAttackVfxBinding binding,
                int damageStage,
                GameObject projectile)
            {
                ExecuteAt = executeAt;
                Slot = slot;
                Binding = binding;
                DamageStage = damageStage;
                Projectile = projectile;
            }

            public float ExecuteAt { get; }
            public MonsterBasicAttackVfxSlot Slot { get; }
            public MonsterBasicAttackVfxBinding Binding { get; }
            public int DamageStage { get; }
            public GameObject Projectile { get; }
        }

        private readonly struct ContractPreviewVfx
        {
            public ContractPreviewVfx(
                GameObject instance,
                float startedAt,
                float lifetime,
                float playbackOffset,
                float playbackSpeed,
                MonsterBasicAttackVfxEndPolicy endPolicy)
            {
                Instance = instance;
                StartedAt = startedAt;
                Lifetime = lifetime;
                PlaybackOffset = playbackOffset;
                PlaybackSpeed = playbackSpeed;
                EndPolicy = endPolicy;
            }

            public GameObject Instance { get; }
            public float StartedAt { get; }
            public float Lifetime { get; }
            public float PlaybackOffset { get; }
            public float PlaybackSpeed { get; }
            public MonsterBasicAttackVfxEndPolicy EndPolicy { get; }
        }

        public void Dispose()
        {
            ClearContents();
            if (utility != null)
            {
                MonsterWorkshopPreviewSceneRecovery.UnregisterOwner(utility);
                utility.Cleanup();
            }
            utility = null;
        }
    }
}
