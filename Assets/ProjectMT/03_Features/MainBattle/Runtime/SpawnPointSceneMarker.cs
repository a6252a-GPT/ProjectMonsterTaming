using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SpawnPointSceneMarker : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Color markerColor = Color.cyan;

        private Renderer[] markerRenderers;
        private MaterialPropertyBlock propertyBlock;
        private bool hasAppliedPlayState;
        private bool lastPlayState;

        private void OnEnable()
        {
            RefreshMarker();
        }

        private void OnValidate()
        {
            RefreshMarker();
        }

        private void Update()
        {
            if (!hasAppliedPlayState || lastPlayState != Application.isPlaying)
            {
                RefreshMarker();
            }
        }

        private void RefreshMarker()
        {
            markerRenderers = GetComponentsInChildren<Renderer>(true);
            bool isVisible = !Application.isPlaying;

            for (int i = 0; i < markerRenderers.Length; i++)
            {
                Renderer markerRenderer = markerRenderers[i];
                if (markerRenderer == null)
                {
                    continue;
                }

                markerRenderer.enabled = isVisible;
                if (isVisible)
                {
                    ApplyMarkerColor(markerRenderer);
                }
            }

            lastPlayState = Application.isPlaying;
            hasAppliedPlayState = true;
        }

        private void ApplyMarkerColor(Renderer markerRenderer)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            markerRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, markerColor);
            propertyBlock.SetColor(ColorId, markerColor);
            markerRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
