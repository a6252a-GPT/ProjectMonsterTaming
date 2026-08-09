using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Features.Equipment
{
    // 08.09 안건준 추가 - 보유 장비 인벤토리 + 장착 상태 저장소.
    //
    // 지금 단계에서는 "테스트용 GetEquipmentButton"으로만 장비를 획득하고, 요청사항에 따라
    // 이 획득 내용은 저장 파일에 남기지 않고 플레이 세션(= 플레이 모드 1회 실행) 동안만 유지한다.
    // static 필드는 유니티 도메인 리로드(플레이 종료 → 재시작, 또는 빌드 재실행) 시 자동으로 초기화되므로
    // 별도 저장 로직을 붙이지 않는 것만으로 "재시작하면 초기화" 요구사항이 만족된다.
    //
    // 실제 원정대 스테이지 드랍과 연결해 영구 저장이 필요해지면, 이 클래스의 데이터를
    // GameProgressData 쪽으로 옮기는 작업을 별도로 진행하면 된다 (지금은 범위 밖).
    public static class EquipmentInventoryRuntime
    {
        // 인벤토리 최대 보유 수량(전체 스택 합계 기준). 장비창 표시(18 / 100)에 맞춘 기존 설정값.
        public const int MaxTotalQuantity = 100;

        private static readonly Dictionary<string, EquipmentStack> stacks = new Dictionary<string, EquipmentStack>();
        private static readonly Dictionary<EquipmentPart, string> equippedKeyByPart = new Dictionary<EquipmentPart, string>();

        // 인벤토리 목록이 바뀔 때(획득/장착/해제 등) 알림. UI가 이 이벤트를 구독해 새로 그린다.
        public static event Action InventoryChanged;

        // 특정 부위의 장착 상태만 바뀌었을 때 알림 (군단장 능력치 갱신 등에 사용).
        public static event Action<EquipmentPart> EquippedChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            stacks.Clear();
            equippedKeyByPart.Clear();
        }

        public static IReadOnlyCollection<EquipmentStack> Stacks => stacks.Values;

        public static int TotalQuantity => stacks.Values.Sum(s => s.TotalQuantity);

        public static bool TryGetStack(string key, out EquipmentStack stack) => stacks.TryGetValue(key, out stack);

        // 장비를 count개 추가한다. 동일 장비 종류(Key)가 이미 있으면 수량만 합산한다.
        // 인벤토리 최대 수량(100)을 넘는 초과분은 지급하지 않는다(가득 찼을 때 처리 - 임시 규칙, P0-1 미확정 값).
        public static int AddEquipment(EquipmentDefinition definition, int count = 1)
        {
            if (definition == null || count <= 0)
            {
                return 0;
            }

            var allowed = Mathf.Max(0, Mathf.Min(count, MaxTotalQuantity - TotalQuantity));
            if (allowed <= 0)
            {
                return 0; // 인벤토리가 가득 찼다
            }

            if (stacks.TryGetValue(definition.Key, out var stack))
            {
                stack.TotalQuantity += allowed;
            }
            else
            {
                stacks[definition.Key] = new EquipmentStack(definition, allowed);
            }

            InventoryChanged?.Invoke();
            return allowed;
        }

        // 지정한 장비 종류를 장착한다. 이미 그 부위에 다른 장비가 장착돼 있으면 자동으로 교체하고,
        // 교체된 기존 장비는 그대로 인벤토리 스택에 남는다(수량 차감 없음, 장착 표시만 이동).
        public static bool TryEquip(string key)
        {
            if (!stacks.TryGetValue(key, out var stack) || stack.TotalQuantity <= 0)
            {
                return false;
            }

            var part = stack.Definition.Part;
            if (equippedKeyByPart.TryGetValue(part, out var previousKey) && previousKey != key)
            {
                if (stacks.TryGetValue(previousKey, out var previousStack))
                {
                    previousStack.IsEquipped = false;
                }
            }

            stack.IsEquipped = true;
            equippedKeyByPart[part] = key;
            InventoryChanged?.Invoke();
            EquippedChanged?.Invoke(part);
            return true;
        }

        // 해당 부위의 장착을 해제한다. 장비 해제는 허용한다(요청 사항: 장착 버튼 텍스트가 "해제"로 바뀌어야 함).
        public static bool TryUnequip(EquipmentPart part)
        {
            if (!equippedKeyByPart.TryGetValue(part, out var key))
            {
                return false;
            }

            equippedKeyByPart.Remove(part);
            if (stacks.TryGetValue(key, out var stack))
            {
                stack.IsEquipped = false;
            }

            InventoryChanged?.Invoke();
            EquippedChanged?.Invoke(part);
            return true;
        }

        public static EquipmentStack GetEquippedStack(EquipmentPart part)
        {
            return equippedKeyByPart.TryGetValue(part, out var key) && stacks.TryGetValue(key, out var stack)
                ? stack
                : null;
        }

        public static bool IsPartEquipped(EquipmentPart part) => equippedKeyByPart.ContainsKey(part);
    }
}
