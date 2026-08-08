using System.Collections;
using System.Collections.Generic;
using ProjectMT.Shared.Pooling;
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

        private readonly List<UnitActor> units = new List<UnitActor>(); // 현재 등록 유닛

        public ICombatFeedbackPlayer Feedback => feedbackPlayer;
        public bool IsPaused { get; private set; }

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
            if (poolScope == null || prefab == null)
            {
                return null;
            }

            var instance = poolScope.Rent(prefab, position, rotation, transform); // 풀에서 유닛 대여
            var actor = instance == null ? null : instance.GetComponent<UnitActor>();
            if (actor == null)
            {
                Debug.LogError($"Unit prefab has no UnitActor: {prefab.name}");
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

            target.Health.ApplyDamage(new DamageRequest(source, stats.damage, target.transform.position + Vector3.up * 0.4f)); // 투사체 실패 시 즉시 피해
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

        public void NotifyDeath(UnitActor unit)
        {
            if (unit != null)
            {
                StartCoroutine(ReturnDeadUnit(unit, 0.38f)); // 사망 연출 뒤 풀 반환
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
