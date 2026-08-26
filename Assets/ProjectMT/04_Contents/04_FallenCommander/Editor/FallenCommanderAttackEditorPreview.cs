using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander.Editor
{
    internal enum FallenCommanderAttackPreviewMode
    {
        PreCast,
        Cast,
        Full
    }

    internal enum FallenCommanderAttackPreviewKind
    {
        Basic,
        Melee,
        MarkStrike,
        TrackingMark,
        BlackHole,
        LineStrike,
        CorruptionRing,
        FinalCharge,
        TimeoutWipe
    }

    internal sealed class FallenCommanderAttackPreviewSpec
    {
        public FallenCommanderAttackPreviewKind Kind { get; set; }
        public string Label { get; set; }
        public FallenCommanderBossConfig Config { get; set; }
        public GameObject BossPrefab { get; set; }
        public Transform SpawnPoint { get; set; }
        public Transform FacingTarget { get; set; }
        public FallenCommanderBasicAttackData BasicAttack { get; set; }
        public FallenCommanderAttackEffectData Effects { get; set; }
        public GameObject TelegraphPrefab { get; set; }
        public float TelegraphRadius { get; set; }
        public float TelegraphWidth { get; set; }
        public float TelegraphLength { get; set; }
        public float SecondaryTelegraphRadius { get; set; }
        public AnimationClip PreCastMotion { get; set; }
        public float PreCastMotionDuration { get; set; }
        public float PreCastMotionSpeed { get; set; } = 1f;
        public AnimationClip CastMotion { get; set; }
        public float CastMotionDuration { get; set; }
        public float CastMotionSpeed { get; set; } = 1f;
        public float WarningDuration { get; set; } = 0.1f;
        public Vector3 StartEffectLocalOffset { get; set; }
    }

    [InitializeOnLoad]
    internal static class FallenCommanderAttackEditorPreview
    {
        private static readonly Type AudioUtilType =
            typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        private static readonly MethodInfo PlayPreviewClipMethod =
            AudioUtilType?.GetMethod(
                "PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null);
        private static readonly MethodInfo StopPreviewClipsMethod =
            AudioUtilType?.GetMethod(
                "StopAllPreviewClips",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly List<ParticleSystem> Particles = new();
        private static readonly Color BasicTelegraphColor =
            new Color(0.2f, 0.85f, 1f, 0.8f);
        private static readonly Color MeleeTelegraphColor =
            new Color(1f, 0.25f, 0.05f, 0.75f);
        private static readonly Color LineTelegraphColor =
            new Color(0.15f, 0.45f, 1f, 0.75f);
        private static readonly Color MarkTelegraphColor =
            new Color(0.9f, 0.15f, 0.8f, 0.75f);
        private static readonly Color TrackingMarkTelegraphColor =
            new Color(0.25f, 0.75f, 1f, 0.75f);
        private static readonly Color BlackHoleTelegraphColor =
            new Color(0.55f, 0.1f, 0.85f, 0.8f);
        private static readonly Color CorruptionRingTelegraphColor =
            new Color(0.65f, 0.05f, 0.15f, 0.8f);
        private static readonly Color CorruptionRingSafeColor =
            new Color(0.1f, 0.9f, 0.45f, 0.8f);
        private static readonly Color FinalChargeTelegraphColor =
            new Color(0.9f, 0.08f, 0.12f, 0.85f);

        private static FallenCommanderAttackPreviewSpec spec;
        private static FallenCommanderAttackPreviewMode mode;
        private static GameObject previewBoss;
        private static Animator previewAnimator;
        private static GameObject startVfxInstance;
        private static FallenCommanderTelegraphView basicTelegraph;
        private static FallenCommanderTelegraphView attackTelegraph;
        private static FallenCommanderTelegraphView secondaryAttackTelegraph;
        private static GameObject basicProjectile;
        private static Vector3 basicProjectilePosition;
        private static Vector3 basicProjectileDirection;
        private static float basicProjectileTravelRemaining;
        private static bool basicProjectileWillHit;
        private static bool basicProjectileLaunched;
        private static bool basicProjectileFinished;
        private static double lastTime;
        private static float elapsed;
        private static float resolveTime;
        private static float totalDuration;
        private static float audioStopTime;
        private static bool hasResolved;
        private static bool isAudioPlaying;

        public static bool IsActive => previewBoss != null && spec != null;

        // 에디터 상태가 바뀌어도 임시 보스·연출·오디오가 남지 않도록 정리 경로를 등록한다.
        static FallenCommanderAttackEditorPreview()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += _ => Stop();
            EditorApplication.quitting += Stop;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }

        // 요청된 공격 단계의 모션·VFX·SFX 미리보기를 안전하게 시작한다.
        public static bool Play(
            FallenCommanderAttackPreviewSpec previewSpec,
            FallenCommanderAttackPreviewMode previewMode)
        {
            Stop();
            FallenCommanderBossEditorPreview.Stop();
            if (PrefabStageUtility.GetCurrentPrefabStage() != null ||
                previewSpec?.BossPrefab == null)
            {
                return false;
            }

            GameObject createdBoss = null;
            var initialized = false;
            try
            {
                createdBoss = PrefabUtility.InstantiatePrefab(
                    previewSpec.BossPrefab) as GameObject;
                if (createdBoss == null)
                {
                    return false;
                }

                createdBoss.name =
                    $"[공격 미리보기] {previewSpec.Label} - {previewSpec.BossPrefab.name}";
                createdBoss.hideFlags = HideFlags.HideAndDontSave;
                if (previewSpec.SpawnPoint != null)
                {
                    createdBoss.transform.SetPositionAndRotation(
                        previewSpec.SpawnPoint.position,
                        previewSpec.SpawnPoint.rotation);
                }

                FaceTarget(createdBoss.transform, previewSpec.FacingTarget);
                foreach (var behaviour in createdBoss.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    behaviour.enabled = false;
                }

                spec = previewSpec;
                mode = previewMode;
                previewBoss = createdBoss;
                previewAnimator = createdBoss.GetComponentInChildren<Animator>(true);
                elapsed = 0f;
                resolveTime = Mathf.Max(0.1f, previewSpec.WarningDuration);
                hasResolved = previewMode == FallenCommanderAttackPreviewMode.Cast;
                totalDuration = ResolveTotalDuration(previewSpec, previewMode);
                lastTime = EditorApplication.timeSinceStartup;

                if (previewAnimator != null &&
                    (previewSpec.PreCastMotion != null || previewSpec.CastMotion != null))
                {
                    AnimationMode.StartAnimationMode();
                }

                if (previewSpec.Kind == FallenCommanderAttackPreviewKind.Basic)
                {
                    BeginBasicAttackPreview(previewMode);
                }
                else if (previewMode == FallenCommanderAttackPreviewMode.Cast)
                {
                    PlayResolvePresentation();
                    Sample(previewSpec.CastMotion, 0f, previewSpec.CastMotionSpeed);
                }
                else
                {
                    PlayStartPresentation();
                    BeginAttackTelegraphPreview();
                    Sample(previewSpec.PreCastMotion, 0f, previewSpec.PreCastMotionSpeed);
                }

                initialized = true;
                SceneView.lastActiveSceneView?.Frame(
                    new Bounds(previewBoss.transform.position + Vector3.up * 1.5f, Vector3.one * 5f),
                    true);
                SceneView.RepaintAll();
                return true;
            }
            finally
            {
                if (!initialized)
                {
                    if (createdBoss != null)
                    {
                        UnityEngine.Object.DestroyImmediate(createdBoss);
                    }

                    ClearState();
                }
            }
        }

        // 실행 중인 에디터 미리보기와 모든 임시 리소스를 제거한다.
        public static void Stop()
        {
            StopAudio();
            if (AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
            }

            if (previewBoss != null)
            {
                UnityEngine.Object.DestroyImmediate(previewBoss);
            }

            ClearState();
            SceneView.RepaintAll();
        }

        // 미리보기 갱신 중 예외가 발생하면 오류를 기록하고 모든 임시 리소스를 정리한다.
        private static void Update()
        {
            if (!IsActive)
            {
                return;
            }

            try
            {
                UpdatePreviewFrame();
            }
            catch (Exception exception)
            {
                try
                {
                    Debug.LogException(exception);
                }
                finally
                {
                    Stop();
                }
            }
        }

        // 실제 시간으로 모션과 파티클을 진행하고 전체 미리보기의 공격 시점을 처리한다.
        private static void UpdatePreviewFrame()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Stop();
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var deltaTime = Mathf.Clamp((float)(now - lastTime), 0f, 0.1f);
            lastTime = now;
            elapsed += deltaTime;

            if (startVfxInstance != null)
            {
                ApplyLiveStartVfxTransform();
            }

            if (isAudioPlaying && elapsed >= audioStopTime)
            {
                StopAudio();
            }

            if (spec.Kind == FallenCommanderAttackPreviewKind.Basic)
            {
                UpdateBasicAttack(deltaTime);
            }
            else
            {
                UpdateAttackTelegraphPreview();
                if (mode == FallenCommanderAttackPreviewMode.Full &&
                    !hasResolved &&
                    elapsed >= resolveTime)
                {
                    hasResolved = true;
                    CompleteAttackTelegraphPreview();
                    PlayResolvePresentation();
                }
            }

            UpdateMotion();
            foreach (var particle in Particles)
            {
                if (particle != null)
                {
                    particle.Simulate(deltaTime, false, false, false);
                }
            }

            if (elapsed >= totalDuration)
            {
                Stop();
                return;
            }

            SceneView.RepaintAll();
        }

        // 실제 기본 공격처럼 경고 범위와 투사체 이동에 필요한 초기 상태를 구성한다.
        private static void BeginBasicAttackPreview(FallenCommanderAttackPreviewMode previewMode)
        {
            var attack = spec.BasicAttack;
            if (attack == null)
            {
                return;
            }

            var origin = previewBoss.transform.position;
            basicProjectileDirection = spec.FacingTarget == null
                ? previewBoss.transform.forward
                : spec.FacingTarget.position - origin;
            basicProjectileDirection.y = 0f;
            if (basicProjectileDirection.sqrMagnitude < 0.0001f)
            {
                basicProjectileDirection = previewBoss.transform.forward;
            }

            basicProjectileDirection.Normalize();
            basicProjectilePosition = origin + Vector3.up * attack.ProjectileHeight;
            basicProjectileTravelRemaining = ResolveBasicTravelDistance(spec, out basicProjectileWillHit);
            basicProjectileLaunched = false;
            basicProjectileFinished = false;

            if (previewMode == FallenCommanderAttackPreviewMode.Cast)
            {
                CreateBasicProjectile();
                return;
            }

            PlayStartPresentation();
            basicTelegraph = FallenCommanderTelegraphView.CreateLine(
                attack.TelegraphPrefab,
                previewBoss.transform,
                origin,
                basicProjectileDirection,
                attack.ProjectileRadius * 2f,
                attack.MaxDistance,
                BasicTelegraphColor);
            if (basicTelegraph != null)
            {
                basicTelegraph.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        // 경고 게이지를 채운 뒤 투사체를 목표 방향으로 이동시키고 도달 시 적중 연출을 재생한다.
        private static void UpdateBasicAttack(float deltaTime)
        {
            var attack = spec.BasicAttack;
            if (attack == null)
            {
                return;
            }

            var warningDuration = Mathf.Max(0.1f, attack.WarningDuration);
            if (basicTelegraph != null)
            {
                basicTelegraph.SetProgress(Mathf.Clamp01(elapsed / warningDuration));
                if (elapsed >= warningDuration)
                {
                    basicTelegraph.gameObject.SetActive(false);
                }
            }

            if (mode == FallenCommanderAttackPreviewMode.Full &&
                !basicProjectileLaunched &&
                elapsed >= warningDuration)
            {
                CreateBasicProjectile();
            }

            if (!basicProjectileLaunched || basicProjectileFinished)
            {
                return;
            }

            var travelDistance = Mathf.Min(
                basicProjectileTravelRemaining,
                Mathf.Max(0.1f, attack.ProjectileSpeed) * Mathf.Max(0f, deltaTime));
            basicProjectilePosition += basicProjectileDirection * travelDistance;
            basicProjectileTravelRemaining = Mathf.Max(
                0f,
                basicProjectileTravelRemaining - travelDistance);
            if (basicProjectile != null)
            {
                basicProjectile.transform.position = basicProjectilePosition;
            }

            if (basicProjectileTravelRemaining > 0f)
            {
                return;
            }

            basicProjectileFinished = true;
            if (basicProjectileWillHit)
            {
                PlayResolvePresentation();
            }

            if (basicProjectile != null)
            {
                basicProjectile.SetActive(false);
            }
        }

        // 선택한 공격의 실제 범위 프리팹을 런타임과 같은 모양과 크기로 생성한다.
        private static void BeginAttackTelegraphPreview()
        {
            if (spec.TelegraphPrefab == null || previewBoss == null)
            {
                return;
            }

            var position = ResolveTelegraphPosition();
            if (spec.Kind == FallenCommanderAttackPreviewKind.LineStrike)
            {
                attackTelegraph = FallenCommanderTelegraphView.CreateLine(
                    spec.TelegraphPrefab,
                    previewBoss.transform,
                    position,
                    ResolvePreviewDirection(),
                    spec.TelegraphWidth,
                    spec.TelegraphLength,
                    LineTelegraphColor);
            }
            else
            {
                attackTelegraph = FallenCommanderTelegraphView.CreateCircle(
                    spec.TelegraphPrefab,
                    previewBoss.transform,
                    position,
                    spec.TelegraphRadius,
                    ResolveTelegraphColor());
            }

            if (attackTelegraph != null)
            {
                attackTelegraph.gameObject.hideFlags = HideFlags.HideAndDontSave;
                attackTelegraph.SetProgress(0f);
            }

            if (spec.Kind != FallenCommanderAttackPreviewKind.CorruptionRing ||
                spec.SecondaryTelegraphRadius <= 0f)
            {
                return;
            }

            secondaryAttackTelegraph = FallenCommanderTelegraphView.CreateCircle(
                spec.TelegraphPrefab,
                previewBoss.transform,
                position + Vector3.up * 0.035f,
                spec.SecondaryTelegraphRadius,
                CorruptionRingSafeColor);
            if (secondaryAttackTelegraph != null)
            {
                secondaryAttackTelegraph.gameObject.hideFlags = HideFlags.HideAndDontSave;
                secondaryAttackTelegraph.SetProgress(1f);
            }
        }

        // 경고시간 동안 공격 범위를 채우고 추적형 범위의 위치를 갱신한다.
        private static void UpdateAttackTelegraphPreview()
        {
            if (attackTelegraph == null)
            {
                return;
            }

            if (spec.Kind == FallenCommanderAttackPreviewKind.TrackingMark)
            {
                var lockDuration = spec.Config == null
                    ? 0f
                    : spec.Config.TrackingMarkLockDuration;
                var trackingDuration = Mathf.Max(0f, spec.WarningDuration - lockDuration);
                if (elapsed < trackingDuration)
                {
                    var position = ResolveTelegraphPosition();
                    position.y += 0.025f;
                    attackTelegraph.transform.position = position;
                }
            }

            attackTelegraph.SetProgress(Mathf.Clamp01(elapsed / spec.WarningDuration));
        }

        // 공격이 발동하는 순간 범위를 완전히 채운 뒤 미리보기에서 숨긴다.
        private static void CompleteAttackTelegraphPreview()
        {
            if (attackTelegraph != null)
            {
                attackTelegraph.SetProgress(1f);
                attackTelegraph.gameObject.SetActive(false);
            }

            if (secondaryAttackTelegraph != null)
            {
                secondaryAttackTelegraph.gameObject.SetActive(false);
            }
        }

        // 공격 종류에 따라 보스 위치·군단장 위치·블랙홀 예시 위치를 선택한다.
        private static Vector3 ResolveTelegraphPosition()
        {
            if (spec.Kind == FallenCommanderAttackPreviewKind.MarkStrike ||
                spec.Kind == FallenCommanderAttackPreviewKind.TrackingMark)
            {
                return spec.FacingTarget == null
                    ? previewBoss.transform.position
                    : spec.FacingTarget.position;
            }

            if (spec.Kind == FallenCommanderAttackPreviewKind.BlackHole)
            {
                return ResolveBlackHolePreviewPosition();
            }

            return previewBoss.transform.position;
        }

        // 블랙홀의 무작위 생성을 대신해 군단장 근처의 재현 가능한 예시 위치를 계산한다.
        private static Vector3 ResolveBlackHolePreviewPosition()
        {
            var bossPosition = previewBoss.transform.position;
            var targetPosition = spec.FacingTarget == null
                ? bossPosition
                : spec.FacingTarget.position;
            var towardTarget = targetPosition - bossPosition;
            towardTarget.y = 0f;
            if (towardTarget.sqrMagnitude < 0.0001f)
            {
                towardTarget = previewBoss.transform.forward;
            }

            towardTarget.Normalize();
            var sideDirection = Vector3.Cross(Vector3.up, towardTarget).normalized;
            var minimumDistance = spec.Config == null
                ? 0f
                : spec.Config.BlackHoleSpawnMinDistance;
            var maximumDistance = spec.Config == null
                ? minimumDistance
                : spec.Config.BlackHoleSpawnMaxDistance;
            var distance = (minimumDistance + maximumDistance) * 0.5f;
            var candidate = targetPosition + sideDirection * distance;
            if (spec.Config != null)
            {
                var outerRadius = Mathf.Max(
                    spec.Config.BlackHoleCoreRadius + 0.1f,
                    spec.TelegraphRadius);
                var halfExtents = spec.Config.BlackHoleArenaHalfExtents;
                var allowedX = Mathf.Max(0f, halfExtents.x - outerRadius);
                var allowedZ = Mathf.Max(0f, halfExtents.y - outerRadius);
                candidate.x = Mathf.Clamp(
                    candidate.x,
                    bossPosition.x - allowedX,
                    bossPosition.x + allowedX);
                candidate.z = Mathf.Clamp(
                    candidate.z,
                    bossPosition.z - allowedZ,
                    bossPosition.z + allowedZ);
            }

            candidate.y = bossPosition.y;
            return candidate;
        }

        // 직선 공격이 보스에서 군단장 방향을 바라보도록 수평 방향을 계산한다.
        private static Vector3 ResolvePreviewDirection()
        {
            var direction = spec.FacingTarget == null
                ? previewBoss.transform.forward
                : spec.FacingTarget.position - previewBoss.transform.position;
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f
                ? previewBoss.transform.forward
                : direction.normalized;
        }

        // 런타임에서 사용하는 공격별 경고 색상을 미리보기에도 적용한다.
        private static Color ResolveTelegraphColor()
        {
            return spec.Kind switch
            {
                FallenCommanderAttackPreviewKind.Melee => MeleeTelegraphColor,
                FallenCommanderAttackPreviewKind.MarkStrike => MarkTelegraphColor,
                FallenCommanderAttackPreviewKind.TrackingMark => TrackingMarkTelegraphColor,
                FallenCommanderAttackPreviewKind.BlackHole => BlackHoleTelegraphColor,
                FallenCommanderAttackPreviewKind.CorruptionRing => CorruptionRingTelegraphColor,
                FallenCommanderAttackPreviewKind.FinalCharge => FinalChargeTelegraphColor,
                _ => BasicTelegraphColor
            };
        }

        // 데이터에 지정된 투사체 프리팹을 런타임과 같은 크기와 위치로 생성한다.
        private static void CreateBasicProjectile()
        {
            var attack = spec.BasicAttack;
            if (attack == null || basicProjectileLaunched)
            {
                return;
            }

            basicProjectile = attack.ProjectilePrefab == null
                ? GameObject.CreatePrimitive(PrimitiveType.Sphere)
                : UnityEngine.Object.Instantiate(attack.ProjectilePrefab);
            basicProjectile.name = "[미리보기] 기본 공격 투사체";
            basicProjectile.hideFlags = HideFlags.HideAndDontSave;
            basicProjectile.transform.SetParent(previewBoss.transform, true);
            basicProjectile.transform.SetPositionAndRotation(
                basicProjectilePosition,
                Quaternion.LookRotation(basicProjectileDirection, Vector3.up));
            basicProjectile.transform.localScale *= Mathf.Max(
                0.1f,
                attack.ProjectileRadius * 2f);

            foreach (var collider in basicProjectile.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (var behaviour in basicProjectile.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            foreach (var particle in basicProjectile.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Simulate(0f, false, true, true);
                particle.Play(false);
                Particles.Add(particle);
            }

            basicProjectileLaunched = true;
            if (basicProjectileTravelRemaining <= 0f)
            {
                UpdateBasicAttack(0f);
            }
        }

        // 선택한 미리보기 단계에 맞는 애니메이션 클립을 샘플링한다.
        private static void UpdateMotion()
        {
            if (mode == FallenCommanderAttackPreviewMode.PreCast)
            {
                Sample(spec.PreCastMotion, elapsed, spec.PreCastMotionSpeed);
                return;
            }

            if (mode == FallenCommanderAttackPreviewMode.Cast)
            {
                Sample(spec.CastMotion, elapsed, spec.CastMotionSpeed);
                return;
            }

            if (!hasResolved)
            {
                Sample(spec.PreCastMotion, elapsed, spec.PreCastMotionSpeed);
                return;
            }

            Sample(
                spec.CastMotion,
                Mathf.Max(0f, elapsed - resolveTime),
                spec.CastMotionSpeed);
        }

        // 시전 VFX와 SFX를 실제 공격 시작 위치 기준으로 재생한다.
        private static void PlayStartPresentation()
        {
            ResolveVfxTransform(
                true,
                out var position,
                out var rotation,
                out var scale);
            var parent = previewBoss.transform;
            var instance = CreateVfx(
                spec.Effects?.StartVfxPrefab,
                position,
                rotation,
                scale,
                parent);
            if (instance != null)
            {
                startVfxInstance = instance;
            }

            PlayAudio(
                spec.Effects?.StartSfx,
                spec.Effects == null ? 0f : spec.Effects.StartSfxDuration);
        }

        // 적중 VFX와 SFX를 실제 공격 해결 위치 기준으로 재생한다.
        private static void PlayResolvePresentation()
        {
            ResolveVfxTransform(
                false,
                out var position,
                out var rotation,
                out var scale);
            CreateVfx(
                spec.Effects?.ResolveVfxPrefab,
                position,
                rotation,
                scale,
                previewBoss.transform);
            PlayAudio(
                spec.Effects?.ResolveSfx,
                spec.Effects == null ? 0f : spec.Effects.ResolveSfxDuration);
        }

        // 데이터의 위치 기준·오프셋·회전·크기를 현재 미리보기 오브젝트 기준으로 계산한다.
        private static void ResolveVfxTransform(
            bool isStart,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 scale)
        {
            var direction = spec.Kind == FallenCommanderAttackPreviewKind.Basic &&
                basicProjectileDirection.sqrMagnitude > 0.0001f
                    ? basicProjectileDirection
                    : previewBoss.transform.forward;
            var projectilePosition = basicProjectile != null
                ? basicProjectile.transform.position
                : spec.Kind == FallenCommanderAttackPreviewKind.Basic
                    ? basicProjectilePosition
                    : (Vector3?)null;
            var context = new FallenCommanderEffectPlacementContext(
                ResolveEffectPosition(isStart),
                direction,
                previewBoss.transform.position,
                spec.FacingTarget == null
                    ? (Vector3?)null
                    : spec.FacingTarget.position,
                projectilePosition);
            var placement = FallenCommanderEffectPlacementResolver.Resolve(
                spec.Effects,
                isStart
                    ? FallenCommanderEffectStage.Start
                    : FallenCommanderEffectStage.Resolve,
                context);
            position = placement.Position;
            rotation = placement.Rotation;
            scale = placement.Scale;
        }

        // 충전 광역기 데이터를 수정하는 동안 시전 VFX의 위치·회전·크기를 즉시 갱신한다.
        private static void ApplyLiveStartVfxTransform()
        {
            if (startVfxInstance == null || spec.Effects?.StartVfxPrefab == null)
            {
                return;
            }

            ResolveVfxTransform(true, out var position, out var rotation, out var scale);
            startVfxInstance.transform.SetPositionAndRotation(position, rotation);
            startVfxInstance.transform.localScale = Vector3.Scale(
                spec.Effects.StartVfxPrefab.transform.localScale,
                scale);
        }

        // 공격 종류에 따라 보스·군단장·투사체 높이 중 연출 위치를 선택한다.
        private static Vector3 ResolveEffectPosition(bool isStart)
        {
            if (spec.Kind == FallenCommanderAttackPreviewKind.Basic)
            {
                return basicProjectilePosition;
            }

            if (spec.Kind == FallenCommanderAttackPreviewKind.MarkStrike ||
                spec.Kind == FallenCommanderAttackPreviewKind.TrackingMark)
            {
                return spec.FacingTarget == null
                    ? previewBoss.transform.position
                    : spec.FacingTarget.position;
            }

            if (spec.Kind == FallenCommanderAttackPreviewKind.FinalCharge && isStart)
            {
                var offset = spec.Config == null
                    ? spec.StartEffectLocalOffset
                    : spec.Config.FinalChargeStartEffectOffset;
                return previewBoss.transform.TransformPoint(offset);
            }

            return previewBoss.transform.position;
        }

        // VFX 프리팹을 임시 보스 계층에 만들고 자식 파티클을 에디터 재생 목록에 등록한다.
        private static GameObject CreateVfx(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Transform parent)
        {
            if (prefab == null)
            {
                return null;
            }

            var instance = UnityEngine.Object.Instantiate(prefab, position, rotation, parent);
            instance.name = $"[미리보기] {prefab.name}";
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.localScale = Vector3.Scale(
                instance.transform.localScale,
                scale);
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Simulate(0f, false, true, true);
                particle.Play(false);
                Particles.Add(particle);
            }

            return instance;
        }

        // AnimationMode에서 보스 Animator에 지정된 모션의 현재 프레임을 적용한다.
        private static void Sample(AnimationClip motion, float time, float playbackSpeed)
        {
            if (previewAnimator == null || motion == null)
            {
                return;
            }

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(
                previewAnimator.gameObject,
                motion,
                Mathf.Clamp(
                    time * Mathf.Max(0.01f, playbackSpeed),
                    0f,
                    motion.length));
            AnimationMode.EndSampling();
        }

        // Unity Editor의 AudioUtil을 이용해 씬 오브젝트 없이 SFX를 재생한다.
        private static void PlayAudio(AudioClip clip, float duration)
        {
            StopAudio();
            if (clip == null || PlayPreviewClipMethod == null)
            {
                return;
            }

            PlayPreviewClipMethod.Invoke(null, new object[] { clip, 0, false });
            var playDuration = duration > 0f
                ? Mathf.Min(duration, clip.length)
                : clip.length;
            audioStopTime = elapsed + Mathf.Max(0.01f, playDuration);
            isAudioPlaying = true;
        }

        // Unity Editor에서 재생 중인 미리보기 SFX를 중지한다.
        private static void StopAudio()
        {
            if (isAudioPlaying && StopPreviewClipsMethod != null)
            {
                StopPreviewClipsMethod.Invoke(null, null);
            }

            isAudioPlaying = false;
            audioStopTime = 0f;
        }

        // 모션·VFX·SFX 중 가장 긴 항목을 기준으로 미리보기 종료시간을 계산한다.
        private static float ResolveTotalDuration(
            FallenCommanderAttackPreviewSpec previewSpec,
            FallenCommanderAttackPreviewMode previewMode)
        {
            if (previewSpec.Kind == FallenCommanderAttackPreviewKind.Basic &&
                previewSpec.BasicAttack != null)
            {
                var warningDuration = Mathf.Max(0.1f, previewSpec.BasicAttack.WarningDuration);
                var startDuration = ResolveStageDuration(previewSpec.Effects, true);
                if (previewMode == FallenCommanderAttackPreviewMode.PreCast)
                {
                    return Mathf.Max(warningDuration, startDuration);
                }

                var travelDuration = ResolveBasicTravelDistance(previewSpec, out var willHit) /
                    Mathf.Max(0.1f, previewSpec.BasicAttack.ProjectileSpeed);
                var resolveDuration = willHit
                    ? ResolveStageDuration(previewSpec.Effects, false)
                    : 0.2f;
                var attackDuration = travelDuration + resolveDuration;
                return previewMode == FallenCommanderAttackPreviewMode.Cast
                    ? Mathf.Max(0.2f, attackDuration)
                    : Mathf.Max(startDuration, warningDuration + attackDuration);
            }

            if (previewMode == FallenCommanderAttackPreviewMode.PreCast)
            {
                return Mathf.Max(
                    0.2f,
                    previewSpec.PreCastMotionDuration,
                    ResolveStageDuration(previewSpec.Effects, true),
                    previewSpec.TelegraphPrefab == null
                        ? 0f
                        : previewSpec.WarningDuration);
            }

            var castDuration = Mathf.Max(
                0.2f,
                previewSpec.CastMotionDuration,
                ResolveStageDuration(previewSpec.Effects, false));
            return previewMode == FallenCommanderAttackPreviewMode.Cast
                ? castDuration
                : Mathf.Max(0.1f, previewSpec.WarningDuration) + castDuration;
        }

        // 군단장까지의 거리와 투사체 반지름을 반영해 실제 충돌 또는 최대 이동거리를 계산한다.
        private static float ResolveBasicTravelDistance(
            FallenCommanderAttackPreviewSpec previewSpec,
            out bool willHit)
        {
            var attack = previewSpec.BasicAttack;
            var maxDistance = Mathf.Max(0.1f, attack == null ? 0f : attack.MaxDistance);
            if (attack == null || previewSpec.FacingTarget == null)
            {
                willHit = false;
                return maxDistance;
            }

            var origin = previewSpec.SpawnPoint == null
                ? previewSpec.BossPrefab.transform.position
                : previewSpec.SpawnPoint.position;
            var target = previewSpec.FacingTarget.position;
            origin.y = 0f;
            target.y = 0f;
            var impactDistance = Mathf.Max(
                0f,
                Vector3.Distance(origin, target) - Mathf.Max(0.1f, attack.ProjectileRadius));
            willHit = impactDistance <= maxDistance;
            return willHit ? impactDistance : maxDistance;
        }

        // 해당 단계에 설정된 VFX·SFX 유지시간을 하나의 비교값으로 변환한다.
        private static float ResolveStageDuration(
            FallenCommanderAttackEffectData effects,
            bool isStart)
        {
            if (effects == null)
            {
                return 0f;
            }

            var vfxDuration = isStart
                ? effects.StartVfxDuration
                : effects.ResolveVfxDuration;
            var clip = isStart ? effects.StartSfx : effects.ResolveSfx;
            var sfxDuration = isStart
                ? effects.StartSfxDuration
                : effects.ResolveSfxDuration;
            if (sfxDuration <= 0f && clip != null)
            {
                sfxDuration = clip.length;
            }

            return Mathf.Max(vfxDuration, sfxDuration, 2f);
        }

        // 임시 상태를 다음 미리보기가 사용할 수 있도록 초기값으로 되돌린다.
        private static void ClearState()
        {
            spec = null;
            previewBoss = null;
            previewAnimator = null;
            startVfxInstance = null;
            basicTelegraph = null;
            attackTelegraph = null;
            secondaryAttackTelegraph = null;
            basicProjectile = null;
            basicProjectilePosition = Vector3.zero;
            basicProjectileDirection = Vector3.zero;
            basicProjectileTravelRemaining = 0f;
            basicProjectileWillHit = false;
            basicProjectileLaunched = false;
            basicProjectileFinished = false;
            Particles.Clear();
            elapsed = 0f;
            resolveTime = 0f;
            totalDuration = 0f;
            audioStopTime = 0f;
            hasResolved = false;
            isAudioPlaying = false;
        }

        // 미리보기 보스를 군단장 방향으로 즉시 회전시킨다.
        private static void FaceTarget(Transform bossTransform, Transform facingTarget)
        {
            if (bossTransform == null || facingTarget == null)
            {
                return;
            }

            var direction = facingTarget.position - bossTransform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            bossTransform.rotation = Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);
        }
    }
}
