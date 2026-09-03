using System.Collections;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Pooling;
using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Shared.UI
{
    [DisallowMultipleComponent]
    public sealed class RewardAcquirePresenter : MonoBehaviour, IRewardPresentationPlayer // 저장 확정 보상만 표시
    {
        [SerializeField] private ScenePoolScope poolScope; // 지속형 UI Item 풀
        [SerializeField] private GameObject itemPrefab; // 정식 보상 획득 Item Prefab
        [SerializeField] private RectTransform displayRoot; // 전체 화면 좌표 기준
        [SerializeField] private RectTransform spawnAnchor; // 화면 중앙 시작점
        [SerializeField] private RectTransform targetAnchor; // 우상단 HUD 도착점
        [SerializeField] private SfxPool sfxPool; // 보상 UI 전용 Voice 풀
        [SerializeField] private SfxCue acquireSfx; // 실제 클립 연결 전에는 무음
        [SerializeField, Min(0.2f)] private float itemDuration = 0.9f; // 한 항목 이동 시간
        [SerializeField, Min(0f)] private float itemInterval = 0.1f; // 여러 항목 시작 간격

        private int activeItemCount; // 검증·동시 표시 상태

        public int ActiveItemCount => activeItemCount;

        private void OnDisable()
        {
            StopAllCoroutines();
            poolScope?.ReturnAll(false); // 부모 비활성화 중 Transform 이동 금지 회피
            activeItemCount = 0;
        }

        public void PlayConfirmed(RewardPresentationRequest request)
        {
            if (spawnAnchor == null)
            {
                return;
            }

            PlayConfirmed(request, spawnAnchor.position);
        }

        public void PlayConfirmed(RewardPresentationRequest request, Vector3 worldSpawnPosition)
        {
            if (request == null || request.IsEmpty || poolScope == null || itemPrefab == null || displayRoot == null ||
                targetAnchor == null || !isActiveAndEnabled)
            {
                return;
            }

            sfxPool?.Play(acquireSfx, transform.position);
            StartCoroutine(PlayItems(request, worldSpawnPosition));
        }

        private IEnumerator PlayItems(RewardPresentationRequest request, Vector3 worldSpawnPosition)
        {
            for (var i = 0; i < request.Items.Count; i++)
            {
                SpawnItem(request.Items[i], worldSpawnPosition);
                if (i + 1 < request.Items.Count && itemInterval > 0f)
                {
                    yield return new WaitForSecondsRealtime(itemInterval);
                }
            }
        }

        private void SpawnItem(RewardPresentationItem item, Vector3 worldSpawnPosition)
        {
            var instance = poolScope.Rent(itemPrefab, Vector3.zero, Quaternion.identity, displayRoot);
            var view = instance == null ? null : instance.GetComponent<RewardAcquireView>();
            if (view == null)
            {
                Debug.LogError("Reward acquire item prefab has no RewardAcquireView.");
                if (instance != null)
                {
                    poolScope.Return(instance);
                }

                return;
            }

            activeItemCount++;
            view.Play(
                poolScope,
                ResolveIcon(item),
                FormatLabel(item),
                ResolveColor(item.Kind),
                displayRoot.InverseTransformPoint(worldSpawnPosition),
                displayRoot.InverseTransformPoint(targetAnchor.position),
                itemDuration,
                HandleItemReleased);
        }

        private void HandleItemReleased()
        {
            activeItemCount = Mathf.Max(0, activeItemCount - 1);
        }

        private static string FormatLabel(RewardPresentationItem item)
        {
            var label = string.IsNullOrWhiteSpace(item.Label)
                ? item.Kind == RewardPresentationKind.Gold ? "골드" : "보상"
                : item.Label;
            return $"{label} +{item.Amount:N0}";
        }

        private static Sprite ResolveIcon(RewardPresentationItem item)
        {
            if (item.Icon != null)
            {
                return item.Icon;
            }

            var itemId = item.Kind == RewardPresentationKind.Gold ? ItemIds.Gold : item.ItemId;
            var catalog = ItemCatalogHub.Current;
            return !string.IsNullOrWhiteSpace(itemId) && catalog != null &&
                   catalog.TryGet(itemId, out var definition)
                ? definition.Icon
                : null;
        }

        private static Color ResolveColor(RewardPresentationKind kind)
        {
            switch (kind)
            {
                case RewardPresentationKind.CommanderExperience:
                    return new Color(0.4f, 0.9f, 1f, 1f);
                case RewardPresentationKind.Item:
                    return new Color(0.78f, 0.58f, 1f, 1f);
                default:
                    return new Color(1f, 0.82f, 0.22f, 1f);
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            ScenePoolScope pool,
            GameObject prefab,
            RectTransform root,
            RectTransform from,
            RectTransform to,
            SfxPool audioPool)
        {
            poolScope = pool;
            itemPrefab = prefab;
            displayRoot = root;
            spawnAnchor = from;
            targetAnchor = to;
            sfxPool = audioPool;
        }
#endif
    }
}
