using System;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public sealed partial class MonsterEffectActiveWorkshopWindow
    {
        private void DrawPreview()
        {
            using (var previewScope = new EditorGUILayout.VerticalScope(
                       GUILayout.MinWidth(PreviewMinimumWidth),
                       GUILayout.ExpandWidth(true)))
            {
                if (profile == null)
                {
                    EditorGUILayout.HelpBox("왼쪽에서 프리셋을 선택하세요.", MessageType.Info);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label("효과 미리보기", EditorStyles.miniBoldLabel, GUILayout.Width(78f));
                    var labels = profile.Groups
                        .Select((group, index) => $"#{index + 1:00} {group.DisplayName}")
                        .ToArray();
                    selectedPreviewGroup = Mathf.Clamp(
                        selectedPreviewGroup,
                        0,
                        Mathf.Max(0, labels.Length - 1));
                    if (labels.Length > 0)
                    {
                        selectedPreviewGroup = EditorGUILayout.Popup(
                            selectedPreviewGroup,
                            labels,
                            GUILayout.ExpandWidth(true));
                    }
                }

                var height = Mathf.Max(430f, position.height - 300f);
                var rect = GUILayoutUtility.GetRect(
                    PreviewMinimumWidth,
                    10000f,
                    height,
                    height,
                    GUILayout.ExpandWidth(true));
                DrawEffectPreview(rect);

                using (new EditorGUI.DisabledScope(!profile.TryValidate(out _) || previewPlaying))
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (MonsterWorkshopVisualTheme.DrawTintedButton(
                            new GUIContent("선택 묶음 재생"),
                            MonsterWorkshopVisualTheme.PreviewColor,
                            30f))
                    {
                        previewAllGroups = false;
                        previewPlaying = true;
                        previewStartedAt = EditorApplication.timeSinceStartup;
                    }
                    var playAll = MonsterWorkshopVisualTheme.DrawTintedButton(
                        new GUIContent("전체 효과 재생"),
                        MonsterWorkshopVisualTheme.PrimaryColor,
                        30f);
                    if (Event.current.type == EventType.Repaint)
                    {
                        lastPreviewToolbarRightmostRect = GUILayoutUtility.GetLastRect();
                    }
                    if (playAll)
                    {
                        previewAllGroups = true;
                        previewPlaying = true;
                        previewStartedAt = EditorApplication.timeSinceStartup;
                    }
                }
                var selectedGroup = profile.Groups.Count == 0
                    ? null
                    : profile.Groups[Mathf.Clamp(selectedPreviewGroup, 0, profile.Groups.Count - 1)];
                GUILayout.Label(
                    previewPlaying
                        ? previewAllGroups
                            ? $"전체 발동 중 · {profile.Groups.Count}개 묶음을 순서대로 확인합니다."
                            : $"{selectedGroup?.DisplayName ?? "효과 묶음"} 재생 중 · 대상과 상태 변화를 확인하세요."
                        : $"{RoleBadge(profile.Role)} · 묶음 {profile.Groups.Count}개 · " +
                          $"VFX 공간 {profile.Groups.Sum(group => group.PresentationSlots.Count)}개",
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.HelpBox(
                    "대상·HP·기력·보호막·상태 변화 계약을 표시합니다. 실제 VFX/SFX 자산은 몬스터 메이커에서 연결합니다.",
                    MessageType.None);
                if (Event.current.type == EventType.Repaint)
                {
                    lastPreviewColumnRect = previewScope.rect;
                }
            }
        }

        private void DrawEffectPreview(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.045f, 0.06f, 0.08f, 1f));
            if (profile.Groups.Count == 0) return;

            var progress = previewPlaying
                ? Mathf.Clamp01((float)(EditorApplication.timeSinceStartup - previewStartedAt) /
                                ResolvePreviewDuration())
                : 0f;
            var groupIndex = Mathf.Clamp(selectedPreviewGroup, 0, profile.Groups.Count - 1);
            var localProgress = progress;
            if (previewPlaying && previewAllGroups)
            {
                var scaled = Mathf.Min(progress * profile.Groups.Count, profile.Groups.Count - 0.001f);
                groupIndex = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, profile.Groups.Count - 1);
                localProgress = scaled - Mathf.Floor(scaled);
            }

            var group = profile.Groups[groupIndex];
            var enemyTarget = IsEnemyPreviewTarget(group.Target);
            var selfOnly = group.Target == MonsterSkillTargetType.Self;
            var multiTarget = group.Target is MonsterSkillTargetType.NearbyAllies or
                MonsterSkillTargetType.AllAllies or MonsterSkillTargetType.TargetAreaEnemies;
            var hasHeal = group.Effects.Any(effect => effect?.Type == MonsterSkillEffectType.Heal);
            var hasEnergyGain = group.Effects.Any(effect => effect?.Type == MonsterSkillEffectType.EnergyGain);
            var hasEnergyDrain = group.Effects.Any(effect => effect?.Type == MonsterSkillEffectType.EnergyDrain);
            var hasShield = group.Effects.Any(effect => effect?.Type == MonsterSkillEffectType.Shield);
            var roleColor = RoleColor(profile.Role);
            var center = new Vector2(rect.center.x, rect.y + rect.height * 0.45f);
            var casterAffected = selfOnly ||
                                 !enemyTarget && group.IncludeCaster &&
                                 group.Target is MonsterSkillTargetType.NearbyAllies or
                                     MonsterSkillTargetType.AllAllies;
            DrawActor(
                center,
                roleColor,
                "시전자",
                previewPlaying && casterAffected && hasHeal ? Mathf.Lerp(0.7f, 1f, localProgress) : 1f,
                previewPlaying && casterAffected && hasEnergyGain
                    ? Mathf.Lerp(0.5f, 0.9f, localProgress)
                    : 1f,
                previewPlaying && casterAffected && hasShield ? localProgress : 0f);

            if (!selfOnly)
            {
                var targetY = enemyTarget
                    ? rect.y + rect.height * 0.2f
                    : rect.y + rect.height * 0.72f;
                var count = multiTarget ? Mathf.Clamp(group.MaxTargets, 1, 3) : 1;
                var spacing = Mathf.Min(105f, rect.width * 0.22f);
                for (var index = 0; index < count; index++)
                {
                    var offset = index - (count - 1) * 0.5f;
                    var health = previewPlaying && hasHeal
                        ? Mathf.Lerp(0.5f, 0.9f, localProgress)
                        : 0.72f;
                    var energy = previewPlaying && hasEnergyGain
                        ? Mathf.Lerp(0.35f, 0.82f, localProgress)
                        : previewPlaying && hasEnergyDrain
                            ? Mathf.Lerp(0.7f, 0.28f, localProgress)
                            : 0.46f;
                    DrawActor(
                        new Vector2(rect.center.x + offset * spacing, targetY),
                        enemyTarget
                            ? new Color(0.88f, 0.32f, 0.35f)
                            : new Color(0.35f, 0.72f, 0.95f),
                        enemyTarget ? $"적 {index + 1}" : $"아군 {index + 1}",
                        health,
                        energy,
                        previewPlaying && hasShield ? localProgress : 0f);
                }
            }

            if (previewPlaying)
            {
                var pulse = 42f + Mathf.Sin(localProgress * Mathf.PI) * 74f;
                Handles.BeginGUI();
                Handles.color = new Color(roleColor.r, roleColor.g, roleColor.b, 0.7f);
                Handles.DrawWireDisc(center, Vector3.forward, pulse);
                Handles.EndGUI();
            }

            var effects = string.Join(" · ", group.Effects
                .Where(effect => effect != null)
                .Select(effect => EffectLabel(effect.Type)));
            GUI.Label(
                new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, 42f),
                $"#{groupIndex + 1:00} {group.DisplayName} · {TargetLabel(group.Target)}\n{effects}",
                EditorStyles.centeredGreyMiniLabel);
        }

        private static bool IsEnemyPreviewTarget(MonsterSkillTargetType target) =>
            target is MonsterSkillTargetType.CurrentTarget or MonsterSkillTargetType.NearestEnemy or
                MonsterSkillTargetType.FarthestEnemy or MonsterSkillTargetType.LowestHealthEnemy or
                MonsterSkillTargetType.HighestAttackEnemy or MonsterSkillTargetType.RangedEnemyFirst or
                MonsterSkillTargetType.TargetAreaEnemies;

        private float ResolvePreviewDuration() =>
            previewAllGroups ? Mathf.Max(2.2f, profile.Groups.Count * 1.35f) : 2.2f;
        private static void DrawActor(
            Vector2 center,
            Color color,
            string label,
            float health,
            float energy,
            float shield)
        {
            EditorGUI.DrawRect(new Rect(center.x - 18f, center.y - 18f, 36f, 36f), color);
            GUI.Label(
                new Rect(center.x - 48f, center.y + 20f, 96f, 18f),
                label,
                EditorStyles.centeredGreyMiniLabel);
            DrawBar(new Rect(center.x - 38f, center.y + 39f, 76f, 6f), health,
                new Color(0.35f, 0.9f, 0.48f));
            DrawBar(new Rect(center.x - 38f, center.y + 48f, 76f, 5f), energy,
                new Color(0.35f, 0.7f, 1f));
            if (shield > 0f)
            {
                DrawBar(new Rect(center.x - 38f, center.y + 56f, 76f, 4f), shield,
                    new Color(0.72f, 0.9f, 1f));
            }
        }

        private static void DrawBar(Rect rect, float ratio, Color color)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.14f, 0.17f));
            EditorGUI.DrawRect(
                new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height),
                color);
        }

        private void TickPreview()
        {
            if (!previewPlaying) return;
            if (EditorApplication.timeSinceStartup - previewStartedAt >= ResolvePreviewDuration())
            {
                previewPlaying = false;
            }
            Repaint();
        }

        private void RefreshProfiles()
        {
            profiles.Clear();
            usages.Clear();
            if (AssetDatabase.IsValidFolder(MonsterEffectActiveAuthoringService.ProfileRoot))
            {
                foreach (var guid in AssetDatabase.FindAssets(
                             "t:MonsterEffectActiveProfile",
                             new[] { MonsterEffectActiveAuthoringService.ProfileRoot }))
                {
                    var candidate = AssetDatabase.LoadAssetAtPath<MonsterEffectActiveProfile>(
                        AssetDatabase.GUIDToAssetPath(guid));
                    if (candidate != null) profiles.Add(candidate);
                }
                profiles.Sort((left, right) =>
                {
                    var role = left.Role.CompareTo(right.Role);
                    return role != 0
                        ? role
                        : string.Compare(
                            left.DisplayName,
                            right.DisplayName,
                            StringComparison.CurrentCultureIgnoreCase);
                });
            }

            if (AssetDatabase.IsValidFolder(MonsterMakerAssetWriter.DraftRoot))
            {
                foreach (var guid in AssetDatabase.FindAssets(
                             "t:MonsterMakerDraft",
                             new[] { MonsterMakerAssetWriter.DraftRoot }))
                {
                    var draft = AssetDatabase.LoadAssetAtPath<MonsterMakerDraft>(
                        AssetDatabase.GUIDToAssetPath(guid));
                    var assigned = draft?.ActiveEffectProfile;
                    if (assigned == null) continue;
                    usages.TryGetValue(assigned, out var count);
                    usages[assigned] = count + 1;
                }
            }
            Repaint();
        }

        private bool MatchesSearch(MonsterEffectActiveProfile candidate)
        {
            if (candidate == null) return false;
            if (string.IsNullOrWhiteSpace(search)) return true;
            return candidate.ProfileId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   candidate.DisplayName.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                   RoleBadge(candidate.Role).Contains(search);
        }

        private void StartBlank()
        {
            DisposeWorkingCopy();
            loadedProfile = null;
            profile = CreateInstance<MonsterEffectActiveProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            profile.EditorConfigure(
                "new_effect",
                "새 효과형 액티브",
                "지원·수호·디버프 효과를 묶음 순서대로 조립합니다.",
                MonsterEffectActiveRole.Support,
                new[] { CreateDefaultGroup(0) });
            serializedProfile = new SerializedObject(profile);
            selectedPreviewGroup = 0;
            SetDirty(false);
            message = "새 작업 사본입니다. 저장 전에는 기존 프리셋에 영향을 주지 않습니다.";
            messageType = MessageType.Info;
        }

        private void LoadProfile(MonsterEffectActiveProfile target)
        {
            if (target == null) return;
            DisposeWorkingCopy();
            loadedProfile = target;
            profile = CreateInstance<MonsterEffectActiveProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            EditorUtility.CopySerialized(target, profile);
            serializedProfile = new SerializedObject(profile);
            selectedPreviewGroup = 0;
            SetDirty(false);
            message = $"프리셋 작업 사본을 불러왔습니다: [{target.ProfileId}] {target.DisplayName}";
            messageType = MessageType.Info;
        }

        private void SaveAsNew()
        {
            serializedProfile.ApplyModifiedProperties();
            if (!MonsterEffectActiveAuthoringService.TryCreate(
                    profile,
                    out var created,
                    out _,
                    out var error))
            {
                message = error;
                messageType = MessageType.Error;
                return;
            }
            loadedProfile = created;
            RefreshProfiles();
            LoadProfile(created);
            message = "새 효과형 액티브 프리셋을 저장했습니다.";
            messageType = MessageType.Info;
        }

        private void UpdateLoaded()
        {
            serializedProfile.ApplyModifiedProperties();
            if (!MonsterEffectActiveAuthoringService.TryUpdate(profile, loadedProfile, out var error))
            {
                message = error;
                messageType = MessageType.Error;
                return;
            }
            var target = loadedProfile;
            RefreshProfiles();
            LoadProfile(target);
            message = "현재 효과형 액티브 프리셋을 업데이트했습니다.";
            messageType = MessageType.Info;
        }

        private void OnChanged()
        {
            serializedProfile.ApplyModifiedProperties();
            SetDirty(true);
            selectedPreviewGroup = Mathf.Clamp(
                selectedPreviewGroup,
                0,
                Mathf.Max(0, profile.Groups.Count - 1));
            Repaint();
        }

        private void SetDirty(bool value)
        {
            dirty = value;
            hasUnsavedChanges = value;
            saveChangesMessage = "효과형 액티브 작업 사본에 저장하지 않은 변경이 있습니다.";
        }

        private void DisposeWorkingCopy()
        {
            serializedProfile?.Dispose();
            serializedProfile = null;
            if (profile != null && !EditorUtility.IsPersistent(profile))
            {
                DestroyImmediate(profile);
            }
            profile = null;
        }
    }
}
