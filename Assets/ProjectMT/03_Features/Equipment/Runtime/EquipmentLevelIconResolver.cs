using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;
using UnityEngine;

namespace ProjectMT.Features.Equipment
{
    // 아이템 레벨 20단위로 페데리카 장비 아이콘 세트를 선택한다.
    public static class EquipmentLevelIconResolver
    {
        public const int LevelsPerSet = 20;
        public const int SetCount = 10;
        public const int MaximumCoveredLevel = LevelsPerSet * SetCount;

        private const string ResourcesPath = "EquipmentLevelIcons";
        private static readonly Dictionary<string, Sprite> SpritesByName =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);

        private static bool loaded;

        public static int GetSetNumber(int itemLevel)
        {
            var clampedLevel = Mathf.Clamp(itemLevel, 1, MaximumCoveredLevel);
            return (clampedLevel - 1) / LevelsPerSet + 1;
        }

        public static Sprite Resolve(EquipmentPart part, int itemLevel, Sprite fallback = null)
        {
            EnsureLoaded();
            return SpritesByName.TryGetValue(BuildSpriteName(GetSetNumber(itemLevel), part), out var sprite)
                && sprite != null
                ? sprite
                : fallback;
        }

        public static string BuildSpriteName(int setNumber, EquipmentPart part)
        {
            var clampedSet = Mathf.Clamp(setNumber, 1, SetCount);
            return $"Federica_Set{clampedSet:00}_{part}";
        }

        private static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            loaded = true;
            SpritesByName.Clear();
            var sprites = Resources.LoadAll<Sprite>(ResourcesPath);
            for (var index = 0; index < sprites.Length; index++)
            {
                var sprite = sprites[index];
                if (sprite != null && !SpritesByName.ContainsKey(sprite.name))
                {
                    SpritesByName.Add(sprite.name, sprite);
                }
            }
        }
    }
}
