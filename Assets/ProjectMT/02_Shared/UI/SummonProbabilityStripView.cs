using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Shared.UI
{
    [DisallowMultipleComponent]
    public sealed class SummonProbabilityStripView : MonoBehaviour
    {
        public readonly struct Entry
        {
            public Entry(string label, float percent, Color color, Sprite icon = null)
            {
                Label = label;
                Percent = percent;
                Color = color;
                Icon = icon;
            }
            public string Label { get; }
            public float Percent { get; }
            public Color Color { get; }
            public Sprite Icon { get; }
        }

        [SerializeField] private RectTransform cellTemplate;
        private readonly List<RectTransform> cells = new List<RectTransform>();

        public void Show(IReadOnlyList<Entry> entries)
        {
            if (cellTemplate == null) return;
            if (cells.Count == 0) cells.Add(cellTemplate);
            var count = entries?.Count ?? 0;
            while (cells.Count < count)
            {
                var cell = Instantiate(cellTemplate, cellTemplate.parent);
                cell.name = $"ProbabilityCell_{cells.Count}";
                cells.Add(cell);
            }
            for (var index = 0; index < cells.Count; index++)
            {
                var cell = cells[index];
                cell.gameObject.SetActive(index < count);
                if (index >= count) continue;
                var entry = entries[index];
                cell.GetComponent<Image>().color = Color.Lerp(new Color(0.1f, 0.09f, 0.12f), entry.Color, 0.2f);
                cell.Find("Accent").GetComponent<Image>().color = entry.Color;
                var label = cell.Find("Name").GetComponent<TMP_Text>();
                label.text = entry.Label;
                label.color = entry.Color;
                cell.Find("Rate").GetComponent<TMP_Text>().text = $"{entry.Percent:0.##}%";
                var icon = cell.Find("Icon").GetComponent<Image>();
                icon.sprite = entry.Icon;
                icon.gameObject.SetActive(entry.Icon != null);
            }
        }
    }
}