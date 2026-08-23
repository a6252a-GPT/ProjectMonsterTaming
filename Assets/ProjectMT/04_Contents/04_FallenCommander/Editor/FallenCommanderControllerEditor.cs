using UnityEditor;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander.Editor
{
    [CustomEditor(typeof(FallenCommanderController))]
    public sealed class FallenCommanderControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty bossPrefabProperty;
        private SerializedProperty bossSpawnPointProperty;
        private SerializedProperty bossConfigProperty;

        private void OnEnable()
        {
            bossPrefabProperty = serializedObject.FindProperty("bossPrefab");
            bossSpawnPointProperty = serializedObject.FindProperty("bossSpawnPoint");
            bossConfigProperty = serializedObject.FindProperty("bossConfig");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Boss Motion Preview", EditorStyles.boldLabel);

            var config = bossConfigProperty.objectReferenceValue as FallenCommanderBossConfig;
            if (config == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a Fallen Commander Boss Config to enable motion previews.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                Application.isPlaying
                    ? "Play Mode: preview plays on the spawned boss."
                    : "Edit Mode: preview creates a temporary boss in the Scene view.",
                MessageType.None);

            DrawAttackPreview("Melee Attack", config.MeleeAttack);
            DrawAttackPreview("Mark Strike", config.MarkStrike);
            DrawAttackPreview("Wide Burst", config.WideBurst);
            DrawAttackPreview("Line Strike", config.LineStrike);
            DrawMotionPreview("Boss Break", config.BreakMotion, config.BreakMotionDuration);
            DrawMotionPreview("Boss Death", config.DeathMotion, config.DeathMotionDuration);

            if (GUILayout.Button("Stop Motion Preview"))
            {
                FallenCommanderBossEditorPreview.Stop();
            }
        }

        private void DrawAttackPreview(string label, FallenCommanderAttackData attack)
        {
            if (attack == null)
            {
                return;
            }

            EditorGUILayout.LabelField(
                $"{label}  ({attack.PreCastMotionDuration:0.###}s → {attack.CastMotionDuration:0.###}s)",
                EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"▶ {label}: Pre Cast → Cast"))
                {
                    PreviewAttack(attack);
                }

                if (GUILayout.Button("Pre Cast", GUILayout.Width(72f)))
                {
                    PreviewMotion(attack.PreCastMotion, attack.PreCastMotionDuration);
                }

                if (GUILayout.Button("Cast", GUILayout.Width(52f)))
                {
                    PreviewMotion(attack.CastMotion, attack.CastMotionDuration);
                }
            }
        }

        private void DrawMotionPreview(string label, AnimationClip motion, float duration)
        {
            if (motion == null)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"{label} ({duration:0.###}s)",
                    GUILayout.Width(190f));
                if (GUILayout.Button($"▶ {label}"))
                {
                    PreviewMotion(motion, duration);
                }
            }
        }

        private void PreviewAttack(FallenCommanderAttackData attack)
        {
            if (Application.isPlaying)
            {
                if (!((FallenCommanderController)target).PreviewBossAttack(attack))
                {
                    EditorUtility.DisplayDialog(
                        "Boss Motion Preview",
                        "Enter the dungeon first so the boss can be spawned.",
                        "OK");
                }

                return;
            }

            FallenCommanderBossEditorPreview.PlaySequence(
                GetBossPrefab(),
                GetSpawnPoint(),
                attack.PreCastMotion,
                attack.PreCastMotionDuration,
                attack.CastMotion,
                attack.CastMotionDuration);
        }

        private void PreviewMotion(AnimationClip motion, float duration)
        {
            if (motion == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                if (!((FallenCommanderController)target).PreviewBossMotion(motion, duration))
                {
                    EditorUtility.DisplayDialog(
                        "Boss Motion Preview",
                        "Enter the dungeon first so the boss can be spawned.",
                        "OK");
                }

                return;
            }

            FallenCommanderBossEditorPreview.PlaySequence(
                GetBossPrefab(),
                GetSpawnPoint(),
                motion,
                duration,
                null,
                0f);
        }

        private GameObject GetBossPrefab()
        {
            return bossPrefabProperty.objectReferenceValue as GameObject;
        }

        private Transform GetSpawnPoint()
        {
            return bossSpawnPointProperty.objectReferenceValue as Transform;
        }
    }

    [InitializeOnLoad]
    internal static class FallenCommanderBossEditorPreview
    {
        private static GameObject previewRoot;
        private static AnimationClip firstMotion;
        private static AnimationClip secondMotion;
        private static float firstDuration;
        private static float secondDuration;
        private static double lastTime;
        private static float elapsed;

        static FallenCommanderBossEditorPreview()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += _ => Stop();
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }

        public static void PlaySequence(
            GameObject prefab,
            Transform spawnPoint,
            AnimationClip first,
            float firstLength,
            AnimationClip second,
            float secondLength)
        {
            Stop();
            if (prefab == null || (first == null && second == null))
            {
                return;
            }

            previewRoot = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (previewRoot == null)
            {
                return;
            }

            previewRoot.name = $"[Motion Preview] {prefab.name}";
            previewRoot.hideFlags = HideFlags.DontSave;
            if (spawnPoint != null)
            {
                previewRoot.transform.SetPositionAndRotation(
                    spawnPoint.position,
                    spawnPoint.rotation);
            }

            foreach (var behaviour in previewRoot.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            firstMotion = first == null ? second : first;
            secondMotion = first == null ? null : second;
            firstDuration = ResolveDuration(
                firstMotion,
                first == null ? secondLength : firstLength);
            secondDuration = secondMotion == null
                ? 0f
                : ResolveDuration(secondMotion, secondLength);
            elapsed = 0f;
            lastTime = EditorApplication.timeSinceStartup;

            AnimationMode.StartAnimationMode();
            Sample(firstMotion, 0f);
            SceneView.RepaintAll();
        }

        public static void Stop()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;

            if (AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
            }

            if (previewRoot != null)
            {
                Object.DestroyImmediate(previewRoot);
            }

            previewRoot = null;
            firstMotion = null;
            secondMotion = null;
            firstDuration = 0f;
            secondDuration = 0f;
            elapsed = 0f;
            SceneView.RepaintAll();
        }

        private static void Update()
        {
            if (previewRoot == null)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var delta = Mathf.Clamp((float)(now - lastTime), 0f, 0.1f);
            lastTime = now;
            elapsed += delta;

            if (elapsed < firstDuration)
            {
                Sample(firstMotion, elapsed);
            }
            else if (secondMotion != null && elapsed < firstDuration + secondDuration)
            {
                Sample(secondMotion, elapsed - firstDuration);
            }
            else
            {
                Stop();
            }

            SceneView.RepaintAll();
        }

        private static void Sample(AnimationClip motion, float time)
        {
            if (previewRoot == null || motion == null)
            {
                return;
            }

            var animators = previewRoot.GetComponentsInChildren<Animator>(true);
            if (animators.Length == 0)
            {
                return;
            }

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(
                animators[0].gameObject,
                motion,
                Mathf.Clamp(time, 0f, motion.length));
            AnimationMode.EndSampling();
        }

        private static float ResolveDuration(AnimationClip motion, float duration)
        {
            return duration > 0f
                ? duration
                : motion == null
                    ? 0f
                    : Mathf.Max(0.01f, motion.length);
        }
    }
}
