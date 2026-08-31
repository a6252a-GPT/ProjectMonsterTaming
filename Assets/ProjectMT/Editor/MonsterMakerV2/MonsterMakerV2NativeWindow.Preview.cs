using System;
using System.Linq;
using ProjectMT.EditorTools.MonsterMaker;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    public sealed partial class MonsterMakerV2Window
    {
        private void CreatePreviewSurface()
        {
            previewIMGUI = new IMGUIContainer(() =>
            {
                var rect = previewIMGUI.contentRect;
                var previewRect = new Rect(0f, 0f, rect.width, rect.height);
                preview.Draw(previewRect);
                DrawPreviewReferenceOverlay(previewRect);
            })
            {
                name = "preview-imgui"
            };
            previewIMGUI.AddToClassList("preview-imgui");
            previewRenderHost.Insert(0, previewIMGUI);
        }

        private void DrawPreviewReferenceOverlay(Rect previewRect)
        {
            var draft = state?.WorkingDraft;
            if (preview?.Camera == null || draft == null)
            {
                return;
            }

            var toolbarRect = MonsterPositionReferenceOverlay.DrawVisibilityToolbar(
                previewRect,
                255f,
                ref showPreviewModelReference,
                ref showPreviewAttackReference,
                ref showPreviewHitReference);

            DrawPreviewReferencePoint(
                previewRect,
                MonsterMakerPreviewReference.Model,
                "모델 기준",
                draft.VisualLocalPosition + Vector3.up * draft.GroundOffset,
                MonsterPositionReferenceOverlay.ModelColor,
                showPreviewModelReference);
            DrawPreviewReferencePoint(
                previewRect,
                MonsterMakerPreviewReference.Attack,
                "공격 기준",
                draft.AttackOriginLocalPosition,
                MonsterPositionReferenceOverlay.AttackColor,
                showPreviewAttackReference);
            DrawPreviewReferencePoint(
                previewRect,
                MonsterMakerPreviewReference.Hit,
                "피격 기준",
                draft.HitCenterLocalPosition,
                MonsterPositionReferenceOverlay.HitColor,
                showPreviewHitReference);
            DrawPreviewReferenceInfoCard(previewRect, toolbarRect, draft);
        }

        private void DrawPreviewReferencePoint(
            Rect previewRect,
            MonsterMakerPreviewReference reference,
            string label,
            Vector3 localPosition,
            Color color,
            bool visible)
        {
            if (!visible ||
                !preview.TryGetWorldPoint(
                    MonsterMakerPreviewAnchor.Root,
                    string.Empty,
                    localPosition,
                    out var worldPosition) ||
                !MonsterPositionReferenceOverlay.TryGetGuiPoint(
                    preview.Camera,
                    previewRect,
                    worldPosition,
                    out var guiPoint))
            {
                return;
            }

            var selected = selectedPreviewReference == reference &&
                           EditorApplication.timeSinceStartup < previewReferenceInfoExpiresAt;
            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                Vector2.Distance(Event.current.mousePosition, guiPoint) <= 10f)
            {
                selectedPreviewReference = reference;
                previewReferenceInfoExpiresAt = EditorApplication.timeSinceStartup + 3d;
                selected = true;
                Event.current.Use();
                previewIMGUI?.MarkDirtyRepaint();
            }

            MonsterPositionReferenceOverlay.DrawPoint(guiPoint, color, selected);
        }

        private void DrawPreviewReferenceInfoCard(
            Rect previewRect,
            Rect toolbarRect,
            MonsterMakerDraft draft)
        {
            if (selectedPreviewReference == MonsterMakerPreviewReference.None ||
                EditorApplication.timeSinceStartup >= previewReferenceInfoExpiresAt)
            {
                return;
            }

            string label;
            Vector3 value;
            Color color;
            switch (selectedPreviewReference)
            {
                case MonsterMakerPreviewReference.Model:
                    label = "모델 기준";
                    value = draft.VisualLocalPosition;
                    color = MonsterPositionReferenceOverlay.ModelColor;
                    break;
                case MonsterMakerPreviewReference.Attack:
                    label = "공격 기준";
                    value = draft.AttackOriginLocalPosition;
                    color = MonsterPositionReferenceOverlay.AttackColor;
                    break;
                case MonsterMakerPreviewReference.Hit:
                    label = "피격 기준";
                    value = draft.HitCenterLocalPosition;
                    color = MonsterPositionReferenceOverlay.HitColor;
                    break;
                default:
                    return;
            }

            var width = Mathf.Min(232f, Mathf.Max(120f, previewRect.width - 20f));
            var cardRect = new Rect(
                previewRect.xMax - 10f - width,
                toolbarRect.yMax + 6f,
                width,
                42f);
            EditorGUI.DrawRect(cardRect, new Color(0.025f, 0.035f, 0.05f, 0.9f));
            EditorGUI.DrawRect(new Rect(cardRect.x, cardRect.y, 3f, cardRect.height), color);
            GUI.Label(
                new Rect(cardRect.x + 9f, cardRect.y + 3f, cardRect.width - 14f, 17f),
                label,
                EditorStyles.miniBoldLabel);
            GUI.Label(
                new Rect(cardRect.x + 9f, cardRect.y + 21f, cardRect.width - 14f, 17f),
                $"X {value.x:0.###}  ·  Y {value.y:0.###}  ·  Z {value.z:0.###}",
                EditorStyles.miniLabel);
        }

        private void ConfigurePreviewControls()
        {
            var choices = Enumerable.Range(0, PrefabPreviewStage.EnvironmentCount)
                .Select(PrefabPreviewStage.GetEnvironmentLabel)
                .ToList();
            environmentField.choices = choices;
            if (choices.Count > 0)
            {
                var index = Mathf.Clamp(preview.EnvironmentIndex, 0, choices.Count - 1);
                environmentField.SetValueWithoutNotify(choices[index]);
            }

            environmentField.RegisterValueChangedCallback(evt =>
            {
                var index = environmentField.choices.IndexOf(evt.newValue);
                if (index < 0)
                {
                    return;
                }

                preview.SetEnvironment(index);
                previewIMGUI.MarkDirtyRepaint();
            });

            rootVisualElement.Q<Button>("view-front").clicked +=
                () => SetPreviewView(180f, 8f, 1f);
            rootVisualElement.Q<Button>("view-side").clicked +=
                () => SetPreviewView(90f, 8f, 1f);
            rootVisualElement.Q<Button>("view-diagonal").clicked +=
                () => SetPreviewView(145f, 10f, 1f);
            rootVisualElement.Q<Button>("play-idle").clicked +=
                () => PlayPreview(preview.PlayIdle);
            rootVisualElement.Q<Button>("play-move").clicked +=
                () => PlayPreview(preview.PlayMove);
            rootVisualElement.Q<Button>("play-death").clicked +=
                () => PlayPreview(preview.PlayDeath);
            playActiveButton.clicked += () => PlayPreview(preview.PlayActiveSkill);
            pauseButton.clicked += () => PlayPreview(preview.TogglePause);
            rootVisualElement.Q<Button>("restart-button").clicked +=
                () => PlayPreview(preview.Restart);
            rootVisualElement.Q<Button>("show-area-button").clicked += () =>
            {
                if (!preview.ShowBasicAttackArea())
                {
                    ShowNotification(new GUIContent("표시할 기본공격 판정이 없습니다."));
                }

                previewIMGUI.MarkDirtyRepaint();
            };

            timelineSlider.RegisterValueChangedCallback(evt =>
            {
                if (updatingTimeline)
                {
                    return;
                }

                preview.Scrub(evt.newValue);
                previewIMGUI.MarkDirtyRepaint();
                UpdatePreviewStatus();
            });
        }

        private void BuildAttackButtons()
        {
            attackButtons.Clear();
            var attacks = state?.WorkingDraft?.Attacks;
            var host = attackButtons.parent;
            if (attacks == null || attacks.Count == 0)
            {
                if (host != null)
                {
                    host.style.flexGrow = 1f;
                }
                var empty = new Label("공격 Motion 없음");
                empty.AddToClassList("attack-empty");
                attackButtons.Add(empty);
                return;
            }

            // 공격 버튼 수와 액티브 버튼을 같은 폭으로 나눠 가로 스크롤 없이 한 줄에 유지한다.
            if (host != null)
            {
                host.style.flexGrow = attacks.Count + 1f;
            }

            for (var index = 0; index < attacks.Count; index++)
            {
                var capturedIndex = index;
                var clipName = attacks[index]?.Clip == null ? "Clip 미지정" : attacks[index].Clip.name;
                var button = new Button(
                    () => PlayPreview(() => preview.PlayAttack(capturedIndex)))
                {
                    text = $"공격 {index + 1}",
                    tooltip = $"공격 {index + 1} · {clipName}"
                };
                button.AddToClassList("motion-button");
                attackButtons.Add(button);
            }

            var randomButton = new Button(() => PlayPreview(preview.PlayRandomAttack))
            {
                text = "랜덤",
                tooltip = "등록된 기본공격 모션 중 하나를 무작위로 재생합니다."
            };
            randomButton.AddToClassList("motion-button");
            randomButton.AddToClassList("motion-button--random");
            attackButtons.Add(randomButton);
        }

        private void PlayPreview(Action action)
        {
            action?.Invoke();
            previewIMGUI?.MarkDirtyRepaint();
            UpdatePreviewStatus();
        }

        private void SetPreviewView(float yaw, float pitch, float distanceScale)
        {
            preview.SetView(yaw, pitch, distanceScale);
            previewIMGUI.MarkDirtyRepaint();
        }

        private void OnEditorUpdate()
        {
            if (preview == null)
            {
                return;
            }

            if (preview.Tick())
            {
                previewIMGUI?.MarkDirtyRepaint();
            }

            UpdatePreviewStatus();
        }

        private void UpdatePreviewStatus()
        {
            if (preview == null || clipLabel == null)
            {
                return;
            }

            updatingTimeline = true;
            var normalized = Mathf.Clamp01(preview.NormalizedTime);
            timelineSlider.SetValueWithoutNotify(normalized);
            timelineValue.text = $"{normalized * 100f:0}%";
            updatingTimeline = false;
            clipLabel.text = string.IsNullOrWhiteSpace(preview.CurrentClipName)
                ? "선택 없음"
                : preview.CurrentClipName;
            pauseButton.text = preview.IsPlaying ? "Ⅱ 일시정지" : "▶ 계속";
            var canPlayActiveSkill = preview.CanPlayActiveSkill;
            playActiveButton.SetEnabled(canPlayActiveSkill);
            playActiveButton.parent.style.display = canPlayActiveSkill ? DisplayStyle.Flex : DisplayStyle.None;
            combatStatus.text = string.IsNullOrWhiteSpace(preview.CombatStatus)
                ? "Preview 대기"
                : preview.CombatStatus;
            combatDetail.text = BuildCombatDetail();
        }

        private string BuildCombatDetail()
        {
            if (preview == null)
            {
                return "Preview 대기";
            }

            var maximum = preview.CombatTargetMaximumHealth;
            if (maximum <= 0f)
            {
                return preview.CombatTargetLabel;
            }

            return $"{preview.CombatTargetLabel}  " +
                   $"HP {preview.CombatTargetCurrentHealth:0.#}/{maximum:0.#}" +
                   $"  ·  마지막 피해 {preview.LastAppliedDamage:0.#}" +
                   $"  ·  적중 {preview.PreviewHitCount}회";
        }
    }
}
