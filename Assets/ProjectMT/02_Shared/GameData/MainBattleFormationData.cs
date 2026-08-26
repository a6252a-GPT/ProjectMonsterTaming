using System;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.GameData
{
    public enum MainBattleFormationLine // 적 방향 기준 편성 열
    {
        Front,
        Middle,
        Rear
    }

    public static class MainBattleFormationRules // 메인전투 시작 진형 저장·검증 기준
    {
        public const int SlotCount = 5;
        public const float UnitRadius = 0.45f;
        public const float OverlapTolerance = 0.02f;
        public const float AreaCenterX = -1.1f;
        public const float AreaCenterZ = -4.6f;
        public const float AreaWidth = 12f;
        public const float AreaDepth = 6.8f;
        public const float HexSpacing = 1.1f; // 인접 육각 칸 중심 간격
        public const float HexVisualRadius = 0.6351f;

        private const float HexRowSpacing = HexSpacing * 0.8660254f;
        private const float DefaultFrontRowZ = -4.61f; // 정식 Scene 아군 앞열
        private const float HexOriginZ = DefaultFrontRowZ + HexRowSpacing * 2f;
        private const float HexPositionTolerance = 0.015f;

        private static readonly Vector2[] DefaultOffsets =
        {
            CreateHexOffset(0, -2),
            CreateHexOffset(1, -2),
            CreateHexOffset(2, -2),
            CreateHexOffset(1, -3),
            CreateHexOffset(2, -3)
        };

        private static readonly Vector2[] HexOffsets = BuildHexOffsets();

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

        public static Vector2[] CopyHexOffsets()
        {
            var copy = new Vector2[HexOffsets.Length];
            Array.Copy(HexOffsets, copy, HexOffsets.Length);
            return copy;
        }

        public static MainBattleFormationLine GetLine(Vector2 offset)
        {
            var row = Mathf.RoundToInt((offset.y - HexOriginZ) / HexRowSpacing);
            if (row >= 0)
            {
                return MainBattleFormationLine.Front; // 맨 앞 2줄
            }

            return row >= -3
                ? MainBattleFormationLine.Middle // 가운데 3줄
                : MainBattleFormationLine.Rear; // 맨 뒤 2줄
        }

        public static Vector2 SnapToHex(Vector2 offset)
        {
            var best = DefaultOffsets[0];
            var bestDistance = float.PositiveInfinity;
            for (var index = 0; index < HexOffsets.Length; index++)
            {
                var distance = (HexOffsets[index] - offset).sqrMagnitude;
                if (distance >= bestDistance)
                {
                    continue;
                }

                best = HexOffsets[index];
                bestDistance = distance;
            }

            return best;
        }

        public static bool IsHexPosition(Vector2 offset)
        {
            var toleranceSquared = HexPositionTolerance * HexPositionTolerance;
            for (var index = 0; index < HexOffsets.Length; index++)
            {
                if ((HexOffsets[index] - offset).sqrMagnitude <= toleranceSquared)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsHexFormation(IReadOnlyList<Vector2> offsets)
        {
            if (!IsValid(offsets))
            {
                return false;
            }

            for (var index = 0; index < offsets.Count; index++)
            {
                if (!IsHexPosition(offsets[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryCreateSnappedOffsets(IReadOnlyList<Vector2> source, out Vector2[] snapped)
        {
            if (source == null || source.Count != SlotCount)
            {
                snapped = CreateDefaultOffsets();
                return false;
            }

            snapped = new Vector2[SlotCount];
            var occupied = new bool[HexOffsets.Length];
            for (var slotIndex = 0; slotIndex < source.Count; slotIndex++)
            {
                var bestIndex = -1;
                var bestDistance = float.PositiveInfinity;
                for (var hexIndex = 0; hexIndex < HexOffsets.Length; hexIndex++)
                {
                    if (occupied[hexIndex])
                    {
                        continue;
                    }

                    var distance = (HexOffsets[hexIndex] - source[slotIndex]).sqrMagnitude;
                    if (distance >= bestDistance)
                    {
                        continue;
                    }

                    bestIndex = hexIndex;
                    bestDistance = distance;
                }

                if (bestIndex < 0)
                {
                    snapped = CreateDefaultOffsets();
                    return false;
                }

                occupied[bestIndex] = true;
                snapped[slotIndex] = HexOffsets[bestIndex];
            }

            return IsHexFormation(snapped);
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
            return Mathf.Abs(offset.x - AreaCenterX) <= halfWidth - UnitRadius + 0.0001f &&
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

        private static Vector2[] BuildHexOffsets()
        {
            var result = new List<Vector2>(96);
            for (var row = -8; row <= 8; row++)
            {
                for (var column = -12; column <= 12; column++)
                {
                    var offset = CreateHexOffset(column, row);
                    if (IsInsideArea(offset))
                    {
                        result.Add(offset);
                    }
                }
            }

            result.Sort((left, right) =>
            {
                var rowComparison = right.y.CompareTo(left.y);
                return rowComparison != 0 ? rowComparison : left.x.CompareTo(right.x);
            });
            return result.ToArray();
        }

        private static Vector2 CreateHexOffset(int column, int row)
        {
            return new Vector2(
                HexSpacing * (column + row * 0.5f),
                HexOriginZ + HexRowSpacing * row);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public static class MainBattleFormationBuffRules // 시작 열에 고정되는 편성 버프
    {
        public const float FrontDefenseRate = 0.2f;
        public const float MiddleAttackRate = 0.2f;
        public const float RearSupportRate = 0.2f;

        public static UnitStatsSnapshot ApplyStats(UnitStatsSnapshot source, Vector2 offset)
        {
            switch (MainBattleFormationRules.GetLine(offset))
            {
                case MainBattleFormationLine.Front:
                    source.defense *= 1f + FrontDefenseRate;
                    break;
                case MainBattleFormationLine.Middle:
                    source.damage *= 1f + MiddleAttackRate;
                    break;
            }

            return source;
        }

        public static float GetSupportOutputMultiplier(Vector2 offset)
        {
            return MainBattleFormationRules.GetLine(offset) == MainBattleFormationLine.Rear
                ? 1f + RearSupportRate
                : 1f;
        }
    }

    [Serializable]
    public sealed class MainBattleFormationData // 본부대 슬롯 1~5의 시작 위치 원본
    {
        private const int CurrentCoordinateSpaceVersion = 3; // PlayerFormationAnchor 기준 XZ 미터 좌표

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
