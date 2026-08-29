using UnityEngine;

namespace ProjectMT.Contents.CastleRaid.Generation
{
    [CreateAssetMenu(
        menuName = "ProjectMT/Castle Raid/Generation/District Template",
        fileName = "CastleDistrictTemplate")]
    public sealed class CastleDistrictTemplate : ScriptableObject // 성 구역의 재사용 배치 규칙
    {
        [SerializeField] private string templateId = "district_variable";
        [SerializeField, Min(4)] private int minimumWidth = 6;
        [SerializeField, Min(4)] private int maximumWidth = 10;
        [SerializeField, Min(4)] private int minimumHeight = 6;
        [SerializeField, Min(4)] private int maximumHeight = 10;
        [SerializeField, Range(1, 2)] private int wallLayers = 1;
        [SerializeField, Min(0)] private int minimumInteriorPlacements = 1;
        [SerializeField, Min(0)] private int maximumInteriorPlacements = 2;
        [SerializeField, Min(0.01f)] private float selectionWeight = 1f;
        [SerializeField] private bool palaceCore;
        [SerializeField] private bool supportsSpecialLoot = true;

        public string TemplateId => templateId;
        public int MinimumWidth => minimumWidth;
        public int MaximumWidth => maximumWidth;
        public int MinimumHeight => minimumHeight;
        public int MaximumHeight => maximumHeight;
        [System.Obsolete("고정 크기 프로토타입 호환용입니다.")]
        public int Width => minimumWidth;
        [System.Obsolete("고정 크기 프로토타입 호환용입니다.")]
        public int Height => minimumHeight;
        public bool HasFixedSize => minimumWidth == maximumWidth && minimumHeight == maximumHeight;
        public int WallLayers => wallLayers;
        public int MinimumInteriorPlacements => minimumInteriorPlacements;
        public int MaximumInteriorPlacements => maximumInteriorPlacements;
        public float SelectionWeight => selectionWeight;
        public bool IsPalaceCore => palaceCore;
        [System.Obsolete("외곽 빈 링 템플릿은 더 이상 사용하지 않습니다.")]
        public bool IsCastleEnvelope => false;
        public bool SupportsSpecialLoot => supportsSpecialLoot;

        public bool SupportsSize(int width, int height)
        {
            return SupportsUnrotated(width, height) || SupportsUnrotated(height, width);
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                error = "TemplateId가 비어 있습니다.";
                return false;
            }

            if (minimumWidth < 4 || minimumHeight < 4 ||
                maximumWidth < minimumWidth || maximumHeight < minimumHeight)
            {
                error = $"{templateId}: 가변 크기 범위가 잘못됐습니다.";
                return false;
            }

            if (wallLayers < 1 || wallLayers > 2 ||
                minimumWidth - wallLayers * 2 < 1 || minimumHeight - wallLayers * 2 < 1)
            {
                error = $"{templateId}: 성벽 층수 뒤에 내부 셀이 남아야 합니다.";
                return false;
            }

            if (minimumInteriorPlacements < 0 || maximumInteriorPlacements < minimumInteriorPlacements)
            {
                error = $"{templateId}: 내부 배치 수 범위가 잘못됐습니다.";
                return false;
            }

            var interiorCapacity =
                (maximumWidth - wallLayers * 2) * (maximumHeight - wallLayers * 2);
            if (maximumInteriorPlacements > interiorCapacity)
            {
                error = $"{templateId}: 내부 배치 최대 수가 실제 내부 셀 수를 초과합니다.";
                return false;
            }

            if (selectionWeight <= 0f)
            {
                error = $"{templateId}: 선택 가중치는 0보다 커야 합니다.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool SupportsUnrotated(int width, int height)
        {
            return width >= minimumWidth && width <= maximumWidth &&
                   height >= minimumHeight && height <= maximumHeight;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            int minimumTemplateWidth,
            int maximumTemplateWidth,
            int minimumTemplateHeight,
            int maximumTemplateHeight,
            int layers,
            int minimumPlacements,
            int maximumPlacements,
            float weight,
            bool isPalaceCore,
            bool allowSpecialLoot)
        {
            templateId = id;
            minimumWidth = minimumTemplateWidth;
            maximumWidth = maximumTemplateWidth;
            minimumHeight = minimumTemplateHeight;
            maximumHeight = maximumTemplateHeight;
            wallLayers = layers;
            minimumInteriorPlacements = minimumPlacements;
            maximumInteriorPlacements = maximumPlacements;
            selectionWeight = weight;
            palaceCore = isPalaceCore;
            supportsSpecialLoot = allowSpecialLoot;
        }
#endif
    }
}
