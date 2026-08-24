using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    [DisallowMultipleComponent]
    public sealed class FallenCommanderBasicProjectileView : MonoBehaviour
    {
        // 기본 공격용 단색 구체를 생성하고 충돌체를 제거한다.
        public static FallenCommanderBasicProjectileView Create(
            Transform parent,
            Vector3 position,
            float radius,
            Color color)
        {
            var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "FallenCommanderBasicProjectile";
            projectile.transform.SetParent(parent, true);
            projectile.transform.position = position;
            projectile.transform.localScale =
                Vector3.one * Mathf.Max(0.1f, radius * 2f);

            var collider = projectile.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = projectile.GetComponent<Renderer>();
            if (renderer != null)
            {
                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_Color", color);
                propertyBlock.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(propertyBlock);
            }

            return projectile.AddComponent<FallenCommanderBasicProjectileView>();
        }

        // 기본 공격 구체를 다음 위치로 이동시킨다.
        public void MoveTo(Vector3 position)
        {
            transform.position = position;
        }
    }
}
