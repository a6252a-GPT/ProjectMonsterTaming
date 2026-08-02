using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.CastleRaid
{
    [DisallowMultipleComponent]
    public sealed class CastleDeploymentZone : MonoBehaviour // 성 외곽 링 배치 판정
    {
        [SerializeField] private Vector2 outerHalfExtents = new Vector2(9.2f, 9.2f); // 배치 링 바깥 경계
        [SerializeField] private Vector2 innerHalfExtents = new Vector2(6.2f, 6.2f); // 성 주변 제외 경계
        [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 1f; // 걸을 수 있는 점 탐색 반경

        public Vector2 OuterHalfExtents => outerHalfExtents;
        public Vector2 InnerHalfExtents => innerHalfExtents;

        public bool ContainsWorldPosition(Vector3 worldPosition)
        {
            var local = transform.InverseTransformPoint(worldPosition);
            var insideOuter = Mathf.Abs(local.x) <= outerHalfExtents.x &&
                              Mathf.Abs(local.z) <= outerHalfExtents.y;
            var outsideInner = Mathf.Abs(local.x) >= innerHalfExtents.x ||
                               Mathf.Abs(local.z) >= innerHalfExtents.y;
            return insideOuter && outsideInner; // 두 사각형 사이만 허용
        }

        public bool TryResolveSpawnPoint(Camera worldCamera, Vector2 screenPosition, out Vector3 spawnPoint)
        {
            spawnPoint = default;
            if (worldCamera == null)
            {
                return false;
            }

            var plane = new Plane(transform.up, transform.position); // 화면 클릭을 맵 평면과 교차
            var ray = worldCamera.ScreenPointToRay(screenPosition);
            if (!plane.Raycast(ray, out var distance))
            {
                return false;
            }

            var worldPoint = ray.GetPoint(distance);
            if (!ContainsWorldPosition(worldPoint) ||
                !NavMesh.SamplePosition(worldPoint, out var hit, navMeshSampleRadius, NavMesh.AllAreas) ||
                !ContainsWorldPosition(hit.position))
            {
                return false;
            }

            spawnPoint = hit.position; // NavMesh 위 최종 소환점
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(Vector2 outer, Vector2 inner, float sampleRadius = 1f)
        {
            outerHalfExtents = outer;
            innerHalfExtents = inner;
            navMeshSampleRadius = Mathf.Max(0.1f, sampleRadius);
        }
#endif
    }
}
