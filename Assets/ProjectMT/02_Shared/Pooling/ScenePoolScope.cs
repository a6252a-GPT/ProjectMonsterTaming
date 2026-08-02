using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.Pooling
{
    [DisallowMultipleComponent]
    public sealed class ScenePoolScope : MonoBehaviour // 현재 실행 영역 전용 풀
    {
        private readonly Dictionary<GameObject, Queue<GameObject>> available = new Dictionary<GameObject, Queue<GameObject>>(); // Prefab별 대기열
        private readonly HashSet<GameObject> active = new HashSet<GameObject>(); // 현재 대여 중 객체

        public int ActiveCount => active.Count;

        public GameObject Rent(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
            {
                return null;
            }

            if (!available.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                available.Add(prefab, queue);
            }

            GameObject instance = null;
            while (queue.Count > 0 && instance == null)
            {
                instance = queue.Dequeue();
            }

            if (instance == null)
            {
                instance = Instantiate(prefab); // 대기 객체가 없을 때만 생성
                instance.SetActive(false);
                var marker = instance.GetComponent<PooledInstance>();
                if (marker == null)
                {
                    marker = instance.AddComponent<PooledInstance>();
                }

                marker.Configure(this, prefab);
            }
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            active.Add(instance);
            instance.SetActive(true);
            return instance;
        }

        public void Return(GameObject instance)
        {
            if (instance == null || !active.Remove(instance))
            {
                return;
            }

            var marker = instance.GetComponent<PooledInstance>();
            if (marker == null || marker.SourcePrefab == null || marker.Owner != this)
            {
                Destroy(instance); // 다른 풀 객체는 안전하게 폐기
                return;
            }

            instance.SetActive(false);
            instance.transform.SetParent(transform, false);
            if (!available.TryGetValue(marker.SourcePrefab, out var queue))
            {
                queue = new Queue<GameObject>();
                available.Add(marker.SourcePrefab, queue);
            }

            queue.Enqueue(instance); // 원본 Prefab 대기열로 복귀
        }

        public void ReturnAll()
        {
            if (active.Count == 0)
            {
                return;
            }

            var buffer = new List<GameObject>(active); // 반환 중 컬렉션 변경 방지
            foreach (var instance in buffer)
            {
                Return(instance);
            }
        }
    }

    public sealed class PooledInstance : MonoBehaviour // 풀 소유권 표식
    {
        public ScenePoolScope Owner { get; private set; } // 반환할 풀
        public GameObject SourcePrefab { get; private set; } // 원본 Prefab

        public void Configure(ScenePoolScope owner, GameObject sourcePrefab)
        {
            Owner = owner;
            SourcePrefab = sourcePrefab;
        }
    }
}
