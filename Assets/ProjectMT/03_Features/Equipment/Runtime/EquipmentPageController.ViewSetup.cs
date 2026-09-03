using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Equipment
{
    public sealed partial class EquipmentPageController
    {
        // 군단장 장착 슬롯 6개는 부위별로 고정된 오브젝트라 항상 올바른 아이콘 스프라이트를 갖고 있다.
        // 그 스프라이트를 부위별 대표 아이콘으로 캐시해서 인벤토리 슬롯에도 그대로 쓴다.
        private void CachePartIconSprites()
        {
            foreach (var pair in commanderSlots)
            {
                var image = pair.Value != null
                    ? pair.Value.GetComponentsInChildren<Image>(true)
                        .FirstOrDefault(candidate => candidate.name == "Item" && candidate.sprite != null)
                    : null;
                if (image != null && image.sprite != null)
                {
                    partIconSprites[pair.Key] = image.sprite;
                }
            }
        }

        // 목업 곳곳(인벤토리 슬롯들)에 흩어져 있는 등급별 프레임을 이름별로 하나씩 찾아서,
        // 눈에 보이지 않는 보관용 오브젝트 아래에 복제해둔다. 이후 슬롯에 실제 등급을 표시할 때
        // 이 복제본을 다시 복제해서 끼워 넣는 방식으로 "기존 프레임 그대로"의 테두리를 재사용한다.
        private void CacheFrameVariantTemplates()
        {
            var storageObject = new GameObject("EquipmentFrameTemplates(Hidden)");
            storageObject.transform.SetParent(transform, false);
            storageObject.SetActive(false);
            frameVariantTemplateStorage = storageObject.transform;

            var all = transform.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                var name = all[i].name;
                if (!name.StartsWith(FrameVariantPrefix))
                {
                    continue;
                }

                var suffix = name.Substring(FrameVariantPrefix.Length);
                if (frameVariantTemplates.ContainsKey(suffix))
                {
                    continue;
                }

                var clone = Instantiate(all[i].gameObject, frameVariantTemplateStorage);
                clone.name = name;
                frameVariantTemplates[suffix] = clone;

                var swatch = FindFrameSwatchGraphic(clone.transform);
                if (swatch != null)
                {
                    frameVariantSwatchColors[suffix] = swatch.color;
                }
            }
        }

        // 프레임 템플릿의 배경("Bg") 색을 찾는다. 분해창 등 단색 UI에서 재사용한다.
        private static Graphic FindFrameSwatchGraphic(Transform frameRoot)
        {
            var namedBg = frameRoot.Find("Bg");
            var namedGraphic = namedBg != null ? namedBg.GetComponent<Graphic>() : null;
            if (namedGraphic != null)
            {
                return namedGraphic;
            }

            var graphics = frameRoot.GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null && graphics[i].name != "Icon")
                {
                    return graphics[i];
                }
            }

            return null;
        }

        // 등급에 맞는 프레임 템플릿을 복제해서 normalArea 밑에 끼워 넣는다.
        // 장비 획득처럼 목록이 같은 프레임에 여러 번 갱신될 수 있으므로, 이전 프레임은 Destroy 예약만
        // 하지 않고 즉시 숨긴다. 그래야 이전 등급 테두리가 새 테두리 위에 잠깐 남지 않는다.
        private void ApplyFrameVariant(Transform normalArea, EquipmentGrade grade)
        {
            if (normalArea == null || !FrameVariantSuffixByGrade.TryGetValue(grade, out var suffix))
            {
                return;
            }

            var desiredName = FrameVariantPrefix + suffix;
            Transform desiredFrame = null;
            for (var index = normalArea.childCount - 1; index >= 0; index--)
            {
                var frame = normalArea.GetChild(index);
                if (!frame.name.StartsWith(FrameVariantPrefix))
                {
                    continue;
                }

                if (desiredFrame == null && frame.name == desiredName && frame.gameObject.activeSelf)
                {
                    desiredFrame = frame;
                    continue;
                }

                frame.gameObject.SetActive(false);
                Destroy(frame.gameObject);
            }

            if (desiredFrame != null)
            {
                return; // 이미 올바른 등급 프레임 하나만 남아 있다.
            }

            if (!frameVariantTemplates.TryGetValue(suffix, out var template) || template == null)
            {
                return;
            }

            var instance = Instantiate(template, normalArea);
            instance.name = desiredName;
            instance.SetActive(true);

            var rect = instance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }

        // 자식 전체(비활성 포함)에서 이름이 일치하는 첫 Transform을 찾는다.
        private static Transform FindDeep(Transform root, string childName)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].name == childName)
                {
                    return all[i];
                }
            }

            return null;
        }

        // 하이어라키 이름이 바뀔 수 있어 여러 후보를 순서대로 시도한다(먼저 찾은 이름이 우선한다).
        private static Transform FindDeepAny(Transform root, params string[] candidateNames)
        {
            for (var i = 0; i < candidateNames.Length; i++)
            {
                var found = FindDeep(root, candidateNames[i]);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
