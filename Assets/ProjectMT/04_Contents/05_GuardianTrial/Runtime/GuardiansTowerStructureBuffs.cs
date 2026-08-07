using System;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.GuardianTrial
{
    // 08.07 안건준 추가 - 방어 건물(기둥)이 살아있는 동안 적에게 주는 버프와, 부서졌을 때 해제 알림을 전담하는
    // 일반 C# 클래스(MonoBehaviour 아님). GuardiansTowerController가 판마다 새로 만들어 소유하고
    // 매 프레임 Tick()을 호출해준다. 이 클래스는 오직 수호자의 탑 전용이며 다른 던전 코드는 건드리지 않는다.
    //
    // 버프 4종류:
    //  1번 건물(Defense)     : 08.07 안건준 수정 - "적 캐릭터" 방어력 *3 → 받는 피해가 1/3이 되도록
    //                          피해의 2/3(=1 - 1/3)을 즉시 환불해 흉내낸다. (이전: 300% 상승 ≈ 4배 → 환불 3/4)
    //  2번 건물(Health)      : 08.07 안건준 수정 - "적 캐릭터" 최대 체력 *3. (이전: 300% 상승 = 4배)
    //  3번 건물(Regen)       : "적 캐릭터"가 1초마다 최대 체력의 30% 회복.
    //  4번 건물(AttackBoost) : 08.07 안건준 수정 - "살아있는 동안 적 증원" 방식을 폐지하고, 이 건물이
    //                          "파괴되는 순간" 1회 "아군"에게 공격력 2배 버프를 발동하는 방식으로 변경.
    //                          실제 배율 적용(UnitActor.SetDamageMultiplier)은 아군 목록을 가진
    //                          GuardiansTowerController만 할 수 있어서, 이 클래스는 파괴 시점에
    //                          onAllyAttackBuffTriggered 콜백만 호출해 알려주고 실제 적용은 컨트롤러가 담당한다.
    // 1~3번 건물이 부서지면 그 버프만 즉시 해제되고, 화면 중앙 알림 콜백이 호출된다.
    public sealed class GuardiansTowerStructureBuffs
    {
        // 08.07 안건준 수정 - 방어력 *3 → 실제 피해 1/3만 남기므로 받은 피해의 2/3을 즉시 환불
        private const float DefenseDamageRefundRatio = 2f / 3f;
        // 08.07 안건준 수정 - 2번 건물: 적 최대 체력 *3 (이전 300% 상승=4배에서 변경)
        private const float EnemyHealthMultiplier = 3f;
        private const float RegenRatioPerTick = 0.30f; // 3번 건물: 1초마다 최대 체력의 30% 회복
        private const float RegenTickInterval = 1f;

        private sealed class EnemyBuffState
        {
            public HealthComponent Health; // 체력·방어력 버프 적용 대상
            public float BaseMaxHealth; // 체력 버프 적용 전 원래 최대 체력(적마다 다를 수 있어 개별 저장)
            public float PreviousHealth; // 직전 프레임 체력(방어력 버프 피해 감지용)
        }

        private readonly GuardiansTowerStructure[] structures;
        private readonly Action<string> showNotification; // 화면 중앙 알림 콜백 (예: 컨트롤러의 ShowCenterNotification)
        private readonly Action onAllyAttackBuffTriggered; // 08.07 안건준 추가 - 4번 건물 파괴 시 아군 공격력 버프 적용 콜백
        private readonly Dictionary<HealthComponent, EnemyBuffState> enemyStates = new Dictionary<HealthComponent, EnemyBuffState>();

        private bool defenseBuffActive;
        private bool healthBuffActive;
        private bool regenBuffActive;
        private float regenTimer;

        public GuardiansTowerStructureBuffs(
            GuardiansTowerStructure[] structureList,
            Action<string> notificationCallback,
            Action allyAttackBuffCallback = null)
        {
            structures = structureList ?? Array.Empty<GuardiansTowerStructure>();
            showNotification = notificationCallback;
            onAllyAttackBuffTriggered = allyAttackBuffCallback; // 08.07 안건준 추가
        }

        // 판 시작마다 호출: 건물들의 Initialize() 이후에 불러야 한다(살아있는 건물 판정을 새로 해야 하므로).
        public void Reset()
        {
            enemyStates.Clear();
            regenTimer = 0f;
            defenseBuffActive = HasAliveStructure(GuardiansTowerStructureRole.Defense);
            healthBuffActive = HasAliveStructure(GuardiansTowerStructureRole.Health);
            regenBuffActive = HasAliveStructure(GuardiansTowerStructureRole.Regen);

            foreach (var structure in structures)
            {
                if (structure != null)
                {
                    structure.Died += HandleStructureDied;
                }
            }
        }

        public void Shutdown()
        {
            foreach (var structure in structures)
            {
                if (structure != null)
                {
                    structure.Died -= HandleStructureDied;
                }
            }

            enemyStates.Clear();
        }

        // 새로 스폰된 적 등록(증원으로 추가 소환된 적 포함): 켜져 있는 체력 버프를 즉시 적용한다.
        // 건물·아군에는 절대 호출하지 않는다.
        public void RegisterEnemy(HealthComponent enemyHealth)
        {
            if (enemyHealth == null)
            {
                return;
            }

            var baseMax = enemyHealth.MaxHealth; // 배율 적용 전 원래 값을 먼저 저장
            if (healthBuffActive)
            {
                enemyHealth.SetMaxHealth(baseMax * EnemyHealthMultiplier, keepCurrentRatio: false); // 새로 스폰되므로 가득 채워 시작
            }

            enemyStates[enemyHealth] = new EnemyBuffState
            {
                Health = enemyHealth,
                BaseMaxHealth = baseMax,
                PreviousHealth = enemyHealth.CurrentHealth
            };
        }

        // 매 프레임 호출: 방어력 환불(피해 감지) + 체력회복 틱 처리.
        public void Tick(float deltaTime)
        {
            if (defenseBuffActive)
            {
                foreach (var pair in enemyStates)
                {
                    var enemyHealth = pair.Key;
                    if (enemyHealth == null || !enemyHealth.IsAlive)
                    {
                        continue;
                    }

                    var state = pair.Value;
                    if (enemyHealth.CurrentHealth < state.PreviousHealth)
                    {
                        var lost = state.PreviousHealth - enemyHealth.CurrentHealth;
                        enemyHealth.Heal(lost * DefenseDamageRefundRatio); // 받은 피해의 상당 부분을 즉시 환불
                    }
                }
            }

            if (regenBuffActive)
            {
                regenTimer += deltaTime;
                if (regenTimer >= RegenTickInterval)
                {
                    regenTimer -= RegenTickInterval;
                    foreach (var pair in enemyStates)
                    {
                        var enemyHealth = pair.Key;
                        if (enemyHealth != null && enemyHealth.IsAlive)
                        {
                            enemyHealth.Heal(enemyHealth.MaxHealth * RegenRatioPerTick);
                        }
                    }
                }
            }

            // 이번 프레임에 적용한 환불·회복까지 반영한 값을 다음 프레임 비교 기준으로 저장.
            foreach (var pair in enemyStates)
            {
                if (pair.Key != null)
                {
                    pair.Value.PreviousHealth = pair.Key.CurrentHealth;
                }
            }
        }

        private void HandleStructureDied(GuardiansTowerStructure structure)
        {
            switch (structure.Role)
            {
                case GuardiansTowerStructureRole.Defense:
                    if (defenseBuffActive)
                    {
                        defenseBuffActive = false;
                        showNotification?.Invoke("적의 방어력이 약해졌습니다.");
                    }

                    break;
                case GuardiansTowerStructureRole.Health:
                    if (healthBuffActive)
                    {
                        healthBuffActive = false;
                        RevertEnemyHealthBuff();
                        showNotification?.Invoke("적의 체력이 감소되었습니다.");
                    }

                    break;
                case GuardiansTowerStructureRole.Regen:
                    if (regenBuffActive)
                    {
                        regenBuffActive = false;
                        showNotification?.Invoke("적의 체력회복 버프가 종료되었습니다.");
                    }

                    break;
                case GuardiansTowerStructureRole.AttackBoost:
                    // 08.07 안건준 수정 - 4번 건물이 부서지는 순간 1회 아군 공격력 2배 버프를 발동한다
                    // (컨트롤러가 onAllyAttackBuffTriggered를 받아 아군 목록에 실제 배율을 적용한다).
                    onAllyAttackBuffTriggered?.Invoke();
                    showNotification?.Invoke("아군의 공격력이 강해졌습니다.");
                    break;
            }
        }

        private void RevertEnemyHealthBuff()
        {
            foreach (var pair in enemyStates)
            {
                var enemyHealth = pair.Key;
                if (enemyHealth != null && enemyHealth.IsAlive)
                {
                    enemyHealth.SetMaxHealth(pair.Value.BaseMaxHealth, keepCurrentRatio: true);
                    pair.Value.PreviousHealth = enemyHealth.CurrentHealth; // 감소를 피해로 오인하지 않도록 즉시 동기화
                }
            }
        }

        private bool HasAliveStructure(GuardiansTowerStructureRole targetRole)
        {
            foreach (var structure in structures)
            {
                if (structure != null && structure.Role == targetRole && structure.IsAlive)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
