using System.Collections;
using System.Collections.Generic;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Pooling;
using ProjectMT.Shared.Stats;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public sealed partial class CombatWorld
    {
        private MonsterActiveFocusStyle activeFocusStyle;
        private bool activeFocusCommitPending;
        private float ResolveFocusLead(MonsterActiveFocusStyle style) =>
            UsesCasterAccent && MonsterActiveFocusStyles.Lead(style) > 0f
                ? MonsterActiveFocusStyles.Lead(style) : activeFocusPreset.FocusLead;

        private bool UsesCasterAccent => MonsterActiveFocusPresentationConfig.Current == null ||
                                        MonsterActiveFocusPresentationConfig.Current.CasterAccentEnabled;
        private float ActiveFocusMinimumVisibleDuration => UsesCasterAccent && activeFocusStyle == MonsterActiveFocusStyle.ClassicDim
            ? activeFocusPreset.MinimumVisibleDuration
            : MonsterActiveFocusPresentationConfig.Current != null
            ? MonsterActiveFocusPresentationConfig.Current.ResolveMinimumVisibleDuration(activeFocusPreset)
            : 0.8f;

        public bool ShouldDeferMonsterBasicAttack(UnitActor caster)
        {
            if (caster == null)
            {
                return false;
            }
            if (activeFocus != null)
            {
                return activeFocus.Caster == caster;
            }
            return activeFocusQueue.Count > 0 && activeFocusQueue[0]?.Caster == caster;
        }

        public void TrackMonsterActiveSkill(UnitActor unit)
        {
            feedbackPlayer?.TrackMonsterActiveSkill(unit);
        }

        public bool RequestMonsterActiveFocus(
            UnitActor caster,
            MonsterActiveSkill skill,
            System.Action commit,
            float commitDelay = 0.24f,
            float totalDuration = 0.72f,
            System.Action begin = null)
        {
            return RequestMonsterActiveFocus(
                caster,
                skill,
                () => caster != null ? caster.Target : null,
                () => true,
                begin,
                () =>
                {
                    commit?.Invoke();
                    return true;
                },
                null,
                null,
                commitDelay,
                totalDuration);
        }

        public bool RequestMonsterActiveFocus(
            UnitActor caster,
            MonsterActiveSkill skill,
            System.Func<UnitActor> targetResolver,
            System.Func<bool> canArm,
            System.Action begin,
            System.Func<bool> commit,
            System.Action cancel,
            System.Func<bool> commitSignal,
            float commitDelay = 0.24f,
            float totalDuration = 0.42f,
            System.Func<bool> completionSignal = null,
            System.Func<float> progressSignal = null)
        {
            if (caster == null || skill == null || commit == null || HasMonsterActiveFocusRequest(caster))
            {
                return false;
            }

            activeFocusQueue.Add(new ActiveFocusRequest(
                caster,
                skill,
                targetResolver,
                canArm,
                begin,
                commit,
                cancel,
                commitSignal,
                completionSignal,
                progressSignal,
                commitDelay,
                totalDuration,
                Time.unscaledTime,
                caster.ActiveFocusPartySlotIndex,
                nextActiveFocusSequence++));
            activeFocusQueue.Sort(ActiveFocusRequest.Compare);
            // 같은 프레임에 준비된 요청을 모두 받은 뒤 다음 CombatWorld Tick에서 안정 정렬합니다.
            return true;
        }

        private void TickMonsterActiveFocus(float unscaledDeltaTime)
        {
            if (activeFocus == null)
            {
                BeginNextMonsterActiveFocus();
                return;
            }
            if (activeFocus.Caster == null || !activeFocus.Caster.IsAlive)
            {
                CompleteMonsterActiveFocus(false, true);
                return;
            }

            var step = Mathf.Clamp(unscaledDeltaTime, 0f, 0.1f);
            if (!activeFocus.Armed)
            {
                activeFocusReadyWait += step;
                if (!TryArmMonsterActiveFocus())
                {
                    if (activeFocusReadyWait >= ActiveFocusRequest.MaxReadyWait)
                    {
                        CompleteMonsterActiveFocus(false, true);
                    }
                    return;
                }
            }

            activeFocusElapsed += step;
            var focusStart = Mathf.Max(0f, activeFocus.CommitDelay - ResolveFocusLead(activeFocusStyle));
            if (!activeFocusVisible && !activeFocusCommitted && activeFocusElapsed >= focusStart)
            {
                ShowMonsterActiveFocusPresentation();
            }

            var commitSignalReached = false;
            if (!activeFocusCommitted && activeFocus.CommitSignal != null)
            {
                try
                {
                    commitSignalReached = activeFocus.CommitSignal();
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, activeFocus.Caster);
                }
            }
            if (!activeFocusCommitted && (commitSignalReached || activeFocusElapsed >= activeFocus.CommitDelay))
            {
                activeFocusCommitPending = true;
                if (!activeFocusVisible) ShowMonsterActiveFocusPresentation();
            }
            var anticipationFinished = !UsesCasterAccent ||
                activeFocusElapsed - activeFocusSlowStartedAt >= MonsterActiveFocusStyles.Lead(activeFocusStyle);
            if (!activeFocusCommitted && activeFocusCommitPending && anticipationFinished)
            {
                if (!activeFocusVisible)
                {
                    ShowMonsterActiveFocusPresentation();
                }

                var committed = false;
                try
                {
                    committed = activeFocus.Commit?.Invoke() == true;
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, activeFocus.Caster);
                }

                if (!committed)
                {
                    CompleteMonsterActiveFocus(false, true);
                    return;
                }

                activeFocusCommitted = true;
                var isAttackFocus = activeFocus.Skill is MonsterAttackActiveSkill;
                if (isAttackFocus)
                {
                    activeFocusCameraReleaseAt = activeFocusElapsed +
                                                 activeFocusPreset.AttackCameraHoldAfterCommitDuration;
                }
                ReleaseMonsterActiveFocusPresentation(
                    false,
                    !isAttackFocus,
                    !isAttackFocus);
            }

            if (activeFocusCommitted &&
                activeFocusCameraReleaseAt >= 0f &&
                activeFocusElapsed >= activeFocusCameraReleaseAt)
            {
                ReleaseMonsterActiveFocusCamera(false);
            }

            if (activeFocusCommitted && activeFocus.CompletionSignal != null)
            {
                var skillCompleted = false;
                try
                {
                    skillCompleted = activeFocus.CompletionSignal();
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, activeFocus.Caster);
                    skillCompleted = true; // 완료 감시 오류가 Focus를 영구 점유하지 않게 한다.
                }

                var presentationFinishedAt = focusStart + ActiveFocusMinimumVisibleDuration;
                if (skillCompleted && activeFocusElapsed >= presentationFinishedAt)
                {
                    CompleteMonsterActiveFocus(false, false);
                    return;
                }
            }
            else if (activeFocusCommitted && activeFocusElapsed >= activeFocusResolvedDuration)
            {
                CompleteMonsterActiveFocus(false, false);
            }
        }

        private void BeginNextMonsterActiveFocus()
        {
            while (activeFocusQueue.Count > 0)
            {
                var next = activeFocusQueue[0];
                activeFocusQueue.RemoveAt(0);
                if (next.Caster == null || !next.Caster.IsAlive)
                {
                    next.Cancel?.Invoke();
                    continue;
                }

                activeFocus = next;
                activeFocusStyle = MonsterActiveFocusStyles.Current;
                activeFocusCommitPending = false;
                activeFocusElapsed = 0f;
                activeFocusSlowStartedAt = -1f;
                activeFocusCameraReleaseAt = -1f;
                activeFocusReadyWait = 0f;
                activeFocusCommitted = false;
                activeFocusVisible = false;
                var config = MonsterActiveFocusPresentationConfig.Current;
                activeFocusPreset = config != null
                    ? config.ResolvePreset(next.Caster.Presentation.Rarity)
                    : default;
                var focusStart = Mathf.Max(0f, next.CommitDelay - ResolveFocusLead(activeFocusStyle));
                activeFocusResolvedDuration = Mathf.Max(
                    next.Duration,
                    focusStart + Mathf.Max(
                        ActiveFocusMinimumVisibleDuration,
                        !UsesCasterAccent && next.Skill is MonsterAttackActiveSkill && next.ProgressSignal == null
                            ? activeFocusPreset.OtherUnitSlowTotalDuration
                            : 0f));
                TryArmMonsterActiveFocus();
                return;
            }
            activeFocus = null;
            activeFocusResolvedDuration = 0f;
        }

        private bool TryArmMonsterActiveFocus()
        {
            if (activeFocus == null || activeFocus.Armed)
            {
                return activeFocus?.Armed == true;
            }
            if (activeFocus.CanArm != null && !activeFocus.CanArm())
            {
                return false;
            }

            try
            {
                activeFocus.Begin?.Invoke();
                activeFocus.Armed = true;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, activeFocus.Caster);
                return false;
            }

            var focusStart = Mathf.Max(0f, activeFocus.CommitDelay - ResolveFocusLead(activeFocusStyle));
            if (focusStart <= 0f)
            {
                ShowMonsterActiveFocusPresentation();
            }
            return true;
        }

        private void ShowMonsterActiveFocusPresentation()
        {
            if (activeFocus == null || activeFocusVisible)
            {
                return;
            }

            if (activeFocusPresenter == null)
            {
                var host = feedbackPlayer != null ? feedbackPlayer.gameObject : gameObject;
                var prefab = MonsterActiveFocusPresentationConfig.Current?.PresenterPrefab;
                if (prefab != null)
                {
                    activeFocusPresenter = Instantiate(prefab, host.transform);
                    activeFocusPresenter.name = "MonsterActiveFocusHud";
                }
                else
                {
                    activeFocusPresenter = host.GetComponent<MonsterActiveFocusPresenter>();
                }
            }

            if (activeFocusPresenter == null)
            {
                return;
            }

            var target = activeFocus.TargetResolver?.Invoke();
            var camera = activeFocusCamera?.WorldCamera;
            activeFocusPresenter.Show(
                activeFocus.Caster,
                target,
                activeFocus.Skill,
                activeFocusPreset,
                camera,
                activeFocusStyle);
            if (!UsesCasterAccent)
            {
                activeFocusCamera?.BeginMonsterActiveFocus(activeFocus.Caster, target, activeFocusPreset);
            }
            activeFocusSlowStartedAt = activeFocusElapsed;
            var startSfx = MonsterActiveFocusPresentationConfig.Current?.ResolveStartSfx(
                activeFocus.Caster.Presentation.Rarity);
            if (startSfx != null)
            {
                PlayMonsterSfx(startSfx, activeFocus.Caster.transform.position);
            }
            var haloPrefab = MonsterActiveFocusPresentationConfig.Current?.ResolveHaloPrefab(
                activeFocus.Caster.Presentation.Rarity);
            if (haloPrefab != null)
            {
                activeFocusHaloInstance = RentMonsterObject(
                    haloPrefab,
                    activeFocus.Caster.transform.position,
                    activeFocus.Caster.transform.rotation,
                    activeFocus.Caster.transform);
                if (activeFocusHaloInstance != null)
                {
                    activeFocusHaloInstance.transform.localPosition = Vector3.zero;
                    MonsterBasicAttackVfxPlayback.RestartAtOffset(
                        activeFocusHaloInstance,
                        0f,
                        playbackSpeed: 1f);
                }
            }
            activeFocusVisible = true;
        }

        private void ReleaseMonsterActiveFocusPresentation(
            bool immediate,
            bool releaseTimeScale = true,
            bool releaseCamera = true)
        {
            if (releaseTimeScale)
            {
                for (var index = 0; index < units.Count; index++)
                {
                    units[index]?.SetActiveFocusTimeScale(1f);
                }
            }
            activeFocusVisible = false;
            if (activeFocusHaloInstance != null)
            {
                ReturnMonsterObject(activeFocusHaloInstance);
                activeFocusHaloInstance = null;
            }
            if (immediate)
            {
                activeFocusPresenter?.HideImmediate();
                ReleaseMonsterActiveFocusCamera(true);
            }
            else
            {
                activeFocusPresenter?.BeginRelease();
                if (releaseCamera)
                {
                    ReleaseMonsterActiveFocusCamera(false);
                }
            }
        }

        private void ReleaseMonsterActiveFocusCamera(bool immediate)
        {
            if (immediate)
            {
                activeFocusCamera?.ResetMonsterActiveFocus();
                activeFocusCameraReleaseAt = float.PositiveInfinity;
                return;
            }
            if (UsesCasterAccent || float.IsPositiveInfinity(activeFocusCameraReleaseAt))
            {
                return;
            }
            activeFocusCamera?.EndMonsterActiveFocus();
            activeFocusCameraReleaseAt = float.PositiveInfinity;
        }

        private void CompleteMonsterActiveFocus(bool clearQueue, bool cancelled = false)
        {
            if (cancelled && activeFocus != null && !activeFocusCommitted)
            {
                activeFocus.Cancel?.Invoke();
            }
            ReleaseMonsterActiveFocusPresentation(clearQueue || cancelled);
            activeFocus = null;
            activeFocusElapsed = 0f;
            activeFocusSlowStartedAt = -1f;
            activeFocusCameraReleaseAt = -1f;
            activeFocusResolvedDuration = 0f;
            activeFocusReadyWait = 0f;
            activeFocusCommitted = false;
            activeFocusPreset = default;
            if (clearQueue)
            {
                for (var index = 0; index < activeFocusQueue.Count; index++)
                {
                    activeFocusQueue[index].Cancel?.Invoke();
                }
                activeFocusQueue.Clear();
            }
            else if (!IsPaused)
            {
                BeginNextMonsterActiveFocus();
            }
        }

        public void NotifyMonsterActiveExecutionComplete(UnitActor caster)
        {
            if (caster == null || activeFocus == null || activeFocus.Caster != caster || !activeFocusCommitted)
            {
                return;
            }

            var focusStart = Mathf.Max(0f, activeFocus.CommitDelay - ResolveFocusLead(activeFocusStyle));
            var presentationFinishedAt = focusStart + ActiveFocusMinimumVisibleDuration;
            if (activeFocusElapsed < presentationFinishedAt)
            {
                return;
            }

            CompleteMonsterActiveFocus(false, false);
        }

        public void CancelMonsterActiveFocus(UnitActor caster)
        {
            if (caster == null)
            {
                return;
            }

            if (activeFocus != null && activeFocus.Caster == caster)
            {
                CompleteMonsterActiveFocus(false, !activeFocusCommitted);
            }

            for (var index = activeFocusQueue.Count - 1; index >= 0; index--)
            {
                if (activeFocusQueue[index].Caster != caster)
                {
                    continue;
                }
                activeFocusQueue[index].Cancel?.Invoke();
                activeFocusQueue.RemoveAt(index);
            }
        }

        public void SetMonsterActiveFocusCamera(IMonsterActiveFocusCamera camera)
        {
            activeFocusCamera = camera;
        }

        public void ClearMonsterActiveFocusCamera(IMonsterActiveFocusCamera camera)
        {
            if (ReferenceEquals(activeFocusCamera, camera))
            {
                activeFocusCamera = null;
            }
        }

        public float GetMonsterActiveFocusTimeScale(UnitActor source)
        {
            if (UsesCasterAccent)
            {
                return activeFocus != null && source != null && source != activeFocus.Caster &&
                       activeFocusVisible && !activeFocusCommitted &&
                       MonsterActiveFocusStyles.Slows(activeFocusStyle) &&
                       activeFocusElapsed - activeFocusSlowStartedAt < MonsterActiveFocusStyles.Lead(activeFocusStyle)
                    ? 0.10f : 1f;
            }
            if (activeFocus == null || source == null || source == activeFocus.Caster)
            {
                return 1f;
            }
            if (activeFocus.Skill is not MonsterAttackActiveSkill)
            {
                return 1f; // 효과형 액티브는 Focus 연출만 공유하고 주변 유닛은 감속하지 않는다.
            }
            if (activeFocusSlowStartedAt < 0f)
            {
                return 1f;
            }

            if (activeFocus.ProgressSignal != null)
            {
                try
                {
                    return ResolveAttackFocusProgressScale(activeFocus.ProgressSignal());
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, activeFocus.Caster);
                }
            }

            return ResolveAttackFocusSlowInScale();
        }

        private float ResolveAttackFocusProgressScale(float progress)
        {
            const float slowInEnd = 0.2f;
            const float slowHoldEnd = 0.6f;
            const float slowReleaseEnd = 0.7f;
            progress = Mathf.Clamp01(progress);
            if (progress < slowInEnd)
            {
                var ratio = Mathf.SmoothStep(0f, 1f, progress / slowInEnd);
                return Mathf.Lerp(1f, activeFocusPreset.AttackOtherUnitTimeScale, ratio);
            }
            if (progress < slowHoldEnd)
            {
                return activeFocusPreset.AttackOtherUnitTimeScale;
            }
            if (progress < slowReleaseEnd)
            {
                var ratio = Mathf.SmoothStep(
                    0f,
                    1f,
                    (progress - slowHoldEnd) / (slowReleaseEnd - slowHoldEnd));
                return Mathf.Lerp(activeFocusPreset.AttackOtherUnitTimeScale, 1f, ratio);
            }
            return 1f;
        }

        private float ResolveAttackFocusSlowInScale()
        {
            var slowElapsed = Mathf.Max(0f, activeFocusElapsed - activeFocusSlowStartedAt);
            var slowIn = activeFocusPreset.OtherUnitSlowInDuration;
            if (slowIn > 0f && slowElapsed < slowIn)
            {
                var ratio = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(slowElapsed / slowIn));
                return Mathf.Lerp(1f, activeFocusPreset.AttackOtherUnitTimeScale, ratio);
            }
            slowElapsed = Mathf.Max(0f, slowElapsed - slowIn);
            if (slowElapsed < activeFocusPreset.OtherUnitSlowHoldDuration)
            {
                return activeFocusPreset.AttackOtherUnitTimeScale;
            }
            slowElapsed -= activeFocusPreset.OtherUnitSlowHoldDuration;
            var slowOut = activeFocusPreset.OtherUnitSlowOutDuration;
            if (slowOut <= 0f || slowElapsed >= slowOut)
            {
                return 1f;
            }
            var releaseRatio = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(slowElapsed / slowOut));
            return Mathf.Lerp(activeFocusPreset.AttackOtherUnitTimeScale, 1f, releaseRatio);
        }

        private bool HasMonsterActiveFocusRequest(UnitActor caster)
        {
            if (activeFocus != null && activeFocus.Caster == caster)
            {
                return true;
            }
            for (var index = 0; index < activeFocusQueue.Count; index++)
            {
                if (activeFocusQueue[index].Caster == caster)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
