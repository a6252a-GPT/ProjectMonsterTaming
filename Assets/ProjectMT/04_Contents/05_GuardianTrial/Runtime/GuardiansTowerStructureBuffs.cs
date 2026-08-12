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
    //  1번 건물(Defense)     : 적에게 방어력 스탯이 없어 방어력을 올릴 수 없으므로, 아군이 적에게 주는
    //                          피해를 적용하기 전에 30%로 계산해서 넘긴다(피해를 100% 적용 후 환불하는
    //                          방식이 아니라, 적용 전에 최종 피해의 30%만 계산해서 넘기는 방식).
    //  2번 건물(Health)      : "적 캐릭터" 최대 체력 *3.
    //  3번 건물(Regen)       : "적 캐릭터"가 1초마다 최대 체력의 30% 회복.
    //  4번 건물(AttackBoost) : 이 건물이 "파괴되는 순간" 1회 "아군"에게 공격력 2배 버프를 발동한다.
    //                          실제 배율 적용(UnitActor.SetDamageMultiplier)은 아군 목록을 가진
    //                          GuardiansTowerController만 할 수 있어서, 이 클래스는 파괴 시점에
    //                          onAllyAttackBuffTriggered 콜백만 호출해 알려주고 실제 적용은 컨트롤러가 담당한다.
    // 1~3번 건물이 부서지면 그 버프만 즉시 해제되고, 화면 중앙 알림 콜백이 호출된다.
    public sealed class GuardiansTowerStructureBuffs
    {
        private const float DefenseIncomingDamageMultiplier = 0.30f; // 1번: 받는 피해를 적용 전에 30%로 계산
        private const float EnemyHealthMultiplier = 3f; // 2번: 적 최대 체력 *3
        private const float RegenRatioPerTick = 0.30f; // 3번 건물: 1초마다 최대 체력의 30% 회복
        private const float RegenTickInterval = 1f;

        private sealed class EnemyBuffState
        {
            public HealthComponent Health; // 체력·받는피해 버프 적용 대상
            public float BaseMaxHealth; // 체력 버프 적용 전 원래 최대 체력(적마다 다를 수 있어 개별 저장)
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

            if (defenseBuffActive)
            {
                enemyHealth.SetIncomingDamageMultiplier(DefenseIncomingDamageMultiplier);
            }

            enemyStates[enemyHealth] = new EnemyBuffState
            {
                Health = enemyHealth,
                BaseMaxHealth = baseMax
            };
        }

        // 매 프레임 호출: 체력회복 틱만 처리(방어 버프는 스폰 시점에 이미 적용됨).
        public void Tick(float deltaTime)
        {
            if (!regenBuffActive)
            {
                return;
            }

            regenTimer += deltaTime;
            if (regenTimer < RegenTickInterval)
            {
                return;
            }

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

        private void HandleStructureDied(GuardiansTowerStructure structure)
        {
            switch (structure.Role)
            {
                case GuardiansTowerStructureRole.Defense:
                    if (defenseBuffActive)
                    {
                        defenseBuffActive = false;
                        RevertEnemyDefenseBuff();
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
                }
            }
        }

        private void RevertEnemyDefenseBuff()
        {
            foreach (var pair in enemyStates)
            {
                pair.Key?.SetIncomingDamageMultiplier(1f);
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
