using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander.Editor
{
    [CustomEditor(typeof(FallenCommanderController))]
    public sealed class FallenCommanderControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty bossPrefabProperty;
        private SerializedProperty bossSpawnPointProperty;
        private SerializedProperty bossConfigProperty;
        private SerializedProperty commanderRootProperty;

        private void OnEnable()
        {
            bossPrefabProperty = serializedObject.FindProperty("bossPrefab");
            bossSpawnPointProperty = serializedObject.FindProperty("bossSpawnPoint");
            bossConfigProperty = serializedObject.FindProperty("bossConfig");
            commanderRootProperty = serializedObject.FindProperty("commanderRoot");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("보스 모션 미리보기", EditorStyles.boldLabel);

            var config = bossConfigProperty.objectReferenceValue as FallenCommanderBossConfig;
            if (config == null)
            {
                EditorGUILayout.HelpBox(
                    "보스 설정 데이터를 연결하면 모션을 미리 볼 수 있어용.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                Application.isPlaying
                    ? "게임 실행 중: 현재 생성된 보스가 모션을 재생해용."
                    : "편집 중: 현재 씬에 임시 보스를 만들어 모션을 보여줘용.",
                MessageType.None);

            DrawAttackPreview("근접 공격", config.MeleeAttack);
            DrawAttackPreview("위치 공격", config.MarkStrike);
            DrawAttackPreview("블랙홀", config.BlackHole);
            DrawAttackPreview("직선 공격", config.LineStrike);
            DrawMotionPreview("보스 브레이크", config.BreakMotion, config.BreakMotionDuration);
            DrawMotionPreview("보스 사망", config.DeathMotion, config.DeathMotionDuration);

            if (GUILayout.Button("모션 미리보기 종료"))
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
                $"{label}  (시전 속도 x{attack.PreCastMotionSpeed:0.##} / {attack.PreCastMotionDuration:0.###}초 → " +
                $"공격 속도 x{attack.CastMotionSpeed:0.##} / {attack.CastMotionDuration:0.###}초)",
                EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"▶ {label}: 시전 → 공격"))
                {
                    PreviewAttack(attack);
                }

                if (GUILayout.Button("시전 보기", GUILayout.Width(72f)))
                {
                    PreviewMotion(
                        attack.PreCastMotion,
                        attack.PreCastMotionDuration,
                        attack.PreCastMotionSpeed);
                }

                if (GUILayout.Button("공격 보기", GUILayout.Width(72f)))
                {
                    PreviewMotion(
                        attack.CastMotion,
                        attack.CastMotionDuration,
                        attack.CastMotionSpeed);
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
                    $"{label} ({duration:0.###}초)",
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
                        "보스 모션 미리보기",
                        "보스가 생성되도록 던전에 먼저 입장해 주세요.",
                        "확인");
                }

                return;
            }

            FallenCommanderBossEditorPreview.PlaySequence(
                GetBossPrefab(),
                GetSpawnPoint(),
                GetFacingTarget(),
                attack.PreCastMotion,
                attack.PreCastMotionDuration,
                attack.PreCastMotionSpeed,
                attack.CastMotion,
                attack.CastMotionDuration,
                attack.CastMotionSpeed);
        }

        private void PreviewMotion(
            AnimationClip motion,
            float duration,
            float playbackSpeed = 1f)
        {
            if (motion == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                if (!((FallenCommanderController)target).PreviewBossMotion(
                    motion,
                    duration,
                    playbackSpeed))
                {
                    EditorUtility.DisplayDialog(
                        "보스 모션 미리보기",
                        "보스가 생성되도록 던전에 먼저 입장해 주세요.",
                        "확인");
                }

                return;
            }

            FallenCommanderBossEditorPreview.PlaySequence(
                GetBossPrefab(),
                GetSpawnPoint(),
                GetFacingTarget(),
                motion,
                duration,
                playbackSpeed,
                null,
                0f,
                1f);
        }

        private GameObject GetBossPrefab()
        {
            return bossPrefabProperty.objectReferenceValue as GameObject;
        }

        private Transform GetSpawnPoint()
        {
            return bossSpawnPointProperty.objectReferenceValue as Transform;
        }

        // 편집 모드 미리보기 보스가 바라볼 군단장 Transform을 반환한다.
        private Transform GetFacingTarget()
        {
            var commanderRoot = commanderRootProperty.objectReferenceValue as GameObject;
            return commanderRoot == null ? null : commanderRoot.transform;
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
        private static float firstSpeed = 1f;
        private static float secondSpeed = 1f;
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
            Transform facingTarget,
            AnimationClip first,
            float firstLength,
            float firstPlaybackSpeed,
            AnimationClip second,
            float secondLength,
            float secondPlaybackSpeed)
        {
            Stop();
            FallenCommanderAttackEditorPreview.Stop();
            if (PrefabStageUtility.GetCurrentPrefabStage() != null ||
                prefab == null ||
                (first == null && second == null))
            {
                return;
            }

            previewRoot = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (previewRoot == null)
            {
                return;
            }

            previewRoot.name = $"[모션 미리보기] {prefab.name}";
            previewRoot.hideFlags = HideFlags.DontSave;
            if (spawnPoint != null)
            {
                previewRoot.transform.SetPositionAndRotation(
                    spawnPoint.position,
                    spawnPoint.rotation);
            }

            FaceTarget(previewRoot.transform, facingTarget);

            foreach (var behaviour in previewRoot.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            firstMotion = first == null ? second : first;
            secondMotion = first == null ? null : second;
            firstSpeed = Mathf.Max(
                0.01f,
                first == null ? secondPlaybackSpeed : firstPlaybackSpeed);
            secondSpeed = Mathf.Max(0.01f, secondPlaybackSpeed);
            firstDuration = ResolveDuration(
                firstMotion,
                first == null ? secondLength : firstLength);
            secondDuration = secondMotion == null
                ? 0f
                : ResolveDuration(secondMotion, secondLength);
            elapsed = 0f;
            lastTime = EditorApplication.timeSinceStartup;

            AnimationMode.StartAnimationMode();
            Sample(firstMotion, 0f, firstSpeed);
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
            firstSpeed = 1f;
            secondSpeed = 1f;
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
                Sample(firstMotion, elapsed, firstSpeed);
            }
            else if (secondMotion != null && elapsed < firstDuration + secondDuration)
            {
                Sample(secondMotion, elapsed - firstDuration, secondSpeed);
            }
            else
            {
                Stop();
            }

            SceneView.RepaintAll();
        }

        private static void Sample(
            AnimationClip motion,
            float time,
            float playbackSpeed)
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
                Mathf.Clamp(time * Mathf.Max(0.01f, playbackSpeed), 0f, motion.length));
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

        // 임시 보스를 생성 즉시 군단장 방향으로 회전시킨다.
        private static void FaceTarget(Transform bossTransform, Transform facingTarget)
        {
            if (bossTransform == null || facingTarget == null)
            {
                return;
            }

            var direction = facingTarget.position - bossTransform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            bossTransform.rotation = Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);
        }
    }
}
