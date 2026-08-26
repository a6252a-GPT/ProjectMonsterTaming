using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    [DisallowMultipleComponent]
    public sealed class FallenCommanderBasicProjectileView : MonoBehaviour
    {
        // 설정된 기본 공격 투사체 프리팹을 생성하고 미지정 시 기본 구체를 대신 사용한다.
        public static FallenCommanderBasicProjectileView Create(
            GameObject projectilePrefab,
            Transform parent,
            Vector3 position,
            float radius,
            Color color)
        {
            var usesFallbackSphere = projectilePrefab == null;
            var projectile = usesFallbackSphere
                ? GameObject.CreatePrimitive(PrimitiveType.Sphere)
                : Instantiate(projectilePrefab);
            projectile.name = "FallenCommanderBasicProjectile";
            projectile.transform.SetParent(parent, true);
            projectile.transform.position = position;
            projectile.transform.localScale *= Mathf.Max(0.1f, radius * 2f);

            foreach (var collider in projectile.GetComponentsInChildren<Collider>(true))
            {
                Destroy(collider);
            }

            if (usesFallbackSphere)
            {
                foreach (var renderer in projectile.GetComponentsInChildren<Renderer>(true))
                {
                    var propertyBlock = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor("_Color", color);
                    propertyBlock.SetColor("_BaseColor", color);
                    renderer.SetPropertyBlock(propertyBlock);
                }
            }

            return projectile.GetComponent<FallenCommanderBasicProjectileView>() ??
                projectile.AddComponent<FallenCommanderBasicProjectileView>();
        }

        // 기본 공격 구체를 다음 위치로 이동시킨다.
        public void MoveTo(Vector3 position)
        {
            transform.position = position;
        }
    }
}
