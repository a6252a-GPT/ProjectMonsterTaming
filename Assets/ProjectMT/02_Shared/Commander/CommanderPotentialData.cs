using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;
using UnityEngine;

namespace ProjectMT.Shared.Commander
{
    // 군단장 잠재능력 슬롯 1건(옵션 종류+등급+확정값).
    // 장비 랜덤 옵션과 동일하게 뽑히는 순간 값이 확정되고, 그 뒤로는 재접속해도 다시 추첨하지 않는다.
    [Serializable]
    public sealed class CommanderPotentialSlotData
    {
        [SerializeField] private bool hasValue;
        [SerializeField] private EquipmentOptionType optionType;
        [SerializeField] private EquipmentGrade grade;
        [SerializeField] private float value;
        [SerializeField] private bool locked; // 재추첨("잠재 능력 변경") 대상에서 제외할지 여부

        public bool HasValue => hasValue;
        public EquipmentOptionType OptionType => optionType;
        public EquipmentGrade Grade => grade;
        public float Value => value;
        public bool Locked => locked;

        public static CommanderPotentialSlotData CreateEmpty() => new CommanderPotentialSlotData();

        public CommanderPotentialSlotData Clone()
        {
            return new CommanderPotentialSlotData
            {
                hasValue = hasValue,
                optionType = optionType,
                grade = grade,
                value = value,
                locked = locked
            };
        }

        internal void Repair()
        {
            if (!hasValue)
            {
                ClearInternal();
                return;
            }

            if (!Enum.IsDefined(typeof(EquipmentOptionType), optionType) ||
                !Enum.IsDefined(typeof(EquipmentGrade), grade) ||
                float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                ClearInternal(); // 손상된 값은 다시 빈 슬롯으로 되돌린다
            }
        }

        // 비어 있는 슬롯에만 값을 배정한다(CAS와 비슷하게, 이미 값이 있으면 실패해서 중복 배정을 막는다).
        internal bool TryAssign(EquipmentOptionType newType, EquipmentGrade newGrade, float newValue)
        {
            if (hasValue || newValue <= 0f)
            {
                return false;
            }

            optionType = newType;
            grade = newGrade;
            value = newValue;
            hasValue = true;
            locked = false;
            return true;
        }

        // "잠재 능력 변경" 재추첨. 값이 있고 잠겨있지 않은 슬롯만 다른 옵션으로 교체할 수 있다.
        internal bool TryReplace(EquipmentOptionType newType, EquipmentGrade newGrade, float newValue)
        {
            if (!hasValue || locked || newValue <= 0f)
            {
                return false;
            }

            optionType = newType;
            grade = newGrade;
            value = newValue;
            return true;
        }

        // "옵션 스탯 변경". 옵션 종류·등급은 그대로 두고 수치만 같은 등급 범위 안에서 다시 뽑는다.
        // 잠금은 "잠재 능력 변경"(옵션 자체가 바뀌는 것)만 막는 용도라 여기서는 잠금 여부를 보지 않는다.
        internal bool TryRerollValue(float newValue)
        {
            if (!hasValue || newValue <= 0f)
            {
                return false;
            }

            value = newValue;
            return true;
        }

        // 사용자가 자물쇠 아이콘으로 재추첨 대상에서 제외할 슬롯을 잠그거나 푼다.
        // expectedLocked로 클릭 시점과 반영 시점 사이 상태가 달라졌는지(중복 클릭 등) 확인한다.
        internal bool TrySetLocked(bool expectedLocked, bool newLocked)
        {
            if (!hasValue || locked != expectedLocked)
            {
                return false;
            }

            locked = newLocked;
            return true;
        }

        private void ClearInternal()
        {
            hasValue = false;
            optionType = default;
            grade = default;
            value = 0f;
            locked = false;
        }
    }

    // 군단장 잠재능력 5슬롯 저장 데이터. "수호자의 힘" 단계 수만큼 슬롯이 순서대로 해금된다.
    [Serializable]
    public sealed class CommanderPotentialData
    {
        public const int SlotCount = 5;

        // "수호자의 힘" 해금 진행도. 1단계에서 시작해 100 경험치마다 다음 단계로 올라가고,
        // 단계 수만큼 잠재능력 슬롯이 순서대로 해금된다(1단계=1슬롯 ... 5단계=5슬롯 전부).
        public const int MaxStage = SlotCount;
        public const long ExperiencePerStage = 100;
        public const long ExperiencePerReroll = 10; // "잠재 능력 변경"/"옵션 스탯 변경" 버튼 1회 클릭당 증가량

        // "능력 잠금" 강화석 비용. 이미 잠긴 슬롯 수(0~4)를 기준으로 다음 슬롯을
        // 잠글 때 1→2→4→8→16개로 2배씩 늘어난다(잠금 해제는 무료). 전부 잠기면 더 잠글 슬롯이 없다.
        private static readonly long[] LockStoneCostByAlreadyLockedCount = { 1, 2, 4, 8, 16 };

        internal static long GetLockStoneCost(int alreadyLockedCount)
        {
            if (alreadyLockedCount < 0)
            {
                alreadyLockedCount = 0;
            }
            else if (alreadyLockedCount >= LockStoneCostByAlreadyLockedCount.Length)
            {
                alreadyLockedCount = LockStoneCostByAlreadyLockedCount.Length - 1;
            }

            return LockStoneCostByAlreadyLockedCount[alreadyLockedCount];
        }

        [SerializeField] private List<CommanderPotentialSlotData> slots;
        [SerializeField] private int stage = 1;
        [SerializeField] private long experience;

        public int Stage => stage;
        public long Experience => experience;

        public static CommanderPotentialData CreateDefault()
        {
            var data = new CommanderPotentialData();
            data.Repair();
            return data;
        }

        public CommanderPotentialData Clone()
        {
            var clone = new CommanderPotentialData();
            EnsureSlots(clone);
            EnsureSlots(this);
            for (var i = 0; i < SlotCount; i++)
            {
                clone.slots[i] = slots[i]?.Clone() ?? CommanderPotentialSlotData.CreateEmpty();
            }

            clone.stage = stage;
            clone.experience = experience;
            return clone;
        }

        internal void Repair()
        {
            EnsureSlots(this);
            for (var i = 0; i < slots.Count; i++)
            {
                slots[i] ??= CommanderPotentialSlotData.CreateEmpty();
                slots[i].Repair();
            }

            if (stage < 1)
            {
                stage = 1;
            }
            else if (stage > MaxStage)
            {
                stage = MaxStage;
            }

            var experienceCap = stage >= MaxStage ? ExperiencePerStage : ExperiencePerStage - 1;
            if (experience < 0)
            {
                experience = 0;
            }
            else if (experience > experienceCap)
            {
                experience = experienceCap;
            }
        }

        // "잠재 능력 변경"/"옵션 스탯 변경" 클릭 시 "수호자의 힘" 경험치를 올리고,
        // 100을 채우면 다음 단계로 승급하며 잠재능력 슬롯 해금 수도 함께 늘어난다.
        internal void AddExperience(long amount)
        {
            if (amount <= 0 || stage >= MaxStage)
            {
                return;
            }

            experience += amount;
            while (stage < MaxStage && experience >= ExperiencePerStage)
            {
                experience -= ExperiencePerStage;
                stage++;
            }

            if (stage >= MaxStage)
            {
                experience = ExperiencePerStage; // 마지막 단계는 항상 가득 찬 상태로 보여준다
            }
        }

        internal CommanderPotentialSlotData GetSlot(int index)
        {
            EnsureSlots(this);
            return index >= 0 && index < slots.Count ? slots[index] : null;
        }

        // 지정한 슬롯이 비어 있을 때만 배정에 성공한다.
        internal bool TryAssignSlot(int index, EquipmentOptionType type, EquipmentGrade grade, float value)
        {
            EnsureSlots(this);
            return index >= 0 && index < slots.Count && slots[index].TryAssign(type, grade, value);
        }

        // 값이 있고 잠기지 않은 슬롯을 다른 옵션으로 교체("잠재 능력 변경").
        internal bool TryReplaceSlot(int index, EquipmentOptionType type, EquipmentGrade grade, float value)
        {
            EnsureSlots(this);
            return index >= 0 && index < slots.Count && slots[index].TryReplace(type, grade, value);
        }

        // 재추첨 대상에서 제외할 슬롯 잠금/해제 토글.
        internal bool TrySetSlotLocked(int index, bool expectedLocked, bool newLocked)
        {
            EnsureSlots(this);
            return index >= 0 && index < slots.Count && slots[index].TrySetLocked(expectedLocked, newLocked);
        }

        // 현재 잠긴 슬롯 수(다음 잠금 비용 계산용).
        internal int CountLockedSlots()
        {
            EnsureSlots(this);
            var count = 0;
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].HasValue && slots[i].Locked)
                {
                    count++;
                }
            }

            return count;
        }

        // "옵션 스탯 변경"(잠금 여부와 무관하게 값만 다시 뽑음).
        internal bool TryRerollSlotValue(int index, float value)
        {
            EnsureSlots(this);
            return index >= 0 && index < slots.Count && slots[index].TryRerollValue(value);
        }

        internal CommanderPotentialView CreateView() => new CommanderPotentialView(this);

        private static void EnsureSlots(CommanderPotentialData data)
        {
            data.slots ??= new List<CommanderPotentialSlotData>(SlotCount);
            while (data.slots.Count < SlotCount)
            {
                data.slots.Add(CommanderPotentialSlotData.CreateEmpty());
            }

            if (data.slots.Count > SlotCount)
            {
                data.slots.RemoveRange(SlotCount, data.slots.Count - SlotCount);
            }
        }
    }

    // "잠재 능력 변경"/"옵션 스탯 변경" 1회 요청에서 슬롯 1개에 새로 뽑힌 결과(여러 슬롯이 대상이면 리스트로
    // 주고받는다). GameProgressChange가 이 값을 그대로 저장해 두었다가 TryApply에서 결정론적으로 반영한다
    // (추첨 자체는 이 구조체를 만들기 전에 이미 끝나 있어야 한다).
    public readonly struct CommanderPotentialRerollEntry
    {
        public CommanderPotentialRerollEntry(int slotIndex, EquipmentOptionType optionType, EquipmentGrade grade, float value)
        {
            SlotIndex = slotIndex;
            OptionType = optionType;
            Grade = grade;
            Value = value;
        }

        public int SlotIndex { get; }
        public EquipmentOptionType OptionType { get; }
        public EquipmentGrade Grade { get; }
        public float Value { get; }
    }

    // 외부(UI·능력치 계산)에 전달할 슬롯 1개의 읽기 전용 복사값.
    public readonly struct CommanderPotentialSlotView
    {
        internal CommanderPotentialSlotView(CommanderPotentialSlotData data)
        {
            HasValue = data != null && data.HasValue;
            OptionType = data?.OptionType ?? default;
            Grade = data?.Grade ?? default;
            Value = data?.Value ?? 0f;
            Locked = data?.Locked ?? false;
        }

        public bool HasValue { get; }
        public EquipmentOptionType OptionType { get; }
        public EquipmentGrade Grade { get; }
        public float Value { get; }
        public bool Locked { get; }
    }

    // 외부에 전달할 5슬롯 전체의 읽기 전용 복사값.
    public readonly struct CommanderPotentialView
    {
        private readonly CommanderPotentialSlotView[] slots;

        internal CommanderPotentialView(CommanderPotentialData data)
        {
            slots = new CommanderPotentialSlotView[CommanderPotentialData.SlotCount];
            for (var i = 0; i < CommanderPotentialData.SlotCount; i++)
            {
                slots[i] = new CommanderPotentialSlotView(data?.GetSlot(i));
            }

            Stage = data?.Stage ?? 1;
            Experience = data?.Experience ?? 0L;
        }

        // "수호자의 힘" 단계(1~5)와 현재 단계 안의 경험치(0~100).
        public int Stage { get; }
        public long Experience { get; }

        public CommanderPotentialSlotView GetSlot(int index)
        {
            return slots != null && index >= 0 && index < slots.Length ? slots[index] : default;
        }
    }
}
