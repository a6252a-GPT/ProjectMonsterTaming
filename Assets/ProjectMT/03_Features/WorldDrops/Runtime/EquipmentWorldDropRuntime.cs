using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Pooling;
using UnityEngine;

namespace ProjectMT.Features.WorldDrops
{
    [DisallowMultipleComponent]
    public sealed class EquipmentWorldDropRuntime : MonoBehaviour, IWorldDropPickupOwner // 장비 상자·고유 인스턴스 저장 관리
    {
        private readonly Dictionary<EquipmentGrade, GameObject> templates =
            new Dictionary<EquipmentGrade, GameObject>();
        private readonly Dictionary<WorldItemDropView, EquipmentInstanceData> activePayloads =
            new Dictionary<WorldItemDropView, EquipmentInstanceData>();
        private readonly HashSet<string> reservedInstanceIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly EquipmentDropBuffer buffer = new EquipmentDropBuffer();
        private readonly SemaphoreSlim settlementGate = new SemaphoreSlim(1, 1);

        private IGameProgressService progress;
        private EquipmentDropChestVisualCatalog visualCatalog;
        private Transform pickupTarget;
        private Camera worldCamera;
        private ScenePoolScope pool;
        private Transform templateRoot;
        private int spawnSequence;

        public int ActiveCount => activePayloads.Count;
        public int TemplateCount => templates.Count;
        public int PendingCount => buffer.Count;
        public int ReservedCount => reservedInstanceIds.Count;
        public event Action<IReadOnlyList<EquipmentInstanceData>> EquipmentConfirmed;
        public int AvailableCapacity => Mathf.Max(
            0,
            EquipmentSaveData.MaxTotalQuantity - ResolveOwnedCount() - reservedInstanceIds.Count);

        public static EquipmentWorldDropRuntime Create(
            Transform parent,
            IGameProgressService progressService,
            EquipmentDropChestVisualCatalog dropVisualCatalog,
            Transform target,
            Camera camera = null)
        {
            var root = new GameObject("EquipmentWorldDropRuntime");
            root.transform.SetParent(parent, false);
            var runtime = root.AddComponent<EquipmentWorldDropRuntime>();
            runtime.Initialize(progressService, dropVisualCatalog, target, camera);
            return runtime;
        }

        public void Initialize(
            IGameProgressService progressService,
            EquipmentDropChestVisualCatalog dropVisualCatalog,
            Transform target,
            Camera camera = null)
        {
            ReturnAllUncollected();
            progress = progressService;
            visualCatalog = dropVisualCatalog;
            pickupTarget = target;
            worldCamera = camera;
            pool ??= GetComponent<ScenePoolScope>() ?? gameObject.AddComponent<ScenePoolScope>();
            EnsureTemplateRoot();
        }

        public bool TrySpawn(EquipmentWorldDropRequest request)
        {
            if (!request.IsValid || AvailableCapacity <= 0 || visualCatalog == null ||
                reservedInstanceIds.Contains(request.Instance.InstanceId) ||
                !visualCatalog.TryResolve(request.Instance.Grade, out var visual) ||
                visual == null || visual.ModelPrefab == null)
            {
                return false;
            }

            var template = GetOrCreateTemplate(visual);
            if (template == null)
            {
                return false;
            }

            var instanceObject = pool.Rent(template, request.Position, Quaternion.identity, transform);
            var view = instanceObject == null ? null : instanceObject.GetComponent<WorldItemDropView>();
            if (view == null)
            {
                if (instanceObject != null)
                {
                    pool.Return(instanceObject);
                }

                return false;
            }

            var payload = request.Instance.Clone();
            activePayloads.Add(view, payload);
            reservedInstanceIds.Add(payload.InstanceId);
            view.ActivateEquipment(this, request.Position, pickupTarget, worldCamera, ++spawnSequence);
            ProjectMT.Shared.Audio.SfxEvents.Play2D(ProjectMT.Shared.Audio.SfxEvents.DropSpawn);
            return true;
        }

        public void CollectAllActive()
        {
            if (activePayloads.Count == 0)
            {
                return;
            }

            var copy = new List<WorldItemDropView>(activePayloads.Keys);
            for (var index = 0; index < copy.Count; index++)
            {
                copy[index]?.ForceCollect();
            }
        }

        public void ReturnAllUncollected()
        {
            if (activePayloads.Count == 0)
            {
                return;
            }

            var copy = new List<KeyValuePair<WorldItemDropView, EquipmentInstanceData>>(activePayloads);
            activePayloads.Clear();
            for (var index = 0; index < copy.Count; index++)
            {
                var pair = copy[index];
                if (pair.Value != null)
                {
                    reservedInstanceIds.Remove(pair.Value.InstanceId);
                }

                if (pair.Key != null)
                {
                    pool?.Return(pair.Key.gameObject);
                }
            }
        }

        public bool TryGetPendingEquipment(out List<EquipmentInstanceData> equipment)
        {
            return buffer.TryCreateSnapshot(out equipment);
        }

        public async Task<bool> FlushAsync()
        {
            var progressService = progress; // 씬 종료 중에도 시작한 저장 계약 유지
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
                        saved = await progressService.TryApplyAndSaveAsync(
                            GameProgressChange.AcquireEquipment(snapshot));
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        return false;
                    }

                    if (!saved)
                    {
                        return false; // 실패한 고유 인스턴스는 다음 체크포인트까지 유지
                    }

                    if (!buffer.TryCommit(snapshot))
                    {
                        Debug.LogError("장비 드랍 저장 Snapshot을 획득 버퍼에서 확정하지 못했습니다.");
                        return false;
                    }

                    for (var index = 0; index < snapshot.Count; index++)
                    {
                        reservedInstanceIds.Remove(snapshot[index].InstanceId);
                    }

                    try
                    {
                        EquipmentConfirmed?.Invoke(snapshot); // 고유 장비 저장 확정 뒤에만 획득 UI로 전달
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

        void IWorldDropPickupOwner.CommitPickup(WorldItemDropView view)
        {
            if (view == null || !activePayloads.TryGetValue(view, out var payload))
            {
                return;
            }

            activePayloads.Remove(view);
            if (!buffer.TryAdd(payload))
            {
                reservedInstanceIds.Remove(payload.InstanceId);
                Debug.LogError($"장비 드랍 획득 버퍼 추가에 실패했습니다. Instance={payload.InstanceId}");
            }

            pool?.Return(view.gameObject);
        }

        private int ResolveOwnedCount()
        {
            return progress == null ? 0 : progress.View.Equipment.Instances.Count;
        }

        private GameObject GetOrCreateTemplate(EquipmentDropChestVisualEntry visual)
        {
            if (templates.TryGetValue(visual.Grade, out var template) && template != null)
            {
                return template;
            }

            EnsureTemplateRoot();
            template = new GameObject($"EquipmentDrop_{visual.Grade}_Template");
            template.transform.SetParent(templateRoot, false);
            var view = template.AddComponent<WorldItemDropView>();
            view.BuildTemplate(visual);
            template.SetActive(false);
            templates[visual.Grade] = template;
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
