using System.Collections;
using System.Collections.Generic;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Pooling;
using ProjectMT.Shared.Stats;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatWorld : MonoBehaviour // 한 전투의 유닛·공격 조율
    {
        [SerializeField] private ScenePoolScope poolScope; // 전투 객체 재사용 창고
        [SerializeField] private CombatFeedbackPlayer feedbackPlayer; // 공용 전투 연출
        [SerializeField] private GameObject projectilePrefab; // 원거리 공격 투사체
        [SerializeField, Min(1)] private int maxMonsterVfxPerFrame = 6; // 전용 Marker VFX 예산
        [SerializeField, Min(1)] private int maxMonsterActiveVfxPerFrame = 64; // 액티브 다중 탄·다중 명중 전용 예산
        [SerializeField, Min(1)] private int maxMonsterFeelPerFrame = 6; // FEEL 프리셋 독립 예산
        private static bool showMonsterBasicAttackHitAreas; // 디버그 버튼으로만 켜는 실제 XZ 판정 표시

        private readonly List<UnitActor> units = new List<UnitActor>(); // 현재 등록 유닛
        private readonly List<MonsterAttackAreaIndicator> monsterBasicAttackHitAreas =
            new List<MonsterAttackAreaIndicator>();
        private readonly MeleeAttackExecutor meleeExecutor = new MeleeAttackExecutor();
        private readonly ProjectileAttackExecutor projectileExecutor = new ProjectileAttackExecutor();
        private readonly MonsterBasicAttackExecutor basicAttackExecutor = new MonsterBasicAttackExecutor();
        private readonly SpecialActionExecutor specialExecutor = new SpecialActionExecutor();
        private readonly List<ActiveFocusRequest> activeFocusQueue = new List<ActiveFocusRequest>();
        private ActiveFocusRequest activeFocus;
        private MonsterActiveFocusPresenter activeFocusPresenter;
        private IMonsterActiveFocusCamera activeFocusCamera;
        private GameObject activeFocusHaloInstance;
        private float activeFocusElapsed;
        private float activeFocusResolvedDuration;
        private float activeFocusReadyWait;
        private bool activeFocusCommitted;
        private bool activeFocusVisible;
        private MonsterActiveFocusPreset activeFocusPreset;
        private long nextActiveFocusSequence;
        private int monsterVfxFrame = -1;
        private int monsterVfxCount;
        private int monsterActiveVfxFrame = -1;
        private int monsterActiveVfxCount;
        private int monsterFeelFrame = -1;
        private int monsterFeelCount;
        private static CombatStatConfig sharedStatConfig;

        public ICombatFeedbackPlayer Feedback => feedbackPlayer;
        public bool IsPaused { get; private set; }
        public UnitActor ActiveFocusCaster => activeFocus?.Caster;
        public int ActiveFocusQueueCount => activeFocusQueue.Count;
        public bool IsMonsterActiveFocusVisible => activeFocusVisible;
        public static bool MonsterBasicAttackHitAreasVisible => showMonsterBasicAttackHitAreas;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDebugSettings()
        {
            showMonsterBasicAttackHitAreas = false; // Play 시작마다 기본 OFF
        }

        public static void SetMonsterBasicAttackHitAreasVisible(bool visible)
        {
            showMonsterBasicAttackHitAreas = visible;
            if (visible)
            {
                return;
            }

            var worlds = FindObjectsByType<CombatWorld>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < worlds.Length; index++)
            {
                worlds[index]?.ClearMonsterBasicAttackHitAreas();
            }
        }

        public static void ConfigureSharedStatRules(CombatStatConfig config)
        {
            sharedStatConfig = config ?? CombatStatConfig.RuntimeDefault;
        }

        private void Update()
        {
            activeFocusPresenter?.Tick(Time.unscaledDeltaTime);
            if (IsPaused)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            TickMonsterActiveFocus(Time.unscaledDeltaTime);
            // 08.07 안건준 수정 - unit.Tick() 도중에(예: 마지막 적 처치로 콘텐츠가 즉시 Complete/Fail 처리되어
            // combatWorld.Clear()가 동기적으로 호출되는 경우) units 목록이 갑자기 비워지거나 크게 줄어들 수 있다.
            // 반복문 시작 시점의 개수(i)만 믿고 접근하면 "Index was out of range" 예외가 발생하므로,
            // 매 반복마다 현재 목록 크기 안에 있는지 다시 확인한다.
            for (var i = units.Count - 1; i >= 0; i--)
            {
                if (i >= units.Count)
                {
                    continue; // 목록이 줄어들어 이미 유효하지 않은 인덱스는 건너뛴다
                }

                var unit = units[i];
                if (unit == null)
                {
                    units.RemoveAt(i);
                    continue;
                }

                var localScale = GetMonsterActiveFocusTimeScale(unit);
                var unitDelta = deltaTime * localScale;
                unit.SetActiveFocusTimeScale(localScale);
                unit.Tick(unitDelta); // 액티브 강조 중 시전자 외 유닛만 국소 감속
            }
        }

        private void OnDisable()
        {
            CompleteMonsterActiveFocus(true, true);
            activeFocusCamera?.ResetMonsterActiveFocus();
        }

        public UnitActor SpawnUnit(GameObject prefab, UnitSpawnRequest request, Vector3 position, Quaternion rotation)
        {
            var resolvedPrefab = request.RuntimeAssetSet != null &&
                                 request.RuntimeAssetSet.VisualAdapterPrefab != null
                ? request.RuntimeAssetSet.VisualAdapterPrefab
                : prefab;
            if (poolScope == null || resolvedPrefab == null)
            {
                return null;
            }

            var instance = poolScope.Rent(resolvedPrefab, position, rotation, transform); // 정식 Adapter 또는 기존 Prefab
            var behaviours = instance == null ? null : instance.GetComponents<MonoBehaviour>();
            if (behaviours != null)
            {
                for (var index = 0; index < behaviours.Length; index++)
                {
                    if (!(behaviours[index] is IUnitSpawnPreparation preparation))
                    {
                        continue;
                    }

                    try
                    {
                        if (preparation.PrepareForSpawn(request))
                        {
                            continue;
                        }
                    }
                    catch (System.Exception exception)
                    {
                        Debug.LogException(exception, behaviours[index]);
                    }

                    Debug.LogError($"Unit spawn preparation failed: {resolvedPrefab.name}", instance);
                    poolScope.Return(instance);
                    return null;
                }
            }

            var actor = instance == null ? null : instance.GetComponent<UnitActor>();
            if (actor == null)
            {
                Debug.LogError($"Unit prefab has no UnitActor: {resolvedPrefab.name}");
                if (instance != null)
                {
                    poolScope.Return(instance);
                }

                return null;
            }

            actor.Initialize(request, this, feedbackPlayer);
            return actor;
        }

        public void Register(UnitActor unit)
        {
            if (unit != null && !units.Contains(unit))
            {
                units.Add(unit);
            }
        }

        public void Unregister(UnitActor unit)
        {
            if (unit != null)
            {
                CancelMonsterActiveFocus(unit);
                feedbackPlayer?.UntrackUnit(unit);
                units.Remove(unit);
            }
        }

        public void TrackMonsterActiveSkill(UnitActor unit)
        {
            feedbackPlayer?.TrackMonsterActiveSkill(unit);
        }

        public bool RequestMonsterActiveFocus(
            UnitActor caster,
            MonsterActiveSkill skill,
            System.Action commit,
            float commitDelay = 0.24f,
            float totalDuration = 0.72f,
            System.Action begin = null)
        {
            return RequestMonsterActiveFocus(
                caster,
                skill,
                () => caster != null ? caster.Target : null,
                () => true,
                begin,
                () =>
                {
                    commit?.Invoke();
                    return true;
                },
                null,
                null,
                commitDelay,
                totalDuration);
        }

        public bool RequestMonsterActiveFocus(
            UnitActor caster,
            MonsterActiveSkill skill,
            System.Func<UnitActor> targetResolver,
            System.Func<bool> canArm,
            System.Action begin,
            System.Func<bool> commit,
            System.Action cancel,
            System.Func<bool> commitSignal,
            float commitDelay = 0.24f,
            float totalDuration = 0.42f)
        {
            if (caster == null || skill == null || commit == null || HasMonsterActiveFocusRequest(caster))
            {
                return false;
            }

            activeFocusQueue.Add(new ActiveFocusRequest(
                caster,
                skill,
                targetResolver,
                canArm,
                begin,
                commit,
                cancel,
                commitSignal,
                commitDelay,
                totalDuration,
                Time.unscaledTime,
                caster.ActiveFocusPartySlotIndex,
                nextActiveFocusSequence++));
            activeFocusQueue.Sort(ActiveFocusRequest.Compare);
            // 같은 프레임에 준비된 요청을 모두 받은 뒤 다음 CombatWorld Tick에서 안정 정렬합니다.
            return true;
        }

        private void TickMonsterActiveFocus(float unscaledDeltaTime)
        {
            if (activeFocus == null)
            {
                BeginNextMonsterActiveFocus();
                return;
            }
            if (activeFocus.Caster == null || !activeFocus.Caster.IsAlive)
            {
                CompleteMonsterActiveFocus(false, true);
                return;
            }

            var step = Mathf.Clamp(unscaledDeltaTime, 0f, 0.1f);
            if (!activeFocus.Armed)
            {
                activeFocusReadyWait += step;
                if (!TryArmMonsterActiveFocus())
                {
                    if (activeFocusReadyWait >= ActiveFocusRequest.MaxReadyWait)
                    {
                        CompleteMonsterActiveFocus(false, true);
                    }
                    return;
                }
            }

            activeFocusElapsed += step;
            var focusStart = Mathf.Max(0f, activeFocus.CommitDelay - activeFocusPreset.FocusLead);
            if (!activeFocusVisible && !activeFocusCommitted && activeFocusElapsed >= focusStart)
            {
                ShowMonsterActiveFocusPresentation();
            }

            var commitSignalReached = false;
            if (!activeFocusCommitted && activeFocus.CommitSignal != null)
            {
                try
                {
                    commitSignalReached = activeFocus.CommitSignal();
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, activeFocus.Caster);
                }
            }
            if (!activeFocusCommitted &&
                (commitSignalReached || activeFocusElapsed >= activeFocus.CommitDelay))
            {
                if (!activeFocusVisible)
                {
                    ShowMonsterActiveFocusPresentation();
                }

                var committed = false;
                try
                {
                    committed = activeFocus.Commit?.Invoke() == true;
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, activeFocus.Caster);
                }

                if (!committed)
                {
                    CompleteMonsterActiveFocus(false, true);
                    return;
                }

                activeFocusCommitted = true;
                ReleaseMonsterActiveFocusPresentation(false); // 판정 프레임에 전투 속도 즉시 복구
            }

            if (activeFocusCommitted && activeFocusElapsed >= activeFocusResolvedDuration)
            {
                CompleteMonsterActiveFocus(false, false);
            }
        }

        private void BeginNextMonsterActiveFocus()
        {
            while (activeFocusQueue.Count > 0)
            {
                var next = activeFocusQueue[0];
                activeFocusQueue.RemoveAt(0);
                if (next.Caster == null || !next.Caster.IsAlive)
                {
                    next.Cancel?.Invoke();
                    continue;
                }

                activeFocus = next;
                activeFocusElapsed = 0f;
                activeFocusReadyWait = 0f;
                activeFocusCommitted = false;
                activeFocusVisible = false;
                var config = MonsterActiveFocusPresentationConfig.Current;
                activeFocusPreset = config != null
                    ? config.ResolvePreset(next.Caster.Presentation.Rarity)
                    : default;
                var focusStart = Mathf.Max(0f, next.CommitDelay - activeFocusPreset.FocusLead);
                activeFocusResolvedDuration = Mathf.Max(
                    next.Duration,
                    focusStart + activeFocusPreset.MinimumVisibleDuration);
                TryArmMonsterActiveFocus();
                return;
            }
            activeFocus = null;
            activeFocusResolvedDuration = 0f;
        }

        private bool TryArmMonsterActiveFocus()
        {
            if (activeFocus == null || activeFocus.Armed)
            {
                return activeFocus?.Armed == true;
            }
            if (activeFocus.CanArm != null && !activeFocus.CanArm())
            {
                return false;
            }

            try
            {
                activeFocus.Begin?.Invoke();
                activeFocus.Armed = true;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, activeFocus.Caster);
                return false;
            }

            var focusStart = Mathf.Max(0f, activeFocus.CommitDelay - activeFocusPreset.FocusLead);
            if (focusStart <= 0f)
            {
                ShowMonsterActiveFocusPresentation();
            }
            return true;
        }

        private void ShowMonsterActiveFocusPresentation()
        {
            if (activeFocus == null || activeFocusVisible)
            {
                return;
            }

            if (activeFocusPresenter == null)
            {
                var host = feedbackPlayer != null ? feedbackPlayer.gameObject : gameObject;
                var prefab = MonsterActiveFocusPresentationConfig.Current?.PresenterPrefab;
                if (prefab != null)
                {
                    activeFocusPresenter = Instantiate(prefab, host.transform);
                    activeFocusPresenter.name = "MonsterActiveFocusHud";
                }
                else
                {
                    activeFocusPresenter = host.GetComponent<MonsterActiveFocusPresenter>() ??
                                           host.AddComponent<MonsterActiveFocusPresenter>();
                }
            }

            var target = activeFocus.TargetResolver?.Invoke();
            var camera = activeFocusCamera?.WorldCamera;
            activeFocusPresenter.Show(
                activeFocus.Caster,
                target,
                activeFocus.Skill,
                activeFocusPreset,
                camera);
            activeFocusCamera?.BeginMonsterActiveFocus(activeFocus.Caster, target, activeFocusPreset);
            var startSfx = MonsterActiveFocusPresentationConfig.Current?.ResolveStartSfx(
                activeFocus.Caster.Presentation.Rarity);
            if (startSfx != null)
            {
                PlayMonsterSfx(startSfx, activeFocus.Caster.transform.position);
            }
            var haloPrefab = MonsterActiveFocusPresentationConfig.Current?.ResolveHaloPrefab(
                activeFocus.Caster.Presentation.Rarity);
            if (haloPrefab != null)
            {
                activeFocusHaloInstance = RentMonsterObject(
                    haloPrefab,
                    activeFocus.Caster.transform.position,
                    activeFocus.Caster.transform.rotation,
                    activeFocus.Caster.transform);
                if (activeFocusHaloInstance != null)
                {
                    activeFocusHaloInstance.transform.localPosition = Vector3.zero;
                    MonsterBasicAttackVfxPlayback.RestartAtOffset(
                        activeFocusHaloInstance,
                        0f,
                        playbackSpeed: 1f);
                }
            }
            activeFocusVisible = true;
        }

        private void ReleaseMonsterActiveFocusPresentation(bool immediate)
        {
            for (var index = 0; index < units.Count; index++)
            {
                units[index]?.SetActiveFocusTimeScale(1f);
            }
            activeFocusVisible = false;
            if (activeFocusHaloInstance != null)
            {
                ReturnMonsterObject(activeFocusHaloInstance);
                activeFocusHaloInstance = null;
            }
            if (immediate)
            {
                activeFocusPresenter?.HideImmediate();
                activeFocusCamera?.ResetMonsterActiveFocus();
            }
            else
            {
                activeFocusPresenter?.BeginRelease();
                activeFocusCamera?.EndMonsterActiveFocus();
            }
        }

        private void CompleteMonsterActiveFocus(bool clearQueue, bool cancelled = false)
        {
            if (cancelled && activeFocus != null && !activeFocusCommitted)
            {
                activeFocus.Cancel?.Invoke();
            }
            ReleaseMonsterActiveFocusPresentation(clearQueue || cancelled);
            activeFocus = null;
            activeFocusElapsed = 0f;
            activeFocusResolvedDuration = 0f;
            activeFocusReadyWait = 0f;
            activeFocusCommitted = false;
            activeFocusPreset = default;
            if (clearQueue)
            {
                for (var index = 0; index < activeFocusQueue.Count; index++)
                {
                    activeFocusQueue[index].Cancel?.Invoke();
                }
                activeFocusQueue.Clear();
            }
            else if (!IsPaused)
            {
                BeginNextMonsterActiveFocus();
            }
        }

        public void CancelMonsterActiveFocus(UnitActor caster)
        {
            if (caster == null)
            {
                return;
            }

            if (activeFocus != null && activeFocus.Caster == caster)
            {
                CompleteMonsterActiveFocus(false, !activeFocusCommitted);
            }

            for (var index = activeFocusQueue.Count - 1; index >= 0; index--)
            {
                if (activeFocusQueue[index].Caster != caster)
                {
                    continue;
                }
                activeFocusQueue[index].Cancel?.Invoke();
                activeFocusQueue.RemoveAt(index);
            }
        }

        public void SetMonsterActiveFocusCamera(IMonsterActiveFocusCamera camera)
        {
            activeFocusCamera = camera;
        }

        public void ClearMonsterActiveFocusCamera(IMonsterActiveFocusCamera camera)
        {
            if (ReferenceEquals(activeFocusCamera, camera))
            {
                activeFocusCamera = null;
            }
        }

        public float GetMonsterActiveFocusTimeScale(UnitActor source)
        {
            return activeFocusVisible && activeFocus != null && source != null && source != activeFocus.Caster
                ? activeFocusPreset.OtherUnitTimeScale
                : 1f;
        }

        private bool HasMonsterActiveFocusRequest(UnitActor caster)
        {
            if (activeFocus != null && activeFocus.Caster == caster)
            {
                return true;
            }
            for (var index = 0; index < activeFocusQueue.Count; index++)
            {
                if (activeFocusQueue[index].Caster == caster)
                {
                    return true;
                }
            }
            return false;
        }

        public UnitActor FindNearestOpponent(UnitActor seeker, float maxDistance)
        {
            return FindOpponent(seeker, maxDistance, UnitTargetPriority.Nearest);
        }

        public UnitActor FindOpponent(UnitActor seeker, float maxDistance, UnitTargetPriority priority)
        {
            return FindOpponent(seeker, maxDistance, priority, 0f);
        }

        public UnitActor FindOpponent(
            UnitActor seeker,
            float maxDistance,
            UnitTargetPriority priority,
            float targetLoadPenalty)
        {
            if (seeker == null)
            {
                return null;
            }

            var maxDistanceSquared = float.IsPositiveInfinity(maxDistance) ? float.PositiveInfinity : maxDistance * maxDistance; // 제곱 거리로 비교
            var bestScore = float.PositiveInfinity;
            UnitActor best = null;
            for (var i = 0; i < units.Count; i++)
            {
                var candidate = units[i];
                if (candidate == null || candidate == seeker || !candidate.IsAlive ||
                    !candidate.IsCombatReady || candidate.Team == seeker.Team)
                {
                    continue;
                }

                var offset = candidate.transform.position - seeker.transform.position;
                offset.y = 0f;
                var distanceSquared = offset.sqrMagnitude;
                if (distanceSquared > maxDistanceSquared)
                {
                    continue;
                }

                var score = priority switch
                {
                    UnitTargetPriority.LowestHealth => ResolveHealthRatio(candidate) * 10000f + distanceSquared,
                    UnitTargetPriority.RangedFirst => (candidate.IsRanged ? 0f : 1000000f) + distanceSquared,
                    _ => distanceSquared
                };
                score += CountAlliedAttackers(seeker, candidate) * Mathf.Max(0f, targetLoadPenalty) * 9f;
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                best = candidate;
            }

            return best;
        }

        private int CountAlliedAttackers(UnitActor seeker, UnitActor target)
        {
            var count = 0;
            for (var index = 0; index < units.Count; index++)
            {
                var ally = units[index];
                if (ally != null && ally != seeker && ally.IsAlive && ally.IsCombatReady &&
                    ally.Team == seeker.Team && ally.Target == target)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountAlive(UnitTeam team)
        {
            var count = 0;
            for (var i = 0; i < units.Count; i++)
            {
                if (units[i] != null && units[i].IsAlive && units[i].Team == team)
                {
                    count++;
                }
            }

            return count;
        }

        public void Attack(UnitActor source, UnitActor target, UnitStatsSnapshot stats)
        {
            if (source == null || target == null || !source.IsAlive || !target.IsAlive || !target.IsCombatReady)
            {
                return;
            }

            if (stats.ranged && projectilePrefab != null && poolScope != null) // 원거리 유닛은 투사체 우선
            {
                var instance = poolScope.Rent(projectilePrefab, source.transform.position + Vector3.up * 0.45f, Quaternion.identity, transform);
                var projectile = instance == null ? null : instance.GetComponent<ProjectileActor>();
                if (projectile != null)
                {
                    projectile.Launch(this, source, target, stats.damage, Mathf.Max(1f, stats.projectileSpeed));
                    return;
                }

                if (instance != null)
                {
                    poolScope.Return(instance);
                }
            }

            ApplyMonsterDamage(source, target.Health, stats.damage); // 투사체 실패 시에도 공용 피해 계산 사용
        }

        public bool ExecuteMonsterAction(
            UnitActor source,
            IDamageable target,
            UnitStatsSnapshot stats,
            MonsterRuntimeAssetSet assetSet,
            MonsterAttackMarker marker,
            MonsterAnimationDriver animationDriver)
        {
            if (source == null || target == null || assetSet?.CombatProfile == null || marker == null)
            {
                return false;
            }

            var context = new MonsterActionExecutionContext(
                this,
                source,
                target,
                stats,
                assetSet,
                marker,
                animationDriver);
            var basicAttackProfile = assetSet.CombatProfile.Action?.BasicAttackProfile;
            var executed = basicAttackProfile != null
                ? basicAttackExecutor.Execute(context)
                : assetSet.CombatProfile.CombatType switch
            {
                MonsterCombatType.Melee => meleeExecutor.Execute(context),
                MonsterCombatType.Ranged => projectileExecutor.Execute(context),
                MonsterCombatType.Special => specialExecutor.Execute(context),
                _ => false
            };

            var feedback = marker.FeedbackOverride;
            if (feedback == null)
            {
                feedback = assetSet.CombatProfile.CombatType == MonsterCombatType.Special
                    ? assetSet.FeedbackProfile?.Special
                    : assetSet.FeedbackProfile?.AttackMarker;
            }

            if (basicAttackProfile == null && assetSet.CombatProfile.CombatType != MonsterCombatType.Ranged)
            {
                PlayMonsterFeedback(
                    feedback,
                    animationDriver,
                    marker.SocketOverride,
                    assetSet.BodyProfile?.VfxScale ?? 1f);
            }

            return executed;
        }

        public bool ApplyMonsterDamage(
            UnitActor source,
            IDamageable target,
            float amount,
            DamageFeedbackFlags feedbackFlags = DamageFeedbackFlags.None)
        {
            return ApplyMonsterDamageInternal(source, target, amount, feedbackFlags, true);
        }

        public bool ApplyMonsterSkillDamage(
            UnitActor source,
            IDamageable target,
            float amount,
            DamageFeedbackFlags feedbackFlags = DamageFeedbackFlags.None)
        {
            var skillRate = source == null ? 1f : Mathf.Max(0f, 1f + source.EffectiveStats.skillDamageRate);
            return ApplyMonsterDamageInternal(source, target, amount * skillRate, feedbackFlags, false);
        }

        private bool ApplyMonsterDamageInternal(
            UnitActor source,
            IDamageable target,
            float amount,
            DamageFeedbackFlags feedbackFlags,
            bool applyOutgoingPassive)
        {
            if (source == null || target == null || !source.IsAlive || !target.IsAlive || amount <= 0f)
            {
                return false;
            }

            var component = target as Component;
            var targetActor = component != null ? component.GetComponent<UnitActor>() : null;
            if (applyOutgoingPassive && source.SkillRuntime.WillEnhanceNextBasicHit)
            {
                feedbackFlags |= DamageFeedbackFlags.PassiveEnhancedNumber;
            }
            var resolvedAmount = applyOutgoingPassive
                ? amount * source.SkillRuntime.ResolveOutgoingDamageMultiplier(targetActor)
                : amount;
            if (targetActor != null && !targetActor.IsCombatReady)
            {
                return false;
            }

            if (targetActor != null)
            {
                resolvedAmount = CombatDamageCalculator.Calculate(
                    resolvedAmount,
                    source.EffectiveStats,
                    targetActor.EffectiveStats,
                    sharedStatConfig ?? CombatStatConfig.RuntimeDefault,
                    Random.value).Amount;
                resolvedAmount = targetActor.SkillRuntime.ResolveIncomingDamage(
                    resolvedAmount,
                    out var shieldAbsorbed);
                if (resolvedAmount <= 0f && shieldAbsorbed > 0f)
                {
                    if (applyOutgoingPassive)
                    {
                        source.SkillRuntime.NotifyBasicAttackHit(true, targetActor);
                    }
                    return true;
                }
            }

            float appliedDamage;
            var health = component != null ? component.GetComponent<HealthComponent>() : null;
            if (health != null)
            {
                var hitPoint = target.Position + Vector3.up * 0.4f;
                health.ApplyDamage(
                    new DamageRequest(source, resolvedAmount, hitPoint, false, feedbackFlags),
                    out appliedDamage);
            }
            else
            {
                appliedDamage = target.ReceiveDamage(source, resolvedAmount);
            }
            if (appliedDamage <= 0f)
            {
                return false;
            }

            if (applyOutgoingPassive && targetActor != null)
            {
                source.SkillRuntime.NotifyBasicAttackHit(true, targetActor);
                if (!targetActor.IsAlive)
                {
                    source.SkillRuntime.NotifyTargetDestroyed();
                }
            }

            if (targetActor == null)
            {
                feedbackPlayer?.PlayDamage(
                    target.Position,
                    appliedDamage,
                    FloatingNumberStyle.EnemyDamage,
                    target.GetHashCode());
            }

            return true;
        }

        public void CollectUnits(
            UnitTeam team,
            Vector3 center,
            float radius,
            int maxCount,
            List<UnitActor> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            var radiusSquared = Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
            maxCount = Mathf.Max(1, maxCount);
            for (var unitIndex = 0; unitIndex < units.Count; unitIndex++)
            {
                var candidate = units[unitIndex];
                if (candidate == null || !candidate.IsAlive || !candidate.IsCombatReady || candidate.Team != team)
                {
                    continue;
                }

                var offset = candidate.transform.position - center;
                offset.y = 0f;
                var distanceSquared = offset.sqrMagnitude;
                if (distanceSquared > radiusSquared)
                {
                    continue;
                }

                var insertIndex = 0;
                while (insertIndex < destination.Count)
                {
                    var existingOffset = destination[insertIndex].transform.position - center;
                    existingOffset.y = 0f;
                    if (distanceSquared < existingOffset.sqrMagnitude)
                    {
                        break;
                    }

                    insertIndex++;
                }

                destination.Insert(insertIndex, candidate);
                if (destination.Count > maxCount)
                {
                    destination.RemoveAt(destination.Count - 1);
                }
            }
        }

        public void CollectUnitsInFan(
            UnitTeam team,
            Vector3 origin,
            Vector3 forward,
            float range,
            float angle,
            int maxCount,
            List<UnitActor> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
            range = Mathf.Max(0.05f, range);
            var minimumDot = Mathf.Cos(Mathf.Clamp(angle, 5f, 180f) * 0.5f * Mathf.Deg2Rad);
            maxCount = Mathf.Max(1, maxCount);
            for (var index = 0; index < units.Count; index++)
            {
                var candidate = units[index];
                if (candidate == null || !candidate.IsAlive || !candidate.IsCombatReady || candidate.Team != team)
                {
                    continue;
                }

                var offset = candidate.transform.position - origin;
                offset.y = 0f;
                var distance = offset.magnitude;
                if (distance > range + candidate.BodyRadius ||
                    (distance > 0.001f && Vector3.Dot(forward, offset / distance) < minimumDot))
                {
                    continue;
                }

                InsertByDistance(destination, candidate, origin, maxCount);
            }
        }

        public void CollectUnitsInLine(
            UnitTeam team,
            Vector3 origin,
            Vector3 forward,
            float length,
            float width,
            int maxCount,
            List<UnitActor> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
            length = Mathf.Max(0.05f, length);
            var halfWidth = Mathf.Max(0.025f, width * 0.5f);
            maxCount = Mathf.Max(1, maxCount);
            for (var index = 0; index < units.Count; index++)
            {
                var candidate = units[index];
                if (candidate == null || !candidate.IsAlive || !candidate.IsCombatReady || candidate.Team != team)
                {
                    continue;
                }

                var offset = candidate.transform.position - origin;
                offset.y = 0f;
                var longitudinal = Vector3.Dot(offset, forward);
                var lateral = (offset - forward * longitudinal).magnitude;
                if (longitudinal < -candidate.BodyRadius ||
                    longitudinal > length + candidate.BodyRadius ||
                    lateral > halfWidth + candidate.BodyRadius)
                {
                    continue;
                }

                InsertByDistance(destination, candidate, origin, maxCount);
            }
        }

        public void ShowMonsterBasicAttackArea(
            MonsterBasicAttackProfile profile,
            UnitActor source,
            Vector3 origin,
            Vector3 forward,
            Vector3 primaryTarget,
            float attackRange)
        {
            if (!showMonsterBasicAttackHitAreas || profile == null || source == null)
            {
                return;
            }

            var color = source.Team == UnitTeam.Player
                ? new Color(0.1f, 0.9f, 1f, 0.72f)
                : new Color(1f, 0.25f, 0.18f, 0.72f);
            monsterBasicAttackHitAreas.RemoveAll(indicator => indicator == null);
            var indicator = MonsterAttackAreaIndicator.Create(
                transform,
                profile,
                origin,
                forward,
                primaryTarget,
                attackRange,
                color);
            if (indicator != null)
            {
                monsterBasicAttackHitAreas.Add(indicator);
            }
        }

        private void ClearMonsterBasicAttackHitAreas()
        {
            for (var index = 0; index < monsterBasicAttackHitAreas.Count; index++)
            {
                var indicator = monsterBasicAttackHitAreas[index];
                if (indicator == null)
                {
                    continue;
                }

                indicator.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(indicator.gameObject);
                }
                else
                {
                    DestroyImmediate(indicator.gameObject);
                }
            }

            monsterBasicAttackHitAreas.Clear();
        }

        private static void InsertByDistance(
            List<UnitActor> destination,
            UnitActor candidate,
            Vector3 origin,
            int maxCount)
        {
            var distanceSquared = (candidate.transform.position - origin).sqrMagnitude;
            var insertIndex = 0;
            while (insertIndex < destination.Count &&
                   (destination[insertIndex].transform.position - origin).sqrMagnitude <= distanceSquared)
            {
                insertIndex++;
            }

            destination.Insert(insertIndex, candidate);
            if (destination.Count > maxCount)
            {
                destination.RemoveAt(destination.Count - 1);
            }
        }

        public GameObject RentMonsterObject(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
        {
            return poolScope?.Rent(prefab, position, rotation, parent ?? transform);
        }

        public void ReturnMonsterObject(GameObject instance)
        {
            poolScope?.Return(instance);
        }

        public void PlayMonsterFeedback(
            MonsterFeedbackCue cue,
            MonsterAnimationDriver animationDriver,
            string socketOverride,
            float bodyVfxScale = 1f)
        {
            if (cue == null || !cue.HasAnyFeedback)
            {
                return;
            }

            var socket = animationDriver != null
                ? animationDriver.ResolveSocket(socketOverride)
                : null;
            var position = socket != null ? socket.position : transform.position;
            var rotation = socket != null ? socket.rotation : Quaternion.identity;
            PlayMonsterFeedbackAt(cue, position, rotation, bodyVfxScale);
        }

        public void PlayMonsterFeedbackAt(
            MonsterFeedbackCue cue,
            Vector3 position,
            Quaternion rotation,
            float vfxScale = 1f)
        {
            PlayMonsterFeedbackAt(cue, position, rotation, vfxScale, 0f);
        }

        public void PlayMonsterFeedbackAt(
            MonsterFeedbackCue cue,
            Vector3 position,
            Quaternion rotation,
            float vfxScale,
            float vfxLifetimeOverride)
        {
            var instance = SpawnMonsterFeedbackVfx(cue, position, rotation, null, vfxScale);
            if (instance == null) return;
            var lifetime = vfxLifetimeOverride > 0f ? vfxLifetimeOverride : cue.VfxLifetime;
            StartCoroutine(ReturnMonsterObjectAfter(instance, lifetime));
        }

        public GameObject SpawnMonsterFeedbackVfx(
            MonsterFeedbackCue cue,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            float vfxScale = 1f)
        {
            if (cue == null || !cue.HasAnyFeedback) return null;
            position += rotation * cue.LocalPosition;
            rotation *= cue.LocalRotation;
            PlayMonsterSfx(cue.Sfx, position);
            if (cue.VfxPrefab == null) return null;

            var frame = Time.frameCount;
            if (monsterVfxFrame != frame)
            {
                monsterVfxFrame = frame;
                monsterVfxCount = 0;
            }
            if (monsterVfxCount >= Mathf.Max(1, maxMonsterVfxPerFrame)) return null;

            monsterVfxCount++;
            var instance = RentMonsterObject(cue.VfxPrefab, position, rotation, parent);
            if (instance == null) return null;
            var scale = cue.Scale * Mathf.Max(0.01f, vfxScale);
            instance.transform.localScale = cue.VfxPrefab.transform.localScale * scale;
            MonsterBasicAttackVfxPlayback.RestartAtOffset(instance, 0f, playbackSpeed: 1f);
            return instance;
        }

        public GameObject SpawnMonsterActiveVfx(
            MonsterFeedbackCue cue,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            float vfxScale = 1f)
        {
            if (cue == null || !cue.HasAnyFeedback) return null;
            position += rotation * cue.LocalPosition;
            rotation *= cue.LocalRotation;
            PlayMonsterSfx(cue.Sfx, position);
            if (cue.VfxPrefab == null) return null;

            var frame = Time.frameCount;
            if (monsterActiveVfxFrame != frame)
            {
                monsterActiveVfxFrame = frame;
                monsterActiveVfxCount = 0;
            }
            if (monsterActiveVfxCount >= Mathf.Max(1, maxMonsterActiveVfxPerFrame)) return null;

            monsterActiveVfxCount++;
            var instance = RentMonsterObject(cue.VfxPrefab, position, rotation, parent);
            if (instance == null) return null;
            var scale = cue.Scale * Mathf.Max(0.01f, vfxScale);
            instance.transform.localScale = cue.VfxPrefab.transform.localScale * scale;
            MonsterBasicAttackVfxPlayback.RestartAtOffset(instance, 0f, playbackSpeed: 1f);
            return instance;
        }

        public GameObject SpawnBasicAttackVfx(
            MonsterBasicAttackVfxBinding binding,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            float bodyVfxScale = 1f)
        {
            if (binding == null || !binding.IsAssigned)
            {
                return null;
            }

            var frame = Time.frameCount;
            if (monsterVfxFrame != frame)
            {
                monsterVfxFrame = frame;
                monsterVfxCount = 0;
            }
            if (monsterVfxCount >= Mathf.Max(1, maxMonsterVfxPerFrame))
            {
                return null;
            }

            monsterVfxCount++;
            position += rotation * binding.LocalPosition;
            rotation *= binding.LocalRotation;
            var instance = RentMonsterObject(binding.Prefab, position, rotation, parent);
            if (instance != null)
            {
                MonsterBasicAttackVfxPlayback.ApplyInstanceScale(
                    instance,
                    binding.Prefab.transform.localScale *
                    binding.Scale * Mathf.Max(0.01f, bodyVfxScale));
                MonsterBasicAttackVfxPlayback.RestartAtOffset(
                    instance,
                    binding.PlaybackOffset,
                    playbackSpeed: binding.PlaybackSpeed);
            }
            return instance;
        }

        public void ScheduleMonsterObjectReturn(GameObject instance, float delay)
        {
            if (instance != null)
            {
                StartCoroutine(ReturnMonsterObjectAfter(instance, delay));
            }
        }

        public bool WillPlayBasicAttackFeelTargetMotion(
            BasicAttackFeelCue cue,
            GameObject target,
            float intensity = 1f)
        {
            if (cue == null || !cue.HasFeel || target == null || poolScope == null)
            {
                return false;
            }

            RefreshMonsterFeelFrameBudget();
            if (monsterFeelCount >= Mathf.Max(1, maxMonsterFeelPerFrame))
            {
                return false;
            }

            var runtime = cue.Prefab.GetComponent(typeof(IBasicAttackFeelRuntime)) as IBasicAttackFeelRuntime;
            return runtime?.IsBasicAttackFeelConfigured == true &&
                   runtime.HasBasicAttackTargetMotion(intensity);
        }

        public void PlayBasicAttackFeelAt(
            BasicAttackFeelCue cue,
            Vector3 position,
            Quaternion rotation,
            float bodyScale = 1f,
            GameObject target = null,
            float intensity = 1f)
        {
            if (cue == null || !cue.HasFeel)
            {
                return;
            }

            RefreshMonsterFeelFrameBudget();

            if (monsterFeelCount >= Mathf.Max(1, maxMonsterFeelPerFrame))
            {
                return;
            }

            monsterFeelCount++;
            position += rotation * cue.LocalPosition;
            rotation *= cue.LocalRotation;
            var instance = RentMonsterObject(cue.Prefab, position, rotation);
            if (instance == null)
            {
                return;
            }

            instance.transform.localScale = cue.Prefab.transform.localScale *
                cue.Scale * Mathf.Max(0.01f, bodyScale);
            PlayBasicAttackFeelRuntime(instance, target, intensity);
            StartCoroutine(ReturnMonsterObjectAfter(instance, cue.Lifetime));
        }

        private void RefreshMonsterFeelFrameBudget()
        {
            var frame = Time.frameCount;
            if (monsterFeelFrame == frame)
            {
                return;
            }
            monsterFeelFrame = frame;
            monsterFeelCount = 0;
        }

        public void PlayBasicAttackFeelRuntime(
            GameObject instance,
            GameObject target = null,
            float intensity = 1f)
        {
            if (instance == null)
            {
                return;
            }

            var feelRuntime = instance.GetComponent(typeof(IBasicAttackFeelRuntime)) as IBasicAttackFeelRuntime;
            feelRuntime?.PlayBasicAttackFeel(
                instance.transform.position,
                target,
                intensity,
                BasicAttackFeelPlaybackOptions.None); // 실전 전역 카메라·히트스탑은 공용 전투 계층만 소유
        }

        public void PlayMonsterSfx(SfxCue cue, Vector3 position)
        {
            feedbackPlayer?.PlayMonsterCue(cue, position);
        }

        // 08.07 안건준 추가 - UnitActor가 아닌 대상(예: 수호자의 탑 방어 건물 같은 IDamageable)을 공격할 때 쓰는 진입점.
        // 원거리 유닛이면 기존과 동일하게 투사체를 쏘고 도착 시 피해+숫자를 표시하며, 근접이면 즉시 피해+숫자를 표시한다.
        // 기존 Attack(UnitActor, UnitActor, ...)와 ProjectileActor.Launch()는 전혀 건드리지 않아 일반 전투에는 영향이 없다.
        public void AttackDamageable(UnitActor source, IDamageable target, UnitStatsSnapshot stats)
        {
            if (source == null || target == null || !source.IsAlive || !target.IsAlive)
            {
                return;
            }

            if (stats.ranged && projectilePrefab != null && poolScope != null) // 원거리 유닛은 투사체 우선
            {
                var instance = poolScope.Rent(projectilePrefab, source.transform.position + Vector3.up * 0.45f, Quaternion.identity, transform);
                var projectile = instance == null ? null : instance.GetComponent<ProjectileActor>();
                if (projectile != null)
                {
                    projectile.LaunchAtDamageable(this, source, target, stats.damage, Mathf.Max(1f, stats.projectileSpeed), feedbackPlayer);
                    return;
                }

                if (instance != null)
                {
                    poolScope.Return(instance);
                }
            }

            // 08.07 안건준 수정 - 적을 공격할 때(HealthComponent.Damaged의 report.AppliedDamage)와 동일하게,
            // 화면 숫자는 요청한 공격력이 아니라 "실제로 깎인 체력"을 표시하도록 통일했다.
            var appliedDamage = target.ReceiveDamage(source, stats.damage); // 투사체 실패 또는 근접이면 즉시 피해
            if (appliedDamage > 0f)
            {
                feedbackPlayer?.PlayDamage(target.Position, appliedDamage, FloatingNumberStyle.EnemyDamage, target.GetHashCode());
            }
        }

        public void NotifyDeath(UnitActor unit, float delay = 0.38f)
        {
            if (unit != null)
            {
                StartCoroutine(ReturnDeadUnit(unit, Mathf.Max(0.05f, delay))); // Death Clip 뒤 풀 반환
            }
        }

        public void ReturnProjectile(GameObject projectile)
        {
            poolScope?.Return(projectile);
        }

        public void PlayClimax(Vector3 position, CombatClimaxStrength strength)
        {
            feedbackPlayer?.PlayClimax(position, strength);
        }

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused)
            {
                return;
            }
            IsPaused = paused;
            if (paused)
            {
                CompleteMonsterActiveFocus(true, true);
            }
        }

        public void Clear()
        {
            StopAllCoroutines();
            IsPaused = false;
            CompleteMonsterActiveFocus(true, true);
            ClearMonsterBasicAttackHitAreas();
            var buffer = new List<UnitActor>(units); // 순회 중 원본 목록 분리
            units.Clear();
            foreach (var unit in buffer)
            {
                if (unit == null)
                {
                    continue;
                }

                unit.Shutdown();
                poolScope?.Return(unit.gameObject);
            }

            poolScope?.ReturnAll(); // 남은 투사체·VFX까지 회수
        }

        private IEnumerator ReturnDeadUnit(UnitActor unit, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (unit == null)
            {
                yield break;
            }

            units.Remove(unit);
            unit.Shutdown();
            poolScope?.Return(unit.gameObject);
        }

        private IEnumerator ReturnMonsterObjectAfter(GameObject instance, float delay)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, delay));
            ReturnMonsterObject(instance);
        }

        private static float ResolveHealthRatio(UnitActor actor)
        {
            return actor?.Health == null || actor.Health.MaxHealth <= 0f
                ? 1f
                : Mathf.Clamp01(actor.Health.CurrentHealth / actor.Health.MaxHealth);
        }

        private sealed class ActiveFocusRequest
        {
            public const float MaxReadyWait = 1.5f;

            public ActiveFocusRequest(
                UnitActor caster,
                MonsterActiveSkill skill,
                System.Func<UnitActor> targetResolver,
                System.Func<bool> canArm,
                System.Action begin,
                System.Func<bool> commit,
                System.Action cancel,
                System.Func<bool> commitSignal,
                float commitDelay,
                float totalDuration,
                float readyTime,
                int partySlotIndex,
                long sequence)
            {
                Caster = caster;
                Skill = skill;
                TargetResolver = targetResolver;
                CanArm = canArm;
                Commit = commit;
                Begin = begin;
                Cancel = cancel;
                CommitSignal = commitSignal;
                CommitDelay = Mathf.Max(0.05f, commitDelay);
                Duration = Mathf.Max(CommitDelay + 0.08f, totalDuration);
                ReadyTime = readyTime;
                PartySlotIndex = partySlotIndex;
                Sequence = sequence;
            }

            public UnitActor Caster { get; }
            public MonsterActiveSkill Skill { get; }
            public System.Func<UnitActor> TargetResolver { get; }
            public System.Func<bool> CanArm { get; }
            public System.Func<bool> Commit { get; }
            public System.Action Begin { get; }
            public System.Action Cancel { get; }
            public System.Func<bool> CommitSignal { get; }
            public float CommitDelay { get; }
            public float Duration { get; }
            public float ReadyTime { get; }
            public int PartySlotIndex { get; }
            public long Sequence { get; }
            public bool Armed { get; set; }

            public static int Compare(ActiveFocusRequest left, ActiveFocusRequest right)
            {
                if (ReferenceEquals(left, right))
                {
                    return 0;
                }
                if (left == null)
                {
                    return 1;
                }
                if (right == null)
                {
                    return -1;
                }

                if (Mathf.Abs(left.ReadyTime - right.ReadyTime) > 0.0001f)
                {
                    return left.ReadyTime.CompareTo(right.ReadyTime);
                }
                var slotComparison = left.PartySlotIndex.CompareTo(right.PartySlotIndex);
                return slotComparison != 0
                    ? slotComparison
                    : left.Sequence.CompareTo(right.Sequence);
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(ScenePoolScope pool, CombatFeedbackPlayer feedback, GameObject projectile)
        {
            poolScope = pool;
            feedbackPlayer = feedback;
            projectilePrefab = projectile;
        }
#endif
    }
}
