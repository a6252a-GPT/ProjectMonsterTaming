using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    // 충전 광역기의 시간·전조·범위 판정과 런타임 정리를 전담한다.
    public sealed class FallenCommanderFinalChargePattern
    {
        private static readonly Color TelegraphColor =
            FallenCommanderTelegraphPalette.Danger;

        private Transform target;
        private FallenCommanderTelegraphView telegraph;
        private Vector3 centerPosition;
        private float duration;
        private float holdDuration;
        private float remainingTime;
        private float radius;

        public bool IsActive { get; private set; }
        public float RemainingTime => Mathf.Max(0f, remainingTime - holdDuration);
        public float Duration => duration;
        public float Radius => radius;
        public Vector3 CenterPosition => centerPosition;

        // 필요한 참조와 수치를 저장하고 보스 중심에 충전 전조를 생성한다.
        public bool Begin(
            Transform bossTransform,
            Transform targetTransform,
            GameObject telegraphPrefab,
            float chargeDuration,
            float completedHoldDuration,
            float chargeRadius)
        {
            Cancel();

            if (bossTransform == null ||
                targetTransform == null ||
                telegraphPrefab == null)
            {
                return false;
            }

            target = targetTransform;
            duration = Mathf.Max(0.1f, chargeDuration);
            holdDuration = Mathf.Max(0f, completedHoldDuration);
            radius = Mathf.Max(0.1f, chargeRadius);
            remainingTime = duration + holdDuration;
            centerPosition = bossTransform.position;
            IsActive = true;

            telegraph = FallenCommanderTelegraphView.CreateCircle(
                telegraphPrefab,
                bossTransform.parent,
                centerPosition,
                radius,
                TelegraphColor);
            telegraph?.SetProgress(0f);
            return true;
        }

        // 충전 시간과 전조 진행도를 갱신하고 충전 완료 여부를 반환한다.
        public bool Tick(float deltaTime)
        {
            if (!IsActive)
            {
                return false;
            }

            remainingTime = Mathf.Max(
                0f,
                remainingTime - Mathf.Max(0f, deltaTime));
            telegraph?.SetProgress(
                duration <= 0f
                    ? 1f
                    : 1f - RemainingTime / duration);
            return remainingTime <= 0f;
        }

        // 표시했던 원형 범위의 최종 적중 여부를 반환하고 패턴을 정리한다.
        public bool Complete(out bool targetInside)
        {
            if (!IsActive)
            {
                targetInside = false;
                return false;
            }

            targetInside = IsTargetInside();
            Cancel();
            return true;
        }

        // 진행 중인 충전과 생성된 전조 및 런타임 참조를 안전하게 정리한다.
        public void Cancel()
        {
            IsActive = false;
            remainingTime = 0f;

            if (telegraph != null)
            {
                Object.Destroy(telegraph.gameObject);
                telegraph = null;
            }

            target = null;
            centerPosition = Vector3.zero;
            duration = 0f;
            holdDuration = 0f;
            radius = 0f;
        }

        // 전조를 생성했던 고정 중심과 대상의 수평 거리를 비교한다.
        private bool IsTargetInside()
        {
            if (target == null)
            {
                return false;
            }

            var offset = target.position - centerPosition;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }
    }
}
