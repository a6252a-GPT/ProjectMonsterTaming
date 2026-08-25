using System;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [Flags]
    public enum BasicAttackFeelPlaybackOptions
    {
        None = 0,
        IncludeGlobalFeedback = 1 << 0
    }

    [Serializable]
    public sealed class BasicAttackFeelCue // VFX와 분리해 재생하는 FEEL 전용 프리셋 슬롯
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0.01f)] private float lifetime = 1f;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField, Min(0.01f)] private float scale = 1f;

        public GameObject Prefab => prefab;
        public BasicAttackFeelProfileMetadata ProfileMetadata =>
            prefab != null ? prefab.GetComponent<BasicAttackFeelProfileMetadata>() : null;
        public float Lifetime => ProfileMetadata != null
            ? ProfileMetadata.Lifetime
            : Mathf.Max(0.01f, lifetime);
        public Vector3 LocalPosition => ProfileMetadata != null
            ? ProfileMetadata.LocalPosition
            : localPosition;
        public Quaternion LocalRotation => ProfileMetadata != null
            ? ProfileMetadata.LocalRotation
            : Quaternion.Euler(localEulerAngles);
        public float Scale => ProfileMetadata != null
            ? ProfileMetadata.Scale
            : Mathf.Max(0.01f, scale);
        public bool HasFeel => prefab != null;

        public bool TryValidate(out string error)
        {
            if (prefab == null)
            {
                error = null;
                return true;
            }

            if (lifetime <= 0f || scale <= 0f)
            {
                error = $"Assigned FEEL preset requires positive lifetime and scale. Prefab={prefab.name}";
                return false;
            }

            var runtime = prefab.GetComponent(typeof(IBasicAttackFeelRuntime)) as IBasicAttackFeelRuntime;
            if (runtime == null)
            {
                error = $"Assigned FEEL preset root requires IBasicAttackFeelRuntime. Prefab={prefab.name}";
                return false;
            }
            if (!runtime.IsBasicAttackFeelConfigured)
            {
                error = $"Assigned FEEL preset runtime is not configured. Prefab={prefab.name}";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            GameObject feelPrefab,
            float playLifetime = 1f,
            Vector3 position = default,
            Vector3 eulerAngles = default,
            float scaleMultiplier = 1f)
        {
            prefab = feelPrefab;
            lifetime = Mathf.Max(0.01f, playLifetime);
            localPosition = position;
            localEulerAngles = eulerAngles;
            scale = Mathf.Max(0.01f, scaleMultiplier);
        }
#endif
    }

    public interface IBasicAttackFeelRuntime
    {
        bool IsBasicAttackFeelConfigured { get; }
        bool HasBasicAttackTargetMotion(float intensity = 1f);
        void PlayBasicAttackFeel(
            Vector3 position,
            GameObject target = null,
            float intensity = 1f,
            BasicAttackFeelPlaybackOptions options = BasicAttackFeelPlaybackOptions.IncludeGlobalFeedback);
        void ResetBasicAttackFeel();
    }
}
