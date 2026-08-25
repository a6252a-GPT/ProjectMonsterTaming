using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander.Editor
{
    [CustomEditor(typeof(FallenCommanderBossConfig))]
    public sealed class FallenCommanderBossConfigEditor : UnityEditor.Editor
    {
        // 기본 데이터와 충전 VFX 미리보기 도구를 함께 표시한다.
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var property = serializedObject.GetIterator();
            var enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                {
                    EditorGUILayout.PropertyField(property, true);
                }

                if (property.propertyPath != "finalChargeStartEffectOffset")
                {
                    continue;
                }

                serializedObject.ApplyModifiedProperties();
                DrawFinalChargePreview((FallenCommanderBossConfig)target);
            }

            serializedObject.ApplyModifiedProperties();
        }

        // 충전 광역기 데이터 바로 아래에 VFX 미리보기 조작부를 표시한다.
        private static void DrawFinalChargePreview(FallenCommanderBossConfig config)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("충전 광역기 VFX 미리보기", EditorStyles.boldLabel);

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                FallenCommanderFinalChargeVfxEditorPreview.Stop();
                EditorGUILayout.HelpBox(
                    "Prefab Mode에서는 원본 프리팹 보호를 위해 미리보기를 실행하지 않아용. " +
                    "Prefab Mode를 닫고 DEV_03_FallenCommander Scene에서 사용해 주세요.",
                    MessageType.Warning);
                return;
            }

            var startVfx = config.FinalChargeEffects?.StartVfxPrefab;
            if (startVfx == null)
            {
                EditorGUILayout.HelpBox(
                    "8. 충전 광역기의 시전 VFX를 먼저 지정해 주세요.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "Play 없이 임시 보스에 시전 VFX를 붙여 표시합니다. " +
                "시전 연출 위치 오프셋을 바꾸면 Scene 미리보기 위치도 바로 갱신됩니다.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                var previewLabel = FallenCommanderFinalChargeVfxEditorPreview.IsActive
                    ? "미리보기 다시 재생"
                    : "충전 시전 VFX 미리보기";
                if (GUILayout.Button(previewLabel))
                {
                    if (!TryStartPreview(config))
                    {
                        EditorUtility.DisplayDialog(
                            "충전 VFX 미리보기",
                            "현재 Scene에서 FallenCommanderController의 보스 프리팹을 찾지 못했어용.",
                            "확인");
                    }
                }

                using (new EditorGUI.DisabledScope(
                    !FallenCommanderFinalChargeVfxEditorPreview.IsActive))
                {
                    if (GUILayout.Button("미리보기 종료", GUILayout.Width(110f)))
                    {
                        FallenCommanderFinalChargeVfxEditorPreview.Stop();
                    }
                }
            }
        }

        // 데이터 인스펙터가 닫히면 임시 미리보기 오브젝트를 정리한다.
        private void OnDisable()
        {
            FallenCommanderFinalChargeVfxEditorPreview.Stop();
        }

        // 현재 Scene의 컨트롤러에서 보스 프리팹과 생성 위치를 찾아 미리보기를 시작한다.
        private static bool TryStartPreview(FallenCommanderBossConfig config)
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                return false;
            }

            foreach (var controller in Object.FindObjectsByType<FallenCommanderController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                var controllerData = new SerializedObject(controller);
                var prefab = controllerData.FindProperty("bossPrefab")
                    ?.objectReferenceValue as GameObject;
                var spawnPoint = controllerData.FindProperty("bossSpawnPoint")
                    ?.objectReferenceValue as Transform;
                var commanderRoot = controllerData.FindProperty("commanderRoot")
                    ?.objectReferenceValue as GameObject;
                if (prefab == null)
                {
                    continue;
                }

                return FallenCommanderFinalChargeVfxEditorPreview.Play(
                    config,
                    prefab,
                    spawnPoint,
                    commanderRoot == null ? null : commanderRoot.transform);
            }

            return false;
        }
    }

    [InitializeOnLoad]
    internal static class FallenCommanderFinalChargeVfxEditorPreview
    {
        private static FallenCommanderBossConfig activeConfig;
        private static GameObject previewBoss;
        private static GameObject previewVfx;
        private static ParticleSystem[] particles = System.Array.Empty<ParticleSystem>();
        private static double lastTime;

        public static bool IsActive => previewBoss != null && previewVfx != null;

        // 에디터 재생·종료·컴파일 시 임시 미리보기가 남지 않도록 정리 경로를 등록한다.
        static FallenCommanderFinalChargeVfxEditorPreview()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += _ => Stop();
            EditorApplication.quitting += Stop;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }

        // 임시 보스와 시전 VFX를 Scene에 생성하고 에디터 재생을 시작한다.
        public static bool Play(
            FallenCommanderBossConfig config,
            GameObject bossPrefab,
            Transform spawnPoint,
            Transform facingTarget)
        {
            Stop();
            if (PrefabStageUtility.GetCurrentPrefabStage() != null ||
                config?.FinalChargeEffects?.StartVfxPrefab == null ||
                bossPrefab == null)
            {
                return false;
            }

            GameObject createdBoss = null;
            GameObject createdVfx = null;
            var initialized = false;
            try
            {
                createdBoss = PrefabUtility.InstantiatePrefab(bossPrefab) as GameObject;
                if (createdBoss == null)
                {
                    return false;
                }

                createdBoss.name = $"[충전 VFX 미리보기] {bossPrefab.name}";
                createdBoss.hideFlags = HideFlags.DontSave;
                if (spawnPoint != null)
                {
                    createdBoss.transform.SetPositionAndRotation(
                        spawnPoint.position,
                        spawnPoint.rotation);
                }

                FaceTarget(createdBoss.transform, facingTarget);

                foreach (var behaviour in createdBoss.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    behaviour.enabled = false;
                }

                createdVfx = Object.Instantiate(
                    config.FinalChargeEffects.StartVfxPrefab,
                    createdBoss.transform,
                    false);
                createdVfx.name = $"[미리보기] {config.FinalChargeEffects.StartVfxPrefab.name}";
                createdVfx.hideFlags = HideFlags.DontSave;
                createdVfx.transform.localPosition = config.FinalChargeStartEffectOffset;
                createdVfx.transform.localRotation = Quaternion.identity;

                activeConfig = config;
                previewBoss = createdBoss;
                previewVfx = createdVfx;
                particles = previewVfx.GetComponentsInChildren<ParticleSystem>(true);
                RestartParticles();
                lastTime = EditorApplication.timeSinceStartup;
                initialized = true;
                SceneView.lastActiveSceneView?.Frame(
                    new Bounds(previewVfx.transform.position, Vector3.one * 4f),
                    true);
                SceneView.RepaintAll();
                return true;
            }
            finally
            {
                if (!initialized)
                {
                    if (createdVfx != null)
                    {
                        Object.DestroyImmediate(createdVfx);
                    }

                    if (createdBoss != null)
                    {
                        Object.DestroyImmediate(createdBoss);
                    }
                }
            }
        }

        // 임시 보스와 VFX를 제거하고 미리보기 상태를 초기화한다.
        public static void Stop()
        {
            if (previewBoss != null)
            {
                Object.DestroyImmediate(previewBoss);
            }
            else if (previewVfx != null)
            {
                Object.DestroyImmediate(previewVfx);
            }

            activeConfig = null;
            previewBoss = null;
            previewVfx = null;
            particles = System.Array.Empty<ParticleSystem>();
            lastTime = 0d;
            SceneView.RepaintAll();
        }

        // 데이터 오프셋을 실시간 반영하고 파티클을 Scene 뷰에서 진행시킨다.
        private static void Update()
        {
            if (!IsActive || activeConfig == null)
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Stop();
                return;
            }

            previewVfx.transform.localPosition = activeConfig.FinalChargeStartEffectOffset;
            var now = EditorApplication.timeSinceStartup;
            var deltaTime = Mathf.Clamp((float)(now - lastTime), 0f, 0.1f);
            lastTime = now;

            foreach (var particle in particles)
            {
                if (particle != null)
                {
                    particle.Simulate(deltaTime, false, false, false);
                }
            }

            SceneView.RepaintAll();
        }

        // 모든 자식 파티클을 처음 상태로 되돌리고 다시 재생한다.
        private static void RestartParticles()
        {
            foreach (var particle in particles)
            {
                if (particle == null)
                {
                    continue;
                }

                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Simulate(0f, false, true, true);
                particle.Play(false);
            }
        }

        // 데이터 미리보기의 임시 보스를 Scene 군단장 방향으로 회전시킨다.
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
