using System;
using UnityEditor;

namespace ProjectMT.EditorTools.MonsterMaker
{
    internal enum MonsterMakerPreviewPositionValueMode
    {
        RootLocal,
        VisualLocal,
        AnchorOffset
    }

    internal enum MonsterMakerPreviewReference
    {
        None,
        Model,
        Attack,
        Hit
    }

    internal sealed class MonsterMakerPreviewPositionBinding // V2 좌표 보정과 공용 Preview의 좁은 연결 계약
    {
        public MonsterMakerPreviewPositionBinding(
            string propertyPath,
            string label,
            MonsterMakerPreviewPositionValueMode valueMode,
            MonsterMakerPreviewAnchor anchor,
            string socketPath = null)
        {
            PropertyPath = propertyPath ?? string.Empty;
            Label = label ?? "위치";
            ValueMode = valueMode;
            Anchor = anchor;
            SocketPath = socketPath ?? string.Empty;
        }

        public string PropertyPath { get; }
        public string Label { get; }
        public MonsterMakerPreviewPositionValueMode ValueMode { get; }
        public MonsterMakerPreviewAnchor Anchor { get; }
        public string SocketPath { get; }

        public bool Matches(SerializedProperty property)
        {
            return property != null && string.Equals(
                PropertyPath,
                property.propertyPath,
                StringComparison.Ordinal);
        }
    }
}