using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    /// <summary>FEEL 프로필 하나가 함께 보관하는 재생·타격점 기본값이다.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class BasicAttackFeelProfileMetadata : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float lifetime = 0.85f;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField, Min(0.01f)] private float scale = 1f;

        public float Lifetime => Mathf.Max(0.05f, lifetime);
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalEulerAngles => localEulerAngles;
        public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
        public float Scale => Mathf.Max(0.01f, scale);

#if UNITY_EDITOR
        public void EditorConfigure(
            float playLifetime,
            Vector3 position,
            Vector3 eulerAngles,
            float scaleMultiplier)
        {
            lifetime = Mathf.Max(0.05f, playLifetime);
            localPosition = position;
            localEulerAngles = eulerAngles;
            scale = Mathf.Max(0.01f, scaleMultiplier);
        }
#endif

        private void OnValidate()
        {
            lifetime = Mathf.Max(0.05f, lifetime);
            scale = Mathf.Max(0.01f, scale);
        }
    }
}
