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
    public sealed partial class CombatWorld : MonoBehaviour // 한 전투의 유닛·공격 조율
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
        private float activeFocusSlowStartedAt = -1f;
        private float activeFocusCameraReleaseAt = -1f;
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
        private float unitEmissionBrightnessScale = 1f; // 콘텐츠별 전투 유닛 자체발광 보정
        private float monsterVfxBrightnessScale = 1f; // 콘텐츠별 전투 VFX 밝기 보정
        private static CombatStatConfig sharedStatConfig;

        public ICombatFeedbackPlayer Feedback => feedbackPlayer;
        public bool IsPaused { get; private set; }
        public UnitActor ActiveFocusCaster => activeFocus?.Caster;
        public int ActiveFocusQueueCount => activeFocusQueue.Count;
        public bool IsMonsterActiveFocusVisible => activeFocusVisible;
        public float UnitEmissionBrightnessScale => unitEmissionBrightnessScale;
        public float MonsterVfxBrightnessScale => monsterVfxBrightnessScale;
        public static bool MonsterBasicAttackHitAreasVisible => showMonsterBasicAttackHitAreas;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDebugSettings()
        {
            showMonsterBasicAttackHitAreas = false; // Play 시작마다 기본 OFF
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
            if (unit == null)
            {
                return;
            }

            if (!units.Contains(unit))
            {
                units.Add(unit);
            }

            unit.VisualFeedback?.SetEmissionBrightnessScale(unitEmissionBrightnessScale);
        }

        public void Unregister(UnitActor unit)
        {
            if (unit != null)
            {
                unit.VisualFeedback?.SetEmissionBrightnessScale(1f); // Pool이 다른 콘텐츠에서 재사용될 때 누수 방지
                CancelMonsterActiveFocus(unit);
                feedbackPlayer?.UntrackUnit(unit);
                units.Remove(unit);
            }
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
                System.Func<bool> completionSignal,
                System.Func<float> progressSignal,
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
                CompletionSignal = completionSignal;
                ProgressSignal = progressSignal;
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
            public System.Func<bool> CompletionSignal { get; }
            public System.Func<float> ProgressSignal { get; }
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
