using System;
using UnityEngine;

namespace ProjectMT.Features.Equipment
{
    // 08.09 안건준 추가 - 카탈로그에 등록하는 "부위 기준 베이스 아이템" 1개.
    // 카탈로그에 이 항목 하나를 추가하면 5개 등급(Common~Mythic) 장비가 전부 자동으로 만들어진다.
    [Serializable]
    public sealed class EquipmentBaseItemDefinition
    {
        [SerializeField] private string id; // 카탈로그 내부 식별용 ID (예: weapon_basic)
        [SerializeField] private string displayName; // 등급 이름과 조합될 기본 표시 이름 (예: "무기" → "일반 무기")
        [SerializeField] private EquipmentPart part;
        [SerializeField] private Sprite icon; // 아이콘 아트가 아직 없다면 비워둬도 된다 (기본 UI로 대체)

        public EquipmentBaseItemDefinition(string id, string displayName, EquipmentPart part, Sprite icon = null)
        {
            this.id = id;
            this.displayName = displayName;
            this.part = part;
            this.icon = icon;
        }

        public string Id => id;
        public string DisplayName => displayName;
        public EquipmentPart Part => part;
        public Sprite Icon => icon;
    }
}
