using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander.Editor
{
    [InitializeOnLoad]
    internal static class FallenCommanderPhaseTransitionEditorPreview
    {
        private static GameObject previewRoot;
        private static GameObject sourceBoss;
        private static GameObject replacementBoss;
        private static FallenCommanderBossTransformationVisual sourceVisual;
        private static FallenCommanderBossTransformationVisual replacementVisual;
        private static FallenCommanderPhaseData activePhaseData;
        private static FallenCommanderPhaseConfig lastConfig;
        private static FallenCommanderBossPhase lastPhase;
        private static GameObject lastBaseBossPrefab;
        private static Transform activeSpawnPoint;
        private static Transform lastSpawnPoint;
        private static Vector3 replacementScale = Vector3.one;
        private static Color screenFadeColor = Color.black;
        private static float screenFadeAlpha = 1f;
        private static float screenFadeDuration = 0.15f;
        private static double startedAt;
        private static bool transitionCompleted;

        static FallenCommanderPhaseTransitionEditorPreview()
        {
            EditorApplication.update += Update;
            SceneView.duringSceneGui += DrawScreenFade;
            EditorApplication.playModeStateChanged += _ => Stop();
            EditorApplication.quitting += Stop;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }

        public static bool Play(
            FallenCommanderPhaseConfig config,
            FallenCommanderBossPhase targetPhase,
            GameObject baseBossPrefab,
            Transform spawnPoint,
            Color fadeColor,
            float fadeAlpha,
            float fadeDuration)
        {
            Stop();
            var targetData = config?.GetPhase(targetPhase);
            if (PrefabStageUtility.GetCurrentPrefabStage() != null ||
                targetData == null ||
                baseBossPrefab == null)
            {
                return false;
            }

            GameObject createdRoot = null;
            var initialized = false;
            try
            {
                createdRoot = new GameObject("[페이즈 전환 미리보기]")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                var position = spawnPoint == null ? Vector3.zero : spawnPoint.position;
                var rotation = spawnPoint == null ? Quaternion.identity : spawnPoint.rotation;
                createdRoot.transform.SetPositionAndRotation(position, rotation);

                var sourcePrefab = ResolveBossPrefab(
                    config,
                    (FallenCommanderBossPhase)((int)targetPhase - 1),
                    baseBossPrefab);
                var replacementPrefab = ResolveBossPrefab(
                    config,
                    targetPhase,
                    baseBossPrefab);
                var createdSource = InstantiateBoss(sourcePrefab, createdRoot.transform);
                var createdReplacement = InstantiateBoss(replacementPrefab, createdRoot.transform);
                if (createdSource == null || createdReplacement == null)
                {
                    return false;
                }

                var sourceData = config.GetPhase(
                    (FallenCommanderBossPhase)((int)targetPhase - 1));
                createdSource.transform.localScale *=
                    sourceData?.BossScaleMultiplier ?? 1f;
                replacementScale = createdReplacement.transform.localScale *
                    targetData.BossScaleMultiplier;
                createdReplacement.transform.localScale = replacementScale * 0.75f;

                sourceBoss = createdSource;
                replacementBoss = createdReplacement;
                previewRoot = createdRoot;
                sourceVisual = new FallenCommanderBossTransformationVisual(sourceBoss.transform);
                replacementVisual = new FallenCommanderBossTransformationVisual(
                    replacementBoss.transform);
                sourceVisual.SetVisibility(1f);
                replacementVisual.SetVisibility(0f);
                activePhaseData = targetData;
                activeSpawnPoint = spawnPoint;
                lastConfig = config;
                lastPhase = targetPhase;
                lastBaseBossPrefab = baseBossPrefab;
                lastSpawnPoint = spawnPoint;
                screenFadeColor = fadeColor;
                screenFadeAlpha = Mathf.Clamp01(fadeAlpha);
                screenFadeDuration = Mathf.Max(0.01f, fadeDuration);
                startedAt = EditorApplication.timeSinceStartup;
                transitionCompleted = false;
                initialized = true;
                SceneView.RepaintAll();
                return true;
            }
            finally
            {
                if (!initialized && createdRoot != null)
                {
                    Object.DestroyImmediate(createdRoot);
                }
            }
        }

        public static bool Restart()
        {
            return lastConfig != null &&
                Play(
                    lastConfig,
                    lastPhase,
                    lastBaseBossPrefab,
                    lastSpawnPoint,
                    screenFadeColor,
                    screenFadeAlpha,
                    screenFadeDuration);
        }

        public static void Stop()
        {
            sourceVisual?.Restore();
            replacementVisual?.Restore();
            if (previewRoot != null)
            {
                Object.DestroyImmediate(previewRoot);
            }

            previewRoot = null;
            sourceBoss = null;
            replacementBoss = null;
            sourceVisual = null;
            replacementVisual = null;
            activePhaseData = null;
            activeSpawnPoint = null;
            replacementScale = Vector3.one;
            startedAt = 0d;
            transitionCompleted = false;
            SceneView.RepaintAll();
        }

        private static void Update()
        {
            if (previewRoot == null || activePhaseData == null)
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Stop();
                return;
            }

            if (activeSpawnPoint != null)
            {
                previewRoot.transform.SetPositionAndRotation(
                    activeSpawnPoint.position,
                    activeSpawnPoint.rotation);
            }

            var now = EditorApplication.timeSinceStartup;
            var elapsed = (float)(now - startedAt);
            if (!transitionCompleted && elapsed >= screenFadeDuration)
            {
                sourceVisual?.Restore();
                sourceBoss.SetActive(false);
                replacementVisual?.Restore();
                replacementBoss.transform.localScale = replacementScale;
                transitionCompleted = true;
            }

            SceneView.RepaintAll();
        }

        private static void DrawScreenFade(SceneView sceneView)
        {
            if (previewRoot == null || activePhaseData == null ||
                activePhaseData.Phase <= FallenCommanderBossPhase.Phase1)
            {
                return;
            }

            var elapsed = (float)(EditorApplication.timeSinceStartup - startedAt);
            var transitionDuration = Mathf.Max(0.05f, activePhaseData.TransitionDuration);
            var fadeDuration = Mathf.Max(0.01f, screenFadeDuration);
            var alpha = elapsed < fadeDuration
                ? screenFadeAlpha * (elapsed / fadeDuration)
                : elapsed < transitionDuration
                    ? screenFadeAlpha
                    : screenFadeAlpha * (1f - Mathf.Clamp01(
                        (elapsed - transitionDuration) / fadeDuration));
            if (alpha <= 0.001f)
            {
                return;
            }

            var color = screenFadeColor;
            color.a = Mathf.Clamp01(alpha);
            Handles.BeginGUI();
            EditorGUI.DrawRect(
                new Rect(0f, 0f, sceneView.position.width, sceneView.position.height),
                color);
            Handles.EndGUI();
        }

        private static GameObject InstantiateBoss(GameObject prefab, Transform parent)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                return null;
            }

            SetHideFlags(instance);
            var behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            for (var index = 0; index < behaviours.Length; index++)
            {
                behaviours[index].enabled = false;
            }

            return instance;
        }

        private static GameObject ResolveBossPrefab(
            FallenCommanderPhaseConfig config,
            FallenCommanderBossPhase phase,
            GameObject baseBossPrefab)
        {
            var resolved = baseBossPrefab;
            for (var phaseNumber = (int)FallenCommanderBossPhase.Phase1;
                 phaseNumber <= (int)phase;
                 phaseNumber++)
            {
                var data = config.GetPhase((FallenCommanderBossPhase)phaseNumber);
                if (data?.BossPrefabOverride != null)
                {
                    resolved = data.BossPrefabOverride;
                }
            }

            return resolved;
        }

        private static void SetHideFlags(GameObject root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < transforms.Length; index++)
            {
                transforms[index].gameObject.hideFlags = HideFlags.HideAndDontSave;
            }
        }
    }
}
