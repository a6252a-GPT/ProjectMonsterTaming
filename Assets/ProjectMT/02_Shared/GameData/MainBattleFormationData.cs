using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.GameData
{
    public static class MainBattleFormationRules // 메인전투 시작 진형 저장·검증 기준
    {
        public const int SlotCount = 5;
        public const float UnitRadius = 0.45f;
        public const float OverlapTolerance = 0.02f;
        public const float AreaCenterZ = -3f;
        public const float AreaWidth = 12f;
        public const float AreaDepth = 6f;

        private static readonly Vector2[] DefaultOffsets =
        {
            new Vector2(-1.8f, -4.5f),
            new Vector2(-0.9f, -4.5f),
            new Vector2(0f, -4.5f),
            new Vector2(-1.35f, -5.3f),
            new Vector2(-0.35f, -5.31f) // 5번과 3번 링의 허용 간격 확보
        };

        public static Vector2 GetDefaultOffset(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < DefaultOffsets.Length
                ? DefaultOffsets[slotIndex]
                : Vector2.zero;
        }

        public static Vector2[] CreateDefaultOffsets()
        {
            var copy = new Vector2[DefaultOffsets.Length];
            Array.Copy(DefaultOffsets, copy, DefaultOffsets.Length);
            return copy;
        }

        public static bool IsValid(IReadOnlyList<Vector2> offsets)
        {
            if (offsets == null || offsets.Count != SlotCount)
            {
                return false;
            }

            for (var index = 0; index < offsets.Count; index++)
            {
                if (!IsInsideArea(offsets[index]))
                {
                    return false;
                }
            }

            for (var left = 0; left < offsets.Count - 1; left++)
            {
                for (var right = left + 1; right < offsets.Count; right++)
                {
                    if (!DoNotOverlap(offsets[left], offsets[right]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public static bool IsInsideArea(Vector2 offset)
        {
            if (!IsFinite(offset.x) || !IsFinite(offset.y))
            {
                return false;
            }

            var halfWidth = AreaWidth * 0.5f;
            var halfDepth = AreaDepth * 0.5f;
            return Mathf.Abs(offset.x) <= halfWidth - UnitRadius + 0.0001f &&
                   Mathf.Abs(offset.y - AreaCenterZ) <= halfDepth - UnitRadius + 0.0001f;
        }

        public static bool DoNotOverlap(Vector2 left, Vector2 right)
        {
            if (!IsFinite(left.x) || !IsFinite(left.y) || !IsFinite(right.x) || !IsFinite(right.y))
            {
                return false;
            }

            return Vector2.Distance(left, right) + OverlapTolerance >= UnitRadius * 2f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [Serializable]
    public sealed class MainBattleFormationData // 본부대 슬롯 1~5의 시작 위치 원본
    {
        private const int CurrentCoordinateSpaceVersion = 3; // 12×12 맵 중심 XZ 미터 좌표

        [SerializeField] private bool initialized;
        [SerializeField] private int coordinateSpaceVersion;
        [SerializeField] private Vector2[] mainPartySlotOffsets = Array.Empty<Vector2>();

        public static MainBattleFormationData CreateDefault()
        {
            return new MainBattleFormationData
            {
                initialized = true,
                coordinateSpaceVersion = CurrentCoordinateSpaceVersion,
                mainPartySlotOffsets = MainBattleFormationRules.CreateDefaultOffsets()
            };
        }

        public MainBattleFormationData Clone()
        {
            return new MainBattleFormationData
            {
                initialized = initialized,
                coordinateSpaceVersion = coordinateSpaceVersion,
                mainPartySlotOffsets = CopyOffsets()
            };
        }

        internal bool TrySet(IReadOnlyList<Vector2> offsets)
        {
            if (!MainBattleFormationRules.IsValid(offsets))
            {
                return false;
            }

            mainPartySlotOffsets = new Vector2[MainBattleFormationRules.SlotCount];
            for (var index = 0; index < mainPartySlotOffsets.Length; index++)
            {
                mainPartySlotOffsets[index] = offsets[index];
            }

            initialized = true;
            coordinateSpaceVersion = CurrentCoordinateSpaceVersion;
            return true;
        }

        internal void Repair()
        {
            if (!initialized || coordinateSpaceVersion != CurrentCoordinateSpaceVersion ||
                !MainBattleFormationRules.IsValid(mainPartySlotOffsets))
            {
                initialized = true;
                coordinateSpaceVersion = CurrentCoordinateSpaceVersion;
                mainPartySlotOffsets = MainBattleFormationRules.CreateDefaultOffsets();
            }
        }

        internal MainBattleFormationView CreateView()
        {
            return new MainBattleFormationView(CopyOffsets());
        }

        private Vector2[] CopyOffsets()
        {
            if (mainPartySlotOffsets == null)
            {
                return Array.Empty<Vector2>();
            }

            var copy = new Vector2[mainPartySlotOffsets.Length];
            Array.Copy(mainPartySlotOffsets, copy, mainPartySlotOffsets.Length);
            return copy;
        }
    }

    public readonly struct MainBattleFormationView // MainBattle에 전달할 위치 복사값
    {
        private readonly Vector2[] offsets;

        internal MainBattleFormationView(Vector2[] values)
        {
            offsets = values ?? Array.Empty<Vector2>();
        }

        public int SlotCount => offsets?.Length ?? 0;

        public bool TryGetSlotOffset(int slotIndex, out Vector2 offset)
        {
            if (offsets != null && slotIndex >= 0 && slotIndex < offsets.Length)
            {
                offset = offsets[slotIndex];
                return true;
            }

            offset = default;
            return false;
        }

        public Vector2[] CopyOffsets()
        {
            if (offsets == null)
            {
                return Array.Empty<Vector2>();
            }

            var copy = new Vector2[offsets.Length];
            Array.Copy(offsets, copy, offsets.Length);
            return copy;
        }
    }
}
