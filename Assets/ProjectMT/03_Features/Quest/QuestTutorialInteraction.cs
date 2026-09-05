using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectMT.Features.Quest
{
    internal static class QuestTutorialInteraction
    {
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();

        internal static bool CanInteract(RectTransform target)
        {
            var button = target != null ? target.GetComponentInParent<Selectable>() : null;
            return button != null && button.IsInteractable() && IsVisible(target);
        }

        internal static bool IsVisible(RectTransform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            var button = target.GetComponentInParent<Selectable>();
            if (button == null || !button.IsActive()) return false;
            if (EventSystem.current == null) return false;
            var canvas = target.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var point = RectTransformUtility.WorldToScreenPoint(camera, target.TransformPoint(target.rect.center));
            if (point.x < 0 || point.y < 0 || point.x > Screen.width || point.y > Screen.height) return false;
            Hits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, Hits);
            return Hits.Count > 0 && Hits[0].gameObject.transform.IsChildOf(button.transform);
        }

        internal static string Message(string step) => step switch
        {
            "shop_gacha" => "소환권을 확인하고 몬스터를 소환하세요",
            "shop_result" => "결과를 확인하면 다음 안내로 이어져요",
            "page_close" => "목표 달성! 창을 닫고 보상을 받아주세요",
            "commander_potential_tab" => "잠재능력 탭을 열어주세요",
            "commander_stats_tab" => "능력치 탭을 열어주세요",
            "commander_potential_action" => "재료를 확인하고 잠재능력을 변경하세요",
            "commander_level_up" => "군단장의 레벨을 올려주세요",
            "commander_health" => "이번 목표는 체력 강화예요",
            "commander_attack" => "이번 목표는 공격력 강화예요",
            "commander_defense" => "이번 목표는 방어력 강화예요",
            "commander_power" => "능력치를 강화해 전투력을 올려주세요",
            "monster_level_up" => "선택한 몬스터를 레벨업하세요",
            "monster_breakthrough_tab" => "돌파 탭을 열어주세요",
            "monster_breakthrough_candidate" => "돌파 가능한 몬스터를 선택하세요",
            "monster_breakthrough_action" => "중복 재료를 확인하고 돌파하세요",
            "formation_candidate" => "편성할 몬스터를 선택하세요",
            "formation_action" => "선택한 몬스터의 편성을 확정하세요",
            "equipment_candidate" => "장착할 장비의 옵션을 확인하세요",
            "equipment_equip" => "옵션을 확인한 장비를 장착하세요",
            "slot_part" => "강화할 장비 슬롯을 선택하세요",
            "slot_upgrade" => "재료를 확인하고 슬롯을 강화하세요",
            "dismantle_tab" => "분해 탭을 열어주세요",
            "dismantle_auto" => "선택 등급 이하의 분해 대상을 모아보세요",
            "dismantle_action" => "분해할 대상과 획득 재료를 확인하세요",
            "growth_dungeon_enter" => "선택한 성장 던전에 입장하세요",
            "castle_raid" => "군단의 역습은 여기에서 시작해요",
            _ => "여기를 눌러 진행하세요"
        };
    }
}
