using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    /// <summary>
    /// 바닥만 BoxCollider를 둡니다. 벽 AABB는 문 구멍을 막아 옆방으로 못 가게 됩니다.
    /// </summary>
    internal static class DemoMapColliderBaker
    {
        public static void Bake(Transform mapRoot)
        {
            if (mapRoot == null)
            {
                return;
            }

            MeshFilter[] meshFilters = mapRoot.GetComponentsInChildren<MeshFilter>(true);
            int bakedCount = 0;

            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                if (!meshFilter.gameObject.name.StartsWith(DemoFloorBounds.FloorNamePrefix))
                {
                    continue;
                }

                if (TryBakeFloorCollider(meshFilter))
                {
                    bakedCount++;
                }
            }

            Debug.Log($"[DemoMapColliderBaker] 바닥 콜라이더 {bakedCount}개");
        }

        private static bool TryBakeFloorCollider(MeshFilter meshFilter)
        {
            Mesh mesh = meshFilter.sharedMesh;
            GameObject target = meshFilter.gameObject;

            BoxCollider boxCollider = target.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = target.AddComponent<BoxCollider>();
            }

            boxCollider.center = mesh.bounds.center;
            Vector3 size = mesh.bounds.size;
            size.y = Mathf.Max(size.y, 0.12f);
            boxCollider.size = size;
            boxCollider.isTrigger = false;
            boxCollider.enabled = true;
            return true;
        }
    }
}
