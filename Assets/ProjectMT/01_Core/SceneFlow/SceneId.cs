using System;
using UnityEngine;

namespace ProjectMT.Core.SceneFlow
{
    [Serializable]
    public struct SceneId : IEquatable<SceneId> // 경로 대신 쓰는 씬 식별자
    {
        [SerializeField] private string value; // 직렬화용 원본 문자열

        public SceneId(string value)
        {
            this.value = value == null ? string.Empty : value.Trim();
        }

        public string Value => value ?? string.Empty;
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(SceneId other)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(Value, other.Value); // 대소문자 무시 비교
        }

        public override bool Equals(object obj)
        {
            return obj is SceneId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(SceneId left, SceneId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SceneId left, SceneId right)
        {
            return !left.Equals(right);
        }
    }
}
