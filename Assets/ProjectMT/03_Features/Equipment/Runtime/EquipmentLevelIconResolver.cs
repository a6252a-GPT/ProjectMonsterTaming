using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;
using UnityEngine;
using UnityEngine.UI;

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

        // 기존 장비 슬롯은 부위별 원본 아이콘 크기에 맞춘 서로 다른 localScale을 가지고 있다.
        // 레벨 아이콘은 모두 같은 정사각 셀이므로 ItemFrame_01 안에서 동일한 크기와 위치를 사용한다.
        public static void NormalizeMainSlotIcon(Image image)
        {
            if (image == null || image.sprite == null ||
                !image.sprite.name.StartsWith("Federica_Set", StringComparison.Ordinal) ||
                image.name != "Item" || image.transform.parent == null ||
                image.transform.parent.name != "ItemFrame_01")
            {
                return;
            }

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.08f, 0.22f);
            rect.anchorMax = new Vector2(0.92f, 0.96f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
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
