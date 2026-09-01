using System.Collections.Generic;
using ProjectMT.Shared.Equipment;
using UnityEngine;

namespace ProjectMT.Features.Equipment
{
    // 08.09 안건준 추가 - 장비 카탈로그. 베이스 아이템(부위 1개당 1개)만 등록해두면
    // 등급 5개(Common~Mythic)는 EquipmentGradeStatTable 수치로 자동 생성된다.
    // 새 장비를 추가하려면 baseItems 리스트에 항목 하나만 추가하면 된다 (인스펙터에서 편집 가능).
    [CreateAssetMenu(menuName = "ProjectMT/Equipment/Equipment Catalog", fileName = "EquipmentCatalog")]
    public sealed class EquipmentCatalog : ScriptableObject
    {
        [SerializeField] private List<EquipmentBaseItemDefinition> baseItems = new List<EquipmentBaseItemDefinition>();

        public IReadOnlyList<EquipmentBaseItemDefinition> BaseItems => baseItems;

        // 카탈로그가 처음 만들어질 때 6개 부위 기본 항목을 자동으로 채워준다.
        private void Reset()
        {
            baseItems = new List<EquipmentBaseItemDefinition>
            {
                new EquipmentBaseItemDefinition("weapon_basic", "무기", EquipmentPart.Weapon),
                new EquipmentBaseItemDefinition("helmet_basic", "투구", EquipmentPart.Helmet),
                new EquipmentBaseItemDefinition("armor_basic", "갑옷", EquipmentPart.Armor),
                new EquipmentBaseItemDefinition("glove_basic", "장갑", EquipmentPart.Glove),
                new EquipmentBaseItemDefinition("boots_basic", "하의", EquipmentPart.Boots),
                new EquipmentBaseItemDefinition("ring_basic", "장신구", EquipmentPart.Ring)
            };
        }

        public bool TryValidate(out string error)
        {
            var ids = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var parts = new HashSet<EquipmentPart>();
            for (var i = 0; i < baseItems.Count; i++)
            {
                var item = baseItems[i];
                if (item == null || string.IsNullOrWhiteSpace(item.Id))
                {
                    error = $"Equipment Catalog entry is invalid. Index={i}";
                    return false;
                }

                if (!ids.Add(item.Id))
                {
                    error = $"Equipment base ID is duplicated. Id={item.Id}";
                    return false;
                }

                if (!parts.Add(item.Part))
                {
                    error = $"Equipment part is duplicated. Part={item.Part}";
                    return false;
                }
            }

            error = null;
            return true;
        }
        // 지정한 부위에 해당하는 베이스 아이템을 찾는다 (같은 부위가 여러 개 등록돼 있으면 첫 번째를 사용).
        public EquipmentBaseItemDefinition FindBaseItemForPart(EquipmentPart part)
        {
            for (var i = 0; i < baseItems.Count; i++)
            {
                if (baseItems[i] != null && baseItems[i].Part == part)
                {
                    return baseItems[i];
                }
            }

            return null;
        }

        // 부위 + 등급 조합으로 최종 장비 정의를 만든다. 드랍/장착 등 모든 로직이 이 메서드를 거친다.
        public EquipmentDefinition GetDefinitionForPart(EquipmentPart part, EquipmentGrade grade)
        {
            return GetDefinitionForPart(part, grade, EquipmentBalanceConfig.RuntimeDefault);
        }

        public EquipmentDefinition GetDefinitionForPart(
            EquipmentPart part,
            EquipmentGrade grade,
            EquipmentBalanceConfig balance)
        {
            var baseItem = FindBaseItemForPart(part);
            var baseName = baseItem != null ? baseItem.DisplayName : EquipmentPartInfo.GetDisplayName(part);
            var baseId = baseItem != null ? baseItem.Id : part.ToString();
            var icon = baseItem != null ? baseItem.Icon : null;
            return new EquipmentDefinition(baseId, baseName, part, grade, icon, balance);
        }

        // 등급 키(예: "Weapon_Common")로 장비 정의를 만든다. 저장/로드 없이 세션 내에서만 쓰이지만,
        // 추후 저장 데이터를 붙일 때도 그대로 재사용할 수 있다.
        public EquipmentDefinition GetDefinitionByKey(string key)
        {
            return GetDefinitionByKey(key, EquipmentBalanceConfig.RuntimeDefault);
        }

        public EquipmentDefinition GetDefinitionByKey(string key, EquipmentBalanceConfig balance)
        {
            foreach (EquipmentPart part in System.Enum.GetValues(typeof(EquipmentPart)))
            {
                foreach (EquipmentGrade grade in System.Enum.GetValues(typeof(EquipmentGrade)))
                {
                    if ($"{part}_{grade}" == key)
                    {
                        return GetDefinitionForPart(part, grade, balance);
                    }
                }
            }

            return null;
        }

        // 현재 카탈로그가 만들어낼 수 있는 모든 장비 종류(부위 6 × 등급 5 = 최대 30개)를 나열한다.
        public IEnumerable<EquipmentDefinition> GetAllDefinitions()
        {
            return GetAllDefinitions(EquipmentBalanceConfig.RuntimeDefault);
        }

        public IEnumerable<EquipmentDefinition> GetAllDefinitions(EquipmentBalanceConfig balance)
        {
            foreach (EquipmentPart part in System.Enum.GetValues(typeof(EquipmentPart)))
            {
                if (FindBaseItemForPart(part) == null)
                {
                    continue; // 아직 등록되지 않은 부위는 건너뛴다
                }

                foreach (EquipmentGrade grade in System.Enum.GetValues(typeof(EquipmentGrade)))
                {
                    yield return GetDefinitionForPart(part, grade, balance);
                }
            }
        }
    }
}
