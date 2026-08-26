using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Tools.FeelPreview
{
    public sealed class BasicAttackFeelPreviewLab : MonoBehaviour
    {
        [SerializeField] private GameObject target;
        [SerializeField] private GameObject[] feelPrefabs;
        [SerializeField] private int selectedPresetIndex;
        [SerializeField, Min(0.1f)] private float lightIntensity = 0.62f;
        [SerializeField, Min(0.1f)] private float standardIntensity = 1f;
        [SerializeField, Min(0.1f)] private float heavyIntensity = 1.45f;
        [SerializeField, Min(0.2f)] private float loopInterval = 1.2f;

        private GameObject activeFeel;
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Vector3 targetScale;
        private bool autoLoop;
        private float selectedIntensity = 1f;
        private float nextLoopTime;
        private string selectedWeightName = "보통";

        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle eyebrowStyle;
        private GUIStyle mutedStyle;
        private GUIStyle sectionStyle;
        private GUIStyle tabStyle;
        private GUIStyle selectedTabStyle;
        private GUIStyle weightStyle;
        private GUIStyle selectedWeightStyle;
        private GUIStyle utilityStyle;
        private GUIStyle infoStyle;
        private GUIStyle infoTitleStyle;
        private Texture2D panelTexture;
        private Texture2D tabTexture;
        private Texture2D selectedTexture;
        private Texture2D utilityTexture;
        private Texture2D infoTexture;

        private void Awake()
        {
            CacheTargetTransform();
        }

        private void Update()
        {
            if (!autoLoop || Time.unscaledTime < nextLoopTime)
            {
                return;
            }
            PlaySelected(selectedIntensity, selectedWeightName);
        }

        private void OnGUI()
        {
            EnsureGuiStyles();

            GUI.Box(new Rect(24f, 24f, 520f, 478f), GUIContent.none, panelStyle);
            GUILayout.BeginArea(new Rect(46f, 42f, 476f, 444f));

            GUILayout.Label("PROJECTMT  /  전투 피드백", eyebrowStyle);
            GUILayout.Label("기본공격 · FEEL 연구실", titleStyle);
            GUILayout.Label("메인전투 카메라  ·  시야각 45  ·  거리 18  ·  45° / -45°", mutedStyle);

            GUILayout.Space(14f);
            GUILayout.Label("타격 방식", sectionStyle);
            DrawPresetRow(0, Mathf.Min(4, feelPrefabs?.Length ?? 0));
            DrawPresetRow(4, Mathf.Min(7, feelPrefabs?.Length ?? 0));

            GUILayout.Space(12f);
            GUILayout.Label("타격 무게", sectionStyle);
            GUILayout.BeginHorizontal();
            DrawWeightButton("가벼움", "가벼움", lightIntensity);
            DrawWeightButton("보통", "보통", standardIntensity);
            DrawWeightButton("무거움", "무거움", heavyIntensity);
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            var nextAutoLoop = GUILayout.Toggle(autoLoop, "자동 반복", autoLoop ? selectedTabStyle : utilityStyle,
                GUILayout.Height(32f));
            if (nextAutoLoop != autoLoop)
            {
                autoLoop = nextAutoLoop;
                nextLoopTime = Time.unscaledTime + loopInterval;
            }
            if (GUILayout.Button("원상복구", utilityStyle, GUILayout.Height(32f)))
            {
                autoLoop = false;
                ResetTarget();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12f);
            GUILayout.BeginVertical(infoStyle);
            var selectedName = feelPrefabs != null && selectedPresetIndex >= 0 && selectedPresetIndex < feelPrefabs.Length
                ? feelPrefabs[selectedPresetIndex]?.name
                : null;
            GUILayout.Label($"{GetDisplayName(selectedName)}  ·  {selectedWeightName}", infoTitleStyle);
            GUILayout.Label(GetAssignedAttackIds(selectedName), mutedStyle);
            GUILayout.Space(5f);
            GUILayout.Label("모델  위치 / 회전 / 압축·신장 / 크기 탄성", mutedStyle);
            GUILayout.Label("화면  타격점 조명 / 카메라 흔들림 / 시야각 / 히트스톱", mutedStyle);
            GUILayout.Label("VFX · SFX  별도 제작 슬롯", mutedStyle);
            GUILayout.EndVertical();

            GUILayout.EndArea();
        }

        private void DrawPresetRow(int startIndex, int endIndex)
        {
            if (feelPrefabs == null || startIndex >= endIndex)
            {
                return;
            }

            GUILayout.BeginHorizontal();
            for (var index = startIndex; index < endIndex; index++)
            {
                var prefab = feelPrefabs[index];
                if (prefab == null)
                {
                    continue;
                }

                if (GUILayout.Button(
                        GetShortName(prefab.name),
                        index == selectedPresetIndex ? selectedTabStyle : tabStyle,
                        GUILayout.Height(34f)) && index != selectedPresetIndex)
                {
                    selectedPresetIndex = index;
                    ResetTarget();
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawWeightButton(string label, string weightName, float intensity)
        {
            if (GUILayout.Button(
                    label,
                    selectedWeightName == weightName ? selectedWeightStyle : weightStyle,
                    GUILayout.Height(40f)))
            {
                PlaySelected(intensity, weightName);
            }
        }

        private void EnsureGuiStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelTexture = CreateTexture(new Color(0.035f, 0.047f, 0.06f, 0.94f));
            tabTexture = CreateTexture(new Color(0.10f, 0.13f, 0.16f, 0.96f));
            selectedTexture = CreateTexture(new Color(0.08f, 0.68f, 0.64f, 0.98f));
            utilityTexture = CreateTexture(new Color(0.13f, 0.16f, 0.19f, 0.96f));
            infoTexture = CreateTexture(new Color(0.065f, 0.085f, 0.105f, 0.96f));

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = panelTexture },
                padding = new RectOffset(0, 0, 0, 0)
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                margin = new RectOffset(0, 0, 0, 1)
            };
            eyebrowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.20f, 0.86f, 0.80f) },
                margin = new RectOffset(0, 0, 0, 0)
            };
            mutedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.67f, 0.72f, 0.77f) },
                wordWrap = true,
                margin = new RectOffset(0, 0, 1, 1)
            };
            sectionStyle = new GUIStyle(eyebrowStyle)
            {
                normal = { textColor = new Color(0.77f, 0.82f, 0.86f) },
                margin = new RectOffset(0, 0, 2, 5)
            };
            tabStyle = CreateButtonStyle(tabTexture, new Color(0.78f, 0.82f, 0.85f));
            selectedTabStyle = CreateButtonStyle(selectedTexture, Color.white);
            weightStyle = CreateButtonStyle(tabTexture, new Color(0.78f, 0.82f, 0.85f));
            selectedWeightStyle = CreateButtonStyle(selectedTexture, Color.white);
            utilityStyle = CreateButtonStyle(utilityTexture, new Color(0.72f, 0.77f, 0.81f));
            infoStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = infoTexture },
                padding = new RectOffset(14, 14, 11, 11),
                margin = new RectOffset(0, 0, 0, 0)
            };
            infoTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                margin = new RectOffset(0, 0, 0, 1)
            };
        }

        private static GUIStyle CreateButtonStyle(Texture2D background, Color textColor)
        {
            return new GUIStyle(GUI.skin.button)
            {
                normal = { background = background, textColor = textColor },
                hover = { background = background, textColor = Color.white },
                active = { background = background, textColor = Color.white },
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(8, 8, 6, 6)
            };
        }

        private static Texture2D CreateTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
        public void Configure(GameObject targetObject, GameObject[] presets)
        {
            target = targetObject;
            feelPrefabs = presets;
            selectedPresetIndex = Mathf.Clamp(selectedPresetIndex, 0, Mathf.Max(0, feelPrefabs?.Length - 1 ?? 0));
            CacheTargetTransform();
        }

        public void PlayPreset(int index)
        {
            selectedPresetIndex = index;
            PlaySelected(standardIntensity, "보통");
        }

        public void PlaySelected(float intensity, string weightName = null)
        {
            if (target == null || feelPrefabs == null || selectedPresetIndex < 0 ||
                selectedPresetIndex >= feelPrefabs.Length || feelPrefabs[selectedPresetIndex] == null)
            {
                return;
            }

            selectedIntensity = Mathf.Max(0.1f, intensity);
            selectedWeightName = string.IsNullOrWhiteSpace(weightName) ? selectedWeightName : weightName;
            ResetTarget();
            var prefab = feelPrefabs[selectedPresetIndex];
            activeFeel = Instantiate(prefab, target.transform.position, target.transform.rotation);
            activeFeel.name = prefab.name + $" [{selectedWeightName} 피격 테스트]";
            var runtime = activeFeel.GetComponentsInChildren<MonoBehaviour>(true)
                .OfType<IBasicAttackFeelRuntime>()
                .FirstOrDefault();
            runtime?.PlayBasicAttackFeel(target.transform.position, target, selectedIntensity);
            nextLoopTime = Time.unscaledTime + loopInterval;
        }

        public void ResetTarget()
        {
            if (activeFeel != null)
            {
                var runtime = activeFeel.GetComponentsInChildren<MonoBehaviour>(true)
                    .OfType<IBasicAttackFeelRuntime>()
                    .FirstOrDefault();
                runtime?.ResetBasicAttackFeel();
                activeFeel.SetActive(false); // 진행 중 Spring 코루틴을 즉시 끊어 연속 비교 시 중첩을 막는다.
                Destroy(activeFeel);
                activeFeel = null;
            }

            if (target != null)
            {
                target.transform.SetPositionAndRotation(targetPosition, targetRotation);
                target.transform.localScale = targetScale;
            }
            Time.timeScale = 1f;
        }

        private void OnDisable()
        {
            ResetTarget();
        }

        private void OnDestroy()
        {
            Destroy(panelTexture);
            Destroy(tabTexture);
            Destroy(selectedTexture);
            Destroy(utilityTexture);
            Destroy(infoTexture);
        }

        private void CacheTargetTransform()
        {
            if (target == null)
            {
                return;
            }
            targetPosition = target.transform.position;
            targetRotation = target.transform.rotation;
            targetScale = target.transform.localScale;
        }

        private static string GetShortName(string prefabName)
        {
            return prefabName switch
            {
                "BAFeel_DirectHit" => "직접",
                "BAFeel_SweepHit" => "횡베기",
                "BAFeel_PierceHit" => "관통",
                "BAFeel_SlamHit" => "내려찍기",
                "BAFeel_BlastHit" => "폭발",
                "BAFeel_RapidHit" => "연타",
                "BAFeel_WaveHit" => "파동",
                _ => prefabName
            };
        }

        private static string GetDisplayName(string prefabName)
        {
            return prefabName switch
            {
                "BAFeel_DirectHit" => "직접타격",
                "BAFeel_SweepHit" => "횡베기",
                "BAFeel_PierceHit" => "관통",
                "BAFeel_SlamHit" => "내려찍기",
                "BAFeel_BlastHit" => "폭발",
                "BAFeel_RapidHit" => "연타",
                "BAFeel_WaveHit" => "파동",
                _ => prefabName
            };
        }

        private static string GetAssignedAttackIds(string prefabName)
        {
            return prefabName switch
            {
                "BAFeel_DirectHit" => "BA_M_01, BA_M_05, BA_R_04",
                "BAFeel_SweepHit" => "BA_M_02",
                "BAFeel_PierceHit" => "BA_M_03, BA_R_01, BA_R_02, BA_R_05, BA_S_01, BA_S_03",
                "BAFeel_SlamHit" => "BA_M_04",
                "BAFeel_BlastHit" => "BA_R_03",
                "BAFeel_RapidHit" => "BA_M_06, BA_S_02",
                "BAFeel_WaveHit" => "BA_S_04",
                _ => "미배정"
            };
        }
    }
}