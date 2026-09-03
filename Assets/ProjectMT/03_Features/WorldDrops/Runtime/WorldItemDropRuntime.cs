using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Pooling;
using UnityEngine;

namespace ProjectMT.Features.WorldDrops
{
    [DisallowMultipleComponent]
    public sealed class WorldItemDropRuntime : MonoBehaviour, IWorldDropPickupOwner // 표시 풀·획득 버퍼·묶음 저장 수명 관리
    {
        private readonly Dictionary<string, GameObject> templates =
            new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<WorldItemDropView> activeViews = new HashSet<WorldItemDropView>();
        private readonly WorldItemDropBuffer buffer = new WorldItemDropBuffer();
        private readonly SemaphoreSlim settlementGate = new SemaphoreSlim(1, 1);

        private IGameProgressService progress;
        private ItemCatalog itemCatalog;
        private WorldItemDropVisualCatalog visualCatalog;
        private Transform pickupTarget;
        private Camera worldCamera;
        private ScenePoolScope pool;
        private Transform templateRoot;
        private int spawnSequence;

        public int ActiveCount => activeViews.Count;
        public int TemplateCount => templates.Count;
        public int PendingItemTypeCount => buffer.ItemTypeCount;
        public event Action<IReadOnlyList<ItemAmount>> ItemsConfirmed;

        public static WorldItemDropRuntime Create(
            Transform parent,
            IGameProgressService progressService,
            ItemCatalog catalog,
            WorldItemDropVisualCatalog dropVisualCatalog,
            Transform target,
            Camera camera = null)
        {
            var root = new GameObject("WorldItemDropRuntime");
            root.transform.SetParent(parent, false);
            var runtime = root.AddComponent<WorldItemDropRuntime>();
            runtime.Initialize(progressService, catalog, dropVisualCatalog, target, camera);
            return runtime;
        }

        public void Initialize(
            IGameProgressService progressService,
            ItemCatalog catalog,
            WorldItemDropVisualCatalog dropVisualCatalog,
            Transform target,
            Camera camera = null)
        {
            ReturnAllUncollected();
            progress = progressService;
            itemCatalog = catalog;
            visualCatalog = dropVisualCatalog;
            pickupTarget = target;
            worldCamera = camera;
            pool ??= GetComponent<ScenePoolScope>() ?? gameObject.AddComponent<ScenePoolScope>();
            EnsureTemplateRoot();
        }

        public bool TrySpawn(WorldItemDropRequest request)
        {
            if (!request.IsValid || itemCatalog == null || visualCatalog == null ||
                !itemCatalog.TryGet(request.ItemId, out var definition) ||
                !visualCatalog.TryResolve(request.ItemId, out var visual) ||
                visual == null || visual.ModelPrefab == null)
            {
                return false;
            }

            var template = GetOrCreateTemplate(visual);
            if (template == null)
            {
                return false;
            }

            var instance = pool.Rent(template, request.Position, Quaternion.identity, transform);
            var view = instance == null ? null : instance.GetComponent<WorldItemDropView>();
            if (view == null)
            {
                if (instance != null)
                {
                    pool.Return(instance);
                }

                return false;
            }

            activeViews.Add(view);
            view.Activate(
                this,
                request.ToItemAmount(),
                request.Position,
                pickupTarget,
                worldCamera,
                ++spawnSequence,
                definition.Icon);
            return true;
        }

        public void CollectAllActive()
        {
            if (activeViews.Count == 0)
            {
                return;
            }

            var copy = new List<WorldItemDropView>(activeViews);
            for (var index = 0; index < copy.Count; index++)
            {
                copy[index]?.ForceCollect();
            }
        }

        public void ReturnAllUncollected()
        {
            if (activeViews.Count == 0)
            {
                return;
            }

            var copy = new List<WorldItemDropView>(activeViews);
            activeViews.Clear();
            for (var index = 0; index < copy.Count; index++)
            {
                var view = copy[index];
                if (view != null)
                {
                    pool?.Return(view.gameObject);
                }
            }
        }

        public bool TryGetPendingItems(out ItemAmount[] items)
        {
            return buffer.TryCreateSnapshot(out items);
        }

        public async Task<bool> FlushAsync()
        {
            var progressService = progress; // 씬 종료 중 참조가 해제돼도 시작한 정산은 같은 저장 계약으로 완료
            await settlementGate.WaitAsync();
            try
            {
                while (buffer.TryCreateSnapshot(out var snapshot))
                {
                    if (progressService == null)
                    {
                        return false;
                    }

                    bool saved;
                    try
                    {
                        saved = await progressService.TryApplyAndSaveAsync(GameProgressChange.GrantItems(snapshot));
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        return false;
                    }

                    if (!saved)
                    {
                        return false; // 실패한 Snapshot은 다음 체크포인트 재시도용으로 유지
                    }

                    if (!buffer.TryCommit(snapshot))
                    {
                        Debug.LogError("월드 드랍 저장 Snapshot을 획득 버퍼에서 확정하지 못했습니다.");
                        return false;
                    }

                    try
                    {
                        ItemsConfirmed?.Invoke(snapshot); // 저장 확정 뒤에만 획득 UI로 전달
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception); // 표시 실패가 이미 끝난 저장을 되돌리지 않음
                    }
                }

                return true;
            }
            finally
            {
                settlementGate.Release();
            }
        }

        internal void CommitPickup(WorldItemDropView view, ItemAmount amount)
        {
            if (view == null || !activeViews.Remove(view))
            {
                return;
            }

            if (!buffer.TryAdd(amount))
            {
                Debug.LogError($"월드 드랍 획득 버퍼 합산에 실패했습니다. Item={amount.ItemId}");
            }

            pool?.Return(view.gameObject); // 기록을 먼저 끝낸 뒤 풀에 반환
        }

        void IWorldDropPickupOwner.CommitPickup(WorldItemDropView view)
        {
            CommitPickup(view, view == null ? default : view.Payload);
        }

        private GameObject GetOrCreateTemplate(WorldItemDropVisualEntry visual)
        {
            if (templates.TryGetValue(visual.ItemId, out var template) && template != null)
            {
                return template;
            }

            EnsureTemplateRoot();
            template = new GameObject($"WorldDrop_{visual.ItemId}_Template");
            template.transform.SetParent(templateRoot, false);
            var view = template.AddComponent<WorldItemDropView>();
            view.BuildTemplate(visual);
            template.SetActive(false);
            templates[visual.ItemId] = template;
            return template;
        }

        private void EnsureTemplateRoot()
        {
            if (templateRoot != null)
            {
                return;
            }

            var root = new GameObject("Templates");
            root.transform.SetParent(transform, false);
            root.SetActive(false);
            templateRoot = root.transform;
        }

    }
}
