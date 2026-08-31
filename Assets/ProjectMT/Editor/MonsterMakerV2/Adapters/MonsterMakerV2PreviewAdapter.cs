using System;
using ProjectMT.EditorTools.MonsterMaker;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    internal sealed class MonsterMakerV2PreviewAdapter : IDisposable // V1 UI 없이 공용 3D Preview 엔진만 연결
    {
        private readonly MonsterMakerPreviewStage stage = new MonsterMakerPreviewStage();

        public bool IsPlaying => stage.IsPlaying;
        public bool CanPlayActiveSkill => stage.CanPlayActiveSkill;
        public bool HasCombatTarget => stage.HasCombatTarget;
        public float NormalizedTime => stage.NormalizedTime;
        public int EnvironmentIndex => stage.EnvironmentIndex;
        public string CurrentClipName => stage.CurrentClipName;
        public string CombatStatus => stage.CombatStatus;
        public string CombatTargetLabel => stage.CombatTargetLabel;
        public float CombatTargetCurrentHealth => stage.CombatTargetCurrentHealth;
        public float CombatTargetMaximumHealth => stage.CombatTargetMaximumHealth;
        public float LastAppliedDamage => stage.LastAppliedDamage;
        public int PreviewHitCount => stage.PreviewHitCount;
        public Camera Camera => stage.Camera;

        public void SetDraft(MonsterMakerDraft draft)
        {
            stage.SetDraft(draft);
        }

        public void Draw(Rect rect)
        {
            if (rect.width <= 1f || rect.height <= 1f)
            {
                return;
            }

            EditorGUI.DrawRect(rect, new Color(0.055f, 0.065f, 0.08f, 1f));
            var texture = stage.Render(rect);
            if (texture != null)
            {
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
            }
            else
            {
                GUI.Label(rect, "Preview에 표시할 모델을 지정하세요.", EditorStyles.centeredGreyMiniLabel);
            }

            if (stage.HandleInput(rect, Event.current))
            {
                texture = stage.RenderAfterInput(rect, true);
                if (texture != null && Event.current.type == EventType.Repaint)
                {
                    GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
                }
            }
        }

        public bool Tick()
        {
            return stage.Tick();
        }

        public void PlayIdle() => stage.PlayIdle();
        public void PlayMove() => stage.PlayMove();
        public void PlayDeath() => stage.PlayDeath();
        public void PlayAttack(int index) => stage.PlayAttack(index);
        public void PlayRandomAttack() => stage.PlayRandomAttack();
        public void PlayActiveSkill() => stage.PlayActiveSkill();
        public void TogglePause() => stage.TogglePause();
        public void Restart() => stage.Restart();
        public void Scrub(float normalizedTime) => stage.Scrub(normalizedTime);
        public void SetEnvironment(int index) => stage.SetEnvironment(index);
        public void SetView(float yaw, float pitch, float distanceScale) =>
            stage.SetView(yaw, pitch, distanceScale);
        public bool ShowBasicAttackArea() => stage.ShowBasicAttackArea();
        public void ApplyDraftPositionOverrides() => stage.ApplyDraftPositionOverrides();
        public bool TryGetWorldPoint(
            MonsterMakerPreviewAnchor anchor,
            string socketPath,
            Vector3 localPosition,
            out Vector3 worldPosition) =>
            stage.TryGetWorldPoint(anchor, socketPath, localPosition, out worldPosition);

        public void Dispose()
        {
            stage.Dispose();
        }
    }
}
