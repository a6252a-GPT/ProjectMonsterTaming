using System;
using System.Linq;
using System.Reflection;
using System.Text;
using ProjectMT.Features.CommanderSkill;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using ProjectMT.Shared.CommanderSkill;
using AP = ProjectMT.Features.CommanderSkill.CommanderSkillAwakeningParameter;

namespace ProjectMT.EditorTools.CommanderSkillWorkshop
{
    internal sealed class CommanderSkillWorkshopPreview : IDisposable // 군단장 모델·판정·VFX 실재생 Stage
    {
        internal const string CommanderVisualPath =
            "Assets/ProjectMT/05_Art/Characters/Commander/PF_CommanderVisual.prefab";
        internal const string TargetVisualPath =
            "Assets/ProjectMT/03_Features/Expedition/Prefabs/PF_Enemy_Knight_T1.prefab";
        internal static readonly Vector3 CastAnchorLocalPosition = new Vector3(0f, 1.15f, 0.25f);
        internal static readonly Vector3 ImpactAnchorLocalPosition = new Vector3(0f, 0.45f, 0f);

        private readonly PrefabPreviewStage stage = new PrefabPreviewStage();
        private CommanderSkillWorkshopDraft draft;
        private GameObject commander;
        private Animator commanderAnimator;
        private int commanderAnimationStateHash;
        private float commanderAnimationDuration;
        private bool commanderAnimationPlaying;
        private GameObject target;
        private GameObject castingVfx;
        private GameObject castVfx;
        private GameObject impactVfx;
        private GameObject persistentVfx;
        private GameObject markVfx;
        private bool markPreview;
        private float markLifetime;
        private float markVfxAge = -1f;
        private float persistentVfxAge = -1f;
        private GameObject projectile;
        private GameObject impactPulse;
        private Material targetRingMaterial;
        private Material rangeMaterial;
        private Material impactMaterial;
        private string sourceSignature = string.Empty;
        private double playbackStartedAt;
        private double lastTickAt;
        private float activationTime;
        private float impactTime;
        private float playbackEndTime;
        private float targetDistance;
        private float castingVfxAge = -1f;
        private float castVfxAge = -1f;
        private float impactVfxAge = -1f;
        private bool playing;
        private bool looping = true;
        private bool activated;
        private bool impacted;
        private int patternImpactCount;
        private int patternImpactIndex;
        private float patternImpactInterval;
        private float lastImpactAt;

        public CommanderSkillWorkshopPreview()
        {
            stage.SetEnvironment(3);
            stage.SetView(154f, 15f, 1.05f);
        }

        public bool IsPlaying => playing;
        public int PreviewLevel { get; private set; } = 1;
        public int PreviewStar { get; private set; }
        private CommanderSkillGrowthSnapshot Growth { get; set; } = 1f;
        private float PersistentDuration => Growth.Resolve(AP.Duration, draft.PatternDuration);
        public void SetProgress(int level, int star)
        {
            PreviewLevel = Mathf.Clamp(level, 1, draft?.MaxLevel ?? 200);
            PreviewStar = Mathf.Clamp(star, 0, 5);
            Refresh();
        }
        public bool Looping => looping;
        public int EnvironmentIndex => stage.EnvironmentIndex;
        public string PhaseLabel { get; private set; } = "대기";

        public void SetSource(CommanderSkillWorkshopDraft source)
        {
            var signature = BuildVisualSignature(source);
            draft = source;
            if (stage.PreviewRoot != null && string.Equals(signature, sourceSignature, StringComparison.Ordinal))
            {
                return;
            }

            sourceSignature = signature;
            Refresh();
        }

        public void Refresh()
        {
            Stop();
            ClearStage();
            if (draft == null)
            {
                return;
            }

            var commanderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CommanderVisualPath);
            Growth = CommanderSkillGrowthSnapshot.FromRule(draft.BuildGrowthRule(), PreviewLevel).WithAwakening(
                new CommanderSkillAwakeningSnapshot(PreviewStar > 0 && PreviewStar <= draft.AwakeningStages.Length
                    ? draft.AwakeningStages[PreviewStar - 1] : null));
            if (commanderPrefab == null)
            {
                PhaseLabel = "군단장 모델 없음";
                return;
            }

            commander = stage.SetPrefab(commanderPrefab, instance =>
            {
                DisableRuntimeBehaviours(instance);
                GroundAndFit(instance, 2.05f);
                instance.transform.rotation = Quaternion.identity;
                commanderAnimator = instance.GetComponentInChildren<Animator>(true);
                PrepareCommanderAnimator();
            });
            if (commander == null)
            {
                PhaseLabel = "군단장 모델 생성 실패";
                return;
            }

            targetDistance = Mathf.Max(0.1f, Growth.Resolve(AP.TargetRange, draft.TargetRange));
            target = CreateTarget(new Vector3(0f, 0f, targetDistance));
            rangeMaterial = CreateLineMaterial(
                draft.Category == CommanderSkillCategory.Buff
                    ? new Color(0.24f, 0.92f, 0.55f, 0.9f)
                    : draft.Category == CommanderSkillCategory.Debuff
                        ? new Color(0.96f, 0.25f, 0.4f, 0.9f)
                        : new Color(0.2f, 0.72f, 1f, 0.9f));
            targetRingMaterial = CreateLineMaterial(
                draft.TargetTeam == CommanderSkillTargetTeam.Ally
                    ? new Color(0.22f, 0.96f, 0.58f, 0.95f)
                    : new Color(1f, 0.32f, 0.24f, 0.95f));
            impactMaterial = CreateLineMaterial(new Color(1f, 0.82f, 0.22f, 0.95f));
            CreateRangeVisuals();
            CreateFeedbackObjects();
            CreateProjectile();
            CreateImpactPulse();
            stage.RecalculateBounds(true);
            stage.SetFramingScale(0.95f);
            PhaseLabel = "대기 · 재생을 누르세요";
        }

        public void Play()
        {
            if (draft == null || commander == null || target == null)
            {
                Refresh();
            }
            if (draft == null || commander == null || target == null)
            {
                return;
            }

            ResetPlaybackObjects();
            StartCommanderAnimation();
            activationTime = Mathf.Max(0f, draft.CastTime);
            var travel = draft.Category == CommanderSkillCategory.Attack &&
                         draft.DeliveryModule == MonsterBasicAttackDeliveryModule.Projectile
                ? targetDistance / Mathf.Max(1f, draft.ProjectileSpeed)
                : 0f;
            impactTime = activationTime + travel;
            patternImpactCount = draft.PatternType switch
            {
                CommanderSkillPatternType.PersistentArea => Mathf.Max(1, Mathf.CeilToInt(PersistentDuration / draft.TickInterval)),
                CommanderSkillPatternType.Chain => Mathf.Max(1, Growth.ResolveCount(AP.ChainCount, draft.ChainCount)),
                CommanderSkillPatternType.Burst or CommanderSkillPatternType.Barrage or CommanderSkillPatternType.Pulse => Mathf.Max(1, Growth.ResolveCount(AP.RepeatCount, draft.RepeatCount)),
                _ => 1
            };
            patternImpactInterval = draft.PatternType == CommanderSkillPatternType.PersistentArea
                ? draft.TickInterval : draft.RepeatInterval;
            playbackEndTime = Mathf.Max(
                activationTime + Mathf.Max(0.05f, Growth.Resolve(AP.Cooldown, draft.Cooldown)),
                impactTime + Mathf.Max(0f, patternImpactCount - 1) * patternImpactInterval + Mathf.Max(0.45f, draft.ImpactVfxLifetime),
                Mathf.Max(0f, draft.CastingVfxLifetime));
            playbackStartedAt = EditorApplication.timeSinceStartup;
            if (draft.PatternType == CommanderSkillPatternType.PersistentArea)
                playbackEndTime = Mathf.Max(playbackEndTime, activationTime + PersistentDuration);
            lastTickAt = playbackStartedAt;
            playing = true;
            PhaseLabel = activationTime > 0f ? "캐스팅" : "발동";
            if (activationTime > 0f)
            {
                BeginCastingFeedback();
            }
            else
            {
                Activate(0f);
            }
        }

        public void Stop()
        {
            playing = false;
            activated = false;
            impacted = false;
            ResetPlaybackObjects();
            if (commander != null)
            {
                PhaseLabel = "대기 · 재생을 누르세요";
            }
        }

        public void PlayMarkFeedback(CommanderMarkFeedbackDraft slot)
        {
            Stop();
            if (slot == null || draft == null) return;
            if (commander == null || target == null) Refresh();
            if (commander == null || target == null) return;
            markVfx = CreateAnchoredVfx(slot.VfxPrefab, "Mark VFX", slot.Anchor,
                slot.LocalOffset, slot.LocalEuler, slot.Scale);
            markLifetime = Mathf.Max(0.05f, slot.Lifetime);
            markPreview = true;
            playing = true;
            playbackStartedAt = lastTickAt = EditorApplication.timeSinceStartup;
            TickVfx(markVfx, 0f, markLifetime, 0f, ref markVfxAge);
            PlaySfx(slot.Sound);
            PhaseLabel = "Mark 슬롯 미리보기";
        }

        public void SetLooping(bool value)
        {
            looping = value;
        }

        public void SetEnvironment(int index)
        {
            stage.SetEnvironment(index);
        }

        public void Render(Rect rect)
        {
            if (rect.width < 2f || rect.height < 2f)
            {
                return;
            }
            Tick();
            stage.HandleInput(rect, Event.current);
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            var texture = stage.Render(rect);
            if (texture != null)
            {
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
            }
            else
            {
                EditorGUI.DrawRect(rect, new Color(0.055f, 0.065f, 0.08f, 1f));
            }
        }

        private void Tick()
        {
            if (!playing || draft == null)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var elapsed = (float)(now - playbackStartedAt);
            var deltaTime = Mathf.Clamp((float)(now - lastTickAt), 0f, 0.1f);
            lastTickAt = now;
            if (markPreview)
            {
                TickVfx(markVfx, elapsed, markLifetime, deltaTime, ref markVfxAge);
                if (elapsed >= markLifetime) Stop();
                return;
            }
            TickCommanderAnimation(elapsed);
            if (!activated && elapsed >= activationTime)
            {
                Activate(elapsed);
            }
            if (activated && patternImpactIndex < patternImpactCount)
            {
                if (!impacted) UpdateProjectile(elapsed);
                var nextImpact = impactTime + patternImpactIndex * patternImpactInterval;
                if (elapsed >= nextImpact)
                {
                    Impact(elapsed);
                    patternImpactIndex++;
                }
            }

            TickVfx(castingVfx, activationTime > 0f ? elapsed : -1f,
                draft.CastingVfxLifetime, deltaTime, ref castingVfxAge);
            TickVfx(castVfx, activated ? elapsed - activationTime : -1f,
                draft.CastVfxLifetime, deltaTime, ref castVfxAge);
            TickVfx(impactVfx, impacted ? elapsed - impactTime : -1f,
                draft.ImpactVfxLifetime, deltaTime, ref impactVfxAge);
            UpdateImpactPulse(elapsed - lastImpactAt);
            TickVfx(persistentVfx, activated ? elapsed - activationTime : -1f,
                PersistentDuration, deltaTime, ref persistentVfxAge);

            if (elapsed < activationTime)
            {
                PhaseLabel = $"캐스팅 · {elapsed:0.00} / {activationTime:0.00}초";
            }
            else if (!impacted)
            {
                PhaseLabel = $"투사체 이동 · {Mathf.Max(0f, impactTime - elapsed):0.00}초";
            }
            else if (elapsed < activationTime + draft.Cooldown)
            {
                PhaseLabel = patternImpactIndex < patternImpactCount
                    ? $"{draft.PatternType} · {patternImpactIndex}/{patternImpactCount}"
                    : $"쿨타임 · {Mathf.Max(0f, activationTime + draft.Cooldown - elapsed):0.0}초";
            }
            else
            {
                PhaseLabel = "완료";
            }

            if (elapsed < playbackEndTime)
            {
                return;
            }

            if (looping)
            {
                Play();
            }
            else
            {
                Stop();
                PhaseLabel = "완료 · 다시 재생할 수 있습니다";
            }
        }

        private void Activate(float elapsed)
        {
            activated = true;
            if (castVfx != null)
            {
                castVfx.SetActive(true);
                RestartVfx(castVfx);
                castVfxAge = 0f;
            }
            PlaySfx(draft.CastSound);

            if (projectile != null)
            {
                projectile.transform.position = CastPosition;
                projectile.SetActive(true);
                RestartVfx(projectile);
                UpdateProjectile(elapsed);
            }
        }

        private void Impact(float elapsed)
        {
            impacted = true;
            lastImpactAt = elapsed;
            if (projectile != null)
            {
                StopVfx(projectile);
                projectile.SetActive(false);
            }
            if (impactVfx != null)
            {
                impactVfx.SetActive(true);
                RestartVfx(impactVfx);
                impactVfxAge = 0f;
            }
            if (impactPulse != null)
            {
                impactPulse.SetActive(true);
            }
            PlaySfx(draft.ImpactSound);
            UpdateImpactPulse(0f);
        }

        private void BeginCastingFeedback()
        {
            if (castingVfx != null)
            {
                castingVfx.SetActive(true);
                RestartVfx(castingVfx);
                castingVfxAge = 0f;
            }
            PlaySfx(draft.CastingSound);
        }

        private void UpdateProjectile(float elapsed)
        {
            if (projectile == null || impactTime <= activationTime)
            {
                return;
            }
            var normalized = Mathf.Clamp01((elapsed - activationTime) / (impactTime - activationTime));
            var position = Vector3.Lerp(CastPosition, ImpactPosition, normalized);
            if (draft.Trajectory == CommanderSkillTrajectory.Arc)
            {
                position.y += Mathf.Sin(normalized * Mathf.PI) * Mathf.Max(0f, draft.ArcHeight);
            }
            projectile.transform.position = position;
            var forward = ImpactPosition - CastPosition;
            if (forward.sqrMagnitude > 0.0001f)
            {
                projectile.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            }
            MonsterBasicAttackVfxPlayback.Simulate(projectile, 1f / 60f);
        }

        private void CreateFeedbackObjects()
        {
            if (draft.PatternType == CommanderSkillPatternType.PersistentArea)
                persistentVfx = CreateAnchoredVfx(draft.PersistentVfxPrefab, "지속 VFX", draft.PersistentVfxAnchor,
                    draft.PersistentVfxLocalOffset, draft.PersistentVfxLocalEuler, draft.PersistentVfxScale);
            castingVfx = CreateVfx(
                draft.CastingVfxPrefab,
                "캐스팅 시작 VFX",
                CastPosition,
                Quaternion.LookRotation(Vector3.forward, Vector3.up),
                draft.CastingVfxLocalOffset,
                draft.CastingVfxLocalEuler,
                draft.CastingVfxScale);
            castVfx = CreateVfx(
                draft.CastVfxPrefab,
                "발동 VFX",
                CastPosition,
                Quaternion.LookRotation(Vector3.forward, Vector3.up),
                draft.CastVfxLocalOffset,
                draft.CastVfxLocalEuler,
                draft.CastVfxScale);
            impactVfx = CreateVfx(
                draft.ImpactVfxPrefab,
                "적중 VFX",
                ImpactPosition,
                Quaternion.LookRotation(Vector3.forward, Vector3.up),
                draft.ImpactVfxLocalOffset,
                draft.ImpactVfxLocalEuler,
                draft.ImpactVfxScale);
        }

        private GameObject CreateAnchoredVfx(GameObject prefab, string label, CommanderMarkFeedbackAnchor anchor,
            Vector3 offset, Vector3 euler, float scale)
        {
            var parent = anchor switch
            {
                CommanderMarkFeedbackAnchor.CasterRoot => commander.transform,
                CommanderMarkFeedbackAnchor.TargetRoot or CommanderMarkFeedbackAnchor.TargetCenter or
                    CommanderMarkFeedbackAnchor.TargetFeet => target.transform,
                _ => null
            };
            var position = anchor switch
            {
                CommanderMarkFeedbackAnchor.CasterRoot => commander.transform.position,
                CommanderMarkFeedbackAnchor.TargetRoot or CommanderMarkFeedbackAnchor.TargetFeet => target.transform.position,
                CommanderMarkFeedbackAnchor.TargetCenter => target.transform.position + Vector3.up * 0.45f,
                _ => ImpactPosition
            };
            return CreateVfx(prefab, label, position + (parent == null ? offset : parent.TransformVector(offset)),
                parent == null ? Quaternion.identity : parent.rotation, Vector3.zero, euler, scale);
        }

        private GameObject CreateVfx(
            GameObject prefab,
            string objectName,
            Vector3 anchor,
            Quaternion anchorRotation,
            Vector3 localOffset,
            Vector3 localEuler,
            float scale)
        {
            if (prefab == null)
            {
                return null;
            }
            var holder = new GameObject($"[Commander Skill Preview] {objectName}")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            holder.SetActive(false);
            holder.transform.position = anchor + anchorRotation * localOffset;
            holder.transform.rotation = anchorRotation * Quaternion.Euler(localEuler);
            var instance = UnityEngine.Object.Instantiate(prefab, holder.transform, false);
            SetHideFlags(instance);
            DisableRuntimeBehaviours(instance);
            MonsterBasicAttackVfxPlayback.ApplyInstanceScale(
                instance,
                prefab.transform.localScale * Mathf.Max(0.01f, scale));
            MonsterBasicAttackVfxPlayback.ApplyBrightnessScale(
                instance,
                MonsterBasicAttackVfxPlayback.DefaultMainBattleBrightnessScale);
            stage.AddAuxiliary(holder);
            return holder;
        }

        private void CreateProjectile()
        {
            if (draft.Category != CommanderSkillCategory.Attack ||
                draft.DeliveryModule != MonsterBasicAttackDeliveryModule.Projectile)
            {
                return;
            }

            if (draft.ProjectilePrefab != null)
            {
                projectile = InstantiateInactive(draft.ProjectilePrefab);
                projectile.name = "[Commander Skill Preview] 투사체";
                SetHideFlags(projectile);
                DisableRuntimeBehaviours(projectile);
                MonsterBasicAttackVfxPlayback.ApplyBrightnessScale(
                    projectile,
                    MonsterBasicAttackVfxPlayback.DefaultMainBattleBrightnessScale);
            }
            else
            {
                projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                projectile.name = "[Commander Skill Preview] 투사체 대체 표시";
                projectile.transform.localScale = Vector3.one * 0.22f;
                var collider = projectile.GetComponent<Collider>();
                if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
                var renderer = projectile.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = impactMaterial;
            }
            projectile.transform.position = CastPosition;
            projectile.SetActive(false);
            stage.AddAuxiliary(projectile);
        }

        private GameObject CreateTarget(Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TargetVisualPath);
            GameObject instance;
            if (prefab != null)
            {
                instance = InstantiateInactive(prefab);
                instance.name = draft.TargetTeam == CommanderSkillTargetTeam.Ally
                    ? "[Commander Skill Preview] 아군 대상"
                    : "[Commander Skill Preview] 적 대상";
                SetHideFlags(instance);
                DisableRuntimeBehaviours(instance);
                GroundAndFit(instance, 1.7f);
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                instance.name = "[Commander Skill Preview] 표준 대상";
                instance.transform.localScale = new Vector3(0.55f, 0.85f, 0.55f);
                var collider = instance.GetComponent<Collider>();
                if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            }
            instance.transform.position += position;
            instance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            stage.AddAuxiliary(instance);
            return instance;
        }

        private static GameObject InstantiateInactive(GameObject prefab)
        {
            var guard = new GameObject("[Commander Skill Preview] Activation Guard")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            guard.SetActive(false);
            try
            {
                var instance = UnityEngine.Object.Instantiate(prefab, guard.transform, false);
                SetHideFlags(instance);
                DisableRuntimeBehaviours(instance);
                instance.transform.SetParent(null, true);
                return instance;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(guard);
            }
        }

        private void CreateRangeVisuals()
        {
            CreateRing("대상 표시", ImpactPosition + Vector3.up * 0.025f, 0.52f, targetRingMaterial);
            CreateLine("시전 경로", new[]
            {
                new Vector3(0f, 0.035f, 0.35f),
                new Vector3(0f, 0.035f, targetDistance)
            }, rangeMaterial, false);

            if (draft.Category != CommanderSkillCategory.Attack)
            {
                if (draft.Effects != null)
                {
                    for (var index = 0; index < draft.Effects.Count; index++)
                    {
                        var effect = draft.Effects[index];
                        if (effect == null || effect.Scope != CommanderSkillEffectScope.Area)
                        {
                            continue;
                        }
                        CreateRing(
                            $"효과 범위 {index + 1:00}",
                            ImpactPosition + Vector3.up * (0.04f + index * 0.012f),
                            Mathf.Max(0.1f, Growth.Resolve(AP.AreaRadius, effect.Radius, effect.EffectId)),
                            rangeMaterial);
                    }
                }
                return;
            }

            switch (draft.Shape)
            {
                case MonsterBasicAttackShape.Single:
                    CreateRing("단일 판정", ImpactPosition + Vector3.up * 0.04f, 0.62f, rangeMaterial);
                    break;
                case MonsterBasicAttackShape.Fan:
                    CreateFan(Mathf.Max(0.1f, Growth.Resolve(AP.TargetRange, draft.TargetRange)), draft.Angle);
                    break;
                case MonsterBasicAttackShape.Line:
                    CreateRectangle(Mathf.Max(0.1f, Growth.Resolve(AP.TargetRange, draft.TargetRange)),
                        Growth.Resolve(AP.LineWidth, draft.LineWidth, draft.SkillId + "_damage"));
                    break;
                default:
                    var center = draft.Center switch
                    {
                        MonsterBasicAttackCenter.Source => Vector3.zero,
                        MonsterBasicAttackCenter.Forward =>
                            Vector3.forward * Mathf.Max(0f, draft.ForwardOffset),
                        _ => ImpactPosition
                    };
                    CreateRing("원형 판정", center + Vector3.up * 0.04f,
                        Mathf.Max(0.1f, Growth.Resolve(AP.AreaRadius, draft.Radius, draft.SkillId + "_damage")), rangeMaterial);
                    break;
            }
        }

        private void CreateFan(float radius, float angle)
        {
            const int segments = 28;
            var points = new Vector3[segments + 3];
            points[0] = Vector3.up * 0.045f;
            for (var index = 0; index <= segments; index++)
            {
                var degrees = -angle * 0.5f + angle * index / segments;
                var direction = Quaternion.Euler(0f, degrees, 0f) * Vector3.forward;
                points[index + 1] = direction * radius + Vector3.up * 0.045f;
            }
            points[points.Length - 1] = points[0];
            CreateLine("부채꼴 판정", points, rangeMaterial, false);
        }

        private void CreateRectangle(float length, float width)
        {
            var half = Mathf.Max(0.05f, width) * 0.5f;
            var y = Vector3.up * 0.045f;
            CreateLine("직선 판정", new[]
            {
                new Vector3(-half, 0f, 0f) + y,
                new Vector3(-half, 0f, length) + y,
                new Vector3(half, 0f, length) + y,
                new Vector3(half, 0f, 0f) + y,
                new Vector3(-half, 0f, 0f) + y
            }, rangeMaterial, false);
        }

        private void CreateRing(string objectName, Vector3 center, float radius, Material material)
        {
            const int segments = 48;
            var points = new Vector3[segments];
            for (var index = 0; index < segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                points[index] = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
            }
            CreateLine(objectName, points, material, true);
        }

        private void CreateLine(
            string objectName,
            Vector3[] points,
            Material material,
            bool loop)
        {
            var lineObject = new GameObject($"[Commander Skill Preview] {objectName}")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.widthMultiplier = 0.045f;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            stage.AddAuxiliary(lineObject);
        }

        private void CreateImpactPulse()
        {
            impactPulse = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            impactPulse.name = "[Commander Skill Preview] 적중 순간";
            impactPulse.hideFlags = HideFlags.HideAndDontSave;
            impactPulse.transform.position = ImpactPosition + Vector3.up * 0.8f;
            impactPulse.transform.localScale = Vector3.one * 0.01f;
            var collider = impactPulse.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            var renderer = impactPulse.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = impactMaterial;
            impactPulse.SetActive(false);
            stage.AddAuxiliary(impactPulse);
        }

        private void UpdateImpactPulse(float age)
        {
            if (impactPulse == null || age < 0f || age > 0.35f)
            {
                if (impactPulse != null) impactPulse.SetActive(false);
                return;
            }
            impactPulse.SetActive(true);
            var normalized = Mathf.Clamp01(age / 0.35f);
            impactPulse.transform.localScale = Vector3.one * Mathf.Lerp(0.08f, 0.72f, normalized);
        }

        private void ResetPlaybackObjects()
        {
            ResetCommanderAnimation();
            markPreview = false;
            if (markVfx != null)
            {
                StopVfx(markVfx);
                DestroyPlayableGraphs(markVfx);
                stage.RemoveAuxiliary(markVfx);
                markVfx = null;
            }
            markVfxAge = -1f;
            ResetVfx(persistentVfx, ref persistentVfxAge);
            activated = false;
            impacted = false;
            patternImpactIndex = 0;
            lastImpactAt = -10f;
            ResetVfx(castingVfx, ref castingVfxAge);
            ResetVfx(castVfx, ref castVfxAge);
            ResetVfx(impactVfx, ref impactVfxAge);
            if (projectile != null)
            {
                StopVfx(projectile);
                projectile.transform.position = CastPosition;
                projectile.SetActive(false);
            }
            if (impactPulse != null)
            {
                impactPulse.transform.localScale = Vector3.one * 0.01f;
                impactPulse.SetActive(false);
            }
        }

        private static void TickVfx(
            GameObject item,
            float age,
            float lifetime,
            float deltaTime,
            ref float simulatedAge)
        {
            if (item == null)
            {
                return;
            }
            var active = age >= 0f && age < Mathf.Max(0.05f, lifetime);
            item.SetActive(active);
            if (!active)
            {
                simulatedAge = -1f;
                return;
            }
            if (simulatedAge < 0f)
            {
                RestartVfx(item);
                simulatedAge = 0f;
            }
            MonsterBasicAttackVfxPlayback.Simulate(item, Mathf.Max(0f, deltaTime));
            simulatedAge = age;
        }

        private static void ResetVfx(GameObject item, ref float age)
        {
            age = -1f;
            if (item == null) return;
            StopVfx(item);
            item.SetActive(false);
        }

        private static void RestartVfx(GameObject item)
        {
            if (item == null) return;
            MonsterBasicAttackVfxPlayback.RestartAtOffset(item, 0f);
            InvokeVisualEffectMethod(item, "Reinit");
            InvokeVisualEffectMethod(item, "Play");
        }

        private static void StopVfx(GameObject item)
        {
            if (item == null) return;
            MonsterBasicAttackVfxPlayback.StopAndClear(item);
            InvokeVisualEffectMethod(item, "Stop");
        }

        private static void InvokeVisualEffectMethod(GameObject root, string methodName)
        {
            var components = root.GetComponentsInChildren<Component>(true);
            for (var index = 0; index < components.Length; index++)
            {
                var component = components[index];
                if (component == null || component.GetType().FullName != "UnityEngine.VFX.VisualEffect")
                {
                    continue;
                }
                if (component is Behaviour behaviour) behaviour.enabled = true;
                component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)
                    ?.Invoke(component, null);
            }
        }

        private static void DisableRuntimeBehaviours(GameObject root)
        {
            if (root == null) return;
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (var index = 0; index < behaviours.Length; index++)
            {
                behaviours[index].enabled = false;
            }

            // Inactive guard 아래에서 미리 끈 뒤 Preview Scene에 옮긴다.
            // Animator/PlayableDirector가 한 프레임이라도 활성화되면 임시 PlayableGraph가
            // 만들어져 EditorWindow 종료 시 누수 경고를 남길 수 있다.
            var animators = root.GetComponentsInChildren<Animator>(true);
            for (var index = 0; index < animators.Length; index++)
            {
                var animator = animators[index];
                animator.enabled = false;
                var graph = animator.playableGraph;
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
            }

            var directors = root.GetComponentsInChildren<PlayableDirector>(true);
            for (var index = 0; index < directors.Length; index++)
            {
                var director = directors[index];
                director.enabled = false;
                var graph = director.playableGraph;
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
            }
        }

        private void PrepareCommanderAnimator()
        {
            if (commanderAnimator == null || commanderAnimator.runtimeAnimatorController == null) return;
            commanderAnimator.applyRootMotion = false;
            commanderAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            commanderAnimator.updateMode = AnimatorUpdateMode.Normal;
            commanderAnimator.enabled = false;
            ResetCommanderAnimation();
        }

        private void StartCommanderAnimation()
        {
            commanderAnimationPlaying = false;
            commanderAnimationDuration = 0f;
            if (commanderAnimator == null || commanderAnimator.runtimeAnimatorController == null || draft == null)
                return;
            var attackNumber = CommanderSkillCastAnimationRules.ResolveAttackNumber(draft.SkillId);
            commanderAnimationStateHash = Animator.StringToHash(
                CommanderSkillCastAnimationRules.StateName(attackNumber));
            if (!commanderAnimator.HasState(0, commanderAnimationStateHash)) return;
            commanderAnimationDuration = ResolveCommanderAnimationDuration(attackNumber);
            commanderAnimationPlaying = true;
            SampleCommanderAnimation(0f);
        }

        private void TickCommanderAnimation(float elapsed)
        {
            if (!commanderAnimationPlaying) return;
            if (elapsed >= commanderAnimationDuration)
            {
                commanderAnimationPlaying = false;
                ResetCommanderAnimation();
                return;
            }
            SampleCommanderAnimation(elapsed);
        }

        private void SampleCommanderAnimation(float elapsed)
        {
            if (commanderAnimator == null) return;
            var normalized = commanderAnimationDuration <= 0.001f
                ? 0f
                : Mathf.Clamp01(elapsed / commanderAnimationDuration);
            commanderAnimator.enabled = true;
            commanderAnimator.speed = 0f;
            commanderAnimator.Play(commanderAnimationStateHash, 0, normalized);
            commanderAnimator.Update(0f);
            commanderAnimator.enabled = false;
        }

        private void ResetCommanderAnimation()
        {
            commanderAnimationPlaying = false;
            if (commanderAnimator == null || commanderAnimator.runtimeAnimatorController == null) return;
            var idleHash = Animator.StringToHash("Base Layer.WorldIdle");
            if (!commanderAnimator.HasState(0, idleHash)) return;
            commanderAnimator.enabled = true;
            commanderAnimator.speed = 0f;
            commanderAnimator.Play(idleHash, 0, 0f);
            commanderAnimator.Update(0f);
            commanderAnimator.enabled = false;
        }

        private float ResolveCommanderAnimationDuration(int attackNumber)
        {
            var expectedName = CommanderSkillCastAnimationRules.ClipName(attackNumber);
            var clips = commanderAnimator.runtimeAnimatorController.animationClips;
            for (var index = 0; index < clips.Length; index++)
            {
                var clip = clips[index];
                if (clip != null && (clip.name == expectedName || clip.name == expectedName + "_inplace"))
                    return Mathf.Max(0.1f,
                        clip.length / CommanderSkillCastAnimationRules.StatePlaybackSpeed);
            }
            return 1f;
        }

        private static void GroundAndFit(GameObject instance, float desiredHeight)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            if (bounds.size.y > 0.001f)
            {
                instance.transform.localScale *= desiredHeight / bounds.size.y;
            }
            renderers = instance.GetComponentsInChildren<Renderer>(true);
            bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            instance.transform.position += Vector3.down * bounds.min.y;
        }

        private static void SetHideFlags(GameObject root)
        {
            if (root == null) return;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < transforms.Length; index++)
            {
                transforms[index].gameObject.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private static Material CreateLineMaterial(Color color)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color");
            if (shader == null)
            {
                return null;
            }
            var material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                color = color
            };
            return material;
        }

        private static void PlaySfx(AudioClip clip)
        {
            if (clip == null) return;
            var audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            var method = audioUtil?.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name == "PlayPreviewClip");
            if (method == null) return;
            var arguments = method.GetParameters().Length switch
            {
                1 => new object[] { clip },
                2 => new object[] { clip, 0 },
                _ => new object[] { clip, 0, false }
            };
            try { method.Invoke(null, arguments); }
            catch { /* Unity 버전별 AudioUtil 차이는 시각 프리뷰를 막지 않는다. */ }
        }

        private Vector3 CastPosition => CastAnchorLocalPosition;
        private Vector3 ImpactPosition => new Vector3(0f, 0.45f, targetDistance);

        private void ClearStage()
        {
            DestroyPlayableGraphs(persistentVfx);
            DestroyPlayableGraphs(markVfx);
            persistentVfx = markVfx = null;
            DestroyPlayableGraphs(commander);
            DestroyPlayableGraphs(target);
            DestroyPlayableGraphs(castingVfx);
            DestroyPlayableGraphs(castVfx);
            DestroyPlayableGraphs(impactVfx);
            DestroyPlayableGraphs(projectile);
            stage.ClearPrefab();
            commander = target = castingVfx = castVfx = impactVfx = projectile = impactPulse = null;
            commanderAnimator = null;
            DestroyMaterial(ref targetRingMaterial);
            DestroyMaterial(ref rangeMaterial);
            DestroyMaterial(ref impactMaterial);
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
            material = null;
        }

        public void Dispose()
        {
            Stop();
            DestroyPlayableGraphs(persistentVfx);
            persistentVfx = null;
            playing = false;
            DestroyPlayableGraphs(commander);
            DestroyPlayableGraphs(target);
            DestroyPlayableGraphs(castingVfx);
            DestroyPlayableGraphs(castVfx);
            DestroyPlayableGraphs(impactVfx);
            DestroyPlayableGraphs(projectile);
            stage.Dispose();
            DestroyMaterial(ref targetRingMaterial);
            DestroyMaterial(ref rangeMaterial);
            DestroyMaterial(ref impactMaterial);
            commander = target = castingVfx = castVfx = impactVfx = projectile = impactPulse = null;
            commanderAnimator = null;
        }

        private static void DestroyPlayableGraphs(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var animators = root.GetComponentsInChildren<Animator>(true);
            for (var index = 0; index < animators.Length; index++)
            {
                var graph = animators[index].playableGraph;
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
            }

            var directors = root.GetComponentsInChildren<PlayableDirector>(true);
            for (var index = 0; index < directors.Length; index++)
            {
                var graph = directors[index].playableGraph;
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
            }
        }

        internal static string BuildVisualSignature(CommanderSkillWorkshopDraft source)
        {
            if (source == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(512);
            AppendFeedback(builder, source.PersistentVfxPrefab, source.PatternDuration,
                source.PersistentVfxLocalOffset, source.PersistentVfxLocalEuler, source.PersistentVfxScale, null);
            builder.Append((int)source.PersistentVfxAnchor).Append('|');
            builder.Append((int)source.Category).Append('|')
                .Append(source.CastTime.GetHashCode()).Append('|')
                .Append(source.Cooldown.GetHashCode()).Append('|')
                .Append((int)source.TargetTeam).Append('|')
                .Append(source.TargetRange.GetHashCode()).Append('|')
                .Append((int)source.DeliveryModule).Append('|')
                .Append((int)source.Shape).Append('|')
                .Append((int)source.Center).Append('|')
                .Append(source.Radius.GetHashCode()).Append('|')
                .Append(source.ForwardOffset.GetHashCode()).Append('|')
                .Append(source.Angle.GetHashCode()).Append('|')
                .Append(source.LineWidth.GetHashCode()).Append('|')
                .Append(ObjectId(source.ProjectilePrefab)).Append('|')
                .Append(source.ProjectileSpeed.GetHashCode()).Append('|')
                .Append((int)source.Trajectory).Append('|')
                .Append(source.ArcHeight.GetHashCode()).Append('|');
            builder.Append(source.SkillId).Append('|');
            builder.Append((int)source.PatternType).Append('|')
                .Append(source.RepeatCount).Append('|').Append(source.RepeatInterval.GetHashCode()).Append('|')
                .Append(source.PatternDuration.GetHashCode()).Append('|').Append(source.TickInterval.GetHashCode()).Append('|')
                .Append(source.RandomRadius.GetHashCode()).Append('|').Append(source.FirstBarrageHitAtTarget).Append('|')
                .Append(source.ChainCount).Append('|')
                .Append(source.ChainRadius.GetHashCode()).Append('|');
            AppendFeedback(
                builder,
                source.CastingVfxPrefab,
                source.CastingVfxLifetime,
                source.CastingVfxLocalOffset,
                source.CastingVfxLocalEuler,
                source.CastingVfxScale,
                source.CastingSound);
            AppendFeedback(
                builder,
                source.CastVfxPrefab,
                source.CastVfxLifetime,
                source.CastVfxLocalOffset,
                source.CastVfxLocalEuler,
                source.CastVfxScale,
                source.CastSound);
            AppendFeedback(
                builder,
                source.ImpactVfxPrefab,
                source.ImpactVfxLifetime,
                source.ImpactVfxLocalOffset,
                source.ImpactVfxLocalEuler,
                source.ImpactVfxScale,
                source.ImpactSound);

            if (source.Effects != null)
            {
                builder.Append(source.Effects.Count).Append('|');
                for (var index = 0; index < source.Effects.Count; index++)
                {
                    var effect = source.Effects[index];
                    if (effect == null)
                    {
                        builder.Append("null|");
                        continue;
                    }
                    builder.Append((int)effect.Scope).Append('|')
                        .Append(effect.Radius.GetHashCode()).Append('|');
                }
            }
            foreach (var stage in source.AwakeningStages) builder.Append(JsonUtility.ToJson(stage));
            return builder.ToString();
        }

        private static void AppendFeedback(
            StringBuilder builder,
            GameObject prefab,
            float lifetime,
            Vector3 offset,
            Vector3 euler,
            float scale,
            AudioClip sound)
        {
            builder.Append(ObjectId(prefab)).Append('|')
                .Append(lifetime.GetHashCode()).Append('|')
                .Append(offset.GetHashCode()).Append('|')
                .Append(euler.GetHashCode()).Append('|')
                .Append(scale.GetHashCode()).Append('|')
                .Append(ObjectId(sound)).Append('|');
        }

        private static int ObjectId(UnityEngine.Object item)
        {
            return item == null ? 0 : item.GetInstanceID();
        }
    }
}
