using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum MonsterRigMode // 첫 정식 Runtime이 지원하는 Mecanim Rig
    {
        Generic,
        Humanoid
    }

    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Body Profile", fileName = "MB_Monster")]
    public sealed class MonsterBodyProfile : ScriptableObject // 모델 크기·축·Socket 규격
    {
        [SerializeField] private Vector3 visualScale = Vector3.one;
        [SerializeField] private Vector3 visualLocalPosition;
        [SerializeField] private float groundOffset;
        [SerializeField] private float facingYawOffset;
        [SerializeField, Min(0.01f)] private float bodyRadius = 0.5f;
        [SerializeField, Min(0.01f)] private float bodyHeight = 1f;
        [SerializeField, Min(0.01f)] private float selectionRadius = 0.65f;
        [SerializeField, Min(0f)] private float hpBarHeight = 1.2f;
        [SerializeField] private string animatorPath;
        [SerializeField] private string attackOriginPath = "AttackOrigin";
        [SerializeField] private string hitCenterPath = "HitCenter";
        [SerializeField] private MonsterRigMode rigMode = MonsterRigMode.Generic;
        [SerializeField, Min(0.01f)] private float previewScale = 1f;
        [SerializeField, Min(0.01f)] private float vfxScale = 1f;

        public Vector3 VisualScale => visualScale;
        public Vector3 VisualLocalPosition => visualLocalPosition;
        public float GroundOffset => groundOffset;
        public float FacingYawOffset => facingYawOffset;
        public float BodyRadius => bodyRadius;
        public float BodyHeight => bodyHeight;
        public float SelectionRadius => selectionRadius;
        public float HpBarHeight => hpBarHeight;
        public string AnimatorPath => animatorPath ?? string.Empty;
        public string AttackOriginPath => attackOriginPath ?? string.Empty;
        public string HitCenterPath => hitCenterPath ?? string.Empty;
        public MonsterRigMode RigMode => rigMode;
        public float PreviewScale => previewScale;
        public float VfxScale => vfxScale;

        public bool TryValidate(out string error)
        {
            if (visualScale.x <= 0f || visualScale.y <= 0f || visualScale.z <= 0f)
            {
                error = $"Monster Body visual scale must be positive. Profile={name}";
                return false;
            }

            if (bodyRadius <= 0f || bodyHeight <= 0f || selectionRadius <= 0f ||
                hpBarHeight < 0f || previewScale <= 0f || vfxScale <= 0f)
            {
                error = $"Monster Body dimensions are invalid. Profile={name}";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Vector3 scale,
            Vector3 localPosition,
            float ground,
            float yaw,
            float radius,
            float height,
            float selectRadius,
            float healthBarHeight,
            string animator,
            string attackOrigin,
            string hitCenter,
            MonsterRigMode mode,
            float previewScaleMultiplier,
            float vfxScaleMultiplier)
        {
            visualScale = scale;
            visualLocalPosition = localPosition;
            groundOffset = ground;
            facingYawOffset = yaw;
            bodyRadius = radius;
            bodyHeight = height;
            selectionRadius = selectRadius;
            hpBarHeight = healthBarHeight;
            animatorPath = animator?.Trim();
            attackOriginPath = attackOrigin?.Trim();
            hitCenterPath = hitCenter?.Trim();
            rigMode = mode;
            previewScale = previewScaleMultiplier;
            vfxScale = vfxScaleMultiplier;
        }
#endif
    }
}
