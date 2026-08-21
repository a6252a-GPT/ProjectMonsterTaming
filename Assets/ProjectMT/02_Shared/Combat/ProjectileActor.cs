using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    [DisallowMultipleComponent]
    public sealed class ProjectileActor : MonoBehaviour // 풀링 원거리 투사체
    {
        private CombatWorld world; // 반환할 전투 영역
        private UnitActor source; // 피해 원인 유닛
        private UnitActor target; // 추적 대상(일반 유닛)
        // 08.07 안건준 추가 - 건물 등 UnitActor가 아닌 대상(IDamageable)을 추적하는 경우에 사용.
        // target(UnitActor)과 damageableTarget은 항상 둘 중 하나만 설정된다.
        private IDamageable damageableTarget;
        private ICombatFeedbackPlayer feedbackForDamageable; // damageableTarget 명중 시 데미지 숫자를 직접 띄워줄 연출 계약
        private float damage; // 명중 피해량
        private float speed; // 이동 속도
        private bool running; // 중복 반환 방지

        public void Launch(CombatWorld combatWorld, UnitActor owner, UnitActor targetUnit, float amount, float moveSpeed)
        {
            world = combatWorld;
            source = owner;
            target = targetUnit;
            damageableTarget = null;
            feedbackForDamageable = null;
            damage = Mathf.Max(0f, amount);
            speed = Mathf.Max(1f, moveSpeed);
            running = true;
        }

        // 08.07 안건준 추가 - 건물(수호자의 탑 방어 건물 등) 같은 IDamageable 대상을 추적하는 투사체 발사.
        // 원거리 아군/적이 건물을 공격할 때도 총알(투사체) 연출이 나오도록 하기 위함. 기존 Launch()는 그대로 두고
        // 별도 진입점만 추가했으므로 일반 유닛 간 전투에는 영향이 없다.
        public void LaunchAtDamageable(
            CombatWorld combatWorld,
            UnitActor owner,
            IDamageable targetDamageable,
            float amount,
            float moveSpeed,
            ICombatFeedbackPlayer feedback)
        {
            world = combatWorld;
            source = owner;
            target = null;
            damageableTarget = targetDamageable;
            feedbackForDamageable = feedback;
            damage = Mathf.Max(0f, amount);
            speed = Mathf.Max(1f, moveSpeed);
            running = true;
        }

        private void Update()
        {
            if (!running || world == null)
            {
                ReturnToPool();
                return;
            }

            if (target != null)
            {
                if (!target.IsAlive)
                {
                    ReturnToPool();
                    return;
                }

                var targetPosition = target.transform.position + Vector3.up * 0.4f;
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime); // 대상 위치 추적
                if ((transform.position - targetPosition).sqrMagnitude <= 0.04f)
                {
                    world.ApplyMonsterDamage(source, target.Health, damage); // 도착 시 공용 피해 계산 후 한 번 적용
                    ReturnToPool();
                }

                return;
            }

            if (damageableTarget != null)
            {
                if (!damageableTarget.IsAlive)
                {
                    ReturnToPool();
                    return;
                }

                var targetPosition = damageableTarget.Position + Vector3.up * 0.4f;
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
                if ((transform.position - targetPosition).sqrMagnitude <= 0.04f)
                {
                    // 08.07 안건준 수정 - 적을 공격할 때와 동일하게 "실제로 깎인 체력"을 표시하도록,
                    // 요청 피해량(damage)이 아니라 ReceiveDamage가 반환하는 적용된 피해량을 사용한다.
                    var appliedDamage = damageableTarget.ReceiveDamage(source, damage); // 도착 시 한 번 피해
                    // 08.07 안건준 추가 - IDamageable 대상은 UnitActor.Damaged 구독이 없어 자동으로 숫자가 뜨지 않으므로 직접 표시
                    if (appliedDamage > 0f)
                    {
                        feedbackForDamageable?.PlayDamage(targetPosition, appliedDamage, FloatingNumberStyle.EnemyDamage, damageableTarget.GetHashCode());
                    }
                    ReturnToPool();
                }

                return;
            }

            ReturnToPool(); // 두 대상 모두 비어있으면(비정상 상태) 안전하게 반환
        }

        private void OnDisable()
        {
            running = false;
            world = null;
            source = null;
            target = null;
            damageableTarget = null;
            feedbackForDamageable = null;
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
