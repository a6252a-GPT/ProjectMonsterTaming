#if UNITY_EDITOR
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Integrations.Feel.Editor
{
    public static class MonsterActiveFocusFeelAssetUtility // 컷인 전용 FEEL 프리셋 생성
    {
        private const string FeelRoot = "Assets/ProjectMT/05_Art/FeelPresets";
        private const string ActiveFocusFolder = FeelRoot + "/ActiveFocus";
        private const string PrefabPath = ActiveFocusFolder + "/PF_MonsterActiveFocusFeel.prefab";
        private const string EdgeMaterialPath =
            "Assets/ProjectMT/02_Shared/Combat/Presentation/MAT_MonsterActiveFocusEdgeFade.mat";

        [MenuItem("Tools/ProjectMT/Combat/Build Active Focus FEEL")]
        public static void BuildAssets()
        {
            EnsureFolders();
            var edgeMaterial = EnsureEdgeFadeMaterial();
            var root = new GameObject(
                "PF_MonsterActiveFocusFeel",
                typeof(RectTransform),
                typeof(MonsterActiveFocusFeelAdapter));
            try
            {
                SetStretchRect(root.GetComponent<RectTransform>());

                var screenFlash = CreateImage("ScreenGradeFlash", root.transform, Color.clear);
                SetStretchRect(screenFlash.rectTransform);

                var panelPulse = CreateImage("PanelPulse", root.transform, Color.clear);
                SetLeftRect(panelPulse.rectTransform, new Vector2(20f, 0f), new Vector2(770f, 480f));
                panelPulse.material = edgeMaterial;

                var energySweep = CreateImage("EnergySweep", root.transform, Color.clear);
                SetLeftRect(energySweep.rectTransform, new Vector2(-220f, 92f), new Vector2(620f, 18f));
                energySweep.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
                energySweep.material = edgeMaterial;

                var releaseGlow = CreateImage("ReleaseGlow", root.transform, Color.clear);
                SetLeftRect(releaseGlow.rectTransform, new Vector2(20f, 0f), new Vector2(790f, 490f));
                releaseGlow.material = edgeMaterial;

                var entryPlayer = CreatePlayer("EntryFEEL", root.transform);
                entryPlayer.FeedbacksList = new List<MMF_Feedback>
                {
                    CreateImageFeedback("화면 등급색 플래시", screenFlash, 0.18f, 0.42f),
                    CreateImageFeedback("컷인 광량 펄스", panelPulse, 0.30f, 0.30f),
                    CreateScaleFeedback("컷인 미세 확장", panelPulse.rectTransform, 0.32f, 1.028f),
                    CreateImageFeedback("광선 스윕 발광", energySweep, 0.38f, 0.42f),
                    CreatePositionFeedback("광선 스윕 이동", energySweep, 0.38f)
                };

                var releasePlayer = CreatePlayer("ReleaseFEEL", root.transform);
                releasePlayer.FeedbacksList = new List<MMF_Feedback>
                {
                    CreateImageFeedback("종료 잔광", releaseGlow, 0.20f, 0.35f),
                    CreateScaleFeedback("종료 잔광 확장", releaseGlow.rectTransform, 0.20f, 1.045f)
                };

                root.GetComponent<MonsterActiveFocusFeelAdapter>().EditorConfigure(
                    entryPlayer,
                    releasePlayer,
                    screenFlash,
                    panelPulse,
                    energySweep,
                    releaseGlow);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null)
                {
                    throw new System.InvalidOperationException("Failed to save active focus FEEL prefab.");
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"[MonsterActiveFocus] FEEL preset ready. Prefab={PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static MMF_Player CreatePlayer(string objectName, Transform parent)
        {
            var gameObject = new GameObject(objectName, typeof(RectTransform), typeof(MMF_Player));
            gameObject.transform.SetParent(parent, false);
            var player = gameObject.GetComponent<MMF_Player>();
            player.AutoPlayOnEnable = false;
            player.AutoPlayOnStart = false;
            player.AutoInitialization = false;
            player.InitializationMode = MMFeedbacks.InitializationModes.Script;
            player.StopFeedbacksOnDisable = true;
            player.RestoreInitialValuesOnDisable = true;
            player.ForceTimescaleMode = true;
            player.ForcedTimescaleMode = TimescaleModes.Unscaled;
            return player;
        }

        private static MMF_Image CreateImageFeedback(
            string label,
            Image target,
            float duration,
            float peakTime)
        {
            var feedback = Prepare(new MMF_Image(), label);
            feedback.BoundImage = target;
            feedback.Mode = MMF_Image.Modes.OverTime;
            feedback.Duration = duration;
            feedback.ModifyColor = true;
            feedback.ColorOverTime = CreateGradient(new Color(1f, 0.84f, 0.35f), 0.12f, peakTime);
            feedback.EnableOnPlay = true;
            feedback.DisableOnInit = false;
            feedback.DisableOnSequenceEnd = false;
            feedback.DisableOnStop = false;
            return feedback;
        }

        private static MMF_Scale CreateScaleFeedback(
            string label,
            RectTransform target,
            float duration,
            float destinationScale)
        {
            var feedback = Prepare(new MMF_Scale(), label);
            feedback.Mode = MMF_Scale.Modes.ToDestination;
            feedback.MovementMode = MMF_Scale.MovementModes.Duration;
            feedback.AnimateScaleTarget = target;
            feedback.AnimateScaleDuration = duration;
            feedback.DetermineScaleOnPlay = true;
            feedback.DestinationScale = Vector3.one * destinationScale;
            feedback.UniformScaling = true;
            feedback.AnimateX = true;
            feedback.AnimateY = true;
            feedback.AnimateZ = true;
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 2.4f),
                new Keyframe(1f, 1f, 0f, 0f));
            feedback.AnimateScaleTweenX = new MMTweenType(curve);
            feedback.AnimateScaleTweenY = new MMTweenType(curve);
            feedback.AnimateScaleTweenZ = new MMTweenType(curve);
            return feedback;
        }

        private static MMF_Position CreatePositionFeedback(
            string label,
            Image target,
            float duration)
        {
            var feedback = Prepare(new MMF_Position(), label);
            feedback.AnimatePositionTarget = target.gameObject;
            feedback.Mode = MMF_Position.Modes.AtoB;
            feedback.Space = MMF_Position.Spaces.RectTransform;
            feedback.MovementMode = MMF_Position.MovementModes.Duration;
            feedback.AnimatePositionDuration = duration;
            feedback.RelativePosition = false;
            feedback.DeterminePositionsOnPlay = false;
            feedback.InitialPosition = new Vector3(-220f, 92f, 0f);
            feedback.DestinationPosition = new Vector3(720f, -18f, 0f);
            feedback.AnimatePositionTween = new MMTweenType(
                new AnimationCurve(
                    new Keyframe(0f, 0f, 0f, 2.5f),
                    new Keyframe(1f, 1f, 0f, 0f)));
            return feedback;
        }

        private static T Prepare<T>(T feedback, string label) where T : MMF_Feedback
        {
            feedback.Label = label;
            feedback.Active = true;
            feedback.Chance = 100f;
            feedback.Timing = new MMFeedbackTiming
            {
                TimescaleMode = TimescaleModes.Unscaled,
                InitialDelay = 0f,
                InterruptsOnStop = true
            };
            return feedback;
        }

        private static Gradient CreateGradient(Color color, float peakAlpha, float peakTime)
        {
            color.a = 1f;
            return new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, peakTime),
                    new GradientColorKey(color, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(peakAlpha, peakTime),
                    new GradientAlphaKey(0f, 1f)
                }
            };
        }

        private static Material EnsureEdgeFadeMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(EdgeMaterialPath);
            var shader = Shader.Find("ProjectMT/UI/MonsterActiveFocusEdgeFade");
            if (shader == null)
            {
                throw new System.InvalidOperationException("Monster active focus edge fade shader is unavailable.");
            }
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "MAT_MonsterActiveFocusEdgeFade"
                };
                AssetDatabase.CreateAsset(material, EdgeMaterialPath);
            }
            else
            {
                material.shader = shader;
            }
            material.SetFloat("_LeftFeather", 0.08f);
            material.SetFloat("_RightFeather", 0.14f);
            material.SetFloat("_TopFeather", 0.11f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(FeelRoot))
            {
                AssetDatabase.CreateFolder("Assets/ProjectMT/05_Art", "FeelPresets");
            }
            if (!AssetDatabase.IsValidFolder(ActiveFocusFolder))
            {
                AssetDatabase.CreateFolder(FeelRoot, "ActiveFocus");
            }
        }

        private static Image CreateImage(string objectName, Transform parent, Color color)
        {
            var gameObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void SetStretchRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetLeftRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
#endif
