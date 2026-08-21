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

        private readonly List<UnitActor> units = new List<UnitActor>(); // 현재 등록 유닛
        private readonly MeleeAttackExecutor meleeExecutor = new MeleeAttackExecutor();
        private readonly ProjectileAttackExecutor projectileExecutor = new ProjectileAttackExecutor();
        private readonly SpecialActionExecutor specialExecutor = new SpecialActionExecutor();
        private int monsterVfxFrame = -1;
        private int monsterVfxCount;
        private static CombatStatConfig sharedStatConfig;

        public ICombatFeedbackPlayer Feedback => feedbackPlayer;
        public bool IsPaused { get; private set; }

        public static void ConfigureSharedStatRules(CombatStatConfig config)
        {
            sharedStatConfig = config ?? CombatStatConfig.RuntimeDefault;
        }

        private void Update()
        {
            if (IsPaused)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
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

                unit.Tick(deltaTime); // 중앙 Tick으로 전투 일시정지 통제
            }
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
                units.Remove(unit);
            }
        }

        public UnitActor FindNearestOpponent(UnitActor seeker, float maxDistance)
        {
            if (seeker == null)
            {
                return null;
            }

            var maxDistanceSquared = float.IsPositiveInfinity(maxDistance) ? float.PositiveInfinity : maxDistance * maxDistance; // 제곱 거리로 비교
            var nearestDistanceSquared = maxDistanceSquared;
            UnitActor nearest = null;
            for (var i = 0; i < units.Count; i++)
            {
                var candidate = units[i];
                if (candidate == null || candidate == seeker || !candidate.IsAlive || candidate.Team == seeker.Team)
                {
                    continue;
                }

                var offset = candidate.transform.position - seeker.transform.position;
                offset.y = 0f;
                var distanceSquared = offset.sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearest = candidate;
                }
            }

            return nearest;
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
            var executed = assetSet.CombatProfile.CombatType switch
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

            if (assetSet.CombatProfile.CombatType != MonsterCombatType.Ranged)
            {
                PlayMonsterFeedback(
                    feedback,
                    animationDriver,
                    marker.SocketOverride,
                    assetSet.BodyProfile?.VfxScale ?? 1f);
            }

            return executed;
        }

        public bool ApplyMonsterDamage(UnitActor source, IDamageable target, float amount)
        {
            if (source == null || target == null || !source.IsAlive || !target.IsAlive || amount <= 0f)
            {
                return false;
            }

            var resolvedAmount = amount;
            var component = target as Component;
            var targetActor = component != null ? component.GetComponent<UnitActor>() : null;
            if (targetActor != null)
            {
                resolvedAmount = CombatDamageCalculator.Calculate(
                    amount,
                    source.EffectiveStats,
                    targetActor.EffectiveStats,
                    sharedStatConfig ?? CombatStatConfig.RuntimeDefault,
                    Random.value).Amount;
            }

            var appliedDamage = target.ReceiveDamage(source, resolvedAmount);
            if (appliedDamage <= 0f)
            {
                return false;
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
                if (candidate == null || !candidate.IsAlive || candidate.Team != team)
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
            if (cue == null || !cue.HasAnyFeedback)
            {
                return;
            }

            position += rotation * cue.LocalPosition;
            rotation *= cue.LocalRotation;
            PlayMonsterSfx(cue.Sfx, position);

            if (cue.VfxPrefab == null)
            {
                return;
            }

            var frame = Time.frameCount;
            if (monsterVfxFrame != frame)
            {
                monsterVfxFrame = frame;
                monsterVfxCount = 0;
            }

            if (monsterVfxCount >= Mathf.Max(1, maxMonsterVfxPerFrame))
            {
                return;
            }

            monsterVfxCount++;
            var instance = RentMonsterObject(cue.VfxPrefab, position, rotation);
            if (instance == null)
            {
                return;
            }

            var scale = cue.Scale * Mathf.Max(0.01f, vfxScale);
            instance.transform.localScale = cue.VfxPrefab.transform.localScale * scale;
            StartCoroutine(ReturnMonsterObjectAfter(instance, cue.VfxLifetime));
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
            IsPaused = paused;
        }

        public void Clear()
        {
            StopAllCoroutines();
            IsPaused = false;
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
