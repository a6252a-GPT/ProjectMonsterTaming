using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    [DisallowMultipleComponent]
    public sealed class FallenCommanderBasicProjectileView : MonoBehaviour
    {
        public static FallenCommanderBasicProjectileView Create(
            GameObject visualSource,
            Transform parent,
            Vector3 position,
            float radius,
            Color color)
        {
            var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "FallenCommanderBasicProjectile";
            projectile.transform.SetParent(parent, true);
            projectile.transform.position = position;
            projectile.transform.localScale = Vector3.one * Mathf.Max(0.1f, radius * 2f);

            var collider = projectile.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = projectile.GetComponent<Renderer>();
            var sourceRenderer = visualSource == null
                ? null
                : visualSource.GetComponentInChildren<Renderer>(true);
            if (renderer != null)
            {
                if (sourceRenderer != null && sourceRenderer.sharedMaterial != null)
                {
                    renderer.sharedMaterial = sourceRenderer.sharedMaterial;
                }

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_Color", color);
                propertyBlock.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(propertyBlock);
            }

            return projectile.AddComponent<FallenCommanderBasicProjectileView>();
        }

        public void MoveTo(Vector3 position)
        {
            transform.position = position;
        }
    }
}
