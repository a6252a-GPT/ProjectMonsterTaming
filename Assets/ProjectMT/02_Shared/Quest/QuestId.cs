using System;
using UnityEngine;

namespace ProjectMT.Shared.Quest
{
    // 문자열 대신 쓰는 퀘스트 식별자. SceneId·ContentId와 동일한 패턴.
    [Serializable]
    public struct QuestId : IEquatable<QuestId>
    {
        [SerializeField] private string value;

        public QuestId(string value)
        {
            this.value = value == null ? string.Empty : value.Trim();
        }

        public string Value => value ?? string.Empty;
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(QuestId other)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is QuestId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(QuestId left, QuestId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(QuestId left, QuestId right)
        {
            return !left.Equals(right);
        }
    }
}
