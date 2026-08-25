using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander.Editor
{
    [CustomEditor(typeof(FallenCommanderBossConfig))]
    public sealed class FallenCommanderBossConfigEditor : UnityEditor.Editor
    {
        private const string SelectedAttackKey =
            "ProjectMT.FallenCommander.BossConfig.SelectedAttack";
        private const string ShowAllAttacksKey =
            "ProjectMT.FallenCommander.BossConfig.ShowAllAttacks";

        private static readonly string[] AttackTabLabels =
        {
            "1. 기본",
            "2. 근접",
            "3. 위치",
            "4. 추적",
            "5. 블랙홀",
            "6. 직선",
            "7. 고리",
            "8. 충전"
        };

        private static readonly string[][] AttackPropertyNames =
        {
            new[] { "projectileBasicAttack" },
            new[] { "meleeAttack" },
            new[] { "markStrike" },
            new[] { "trackingMark", "trackingMarkLockDuration" },
            new[]
            {
                "blackHole",
                "blackHoleActiveDuration",
                "blackHoleCoreRadius",
                "blackHoleSpawnMinDistance",
                "blackHoleSpawnMaxDistance",
                "blackHoleOuterPullSpeed",
                "blackHoleInnerPullSpeed",
                "blackHolePullStrengthCurve",
                "blackHoleArenaHalfExtents",
                "blackHoleEndEffects"
            },
            new[] { "lineStrike" },
            new[] { "corruptionRing", "corruptionRingSafeRadius" },
            new[]
            {
                "finalChargeTelegraphPrefab",
                "finalChargeEffects",
                "finalChargeStartEffectOffset"
            }
        };

        private int selectedAttack;
        private bool showAllAttacks;

        // 마지막으로 선택한 공격 탭과 전체 보기 상태를 불러온다.
        private void OnEnable()
        {
            selectedAttack = Mathf.Clamp(
                EditorPrefs.GetInt(SelectedAttackKey, 0),
                0,
                AttackTabLabels.Length - 1);
            showAllAttacks = EditorPrefs.GetBool(ShowAllAttacksKey, false);
        }

        // 공통 데이터와 선택된 공격 탭의 데이터만 순서대로 표시한다.
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var property = serializedObject.GetIterator();
            var enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "projectileBasicAttack")
                {
                    DrawAttackTabs();
                    continue;
                }

                if (IsAttackProperty(property.propertyPath))
                {
                    continue;
                }

                using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                {
                    EditorGUILayout.PropertyField(
                        property,
                        FallenCommanderInspectorLabels.BossConfig(property),
                        true);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        // 두 줄 공격 버튼과 전체 보기 전환 버튼을 표시한다.
        private void DrawAttackTabs()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("공격 설정", EditorStyles.boldLabel);
                var nextShowAll = GUILayout.Toggle(
                    showAllAttacks,
                    "전체 보기",
                    EditorStyles.miniButton,
                    GUILayout.Width(78f));
                if (nextShowAll != showAllAttacks)
                {
                    showAllAttacks = nextShowAll;
                    EditorPrefs.SetBool(ShowAllAttacksKey, showAllAttacks);
                }
            }

            for (var row = 0; row < 2; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (var column = 0; column < 4; column++)
                    {
                        var index = row * 4 + column;
                        var selected = GUILayout.Toggle(
                            selectedAttack == index,
                            AttackTabLabels[index],
                            EditorStyles.miniButton);
                        if (selected && selectedAttack != index)
                        {
                            selectedAttack = index;
                            EditorPrefs.SetInt(SelectedAttackKey, selectedAttack);
                            GUI.FocusControl(null);
                        }
                    }
                }
            }

            EditorGUILayout.Space(4f);
            if (showAllAttacks)
            {
                for (var index = 0; index < AttackPropertyNames.Length; index++)
                {
                    DrawAttackProperties(index);
                }

                return;
            }

            DrawAttackProperties(selectedAttack);
        }

        // 선택된 공격에 포함된 SerializedProperty와 전용 도구를 표시한다.
        private void DrawAttackProperties(int attackIndex)
        {
            foreach (var propertyName in AttackPropertyNames[attackIndex])
            {
                var attackProperty = serializedObject.FindProperty(propertyName);
                if (attackProperty != null)
                {
                    EditorGUILayout.PropertyField(
                        attackProperty,
                        FallenCommanderInspectorLabels.BossConfig(attackProperty),
                        true);
                }
            }

            serializedObject.ApplyModifiedProperties();
            DrawAttackPreviewTools(
                (FallenCommanderBossConfig)target,
                attackIndex);

            if (showAllAttacks && attackIndex < AttackPropertyNames.Length - 1)
            {
                EditorGUILayout.Space(6f);
            }
        }

        // 공격 탭 안에 시전·공격·전체 미리보기와 종료 버튼을 표시한다.
        private static void DrawAttackPreviewTools(
            FallenCommanderBossConfig config,
            int attackIndex)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("공격 연출 미리보기", EditorStyles.boldLabel);

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                FallenCommanderAttackEditorPreview.Stop();
                EditorGUILayout.HelpBox(
                    "프리팹 편집 화면에서는 원본 보호를 위해 미리보기를 실행하지 않아용. " +
                    "프리팹 편집 화면을 닫고 개발용 군단장 씬에서 사용해 주세요.",
                    MessageType.Warning);
                return;
            }

            if (!TryBuildAttackPreviewSpec(config, attackIndex, out var previewSpec))
            {
                EditorGUILayout.HelpBox(
                    "현재 씬에서 군단장 보스 실행 오브젝트를 찾지 못했어용.",
                    MessageType.Info);
                return;
            }

            var hasPreCast = HasPreCastPresentation(previewSpec);
            var hasCast = HasCastPresentation(previewSpec);
            if (!hasPreCast && !hasCast)
            {
                EditorGUILayout.HelpBox(
                    "이 공격에는 아직 모션·시각 효과·효과음이 지정되지 않았어용.",
                    MessageType.Info);
            }
            else if (attackIndex == 0)
            {
                EditorGUILayout.HelpBox(
                    "전체 미리보기는 직선 경고범위가 차오른 뒤 기본 공격 구체가 " +
                    "군단장 방향으로 날아가고, 닿는 순간 적중 연출을 재생해용.",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"전체 미리보기는 시전 후 {previewSpec.WarningDuration:0.##}초에 " +
                    "공격 모션·적중 시각 효과·효과음을 재생해용.",
                    MessageType.None);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!hasPreCast))
                {
                    if (GUILayout.Button("▶ 시전"))
                    {
                        FallenCommanderAttackEditorPreview.Play(
                            previewSpec,
                            FallenCommanderAttackPreviewMode.PreCast);
                    }
                }

                using (new EditorGUI.DisabledScope(!hasCast))
                {
                    if (GUILayout.Button("▶ 공격"))
                    {
                        FallenCommanderAttackEditorPreview.Play(
                            previewSpec,
                            FallenCommanderAttackPreviewMode.Cast);
                    }
                }

                using (new EditorGUI.DisabledScope(!hasPreCast && !hasCast))
                {
                    if (GUILayout.Button("▶ 전체"))
                    {
                        FallenCommanderAttackEditorPreview.Play(
                            previewSpec,
                            FallenCommanderAttackPreviewMode.Full);
                    }
                }

                using (new EditorGUI.DisabledScope(
                    !FallenCommanderAttackEditorPreview.IsActive))
                {
                    if (GUILayout.Button("■ 종료"))
                    {
                        FallenCommanderAttackEditorPreview.Stop();
                    }
                }
            }
        }

        // 현재 Scene 참조와 선택된 공격 데이터를 범용 미리보기 명세로 묶는다.
        private static bool TryBuildAttackPreviewSpec(
            FallenCommanderBossConfig config,
            int attackIndex,
            out FallenCommanderAttackPreviewSpec previewSpec)
        {
            previewSpec = null;
            foreach (var controller in Object.FindObjectsByType<FallenCommanderController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                var controllerData = new SerializedObject(controller);
                var bossPrefab = controllerData.FindProperty("bossPrefab")
                    ?.objectReferenceValue as GameObject;
                if (bossPrefab == null)
                {
                    continue;
                }

                var spawnPoint = controllerData.FindProperty("bossSpawnPoint")
                    ?.objectReferenceValue as Transform;
                var commanderRoot = controllerData.FindProperty("commanderRoot")
                    ?.objectReferenceValue as GameObject;
                var attack = ResolveAttackData(config, attackIndex);
                var basicAttack = attackIndex == 0 ? config.BasicAttack : null;
                var effects = attackIndex == 7
                    ? config.FinalChargeEffects
                    : attackIndex == 0
                        ? basicAttack?.Effects
                        : attack?.Effects;
                var warningDuration = attackIndex == 7
                    ? controllerData.FindProperty("finalChargeDuration")?.floatValue ?? 0.1f
                    : attackIndex == 0
                        ? basicAttack?.WarningDuration ?? 0.1f
                        : attack?.WarningDuration ?? 0.1f;

                previewSpec = new FallenCommanderAttackPreviewSpec
                {
                    AttackIndex = attackIndex,
                    Label = AttackTabLabels[attackIndex],
                    Config = config,
                    BossPrefab = bossPrefab,
                    SpawnPoint = spawnPoint,
                    FacingTarget = commanderRoot == null ? null : commanderRoot.transform,
                    BasicAttack = basicAttack,
                    Effects = effects,
                    PreCastMotion = attack?.PreCastMotion,
                    PreCastMotionDuration = attack?.PreCastMotionDuration ?? 0f,
                    PreCastMotionSpeed = attack?.PreCastMotionSpeed ?? 1f,
                    CastMotion = attack?.CastMotion,
                    CastMotionDuration = attack?.CastMotionDuration ?? 0f,
                    CastMotionSpeed = attack?.CastMotionSpeed ?? 1f,
                    WarningDuration = Mathf.Max(0.1f, warningDuration),
                    StartEffectLocalOffset = attackIndex == 7
                        ? config.FinalChargeStartEffectOffset
                        : Vector3.zero,
                    AttachStartEffectToBoss = attackIndex == 7
                };
                return true;
            }

            return false;
        }

        // 공격 번호에 해당하는 일반 공격 데이터를 반환한다.
        private static FallenCommanderAttackData ResolveAttackData(
            FallenCommanderBossConfig config,
            int attackIndex)
        {
            return attackIndex switch
            {
                1 => config.MeleeAttack,
                2 => config.MarkStrike,
                3 => config.TrackingMark,
                4 => config.BlackHole,
                5 => config.LineStrike,
                6 => config.CorruptionRing,
                _ => null
            };
        }

        // 시전 단계에 모션·VFX·SFX 중 하나라도 있는지 확인한다.
        private static bool HasPreCastPresentation(FallenCommanderAttackPreviewSpec previewSpec)
        {
            return previewSpec.BasicAttack != null ||
                previewSpec.PreCastMotion != null ||
                previewSpec.Effects?.StartVfxPrefab != null ||
                previewSpec.Effects?.StartSfx != null;
        }

        // 공격 단계에 모션·VFX·SFX 중 하나라도 있는지 확인한다.
        private static bool HasCastPresentation(FallenCommanderAttackPreviewSpec previewSpec)
        {
            return previewSpec.BasicAttack != null ||
                previewSpec.CastMotion != null ||
                previewSpec.Effects?.ResolveVfxPrefab != null ||
                previewSpec.Effects?.ResolveSfx != null;
        }

        // 현재 속성이 공격 탭에서 별도로 표시되는 데이터인지 확인한다.
        private static bool IsAttackProperty(string propertyPath)
        {
            foreach (var propertyNames in AttackPropertyNames)
            {
                foreach (var propertyName in propertyNames)
                {
                    if (propertyPath == propertyName)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // 충전 광역기 데이터 바로 아래에 VFX 미리보기 조작부를 표시한다.
        private static void DrawFinalChargePreview(FallenCommanderBossConfig config)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("충전 광역기 시각 효과 미리보기", EditorStyles.boldLabel);

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                FallenCommanderFinalChargeVfxEditorPreview.Stop();
                EditorGUILayout.HelpBox(
                    "프리팹 편집 화면에서는 원본 보호를 위해 미리보기를 실행하지 않아용. " +
                    "프리팹 편집 화면을 닫고 개발용 군단장 씬에서 사용해 주세요.",
                    MessageType.Warning);
                return;
            }

            var startVfx = config.FinalChargeEffects?.StartVfxPrefab;
            if (startVfx == null)
            {
                EditorGUILayout.HelpBox(
                    "8. 충전 광역기의 시전 시각 효과를 먼저 지정해 주세요.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "게임을 실행하지 않고 임시 보스에 시전 시각 효과를 붙여 표시합니다. " +
                "시전 연출 위치 오프셋을 바꾸면 씬 미리보기 위치도 바로 갱신됩니다.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                var previewLabel = FallenCommanderFinalChargeVfxEditorPreview.IsActive
                    ? "미리보기 다시 재생"
                    : "충전 시전 시각 효과 미리보기";
                if (GUILayout.Button(previewLabel))
                {
                    if (!TryStartPreview(config))
                    {
                        EditorUtility.DisplayDialog(
                            "충전 시각 효과 미리보기",
                            "현재 씬에서 군단장 보스 실행 오브젝트를 찾지 못했어용.",
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
            FallenCommanderAttackEditorPreview.Stop();
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

                createdBoss.name = $"[충전 시각 효과 미리보기] {bossPrefab.name}";
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
