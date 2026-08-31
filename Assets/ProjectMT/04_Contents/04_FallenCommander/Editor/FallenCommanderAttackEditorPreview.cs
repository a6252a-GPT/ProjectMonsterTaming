using System;
using System.Collections.Generic;
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
        None,
        Basic,
        Melee,
        MarkStrike,
        TrackingMark,
        BlackHole,
        LineStrike,
        CorruptionRing,
        FinalCharge,
        TimeoutWipe,
        TwistedBattlefield,
        FallingBarrage
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
        public FallenCommanderMarkStrikePhaseData MarkStrikePattern { get; set; }
        public FallenCommanderTwistedBattlefieldData TwistedBattlefield { get; set; }
        public int TwistedBeatCount { get; set; } = 2;
        public float TwistedBeatInterval { get; set; } = 0.3f;
        public float TwistedAttackInterval { get; set; } = 0.8f;
        public FallenCommanderFallingBarrageData FallingBarrage { get; set; }
        public int FallingProjectileCount { get; set; } = 12;
        public float FallingSpawnInterval { get; set; } = 0.08f;
        public float FallingSpawnJitter { get; set; } = 0.06f;
        public float FallingDuration { get; set; } = 1.4f;
        public float FallingAirHoldDuration { get; set; } = 0.6f;
        public FallenCommanderAttackEffectData Effects { get; set; }
        public GameObject TelegraphPrefab { get; set; }
        public float TelegraphRadius { get; set; }
        public float TelegraphWidth { get; set; }
        public float TelegraphLength { get; set; }
        public float SecondaryTelegraphRadius { get; set; }
        public Func<float> TelegraphRadiusProvider { get; set; }
        public Func<float> TelegraphWidthProvider { get; set; }
        public Func<float> TelegraphLengthProvider { get; set; }
        public Func<float> SecondaryTelegraphRadiusProvider { get; set; }
        public float BlackHoleActiveDuration { get; set; }
        public FallenCommanderAttackEffectData BlackHoleEndEffects { get; set; }
        public AnimationClip PreCastMotion { get; set; }
        public float PreCastMotionSpeed { get; set; } = 1f;
        public float PreCastMotionStart { get; set; }
        public float PreCastMotionEnd { get; set; } = 1f;
        public AnimationClip CastMotion { get; set; }
        public float CastMotionDuration { get; set; }
        public float CastMotionSpeed { get; set; } = 1f;
        public float CastMotionStart { get; set; }
        public float CastMotionEnd { get; set; } = 1f;
        public float WarningDuration { get; set; } = 0.1f;
        public float TelegraphHoldDuration { get; set; }
        public float TimeoutRiseHeight { get; set; }
        public AnimationCurve TimeoutRiseCurve { get; set; }
        public Vector3 StartEffectLocalOffset { get; set; }
    }

    [InitializeOnLoad]
    internal static class FallenCommanderAttackPreviewController
    {
        private sealed class PreviewVfxLifetime
        {
            public GameObject Instance { get; set; }
            public float DestroyAt { get; set; }
        }

        private sealed class TwistedPreviewTile
        {
            public bool IsDangerous { get; set; }
            public Vector3 Center { get; set; }
            public Vector2 Size { get; set; }
            public FallenCommanderTelegraphView Telegraph { get; set; }
        }

        private sealed class MarkPreviewStrike
        {
            public Vector3 Position { get; set; }
            public float SpawnAt { get; set; }
            public float ResolveAt { get; set; }
            public bool Resolved { get; set; }
            public FallenCommanderTelegraphView Telegraph { get; set; }
        }

        private sealed class FallingPreviewShot
        {
            public GameObject Projectile { get; set; }
            public FallenCommanderTelegraphView Telegraph { get; set; }
            public Vector3 Target { get; set; }
            public float StartDelay { get; set; }
            public bool Resolved { get; set; }
        }

        private static readonly List<ParticleSystem> Particles = new();
        private static readonly List<PreviewVfxLifetime> VfxLifetimes = new();
        private static readonly List<TwistedPreviewTile> TwistedTiles = new();
        private static readonly List<int> TwistedRecordedLayouts = new();
        private static readonly List<bool> TwistedRecordedInversions = new();
        private static readonly List<MarkPreviewStrike> MarkStrikes = new();
        private static readonly List<GameObject> TwistedResolveVfx = new();
        private static readonly List<FallingPreviewShot> FallingShots = new();
        private static readonly Color BasicTelegraphColor =
            FallenCommanderTelegraphPalette.Danger;
        private static readonly Color MeleeTelegraphColor =
            FallenCommanderTelegraphPalette.Danger;
        private static readonly Color LineTelegraphColor =
            FallenCommanderTelegraphPalette.Danger;
        private static readonly Color MarkTelegraphColor =
            FallenCommanderTelegraphPalette.Danger;
        private static readonly Color TrackingMarkTelegraphColor =
            FallenCommanderTelegraphPalette.Danger;
        private static readonly Color BlackHoleTelegraphColor =
            FallenCommanderTelegraphPalette.Danger;
        private static readonly Color CorruptionRingTelegraphColor =
            FallenCommanderTelegraphPalette.Danger;
        private static readonly Color CorruptionRingSafeColor =
            FallenCommanderTelegraphPalette.Safe;
        private static readonly Color FinalChargeTelegraphColor =
            FallenCommanderTelegraphPalette.Danger;

        private static FallenCommanderAttackPreviewSpec spec;
        private static FallenCommanderAttackPreviewMode mode;
        private static GameObject previewRoot;
        private static GameObject previewBoss;
        private static Animator previewAnimator;
        private static GameObject startVfxInstance;
        private static GameObject activeResolveVfxInstance;
        private static FallenCommanderTelegraphView basicTelegraph;
        private static FallenCommanderTelegraphView attackTelegraph;
        private static FallenCommanderTelegraphView secondaryAttackTelegraph;
        private static GameObject basicProjectile;
        private static Vector3 basicProjectilePosition;
        private static Vector3 basicProjectileDirection;
        private static Vector3 lockedAttackPosition;
        private static Vector3 previewBossStartPosition;
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
        private static bool hasCompletedBlackHole;
        private static int twistedBeatIndex;
        private static int twistedLayoutIndex;
        private static float twistedBeatStartTime;
        private static float twistedLastResolveTime;
        private static bool twistedBeatResolved;
        private static bool twistedReplaying;
        private static bool twistedReplayComplete;
        private static int twistedReplayIndex;
        private static bool isAudioPlaying;

        public static bool IsActive => previewBoss != null && spec != null;

        // 에디터 상태가 바뀌어도 임시 보스·연출·오디오가 남지 않도록 정리 경로를 등록한다.
        static FallenCommanderAttackPreviewController()
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

            GameObject createdRoot = null;
            GameObject createdBoss = null;
            var initialized = false;
            try
            {
                createdRoot = new GameObject("[공격 미리보기 루트]")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                createdRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                createdRoot.transform.localScale = Vector3.one;

                createdBoss = PrefabUtility.InstantiatePrefab(
                    previewSpec.BossPrefab) as GameObject;
                if (createdBoss == null)
                {
                    return false;
                }

                createdBoss.name =
                    $"[공격 미리보기] {previewSpec.Label} - {previewSpec.BossPrefab.name}";
                createdBoss.hideFlags = HideFlags.HideAndDontSave;
                createdBoss.transform.SetParent(createdRoot.transform, true);
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
                previewRoot = createdRoot;
                previewBoss = createdBoss;
                previewBossStartPosition = createdBoss.transform.position;
                lockedAttackPosition = ResolveInitialAttackPosition();
                previewAnimator = createdBoss.GetComponentInChildren<Animator>(true);
                elapsed = 0f;
                resolveTime = Mathf.Max(0.1f, previewSpec.WarningDuration) +
                    Mathf.Max(0f, previewSpec.TelegraphHoldDuration);
                hasResolved = previewMode == FallenCommanderAttackPreviewMode.Cast;
                hasCompletedBlackHole = false;
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
                    if (previewSpec.Kind == FallenCommanderAttackPreviewKind.BlackHole)
                    {
                        BeginBlackHoleActivePreview();
                    }
                    else if (previewSpec.Kind == FallenCommanderAttackPreviewKind.TwistedBattlefield)
                    {
                        BeginTwistedBattlefieldBeat();
                        SetTwistedBattlefieldProgress(1f);
                        PlayDangerAreaResolvePresentation();
                        PlayHitPresentation();
                    }
                    else if (previewSpec.Kind == FallenCommanderAttackPreviewKind.FallingBarrage)
                    {
                        BeginFallingBarragePreview(true);
                    }
                    else if (previewSpec.Kind == FallenCommanderAttackPreviewKind.MarkStrike)
                    {
                        BeginMarkStrikePreview(true);
                    }
                    else
                    {
                        PlayDangerAreaResolvePresentation();
                        PlayHitPresentation();
                    }

                    Sample(
                        previewSpec.CastMotion,
                        0f,
                        previewSpec.CastMotionSpeed,
                        previewSpec.CastMotionStart,
                        previewSpec.CastMotionEnd);
                }
                else
                {
                    PlayStartPresentation();
                    if (previewSpec.Kind == FallenCommanderAttackPreviewKind.TwistedBattlefield)
                    {
                        BeginTwistedBattlefieldBeat();
                    }
                    else if (previewSpec.Kind == FallenCommanderAttackPreviewKind.FallingBarrage)
                    {
                        BeginFallingBarragePreview(false);
                    }
                    else if (previewSpec.Kind == FallenCommanderAttackPreviewKind.MarkStrike)
                    {
                        BeginMarkStrikePreview(false);
                    }
                    else
                    {
                        BeginAttackTelegraphPreview();
                    }
                    Sample(
                        previewSpec.PreCastMotion,
                        0f,
                        previewSpec.PreCastMotionSpeed,
                        previewSpec.PreCastMotionStart,
                        previewSpec.PreCastMotionEnd);
                }

                initialized = true;
                SceneView.RepaintAll();
                return true;
            }
            finally
            {
                if (!initialized)
                {
                    if (createdRoot != null)
                    {
                        UnityEngine.Object.DestroyImmediate(createdRoot);
                    }
                    else if (createdBoss != null)
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

            if (previewRoot != null)
            {
                FallenCommanderPreviewCleanup.Destroy(previewRoot);
            }
            else if (previewBoss != null)
            {
                FallenCommanderPreviewCleanup.Destroy(previewBoss);
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

            UpdateVfxLifetimes();
            RefreshTelegraphPreviewIfNeeded();

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
            else if (spec.Kind == FallenCommanderAttackPreviewKind.TwistedBattlefield)
            {
                UpdateTwistedBattlefieldPreview();
            }
            else if (spec.Kind == FallenCommanderAttackPreviewKind.FallingBarrage)
            {
                UpdateFallingBarragePreview();
            }
            else if (spec.Kind == FallenCommanderAttackPreviewKind.MarkStrike)
            {
                UpdateMarkStrikePreview();
            }
            else
            {
                UpdateTimeoutWipeRisePreview();
                UpdateAttackTelegraphPreview();
                if (mode == FallenCommanderAttackPreviewMode.Full &&
                    !hasResolved &&
                    elapsed >= resolveTime)
                {
                    hasResolved = true;
                    if (spec.Kind == FallenCommanderAttackPreviewKind.BlackHole)
                    {
                        BeginBlackHoleActivePreview();
                    }
                    else
                    {
                        CompleteAttackTelegraphPreview();
                        if (spec.Kind == FallenCommanderAttackPreviewKind.TimeoutWipe)
                        {
                            DestroyPreviewVfx(ref startVfxInstance);
                        }

                        PlayDangerAreaResolvePresentation();
                        PlayHitPresentation();
                    }
                }

                UpdateBlackHoleCompletion();
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

        private static void UpdateTimeoutWipeRisePreview()
        {
            if (spec.Kind != FallenCommanderAttackPreviewKind.TimeoutWipe ||
                previewBoss == null ||
                mode == FallenCommanderAttackPreviewMode.Cast)
            {
                return;
            }

            var riseDuration = Mathf.Max(
                0.01f,
                spec.WarningDuration + spec.TelegraphHoldDuration);
            var progress = Mathf.Clamp01(elapsed / riseDuration);
            var curveProgress = spec.TimeoutRiseCurve == null
                ? Mathf.SmoothStep(0f, 1f, progress)
                : Mathf.Clamp01(spec.TimeoutRiseCurve.Evaluate(progress));
            previewBoss.transform.position = previewBossStartPosition +
                Vector3.up * spec.TimeoutRiseHeight * curveProgress;
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

            basicTelegraph = FallenCommanderTelegraphView.CreateRectangle(
                attack.TelegraphPrefab,
                previewRoot.transform,
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

        // 경고 게이지를 채운 뒤 발동 연출과 함께 투사체를 목표 방향으로 이동시킨다.
        private static void UpdateBasicAttack(float deltaTime)
        {
            var attack = spec.BasicAttack;
            if (attack == null)
            {
                return;
            }

            var warningDuration = Mathf.Max(0.1f, attack.WarningDuration);
            var attackStartTime = warningDuration + attack.TelegraphHoldDuration;
            if (basicTelegraph != null)
            {
                basicTelegraph.SetProgress(Mathf.Clamp01(elapsed / warningDuration));
                if (elapsed >= attackStartTime)
                {
                    basicTelegraph.gameObject.SetActive(false);
                }
            }

            if (mode == FallenCommanderAttackPreviewMode.Full &&
                !basicProjectileLaunched &&
                elapsed >= attackStartTime)
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
                PlayHitPresentation();
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
                attackTelegraph = FallenCommanderPreviewRangeRenderer.CreateLine(
                    spec.TelegraphPrefab,
                    previewRoot.transform,
                    position,
                    ResolvePreviewDirection(),
                    spec.TelegraphWidth,
                    spec.TelegraphLength,
                    LineTelegraphColor);
            }
            else
            {
                attackTelegraph = FallenCommanderPreviewRangeRenderer.CreateCircle(
                    spec.TelegraphPrefab,
                    previewRoot.transform,
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

            secondaryAttackTelegraph = FallenCommanderPreviewRangeRenderer.CreateCircle(
                spec.TelegraphPrefab,
                previewRoot.transform,
                position + Vector3.up * 0.035f,
                spec.SecondaryTelegraphRadius,
                CorruptionRingSafeColor);
            if (secondaryAttackTelegraph != null)
            {
                secondaryAttackTelegraph.gameObject.hideFlags = HideFlags.HideAndDontSave;
                secondaryAttackTelegraph.SetProgress(1f);
            }
        }

        // 2페이즈의 개수·묶음·간격을 사용해 실제 연속 위치 공격과 같은 순서로 장판을 준비한다.
        private static void BeginMarkStrikePreview(bool resolveImmediately)
        {
            DestroyMarkStrikes();
            var settings = spec.MarkStrikePattern;
            if (settings == null || spec.TelegraphPrefab == null || previewRoot == null)
            {
                return;
            }

            var center = previewBoss == null ? Vector3.zero : previewBoss.transform.position;
            var totalCount = settings.TotalCount;
            var groupSize = settings.SimultaneousCount;
            for (var index = 0; index < totalCount; index++)
            {
                var groupIndex = index / groupSize;
                var angle = index * Mathf.PI * 2f / Mathf.Max(1, totalCount);
                var normalizedRadius = 0.35f + 0.5f * ((index % 3) / 2f);
                var position = center + new Vector3(
                    Mathf.Cos(angle) * settings.ArenaHalfExtents.x * normalizedRadius,
                    0f,
                    Mathf.Sin(angle) * settings.ArenaHalfExtents.y * normalizedRadius);
                var spawnAt = resolveImmediately ? 0f : groupIndex * settings.GroupInterval;
                MarkStrikes.Add(new MarkPreviewStrike
                {
                    Position = position,
                    SpawnAt = spawnAt,
                    ResolveAt = spawnAt + settings.WarningDuration + spec.TelegraphHoldDuration
                });
            }

            if (!resolveImmediately)
            {
                return;
            }

            for (var index = 0; index < MarkStrikes.Count; index++)
            {
                ResolveMarkStrike(MarkStrikes[index]);
            }
        }

        // 각 묶음의 생성시각과 독립 경고시간을 따라 장판 진행도와 발동 VFX를 갱신한다.
        private static void UpdateMarkStrikePreview()
        {
            var settings = spec.MarkStrikePattern;
            if (settings == null || mode == FallenCommanderAttackPreviewMode.Cast)
            {
                return;
            }

            for (var index = 0; index < MarkStrikes.Count; index++)
            {
                var strike = MarkStrikes[index];
                if (elapsed < strike.SpawnAt)
                {
                    continue;
                }

                if (strike.Telegraph == null && !strike.Resolved)
                {
                    strike.Telegraph = FallenCommanderTelegraphView.CreateCircle(
                        spec.TelegraphPrefab,
                        previewRoot.transform,
                        strike.Position,
                        spec.TelegraphRadius,
                        MarkTelegraphColor);
                    if (strike.Telegraph != null)
                    {
                        strike.Telegraph.gameObject.hideFlags = HideFlags.HideAndDontSave;
                    }
                }

                var fillElapsed = Mathf.Min(
                    settings.WarningDuration,
                    Mathf.Max(0f, elapsed - strike.SpawnAt));
                strike.Telegraph?.SetProgress(
                    fillElapsed / Mathf.Max(0.1f, settings.WarningDuration));
                if (!strike.Resolved && elapsed >= strike.ResolveAt)
                {
                    ResolveMarkStrike(strike);
                }
            }
        }

        private static void ResolveMarkStrike(MarkPreviewStrike strike)
        {
            strike.Resolved = true;
            if (strike.Telegraph != null)
            {
                UnityEngine.Object.DestroyImmediate(strike.Telegraph.gameObject);
                strike.Telegraph = null;
            }

            var previousPosition = lockedAttackPosition;
            lockedAttackPosition = strike.Position;
            CreateDangerAreaResolveVfx(
                strike.Position,
                Vector3.forward,
                Vector3.one * Mathf.Max(0.1f, spec.TelegraphRadius));
            PlayResolveAudio();
            lockedAttackPosition = previousPosition;
        }

        private static void DestroyMarkStrikes()
        {
            for (var index = 0; index < MarkStrikes.Count; index++)
            {
                var telegraph = MarkStrikes[index].Telegraph;
                if (telegraph != null)
                {
                    UnityEngine.Object.DestroyImmediate(telegraph.gameObject);
                }
            }

            MarkStrikes.Clear();
        }

        // 연속 장판 공격의 현재 박자를 실제 패턴과 같은 무작위 분할 장판으로 생성한다.
        private static void BeginTwistedBattlefieldBeat()
        {
            DestroyTwistedResolveVfx();
            DestroyTwistedBattlefieldTiles();
            var data = spec.TwistedBattlefield;
            if (data == null || data.TelegraphPrefab == null || previewRoot == null)
            {
                return;
            }

            if ((twistedBeatIndex & 1) == 0)
            {
                twistedLayoutIndex = UnityEngine.Random.Range(0, 3);
            }

            BuildTwistedBattlefieldTiles(
                twistedLayoutIndex,
                (twistedBeatIndex & 1) == 1,
                true);
            twistedBeatStartTime = elapsed;
            twistedBeatResolved = false;
        }

        private static void BuildTwistedBattlefieldTiles(
            int layoutIndex,
            bool isInverted,
            bool showTelegraph)
        {
            var data = spec.TwistedBattlefield;
            if (data == null || previewRoot == null)
            {
                return;
            }

            var extents = data.ArenaHalfExtents;
            var columns = layoutIndex == 1 ? 1 : data.ColumnCount;
            var rows = layoutIndex == 0 ? 1 :
                layoutIndex == 1 ? Mathf.Max(2, data.RowCount * 2) : data.RowCount;
            var cellWidth = extents.x * 2f / columns;
            var cellLength = extents.y * 2f / rows;
            var visibleWidth = Mathf.Max(0.1f, cellWidth - data.TileGap);
            var visibleLength = Mathf.Max(0.1f, cellLength - data.TileGap);
            var center = previewBoss == null ? Vector3.zero : previewBoss.transform.position;
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var tileCenter = center + new Vector3(
                        -extents.x + cellWidth * (column + 0.5f),
                        0f,
                        -extents.y + cellLength * (row + 0.5f));
                    var isDangerous = ((row + column) & 1) == 0;
                    if (isInverted)
                    {
                        isDangerous = !isDangerous;
                    }

                    FallenCommanderTelegraphView telegraph = null;
                    if (showTelegraph)
                    {
                        var origin = tileCenter - Vector3.forward * (visibleLength * 0.5f);
                        telegraph = FallenCommanderTelegraphView.CreateRectangle(
                            data.TelegraphPrefab,
                            previewRoot.transform,
                            origin,
                            Vector3.forward,
                            visibleWidth,
                            visibleLength,
                            isDangerous
                                ? FallenCommanderTelegraphPalette.Danger
                                : FallenCommanderTelegraphPalette.Safe);
                    }
                    if (telegraph != null)
                    {
                        telegraph.gameObject.hideFlags = HideFlags.HideAndDontSave;
                        telegraph.SetProgress(isDangerous ? 0f : 1f);
                    }

                    TwistedTiles.Add(new TwistedPreviewTile
                    {
                        IsDangerous = isDangerous,
                        Center = tileCenter,
                        Size = new Vector2(cellWidth, cellLength),
                        Telegraph = telegraph
                    });
                }
            }
        }

        // 경고 게이지를 채우고 발동 후 다음 박자에서 위험·안전 장판을 반전한다.
        private static void UpdateTwistedBattlefieldPreview()
        {
            if (mode == FallenCommanderAttackPreviewMode.Cast ||
                spec.TwistedBattlefield == null)
            {
                return;
            }

            if (twistedReplaying)
            {
                var replayInterval = twistedReplayIndex == 0
                    ? spec.TwistedBeatInterval
                    : spec.TwistedAttackInterval;
                if (!twistedReplayComplete &&
                    elapsed >= twistedLastResolveTime + replayInterval)
                {
                    ResolveRecordedTwistedBeat();
                }

                return;
            }

            var localElapsed = Mathf.Max(0f, elapsed - twistedBeatStartTime);
            var progress = Mathf.Clamp01(localElapsed / Mathf.Max(0.1f, spec.WarningDuration));
            SetTwistedBattlefieldProgress(progress);

            if (mode == FallenCommanderAttackPreviewMode.PreCast)
            {
                return;
            }

            var resolveAt = spec.WarningDuration + spec.TelegraphHoldDuration;
            if (!twistedBeatResolved && localElapsed >= resolveAt)
            {
                twistedBeatResolved = true;
                twistedLastResolveTime = elapsed;
                TwistedRecordedLayouts.Add(twistedLayoutIndex);
                TwistedRecordedInversions.Add((twistedBeatIndex & 1) == 1);
                DestroyTwistedBattlefieldTiles();
                twistedBeatIndex++;
                if (twistedBeatIndex >= spec.TwistedBeatCount)
                {
                    twistedReplaying = true;
                    twistedReplayIndex = 0;
                }
            }

            if (!twistedBeatResolved || twistedReplaying ||
                elapsed < twistedLastResolveTime + spec.TwistedBeatInterval)
            {
                return;
            }

            BeginTwistedBattlefieldBeat();
        }

        // 앞에서 보여준 장판 배치를 저장 순서 그대로 복원해 공격 연출만 재생한다.
        private static void ResolveRecordedTwistedBeat()
        {
            DestroyTwistedResolveVfx();
            DestroyTwistedBattlefieldTiles();
            if (twistedReplayIndex >= TwistedRecordedLayouts.Count)
            {
                twistedReplayComplete = true;
                return;
            }

            BuildTwistedBattlefieldTiles(
                TwistedRecordedLayouts[twistedReplayIndex],
                TwistedRecordedInversions[twistedReplayIndex],
                false);
            PlayDangerAreaResolvePresentation();
            PlayHitPresentation();
            DestroyTwistedBattlefieldTiles();
            twistedReplayIndex++;
            twistedLastResolveTime = elapsed;
            twistedReplayComplete = twistedReplayIndex >= TwistedRecordedLayouts.Count;
        }

        // 연속 장판 공격의 모든 위험 장판에 같은 충전 진행도를 적용한다.
        private static void SetTwistedBattlefieldProgress(float progress)
        {
            for (var index = 0; index < TwistedTiles.Count; index++)
            {
                if (TwistedTiles[index].IsDangerous)
                {
                    TwistedTiles[index].Telegraph?.SetProgress(progress);
                }
            }
        }

        // 연속 장판 공격 미리보기에서 생성한 모든 장판을 즉시 정리한다.
        private static void DestroyTwistedBattlefieldTiles()
        {
            for (var index = 0; index < TwistedTiles.Count; index++)
            {
                var telegraph = TwistedTiles[index].Telegraph;
                if (telegraph != null)
                {
                    UnityEngine.Object.DestroyImmediate(telegraph.gameObject);
                }
            }

            TwistedTiles.Clear();
        }

        // 낙하 탄막 미리보기를 공통 수량과 시간 설정으로 생성한다.
        private static void BeginFallingBarragePreview(bool resolveImmediately)
        {
            DestroyFallingBarrageShots();
            var data = spec.FallingBarrage;
            if (data == null || data.ProjectilePrefab == null ||
                data.TelegraphPrefab == null || previewRoot == null)
            {
                return;
            }

            var count = resolveImmediately ? 1 : Mathf.Max(1, spec.FallingProjectileCount);
            var center = previewBoss == null ? Vector3.zero : previewBoss.transform.position;
            var extents = data.ArenaHalfExtents;
            for (var index = 0; index < count; index++)
            {
                var target = resolveImmediately && spec.FacingTarget != null
                    ? spec.FacingTarget.position
                    : center + new Vector3(
                        UnityEngine.Random.Range(-extents.x, extents.x),
                        0f,
                        UnityEngine.Random.Range(-extents.y, extents.y));
                var projectile = UnityEngine.Object.Instantiate(
                    data.ProjectilePrefab,
                    previewRoot.transform);
                projectile.name = "[미리보기] 낙하 탄막";
                projectile.hideFlags = HideFlags.HideAndDontSave;
                projectile.transform.position = target + Vector3.up * data.SpawnHeight;
                DisablePreviewBehaviours(projectile);

                var telegraph = FallenCommanderTelegraphView.CreateCircle(
                    data.TelegraphPrefab,
                    previewRoot.transform,
                    target,
                    data.ImpactRadius,
                    FallenCommanderTelegraphPalette.Danger);
                if (telegraph != null)
                {
                    telegraph.gameObject.hideFlags = HideFlags.HideAndDontSave;
                }

                var shot = new FallingPreviewShot
                {
                    Projectile = projectile,
                    Telegraph = telegraph,
                    Target = target,
                    StartDelay = resolveImmediately
                        ? 0f
                        : data.WarningMessageDuration + Mathf.Max(
                            0f,
                            index * spec.FallingSpawnInterval +
                            UnityEngine.Random.Range(
                                -spec.FallingSpawnJitter,
                                spec.FallingSpawnJitter))
                };
                FallingShots.Add(shot);

                if (resolveImmediately)
                {
                    projectile.transform.position = target;
                    telegraph?.SetProgress(1f);
                    shot.Resolved = true;
                    lockedAttackPosition = target;
                    PlayResolvePresentation(projectile.transform);
                    PlayHitPresentation();
                    continue;
                }

                projectile.SetActive(false);
                telegraph?.gameObject.SetActive(false);
            }
        }

        // 낙하 시작시간의 작은 무작위 차이와 실제 낙하시간으로 탄막 위치를 갱신한다.
        private static void UpdateFallingBarragePreview()
        {
            if (mode == FallenCommanderAttackPreviewMode.Cast ||
                spec?.FallingBarrage == null)
            {
                return;
            }

            var fallDuration = Mathf.Max(0.1f, spec.FallingDuration);
            for (var index = 0; index < FallingShots.Count; index++)
            {
                var shot = FallingShots[index];
                if (shot.Resolved || elapsed < shot.StartDelay)
                {
                    continue;
                }

                shot.Projectile?.SetActive(true);
                shot.Telegraph?.gameObject.SetActive(true);
                var normalizedTime = Mathf.Clamp01(
                    (elapsed - shot.StartDelay - spec.FallingAirHoldDuration) /
                    fallDuration);
                var progress = spec.FallingBarrage.EvaluateFallProgress(normalizedTime);
                if (shot.Projectile != null)
                {
                    shot.Projectile.transform.position = Vector3.Lerp(
                        shot.Target + Vector3.up * spec.FallingBarrage.SpawnHeight,
                        shot.Target,
                        progress);
                }

                shot.Telegraph?.SetProgress(progress);
                if (normalizedTime < 1f)
                {
                    continue;
                }

                shot.Resolved = true;
                if (mode == FallenCommanderAttackPreviewMode.PreCast)
                {
                    continue;
                }

                lockedAttackPosition = shot.Target;
                PlayResolvePresentation(shot.Projectile == null
                    ? null
                    : shot.Projectile.transform);
                shot.Projectile?.SetActive(false);
                shot.Telegraph?.gameObject.SetActive(false);
            }
        }

        // 낙하 탄막 미리보기에서 생성한 투사체와 경고 장판을 즉시 정리한다.
        private static void DestroyFallingBarrageShots()
        {
            for (var index = 0; index < FallingShots.Count; index++)
            {
                if (FallingShots[index].Projectile != null)
                {
                    UnityEngine.Object.DestroyImmediate(FallingShots[index].Projectile);
                }

                if (FallingShots[index].Telegraph != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        FallingShots[index].Telegraph.gameObject);
                }
            }

            FallingShots.Clear();
        }

        // 미리보기 프리팹의 실제 런타임 동작은 끄고 파티클만 에디터 시간으로 재생한다.
        private static void DisablePreviewBehaviours(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Simulate(0f, false, true, true);
                particle.Play(false);
                Particles.Add(particle);
            }
        }

        // Config의 공격 범위 수치가 바뀐 순간에만 현재 미리보기 범위를 다시 생성한다.
        private static void RefreshTelegraphPreviewIfNeeded()
        {
            if (spec == null || mode == FallenCommanderAttackPreviewMode.Cast)
            {
                return;
            }

            if (spec.Kind == FallenCommanderAttackPreviewKind.TwistedBattlefield ||
                spec.Kind == FallenCommanderAttackPreviewKind.FallingBarrage)
            {
                return;
            }

            var radius = ResolveLiveValue(
                spec.TelegraphRadiusProvider,
                spec.TelegraphRadius);
            var width = ResolveLiveValue(
                spec.TelegraphWidthProvider,
                spec.TelegraphWidth);
            var length = ResolveLiveValue(
                spec.TelegraphLengthProvider,
                spec.TelegraphLength);
            var secondaryRadius = ResolveLiveValue(
                spec.SecondaryTelegraphRadiusProvider,
                spec.SecondaryTelegraphRadius);
            if (Mathf.Approximately(spec.TelegraphRadius, radius) &&
                Mathf.Approximately(spec.TelegraphWidth, width) &&
                Mathf.Approximately(spec.TelegraphLength, length) &&
                Mathf.Approximately(spec.SecondaryTelegraphRadius, secondaryRadius))
            {
                return;
            }

            spec.TelegraphRadius = radius;
            spec.TelegraphWidth = width;
            spec.TelegraphLength = length;
            spec.SecondaryTelegraphRadius = secondaryRadius;
            if (spec.Kind == FallenCommanderAttackPreviewKind.Basic)
            {
                if (basicProjectileLaunched)
                {
                    return;
                }

                DestroyTelegraph(ref basicTelegraph);
                basicProjectileTravelRemaining = ResolveBasicTravelDistance(
                    spec,
                    out basicProjectileWillHit);
                basicTelegraph = FallenCommanderTelegraphView.CreateRectangle(
                    spec.TelegraphPrefab,
                    previewRoot.transform,
                    previewBoss.transform.position,
                    basicProjectileDirection,
                    spec.TelegraphWidth,
                    spec.TelegraphLength,
                    BasicTelegraphColor);
                if (basicTelegraph != null)
                {
                    basicTelegraph.gameObject.hideFlags = HideFlags.HideAndDontSave;
                    basicTelegraph.SetProgress(Mathf.Clamp01(
                        elapsed / Mathf.Max(0.1f, spec.BasicAttack.WarningDuration)));
                }

                return;
            }

            if (hasResolved)
            {
                return;
            }

            DestroyTelegraph(ref attackTelegraph);
            DestroyTelegraph(ref secondaryAttackTelegraph);
            BeginAttackTelegraphPreview();
            UpdateAttackTelegraphPreview();
        }

        // 실시간 조회 함수가 없으면 미리보기 시작 시 저장한 값을 그대로 사용한다.
        private static float ResolveLiveValue(Func<float> provider, float fallback)
        {
            return provider == null ? fallback : provider.Invoke();
        }

        // 다시 만들 범위 오브젝트만 즉시 제거하고 참조를 초기화한다.
        private static void DestroyTelegraph(ref FallenCommanderTelegraphView telegraph)
        {
            FallenCommanderPreviewCleanup.Destroy(ref telegraph);
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
                    lockedAttackPosition = position;
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

        // 경고가 끝난 블랙홀 범위를 제거하고 실제 활성 VFX 단계로 전환한다.
        private static void BeginBlackHoleActivePreview()
        {
            CompleteAttackTelegraphPreview();
            DestroyPreviewVfx(ref startVfxInstance);
            activeResolveVfxInstance = PlayResolvePresentation();
            PlayHitPresentation();
        }

        // 실제 활성시간이 끝나면 블랙홀 범위와 활성 VFX를 제거하고 종료 연출을 재생한다.
        private static void UpdateBlackHoleCompletion()
        {
            if (spec.Kind != FallenCommanderAttackPreviewKind.BlackHole ||
                !hasResolved ||
                hasCompletedBlackHole)
            {
                return;
            }

            var activeStartTime = mode == FallenCommanderAttackPreviewMode.Cast
                ? 0f
                : resolveTime;
            if (elapsed < activeStartTime + Mathf.Max(0.1f, spec.BlackHoleActiveDuration))
            {
                return;
            }

            hasCompletedBlackHole = true;
            DestroyPreviewVfx(ref activeResolveVfxInstance);
            PlayBlackHoleEndPresentation();
        }

        // 블랙홀 종료 데이터의 종료 VFX와 SFX를 실제 중심 위치에서 재생한다.
        private static void PlayBlackHoleEndPresentation()
        {
            var effects = spec.BlackHoleEndEffects;
            if (effects == null)
            {
                return;
            }

            var context = new FallenCommanderEffectPlacementContext(
                lockedAttackPosition,
                Vector3.forward,
                previewBoss.transform.position,
                spec.FacingTarget == null
                    ? (Vector3?)null
                    : spec.FacingTarget.position,
                null);
            var placement = FallenCommanderEffectPlacementResolver.Resolve(
                effects,
                FallenCommanderEffectStage.Resolve,
                context);
            CreateVfx(
                effects.ResolveVfxPrefab,
                effects.ResolveVfxDuration,
                placement.Position,
                placement.Rotation,
                placement.Scale,
                previewRoot.transform);
            PlayAudio(
                effects.ResolveSfx,
                effects.ResolveSfxDuration,
                effects.SfxVolume);
        }

        // 공격 종류에 따라 보스 위치·군단장 위치·블랙홀 예시 위치를 선택한다.
        private static Vector3 ResolveTelegraphPosition()
        {
            if (spec.Kind == FallenCommanderAttackPreviewKind.MarkStrike ||
                spec.Kind == FallenCommanderAttackPreviewKind.TrackingMark)
            {
                return lockedAttackPosition;
            }

            if (spec.Kind == FallenCommanderAttackPreviewKind.BlackHole)
            {
                return lockedAttackPosition;
            }

            return previewBoss.transform.position;
        }

        // 공격 시작 시 실제 런타임이 저장하는 고정 공격 지점을 미리보기에도 보관한다.
        private static Vector3 ResolveInitialAttackPosition()
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

            basicProjectile = FallenCommanderPreviewProjectilePlayer.Create(
                attack.ProjectilePrefab,
                previewRoot.transform,
                basicProjectilePosition,
                basicProjectileDirection,
                attack.ProjectileRadius,
                Particles);
            PlayResolvePresentation();

            basicProjectileLaunched = true;
            if (basicProjectileTravelRemaining <= 0f)
            {
                UpdateBasicAttack(0f);
            }
        }

        // 선택한 미리보기 단계에 맞는 애니메이션 클립을 샘플링한다.
        private static void UpdateMotion()
        {
            if (spec.Kind == FallenCommanderAttackPreviewKind.TwistedBattlefield &&
                mode == FallenCommanderAttackPreviewMode.Full)
            {
                if (twistedBeatIndex == 0 && !twistedBeatResolved)
                {
                    Sample(
                        spec.PreCastMotion,
                        elapsed,
                        spec.PreCastMotionSpeed,
                        spec.PreCastMotionStart,
                        spec.PreCastMotionEnd);
                    return;
                }

                Sample(
                    spec.CastMotion,
                    Mathf.Max(0f, elapsed - twistedLastResolveTime),
                    spec.CastMotionSpeed,
                    spec.CastMotionStart,
                    spec.CastMotionEnd);
                return;
            }

            if (mode == FallenCommanderAttackPreviewMode.PreCast)
            {
                Sample(
                    spec.PreCastMotion,
                    elapsed,
                    spec.PreCastMotionSpeed,
                    spec.PreCastMotionStart,
                    spec.PreCastMotionEnd);
                return;
            }

            if (mode == FallenCommanderAttackPreviewMode.Cast)
            {
                Sample(
                    spec.CastMotion,
                    elapsed,
                    spec.CastMotionSpeed,
                    spec.CastMotionStart,
                    spec.CastMotionEnd);
                return;
            }

            if (!hasResolved)
            {
                Sample(
                    spec.PreCastMotion,
                    elapsed,
                    spec.PreCastMotionSpeed,
                    spec.PreCastMotionStart,
                    spec.PreCastMotionEnd);
                return;
            }

            Sample(
                spec.CastMotion,
                Mathf.Max(0f, elapsed - resolveTime),
                spec.CastMotionSpeed,
                spec.CastMotionStart,
                spec.CastMotionEnd);
        }

        // 시전 VFX와 SFX를 실제 공격 시작 위치 기준으로 재생한다.
        private static void PlayStartPresentation()
        {
            ResolveVfxTransform(
                true,
                out var position,
                out var rotation,
                out var scale);
            var parent = spec.Kind == FallenCommanderAttackPreviewKind.FinalCharge
                ? previewBoss.transform
                : previewRoot.transform;
            var instance = CreateVfx(
                spec.Effects?.StartVfxPrefab,
                spec.Effects == null ? 0f : spec.Effects.StartVfxDuration,
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
                spec.Effects == null ? 0f : spec.Effects.StartSfxDuration,
                spec.Effects == null ? 1f : spec.Effects.SfxVolume);
        }

        // 발동 VFX와 SFX를 실제 공격 해결 위치 기준으로 재생한다.
        private static GameObject PlayResolvePresentation(
            Transform projectileOverride = null)
        {
            ResolveVfxTransform(
                FallenCommanderEffectStage.Resolve,
                projectileOverride,
                out var position,
                out var rotation,
                out var scale);
            var instance = CreateVfx(
                spec.Effects?.ResolveVfxPrefab,
                spec.Effects == null ? 0f : spec.Effects.ResolveVfxDuration,
                position,
                rotation,
                scale,
                previewRoot.transform);
            PlayAudio(
                spec.Effects?.ResolveSfx,
                spec.Effects == null ? 0f : spec.Effects.ResolveSfxDuration,
                spec.Effects == null ? 1f : spec.Effects.SfxVolume);
            return instance;
        }

        private static void PlayDangerAreaResolvePresentation()
        {
            if (spec.Kind == FallenCommanderAttackPreviewKind.TwistedBattlefield)
            {
                for (var index = 0; index < TwistedTiles.Count; index++)
                {
                    var tile = TwistedTiles[index];
                    if (!tile.IsDangerous)
                    {
                        continue;
                    }

                    CreateDangerAreaResolveVfx(
                        tile.Center,
                        Vector3.forward,
                        ResolveTileVfxScale(tile.Size));
                }

                PlayResolveAudio();
                return;
            }

            if (spec.Kind != FallenCommanderAttackPreviewKind.CorruptionRing)
            {
                PlayResolvePresentation();
                return;
            }

            var center = ResolveTelegraphPosition();
            var safeRadius = Mathf.Max(0f, spec.SecondaryTelegraphRadius);
            var outerRadius = Mathf.Max(safeRadius + 0.1f, spec.TelegraphRadius);
            var instance = CreateDangerAreaResolveVfx(
                center,
                Vector3.forward,
                Vector3.one * outerRadius);
            if (instance != null &&
                instance.TryGetComponent<FallenCommanderRingVfxView>(out var ringView))
            {
                ringView.Configure(safeRadius, outerRadius);
            }

            PlayResolveAudio();
        }

        private static Vector3 ResolveTileVfxScale(Vector2 tileSize)
        {
            return new Vector3(
                Mathf.Max(0.01f, tileSize.x),
                1f,
                Mathf.Max(0.01f, tileSize.y));
        }

        private static GameObject CreateDangerAreaResolveVfx(
            Vector3 position,
            Vector3 direction,
            Vector3 areaScale)
        {
            var effects = spec.Effects;
            if (effects == null)
            {
                return null;
            }

            var context = new FallenCommanderEffectPlacementContext(
                position,
                direction,
                previewBoss == null ? (Vector3?)null : previewBoss.transform.position,
                spec.FacingTarget == null
                    ? (Vector3?)null
                    : spec.FacingTarget.position,
                null);
            var placement = FallenCommanderEffectPlacementResolver.Resolve(
                effects,
                FallenCommanderEffectStage.Resolve,
                context);
            var instance = CreateVfx(
                effects.ResolveVfxPrefab,
                effects.ResolveVfxDuration,
                placement.Position,
                placement.Rotation,
                Vector3.Scale(placement.Scale, areaScale),
                previewRoot.transform);
            if (spec.Kind == FallenCommanderAttackPreviewKind.TwistedBattlefield &&
                instance != null)
            {
                TwistedResolveVfx.Add(instance);
            }

            return instance;
        }

        private static void DestroyTwistedResolveVfx()
        {
            for (var index = TwistedResolveVfx.Count - 1; index >= 0; index--)
            {
                var instance = TwistedResolveVfx[index];
                for (var lifetimeIndex = VfxLifetimes.Count - 1;
                    lifetimeIndex >= 0;
                    lifetimeIndex--)
                {
                    if (VfxLifetimes[lifetimeIndex].Instance == instance)
                    {
                        VfxLifetimes.RemoveAt(lifetimeIndex);
                    }
                }

                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            TwistedResolveVfx.Clear();
        }

        private static void PlayResolveAudio()
        {
            PlayAudio(
                spec.Effects?.ResolveSfx,
                spec.Effects == null ? 0f : spec.Effects.ResolveSfxDuration,
                spec.Effects == null ? 1f : spec.Effects.SfxVolume);
        }

        // 적중 VFX와 SFX를 미리보기 군단장의 충돌 위치에서 별도로 재생한다.
        private static void PlayHitPresentation()
        {
            ResolveVfxTransform(
                FallenCommanderEffectStage.Hit,
                out var position,
                out var rotation,
                out var scale);
            CreateVfx(
                spec.Effects?.HitVfxPrefab,
                spec.Effects == null ? 0f : spec.Effects.HitVfxDuration,
                position,
                rotation,
                scale,
                previewRoot.transform);
            PlayAudio(
                spec.Effects?.HitSfx,
                spec.Effects == null ? 0f : spec.Effects.HitSfxDuration,
                spec.Effects == null ? 1f : spec.Effects.HitSfxVolume);
        }

        // 데이터의 위치 기준·오프셋·회전·크기를 현재 미리보기 오브젝트 기준으로 계산한다.
        private static void ResolveVfxTransform(
            bool isStart,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 scale)
        {
            ResolveVfxTransform(
                isStart
                    ? FallenCommanderEffectStage.Start
                    : FallenCommanderEffectStage.Resolve,
                out position,
                out rotation,
                out scale);
        }

        // 지정한 연출 단계의 배치값을 공격 위치와 미리보기 군단장 위치를 기준으로 계산한다.
        private static void ResolveVfxTransform(
            FallenCommanderEffectStage stage,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 scale)
        {
            ResolveVfxTransform(
                stage,
                null,
                out position,
                out rotation,
                out scale);
        }

        private static void ResolveVfxTransform(
            FallenCommanderEffectStage stage,
            Transform projectileOverride,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 scale)
        {
            var isStart = stage == FallenCommanderEffectStage.Start;
            var direction = spec.Kind == FallenCommanderAttackPreviewKind.Basic &&
                basicProjectileDirection.sqrMagnitude > 0.0001f
                    ? basicProjectileDirection
                    : previewBoss.transform.forward;
            var projectilePosition = projectileOverride != null
                ? projectileOverride.position
                : basicProjectile != null
                    ? basicProjectile.transform.position
                : spec.Kind == FallenCommanderAttackPreviewKind.Basic
                    ? basicProjectilePosition
                    : (Vector3?)null;
            var context = new FallenCommanderEffectPlacementContext(
                stage == FallenCommanderEffectStage.Hit && spec.FacingTarget != null
                    ? spec.FacingTarget.position
                    : ResolveEffectPosition(isStart),
                direction,
                previewBoss.transform.position,
                spec.FacingTarget == null
                    ? (Vector3?)null
                    : spec.FacingTarget.position,
                projectilePosition);
            var placement = FallenCommanderEffectPlacementResolver.Resolve(
                spec.Effects,
                stage,
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
                spec.Kind == FallenCommanderAttackPreviewKind.TrackingMark ||
                spec.Kind == FallenCommanderAttackPreviewKind.BlackHole)
            {
                return lockedAttackPosition;
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
            float duration,
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

            VfxLifetimes.Add(new PreviewVfxLifetime
            {
                Instance = instance,
                DestroyAt = elapsed + ResolveVfxLifetime(instance, duration)
            });

            return instance;
        }

        // 런타임과 같은 유지시간이 지난 VFX만 미리보기 루트에서 제거한다.
        private static void UpdateVfxLifetimes()
        {
            for (var index = VfxLifetimes.Count - 1; index >= 0; index--)
            {
                var lifetime = VfxLifetimes[index];
                if (lifetime.Instance != null && elapsed < lifetime.DestroyAt)
                {
                    continue;
                }

                if (lifetime.Instance == startVfxInstance)
                {
                    startVfxInstance = null;
                }

                if (lifetime.Instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(lifetime.Instance);
                }

                VfxLifetimes.RemoveAt(index);
            }
        }

        // 즉시 전환되는 단계의 VFX를 수명 목록과 미리보기 루트에서 함께 제거한다.
        private static void DestroyPreviewVfx(ref GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            for (var index = VfxLifetimes.Count - 1; index >= 0; index--)
            {
                if (VfxLifetimes[index].Instance == instance)
                {
                    VfxLifetimes.RemoveAt(index);
                }
            }

            UnityEngine.Object.DestroyImmediate(instance);
            instance = null;
        }

        // 설정값이 없으면 자식 파티클의 최대 재생시간을 계산해 런타임 제거시간과 맞춘다.
        private static float ResolveVfxLifetime(GameObject instance, float overrideDuration)
        {
            if (overrideDuration > 0f)
            {
                return Mathf.Max(0.01f, overrideDuration);
            }

            var lifetime = 2f;
            foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                lifetime = Mathf.Max(
                    lifetime,
                    main.duration + main.startLifetime.constantMax);
            }

            return Mathf.Clamp(lifetime, 0.1f, 10f);
        }

        // 실제 전투와 동일하게 공격 모션을 반복하지 않고 마지막 프레임에서 멈춘다.
        private static void Sample(
            AnimationClip motion,
            float time,
            float playbackSpeed,
            float normalizedStart,
            float normalizedEnd)
        {
            FallenCommanderPreviewMotionPlayer.Sample(
                previewAnimator,
                motion,
                time,
                playbackSpeed,
                normalizedStart,
                normalizedEnd);
        }

        // Unity Editor의 AudioUtil을 이용해 씬 오브젝트 없이 SFX를 재생한다.
        private static void PlayAudio(
            AudioClip clip,
            float duration,
            float volume)
        {
            if (!FallenCommanderPreviewEffectPlayer.PlayAudio(clip, volume))
            {
                return;
            }
            var playDuration = duration > 0f
                ? Mathf.Min(duration, clip.length)
                : clip.length;
            audioStopTime = Mathf.Max(
                audioStopTime,
                elapsed + Mathf.Max(0.01f, playDuration));
            isAudioPlaying = true;
        }

        // Unity Editor에서 재생 중인 미리보기 SFX를 중지한다.
        private static void StopAudio()
        {
            FallenCommanderPreviewEffectPlayer.StopAudio();

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
                var attackStartTime = warningDuration +
                    previewSpec.BasicAttack.TelegraphHoldDuration;
                var startDuration = ResolveStageDuration(previewSpec.Effects, true);
                if (previewMode == FallenCommanderAttackPreviewMode.PreCast)
                {
                    return Mathf.Max(attackStartTime, startDuration);
                }

                var travelDuration = ResolveBasicTravelDistance(previewSpec, out var willHit) /
                    Mathf.Max(0.1f, previewSpec.BasicAttack.ProjectileSpeed);
                var resolveDuration = willHit
                    ? ResolveStageDuration(previewSpec.Effects, false)
                    : 0.2f;
                var attackDuration = travelDuration + resolveDuration;
                return previewMode == FallenCommanderAttackPreviewMode.Cast
                    ? Mathf.Max(0.2f, attackDuration)
                    : Mathf.Max(startDuration, attackStartTime + attackDuration);
            }

            if (previewSpec.Kind == FallenCommanderAttackPreviewKind.TwistedBattlefield)
            {
                var beatDuration = Mathf.Max(0.1f, previewSpec.WarningDuration) +
                    previewSpec.TelegraphHoldDuration;
                if (previewMode == FallenCommanderAttackPreviewMode.PreCast)
                {
                    return Mathf.Max(
                        beatDuration,
                        ResolveStageDuration(previewSpec.Effects, true));
                }

                var twistedCastDuration = Mathf.Max(
                    0.2f,
                    previewSpec.CastMotionDuration,
                    ResolveStageDuration(previewSpec.Effects, false),
                    ResolveHitStageDuration(previewSpec.Effects));
                if (previewMode == FallenCommanderAttackPreviewMode.Cast)
                {
                    return Mathf.Max(0.8f, twistedCastDuration);
                }

                var beatCount = Mathf.Max(2, previewSpec.TwistedBeatCount);
                return beatDuration * beatCount +
                    Mathf.Max(0f, previewSpec.TwistedBeatInterval) * beatCount +
                    Mathf.Max(0.1f, previewSpec.TwistedAttackInterval) *
                        (beatCount - 1) +
                    twistedCastDuration;
            }

            if (previewSpec.Kind == FallenCommanderAttackPreviewKind.MarkStrike &&
                previewSpec.MarkStrikePattern != null)
            {
                var settings = previewSpec.MarkStrikePattern;
                var groupCount = Mathf.CeilToInt(
                    settings.TotalCount / (float)settings.SimultaneousCount);
                var lastGroupStart = Mathf.Max(0, groupCount - 1) * settings.GroupInterval;
                var sequenceDuration = lastGroupStart + settings.WarningDuration +
                    previewSpec.TelegraphHoldDuration;
                if (previewMode == FallenCommanderAttackPreviewMode.Cast)
                {
                    return Mathf.Max(
                        0.2f,
                        ResolveStageDuration(previewSpec.Effects, false));
                }

                return sequenceDuration +
                    (previewMode == FallenCommanderAttackPreviewMode.Full
                        ? ResolveStageDuration(previewSpec.Effects, false)
                        : 0f);
            }

            if (previewSpec.Kind == FallenCommanderAttackPreviewKind.FallingBarrage)
            {
                var fallingDuration = Mathf.Max(0.1f, previewSpec.FallingDuration);
                var fallingCastDuration = Mathf.Max(
                    0.2f,
                    previewSpec.CastMotionDuration,
                    ResolveStageDuration(previewSpec.Effects, false),
                    ResolveHitStageDuration(previewSpec.Effects));
                if (previewMode == FallenCommanderAttackPreviewMode.Cast)
                {
                    return fallingCastDuration;
                }

                var lastStartTime = Mathf.Max(0, previewSpec.FallingProjectileCount - 1) *
                    Mathf.Max(0f, previewSpec.FallingSpawnInterval) +
                    Mathf.Max(0f, previewSpec.FallingSpawnJitter);
                var barrageDuration = previewSpec.FallingBarrage.WarningMessageDuration +
                    lastStartTime +
                    Mathf.Max(0f, previewSpec.FallingAirHoldDuration) +
                    fallingDuration;
                return previewMode == FallenCommanderAttackPreviewMode.PreCast
                    ? barrageDuration
                    : barrageDuration + fallingCastDuration;
            }

            if (previewMode == FallenCommanderAttackPreviewMode.PreCast)
            {
                return Mathf.Max(
                    0.2f,
                    ResolveStageDuration(previewSpec.Effects, true),
                    previewSpec.TelegraphPrefab == null
                        ? 0f
                        : previewSpec.WarningDuration + previewSpec.TelegraphHoldDuration);
            }

            if (previewSpec.Kind == FallenCommanderAttackPreviewKind.BlackHole &&
                previewMode != FallenCommanderAttackPreviewMode.PreCast)
            {
                var activeStartTime = previewMode == FallenCommanderAttackPreviewMode.Cast
                    ? 0f
                    : Mathf.Max(0.1f, previewSpec.WarningDuration) +
                        previewSpec.TelegraphHoldDuration;
                var endDuration = ResolveStageDuration(
                    previewSpec.BlackHoleEndEffects,
                    false);
                var hitDuration = ResolveHitStageDuration(previewSpec.Effects);
                return activeStartTime +
                    Mathf.Max(
                        Mathf.Max(0.1f, previewSpec.BlackHoleActiveDuration) +
                            endDuration,
                        hitDuration);
            }

            var castDuration = Mathf.Max(
                0.2f,
                previewSpec.CastMotionDuration,
                ResolveStageDuration(previewSpec.Effects, false),
                ResolveHitStageDuration(previewSpec.Effects));
            return previewMode == FallenCommanderAttackPreviewMode.Cast
                ? castDuration
                : Mathf.Max(0.1f, previewSpec.WarningDuration) +
                    previewSpec.TelegraphHoldDuration + castDuration;
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

            var prefab = isStart
                ? effects.StartVfxPrefab
                : effects.ResolveVfxPrefab;
            var overrideDuration = isStart
                ? effects.StartVfxDuration
                : effects.ResolveVfxDuration;
            var vfxDuration = prefab == null
                ? 0f
                : ResolveVfxLifetime(prefab, overrideDuration);
            var clip = isStart ? effects.StartSfx : effects.ResolveSfx;
            var sfxDuration = isStart
                ? effects.StartSfxDuration
                : effects.ResolveSfxDuration;
            if (sfxDuration <= 0f && clip != null)
            {
                sfxDuration = clip.length;
            }

            return Mathf.Max(vfxDuration, sfxDuration, 0.2f);
        }

        // 적중 VFX와 SFX 중 더 긴 재생시간을 미리보기 유지시간으로 반환한다.
        private static float ResolveHitStageDuration(
            FallenCommanderAttackEffectData effects)
        {
            if (effects == null)
            {
                return 0f;
            }

            var vfxDuration = effects.HitVfxPrefab == null
                ? 0f
                : ResolveVfxLifetime(
                    effects.HitVfxPrefab,
                    effects.HitVfxDuration);
            var sfxDuration = effects.HitSfxDuration;
            if (sfxDuration <= 0f && effects.HitSfx != null)
            {
                sfxDuration = effects.HitSfx.length;
            }

            return Mathf.Max(vfxDuration, sfxDuration, 0.2f);
        }

        // 임시 상태를 다음 미리보기가 사용할 수 있도록 초기값으로 되돌린다.
        private static void ClearState()
        {
            DestroyTwistedBattlefieldTiles();
            DestroyFallingBarrageShots();
            DestroyMarkStrikes();
            spec = null;
            previewRoot = null;
            previewBoss = null;
            previewAnimator = null;
            startVfxInstance = null;
            activeResolveVfxInstance = null;
            basicTelegraph = null;
            attackTelegraph = null;
            secondaryAttackTelegraph = null;
            basicProjectile = null;
            basicProjectilePosition = Vector3.zero;
            basicProjectileDirection = Vector3.zero;
            lockedAttackPosition = Vector3.zero;
            previewBossStartPosition = Vector3.zero;
            basicProjectileTravelRemaining = 0f;
            basicProjectileWillHit = false;
            basicProjectileLaunched = false;
            basicProjectileFinished = false;
            Particles.Clear();
            VfxLifetimes.Clear();
            TwistedResolveVfx.Clear();
            TwistedRecordedLayouts.Clear();
            TwistedRecordedInversions.Clear();
            elapsed = 0f;
            resolveTime = 0f;
            totalDuration = 0f;
            audioStopTime = 0f;
            hasResolved = false;
            hasCompletedBlackHole = false;
            twistedBeatIndex = 0;
            twistedLayoutIndex = 0;
            twistedBeatStartTime = 0f;
            twistedLastResolveTime = 0f;
            twistedBeatResolved = false;
            twistedReplaying = false;
            twistedReplayComplete = false;
            twistedReplayIndex = 0;
            isAudioPlaying = false;
        }

        // 미리보기 보스를 군단장 방향으로 즉시 회전시킨다.
        private static void FaceTarget(Transform bossTransform, Transform facingTarget)
        {
            FallenCommanderPreviewMotionPlayer.FaceTarget(
                bossTransform,
                facingTarget);
        }
    }

    internal static class FallenCommanderAttackEditorPreview
    {
        public static bool IsActive => FallenCommanderAttackPreviewController.IsActive;

        public static bool Play(
            FallenCommanderAttackPreviewSpec previewSpec,
            FallenCommanderAttackPreviewMode previewMode)
        {
            return FallenCommanderAttackPreviewController.Play(
                previewSpec,
                previewMode);
        }

        public static void Stop()
        {
            FallenCommanderAttackPreviewController.Stop();
        }
    }
}
