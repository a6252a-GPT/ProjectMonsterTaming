using ProjectMT.Shared.Items;
using UnityEngine;

namespace ProjectMT.Features.WorldDrops
{
    public interface IWorldDropPickupOwner
    {
        void CommitPickup(WorldItemDropView view);
    }

    [DisallowMultipleComponent]
    public sealed class WorldItemDropView : MonoBehaviour // 일반 아이템·장비 공용 투척·대기·흡수 풀 객체
    {
        private const float TossDuration = 0.38f;
        private const float TossHeight = 0.52f;
        private const float LandingPickupDelay = 0.55f;
        private const float AbsorbDuration = 0.2f;
        private const float IconWorldSize = 0.3f;

        private enum DropState
        {
            Inactive,
            Tossing,
            Waiting,
            Absorbing
        }

        [SerializeField] private Transform modelRoot;
        [SerializeField] private Transform iconRoot;
        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private Vector3 modelBaseEulerAngles;

        private IWorldDropPickupOwner owner;
        private Transform pickupTarget;
        private Camera worldCamera;
        private ItemAmount payload;
        private DropState state;
        private Vector3 spawnPosition;
        private Vector3 landingPosition;
        private Vector3 absorbStartPosition;
        private Vector3 fullScale = Vector3.one;
        private float stateElapsed;
        private float lifeElapsed;
        private float hoverPhase;
        private float spinSpeed;
        private bool collectionCommitted;

        public ItemAmount Payload => payload;
        public bool IsCollectionCommitted => collectionCommitted;

        internal void BuildTemplate(WorldItemDropVisualEntry visual)
        {
            if (visual == null || visual.ModelPrefab == null)
            {
                return;
            }

            var model = Instantiate(visual.ModelPrefab, transform);
            model.name = "Model";
            model.transform.localPosition = visual.LocalPosition;
            model.transform.localRotation = visual.LocalRotation;
            model.transform.localScale = visual.LocalScale;
            modelRoot = model.transform;
            modelBaseEulerAngles = modelRoot.localEulerAngles;

            var iconObject = new GameObject("ItemIcon");
            iconObject.transform.SetParent(transform, false);
            iconObject.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            iconRoot = iconObject.transform;
            iconRenderer = iconObject.AddComponent<SpriteRenderer>();
            iconRenderer.sortingOrder = 20;
        }

        internal void BuildTemplate(EquipmentDropChestVisualEntry visual)
        {
            if (visual == null || visual.ModelPrefab == null)
            {
                return;
            }

            var model = Instantiate(visual.ModelPrefab, transform);
            model.name = "Model";
            model.transform.localPosition = visual.LocalPosition;
            model.transform.localRotation = visual.LocalRotation;
            model.transform.localScale = visual.LocalScale;
            modelRoot = model.transform;
            modelBaseEulerAngles = modelRoot.localEulerAngles;
        }

        internal void Activate(
            WorldItemDropRuntime runtimeOwner,
            ItemAmount itemAmount,
            Vector3 position,
            Transform target,
            Camera camera,
            int sequence,
            Sprite icon)
        {
            ActivateCommon(runtimeOwner, position, target, camera, sequence, icon);
            payload = itemAmount;
        }

        internal void ActivateEquipment(
            IWorldDropPickupOwner runtimeOwner,
            Vector3 position,
            Transform target,
            Camera camera,
            int sequence)
        {
            ActivateCommon(runtimeOwner, position, target, camera, sequence, null);
            payload = default;
        }

        private void ActivateCommon(
            IWorldDropPickupOwner runtimeOwner,
            Vector3 position,
            Transform target,
            Camera camera,
            int sequence,
            Sprite icon)
        {
            owner = runtimeOwner;
            pickupTarget = target;
            worldCamera = camera;
            collectionCommitted = false;
            state = DropState.Tossing;
            stateElapsed = 0f;
            lifeElapsed = 0f;
            spawnPosition = position + Vector3.up * 0.08f;
            var angle = sequence * 137.50776f * Mathf.Deg2Rad;
            var radius = 0.12f + sequence % 4 * 0.055f;
            landingPosition = position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            hoverPhase = sequence * 0.73f;
            spinSpeed = 72f + sequence % 5 * 13f;
            fullScale = Vector3.one;
            transform.position = spawnPosition;
            transform.rotation = Quaternion.identity;
            transform.localScale = fullScale;

            if (modelRoot != null)
            {
                modelRoot.localEulerAngles = modelBaseEulerAngles;
            }

            if (iconRenderer != null)
            {
                iconRenderer.sprite = icon;
                iconRenderer.color = Color.white;
                ResizeIcon(icon);
            }
        }

        internal void ForceCollect()
        {
            CommitCollection();
        }

        private void Update()
        {
            if (owner == null || state == DropState.Inactive)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            stateElapsed += deltaTime;
            lifeElapsed += deltaTime;
            if (modelRoot != null)
            {
                modelRoot.Rotate(0f, spinSpeed * deltaTime, 0f, Space.Self);
            }

            switch (state)
            {
                case DropState.Tossing:
                    UpdateToss();
                    break;
                case DropState.Waiting:
                    UpdateWaiting();
                    break;
                case DropState.Absorbing:
                    UpdateAbsorb();
                    break;
            }
        }

        private void LateUpdate()
        {
            if (iconRoot == null || iconRenderer == null || iconRenderer.sprite == null)
            {
                return;
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (worldCamera != null)
            {
                var direction = worldCamera.transform.position - iconRoot.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    iconRoot.rotation = Quaternion.LookRotation(direction, worldCamera.transform.up);
                }
            }
        }

        private void UpdateToss()
        {
            var progress = Mathf.Clamp01(stateElapsed / TossDuration);
            var height = 4f * TossHeight * progress * (1f - progress);
            transform.position = Vector3.Lerp(spawnPosition, landingPosition, progress) + Vector3.up * height;
            if (progress < 1f)
            {
                return;
            }

            state = DropState.Waiting;
            stateElapsed = 0f;
            transform.position = landingPosition;
        }

        private void UpdateWaiting()
        {
            var hover = 0.045f + Mathf.Sin(lifeElapsed * 4.2f + hoverPhase) * 0.025f;
            transform.position = landingPosition + Vector3.up * hover;
            if (stateElapsed < LandingPickupDelay)
            {
                return;
            }

            if (pickupTarget == null)
            {
                CommitCollection(); // 대상이 사라져도 획득은 누락하지 않음
                return;
            }

            state = DropState.Absorbing;
            stateElapsed = 0f;
            absorbStartPosition = transform.position;
        }

        private void UpdateAbsorb()
        {
            if (pickupTarget == null)
            {
                CommitCollection(); // 흡수 중 대상 소실도 즉시 획득 확정
                return;
            }

            var progress = Mathf.Clamp01(stateElapsed / AbsorbDuration);
            var eased = 1f - (1f - progress) * (1f - progress);
            var targetPosition = pickupTarget.position + Vector3.up * 0.75f;
            transform.position = Vector3.Lerp(absorbStartPosition, targetPosition, eased);
            transform.localScale = Vector3.Lerp(fullScale, Vector3.zero, eased);
            if (progress >= 1f)
            {
                CommitCollection();
            }
        }

        private void CommitCollection()
        {
            if (collectionCommitted || owner == null)
            {
                return;
            }

            collectionCommitted = true; // 같은 풀 객체의 중복 획득 차단
            owner.CommitPickup(this);
        }

        private void ResizeIcon(Sprite icon)
        {
            if (iconRoot == null)
            {
                return;
            }

            if (icon == null)
            {
                iconRoot.localScale = Vector3.one;
                return;
            }

            var size = icon.bounds.size;
            var largest = Mathf.Max(size.x, size.y);
            var scale = largest <= 0.0001f ? 1f : IconWorldSize / largest;
            iconRoot.localScale = Vector3.one * scale;
        }

        private void OnDisable()
        {
            owner = null;
            pickupTarget = null;
            worldCamera = null;
            payload = default;
            state = DropState.Inactive;
            collectionCommitted = false;
            transform.localScale = Vector3.one;
            if (iconRenderer != null)
            {
                iconRenderer.sprite = null;
            }
        }
    }
}
