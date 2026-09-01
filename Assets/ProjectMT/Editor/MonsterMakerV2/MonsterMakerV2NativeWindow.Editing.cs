using System;
using System.Linq;
using ProjectMT.EditorTools.MonsterMaker;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    public sealed partial class MonsterMakerV2Window
    {
        private void OpenDraftInternal(MonsterMakerDraft source)
        {
            if (source == null || state == null)
            {
                return;
            }

            if (state.SourceDraft != source && !CanLeaveCurrentDraft())
            {
                return;
            }

            state.Load(source);
            selectedDefinition = FindDefinition(source.MonsterId);
            BindCurrentDraft();
            ApplySearch(searchField?.value);
        }

        private void BindCurrentDraft()
        {
            if (state?.WorkingDraft == null || draftView == null)
            {
                return;
            }

            draftView.Bind(
                state.SerializedDraft,
                state.WorkingDraft,
                state.SourceDraft,
                !state.IsNew,
                OnDraftChanged);
            preview.SetDraft(state.WorkingDraft);
            BuildAttackButtons();
            ShowValidation(null);
            CaptureRecovery();
            UpdateAllUi();
            previewIMGUI?.MarkDirtyRepaint();
        }

        private void OnDraftChanged()
        {
            state.MarkChanged();
            EditorApplication.delayCall -= RefreshDraftPreview;
            EditorApplication.delayCall += RefreshDraftPreview;
            CaptureRecovery();
            UpdateDirtyUi();
            UpdateProfileSummary();
            ShowValidation(null);
        }

        private void RefreshDraftPreview()
        {
            if (state?.WorkingDraft == null || preview == null)
            {
                return;
            }

            preview.SetDraft(state.WorkingDraft);
            BuildAttackButtons();
            previewIMGUI?.MarkDirtyRepaint();
            UpdatePreviewStatus();
        }

        private void CreateNewDraft()
        {
            if (!CanLeaveCurrentDraft())
            {
                return;
            }

            state.CreateNew();
            selectedDefinition = null;
            suppressCatalogSelection = true;
            catalogList.ClearSelection();
            suppressCatalogSelection = false;
            BindCurrentDraft();
            catalogStatus.text = "새 제작 원본 · 저장 전 작업 사본";
        }

        private bool TrySaveDraft()
        {
            if (state == null)
            {
                return false;
            }

            if (!state.TrySave(out var error))
            {
                EditorUtility.DisplayDialog("Monster Maker V2 저장 실패", error, "확인");
                return false;
            }

            ClearRecovery();
            CaptureRecovery();
            UpdateDirtyUi();
            ReloadCatalog(true);
            BindCurrentDraft();
            ShowNotification(new GUIContent("제작 원본을 저장했습니다."));
            return true;
        }

        private void DiscardCurrentChanges()
        {
            if (state == null || !state.IsDirty)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "변경 폐기",
                    "현재 V2 작업 사본의 변경 사항을 버리고 마지막 저장 상태로 돌아갈까요?",
                    "변경 폐기",
                    "취소"))
            {
                return;
            }

            state.DiscardChanges();
            BindCurrentDraft();
            ClearRecovery();
        }

        private void RestoreInitialDraft()
        {
            if (state?.WorkingDraft == null ||
                !EditorUtility.DisplayDialog(
                    "초기 상태 복원",
                    "이 제작 원본을 V2에서 처음 열었을 때의 상태로 되돌릴까요?",
                    "복원",
                    "취소"))
            {
                return;
            }

            state.RestoreInitial();
            BindCurrentDraft();
        }

        private void ValidateDraft()
        {
            if (state?.WorkingDraft != null)
            {
                ShowValidation(state.Validate());
            }
        }

        private void PublishDraft()
        {
            if (state?.WorkingDraft == null)
            {
                return;
            }

            var report = state.Validate();
            ShowValidation(report);
            if (report.HasErrors)
            {
                EditorUtility.DisplayDialog(
                    "전투 반영 중단",
                    "입력 오류를 먼저 수정해 주세요.",
                    "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "전투 반영 확인",
                    $"{state.WorkingDraft.MonsterId}의 제작 원본을 저장하고 " +
                    "전투 자산과 Catalog를 갱신할까요?",
                    "저장 후 반영",
                    "취소"))
            {
                return;
            }

            if (!state.TryBuildAndRegister(out var result, out var error))
            {
                EditorUtility.DisplayDialog("전투 반영 실패", error, "확인");
                ShowValidation(state.Validation);
                return;
            }

            ClearRecovery();
            CaptureRecovery();
            ReloadCatalog(true);
            BindCurrentDraft();
            var action = result.UpdatedExisting ? "갱신" : "생성";
            EditorUtility.DisplayDialog(
                "전투 반영 완료",
                $"{result.Definition.DisplayName} 전투 자산을 {action}했습니다.\n" +
                $"산출물 {result.OutputPaths.Count}개",
                "확인");
        }

        private void ShowValidation(MonsterMakerValidationReport report)
        {
            if (validationList == null)
            {
                return;
            }

            validationList.Clear();
            if (report == null)
            {
                validationSummary.text = "입력 검증 전";
                validationCard.style.display = DisplayStyle.None;
                bottomWorkspace.RemoveFromClassList("bottom-workspace--validation");
                return;
            }

            validationCard.style.display = DisplayStyle.Flex;
            bottomWorkspace.AddToClassList("bottom-workspace--validation");
            var errors = report.Issues.Count(
                issue => issue.Severity == MonsterMakerIssueSeverity.Error);
            var warnings = report.Issues.Count - errors;
            validationSummary.text = report.Issues.Count == 0
                ? "검증 통과 · 오류/경고 없음"
                : $"오류 {errors} · 경고 {warnings}";

            if (report.Issues.Count == 0)
            {
                var success = new Label("전투 반영 전 검사를 통과했습니다.");
                success.AddToClassList("validation-item--success");
                validationList.Add(success);
                commandDetailsScroll.schedule.Execute(() =>
                    commandDetailsScroll.ScrollTo(validationCard));
                return;
            }

            foreach (var issue in report.Issues)
            {
                var capturedContext = issue.Context;
                var item = new Button(() => PingIssueContext(capturedContext))
                {
                    text = $"[{issue.Code}] {issue.Message}"
                };
                item.AddToClassList("validation-item");
                item.AddToClassList(issue.Severity == MonsterMakerIssueSeverity.Error
                    ? "validation-item--error"
                    : "validation-item--warning");
                validationList.Add(item);
            }

            commandDetailsScroll.schedule.Execute(() =>
                commandDetailsScroll.ScrollTo(validationCard));
        }

        private void OpenBasicWorkshop()
        {
            if (state?.WorkingDraft != null)
            {
                MonsterWorkshopV2Window.OpenBasic(
                    state.WorkingDraft,
                    state.WorkingDraft.BasicAttackProfile);
            }
        }

        private void OpenActiveWorkshop(bool effectMode)
        {
            if (state?.WorkingDraft != null)
            {
                if (effectMode)
                {
                    MonsterWorkshopV2Window.OpenEffect(
                        state.WorkingDraft.ActiveEffectProfile,
                        state.WorkingDraft);
                }
                else
                {
                    MonsterWorkshopV2Window.OpenAttack(
                        state.WorkingDraft.ActiveAttackProfile,
                        state.WorkingDraft);
                }
            }
        }

        private void ShowBasicAttackArea()
        {
            if (preview == null || !preview.ShowBasicAttackArea())
            {
                ShowNotification(new GUIContent("표시할 기본공격 판정이 없습니다."));
            }
            previewIMGUI?.MarkDirtyRepaint();
        }

        private void SynchronizeActiveRuntime()
        {
            if (state?.WorkingDraft == null || state.WorkingDraft.ActiveAttackProfile == null)
            {
                ShowNotification(new GUIContent("공격형 액티브 프리셋을 먼저 선택하세요."));
                return;
            }

            // 조립소에서 Step 구조가 바뀐 경우 검증보다 먼저 작업 사본을 자동 재조립한다.
            // 그래야 "Step 수가 다릅니다" 경고를 해결하려고 누른 갱신 버튼이 같은 경고에 막히지 않는다.
            state.SynchronizeActiveAttackAuthoring();
            BindCurrentDraft();
            var preflight = MonsterMakerValidator.ValidateActiveAttack(state.WorkingDraft);
            if (preflight.HasErrors)
            {
                ShowValidation(preflight);
                ShowActiveRuntimePreflightFailure(preflight);
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "공격 액티브 게임 자산 갱신",
                    "현재 V2 작업값을 원본에 저장하고 공격·모션·VFX/SFX 게임 자산을 함께 갱신할까요?",
                    "저장하고 갱신",
                    "취소"))
            {
                return;
            }

            if (!state.TrySave(out var saveError))
            {
                EditorUtility.DisplayDialog("액티브 반영 실패", saveError, "확인");
                return;
            }

            try
            {
                var synchronized = MonsterMakerAssetWriter.SynchronizeActiveAttackRuntime(
                    state.SourceDraft);
                ClearRecovery();
                CaptureRecovery();
                ReloadCatalog(true);
                Selection.activeObject = synchronized;
                EditorGUIUtility.PingObject(synchronized);
                BindCurrentDraft();
                ShowNotification(new GUIContent("액티브 공격·모션·연출 게임 자산 반영 완료"));
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("액티브 반영 실패", exception.Message, "확인");
            }
        }

        private void ShowActiveRuntimePreflightFailure(MonsterMakerValidationReport report)
        {
            var errors = report.Issues
                .Where(issue => issue.Severity == MonsterMakerIssueSeverity.Error)
                .ToArray();
            var visibleErrors = errors
                .Take(5)
                .Select(issue => $"• {issue.Message}")
                .ToArray();
            var remaining = errors.Length > visibleErrors.Length
                ? $"\n• 그 외 {errors.Length - visibleErrors.Length}개"
                : string.Empty;
            EditorUtility.DisplayDialog(
                "공격 액티브 갱신 전 설정 확인",
                $"게임 자산 갱신 전에 오류 {errors.Length}개를 해결해야 합니다.\n\n" +
                string.Join("\n", visibleErrors) + remaining +
                "\n\n하단 입력 검증에서 전체 항목을 확인할 수 있습니다.",
                "확인");
        }

        private void OpenPositionAdjust(
            string propertyPath,
            string label,
            MonsterMakerPreviewPositionValueMode valueMode,
            MonsterMakerPreviewAnchor anchor)
        {
            if (state?.WorkingDraft == null ||
                !MonsterMakerV2AdjustmentWindow.CanOpen(state.WorkingDraft))
            {
                ShowNotification(new GUIContent("먼저 3D 모델 프리팹을 지정하세요."));
                return;
            }

            if (preview.IsPlaying)
            {
                ShowNotification(new GUIContent("좌표 조절 전에 Preview를 일시정지하세요."));
                return;
            }

            state.SerializedDraft.ApplyModifiedProperties();
            var property = state.SerializedDraft.FindProperty(propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.Vector3)
            {
                return;
            }

            var targetDraft = state.WorkingDraft;
            var binding = new MonsterMakerPreviewPositionBinding(
                propertyPath,
                label,
                valueMode,
                anchor);
            MonsterMakerV2AdjustmentWindow.OpenPosition(
                this,
                targetDraft,
                binding,
                property.vector3Value,
                value => ApplyPositionValue(targetDraft, binding, value));
        }

        private bool ApplyPositionValue(
            MonsterMakerDraft targetDraft,
            MonsterMakerPreviewPositionBinding binding,
            Vector3 value)
        {
            if (state?.WorkingDraft != targetDraft || preview?.IsPlaying == true)
            {
                return false;
            }

            state.SerializedDraft.UpdateIfRequiredOrScript();
            var property = state.SerializedDraft.FindProperty(binding.PropertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.Vector3)
            {
                return false;
            }

            Undo.RecordObject(targetDraft, $"{binding.Label} 좌표 조절");
            property.vector3Value = value;
            state.SerializedDraft.ApplyModifiedPropertiesWithoutUndo();
            state.MarkChanged();
            preview.ApplyDraftPositionOverrides();
            previewIMGUI?.MarkDirtyRepaint();
            CaptureRecovery();
            UpdateDirtyUi();
            UpdateProfileSummary();
            return true;
        }

        private void OpenVfxAdjust(
            MonsterBasicAttackVfxSlot slot,
            string bindingPath)
        {
            if (state?.WorkingDraft == null ||
                !MonsterMakerV2AdjustmentWindow.CanOpen(state.WorkingDraft))
            {
                ShowNotification(new GUIContent("먼저 3D 모델 프리팹을 지정하세요."));
                return;
            }

            if (preview.IsPlaying)
            {
                ShowNotification(new GUIContent("VFX 조절 전에 Preview를 일시정지하세요."));
                return;
            }

            state.SerializedDraft.ApplyModifiedProperties();
            var binding = state.SerializedDraft.FindProperty(bindingPath);
            var prefab = binding?.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
            if (binding == null || prefab == null)
            {
                ShowNotification(new GUIContent("먼저 VFX Prefab을 배정하세요."));
                return;
            }

            var position = binding.FindPropertyRelative("localPosition");
            var euler = binding.FindPropertyRelative("localEulerAngles");
            var scale = binding.FindPropertyRelative("scale");
            var lifetime = binding.FindPropertyRelative("lifetime");
            var playbackOffset = binding.FindPropertyRelative("playbackOffset");
            var playbackSpeed = binding.FindPropertyRelative("playbackSpeed");
            var targetDraft = state.WorkingDraft;
            var positionBinding = new MonsterMakerPreviewPositionBinding(
                position.propertyPath,
                slot.DisplayName,
                MonsterMakerPreviewPositionValueMode.AnchorOffset,
                ResolveVfxAnchor(slot.Anchor));
            MonsterMakerV2AdjustmentWindow.OpenVfx(
                this,
                targetDraft,
                positionBinding,
                prefab,
                position.vector3Value,
                euler.vector3Value,
                scale.floatValue,
                lifetime.floatValue,
                playbackOffset.floatValue,
                playbackSpeed.floatValue,
                (nextPosition, nextEuler, nextScale, nextLifetime, nextOffset, nextSpeed) =>
                    ApplyVfxValues(
                        targetDraft,
                        bindingPath,
                        slot,
                        nextPosition,
                        nextEuler,
                        nextScale,
                        nextLifetime,
                        nextOffset,
                        nextSpeed));
        }

        private bool ApplyVfxValues(
            MonsterMakerDraft targetDraft,
            string bindingPath,
            MonsterBasicAttackVfxSlot slot,
            Vector3 position,
            Vector3 euler,
            float scale,
            float lifetime,
            float playbackOffset,
            float playbackSpeed)
        {
            if (state?.WorkingDraft != targetDraft || preview?.IsPlaying == true)
            {
                return false;
            }

            state.SerializedDraft.UpdateIfRequiredOrScript();
            var binding = state.SerializedDraft.FindProperty(bindingPath);
            if (binding == null)
            {
                return false;
            }

            Undo.RecordObject(targetDraft, $"{slot.DisplayName} VFX 보정");
            binding.FindPropertyRelative("localPosition").vector3Value = position;
            binding.FindPropertyRelative("localEulerAngles").vector3Value = euler;
            binding.FindPropertyRelative("scale").floatValue = Mathf.Max(0.01f, scale);
            binding.FindPropertyRelative("lifetime").floatValue = Mathf.Max(0.01f, lifetime);
            binding.FindPropertyRelative("playbackOffset").floatValue = Mathf.Max(0f, playbackOffset);
            binding.FindPropertyRelative("playbackSpeed").floatValue = Mathf.Max(0.01f, playbackSpeed);
            state.SerializedDraft.ApplyModifiedPropertiesWithoutUndo();
            state.MarkChanged();
            preview.SetDraft(targetDraft);
            previewIMGUI?.MarkDirtyRepaint();
            CaptureRecovery();
            UpdateDirtyUi();
            return true;
        }

        private static MonsterMakerPreviewAnchor ResolveVfxAnchor(
            MonsterBasicAttackVfxAnchor anchor)
        {
            return anchor switch
            {
                MonsterBasicAttackVfxAnchor.AttackOrigin or
                MonsterBasicAttackVfxAnchor.MarkerSocket or
                MonsterBasicAttackVfxAnchor.ProjectileRoot or
                MonsterBasicAttackVfxAnchor.TrajectoryOrigin =>
                    MonsterMakerPreviewAnchor.AttackOrigin,
                MonsterBasicAttackVfxAnchor.TargetRoot or
                MonsterBasicAttackVfxAnchor.HitPoint or
                MonsterBasicAttackVfxAnchor.AreaCenter =>
                    MonsterMakerPreviewAnchor.HitCenter,
                _ => MonsterMakerPreviewAnchor.Root
            };
        }

        private void OpenFeedbackVfxAdjust(
            string feedbackPath,
            string label,
            MonsterMakerPreviewAnchor anchor)
        {
            if (state?.WorkingDraft == null ||
                !MonsterMakerV2AdjustmentWindow.CanOpen(state.WorkingDraft))
            {
                ShowNotification(new GUIContent("먼저 3D 모델 프리팹을 지정하세요."));
                return;
            }
            if (preview.IsPlaying)
            {
                ShowNotification(new GUIContent("VFX 조절 전에 Preview를 일시정지하세요."));
                return;
            }

            state.SerializedDraft.ApplyModifiedProperties();
            var feedback = state.SerializedDraft.FindProperty(feedbackPath);
            var prefab = feedback?.FindPropertyRelative("vfxPrefab").objectReferenceValue as GameObject;
            if (feedback == null || prefab == null)
            {
                ShowNotification(new GUIContent("먼저 VFX Prefab을 배정하세요."));
                return;
            }

            var position = feedback.FindPropertyRelative("localPosition");
            var euler = feedback.FindPropertyRelative("localEulerAngles");
            var scale = feedback.FindPropertyRelative("scale");
            var lifetime = feedback.FindPropertyRelative("vfxLifetime");
            var targetDraft = state.WorkingDraft;
            var positionBinding = new MonsterMakerPreviewPositionBinding(
                position.propertyPath,
                label,
                MonsterMakerPreviewPositionValueMode.AnchorOffset,
                anchor);
            MonsterMakerV2AdjustmentWindow.OpenVfx(
                this,
                targetDraft,
                positionBinding,
                prefab,
                position.vector3Value,
                euler.vector3Value,
                scale.floatValue,
                lifetime.floatValue,
                0f,
                1f,
                (nextPosition, nextEuler, nextScale, nextLifetime, _, _) =>
                    ApplyFeedbackVfxValues(
                        targetDraft,
                        feedbackPath,
                        label,
                        nextPosition,
                        nextEuler,
                        nextScale,
                        nextLifetime));
        }

        private bool ApplyFeedbackVfxValues(
            MonsterMakerDraft targetDraft,
            string feedbackPath,
            string label,
            Vector3 position,
            Vector3 euler,
            float scale,
            float lifetime)
        {
            if (state?.WorkingDraft != targetDraft || preview?.IsPlaying == true)
            {
                return false;
            }

            state.SerializedDraft.UpdateIfRequiredOrScript();
            var feedback = state.SerializedDraft.FindProperty(feedbackPath);
            if (feedback == null)
            {
                return false;
            }

            Undo.RecordObject(targetDraft, $"{label} VFX 보정");
            feedback.FindPropertyRelative("localPosition").vector3Value = position;
            feedback.FindPropertyRelative("localEulerAngles").vector3Value = euler;
            feedback.FindPropertyRelative("scale").floatValue = Mathf.Max(0.01f, scale);
            feedback.FindPropertyRelative("vfxLifetime").floatValue = Mathf.Max(0.01f, lifetime);
            state.SerializedDraft.ApplyModifiedPropertiesWithoutUndo();
            state.MarkChanged();
            preview.SetDraft(targetDraft);
            previewIMGUI?.MarkDirtyRepaint();
            CaptureRecovery();
            UpdateDirtyUi();
            return true;
        }

        private void OnWorkshopAssigned()
        {
            if (state?.WorkingDraft == null)
            {
                return;
            }

            state.WorkingDraft.EditorSyncActiveAttackAuthoring();
            state.WorkingDraft.EditorSyncActiveEffectAuthoring();
            state.RefreshAfterUndo();
            BindCurrentDraft();
        }

        private void OnUndoRedo()
        {
            if (state?.WorkingDraft == null)
            {
                return;
            }

            state.RefreshAfterUndo();
            BindCurrentDraft();
        }

        private bool CanLeaveCurrentDraft()
        {
            if (state == null || !state.IsDirty)
            {
                return true;
            }

            var choice = EditorUtility.DisplayDialogComplex(
                "저장되지 않은 V2 작업",
                "현재 작업 사본에 저장되지 않은 변경 사항이 있습니다.",
                "원본 저장",
                "계속 편집",
                "변경 폐기");
            if (choice == 0)
            {
                return TrySaveDraft();
            }

            if (choice == 2)
            {
                state.DiscardChanges();
                ClearRecovery();
                return true;
            }

            return false;
        }

        private static void PingIssueContext(UnityEngine.Object context)
        {
            if (context == null)
            {
                return;
            }

            Selection.activeObject = context;
            EditorGUIUtility.PingObject(context);
        }
    }
}
