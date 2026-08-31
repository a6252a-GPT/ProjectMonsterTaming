using System;
using System.Linq;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    public sealed partial class MonsterWorkshopV2Window
    {
        partial void BuildPreviewToolbar()
        {
            if (mode == WorkshopMode.Basic) BuildBasicPreviewToolbar();
            else if (mode == WorkshopMode.Attack) BuildAttackPreviewToolbar();
            else BuildEffectPreviewToolbar();
        }

        private void BuildBasicPreviewToolbar()
        {
            basicPreview ??= new MonsterBasicAttackWorkshopPreviewV2();
            basicPreview.SetSource(basicSession.WorkingProfile, basicSession.Recipe, originDraft);
            var motions = originDraft?.Attacks;
            selectedBasicMotion = Mathf.Clamp(selectedBasicMotion, 0, Mathf.Max(0, (motions?.Count ?? 0) - 1));
            basicPreview.SetMotionIndex(selectedBasicMotion);
            if (motions != null && motions.Count > 1)
            {
                var labels = motions.Select((attack, index) => attack?.Clip == null
                    ? $"공격 {index + 1:00} · Clip 없음"
                    : $"공격 {index + 1:00} · {attack.Clip.name}").ToList();
                var motion = new PopupField<string>(labels, selectedBasicMotion);
                motion.tooltip = "프리셋에 저장되지 않는 미리보기 타이밍 기준 모션";
                motion.RegisterValueChangedCallback(evt =>
                {
                    selectedBasicMotion = labels.IndexOf(evt.newValue);
                    basicPreview.SetMotionIndex(selectedBasicMotion);
                    previewSummary.text = basicPreview.Summary;
                    previewSurface.MarkDirtyRepaint();
                });
                previewToolbar.Add(motion);
            }
            var play = new Button(() => { basicPreview.Play(); previewSurface.MarkDirtyRepaint(); }) { text = "공격 재생" };
            var stop = new Button(() => { basicPreview.Stop(); previewSurface.MarkDirtyRepaint(); }) { text = "정지" };
            var view = new Button(() => { topDownPreview = !topDownPreview; RebuildPreview(); }) { text = topDownPreview ? "사선 보기" : "탑다운 보기" };
            previewToolbar.Add(play); previewToolbar.Add(stop); previewToolbar.Add(view);
            var strength = new PopupField<MonsterImpactStrength>(EnumValues<MonsterImpactStrength>(), MonsterImpactStrength.Standard,
                value => value switch { MonsterImpactStrength.Light => "가벼움", MonsterImpactStrength.Standard => "보통", _ => "강함" },
                value => value switch { MonsterImpactStrength.Light => "가벼움", MonsterImpactStrength.Standard => "보통", _ => "강함" });
            strength.tooltip = "미리보기 타격 강도";
            strength.RegisterValueChangedCallback(evt => basicPreview.SetImpactStrength(evt.newValue));
            previewToolbar.Add(strength);
            previewStatus.text = "실제 기본공격 판정과 공통 FEEL을 확인합니다.";
            previewSummary.text = basicPreview.Summary;
        }

        private void BuildAttackPreviewToolbar()
        {
            attackPreview ??= new ProjectMT.EditorTools.MonsterMaker.MonsterActiveAttackAuthoringPreview();
            attackPreview.SetProfile(attackWorking); attackPreview.Refresh();
            var labels = attackWorking.Steps.Select((step, index) => $"#{index + 1:00} {step.DisplayName}").ToList();
            selectedAttackStep = Mathf.Clamp(selectedAttackStep, 0, Mathf.Max(0, labels.Count - 1));
            if (labels.Count > 0)
            {
                var popup = new PopupField<string>(labels, selectedAttackStep);
                popup.RegisterValueChangedCallback(evt => selectedAttackStep = labels.IndexOf(evt.newValue));
                previewToolbar.Add(popup);
            }
            previewToolbar.Add(new Button(() => { attackPreview.PlayStep(selectedAttackStep); previewSurface.MarkDirtyRepaint(); }) { text = "선택 Step 재생" });
            previewToolbar.Add(new Button(() => { attackPreview.PlayAll(); previewSurface.MarkDirtyRepaint(); }) { text = "전체 공격 재생" });
            previewToolbar.Add(new Button(() => { attackPreview.Dispose(); attackPreview = null; RebuildPreview(); }) { text = "정지" });
            var view = new Button(() => { topDownPreview = !topDownPreview; RebuildPreview(); }) { text = topDownPreview ? "사선 보기" : "탑다운 보기" };
            view.AddToClassList("preview-toolbar-last");
            previewToolbar.Add(view);
            previewStatus.text = "Step 순서·딜레이·타깃 전환·판정을 확인합니다.";
            previewSummary.text = $"Step {attackWorking.Steps.Count}개 · 예상 연출 {attackWorking.EstimateDuration():0.##}초";
        }

        private void BuildEffectPreviewToolbar()
        {
            var labels = effectWorking.Groups.Select((group, index) => $"#{index + 1:00} {group.DisplayName}").ToList();
            selectedEffectGroup = Mathf.Clamp(selectedEffectGroup, 0, Mathf.Max(0, labels.Count - 1));
            if (labels.Count > 0)
            {
                var popup = new PopupField<string>(labels, selectedEffectGroup);
                popup.RegisterValueChangedCallback(evt => { selectedEffectGroup = labels.IndexOf(evt.newValue); previewSurface.MarkDirtyRepaint(); });
                previewToolbar.Add(popup);
            }
            previewToolbar.Add(new Button(() => StartEffectPreview(false)) { text = "선택 묶음 재생" });
            previewToolbar.Add(new Button(() => StartEffectPreview(true)) { text = "전체 효과 재생" });
            var stop = new Button(() => { effectPreviewPlaying = false; previewSurface.MarkDirtyRepaint(); }) { text = "정지" };
            stop.AddToClassList("preview-toolbar-last");
            previewToolbar.Add(stop);
            previewStatus.text = "대상·HP·기력·보호막·상태 변화 계약을 확인합니다.";
            previewSummary.text = $"{EffectRoleLabel(effectWorking.Role)} · 묶음 {effectWorking.Groups.Count}개 · VFX/SFX 공간 {effectWorking.Groups.Sum(x => x.PresentationSlots.Count)}개";
        }

        private void StartEffectPreview(bool all)
        {
            if (effectWorking.Groups.Count == 0) return;
            effectPreviewAll = all; effectPreviewPlaying = true; effectPreviewStartedAt = EditorApplication.timeSinceStartup;
            previewSurface.MarkDirtyRepaint();
        }

        private void RefreshPreviewAfterAuthoringChange()
        {
            if (previewSurface == null) return;
            if (mode == WorkshopMode.Basic)
            {
                basicPreview?.SetSource(basicSession.WorkingProfile, basicSession.Recipe, originDraft);
                if (previewSummary != null && basicPreview != null) previewSummary.text = basicPreview.Summary;
            }
            else if (mode == WorkshopMode.Attack)
            {
                if (attackPreview != null)
                {
                    attackPreview.SetProfile(attackWorking);
                    attackPreview.Refresh();
                }
                if (previewSummary != null)
                    previewSummary.text = $"Step {attackWorking.Steps.Count}개 · 예상 연출 {attackWorking.EstimateDuration():0.##}초";
            }
            else if (previewSummary != null)
            {
                previewSummary.text = $"{EffectRoleLabel(effectWorking.Role)} · 묶음 {effectWorking.Groups.Count}개 · VFX/SFX 공간 {effectWorking.Groups.Sum(x => x.PresentationSlots.Count)}개";
            }
            previewSurface.MarkDirtyRepaint();
        }

        partial void DrawCurrentPreview()
        {
            var rect = GUILayoutUtility.GetRect(100f, 10000f, 100f, 10000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (mode == WorkshopMode.Basic)
            {
                basicPreview ??= new MonsterBasicAttackWorkshopPreviewV2();
                basicPreview.SetSource(basicSession.WorkingProfile, basicSession.Recipe, originDraft);
                basicPreview.Render(rect, topDownPreview);
            }
            else if (mode == WorkshopMode.Attack)
            {
                attackPreview ??= new ProjectMT.EditorTools.MonsterMaker.MonsterActiveAttackAuthoringPreview();
                attackPreview.SetProfile(attackWorking); attackPreview.Tick(); attackPreview.Render(rect, topDownPreview);
            }
            else DrawEffectPreviewV2(rect);
        }

        private void DrawEffectPreviewV2(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.045f, 0.06f, 0.08f, 1f));
            if (effectWorking.Groups.Count == 0) return;
            var duration = effectPreviewAll ? Mathf.Max(2.2f, effectWorking.Groups.Count * 1.35f) : 2.2f;
            var elapsed = (float)(EditorApplication.timeSinceStartup - effectPreviewStartedAt);
            var progress = effectPreviewPlaying ? Mathf.Clamp01(elapsed / duration) : 0f;
            if (effectPreviewPlaying && elapsed >= duration) effectPreviewPlaying = false;
            var groupIndex = Mathf.Clamp(selectedEffectGroup, 0, effectWorking.Groups.Count - 1); var local = progress;
            if (effectPreviewPlaying && effectPreviewAll)
            {
                var scaled = Mathf.Min(progress * effectWorking.Groups.Count, effectWorking.Groups.Count - 0.001f);
                groupIndex = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, effectWorking.Groups.Count - 1); local = scaled - Mathf.Floor(scaled);
            }
            var group = effectWorking.Groups[groupIndex]; var enemy = IsEnemyTarget(group.Target); var self = group.Target == MonsterSkillTargetType.Self;
            var multi = group.Target is MonsterSkillTargetType.NearbyAllies or MonsterSkillTargetType.AllAllies or MonsterSkillTargetType.TargetAreaEnemies;
            var heal = group.Effects.Any(x => x?.Type == MonsterSkillEffectType.Heal); var energyUp = group.Effects.Any(x => x?.Type == MonsterSkillEffectType.EnergyGain);
            var energyDown = group.Effects.Any(x => x?.Type == MonsterSkillEffectType.EnergyDrain); var shield = group.Effects.Any(x => x?.Type == MonsterSkillEffectType.Shield);
            var roleColor = effectWorking.Role switch { MonsterEffectActiveRole.Support => new Color(0.25f, 0.82f, 0.58f), MonsterEffectActiveRole.Guard => new Color(0.3f, 0.65f, 0.95f), _ => new Color(0.78f, 0.4f, 0.9f) };
            var center = new Vector2(rect.center.x, rect.y + rect.height * 0.45f);
            var casterAffected = self || !enemy && group.IncludeCaster;
            DrawEffectActor(center, roleColor, "시전자", effectPreviewPlaying && casterAffected && heal ? Mathf.Lerp(0.7f, 1f, local) : 1f,
                effectPreviewPlaying && casterAffected && energyUp ? Mathf.Lerp(0.5f, 0.9f, local) : 1f, effectPreviewPlaying && casterAffected && shield ? local : 0f);
            if (!self)
            {
                var y = enemy ? rect.y + rect.height * 0.2f : rect.y + rect.height * 0.72f; var count = multi ? Mathf.Clamp(group.MaxTargets, 1, 3) : 1;
                var spacing = Mathf.Min(105f, rect.width * 0.22f);
                for (var index = 0; index < count; index++)
                {
                    var offset = index - (count - 1) * 0.5f;
                    DrawEffectActor(new Vector2(rect.center.x + offset * spacing, y), enemy ? new Color(0.88f, 0.32f, 0.35f) : new Color(0.35f, 0.72f, 0.95f),
                        enemy ? $"적 {index + 1}" : $"아군 {index + 1}", effectPreviewPlaying && heal ? Mathf.Lerp(0.5f, 0.9f, local) : 0.72f,
                        effectPreviewPlaying && energyUp ? Mathf.Lerp(0.35f, 0.82f, local) : effectPreviewPlaying && energyDown ? Mathf.Lerp(0.7f, 0.28f, local) : 0.46f,
                        effectPreviewPlaying && shield ? local : 0f);
                }
            }
            if (effectPreviewPlaying)
            {
                Handles.BeginGUI(); Handles.color = new Color(roleColor.r, roleColor.g, roleColor.b, 0.7f);
                Handles.DrawWireDisc(center, Vector3.forward, 42f + Mathf.Sin(local * Mathf.PI) * 74f); Handles.EndGUI();
            }
            var effects = string.Join(" · ", group.Effects.Where(x => x != null).Select(x => SkillEffectLabel(x.Type)));
            GUI.Label(new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, 42f), $"#{groupIndex + 1:00} {group.DisplayName} · {EffectTargetLabel(group.Target)}\n{effects}", EditorStyles.centeredGreyMiniLabel);
        }

        private static void DrawEffectActor(Vector2 center, Color color, string label, float hp, float energy, float shield)
        {
            EditorGUI.DrawRect(new Rect(center.x - 18f, center.y - 18f, 36f, 36f), color);
            GUI.Label(new Rect(center.x - 48f, center.y + 20f, 96f, 18f), label, EditorStyles.centeredGreyMiniLabel);
            DrawEffectBar(new Rect(center.x - 38f, center.y + 39f, 76f, 6f), hp, new Color(0.35f, 0.9f, 0.48f));
            DrawEffectBar(new Rect(center.x - 38f, center.y + 48f, 76f, 5f), energy, new Color(0.35f, 0.7f, 1f));
            if (shield > 0f) DrawEffectBar(new Rect(center.x - 38f, center.y + 56f, 76f, 4f), shield, new Color(0.72f, 0.9f, 1f));
        }
        private static void DrawEffectBar(Rect rect, float ratio, Color color)
        { EditorGUI.DrawRect(rect, new Color(0.12f, 0.14f, 0.17f)); EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height), color); }
        private static bool IsEnemyTarget(MonsterSkillTargetType target) => target is MonsterSkillTargetType.CurrentTarget or MonsterSkillTargetType.NearestEnemy or MonsterSkillTargetType.FarthestEnemy or MonsterSkillTargetType.LowestHealthEnemy or MonsterSkillTargetType.HighestAttackEnemy or MonsterSkillTargetType.RangedEnemyFirst or MonsterSkillTargetType.TargetAreaEnemies;
    }
}
