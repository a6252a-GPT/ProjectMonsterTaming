using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    [DisallowMultipleComponent]
    public sealed class ProjectileActor : MonoBehaviour // 풀링 원거리 투사체
    {
        private CombatWorld world; // 반환할 전투 영역
        private UnitActor source; // 피해 원인 유닛
        private UnitActor target; // 추적 대상
        private float damage; // 명중 피해량
        private float speed; // 이동 속도
        private bool running; // 중복 반환 방지

        public void Launch(CombatWorld combatWorld, UnitActor owner, UnitActor targetUnit, float amount, float moveSpeed)
        {
            world = combatWorld;
            source = owner;
            target = targetUnit;
            damage = Mathf.Max(0f, amount);
            speed = Mathf.Max(1f, moveSpeed);
            running = true;
        }

        private void Update()
        {
            if (!running || world == null || target == null || !target.IsAlive)
            {
                ReturnToPool();
                return;
            }

            var targetPosition = target.transform.position + Vector3.up * 0.4f;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime); // 대상 위치 추적
            if ((transform.position - targetPosition).sqrMagnitude <= 0.04f)
            {
                target.Health.ApplyDamage(new DamageRequest(source, damage, targetPosition)); // 도착 시 한 번 피해
                ReturnToPool();
            }
        }

        private void OnDisable()
        {
            running = false;
            world = null;
            source = null;
            target = null;
        }

        private void ReturnToPool()
        {
            if (!running)
            {
                return;
            }

            running = false;
            var owner = world;
            world = null;
            owner?.ReturnProjectile(gameObject); // 파괴 대신 풀 반환
        }
    }
}
