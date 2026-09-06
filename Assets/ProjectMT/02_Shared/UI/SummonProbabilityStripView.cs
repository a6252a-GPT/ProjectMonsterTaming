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
        [SerializeField] private RectTransform[] cells = System.Array.Empty<RectTransform>();
        [SerializeField] private Color backgroundColor = new Color(0.1f, 0.09f, 0.12f);
        [SerializeField, Range(0f, 1f)] private float rarityTint = 0.2f;

        public void Show(IReadOnlyList<Entry> entries)
        {
            if (cells == null || cells.Length == 0)
                throw new System.InvalidOperationException("Probability strip requires authored cells.");
            var count = entries?.Count ?? 0;
            if (count > cells.Length)
                throw new System.InvalidOperationException("Probability entries exceed authored cell capacity.");
            for (var index = 0; index < cells.Length; index++)
            {
                var cell = cells[index];
                cell.gameObject.SetActive(index < count);
                if (index >= count) continue;
                var entry = entries[index];
                cell.GetComponent<Image>().color = Color.Lerp(backgroundColor, entry.Color, rarityTint);
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