using System.Collections;
using UnityEngine;

namespace ProjectMT.Tools.FeelPreview
{
    /// <summary>70종 단독 실행과 즉시 원복 계약을 PlayMode에서 전수 검사한다.</summary>
    public sealed partial class CombatFeelCatalogPreviewLab
    {
        private Coroutine diagnosticSweep;
        public bool DiagnosticRunning => diagnosticSweep != null;
        public int DiagnosticPassed { get; private set; }
        public int DiagnosticFailed { get; private set; }
        public string DiagnosticReport { get; private set; } = "미실행";

        public void StartDiagnosticSweep(float sampleDuration = 0.16f)
        {
            if (diagnosticSweep != null) StopCoroutine(diagnosticSweep);
            diagnosticSweep = StartCoroutine(DiagnosticSweep(Mathf.Clamp(sampleDuration, 0.05f, 0.5f)));
        }

        private IEnumerator DiagnosticSweep(float sampleDuration)
        {
            ResetPreview();
            CacheReferences();
            DiagnosticPassed = 0;
            DiagnosticFailed = 0;
            DiagnosticReport = "실행 중";
            var failures = new System.Collections.Generic.List<string>();
            var baselinePosition = visual != null ? visual.localPosition : Vector3.zero;
            var baselineRotation = visual != null ? visual.localRotation : Quaternion.identity;
            var baselineScale = visual != null ? visual.localScale : Vector3.one;

            foreach (var item in Items)
            {
                PreviewEffect(item.TypeName);
                yield return Wait(sampleDuration);
                ResetPreview();
                yield return null;

                var restored = visual != null
                               && Vector3.Distance(visual.localPosition, baselinePosition) < 0.0001f
                               && Quaternion.Angle(visual.localRotation, baselineRotation) < 0.01f
                               && Vector3.Distance(visual.localScale, baselineScale) < 0.0001f
                               && Mathf.Approximately(Time.timeScale, 1f)
                               && transientObjects.Count == 0
                               && playing == null
                               && volumeRoot == null
                               && volumeProfile == null
                               && CameraRestoredForDiagnostic()
                               && RenderersRestoredForDiagnostic();
                if (restored) DiagnosticPassed++;
                else
                {
                    DiagnosticFailed++;
                    failures.Add(item.TypeName);
                }
            }

            ResetPreview();
            DiagnosticReport = $"FEEL_CATALOG_SWEEP {DiagnosticPassed}/{Items.Length}, failed={DiagnosticFailed}"
                               + (failures.Count > 0 ? $", failures={string.Join(",", failures)}" : string.Empty);
            Debug.Log(DiagnosticReport);
            diagnosticSweep = null;
        }

        private bool CameraRestoredForDiagnostic()
        {
            if (previewCamera == null) return false;
            return Vector3.Distance(previewCamera.transform.position, cameraPosition) < 0.0001f
                   && Quaternion.Angle(previewCamera.transform.rotation, cameraRotation) < 0.01f
                   && Mathf.Abs(previewCamera.fieldOfView - cameraFov) < 0.001f
                   && Mathf.Abs(previewCamera.nearClipPlane - cameraNear) < 0.001f
                   && Mathf.Abs(previewCamera.farClipPlane - cameraFar) < 0.01f
                   && previewCamera.orthographic == cameraOrthographic
                   && Mathf.Abs(previewCamera.orthographicSize - cameraOrthographicSize) < 0.001f;
        }

        private bool RenderersRestoredForDiagnostic()
        {
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                if (demoRendererEnabled.TryGetValue(renderer, out var enabled) && renderer.enabled != enabled) return false;
            }
            return true;
        }
    }
}
