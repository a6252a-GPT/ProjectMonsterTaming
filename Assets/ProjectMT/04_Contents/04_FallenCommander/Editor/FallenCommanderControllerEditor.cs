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
                    "보스 설정 데이터를 연결하면 모션을 미리 볼 수 있습니다.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                Application.isPlaying
                    ? "게임 실행 중: 현재 생성된 보스가 모션을 재생합니다."
                    : "편집 중: 현재 씬에 임시 보스를 생성하여 모션을 표시합니다.",
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
                $"{label}  (시전 {attack.PreCastMotionStart:P0}~{attack.PreCastMotionEnd:P0} " +
                $"x{attack.PreCastMotionSpeed:0.##} → 공격 {attack.CastMotionStart:P0}~" +
                $"{attack.CastMotionEnd:P0} x{attack.CastMotionSpeed:0.##})",
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
                        ResolveMotionDuration(
                            attack.PreCastMotion,
                            attack.PreCastMotionSpeed,
                            attack.PreCastMotionStart,
                            attack.PreCastMotionEnd),
                        attack.PreCastMotionSpeed,
                        attack.PreCastMotionStart,
                        attack.PreCastMotionEnd);
                }

                if (GUILayout.Button("공격 보기", GUILayout.Width(72f)))
                {
                    PreviewMotion(
                        attack.CastMotion,
                        attack.CastMotionDuration,
                        attack.CastMotionSpeed,
                        attack.CastMotionStart,
                        attack.CastMotionEnd);
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
                attack.WarningDuration,
                attack.PreCastMotionSpeed,
                attack.CastMotion,
                attack.CastMotionDuration,
                attack.CastMotionSpeed,
                attack.PreCastMotionStart,
                attack.PreCastMotionEnd,
                attack.CastMotionStart,
                attack.CastMotionEnd);
        }

        private void PreviewMotion(
            AnimationClip motion,
            float duration,
            float playbackSpeed = 1f,
            float normalizedStart = 0f,
            float normalizedEnd = 1f)
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
                    playbackSpeed,
                    normalizedStart,
                    normalizedEnd))
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
                1f,
                normalizedStart,
                normalizedEnd);
        }

        // 모션 길이와 재생 속도로 자동 재생시간을 계산한다.
        private static float ResolveMotionDuration(
            AnimationClip motion,
            float playbackSpeed,
            float normalizedStart,
            float normalizedEnd)
        {
            if (motion == null)
            {
                return 0f;
            }

            var safeStart = Mathf.Clamp(normalizedStart, 0f, 0.999f);
            var safeEnd = Mathf.Clamp(normalizedEnd, safeStart + 0.001f, 1f);
            return Mathf.Max(
                0.01f,
                motion.length * (safeEnd - safeStart) /
                Mathf.Max(0.01f, playbackSpeed));
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
        private static float firstStart;
        private static float firstEnd = 1f;
        private static float secondStart;
        private static float secondEnd = 1f;
        private static double lastTime;
        private static float elapsed;

        static FallenCommanderBossEditorPreview()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += _ => Stop();
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }

        // 임시 보스를 안전하게 생성하고 지정된 모션들을 순서대로 미리 재생한다.
        public static void PlaySequence(
            GameObject prefab,
            Transform spawnPoint,
            Transform facingTarget,
            AnimationClip first,
            float firstLength,
            float firstPlaybackSpeed,
            AnimationClip second,
            float secondLength,
            float secondPlaybackSpeed,
            float requestedFirstStart = 0f,
            float requestedFirstEnd = 1f,
            float requestedSecondStart = 0f,
            float requestedSecondEnd = 1f)
        {
            Stop();
            FallenCommanderAttackEditorPreview.Stop();
            if (PrefabStageUtility.GetCurrentPrefabStage() != null ||
                prefab == null ||
                (first == null && second == null))
            {
                return;
            }

            GameObject createdRoot = null;
            var initialized = false;
            try
            {
                createdRoot = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (createdRoot == null)
                {
                    return;
                }

                createdRoot.name = $"[모션 미리보기] {prefab.name}";
                createdRoot.hideFlags = HideFlags.HideAndDontSave;
                if (spawnPoint != null)
                {
                    createdRoot.transform.SetPositionAndRotation(
                        spawnPoint.position,
                        spawnPoint.rotation);
                }

                FaceTarget(createdRoot.transform, facingTarget);

                foreach (var behaviour in createdRoot.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    behaviour.enabled = false;
                }

                var resolvedFirstMotion = first == null ? second : first;
                var resolvedSecondMotion = first == null ? null : second;
                var resolvedFirstSpeed = Mathf.Max(
                    0.01f,
                    first == null ? secondPlaybackSpeed : firstPlaybackSpeed);
                var resolvedSecondSpeed = Mathf.Max(0.01f, secondPlaybackSpeed);
                var resolvedFirstStart = first == null
                    ? requestedSecondStart
                    : requestedFirstStart;
                var resolvedFirstEnd = first == null
                    ? requestedSecondEnd
                    : requestedFirstEnd;
                var resolvedFirstDuration = ResolveDuration(
                    resolvedFirstMotion,
                    first == null ? secondLength : firstLength,
                    resolvedFirstSpeed,
                    resolvedFirstStart,
                    resolvedFirstEnd);
                var resolvedSecondDuration = resolvedSecondMotion == null
                    ? 0f
                    : ResolveDuration(
                        resolvedSecondMotion,
                        secondLength,
                        resolvedSecondSpeed,
                        requestedSecondStart,
                        requestedSecondEnd);

                previewRoot = createdRoot;
                firstMotion = resolvedFirstMotion;
                secondMotion = resolvedSecondMotion;
                firstSpeed = resolvedFirstSpeed;
                secondSpeed = resolvedSecondSpeed;
                firstStart = resolvedFirstStart;
                firstEnd = resolvedFirstEnd;
                secondStart = requestedSecondStart;
                secondEnd = requestedSecondEnd;
                firstDuration = resolvedFirstDuration;
                secondDuration = resolvedSecondDuration;
                elapsed = 0f;
                lastTime = EditorApplication.timeSinceStartup;

                AnimationMode.StartAnimationMode();
                Sample(firstMotion, 0f, firstSpeed, firstStart, firstEnd);
                SceneView.RepaintAll();
                initialized = true;
            }
            finally
            {
                if (!initialized)
                {
                    if (createdRoot != null && previewRoot == createdRoot)
                    {
                        Stop();
                    }
                    else if (createdRoot != null)
                    {
                        Object.DestroyImmediate(createdRoot);
                    }
                }
            }
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
            firstStart = 0f;
            firstEnd = 1f;
            secondStart = 0f;
            secondEnd = 1f;
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
                Sample(firstMotion, elapsed, firstSpeed, firstStart, firstEnd);
            }
            else if (secondMotion != null && elapsed < firstDuration + secondDuration)
            {
                Sample(
                    secondMotion,
                    elapsed - firstDuration,
                    secondSpeed,
                    secondStart,
                    secondEnd);
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
            float playbackSpeed,
            float normalizedStart,
            float normalizedEnd)
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

            var safeStart = Mathf.Clamp(normalizedStart, 0f, 0.999f);
            var safeEnd = Mathf.Clamp(normalizedEnd, safeStart + 0.001f, 1f);
            var startTime = motion.length * safeStart;
            var endTime = motion.length * safeEnd;
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(
                animators[0].gameObject,
                motion,
                Mathf.Clamp(
                    startTime + time * Mathf.Max(0.01f, playbackSpeed),
                    startTime,
                    endTime));
            AnimationMode.EndSampling();
        }

        private static float ResolveDuration(
            AnimationClip motion,
            float duration,
            float playbackSpeed,
            float normalizedStart,
            float normalizedEnd)
        {
            var safeStart = Mathf.Clamp(normalizedStart, 0f, 0.999f);
            var safeEnd = Mathf.Clamp(normalizedEnd, safeStart + 0.001f, 1f);
            return duration > 0f
                ? duration
                : motion == null
                    ? 0f
                    : Mathf.Max(
                        0.01f,
                        motion.length * (safeEnd - safeStart) /
                        Mathf.Max(0.01f, playbackSpeed));
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
