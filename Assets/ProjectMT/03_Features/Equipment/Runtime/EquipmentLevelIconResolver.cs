using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;
using TMPro;
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
            // 슬롯 네 방향에 같은 여백을 두어 아이콘의 중심을 프레임 중심에 맞춘다.
            // 기존 세로 74% 영역보다 조금 큰 84% 정사각 영역을 사용한다.
            rect.anchorMin = new Vector2(0.08f, 0.08f);
            rect.anchorMax = new Vector2(0.92f, 0.92f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }

        // 작은 메인 장비 슬롯의 레벨은 아이콘 위에 겹쳐 표시되므로
        // 원래 인상을 유지한 채 크기와 위치를 조금 보정하고 얇은 그림자만 더한다.
        public static void NormalizeMainSlotLevel(TMP_Text text)
        {
            if (text == null || text.name != "Text_Level")
            {
                return;
            }

            // Prefab마다 16px/24px가 섞여 있으므로 고정 크기로 덮지 않고
            // 각 Text의 기존 Auto Size 상한에서 정확히 2px만 올린다.
            if (text.enableAutoSizing)
            {
                text.fontSize = Mathf.Max(text.fontSize, text.fontSizeMax) + 2f;
                text.enableAutoSizing = false;

                var rect = text.rectTransform;
                rect.anchoredPosition += new Vector2(0f, 3f);
            }

            var shadow = text.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = text.gameObject.AddComponent<Shadow>();
            }

            text.outlineWidth = 0f;
            text.raycastTarget = false;
            shadow.effectColor = new Color32(0, 0, 0, 150);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.useGraphicAlpha = true;
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
